using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;

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

    // T-064: 画像のドラッグ(移動)・リサイズ(ハンドル)のしきい値判定用状態(他ドラッグ系と同型)。
    private Point _imageDragPressPositionDip;
    private bool _imageDragStarted;
    private bool _imageDragConsumedByEscape;
    private Point _imageResizePressPositionDip;
    private bool _imageResizeStarted;
    private bool _imageResizeConsumedByEscape;

    // T-088: 基本図形(Element)ドラッグの同種の状態(画像ドラッグと同型)。
    private Point _elementDragPressPositionDip;
    private bool _elementDragStarted;
    private bool _elementDragConsumedByEscape;

    // T-067(2): GroupFrame(枠)ドラッグの同種の状態(要素ドラッグと同型)。
    private Point _frameDragPressPositionDip;
    private bool _frameDragStarted;
    private bool _frameDragConsumedByEscape;

    // T-067(3): GroupFrame新規作成(マウスドラッグ)中のEscape消費フラグ。しきい値判定は不要
    // (クリックのみでも1x1の枠として有効なため、他ドラッグ系と異なり*Started相当の状態は持たない)。
    private bool _frameCreateDragConsumedByEscape;

    // T-061第三歩: テストモード中、押しボタンのモーメンタリ動作用に押下中のデバイス名を保持する
    // (MouseUp/LostMouseCaptureでOFFに戻す、他ドラッグ系のView状態保持と同じ設計)。
    private string? _testModePressedDevice;

    // T-061修正(A-1確認事項1、殿裁定=Enterキーをテスト通電操作として新規結線): マウス側の
    // _testModePressedDeviceとは別に持つ(マウスとキーボードが同時に別要素を操作する可能性への
    // 安全側、責務分離)。PreviewKeyUpでOFFに戻す。
    private string? _testModeEnterPressedDevice;

    // T-061第五歩: 実時間タイマパネル(GuiEcad StartRealtimeTimer/StopRealtimeTimer/OnRealtimeTick
    // 踏襲)。DispatcherTimer/StopwatchともWPF標準機構のためView層に持たせる(VM層はUIタイマーに
    // 依存させない既存方針)。
    private DispatcherTimer? _realtimeTimer;
    private readonly System.Diagnostics.Stopwatch _realtimeClock = new();
    private long _lastTickMs;

    // T-082: シートナビゲーション(SheetNavList)のドラッグ&ドロップ並び替え用状態。キャンバス要素の
    // ドラッグ(マウスキャプチャ方式)とは対象が異なりWPFネイティブDragDrop APIを使う(Explore調査で
    // 既存流用パターン無しと確認済み)。
    private Point _sheetDragStartPoint;
    // T-118: ListBoxItem.DataContext・DragDropペイロードともSheetListItem型(旧Sheet型)に変更。
    private ViewModels.SheetListItem? _sheetDragSource;
    private ListBoxItem? _sheetDragSourceContainer;
    private Adorner? _sheetReorderAdorner;

    // T-058増分1(殿裁定): 誤操作でパネルがフロート化/オートハイド化しキーボード・マウスいずれでも
    // 復旧不能になる致命的UXが忍者実機確認で確定したため、全パネル共通のレイアウトリセット機能
    // (Ctrl+Alt+R)を新設する。起動直後の既定レイアウトをXmlLayoutSerializerで文字列として保持し、
    // リセット時にDeserializeし直す(PoC実証済み手法の流用)。
    private readonly Dictionary<string, object?> _dockingContentRegistry = new();
    // T-110増分1(家老采配2026-07-22、B-3): 単一MainDockingManagerへの統合に伴い、Manager単位の
    // Dictionaryを単一値へ縮退する。
    private string? _defaultDockingLayoutXml;
    // 家老采配2026-07-19(T-099(c)復旧作業で発覚、%AppData%配下の永続化レイアウトXML破損対策):
    // 「本来存在すべきContentIdの集合」をXAML初期状態(RegisterDockingContents呼出時点)から
    // キャプチャしておく。HasExpectedContentでの破損検出に使う。
    private HashSet<string> _expectedContentIds = new();

    // T-058増分4(殿裁定=保存タイミング両方・保存先アプリ共通設定): 明示保存済みの既定レイアウトを
    // %AppData%配下へ単一ファイルとして永続化する。_defaultDockingLayoutXml
    // (起動直後にキャプチャする出荷時ハードコード既定、メモリ上・不変)とは独立した別層であり、
    // Ctrl+Alt+Rはファイルが存在すればそちらを優先する(4-4節)。
    private string DockingLayoutDirectory =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ecad2", "docking-layout");

    // T-110増分1(家老采配2026-07-22、B-1): 4分割時代の4分岐(GetDockingLayoutFileName)を単一
    // ファイル名へ縮退。旧4ファイル(left-palette.xml等)は放置する(rm禁止・裁3=移行ロジック無し、
    // 新ファイル名は旧ファイルを参照しないため既定フォールバックで無害)。
    internal const string DockingLayoutFileName = "main-layout.xml";

    private string DockingLayoutFilePath =>
        System.IO.Path.Combine(DockingLayoutDirectory, DockingLayoutFileName);

    // 殿実機確認で発覚(重要): フロート化したパネル自体にフォーカスがある間はメインウィンドウの
    // PreviewKeyDownが発火せず復旧不能のままだった——AvalonDockはフロート化したLayoutAnchorableを
    // 別のWindowインスタンス(独自のフォーカス・イベントツリー)として生成するため。
    // EventManager.RegisterClassHandlerでアプリケーション内の全Windowインスタンス
    // (メインウィンドウ・AvalonDockフロートウィンドウ問わず)をクラスハンドラとして捕捉することで、
    // どこにフォーカスがあってもCtrl+Alt+R/Sが機能するようにする。
    // T-058増分4: Ctrl+Alt+S(現在のレイアウトを既定として保存)もCtrl+Alt+Rと同じ理由で
    // フロートウィンドウ上から機能させる必要があるため、同一ハンドラ内でキー判定を分岐する。
    static MainWindow()
    {
        EventManager.RegisterClassHandler(typeof(Window), PreviewKeyDownEvent, new KeyEventHandler(OnGlobalDockingLayoutShortcut));
    }

    private static void OnGlobalDockingLayoutShortcut(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Alt)) return;
        if (e.Key != Key.R && e.Key != Key.S) return;
        // 隠密静的レビュー指摘(CONFIRMED): typeof(Window)クラスハンドラはモーダルダイアログ
        // (AddSheetDialog等)も捕捉してしまい、ダイアログ表示中にCtrl+Alt+Rを押すと裏で
        // メインウィンドウのレイアウトが無言リセットされていた。メインウィンドウ自身、または
        // AvalonDockが生成するフロートウィンドウ(LayoutFloatingWindowControl派生)からの
        // イベントのみを対象とし、それ以外(モーダルダイアログ)は無視する。
        if (sender is not (MainWindow or AvalonDock.Controls.LayoutFloatingWindowControl)) return;
        if (Application.Current.MainWindow is not MainWindow mainWindow) return;

        if (e.Key == Key.R) mainWindow.ResetDockingLayoutToDefault();
        else mainWindow.SaveDockingLayoutAsDefault();
        e.Handled = true;
    }

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
        // T-083増分1追加修正(家老采配2026-07-16): 起動直後(トグル未操作、IsDarkMode既定値=false)
        // でもDockingManagerがVS2013 Lightテーマ適用済みの状態にしておく(既存意匠統一の帰結)。
        ApplyDockingManagerThemes(_viewModel.IsDarkMode);
        RegisterDockingContents();
        SerializeDefaultDockingLayouts();
        // T-058増分4: 出荷時ハードコード既定(直前のSerializeDefaultDockingLayouts、不変)を必ず
        // キャプチャした後に、保存済みファイルがあれば読み込んで適用する(順序が重要、設計叩き台3-1節)。
        LoadDockingLayoutFromFileIfExists();
        // 忍者実機確認で発覚(往復2周目): LayoutAnchorable(LayoutContent→LayoutElement:DependencyObject)
        // はFrameworkElementではなくWPFのDataContext継承(Visual/Logical Tree経由)の対象外のため、
        // Title="{Binding Find.IsVisible, ...}"は解決されず完全に空白になっていた(GitHub一次ソース
        // 確認済み、家老経由の追加検証=DataGrid等Content内部のBindingは正常でTitleのみ機能せず、で
        // 裏付け済み)。BindingではなくFind.PropertyChangedを購読しTitleを直接更新する方式に切替える。
        _viewModel.Find.PropertyChanged += Find_PropertyChanged;
        UpdateOutputPanelTitle();
        UpdateRightPanelBottomTitle();
        // T-110増分1(家老采配2026-07-22、A-3=候補a確定): T-099(c)案Y(ContentDockingをCancelし
        // ハードコード既定XMLへDeserializeする機構)は撤去する。増分0のPoCで標準Dock()を5周検証し
        // タブ自己複製・縦長化・空白化いずれも再現しなかったため、統合トポロジではバグの前提
        // (PreviousContainer解決の文脈)自体が変わり再現しないと確定した(忍者実機確認
        // docs/ecad2-t110-poc-verification-ninja.md)。ContentDockingイベント購読・
        // ResetPlacementToolBarLayoutToDefaultは撤去(標準Dock()に任せる)。
        // T-099(c)調査5(b): メニュー「フローティング」経由のFloat()はドラッグ経路と異なり
        // FloatingLeft/Topの位置補正(ドラッグ経路のInternalOnActivated相当)を経ないため、既定
        // 0.0のままプライマリモニタ原点(0,0)にフロートウィンドウが生成される(一次ソース
        // DockingManager.cs:3281-3287で確認済み)。ContentFloatingイベント(Float実行前、一次ソース
        // DockingManager.cs:2313-2328)で配置ツールバー自身の現在スクリーン座標を設定しておくことで、
        // Float()内のウィンドウ生成がこの値をそのまま使う。T-110増分1: 単一Manager化に伴い
        // MainDockingManagerへ購読先変更(ロジック自体はContentId=="PlacementToolBar"チェック済みの
        // ため無改修)。
        MainDockingManager.ContentFloating += PlacementToolBarDockingManager_ContentFloating;
        // T-103 PoC(家老采配2026-07-20、侍提案、docs/todo.md T-103節): AvalonDock標準の
        // OverlayWindow/DropTarget(位置ズレバグ)に依存しない独自ドロップ枠方式。フロートウィンドウ
        // 生成直後(Show前)に発火するこのイベントでLoaded後フックの登録を仕込む
        // (docs/ecad2-t103-drag-message-path-and-guard-survey-samurai.md参照)。
        // T-110増分1(隠密プランC-1、着手前チェックC-1): 単一Manager化で本イベントは全ペインの
        // フロートで発火するようになるため、ハンドラ内のContentIdフィルタ(isPlacementToolBar判定、
        // 既存実装済み)が防御として機能する。購読先をMainDockingManagerへ変更。
        MainDockingManager.LayoutFloatingWindowControlCreated += PlacementToolBarDockingManager_LayoutFloatingWindowControlCreated;
        // T-104増分1 DoD(4)対策・案2(家老采配2026-07-20、隠密設計、往復2周目=計画的深掘り):
        // 1段階目(暗黙的StyleでLayoutAnchorSideControl自体をFocusable=False化)は不十分と判明
        // ——Focusableはローカル値・非継承プロパティのため、AnchorSideTemplate内の名前なし
        // ItemsControl(一次ソースgeneric.xaml:382、AvalonDock標準テンプレート)には伝播しない
        // (WPF仕様通りの帰結)。VisualTreeHelperでこのItemsControlインスタンスを直接検索し
        // Focusable/IsTabStopを設定する、より確実性の高い方式へ切替える。
        // T-110増分1(隠密プラン§3.5、望ましい方向): 単一Manager化によりAutoHideサイド領域は
        // ウィンドウ全域に及ぶため、本対処が全ペイン共通で有効になる。
        MainDockingManager.Loaded += PlacementToolBarDockingManager_Loaded;
    }

    private void PlacementToolBarDockingManager_Loaded(object sender, RoutedEventArgs e)
    {
        DisableFocusOnAutoHideSideItemsControl(MainDockingManager.LeftSidePanel);
        DisableFocusOnAutoHideSideItemsControl(MainDockingManager.TopSidePanel);
        DisableFocusOnAutoHideSideItemsControl(MainDockingManager.RightSidePanel);
        DisableFocusOnAutoHideSideItemsControl(MainDockingManager.BottomSidePanel);

        // T-110増分1差し戻し(忍者実機確認(2)NG、家老采配2026-07-22): XAML宣言の
        // SelectedContentIndex="1"(増分0のPoCでも同一現象を確認済み、E-1)が起動時に反映されず
        // 「基本機能」(index0)が選択された状態で起動していた。Loaded後にContentIdベースで
        // 対象LayoutAnchorableを検索しIsActiveを明示設定する(インデックス依存を避ける、
        // AvalonDock標準のアクティブ化機構)。
        var placementToolBarAnchorable = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == "PlacementToolBar");
        if (placementToolBarAnchorable is not null)
            placementToolBarAnchorable.IsActive = true;
    }

    private static void DisableFocusOnAutoHideSideItemsControl(AvalonDock.Controls.LayoutAnchorSideControl? sideControl)
    {
        if (sideControl == null) return;
        sideControl.ApplyTemplate();
        if (FindVisualChild<ItemsControl>(sideControl) is not ItemsControl itemsControl) return;
        itemsControl.Focusable = false;
        KeyboardNavigation.SetIsTabStop(itemsControl, false);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            if (FindVisualChild<T>(child) is T found) return found;
        }
        return null;
    }

    // T-110増分1(家老采配2026-07-22、A-3=候補a確定): 旧PlacementToolBarDockingManager_ContentDocking・
    // ResetPlacementToolBarLayoutToDefault(T-099(c)案Y、ContentDockingをCancelしハードコード既定XML
    // Deserializeで復帰する機構)は撤去した。増分0のPoCで標準Dock()を5周検証しタブ自己複製・
    // 縦長化・空白化いずれも再現しなかったため(忍者実機確認docs/ecad2-t110-poc-verification-
    // ninja.md)、統合トポロジではバグの前提(PreviousContainer解決の文脈)自体が変わり再現しないと
    // 確定した。撤去に伴い_defaultDockingLayoutXmlByManagerへの旧参照(PlacementToolBarDockingManager
    // キー)も消滅、孤立参照は残らない(隠密着手前チェックA-3確認事項)。

    // T-103 PoC(家老采配2026-07-20、侍提案): 独自ドロップ枠のヒットテスト用フック。
    // フロートウィンドウはドラッグのたび新規生成されるため、このフィールドは常に「現在フロート中の
    // 配置ツールバーウィンドウに紐づくフック」1つのみを保持する(複数同時フロートは構成上あり得ない)。
    private HwndSourceHook? _placementToolBarFloatingWindowHook;

    private const int WM_EXITSIZEMOVE = 0x0232;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Win32Point point);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    // T-103 PoC: フロートウィンドウ生成直後(Show前)に発火。ここでLoaded後フックの登録を仕込む
    // (docs/ecad2-t103-drag-message-path-and-guard-survey-samurai.md「二重実行ガードの設計確定」参照
    // ——AvalonDock自身のOnLoaded→AddHookより後にこちら側がAddHookすれば、HwndSourceのフック走査は
    // LIFOのためWM_EXITSIZEMOVE受信時にこちらが先に呼ばれ、handled=trueで返すとAvalonDock標準の
    // DragService.Drop()を丸ごとスキップできる)。
    private void PlacementToolBarDockingManager_LayoutFloatingWindowControlCreated(object? sender, LayoutFloatingWindowControlCreatedEventArgs e)
    {
        var fwc = e.LayoutFloatingWindowControl;
        var isPlacementToolBar = fwc.Model.Descendents().OfType<LayoutAnchorable>().Any(a => a.ContentId == "PlacementToolBar");
        if (!isPlacementToolBar) return;

        PlacementToolBarDropZoneOverlay.Visibility = Visibility.Visible;
        fwc.Loaded += PlacementToolBarFloatingWindow_Loaded;
        fwc.Closed += PlacementToolBarFloatingWindow_Closed;
    }

    private void PlacementToolBarFloatingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var fwc = (Window)sender;
        fwc.Loaded -= PlacementToolBarFloatingWindow_Loaded;
        if (PresentationSource.FromVisual(fwc) is not HwndSource hwndSource) return;
        _placementToolBarFloatingWindowHook = PlacementToolBarFloatingWindowFilterMessage;
        hwndSource.AddHook(_placementToolBarFloatingWindowHook);
    }

    private void PlacementToolBarFloatingWindow_Closed(object? sender, EventArgs e)
    {
        var fwc = (Window)sender!;
        fwc.Closed -= PlacementToolBarFloatingWindow_Closed;
        PlacementToolBarDropZoneOverlay.Visibility = Visibility.Collapsed;
        if (_placementToolBarFloatingWindowHook == null) return;
        if (PresentationSource.FromVisual(fwc) is HwndSource hwndSource)
            hwndSource.RemoveHook(_placementToolBarFloatingWindowHook);
        _placementToolBarFloatingWindowHook = null;
    }

    // T-103 PoC: GetCursorPos(物理ピクセル座標)と枠(PlacementToolBarDropZoneOverlay)のスクリーン
    // 矩形(PointToScreenも物理ピクセル座標を返す、既存PlacementToolBarDockingManager_ContentFloating
    // と同じ座標系の扱い)を突き合わせる自前ヒットテスト。ActualWidth/HeightはDIPのためDPI倍率で
    // 物理ピクセルへ変換する。
    private IntPtr PlacementToolBarFloatingWindowFilterMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_EXITSIZEMOVE) return IntPtr.Zero;
        if (!GetCursorPos(out var cursor)) return IntPtr.Zero;

        var topLeft = PlacementToolBarDropZoneOverlay.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(PlacementToolBarDropZoneOverlay);
        var dropZone = new Rect(
            topLeft.X, topLeft.Y,
            PlacementToolBarDropZoneOverlay.ActualWidth * dpi.DpiScaleX,
            PlacementToolBarDropZoneOverlay.ActualHeight * dpi.DpiScaleY);

        if (!dropZone.Contains(cursor.X, cursor.Y)) return IntPtr.Zero;

        handled = true;
        // T-110増分1対策(家老采配2026-07-22、忍者所見=十字型ドロップターゲット残留の間欠事象):
        // handled=trueによりAvalonDock自身のWM_EXITSIZEMOVEハンドラ(=DragService.Drop()内の
        // _currentHost.HideOverlayWindow()、一次ソースDragService.cs:207-238)が丸ごとスキップ
        // されるため、AvalonDock標準のOverlayWindow後始末に頼らず、T-103側で確実に後始末する。
        PlacementToolBarDropZoneOverlay.Visibility = Visibility.Collapsed;
        // T-110増分1(A-3=候補a確定に伴う派生対応): ハードコード既定XMLへの強制Deserialize
        // (ResetPlacementToolBarLayoutToDefault、撤去済み)ではなく、標準のDock()を呼ぶ形に変更する。
        // 「標準Dock()に任せる」方針(A-3)と一貫させるため。
        var anchorableToDock = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == "PlacementToolBar");
        anchorableToDock?.Dock();
        return IntPtr.Zero;
    }

    private void PlacementToolBarDockingManager_ContentFloating(object? sender, ContentFloatingEventArgs e)
    {
        if (e.Content.ContentId != "PlacementToolBar") return;
        // 隠密レビュー指摘: PointToScreenは物理ピクセル座標を返すが、FloatingLeft/TopはDIP消費の
        // ため、DPI拡大率が100%でない環境ではズレる。VisualTreeHelper.GetDpiのDpiScaleX/Yで
        // 物理ピクセルからDIPへ変換する。
        var topLeft = MainDockingManager.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(MainDockingManager);
        e.Content.FloatingLeft = topLeft.X / dpi.DpiScaleX;
        e.Content.FloatingTop = topLeft.Y / dpi.DpiScaleY;
    }

    private void Find_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.FindViewModel.IsVisible))
            UpdateOutputPanelTitle();
    }

    private void UpdateOutputPanelTitle()
    {
        var outputAnchorable = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == "OutputPanel");
        if (outputAnchorable != null)
        {
            outputAnchorable.Title = _viewModel.Find.IsVisible ? "検索結果" : "出力";
        }
    }

    // T-058増分3: 増分2のUpdateOutputPanelTitleと同型。右パネル下段はIsPartSelectionVisible
    // (Tool.Modeから算出、Tool.setterでOnPropertyChanged明示発火)の変化に応じ
    // 「プロパティ」⇔「部品選択」を直接更新する(AvalonDockのオフツリー構造ゆえBinding不可)。
    private void UpdateRightPanelBottomTitle()
    {
        var bottomAnchorable = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == "RightPanelBottom");
        if (bottomAnchorable != null)
        {
            bottomAnchorable.Title = _viewModel.IsPartSelectionVisible ? "部品選択" : "プロパティ";
        }
    }

    // T-110増分3(裁5付帯裁定、家老采配2026-07-22、設計書§3.1-4): メニューを開くたびに実際の
    // AutoHide状態(LayoutAnchorable.IsAutoHidden)を4項目へ反映する。変更通知の有無が未確認のため
    // Bindingではなく都度評価する設計(通知が無くても正しく動く、通知があっても害はない)。
    private void AutoHideSubmenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        AutoHideLeftPaletteMenuItem.IsChecked = IsPaneAutoHidden("LeftPalette");
        AutoHideDeviceTableMenuItem.IsChecked = IsPaneAutoHidden("DeviceTable");
        AutoHideRightPanelBottomMenuItem.IsChecked = IsPaneAutoHidden("RightPanelBottom");
        AutoHideOutputPanelMenuItem.IsChecked = IsPaneAutoHidden("OutputPanel");
    }

    private bool IsPaneAutoHidden(string contentId)
    {
        var anchorable = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == contentId);
        return anchorable?.IsAutoHidden ?? false;
    }

    // T-110増分3(裁5付帯裁定、家老采配2026-07-22、設計書§3.1-1/2): 発動・復帰とも同じ項目のトグル。
    // 対象取得はContentId検索(x:Name参照は使わない、レイアウトDeserializeでモデルツリーが丸ごと
    // 差し替わるT-099(c)の教訓)。ToggleAutoHide()はpublic、DockingManager.ExecuteAutoHideCommand
    // (internal)の中身も同メソッド呼出のみと一次ソース確認済み(設計書§3.1-1)。
    private void AutoHideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string contentId }) return;
        var anchorable = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == contentId);
        anchorable?.ToggleAutoHide();
    }

    // T-110増分1(家老采配2026-07-22、B-3): 単一MainDockingManagerへの統合に伴いforeachを撤去。
    // B-2: LayoutDocument走査により、キャンバスDocumentのContentId("Canvas")も期待集合へ
    // 自然に含まれる(既存のLayoutAnchorable/LayoutDocument両方の走査ロジックを維持するだけで足りる)。
    private void RegisterDockingContents()
    {
        var expectedIds = new HashSet<string>();
        foreach (var anchorable in MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>())
        {
            _dockingContentRegistry[anchorable.ContentId] = anchorable.Content;
            expectedIds.Add(anchorable.ContentId);
        }
        foreach (var document in MainDockingManager.Layout.Descendents().OfType<LayoutDocument>())
        {
            _dockingContentRegistry[document.ContentId] = document.Content;
            expectedIds.Add(document.ContentId);
        }
        _expectedContentIds = expectedIds;
    }

    // 家老采配2026-07-19(読込側防御・本丸): Deserialize自体は成功してもContentId要素はあるが
    // 実体(Content)が欠落した壊れたXMLを検出する(RebindDockingContentが_dockingContentRegistryに
    // 該当ContentIdを見つけられずContentがnullのまま残るケース、およびXMLからLayoutContent要素
    // 自体が丸ごと消えているケースの両方を拾う)。今回のT-099(c)復旧作業で発覚した%AppData%配下
    // 永続化XML汚染(配置ツールの実体が完全欠落)の再発防止。
    private bool HasExpectedContent()
    {
        var presentIds = MainDockingManager.Layout.Descendents().OfType<LayoutContent>()
            .Where(c => c.Content != null && c.ContentId != null)
            .Select(c => c.ContentId)
            .ToHashSet();
        return _expectedContentIds.All(id => presentIds.Contains(id));
    }

    private void SerializeDefaultDockingLayouts()
    {
        var serializer = new XmlLayoutSerializer(MainDockingManager);
        using var writer = new StringWriter();
        serializer.Serialize(writer);
        _defaultDockingLayoutXml = writer.ToString();
    }

    // T-058増分4: ResetDockingLayoutToDefault()の既存ラムダと同一ロジックのため共通化する
    // (増分3隠密指摘2「rule of threeではあるが完全一致重複は避ける」と同型判断)。
    // LoadDockingLayoutFromFileIfExists()とも共有する。
    private void RebindDockingContent(object? sender, LayoutSerializationCallbackEventArgs args)
    {
        if (args.Model.ContentId != null && _dockingContentRegistry.TryGetValue(args.Model.ContentId, out var content))
        {
            args.Content = content;
        }
    }

    // T-058増分4(殿裁定=保存タイミング両方): アプリ終了時(Window_Closing)・明示コマンド
    // (SaveDockingLayoutMenuItem_Click)の双方から呼ばれる単一の保存メソッド。書込失敗
    // (権限/容量等)はクラッシュさせずステータスメッセージのみに留める(殿裁定(5)フォールバック)。
    // 家老采配2026-07-19(保存側防御): 実体欠落状態(フロート化処理中の過渡状態等)のレイアウトを
    // 誤って既定として焼き付けてしまうと、今回のT-099(c)復旧作業のような%AppData%汚染の再発源に
    // なる。保存前にHasExpectedContentで検証し、欠落状態なら保存自体をスキップする
    // (読込側防御と対になる二重の備え)。
    private void SaveDockingLayoutAsDefault()
    {
        try
        {
            if (!HasExpectedContent())
            {
                _viewModel.StatusMessage = "パネルレイアウトが不完全な状態のため保存をスキップしました";
                return;
            }
            Directory.CreateDirectory(DockingLayoutDirectory);
            var serializer = new XmlLayoutSerializer(MainDockingManager);
            using var writer = new StreamWriter(DockingLayoutFilePath);
            serializer.Serialize(writer);
            _viewModel.StatusMessage = "現在のパネルレイアウトを既定として保存しました";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _viewModel.StatusMessage = "パネルレイアウトの保存に失敗しました";
        }
    }

    // T-058増分4: 起動時、保存済みファイルがあれば適用する(SerializeDefaultDockingLayouts()の
    // 直後から呼ぶこと、コンストラクタ参照)。ファイル無し/破損いずれもクラッシュさせず、
    // XAML初期状態のまま起動を継続する(殿裁定(5)フォールバック)。
    // 家老裁可(2026-07-15): 破損ファイル等で読込に失敗した場合は沈黙のフォールバックを避け、
    // ステータスメッセージで一言知らせる(GCADのようなバージョン管理を持たない代わりの透明性確保)。
    // 家老采配2026-07-19(読込側防御・本丸): TryReadSavedDockingLayoutXml→TryDeserializeDockingLayout
    // の二段フォールバック構造で、ResetDockingLayoutToDefault()と同じ保護(IO/XML構文/Content実体
    // 欠落の3層)を持たせる。
    // T-110増分1(裁3、殿裁可済み): 旧4ファイル(left-palette.xml等)は新ファイル名
    // (main-layout.xml)からは参照されないため、既存ユーザーは全員ファイル無し扱いとなり
    // XAML初期状態のまま起動する(保存カスタムレイアウト喪失は許容、移行ロジックは作らない)。
    // 隠密静的レビュー指摘(2026-07-22、副次所見・退行): 増分1実装時、ファイル無し(savedXml is
    // null)の場合に即returnせずフォールスルーし、_defaultDockingLayoutXml(=起動直後のXAML初期
    // 状態を自己Serializeしたもの)への無意味なDeserializeを毎回実行してしまっていた。
    // XAML初期状態は起動時点で既に正しく構築済みのため、ファイル無し時は何もしない(旧実装の
    // continue相当)のが正しく、自己参照的な再構築は不要な処理コストでしかない。ガードを復元する。
    private void LoadDockingLayoutFromFileIfExists()
    {
        string? savedXml = TryReadSavedDockingLayoutXml();
        if (savedXml is null) return;
        if (TryDeserializeDockingLayout(savedXml)) return;

        // 破損ファイル等はハードコード既定(XAML初期状態)へ二段フォールバックする。
        if (_defaultDockingLayoutXml is not null)
            TryDeserializeDockingLayout(_defaultDockingLayoutXml);

        // T-104増分2(3)(家老采配2026-07-20、殿裁定=文言変更): 旧文言「保存済みレイアウトの
        // 読込に失敗したため既定で起動しました」は、実際には破損以外にバージョンアップに伴う
        // レイアウト構成変更(今回のタブ新設等)でも同じ経路を通るため、毎回「失敗」という
        // 強い表現でユーザーに不安を与えていた。原因を問わず前向きに伝わる文言へ変更(叩き台、
        // 最終確定は殿)。
        _viewModel.StatusMessage = "レイアウトを既定の状態に更新しました";
    }

    // Ctrl+Alt+Rハンドラから呼ばれる。T-058増分4(殿裁定(4)): 保存済みファイルがあればそちらを
    // 優先し、無ければ従来どおりハードコード既定(_defaultDockingLayoutXml)へ戻す。
    // 隠密静的レビュー指摘(CONFIRMED、severity中〜高、2026-07-15): 保存済みファイルはIO面は
    // TryReadSavedDockingLayoutXmlで保護済みだが、読めても中身のXML構文が壊れているケース
    // (手動編集ミス・アプリクラッシュ時の中途半端な書込み等)ではDeserialize自体が例外を投げ、
    // LoadDockingLayoutFromFileIfExists()と非対称に無防備だった(実機RED実測: 破損XMLで
    // 「予期しないエラー」ダイアログが発生することを確認済み)。TryDeserializeDockingLayoutで
    // Deserialize自体も保護し、失敗時はハードコード既定側へ二段フォールバックする。
    private void ResetDockingLayoutToDefault()
    {
        string? savedXml = TryReadSavedDockingLayoutXml();
        if (savedXml is null || !TryDeserializeDockingLayout(savedXml))
        {
            if (_defaultDockingLayoutXml is not null)
                TryDeserializeDockingLayout(_defaultDockingLayoutXml);
        }
        // T-058増分3隠密静的レビュー指摘1(CONFIRMED、増分2から持ち越しの既存欠陥の複製):
        // Deserialize直後のLayoutAnchorable.Titleは既定レイアウトXML焼き付け時点の初期値
        // ("プロパティ"/"出力")のまま。状況依存中(部品選択モード・検索結果表示中)にリセットすると
        // 中身はVisibilityバインディングにより現在の状況のまま変化しないため、タイトルだけ既定値へ
        // 巻き戻り中身と食い違う。両タイトル同期メソッドを呼び直して整合を回復する。
        UpdateOutputPanelTitle();
        UpdateRightPanelBottomTitle();
        _viewModel.StatusMessage = "パネルレイアウトを既定に戻しました";
    }

    // T-058増分4: 保存済みファイルの読込に失敗(破損等)した場合はnullを返し、呼び出し元で
    // ハードコード既定へフォールバックさせる(殿裁定(5))。
    private string? TryReadSavedDockingLayoutXml()
    {
        if (!File.Exists(DockingLayoutFilePath)) return null;
        try
        {
            return File.ReadAllText(DockingLayoutFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // 隠密静的レビュー指摘対応(2026-07-15): XmlLayoutSerializer.Deserialize自体(XML構文エラー等)
    // をtry-catchで保護する。LayoutSerializationCallbackでContentIdをキーに元のコンテンツを
    // 再バインドする(Deserialize直後は新規生成インスタンスのためContentが失われる、PoC実証済みの対処)。
    private bool TryDeserializeDockingLayout(string xml)
    {
        try
        {
            var serializer = new XmlLayoutSerializer(MainDockingManager);
            serializer.LayoutSerializationCallback += RebindDockingContent;
            using var reader = new StringReader(xml);
            serializer.Deserialize(reader);
            // T-110増分1対策(家老采配2026-07-22、隠密案D第4防御): 5123eb3より前に保存された
            // main-layout.xml等、RootPanelにCanDock属性が無いXMLを読み込むとLayoutPanel.ReadXml
            // は既定値trueへ復元し、XAML側のCanDock="False"が上書き消滅する(「十字型が再発する」
            // という偽の再発を招く)。Deserialize成功直後に強制Falseへ戻すことで無害化する
            // (次回保存時には正規化されたXMLが書き出される)。
            MainDockingManager.Layout.RootPanel.CanDock = false;
            // 家老采配2026-07-19(読込側防御・本丸): Deserialize自体は成功してもContent実体が
            // 欠落した壊れたXMLをここで検出する(HasExpectedContent参照)。
            return HasExpectedContent();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Xml.XmlException)
        {
            return false;
        }
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
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedConnectionDot)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedImage)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.ImageInsertDraftPreview)
            // T-067(1)欠陥修正(忍者実機NG2026-07-18): SelectedFrameが列挙から漏れており、枠の
            // クリック選択直後にハイライトが描画されなかった(他の再描画契機を挟むと現れる)。
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.SelectedFrame)
            || e.PropertyName == nameof(ViewModels.MainWindowViewModel.Mode))
            RedrawCanvas();

        // T-061第五歩: モード遷移に合わせて実時間タイマを開始/停止する(GuiEcad
        // 「_testMode ? StartRealtimeTimer() : StopRealtimeTimer()」踏襲)。
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.Mode))
        {
            if (_viewModel.Mode == ViewModels.AppMode.Test) StartRealtimeTimer();
            else StopRealtimeTimer();
        }

        // T-056: グリッド表示切替。LadderCanvasはカスタムFrameworkElementでDraw()呼び出しが
        // 描画トリガーのため、ShowGridの値をViewへ反映した上で明示的に再描画する。
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsGridVisible))
        {
            LadderCanvasHost.ShowGrid = _viewModel.IsGridVisible;
            RedrawCanvas();
        }

        // T-083 PoC(家老采配2026-07-15): 作図キャンバス色のテーマ切替。IsGridVisibleと同型、
        // LadderCanvasはDraw()呼び出しが描画トリガーのため明示的に再描画する。
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsDarkMode))
        {
            LadderCanvasHost.Theme = _viewModel.IsDarkMode
                ? Ecad2.Rendering.DrawingTheme.Dark
                : Ecad2.Rendering.DrawingTheme.Default;
            RedrawCanvas();

            // T-083増分1(家老采配2026-07-16): AvalonDockドッキングクローム(タブ・タイトルバー等)を
            // VS2013テーマへ連動。4つのDockingManagerへ同型適用のため、取りこぼし防止で共通化する
            // (PR-17と同種の横展開漏れ再発パターン、隠密所見)。
            ApplyDockingManagerThemes(_viewModel.IsDarkMode);

            // T-083増分2(家老采配2026-07-16): UIクローム(メニュー・ツールバー本体・シート0件時
            // キャンバス色等)のテーマ切替。WPF標準のMergedDictionaries差替え方式(新規外部依存なし)。
            ApplyUiChromeTheme(_viewModel.IsDarkMode);

            // T-083新規発見5(家老采配2026-07-17): 部品選択パネルのサムネイルはビットマップ事前
            // レンダリングのためブラシ差替えで追従できず、テーマ切替時に全件再生成する。
            _viewModel.PartPalette.RefreshThumbnails(_viewModel.IsDarkMode
                ? Ecad2.Rendering.DrawingTheme.Dark.Foreground
                : Ecad2.Rendering.DrawingTheme.Default.Foreground);
        }

        // T-058増分3: 右パネル下段タイトルの状況依存切替(UpdateRightPanelBottomTitle参照)。
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsPartSelectionVisible))
            UpdateRightPanelBottomTitle();

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
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsDraggingImage) && !_viewModel.IsDraggingImage)
        {
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _imageDragStarted = false;
            _imageDragConsumedByEscape = false;
        }
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsResizingImage) && !_viewModel.IsResizingImage)
        {
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _imageResizeStarted = false;
            _imageResizeConsumedByEscape = false;
        }
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsDraggingElement) && !_viewModel.IsDraggingElement)
        {
            // T-088: ForceCancelDragElementIfAny(ReplaceDocument等の外部要因)による強制キャンセルに
            // View側の状態(マウスキャプチャ・しきい値・Escape消費フラグ)も追随させる(他ドラッグ系と同型)。
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _elementDragStarted = false;
            _elementDragConsumedByEscape = false;
        }
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.IsDraggingFrame) && !_viewModel.IsDraggingFrame)
        {
            // T-067(2): ForceCancelDragFrameIfAny(SelectedCellのsetter経由等の外部要因)による
            // 強制キャンセルにView側の状態も追随させる(他ドラッグ系と同型)。
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _frameDragStarted = false;
            _frameDragConsumedByEscape = false;
        }
        if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.FrameDraftPreview) && _viewModel.FrameDraftPreview is null)
        {
            // T-067(3): ClearFrameDraftIfAny(CancelResidualDraftForToolSwitch/ReplaceDocument等の
            // 外部要因)による強制クリアにView側のキャプチャ・Escape消費フラグを追随させる。
            // _frameDraftはBegin/Confirm/Cancel全経路で明示的にOnPropertyChangedを発火する設計
            // (IsDraggingXxx系のForceCancel限定とは異なる)ため、通常のMouseUp確定時にも本ブロックが
            // 発火するが、非キャプチャ状態へのReleaseMouseCaptureは無害なため実害はない。
            if (LadderCanvasHost.IsMouseCaptured) LadderCanvasHost.ReleaseMouseCapture();
            _frameCreateDragConsumedByEscape = false;
        }
    }

    // T-061第五歩: テストモード中、タイマ経過を実時間で進める(GuiEcad MainPage.xaml.cs全文移植)。
    private void StartRealtimeTimer()
    {
        _realtimeClock.Restart();
        _lastTickMs = 0;
        _realtimeTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _realtimeTimer.Tick -= OnRealtimeTick;
        _realtimeTimer.Tick += OnRealtimeTick;
        _realtimeTimer.Start();
    }

    private void StopRealtimeTimer()
    {
        _realtimeTimer?.Stop();
        _realtimeClock.Stop();
    }

    private void OnRealtimeTick(object? sender, EventArgs e)
    {
        if (_viewModel.Mode != ViewModels.AppMode.Test || _viewModel.CurrentTestSession is not Ecad2.Simulation.TestSession session)
            return;
        long now = _realtimeClock.ElapsedMilliseconds;
        double dt = (now - _lastTickMs) / 1000.0;
        _lastTickMs = now;
        if (dt <= 0) return;
        session.Tick(dt);
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
                _viewModel.FreeLineDraftPreview, _viewModel.SelectedConnectionDot,
                _viewModel.SelectedImage, _viewModel.ImageInsertDraftPreview, _viewModel.SelectedFrame,
                _viewModel.CurrentTestSession?.State, _viewModel.Document.Devices);
        else
            LadderCanvasHost.Clear();
    }

    // T-110増分1(家老采配2026-07-22、C-3): 単一MainDockingManagerへの統合に伴い、旧4Manager出し
    // 分け(manager identity判定)を撤去する。統合タイトルスタイル(UnifiedAnchorablePaneTitleStyle、
    // Model.ContentId分岐を内包)を1つだけ登録すればよい。
    private void ApplyDockingManagerThemes(bool isDarkMode)
    {
        // T-100(家老采配2026-07-17): VS2013テーマ(Theme設定でMergedDictionaries経由追加される)側の
        // AnchorablePaneTitle暗黙的スタイルはハッチング模様(DragHandleTexture)を含むため、
        // DockingManager.Resourcesへ直接キー登録して優先させる(ローカルエントリはMergedDictionaries
        // より優先解決される)。
        var unifiedAnchorablePaneTitleStyle = (Style)FindResource("UnifiedAnchorablePaneTitleStyle");
        MainDockingManager.Theme = isDarkMode
            ? new AvalonDock.Themes.Vs2013DarkTheme()
            : new AvalonDock.Themes.Vs2013LightTheme();
        MainDockingManager.Resources[typeof(AvalonDock.Controls.AnchorablePaneTitle)] = unifiedAnchorablePaneTitleStyle;

        // T-110増分3(裁5=案A、家老采配2026-07-22、設計書§2.4): 単一ペインタイトルバー完全非表示。
        // UnifiedAnchorablePaneTitleStyleと同じ暗黙スタイル登録方式・同じ場所に揃える
        // (テーマ切替時も同一経路で再登録、スタイル自体はDynamicResource参照ゆえテーマ非依存)。
        var titleBarHiddenAnchorableControlStyle = (Style)FindResource("TitleBarHiddenAnchorableControlStyle");
        MainDockingManager.Resources[typeof(AvalonDock.Controls.LayoutAnchorableControl)] = titleBarHiddenAnchorableControlStyle;
    }

    // T-083増分2(層C=UIクローム基盤): Application.Resourcesのテーマ辞書(Theme.Light.xaml/
    // Theme.Dark.xaml)を差し替える。App.xamlに既定でTheme.Light.xamlが1件マージ済みのため、
    // 既存の1件を除去してから新テーマを追加する(WPF標準の伝統的テーマ切替手法)。
    private void ApplyUiChromeTheme(bool isDarkMode)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeUri = new Uri(isDarkMode ? "Themes/Theme.Dark.xaml" : "Themes/Theme.Light.xaml", UriKind.Relative);
        var newTheme = new ResourceDictionary { Source = themeUri };

        var existing = dictionaries.FirstOrDefault(d =>
            d.Source is not null && (d.Source.OriginalString.EndsWith("Theme.Light.xaml") || d.Source.OriginalString.EndsWith("Theme.Dark.xaml")));
        if (existing is not null) dictionaries.Remove(existing);

        dictionaries.Add(newTheme);
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

    // T-107: 機器コメント編集(DeviceNameBoxと同型のExplicit確定)。CommitDeviceNameEditが
    // CommentBoxも併せて確定するため、専用のCommitメソッドは設けず既存を呼ぶ。
    private void CommentBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitDeviceNameEdit();

    private void CommentBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitDeviceNameEdit();
    }

    // T-086: ノッチ位置編集(DeviceNameBoxと同型のExplicit確定)。CommitDeviceNameEditが
    // NotchPositionBoxも併せて確定するため、専用のCommitメソッドは設けず既存を呼ぶ。
    private void NotchPositionBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitDeviceNameEdit();

    private void NotchPositionBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitDeviceNameEdit();
    }

    // T-085: ランプ色編集(DeviceNameBox/NotchPositionBoxと同型のExplicit確定)。CommitDeviceNameEditが
    // LampColorBoxも併せて確定するため、専用のCommitメソッドは設けず既存を呼ぶ。
    private void LampColorBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitDeviceNameEdit();

    private void LampColorBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitDeviceNameEdit();
    }

    // T-085: ランプ色クリアボタン。テキストを空にしてから即時確定する(DoD=クリアボタンつき)。
    private void LampColorClearButton_Click(object sender, RoutedEventArgs e)
    {
        LampColorBox.Text = "";
        CommitDeviceNameEdit();
    }

    // T-096: タイマ設定時間編集(DeviceNameBox/NotchPositionBox/LampColorBoxと同型のExplicit確定)。
    // CommitDeviceNameEditがSetpointBoxも併せて確定するため、専用のCommitメソッドは設けず既存を呼ぶ。
    private void SetpointBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitDeviceNameEdit();

    private void SetpointBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitDeviceNameEdit();
    }

    // T-097: ラベル高さオフセット編集(DeviceNameBox/NotchPositionBox/LampColorBox/SetpointBoxと同型の
    // Explicit確定)。CommitDeviceNameEditがLabelDyBoxも併せて確定するため、専用のCommitメソッドは
    // 設けず既存を呼ぶ。
    private void LabelDyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitDeviceNameEdit();

    private void LabelDyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitDeviceNameEdit();
    }

    // T-049(殿裁定): デバイス名編集中、フォーカスを保持したままCtrl+S/N/O・ウィンドウクローズが
    // 実行されると、UpdateSourceTrigger=Explicit(上記コメント参照)ゆえLostKeyboardFocus/Enterの
    // いずれも発火せず、編集内容がサイレントに保存漏れ/無確認破棄されうる(P-013)。保存・破棄判定
    // (SaveDocument/ConfirmDiscardIfDirty)の入口で必ず本メソッドを呼び、確定してから判定する
    // (確認ダイアログは挟まない、殿裁定)。SelectedElementDeviceNameのsetterは同値なら早期returnする
    // ため(値変更が無い呼び出しは無害)、常時呼んでよい。
    // T-086: NotchPositionBoxも同型のUpdateSourceTrigger=Explicit入力欄のため、CommitDeviceNameEditの
    // 呼び出し元全箇所(P-071型の呼び忘れ再発防止)で併せて確定させる。NotchPositionBoxは
    // IsSelectedElementSelectSwitch=falseの間Bindingが無効な場合があるため?.で安全に扱う。
    // T-085: LampColorBoxも同型(IsSelectedElementLamp=falseの間は?.で安全に扱う)。
    // T-096: SetpointBoxも同型(IsSelectedElementTimerRelated=falseの間は?.で安全に扱う)。
    // T-097: LabelDyBoxも同型(全要素種別共通ゆえ種別限定Visibilityは無いが、HasSelectedElement=false
    // の間はBinding自体が無効なため同様に?.で安全に扱う)。
    // T-107: CommentBoxも同型(LabelDyBoxと同じく全要素種別共通)。
    private void CommitDeviceNameEdit()
    {
        DeviceNameBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        NotchPositionBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        LampColorBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        SetpointBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        LabelDyBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        CommentBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
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

    // T-077増分1(家老采配2026-07-21、隠密プランdocs/ecad2-t077-plan-onmitsu.md 2節): ヘルプ→使い方、
    // F1キーとも共通(Window_PreviewKeyDown参照)。プロジェクト初の非モーダルWindow(Show()、
    // ShowDialog()ではない)。既存インスタンスがあれば再度開かずActivate()のみ行う多重起動防止。
    private Views.UsageWindow? _usageWindow;
    private void ShowUsageWindow()
    {
        if (_usageWindow is null)
        {
            _usageWindow = new Views.UsageWindow { Owner = this };
            _usageWindow.Closed += (_, _) => _usageWindow = null;
            _usageWindow.Show();
        }
        else
        {
            _usageWindow.Activate();
        }
    }
    private void UsageMenuItem_Click(object sender, RoutedEventArgs e) => ShowUsageWindow();

    // T-058増分4(殿裁定=保存タイミング両方の1つ、明示コマンド)。表示メニュー・Ctrl+Alt+S共通。
    private void SaveDockingLayoutMenuItem_Click(object sender, RoutedEventArgs e) => SaveDockingLayoutAsDefault();

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

    // T-114(P-081対処、隠密所見2026-07-14): 機器表「型式」列がCanEditDiagram未ガードのまま
    // テストモード中も編集可能だった見落とし。既存の機器名編集欄(DeviceNameBox.IsEnabled)・
    // 画像挿入メニュー(IsEnabled)と同種のガードを、DataGridColumnはVisual Tree外でDataContextを
    // 継承しないためBeginningEditイベントで代替する。
    private void DeviceTableGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (!_viewModel.CanEditDiagram) e.Cancel = true;
    }

    // シート名変更ボタン。ダイアログ表示自体はView側の責務のためcode-behindで行い、結果の反映のみ
    // ViewModelのRenameCommandへ委譲する。
    private void RenameSheetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SheetNavigation.SelectedSheet is not ViewModels.SheetListItem sheetItem) return;

        var dialog = new Views.RenameDialog(sheetItem.Name) { Owner = this };
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
        if (_viewModel.SheetNavigation.SelectedSheet is not ViewModels.SheetListItem sheetItem) return;

        var sheet = sheetItem.Sheet;
        var dialog = new Views.SheetSettingsDialog(sheet.Grid.Rows, sheet.Bus.LeftName, sheet.Bus.RightName) { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.UpdateSheetSettingsCommand.Execute(new ViewModels.MainWindowViewModel.SheetSettings(dialog.Rows, dialog.LeftName, dialog.RightName));
    }

    private const string GcadFileFilter = "GCADファイル (*.gcad)|*.gcad";
    // T-064(殿裁定=jpg等も追加、WPF BitmapImage標準機能で対応可): bmp/png/jpg/jpeg/gif。
    private const string ImageFileFilter = "画像ファイル (*.bmp;*.png;*.jpg;*.jpeg;*.gif)|*.bmp;*.png;*.jpg;*.jpeg;*.gif";

    // T-064(殿裁定): 挿入トリガー=メニューのみ。ファイル選択完了後、配置待機モード(2段階操作、
    // 既存の記入中ドラフトと同型の操作感、殿裁定「案A」)へ入る。実ピクセルサイズをmm換算し、
    // 長辺120mm超ならアスペクト比を維持して縮小する(GuiEcad踏襲)。開始位置はメニュー操作直後の
    // ため暫定的にページ左上(0,0)を起点とし、以降のホバー追従で確定前に調整する。
    private void InsertImageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = ImageFileFilter };
        if (dialog.ShowDialog(this) != true) return;

        var (widthMm, heightMm) = CalculateInitialImageSizeMm(dialog.FileName);
        _viewModel.BeginImageInsertDraft(dialog.FileName, widthMm, heightMm, 0, 0);
        _viewModel.StatusMessage = "マウスで配置位置を決め、クリックで確定(Escで取消)";
    }

    // T-064: 画像の実ピクセルサイズをmm換算し、長辺が120mm超ならアスペクト比を維持して縮小する
    // (GuiEcad踏襲)。DPI情報が取得できない場合(0以下)は96DPI(WPF既定)を仮定する。
    private static (double WidthMm, double HeightMm) CalculateInitialImageSizeMm(string filePath)
    {
        const double MaxLongSideMm = 120.0;
        const double MmPerInch = 25.4;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.EndInit();

        double dpiX = bitmap.DpiX > 0 ? bitmap.DpiX : 96.0;
        double dpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 96.0;
        double widthMm = bitmap.PixelWidth / dpiX * MmPerInch;
        double heightMm = bitmap.PixelHeight / dpiY * MmPerInch;

        double longSide = Math.Max(widthMm, heightMm);
        if (longSide > MaxLongSideMm)
        {
            double scale = MaxLongSideMm / longSide;
            widthMm *= scale;
            heightMm *= scale;
        }
        return (widthMm, heightMm);
    }

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

    // T-070: 検索バーを閉じる(閉じるボタン)。FindViewModel.IsVisibleのsetterがQueryをクリアする。
    private void FindCloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Find.IsVisible = false;
        FocusCanvas();
    }

    // T-070: 検索ボックス内のEnter(次へ、GuiEcad踏襲)。Escは親のWindow_PreviewKeyDown(Tunnelingで
    // 本ハンドラより先に発火)がFindBar表示中の最優先層として処理するため、ここでは扱わない。
    private void FindQueryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_viewModel.Find.NextCommand.CanExecute(null)) _viewModel.Find.NextCommand.Execute(null);
            e.Handled = true;
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
        if (!ConfirmDiscardIfDirty())
        {
            e.Cancel = true;
            return;
        }
        // T-058増分4(殿裁定=保存タイミング両方の1つ、アプリ終了時自動保存)。
        SaveDockingLayoutAsDefault();
    }

    // T-061修正E-1(静的レビューPR-07該当): 行範囲チェック式が3箇所(テストモード左クリック・
    // 右クリックのrowInRange・ShowTestModeContextMenu)に手書き重複していたため共有化する。
    private static bool IsRowInRange(int row, Ecad2.Model.Sheet sheet) => row >= 0 && row < sheet.Grid.Rows;

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

        // T-061第三歩: テストモード中は選択操作(行コメント編集・要素選択・ドラッグ等)を一切行わず、
        // 押しボタン/トグル/セレクトSWの入力操作のみを行う(GuiEcad「進行中のドラッグ/パン状態の
        // 破棄」に相当する分離、Modeのsetterで既にTool=SelectDefaultへ固定済み)。
        if (_viewModel.Mode == ViewModels.AppMode.Test)
        {
            if (_viewModel.CurrentSheet is Ecad2.Model.Sheet testSheet)
            {
                var testPos = LadderCanvasHost.ToGridPos(position);
                if (IsRowInRange(testPos.Row, testSheet)
                    && _viewModel.TestModePress(testPos) is string pressedDevice)
                {
                    // T-061修正D-2(静的レビュー指摘): 他5箇所の確立パターン(CaptureMouse戻り値
                    // チェック)と異なりここだけ戻り値未チェックだった。キャプチャ失敗時はモーメンタリ
                    // がON固定されうるため、即座にTestModeReleaseでOFFへ戻す。
                    if (LadderCanvasHost.CaptureMouse())
                        _testModePressedDevice = pressedDevice;
                    else
                        _viewModel.TestModeRelease(pressedDevice);
                }
                RedrawCanvas();
            }
            e.Handled = true;
            return;
        }

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

        // T-067(4): 枠ラベルのダブルクリック編集(GuiEcad踏襲、RungCommentEditorと同型パターン)。
        // ツールモードを問わず優先判定する(RungCommentと同じ方針)。HitTestFrameは境界線近傍のみ
        // ヒットする(選択と同じ判定を再利用)。
        if (e.ClickCount == 2 && _viewModel.CurrentSheet is Ecad2.Model.Sheet flblSheet
            && LadderCanvasHost.HitTestFrame(position, flblSheet) is Ecad2.Model.GroupFrame flblFrame)
        {
            OpenFrameLabelEditor(flblFrame);
            return;
        }

        // T-064(殿裁定「案A」): 画像挿入の配置待機モード中のクリックは確定操作。Tool.Mode!=Selectの
        // 早期return(直後)より前で扱う必要がある(でないとPlaceImage中は常に無視されてしまう)。
        if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceImage)
        {
            if (_viewModel.ConfirmImageInsertDraft())
            {
                _viewModel.StatusMessage = "";
                RedrawCanvas();
            }
            e.Handled = true;
            return;
        }

        // T-067(3): 枠配置モード中のキャンバス押下は新規作成ドラッグの開始(GuiEcad原本移植、
        // グリッドセル単位スナップに翻案)。Tool.Mode!=Selectの早期returnより前で扱う必要がある
        // 理由はPlaceImageと同じ(でないと本モード中は常に無視されてしまう)。押下セルがグリッド
        // 範囲外なら何もしない(既存要素配置と同じく範囲外クリックは無視)。
        if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceFrame && _viewModel.CurrentSheet is Ecad2.Model.Sheet frameCreateSheet)
        {
            var anchor = LadderCanvasHost.ToGridPos(position);
            if (anchor.Row >= 0 && anchor.Row < frameCreateSheet.Grid.Rows
                && anchor.Column >= 0 && anchor.Column < frameCreateSheet.Grid.Columns)
            {
                _viewModel.BeginFrameDraft(anchor);
                if (!LadderCanvasHost.CaptureMouse()) _viewModel.CancelFrameDraft();
                RedrawCanvas();
            }
            e.Handled = true;
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
            return;
        }

        // T-064: 選択中の画像のリサイズハンドル(4隅)を押下したらリサイズを開始する(ドラッグ移動
        // より判定を先にする、ハンドルは画像本体の矩形の隅に重なるため)。
        if (_viewModel.SelectedImage is Ecad2.Model.ImageInsert resizeImg
            && _viewModel.CurrentSheet is Ecad2.Model.Sheet imgHandleSheet
            && LadderCanvasHost.HitTestImageResizeHandle(position, resizeImg) is ViewModels.ImageResizeHandle handle)
        {
            var (rxMm, ryMm) = LadderCanvasHost.ToMmPoint(position);
            _viewModel.BeginResizeImage(resizeImg, handle, rxMm, ryMm,
                imgHandleSheet.Grid.Columns * LadderCanvasHost.CellMm, imgHandleSheet.Grid.Rows * LadderCanvasHost.CellMm);
            if (!LadderCanvasHost.CaptureMouse()) { _viewModel.CancelResizeImage(); return; }
            _imageResizePressPositionDip = position;
            _imageResizeStarted = false;
            return;
        }

        // T-064: 選択中の画像本体を押下したらドラッグ(移動)を開始する。
        if (_viewModel.SelectedImage is Ecad2.Model.ImageInsert dragImg
            && _viewModel.CurrentSheet is Ecad2.Model.Sheet imgDragSheet
            && LadderCanvasHost.HitTestImage(position, imgDragSheet) == dragImg)
        {
            var (dxMm, dyMm) = LadderCanvasHost.ToMmPoint(position);
            _viewModel.BeginDragImage(dragImg, dxMm, dyMm,
                imgDragSheet.Grid.Columns * LadderCanvasHost.CellMm, imgDragSheet.Grid.Rows * LadderCanvasHost.CellMm);
            if (!LadderCanvasHost.CaptureMouse()) { _viewModel.CancelDragImage(); return; }
            _imageDragPressPositionDip = position;
            _imageDragStarted = false;
            return;
        }

        // T-088: 選択中の要素(SelectedElement)本体を押下したらドラッグ(移動)を開始する
        // (調査書docs/ecad2-element-move-feature-survey-onmitsu.md、既存の縦コネクタ・画像ドラッグと
        // 同型パターン)。
        if (_viewModel.SelectedElement is Ecad2.Model.ElementInstance dragElem)
        {
            var elemHitPos = LadderCanvasHost.ToGridPos(position);
            if (elemHitPos.Row == dragElem.Pos.Row
                && elemHitPos.Column >= dragElem.Pos.Column
                && elemHitPos.Column <= dragElem.Pos.Column + dragElem.CellWidth - 1)
            {
                _viewModel.BeginDragElement(dragElem);
                if (!LadderCanvasHost.CaptureMouse()) { _viewModel.CancelDragElement(); return; }
                _elementDragPressPositionDip = position;
                _elementDragStarted = false;
            }
        }

        // T-067(2): 選択中の枠(SelectedFrame)の境界線を押下したらドラッグ(移動)を開始する
        // (要素ドラッグT-088と同型パターン、殿裁定④=移動はドラッグ)。掴む判定はヒットテストと
        // 同じ境界線近傍(HitTestFrame)——枠は塗りつぶしが無いため内部クリックでは掴まない。
        if (_viewModel.SelectedFrame is Ecad2.Model.GroupFrame dragFrame
            && _viewModel.CurrentSheet is Ecad2.Model.Sheet frameDragSheet
            && LadderCanvasHost.HitTestFrame(position, frameDragSheet) == dragFrame)
        {
            _viewModel.BeginDragFrame(dragFrame);
            if (!LadderCanvasHost.CaptureMouse()) { _viewModel.CancelDragFrame(); return; }
            _frameDragPressPositionDip = position;
            _frameDragStarted = false;
        }
    }

    // T-041増分7: ドラッグ中(キャプチャ中)のみ処理する。しきい値未満の移動はクリックとの区別のため
    // 無視する(poc/t041-drag-poc/DragCanvas.csと同じ設計)。
    private void LadderCanvasHost_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        // T-064(殿裁定「案A」): 画像挿入の配置待機モード(記入中ドラフトと同型)はマウスキャプチャ
        // 無しでホバー追従する(ボタンを押さず移動、クリックで確定)。他のドラッグ系はキャプチャ中
        // のみ処理する既存方針と異なるため、キャプチャ判定より前で扱う。
        if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceImage && _viewModel.CurrentSheet is Ecad2.Model.Sheet imgSheet)
        {
            var hoverPos = e.GetPosition(LadderCanvasHost);
            var (hoverXMm, hoverYMm) = LadderCanvasHost.ToMmPoint(hoverPos);
            _viewModel.UpdateImageInsertDraftPosition(hoverXMm, hoverYMm,
                imgSheet.Grid.Columns * LadderCanvasHost.CellMm, imgSheet.Grid.Rows * LadderCanvasHost.CellMm);
            RedrawCanvas();
            return;
        }

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
            return;
        }

        if (_viewModel.IsDraggingImage)
        {
            if (!_imageDragStarted)
            {
                if ((position - _imageDragPressPositionDip).Length < DragStartThresholdDip) return;
                _imageDragStarted = true;
            }
            var (xMm, yMm) = LadderCanvasHost.ToMmPoint(position);
            _viewModel.UpdateDragImage(xMm, yMm);
            RedrawCanvas();
            return;
        }

        if (_viewModel.IsResizingImage)
        {
            if (!_imageResizeStarted)
            {
                if ((position - _imageResizePressPositionDip).Length < DragStartThresholdDip) return;
                _imageResizeStarted = true;
            }
            var (xMm, yMm) = LadderCanvasHost.ToMmPoint(position);
            _viewModel.UpdateResizeImage(xMm, yMm);
            RedrawCanvas();
            return;
        }

        if (_viewModel.IsDraggingElement)
        {
            if (!_elementDragStarted)
            {
                if ((position - _elementDragPressPositionDip).Length < DragStartThresholdDip) return;
                _elementDragStarted = true;
            }
            _viewModel.UpdateDragElement(LadderCanvasHost.ToGridPos(position));
            RedrawCanvas();
            return;
        }

        if (_viewModel.IsDraggingFrame)
        {
            if (!_frameDragStarted)
            {
                if ((position - _frameDragPressPositionDip).Length < DragStartThresholdDip) return;
                _frameDragStarted = true;
            }
            _viewModel.UpdateDragFrame(LadderCanvasHost.ToGridPos(position));
            RedrawCanvas();
            return;
        }

        // T-067(3): 枠新規作成ドラッグ中(FrameDraftPreview!=null)、現在のマウス位置のセルまで
        // 右下方向へ矩形を伸縮する(Anchor=左上固定、GuiEcad原本のドラッグ追従を翻案)。
        // AdjustFrameDraftは差分方式のため、目標サイズとの差分を都度計算して渡す
        // (グリッド範囲外へ伸ばそうとした場合はAdjustFrameDraft内部のIsFrameWithinGridBoundsで
        // 無視される、キーボードステップ方式と共通のガード)。
        if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceFrame && _viewModel.FrameDraftPreview is Ecad2.Model.GroupFrame framePreview)
        {
            var current = LadderCanvasHost.ToGridPos(position);
            int targetWidth = Math.Max(1, current.Column - framePreview.TopLeft.Column + 1);
            int targetHeight = Math.Max(1, current.Row - framePreview.TopLeft.Row + 1);
            _viewModel.AdjustFrameDraft(targetWidth - framePreview.Width, targetHeight - framePreview.Height);
            RedrawCanvas();
        }
    }

    private void LadderCanvasHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // T-061第三歩: 押しボタンのモーメンタリ解除(押している間だけON)。
        if (_testModePressedDevice is string releasedDevice)
        {
            _viewModel.TestModeRelease(releasedDevice);
            _testModePressedDevice = null;
            LadderCanvasHost.ReleaseMouseCapture();
            RedrawCanvas();
            return;
        }

        // T-061往復修正: テストモード中は_testModePressedDeviceが未設定(TestModePressがnullを
        // 返すケース)でも、以降の通常クリック処理(セル選択等)へフォールスルーしない。
        if (ShouldSkipSelectionInTestMode(_viewModel.Mode)) return;

        // T-041増分7実機確認で発覚(往復1周目): Escでキャンセル済み(IsDragging*=falseだが
        // *DragConsumedByEscape=true)のマウスアップは、押していた指を離しただけの後始末。
        // キャプチャを解放するのみで、通常のクリック処理(セル選択/配線プリミティブ選択切替)は行わない
        // (これをスキップしないと、離した位置がたまたま別要素の上にあると誤選択されてしまう)。
        if (_connectorDragConsumedByEscape || _wireBreakDragConsumedByEscape || _freeLineDragConsumedByEscape
            || _connectionDotDragConsumedByEscape || _imageDragConsumedByEscape || _imageResizeConsumedByEscape
            || _elementDragConsumedByEscape || _frameDragConsumedByEscape || _frameCreateDragConsumedByEscape)
        {
            LadderCanvasHost.ReleaseMouseCapture();
            _connectorDragConsumedByEscape = false;
            _wireBreakDragConsumedByEscape = false;
            _freeLineDragConsumedByEscape = false;
            _connectionDotDragConsumedByEscape = false;
            _imageDragConsumedByEscape = false;
            _imageResizeConsumedByEscape = false;
            _elementDragConsumedByEscape = false;
            _frameDragConsumedByEscape = false;
            _frameCreateDragConsumedByEscape = false;
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
        if (_viewModel.IsDraggingImage)
        {
            _viewModel.ConfirmDragImage();
            LadderCanvasHost.ReleaseMouseCapture();
            _imageDragStarted = false;
            RedrawCanvas();
            return;
        }
        if (_viewModel.IsResizingImage)
        {
            _viewModel.ConfirmResizeImage();
            LadderCanvasHost.ReleaseMouseCapture();
            _imageResizeStarted = false;
            RedrawCanvas();
            return;
        }
        if (_viewModel.IsDraggingElement)
        {
            _viewModel.ConfirmDragElement();
            LadderCanvasHost.ReleaseMouseCapture();
            _elementDragStarted = false;
            RedrawCanvas();
            return;
        }
        if (_viewModel.IsDraggingFrame)
        {
            _viewModel.ConfirmDragFrame();
            LadderCanvasHost.ReleaseMouseCapture();
            _frameDragStarted = false;
            RedrawCanvas();
            return;
        }
        // T-067(3): 枠新規作成ドラッグの確定。しきい値未満(実質クリックのみ)でも1x1の枠として
        // 有効に成立させる(GuiEcad原本の「半セル未満は無視」はmm連続座標ゆえの措置で、グリッド
        // セル単位のecad2では1x1が最小単位として意味を持つため踏襲しない、殿裁定②の帰結)。
        if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceFrame && _viewModel.FrameDraftPreview is not null)
        {
            _viewModel.ConfirmFrameDraft();
            LadderCanvasHost.ReleaseMouseCapture();
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
            // T-067: GroupFrame(グループ枠)の選択。GuiEcad優先順位(要素→縦コネクタ→枠→点…)に倣い
            // Connectorの直後に置く。同じ排他クリア順序(SelectedCell=null→SelectedFrame=frame)に倣う。
            if (LadderCanvasHost.HitTestFrame(position, sheet) is Ecad2.Model.GroupFrame frame)
            {
                _viewModel.SelectedCell = null;
                _viewModel.SelectedFrame = frame;
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
            // T-116(P-107対処、GuiEcad原本=MainPage.Pointer.cs354行踏襲): 接続点は線の交点上に
            // 置かれるため、自由線より先に判定する(逆順だと交点上の接続点が自由線の当たり判定に
            // 隠れ実質選択不能になる)。
            if (LadderCanvasHost.HitTestConnectionDot(position, sheet) is Ecad2.Model.ConnectionDot dot)
            {
                _viewModel.SelectedCell = null;
                _viewModel.SelectedConnectionDot = dot;
                return;
            }
            if (LadderCanvasHost.HitTestFreeLine(position, sheet) is Ecad2.Model.FreeLine freeLine)
            {
                _viewModel.SelectedCell = null;
                _viewModel.SelectedFreeLine = freeLine;
                return;
            }
            // T-064: 画像の選択(GuiEcad同様、背面固定描画のため他要素より判定優先度が最後)。
            if (LadderCanvasHost.HitTestImage(position, sheet) is Ecad2.Model.ImageInsert img)
            {
                _viewModel.SelectedCell = null;
                _viewModel.SelectedImage = img;
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
    //
    // T-069往復2周目(隠密レビュー指摘、修正3+4): 記入中(縦コネクタ/自由線/画像挿入ドラフト)は
    // 右クリック処理自体を行わない。SelectedCellのsetterは値変化の有無に関わらず常時
    // ClearConnectorDraftIfAny/ClearFreeLineDraftIfAny等を実行する「選択状態をクリアする唯一の
    // 入口」設計(T-041由来)のため、これを経由するだけでメニューを開いた時点(選ぶ前)に記入中
    // ドラフトが警告なく破棄されてしまう(殿裁定=保護する方針)。
    //
    // T-069往復3周目修正2(隠密レビュー指摘): 往復2周目はTool.Mode!=Select全般でガードしていたが、
    // これは記入中ドラフトを一切持たないPlaceElement(連続配置、T-021分岐A、常用ワークフロー)まで
    // 一律ブロックする過剰な副作用があった。ガードをHasAnyDraft(実際にドラフトを保持しているか)へ
    // 絞り込み、静的なツールモードでは右クリックメニュー(削除・機器名変更・行操作等)が引き続き
    // 使えるようにする。部品配置モード中はDeviceNameBoxがCollapsedでFocus()が効かず「機器名変更」
    // が無反応だった問題(往復2周目修正3)は、要素上での右クリック自体が到達可能になったことで
    // 実機確認が必要な範囲へ縮小する(HasSelectedElement化に伴うプロパティパネル切替の実挙動)。
    private void LadderCanvasHost_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // T-061修正D-1(静的レビュー指摘): テストモード分岐がHasAnyDraftガード(T-069確立)より前に
        // あり、記入中ドラフトの保護を迂回していた。Modeセッタで既にドラフトを明示クリアする
        // ようになった(D-1本体)ため実害は塞がったが、二重の安全網としてガードの後段へ移す。
        if (_viewModel.HasAnyDraft) return;

        // T-061第四歩: テストモード中は作画モード用メニューと完全に切り替え、接点(ContactNO/NC)
        // 限定の手動強制ON/OFFメニューのみを出す(GuiEcad ShowContactContextMenu踏襲)。
        if (_viewModel.Mode == ViewModels.AppMode.Test)
        {
            ShowTestModeContextMenu(e.GetPosition(LadderCanvasHost));
            e.Handled = true;
            return;
        }
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet) return;
        var position = e.GetPosition(LadderCanvasHost);
        var pos = LadderCanvasHost.ToGridPos(position);
        bool rowInRange = IsRowInRange(pos.Row, sheet);
        // T-064往復2周目修正1(隠密再レビュー指摘): 画像はグリッド非依存の自由配置要素で、上部
        // 余白帯(Y<MarginMm)等の行範囲外にも配置・リサイズできる。行範囲外ガードを画像ヒット
        // テストより先に置くと、範囲外にある画像は右クリックメニューが一切出ず、行範囲チェックの
        // 無い左クリックとの対称性が崩れる(修正5で意図した対称性回復が未達成だった)。画像がヒット
        // しない場合のみ、従来どおり行範囲外で打ち切る。
        // T-067(5)修正(隠密机上検証、家老采配2026-07-19): GroupFrameもHitTestFrameの境界線マージン
        // 判定がToGridPos(position)由来のpos.Rowと独立している(境界線の物理位置とグリッドセル
        // 判定がズレる)ため、最下行付近の枠は下辺境界ヒット帯の大半がrowInRange=falseと判定され
        // 右クリックがほぼ不能になっていた(当初「殿裁定によりグリッドセル単位固定だからConnector
        // と同じでよい」と判断したが誤りだった、画像と同じくrowInRangeガードの対象外にする)。
        // T-114(P-075対処、隠密所見2026-07-13): 同一クリックに対しHitTestImageが2回(この行範囲外
        // ガード判定用と、後段のメニュー構築判定用)呼ばれ同じ結果を再計算していた冗長を解消する
        // (position/sheetとも不変、この関数内でsheet.Imagesを変更する操作は無いため結果は同一)。
        var imageHitForContextMenu = LadderCanvasHost.HitTestImage(position, sheet);
        if (!rowInRange && imageHitForContextMenu is null
            && LadderCanvasHost.HitTestFrame(position, sheet) is null) return;

        var menu = new ContextMenu();

        if (rowInRange && _viewModel.HitTestElement(pos) is Ecad2.Model.ElementInstance hitElement)
        {
            // T-069往復2周目修正2(隠密レビュー指摘): DeviceNameBoxの未確定編集(UpdateSourceTrigger=
            // Explicit)を、選択切替でサイレント消失させないよう既存DeleteMenuItem_Clickと同じ規約で
            // 確定してから選択状態を切り替える。
            CommitDeviceNameEdit();
            // T-069往復3周目修正1(隠密レビュー指摘「表示と実行の整合原則」): HitTestElementは
            // CellWidth>1要素の非アンカーセルでも占有範囲内なら検出する(区間交差判定)が、
            // SelectedElement等のSelectedCellベース経路は単純位置一致(el.Pos==pos)のまま。実行側
            // (削除・機器名変更)が解決する要素をヒット要素と一致させるには、検出した要素のアンカー
            // 位置(hitElement.Pos)へSelectedCellを正規化する必要がある(案B=局所正規化)。
            // T-069往復4周目修正2(隠密再レビュー指摘、殿裁可済み): ただし、この正規化をメニュー
            // 表示の時点(ここ)で即座に行うと、連続配置中(SelectedCell=作業起点P0)に無関係な要素を
            // 確認するだけで右クリックしただけでもP0が書き換わってしまい、メニューをキャンセルして
            // 戻っても作業起点を失う(実害大)。正規化はBuildElementContextMenuItems内の各メニュー
            // 項目のClickハンドラへ遅延させ、キャンセル時はSelectedCellを元のまま維持しつつ、項目を
            // 実際に実行する時点でのみ正規化する(表示と実行の整合原則は維持、破壊はしない)。
            BuildElementContextMenuItems(menu, hitElement.Pos, sheet);
        }
        else if (rowInRange && LadderCanvasHost.HitTestConnector(position, sheet) is Ecad2.Model.VerticalConnector connector)
        {
            CommitDeviceNameEdit();
            _viewModel.SelectedCell = null;
            _viewModel.SelectedConnector = connector;
            var deleteConnectorItem = new MenuItem { Header = "縦コネクタ削除" };
            deleteConnectorItem.Click += DeleteMenuItem_Click;
            menu.Items.Add(deleteConnectorItem);
        }
        // T-067(5): GroupFrame(グループ枠)の右クリックメニュー。左クリックのヒットテスト優先順位
        // (T-067実装コメント「要素→縦コネクタ→枠→…」)に倣い、要素・縦コネクタの直後に置く。
        // Connectorと同じくヒットテストが返すのはGroupFrame参照そのもの(位置正規化の問題が無い)
        // ため、要素分岐と異なりメニュー表示時点で即座に選択状態を切り替えてよい(Connector/Imageと
        // 同型)。【2026-07-19訂正】当初「グリッドセル単位固定だからConnectorと同じrowInRange
        // ガード対象でよい」と判断したが誤り——HitTestFrameの境界線マージン判定はpos.Row由来の
        // rowInRangeとは独立した物理座標ベースのため、最下行付近の枠は右クリックがほぼ不能になる
        // 実害があった(隠密机上検証で確定)。画像と同じくrowInRangeガードの対象外にする。
        else if (LadderCanvasHost.HitTestFrame(position, sheet) is Ecad2.Model.GroupFrame hitFrame)
        {
            CommitDeviceNameEdit();
            _viewModel.SelectedCell = null;
            _viewModel.SelectedFrame = hitFrame;
            BuildFrameContextMenuItems(menu);
        }
        // T-064往復1周目修正5(隠密レビュー指摘): 右クリックのヒットテストチェーンにHitTestImageが
        // 無く、左クリック(LadderCanvasHost_PreviewMouseLeftButtonUp)とは対称性が崩れていた
        // (画像上で右クリックしても行操作メニューへフォールバックし削除できなかった)。GuiEcad同様
        // 背面固定描画のため要素・縦コネクタより後、行操作より前の優先順位で判定する。
        else if (imageHitForContextMenu is Ecad2.Model.ImageInsert hitImage)
        {
            CommitDeviceNameEdit();
            _viewModel.SelectedCell = null;
            _viewModel.SelectedImage = hitImage;
            var deleteImageItem = new MenuItem { Header = "削除" };
            deleteImageItem.Click += DeleteMenuItem_Click;
            menu.Items.Add(deleteImageItem);
        }
        else
        {
            // T-069往復3周目修正3(隠密レビュー指摘): 行操作コマンド(InsertRowBeforeCommand/
            // DeleteRowAtCommand)の実行内部でSelectedCellが行シフトされ、SelectedElementDeviceName
            // のPropertyChangedが間接的に発火する経路がある。要素・縦コネクタ分岐と同じ規約
            // (選択状態を動かす前に未確定編集を確定)へ揃える。
            CommitDeviceNameEdit();
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

    // T-061第四歩: テストモード中の右クリックメニュー(接点ContactNO/NC限定、手動強制ON/OFF、
    // GuiEcad ShowContactContextMenu踏襲)。接点以外・デバイス名なし・ヒットしない場合は何も
    // 表示しない(GuiEcadもhit is null || DeviceName is null || Kind not in (NO,NC)で早期return)。
    private void ShowTestModeContextMenu(Point position)
    {
        if (_viewModel.CurrentTestSession is not Ecad2.Simulation.TestSession session
            || _viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet) return;
        var pos = LadderCanvasHost.ToGridPos(position);
        if (!IsRowInRange(pos.Row, sheet)) return;
        if (_viewModel.HitTestElement(pos) is not Ecad2.Model.ElementInstance hit || hit.DeviceName is not string dev)
            return;
        // T-061修正C-2(静的レビュー): hit.Kindは自作パーツ配置時に常時既定値ContactNOのまま固定
        // される(T-046由来)ため、この判定は常にfalse(素通り)になりガードが機能していなかった。
        // PartResolver.ComponentKind経由で実際の電気的種別を解決するIsRealContactElement(C-3と
        // 共通ヘルパー)へ置換する。
        if (!_viewModel.IsRealContactElement(hit)) return;

        bool isForced = session.State.Inputs.TryGetValue(dev, out var cur) && cur;

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem
        {
            Header = $"接点: {dev} [{(isForced ? "手動ON中" : "シミュレーション依存")}]",
            IsEnabled = false,
        });
        menu.Items.Add(new Separator());

        var forceOnItem = new MenuItem { Header = "手動でON(強制閉路)", IsEnabled = !isForced };
        forceOnItem.Click += (_, _) => { session.SetInput(dev, true); RedrawCanvas(); };
        menu.Items.Add(forceOnItem);

        var releaseItem = new MenuItem { Header = "手動を解除(シミュレーション依存に戻す)", IsEnabled = isForced };
        releaseItem.Click += (_, _) => { session.SetInput(dev, false); RedrawCanvas(); };
        menu.Items.Add(releaseItem);

        LadderCanvasHost.ContextMenu = menu;
        menu.IsOpen = true;
    }

    // T-069: 要素上での右クリックメニュー項目(削除/機器名変更/コメント編集)。削除はDeleteMenuItem_Click
    // (T-063、要素/コネクタ/配線プリミティブ横断の既存ハンドラ)をそのまま流用する(選択状態は要素のみに
    // 絞られているため、DeleteSelectedElementだけがtrueを返す)。機器名変更は殿裁定どおりDeviceNameBoxへの
    // 自動フォーカス(新規インライン入力ボックスは不採用)。コメント編集はT-080で完成済みのRungComment
    // 編集UI(OpenRungCommentEditor)をF2キーと同じ対象条件(主回路シート対象外・描画範囲内の行のみ)で
    // 呼ぶだけ(新規UI設計不要、家老裏取り訂正2026-07-13)。
    // T-069往復4周目修正2(隠密再レビュー指摘、殿裁可済み): posへのSelectedCell正規化は、呼び出し元の
    // メニュー表示時点ではなく、ここの各項目Clickハンドラ内(実行直前)で行う。メニュー表示だけで
    // 作業起点(連続配置中のSelectedCell)を破壊しないため。
    private void BuildElementContextMenuItems(ContextMenu menu, Ecad2.Model.GridPos pos, Ecad2.Model.Sheet sheet)
    {
        var deleteItem = new MenuItem { Header = "削除" };
        deleteItem.Click += (s, e) =>
        {
            _viewModel.SelectedCell = pos;
            DeleteMenuItem_Click(s, e);
        };
        menu.Items.Add(deleteItem);

        var renameItem = new MenuItem { Header = "機器名変更" };
        renameItem.Click += (_, _) =>
        {
            _viewModel.SelectedCell = pos;
            DeviceNameBox.Focus();
            DeviceNameBox.SelectAll();
        };
        menu.Items.Add(renameItem);

        if (!sheet.MainCircuit && pos.Row >= 0 && pos.Row < Ecad2.Rendering.DiagramRenderer.TotalRows(sheet))
        {
            var commentItem = new MenuItem { Header = "コメント編集" };
            commentItem.Click += (_, _) =>
            {
                _viewModel.SelectedCell = pos;
                OpenRungCommentEditor(pos.Row, sheet);
            };
            menu.Items.Add(commentItem);
        }
    }

    // T-067(5): GroupFrame(グループ枠)上での右クリックメニュー項目(線種変更/削除、GuiEcad
    // 「線種」サブメニュー(実線/破線/点線)+「削除」踏襲、docs/ecad2-t067-groupframe-design-
    // onmitsu2.md参照)。呼び出し元(LadderCanvasHost_PreviewMouseRightButtonDown)で既に
    // SelectedFrameへ切り替え済みのため、要素分岐のような位置正規化は不要(Connector/Imageと同型)。
    // 削除は既存DeleteMenuItem_Click(DeleteSelectedFrameを含むOR連鎖)をそのまま流用する。
    private void BuildFrameContextMenuItems(ContextMenu menu)
    {
        var lineStyleItem = new MenuItem { Header = "線種" };
        foreach (var (label, style) in new (string, Ecad2.Model.LineStyle)[]
        {
            ("実線", Ecad2.Model.LineStyle.Solid),
            ("破線", Ecad2.Model.LineStyle.Dashed),
            ("点線", Ecad2.Model.LineStyle.Dotted),
        })
        {
            var styleItem = new MenuItem { Header = label };
            styleItem.Click += (_, _) =>
            {
                // T-067(5)往復1周目修正(忍者実機NG=docs-notes/ecad2-t067-5-contextmenu-
                // verification-ninja.md): 削除(DeleteMenuItem_Click)はSelectedFrame=null経由の
                // PropertyChanged(ViewModel_PropertyChanged)でもRedrawCanvasが発火するため、
                // クリックハンドラ内の直接呼び出しと合わせて同期的に2回再描画される。線種変更は
                // SelectedFrame自体を変更しないためPropertyChanged非発火・直接呼び出し1回のみで、
                // ContextMenuが閉じる処理と競合し画面へ反映されなかった(内部モデルは正しく変更
                // 済み、Undo後の再描画では正しく表示されることを忍者が確認済み)。ContextMenuの
                // クローズ処理完了後まで再描画をDispatcher.BeginInvokeで遅延させる(既存の
                // Focus()遅延パターン[3103行等]と同型の対処)。
                if (_viewModel.SetSelectedFrameBorderStyle(style))
                    Dispatcher.BeginInvoke(new Action(RedrawCanvas), DispatcherPriority.Background);
            };
            lineStyleItem.Items.Add(styleItem);
        }
        menu.Items.Add(lineStyleItem);

        var deleteFrameItem = new MenuItem { Header = "削除" };
        deleteFrameItem.Click += DeleteMenuItem_Click;
        menu.Items.Add(deleteFrameItem);
    }

    // T-041増分7隠密レビュー所見C対応: Alt+Tab等の外的要因でマウスキャプチャが失われた場合、
    // 進行中のドラッグを安全にキャンセルする(掴んだ位置への復元、MarkDirty()しない)。
    // ReleaseMouseCapture()を能動的に呼んだ直後の正常フロー(MouseUp/Escの各分岐)でも本イベントは
    // 発火するが、その時点では既にConfirm/CancelDrag*でIsDragging*=falseになっているため
    // 各ガードが素通しし二重処理にはならない。
    private void LadderCanvasHost_LostMouseCapture(object sender, MouseEventArgs e)
    {
        // T-061第三歩: 押しボタン押下中にキャプチャを失った場合もモーメンタリを確実にOFFへ戻す。
        if (_testModePressedDevice is string lostDevice)
        {
            _viewModel.TestModeRelease(lostDevice);
            _testModePressedDevice = null;
            RedrawCanvas();
        }
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
        if (_viewModel.IsDraggingImage)
        {
            _viewModel.CancelDragImage();
            _imageDragStarted = false;
            RedrawCanvas();
        }
        if (_viewModel.IsResizingImage)
        {
            _viewModel.CancelResizeImage();
            _imageResizeStarted = false;
            RedrawCanvas();
        }
        if (_viewModel.IsDraggingElement)
        {
            _viewModel.CancelDragElement();
            _elementDragStarted = false;
            RedrawCanvas();
        }
        if (_viewModel.IsDraggingFrame)
        {
            _viewModel.CancelDragFrame();
            _frameDragStarted = false;
            RedrawCanvas();
        }
        // T-067(3): 枠新規作成ドラッグ中にキャプチャを失った場合(Alt+Tab等)もドラフトを破棄する
        // (他ドラッグ系と同型の安全網)。
        if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceFrame && _viewModel.FrameDraftPreview is not null)
        {
            _viewModel.CancelFrameDraft();
            RedrawCanvas();
        }
        _connectorDragConsumedByEscape = false;
        _wireBreakDragConsumedByEscape = false;
        _freeLineDragConsumedByEscape = false;
        _connectionDotDragConsumedByEscape = false;
        _imageDragConsumedByEscape = false;
        _imageResizeConsumedByEscape = false;
        _elementDragConsumedByEscape = false;
        _frameDragConsumedByEscape = false;
        _frameCreateDragConsumedByEscape = false;
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
        if (_viewModel.IsPlacementBarVisible)
        {
            // T-070隠密レビュー指摘A-7: 配置バーと検索バーが同時表示状態になりうる(検索バーは非
            // モーダルのため配置バー表示自体をブロックしない)。本ガードがswitch文より前で早期return
            // するため、下記EscapeケースのFindBar優先処理(コメント参照)に到達できず、検索バーの
            // Escapeが配置バー消滅後まで機能しなかった。配置バー自身のEsc/Enterは別経路(IsCancel/
            // IsDefault)で処理されるため本ガードの影響を受けず、ここで個別処理してもバッティングしない。
            if (e.Key == Key.Escape && _viewModel.Find.IsVisible)
            {
                // T-070隠密独立調査(A-7再発、ecad2-t070-a7-escape-double-press-investigation-onmitsu.md):
                // e.Handled=trueにするとWPF標準のAccessKeyManager(配置バーIsCancelボタンのEscape検出、
                // PostProcessInput経由)がPostProcessInput到達時点でHandled=trueのため丸ごとスキップ
                // され、配置バーが1回目のEscapeで閉じなかった。ClosePlacementBar()を明示的に呼び、
                // 検索バー・配置バーとも1回のEscapeで対称に閉じるようにする(FocusCanvas()も内包)。
                _viewModel.Find.IsVisible = false;
                ClosePlacementBar();
                e.Handled = true;
            }
            return;
        }

        // T-080: 行コメントエディタ編集中はグローバルショートカット(F5等)を無効化する
        // (ElementPlacementBar表示中と同じ設計方針)。Enter/Tab/EscapeはRungCommentBox自身の
        // PreviewKeyDown(RungCommentBox_PreviewKeyDown)が処理するため、本ハンドラより先に
        // 到達させる必要がある(本ハンドラはTunnelingでRungCommentBoxより先に発火するため、
        // ここで早期returnしないとEscape等が意図せず消費されてしまう)。
        if (_rungCommentEditingRow is not null) return;

        // T-067(4): 枠ラベルエディタ編集中も同じ理由(RungCommentEditorと同型)でグローバル
        // ショートカットを無効化する。
        if (_frameLabelEditingFrame is not null) return;

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
            case Key.F1 when noModifier:
                // T-077増分1(家老采配2026-07-21、殿裁定=F1割当): ヘルプ表示は編集操作ではない汎用
                // 機能のためdesign-brief原則1(単キーショートカットはキャンバスフォーカス時のみ有効)
                // の対象外とし、フォーカス位置を問わず常時有効にする(他パネルのキー操作と衝突しない、
                // F1は既存コードで未使用と確認済み)。
                ShowUsageWindow();
                e.Handled = true;
                break;
            case Key.F11 when noModifier && _viewModel.CanEditDiagram:
                // T-087往復4周目修正(隠密静的レビュー指摘、PR-13): CyclePanelFocusのPartSelectionList
                // 分岐(下記)と重複していたロジックをActivateAndFocusPartSelectionへ統合(rule of two)。
                ActivateAndFocusPartSelection();
                e.Handled = true;
                break;
            case Key.Escape:
                // T-070: 検索バー表示中のEscapeは最優先でバーを閉じる。本ハンドラはTunnelingで
                // FindQueryBox(専用のFindQueryBox_PreviewKeyDown)より先に発火するため、ここで
                // 処理しないとFindBar内のEsc取消が到達不能になる(IsPlacementBarVisible等と異なり
                // FindBarは非モーダル=グローバルショートカット全体は無効化しないため、Escapeの
                // 奪い合いのみここで個別に解消する)。
                if (_viewModel.Find.IsVisible)
                {
                    _viewModel.Find.IsVisible = false;
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
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
                if (_viewModel.IsDraggingImage)
                {
                    _viewModel.CancelDragImage();
                    _imageDragStarted = false;
                    _imageDragConsumedByEscape = true;
                    RedrawCanvas();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                if (_viewModel.IsResizingImage)
                {
                    _viewModel.CancelResizeImage();
                    _imageResizeStarted = false;
                    _imageResizeConsumedByEscape = true;
                    RedrawCanvas();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                if (_viewModel.IsDraggingElement)
                {
                    // T-088: 要素ドラッグ中のEscも他ドラッグ系と同型の独立最優先層とする。
                    _viewModel.CancelDragElement();
                    _elementDragStarted = false;
                    _elementDragConsumedByEscape = true;
                    RedrawCanvas();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                if (_viewModel.IsDraggingFrame)
                {
                    // T-067(2): 枠ドラッグ中のEscも他ドラッグ系と同型の独立最優先層とする。
                    _viewModel.CancelDragFrame();
                    _frameDragStarted = false;
                    _frameDragConsumedByEscape = true;
                    RedrawCanvas();
                    FocusCanvas();
                    e.Handled = true;
                    break;
                }
                // T-067(3): 枠新規作成ドラッグ中(マウスキャプチャ中、FrameDraftPreview!=null)のEscも
                // 独立最優先層とする。新規作成はドラフト自体を丸ごと破棄する(既存要素移動ドラッグと
                // 異なり「元の位置」という概念が無いため)。指がまだ押されたままの想定でキャプチャは
                // 維持し、他ドラッグ系と同じくMouseUp側で後始末する。
                if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceFrame && _viewModel.FrameDraftPreview is not null)
                {
                    _viewModel.CancelFrameDraft();
                    _frameCreateDragConsumedByEscape = true;
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
                else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceImage)
                {
                    // 層2'''(T-064): 画像挿入の配置待機中 → 取消して選択モードへ戻す。何も生成しない。
                    _viewModel.CancelImageInsertDraft();
                }
                else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceFrame)
                {
                    // 層2''''(T-067(3)): 枠配置モード中(ドラッグ未開始、FrameDraftPreview==nullは
                    // 上記最優先層で処理済みのためここには到達しない) → 選択モードへ戻す。
                    _viewModel.Tool = ViewModels.ToolState.SelectDefault;
                }
                else if (_viewModel.SelectedCell is not null || _viewModel.SelectedConnector is not null
                    || _viewModel.SelectedWireBreak is not null || _viewModel.SelectedFreeLine is not null
                    || _viewModel.SelectedConnectionDot is not null || _viewModel.SelectedImage is not null
                    || _viewModel.SelectedFrame is not null)
                {
                    // 層3: 要素選択中・配線プリミティブ選択中(T-041増分1/3/5)・画像選択中(T-064往復
                    // 1周目修正3、隠密レビュー指摘=条件リストにSelectedImageが漏れており画像単独
                    // 選択時にEscで解除できなかった)・枠選択中(T-067(2)、同型横展開) → 選択解除のみ。
                    // SelectedCellのsetterが値変化の有無に関わらず全ての配線プリミティブ選択・
                    // SelectedImage・SelectedFrameも常にクリアするため(隠密レビュー指摘、
                    // MainWindowViewModel.SelectedCell参照)、1行で足りる。
                    _viewModel.SelectedCell = null;
                }
                // 層4: 何もなし → 無視(キャンバスフォーカス維持のみ)。
                // Escapeはボタンのマウス/キーボード二重発火問題を持たないグローバルショートカットの
                // ため、フォーカス復帰は全層で常時実行する(隠密の設計集約プラン根拠3のとおり変更不要)。
                FocusCanvas();
                e.Handled = true;
                break;
            // T-070隠密レビュー指摘A-6: 以下F5〜F10の配置ショートカットは他の同種ケース(F2/Delete/
            // 矢印キー等)と異なりIsCanvasFocused()を持たず非対称だった。FindQueryBox等にフォーカスが
            // ある状態でもF5等が素通しで成立し、意図せず要素が配置されてしまっていた。
            // T-091修正(隠密指摘P-093、T-087のF11実装=ActivateAndFocusPartSelection/
            // ShouldSuppressPartSelectionActivationと同型): case guardが!HasAnyDraftを見ておらず、
            // 記入中(縦コネクタ/自由線/画像挿入ドラフト中)でも配置バーが開いてしまいドラフトが
            // キャンセルされず宙に浮いていた。
            case Key.F5 when noModifier && IsCanvasFocused() && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
                TryPlaceBuiltin("a接点", isOr: false);
                e.Handled = true;
                break;
            case Key.F6 when noModifier && IsCanvasFocused() && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
                TryPlaceBuiltin("b接点", isOr: false);
                e.Handled = true;
                break;
            case Key.F5 when shift && IsCanvasFocused() && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
                TryPlaceBuiltin("a接点", isOr: true);
                e.Handled = true;
                break;
            case Key.F6 when shift && IsCanvasFocused() && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
                TryPlaceBuiltin("b接点", isOr: true);
                e.Handled = true;
                break;
            case Key.F7 when noModifier && IsCanvasFocused() && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
                TryPlaceBuiltin("コイル", isOr: false);
                e.Handled = true;
                break;
            case Key.F8 when noModifier && IsCanvasFocused() && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
                TryPlaceBuiltin("端子台", isOr: false);
                e.Handled = true;
                break;
            case Key.F9 when noModifier && IsCanvasFocused() && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
                // T-041増分5: F9で自由線(横線)手動記入モードを開始する(主回路シート限定、
                // `ecad2-t041-key-flow-proposal-samurai.md`4節・殿裁定「案A」)。制御回路シートでは
                // 当面未使用(原案どおり、自動横配線があるため対応する手動記入は無い)。
                TryBeginFreeLineDraft(horizontal: true);
                e.Handled = true;
                break;
            case Key.F9 when shift && IsCanvasFocused() && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
                // T-041増分2/5: sF9はシート種別で対象が切替わる(殿裁定「シート種別で自動切替」)。
                // 制御回路シート→縦コネクタ手動記入、主回路シート→自由線(縦線)手動記入。
                if (_viewModel.CurrentSheet is Ecad2.Model.Sheet sf9Sheet && sf9Sheet.MainCircuit)
                    TryBeginFreeLineDraft(horizontal: false);
                else
                    TryBeginConnectorDraft();
                e.Handled = true;
                break;
            case Key.System when noModifier && IsCanvasFocused() && e.SystemKey == Key.F10 && ShouldAllowShortcutPlacement(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft):
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
                // T-061修正A-1(殿裁定確定=第三案): テストモード中は矢印キーによるSelectedCellの
                // 単純移動のみ許可し、選択中プリミティブ(Connector/WireBreak/FreeLine/ConnectionDot/
                // Image)の平行移動は編集操作に該当するため禁止のままとする。Tool.Modeはテスト中は
                // 常にSelectのため下記Tool.Mode分岐には到達しないが、Selected*は意図的にモード遷移で
                // クリアされない設計(5-1注)のため、この専用分岐が無いと選択中プリミティブの移動へ
                // 素通りしてしまう。
                if (!_viewModel.CanEditDiagram)
                {
                    MoveSelectedCell(e.Key);
                    e.Handled = true;
                    break;
                }
                // design-brief原則1「単キーショートカットはキャンバスフォーカス時のみ有効」に従い、
                // 他パネル(シートナビゲーション/機器表)にフォーカスがある間は既定のリスト操作に譲る。
                // キャンバスフォーカス時はScrollViewer(CanvasArea)の既定スクロールを上書きし、
                // SelectedCellをセル単位で移動する(T-017)。T-041増分2/5: 縦コネクタ/自由線記入中は
                // 矢印キーをSelectedCell移動ではなく記入中プレビューの範囲/位置の調整に転用する。
                if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceConnector)
                    AdjustConnectorDraft(e.Key, cellCenterStep: false);
                else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceLine)
                    AdjustFreeLineDraft(e.Key);
                else if (_viewModel.Tool.Mode == ViewModels.ToolMode.PlaceImage)
                {
                    // T-064往復1周目修正2(隠密レビュー指摘): 配置待機中(殿裁定「案A」の2段階操作)は
                    // ホバー追従で位置が決まるため矢印キーによる調整機能は無い。ここで無視しないと
                    // else節のMoveSelectedCellへ落ち、SelectedCellのsetter経由で無条件の
                    // CancelImageInsertDraftが発火し、記入中ドラフトが警告なく破棄されてしまう
                    // (PlaceConnector/PlaceLineには専用分岐があるのに対称性が崩れていた)。
                }
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
                else if (_viewModel.SelectedImage is not null)
                    // T-064矢印キー画像平行移動(隠密静的調査により原因特定、殿裁定2026-07-13):
                    // 従来この分岐が無く既定のMoveSelectedCellへフォールスルーし、画像選択解除+
                    // 無関係セル選択という副作用が発生していた(横展開漏れ)。他のSelected*と同じ
                    // 位置(SelectedCellへのフォールスルー直前)に追加し、対称性を回復する。
                    MoveSelectedImageByKey(e.Key);
                else if (_viewModel.SelectedFrame is not null)
                    // T-105(殿裁定2026-07-21「案A」): 選択中の枠(SelectedFrame)を平行移動する。
                    // SelectedFrameはSelectedImageと同型の独立選択状態(SelectedElementのような
                    // SelectedCellからの自動算出プロパティではない)のため、同じ位置(SelectedCellへの
                    // フォールスルー直前)に追加し対称性を保つ。
                    MoveSelectedFrameByKey(e.Key);
                else
                    MoveSelectedCell(e.Key);
                e.Handled = true;
                break;
            case Key.Up or Key.Down or Key.Left or Key.Right
                    when Keyboard.Modifiers == ModifierKeys.Control && IsCanvasFocused() && _viewModel.CanEditDiagram:
                // T-088(殿裁定2026-07-14): Ctrl+矢印キーで選択中の要素(SelectedElement)を平行移動
                // する。通常の矢印キー(修飾キー無し)はSelectedCellの単純移動のまま維持し、Ctrl併用時
                // のみ要素移動に切り替える(SelectedElementはSelectedCellから自動算出される特殊
                // プロパティのため、他のSelected*と異なり修飾キーで区別する必要がある)。
                MoveSelectedElementByKey(e.Key);
                e.Handled = true;
                break;
            case Key.Left or Key.Right when shift && IsCanvasFocused()
                    && _viewModel.Tool.Mode == ViewModels.ToolMode.PlaceConnector:
                // T-041増分2: Shift+Left/Rightはセル中央(X.5)刻みでの列位置調整(原案3節)。
                AdjustConnectorDraft(e.Key, cellCenterStep: true);
                e.Handled = true;
                break;
            case Key.Up or Key.Down when shift && IsCanvasFocused() && _viewModel.CanEditDiagram
                    && _viewModel.SelectedConnector is not null:
                // T-041増分7(殿裁定P-033=案2): Tabで選んだ操作対象端点(始点/終点)をUp=-1/Down=+1で
                // 伸縮する。VerticalConnectorは常に縦線のためLeft/Rightの端点伸縮は意味を持たず未対応。
                // T-061修正A-1: 端点伸縮は編集操作のためCanEditDiagramガード対象(選択中プリミティブの
                // 移動禁止と同根)。
                if (_viewModel.ResizeSelectedConnectorEndpoint(e.Key == Key.Up ? -1 : 1))
                    RedrawCanvas();
                e.Handled = true;
                break;
            case Key.Up or Key.Down or Key.Left or Key.Right when shift && IsCanvasFocused()
                    && _viewModel.CanEditDiagram && _viewModel.SelectedFreeLine is not null:
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
            case Key.F2 when noModifier && IsCanvasFocused() && _viewModel.CanEditDiagram
                    && _viewModel.SelectedCell is { } commentCell
                    && _viewModel.CurrentSheet is Ecad2.Model.Sheet commentSheet:
                // T-080往復1周目・追加I(殿裁定): 選択セルの行の行コメントエディタをキーボードで
                // 開く等価経路(キーボードファースト原則。GuiEcad原本にも「コメント編集」キー割当が
                // 存在した、T-081調査)。矢印キー・Deleteと同じくキャンバスフォーカス時のみ有効
                // (選択セルに対する操作のため)。T-114(P-062対処): 対象条件はダブルクリック経路
                // (HitTestRungCommentRow)と共有ヘルパーIsRungCommentRowEligibleで統一する
                // (旧実装は同じ条件[主回路シート除外・行範囲判定]を手書きで重複していた)。
                if (Views.LadderCanvas.IsRungCommentRowEligible(commentCell.Row, commentSheet))
                    OpenRungCommentEditor(commentCell.Row, commentSheet);
                e.Handled = true;
                break;
            case Key.Delete when noModifier && IsCanvasFocused() && _viewModel.CanEditDiagram:
                // 選択中の要素を削除する(T-017追加スコープ)。Escは従来通り選択解除のみで削除しない
                // (殿裁定)。矢印キーと同様キャンバスフォーカス時のみ有効。
                // T-041増分1/3/5(案A): 選択中の要素が無く配線プリミティブ(縦コネクタ・配線分断・
                // 自由線・接続点)が選択中であればそれを削除する(既存の部品削除と同じDeleteキーへ
                // 統合)。クリック時点でいずれも排他的にしか選択されない設計だが、優先順位を明記しておく。
                if (_viewModel.DeleteSelectedElement() || _viewModel.DeleteSelectedConnector()
                    || _viewModel.DeleteSelectedWireBreak() || _viewModel.DeleteSelectedFreeLine()
                    || _viewModel.DeleteSelectedConnectionDot() || _viewModel.DeleteSelectedImage()
                    || _viewModel.DeleteSelectedFrame())
                    RedrawCanvas();
                e.Handled = true;
                break;
            case Key.Enter when noModifier && IsCanvasFocused() && _viewModel.Mode == ViewModels.AppMode.Test
                    && !e.IsRepeat && _viewModel.SelectedCell is Ecad2.Model.GridPos testEnterPos:
                // T-061修正A-3(隠密2周目レビュー指摘): 他の同種Enterケース(F2/Delete/Enter配置確定)は
                // 全てIsCanvasFocused()を条件に含むが本ケースだけ欠けていたため、DeviceNameBox編集中の
                // Enterを本ケースが先に消費しCommitDeviceNameEditへ到達できなかった(Tunnelingイベントの
                // 到達順序上、Window側が先にe.Handled=trueにすると子のDeviceNameBoxへ伝播しない)。
                // T-061修正(A-1確認事項1、殿裁定新規結線): テストモード中、選択セル上の要素への
                // Enter押下をマウス左クリック相当として扱う(TestModePressをマウスハンドラと共用)。
                // モーメンタリ解除はWindow_PreviewKeyUpでEnterキーアップ時に行う。e.IsRepeatで
                // キーリピート(押しっぱなしのKeyDown連続発火)による多重実行を防ぐ。通常のEnter配置
                // 確定ケース(下記、Tool.Mode==PlaceElement)とは独立したcase(テスト中はTool.Modeが
                // 常にSelectのため両者は排他)。
                if (_viewModel.TestModePress(testEnterPos) is string enterPressedDevice)
                    _testModeEnterPressedDevice = enterPressedDevice;
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
            // T-094(殿裁定2026-07-15、P-096起票): Ctrl+Z/YはF5〜F10や矢印キー等の他の多くの
            // ショートカットが持つIsCanvasFocused()判定を欠いており、シートパネル等にフォーカスが
            // あっても素通しで実行できてしまっていた(バグ扱い、フォーカス範囲の統一)。
            // 副作用: DeviceNameBox編集中(=IsCanvasFocused()==false)はcase自体に到達しなくなり
            // Ctrl+Zが無反応になる。CommitDeviceNameEdit()(T-051バグ修正#3)は編集欄からキャンバスへ
            // フォーカスを戻した後の操作のみを想定する形になるが、他のF5〜F10と一貫した挙動であり、
            // 呼び出し自体は残す(将来的にIsCanvasFocused範囲が変わってもガードとして機能する)。
            case Key.Z when Keyboard.Modifiers == ModifierKeys.Control && IsCanvasFocused():
                // T-051: メニュー/ツールバーのInputGestureText表示(Ctrl+Z)と整合させる。
                // T-051バグ修正#3(隠密レビューCONFIRMED重大): 既存Ctrl+S/O/Nと同型のガード。
                // DeviceNameBox編集中の未確定入力を確定してからUndoを実行しないと、Undoで
                // Documentが差し替わった後にフォーカスが外れた際、別の要素へ誤書き込みされうる。
                CommitDeviceNameEdit();
                // T-061修正A-3: CanExecute(CanEditDiagram統合済み)を経由せず直接Executeしていたため
                // テストモード中もキーボード経由でUndoが実行できてしまっていた(静的レビュー指摘)。
                if (_viewModel.UndoCommand.CanExecute(null)) _viewModel.UndoCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Y when Keyboard.Modifiers == ModifierKeys.Control && IsCanvasFocused():
                CommitDeviceNameEdit();
                if (_viewModel.RedoCommand.CanExecute(null)) _viewModel.RedoCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.G when Keyboard.Modifiers == ModifierKeys.Control:
                // T-056: メニューのInputGestureText表示(Ctrl+G)と整合させる。
                _viewModel.IsGridVisible = !_viewModel.IsGridVisible;
                e.Handled = true;
                break;
            case Key.T when Keyboard.Modifiers == ModifierKeys.Control && _viewModel.HasProject:
                // T-087(殿直接指示)往復修正(PR-13、隠密静的レビュー指摘): テストモードのON/OFF
                // トグル。メニュー/ツールバーのIsTestModeバインドはIsEnabled="{Binding HasProject}"
                // で保護されているが、キーボードショートカットは既存の統一ゲートを素通りしやすい
                // (PR-13、T-070 A-1と同型の再発)。HasProjectをここでも明示的に確認する。
                _viewModel.IsTestMode = !_viewModel.IsTestMode;
                e.Handled = true;
                break;
            case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                // T-070(殿裁定(1)): 検索・置換バーのトグル表示(GuiEcad ToggleFindBar踏襲)。
                _viewModel.Find.IsVisible = !_viewModel.Find.IsVisible;
                if (_viewModel.Find.IsVisible)
                    FindQueryBox.Focus();
                else
                    // T-070隠密レビュー指摘B-3: 閉じるボタン・Escapeでの終了はFocusCanvas()を呼ぶが、
                    // このCtrl+Fトグルオフ経路だけ欠けており、非表示化したFindQueryBoxにフォーカスが
                    // 残留し以後のキャンバス操作(矢印キー等)が効かなくなる非対称があった。
                    FocusCanvas();
                e.Handled = true;
                break;
            // T-094(殿裁定2026-07-15、P-096起票、忍者T-090実機確認中に発覚): Ctrl+Shift+Up/Downも
            // F5〜F10や矢印キー等と同様IsCanvasFocused()判定を欠いており、シートパネル等に
            // フォーカスがあっても素通しで実行できてしまっていた(バグ扱い、フォーカス範囲の統一)。
            case Key.Up when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && IsCanvasFocused():
                // T-055増分1: 末尾行を1行追加する(ツールバー「行を追加」ボタンと同一コマンド)。
                // T-090修正(隠密指摘P-090、PR-13型): CanExecute(CanEditDiagram含む)を経由せず直接
                // Executeしていたため、テストモード中等でもキーボード経由で行追加が実行できてしまって
                // いた(Execute内の安全弁は行数上限のみでCanEditDiagramはカバーしていない、T-061修正
                // A-3のUndo/Redo同型パターン踏襲)。
                if (_viewModel.AddRowCommand.CanExecute(null)) _viewModel.AddRowCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && IsCanvasFocused():
                // T-055増分1: 末尾行を1行削除する(ツールバー「行を削除」ボタンと同一コマンド)。
                // T-090修正: 上記AddRowCommandと同事情。
                if (_viewModel.DeleteRowCommand.CanExecute(null)) _viewModel.DeleteRowCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // T-061修正(A-1確認事項1): Enterキーによるテスト通電操作のモーメンタリ解除
    // (Window_PreviewKeyDownの新規Enterケースと対、マウスのMouseUp/LostMouseCaptureに相当)。
    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _testModeEnterPressedDevice is string device)
        {
            _viewModel.TestModeRelease(device);
            _testModeEnterPressedDevice = null;
            RedrawCanvas();
            e.Handled = true;
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
            || _viewModel.DeleteSelectedConnectionDot() || _viewModel.DeleteSelectedImage()
            || _viewModel.DeleteSelectedFrame())
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

    // T-064矢印キー画像平行移動(隠密静的調査により原因特定、殿裁定2026-07-13): 選択中の画像を
    // 矢印キー1回分(Shift無し)平行移動する(mm実座標系、1ステップ=CellMm、
    // MoveSelectedConnectionDotByKeyと同じ設計)。
    private void MoveSelectedImageByKey(Key key)
    {
        if (_viewModel.CurrentSheet is not Ecad2.Model.Sheet sheet) return;
        double step = LadderCanvasHost.CellMm;
        double maxXMm = sheet.Grid.Columns * step;
        double maxYMm = sheet.Grid.Rows * step;
        bool moved = key switch
        {
            Key.Up => _viewModel.MoveSelectedImage(0, -step, maxXMm, maxYMm),
            Key.Down => _viewModel.MoveSelectedImage(0, step, maxXMm, maxYMm),
            Key.Left => _viewModel.MoveSelectedImage(-step, 0, maxXMm, maxYMm),
            Key.Right => _viewModel.MoveSelectedImage(step, 0, maxXMm, maxYMm),
            _ => false,
        };
        if (moved) RedrawCanvas();
    }

    // T-088(殿裁定2026-07-14): 選択中の要素(SelectedElement)をCtrl+矢印キー1回分、GridPos単位で
    // 平行移動する(MoveSelectedImageByKeyと同型)。
    private void MoveSelectedElementByKey(Key key)
    {
        bool moved = key switch
        {
            Key.Up => _viewModel.MoveSelectedElement(-1, 0),
            Key.Down => _viewModel.MoveSelectedElement(1, 0),
            Key.Left => _viewModel.MoveSelectedElement(0, -1),
            Key.Right => _viewModel.MoveSelectedElement(0, 1),
            _ => false,
        };
        if (moved) RedrawCanvas();
    }

    // T-105(殿裁定2026-07-21「案A」=既存の独立選択状態群(Connector/WireBreak/FreeLine/
    // ConnectionDot/Image)と同一の無修飾矢印キー割当): 選択中の枠(SelectedFrame)を矢印キー1回分、
    // GridPos単位で平行移動する(MoveSelectedImageByKeyと同じ設計、GroupFrameはGrid座標系のため
    // 引数はMoveSelectedElementByKeyと同型のint deltaRow/Column)。
    private void MoveSelectedFrameByKey(Key key)
    {
        bool moved = key switch
        {
            Key.Up => _viewModel.MoveSelectedFrame(-1, 0),
            Key.Down => _viewModel.MoveSelectedFrame(1, 0),
            Key.Left => _viewModel.MoveSelectedFrame(0, -1),
            Key.Right => _viewModel.MoveSelectedFrame(0, 1),
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

        // T-069往復4周目修正1(隠密テスト設計書、殿裁可済み): 記入中ドラフトを保持したままTool.Mode
        // を上書きするとHasAnyDraftが意図せず真のまま残留する(Escapeとの対称性が崩れていた)。
        // 切替前に必ずクリアする。
        _viewModel.CancelResidualDraftForToolSwitch();
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
        // T-069往復4周目修正1(隠密テスト設計書、殿裁可済み): ActivateBuiltinToolと同じ理由で
        // 切替前に記入中ドラフトを必ずクリアする。
        _viewModel.CancelResidualDraftForToolSwitch();
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

    // T-067(3): GroupFrame新規作成ツールへの入口。ActivateBuiltinTool(PlaceElement)と同型——
    // ここではツールモードの切替のみ行い、実際の枠生成はキャンバス上のマウスドラッグ確定時
    // (LadderCanvasHost_PreviewMouseLeftButtonUp)で行う。
    private void FrameToolButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CancelResidualDraftForToolSwitch();
        _viewModel.Tool = new ViewModels.ToolState(ViewModels.ToolMode.PlaceFrame);
        _viewModel.StatusMessage = "キャンバス上でドラッグして枠の範囲を指定してください";
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

    // T-070: 検索結果パネルの行クリック(OutputGridRow_Clickedと同型)。
    private void FindResultsGridRow_Clicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: ViewModels.FindMatch match })
            _viewModel.Find.JumpToMatch(match);
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
    // ElementPlacementBarはRootLayoutGrid直下(Grid.Row="1"、ラッパーMainContentAreaの外)にあるため、
    // Marginの基準はRootLayoutGrid座標系でなければならない。
    //
    // T-110増分1(家老采配2026-07-22、C-4): GridSplitter撤去・MainWorkAreaGrid撤去に伴い、
    // クランプ基準をキャンバスDocument内包コンテナ(CanvasDocumentGrid、LayoutDocument直下の
    // Grid)へ変更する。旧MainWorkAreaGrid(左パレット+キャンバス+右パネル全体)よりも狭い
    // 範囲(キャンバス領域のみ)でのクランプになるが、配置バーは常にキャンバス上のセル近くに
    // 表示されるものであり、意味的にはより自然な基準になる(忍者実機確認で挙動を確認)。
    //
    // バーの実サイズはVisibility=Visible反映後でないと取得できない(WPF仕様: Collapsed中の
    // Measure()はDesiredSizeを強制的に0,0にする)。呼び出し元でIsPlacementBarVisible=trueを
    // 先に設定してから本メソッドを呼ぶ前提。
    // T-114(P-065対処、隠密所見2026-07-12「課題3修正の横展開」): クランプ基準をCanvasDocumentGrid
    // (キャンバス文書全体、スクロールバー等含む外枠)からCanvasArea(ScrollViewerの可視ビューポート
    // そのもの)へ変更し、PositionRungCommentEditorと同じ共有ヘルパーClampToViewportを使う形へ統一。
    // 現状は常に可視セルクリックからのみ呼ばれるため実害未確認だったが(隠密所見どおり)、将来
    // スクロール外セルから呼ぶ経路が増えた場合の画面外描画(課題3と同型)を予防する。
    private void PositionPlacementBar(Ecad2.Model.GridPos cell)
    {
        var localRect = LadderCanvasHost.CellRectDip(cell);
        var inputPoint = new Point(localRect.X, localRect.Bottom);
        var topLeft = LadderCanvasHost.TranslatePoint(inputPoint, RootLayoutGrid);
        var canvasAreaOrigin = CanvasArea.TranslatePoint(new Point(0, 0), RootLayoutGrid);

        // 診断ログ一次パスCONFIRMED(docs-notes/ecad2-t033-diag-pass1-diagnosis-samurai.md): 前回呼び出し
        // 終了時のMarginが残留したままMeasure()すると、WPF仕様(DesiredSize=content+Margin)により前回の
        // 位置が今回の測定結果を汚染する自己参照フィードバックループが生じる。測定前にリセットする。
        ElementPlacementBar.Margin = new Thickness(0);
        ElementPlacementBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size barSize = ElementPlacementBar.DesiredSize;

        Point clamped = ClampToViewport(topLeft, canvasAreaOrigin, CanvasArea.ActualWidth, CanvasArea.ActualHeight, barSize);

        ElementPlacementBar.Margin = new Thickness(clamped.X, clamped.Y, 0, 0);
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

    // T-061往復修正(バグ、忍者実機確認・隠密静的レビュー合同発見): LadderCanvasHost_
    // PreviewMouseLeftButtonUpにMode==Testのガードが無く、TestModePressがnullを返すケース
    // (SelectSwitch/Relay(ContactNO/NC以外)/ヒット無し等)で、_testModePressedDeviceが未設定の
    // まま通常編集モードの選択処理(セル選択等)へ意図せずフォールスルーしていた
    // (MouseDown側のコメント「テストモード中は選択操作を一切行わない」という設計意図との食い違い)。
    // ShouldSkipSelectionForRungCommentAreaClickと同型の判定関数として抽出する(テスト容易性)。
    internal static bool ShouldSkipSelectionInTestMode(ViewModels.AppMode mode)
        => mode == ViewModels.AppMode.Test;

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

    // T-067(4): 枠ラベル編集中の対象(_rungCommentEditingRowと同型パターン、GroupFrameは参照型
    // ゆえ行番号でなくオブジェクト自体を保持する)。
    private Ecad2.Model.GroupFrame? _frameLabelEditingFrame;

    // 枠ラベルエディタを開く(枠のダブルクリック、RungCommentEditorのOpenRungCommentEditorと同型)。
    // SelectedFrameを編集対象へ更新することで、確定処理(RenameSelectedFrame、P-071対応の受け皿)を
    // そのまま使える。編集ボックス表示中はIsMainContentEnabled経由でキャンバス操作がブロックされる
    // ため、編集中にSelectedFrameが他へ変化する心配はない(RungCommentと同じ設計)。
    private void OpenFrameLabelEditor(Ecad2.Model.GroupFrame frame)
    {
        _frameLabelEditingFrame = frame;
        _viewModel.SelectedFrame = frame;
        FrameLabelBox.Text = frame.Label;
        _viewModel.IsFrameLabelEditorVisible = true;
        PositionFrameLabelEditor(frame);
        RedrawCanvas();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            FrameLabelBox.Focus();
            FrameLabelBox.SelectAll();
        }), DispatcherPriority.Loaded);
    }

    // 枠ラベルエディタの位置決め(PositionRungCommentEditorと同型のTranslatePoint方式)。
    private void PositionFrameLabelEditor(Ecad2.Model.GroupFrame frame)
    {
        var inputPoint = LadderCanvasHost.FrameLabelAnchorDip(frame);
        var topLeft = LadderCanvasHost.TranslatePoint(inputPoint, RootLayoutGrid);
        var canvasAreaOrigin = CanvasArea.TranslatePoint(new Point(0, 0), RootLayoutGrid);

        FrameLabelEditor.Margin = new Thickness(0);
        FrameLabelEditor.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size barSize = FrameLabelEditor.DesiredSize;

        Point clamped = ClampToViewport(topLeft, canvasAreaOrigin, CanvasArea.ActualWidth, CanvasArea.ActualHeight, barSize);

        FrameLabelEditor.Margin = new Thickness(clamped.X, clamped.Y, 0, 0);
    }

    // 確定(Enter/Tab/フォーカスロスト、GuiEcad踏襲)。RenameSelectedFrame(P-071の確定処理受け皿)を
    // ここで実配線する。値未変更ならRenameSelectedFrame内でMarkDirty()しない(同値ガード規約)ため、
    // 無変更のまま確定しても無害。
    private void CommitFrameLabelEditor(bool restoreFocus)
    {
        if (_frameLabelEditingFrame is null) return;
        _viewModel.RenameSelectedFrame(FrameLabelBox.Text);
        CloseFrameLabelEditor(restoreFocus);
        RedrawCanvas();
    }

    private void CancelFrameLabelEditor() => CloseFrameLabelEditor(restoreFocus: true);

    // restoreFocus=false: フォーカスロスト確定経路(CloseRungCommentEditorと同じ非対称の理由、
    // ユーザーがマウスで移した先からフォーカスを奪い返さない)。
    private void CloseFrameLabelEditor(bool restoreFocus)
    {
        _frameLabelEditingFrame = null;
        _viewModel.IsFrameLabelEditorVisible = false;
        if (restoreFocus) FocusCanvas();
    }

    // Enter/Tab=確定、Escape=取消(GuiEcad踏襲)。
    private void FrameLabelBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            CommitFrameLabelEditor(restoreFocus: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelFrameLabelEditor();
            e.Handled = true;
        }
    }

    // フォーカスロスト=確定扱い(キャンセルではない、GuiEcad踏襲)。RungCommentBox_LostKeyboardFocus
    // と同型(_frameLabelEditingFrameを先にnull化するため多重確定にはならない)。
    private void FrameLabelBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_frameLabelEditingFrame is not null) CommitFrameLabelEditor(restoreFocus: false);
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
        // T-087往復修正(隠密静的レビュー指摘): F11で部品パネルへ直接フォーカスした後、Shift+Tabの
        // 循環対象にPartSelectionListが含まれていないと意図しない遷移(直前のパネルへ戻れない等)に
        // なるため、既存循環対象の末尾に追加する。
        UIElement[] panels = { SheetNavList, LadderCanvasHost, DeviceTableGrid, PartSelectionList };

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
        if (ReferenceEquals(target, PartSelectionList))
        {
            // T-087往復5周目修正(隠密設計書、殿裁定): 記入中ドラフト等でActivateAndFocusPartSelectionが
            // 抑制(false)された場合、循環がこの番で「詰まる」のを避けるため次のパネルへ自動スキップする
            // (殿裁定=Shift+Tab循環は無反応ではなく自動スキップ、F11単発は無反応のまま据え置き)。
            // panels配列の末尾がPartSelectionListのため、次は必ずpanels[0](SheetNavList)に戻る。
            if (!ActivateAndFocusPartSelection())
            {
                FocusPanel(panels[(next + 1) % panels.Length]);
            }
        }
        else
        {
            FocusPanel(target);
        }
    }

    // T-087往復5周目修正(隠密設計書docs/ecad2-t087-part-panel-focus-design-onmitsu.md、殿裁定):
    // 対症療法(ガード後追い)ではなく、ActivateOpenPartSelection自体の無条件呼び出しという根本原因を
    // 解消する。記入中ドラフトがある間はこの操作自体を抑制し(F5〜F10等と同じ「無反応」で一貫性、
    // 殿裁定)、Tool.Modeが既にPlaceElement(武装済み含む)ならTool再代入自体を行わずフォーカス移動
    // のみで足りる。戻り値は呼び出し元(CyclePanelFocus)が抑制発生を検知し次パネルへスキップするため。
    private bool ActivateAndFocusPartSelection()
    {
        if (ShouldSuppressPartSelectionActivation(_viewModel.CanEditDiagram, _viewModel.HasAnyDraft))
            return false;
        if (ShouldReactivateToolForPartSelection(_viewModel.Tool.Mode))
            ActivateOpenPartSelection();
        Dispatcher.BeginInvoke(new Action(() => FocusPanel(PartSelectionList)), DispatcherPriority.Loaded);
        return true;
    }

    // T-091修正(隠密指摘P-093): F5〜F10グローバルショートカットのcase guard判定を
    // internal static抽出し単体テスト可能にする(コードビハインドのDispatcher/UI要素依存部分から
    // 判定ロジックを切り離す、ShouldSuppressPartSelectionActivationと同型パターン)。
    internal static bool ShouldAllowShortcutPlacement(bool canEditDiagram, bool hasAnyDraft)
        => canEditDiagram && !hasAnyDraft;

    // T-087往復5周目修正: ActivateAndFocusPartSelectionの判定ロジックのみをinternal static抽出し
    // 単体テスト可能にする(ShouldSkipSelectionInTestModeと同型パターン)。
    // T-093(家老采配2026-07-15、P-095): ShouldAllowShortcutPlacementのド・モルガン否定と完全等価な
    // 重複実装だったため、共通ロジックへ委譲する形に統合(呼び出し元・シグネチャは無改変)。
    internal static bool ShouldSuppressPartSelectionActivation(bool canEditDiagram, bool hasAnyDraft)
        => !ShouldAllowShortcutPlacement(canEditDiagram, hasAnyDraft);

    internal static bool ShouldReactivateToolForPartSelection(ViewModels.ToolMode currentToolMode)
        => currentToolMode != ViewModels.ToolMode.PlaceElement;

    // T-087: CyclePanelFocus(2581-2585行相当)から抽出した単一要素向けフォーカス設定処理
    // (F11の部品パネル直接フォーカスと共有、rule of two)。対象要素が独立したFocusScope内にある
    // 場合、Keyboard.Focus()だけでは実フォーカスが移らないことがあるため、まずFocusScope自体にも
    // 論理フォーカスを設定しておく。
    private static void FocusPanel(UIElement target)
    {
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
        _sheetDragSource = container?.DataContext as ViewModels.SheetListItem;
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
        if (!e.Data.GetDataPresent(typeof(ViewModels.SheetListItem)))
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
        if (e.Data.GetData(typeof(ViewModels.SheetListItem)) is not ViewModels.SheetListItem droppedSheet) return;

        var sheets = _viewModel.SheetNavigation.Sheets;
        int fromIndex = sheets.IndexOf(droppedSheet);
        if (fromIndex < 0) return;

        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        int toIndex;
        if (container?.DataContext is ViewModels.SheetListItem targetSheet)
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