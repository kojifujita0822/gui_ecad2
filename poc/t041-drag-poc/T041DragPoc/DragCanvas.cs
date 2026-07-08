using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Rendering.Wpf;

namespace T041DragPoc;

/// <summary>
/// T-041増分6 PoC専用キャンバス。VerticalConnector1本を対象に、マウスドラッグによる
/// 本体移動・端点リサイズ・Escキャンセルの技術的成立性を検証する(実装プラン4.2節の
/// 確認項目1〜4に対応)。既存src/Ecad2.App/Views/LadderCanvas.csの描画・ヒットテスト
/// パターン(DrawingVisualホスト・GridGeometry・DiagramRenderer)を踏襲するが、本体src/へは
/// 一切変更を加えない(poc/隔離ルール)。
/// </summary>
public sealed class DragCanvas : FrameworkElement
{
    private const double MmToDip = 96.0 / 25.4;

    // 4.2節②: クリックとドラッグを区別する移動量しきい値(WPFの既定ドラッグ検出目安=SystemParameters
    // .MinimumHorizontalDragDistance相当を参考にした値)。
    private const double DragStartThresholdDip = 4.0;

    // 4.2節③: 端点近傍と判定する許容誤差(mm)。既存LadderCanvas.ConnectorHitToleranceMmと同値。
    private const double EndpointGrabToleranceMm = 2.0;
    private const double BodyHitToleranceMm = 2.0;

    private readonly VisualCollection _children;
    private readonly DiagramRenderer _renderer = new(options: new Ecad2.Rendering.RenderOptions { ShowGrid = true });
    private readonly Sheet _sheet;
    private readonly VerticalConnector _connector;

    /// <summary>PoC専用の状態表示(MainWindow.xaml.csがTextBlockへ反映する)。</summary>
    public event Action<string>? StatusChanged;

    private enum DragMode { None, Move, ResizeTop, ResizeBottom }

    private DragMode _mode = DragMode.None;
    private bool _dragStarted;
    private Point _pressPositionDip;
    private int _origTopRow;
    private int _origBottomRow;

    public DragCanvas()
    {
        _children = new VisualCollection(this);
        Focusable = true;
        // 親ScrollViewer(MainWindow.xaml)にFocusManager.IsFocusScope="True"を設定した上でないと、
        // Keyboard.Focus(this)が実際には効かずScrollViewer側にフォーカスが残ってしまう
        // (Escキーがこのキャンバスまで届かない不具合として顕在化、既存LadderCanvas.csの実装
        // コメント通りの既知パターン。詳細はREADME.md参照)。
        PreviewMouseLeftButtonDown += (_, _) => Keyboard.Focus(this);

        _sheet = new Sheet
        {
            PageNumber = 1,
            Name = "PoCシート",
            Grid = new GridSpec { Rows = 12, Columns = 10 },
        };
        _connector = new VerticalConnector { Column = 4.5, TopRow = 2, BottomRow = 7 };
        _sheet.Connectors.Add(_connector);

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        PreviewKeyDown += OnPreviewKeyDown;

        Draw();
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        _mode = HitTest(pos);
        if (_mode == DragMode.None)
        {
            StatusChanged?.Invoke("押下: ヒットなし(本体・端点いずれの近傍でもない)");
            return;
        }

        _pressPositionDip = pos;
        _dragStarted = false;
        _origTopRow = _connector.TopRow;
        _origBottomRow = _connector.BottomRow;
        CaptureMouse();
        StatusChanged?.Invoke($"押下: モード={ModeLabel(_mode)}(しきい値{DragStartThresholdDip}px超えでドラッグ開始)");
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_mode == DragMode.None || !IsMouseCaptured) return;
        var pos = e.GetPosition(this);

        if (!_dragStarted)
        {
            double dist = (pos - _pressPositionDip).Length;
            if (dist < DragStartThresholdDip) return;   // 4.2節②: しきい値未満はクリックのまま何もしない
            _dragStarted = true;
        }

        var geo = _renderer.Geometry;
        int row = geo.RowAt(pos.Y / MmToDip);
        int pressRow = geo.RowAt(_pressPositionDip.Y / MmToDip);

        switch (_mode)
        {
            case DragMode.Move:
                int deltaRow = row - pressRow;
                _connector.TopRow = Math.Clamp(_origTopRow + deltaRow, 0, _sheet.Grid.Rows);
                _connector.BottomRow = Math.Clamp(_origBottomRow + deltaRow, 0, _sheet.Grid.Rows);
                break;
            case DragMode.ResizeTop:
                _connector.TopRow = Math.Clamp(row, 0, _connector.BottomRow);
                break;
            case DragMode.ResizeBottom:
                _connector.BottomRow = Math.Clamp(row, _connector.TopRow, _sheet.Grid.Rows);
                break;
        }

        // 4.2節①: ドラッグ中の毎フレーム再描画コストを実測する(マウスキャプチャ中のMouseMoveは
        // WPFが高頻度で発火させるため、ここでのRenderコストがそのまま実用上の応答性の目安になる)。
        var sw = Stopwatch.StartNew();
        Draw();
        sw.Stop();
        StatusChanged?.Invoke($"ドラッグ中: モード={ModeLabel(_mode)} Top={_connector.TopRow} Bottom={_connector.BottomRow} " +
            $"| Render 1回={sw.Elapsed.TotalMilliseconds:F2}ms (60fps許容枠16.6ms)");
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_mode == DragMode.None) return;
        ReleaseMouseCapture();
        StatusChanged?.Invoke(_dragStarted
            ? $"確定: モード={ModeLabel(_mode)} Top={_connector.TopRow} Bottom={_connector.BottomRow}"
            : "クリック扱い(ドラッグしきい値未満のため無変化)");
        _mode = DragMode.None;
        _dragStarted = false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 4.2節④: ドラッグ中(マウスキャプチャ中)のみEscで掴んだ位置へ復元する。
        if (e.Key != Key.Escape || _mode == DragMode.None || !IsMouseCaptured) return;

        _connector.TopRow = _origTopRow;
        _connector.BottomRow = _origBottomRow;
        ReleaseMouseCapture();
        _mode = DragMode.None;
        _dragStarted = false;
        Draw();
        StatusChanged?.Invoke($"Escでキャンセル: 掴んだ位置へ復元(Top={_origTopRow} Bottom={_origBottomRow})");
        e.Handled = true;
    }

    /// <summary>クリック位置が本体(中央寄り)/上端点/下端点のいずれに近いか判定する(4.2節③)。
    /// 端点からEndpointGrabToleranceMm以内なら該当端点のリサイズ、それ以外で線分近傍
    /// (LadderCanvas.HitTestConnectorと同じ許容誤差)なら本体移動と判定する。</summary>
    private DragMode HitTest(Point posDip)
    {
        double xMm = posDip.X / MmToDip, yMm = posDip.Y / MmToDip;
        var geo = _renderer.Geometry;
        if (Math.Abs(xMm - geo.X(_connector.Column)) > BodyHitToleranceMm) return DragMode.None;

        double yTop = geo.YRow(_connector.TopRow), yBot = geo.YRow(_connector.BottomRow);
        if (yMm < yTop - BodyHitToleranceMm || yMm > yBot + BodyHitToleranceMm) return DragMode.None;

        if (Math.Abs(yMm - yTop) <= EndpointGrabToleranceMm) return DragMode.ResizeTop;
        if (Math.Abs(yMm - yBot) <= EndpointGrabToleranceMm) return DragMode.ResizeBottom;
        return DragMode.Move;
    }

    private static string ModeLabel(DragMode mode) => mode switch
    {
        DragMode.Move => "本体移動",
        DragMode.ResizeTop => "端点リサイズ(上端)",
        DragMode.ResizeBottom => "端点リサイズ(下端)",
        _ => "なし",
    };

    /// <summary>4.2節①: 対話操作を介さず、実際のDiagramRenderer.Renderの1回あたりコストを
    /// 自動計測する(起動直後に自動実行、対話ドラッグ操作の実測と合わせて定量評価する)。</summary>
    public void RunRenderBenchmark(int iterations = 500)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) Draw();
        sw.Stop();
        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        StatusChanged?.Invoke($"起動時ベンチマーク: Render {iterations}回, 平均{avgMs:F3}ms/回 " +
            $"(60fps許容枠16.6ms以内なら毎フレーム再描画しても余裕)。ドラッグして実測比較可。");
    }

    private void Draw()
    {
        _children.Clear();
        var size = _renderer.PageSize(_sheet);
        double widthDip = size.Width * MmToDip, heightDip = size.Height * MmToDip;

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, widthDip, heightDip));
            var wpfRenderer = new WpfRenderer(dc);
            _renderer.Render(wpfRenderer, _sheet);

            var geo = _renderer.Geometry;
            var pen = new Pen(_mode == DragMode.None ? Brushes.OrangeRed : Brushes.DodgerBlue, 3.5);
            double x = geo.X(_connector.Column) * MmToDip;
            double yTop = geo.YRow(_connector.TopRow) * MmToDip;
            double yBot = geo.YRow(_connector.BottomRow) * MmToDip;
            dc.DrawLine(pen, new Point(x, yTop), new Point(x, yBot));
        }
        _children.Add(visual);

        Width = widthDip;
        Height = heightDip;
        InvalidateMeasure();
    }
}
