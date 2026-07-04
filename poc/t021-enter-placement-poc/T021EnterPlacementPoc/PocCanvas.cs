using System.Windows;
using System.Windows.Media;

namespace T021EnterPlacementPoc;

/// <summary>
/// 本体 LadderCanvas(src/Ecad2.App/Views/LadderCanvas.cs)のフォーカス機構を最小模倣したPoC用キャンバス。
/// Focusable=true＋クリックで明示Focus、セル選択ハイライト、配置済み要素の簡易描画のみを持つ。
/// グリッドは 10行×20列、1セル=32DIP角の固定。フォーカス所在検証が目的なので描画は簡素にする。
/// </summary>
public sealed class PocCanvas : FrameworkElement
{
    public const int Rows = 10;
    public const int Columns = 20;
    public const double CellSize = 32.0;

    private readonly VisualCollection _children;
    private static readonly Pen SelectedCellPen = new(Brushes.OrangeRed, 2.0);
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromRgb(0xD2, 0xD2, 0xD2)), 0.5);

    // (row,col) → デバイス名。配置済み要素の簡易表現。
    private readonly Dictionary<(int Row, int Col), string> _placed = new();

    public (int Row, int Col)? SelectedCell { get; private set; }

    public PocCanvas()
    {
        _children = new VisualCollection(this);
        Focusable = true;
        // 本体 LadderCanvas() と同じ: クリックで自分にフォーカスを取る。
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

    /// <summary>配置済み要素を全消去する(自動ループの各条件をクリーンな状態で比較するため)。</summary>
    public void ClearPlaced()
    {
        _placed.Clear();
        Redraw();
    }

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
