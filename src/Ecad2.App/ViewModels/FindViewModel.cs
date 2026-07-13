using System.Windows.Input;
using Ecad2.App.Commands;
using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.App.ViewModels;

/// <summary>1件の検索一致(シート・要素のペア)。検索結果パネル(DataGrid)へ直接バインドできるよう
/// タプルではなく表示用プロパティを持つ専用recordにする(DRCパネルのDiagnostic型と同型の設計)。</summary>
public sealed record FindMatch(Sheet Sheet, ElementInstance Element)
{
    public string SheetName => Sheet.Name;
    public string DeviceName => Element.DeviceName ?? "";
    public string Location => $"P{Sheet.PageNumber} 行{Element.Pos.Row + 1}";
}

/// <summary>
/// 作図エリア上部の検索・置換バー用ViewModel(T-070)。MainWindowViewModelの子プロパティとして
/// 持たせる(design-brief 3節#1: God Class化の再発防止、OutputPanelViewModel等と同型)。
/// 検索対象は機器名(DeviceName)の完全一致のみ(殿裁定)。中核ロジックはCore層の
/// DeviceRenamer.Find/Renameが既に完備しているため、本クラスは循環Next/Prev・件数表示・
/// ジャンプ(OutputPanelViewModel.JumpToパターン踏襲)・置換1件/全置換を担う薄いラッパ。
/// </summary>
public sealed class FindViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _owner;

    public FindViewModel(MainWindowViewModel owner)
    {
        _owner = owner;
        NextCommand = new RelayCommand(Next, () => Matches.Count > 0);
        PrevCommand = new RelayCommand(Prev, () => Matches.Count > 0);
        ReplaceOneCommand = new RelayCommand(ReplaceOne, () => CurrentMatch is not null && ReplaceWith.Trim().Length > 0);
        ReplaceAllCommand = new RelayCommand(ReplaceAll, () => Matches.Count > 0 && ReplaceWith.Trim().Length > 0);
    }

    private bool _isVisible;

    /// <summary>FindBarの表示状態(Ctrl+Fでトグル)。閉じると検索状態をクリアする(GuiEcad踏襲、
    /// 再度開いたら毎回まっさらな状態から始める)。</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (!SetProperty(ref _isVisible, value)) return;
            if (!value) Query = "";
        }
    }

    private string _query = "";

    /// <summary>検索クエリ(機器名完全一致、大小無視はDeviceRenamer.Find側が担う)。随時検索
    /// (GuiEcad踏襲、TextChangedで即再検索)。</summary>
    public string Query
    {
        get => _query;
        set
        {
            if (!SetProperty(ref _query, value)) return;
            RunSearch();
        }
    }

    private string _replaceWith = "";

    /// <summary>置換後の機器名。</summary>
    public string ReplaceWith
    {
        get => _replaceWith;
        set => SetProperty(ref _replaceWith, value);
    }

    private IReadOnlyList<FindMatch> _matches = Array.Empty<FindMatch>();

    /// <summary>現在の検索結果一覧。検索結果パネル(DataGrid)へ直接バインドできるよう、
    /// タプルではなく表示用プロパティを持つ専用recordにする。</summary>
    public IReadOnlyList<FindMatch> Matches
    {
        get => _matches;
        private set => SetProperty(ref _matches, value);
    }

    private int _currentIndex = -1;

    /// <summary>現在ハイライト中の一致のインデックス(Matches内)。一致無しは-1。</summary>
    public int CurrentIndex
    {
        get => _currentIndex;
        private set
        {
            if (!SetProperty(ref _currentIndex, value)) return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CurrentMatch));
        }
    }

    /// <summary>現在ハイライト中の一致(無ければnull)。</summary>
    public FindMatch? CurrentMatch
        => CurrentIndex >= 0 && CurrentIndex < Matches.Count ? Matches[CurrentIndex] : null;

    /// <summary>件数ステータス表示(「N / M」形式、GuiEcad踏襲)。</summary>
    public string StatusText => Matches.Count == 0 ? "0 / 0" : $"{CurrentIndex + 1} / {Matches.Count}";

    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand ReplaceOneCommand { get; }
    public ICommand ReplaceAllCommand { get; }

    private void RunSearch()
    {
        string trimmed = Query.Trim();
        Matches = trimmed.Length == 0
            ? Array.Empty<FindMatch>()
            : DeviceRenamer.Find(_owner.Document, trimmed).Select(m => new FindMatch(m.Sheet, m.Element)).ToList();
        CurrentIndex = Matches.Count > 0 ? 0 : -1;
        // CurrentIndexがSetProperty同値ガードで通知されないケース(前回検索結果と偶然同じ添字)でも
        // Matches自体は入れ替わっているため、StatusText/CurrentMatchは無条件で再通知する。
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CurrentMatch));
        if (CurrentMatch is { } m) JumpTo(m);
    }

    /// <summary>次の一致へ循環移動する((Index+1)%Count方式、GuiEcad踏襲=先頭↔末尾を跨いで循環)。</summary>
    private void Next()
    {
        if (Matches.Count == 0) return;
        CurrentIndex = (CurrentIndex + 1) % Matches.Count;
        JumpTo(Matches[CurrentIndex]);
    }

    private void Prev()
    {
        if (Matches.Count == 0) return;
        CurrentIndex = (CurrentIndex - 1 + Matches.Count) % Matches.Count;
        JumpTo(Matches[CurrentIndex]);
    }

    /// <summary>一致要素のシートへ切替え、該当セルへ選択移動する(OutputPanelViewModel.JumpToと
    /// 同型の軽量パターン、GuiEcadのビューパン処理は移植不要という調査結論に基づく)。</summary>
    private void JumpTo(FindMatch match)
    {
        int sheetIndex = _owner.Document.Sheets.IndexOf(match.Sheet);
        if (sheetIndex < 0) return;
        _owner.CurrentSheetIndex = sheetIndex;
        _owner.SelectedCell = match.Element.Pos;
    }

    /// <summary>検索結果パネルの行クリックから直接呼ぶジャンプ実行(T-018の
    /// OutputPanelViewModel.JumpToDiagnosticと同型。DataGridは既に選択中の行を再クリックしても
    /// SelectedItemバインディングが更新されないWPFの既知の仕様のため、SelectedItemバインドには
    /// 頼らずRowのPreviewMouseLeftButtonDownから確実に呼ぶ設計にする)。</summary>
    public void JumpToMatch(FindMatch match)
    {
        int idx = Matches.ToList().IndexOf(match);
        if (idx < 0) return;
        CurrentIndex = idx;
        JumpTo(match);
    }

    /// <summary>現在ハイライト中の1要素だけをリネームする(殿裁定=GuiEcad同様、機器名としての
    /// 一意性は保証しない設計)。DeviceRenamer.Renameは同名一致の全要素を巻き込むため使わず、
    /// MainWindowViewModel.ReplaceOneDeviceName(単発変更+機器表整合)を呼ぶ。</summary>
    private void ReplaceOne()
    {
        if (CurrentMatch is not { } m) return;
        string newName = ReplaceWith.Trim();
        if (newName.Length == 0) return;
        _owner.UndoManager.RecordSnapshot(_owner.Document);
        _owner.ReplaceOneDeviceName(m.Element, newName);
        RunSearch();   // 置換後は一致集合が変わりうるため再検索
    }

    /// <summary>一致する全要素を一括置換する(DeviceRenamer.Renameをそのまま呼ぶだけで完結、
    /// 機器表のキー移行も含めて既存ロジックに委ねる)。</summary>
    private void ReplaceAll()
    {
        string from = Query.Trim();
        string to = ReplaceWith.Trim();
        if (from.Length == 0 || to.Length == 0) return;
        _owner.UndoManager.RecordSnapshot(_owner.Document);
        DeviceRenamer.Rename(_owner.Document, from, to);
        _owner.MarkDirty();
        _owner.DeviceTable.Refresh();
        RunSearch();
    }
}
