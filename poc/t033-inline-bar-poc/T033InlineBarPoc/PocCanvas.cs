using System.Windows;
using System.Windows.Media;

namespace T033InlineBarPoc;

/// <summary>
/// 本体 LadderCanvas(src/Ecad2.App/Views/LadderCanvas.cs)のフォーカス機構・CellRectDipを最小模倣した
/// PoC用キャンバス。Focusable=true+クリックで明示Focus、セル選択ハイライト、配置済み要素の簡易描画、
/// セル→ローカル矩形(CellRect、Popup位置決めに使う)を持つ。グリッドは10行×20列、1セル=32DIP角固定。
/// </summary>
public sealed class PocCanvas : FrameworkElement
{
    public const int Rows = 10;
    public const int Columns = 20;
    public const double CellSize = 32.0;

    private readonly VisualCollection _children;
    private static readonly Pen SelectedCellPen = new(Brushes.OrangeRed, 2.0);
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromRgb(0xD2, 0xD2, 0xD2)), 0.5);

    private readonly Dictionary<(int Row, int Col), string> _placed = new();

    public (int Row, int Col)? SelectedCell { get; private set; }

    public PocCanvas()
    {
        _children = new VisualCollection(this);
        Focusable = true;
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            Focus();
            var p = e.GetPosition(this);
            int col = (int)(p.X / CellSize);
            int row = (int)(p.Y / CellSize);
            if (row >= 0 && row < Rows && col >= 0 && col < Columns)
                SelectCell(row, col);
        };
        Width = Columns * CellSize;
        Height = Rows * CellSize;
        Redraw();
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public void SelectCell(int row, int col)
    {
        SelectedCell = (row, col);
        Redraw();
    }

    public void MoveSelection(int dRow, int dCol)
    {
        var cur = SelectedCell ?? (0, 0);
        int row = Math.Clamp(cur.Item1 + dRow, 0, Rows - 1);
        int col = Math.Clamp(cur.Item2 + dCol, 0, Columns - 1);
        SelectCell(row, col);
    }

    public bool IsSelectedCellOccupied()
        => SelectedCell is { } c && _placed.ContainsKey(c);

    public void Place(string deviceName)
    {
        if (SelectedCell is { } c) _placed[c] = deviceName;
        Redraw();
    }

    public void ClearPlaced()
    {
        _placed.Clear();
        Redraw();
    }

    /// <summary>選択セルの真下にバーを出すための基準矩形(ローカルDIP座標)。本体CellRectDip相当。</summary>
    public Rect CellRect(int row, int col)
        => new(col * CellSize, row * CellSize, CellSize, CellSize);

    private void Redraw()
    {
        _children.Clear();
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, Height));
            for (int r = 0; r <= Rows; r++)
                dc.DrawLine(GridPen, new Point(0, r * CellSize), new Point(Width, r * CellSize));
            for (int c = 0; c <= Columns; c++)
                dc.DrawLine(GridPen, new Point(c * CellSize, 0), new Point(c * CellSize, Height));

            foreach (var ((row, col), name) in _placed)
            {
                var rect = new Rect(col * CellSize + 4, row * CellSize + 4, CellSize - 8, CellSize - 8);
                dc.DrawRectangle(Brushes.LightSteelBlue, new Pen(Brushes.SteelBlue, 1), rect);
                var ft = new FormattedText(name, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Meiryo"), 9, Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(col * CellSize + 3, row * CellSize + 10));
            }

            if (SelectedCell is { } sel)
                dc.DrawRectangle(null, SelectedCellPen,
                    new Rect(sel.Col * CellSize, sel.Row * CellSize, CellSize, CellSize));
        }
        _children.Add(visual);
    }
}
