using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Rendering.Wpf;

/// <summary>
/// 部品(PartDefinition)単体のサムネイル(ImageSource)を生成する(T-015、部品選択リスト用)。
/// DiagramRenderer.DrawPreviewをDrawingVisual→RenderTargetBitmap化する薄いラッパー。
/// </summary>
public static class PartThumbnailRenderer
{
    private const double K = 96.0 / 25.4;   // mm → WPF DIP(1/96インチ)

    /// <summary>partId(PartLibrary内のPartDefinition.Idと一致)の図形を1セル分の正方形サムネイル
    /// として描画する。MarginMm=0・Pos=(0,0)の専用DiagramRendererで原点合わせして描く。</summary>
    public static ImageSource Render(string partId, PartLibrary library, double cellMm = 9.0)
    {
        var renderer = new DiagramRenderer(options: new RenderOptions { CellMm = cellMm, MarginMm = 0 });
        var element = new ElementInstance { PartId = partId, Pos = new GridPos(0, 0) };

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var wpfRenderer = new WpfRenderer(dc);
            renderer.DrawPreview(wpfRenderer, element, DrawingTheme.Black, library);
        }

        int sizeDip = (int)Math.Round(cellMm * K);
        var bitmap = new RenderTargetBitmap(sizeDip, sizeDip, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
