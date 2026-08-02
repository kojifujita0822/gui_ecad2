using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-136(B)増分4（殿裁定2026-08-02）: 接続点の種類（<see cref="PortKind"/>）の回帰テスト。
/// 計画書=docs/ecad2-t136-increment4-plan-samurai.md §6。
/// 【射程】ここで測るのは「器（既定値・温存・色変換）」のみ。実際の見え方（赤丸／青丸）は
/// View層（PartEditorCanvas）にて忍者の実機確認へ委ねる（本増分は接続処理へ反映しない）。
/// </summary>
public class PortKindTests
{
    [Fact]
    public void PortDef_第4引数省略時は既定値Powerになる()
    {
        // T-136(B)増分3以前からの全呼び出し箇所（ElementCatalog・BasicPartTemplates・
        // PartEditorCanvas.AddPort）と同型の3引数呼び出しを模す。
        var port = new PortDef("L", 0, 0);

        Assert.Equal(PortKind.Power, port.Kind);
    }

    [Theory]
    [InlineData(PortKind.Power)]
    [InlineData(PortKind.DrcExempt)]
    public void PortDef_第4引数を明示すれば保持される(PortKind kind)
    {
        var port = new PortDef("L", 0, 0, kind);

        Assert.Equal(kind, port.Kind);
    }

    [Theory]
    [InlineData(PortKind.Power)]
    [InlineData(PortKind.DrcExempt)]
    public void DrawingTheme_PortColorは種類ごとに異なる色を返す(PortKind kind)
    {
        var expected = kind == PortKind.Power ? DrawingTheme.PortPower : DrawingTheme.PortDrcExempt;

        Assert.Equal(expected, DrawingTheme.PortColor(kind));
    }

    [Fact]
    public void DrawingTheme_PortPowerとPortDrcExemptは異なる色である()
    {
        // 赤・青が同値では種類の描き分けそのものが成立しない（対称・退化の罠を自ら確かめる）。
        Assert.NotEqual(DrawingTheme.PortPower, DrawingTheme.PortDrcExempt);
    }

    /// <summary>
    /// PartOptimizer.ClampPortsToFrame（PartEditorCanvas.UpdatePortDragと同型の`with`式を使う）が
    /// Kindを温存することの実測。隠密の留保「positional record structの既定引数埋めは型の性質からの
    /// 推論であり実測しておらぬ」への回答（親計画書§4増分4）。
    /// クランプが発生する経路・発生しない経路の両方でKindが変わらぬことを確認する
    /// （`with`式は指定したプロパティのみ変更するため理屈の上では自明だが、実測で確かめる）。
    /// </summary>
    [Theory]
    [InlineData(PortKind.Power)]
    [InlineData(PortKind.DrcExempt)]
    public void ClampPortsToFrame_範囲内でもKindは変わらない(PortKind kind)
    {
        var result = PartOptimizer.ClampPortsToFrame(
            new[] { new PortDef("P1", 0, 1, kind) }, widthCells: 4, heightCells: 2);

        Assert.Equal(kind, Assert.Single(result).Kind);
    }

    [Theory]
    [InlineData(PortKind.Power)]
    [InlineData(PortKind.DrcExempt)]
    public void ClampPortsToFrame_クランプが発生してもKindは変わらない(PortKind kind)
    {
        // RowOffset=-3はheightCells=2(rowLimit=1)の範囲外ゆえクランプが発生する経路
        // （PartOptimizerClampPortsTests.ClampPortsToFrame_RowOffsetBelowRange...と同じ入力）。
        var result = PartOptimizer.ClampPortsToFrame(
            new[] { new PortDef("P1", -3, 1, kind) }, widthCells: 4, heightCells: 2);

        var port = Assert.Single(result);
        Assert.Equal(-1, port.RowOffset); // クランプされたことの確認（Kindだけの回帰にならぬよう）
        Assert.Equal(kind, port.Kind);
    }

    [Fact]
    public void ClampPortsToFrame_PowerとDrcExemptが混在しても個別に温存される()
    {
        // 退化（単一種類のみ）を避け、複数種類が混在する検体でも取り違えが無いことを確かめる。
        var result = PartOptimizer.ClampPortsToFrame(
            new[] { new PortDef("A", 0, 0, PortKind.Power), new PortDef("B", 0, 1, PortKind.DrcExempt) },
            widthCells: 4, heightCells: 2);

        Assert.Equal(PortKind.Power, result[0].Kind);
        Assert.Equal(PortKind.DrcExempt, result[1].Kind);
    }
}
