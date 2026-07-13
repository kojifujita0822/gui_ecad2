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
        // T-070隠密レビュー指摘A-1(最重要): 置換系のみCanEditDiagram(T-061確立、テストモード中の
        // 編集禁止統一ゲート)を条件に含める。検索・移動(Next/Prev)は観察の範疇として対象外(殿裁定)。
        ReplaceOneCommand = new RelayCommand(ReplaceOne,
            () => _owner.CanEditDiagram && CurrentMatch is not null && ReplaceWith.Trim().Length > 0);
        ReplaceAllCommand = new RelayCommand(ReplaceAll,
            () => _owner.CanEditDiagram && Matches.Count > 0 && ReplaceWith.Trim().Length > 0);
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
            if (!value)
            {
                Query = "";
                // T-070隠密レビュー指摘A-8: ReplaceWithもQueryと同様に閉じたらクリアする(直上コメント
                // 「再度開いたら毎回まっさらな状態から始める」契約に合わせる、残留した置換後文字列への
                // 意図しない一括置換を防ぐ)。
                ReplaceWith = "";
            }
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
        // T-070隠密レビュー指摘B-2: 記入中ドラフト(縦コネクタ・自由線・画像挿入)がある間は、検索入力の
        // たびに走る自動JumpToがSelectedCellセッター経由で無警告にドラフトを破棄してしまう
        // (Enterでの確定時は案内メッセージが出る設計と非対称)。ドラフト中はジャンプ自体を抑制して保護する
        // (Matches/CurrentIndex自体は更新するため検索結果件数表示は正しいまま)。
        if (CurrentMatch is { } m && !_owner.HasAnyDraft) JumpTo(m);
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
        // T-070隠密レビュー指摘A-5: 実際に変化するかを判定する前にRecordSnapshotを呼ぶとno-op置換
        // (現在名と同じ文字列を置換後欄に入力)でも無意味なスナップショットが積まれ、既存のRedo履歴が
        // 消去される(MoveSelectedImage等の確立規約=値が実際に変化する場合のみRecordSnapshotに合わせる)。
        if ((m.Element.DeviceName ?? "") == newName) return;
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
        // T-070隠密レビュー指摘A-5: from==to(完全同一文字列)のno-op置換はDeviceRenamer.Rename内部でも
        // 何も変えず即returnするため、その手前でRecordSnapshotを呼ぶと無意味なスナップショットになる。
        if (from.Length == 0 || to.Length == 0 || from == to) return;
        _owner.UndoManager.RecordSnapshot(_owner.Document);
        _owner.ReplaceAllDeviceName(from, to);
        RunSearch();
    }

    /// <summary>Undo/Redoによる Document 差し替え後、古い Sheet/ElementInstance 参照を保持したままの
    /// 検索結果を破棄する(T-070隠密レビューB-1対応、MainWindowViewModel.ApplyUndoRedoSnapshotから呼ぶ)。
    /// OutputPanel.ClearResultsと同型の単純クリアとし、JumpToは伴わない(Undo/RedoはSelectedCellを
    /// 巻き戻さず現状維持する殿裁定の意味論を、再検索によるJumpToで壊さないため)。</summary>
    public void ClearResults()
    {
        Matches = Array.Empty<FindMatch>();
        CurrentIndex = -1;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CurrentMatch));
    }
}
