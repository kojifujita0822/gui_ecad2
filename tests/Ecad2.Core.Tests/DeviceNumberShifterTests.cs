using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// 機器番号の挿入（殿ご下命2026-10-09）の順送り規則のテスト。
/// CR1〜CR7 が在るとき CR4 を指定すれば CR4〜CR7 が CR5〜CR8 になり、CR4 が空く。
/// </summary>
public class DeviceNumberShifterTests
{
    /// <summary>名ごとに要素を1つ持つ1シートの図面を作る。</summary>
    private static LadderDocument DocWithElements(params string[] deviceNames)
    {
        var sheet = new Sheet();
        for (int i = 0; i < deviceNames.Length; i++)
            sheet.Elements.Add(new ElementInstance { Pos = new GridPos(i, 0), DeviceName = deviceNames[i] });
        return new LadderDocument { Sheets = { sheet } };
    }

    private static string?[] NamesOf(LadderDocument doc) =>
        doc.Sheets.SelectMany(s => s.Elements).Select(e => e.DeviceName).ToArray();

    [Theory]
    [InlineData("CR4", "CR", 4)]
    [InlineData("T1", "T", 1)]
    [InlineData("MC12", "MC", 12)]
    [InlineData("CR04", "CR", 4)]
    [InlineData("X1-2", "X1-", 2)]
    public void TryParse_NumberedName_SplitsPrefixAndNumber(string name, string expectedPrefix, int expectedNumber)
    {
        Assert.True(DeviceNumberShifter.TryParse(name, out string prefix, out int number));
        Assert.Equal(expectedPrefix, prefix);
        Assert.Equal(expectedNumber, number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CR")]
    [InlineData("4")]
    [InlineData("CR4A")]
    [InlineData("CR99999999999")]
    public void TryParse_NotNumberedName_ReturnsFalse(string? name)
    {
        Assert.False(DeviceNumberShifter.TryParse(name, out _, out _));
    }

    /// <summary>殿のお示しの例そのもの。</summary>
    [Fact]
    public void InsertAt_MiddleOfSeries_ShiftsThatNumberAndAbove()
    {
        var doc = DocWithElements("CR1", "CR2", "CR3", "CR4", "CR5", "CR6", "CR7");

        var result = DeviceNumberShifter.InsertAt(doc, "CR4");

        Assert.Null(result.Conflict);
        Assert.Equal(new[] { "CR1", "CR2", "CR3", "CR5", "CR6", "CR7", "CR8" }, NamesOf(doc));
        Assert.Equal(4, result.Renames.Count);
    }

    /// <summary>欠番で止めず末尾まで送る（殿ご裁可）。</summary>
    [Fact]
    public void InsertAt_GapInSeries_ShiftsBeyondGap()
    {
        var doc = DocWithElements("CR4", "CR5", "CR10");

        DeviceNumberShifter.InsertAt(doc, "CR4");

        Assert.Equal(new[] { "CR5", "CR6", "CR11" }, NamesOf(doc));
    }

    /// <summary>接頭辞の違う名・番号を持たぬ名は動かさぬ。CR と CRX は別の接頭辞である。</summary>
    [Fact]
    public void InsertAt_OtherPrefixes_StayUntouched()
    {
        var doc = DocWithElements("CR4", "T4", "T5", "CRX4", "PB", "MC10");

        DeviceNumberShifter.InsertAt(doc, "CR4");

        Assert.Equal(new[] { "CR5", "T4", "T5", "CRX4", "PB", "MC10" }, NamesOf(doc));
    }

    /// <summary>同じ機器名を持つコイルと接点は、シートを跨いでも揃って送られる。</summary>
    [Fact]
    public void InsertAt_SameDeviceOnMultipleSheets_AllReferencesFollow()
    {
        var doc = DocWithElements("CR4", "CR5");
        var second = new Sheet();
        second.Elements.Add(new ElementInstance { Pos = new GridPos(0, 0), DeviceName = "CR4" });
        second.Elements.Add(new ElementInstance { Pos = new GridPos(1, 0), DeviceName = "CR5" });
        doc.Sheets.Add(second);

        DeviceNumberShifter.InsertAt(doc, "CR4");

        Assert.Equal(new[] { "CR5", "CR6", "CR5", "CR6" }, NamesOf(doc));
    }

    /// <summary>機器表の型式・コメントは名に付いて移り、隣の機器のものと入れ替わらぬ。</summary>
    [Fact]
    public void InsertAt_DeviceTableEntries_MoveWithTheirNames()
    {
        var doc = DocWithElements("CR4", "CR5");
        doc.Devices.ByName["CR4"] = new Device { Name = "CR4", Comment = "四番" };
        doc.Devices.ByName["CR5"] = new Device { Name = "CR5", Comment = "五番" };

        DeviceNumberShifter.InsertAt(doc, "CR4");

        Assert.False(doc.Devices.ByName.ContainsKey("CR4"));
        Assert.Equal("四番", doc.Devices.ByName["CR5"].Comment);
        Assert.Equal("CR5", doc.Devices.ByName["CR5"].Name);
        Assert.Equal("五番", doc.Devices.ByName["CR6"].Comment);
    }

    /// <summary>機器表にのみ在る名も送る。見落とせば、CR4 を CR5 へ送った折に CR4 の情報が捨てられる。</summary>
    [Fact]
    public void InsertAt_NameOnlyInDeviceTable_IsShiftedToo()
    {
        var doc = DocWithElements("CR4");
        doc.Devices.ByName["CR4"] = new Device { Name = "CR4", Comment = "四番" };
        doc.Devices.ByName["CR5"] = new Device { Name = "CR5", Comment = "表のみ" };

        DeviceNumberShifter.InsertAt(doc, "CR4");

        Assert.Equal("四番", doc.Devices.ByName["CR5"].Comment);
        Assert.Equal("表のみ", doc.Devices.ByName["CR6"].Comment);
    }

    /// <summary>桁数は元の名のものを保ち、桁が溢れれば伸びる。</summary>
    [Fact]
    public void InsertAt_ZeroPaddedNames_KeepWidth()
    {
        var doc = DocWithElements("CR08", "CR09");

        DeviceNumberShifter.InsertAt(doc, "CR08");

        Assert.Equal(new[] { "CR09", "CR10" }, NamesOf(doc));
    }

    /// <summary>接頭辞は大文字小文字を区別せず、綴りは元の名のものを保つ。</summary>
    [Fact]
    public void InsertAt_PrefixCaseDiffers_TreatedAsSameSeries()
    {
        var doc = DocWithElements("CR4", "cr5");

        DeviceNumberShifter.InsertAt(doc, "CR4");

        Assert.Equal(new[] { "CR5", "cr6" }, NamesOf(doc));
    }

    /// <summary>送り先が重なる場合は送らず、理由を返す（別々の機器を黙って同じ名へ潰さぬ）。</summary>
    [Fact]
    public void InsertAt_TargetsCollide_DoesNothingAndReportsConflict()
    {
        var doc = DocWithElements("CR9", "CR09");

        var result = DeviceNumberShifter.InsertAt(doc, "CR9");

        Assert.NotNull(result.Conflict);
        Assert.Empty(result.Renames);
        Assert.Equal(new[] { "CR9", "CR09" }, NamesOf(doc));
    }

    /// <summary>挿入位置より上の番号が無ければ（指定した名自体も無ければ）何も送らぬ。</summary>
    [Fact]
    public void InsertAt_NothingAtOrAbove_DoesNothing()
    {
        var doc = DocWithElements("CR1", "CR2");

        var result = DeviceNumberShifter.InsertAt(doc, "CR3");

        Assert.Null(result.Conflict);
        Assert.Empty(result.Renames);
        Assert.Equal(new[] { "CR1", "CR2" }, NamesOf(doc));
    }

    /// <summary>詰め＝挿入の逆。CR1 を消した後に CR2 を指定すれば CR2 以降が一つずつ前へ出る。</summary>
    [Fact]
    public void RemoveAt_AfterLowerNumberDeleted_ShiftsThatNumberAndAboveDown()
    {
        var doc = DocWithElements("CR2", "CR3", "CR4");

        var result = DeviceNumberShifter.RemoveAt(doc, "CR2");

        Assert.Null(result.Conflict);
        Assert.Equal(new[] { "CR1", "CR2", "CR3" }, NamesOf(doc));
        // 小さい番号から送る（逆順では CR3 を CR2 にした時点で元の CR2 と混ざる）。
        Assert.Equal(new[] { ("CR2", "CR1"), ("CR3", "CR2"), ("CR4", "CR3") }, result.Renames);
    }

    /// <summary>起点より下の番号は動かさず、欠番でも止めずに末尾まで詰める。</summary>
    [Fact]
    public void RemoveAt_MiddleOfSeriesWithGap_KeepsLowerAndShiftsBeyondGap()
    {
        var doc = DocWithElements("CR1", "CR2", "CR4", "CR5", "CR10", "T4");

        DeviceNumberShifter.RemoveAt(doc, "CR4");

        Assert.Equal(new[] { "CR1", "CR2", "CR3", "CR4", "CR9", "T4" }, NamesOf(doc));
    }

    /// <summary>詰める先の番号がまだ使われておれば送らず、理由を返す（別々の機器を同じ名へ潰さぬ）。</summary>
    [Fact]
    public void RemoveAt_TargetStillInUse_DoesNothingAndReportsConflict()
    {
        var doc = DocWithElements("CR1", "CR2", "CR3");

        var result = DeviceNumberShifter.RemoveAt(doc, "CR2");

        Assert.NotNull(result.Conflict);
        Assert.Empty(result.Renames);
        Assert.Equal(new[] { "CR1", "CR2", "CR3" }, NamesOf(doc));
    }

    /// <summary>詰める先が機器表にのみ残っておる場合も塞がりと見る。</summary>
    [Fact]
    public void RemoveAt_TargetOnlyInDeviceTable_ReportsConflict()
    {
        var doc = DocWithElements("CR2");
        doc.Devices.ByName["CR1"] = new Device { Name = "CR1" };

        var result = DeviceNumberShifter.RemoveAt(doc, "CR2");

        Assert.NotNull(result.Conflict);
        Assert.Equal(new[] { "CR2" }, NamesOf(doc));
    }

    /// <summary>番号 0 はそれ以上前へ送れぬ。</summary>
    [Fact]
    public void RemoveAt_NumberZero_ReportsConflict()
    {
        var doc = DocWithElements("X0", "X1");

        var result = DeviceNumberShifter.RemoveAt(doc, "X0");

        Assert.NotNull(result.Conflict);
        Assert.Equal(new[] { "X0", "X1" }, NamesOf(doc));
    }

    /// <summary>桁数を保つのは先頭が 0 の名のみ。CR10 を詰めて CR09 にはせぬ。</summary>
    [Fact]
    public void RemoveAt_DigitCountDrops_OnlyZeroPaddedNamesKeepWidth()
    {
        var doc = DocWithElements("CR10", "T010");

        DeviceNumberShifter.RemoveAt(doc, "CR10");
        DeviceNumberShifter.RemoveAt(doc, "T010");

        Assert.Equal(new[] { "CR9", "T009" }, NamesOf(doc));
    }

    /// <summary>挿入してから同じ位置で詰めれば元へ戻る。
    /// <para><b>ただし桁が溢れた 0 埋めの名は戻らぬ</b>——CR09 は CR10 になった時点で「先頭が 0」という
    /// 手掛かりを失い、詰め戻すと CR9 になる。ここでは溢れぬ名のみで往復を測る。</para></summary>
    [Fact]
    public void InsertAtThenRemoveAt_RestoresOriginalNames()
    {
        var doc = DocWithElements("CR1", "CR4", "CR5", "CR9", "CR10", "T007");

        DeviceNumberShifter.InsertAt(doc, "CR4");
        DeviceNumberShifter.InsertAt(doc, "T007");
        DeviceNumberShifter.RemoveAt(doc, "CR5");
        DeviceNumberShifter.RemoveAt(doc, "T008");

        Assert.Equal(new[] { "CR1", "CR4", "CR5", "CR9", "CR10", "T007" }, NamesOf(doc));
    }

    /// <summary>Plan は段取りを立てるのみで、図面を書き換えぬ。</summary>
    [Fact]
    public void Plan_DoesNotModifyDocument()
    {
        var doc = DocWithElements("CR4", "CR5");

        var result = DeviceNumberShifter.Plan(doc, "CR4");

        Assert.Equal(new[] { ("CR5", "CR6"), ("CR4", "CR5") }, result.Renames);
        Assert.Equal(new[] { "CR4", "CR5" }, NamesOf(doc));
    }
}
