using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Rendering;

namespace Ecad2.Rendering.Wpf;

/// <summary>
/// 部品(PartDefinition)単体のサムネイル(ImageSource)を生成する(T-015、部品選択リスト用)。
/// DiagramRenderer.DrawPreviewをDrawingVisual→RenderTargetBitmap化する薄いラッパー。
/// </summary>
public static class PartThumbnailRenderer
{
    private const double K = 96.0 / 25.4;   // mm → WPF DIP(1/96インチ)

    // T-043(殿裁定): ORa/ORbはツールバーsF5/sF6(T-040)と同じGX様式グリフ(└┤├┘構図)で統一する。
    // Path Dataは Ecad2.App.Converters.PartEntryToGlyphGeometryConverter のORa/ORbグリフと同一
    // (App→Rendering.Wpfの参照方向のため値を複製している。ツールバー意匠を変える場合は両方を
    // 合わせて直すこと)。座標系は18x18キャンバス基準(x2-16,y3-15相当、T-040の慣例)。
    private static readonly Geometry OrContactNoGlyph =
        Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M6,4 L6,14 M12,4 L12,14 M2,4 L2,9 M16,4 L16,9");
    private static readonly Geometry OrContactNcGlyph =
        Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M6,4 L6,14 M12,4 L12,14 M2,4 L2,9 M16,4 L16,9 M4,15 L14,3");

    static PartThumbnailRenderer()
    {
        OrContactNoGlyph.Freeze();
        OrContactNcGlyph.Freeze();
    }

    /// <summary>partId(PartLibrary内のPartDefinition.Idと一致)の図形を1セル分の正方形サムネイル
    /// として描画する。MarginMm=0・Pos=(0,0)の専用DiagramRendererで原点合わせして描く。
    /// ORa/ORb(isOr=true かつ a接点/b接点)はツールバーsF5/sF6と同じGX様式グリフで描画する(T-043)。
    /// それ以外(ORa/ORb以外の5種、および将来isOr=trueになりうるその他の部品)は従来どおり
    /// PartDefinitionの形状をそのまま描画する。</summary>
    public static ImageSource Render(string partId, PartLibrary library, bool isOr = false, double cellMm = 9.0)
    {
        if (isOr && partId == BasicPartTemplates.ContactNOId) return RenderGlyph(OrContactNoGlyph, cellMm);
        if (isOr && partId == BasicPartTemplates.ContactNCId) return RenderGlyph(OrContactNcGlyph, cellMm);

        var renderer = new DiagramRenderer(options: new RenderOptions { CellMm = cellMm, MarginMm = 0 });
        var element = new ElementInstance { PartId = partId, Pos = new GridPos(0, 0) };

        int sizeDip = (int)Math.Round(cellMm * K);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var wpfRenderer = new WpfRenderer(dc);
            renderer.DrawPreview(wpfRenderer, element, DrawingTheme.Black, library);
        }

        var bitmap = new RenderTargetBitmap(sizeDip, sizeDip, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    // GX様式グリフ(18x18キャンバス基準のGeometry)をsizeDip四方の正方形へ均等スケールして描画する。
    private static ImageSource RenderGlyph(Geometry glyph, double cellMm)
    {
        int sizeDip = (int)Math.Round(cellMm * K);
        double scale = sizeDip / 18.0;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawGeometry(null, new Pen(Brushes.Black, 1.0), glyph);
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(sizeDip, sizeDip, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
