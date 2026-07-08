using System.Collections.ObjectModel;
using System.Windows.Input;
using Ecad2.App.Commands;
using Ecad2.Model;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 左パレット（シートナビゲーション）用の ViewModel。GuiEcadのナビツリー調査（T-026）に基づき、
/// 実質フラットリスト（1シート=1ノード、階層なし）を踏襲する。MainWindowViewModel の子プロパティ
/// として持たせる（design-brief 3節#1: God Class化の再発防止）。
/// </summary>
public sealed class SheetNavigationViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _owner;
    private readonly IDispatcherService _dispatcher;

    /// <summary>
    /// Document.Sheets のミラー。ListBox.ItemsSource は素の List&lt;Sheet&gt; を直接バインドすると
    /// 同一参照のままでは OnPropertyChanged(nameof(Sheets)) を発してもWPFが変化なしと判定し
    /// 再列挙されないため、ObservableCollection で Add/Remove を個別に通知する。
    /// </summary>
    public ObservableCollection<Sheet> Sheets { get; }

    /// <summary>選択中のシート。MainWindowViewModel.CurrentSheetIndex と同期する。</summary>
    public Sheet? SelectedSheet
    {
        get
        {
            int index = _owner.CurrentSheetIndex;
            return index >= 0 && index < Sheets.Count ? Sheets[index] : null;
        }
        set
        {
            if (value is null) return;
            int index = Sheets.IndexOf(value);
            if (index >= 0) _owner.CurrentSheetIndex = index;
            OnPropertyChanged();
        }
    }

    /// <summary>CurrentSheetIndexが外部(DRC出力パネルのジャンプ等、T-018)から変更された際、
    /// SelectedSheetのバインディング(左パネルの選択ハイライト)を同期させるために呼ぶ。</summary>
    public void RefreshSelectedSheet() => OnPropertyChanged(nameof(SelectedSheet));

    /// <summary>Document丸ごと差し替え(T-019: 新規/開く)後、SheetsをDocument.Sheetsへ再同期する。</summary>
    public void ResetSheets()
    {
        Sheets.Clear();
        foreach (var sheet in _owner.Document.Sheets) Sheets.Add(sheet);
        OnPropertyChanged(nameof(SelectedSheet));
    }

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RenameCommand { get; }

    public SheetNavigationViewModel(MainWindowViewModel owner, IDispatcherService dispatcher)
    {
        _owner = owner;
        _dispatcher = dispatcher;
        Sheets = new ObservableCollection<Sheet>(_owner.Document.Sheets);

        // T-041(殿裁定「案1」): シート追加時に名前・種別(制御回路/主回路)をダイアログで選ばせる
        // (忍者範囲外検出=UIから主回路シートを作る手段が無かった問題への対処)。ダイアログ表示自体は
        // View側の責務(AddSheetDialog、RenameCommandと同じくパラメータ渡しの流儀)。名前が空なら
        // 従来どおりの自動採番名にフォールバックする(「＋」ボタンが失敗せず必ず追加される既存挙動を保つ)。
        AddCommand = new RelayCommand(param =>
        {
            if (param is not ValueTuple<string, bool> args) return;
            var (rawName, isMainCircuit) = args;
            bool wasEmpty = _owner.Document.Sheets.Count == 0;
            int pageNumber = _owner.Document.Sheets.Count + 1;
            string name = rawName.Trim();
            if (name.Length == 0) name = $"シート{pageNumber}";
            var sheet = new Sheet
            {
                PageNumber = pageNumber,
                Name = name,
                Grid = new GridSpec { Rows = 10, Columns = 20 },
                MainCircuit = isMainCircuit,
            };
            _owner.Document.Sheets.Add(sheet);
            Sheets.Add(sheet);
            _owner.MarkDirty();
            // 殿実機検出の修正: HasProjectはReplaceDocument内でのみ明示通知されるため、
            // Sheets=0(濃紺)からここでシート追加してもHasProjectの変更がUIへ伝わらず、
            // 画面が濃紺のまま作業領域色へ切り替わらなかった。
            _owner.NotifyHasProjectChanged();
            // 隠密レビュー指摘(往復2周目回帰): Sheets 0→1遷移時はCurrentSheetIndexが0→0のまま
            // 変化しないためCurrentSheetのPropertyChangedが不発になり、RedrawCanvasが呼ばれず
            // キャンバスが空白のままになる。この経路でのみ明示発火する。
            if (wasEmpty) _owner.NotifyCurrentSheetChanged();

            // ObservableCollectionへのAdd直後は、ListBoxがまだ新しいアイテムのUI要素を生成し
            // 終えていないため、この場で同期的にSelectedSheetを設定すると視覚上の選択ハイライトが
            // 追従しない(IsSelectedはSelectionItemPattern上は正しく更新されるが表示は前の項目の
            // ままになる。T-026実機確認で発見)。UI要素生成後の次フレームへ遅延させる。
            // T-045(P-016対応): IDispatcherService経由にし、WPF Applicationへの直接依存を除去。
            _dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                () => SelectedSheet = sheet);
        });

        // 最後の1枚は削除不可（ドキュメントにシートが0枚の状態を作らない）。
        DeleteCommand = new RelayCommand(
            () =>
            {
                if (SelectedSheet is not Sheet sheet) return;
                int index = Sheets.IndexOf(sheet);
                _owner.Document.Sheets.RemoveAt(index);
                Sheets.RemoveAt(index);
                _owner.CurrentSheetIndex = Math.Min(index, Sheets.Count - 1);
                _owner.MarkDirty();
                // 家老裁定: Sheets数を変える全経路で通知発火の不変条件を揃える(AddCommandと同型の
                // 欠陥、現状のガードにより到達不能でも将来ガード変更時の再発を防ぐため揃えておく)。
                _owner.NotifyHasProjectChanged();
                OnPropertyChanged(nameof(SelectedSheet));
            },
            () => Sheets.Count > 1);

        // パラメータに新しいシート名(string)を渡して呼び出す。ダイアログ表示自体はView側の責務。
        // Sheetモデルクラスは(永続化対象のため)INotifyPropertyChangedを実装していないので、
        // sheet.Name への直接代入だけではListBoxの表示に反映されない。また
        // ObservableCollection[index]=同一参照 の Replace も、ItemContainerGeneratorが
        // 「DataContext自体は変わっていない」と判定し再評価しないため反映されなかった(T-026実機
        // 確認で発見)。RemoveAt+Insertで確実にコンテナを再構築させる。
        RenameCommand = new RelayCommand(param =>
        {
            if (SelectedSheet is not Sheet sheet || param is not string newName) return;
            string trimmed = newName.Trim();
            // 隠密レビュー指摘(往復1周目、軽微): 同名リネーム(実質無変更)ではMarkDirtyを呼ばない
            // (SelectedElementDeviceNameセッターの同値ガードと対称)。
            if (trimmed.Length == 0 || trimmed == sheet.Name) return;
            sheet.Name = trimmed;
            int index = Sheets.IndexOf(sheet);
            if (index < 0) return;

            _owner.MarkDirty();
            Sheets.RemoveAt(index);
            Sheets.Insert(index, sheet);
            // T-041増分5隠密レビュー指摘(往復3周目、所見L真因対処): 改名は同一シートに留まる
            // 操作(indexは不変)のため、SelectedSheetのsetter経由でCurrentSheetIndexへ再代入する
            // 必要は無い。CurrentSheetIndexへの代入はSelectedCellクリア等のクロスカット処理を
            // 伴うため、改名だけで記入中の縦コネクタ・自由線ドラフトが警告なく破棄される副作用
            // (所見L)を生んでいた。RemoveAt+Insertでズレたコンテナの選択ハイライトは
            // RefreshSelectedSheet()の変更通知だけで再同期できる(SelectedSheetのgetterは
            // CurrentSheetIndex経由でSheets[index]を返すため、indexが不変ならgetterの戻り値は
            // 改名後のsheet参照を正しく指す)。
            // T-045(P-016対応): IDispatcherService経由にし、WPF Applicationへの直接依存を除去。
            _dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                RefreshSelectedSheet);
        });
    }
}
