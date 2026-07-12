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
    // WpfRenderer内部のK(mm→DIP)と同じ換算率(LadderCanvasと同一定数)。呼び出し元(PdfPreviewDialog)が
    // ダイアログ幅に対する相対フィットscaleを計算する際にも参照する(T-060隠密静的レビュー指摘D対応)。
    public const double MmToDip = 96.0 / 25.4;

    private readonly VisualCollection _children;

    public PdfPreviewCanvas() => _children = new VisualCollection(this);

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    /// <summary>ページ内容を描画する。<paramref name="pageSizeMm"/>はDiagramRendererが返す
    /// ページ寸法(mm)、<paramref name="scale"/>はmm→DIP変換後にさらに掛ける最終スケール
    /// (呼び出し元がダイアログ幅に対する相対フィットで計算する、GuiEcad原本のzoom*(availW-40)/pageWidthMm
    /// と同じ意味、T-060隠密静的レビュー指摘D対応)。<paramref name="render"/>は実際に
    /// DiagramRenderer.Render/RenderCrossRefPage/RenderBomPageのいずれかを呼ぶデリゲート。</summary>
    public void DrawPage(Size2D pageSizeMm, double scale, Action<IRenderer> render)
    {
        _children.Clear();

        double widthDip = pageSizeMm.Width * MmToDip * scale;
        double heightDip = pageSizeMm.Height * MmToDip * scale;

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, widthDip, heightDip));
            dc.PushTransform(new ScaleTransform(scale, scale));
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
