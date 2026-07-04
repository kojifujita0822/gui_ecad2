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
                // 選択ツールボタンと同じ操作のため共通のActivateSelectDefault()を使う(増分vi、
                // BuiltinPlaceButton_Clickでセットした案内メッセージ("配置ツール: ...")がキャンセル
                // 後も残り続けるバグの修正=忍者実機検証で発覚、も込み)。Escapeはボタンのマウス/
                // キーボード二重発火問題を持たないグローバルショートカットのため、フォーカス復帰は
                // 常時実行する(隠密の設計集約プラン根拠3のとおり変更不要)。
                ActivateSelectDefault();
                FocusCanvas();
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
    //
    // 増分(vi, T-021設計集約プラン、隠密案(a)+(c)ハイブリッド、差し戻し1周目で改訂): 当初
    // PreviewMouseLeftButtonUp/PreviewKeyDownへ経路そのものを分離する案を試みたが、隠密の
    // コードレビューでUI Automation Invoke()の無反応・マウスキャプチャ意味論の喪失・キーリピート
    // 誤爆・Spaceキャンセル猶予の喪失、の4点が判明したため撤回(いずれもButtonBase標準のClick
    // 発火経路を迂回したことに起因)。Clickイベントは維持し、PreviewKeyDown(Enter/Space)では
    // 共通処理を呼ばず「キーボード由来フラグ」を立てるだけに留める。Click発火自体はButtonBase
    // 標準(マウスIsPressed判定・キーリピート耐性・Spaceキャンセル猶予)に委ねることで4点とも解消し、
    // Clickハンドラ側でフラグの有無によりFocusCanvas()の要否を判定する(フラグ有=キーボード起因
    // →ツールバー内ナビゲーション維持のためFocusCanvas()を呼ばない、懸念4の解消は維持)。
    private void ActivateSelectDefault()
    {
        _viewModel.SelectedCell = null;
        _viewModel.Tool = ViewModels.ToolState.SelectDefault;
        _viewModel.StatusMessage = "";
    }

    private void SelectDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        ActivateSelectDefault();
        ConsumeToolButtonFocusRestore(sender);
    }

    // a接点/b接点/コイル/端子台/ORa接点/ORb接点ボタン共通処理。Tagに図形名("a接点"等)、
    // OR系は"OR:"接頭辞を付けて区別する(MainWindow.xaml参照)。殿裁定によりボタンは「押下→ツール
    // 選択状態→キャンバスクリックで位置確定→ダイアログ」という旧T-016寄りのフローに戻す
    // (キーボードショートカットは従来通り「セル選択→キーで即ダイアログ」のまま、経路が異なる)。
    // ゴースト(プレビュー)表示は簡易版としてステータスバー表示のみに留める(視覚プレビューはT-029)。
    private void ActivateBuiltinTool(string partName, bool isOr)
    {
        var entry = _viewModel.PartPalette.Entries.FirstOrDefault(pe => pe.Category == "" && pe.Definition.Name == partName);
        if (entry is null) return;

        _viewModel.Tool = new ViewModels.ToolState(ViewModels.ToolMode.PlaceElement, PartId: entry.Definition.Id, IsOr: isOr);
        _viewModel.StatusMessage = $"配置ツール: {partName}{(isOr ? "(OR)" : "")} - キャンバスをクリックして配置位置を指定してください";
    }

    private static (string PartName, bool IsOr) ParseBuiltinTag(string tag)
    {
        bool isOr = tag.StartsWith("OR:");
        return (isOr ? tag[3..] : tag, isOr);
    }

    private void BuiltinPlaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        var (partName, isOr) = ParseBuiltinTag(tag);
        ActivateBuiltinTool(partName, isOr);
        // ツールバーボタンでツール選択後、フォーカスがボタンに残るとEnter配置(案X, T-021)が効かない
        // (キャンバスフォーカスがEnterのガード条件のため)。マウス操作(フラグ無し)ならキャンバスへ
        // フォーカスを戻し、F5等のキーボード選択と同じく「ツール選択方法によらずEnterで配置できる」
        // を成立させる(忍者実機検証で発見)。キーボード操作(フラグ有)ではツールバー内ナビゲーション
        // を維持するため戻さない(懸念4)。
        ConsumeToolButtonFocusRestore(sender);
    }

    // キーボード(Enter/Space)由来のツールバーボタン活性化を記録する「由来ボタン参照」
    // (増分vi差し戻し2周目、隠密レビュー穴1対応)。boolフラグだと「ボタンAをSpaceで押下中に
    // ボタンBをマウスクリック」のような場合にB側のClickがA由来のフラグを誤って消費してしまう
    // (attribution swap)。senderそのものを記録し、Clickハンドラ側で「記録==sender」の場合のみ
    // キーボード由来と判定することでボタン間の取り違えを構造的に防ぐ。
    private object? _toolButtonKeyboardClickSource;

    // 対象3ボタン共通のPreviewKeyDown。本体はsenderの記録のみで3ボタンとも完全に同一のため、
    // ボタンごとに分けず単一ハンドラへ集約する(隠密レビュー指摘6)。
    private void ToolButtonPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space) _toolButtonKeyboardClickSource = sender;
    }

    // 記録されたボタンと今回のClickのsenderが一致する場合のみキーボード由来と判定し、
    // キャンバスへのフォーカス復帰(FocusCanvas)をスキップする(ツールバー内ナビゲーション維持、
    // 懸念4)。一致・不一致いずれの場合も記録は必ずクリアする(取り違え防止、隠密レビュー穴1)。
    //
    // 増分(vi)差し戻し3周目(案A、殿承認2026-07-04): キーボード由来と判定した場合、
    // FocusCanvasスキップに加えてボタン自身へ明示的にフォーカスを戻す。ButtonBaseはSpace押下
    // 時に限りOnKeyUp内でReleaseMouseCapture→Keyboard.Focus(null)という内部経路を通り、Click
    // 発火の直前に一瞬フォーカスを失う(隠密の原因調査)。この掃除を怠るとフォーカスが宙に浮き
    // 「ツールバーに留まる」が実質不成立になるため、明示的に戻して担保する。Enterではフォーカスは
    // 元々ボタン上にあるため、この呼び出しは冪等・無害。
    private void ConsumeToolButtonFocusRestore(object sender)
    {
        bool isKeyboardOrigin = ReferenceEquals(_toolButtonKeyboardClickSource, sender);
        if (isKeyboardOrigin)
            (sender as UIElement)?.Focus();
        else
            FocusCanvas();
        _toolButtonKeyboardClickSource = null;
    }

    // Space押下でボタンが押下状態になった後、フォーカス喪失でClickがキャンセルされた場合の掃除
    // (増分vi差し戻し2周目、隠密レビュー穴2対応)。ButtonBase標準はこの場合Clickを発火させない
    // ため、ConsumeToolButtonFocusRestore側では記録をクリアできず、LostKeyboardFocusが唯一の
    // 掃除どころになる。対象3ボタン共通のためToolButtonPreviewKeyDownと同様に単一ハンドラへ集約。
    //
    // 増分(vi)差し戻し3周目(案A、殿承認2026-07-04): SpaceのReleaseMouseCapture→
    // Keyboard.Focus(null)によるフォーカス喪失はe.NewFocusがnullになる一時的な内部経路であり、
    // 真のキャンセルではない(隠密の原因調査)。従来はこれを本物のキャンセルと誤認して記録を
    // クリアしてしまい、直後のClickでConsumeToolButtonFocusRestoreがマウス起因と誤判定していた
    // (Space/Enter非対称バグ)。NewFocusが具体的要素(キャンバス・他ボタン等)の場合のみ真の
    // フォーカス移動とみなしクリアする。
    private void ToolButtonLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is null) return;
        if (ReferenceEquals(_toolButtonKeyboardClickSource, sender)) _toolButtonKeyboardClickSource = null;
    }

    // マウス押下時の安全側の掃除(増分vi差し戻し2周目、隠密改善案4)。8ボタン個別配線ではなく
    // Windowレベルの単一ハンドラへ集約する(動作は変更なし、配線箇所のみ整理)。新たなマウス押下が
    // window内のどこであれ発生した時点で記録をクリアし、直後のClickをマウス起因として正しく
    // 扱えるようにする。
    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _toolButtonKeyboardClickSource = null;

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
    // マウス/キーボード分離は上記2ボタンと同じ理由(増分vi、懸念4解消)。
    private void ActivateOpenPartSelection()
    {
        _viewModel.Tool = new ViewModels.ToolState(ViewModels.ToolMode.PlaceElement);
    }

    private void OpenPartSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        ActivateOpenPartSelection();
        ConsumeToolButtonFocusRestore(sender);
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