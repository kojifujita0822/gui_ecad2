using System.Windows;
using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Rendering.Wpf;

namespace Ecad2.App.Views;

/// <summary>
/// ラダー図面を描画するキャンバス。T-002 PoCの SymbolCanvas パターン(DrawingVisualホスト)を踏襲した
/// 新規実装。Ecad2.Core.Rendering.DiagramRenderer(上位描画器)と Ecad2.Rendering.Wpf.WpfRenderer
/// (IRendererのWPF実装、T-007成果物)を使って Sheet を描画する。
/// </summary>
public sealed class LadderCanvas : FrameworkElement
{
    // WpfRenderer内部のK(mm→DIP)と同じ換算率。DiagramRenderer.PageSizeはmm単位を返すため、
    // Width/Height(WPF DIP)へ変換するのはビュー側の責務。
    private const double MmToDip = 96.0 / 25.4;

    private readonly VisualCollection _children;
    private readonly DiagramRenderer _renderer = new();

    public LadderCanvas()
    {
        _children = new VisualCollection(this);
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public void Draw(Sheet sheet, PartLibrary? library = null)
    {
        _children.Clear();

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            var wpfRenderer = new WpfRenderer(dc);
            _renderer.Render(wpfRenderer, sheet, library);
        }
        _children.Add(visual);

        var size = _renderer.PageSize(sheet);
        Width = size.Width * MmToDip;
        Height = size.Height * MmToDip;
        InvalidateMeasure();
    }
}
