using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-052: DRC-PART-001（未解決PartIdのContactNOフォールバック警告）の回帰テスト。
/// 案の出典: docs/ecad2-uiux-proposals-p017-p020-p023-onmitsu.md P-017節（案A採用、殿裁定2026-07-10）。
/// </summary>
public class DesignRuleCheckPartIdTests
{
    private static LadderDocument MakeDoc(ElementInstance elem)
    {
        var sheet = new Sheet { PageNumber = 1, Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(elem);
        return new LadderDocument { Sheets = { sheet } };
    }

    [Fact]
    public void CheckUnresolvedPartId_PartIdSetButNotInLibrary_EmitsWarning()
    {
        var elem = new ElementInstance { PartId = "missing-id", DeviceName = "CR1", Pos = new GridPos(2, 1) };
        var doc = MakeDoc(elem);
        var lib = new PartLibrary();

        var diagnostics = DesignRuleCheck.CheckUnresolvedPartId(doc, lib);

        var d = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        Assert.Equal(DesignRuleCheck.UnresolvedPartId, d.Code);
        Assert.Equal("CR1", d.DeviceName);
        Assert.Contains("部品参照が見つからず", d.Message);
        var loc = Assert.Single(d.Locations);
        Assert.Equal(1, loc.PageNumber);
        Assert.Equal(3, loc.CircuitNumber); // Pos.Row(2) + 1
    }

    [Fact]
    public void CheckUnresolvedPartId_PartIdResolvesInLibrary_NoWarning()
    {
        var elem = new ElementInstance { PartId = "known-id", DeviceName = "CR1", Pos = new GridPos(0, 0) };
        var doc = MakeDoc(elem);
        var lib = new PartLibrary();
        lib.ById["known-id"] = new PartDefinition { Id = "known-id", Name = "自作部品", Role = PartRole.ContactNO };

        var diagnostics = DesignRuleCheck.CheckUnresolvedPartId(doc, lib);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void CheckUnresolvedPartId_BuiltInKindWithoutPartId_NoWarning()
    {
        var elem = new ElementInstance { Kind = ElementKind.ContactNO, PartId = null, DeviceName = "CR1", Pos = new GridPos(0, 0) };
        var doc = MakeDoc(elem);

        var diagnostics = DesignRuleCheck.CheckUnresolvedPartId(doc, lib: null);

        Assert.Empty(diagnostics);
    }
}
