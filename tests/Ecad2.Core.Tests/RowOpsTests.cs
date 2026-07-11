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

    // T-055増分3差し込み(殿裁定2026-07-11)により本Theoryから"VerticalConnector"を除外した。
    // 共有PlaceElementAtヘルパーはVerticalConnectorを(row, row+1)で配置するため、row=2/targetRow=3
    // だとBottomRow(3)==targetRowとなり新設の端点削除ロジック(B5/B6参照)が誤って発火してしまう
    // (要素ごと削除が入る前は単純シフト判定のみだったため無害だった)。VerticalConnectorの
    // 「両端点とも対象行より前」ケースは専用Factで検証する(下記
    // DeleteRow_VerticalConnector_BothEndpointsBeforeTarget_DoesNotShift)。
    [Theory]
    [InlineData("ElementInstance")]
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
    public void DeleteRow_VerticalConnector_BothEndpointsBeforeTarget_DoesNotShift()
    {
        var sheet = MakeSheet();
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 0, BottomRow = 1 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Equal(0, sheet.Connectors[0].TopRow);
        Assert.Equal(1, sheet.Connectors[0].BottomRow);
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

    // ---- DeleteRow: GroupFrame(開始行より後ろは位置シフトのみ) ----

    [Fact]
    public void DeleteRow_GroupFrame_StartRowAfterTarget_ShiftsPositionOnly()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(5, 1), Width = 3, Height = 2 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Equal(4, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(2, sheet.Frames[0].Height);
    }

    // ---- DeleteRow: 要素ごと削除(T-055増分3差し込み、殿裁定2026-07-11) ----
    // 対象行の要素(広義5種)は拒否せず削除する(GuiEcad同型)。設計・境界値の出典:
    // docs/ecad2-t055-increment3-delete-occupied-design-onmitsu.md §1〜3。

    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("WireBreak")]
    [InlineData("RungComment")]
    public void DeleteRow_RemovesElementAtTargetRow(string elementType)
    {
        var sheet = MakeSheet();
        PlaceElementAt(sheet, elementType, row: 3);

        RowOps.DeleteRow(sheet, targetRow: 3);

        int count = elementType switch
        {
            "ElementInstance" => sheet.Elements.Count,
            "WireBreak" => sheet.WireBreaks.Count,
            "RungComment" => sheet.RungComments.Count,
            _ => throw new ArgumentOutOfRangeException(nameof(elementType)),
        };
        Assert.Equal(0, count);
    }

    [Fact]
    public void DeleteRow_ReturnsRemovedElementInstances()
    {
        var sheet = MakeSheet();
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "CR1", Pos = new GridPos(3, 1) });

        var removed = RowOps.DeleteRow(sheet, targetRow: 3);

        var removedEl = Assert.Single(removed);
        Assert.Equal("CR1", removedEl.DeviceName);
    }

    /// <summary>B5: TopRow==targetRow(上端一致)→削除される。</summary>
    [Fact]
    public void DeleteRow_VerticalConnector_TopRowEqualsTarget_RemovesConnector()
    {
        var sheet = MakeSheet();
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 3, BottomRow = 6 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Empty(sheet.Connectors);
    }

    /// <summary>B6: BottomRow==targetRow(下端一致)→削除される。</summary>
    [Fact]
    public void DeleteRow_VerticalConnector_BottomRowEqualsTarget_RemovesConnector()
    {
        var sheet = MakeSheet();
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 1, BottomRow = 3 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Empty(sheet.Connectors);
    }

    // B7(範囲を跨ぐが端点でない→削除されず端点のみシフト)は既存の
    // DeleteRow_VerticalConnector_ShiftsTopAndBottomIndependently で検証済み。

    /// <summary>B1: Height=1(最小)の枠が削除対象そのもの→枠ごと削除(Height--で0にする経路ではない)。</summary>
    [Fact]
    public void DeleteRow_GroupFrame_HeightOne_TargetEqualsStartRow_RemovesFrame()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(3, 1), Width = 3, Height = 1 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Empty(sheet.Frames);
    }

    /// <summary>
    /// 開始行==対象行(Height>1)→枠ごと削除。旧テスト(暫定「無変化」挙動)の更新版
    /// (2026-07-11殿裁定「要素ごと削除」採用によりGroupFrameも枠ごと削除が確定仕様となった)。
    /// </summary>
    [Fact]
    public void DeleteRow_GroupFrame_TargetEqualsFrameStartRow_RemovesFrame()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(3, 1), Width = 3, Height = 2 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Empty(sheet.Frames);
    }

    /// <summary>B2: 対象行が終端行ちょうど→Height--(内部詰め、開始行不変)。</summary>
    [Fact]
    public void DeleteRow_GroupFrame_TargetAtEndRow_DecreasesHeight()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(3, 1), Width = 3, Height = 3 }); // 範囲3-5

        RowOps.DeleteRow(sheet, targetRow: 5);

        Assert.Equal(3, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(2, sheet.Frames[0].Height);
    }

    /// <summary>B3: 対象行が開始行の直後(内部の最初の行)→Height--(内部詰め)。</summary>
    [Fact]
    public void DeleteRow_GroupFrame_TargetJustAfterStartRow_DecreasesHeight()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(3, 1), Width = 3, Height = 3 }); // 範囲3-5

        RowOps.DeleteRow(sheet, targetRow: 4);

        Assert.Equal(3, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(2, sheet.Frames[0].Height);
    }

    /// <summary>B4: 対象行が開始行の直前→位置のみ-1(StartRowAfterTargetの境界隣接版)。</summary>
    [Fact]
    public void DeleteRow_GroupFrame_TargetJustBeforeStartRow_ShiftsPositionOnly()
    {
        var sheet = MakeSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(3, 1), Width = 3, Height = 3 });

        RowOps.DeleteRow(sheet, targetRow: 2);

        Assert.Equal(2, sheet.Frames[0].TopLeft.Row);
        Assert.Equal(3, sheet.Frames[0].Height);
    }

    /// <summary>B9: 同一行に5種すべてが同時存在→全種が削除される(複合ケース、対称性点検)。</summary>
    [Fact]
    public void DeleteRow_AllFiveTypesAtTargetRow_AllRemoved()
    {
        var sheet = MakeSheet();
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(3, 1) });
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 3, BottomRow = 4 });
        sheet.WireBreaks.Add(new WireBreak { Boundary = 2.5, Row = 3 });
        sheet.RungComments.Add(new RungComment { Row = 3, Text = "注記" });
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(3, 1), Width = 3, Height = 1 });

        RowOps.DeleteRow(sheet, targetRow: 3);

        Assert.Empty(sheet.Elements);
        Assert.Empty(sheet.Connectors);
        Assert.Empty(sheet.WireBreaks);
        Assert.Empty(sheet.RungComments);
        Assert.Empty(sheet.Frames);
    }
}
