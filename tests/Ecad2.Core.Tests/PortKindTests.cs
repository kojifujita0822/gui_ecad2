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
    /// 【往復2周目・家老裁定】throw化を撤回し、未知の値は目立つ色(PortUnknown)へ寄せる形に改めた
    /// (描画中の例外は画面が落ちパーツを喪失しうるため)。JSON経由で数値表記("kind": 99)を渡すと
    /// JsonStringEnumConverterが検めず未知の値のままPortColorへ届く経路が実在する(隠密指摘、
    /// JsonOptions.cs:18)。
    /// </summary>
    [Fact]
    public void DrawingTheme_PortColor_未知の値はPortUnknownを返す()
    {
        Assert.Equal(DrawingTheme.PortUnknown, DrawingTheme.PortColor((PortKind)99));
    }

    [Fact]
    public void DrawingTheme_PortUnknownはPortPowerともPortDrcExemptとも異なる色である()
    {
        // PortUnknownが既存2色のいずれかと同値では、フォールバックが働いても目に立たない
        // （案2の狙い＝「case行が消えても即座に目立つ」が成立しなくなる）。
        Assert.NotEqual(DrawingTheme.PortPower, DrawingTheme.PortUnknown);
        Assert.NotEqual(DrawingTheme.PortDrcExempt, DrawingTheme.PortUnknown);
    }

    /// <summary>
    /// PartOptimizer.ClampPortsToFrame（PartEditorCanvas.UpdatePortDragと同型の`with`式を使う）が
    /// Kindを温存することの実測。隠密の留保「positional record structの既定引数埋めは型の性質からの
    /// 推論であり実測しておらぬ」への回答（親計画書§4増分4）。
    /// 【往復1周目訂正・家老の静的レビュー指摘】クランプが発生しない経路（RowOffset/BoundaryOffsetが
    /// 範囲内）では、実装が`row == p.RowOffset &amp;&amp; boundary == p.BoundaryOffset`の分岐で
    /// <c>p</c>自身をそのまま返すため、<c>with</c>式そのものを通らない——すなわちこの経路のテストは
    /// 「Kindの温存」を検証しておらず、検出力を持たぬ（壊す実測で確認済み。下記参照）。
    /// 検出力があるのは「クランプが発生する」経路（<c>with</c>式を実際に通る）のみ。
    /// </summary>
    [Theory]
    [InlineData(PortKind.Power)]
    [InlineData(PortKind.DrcExempt)]
    public void ClampPortsToFrame_範囲内でもKindは変わらない(PortKind kind)
    {
        // 【検出力なし・記録のみ】この経路はwith式を通らずpがそのまま返るため、Kindが壊れる余地が
        // 無い（実装が分岐そのものを削って常にwith式を通す形へ変わった場合のみ効く安全網）。
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
        // 【検出力の限界・家老の静的レビュー指摘で判明】"Kind=Powerへ強制上書き"という壊し方では、
        // このケースのうちkind=Power側は改変後も期待値と一致してしまい検出できぬ（偽陰性）。
        // kind=DrcExempt側のみ検出できる。両側の検出力は次の反転検証で別途確かめる。
        var result = PartOptimizer.ClampPortsToFrame(
            new[] { new PortDef("P1", -3, 1, kind) }, widthCells: 4, heightCells: 2);

        var port = Assert.Single(result);
        Assert.Equal(-1, port.RowOffset); // クランプされたことの確認（Kindだけの回帰にならぬよう）
        Assert.Equal(kind, port.Kind);
    }

    [Fact]
    public void ClampPortsToFrame_PowerとDrcExemptが混在しても個別に温存される()
    {
        // 【往復1周目訂正・家老の静的レビュー指摘】旧版はRowOffset/BoundaryOffsetとも範囲内で
        // with式を通らず検出力ゼロだった。クランプが発生する値へ改め、真にwith式を通る経路で
        // 複数種類の混在を確かめる（退化＝単一種類のみを避ける趣旨は維持）。
        var result = PartOptimizer.ClampPortsToFrame(
            new[] { new PortDef("A", -3, 1, PortKind.Power), new PortDef("B", 5, 2, PortKind.DrcExempt) },
            widthCells: 4, heightCells: 2);

        Assert.Equal(-1, result[0].RowOffset); // クランプされたことの確認（両方とも範囲外→範囲内）
        Assert.Equal(1, result[1].RowOffset);
        Assert.Equal(PortKind.Power, result[0].Kind);
        Assert.Equal(PortKind.DrcExempt, result[1].Kind);
    }

}
