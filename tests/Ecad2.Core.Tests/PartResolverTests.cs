using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-052往復1周目・隠密指摘#3: DRC-PART-001判定とJumpToのフォールバック判定で複製していた
/// 「PartId解決可否」ロジックをPartResolver.IsUnresolvedPartIdへ一本化した回帰テスト。
/// </summary>
public class PartResolverTests
{
    [Fact]
    public void IsUnresolvedPartId_PartIdSetButNotInLibrary_ReturnsTrue()
    {
        var elem = new ElementInstance { PartId = "missing-id" };
        var lib = new PartLibrary();

        Assert.True(PartResolver.IsUnresolvedPartId(elem, lib));
    }

    [Fact]
    public void IsUnresolvedPartId_PartIdResolvesInLibrary_ReturnsFalse()
    {
        var elem = new ElementInstance { PartId = "known-id" };
        var lib = new PartLibrary();
        lib.ById["known-id"] = new PartDefinition { Id = "known-id", Name = "自作部品" };

        Assert.False(PartResolver.IsUnresolvedPartId(elem, lib));
    }

    [Fact]
    public void IsUnresolvedPartId_PartIdNull_ReturnsFalse()
    {
        var elem = new ElementInstance { PartId = null };

        Assert.False(PartResolver.IsUnresolvedPartId(elem, lib: null));
    }

    [Fact]
    public void IsUnresolvedPartId_PartIdSetButLibraryNull_ReturnsTrue()
    {
        var elem = new ElementInstance { PartId = "some-id" };

        Assert.True(PartResolver.IsUnresolvedPartId(elem, lib: null));
    }
}
