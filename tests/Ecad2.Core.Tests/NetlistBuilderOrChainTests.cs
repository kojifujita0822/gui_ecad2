using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-044隠密レビュー指摘(docs/ecad2-t044-review-onmitsu.md所見1、CONFIRMED・重大)のネットリスト
/// レベル回帰テスト。MainWindowViewModel.PlaceElementAtSelectedCellの修正版(左縦分岐省略ロジックが
/// 既存の縦コネクタも見る)が生成する接続構成を直接NetlistBuilderへ与え、同一列で3階層重ねたOR配置
/// (行0=A0/A2、行1=B、行2=C)で、Cが正しくBと同一ネット(A0/A2経由の分岐)になり、母線ネットへ
/// 誤って直結されないことを確認する。
/// </summary>
public class NetlistBuilderOrChainTests
{
    private static ElementInstance MakeContact(int row, int column, string deviceName)
        => new() { Kind = ElementKind.ContactNO, Pos = new GridPos(row, column), DeviceName = deviceName };

    [Fact]
    public void Build_ThreeTierOrChainAtSameColumn_FinalTierSharesNetWithMiddleTierNotRail()
    {
        var sheet = new Sheet();
        sheet.Elements.Add(MakeContact(0, 0, "A0"));
        sheet.Elements.Add(MakeContact(0, 2, "A2"));
        sheet.Elements.Add(MakeContact(1, 2, "B"));
        sheet.Elements.Add(MakeContact(2, 2, "C"));
        // 修正後のPlaceElementAtSelectedCellが生成する接続(左右とも各段で維持)を再現する。
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 0, BottomRow = 1 });
        sheet.Connectors.Add(new VerticalConnector { Column = 3, TopRow = 0, BottomRow = 1 });
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 1, BottomRow = 2 });
        sheet.Connectors.Add(new VerticalConnector { Column = 3, TopRow = 1, BottomRow = 2 });

        var netlist = NetlistBuilder.Build(sheet);

        var b = netlist.Components.Single(c => c.DeviceName == "B");
        var c = netlist.Components.Single(c => c.DeviceName == "C");

        Assert.Equal(b.NetA, c.NetA);
        Assert.NotEqual(netlist.LeftRailNet, c.NetA);
    }

    [Fact]
    public void Build_ThreeTierOrChainWithMiddleTierLeftConnectorMissing_FinalTierWronglyJoinsRailNet()
    {
        // バグ再現(修正前の挙動を模擬): Cの左縦分岐(列2、行1-2)を省略すると、Cの左ノードは行1(B)の
        // 既存縦コネクタを考慮されず、母線へ直結されてしまう(本来Bと同一ネットであるべき)。
        var sheet = new Sheet();
        sheet.Elements.Add(MakeContact(0, 0, "A0"));
        sheet.Elements.Add(MakeContact(0, 2, "A2"));
        sheet.Elements.Add(MakeContact(1, 2, "B"));
        sheet.Elements.Add(MakeContact(2, 2, "C"));
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 0, BottomRow = 1 });
        sheet.Connectors.Add(new VerticalConnector { Column = 3, TopRow = 0, BottomRow = 1 });
        sheet.Connectors.Add(new VerticalConnector { Column = 3, TopRow = 1, BottomRow = 2 });   // 列2(左)は省略

        var netlist = NetlistBuilder.Build(sheet);

        var c = netlist.Components.Single(comp => comp.DeviceName == "C");

        Assert.Equal(netlist.LeftRailNet, c.NetA);
    }
}
