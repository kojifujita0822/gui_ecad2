using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ecad2.Model;

namespace Ecad2.Rendering.Wpf;

/// <summary>
/// WPF <see cref="DrawingContext"/> による <see cref="IRenderer"/> 実装。
/// 描画はワールド座標 mm。DrawingContext は Push系(Transform/Clip)を共通の Pop() で戻す
/// スタックを内部に持つため、PopTransform/PopClip はどちらも Pop() を呼ぶだけでよい。
/// 設計は gui_ecad の GuiEcad.App/Win2DRenderer.cs（Win2D実装）を踏襲した新規実装
/// （WinUI3のWin2D APIはWPFに無いため直接移植は不可。T-002/T-006のPoCで検証した
/// DrawingVisual/DrawingContext知見を反映）。
/// </summary>
public sealed class WpfRenderer : IRenderer
{
    private const double K = 96.0 / 25.4;   // mm → WPF DIP(1/96インチ)

    // 挿入画像のキャッシュ（ファイルパス→BitmapImage）。WPFのBitmapImageは同期ロード可能なため
    // Win2D版のような非同期プリロードAPIは不要。読込失敗はスキップして描画継続する。
    private static readonly Dictionary<string, BitmapImage?> _bitmapCache = new();

    private readonly DrawingContext _dc;

    public WpfRenderer(DrawingContext dc) => _dc = dc;

    public void PushTransform(double translateX, double translateY, double scale = 1.0)
    {
        var group = new TransformGroup();
        if (scale != 1.0) group.Children.Add(new ScaleTransform(scale, scale));
        group.Children.Add(new TranslateTransform(translateX * K, translateY * K));
        _dc.PushTransform(group);
    }

    public void PopTransform() => _dc.Pop();

    public void PushClip(Rect2D rect)
        => _dc.PushClip(new RectangleGeometry(new Rect(rect.X * K, rect.Y * K, rect.Width * K, rect.Height * K)));

    public void PopClip() => _dc.Pop();

    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke)
        => _dc.DrawLine(Pen(stroke), P(a), P(b));

    public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke)
    {
        if (points.Length < 2) return;
        var pen = Pen(stroke);
        for (int i = 1; i < points.Length; i++)
            _dc.DrawLine(pen, P(points[i - 1]), P(points[i]));
    }

    public void DrawRectangle(Rect2D rect, StrokeStyle stroke)
        => _dc.DrawRectangle(null, Pen(stroke), R(rect));

    public void FillRectangle(Rect2D rect, Color color)
        => _dc.DrawRectangle(Brush(color), null, R(rect));

    public void DrawCircle(Point2D center, double radius, StrokeStyle stroke)
        => _dc.DrawEllipse(null, Pen(stroke), P(center), radius * K, radius * K);

    public void FillCircle(Point2D center, double radius, Color color)
        => _dc.DrawEllipse(Brush(color), null, P(center), radius * K, radius * K);

    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke)
        => _dc.DrawEllipse(null, Pen(stroke), P(center), radiusX * K, radiusY * K);

    public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke)
    {
        double a0 = startDeg * Math.PI / 180.0;
        double a1 = (startDeg + sweepDeg) * Math.PI / 180.0;
        var start = new Point((center.X + radius * Math.Cos(a0)) * K, (center.Y + radius * Math.Sin(a0)) * K);
        var end = new Point((center.X + radius * Math.Cos(a1)) * K, (center.Y + radius * Math.Sin(a1)) * K);
        bool isLargeArc = Math.Abs(sweepDeg) > 180;
        var sweepDirection = sweepDeg > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius * K, radius * K), 0, isLargeArc, sweepDirection,
                      isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        _dc.DrawGeometry(null, Pen(stroke), geo);
    }

    public void DrawText(string text, Point2D position, TextStyle style)
    {
        var ft = FormattedTextOf(text, style);
        double x = style.HAlign switch
        {
            HAlign.Center => position.X * K - ft.Width / 2,
            HAlign.Right => position.X * K - ft.Width,
            _ => position.X * K,
        };
        double y = style.VAlign switch
        {
            VAlign.Middle => position.Y * K - ft.Height / 2,
            VAlign.Bottom => position.Y * K - ft.Height,
            VAlign.Baseline => position.Y * K - ft.Baseline,
            _ => position.Y * K,   // Top
        };
        _dc.DrawText(ft, new Point(x, y));
    }

    public Size2D MeasureText(string text, TextStyle style)
    {
        var ft = FormattedTextOf(text, style);
        return new Size2D(ft.Width / K, ft.Height / K);
    }

    public void DrawImage(string filePath, Rect2D bounds)
    {
        if (!_bitmapCache.TryGetValue(filePath, out var bmp))
        {
            bmp = TryLoad(filePath);
            _bitmapCache[filePath] = bmp;
        }
        if (bmp is not null)
            _dc.DrawImage(bmp, R(bounds));
    }

    private static BitmapImage? TryLoad(string filePath)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception)
        {
            return null; // ファイル欠損・読込不可は無視してスキップ（描画全体は継続）
        }
    }

    private static Point P(Point2D p) => new(p.X * K, p.Y * K);
    private static Rect R(Rect2D r) => new(r.X * K, r.Y * K, r.Width * K, r.Height * K);

    private static Pen Pen(StrokeStyle s)
    {
        var pen = new Pen(Brush(s.Color), Math.Max(s.Width, DrawingTheme.MinStrokeWidthMm) * K)
        {
            StartLineCap = Cap(s.Cap),
            EndLineCap = Cap(s.Cap),
            DashCap = Cap(s.Cap),
        };
        // 破線は線幅倍数のカスタムパターンで PDF/SVG と同一比率に揃える。
        switch (s.Style)
        {
            case LineStyle.Dashed:
                pen.DashStyle = new DashStyle(new double[] { DrawingTheme.DashOn, DrawingTheme.DashOff }, 0);
                break;
            case LineStyle.Dotted:
                pen.DashStyle = new DashStyle(new double[] { DrawingTheme.DotOn, DrawingTheme.DotOff }, 0);
                break;
        }
        pen.Freeze();
        return pen;
    }

    private static SolidColorBrush Brush(Color c)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B));
        brush.Freeze();
        return brush;
    }

    private static PenLineCap Cap(LineCap c) => c switch
    {
        LineCap.Round => PenLineCap.Round,
        LineCap.Square => PenLineCap.Square,
        _ => PenLineCap.Flat,
    };

    private static FormattedText FormattedTextOf(string text, TextStyle style)
    {
        var typeface = new Typeface(new FontFamily(style.FontFamily),
            style.Italic ? FontStyles.Italic : FontStyles.Normal,
            style.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);
        return new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            style.FontSizeMm * K,
            Brush(style.Color),
            1.0);
    }
}
