using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-147: <see cref="SvgRenderer.GenerateSymbolSvg"/> が <c>SymbolGlyphs.Draw</c> へ orient を
/// 渡すようにする。従前は渡しておらず、向きを持つ記号（主回路3極記号＝"H"で横向き、三相モータ＝
/// "V"で縦向き）が常に既定の向きで描かれていた。
/// <para>
/// 起票時点で実害は無い（<c>GenerateSymbolSvg</c> は src 内に呼び手0件）。それでも直す理由は、
/// DiagramRenderer 側の2つの呼び出しが既に orient を渡しており、ここだけが渡さない非対称が
/// 「将来呼ばれたときに向きが落ちる」形の負債になるため（家老の判断、台帳 T-147）。
/// </para>
/// <para>
/// RED先行証明の形＝新設の引数に依存するため、修正前のコードではテストがコンパイルできない
/// （memory: feedback_red_proof_new_api_limitation）。ゆえに根本原因の再現可否へ観点を切り替え、
/// 修正後のコードで <c>orient: orient</c> を <c>orient: null</c> へ戻す壊す実測で検出力を測った。
/// </para>
/// </summary>
public class T147SvgRendererOrientTests
{
    [Fact]
    public void GenerateSymbolSvg_三相モータはorientで絵が変わる()
    {
        string defaultSvg = SvgRenderer.GenerateSymbolSvg(ElementKind.Motor);
        string verticalSvg = SvgRenderer.GenerateSymbolSvg(ElementKind.Motor, orient: "V");

        Assert.NotEqual(defaultSvg, verticalSvg);
    }

    [Fact]
    public void GenerateSymbolSvg_主回路3極記号はorientで絵が変わる()
    {
        string defaultSvg = SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P);
        string horizontalSvg = SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P, orient: "H");

        Assert.NotEqual(defaultSvg, horizontalSvg);
    }

    /// <summary>素朴なベースライン（memory: feedback_control_experiment_needs_naive_baseline）。
    /// 向きを持たない種別では orient を渡しても絵が変わらないことを示す。これが無いと、上の2件が
    /// 「orient が効いた」のか「引数を足したこと自体で何かが変わった」のかを弁別できない。</summary>
    [Fact]
    public void GenerateSymbolSvg_向きを持たぬ種別はorientを渡しても変わらない()
    {
        string defaultSvg = SvgRenderer.GenerateSymbolSvg(ElementKind.ContactNO);

        Assert.Equal(defaultSvg, SvgRenderer.GenerateSymbolSvg(ElementKind.ContactNO, orient: "V"));
        Assert.Equal(defaultSvg, SvgRenderer.GenerateSymbolSvg(ElementKind.ContactNO, orient: "H"));
    }

    /// <summary>orient 未指定時の絵が従前と変わっていないこと（既定の互換）。既定引数を足した
    /// だけで既存の呼び出し（引数3つまで）の出力が動いては、実害0件という前提が崩れる。</summary>
    [Fact]
    public void GenerateSymbolSvg_orient未指定は明示的なnullと同じ絵になる()
    {
        Assert.Equal(
            SvgRenderer.GenerateSymbolSvg(ElementKind.Motor),
            SvgRenderer.GenerateSymbolSvg(ElementKind.Motor, orient: null));
    }

    // ---- T-150: variant も同じ非対称であったため併せて渡す ----

    /// <summary>variant が効くのは <see cref="ElementKind.Breaker3P"/> の一箇所のみで、
    /// 効き方は <c>variant == "ELB"</c> のときテストボタンの小四角を描き足すか否か
    /// （<c>SymbolGlyphs.cs</c> の Breaker3P、侍の一次ソース直読2026-08-08）。</summary>
    [Fact]
    public void GenerateSymbolSvg_主回路3極記号はELBで絵が変わる()
    {
        string defaultSvg = SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P);
        string elbSvg = SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P, variant: "ELB");

        Assert.NotEqual(defaultSvg, elbSvg);
    }

    /// <summary><b>射程の証拠であり、これを測らねば次に読む者が「ブレーカ三種を描き分ける」と
    /// 誤読する。</b>NFB・MCCB は既定と同じ絵になる——<c>SymbolGlyphs</c> が見るのは "ELB" か否かのみ。
    /// 図面上で NFB と MCCB を見分けているのは記号の形ではなく <c>DiagramRenderer</c> が記号脇へ記す
    /// 文字であり、そちらは <see cref="SvgRenderer.GenerateSymbolSvg"/> の描画対象外にござる。</summary>
    [Theory]
    [InlineData("NFB")]
    [InlineData("MCCB")]
    public void GenerateSymbolSvg_主回路3極記号はNFBとMCCBでは絵が変わらない(string variant)
    {
        Assert.Equal(
            SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P),
            SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P, variant: variant));
    }

    /// <summary>素朴なベースライン。variant を見ぬ種別では渡しても絵が変わらぬ。</summary>
    [Fact]
    public void GenerateSymbolSvg_variantを見ぬ種別は渡しても変わらない()
    {
        string defaultSvg = SvgRenderer.GenerateSymbolSvg(ElementKind.Motor);

        Assert.Equal(defaultSvg, SvgRenderer.GenerateSymbolSvg(ElementKind.Motor, variant: "ELB"));
    }

    /// <summary>orient と variant を同時に渡しても双方が効くこと。片方だけを渡す形でしか測らねば、
    /// 引数の取り違え（variant を orient の位置へ渡す等）を素通しする。</summary>
    [Fact]
    public void GenerateSymbolSvg_orientとvariantは同時に効く()
    {
        string both = SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P, orient: "H", variant: "ELB");

        Assert.NotEqual(SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P, orient: "H"), both);
        Assert.NotEqual(SvgRenderer.GenerateSymbolSvg(ElementKind.Breaker3P, variant: "ELB"), both);
    }
}
