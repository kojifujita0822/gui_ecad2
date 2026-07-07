using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ecad2.App.Converters;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-043往復2周目隠密レビュー指摘(docs/ecad2-t043-review-onmitsu-2.md所見1、CONFIRMED)の回帰テスト。
/// Explorerコピー由来でId再採番された基本図形パーツ(T-035、Category/Role/IsOrEligibleは維持され
/// Idのみ変わる)でも、配置バー種別選択コンボボックスのグリフがId非依存に正しく表示され続けること
/// (旧Id完全一致判定はこのケースで汎用フォルダアイコンに落ちる退行だった)。あわせて、Role/IsOrEligible
/// の組が偶然一致する自作パーツ(Category="自作")を既知5種のグリフに巻き込まないことも確認する。
/// </summary>
public class PartEntryToGlyphGeometryConverterTests
{
    private static readonly ImageSource BlankThumbnail = new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);
    private static readonly PartEntryToGlyphGeometryConverter Converter = new();

    private static object? GetGlyph(PartDefinition definition, string category, bool isOr = false)
    {
        var folderEntry = new PartFolderEntry(category, $"{definition.Id}.gcadpart", definition);
        var entry = new PartSelectionEntryViewModel(folderEntry, BlankThumbnail, isOr);
        return Converter.Convert(entry, typeof(Geometry), null, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void GetGlyph_IdReassignedCopyOfContactNo_ReturnsSameGlyphAsOriginal()
    {
        var original = BasicPartTemplates.All().Single(d => d.Id == BasicPartTemplates.ContactNOId);
        var copy = new PartDefinition
        {
            Id = "reassigned-guid-simulated",
            Name = original.Name + "のコピー",
            Role = original.Role,
            IsOrEligible = original.IsOrEligible,
        };

        Assert.Same(GetGlyph(original, ""), GetGlyph(copy, ""));
    }

    [Fact]
    public void GetGlyph_IdReassignedCopyOfOrA_ReturnsSameOrGlyphAsOriginal()
    {
        var original = BasicPartTemplates.All().Single(d => d.Id == BasicPartTemplates.ContactNOId);
        var copy = new PartDefinition
        {
            Id = "reassigned-guid-simulated-2",
            Role = original.Role,
            IsOrEligible = original.IsOrEligible,
        };

        Assert.Same(GetGlyph(original, "", isOr: true), GetGlyph(copy, "", isOr: true));
    }

    [Fact]
    public void GetGlyph_SelectSwitchIdReassignedCopy_ReturnsSameGlyphAsOriginal()
    {
        var original = BasicPartTemplates.All().Single(d => d.Id == BasicPartTemplates.SelectSwitchId);
        var copy = new PartDefinition
        {
            Id = "reassigned-guid-simulated-3",
            Role = original.Role,
            IsOrEligible = original.IsOrEligible,
        };

        Assert.Same(GetGlyph(original, ""), GetGlyph(copy, ""));
    }

    [Fact]
    public void GetGlyph_CustomPartWithBuiltinLikeRole_DoesNotUseBuiltinGlyph()
    {
        var builtinCoil = BasicPartTemplates.All().Single(d => d.Id == BasicPartTemplates.CoilId);
        var customLookalike = new PartDefinition { Id = "custom-lookalike", Role = PartRole.Coil, IsOrEligible = false };

        Assert.NotSame(GetGlyph(builtinCoil, ""), GetGlyph(customLookalike, "自作"));
    }
}
