using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// 範囲選択のコピー・貼り付け（殿ご下命2026-10-09）の Core 側のテスト。
/// 何を写すか（<see cref="RangeClipboard.Copy"/>）、どこへ貼れるか（<see cref="RangePaste.Plan"/>）、
/// 貼った結果（<see cref="RangePaste.Apply"/>）を測る。
/// </summary>
public class RangeClipboardTests
{
    private static Sheet NewSheet(int rows = 10, int columns = 20, bool mainCircuit = false) =>
        new() { Grid = new GridSpec { Rows = rows, Columns = columns }, MainCircuit = mainCircuit };

    private static ElementInstance Element(int row, int column, string? name = null,
                                           ElementKind kind = ElementKind.ContactNO, int width = 1, int height = 1) =>
        new() { Pos = new GridPos(row, column), Kind = kind, DeviceName = name, CellWidth = width, CellHeight = height };

    // ---- 写し取り ----

    [Fact]
    public void Copy_ElementsInsideRange_AreCopiedWithoutDeviceNames()
    {
        var sheet = NewSheet();
        sheet.Elements.Add(Element(1, 2, "CR1"));
        sheet.Elements.Add(Element(2, 3, "CR2", ElementKind.Coil));
        sheet.Elements.Add(Element(5, 5, "範囲外"));

        var clip = RangeClipboard.Copy(sheet, new CellRange(1, 2, 2, 3));

        Assert.Equal(2, clip.Elements.Count);
        Assert.All(clip.Elements, e => Assert.Null(e.DeviceName));
        Assert.Equal(new[] { ElementKind.ContactNO, ElementKind.Coil }, clip.Elements.Select(e => e.Kind));
        Assert.Equal(new GridPos(1, 2), clip.Origin);
        Assert.Equal((2, 2), (clip.Rows, clip.Columns));
    }

    /// <summary>写したものは元の実体と切り離されており、元の図面には触れぬ。</summary>
    [Fact]
    public void Copy_ReturnsDetachedClones()
    {
        var sheet = NewSheet();
        var original = Element(1, 2, "CR1");
        original.Params[ParamKeys.Orient] = "V";
        sheet.Elements.Add(original);

        var clip = RangeClipboard.Copy(sheet, new CellRange(1, 2, 1, 2));

        var copied = Assert.Single(clip.Elements);
        Assert.NotSame(original, copied);
        Assert.NotEqual(original.Id, copied.Id);
        Assert.Equal("V", copied.Params[ParamKeys.Orient]);
        Assert.Equal("CR1", original.DeviceName);
    }

    /// <summary>占有セルの一部が範囲の外に出る要素は写さぬ（幅3の右端・高さ2の上下行）。</summary>
    [Theory]
    [InlineData(1, 2, 1, 3, false)]   // 幅3（列2〜4）に対し範囲は列2〜3
    [InlineData(1, 2, 1, 4, true)]
    public void Copy_WideElement_RequiresWholeWidthInside(int top, int left, int bottom, int right, bool expected)
    {
        var sheet = NewSheet();
        sheet.Elements.Add(Element(1, 2, width: 3));

        var clip = RangeClipboard.Copy(sheet, new CellRange(top, left, bottom, right));

        Assert.Equal(expected, clip.Elements.Count == 1);
    }

    [Theory]
    [InlineData(3, 3, false)]   // 高さ2＝行2〜4を占める。中心の行だけでは足りぬ
    [InlineData(2, 4, true)]
    public void Copy_TallElement_RequiresAllOccupiedRowsInside(int top, int bottom, bool expected)
    {
        var sheet = NewSheet();
        sheet.Elements.Add(Element(3, 2, height: 2));

        var clip = RangeClipboard.Copy(sheet, new CellRange(top, 2, bottom, 2));

        Assert.Equal(expected, clip.Elements.Count == 1);
    }

    /// <summary>縦コネクタは両端の行が範囲内で、列境界が範囲の左端〜右端の境界（Right+1 まで）に載るもの。</summary>
    [Fact]
    public void Copy_Connectors_OnlyThoseFullyInside()
    {
        var sheet = NewSheet();
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 1, BottomRow = 2 });     // 左端の境界
        sheet.Connectors.Add(new VerticalConnector { Column = 4, TopRow = 1, BottomRow = 2 });     // 右端の境界(Right+1)
        sheet.Connectors.Add(new VerticalConnector { Column = 2.5, TopRow = 1, BottomRow = 2 });   // セル中央
        sheet.Connectors.Add(new VerticalConnector { Column = 3, TopRow = 1, BottomRow = 5 });     // 下へ跨ぐ
        sheet.Connectors.Add(new VerticalConnector { Column = 5, TopRow = 1, BottomRow = 2 });     // 範囲の右外

        var clip = RangeClipboard.Copy(sheet, new CellRange(1, 2, 2, 3));

        Assert.Equal(new[] { 2.0, 4.0, 2.5 }, clip.Connectors.Select(c => c.Column));
    }

    [Fact]
    public void Copy_WireBreaksAndFrames_OnlyThoseFullyInside()
    {
        var sheet = NewSheet();
        sheet.WireBreaks.Add(new WireBreak { Row = 1, Boundary = 2.5 });
        sheet.WireBreaks.Add(new WireBreak { Row = 4, Boundary = 2.5 });
        sheet.Frames.Add(new GroupFrame { Label = "内", TopLeft = new GridPos(1, 2), Width = 2, Height = 2 });
        sheet.Frames.Add(new GroupFrame { Label = "はみ出し", TopLeft = new GridPos(1, 2), Width = 3, Height = 2 });

        var clip = RangeClipboard.Copy(sheet, new CellRange(1, 2, 2, 3));

        Assert.Single(clip.WireBreaks);
        Assert.Equal("内", Assert.Single(clip.Frames).Label);
    }

    /// <summary>行コメントは写さぬ（殿ご裁可）。行コメントしか無い範囲は空である。</summary>
    [Fact]
    public void Copy_RungCommentsAreNotCopied()
    {
        var sheet = NewSheet();
        sheet.RungComments.Add(new RungComment { Row = 1, Text = "コメント" });

        var clip = RangeClipboard.Copy(sheet, new CellRange(0, 0, 9, 19));

        Assert.True(clip.IsEmpty);
    }

    // ---- 段取り ----

    private static RangeClipboard TwoRowClip()
    {
        var source = NewSheet();
        source.Elements.Add(Element(0, 0));
        source.Elements.Add(Element(1, 2, kind: ElementKind.Coil));
        source.Connectors.Add(new VerticalConnector { Column = 1, TopRow = 0, BottomRow = 1 });
        return RangeClipboard.Copy(source, new CellRange(0, 0, 1, 2));
    }

    [Fact]
    public void Plan_EmptyDestination_PastesWithoutAddingRows()
    {
        var plan = RangePaste.Plan(NewSheet(), TwoRowClip(), new GridPos(3, 4), lib: null);

        Assert.Equal(new RangePastePlan(RangePasteStatus.Ok), plan);
    }

    /// <summary>貼り付け先が塞がっておれば、コピーした行数ぶんを挿入する（殿ご裁可）。</summary>
    [Fact]
    public void Plan_DestinationOccupiedByElement_InsertsCopiedRowCount()
    {
        var dest = NewSheet();
        dest.Elements.Add(Element(4, 5));   // 貼り付け先(行3〜4・列4〜6)の内

        var plan = RangePaste.Plan(dest, TwoRowClip(), new GridPos(3, 4), lib: null);

        Assert.Equal(new RangePastePlan(RangePasteStatus.Ok, RowsToInsert: 2), plan);
    }

    /// <summary>貼り付け先の矩形を通る縦コネクタ・配線分断も塞がりと見る。枠は見ぬ。</summary>
    [Fact]
    public void Plan_DestinationCrossedByConnectorOrBreak_Inserts_ButFrameDoesNot()
    {
        var withConnector = NewSheet();
        withConnector.Connectors.Add(new VerticalConnector { Column = 5, TopRow = 0, BottomRow = 8 });
        var withBreak = NewSheet();
        withBreak.WireBreaks.Add(new WireBreak { Row = 3, Boundary = 4.5 });
        var withFrame = NewSheet();
        withFrame.Frames.Add(new GroupFrame { TopLeft = new GridPos(2, 3), Width = 6, Height = 6 });

        Assert.Equal(2, RangePaste.Plan(withConnector, TwoRowClip(), new GridPos(3, 4), null).RowsToInsert);
        Assert.Equal(2, RangePaste.Plan(withBreak, TwoRowClip(), new GridPos(3, 4), null).RowsToInsert);
        Assert.Equal(0, RangePaste.Plan(withFrame, TwoRowClip(), new GridPos(3, 4), null).RowsToInsert);
    }

    /// <summary>同じ行でも、列が貼り付け先の外なら塞がりではない。</summary>
    [Fact]
    public void Plan_ElementOnSameRowsButOutsideColumns_DoesNotInsert()
    {
        var dest = NewSheet();
        dest.Elements.Add(Element(3, 0));
        dest.Elements.Add(Element(4, 10));

        var plan = RangePaste.Plan(dest, TwoRowClip(), new GridPos(3, 4), lib: null);

        Assert.Equal(0, plan.RowsToInsert);
    }

    /// <summary>空いておるが下へはみ出す場合は、はみ出すぶんだけ末尾へ足す。</summary>
    [Fact]
    public void Plan_ExtendsBelowGrid_AppendsMissingRows()
    {
        var plan = RangePaste.Plan(NewSheet(rows: 10), TwoRowClip(), new GridPos(9, 0), lib: null);

        Assert.Equal(new RangePastePlan(RangePasteStatus.Ok, RowsToAppend: 1), plan);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(10, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 20)]
    public void Plan_AnchorOutsideGrid_Rejects(int row, int column)
    {
        var plan = RangePaste.Plan(NewSheet(), TwoRowClip(), new GridPos(row, column), lib: null);

        Assert.Equal(RangePasteStatus.AnchorOutOfGrid, plan.Status);
        Assert.False(plan.CanPaste);
    }

    /// <summary>右へはみ出す場合は貼れぬ（列は増やせぬ）。境界値＝ちょうど収まる列は貼れる。</summary>
    [Theory]
    [InlineData(17, RangePasteStatus.Ok)]
    [InlineData(18, RangePasteStatus.ColumnsOverflow)]
    public void Plan_ColumnsOverflow_Rejects(int anchorColumn, RangePasteStatus expected)
    {
        var plan = RangePaste.Plan(NewSheet(columns: 20), TwoRowClip(), new GridPos(0, anchorColumn), lib: null);

        Assert.Equal(expected, plan.Status);
    }

    [Fact]
    public void Plan_RowLimitExceeded_Rejects()
    {
        var full = NewSheet(rows: GridSpec.MaxRows);
        full.Elements.Add(Element(3, 4));
        var nearlyFull = NewSheet(rows: GridSpec.MaxRows - 2);
        nearlyFull.Elements.Add(Element(3, 4));

        Assert.Equal(RangePasteStatus.RowLimitExceeded, RangePaste.Plan(full, TwoRowClip(), new GridPos(3, 4), null).Status);
        // ちょうど上限に届く挿入は通る。
        Assert.Equal(RangePasteStatus.Ok, RangePaste.Plan(nearlyFull, TwoRowClip(), new GridPos(3, 4), null).Status);
    }

    /// <summary>基準行が貼り付け行より上に在って下へ張り出す部品は、行を挿入しても動かぬ。そこへは割り込めぬ。</summary>
    [Fact]
    public void Plan_TallElementStraddlingAnchorRow_Rejects()
    {
        var dest = NewSheet();
        dest.Elements.Add(Element(2, 4, height: 2));   // 行1〜3を占め、基準行2 < 貼り付け行3

        var plan = RangePaste.Plan(dest, TwoRowClip(), new GridPos(3, 4), lib: null);

        Assert.Equal(RangePasteStatus.BlockedByTallElement, plan.Status);
    }

    /// <summary>縦コネクタ・配線分断は主回路シートへ貼れぬ。主回路専用の記号は制御回路シートへ貼れぬ。</summary>
    [Fact]
    public void Plan_SheetKindMismatch_Rejects()
    {
        var mainSource = NewSheet(mainCircuit: true);
        mainSource.Elements.Add(Element(1, 0, kind: ElementKind.Breaker3P, width: 2, height: 2));
        var mainOnlyClip = RangeClipboard.Copy(mainSource, new CellRange(0, 0, 2, 1));
        Assert.Single(mainOnlyClip.Elements);

        Assert.Equal(RangePasteStatus.NotAllowedOnSheet,
            RangePaste.Plan(NewSheet(mainCircuit: true), TwoRowClip(), new GridPos(0, 0), null).Status);
        Assert.Equal(RangePasteStatus.NotAllowedOnSheet,
            RangePaste.Plan(NewSheet(mainCircuit: false), mainOnlyClip, new GridPos(0, 0), null).Status);
        Assert.Equal(RangePasteStatus.Ok,
            RangePaste.Plan(NewSheet(mainCircuit: true), mainOnlyClip, new GridPos(0, 0), null).Status);
    }

    // ---- 貼り付け ----

    [Fact]
    public void Apply_EmptyDestination_AddsShiftedClones()
    {
        var dest = NewSheet();
        var clip = TwoRowClip();
        var anchor = new GridPos(3, 4);

        RangePaste.Apply(dest, clip, anchor, RangePaste.Plan(dest, clip, anchor, null));

        Assert.Equal(new[] { new GridPos(3, 4), new GridPos(4, 6) }, dest.Elements.Select(e => e.Pos));
        var connector = Assert.Single(dest.Connectors);
        Assert.Equal((5.0, 3, 4), (connector.Column, connector.TopRow, connector.BottomRow));
        Assert.Equal(10, dest.Grid.Rows);
    }

    /// <summary>二度貼っても同じ実体・同じ Id を使い回さぬ。</summary>
    [Fact]
    public void Apply_Twice_CreatesIndependentInstances()
    {
        var dest = NewSheet();
        var clip = TwoRowClip();

        RangePaste.Apply(dest, clip, new GridPos(0, 0), RangePaste.Plan(dest, clip, new GridPos(0, 0), null));
        RangePaste.Apply(dest, clip, new GridPos(5, 0), RangePaste.Plan(dest, clip, new GridPos(5, 0), null));

        Assert.Equal(4, dest.Elements.Count);
        Assert.Equal(4, dest.Elements.Select(e => e.Id).Distinct().Count());
        Assert.Equal(2, dest.Connectors.Distinct().Count());
    }

    /// <summary>塞がっておる所へ貼ると、既存のものは行数ぶん下へ送られ、空いた所へ入る。</summary>
    [Fact]
    public void Apply_OccupiedDestination_InsertsRowsAndShiftsExisting()
    {
        var dest = NewSheet(rows: 10);
        var above = Element(2, 4, "上");
        var atAnchor = Element(3, 4, "貼り付け行");
        var below = Element(4, 5, "下");
        dest.Elements.AddRange(new[] { above, atAnchor, below });
        dest.RungComments.Add(new RungComment { Row = 3, Text = "コメント" });
        var clip = TwoRowClip();
        var anchor = new GridPos(3, 4);

        RangePaste.Apply(dest, clip, anchor, RangePaste.Plan(dest, clip, anchor, null));

        Assert.Equal(12, dest.Grid.Rows);
        Assert.Equal(new GridPos(2, 4), above.Pos);
        Assert.Equal(new GridPos(5, 4), atAnchor.Pos);
        Assert.Equal(new GridPos(6, 5), below.Pos);
        Assert.Equal(5, dest.RungComments[0].Row);
        // 貼った2つは挿入した行3〜4に居り、既存のどれとも重ならぬ。
        var pasted = dest.Elements.Where(e => e.DeviceName is null).Select(e => e.Pos).ToArray();
        Assert.Equal(new[] { new GridPos(3, 4), new GridPos(4, 6) }, pasted);
    }

    [Fact]
    public void Apply_ExtendsBelowGrid_GrowsRowCount()
    {
        var dest = NewSheet(rows: 10);
        var clip = TwoRowClip();
        var anchor = new GridPos(9, 0);

        RangePaste.Apply(dest, clip, anchor, RangePaste.Plan(dest, clip, anchor, null));

        Assert.Equal(11, dest.Grid.Rows);
        Assert.Equal(new[] { new GridPos(9, 0), new GridPos(10, 2) }, dest.Elements.Select(e => e.Pos));
    }

    [Fact]
    public void Apply_RejectedPlan_DoesNothing()
    {
        var dest = NewSheet();
        var clip = TwoRowClip();

        var result = RangePaste.Apply(dest, clip, new GridPos(0, 19), RangePaste.Plan(dest, clip, new GridPos(0, 19), null));

        Assert.Null(result);
        Assert.Empty(dest.Elements);
        Assert.Equal(10, dest.Grid.Rows);
    }

    /// <summary>自由式の枠（mm 座標つき）は、グリッドの位置と同じ差分だけ mm 座標も動く。大きさは変わらぬ。</summary>
    [Fact]
    public void Apply_FreeFormFrame_ShiftsMmPositionByCellDelta()
    {
        var source = NewSheet();
        source.Frames.Add(new GroupFrame
        {
            Label = "盤", TopLeft = new GridPos(1, 1), Width = 2, Height = 2, BorderStyle = LineStyle.Solid,
            VisualXMm = 30, VisualYMm = 40, VisualWidthMm = 17, VisualHeightMm = 19,
        });
        source.Frames.Add(new GroupFrame { Label = "グリッド式", TopLeft = new GridPos(1, 1), Width = 1, Height = 1 });
        var clip = RangeClipboard.Copy(source, new CellRange(1, 1, 2, 2));
        var dest = NewSheet();
        var anchor = new GridPos(4, 3);   // 行+3・列+2
        double cell = Ecad2.Rendering.GridGeometry.DefaultCellMm;

        RangePaste.Apply(dest, clip, anchor, RangePaste.Plan(dest, clip, anchor, null));

        var free = dest.Frames.Single(f => f.Label == "盤");
        Assert.Equal(new GridPos(4, 3), free.TopLeft);
        Assert.Equal((30 + 2 * cell, 40 + 3 * cell), (free.VisualXMm, free.VisualYMm));
        Assert.Equal((17.0, 19.0), (free.VisualWidthMm, free.VisualHeightMm));
        Assert.Equal(LineStyle.Solid, free.BorderStyle);
        var gridBased = dest.Frames.Single(f => f.Label == "グリッド式");
        Assert.Null(gridBased.VisualXMm);
        Assert.Null(gridBased.VisualYMm);
    }

    /// <summary>ゴースト用の中身は呼ぶたびに新しい実体で、コピー元を書き換えぬ。</summary>
    [Fact]
    public void Materialize_ReturnsFreshClonesEachCall()
    {
        var clip = TwoRowClip();

        var first = RangePaste.Materialize(clip, new GridPos(3, 4));
        var second = RangePaste.Materialize(clip, new GridPos(3, 4));

        Assert.NotSame(first.Elements[0], second.Elements[0]);
        Assert.Equal(new GridPos(0, 0), clip.Elements[0].Pos);
        Assert.Equal(new GridPos(3, 4), first.Elements[0].Pos);
    }
}
