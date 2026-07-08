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
    private void DeviceNameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        DeviceNameBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        RedrawCanvas();
    }

    // Enterキーでの即時確定(殿の期待仕様)。Tab・クリック等によるフォーカス移動はLostKeyboardFocus
    // でカバーされるため、ここではEnter押下時のみUpdateSource()を呼ぶ(フォーカスは維持したまま)。
    // RedrawCanvas()もLostKeyboardFocus経路と同様に呼び、キャンバス上のデバイス名ラベル表示を
    // 即時反映する(隠密レビュー指摘)。
    private void DeviceNameBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DeviceNameBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            RedrawCanvas();
        }
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
        if (_viewModel.Tool.Mode != ViewModels.ToolMode.Select) return;
        var position = e.GetPosition(LadderCanvasHost);

        if (_viewModel.SelectedConnector is Ecad2.Model.VerticalConnector connector
            && LadderCanvasHost.HitTestConnectorDragMode(position, connector) is (bool isEndpoint, bool isTop))
        {
            int startRow = LadderCanvasHost.RowAtDip(position.Y);
            _viewModel.BeginDragConnector(connector, isEndpoint, isTop, startRow);
            _connectorDragPressPositionDip = position;
            _connectorDragStarted = false;
            LadderCanvasHost.CaptureMouse();
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
            _wireBreakDragPressPositionDip = position;
            _wireBreakDragStarted = false;
            LadderCanvasHost.CaptureMouse();
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
            _viewModel.UpdateDragConnector(LadderCanvasHost.RowAtDip(position.Y));
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
        }
    }

    private void LadderCanvasHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // T-041増分7実機確認で発覚(往復1周目): Escでキャンセル済み(IsDragging*=falseだが
        // *DragConsumedByEscape=true)のマウスアップは、押していた指を離しただけの後始末。
        // キャプチャを解放するのみで、通常のクリック処理(セル選択/配線プリミティブ選択切替)は行わない
        // (これをスキップしないと、離した位置がたまたま別要素の上にあると誤選択されてしまう)。
        if (_connectorDragConsumedByEscape || _wireBreakDragConsumedByEscape)
        {
            LadderCanvasHost.ReleaseMouseCapture();
            _connectorDragConsumedByEscape = false;
            _wireBreakDragConsumedByEscape = false;
            return;
        }

        // T-041増分7: ドラッグ中だった場合はここで確定し、以降の通常クリック処理(セル選択/配線
        // プリミティブ選択切替)は行わない。ドラッグしきい値未満のまま離した場合もConfirmDrag*は
        // 値が変化していなければMarkDirty()しないため、実質クリックとして無害。
        if (_viewModel.IsDraggingConnector)
        {
            LadderCanvasHost.ReleaseMouseCapture();
            _viewModel.ConfirmDragConnector();
            _connectorDragStarted = false;
            RedrawCanvas();
            return;
        }
        if (_viewModel.IsDraggingWireBreak)
        {
            LadderCanvasHost.ReleaseMouseCapture();
            _viewModel.ConfirmDragWireBreak();
            _wireBreakDragStarted = false;
            RedrawCanvas();
            return;
        }

        var position = e.GetPosition(LadderCanvasHost);

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
            case Key.Tab when noModifier && IsCanvasFocused() && _viewModel.HasSelectedLinePrimitive:
                // T-041増分7(殿裁定P-033=案2): 操作対象端点(始点/終点)をトグルする。表示は
                // ステータスバーのSelectedEndpointDisplayバインディングで自動反映される。
                _viewModel.ToggleSelectedEndpoint();
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
            case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                NewButton_Click(sender, e);
                e.Handled = true;
                break;
        }
    }

    private bool IsCanvasFocused() => IsWithin(LadderCanvasHost, Keyboard.FocusedElement as DependencyObject);

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
        if (isKeyboardOrigin)
            (sender as UIElement)?.Focus();
        else
            FocusCanvas();
        _toolButtonKeyboardClickSource = null;
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
        if (_viewModel.IsSelectedCellOccupied())
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
        PlacementPartComboBox.SelectedItem = _viewModel.PartPalette.SelectionEntries
            .FirstOrDefault(e => e.Definition.Id == initialEntry.Definition.Id && e.IsOr == isOr)
            ?? _viewModel.PartPalette.SelectionEntries.FirstOrDefault(e => e.Definition.Id == initialEntry.Definition.Id)
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
}