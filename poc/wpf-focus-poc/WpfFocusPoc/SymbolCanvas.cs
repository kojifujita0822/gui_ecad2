using System.Windows;
using System.Windows.Media;

namespace WpfFocusPoc;

public class SymbolCanvas : FrameworkElement
{
    private readonly VisualCollection _children;

    public SymbolCanvas()
    {
        _children = new VisualCollection(this);
    }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];

    public void DrawSymbols(int count)
    {
        _children.Clear();

        const double spacing = 24;
        int cols = (int)Math.Sqrt(count) + 1;

        for (int i = 0; i < count; i++)
        {
            double x = (i % cols) * spacing;
            double y = (i / cols) * spacing;

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                DrawRelaySymbol(dc, x, y);
            }
            _children.Add(visual);
        }

        Width = cols * spacing;
        Height = (count / cols + 1) * spacing;
        InvalidateMeasure();
    }

    private static void DrawRelaySymbol(DrawingContext dc, double x, double y)
    {
        var pen = new Pen(Brushes.Black, 1);
        pen.Freeze();

        dc.DrawLine(pen, new Point(x, y + 3), new Point(x + 6, y + 3));
        dc.DrawLine(pen, new Point(x + 12, y + 3), new Point(x + 18, y + 3));
        dc.DrawEllipse(Brushes.Black, null, new Point(x + 6, y + 3), 1.5, 1.5);
        dc.DrawEllipse(Brushes.Black, null, new Point(x + 12, y + 3), 1.5, 1.5);
        dc.DrawLine(pen, new Point(x + 5, y), new Point(x + 13, y + 6));
    }
}
