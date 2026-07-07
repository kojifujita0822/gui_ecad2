using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Rendering.Wpf;

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
    // ShowGrid=true: 作図ガイドの薄いグリッド線を画面表示する(T-030、殿裁定)。実機テストで
    // 行位置を目視で合わせやすくする狙い。PDF出力(Ecad2.Pdf)側は別のDiagramRendererインスタンス
    // を使うため、ここでの設定は画面表示にのみ影響する。
    private readonly DiagramRenderer _renderer = new(options: new Ecad2.Rendering.RenderOptions { ShowGrid = true });

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

    // 記入中(未確定)の縦コネクタのプレビュー線(T-041増分2)。確定済みの選択ハイライトと区別する
    // ため破線にする(DashStyle未共有=独立インスタンスにしないとFreeze例外になるため個別生成)。
    private static readonly Pen ConnectorDraftPen = CreateConnectorDraftPen();

    private static Pen CreateConnectorDraftPen()
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
        WireBreak? selectedWireBreak = null)
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
            // 罫線・要素が無い空きセルもクリックで拾えるよう、まず透明な背景矩形をページ全体に
            // 描画しておく(T-026 OR入力実機検証で発覚: 空き行が常にヒットテスト対象外だった)。
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, widthDip, heightDip));

            var wpfRenderer = new WpfRenderer(dc);
            _renderer.Render(wpfRenderer, sheet, library);

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
        }
        _children.Add(visual);

        Width = widthDip;
        Height = heightDip;
        InvalidateMeasure();
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
