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
}
