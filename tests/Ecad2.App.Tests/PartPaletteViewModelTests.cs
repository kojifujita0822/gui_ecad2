using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-054: PartPaletteViewModel.ResolveEntryの照合ロジック(配置バー初期選択・選択中部品表示の
/// 双方で共有)。IsOr込み優先・PartId一致のみへのフォールバック・未知IDでnullを検証する。
/// </summary>
public class PartPaletteViewModelTests : ViewModelTestBase
{
    [Fact]
    public void ResolveEntry_ExactMatchWithOr_ReturnsOrEntry()
    {
        var vm = CreateViewModel();

        var entry = vm.PartPalette.ResolveEntry(BasicPartTemplates.ContactNOId, isOr: true);

        Assert.NotNull(entry);
        Assert.True(entry!.IsOr);
        Assert.Equal(BasicPartTemplates.ContactNOId, entry.Definition.Id);
    }

    [Fact]
    public void ResolveEntry_ExactMatchWithoutOr_ReturnsNormalEntry()
    {
        var vm = CreateViewModel();

        var entry = vm.PartPalette.ResolveEntry(BasicPartTemplates.ContactNOId, isOr: false);

        Assert.NotNull(entry);
        Assert.False(entry!.IsOr);
        Assert.Equal(BasicPartTemplates.ContactNOId, entry.Definition.Id);
    }

    /// <summary>OR版エントリが存在しない部品(Coil等、IsOrEligibleでない)にisOr=trueで問い合わせても、
    /// PartId一致のみへフォールバックし通常版を返す。</summary>
    [Fact]
    public void ResolveEntry_IsOrRequestedButNoOrEntryExists_FallsBackToNormalEntry()
    {
        var vm = CreateViewModel();

        var entry = vm.PartPalette.ResolveEntry(BasicPartTemplates.CoilId, isOr: true);

        Assert.NotNull(entry);
        Assert.False(entry!.IsOr);
        Assert.Equal(BasicPartTemplates.CoilId, entry.Definition.Id);
    }

    [Fact]
    public void ResolveEntry_UnknownPartId_ReturnsNull()
    {
        var vm = CreateViewModel();

        var entry = vm.PartPalette.ResolveEntry("unknown-part-id", isOr: false);

        Assert.Null(entry);
    }
}
