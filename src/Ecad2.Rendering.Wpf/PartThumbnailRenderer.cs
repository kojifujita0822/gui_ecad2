using System.Globalization;
using System.Windows;
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
    /// として描画する。MarginMm=0・Pos=(0,0)の専用DiagramRendererで原点合わせして描く。isOr=true
    /// の場合、右下に小さな"OR"バッジを合成描画する(T-037、殿裁定=案A)。PartDefinitionの形状のみ
    /// ではOR/非ORの区別がつかない問題(隠密調査所見)への対応。</summary>
    public static ImageSource Render(string partId, PartLibrary library, bool isOr = false, double cellMm = 9.0)
    {
        var renderer = new DiagramRenderer(options: new RenderOptions { CellMm = cellMm, MarginMm = 0 });
        var element = new ElementInstance { PartId = partId, Pos = new GridPos(0, 0) };

        int sizeDip = (int)Math.Round(cellMm * K);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var wpfRenderer = new WpfRenderer(dc);
            renderer.DrawPreview(wpfRenderer, element, DrawingTheme.Black, library);
            if (isOr) DrawOrBadge(dc, sizeDip);
        }

        var bitmap = new RenderTargetBitmap(sizeDip, sizeDip, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    // サムネイル右下に白背景+黒枠+"OR"の小バッジを重ね描きする。
    private static void DrawOrBadge(DrawingContext dc, int sizeDip)
    {
        double badgeSize = sizeDip * 0.42;
        double x = sizeDip - badgeSize;
        double y = sizeDip - badgeSize;
        dc.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 1.0), new Rect(x, y, badgeSize, badgeSize));

        var text = new FormattedText("OR", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), badgeSize * 0.5, Brushes.Black, 96.0 / 96.0);
        dc.DrawText(text, new Point(x + (badgeSize - text.Width) / 2, y + (badgeSize - text.Height) / 2));
    }
}
