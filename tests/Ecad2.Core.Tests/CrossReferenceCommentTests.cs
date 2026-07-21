using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-107増分2(殿裁定=デバイス単位で共有、GX3準拠)の回帰テスト。CrossReferenceBuilderの
/// コメント集約ロジックは、要素側(Element.Comment、廃止済み)の集約・重複除去から
/// Device.Commentの単純参照へ単純化された。DoD(4): 機器表(クロスリファレンス表)の
/// コメント列表示への反映を検証する。
/// </summary>
public class CrossReferenceCommentTests
{
    private static LadderDocument MakeDocument(params ElementInstance[] elements)
    {
        var doc = new LadderDocument();
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        foreach (var e in elements) sheet.Elements.Add(e);
        doc.Sheets.Add(sheet);
        return doc;
    }

    [Fact]
    public void Build_DeviceにCommentがあればCrossRefEntry_Commentへ反映される()
    {
        var doc = MakeDocument(new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(0, 0), DeviceName = "Y001" });
        doc.Devices.ByName["Y001"] = new Device { Name = "Y001", Comment = "出力リレー" };

        var xref = CrossReferenceBuilder.Build(doc);

        Assert.True(xref.TryGet("Y001", out var entry));
        Assert.Equal("出力リレー", entry.Comment);
    }

    [Fact]
    public void Build_Device未登録またはComment未設定ならCrossRefEntry_Commentはnull()
    {
        var doc = MakeDocument(new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(0, 0), DeviceName = "Y001" });
        // Device自体を登録しないケース(機器表エントリなし)。

        var xref = CrossReferenceBuilder.Build(doc);

        Assert.True(xref.TryGet("Y001", out var entry));
        Assert.Null(entry.Comment);
    }

    /// <summary>T-107増分2の主目的: 同一デバイス名の複数要素(コイル+接点)があっても、
    /// CrossRefEntry.Commentは1つに定まる(Device単位で共有されるため)。</summary>
    [Fact]
    public void Build_同一デバイス名の複数要素でもCommentは1つに定まる()
    {
        var doc = MakeDocument(
            new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(0, 0), DeviceName = "M1" },
            new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 0), DeviceName = "M1" });
        doc.Devices.ByName["M1"] = new Device { Name = "M1", Comment = "共有コメント" };

        var xref = CrossReferenceBuilder.Build(doc);

        Assert.True(xref.TryGet("M1", out var entry));
        Assert.Equal("共有コメント", entry.Comment);
        Assert.Single(entry.Coils);
        Assert.Single(entry.Contacts);
    }
}
