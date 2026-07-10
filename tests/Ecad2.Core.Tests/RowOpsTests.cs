using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-055増分3: 任意位置への行挿入・削除に伴う要素シフト(RowOps)。
/// 規則の出典: docs/ecad2-t055-increment3-precheck-onmitsu.md §2.2/§2.3。
/// </summary>
public class RowOpsTests
{
    private static Sheet MakeSheet(int rows = 10) => new() { Grid = new GridSpec { Rows = rows, Columns = 20 } };

    private static void PlaceElementAt(Sheet sheet, string elementType, int row)
    {
        switch (elementType)
        {
            case "ElementInstance":
                sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(row, 1) });
                break;
            case "VerticalConnector":
                sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = row, BottomRow = row + 1 });
                break;
            case "WireBreak":
                sheet.WireBreaks.Add(new WireBreak { Boundary = 2.5, Row = row });
                break;
            case "RungComment":
                sheet.RungComments.Add(new RungComment { Row = row, Text = "注記" });
                break;
        }
    }

    private static int GetRow(Sheet sheet, string elementType) => elementType switch
    {
        "ElementInstance" => sheet.Elements[0].Pos.Row,
        "VerticalConnector" => sheet.Connectors[0].TopRow,
        "WireBreak" => sheet.WireBreaks[0].Row,
        "RungComment" => sheet.RungComments[0].Row,
        _ => throw new ArgumentOutOfRangeException(nameof(elementType)),
    };

    // ---- InsertRow: 4種共通(挿入点以降は+1、挿入点より前は不変) ----

    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("RungComment")]
    public void InsertRow_ShiftsElementAtTargetRow(string elementType)
    {
        var sheet = MakeSheet();
        PlaceElementAt(sheet, elementType, row: 3);

        RowOps.InsertRow(sheet, targetRow: 3);

        Assert.Equal(4, GetRow(sheet, elementType));
    }

    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("RungComment")]
    public void InsertRow_ShiftsElementAfterTargetRow(string elementType)
    {
        var sheet = MakeSheet();
        PlaceElementAt(sheet, elementType, row: 5);

        RowOps.InsertRow(sheet, targetRow: 3);

        Assert.Equal(6, GetRow(sheet, elementType));
    }

    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("RungComment")]
    public void InsertRow_DoesNotShiftElementBeforeTargetRow(string elementType)
    {
        var sheet = MakeSheet();
        PlaceElementAt(sheet, elementType, row: 2);

        RowOps.InsertRow(sheet, targetRow: 3);

        Assert.Equal(2, GetRow(sheet, elementType));
    }

    [Fact]
    public void InsertRow_AtRowZero_ShiftsAllElements()
    {
        var sheet = MakeSheet();
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(0, 1) });

        RowOps.InsertRow(sheet, targetRow: 0);

        Assert.Equal(1, sheet.Elements[0].Pos.Row);
    }

    [Fact]
    public void InsertRow_AtLastRow_DoesNotAffectElementsStrictlyBefore()
    {
        var sheet = MakeSheet();
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(8, 1) });

        RowOps.InsertRow(sheet, targetRow: 9);

        Assert.Equal(8, sheet.Elements[0].Pos.Row);
    }

    [Fact]
    public void InsertRow_MultipleElementsSameRow_AllShift()
    {
        var sheet = MakeSheet();
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(5, 1) });
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(5, 3) });
        sheet.WireBreaks.Add(new WireBreak { Boundary = 2.5, Row = 5 });

        RowOps.InsertRow(sheet, targetRow: 5);

        Assert.All(sheet.Elements, e => Assert.Equal(6, e.Pos.Row));
        Assert.Equal(6, sheet.WireBreaks[0].Row);
    }

    [Fact]
    public void InsertRow_VerticalConnector_ShiftsTopAndBottomIndependently()
    {
        var sheet = MakeSheet();
        // 挿入点(3)が接続の範囲(1-4)にまたがる場合、境界(>=3)の側だけ動く。
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 1, BottomRow = 4 });

        RowOps.InsertRow(sheet, targetRow: 3);

        Assert.Equal(1, sheet.Connectors[0].TopRow);
        Assert.Equal(5, sheet.Connectors[0].BottomRow);
    }

    // ---- InsertRow: GroupFrame個別分岐 ----

    [Fact]
    public void InsertRow_GroupFrame_StartRowAtOrAfterTarget_ShiftsPositionOnly()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(5, 1), Width = 3, Height = 2 });

        RowOps.InsertRow(sheet, targetRow: 5);

        Assert.Equal(6, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(2, sheet.Frames[0].Height);
    }

    [Fact]
    public void InsertRow_GroupFrame_TargetInsideFrame_IncreasesHeightOnly()
    {
        var sheet = MakeSheet();
        // 枠は行3〜5(Height=3)。挿入点4は枠開始行より後ろ、終端行(5)より前=内部挿入。
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(3, 1), Width = 3, Height = 3 });

        RowOps.InsertRow(sheet, targetRow: 4);

        Assert.Equal(3, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(4, sheet.Frames[0].Height);
    }

    [Fact]
    public void InsertRow_GroupFrame_TargetAfterFrame_NoChange()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(1, 1), Width = 3, Height = 2 });

        RowOps.InsertRow(sheet, targetRow: 5);

        Assert.Equal(1, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(2, sheet.Frames[0].Height);
    }

    // ---- DeleteRow: 4種共通(削除点より後ろは-1、削除点以下は不変) ----

    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("RungComment")]
    public void DeleteRow_ShiftsElementAfterTargetRow(string elementType)
    {
        var sheet = MakeSheet();
        PlaceElementAt(sheet, elementType, row: 5);

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Equal(4, GetRow(sheet, elementType));
    }

    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("RungComment")]
    public void DeleteRow_DoesNotShiftElementAtOrBeforeTargetRow(string elementType)
    {
        var sheet = MakeSheet();
        PlaceElementAt(sheet, elementType, row: 2);

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Equal(2, GetRow(sheet, elementType));
    }

    [Fact]
    public void DeleteRow_MultipleElementsSameRow_AllShift()
    {
        var sheet = MakeSheet();
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(5, 1) });
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(5, 3) });
        sheet.RungComments.Add(new RungComment { Row = 5, Text = "注記" });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.All(sheet.Elements, e => Assert.Equal(4, e.Pos.Row));
        Assert.Equal(4, sheet.RungComments[0].Row);
    }

    [Fact]
    public void DeleteRow_VerticalConnector_ShiftsTopAndBottomIndependently()
    {
        var sheet = MakeSheet();
        // 削除点(3)より後ろの境界だけ動く(TopRow=1は不変、BottomRow=4は-1)。
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 1, BottomRow = 4 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Equal(1, sheet.Connectors[0].TopRow);
        Assert.Equal(3, sheet.Connectors[0].BottomRow);
    }

    // ---- DeleteRow: GroupFrame(契約=targetRow行に要素なし。開始行より後ろのみ実装対象) ----

    [Fact]
    public void DeleteRow_GroupFrame_StartRowAfterTarget_ShiftsPositionOnly()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(5, 1), Width = 3, Height = 2 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Equal(4, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(2, sheet.Frames[0].Height);
    }

    /// <summary>
    /// 隠密レビュー指摘b(往復1周目、CONFIRMED)対応: 枠開始行そのものが削除対象(TopLeft.Row==targetRow)の
    /// ケース。GuiEcadでは「枠ごと削除」だが、これは「削除対象行に要素がある場合」論点(殿確認待ち、
    /// 2026-07-10)に該当し、RowOps.DeleteRowの対象外(契約=targetRow行に要素なし)。本テストは現状の
    /// 暫定挙動(無変化)を記録するのみで、確定仕様ではない。将来「枠ごと削除」を実装する際は本テストを
    /// 更新すること(このAssertを検証済み仕様と誤読しないこと)。
    /// </summary>
    [Fact]
    public void DeleteRow_GroupFrame_TargetEqualsFrameStartRow_CurrentlyNoChange_PendingDecision()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(3, 1), Width = 3, Height = 2 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Equal(3, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(2, sheet.Frames[0].Height);
    }
}
