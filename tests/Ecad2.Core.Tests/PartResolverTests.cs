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

    // ===== シート種別の枷（T-136(A)増分1、殿裁定2026-07-31＝3値・既定 Any） =====

    [Theory]
    [InlineData(ElementKind.Breaker3P)]
    [InlineData(ElementKind.ContactorMain3P)]
    [InlineData(ElementKind.ThermalOverload3P)]
    public void SheetAffinityOf_主回路3極記号は主回路専用(ElementKind kind)
    {
        // T-133の殿裁定4（3極記号は主回路シート限定）がここに乗る。
        Assert.Equal(SheetAffinity.MainCircuitOnly, ElementCatalog.SheetAffinityOf(kind));
    }

    [Theory]
    [InlineData(ElementKind.ContactNO)]
    [InlineData(ElementKind.Coil)]
    [InlineData(ElementKind.Lamp)]
    [InlineData(ElementKind.Motor)]   // 主回路で使うが3極記号ではない——枷の対象外であることを固定する
    public void SheetAffinityOf_他の組込み種別はどちらのシートにも置ける(ElementKind kind)
        => Assert.Equal(SheetAffinity.Any, ElementCatalog.SheetAffinityOf(kind));

    [Fact]
    public void SheetAffinityOf_自作パーツは定義の側から解決する()
    {
        // 入力の選び方: Kind を主回路専用の3極記号、定義を制御専用と<b>逆に</b>置く。
        // 二分岐を取り違えれば必ず落ちる（両方を同じ値にすると、どちらを見ても通ってしまう）。
        var lib = new PartLibrary();
        lib.ById["p1"] = new PartDefinition { Id = "p1", SheetAffinity = SheetAffinity.ControlOnly };
        var elem = new ElementInstance { PartId = "p1", Kind = ElementKind.Breaker3P };

        Assert.Equal(SheetAffinity.ControlOnly, PartResolver.SheetAffinityOf(elem, lib));
    }

    [Fact]
    public void SheetAffinityOf_組込み種別は種別の側から解決する()
    {
        var elem = new ElementInstance { PartId = null, Kind = ElementKind.Breaker3P };

        Assert.Equal(SheetAffinity.MainCircuitOnly, PartResolver.SheetAffinityOf(elem, lib: null));
    }

    [Fact]
    public void SheetAffinityOf_未解決のPartIdは種別へフォールバックする()
    {
        // ComponentKind が Kind へ静かに落ちるのと同じ扱い。ライブラリを失った要素が
        // 移動すらできなくなるのを避ける。
        var elem = new ElementInstance { PartId = "missing", Kind = ElementKind.ContactNO };

        Assert.Equal(SheetAffinity.Any, PartResolver.SheetAffinityOf(elem, new PartLibrary()));
    }

    [Theory]
    [InlineData(SheetAffinity.Any, false, true)]
    [InlineData(SheetAffinity.Any, true, true)]
    [InlineData(SheetAffinity.ControlOnly, false, true)]        // 制御シート＝MainCircuit が false
    [InlineData(SheetAffinity.ControlOnly, true, false)]
    [InlineData(SheetAffinity.MainCircuitOnly, false, false)]
    [InlineData(SheetAffinity.MainCircuitOnly, true, true)]
    public void IsAllowedOnSheet_3値と2種のシートの全6組合せ(SheetAffinity affinity, bool mainCircuit, bool expected)
    {
        // 6通りを全網羅する。真偽を取り違えても、どれかの組が必ず落ちる形。
        Assert.Equal(expected, PartResolver.IsAllowedOnSheet(affinity, mainCircuit));
    }
}
