using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ecad2.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ViewModels.MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new ViewModels.MainWindowViewModel();
        DataContext = _viewModel;
        // シートナビゲーション(T-026)でCurrentSheetIndexが変わった時、および選択セル(T-017/T-027)
        // が変わった時にキャンバスを再描画する。LadderCanvasはカスタムFrameworkElementでDraw()
        // 呼び出しが描画トリガーのため、バインディングだけでは自動再描画されない。
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        RedrawCanvas();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.CurrentSheet)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedCell))
            RedrawCanvas();
    }

    private void RedrawCanvas()
        => LadderCanvasHost.Draw(_viewModel.CurrentSheet, _viewModel.PartLibrary, _viewModel.SelectedCell);

    // Ctrl+マウスホイールでキャンバスを拡大縮小する。Ctrl無しは通常のスクロールに委ねる。
    private void CanvasArea_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        _viewModel.CanvasScale += e.Delta > 0 ? 0.1 : -0.1;
        e.Handled = true;
    }

    // プロパティパネルのデバイス名編集(T-017)。ElementInstanceはINotifyPropertyChangedを実装
    // していないため、値自体はSelectedElementDeviceNameのsetterで直接書き換わるが、キャンバス上の
    // 表示(デバイス名ラベル)への反映にはDraw()の明示的な再呼び出しが要る(T-026のリネームバグと同種)。
    private void DeviceNameBox_LostFocus(object sender, RoutedEventArgs e) => RedrawCanvas();

    // シート名変更ボタン。ダイアログ表示自体はView側の責務のためcode-behindで行い、結果の反映のみ
    // ViewModelのRenameCommandへ委譲する。
    private void RenameSheetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SheetNavigation.SelectedSheet is not Ecad2.Model.Sheet sheet) return;

        var dialog = new Views.RenameDialog(sheet.Name) { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.SheetNavigation.RenameCommand.Execute(dialog.NewName);
    }

    // キャンバスクリックでセルを選択する(T-026段階4新配置フロー)。旧T-016フロー(ツール選択→
    // クリックで即配置)は廃止。ただしツールバーボタン経由(Tool.Mode==PlaceElement、殿裁定で
    // ゴースト表示は簡易版=視覚プレビューなしのステータスバー表示に留める、T-029へ切り出し)の
    // 場合はクリック位置がそのまま配置位置になるため、その場でTryPlaceElementを呼ぶ。
    // キーボードショートカット(F5等)は、SelectedCellが既にある前提でTryPlaceBuiltinから直接呼ぶ。
    private void LadderCanvasHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(LadderCanvasHost);
        _viewModel.SelectedCell = LadderCanvasHost.ToGridPos(position);
        TryPlaceActiveTool();
    }

    // アクティブな配置ツール(Tool.Mode==PlaceElement && Tool.PartId)の要素を、現在の選択セルへ配置する。
    // クリック配置(LadderCanvasHost_PreviewMouseLeftButtonUp)とEnter配置(増分i・案X、T-021)の共通経路
    // (家老采配「両経路で挙動を揃える」)。SelectedCellのnull/占有チェックはTryPlaceElement側で行う。
    private void TryPlaceActiveTool()
    {
        if (_viewModel.Tool.Mode != ViewModels.ToolMode.PlaceElement || _viewModel.Tool.PartId is not string partId)
            return;
        var entry = _viewModel.PartPalette.Entries.FirstOrDefault(e => e.Definition.Id == partId);
        if (entry is not null) TryPlaceElement(entry, _viewModel.Tool.IsOr);
    }

    // design-brief 4節の7原則の全体配線（段階8、最小実装）:
    // #2「Enter/Escの一枚岩の意味テーブル」→ Escは常に1階層キャンセルとして配置ツールを選択モードへ戻す
    // #3「パネル間ナビゲーションをTabと分離」→ Shift+Tabで左パレット/キャンバス/右パネルを循環移動する
    // (T-026段階4でF6から変更。F6はOR入力(Shift+F5=OR/Shift+F6=NOR)導入によりF5/F6系のキー体系と
    // 衝突するため、殿裁定でパネル循環はShift+Tabへ移設。単体Tabはキャンバス内用途に温存)。
    // F5=AND(a接点)/F6=NAND(b接点)/Shift+F5=OR(ORa接点)/Shift+F6=NOR(ORb接点)キー体系(殿裁定)。
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            CyclePanelFocus();
            e.Handled = true;
            return;
        }

        bool shift = Keyboard.Modifiers == ModifierKeys.Shift;
        bool noModifier = Keyboard.Modifiers == ModifierKeys.None;
        switch (e.Key)
        {
            case Key.Escape:
                _viewModel.SelectedCell = null;
                _viewModel.Tool = ViewModels.ToolState.SelectDefault;
                // BuiltinPlaceButton_Clickでセットした案内メッセージ("配置ツール: ...")が
                // キャンセル後も残り続けるバグの修正(忍者実機検証で発覚)。
                _viewModel.StatusMessage = "";
                e.Handled = true;
                break;
            case Key.F5 when noModifier:
                TryPlaceBuiltin("a接点", isOr: false);
                e.Handled = true;
                break;
            case Key.F6 when noModifier:
                TryPlaceBuiltin("b接点", isOr: false);
                e.Handled = true;
                break;
            case Key.F5 when shift:
                TryPlaceBuiltin("a接点", isOr: true);
                e.Handled = true;
                break;
            case Key.F6 when shift:
                TryPlaceBuiltin("b接点", isOr: true);
                e.Handled = true;
                break;
            case Key.F7 when noModifier:
                TryPlaceBuiltin("コイル", isOr: false);
                e.Handled = true;
                break;
            case Key.F8 when noModifier:
                TryPlaceBuiltin("端子台", isOr: false);
                e.Handled = true;
                break;
            case Key.Up or Key.Down or Key.Left or Key.Right when noModifier && IsCanvasFocused():
                // design-brief原則1「単キーショートカットはキャンバスフォーカス時のみ有効」に従い、
                // 他パネル(シートナビゲーション/機器表)にフォーカスがある間は既定のリスト操作に譲る。
                // キャンバスフォーカス時はScrollViewer(CanvasArea)の既定スクロールを上書きし、
                // SelectedCellをセル単位で移動する(T-017)。
                MoveSelectedCell(e.Key);
                e.Handled = true;
                break;
            case Key.Delete when noModifier && IsCanvasFocused():
                // 選択中の要素を削除する(T-017追加スコープ)。Escは従来通り選択解除のみで削除しない
                // (殿裁定)。矢印キーと同様キャンバスフォーカス時のみ有効。
                if (_viewModel.DeleteSelectedElement())
                    RedrawCanvas();
                e.Handled = true;
                break;
            case Key.Enter when noModifier && IsCanvasFocused()
                    && _viewModel.Tool.Mode == ViewModels.ToolMode.PlaceElement
                    && _viewModel.Tool.PartId is not null
                    && _viewModel.SelectedCell is not null:
                // 増分(i, T-021・案X): 選択セルでEnter→アクティブツールの要素を配置する(殿裁定)。
                // ツールバーボタンで種別選択済み(Tool.Mode==PlaceElement && PartId)かつセル選択済みの
                // 前提。配置本体はクリック配置と共通のTryPlaceActiveToolへ委譲する。Enterがこの4条件で
                // 成立しないときは配置以外(将来用途)へ委ねるためHandledにしない。
                TryPlaceActiveTool();
                e.Handled = true;
                break;
        }
    }

    private bool IsCanvasFocused() => IsWithin(LadderCanvasHost, Keyboard.FocusedElement as DependencyObject);

    private void MoveSelectedCell(Key key)
    {
        var current = _viewModel.SelectedCell ?? new Ecad2.Model.GridPos(0, 0);
        var grid = _viewModel.CurrentSheet.Grid;
        int row = current.Row;
        int column = current.Column;
        switch (key)
        {
            case Key.Up: row = Math.Max(0, row - 1); break;
            case Key.Down: row = Math.Min(grid.Rows - 1, row + 1); break;
            case Key.Left: column = Math.Max(0, column - 1); break;
            case Key.Right: column = Math.Min(grid.Columns, column + 1); break;
        }
        _viewModel.SelectedCell = new Ecad2.Model.GridPos(row, column);
    }

    // 選択ツール(Esc)ボタン。Window_PreviewKeyDownのEscケースと同じ操作。
    private void SelectDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedCell = null;
        _viewModel.Tool = ViewModels.ToolState.SelectDefault;
        _viewModel.StatusMessage = "";
        // 選択ツールボタンをマウスクリックした場合もフォーカスがボタンに残ると、矢印キー移動
        // (MoveSelectedCell)やDelete等のキャンバスフォーカス依存操作(IsCanvasFocused()ガード)が
        // 効かなくなる。キャンバスへフォーカスを戻す(隠密レビュー観点2、Enter配置固有でなく
        // キャンバスフォーカス依存操作全般の問題)。
        FocusCanvas();
    }

    // a接点/b接点/コイル/端子台/ORa接点/ORb接点ボタン共通ハンドラ。Tagに図形名("a接点"等)、
    // OR系は"OR:"接頭辞を付けて区別する(MainWindow.xaml参照)。殿裁定によりボタンは「押下→ツール
    // 選択状態→キャンバスクリックで位置確定→ダイアログ」という旧T-016寄りのフローに戻す
    // (キーボードショートカットは従来通り「セル選択→キーで即ダイアログ」のまま、経路が異なる)。
    // ゴースト(プレビュー)表示は簡易版としてステータスバー表示のみに留める(視覚プレビューはT-029)。
    private void BuiltinPlaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        bool isOr = tag.StartsWith("OR:");
        string partName = isOr ? tag[3..] : tag;
        var entry = _viewModel.PartPalette.Entries.FirstOrDefault(pe => pe.Category == "" && pe.Definition.Name == partName);
        if (entry is null) return;

        _viewModel.Tool = new ViewModels.ToolState(ViewModels.ToolMode.PlaceElement, PartId: entry.Definition.Id, IsOr: isOr);
        _viewModel.StatusMessage = $"配置ツール: {partName}{(isOr ? "(OR)" : "")} - キャンバスをクリックして配置位置を指定してください";

        // ツールバーボタンでツール選択後、フォーカスがボタンに残るとEnter配置(案X, T-021)が効かない
        // (キャンバスフォーカスがEnterのガード条件のため)。キャンバスへフォーカスを戻し、F5等のキー
        // ボード選択と同じく「ツール選択方法によらずEnterで配置できる」を成立させる(忍者実機検証で発見)。
        FocusCanvas();
    }

    // LadderCanvasHostへ確実にフォーカスを移す。CanvasArea(ScrollViewer)はFocusManager.IsFocusScope
    // ="True"の独立FocusScopeのため、Keyboard.Focus()単体では実フォーカスが移らないことがある
    // (T-016の罠。CyclePanelFocusと同じくFocusManager.SetFocusedElementを先に呼ぶ2段方式で回避する)。
    private void FocusCanvas()
    {
        var scope = FocusManager.GetFocusScope(LadderCanvasHost);
        FocusManager.SetFocusedElement(scope, LadderCanvasHost);
        Keyboard.Focus(LadderCanvasHost);
    }

    // 図形名(基本図形のDefinition.Name)からPartFolderEntryを検索してTryPlaceElementを呼ぶ。
    // F5/F6/Shift+F5/Shift+F6/F7/F8キー処理専用(SelectedCellが既にある前提で即ダイアログを開く)。
    private void TryPlaceBuiltin(string partName, bool isOr)
    {
        var entry = _viewModel.PartPalette.Entries.FirstOrDefault(e => e.Category == "" && e.Definition.Name == partName);
        if (entry is not null) TryPlaceElement(entry, isOr);
    }

    // 自作パーツボタン(T-026段階4-7、案B)。Tool.Mode=PlaceElementにすることで右パネル下段を
    // 部品選択表示へ切替える(パネルを開くための明示的な入口、鶏卵問題の回避)。
    private void OpenPartSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Tool = new ViewModels.ToolState(ViewModels.ToolMode.PlaceElement);
    }

    // 右パネル下段の部品選択リストの項目クリック。PreviewMouseLeftButtonDownを使う理由は
    // ListBoxItem.Selectedが同一アイテム再選択時に発火しない(WPFの仕様、T-016で確認済み)ため。
    private void PartSelectionItem_Clicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: Ecad2.Persistence.PartFolderEntry entry })
            TryPlaceElement(entry, isOr: false);
    }

    // 下部出力パネル(DRC結果)の行クリック(T-018)。DataGridRow.PreviewMouseLeftButtonDownを使う
    // 理由はPartSelectionItem_Clickedと同じ(同一行の再選択でSelectedItemバインディングが更新
    // されず、ジャンプが再実行されない事象が忍者実機検証で発見されたため)。
    private void OutputGridRow_Clicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: Ecad2.Simulation.Diagnostic diagnostic })
            _viewModel.OutputPanel.JumpToDiagnostic(diagnostic);
    }

    // 選択中セル(SelectedCell)へ要素を配置する(T-026段階4新配置フロー)。未選択・空き行チェック→
    // 浮動インライン入力ダイアログ(種別+デバイス名)→OKで確定配置。isOr=trueの場合、実際のOR接続
    // 処理(基準行判定・縦コネクタ生成)はViewModel側の責務。配置完了後は右パネルをプロパティ表示へ
    // 戻す(Tool=SelectDefault、IsPartSelectionVisible連動)。
    private void TryPlaceElement(Ecad2.Persistence.PartFolderEntry initialEntry, bool isOr)
    {
        if (_viewModel.SelectedCell is null)
        {
            _viewModel.StatusMessage = "配置するセルを先に選択してください";
            return;
        }
        if (_viewModel.IsSelectedCellOccupied())
        {
            _viewModel.StatusMessage = "選択したセルには既に要素があります";
            return;
        }

        var dialog = new Views.ElementPlacementDialog(_viewModel.PartPalette.Entries, initialEntry.Definition.Id) { Owner = this };
        // 分岐B(殿裁定=命名中Escは配置ごと原子的取消, T-021): 配置(PlaceElementAtSelectedCell)は
        // ダイアログをOK確定した場合のみ行う。Esc/キャンセルでは要素を一切作らないため、未命名の
        // 孤立要素は構造上残らない(現行の「OK後に配置」構造がそのまま原子的取消を満たす)。
        if (dialog.ShowDialog() == true && dialog.SelectedPartId is string partId)
        {
            _viewModel.PlaceElementAtSelectedCell(partId, dialog.DeviceName, isOr);
            _viewModel.StatusMessage = "";
            RedrawCanvas();
        }

        // 分岐A(殿裁定=ツール保持で連続配置, T-021): 配置後もTool/SelectedCellをリセットしない。
        // 「移動(矢印)→配置(Enter)→命名→確定→また移動…」の一気通貫(案X)を継続できるよう、
        // アクティブツールと選択セル(次の移動起点)を保持する。ツール解除はEsc(Window_PreviewKeyDownの
        // Escapeケース)に委ねる。クリック配置経路(LadderCanvasHost_PreviewMouseLeftButtonUp)も本
        // メソッド経由のため、両経路で連続配置の挙動に揃う。
        // 増分(iii, T-021): ダイアログを閉じた後、フォーカスをキャンバスへ明示復帰する(OK確定・
        // キャンセル両経路)。PoC(poc/t021-enter-placement-poc)で暗黙委譲でも戻ることは確認済みだが、
        // 環境差への保険として明示し確実化する。独立FocusScopeの罠(T-016)を避けるため素の
        // Keyboard.Focusではなく2段方式のFocusCanvasに統一(隠密レビュー観点3)。
        FocusCanvas();
    }

    private void CyclePanelFocus()
    {
        UIElement[] panels = { SheetNavList, LadderCanvasHost, DeviceTableGrid };

        // FocusManager.GetFocusedElement(this) は Window スコープの論理フォーカスしか返さない。
        // CanvasArea(ScrollViewer)は FocusManager.IsFocusScope="True" で独立したFocusScopeのため、
        // その中(LadderCanvasHost)へフォーカスが移ってもWindowスコープの論理フォーカスは追随せず、
        // 常に同じpanelへ戻ってしまう(忍者実機確認T-016で発見)。Keyboard.FocusedElementはスコープを
        // 問わない実際のキーボードフォーカス要素を返すため、これを使う。
        var current = Keyboard.FocusedElement as DependencyObject;

        int index = -1;
        for (int i = 0; i < panels.Length; i++)
        {
            if (IsWithin(panels[i], current)) { index = i; break; }
        }
        int next = (index + 1) % panels.Length;
        var target = panels[next];

        // 対象要素が独立したFocusScope内にある場合、Keyboard.Focus()だけでは実フォーカスが
        // 移らないことがあるため、まずFocusScope自体にも論理フォーカスを設定しておく。
        var scope = FocusManager.GetFocusScope(target);
        FocusManager.SetFocusedElement(scope, target);
        Keyboard.Focus(target);
    }

    private static bool IsWithin(DependencyObject root, DependencyObject? element)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, root)) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }
}