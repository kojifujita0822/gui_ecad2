using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Rendering.Wpf;

namespace Ecad2.App.Tests;

/// <summary>
/// T-043隠密レビュー指摘の回帰テスト: Explorerコピー由来でId再採番されたパーツ(T-035、
/// IsOrEligible/Roleは維持されIdのみ変わる)でも、ORa/ORbのサムネイルがツールバー同意匠の
/// グリフのまま表示され続けること(旧Id完全一致判定はこのケースでグリフが欠落する退行だった)。
/// </summary>
public class PartThumbnailRendererTests
{
    private static byte[] GetPixels(System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    [Fact]
    public void Render_IdReassignedCopyOfContactNo_StillRendersOrGlyph()
    {
        var original = BasicPartTemplates.All().Single(d => d.Id == BasicPartTemplates.ContactNOId);
        // T-035再採番後を模擬: Idのみ変わり、IsOrEligible/RoleはPartFolderStoreの書き戻し仕様どおり維持される。
        var copy = new PartDefinition
        {
            Id = "reassigned-guid-simulated",
            Name = original.Name + "のコピー",
            WidthCells = original.WidthCells,
            HeightCells = original.HeightCells,
            Role = original.Role,
            IsOrEligible = original.IsOrEligible,
            Ports = original.Ports,
            Primitives = original.Primitives,
        };
        var library = new PartLibrary();
        library.ById[original.Id] = original;
        library.ById[copy.Id] = copy;

        var originalBitmap = (System.Windows.Media.Imaging.BitmapSource)PartThumbnailRenderer.Render(original, library, isOr: true);
        var copyBitmap = (System.Windows.Media.Imaging.BitmapSource)PartThumbnailRenderer.Render(copy, library, isOr: true);

        Assert.Equal(GetPixels(originalBitmap), GetPixels(copyBitmap));
    }

    [Fact]
    public void Render_SelectSwitch_NeverRendersOrGlyphEvenIfIsOrPassedTrue()
    {
        var selectSwitch = BasicPartTemplates.All().Single(d => d.Id == BasicPartTemplates.SelectSwitchId);
        var library = new PartLibrary();
        library.ById[selectSwitch.Id] = selectSwitch;

        // セレクトSWはIsOrEligible=falseのため、万一isOr=trueで呼ばれてもOrグリフへは分岐しない
        // (T-037で問題になったRole判定によるセレクトSW巻き込みの再発防止)。
        var withOrTrue = (System.Windows.Media.Imaging.BitmapSource)PartThumbnailRenderer.Render(selectSwitch, library, isOr: true);
        var withOrFalse = (System.Windows.Media.Imaging.BitmapSource)PartThumbnailRenderer.Render(selectSwitch, library, isOr: false);

        Assert.Equal(GetPixels(withOrFalse), GetPixels(withOrTrue));
    }
}
