using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分4-A: 配置メニューのタグ解析（<c>SymbolTagParser.TryParse</c>）。
/// <para>
/// <b>【この関数を切り出した値打ちは、拒む側を測れることにある】</b>
/// 解釈をメニューの <c>Click</c> ハンドラへ書けば動きはするが、<b>不正入力・退化入力の振る舞いを
/// 測る術が無くなる</b>。切り出したゆえ「どこまで受け入れ、どこから拒むか」を数で固定できる。
/// </para>
/// <para>
/// <b>【呼び手は増分4-C で繋ぐ】</b>本増分の時点では参照0件——<b>意図した中間状態</b>である。
/// 「作ったのに一度も呼ばれておらぬ」を素通しせぬよう、繋ぎ込みは増分4-C で別に測る
/// （<c>samurai.md</c>【MUST】、T-125増分αとT-144の実例）。
/// </para>
/// </summary>
public class SymbolTagParserTests
{
    // ===== 受け入れる形 =====

    /// <summary>向き付きのタグ（3極記号3種 × V/H の6通り＝増分4-C で実際にメニューへ載る組み合わせ）。</summary>
    [Theory]
    [InlineData("Breaker3P#V", ElementKind.Breaker3P, "V")]
    [InlineData("Breaker3P#H", ElementKind.Breaker3P, "H")]
    [InlineData("ContactorMain3P#V", ElementKind.ContactorMain3P, "V")]
    [InlineData("ContactorMain3P#H", ElementKind.ContactorMain3P, "H")]
    [InlineData("ThermalOverload3P#V", ElementKind.ThermalOverload3P, "V")]
    [InlineData("ThermalOverload3P#H", ElementKind.ThermalOverload3P, "H")]
    public void 向き付きのタグを解ける(string tag, ElementKind expectedKind, string expectedOrient)
    {
        Assert.True(SymbolTagParser.TryParse(tag, out var kind, out var orient));

        Assert.Equal(expectedKind, kind);
        Assert.Equal(expectedOrient, orient);
    }

    /// <summary>向きを持たぬタグは、向きが <c>null</c> で成功する
    /// （原本のタグ形式は実質5通りあり、<c>Kind</c> 単独もその一つ＝隠密の調査書）。</summary>
    [Theory]
    [InlineData("Breaker3P", ElementKind.Breaker3P)]
    [InlineData("ContactNO", ElementKind.ContactNO)]
    [InlineData("Motor", ElementKind.Motor)]
    public void 向きなしのタグは向きがnullで成功する(string tag, ElementKind expectedKind)
    {
        Assert.True(SymbolTagParser.TryParse(tag, out var kind, out var orient));

        Assert.Equal(expectedKind, kind);
        Assert.Null(orient);
    }

    /// <summary>
    /// <b>種別と向きの組み合わせの妥当性は見ない</b>——本クラスが見るのは文字列の形だけである
    /// （組み合わせの正しさはタグを書くメニュー定義の責務。実装のクラスコメント参照）。
    /// <b>「なぜコイルに向きが付いても通るのか」を後の者が疑わぬよう、意図として固定しておく。</b>
    /// </summary>
    [Fact]
    public void 向きを持たぬ種別に向きが付いても形が正しければ通る()
    {
        Assert.True(SymbolTagParser.TryParse("Coil#V", out var kind, out var orient));

        Assert.Equal(ElementKind.Coil, kind);
        Assert.Equal("V", orient);
    }

    // ===== 拒む形 =====

    /// <summary>空・null は拒む（退化入力）。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 空のタグは拒む(string? tag)
        => Assert.False(SymbolTagParser.TryParse(tag, out _, out _));

    /// <summary>
    /// 向きは <c>V</c>／<c>H</c> のみ。<b>綴り誤りを黙って通せば、<c>Params[Orient]</c> に
    /// 意味を持たぬ値が入り、描画は既定の縦向きへ静かに倒れる</b>——誤りが画面に現れぬ。
    /// <b>小文字も拒む</b>のは、<c>DiagramRenderer</c> の判定が <c>orient == "H"</c> と
    /// 大文字小文字を区別するためである。
    /// </summary>
    [Theory]
    [InlineData("Breaker3P#X")]
    [InlineData("Breaker3P#")]
    [InlineData("Breaker3P#v")]
    [InlineData("Breaker3P#h")]
    [InlineData("Breaker3P#VV")]
    [InlineData("Breaker3P#縦")]
    public void 向きがVH以外なら拒む(string tag)
        => Assert.False(SymbolTagParser.TryParse(tag, out _, out _));

    /// <summary>未知の種別名は拒む。<b>種別名の大文字小文字も区別する</b>
    /// （タグは我々が書くゆえ厳格でよく、緩めれば綴り誤りが露見せぬ）。</summary>
    [Theory]
    [InlineData("Foo#V")]
    [InlineData("Foo")]
    [InlineData("breaker3p#V")]
    [InlineData("BREAKER3P")]
    [InlineData("#V")]
    [InlineData("#")]
    public void 未知の種別名は拒む(string tag)
        => Assert.False(SymbolTagParser.TryParse(tag, out _, out _));

    /// <summary>
    /// 数値表記は拒む。
    /// <b>【この網が要る理由】<c>Enum.TryParse</c> は数値文字列を受け入れる</b>
    /// ——<c>"0"</c> は <c>ContactNO</c>、<c>"999"</c> は未定義値として通ってしまう。
    /// 名前で照合する実装にしたのはこれを塞ぐためであり、<b>実装を <c>Enum.TryParse</c> だけへ
    /// 戻せば、このテストが鳴る。</b>
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("0#V")]
    [InlineData("999")]
    [InlineData("-1")]
    public void 数値表記の種別は拒む(string tag)
        => Assert.False(SymbolTagParser.TryParse(tag, out _, out _));

    /// <summary>区切りが2つ以上あるタグは拒む（形が定まらぬため）。</summary>
    [Theory]
    [InlineData("Breaker3P#V#H")]
    [InlineData("Breaker3P##V")]
    [InlineData("##")]
    public void 区切りが二つ以上なら拒む(string tag)
        => Assert.False(SymbolTagParser.TryParse(tag, out _, out _));

    /// <summary>
    /// 拒んだときの出力は既定値のまま（呼び出し側が戻り値を見ずに使う誤りへの備え）。
    /// <b>「失敗したのに半端な値が残っている」型の事故を塞ぐ。</b>
    /// <para>
    /// <b>【拒む段ごとに測る・RED証明の途中で気づいた穴】</b>当初は <c>"Foo#V"</c> の一件だけを
    /// 測っておったが、<b>それは種別名の段で弾かれるため、向きの段より後ろの穴を測れておらぬ</b>
    /// ——<b>早く弾かれる入力だけでは、後段の緩みが現れぬ</b>。
    /// <c>samurai.md</c>「範囲の外側を測る」と同じ形の見落としにて、拒む段ごとに1件ずつ置く。
    /// </para>
    /// <para>
    /// <b>【4段すべてを揃えた・隠密の指摘 2026-08-02】</b>拒む段は4つ（空・区切り過多・未知種別・向き不正）
    /// あるが、当初は後ろ3段しか測っておらなんだ。<b>1段目は初期化直後の最速 return ゆえ構造上自明に安全</b>
    /// と隠密も判じておるが、<b>「3段は測ったが1段は測らぬ」という非対称を残せば、
    /// 次に段を足す者が「どこまで測るのが作法か」を測りかねる。</b>1行で揃うゆえ足す。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(null)]             // 空・null の段（最前段）
    [InlineData("")]               // 同上
    [InlineData("Breaker3P#V#H")]  // 区切りの段
    [InlineData("Foo#V")]          // 種別名の段
    [InlineData("Breaker3P#X")]    // 向きの段（最後段）
    public void 拒んだときの出力は既定値のまま(string? tag)
    {
        Assert.False(SymbolTagParser.TryParse(tag, out var kind, out var orient));

        Assert.Equal(default, kind);
        Assert.Null(orient);
    }
}
