using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Rendering.Wpf;
using Ecad2.Simulation;

namespace Ecad2.App.Views;

/// <summary>
/// ラダー図面を描画するキャンバス。T-002 PoCの SymbolCanvas パターン(DrawingVisualホスト)を踏襲した
/// 新規実装。Ecad2.Core.Rendering.DiagramRenderer(上位描画器)と Ecad2.Rendering.Wpf.WpfRenderer
/// (IRendererのWPF実装、T-007成果物)を使って Sheet を描画する。
///
/// フォーカス: Focusable=true とし、クリックで明示的にフォーカスを取得する
/// (T-002/T-006で検証したFocusScope制御パターンの最小反映)。MainWindow.xaml側で
/// このキャンバスを含む領域を FocusManager.IsFocusScope="True" として独立させている。
/// 要素単位の選択・編集フォーカス制御（PreviewLostKeyboardFocusのキャンセル等）は
/// 配置ツール機能の実装に合わせて将来追加する。
/// </summary>
public sealed class LadderCanvas : FrameworkElement
{
    // WpfRenderer内部のK(mm→DIP)と同じ換算率。DiagramRenderer.PageSizeはmm単位を返すため、
    // Width/Height(WPF DIP)へ変換するのはビュー側の責務。
    private const double MmToDip = 96.0 / 25.4;

    private readonly VisualCollection _children;
    // ShowGrid: 作図ガイドの薄いグリッド線を画面表示するか(T-030で常時trueとして導入、
    // T-056でユーザーが切替可能に)。PDF出力(Ecad2.Pdf)側は別のDiagramRendererインスタンス
    // を使うため、ここでの設定は画面表示にのみ影響する。
    private bool _showGrid = true;
    // Theme: 作図キャンバス色のテーマ(T-083 PoC)。ShowGridと同型、_rendererの再生成が必要。
    private DrawingTheme _theme = DrawingTheme.Default;
    private DiagramRenderer _renderer = new(DrawingTheme.Default, new Ecad2.Rendering.RenderOptions { ShowGrid = true });

    /// <summary>ShowGrid/Themeいずれかの変更後、両方の現在値を反映して_rendererを再生成する
    /// (どちらか一方のセッターが他方の設定を巻き戻さないようにする)。</summary>
    private void RebuildRenderer()
        => _renderer = new DiagramRenderer(_theme, new Ecad2.Rendering.RenderOptions { ShowGrid = _showGrid });

    /// <summary>作図ガイドのグリッド線を画面表示するか(T-056、既定=表示は殿裁定2026-07-11)。
    /// 変更しただけでは画面に反映されない(このクラスはDraw()呼び出しが描画トリガーのため、
    /// 呼び出し元が明示的に再描画すること、MainWindow.RedrawCanvas参照)。</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (_showGrid == value) return;
            _showGrid = value;
            RebuildRenderer();
        }
    }

    /// <summary>作図キャンバス色のテーマ(T-083 PoC、家老采配2026-07-15=最小疎通)。
    /// ShowGridと同型、変更しただけでは画面に反映されないため呼び出し元が明示的に再描画すること
    /// (MainWindow.RedrawCanvas参照)。</summary>
    public DrawingTheme Theme
    {
        get => _theme;
        set
        {
            if (ReferenceEquals(_theme, value)) return;
            _theme = value;
            RebuildRenderer();
        }
    }

    public LadderCanvas()
    {
        _children = new VisualCollection(this);
        Focusable = true;
        PreviewMouseLeftButtonDown += (_, _) => Focus();
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    // 選択セルのハイライト枠線(T-017/T-027)。基本図形は全て1セル幅(BasicPartTemplates確認済み)
    // のため、常に1セル分の矩形で描く。
    private static readonly Pen SelectedCellPen = new(Brushes.OrangeRed, 2.0);

    // 選択中の配線プリミティブのハイライト線(T-041増分1)。SelectedCellPenと同色・やや太めにして
    // 「配線が選択されている」ことを線そのものの強調で示す(セルの矩形ハイライトとは表現を変える)。
    private static readonly Pen SelectedConnectorPen = new(Brushes.OrangeRed, 3.5);

    // 記入中(未確定)の縦コネクタ/自由線のプレビュー線(T-041増分2/5)。確定済みの選択ハイライトと
    // 区別するため破線にする(DashStyle未共有=独立インスタンスにしないとFreeze例外になるため個別生成、
    // 縦コネクタ用・自由線用で別インスタンスを持つ)。
    private static readonly Pen ConnectorDraftPen = CreateDraftPen();

    private static Pen CreateDraftPen()
    {
        var pen = new Pen(Brushes.DodgerBlue, 2.5) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        pen.Freeze();
        return pen;
    }

    // 縦コネクタのヒットテスト許容誤差(mm)。要調整・実機確認で見直す可能性あり(T-041増分1、隠密レビュー対象)。
    private const double ConnectorHitToleranceMm = 2.0;

    // 配線分断(WireBreak)のヒットテスト許容誤差(mm、T-041増分3)。ConnectorHitToleranceMmと同値の
    // 出発点(実機確認で見直す可能性あり)。
    private const double WireBreakHitToleranceMm = 2.0;

    // 選択中の配線分断のハイライトマーク(T-041増分3)。WireBreakは通常時「マーク無し・提出品質」
    // (DiagramRenderer.DrawRungSegment参照、分断は線の空白として表現されるだけ)のため、選択中である
    // ことを示す視覚要素が元々存在しない。選択操作(クリック選択→Delete)専用の一時マークとして
    // 小さな塗り円を描く(印刷・PDF出力には影響しない画面表示専用)。
    private static readonly Brush SelectedWireBreakBrush = Brushes.OrangeRed;

    // 自由線(FreeLine)・接続点(ConnectionDot)のヒットテスト許容誤差(mm、T-041増分5)。
    // PoC(poc/t041-freeline-hittest-poc)で検証済みの値。
    private const double FreeLineHitToleranceMm = 2.0;
    private const double ConnectionDotHitToleranceMm = 2.0;

    // 選択中の自由線のハイライト線(T-041増分5、SelectedConnectorPenと同型)。
    private static readonly Pen SelectedFreeLinePen = new(Brushes.OrangeRed, 3.5);

    // 記入中(未確定)の自由線のプレビュー(T-041増分5、ConnectorDraftPenと同型)。
    private static readonly Pen FreeLineDraftPen = CreateDraftPen();

    // 選択中の接続点のハイライトマーク(T-041増分5、SelectedWireBreakBrushと同型)。
    private static readonly Brush SelectedConnectionDotBrush = Brushes.OrangeRed;

    // 選択中の画像のハイライト枠+リサイズハンドル(T-064、SelectedConnectorPen等と同色)。
    private static readonly Pen SelectedImagePen = new(Brushes.OrangeRed, 2.0);
    private const double ImageResizeHandleSizeDip = 8.0;

    // 選択中のGroupFrame(グループ枠)のハイライト枠(T-067、SelectedImagePen等と同型)。
    // T-067(5)修正(隠密所見、家老采配2026-07-19、docs-notes/ecad2-t067-5-contextmenu-
    // verification-ninja.md): 固定Solidペンだと、選択中に線種変更してもDiagramRenderer.
    // DrawFramesの線種描画と同一矩形に上塗りされ、変更後の線種が選択解除するまで画面上で常に
    // 隠されて見えていた(削除→Undoで選択解除後は正しい線種が見えた、という忍者一次観測から
    // 因果関係が確定)。BorderStyleに応じたDashStyle付きPenを動的に選択する(DrawingTheme.
    // DashOn/DashOff等、WpfRenderer.Penと同じ倍数値で視覚言語を揃える)。
    private static readonly Pen SelectedFrameSolidPen = new(Brushes.OrangeRed, 2.0);
    private static readonly Pen SelectedFrameDashedPen = CreateSelectedFrameDashPen(DrawingTheme.DashOn, DrawingTheme.DashOff);
    private static readonly Pen SelectedFrameDottedPen = CreateSelectedFrameDashPen(DrawingTheme.DotOn, DrawingTheme.DotOff);

    private static Pen CreateSelectedFrameDashPen(double on, double off)
    {
        var pen = new Pen(Brushes.OrangeRed, 2.0) { DashStyle = new DashStyle(new double[] { on, off }, 0) };
        pen.Freeze();
        return pen;
    }

    // DiagramRenderer.DrawFrames(f.BorderStyle ?? LineStyle.Dashed)と同じフォールバック規約。
    private static Pen SelectedFramePenFor(LineStyle? borderStyle) => (borderStyle ?? LineStyle.Dashed) switch
    {
        LineStyle.Solid => SelectedFrameSolidPen,
        LineStyle.Dotted => SelectedFrameDottedPen,
        _ => SelectedFrameDashedPen,
    };

    // GroupFrameのヒットテスト許容誤差上限(mm、T-067)。GuiEcad原本(MainPage.xaml.cs.HitTestFrame)の
    // margin = Math.Min(CellMm * 0.15, 3.0) をそのまま移植。枠は塗りつぶし無し(点線境界のみ)のため、
    // HitTestImageのような内部全域ヒットではなく、境界線近傍のみをヒット対象とする
    // (GuiEcad側コメント「画像は矩形塗りつぶし相当…枠のように境界のみではない」参照)。
    private const double FrameHitMarginMaxMm = 3.0;

    // 記入中(未確定)の画像挿入プレビュー(T-064、殿裁定「案A」配置待機モード)。確定済み画像との
    // 区別のため半透明の塗り+破線枠にする(ConnectorDraftPen等と同系統の表現)。
    private static readonly Brush ImageDraftFillBrush = CreateImageDraftFillBrush();
    private static readonly Pen ImageDraftPen = CreateDraftPen();

    private static Brush CreateImageDraftFillBrush()
    {
        var brush = new SolidColorBrush(Colors.DodgerBlue) { Opacity = 0.25 };
        brush.Freeze();
        return brush;
    }

    // 直近のDraw()呼び出し内容(T-023)。LadderCanvasAutomationPeer/SymbolAutomationPeerが
    // Draw()の呼び出しタイミングと無関係にいつでも参照できるよう、キャンバス自身の状態として保持する
    // (Drawはビュー外部(MainWindow)から都度呼ばれる素通しメソッドのため、他に保持場所が無い)。
    private Sheet? _lastSheet;
    private PartLibrary? _lastLibrary;
    private GridPos? _lastSelectedCell;

    internal Sheet? CurrentSheet => _lastSheet;
    internal GridPos? SelectedCellForAutomation => _lastSelectedCell;

    protected override AutomationPeer OnCreateAutomationPeer() => new LadderCanvasAutomationPeer(this);

    public void Draw(Sheet sheet, PartLibrary? library = null, GridPos? selectedCell = null,
        VerticalConnector? selectedConnector = null, VerticalConnector? connectorDraft = null,
        WireBreak? selectedWireBreak = null, FreeLine? selectedFreeLine = null,
        FreeLine? freeLineDraft = null, ConnectionDot? selectedConnectionDot = null,
        ImageInsert? selectedImage = null, ImageInsert? imageInsertDraft = null,
        GroupFrame? selectedFrame = null,
        SimState? sim = null)
    {
        _lastSheet = sheet;
        _lastLibrary = library;
        _lastSelectedCell = selectedCell;
        _children.Clear();

        var size = _renderer.PageSize(sheet);
        double widthDip = size.Width * MmToDip;
        double heightDip = size.Height * MmToDip;

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            // DrawingVisualは実際に何か描画された領域のみがヒットテスト対象になる(WPFの仕様)。
            // 罫線・要素が無い空きセルもクリックで拾えるよう、まず背景矩形をページ全体に描画して
            // おく(T-026 OR入力実機検証で発覚: 空き行が常にヒットテスト対象外だった)。
            // T-083 PoC: 従来は常にBrushes.Transparentだったが、作図キャンバス色のテーマ切替
            // (家老采配2026-07-15)に伴い_theme.Backgroundで塗る(ヒットテスト対象になる条件は
            // 「何か描画されていること」のみで不透明である必要はない)。
            var bg = _theme.Background;
            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(bg.A, bg.R, bg.G, bg.B)),
                null, new Rect(0, 0, widthDip, heightDip));

            var wpfRenderer = new WpfRenderer(dc);
            // T-061: sim(テストモード中のみ非null)を渡すと通電配線・励磁要素が通電色でハイライトされる。
            _renderer.Render(wpfRenderer, sheet, library, sim);

            if (selectedCell is { } cell)
                dc.DrawRectangle(null, SelectedCellPen, CellRectDip(cell));

            // 選択中の縦コネクタのハイライト(T-041増分1)。DiagramRenderer.DrawConnectorsと
            // 同じ線分(列境界・TopRow〜BottomRow)を太線で上書き描画する。
            if (selectedConnector is { } connector)
            {
                var (p1, p2) = ConnectorEndpointsDip(connector.Column, connector.TopRow, connector.BottomRow);
                dc.DrawLine(SelectedConnectorPen, p1, p2);
            }

            // 記入中(未確定)の縦コネクタのプレビュー(T-041増分2)。TopRow==BottomRow(まだ範囲を
            // 広げていない)間は線として見えないため、始点位置に短い縦線を出して視認できるようにする。
            if (connectorDraft is { } draft)
            {
                var (p1, p2) = draft.TopRow == draft.BottomRow
                    ? ConnectorEndpointsDip(draft.Column, draft.TopRow, draft.TopRow, extendBottomMm: _renderer.Geometry.CellMm * 0.3)
                    : ConnectorEndpointsDip(draft.Column, draft.TopRow, draft.BottomRow);
                dc.DrawLine(ConnectorDraftPen, p1, p2);
            }

            // 選択中の配線分断のハイライトマーク(T-041増分3)。通常時は無表示(マーク無し・提出品質)
            // のため、選択中である旨を示す小さな塗り円を独自に描く(印刷・PDF出力には出ない画面専用)。
            if (selectedWireBreak is { } wireBreak)
            {
                var geo = _renderer.Geometry;
                double x = geo.X(wireBreak.Boundary) * MmToDip;
                double y = geo.YRow(wireBreak.Row) * MmToDip;
                dc.DrawEllipse(SelectedWireBreakBrush, null, new Point(x, y), geo.CellMm * 0.15 * MmToDip, geo.CellMm * 0.15 * MmToDip);
            }

            // 選択中の自由線のハイライト(T-041増分5)。mm実座標をそのままDIPへ変換して上書き描画する。
            if (selectedFreeLine is { } freeLine)
                dc.DrawLine(SelectedFreeLinePen, FreeLineEndpointDip(freeLine, start: true), FreeLineEndpointDip(freeLine, start: false));

            // 記入中(未確定)の自由線のプレビュー(T-041増分5)。ConnectorDraftPenと同様、まだ伸縮
            // していない(ゼロ長)間も短い線として視認できるよう最低限の長さを描く。
            if (freeLineDraft is { } lineDraft)
            {
                var p1 = FreeLineEndpointDip(lineDraft, start: true);
                var p2 = FreeLineEndpointDip(lineDraft, start: false);
                if (p1 == p2) p2 = new Point(p1.X, p1.Y + _renderer.Geometry.CellMm * 0.3 * MmToDip);
                dc.DrawLine(FreeLineDraftPen, p1, p2);
            }

            // 選択中の接続点のハイライトマーク(T-041増分5、SelectedWireBreakと同型)。
            if (selectedConnectionDot is { } dot)
            {
                double x = dot.XMm * MmToDip;
                double y = dot.YMm * MmToDip;
                double r = _renderer.Geometry.CellMm * 0.15 * MmToDip;
                dc.DrawEllipse(SelectedConnectionDotBrush, null, new Point(x, y), r, r);
            }

            // 選択中の画像のハイライト枠+4隅リサイズハンドル(T-064、殿裁定=ドラッグハンドル新設)。
            if (selectedImage is { } img)
            {
                var rect = ImageRectDip(img);
                dc.DrawRectangle(null, SelectedImagePen, rect);
                foreach (var corner in new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight })
                    dc.DrawRectangle(Brushes.White, SelectedImagePen,
                        new Rect(corner.X - ImageResizeHandleSizeDip / 2, corner.Y - ImageResizeHandleSizeDip / 2,
                            ImageResizeHandleSizeDip, ImageResizeHandleSizeDip));
            }

            // 選択中のGroupFrame(グループ枠)のハイライト(T-067)。DiagramRenderer.DrawFramesと
            // 同じ矩形位置(FrameRectDip)を専用Penで上書き再描画する(画像等と同型パターン、次段階
            // (2)〜(5)=キーボード配線・ドラッグ作成・ラベル編集・右クリックメニューは未実装)。
            if (selectedFrame is { } frame)
                dc.DrawRectangle(null, SelectedFramePenFor(frame.BorderStyle), FrameRectDip(frame));

            // 記入中(未確定)の画像挿入プレビュー(T-064、殿裁定「案A」配置待機モード)。半透明の塗り+
            // 破線枠で配置枠を示す(実画像内容の描画は行わない、シンプルさ優先)。
            if (imageInsertDraft is { } draftImg)
            {
                var rect = ImageRectDip(draftImg);
                dc.DrawRectangle(ImageDraftFillBrush, ImageDraftPen, rect);
            }
        }
        _children.Add(visual);

        Width = widthDip;
        Height = heightDip;
        InvalidateMeasure();
    }

    /// <summary>
    /// クリック位置(ローカルDIP座標)が行コメント記入領域(右母線の右側)にあるか判定し、該当すれば
    /// 行番号を返す(T-080)。GuiEcad原本のヒット領域(xMm > 右母線位置、かつ行が描画範囲内)を踏襲する。
    /// </summary>
    internal int? HitTestRungCommentRow(Point localPositionDip, Sheet sheet)
    {
        // 主回路シートは右母線を描画しない=行コメントの対象外のため、ヒットさせない
        // (T-080往復1周目指摘G: RightBusXはMainCircuit非依存の機械的な列数計算のため、無条件だと
        // 右母線相当の座標帯より右のダブルクリックが、ツールモードを問わず最優先の本判定に
        // 吸い込まれてしまう)。
        if (sheet.MainCircuit) return null;

        (double xMm, double yMm) = ToMm(localPositionDip);
        var geo = _renderer.Geometry;
        if (xMm <= _renderer.RightBusX(sheet.Grid.Columns)) return null;

        int row = geo.RowAt(yMm);
        if (row < 0 || row >= DiagramRenderer.TotalRows(sheet)) return null;
        return row;
    }

    /// <summary>行コメントエディタのアンカー位置をローカルDIP座標で返す(T-080)。X座標は
    /// DrawRungCommentsの描画位置(右母線+RungCommentXOffsetMm)と同じ基準。Y座標は、描画側が
    /// VAlign.Bottom(アンカー=文字下端、文字は行中心YRowから上方向へ展開)のため、行中心から
    /// フォント高さ相当(RungCommentFontSizeMm)を差し引いた文字上端相当とする(T-080往復1周目指摘E:
    /// 行中心のままだと、VerticalAlignment=Topで下方向へ展開する編集ボックスが実描画位置より
    /// 1行分弱下にずれて表示される)。</summary>
    internal Point RungCommentAnchorDip(int row, Sheet sheet)
    {
        var geo = _renderer.Geometry;
        double xMm = _renderer.RightBusX(sheet.Grid.Columns) + DiagramRenderer.RungCommentXOffsetMm;
        double yMm = geo.YRow(row) - DiagramRenderer.RungCommentFontSizeMm;
        return new Point(xMm * MmToDip, yMm * MmToDip);
    }

    /// <summary>
    /// クリック位置(ローカルDIP座標)に十分近い縦コネクタを探す(T-041増分1)。列境界からのmm距離が
    /// 許容誤差以内、かつ行範囲(TopRow〜BottomRow、許容誤差ぶんの余裕含む)内であることを両方
    /// 確認する。同時に複数該当する場合は<see cref="Sheet.Connectors"/>の先頭を優先する
    /// (通常は列位置が異なるため同時該当は起きにくい)。
    /// </summary>
    internal VerticalConnector? HitTestConnector(Point localPositionDip, Sheet sheet)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);
        var geo = _renderer.Geometry;

        foreach (var c in sheet.Connectors)
        {
            if (Math.Abs(xMm - geo.X(c.Column)) > ConnectorHitToleranceMm) continue;
            double yTop = geo.YRow(c.TopRow);
            double yBot = geo.YRow(c.BottomRow);
            if (yMm < yTop - ConnectorHitToleranceMm || yMm > yBot + ConnectorHitToleranceMm) continue;
            return c;
        }
        return null;
    }

    /// <summary>
    /// クリック位置(ローカルDIP座標)が選択中の縦コネクタ(<paramref name="connector"/>、選択済み前提)の
    /// 本体/端点いずれに近いか判定する(T-041増分7、ドラッグ操作用)。HitTestConnectorが「複数候補
    /// から選ぶ」のに対し、こちらは選択済みの1本に対して「本体/端点どちらを掴んだか」を判定する
    /// (PoC=poc/t041-drag-poc/T041DragPoc/DragCanvas.csのHitTestを移植)。null=対象外(許容誤差外)。
    /// P-039(殿裁定、列ドラッグ対応)で確認: この列ズレ制限(ConnectorHitToleranceMm)は「ドラッグを
    /// 開始するための掴む精度」であり、開始後にUpdateDragConnectorが列位置を動かせることとは独立
    /// (本体をつまむにはコネクタの線の近くをクリックする必要がある、という制約はそのまま妥当)。
    /// よって変更不要と判断する。
    /// </summary>
    internal (bool IsEndpoint, bool IsTop)? HitTestConnectorDragMode(Point localPositionDip, VerticalConnector connector)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);
        var geo = _renderer.Geometry;
        if (Math.Abs(xMm - geo.X(connector.Column)) > ConnectorHitToleranceMm) return null;

        double yTop = geo.YRow(connector.TopRow), yBot = geo.YRow(connector.BottomRow);
        if (yMm < yTop - ConnectorHitToleranceMm || yMm > yBot + ConnectorHitToleranceMm) return null;

        if (Math.Abs(yMm - yTop) <= ConnectorHitToleranceMm) return (true, true);
        if (Math.Abs(yMm - yBot) <= ConnectorHitToleranceMm) return (true, false);
        return (false, false);
    }

    /// <summary>ローカルDIP座標を(行, 列境界0.5刻み)へ変換する(T-041増分7、WireBreakドラッグ用)。
    /// GridGeometry.BoundaryAtHalfで縦コネクタ/配線分断の記入時と同じ0.5刻みにスナップする。</summary>
    internal (int Row, double Boundary) ToRowBoundary(Point localPositionDip)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);
        var geo = _renderer.Geometry;
        return (geo.RowAt(yMm), geo.BoundaryAtHalf(xMm));
    }

    /// <summary>
    /// クリック位置(ローカルDIP座標)に十分近い配線分断を探す(T-041増分3)。列境界(Boundary)・行
    /// (Row)ともmm距離が許容誤差以内であることを確認する(HitTestConnectorが線分への距離を見るのに
    /// 対し、こちらは単純な1点への距離)。
    /// </summary>
    internal WireBreak? HitTestWireBreak(Point localPositionDip, Sheet sheet)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);
        var geo = _renderer.Geometry;

        foreach (var b in sheet.WireBreaks)
        {
            if (Math.Abs(xMm - geo.X(b.Boundary)) > WireBreakHitToleranceMm) continue;
            if (Math.Abs(yMm - geo.YRow(b.Row)) > WireBreakHitToleranceMm) continue;
            return b;
        }
        return null;
    }

    /// <summary>
    /// クリック位置(ローカルDIP座標)が選択中の自由線(<paramref name="line"/>、選択済み前提)の
    /// 本体/端点いずれに近いか判定する(T-041増分7、ドラッグ操作用)。HitTestConnectorDragModeの
    /// mm実座標版。null=対象外(許容誤差外)。
    /// </summary>
    internal (bool IsEndpoint, bool IsStart)? HitTestFreeLineDragMode(Point localPositionDip, FreeLine line)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);

        double dStart = Math.Sqrt(Math.Pow(xMm - line.X1Mm, 2) + Math.Pow(yMm - line.Y1Mm, 2));
        double dEnd = Math.Sqrt(Math.Pow(xMm - line.X2Mm, 2) + Math.Pow(yMm - line.Y2Mm, 2));
        if (dStart <= FreeLineHitToleranceMm && dStart <= dEnd) return (true, true);
        if (dEnd <= FreeLineHitToleranceMm) return (true, false);

        double bodyDistance = DistancePointToSegment(xMm, yMm, line.X1Mm, line.Y1Mm, line.X2Mm, line.Y2Mm);
        if (bodyDistance <= FreeLineHitToleranceMm) return (false, false);
        return null;
    }

    /// <summary>ローカルDIP座標をmm実座標へ変換する(T-041増分7、FreeLine/ConnectionDotドラッグ用)。
    /// 既存のprivate ToMmをView外部(MainWindow.xaml.cs)へ公開する薄いラッパー。</summary>
    internal (double XMm, double YMm) ToMmPoint(Point localPositionDip) => ToMm(localPositionDip);

    /// <summary>右母線のX座標(mm)。private _renderer.RightBusXをView外部へ公開する薄いラッパー
    /// (ToMmPointと同型)。診断ログ用に導入したが(T-080往復2周目)、診断ログ除去後もテスト容易性
    /// (RungCommentHitTestTestsがHitTestRungCommentRowのヒット境界を検証するために使用)のため
    /// 存置する。</summary>
    internal double RightBusXMm(int columns) => _renderer.RightBusX(columns);

    /// <summary>
    /// クリック位置(ローカルDIP座標)に十分近い自由線を探す(T-041増分5)。点と線分の距離計算
    /// (PoC=poc/t041-freeline-hittest-poc/Program.cs)を移植。<see cref="HitTestConnector"/>の
    /// 「先頭一致」とは異なり、全候補の中から最短距離(nearest-wins)のものを選ぶ(PoC所見、複数の
    /// 自由線が近接する場合に意図しない方を選ばないための設計)。
    /// </summary>
    internal FreeLine? HitTestFreeLine(Point localPositionDip, Sheet sheet)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);

        FreeLine? best = null;
        double bestDistance = double.MaxValue;
        foreach (var line in sheet.FreeLines)
        {
            double distance = DistancePointToSegment(xMm, yMm, line.X1Mm, line.Y1Mm, line.X2Mm, line.Y2Mm);
            if (distance <= FreeLineHitToleranceMm && distance < bestDistance)
            {
                best = line;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>
    /// クリック位置(ローカルDIP座標)に十分近い接続点を探す(T-041増分5)。<see cref="HitTestFreeLine"/>
    /// と同様nearest-wins方式。
    /// </summary>
    internal ConnectionDot? HitTestConnectionDot(Point localPositionDip, Sheet sheet)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);

        ConnectionDot? best = null;
        double bestDistance = double.MaxValue;
        foreach (var dot in sheet.ConnectionDots)
        {
            double dx = xMm - dot.XMm, dy = yMm - dot.YMm;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= ConnectionDotHitToleranceMm && distance < bestDistance)
            {
                best = dot;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>クリック位置(ローカルDIP座標)にヒットする画像を探す(T-064)。GuiEcad同様「背面固定
    /// 描画のため他要素より判定優先度が最後」の設計方針(呼び出し元で他ヒットテストの後に呼ぶ想定)。
    /// 矩形内ヒット(面積を持つため許容誤差は不要)。後から挿入された画像ほど手前に見える想定で
    /// 配列末尾から探す(nearest-winsではなく最前面優先)。</summary>
    internal ImageInsert? HitTestImage(Point localPositionDip, Sheet sheet)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);
        for (int i = sheet.Images.Count - 1; i >= 0; i--)
        {
            var img = sheet.Images[i];
            if (xMm >= img.XMm && xMm <= img.XMm + img.WidthMm && yMm >= img.YMm && yMm <= img.YMm + img.HeightMm)
                return img;
        }
        return null;
    }

    /// <summary>選択中の画像のリサイズハンドル(4隅)上にクリック位置があるか判定する(T-064、
    /// ドラッグハンドル方式、殿裁定)。ヒットすれば掴んだ隅を返す。</summary>
    internal Ecad2.App.ViewModels.ImageResizeHandle? HitTestImageResizeHandle(Point localPositionDip, ImageInsert image)
    {
        var rect = ImageRectDip(image);
        double half = ImageResizeHandleSizeDip / 2 + 2.0; // ハンドル本体+若干の許容誤差
        (Point Corner, Ecad2.App.ViewModels.ImageResizeHandle Handle)[] handles =
        {
            (rect.TopLeft, Ecad2.App.ViewModels.ImageResizeHandle.TopLeft),
            (rect.TopRight, Ecad2.App.ViewModels.ImageResizeHandle.TopRight),
            (rect.BottomLeft, Ecad2.App.ViewModels.ImageResizeHandle.BottomLeft),
            (rect.BottomRight, Ecad2.App.ViewModels.ImageResizeHandle.BottomRight),
        };
        foreach (var (corner, handle) in handles)
        {
            if (Math.Abs(localPositionDip.X - corner.X) <= half && Math.Abs(localPositionDip.Y - corner.Y) <= half)
                return handle;
        }
        return null;
    }

    /// <summary>点(px,py)と線分((x1,y1)-(x2,y2))の距離(mm)。PoC(poc/t041-freeline-hittest-poc)で
    /// 検証済みのロジックをそのまま移植。</summary>
    private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        double t = ((px - x1) * dx + (py - y1) * dy) / lenSq;
        t = Math.Clamp(t, 0.0, 1.0);
        double cx = x1 + t * dx, cy = y1 + t * dy;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    /// <summary>グリッドセル位置をmm実座標(グリッドに依存しないFreeLine/ConnectionDot用)へ変換する
    /// (T-041増分5)。SelectedCellをFreeLine始点・ConnectionDot位置のmm座標へ変換する際に使う
    /// (MainWindow.xaml.csのTryBeginFreeLineDraft/TryPlaceConnectionDotから呼ぶ)。</summary>
    internal (double XMm, double YMm) CellToMm(GridPos cell)
    {
        var geo = _renderer.Geometry;
        return (geo.X(cell.Column), geo.YRow(cell.Row));
    }

    /// <summary>グリッド1セル分のmm寸法(T-041増分5、自由線記入の矢印キー1回分の移動量に使う)。</summary>
    internal double CellMm => _renderer.Geometry.CellMm;

    /// <summary>FreeLineの始点/終点をローカルDIP座標へ変換する(T-041増分5)。</summary>
    private static Point FreeLineEndpointDip(FreeLine line, bool start)
        => start ? new Point(line.X1Mm * MmToDip, line.Y1Mm * MmToDip) : new Point(line.X2Mm * MmToDip, line.Y2Mm * MmToDip);

    /// <summary>ImageInsertの矩形(mm実座標)をローカルDIP座標へ変換する(T-064)。</summary>
    private static Rect ImageRectDip(ImageInsert image)
        => new(image.XMm * MmToDip, image.YMm * MmToDip, image.WidthMm * MmToDip, image.HeightMm * MmToDip);

    /// <summary>GroupFrameの矩形をmm実座標で返す(T-067)。DiagramRenderer.DrawFramesと同じ計算式
    /// (Visual*Mm優先、無ければTopLeft/Width/Height由来)をView側で再現する(HitTestConnector等の
    /// 既存パターン踏襲、Core層描画ロジックの重複はやむを得ない設計)。殿裁定=配置単位はグリッド
    /// セル単位のため新規作成の枠はVisual*Mmが常にnullだが、旧ファイル互換で値が入っている場合も
    /// 描画位置とヒットテスト位置を一致させるため描画側と同じフォールバック式を用いる。</summary>
    private Rect FrameRectMm(GroupFrame frame)
    {
        var geo = _renderer.Geometry;
        double x = frame.VisualXMm ?? geo.X(frame.TopLeft.Column);
        double y = frame.VisualYMm ?? (geo.YRow(frame.TopLeft.Row) - geo.CellMm * 0.4);
        double w = frame.VisualWidthMm ?? frame.Width * geo.CellMm;
        double h = frame.VisualHeightMm ?? frame.Height * geo.CellMm;
        // 隠密レビュー指摘(T-067(1)、2026-07-18): 旧ファイル互換のVisual*Mm・破損データ経由で
        // 負値が入るとRectコンストラクタがArgumentExceptionを投げるため非負へクランプする
        // (描画側DiagramRenderer.DrawFramesはIRenderer実装が負値を許容するため例外にならないが、
        // Rect構築はWPF側の検証が働く)。
        return new Rect(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    /// <summary>GroupFrameの矩形をローカルDIP座標へ変換する(T-067、ImageRectDipと同型)。</summary>
    private Rect FrameRectDip(GroupFrame frame)
    {
        var mm = FrameRectMm(frame);
        return new Rect(mm.X * MmToDip, mm.Y * MmToDip, mm.Width * MmToDip, mm.Height * MmToDip);
    }

    /// <summary>枠ラベルエディタのアンカー位置をローカルDIP座標で返す(T-067(4))。X座標はDrawFramesの
    /// 描画位置(枠左上+1.0mm)と同じ基準。Y座標は、描画側がVAlign.Bottom(アンカー=文字下端、文字は
    /// 枠上辺から上方向へ展開)のため、枠上辺のラベル位置からフォント高さ相当
    /// (DiagramRenderer.FrameLabelFontSizeMm)を差し引いた文字上端相当とする
    /// (RungCommentAnchorDipと同型パターン)。</summary>
    internal Point FrameLabelAnchorDip(GroupFrame frame)
    {
        var rect = FrameRectMm(frame);
        double xMm = rect.X + 1.0;
        double yMm = rect.Y - _renderer.Geometry.CellMm * DiagramRenderer.FrameLabelOffsetYCellRatio
            - DiagramRenderer.FrameLabelFontSizeMm;
        return new Point(xMm * MmToDip, yMm * MmToDip);
    }

    /// <summary>クリック位置(ローカルDIP座標)にヒットするGroupFrame(グループ枠)を探す(T-067)。
    /// GuiEcad原本(MainPage.xaml.cs.HitTestFrame)を移植: 枠は塗りつぶし無し(点線境界のみ)のため
    /// 内部全域ではなく境界線近傍(margin付き)のみをヒット対象とする。margin込みの矩形内かつ、
    /// いずれかの辺の近傍(onBorderX/onBorderY)であることを要求する。複数該当時は面積最小(入れ子の
    /// 枠がある場合に内側の小さい枠を優先)を返す。</summary>
    internal GroupFrame? HitTestFrame(Point localPositionDip, Sheet sheet)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);
        var geo = _renderer.Geometry;
        double margin = Math.Min(geo.CellMm * 0.15, FrameHitMarginMaxMm);

        GroupFrame? best = null;
        double bestArea = double.MaxValue;
        foreach (var f in sheet.Frames)
        {
            var rect = FrameRectMm(f);
            bool insideX = xMm >= rect.X - margin && xMm <= rect.Right + margin;
            bool insideY = yMm >= rect.Y - margin && yMm <= rect.Bottom + margin;
            bool onBorderX = xMm <= rect.X + margin || xMm >= rect.Right - margin;
            bool onBorderY = yMm <= rect.Y + margin || yMm >= rect.Bottom - margin;
            if (!insideX || !insideY || !(onBorderX || onBorderY)) continue;

            double area = (double)f.Width * f.Height;
            if (area < bestArea) { best = f; bestArea = area; }
        }
        return best;
    }

    /// <summary>描画内容を消去する(T-019: Document.Sheets.Count==0の空状態で使う。
    /// 前回シートの残像を残さない)。</summary>
    public void Clear()
    {
        _lastSheet = null;
        _lastLibrary = null;
        _lastSelectedCell = null;
        _children.Clear();
        Width = 0;
        Height = 0;
        InvalidateMeasure();
    }

    /// <summary>
    /// 縦コネクタ(列境界・TopRow〜BottomRow)の2端点をローカルDIP座標へ変換する(T-041増分2隠密
    /// レビュー所見D、選択ハイライト・記入中プレビューで重複していた組み立てロジックを集約)。
    /// <paramref name="extendBottomMm"/>を指定すると下端をmm単位で延長する(記入直後のTopRow==
    /// BottomRow=ゼロ長時に短い縦線として視認できるようにするための特例)。
    /// </summary>
    private (Point Top, Point Bottom) ConnectorEndpointsDip(double column, int topRow, int bottomRow, double extendBottomMm = 0)
    {
        var geo = _renderer.Geometry;
        double x = geo.X(column) * MmToDip;
        double yTop = geo.YRow(topRow) * MmToDip;
        double yBot = geo.YRow(bottomRow) * MmToDip + extendBottomMm * MmToDip;
        return (new Point(x, yTop), new Point(x, yBot));
    }

    /// <summary>
    /// このキャンバス自身のローカル座標系（DIP単位、LayoutTransform適用前の内部座標）を
    /// グリッド座標へ変換する。要素配置（T-016）のクリック位置判定に使う。
    /// </summary>
    public GridPos ToGridPos(Point localPositionDip)
    {
        (double xMm, double yMm) = ToMm(localPositionDip);
        var geo = _renderer.Geometry;
        return new GridPos(geo.RowAt(yMm), geo.ColAt(xMm));
    }

    /// <summary>ローカルDIP座標をmm座標へ変換する(T-041増分1隠密レビュー指摘=Reuse、ToGridPos/
    /// HitTestConnectorで重複していたDIP→mm変換を集約)。</summary>
    private static (double XMm, double YMm) ToMm(Point localPositionDip)
        => (localPositionDip.X / MmToDip, localPositionDip.Y / MmToDip);

    /// <summary>グリッドセル1つ分のローカル矩形(DIP単位)。選択ハイライト描画とSymbolAutomationPeer
    /// のGetBoundingRectangleCore(T-023)の両方で使う共通座標計算。</summary>
    internal Rect CellRectDip(GridPos cell)
    {
        var geo = _renderer.Geometry;
        double x = geo.X(cell.Column) * MmToDip;
        double y = (geo.YRow(cell.Row) - geo.CellMm / 2) * MmToDip;
        double cellSize = geo.CellMm * MmToDip;
        return new Rect(x, y, cellSize, cellSize);
    }

    /// <summary>
    /// 要素の表示名解決(T-023、UI Automation Name用)。MainWindowViewModel.
    /// SelectedElementKindDisplay/KindDisplayNameと同じ規則(PartIdがあれば図形定義名、無ければ
    /// Kindの日本語ラダー用語)を踏襲する(T-031方針: UI表示は日本語ラダー用語で統一)。
    /// </summary>
    internal string DisplayNameFor(ElementInstance element)
    {
        if (element.PartId is string partId && _lastLibrary is not null
            && _lastLibrary.ById.TryGetValue(partId, out var def))
            return def.Name;
        return KindDisplayName(element.Kind);
    }

    private static string KindDisplayName(ElementKind kind) => kind switch
    {
        ElementKind.ContactNO => "a接点",
        ElementKind.ContactNC => "b接点",
        ElementKind.Coil => "コイル",
        ElementKind.Lamp => "ランプ",
        ElementKind.PushButtonNO => "押しボタン(NO)",
        ElementKind.PushButtonNC => "押しボタン(NC)",
        ElementKind.SelectSwitch => "セレクトSW",
        ElementKind.Terminal => "端子台",
        ElementKind.Timer => "タイマ",
        ElementKind.Counter => "カウンタ",
        _ => kind.ToString(),
    };
}
