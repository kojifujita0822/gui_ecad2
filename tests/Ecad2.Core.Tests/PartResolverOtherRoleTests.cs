using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// 自作パーツの役割「その他」（<see cref="PartRole.Other"/>、殿ご下命2026-10-09）のテスト。
/// 動作はコイルと同じで、クロスリファレンス検査からは常に除かれる。
/// </summary>
public class PartResolverOtherRoleTests
{
    private const string PartId = "other-role-part";

    private static PartDefinition Part(PartRole role, bool excluded = false) => new()
    {
        Id = PartId,
        Name = "その他役割の検体",
        Role = role,
        IsExcludedFromCrossReference = excluded,
        Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
    };

    private static PartLibrary LibraryOf(PartDefinition part) => new() { ById = { [part.Id] = part } };

    private static LadderDocument DocWithLoad(string deviceName)
    {
        var sheet = new Sheet();
        sheet.Elements.Add(new ElementInstance { Pos = new GridPos(0, 0), PartId = PartId, DeviceName = deviceName });
        return new LadderDocument { Sheets = { sheet } };
    }

    /// <summary>
    /// 役割を足した折の追加漏れを捕まえる網。<see cref="PartResolver.ComponentKind"/> の switch は
    /// 未対応の役割で例外を投げるゆえ、<b>漏れはコンパイルエラーでなく実行時例外として現れる</b>
    /// （T-152 で役割の新設が退けられた理由）。結線に加わる全役割を回して、例外が出ぬことを確かめる。
    /// </summary>
    [Fact]
    public void ComponentKind_EveryRoleThatCreatesComponent_IsMapped()
    {
        foreach (var role in Enum.GetValues<PartRole>())
        {
            var lib = LibraryOf(Part(role));
            var element = new ElementInstance { PartId = PartId };
            if (!PartResolver.CreatesComponent(element, lib))
            {
                Assert.Equal(PartRole.NonSimulated, role);   // 結線に加わらぬのは非シミュレートのみ
                continue;
            }

            var exception = Record.Exception(() => PartResolver.ComponentKind(element, lib));

            Assert.True(exception is null, $"役割 {role} が ComponentKind で写像されておらぬ: {exception?.Message}");
        }
    }

    [Fact]
    public void ComponentKind_OtherRole_BehavesAsCoil()
    {
        var lib = LibraryOf(Part(PartRole.Other));
        var element = new ElementInstance { PartId = PartId };

        Assert.True(PartResolver.CreatesComponent(element, lib));
        Assert.True(PartResolver.ParticipatesInWiring(element, lib));
        Assert.Equal(ElementKind.Coil, PartResolver.ComponentKind(element, lib));
    }

    /// <summary>ネットリスト上は負荷になる（テストモードで通電すれば励磁する側）。</summary>
    [Fact]
    public void Netlist_OtherRole_BecomesLoadComponent()
    {
        var doc = DocWithLoad("SOL1");

        var netlist = NetlistBuilder.Build(doc.Sheets[0], LibraryOf(Part(PartRole.Other)));

        var component = Assert.Single(netlist.Components);
        Assert.Equal((ElementKind.Coil, ComponentRole.Load, "SOL1"), (component.Kind, component.Role, component.DeviceName));
    }

    /// <summary>
    /// 「その他」は印（<see cref="PartDefinition.IsExcludedFromCrossReference"/>）を立てずとも
    /// 死にリレー検査に掛からぬ。対照＝同じ形でも役割がコイルなら掛かる。
    /// </summary>
    [Theory]
    [InlineData(PartRole.Coil, false, true)]
    [InlineData(PartRole.Coil, true, false)]
    [InlineData(PartRole.Other, false, false)]
    [InlineData(PartRole.Other, true, false)]
    public void CheckCrossReference_CoilWithoutContact_ReportedOnlyForPlainCoil(PartRole role, bool excluded, bool expectWarning)
    {
        var diagnostics = DesignRuleCheck.CheckCrossReference(DocWithLoad("SOL1"), LibraryOf(Part(role, excluded)));

        Assert.Equal(expectWarning, diagnostics.Any(d => d.Code == DesignRuleCheck.CoilWithoutContact));
    }
}
