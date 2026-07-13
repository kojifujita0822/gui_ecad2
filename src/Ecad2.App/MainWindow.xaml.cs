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
using System.Windows.Threading;

namespace Ecad2.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ViewModels.MainWindowViewModel _viewModel;

    // T-041増分7: 配線プリミティブのドラッグ(本体移動/端点リサイズ)のしきい値判定用状態。
    // ドラッグの状態機械自体(対象・モード・スナップショット)はViewModel側(BeginDrag*/UpdateDrag*/
    // ConfirmDrag*/CancelDrag*)が持つが、「クリックとドラッグを区別するしきい値判定」はマウス
    // イベントの連続性に依存するView固有の関心事のためここで保持する(poc/t041-drag-poc/PoCと同じ設計)。
    private Point _connectorDragPressPositionDip;
    private bool _connectorDragStarted;
    private const double DragStartThresholdDip = 4.0;

    // T-041増分7実機確認で発覚(往復1周目): Escでドラッグをキャンセルした時点ではユーザーの指は
    // まだマウスボタンを押したままのため、ReleaseMouseCapture()するとその後実際に指を離した際の
    // MouseUpがキャプチャ外の「新規クリック」として処理され、意図せぬセル/プリミティブ選択が
    // 発生していた(離した位置がたまたま別要素の上にあると誤選択される)。キャンセル時はキャプチャを
    // 維持したまま「このマウスダウン〜アップはドラッグ関連だった」ことだけを記録し、実際のMouseUpで
    // 通常のクリック処理をスキップしてキャプチャを解放する。
    private bool _connectorDragConsumedByEscape;

    // T-041増分7横展開: 配線分断(WireBreak)ドラッグの同種の状態(VerticalConnectorと同じ理由で
    // View側に保持)。
    private Point _wireBreakDragPressPositionDip;
    private bool _wireBreakDragStarted;
    private bool _wireBreakDragConsumedByEscape;

    // T-041増分7横展開: 自由線(FreeLine)ドラッグの同種の状態(VerticalConnectorと同じ理由で
    // View側に保持)。
    private Point _freeLineDragPressPositionDip;
    private bool _freeLineDragStarted;
    private bool _freeLineDragConsumedByEscape;

    // T-041増分7横展開: 接続点(ConnectionDot)ドラッグの同種の状態(VerticalConnectorと同じ理由で
    // View側に保持)。
    private Point _connectionDotDragPressPositionDip;
    private bool _connectionDotDragStarted;
    private bool _connectionDotDragConsumedByEscape;

    // T-082: シートナビゲーション(SheetNavList)のドラッグ&ドロップ並び替え用状態。キャンバス要素の
    // ドラッグ(マウスキャプチャ方式)とは対象が異なりWPFネイティブDragDrop APIを使う(Explore調査で
    // 既存流用パターン無しと確認済み)。
    private Point _sheetDragStartPoint;
    private Ecad2.Model.Sheet? _sheetDragSource;
    private ListBoxItem? _sheetDragSourceContainer;
    private Adorner? _sheetReorderAdorner;

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
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedCell)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedConnector)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.ConnectorDraftPreview)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedWireBreak)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedFreeLine)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.FreeLineDraftPreview)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedConnectionDot))
            RedrawCanvas();

        // T-056: グリッド表示切替。LadderCanvasはカスタムFrameworkElementでDraw()呼び出しが
        // 描画トリガーのため、ShowGridの値をViewへ反映した上で明示的に再描画する。
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsGridVisible))
        {
            LadderCanvasHost.ShowGrid = _viewModel.IsGridVisible;
            RedrawCanvas();
        }

        // T-041増分7隠密レビュー所見A対応: ドラッグ中に外部要因(Delete・シート切替・ドキュメント
        // 差し替え、いずれもSelectedConnector等のsetterを経由する)でForceCancelDrag*IfAnyが発火し
        // IsDragging*がfalseへ変わった場合、View側のキャプチャ・一時フラグも追従してリセットする。
        // 通常のConfirm/CancelDrag*(MouseUp/Esc)はOnPropertyChangedを発火しないため、ここは
        // ForceCancel経由の場合のみ反応する(二重処理は起きない)。
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsDraggingConnector) && !_viewModel.IsDraggingConnector)
        {
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _connectorDragStarted = false;
            _connectorDragConsumedByEscape = false;
        }
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsDraggingWireBreak) && !_viewModel.IsDraggingWireBreak)
        {
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _wireBreakDragStarted = false;
            _wireBreakDragConsumedByEscape = false;
        }
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsDraggingFreeLine) && !_viewModel.IsDraggingFreeLine)
        {
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _freeLineDragStarted = false;
            _freeLineDragConsumedByEscape = false;
        }
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsDraggingConnectionDot) && !_viewModel.IsDraggingConnectionDot)
        {
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _connectionDotDragStarted = false;
            _connectionDotDragConsumedByEscape = false;
        }
    }

    // T-019: Document.Sheets.Count==0(新規直後の暫定挙動)の間はCurrentSheetがnullになる。
    // 前回シートの描画がキャンバスに残り続けないよう明示的にClearする(空状態=濃紺はT-020の
    // ScrollViewer背景切替が担うが、その上に前回図面が重なって見えるのを防ぐ)。
    private void RedrawCanvas()
    {
        if (_viewModel.CurrentSheet is Ecad2.Model.Sheet sheet)
            LadderCanvasHost.Draw(sheet, _viewModel.PartLibrary, _viewModel.SelectedCell, _viewModel.SelectedConnector,
                _viewModel.ConnectorDraftPreview, _viewModel.SelectedWireBreak, _viewModel.SelectedFreeLine,
                _viewModel.FreeLineDraftPreview, _viewModel.SelectedConnectionDot);
        else
            LadderCanvasHost.Clear();
    }

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
    // UpdateSourceTrigger=LostFocus(論理フォーカス)はCanvasArea等の独立FocusScope跨ぎで発火しない
    // ため(殿実機確認で発覚した回帰、診断ログで実測確認済み)、Explicit化しLostKeyboardFocus(物理
    // フォーカス喪失、スコープを跨いでも必ず発火)で明示的にUpdateSource()を呼ぶ(T-036追加修正)。
    private void DeviceNameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitDeviceNameEdit();

    // Enterキーでの即時確定(殿の期待仕様)。Tab・クリック等によるフォーカス移動はLostKeyboardFocus
    // でカバーされるため、ここではEnter押下時のみUpdateSource()を呼ぶ(フォーカスは維持したまま)。
    private void DeviceNameBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitDeviceNameEdit();
    }

    // T-049(殿裁定): デバイス名編集中、フォーカスを保持したままCtrl+S/N/O・ウィンドウクローズが
    // 実行されると、UpdateSourceTrigger=Explicit(上記コメント参照)ゆえLostKeyboardFocus/Enterの
    // いずれも発火せず、編集内容がサイレントに保存漏れ/無確認破棄されうる(P-013)。保存・破棄判定
    // (SaveDocument/ConfirmDiscardIfDirty)の入口で必ず本メソッドを呼び、確定してから判定する
    // (確認ダイアログは挟まない、殿裁定)。SelectedElementDeviceNameのsetterは同値なら早期returnする
    // ため(値変更が無い呼び出しは無害)、常時呼んでよい。
    private void CommitDeviceNameEdit()
    {
        DeviceNameBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        RedrawCanvas();
    }

    // T-066関連バグB(隠密静的レビュー指摘、往復1周目): 機器表の型式セル編集中はUpdateSourceTrigger
    // 既定(LostFocus)のCellEditEndingで確定するため、フォーカスを保持したままのCtrl+S/新規/クローズ
    // では確定されない。CommitDeviceNameEditと並べて保存・破棄判定の入口で必ず呼び、無警告破棄を防ぐ。
    private void CommitDeviceTableEdit()
    {
        DeviceTableGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        DeviceTableGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    // ヘルプ→バージョン情報。表示のみで状態変更を伴わないためViewModelへの委譲なし(T-074)。
    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.AboutDialog { Owner = this };
        dialog.ShowDialog();
    }

    // 図面→ドキュメント情報。ダイアログ表示自体はView側の責務のためcode-behindで行い、
    // 結果の反映はViewModelのApplyDocumentInfoへ委譲する(RenameSheetButton_Clickと同型、T-065)。
    private void DocumentInfoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.DocumentInfoDialog(_viewModel.Document.Info) { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.ApplyDocumentInfo(dialog.Result);
    }

    // 機器表(型式列)のセル編集確定(T-066)。Bindingが直接Device.Modelへ書き戻すため、ここでは
    // MarkDirty()のみ呼ぶ(キャンセル時はEditAction==Cancelのため呼ばない)。まだBindingが確定する
    // 前のタイミングで発火するため、編集要素(TextBox)の新値と旧値(Device.Model)を比較し、実際に
    // 変化した場合のみMarkDirty()する(隠密静的レビュー指摘C、往復1周目。同値ガード規約に合わせる)。
    private void DeviceTableGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not Ecad2.Model.Device device) return;
        if (e.EditingElement is not TextBox textBox) return;
        if (textBox.Text == (device.Model ?? "")) return;
        _viewModel.MarkDirty();
    }

    // シート名変更ボタン。ダイアログ表示自体はView側の責務のためcode-behindで行い、結果の反映のみ
    // ViewModelのRenameCommandへ委譲する。
    private void RenameSheetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SheetNavigation.SelectedSheet is not Ecad2.Model.Sheet sheet) return;

        var dialog = new Views.RenameDialog(sheet.Name) { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.SheetNavigation.RenameCommand.Execute(dialog.NewName);
    }

    // T-041(殿裁定「案1」): シート追加ボタン。RenameSheetButton_Clickと同型、ダイアログ表示は
    // View側の責務でAddCommandへ(名前, 主回路か)を渡す。
    private void AddSheetButton_Click(object sender, RoutedEventArgs e)
    {
        int pageNumber = _viewModel.Document.Sheets.Count + 1;
        var dialog = new Views.AddSheetDialog($"シート{pageNumber}") { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.SheetNavigation.AddCommand.Execute((dialog.SheetName, dialog.IsMainCircuit));
    }

    // T-055増分2: シート設定ボタン。RenameSheetButton_Clickと同型、ダイアログ表示はView側の責務で
    // UpdateSheetSettingsCommandへ(行数, 左母線名, 右母線名)を渡す。
    private void SheetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SheetNavigation.SelectedSheet is not Ecad2.Model.Sheet sheet) return;

        var dialog = new Views.SheetSettingsDialog(sheet.Grid.Rows, sheet.Bus.LeftName, sheet.Bus.RightName) { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.UpdateSheetSettingsCommand.Execute(new ViewModels.MainWindowViewModel.SheetSettings(dialog.Rows, dialog.LeftName, dialog.RightName));
    }

    private const string GcadFileFilter = "GCADファイル (*.gcad)|*.gcad";

    // 上書き保存(T-019)。ファイルダイアログ表示はView側の責務、実際の保存(GcadSerializer呼び出し)
    // はViewModelのSaveToFileへ委譲する。パス未確定(新規作成後の初回保存)は名前を付けて保存へ
    // フォールバックする(標準的な挙動)。
    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveDocument();

    private void SaveDocument()
    {
        // 隠密レビュー指摘(往復2周目、見落とし): Sheets=0(濃紺)では保存操作を無効化する
        // (家老既定案、殿帰宅後に実挙動確認)。Ctrl+S/ツールバー/メニューいずれもここを通るため
        // 単一の関門になる。IsEnabledバインディングと二重防御。
        if (!_viewModel.HasProject) return;

        // T-049: デバイス名編集中にフォーカスを保持したままの保存(Ctrl+S等)で編集が保存漏れ
        // しないよう、保存前に確定させる(CommitDeviceNameEdit参照)。
        CommitDeviceNameEdit();
        CommitDeviceTableEdit();

        if (_viewModel.CurrentFilePath is string path)
            TrySaveToFile(path);
        else
            SaveDocumentAs();
    }

    private void SaveDocumentAs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = GcadFileFilter, DefaultExt = ".gcad" };
        if (dialog.ShowDialog(this) == true)
            TrySaveToFile(dialog.FileName);
    }

    // 「名前を付けて保存」メニュー(T-063)。SaveDocument()と同じ前提チェック・確定処理を経た上で、
    // パス確定済みでも常にSaveDocumentAsへ進む点のみSaveDocument()と異なる。
    private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasProject) return;
        CommitDeviceNameEdit();
        CommitDeviceTableEdit();
        SaveDocumentAs();
    }

    // PDF出力(T-060): 回路番号採番+クロスリファレンス構築の後、プレビューダイアログを開く
    // (GuiEcadのOnMenuPreviewPdfと同型2段階UI、殿裁定2026-07-12=プレビュー機能を今回実装)。
    // 保存ダイアログ経由の実際のエクスポートはPdfPreviewDialog側(PDF出力ボタン)が担う。
    private void PdfExportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasProject) return;
        CommitDeviceNameEdit();
        CommitDeviceTableEdit();

        Ecad2.Simulation.CircuitNumberer.Number(_viewModel.Document);
        var xref = Ecad2.Simulation.CrossReferenceBuilder.Build(_viewModel.Document, _viewModel.PartLibrary);

        var dialog = new Views.PdfPreviewDialog(_viewModel.Document, _viewModel.PartLibrary, xref,
            _viewModel.Document.Settings.EnableBorder) { Owner = this };
        dialog.ShowDialog();
    }

    // I/O例外をそのままユーザーに見せず、保存エラーダイアログへ変換する(隠密調査
    // docs/ecad2-guiecad-code-survey-onmitsu.md T-024節推奨)。修正(往復2周目、忍者実機検出):
    // 開く側と同じ欠陥(ex.Messageの生の技術的文面をそのまま表示)が無いよう、一般向け日本語文面
    // ＋対象パスのみを表示する(ex変数は本文に使わないためcatch (Exception)で受ける)。
    private void TrySaveToFile(string path)
    {
        try
        {
            _viewModel.SaveToFile(path);
        }
        catch (Exception)
        {
            MessageBox.Show(this,
                $"ファイルを保存できませんでした。保存先の権限やディスクの空き容量をご確認ください。\n{path}",
                "保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 新規作成(T-019)。未保存の変更(IsDirty)があれば確認を挟む(殿裁定2026-07-05)。
    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardIfDirty()) return;
        _viewModel.NewDocument();
    }

    // 開く(T-019)。未保存の変更(IsDirty)があれば確認を挟む(殿裁定2026-07-05)。
    // I/O・スキーマ不一致例外は読み込みエラーダイアログへ変換し、Document自体は差し替えない
    // (LoadFromFileが例外を投げた場合ReplaceDocumentは未実行のため、現在のドキュメントを保つ)。
    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardIfDirty()) return;

        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = GcadFileFilter, DefaultExt = ".gcad" };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _viewModel.LoadFromFile(dialog.FileName);
        }
        catch (Exception)
        {
            // 修正(往復2周目、忍者実機検出): GcadSerializer.Deserializeが投げるJsonException等の
            // 生の技術的例外文面(英語)がex.Message経由でそのまま表示されていた欠陥を修正。
            // 一般向け日本語文面＋対象パスのみを表示する(プラン段階2の意図どおり)。
            MessageBox.Show(this,
                $"ファイルを読み込めませんでした。ファイルが壊れているか、対応していない形式の可能性があります。\n{dialog.FileName}",
                "読み込みエラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 未保存の変更(IsDirty)があれば保存/破棄/キャンセルの3択を提示する(T-019、殿裁定2026-07-05。
    // 新規/開くの文書破棄操作を単一ゲートウェイに通す、docs/ecad2-guiecad-code-survey-onmitsu.md
    // T-024節推奨に基づく)。戻り値: true=文書破棄して続行可、false=中止(呼び出し元は何もしない)。
    private bool ConfirmDiscardIfDirty()
    {
        // T-049: 新規/開く/ウィンドウクローズはいずれも本メソッドを通る単一の関門。IsDirty判定・
        // 破棄確定のいずれもデバイス名編集中の未確定値を正しく反映させるため、判定前に確定させる。
        CommitDeviceNameEdit();
        CommitDeviceTableEdit();

        if (!_viewModel.IsDirty) return true;

        var result = MessageBox.Show(this,
            "現在のドキュメントには保存されていない変更があります。保存しますか？",
            "確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        switch (result)
        {
            case MessageBoxResult.Yes:
                SaveDocument();
                // 名前を付けて保存のダイアログをキャンセルした、または保存に失敗した場合は
                // IsDirtyがtrueのまま残るため、その場合は遷移を中止する。
                return !_viewModel.IsDirty;
            case MessageBoxResult.No:
                return true;
            default:
                return false;
        }
    }

    // ウィンドウを閉じる操作(×ボタン/Alt+F4)にも未保存確認を適用する(隠密レビュー指摘、往復1周目:
    // GuiEcadのOnMenuRestart同様「文書破棄を伴う入口の一つに確認漏れ」があった)。新規/開くと同じ
    // ConfirmDiscardIfDirtyを流用し、キャンセル/保存中止時はクローズ自体を取り消す。
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscardIfDirty()) e.Cancel = true;
    }

    // キャンバスクリックでセルを選択する(T-026段階4新配置フロー)。旧T-016フロー(ツール選択→
    // クリックで即配置)は廃止。ただしツールバーボタン経由(Tool.Mode==PlaceElement、殿裁定で
    // ゴースト表示は簡易版=視覚プレビューなしのステータスバー表示に留める、T-029へ切り出し)の
    // 場合はクリック位置がそのまま配置位置になるため、その場でTryPlaceElementを呼ぶ。
    // キーボードショートカット(F5等)は、SelectedCellが既にある前提でTryPlaceBuiltinから直接呼ぶ。
    // T-041増分7: 選択中の縦コネクタの本体/端点付近を押下したら、しきい値付きドラッグを開始する。
    // 選択中でない、またはヒットしない場合は何もしない(その後のMouseLeftButtonUpが通常のクリック
    // 処理=セル選択/縦コネクタ選択切替へ素通しされる)。
    private void LadderCanvasHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(LadderCanvasHost);

        // T-080往復2周目(a)修正: WPFの既知の仕様(MouseButtonEventArgs.ClickCountはMouseUp側では
        // 常に1に固定され、MouseDown側でのみ2以上に到達する)により、往復1周目まではUp側で
        // e.ClickCount==2を判定していたため物理ダブルクリックでも条件成立しなかった(忍者実測で
        // 両クリックともClickCount=1固定・着弾位置はヒット領域内を確認、T-080往復2周目実測)。
        // 判定をDown側へ移設する。ツールモードを問わず優先判定する点は従来仕様のまま維持する
        // (GuiEcad踏襲、殿裁定=ダブルクリックトリガー)。判定条件自体はShouldOpenRungCommentEditor
        // (テスト容易性のため純粋関数として抽出、隠密テスト設計・家老裁定3)へ委ねる。
        // 隠密再レビュー要注意2対応: e.ClickCount==2を先に評価し、通常クリック(ClickCount=1)では
        // HitTestRungCommentRow(内部でDiagramRenderer.TotalRowsのO(n)走査を伴う)を呼ばない
        // 短絡評価を復す(旧Up側実装の&&短絡と同じ挙動)。
        if (e.ClickCount == 2 && _viewModel.CurrentSheet is Ecad2.Model.Sheet rcSheet
            && ShouldOpenRungCommentEditor(e.ClickCount, LadderCanvasHost.HitTestRungCommentRow(position, rcSheet)) is int rcRow)
        {
            OpenRungCommentEditor(rcRow, rcSheet);
            return;
        }

        if (_viewModel.Tool.Mode != ViewModels.ToolMode.Select) return;

        // T-041増分7隠密レビュー所見C対応: CaptureMouse()の戻り値を確認する。何らかの理由(既に
        // 別要素がキャプチャ中等)で失敗した場合、ViewModel側で開始してしまったドラッグ状態を
        // 即座に取り消す(CancelDrag*はMarkDirty()せず単に状態をクリアするだけなので安全)。
        if (_viewModel.SelectedConnector is Ecad2.Model.VerticalConnector connector
            && LadderCanvasHost.HitTestConnectorDragMode(position, connector) is (bool isEndpoint, bool isTop))
        {
            // P-039(殿裁定): 本体移動時に列位置も動かせるよう、開始時の列境界(0.5刻み)も取得する。
            var (startRow, startColumn) = LadderCanvasHost.ToRowBoundary(position);
            _viewModel.BeginDragConnector(connector, isEndpoint, isTop, startRow, startColumn);
            if (!LadderCanvasHost.CaptureMouse()) { _viewModel.CancelDragConnector(); return; }
            _connectorDragPressPositionDip = position;
            _connectorDragStarted = false;
            return;
        }

        // T-041増分7横展開: 選択中の配線分断(点系)を押下したらドラッグを開始する。HitTestWireBreak
        // (複数候補から探す通常のヒットテスト)の結果が選択中の1点と一致する場合のみ対象とする
        // (VerticalConnectorのHitTestConnectorDragModeと異なり、点は本体/端点の区別が無いため
        // 専用のドラッグ用HitTestは不要)。
        if (_viewModel.SelectedWireBreak is Ecad2.Model.WireBreak wireBreak
            && _viewModel.CurrentSheet is Ecad2.Model.Sheet sheet
            && LadderCanvasHost.HitTestWireBreak(position, sheet) == wireBreak)
        {
            var (row, boundary) = LadderCanvasHost.ToRowBoundary(position);
            _viewModel.BeginDragWireBreak(wireBreak, row, boundary);
            if (!LadderCanvasHost.CaptureMouse()) { _viewModel.CancelDragWireBreak(); return; }
            _wireBreakDragPressPositionDip = position;
            _wireBreakDragStarted = false;
            return;
        }

        // T-041増分7横展開: 選択中の自由線(mm実座標系の線分)を押下したらドラッグを開始する。
        if (_viewModel.SelectedFreeLine is Ecad2.Model.FreeLine freeLine
            && _viewModel.CurrentSheet is Ecad2.Model.Sheet flSheet
            && LadderCanvasHost.HitTestFreeLineDragMode(position, freeLine) is (bool flIsEndpoint, bool flIsStart))
        {
            var (xMm, yMm) = LadderCanvasHost.ToMmPoint(position);
            // T-041増分7隠密レビュー所見AA対応: ページ境界(mm)をViewModelは幾何を知らない設計のため
            // ここで計算して渡す(TryBeginFreeLineDraftのCellMm渡しと同じ設計原則)。
            _viewModel.BeginDragFreeLine(freeLine, flIsEndpoint, flIsStart, xMm, yMm,
                flSheet.Grid.Columns * LadderCanvasHost.CellMm, flSheet.Grid.Rows * LadderCanvasHost.CellMm);
            if (!LadderCanvasHost.CaptureMouse()) { _viewModel.CancelDragFreeLine(); return; }
            _freeLineDragPressPositionDip = position;
            _freeLineDragStarted = false;
            return;
        }

        // T-041増分7横展開: 選択中の接続点(mm実座標系の点)を押下したらドラッグを開始する。
        if (_viewModel.SelectedConnectionDot is Ecad2.Model.ConnectionDot dot
            && _viewModel.CurrentSheet is Ecad2.Model.Sheet cdSheet
            && LadderCanvasHost.HitTestConnectionDot(position, cdSheet) == dot)
        {
            var (xMm, yMm) = LadderCanvasHost.ToMmPoint(position);
            // T-041増分7隠密レビュー所見AD対応: ページ境界(mm)を渡す(BeginDragFreeLineと同じ設計)。
            _viewModel.BeginDragConnectionDot(dot, xMm, yMm,
                cdSheet.Grid.Columns * LadderCanvasHost.CellMm, cdSheet.Grid.Rows * LadderCanvasHost.CellMm);
            if (!LadderCanvasHost.CaptureMouse()) { _viewModel.CancelDragConnectionDot(); return; }
            _connectionDotDragPressPositionDip = position;
            _connectionDotDragStarted = false;
        }
    }

    // T-041増分7: ドラッグ中(キャプチャ中)のみ処理する。しきい値未満の移動はクリックとの区別のため
    // 無視する(poc/t041-drag-poc/DragCanvas.csと同じ設計)。
    private void LadderCanvasHost_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!LadderCanvasHost.IsMouseCaptured) return;
        var position = e.GetPosition(LadderCanvasHost);

        if (_viewModel.IsDraggingConnector)
        {
            if (!_connectorDragStarted)
            {
                if ((position - _connectorDragPressPositionDip).Length < DragStartThresholdDip) return;
                _connectorDragStarted = true;
            }
            // P-039(殿裁定): 本体移動時に列位置も動かせるよう、現在の列境界(0.5刻み)も渡す。
            var (currentRow, currentColumn) = LadderCanvasHost.ToRowBoundary(position);
            _viewModel.UpdateDragConnector(currentRow, currentColumn);
            RedrawCanvas();
            return;
        }

        if (_viewModel.IsDraggingWireBreak)
        {
            if (!_wireBreakDragStarted)
            {
                if ((position - _wireBreakDragPressPositionDip).Length < DragStartThresholdDip) return;
                _wireBreakDragStarted = true;
            }
            var (row, boundary) = LadderCanvasHost.ToRowBoundary(position);
            _viewModel.UpdateDragWireBreak(row, boundary);
            RedrawCanvas();
            return;
        }

        if (_viewModel.IsDraggingFreeLine)
        {
            if (!_freeLineDragStarted)
            {
                if ((position - _freeLineDragPressPositionDip).Length < DragStartThresholdDip) return;
                _freeLineDragStarted = true;
            }
            var (xMm, yMm) = LadderCanvasHost.ToMmPoint(position);
            _viewModel.UpdateDragFreeLine(xMm, yMm);
            RedrawCanvas();
            return;
        }

        if (_viewModel.IsDraggingConnectionDot)
        {
            if (!_connectionDotDragStarted)
            {
                if ((position - _connectionDotDragPressPositionDip).Length < DragStartThresholdDip) return;
                _connectionDotDragStarted = true;
            }
            var (xMm, yMm) = LadderCanvasHost.ToMmPoint(position);
            _viewModel.UpdateDragConnectionDot(xMm, yMm);
            RedrawCanvas();
        }
    }

    private void LadderCanvasHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // T-041増分7実機確認で発覚(往復1周目): Escでキャンセル済み(IsDragging*=falseだが
        // *DragConsumedByEscape=true)のマウスアップは、押していた指を離しただけの後始末。
        // キャプチャを解放するのみで、通常のクリック処理(セル選択/配線プリミティブ選択切替)は行わない
        // (これをスキップしないと、離した位置がたまたま別要素の上にあると誤選択されてしまう)。
        if (_connectorDragConsumedByEscape || _wireBreakDragConsumedByEscape || _freeLineDragConsumedByEscape
            || _connectionDotDragConsumedByEscape)
        {
            LadderCanvasHost.ReleaseMouseCapture();
            _connectorDragConsumedByEscape = false;
            _wireBreakDragConsumedByEscape = false;
            _freeLineDragConsumedByEscape = false;
            _connectionDotDragConsumedByEscape = false;
            return;
        }

        // T-041増分7: ドラッグ中だった場合はここで確定し、以降の通常クリック処理(セル選択/配線
        // プリミティブ選択切替)は行わない。ドラッグしきい値未満のまま離した場合もConfirmDrag*は
        // 値が変化していなければMarkDirty()しないため、実質クリックとして無害。
        if (_viewModel.IsDraggingConnector)
        {
            // T-041増分7隠密レビュー所見X対応: ReleaseMouseCapture()はキャプチャ保持中の要素に対し
            // LostMouseCaptureを同一コールスタック内で同期発火する。ConfirmDrag*より先に呼ぶと
            // IsDraggingConnector=trueのままLostMouseCaptureハンドラのCancelDragConnector()が
            // 割り込み実行され、直後のConfirmDragConnector()が空振りしてドラッグ結果が巻き戻る。
            // 必ずConfirmDrag*でIsDragging*=falseにしてからReleaseMouseCapture()を呼ぶ。
            _viewModel.ConfirmDragConnector();
            LadderCanvasHost.ReleaseMouseCapture();
            _connectorDragStarted = false;
            RedrawCanvas();
            return;
        }
        if (_viewModel.IsDraggingWireBreak)
        {
            _viewModel.ConfirmDragWireBreak();
            LadderCanvasHost.ReleaseMouseCapture();
            _wireBreakDragStarted = false;
            RedrawCanvas();
            return;
        }
        if (_viewModel.IsDraggingFreeLine)
        {
            _viewModel.ConfirmDragFreeLine();
            LadderCanvasHost.ReleaseMouseCapture();
            _freeLineDragStarted = false;
            RedrawCanvas();
            return;
        }
        if (_viewModel.IsDraggingConnectionDot)
        {
            _viewModel.ConfirmDragConnectionDot();
            LadderCanvasHost.ReleaseMouseCapture();
            _connectionDotDragStarted = false;
            RedrawCanvas();
            return;
        }

        var position = e.GetPosition(LadderCanvasHost);

        // T-080往復2周目(a)修正: 行コメント記入のダブルクリック判定はDown側
        // (LadderCanvasHost_PreviewMouseLeftButtonDown)へ移設した(理由は同メソッドのコメント参照、
        // Up側のe.ClickCountは常に1でありここでの判定は成立しない既知のWPF仕様のため)。
        //
        // T-080往復2周目 追加修正(課題2、忍者実機NG=docs-notes/ecad2-t080-ninja-final-verification.md
        // 観点4): 行コメント記入領域(右母線右側)はグリッドセルの概念に載らない帯のため、ここへの
        // クリックはセル選択・配線プリミティブ選択・要素配置のいずれの対象にもしない。ダブルクリックの
        // 1発目(ClickCount==1、Down側のe.ClickCount==2判定はまだ成立しない段階)がこの早期return無しに
        // 下記へ素通りすると、659行目の`SelectedCell = ToGridPos(position)`が(SelectedCellのsetterは
        // 無条件でSelectedConnector等をクリアする既存仕様のため)選択中の配線プリミティブを巻き添えで
        // クリアしてしまう。ClickCountを問わずヒット領域そのもので判定する(Down側のダブルクリック
        // トリガー判定とは独立)。判定条件自体はShouldSkipSelectionForRungCommentAreaClickへ
        // 抽出する(テスト容易性、隠密テスト設計・ShouldOpenRungCommentEditorと同型)。
        if (_viewModel.CurrentSheet is Ecad2.Model.Sheet rcGuardSheet
            && ShouldSkipSelectionForRungCommentAreaClick(LadderCanvasHost.HitTestRungCommentRow(position, rcGuardSheet)))
        {
            return;
        }

        // T-041増分1: 配線プリミティブ(縦コネクタ)の選択は、選択モード中のクリックのみで試みる
        // (配置モード中のクリックは常に要素配置目的のため対象外とする)。ヒットすればSelectedCellは
        // 使わず(セル単位の概念に載らないため)排他的に切り替える。
        // 隠密レビュー指摘: SelectedCellのsetterが常にSelectedConnectorをクリアする(上記
        // MainWindowViewModel.SelectedCell参照)ため、必ずSelectedCell=null→SelectedConnector=
        // connectorの順で呼ぶ(逆順だとSelectedCellのクリアが直後に打ち消してしまう)。
        if (_viewModel.Tool.Mode == ViewModels.ToolMode.Select && _viewModel.CurrentSheet is Ecad2.Model.Sheet sheet)
        {
            if (LadderCanvasHost.HitTestConnector(position, sheet) is Ecad2.Model.VerticalConnector connector)
            {
                _viewModel.SelectedCell = null;
                _viewModel.SelectedConnector = connector;
                return;
            }
            // T-041増分3: 配線分断(WireBreak)の選択。SelectedConnectorと同じ排他クリア順序
            // (SelectedCell=null→SelectedWireBreak=wireBreak)に倣う。
            if (LadderCanvasHost.HitTestWireBreak(position, sheet) is Ecad2.Model.WireBreak wireBreak)
            {
                _viewModel.SelectedCell = null;
                _viewModel.SelectedWireBreak = wireBreak;
                return;
            }
            // T-041増分5: 自由線・接続点(主回路シート)の選択。同じ排他クリア順序に倣う。
            if (LadderCanvasHost.HitTestFreeLine(position, sheet) is Ecad2.Model.FreeLine freeLine)
            {
                _viewModel.SelectedCell = null;
                _viewModel.SelectedFreeLine = freeLine;
                return;
            }
            if (LadderCanvasHost.HitTestConnectionDot(position, sheet) is Ecad2.Model.ConnectionDot dot)
            {
                _viewModel.SelectedCell = null;
                _viewModel.SelectedConnectionDot = dot;
                return;
            }
        }

        _viewModel.SelectedCell = LadderCanvasHost.ToGridPos(position);
        TryPlaceActiveTool();
    }

    // T-055増分3: 行の任意位置挿入・削除メニュー。ToGridPosで行番号を確定し、コードビハインドで
    // ContextMenuを都度生成する(ecad2初のContextMenu、前例なし。調査書
    // docs/ecad2-t055-increment3-precheck-onmitsu.md §1の推奨アプローチに倣う)。
    // Command+CommandParameterでバインドし、CanExecuteの反映(グレーアウト)はWPF標準機構に任せる。
    //
    // T-069(右クリックメニュー残り4系統の即着手可能部分): ヒットテスト優先順位はGuiEcad踏襲
    // (要素→縦コネクタ→行、GroupFrame系は今回対象外のためT-067完了後に別途追加する)。要素・
    // 縦コネクタは器のみパターン(既存メソッド呼出のみ)、行操作は既存のまま変更なし。
    private void LadderCanvasHost_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet) return;
        var position = e.GetPosition(LadderCanvasHost);
        var pos = LadderCanvasHost.ToGridPos(position);
        if (pos.Row < 0 || pos.Row >= sheet.Grid.Rows) return;

        var menu = new ContextMenu();

        if (sheet.Elements.FirstOrDefault(el => el.Pos == pos) is not null)
        {
            _viewModel.SelectedCell = pos;
            BuildElementContextMenuItems(menu, pos, sheet);
        }
        else if (LadderCanvasHost.HitTestConnector(position, sheet) is Ecad2.Model.VerticalConnector connector)
        {
            _viewModel.SelectedCell = null;
            _viewModel.SelectedConnector = connector;
            var deleteConnectorItem = new MenuItem { Header = "縦コネクタ削除" };
            deleteConnectorItem.Click += DeleteMenuItem_Click;
            menu.Items.Add(deleteConnectorItem);
        }
        else
        {
            menu.Items.Add(new MenuItem
            {
                Header = $"行{pos.Row + 1}の前に行を挿入",
                Command = _viewModel.InsertRowBeforeCommand,
                CommandParameter = pos.Row,
            });
            menu.Items.Add(new MenuItem
            {
                Header = "末尾に行を追加",
                Command = _viewModel.AddRowCommand,
            });
            menu.Items.Add(new MenuItem
            {
                Header = $"行{pos.Row + 1}を削除",
                Command = _viewModel.DeleteRowAtCommand,
                CommandParameter = pos.Row,
            });
        }

        LadderCanvasHost.ContextMenu = menu;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // T-069: 要素上での右クリックメニュー項目(削除/機器名変更/コメント編集)。削除はDeleteMenuItem_Click
    // (T-063、要素/コネクタ/配線プリミティブ横断の既存ハンドラ)をそのまま流用する(選択状態は要素のみに
    // 絞られているため、DeleteSelectedElementだけがtrueを返す)。機器名変更は殿裁定どおりDeviceNameBoxへの
    // 自動フォーカス(新規インライン入力ボックスは不採用)。コメント編集はT-080で完成済みのRungComment
    // 編集UI(OpenRungCommentEditor)をF2キーと同じ対象条件(主回路シート対象外・描画範囲内の行のみ)で
    // 呼ぶだけ(新規UI設計不要、家老裏取り訂正2026-07-13)。
    private void BuildElementContextMenuItems(ContextMenu menu, Ecad2.Model.GridPos pos, Ecad2.Model.Sheet sheet)
    {
        var deleteItem = new MenuItem { Header = "削除" };
        deleteItem.Click += DeleteMenuItem_Click;
        menu.Items.Add(deleteItem);

        var renameItem = new MenuItem { Header = "機器名変更" };
        renameItem.Click += (_, _) =>
        {
            DeviceNameBox.Focus();
            DeviceNameBox.SelectAll();
        };
        menu.Items.Add(renameItem);

        if (!sheet.MainCircuit && pos.Row >= 0 && pos.Row < Ecad2.Rendering.DiagramRenderer.TotalRows(sheet))
        {
            var commentItem = new MenuItem { Header = "コメント編集" };
            commentItem.Click += (_, _) => OpenRungCommentEditor(pos.Row, sheet);
            menu.Items.Add(commentItem);
        }
    }

    // T-041増分7隠密レビュー所見C対応: Alt+Tab等の外的要因でマウスキャプチャが失われた場合、
    // 進行中のドラッグを安全にキャンセルする(掴んだ位置への復元、MarkDirty()しない)。
    // ReleaseMouseCapture()を能動的に呼んだ直後の正常フロー(MouseUp/Escの各分岐)でも本イベントは
    // 発火するが、その時点では既にConfirm/CancelDrag*でIsDragging*=falseになっているため
    // 各ガードが素通しし二重処理にはならない。
    private void LadderCanvasHost_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_viewModel.IsDraggingConnector)
        {
            _viewModel.CancelDragConnector();
            _connectorDragStarted = false;
            RedrawCanvas();
        }
        if (_viewModel.IsDraggingWireBreak)
        {
            _viewModel.CancelDragWireBreak();
            _wireBreakDragStarted = false;
            RedrawCanvas();
        }
        if (_viewModel.IsDraggingFreeLine)
        {
            _viewModel.CancelDragFreeLine();
            _freeLineDragStarted = false;
            RedrawCanvas();
        }
        if (_viewModel.IsDraggingConnectionDot)
        {
            _viewModel.CancelDragConnectionDot();
            _connectionDotDragStarted = false;
            RedrawCanvas();
        }
        _connectorDragConsumedByEscape = false;
        _wireBreakDragConsumedByEscape = false;
        _freeLineDragConsumedByEscape = false;
        _connectionDotDragConsumedByEscape = false;
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
        // T-033増分1(殿裁定2026-07-07、PoC所見・隠密レビュー往復1周目指摘): 非モーダル化により
        // 本ハンドラがバー表示中も発火してしまう(モーダルWindow時代は別Windowのため到達しなかった)。
        // バー表示中はF5〜F8等の配置ショートカット・Escapeの多層キャンセル等、本メソッドの
        // グローバルショートカットを一切無効化し、現行モーダル同等の使用感(誤押しによる意図せぬ
        // 確定・取消を避ける安全側)を保つ。Esc/Enterによるバー自身の確定・取消はIsCancel/IsDefault
        // (PlacementOkButton_Click/PlacementCancelButton_Click)が別経路で処理するため、本ガードの
        // 影響を受けない。マウス経路6系統(隠密レビュー指摘)はMainWindow.xamlのMenu/ToolBarTray/
        // メイン作業域Grid/OutputPanelAreaのIsEnabledバインドで別途一括無効化しており(単一の真実源
        // =IsPlacementBarVisible)、キーボード・マウス両経路が同じフラグから連動する。
        if (_viewModel.IsPlacementBarVisible) return;

        // T-080: 行コメントエディタ編集中はグローバルショートカット(F5等)を無効化する
        // (ElementPlacementBar表示中と同じ設計方針)。Enter/Tab/EscapeはRungCommentBox自身の
        // PreviewKeyDown(RungCommentBox_PreviewKeyDown)が処理するため、本ハンドラより先に
        // 到達させる必要がある(本ハンドラはTunnelingでRungCommentBoxより先に発火するため、
        // ここで早期returnしないとEscape等が意図せず消費されてしまう)。
        if (_rungCommentEditingRow is not null) return;

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
                // T-041増分7: ドラッグ中(マウスキャプチャ中)のEscは掴んだ位置への復元のみを行う
                // 独立した最優先の層とする(記入中モードと同じ「1回のEscは1層だけ」の原則、下記の
                // 層2/3/4処理へは落とさない)。poc/t041-drag-poc/DragCanvas.csと同じ設計。
                if (_viewModel.IsDraggingConnector)
                {
                    // キャプチャは解放しない(ユーザーの指はまだボタンを押したままの想定)。
                    // _connectorDragConsumedByEscapeで印を付け、実際のMouseUpで無害化してから解放する。
                    _viewModel.CancelDragConnector();
                    _connectorDragStarted = false;
                    _connectorDragConsumedByEscape = true;
                    RedrawCanvas();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                if (_viewModel.IsDraggingWireBreak)
                {
                    _viewModel.CancelDragWireBreak();
                    _wireBreakDragStarted = false;
                    _wireBreakDragConsumedByEscape = true;
                    RedrawCanvas();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                if (_viewModel.IsDraggingFreeLine)
                {
                    _viewModel.CancelDragFreeLine();
                    _freeLineDragStarted = false;
                    _freeLineDragConsumedByEscape = true;
                    RedrawCanvas();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                if (_viewModel.IsDraggingConnectionDot)
                {
                    _viewModel.CancelDragConnectionDot();
                    _connectionDotDragStarted = false;
                    _connectionDotDragConsumedByEscape = true;
                    RedrawCanvas();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                // T-036追加修正(殿裁定=Esc入力破棄、隠密レビュー指摘=Esc層消費): デバイス名編集中の
                // Escは表示復元(UpdateTarget())+フォーカス復帰のみの独立した1層として消費し、
                // 下記の層2/3/4処理(選択解除等)へは落とさない(T-021「1回のEscは1層だけ」の原則に
                // 整合。選択・プロパティパネルは保持され、次のEscで従来どおり選択解除が働く)。
                // 本ハンドラはPreviewKeyDown(Tunneling)でDeviceNameBox自身のPreviewKeyDownより先に
                // 発火し、FocusCanvas()がLostKeyboardFocus経由でUpdateSource()を誘発してしまうため、
                // それより前に表示を戻す必要がある(DeviceNameBox側にEsc処理を置いても手遅れ)。
                if (Keyboard.FocusedElement == DeviceNameBox)
                {
                    DeviceNameBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                // 増分(iv, T-021): Esc多段階4層(論点3、殿裁定)。1回のEscで内側から1層だけ戻す。
                // 層1(配置バー内テキスト編集中の編集キャンセル)は、本ハンドラ冒頭のバー表示中
                // 早期リターン(T-033増分1)により、バー表示中は本switch自体に到達しないため対象外。
                // 配置バーのIsCancel="True"ボタン(WPF標準規約)で既に実現済み(層1は本ケースの範囲外)。
                // StatusMessageのクリアは層に依らず全Esc押下で一度だけ行う(層2/層3内に重複させない)。
                // F5〜F8のTryPlaceBuiltinはTool.Mode=PlaceElementを経ずにエラーメッセージ
                // ("配置するセルを先に選択してください"等)を設定しうるため、層2/層3のどちらの条件も
                // 満たさず層4へ落ちてもメッセージが残らぬよう、条件分岐の外でクリアする(隠密レビュー指摘)。
                _viewModel.StatusMessage = "";
                if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceElement)
                {
                    // 層2: 配置モード中 → 選択モードへ戻す。SelectedCellは保持し、続けて別ツールで
                    // 同じセルへ配置し直せるようにする。
                    _viewModel.Tool = ViewModels.ToolState.SelectDefault;
                }
                else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceConnector)
                {
                    // 層2'(T-041増分2): 縦コネクタ記入中 → 取消して選択モードへ戻す。何も生成しない。
                    _viewModel.CancelConnectorDraft();
                }
                else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceLine)
                {
                    // 層2''(T-041増分5): 自由線記入中 → 取消して選択モードへ戻す。何も生成しない。
                    _viewModel.CancelFreeLineDraft();
                }
                else if (_viewModel.SelectedCell is not null || _viewModel.SelectedConnector is not null
                    || _viewModel.SelectedWireBreak is not null || _viewModel.SelectedFreeLine is not null
                    || _viewModel.SelectedConnectionDot is not null)
                {
                    // 層3: 要素選択中・配線プリミティブ選択中(T-041増分1/3/5) → 選択解除のみ。
                    // SelectedCellのsetterが値変化の有無に関わらず全ての配線プリミティブ選択も常に
                    // クリアするため(隠密レビュー指摘、MainWindowViewModel.SelectedCell参照)、
                    // 1行で足りる。
                    _viewModel.SelectedCell = null;
                }
                // 層4: 何もなし → 無視(キャンバスフォーカス維持のみ)。
                // Escapeはボタンのマウス/キーボード二重発火問題を持たないグローバルショートカットの
                // ため、フォーカス復帰は全層で常時実行する(隠密の設計集約プラン根拠3のとおり変更不要)。
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
            case Key.F9 when noModifier:
                // T-041増分5: F9で自由線(横線)手動記入モードを開始する(主回路シート限定、
                // `ecad2-t041-key-flow-proposal-samurai.md`4節・殿裁定「案A」)。制御回路シートでは
                // 当面未使用(原案どおり、自動横配線があるため対応する手動記入は無い)。
                TryBeginFreeLineDraft(horizontal: true);
                e.Handled = true;
                break;
            case Key.F9 when shift:
                // T-041増分2/5: sF9はシート種別で対象が切替わる(殿裁定「シート種別で自動切替」)。
                // 制御回路シート→縦コネクタ手動記入、主回路シート→自由線(縦線)手動記入。
                if (_viewModel.CurrentSheet is Ecad2.Model.Sheet sf9Sheet && sf9Sheet.MainCircuit)
                    TryBeginFreeLineDraft(horizontal: false);
                else
                    TryBeginConnectorDraft();
                e.Handled = true;
                break;
            case Key.System when noModifier && e.SystemKey == Key.F10:
                // T-041増分3/5: F10もシート種別で対象が切替わる(制御回路→配線分断、主回路→接続点)。
                // `ecad2-t041-key-flow-proposal-samurai.md`4節・殿裁定「案A・F10」。
                // 忍者実機発見(F10無反応・メインメニューへフォーカス移動)への対処: F10はAlt併用
                // 有無に関わらずWin32のWM_SYSKEYDOWN(システムキー、メニューアクセラレータ由来)
                // として扱われるWPF既知の仕様があり、この場合e.Keyには`Key.System`が入り実キーは
                // `e.SystemKey`側に入る(F5〜F9は通常キーのためe.Keyでそのまま拾えるが、F10のみ
                // この特別扱いを受ける)。case Key.F10単体では到達せず、WPF既定のメニューフォーカス
                // 処理へ素通しされていたのが無反応の原因。
                if (_viewModel.CurrentSheet is Ecad2.Model.Sheet f10Sheet && f10Sheet.MainCircuit)
                    TryPlaceConnectionDot();
                else
                    TryPlaceWireBreak();
                e.Handled = true;
                break;
            case Key.System when Keyboard.Modifiers == ModifierKeys.Alt
                    && (e.SystemKey == Key.Up || e.SystemKey == Key.Down)
                    && IsSheetNavFocused():
                // T-082(殿裁定「Alt+上下」): シートナビゲーションパネル(SheetNavList)にフォーカスが
                // ある間、選択中のシートを上下へ並び替える。F10と同型(上記コメント参照)でAlt併用キーは
                // WM_SYSKEYDOWN経由となりe.KeyがKey.Systemになりe.SystemKeyに実キーが入るWPF既知仕様
                // のため、この分岐が必要(実機検証要、忍者確認予定)。
                MoveCurrentSheet(e.SystemKey == Key.Up ? -1 : 1);
                e.Handled = true;
                break;
            case Key.Up or Key.Down or Key.Left or Key.Right when noModifier && IsCanvasFocused():
                // design-brief原則1「単キーショートカットはキャンバスフォーカス時のみ有効」に従い、
                // 他パネル(シートナビゲーション/機器表)にフォーカスがある間は既定のリスト操作に譲る。
                // キャンバスフォーカス時はScrollViewer(CanvasArea)の既定スクロールを上書きし、
                // SelectedCellをセル単位で移動する(T-017)。T-041増分2/5: 縦コネクタ/自由線記入中は
                // 矢印キーをSelectedCell移動ではなく記入中プレビューの範囲/位置の調整に転用する。
                if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceConnector)
                    AdjustConnectorDraft(e.Key, cellCenterStep: false);
                else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceLine)
                    AdjustFreeLineDraft(e.Key);
                else if (_viewModel.SelectedConnector is not null)
                    // T-041増分7: 選択中の縦コネクタを平行移動する(キーボード等価操作、案X)。
                    MoveSelectedConnectorByKey(e.Key);
                else if (_viewModel.SelectedWireBreak is not null)
                    // T-041増分7横展開: 選択中の配線分断を平行移動する(点系、本体移動のみ)。
                    MoveSelectedWireBreakByKey(e.Key);
                else if (_viewModel.SelectedFreeLine is not null)
                    // T-041増分7横展開: 選択中の自由線を平行移動する(mm実座標系)。
                    MoveSelectedFreeLineByKey(e.Key);
                else if (_viewModel.SelectedConnectionDot is not null)
                    // T-041増分7横展開: 選択中の接続点を平行移動する(点系・mm実座標系、本体移動のみ)。
                    MoveSelectedConnectionDotByKey(e.Key);
                else
                    MoveSelectedCell(e.Key);
                e.Handled = true;
                break;
            case Key.Left or Key.Right when shift && IsCanvasFocused()
                    && _viewModel.Tool.Mode == ViewModels.ToolMode.PlaceConnector:
                // T-041増分2: Shift+Left/Rightはセル中央(X.5)刻みでの列位置調整(原案3節)。
                AdjustConnectorDraft(e.Key, cellCenterStep: true);
                e.Handled = true;
                break;
            case Key.Up or Key.Down when shift && IsCanvasFocused() && _viewModel.SelectedConnector is not null:
                // T-041増分7(殿裁定P-033=案2): Tabで選んだ操作対象端点(始点/終点)をUp=-1/Down=+1で
                // 伸縮する。VerticalConnectorは常に縦線のためLeft/Rightの端点伸縮は意味を持たず未対応。
                if (_viewModel.ResizeSelectedConnectorEndpoint(e.Key == Key.Up ? -1 : 1))
                    RedrawCanvas();
                e.Handled = true;
                break;
            case Key.Up or Key.Down or Key.Left or Key.Right when shift && IsCanvasFocused()
                    && _viewModel.SelectedFreeLine is not null:
                // T-041増分7横展開: Tabで選んだ操作対象端点(始点/終点)を伸縮する。自由線は水平線
                // (Left/Rightのみ意味を持つ)・垂直線(Up/Downのみ)のいずれかのため、線の向きに沿わない
                // キーは呼び出し元(ResizeSelectedFreeLineByKey)が無視する(AdjustFreeLineDraftと同型)。
                ResizeSelectedFreeLineByKey(e.Key);
                e.Handled = true;
                break;
            case Key.Tab when noModifier && IsCanvasFocused() && _viewModel.HasSelectedLinePrimitive:
                // T-041増分7(殿裁定P-033=案2): 操作対象端点(始点/終点)をトグルする。表示は
                // ステータスバーのSelectedEndpointDisplayバインディングで自動反映される。
                _viewModel.ToggleSelectedEndpoint();
                e.Handled = true;
                break;
            case Key.F2 when noModifier && IsCanvasFocused()
                    && _viewModel.SelectedCell is { } commentCell
                    && _viewModel.CurrentSheet is Ecad2.Model.Sheet commentSheet:
                // T-080往復1周目・追加I(殿裁定): 選択セルの行の行コメントエディタをキーボードで
                // 開く等価経路(キーボードファースト原則。GuiEcad原本にも「コメント編集」キー割当が
                // 存在した、T-081調査)。対象条件はダブルクリック経路(HitTestRungCommentRow)と
                // 揃える: 主回路シートは対象外(指摘G)・行は描画範囲内のみ。矢印キー・Deleteと同じく
                // キャンバスフォーカス時のみ有効(選択セルに対する操作のため)。
                if (!commentSheet.MainCircuit && commentCell.Row >= 0
                    && commentCell.Row < Ecad2.Rendering.DiagramRenderer.TotalRows(commentSheet))
                    OpenRungCommentEditor(commentCell.Row, commentSheet);
                e.Handled = true;
                break;
            case Key.Delete when noModifier && IsCanvasFocused():
                // 選択中の要素を削除する(T-017追加スコープ)。Escは従来通り選択解除のみで削除しない
                // (殿裁定)。矢印キーと同様キャンバスフォーカス時のみ有効。
                // T-041増分1/3/5(案A): 選択中の要素が無く配線プリミティブ(縦コネクタ・配線分断・
                // 自由線・接続点)が選択中であればそれを削除する(既存の部品削除と同じDeleteキーへ
                // 統合)。クリック時点でいずれも排他的にしか選択されない設計だが、優先順位を明記しておく。
                if (_viewModel.DeleteSelectedElement() || _viewModel.DeleteSelectedConnector()
                    || _viewModel.DeleteSelectedWireBreak() || _viewModel.DeleteSelectedFreeLine()
                    || _viewModel.DeleteSelectedConnectionDot())
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
            case Key.Enter when noModifier && IsCanvasFocused()
                    && _viewModel.Tool.Mode == ViewModels.ToolMode.PlaceConnector:
                // T-041増分2: 記入中の縦コネクタを確定する。範囲が0(まだ上下キーで広げていない)場合は
                // 確定せず案内のみ出す(原案3節)。
                if (_viewModel.ConfirmConnectorDraft())
                    RedrawCanvas();
                else
                    _viewModel.StatusMessage = "上下キーで範囲を広げてから確定してください";
                e.Handled = true;
                break;
            case Key.Enter when noModifier && IsCanvasFocused()
                    && _viewModel.Tool.Mode == ViewModels.ToolMode.PlaceLine:
                // T-041増分5: 記入中の自由線を確定する。長さが0(まだ矢印キーで伸ばしていない)場合は
                // 確定せず案内のみ出す(縦コネクタと同型)。
                if (_viewModel.ConfirmFreeLineDraft())
                    RedrawCanvas();
                else
                    _viewModel.StatusMessage = "矢印キーで長さを広げてから確定してください";
                e.Handled = true;
                break;
            case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                // T-019: メニュー/ツールバーのInputGestureText表示(Ctrl+S)と整合させる。
                SaveDocument();
                e.Handled = true;
                break;
            case Key.O when Keyboard.Modifiers == ModifierKeys.Control:
                OpenButton_Click(sender, e);
                e.Handled = true;
                break;
            case Key.P when Keyboard.Modifiers == ModifierKeys.Control:
                // T-060隠密静的レビュー指摘B対応: メニュー/ツールバーのInputGestureText表示
                // (Ctrl+P)と整合させる(キーボードファースト方針、CLAUDE.md)。
                PdfExportMenuItem_Click(sender, e);
                e.Handled = true;
                break;
            case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                NewButton_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                // T-051: メニュー/ツールバーのInputGestureText表示(Ctrl+Z)と整合させる。
                // T-051バグ修正#3(隠密レビューCONFIRMED重大): 既存Ctrl+S/O/Nと同型のガード。
                // DeviceNameBox編集中の未確定入力を確定してからUndoを実行しないと、Undoで
                // Documentが差し替わった後にフォーカスが外れた際、別の要素へ誤書き込みされうる。
                CommitDeviceNameEdit();
                _viewModel.UndoCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Y when Keyboard.Modifiers == ModifierKeys.Control:
                CommitDeviceNameEdit();
                _viewModel.RedoCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.G when Keyboard.Modifiers == ModifierKeys.Control:
                // T-056: メニューのInputGestureText表示(Ctrl+G)と整合させる。
                _viewModel.IsGridVisible = !_viewModel.IsGridVisible;
                e.Handled = true;
                break;
            case Key.Up when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                // T-055増分1: 末尾行を1行追加する(ツールバー「行を追加」ボタンと同一コマンド)。
                _viewModel.AddRowCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                // T-055増分1: 末尾行を1行削除する(ツールバー「行を削除」ボタンと同一コマンド)。
                _viewModel.DeleteRowCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // 「削除」メニュー(T-063)。Key.Delete case(上記883行付近)と同じ削除ロジックをそのまま流用する。
    // IsCanvasFocused()判定はキー入力がキャンバス宛かを見るためのものでメニュークリックには不要、
    // 選択が無ければ各Delete*系は何もせずfalseを返すため無効化バインディングも付けていない。
    // メニュークリックはフォーカス非依存で発火するため、Key.Delete caseと異なりDeviceNameBox編集中
    // にも到達しうる。未確定入力を黙って破棄しないよう削除前にCommitDeviceNameEdit()で確定させる
    // (隠密静的レビュー指摘、往復1周目)。
    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CommitDeviceNameEdit();
        if (_viewModel.DeleteSelectedElement() || _viewModel.DeleteSelectedConnector()
            || _viewModel.DeleteSelectedWireBreak() || _viewModel.DeleteSelectedFreeLine()
            || _viewModel.DeleteSelectedConnectionDot())
            RedrawCanvas();
    }

    private bool IsCanvasFocused() => IsWithin(LadderCanvasHost, Keyboard.FocusedElement as DependencyObject);

    // T-082: Alt+上下キーがシートナビゲーション(SheetNavList)宛かを見る判定(IsCanvasFocusedと対)。
    private bool IsSheetNavFocused() => IsWithin(SheetNavList, Keyboard.FocusedElement as DependencyObject);

    // T-082: 選択中のシートを上(delta=-1)/下(delta=+1)へ1つ並び替える(Alt+上下キー共通経路)。
    // 端(先頭/末尾)ではMoveSheetCommandのCanExecuteがfalseとなり何もしない(no-op)。
    private void MoveCurrentSheet(int delta)
    {
        int fromIndex = _viewModel.CurrentSheetIndex;
        var command = _viewModel.SheetNavigation.MoveSheetCommand;
        var param = (fromIndex, fromIndex + delta);
        if (command.CanExecute(param)) command.Execute(param);
    }

    private void MoveSelectedCell(Key key)
    {
        // T-019: Document.Sheets.Count==0(新規直後の暫定挙動)の間はCurrentSheetがnullのため、
        // 移動先のGridが存在せず無視する(キャンバスにフォーカスは当たりうるため、ここでの防御が要る)。
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet currentSheet) return;

        var current = _viewModel.SelectedCell ?? new Ecad2.Model.GridPos(0, 0);
        var grid = currentSheet.Grid;
        int row = current.Row;
        int column = current.Column;
        switch (key)
        {
            case Key.Up: row = Math.Max(0, row - 1); break;
            case Key.Down: row = Math.Min(grid.Rows - 1, row + 1); break;
            case Key.Left: column = Math.Max(0, column - 1); break;
            case Key.Right: column = Math.Min(grid.Columns, column + 1); break;
        }
        var newCell = new Ecad2.Model.GridPos(row, column);
        _viewModel.SelectedCell = newCell;

        // 増分(v, T-021): 矢印移動時のカーソル追従スクロール(論点5、パン=矢印追従+ホイール)。
        // 修正2(差し戻し1周目): グリッド端でクランプされ実移動が無い場合(newCell==current)は
        // BringIntoViewを呼ばない。無条件発火だと、端で矢印を押しても手動スクロール中のビューを
        // 同位置へ強制的に引き戻してしまう(隠密レビュー指摘)。GridPosはrecord structゆえ != は値比較。
        if (newCell != current)
        {
            // 修正1(差し戻し1周目): 右母線位置(Column==grid.Columns、Element.cs:39「右母線=Columns」)
            // ではCellRectDipが右母線の外側の余白矩形を返し、BringIntoViewが余白へ能動スクロールする。
            // スクロール座標に限り最終セル列(Columns-1)へクランプして余白送りを防ぐ。選択セル・
            // ハイライトのColumn==Columnsはそのまま維持する(CellRectDip本体はハイライト描画・T-023
            // Automation Peerと共有のため変更せず、呼び出し側で調整=家老指示)。
            int viewColumn = Math.Min(newCell.Column, grid.Columns - 1);
            var viewCell = new Ecad2.Model.GridPos(newCell.Row, viewColumn);
            // CellRectDipはLayoutTransform適用前のローカルDIP座標。BringIntoViewのRequestBringIntoView
            // 経路でMakeVisibleがズームのScaleTransformを含む変換を行うため、ローカル座標のまま渡す
            // 想定(ズーム≠100%時の座標一致は理論確認のみ、忍者の実機検証で最終確認する)。
            // スクロール量は「見えるまで最小限」のWPF標準挙動(分岐C、家老承認済み)。
            var viewRect = LadderCanvasHost.CellRectDip(viewCell);
            // 増分(v)追加修正(殿指示): 左端(Column 0)・右端(Column Columns-1)到達時は、セルだけ
            // 見えても母線が画面外のままだと視認しづらいため、BringIntoView対象を母線側へ
            // セル1個分広げる。2条件は独立判定のため、Columns==1のような極小グリッドでも
            // 両母線が同時に見える範囲になる。行方向は対象外(殿指示)。
            // 修正(隠密レビュー340f53d指摘#1): widenは今回の操作で列が実際に変化した場合に限る。
            // 列値だけで判定すると、列端に留まったままUp/Downで行だけ動かした際にも毎回発火し、
            // 手動横スクロール位置を引き戻しうる(増分(v)のnewCell!=currentガードと同種の回帰)。
            bool columnChanged = newCell.Column != current.Column;
            double viewLeft = viewRect.X;
            double viewWidth = viewRect.Width;
            if (columnChanged && viewColumn == 0)
            {
                viewLeft -= viewRect.Width;
                viewWidth += viewRect.Width;
            }
            if (columnChanged && viewColumn == grid.Columns - 1)
            {
                viewWidth += viewRect.Width;
            }
            LadderCanvasHost.BringIntoView(new Rect(viewLeft, viewRect.Y, viewWidth, viewRect.Height));
        }
    }

    // T-041増分7: 選択中の縦コネクタを矢印キー1回分(Shift無し)平行移動する(キーボード等価操作、
    // 案X)。Up/Down=行方向、Left/Right=列方向。ViewModelのMoveSelectedConnector/
    // MoveSelectedConnectorColumnはモデル(TopRow/BottomRow/Column)を直接更新するのみで
    // SelectedConnector自体の参照は変わらないためPropertyChangedが発火せず、RedrawCanvas()を
    // ここで明示的に呼ぶ(ドラッグ確定・Escキャンセルと同じ理由)。
    private void MoveSelectedConnectorByKey(Key key)
    {
        bool moved = key switch
        {
            Key.Up => _viewModel.MoveSelectedConnector(-1),
            Key.Down => _viewModel.MoveSelectedConnector(1),
            Key.Left => _viewModel.MoveSelectedConnectorColumn(-1),
            Key.Right => _viewModel.MoveSelectedConnectorColumn(1),
            _ => false,
        };
        if (moved) RedrawCanvas();
    }

    // T-041増分7横展開: 選択中の配線分断を矢印キー1回分(Shift無し)平行移動する(点系、本体移動のみ)。
    // MoveSelectedConnectorByKeyと同じ理由でRedrawCanvas()をここで明示的に呼ぶ。
    private void MoveSelectedWireBreakByKey(Key key)
    {
        bool moved = key switch
        {
            Key.Up => _viewModel.MoveSelectedWireBreak(-1, 0),
            Key.Down => _viewModel.MoveSelectedWireBreak(1, 0),
            Key.Left => _viewModel.MoveSelectedWireBreak(0, -1),
            Key.Right => _viewModel.MoveSelectedWireBreak(0, 1),
            _ => false,
        };
        if (moved) RedrawCanvas();
    }

    // T-041増分7横展開: 選択中の自由線を矢印キー1回分(Shift無し)平行移動する(mm実座標系、
    // 1ステップ=CellMm=記入時(BeginFreeLineDraft)・WireBreak横展開と同じ単位に揃える)。
    private void MoveSelectedFreeLineByKey(Key key)
    {
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet) return;
        double step = LadderCanvasHost.CellMm;
        // T-041増分7隠密レビュー所見AA対応: ページ境界(mm)を渡す(BeginDragFreeLineと同じ設計)。
        double maxXMm = sheet.Grid.Columns * step;
        double maxYMm = sheet.Grid.Rows * step;
        bool moved = key switch
        {
            Key.Up => _viewModel.MoveSelectedFreeLine(0, -step, maxXMm, maxYMm),
            Key.Down => _viewModel.MoveSelectedFreeLine(0, step, maxXMm, maxYMm),
            Key.Left => _viewModel.MoveSelectedFreeLine(-step, 0, maxXMm, maxYMm),
            Key.Right => _viewModel.MoveSelectedFreeLine(step, 0, maxXMm, maxYMm),
            _ => false,
        };
        if (moved) RedrawCanvas();
    }

    // T-041増分7横展開(殿裁定P-033=案2): Tabで選んだ操作対象端点をShift+矢印で伸縮する。水平線は
    // Left/Rightのみ、垂直線はUp/Downのみ意味を持つ(線の向きに沿わないキーは無視、
    // AdjustFreeLineDraftと同じ制約)。
    private void ResizeSelectedFreeLineByKey(Key key)
    {
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet) return;
        double step = LadderCanvasHost.CellMm;
        // T-041増分7隠密レビュー所見AC対応: ページ境界(mm)を渡す(BeginDragFreeLineと同じ設計)。
        double maxXMm = sheet.Grid.Columns * step;
        double maxYMm = sheet.Grid.Rows * step;
        bool resized = key switch
        {
            Key.Up => _viewModel.ResizeSelectedFreeLineEndpoint(0, -step, maxXMm, maxYMm),
            Key.Down => _viewModel.ResizeSelectedFreeLineEndpoint(0, step, maxXMm, maxYMm),
            Key.Left => _viewModel.ResizeSelectedFreeLineEndpoint(-step, 0, maxXMm, maxYMm),
            Key.Right => _viewModel.ResizeSelectedFreeLineEndpoint(step, 0, maxXMm, maxYMm),
            _ => false,
        };
        if (resized) RedrawCanvas();
    }

    // T-041増分7横展開: 選択中の接続点を矢印キー1回分(Shift無し)平行移動する(点系・mm実座標系、
    // 1ステップ=CellMm、本体移動のみ)。
    private void MoveSelectedConnectionDotByKey(Key key)
    {
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet) return;
        double step = LadderCanvasHost.CellMm;
        // T-041増分7隠密レビュー所見AD対応: ページ境界(mm)を渡す(MoveSelectedFreeLineと同じ設計)。
        double maxXMm = sheet.Grid.Columns * step;
        double maxYMm = sheet.Grid.Rows * step;
        bool moved = key switch
        {
            Key.Up => _viewModel.MoveSelectedConnectionDot(0, -step, maxXMm, maxYMm),
            Key.Down => _viewModel.MoveSelectedConnectionDot(0, step, maxXMm, maxYMm),
            Key.Left => _viewModel.MoveSelectedConnectionDot(-step, 0, maxXMm, maxYMm),
            Key.Right => _viewModel.MoveSelectedConnectionDot(step, 0, maxXMm, maxYMm),
            _ => false,
        };
        if (moved) RedrawCanvas();
    }

    // 選択ツールボタン(ツールバーのEsc相当ボタン)の即時処理。選択セル・ツール・案内メッセージを
    // 一括で全解除する。Window_PreviewKeyDownのEscキーは増分(iv)で段階的(1回1層)になったため
    // 「同じ操作」ではない。ボタンは即時全解除、Escキーは内→外へ1層ずつ戻す。
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
        if (isKeyboardOrigin && !RequiresCanvasFocusContinuation(_viewModel.Tool.Mode))
            (sender as UIElement)?.Focus();
        else
            FocusCanvas();
        _toolButtonKeyboardClickSource = null;
    }

    // T-047修正(隠密2所見1+忍者実機4-c対応、隠密設計書1-2節推奨案採用): 記入中状態
    // (PlaceConnector/PlaceLine)へ遷移した場合、次の操作は必ずキャンバス側の矢印キー
    // 調整・Enter確定であり「ツールバーに留まりたい」という懸念4のシナリオが原理的に
    // 存在しない。Tool.Modeで判定するため、既存8ボタン(実行後は常にSelect/PlaceElement)
    // には一切影響せず懸念4の挙動を保つ(隠密設計書1-3節のトレース表で確認済み)。WPF依存の
    // 無い純粋な条件判定として切り出し、ユニットテスト(reflection経由)を可能にする
    // (隠密設計書2-2節)。
    private static bool RequiresCanvasFocusContinuation(ViewModels.ToolMode mode) =>
        mode is ViewModels.ToolMode.PlaceConnector or ViewModels.ToolMode.PlaceLine;

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
    // ツールバーボタンはHasProject連動でIsEnabled=falseになるがキーボードショートカットは
    // ボタンの活性状態と独立に発火するため、ここでHasProjectを明示ガードする(殿実機確認で
    // 発覚した通知漏れ対処、T-019追加増分)。
    private void TryPlaceBuiltin(string partName, bool isOr)
    {
        if (!_viewModel.HasProject)
        {
            _viewModel.StatusMessage = "シートがありません。新規作成（Ctrl+N）から始めてください";
            return;
        }
        var entry = _viewModel.PartPalette.Entries.FirstOrDefault(e => e.Category == "" && e.Definition.Name == partName);
        if (entry is not null) TryPlaceElement(entry, isOr);
    }

    // T-041増分2: sF9押下時の縦コネクタ記入モード開始。TryPlaceBuiltinと同型の前提チェック
    // (HasProject→SelectedCell)に加え、制御回路シート限定(主回路のFreeLineは増分5)を確認する。
    private void TryBeginConnectorDraft()
    {
        if (!_viewModel.HasProject)
        {
            _viewModel.StatusMessage = "シートがありません。新規作成（Ctrl+N）から始めてください";
            return;
        }
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet || sheet.MainCircuit)
        {
            _viewModel.StatusMessage = "縦分岐線の記入は制御回路シートでのみ使用できます";
            return;
        }
        if (_viewModel.SelectedCell is null)
        {
            _viewModel.StatusMessage = "配置するセルを先に選択してください";
            return;
        }
        _viewModel.BeginConnectorDraft();
        _viewModel.StatusMessage = "上下キーで範囲、左右キー(Shiftでセル中央)で列位置を調整しEnterで確定、Escで取消";
    }

    // T-041増分2: 縦コネクタ記入中(Tool.Mode==PlaceConnector)の矢印キーで範囲・列位置を調整する。
    // Up/Downで終点行を伸縮、Left/Rightで列境界を移動(cellCenterStep=falseは整数境界1.0刻み、
    // true(Shift併用)はセル中央0.5刻み、原案3節)。
    private void AdjustConnectorDraft(Key key, bool cellCenterStep)
    {
        switch (key)
        {
            case Key.Up: _viewModel.MoveConnectorDraftRow(-1); break;
            case Key.Down: _viewModel.MoveConnectorDraftRow(1); break;
            case Key.Left: _viewModel.MoveConnectorDraftColumn(cellCenterStep ? -0.5 : -1.0); break;
            case Key.Right: _viewModel.MoveConnectorDraftColumn(cellCenterStep ? 0.5 : 1.0); break;
        }
    }

    // T-041増分3: F10押下時の配線分断(WireBreak)即時記入。点系は確認フェーズ無し(原案4節)。
    private void TryPlaceWireBreak()
    {
        if (!_viewModel.HasProject)
        {
            _viewModel.StatusMessage = "シートがありません。新規作成（Ctrl+N）から始めてください";
            return;
        }
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet || sheet.MainCircuit)
        {
            _viewModel.StatusMessage = "配線分断の記入は制御回路シートでのみ使用できます";
            return;
        }
        if (_viewModel.SelectedCell is null)
        {
            _viewModel.StatusMessage = "配置するセルを先に選択してください";
            return;
        }
        if (_viewModel.PlaceWireBreakAtSelectedCell())
            RedrawCanvas();
        else
            _viewModel.StatusMessage = "この位置には既に配線分断があります";
    }

    // T-041増分5: F9(横線)/sF9(縦線)押下時の自由線記入モード開始。TryBeginConnectorDraftと同型の
    // 前提チェック(HasProject→SelectedCell)に加え、主回路シート限定(制御回路のVerticalConnectorは
    // 増分2)を確認する。mm座標への変換(SelectedCell→mm)・矢印キー1回分の移動量(CellMm)はここで
    // LadderCanvasHostから取得しViewModelへ渡す(ViewModelは幾何を知らない設計、増分5節参照)。
    private void TryBeginFreeLineDraft(bool horizontal)
    {
        if (!_viewModel.HasProject)
        {
            _viewModel.StatusMessage = "シートがありません。新規作成（Ctrl+N）から始めてください";
            return;
        }
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet || !sheet.MainCircuit)
        {
            _viewModel.StatusMessage = "自由線の記入は主回路シートでのみ使用できます";
            return;
        }
        if (_viewModel.SelectedCell is not { } pos)
        {
            _viewModel.StatusMessage = "配置するセルを先に選択してください";
            return;
        }
        var (xMm, yMm) = LadderCanvasHost.CellToMm(pos);
        _viewModel.BeginFreeLineDraft(horizontal, xMm, yMm, LadderCanvasHost.CellMm);
        _viewModel.StatusMessage = (horizontal ? "左右キー" : "上下キー") + "で長さを調整しEnterで確定、Escで取消";
    }

    // T-041増分5: 自由線記入中(Tool.Mode==PlaceLine)の矢印キーで長さを調整する。水平線はLeft/Right
    // のみ、垂直線はUp/Downのみが有効(直交方向のキーは無視、原案4節「水平・垂直のみ」の制約)。
    private void AdjustFreeLineDraft(Key key)
    {
        bool horizontal = _viewModel.IsFreeLineDraftHorizontal;
        int delta = (horizontal, key) switch
        {
            (true, Key.Left) => -1,
            (true, Key.Right) => 1,
            (false, Key.Up) => -1,
            (false, Key.Down) => 1,
            _ => 0,
        };
        if (delta != 0) _viewModel.MoveFreeLineDraftEnd(delta);
    }

    // T-041増分5: F10押下時(主回路シート)の接続点即時記入。TryPlaceWireBreakと同型、点系は確認
    // フェーズ無し(原案4節)。mm座標への変換はTryBeginFreeLineDraftと同様ここで行う。
    private void TryPlaceConnectionDot()
    {
        if (!_viewModel.HasProject)
        {
            _viewModel.StatusMessage = "シートがありません。新規作成（Ctrl+N）から始めてください";
            return;
        }
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet || !sheet.MainCircuit)
        {
            _viewModel.StatusMessage = "接続点の記入は主回路シートでのみ使用できます";
            return;
        }
        if (_viewModel.SelectedCell is not { } pos)
        {
            _viewModel.StatusMessage = "配置するセルを先に選択してください";
            return;
        }
        var (xMm, yMm) = LadderCanvasHost.CellToMm(pos);
        if (_viewModel.PlaceConnectionDot(xMm, yMm))
            RedrawCanvas();
        else
            _viewModel.StatusMessage = "この位置には既に接続点があります";
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

    // T-047: 手動配線系(F9/Shift+F9/F10)ボタン。呼び出し先の各Try系メソッドはキーボード
    // ショートカット(Window_PreviewKeyDown)と共有の既存メソッドで、HasProject/シート種別/
    // SelectedCellのガードは各メソッド内で完結している(中身は不変)。IsEnabledのシート種別連動
    // (MainWindow.xaml参照)により非対応シートではボタン自体がグレーアウトし押下できない。
    private void FreeLineHorizontalButton_Click(object sender, RoutedEventArgs e)
    {
        TryBeginFreeLineDraft(horizontal: true);
        ConsumeToolButtonFocusRestore(sender);
    }

    private void FreeLineVerticalButton_Click(object sender, RoutedEventArgs e)
    {
        TryBeginFreeLineDraft(horizontal: false);
        ConsumeToolButtonFocusRestore(sender);
    }

    private void VerticalConnectorButton_Click(object sender, RoutedEventArgs e)
    {
        TryBeginConnectorDraft();
        ConsumeToolButtonFocusRestore(sender);
    }

    private void ConnectionDotButton_Click(object sender, RoutedEventArgs e)
    {
        TryPlaceConnectionDot();
        ConsumeToolButtonFocusRestore(sender);
    }

    private void WireBreakButton_Click(object sender, RoutedEventArgs e)
    {
        TryPlaceWireBreak();
        ConsumeToolButtonFocusRestore(sender);
    }

    // 右パネル下段の部品選択リストの項目クリック。PreviewMouseLeftButtonDownを使う理由は
    // ListBoxItem.Selectedが同一アイテム再選択時に発火しない(WPFの仕様、T-016で確認済み)ため。
    // DataContextはPartSelectionList表示専用のサムネイル付きラッパー(T-015)、配置処理へは
    // 元のPartFolderEntry(.Entry)を渡す。
    private void PartSelectionItem_Clicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: ViewModels.PartSelectionEntryViewModel entry })
            TryPlaceElement(entry.Entry, entry.IsOr);
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
    // 浮動インラインバー(種別+デバイス名、T-033増分1で同一Window内オーバーレイの非モーダル化)を
    // 選択セル付近に表示→OKで確定配置。isOr=trueの場合、実際のOR接続処理(基準行判定・縦コネクタ
    // 生成)はViewModel側の責務。
    private void TryPlaceElement(Ecad2.Persistence.PartFolderEntry initialEntry, bool isOr)
    {
        if (_viewModel.SelectedCell is not { } cell)
        {
            _viewModel.StatusMessage = "配置するセルを先に選択してください";
            return;
        }
        // T-071バグ修正: initialEntry(クリックされた部品)のWidthCellsをプレチェックへ渡す。配置バー
        // 表示後にコンボボックスで別部品へ切り替えられた場合はPlaceElementAtSelectedCell側の
        // ValidatePlacement(実際に配置するpartIdのWidthCellsで再判定)が最終防御になる。
        int cellWidth = initialEntry.Definition.WidthCells;
        if (!_viewModel.IsSelectedCellWithinGrid(cellWidth))
        {
            _viewModel.StatusMessage = "選択したセルはグリッド範囲外です";
            return;
        }
        if (_viewModel.IsSelectedCellOccupied(cellWidth))
        {
            _viewModel.StatusMessage = "選択したセルには既に要素があります";
            return;
        }

        // T-033増分1: 非モーダル化により`ShowDialog()`の同期戻り値待ちが失われるため、OK/キャンセル
        // 確定処理はPlacementOkButton_Click/PlacementCancelButton_Clickへ移設した(旧
        // `if (dialog.ShowDialog() == true ...)`の同期構造からの構造変更、プラン3.3節参照)。
        //
        // T-033増分5(殿裁定=表示どおりの動作): ドロップダウンは部品選択リストと同じ7種
        // (ORa/ORb含む、PartPaletteViewModel.SelectionEntries)を表示する。入口がsF5/sF6(OR系
        // ツールバーボタン)ならisOr=trueで呼ばれるため、初期選択もOR版(ORa接点/ORb接点)を選ぶ
        // (Id一致かつIsOr一致を優先し、無ければId一致のみへフォールバック)。
        PlacementPartComboBox.ItemsSource = _viewModel.PartPalette.SelectionEntries;
        PlacementPartComboBox.SelectedItem = _viewModel.PartPalette.ResolveEntry(initialEntry.Definition.Id, isOr)
            ?? _viewModel.PartPalette.SelectionEntries.FirstOrDefault();
        PlacementDeviceNameBox.Text = "";
        _viewModel.IsPlacementBarVisible = true;
        PositionPlacementBar(cell);
        // 隠密レビュー指摘(観点3、Microsoft Learn「Focus Overview - WPF」一次情報): Collapsed→
        // Visible直後はMeasure/Arrange未完了のため、Focus()の同期呼び出しは失敗しうる(例外も
        // フィードバックも無く気づかれにくい)。レイアウトパス完了後に確実にフォーカスするため
        // Dispatcher.BeginInvokeへ委譲する。
        Dispatcher.BeginInvoke(new Action(() => PlacementDeviceNameBox.Focus()), DispatcherPriority.Loaded);
    }

    // T-033増分2(殿注文1): 配置バーを選択セルの直下へ表示する。CellRectDipはLayoutTransform
    // 適用前のローカルDIP座標のため、TranslatePointで変換する。TranslatePointはズーム
    // (LayoutTransform)・ScrollViewerのスクロールオフセットの両方を実際の描画位置として反映する
    // (PointToScreenと同じ変換機構、SymbolAutomationPeer.GetBoundingRectangleCore参照)。
    //
    // 隠密レビューCONFIRMED(T-033増分2位置バグ、`docs/ecad2-t033-review-onmitsu-3.md`観点a):
    // ElementPlacementBarはRootLayoutGrid直下(Grid.Row="2"、ラッパーMainContentAreaの外)にあるため、
    // Marginの基準はRootLayoutGrid座標系でなければならない。旧実装はMainWorkAreaGrid基準の座標を
    // そのまま流用しており、MainContentAreaのRowSpan導入でルートのAuto行(メニュー/ツールバー等)が
    // 実質0に潰れる副作用と相まって、両者の原点が食い違っていた(バーが恒常的に上へ表示される原因)。
    // 変換先をRootLayoutGridへ揃え、クランプ基準もMainWorkAreaGridの原点をRootLayoutGrid座標系へ
    // 変換した値(workAreaOrigin)から算出することで、原点の食い違いを解消する。
    //
    // バーの実サイズはVisibility=Visible反映後でないと取得できない(WPF仕様: Collapsed中の
    // Measure()はDesiredSizeを強制的に0,0にする)。呼び出し元でIsPlacementBarVisible=trueを
    // 先に設定してから本メソッドを呼ぶ前提。
    private void PositionPlacementBar(Ecad2.Model.GridPos cell)
    {
        var localRect = LadderCanvasHost.CellRectDip(cell);
        var inputPoint = new Point(localRect.X, localRect.Bottom);
        var topLeft = LadderCanvasHost.TranslatePoint(inputPoint, RootLayoutGrid);
        var workAreaOrigin = MainWorkAreaGrid.TranslatePoint(new Point(0, 0), RootLayoutGrid);

        // 診断ログ一次パスCONFIRMED(docs-notes/ecad2-t033-diag-pass1-diagnosis-samurai.md): 前回呼び出し
        // 終了時のMarginが残留したままMeasure()すると、WPF仕様(DesiredSize=content+Margin)により前回の
        // 位置が今回の測定結果を汚染する自己参照フィードバックループが生じる。測定前にリセットする。
        ElementPlacementBar.Margin = new Thickness(0);
        ElementPlacementBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size barSize = ElementPlacementBar.DesiredSize;

        // 画面端クランプ(殿注文2): 右端・下端でバーがMainWorkAreaGridの外へはみ出さないようにする。
        double maxX = Math.Max(workAreaOrigin.X, workAreaOrigin.X + MainWorkAreaGrid.ActualWidth - barSize.Width);
        double maxY = Math.Max(workAreaOrigin.Y, workAreaOrigin.Y + MainWorkAreaGrid.ActualHeight - barSize.Height);
        double x = Math.Clamp(topLeft.X, workAreaOrigin.X, maxX);
        double y = Math.Clamp(topLeft.Y, workAreaOrigin.Y, maxY);

        ElementPlacementBar.Margin = new Thickness(x, y, 0, 0);
    }

    // T-080: 行コメント編集中の行番号。エディタが閉じている間はnull。
    private int? _rungCommentEditingRow;

    // T-080往復2周目(a)修正: 行コメントダブルクリックを開くべきかの判定を純粋関数として抽出
    // (隠密テスト設計docs/ecad2-t080-doubleclick-root-cause-onmitsu.md不明点3、家老裁定3=
    // MouseButtonEventArgs.ClickCountのsetアクセサがinternalで直接構築できないテスト容易性の
    // 制約を回避する最小限の工夫)。clickCount==2の等値判定は現行のまま維持する(トリプルクリック
    // 以降は開かない、家老裁定1=殿へ報告済み)。hitTestRowはLadderCanvas.HitTestRungCommentRow
    // (ヒット領域内外・主回路シートガードを内包)の結果をそのまま受け取る。
    internal static int? ShouldOpenRungCommentEditor(int clickCount, int? hitTestRow)
        => clickCount == 2 ? hitTestRow : null;

    // T-080追加往復(課題2)修正: 行コメント領域内へのクリックで選択状態(SelectedCell代入による
    // SelectedConnector/SelectedWireBreak/SelectedFreeLine/SelectedConnectionDotの巻き添えクリア)を
    // 変更すべきでないかを判定する純粋関数として抽出(隠密テスト設計
    // docs/ecad2-t080-issue2-3-root-cause-onmitsu.md、ShouldOpenRungCommentEditorと同型)。
    // hitTestRowが非nullならヒット領域内(=グリッドセルの概念に載らない帯)であり、ClickCountを
    // 問わず選択状態を変更しない。4種の配線プリミティブいずれについても、この1つの判定を
    // 呼び出し側で早期returnとして使うため対称に保護される(個別の特別扱いをしない設計)。
    internal static bool ShouldSkipSelectionForRungCommentAreaClick(int? hitTestRow)
        => hitTestRow is not null;

    // 行コメントエディタを開く(右母線右側ダブルクリック、またはF2キー=往復1周目追加I)。
    // 既存コメントがあれば読み込む。表示状態はIsRungCommentEditorVisible(往復1周目指摘F)への
    // バインドで反映し、MainContentAreaのIsEnabledと連動させる(配置バーと同じ仕組み)。
    private void OpenRungCommentEditor(int row, Ecad2.Model.Sheet sheet)
    {
        _rungCommentEditingRow = row;
        RungCommentBox.Text = _viewModel.GetRungComment(row);
        _viewModel.IsRungCommentEditorVisible = true;
        PositionRungCommentEditor(row, sheet);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            RungCommentBox.Focus();
            RungCommentBox.SelectAll();
        }), DispatcherPriority.Loaded);
    }

    // 行コメントエディタの位置決め(T-080)。PositionPlacementBarと同型のTranslatePoint方式
    // (RootLayoutGrid座標系への変換)を流用するが、画面端クランプの基準はCanvasArea(ScrollViewer)の
    // 可視ビューポートとする。
    //
    // T-080往復2周目 追加修正(課題3、忍者実機NG=docs-notes/ecad2-t080-ninja-final-verification.md
    // 範囲外検出節): 旧実装はMainWorkAreaGrid(左パレット+キャンバス+右パネル機器表の全体)基準で
    // クランプしていた。対象行が水平/垂直スクロール範囲外(左端表示のまま右母線側の行コメントを開く等)
    // だとTranslatePointの返す値がCanvasAreaの可視領域を大きく超え、クランプが常時発動して
    // MainWorkAreaGrid右端(=機器表パネルの真下、画面外扱いで実質不可視)に固定表示されてしまっていた。
    // PositionPlacementBarは常に可視セルクリックからしか呼ばれないためこの問題が顕在化しなかった
    // (同じロジックの潜在バグだが範囲外、本修正では触れない)。クランプ基準をCanvasArea自身の
    // ActualWidth/ActualHeightへ変更し、対象が可視範囲外でもキャンバスの可視領域内に留めて表示する。
    private void PositionRungCommentEditor(int row, Ecad2.Model.Sheet sheet)
    {
        var inputPoint = LadderCanvasHost.RungCommentAnchorDip(row, sheet);
        var topLeft = LadderCanvasHost.TranslatePoint(inputPoint, RootLayoutGrid);
        var canvasAreaOrigin = CanvasArea.TranslatePoint(new Point(0, 0), RootLayoutGrid);

        RungCommentEditor.Margin = new Thickness(0);
        RungCommentEditor.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size barSize = RungCommentEditor.DesiredSize;

        Point clamped = ClampToViewport(topLeft, canvasAreaOrigin, CanvasArea.ActualWidth, CanvasArea.ActualHeight, barSize);

        RungCommentEditor.Margin = new Thickness(clamped.X, clamped.Y, 0, 0);
    }

    // T-080追加往復(課題3)修正: 画面端クランプの計算式自体を純粋関数として抽出(隠密テスト設計
    // docs/ecad2-t080-issue2-3-root-cause-onmitsu.md「部分的に単体テスト化できる範囲」)。
    // ActualWidth/TranslatePoint等のレイアウト依存値の取得そのものは単体テスト化できない
    // (実ウィンドウのレイアウトパスが必要)ため、それらを呼び出し元で解決した後の「純粋な範囲制限
    // 計算」のみを切り出す。topLeftがviewport外(左右上下いずれか)ならviewport内へ収める。
    internal static Point ClampToViewport(Point topLeft, Point viewportOrigin, double viewportWidth, double viewportHeight, Size barSize)
    {
        double maxX = Math.Max(viewportOrigin.X, viewportOrigin.X + viewportWidth - barSize.Width);
        double maxY = Math.Max(viewportOrigin.Y, viewportOrigin.Y + viewportHeight - barSize.Height);
        double x = Math.Clamp(topLeft.X, viewportOrigin.X, maxX);
        double y = Math.Clamp(topLeft.Y, viewportOrigin.Y, maxY);
        return new Point(x, y);
    }

    // 確定(Enter/Tab/フォーカスロスト、GuiEcad踏襲)。SetRungCommentは値未変更なら
    // MarkDirty()しない(同値ガード規約)ため、無変更のまま確定しても無害。
    private void CommitRungCommentEditor(bool restoreFocus)
    {
        if (_rungCommentEditingRow is not int row) return;
        _viewModel.SetRungComment(row, RungCommentBox.Text);
        CloseRungCommentEditor(restoreFocus);
        RedrawCanvas();
    }

    private void CancelRungCommentEditor() => CloseRungCommentEditor(restoreFocus: true);

    // restoreFocus=false: フォーカスロスト確定経路(往復1周目指摘H)。無条件のFocusCanvas()は
    // Keyboard.Focus()経由で直前のフォーカス保持者にLostKeyboardFocusを同期再発火させる再入機構
    // (下記Escapeハンドラの既存コメントで実証済み)を持ち、ユーザーがマウスで移した先から
    // フォーカスを奪い返してしまうため、キーボード経路(Enter/Tab/Esc)のみキャンバスへ復帰する
    // (DeviceNameBox_LostKeyboardFocusがFocusCanvas()を呼ばないのと同じ非対称の解消)。
    private void CloseRungCommentEditor(bool restoreFocus)
    {
        _rungCommentEditingRow = null;
        _viewModel.IsRungCommentEditorVisible = false;
        if (restoreFocus) FocusCanvas();
    }

    // Enter/Tab=確定、Escape=取消(GuiEcad踏襲)。
    private void RungCommentBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            CommitRungCommentEditor(restoreFocus: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelRungCommentEditor();
            e.Handled = true;
        }
    }

    // フォーカスロスト=確定扱い(キャンセルではない、GuiEcad踏襲)。CommitRungCommentEditor内の
    // CloseRungCommentEditorがフォーカス遷移を誘発しうるが、_rungCommentEditingRowを先に
    // null化しているため多重確定にはならない。restoreFocus=false: フォーカスの行き先はユーザーの
    // 操作(クリック先等)に委ね、キャンバスへ奪い返さない(往復1周目指摘H)。
    private void RungCommentBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_rungCommentEditingRow is not null) CommitRungCommentEditor(restoreFocus: false);
    }

    // 分岐B(殿裁定=命名中Escは配置ごと原子的取消, T-021): 配置(PlaceElementAtSelectedCell)は
    // OK確定した場合のみ行う。Esc/キャンセル(PlacementCancelButton_Click)では要素を一切作らない
    // ため、未命名の孤立要素は構造上残らない(現行の「OK後に配置」構造がそのまま原子的取消を満たす)。
    private void PlacementOkButton_Click(object sender, RoutedEventArgs e)
    {
        if (PlacementPartComboBox.SelectedItem is ViewModels.PartSelectionEntryViewModel entry)
        {
            // T-033増分5(殿裁定=表示どおりの動作): ドロップダウンで選んだ項目そのものがOR属性を
            // 決める(見たまま=実態)。T-037で導入した「接点系同士の切替でisOrを暗黙保持する」ルール
            // (旧: `_placementIsOr == true && entry.Definition.IsOrEligible`)は本裁定で廃止する。
            // ORa接点で開いてもb接点へ切り替えればORを失う(明示的にORb接点を選べばOR並列b接点になる)。
            bool effectiveIsOr = entry.IsOr;
            _viewModel.PlaceElementAtSelectedCell(entry.Definition.Id, PlacementDeviceNameBox.Text.Trim(), effectiveIsOr);
            _viewModel.StatusMessage = "";
            RedrawCanvas();
        }
        ClosePlacementBar();
    }

    private void PlacementCancelButton_Click(object sender, RoutedEventArgs e) => ClosePlacementBar();

    // 分岐A(殿裁定=ツール保持で連続配置, T-021): 配置後もTool/SelectedCellをリセットしない。
    // 「移動(矢印)→配置(Enter)→命名→確定→また移動…」の一気通貫(案X)を継続できるよう、
    // アクティブツールと選択セル(次の移動起点)を保持する。ツール解除はEsc(Window_PreviewKeyDownの
    // Escapeケース)に委ねる。クリック配置経路(LadderCanvasHost_PreviewMouseLeftButtonUp)も
    // TryPlaceElement経由のため、両経路で連続配置の挙動に揃う。
    // T-033増分1(PoC所見): バーを閉じた後、フォーカスをキャンバスへ明示復帰する処理をここへ
    // 一箇所集約する(OK確定・キャンセル両経路。T-021モグラ叩きの教訓=遷移点は複数箇所に分散させない。
    // PoCで暗黙委譲では戻らないことを確認済みのため、明示呼び出しは保険ではなく必須)。独立FocusScope
    // の罠(T-016)を避けるため素のKeyboard.Focusではなく2段方式のFocusCanvasに統一(隠密レビュー観点3)。
    private void ClosePlacementBar()
    {
        _viewModel.IsPlacementBarVisible = false;
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

    // T-082: シートナビゲーション(SheetNavList)のドラッグ&ドロップ並び替え(殿裁定「案A」=標準
    // フィードバック=ドラッグ中カーソル変化+ドロップ位置に挿入線+ドラッグ元アイテム半透明化)。
    // ListBoxアイテムの並び替えという性質上、既存のキャンバス要素ドラッグ(マウスキャプチャ方式)とは
    // 対象が異なるため、WPFネイティブDragDrop APIで新規実装する(Explore調査で既存流用パターン
    // 無しと確認済み)。カーソル変化はDragDropEffects.Moveに対するWPF既定カーソルに委ねる
    // (GiveFeedbackを未フックでもUseDefaultCursors既定trueで自動表示される)。
    private void SheetNavList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _sheetDragStartPoint = e.GetPosition(null);
        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        _sheetDragSourceContainer = container;
        _sheetDragSource = container?.DataContext as Ecad2.Model.Sheet;
    }

    private void SheetNavList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_sheetDragSource is null || e.LeftButton != MouseButtonState.Pressed) return;
        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _sheetDragStartPoint.X) < DragStartThresholdDip
            && Math.Abs(current.Y - _sheetDragStartPoint.Y) < DragStartThresholdDip)
            return;

        var sheet = _sheetDragSource;
        var container = _sheetDragSourceContainer;
        _sheetDragSource = null;
        _sheetDragSourceContainer = null;
        if (sheet is null) return;

        if (container is not null) container.Opacity = 0.4;
        DragDrop.DoDragDrop(SheetNavList, sheet, DragDropEffects.Move);
        if (container is not null) container.Opacity = 1.0;
        RemoveSheetReorderAdorner();
    }

    private void SheetNavList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(Ecad2.Model.Sheet)))
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = DragDropEffects.Move;

        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container is null)
        {
            RemoveSheetReorderAdorner();
            return;
        }
        bool insertAfter = e.GetPosition(container).Y > container.ActualHeight / 2;
        ShowSheetReorderAdorner(container, insertAfter);
        e.Handled = true;
    }

    private void SheetNavList_DragLeave(object sender, DragEventArgs e) => RemoveSheetReorderAdorner();

    private void SheetNavList_Drop(object sender, DragEventArgs e)
    {
        RemoveSheetReorderAdorner();
        if (e.Data.GetData(typeof(Ecad2.Model.Sheet)) is not Ecad2.Model.Sheet droppedSheet) return;

        var sheets = _viewModel.SheetNavigation.Sheets;
        int fromIndex = sheets.IndexOf(droppedSheet);
        if (fromIndex < 0) return;

        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        int toIndex;
        if (container?.DataContext is Ecad2.Model.Sheet targetSheet)
        {
            int targetIndex = sheets.IndexOf(targetSheet);
            bool insertAfter = e.GetPosition(container).Y > container.ActualHeight / 2;
            toIndex = CalculateSheetDropIndex(fromIndex, targetIndex, insertAfter);
        }
        else
        {
            // リスト空白部分(末尾余白)へのドロップは末尾へ移動する。
            toIndex = sheets.Count - 1;
        }
        toIndex = Math.Clamp(toIndex, 0, sheets.Count - 1);

        var command = _viewModel.SheetNavigation.MoveSheetCommand;
        var param = (fromIndex, toIndex);
        if (command.CanExecute(param)) command.Execute(param);
    }

    /// <summary>
    /// T-082往復1周目(隠密レビュー指摘・テストカバレッジ穴埋め4): ドロップ位置(ドロップ先アイテムの
    /// 現添字targetIndex・その下半分にドロップしたかinsertAfter)からtoIndexを算出する純粋関数。
    /// 既存のShouldOpenRungCommentEditor等のstatic抽出パターンに倣い、D&Dで最もバグを生みやすい
    /// 座標系補正ロジックを直接テスト可能にする(SheetNavList_Dropのprivateインスタンスメソッド内
    /// 直書きでは単体テストできなかった)。fromIndexを除去した後の座標系に合わせて補正する
    /// (除去で後続要素が1つ前へ詰まるため)。
    /// </summary>
    internal static int CalculateSheetDropIndex(int fromIndex, int targetIndex, bool insertAfter)
    {
        int toIndex = insertAfter ? targetIndex + 1 : targetIndex;
        if (fromIndex < toIndex) toIndex--;
        return toIndex;
    }

    private void ShowSheetReorderAdorner(FrameworkElement container, bool insertAfter)
    {
        RemoveSheetReorderAdorner();
        var layer = AdornerLayer.GetAdornerLayer(container);
        if (layer is null) return;
        _sheetReorderAdorner = new Views.SheetReorderInsertionAdorner(container, insertAfter);
        layer.Add(_sheetReorderAdorner);
    }

    private void RemoveSheetReorderAdorner()
    {
        if (_sheetReorderAdorner is null) return;
        var layer = AdornerLayer.GetAdornerLayer(_sheetReorderAdorner.AdornedElement);
        layer?.Remove(_sheetReorderAdorner);
        _sheetReorderAdorner = null;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target) return target;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}