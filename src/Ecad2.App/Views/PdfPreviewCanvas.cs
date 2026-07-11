using System.Windows;
using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Rendering.Wpf;

namespace Ecad2.App.Views;

/// <summary>
/// PDFプレビュー(T-060)の1ページ分を描画するキャンバス。LadderCanvas(T-002 PoC由来の
/// DrawingVisualホストパターン)と同型の新規実装。GuiEcadのWin2D CanvasControl相当を
/// WPFのDrawingVisual+WpfRendererで代替する(WinUI3のWin2D APIはWPFに無いため直接移植は不可)。
/// ページ種別(Sheet/CrossRef/Bom)を問わず「渡された描画デリゲートをIRenderer経由で1回呼ぶ」
/// だけの汎用コンポーネントとし、呼び出し元(PdfPreviewDialog)がページ種別ごとの
/// DiagramRenderer呼び出しを組み立てる。
/// </summary>
public sealed class PdfPreviewCanvas : FrameworkElement
{
    // WpfRenderer内部のK(mm→DIP)と同じ換算率(LadderCanvasと同一定数)。
    private const double MmToDip = 96.0 / 25.4;

    private readonly VisualCollection _children;

    public PdfPreviewCanvas() => _children = new VisualCollection(this);

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    /// <summary>ページ内容を描画する。<paramref name="pageSizeMm"/>はDiagramRendererが返す
    /// ページ寸法(mm)、<paramref name="zoom"/>は表示倍率(1.0=等倍)。<paramref name="render"/>は
    /// 実際にDiagramRenderer.Render/RenderCrossRefPage/RenderBomPageのいずれかを呼ぶデリゲート。</summary>
    public void DrawPage(Size2D pageSizeMm, double zoom, Action<IRenderer> render)
    {
        _children.Clear();

        double widthDip = pageSizeMm.Width * MmToDip * zoom;
        double heightDip = pageSizeMm.Height * MmToDip * zoom;

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, widthDip, heightDip));
            dc.PushTransform(new ScaleTransform(zoom, zoom));
            var renderer = new WpfRenderer(dc);
            render(renderer);
            dc.Pop();
        }
        _children.Add(visual);

        Width = widthDip;
        Height = heightDip;
        InvalidateMeasure();
    }
}
