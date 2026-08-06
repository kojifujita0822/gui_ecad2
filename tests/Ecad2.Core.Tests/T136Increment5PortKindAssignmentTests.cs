using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-136(B)増分5 論点5「既存部品への接続点の種類の割り当て」（殿裁定2026-08-02）の回帰テスト。
/// <para>
/// <b>殿の裁定</b>＝<b>モータのみ青（3端子とも同色）、残り30件は赤。</b>
/// 内訳＝組込み（<see cref="ElementCatalog"/>）17件のうち <see cref="ElementKind.Motor"/> が青・16件が赤／
/// 自作テンプレート（<see cref="BasicPartTemplates"/>）15件のうちモータが青・14件が赤。計32件。
/// </para>
/// <para>
/// <b>【2026-08-06の改訂＝自作テンプレートのモータが接続点を失った】</b>殿がモータの図柄をお描き直しになり、
/// その <c>ports:[]</c> を含めて一括で採る裁定が下った（殿裁定＝案C。詳細は
/// <see cref="自作テンプレート_モータは接続点を持たぬ"/> のコメント）。
/// <b>「モータのみ青」という骨子は組込み側に生きており、覆っておらぬ。</b>
/// <b>現時点の員数＝接続点を持つのは33件</b>（組込み17件＋自作16件）<b>、うち青は組込みのモータ1件のみ、赤32件。</b>
/// </para>
/// <para>
/// <b>【T-133増分7で自作テンプレートが15→17件になった】</b>サーマルリレーa/bの移植による。
/// <b>裁定の骨子「モータのみ青」は変わらず、新規2件は接点ゆえ赤</b>（<see cref="PortDef"/> の既定）
/// ——モータの青（<see cref="PortKind.DrcExempt"/>）はDRC免除のための扱いにて、
/// シミュレート対象の接点には当たらぬ。<b>赤の員数のみ30→32件、総数32→34件へ改まった。</b>
/// <b>本テスト群が「堰き止め」として設計どおり働き、この判断を促した</b>
/// ——員数を固定していなければ、新規2件は既定値のまま黙って流れ込んでおった。
/// </para>
/// <para>
/// <b>「17件」の1件＝<see cref="ElementKind"/> の1メンバー</b>である（部品の実体数ではない）。
/// 主回路3極記号3種は接続点を0個しか持たぬゆえ17件に含まれておらぬ。
/// 組込み17件と自作テンプレート15件は別データにて、同名（例＝a接点）でも独立に設定する
/// ——ゆえに32＝15＋17で重複はない。
/// </para>
/// <para>
/// <b>なぜ赤を明示引数で書かず、テストで固定するか</b>：<see cref="PortDef"/> の <c>Kind</c> は
/// 既定が <see cref="PortKind.Power"/>（赤）ゆえ、30件は何も書かずとも赤になる。
/// されど<b>既定に頼るだけでは「赤にすると決めた」ことが記録に残らぬ</b>——
/// 誰かが既定値を変えれば30件が黙って青へ転ずる。
/// 明示引数を30箇所へ書き足すより、<b>本テスト群で全件を押さえる方が読み手に優しく、
/// かつ既定値の変更を検出できる</b>（<c>samurai.md</c>「既存の式・定数・既定値を変える前に、
/// その値を前提にした既存テストを洗う」の受け皿でもある）。
/// </para>
/// <para>
/// <b>【消す前に読まれたい】本テスト群は「殿の裁定そのもの」を記録したものである。</b>
/// 32件を一つずつ押さえるのは冗長に見えるが、<b>冗長さこそが目的にござる</b>——
/// 実装の側には赤の記述が一つも無く（既定値に依る）、
/// <b>「30件を赤と決めた」という事実は本テスト群にしか残っておらぬ。</b>
/// ここを削れば、次に既定値へ手を入れる者は何も気づかずに30件を転じさせ得る。
/// 実装を読んでも出て来ぬ決定ゆえ、テストが唯一の記録になっておる。
/// </para>
/// </summary>
public class T136Increment5PortKindAssignmentTests
{
    /// <summary>接続点を持つ組込み種別（＝主回路3極記号3種を除いた17種）。</summary>
    private static IReadOnlyList<ElementKind> KindsWithPorts()
        => Enum.GetValues<ElementKind>().Where(k => ElementCatalog.Ports(k, 1).Count > 0).ToList();

    // ---- 母集団の健全性（数が黙って変わらぬための網） ----

    /// <summary>
    /// 組込みで接続点を持つ種別は17件。
    /// <b>新しい種別が足されればここが落ちる</b>——そのとき赤か青かは殿の裁定を要するゆえ、
    /// 既定へ黙って流れ込まぬよう堰き止める。
    /// </summary>
    [Fact]
    public void 組込み_接続点を持つ種別は17件()
        => Assert.Equal(17, KindsWithPorts().Count);

    /// <summary>
    /// 自作テンプレートは17件（T-133増分7でサーマルリレーa/bを足し、15→17）。理由は上と同じ。
    /// <para>
    /// <b>【この17は<c>All()</c>自体の件数であり、部品リストの表示件数19とは別の単位】</b>
    /// 表示件数は<c>All()</c>17件＋OR論理2件（<c>ContactNO</c>／<c>ContactNC</c>の
    /// <c>IsOrEligible</c>分、<c>PartPaletteViewModel.cs:75-76</c>）＝19件。
    /// <b>増分7の前は「<c>All()</c>15件・表示17件」であった</b>——
    /// <b>同じ「17」が前後で別の意味を指すゆえ、単位を明記しておく</b>（隠密の検算、2026-08-06）。
    /// </para>
    /// </summary>
    [Fact]
    public void 自作テンプレートは17件()
        => Assert.Equal(17, BasicPartTemplates.All().Count);

    /// <summary>
    /// 主回路3極記号3種は接続点を持たぬ（＝17件の外にある）ことの対照。
    /// これが崩れると上の「17件」の意味が変わる。
    /// </summary>
    [Theory]
    [InlineData(ElementKind.Breaker3P)]
    [InlineData(ElementKind.ContactorMain3P)]
    [InlineData(ElementKind.ThermalOverload3P)]
    public void 組込み_主回路3極記号は接続点を持たぬ(ElementKind kind)
        => Assert.Empty(ElementCatalog.Ports(kind, 1));

    // ---- 組込み17件の割り当て ----

    /// <summary>
    /// 組込みのモータは3端子とも青。
    /// <b>3端子を個別に確かめる</b>——殿の裁定「3端子とも同色」は、
    /// U・V・W のいずれか1つだけ変えても崩れるゆえ。
    /// </summary>
    [Fact]
    public void 組込み_モータの3端子はいずれも青()
    {
        var ports = ElementCatalog.Ports(ElementKind.Motor, 1);

        Assert.Equal(3, ports.Count);
        Assert.All(ports, p => Assert.Equal(PortKind.DrcExempt, p.Kind));
    }

    /// <summary>組込みのモータ以外16種は、すべての接続点が赤。</summary>
    [Fact]
    public void 組込み_モータ以外16種の接続点はすべて赤()
    {
        var kinds = KindsWithPorts().Where(k => k != ElementKind.Motor).ToList();

        Assert.Equal(16, kinds.Count);
        foreach (var k in kinds)
            Assert.All(ElementCatalog.Ports(k, 1), p => Assert.Equal(PortKind.Power, p.Kind));
    }

    // ---- 自作テンプレート15件の割り当て ----

    /// <summary>
    /// 自作テンプレートのモータは接続点を持たぬ（<b>殿裁定2026-08-06による改訂。旧＝3端子とも青</b>）。
    /// <para>
    /// <b>【なぜ改まったか——員数合わせではない】</b>殿がパーツエディタでモータの図柄をお描き直しになり
    /// （<c>sample/モーター(横).gcadparts</c>）、その <c>ports:[]</c> を含めて<b>一括で採る</b>裁定が下った。
    /// <b>家老は「接続点の青丸3つが消える」を選択肢に明記してお諮りし、殿はそれをご覧のうえでお選びになった</b>
    /// ——意図的な裁定である。
    /// </para>
    /// <para>
    /// <b>【増分5の裁定「モータのみ青」そのものは覆っておらぬ】</b>変わったのは自作テンプレート側だけで、
    /// <b>組込み側（<see cref="組込み_モータの3端子はいずれも青"/>）は今も青</b>——T-133裁定5
    /// 「ポート定義は不変」が生きておるゆえ（殿裁定2026-08-06＝案C。侍が両裁定の衝突を報じ、殿が裁かれた）。
    /// <b>ゆえに同じ三相モータでも、置き方によって接続点の有無が違う</b>——メニュー／部品リストから
    /// 置けば接続点なし、<c>Kind</c> 経路（T-133増分8の縦向き）で置けば青の3点。
    /// </para>
    /// <para>
    /// <b>接続点を失っても電気的な振舞いは変わらぬ</b>——<c>Role=NonSimulated</c> ゆえ T-136(C) で既に
    /// 結線から外れており、<c>PartResolver.ParticipatesInWiring</c> は接続点の有無を見ておらぬ。
    /// </para>
    /// </summary>
    [Fact]
    public void 自作テンプレート_モータは接続点を持たぬ()
    {
        var motor = BasicPartTemplates.All().Single(p => p.Id == BasicPartTemplates.MotorId);

        Assert.Empty(motor.Ports);
    }

    /// <summary>自作テンプレートのモータ以外16件は、すべての接続点が赤
    /// （T-133増分7でサーマルリレーa/bが加わり14→16。両件とも接点ゆえ赤）。</summary>
    [Fact]
    public void 自作テンプレート_モータ以外16件の接続点はすべて赤()
    {
        var others = BasicPartTemplates.All().Where(p => p.Id != BasicPartTemplates.MotorId).ToList();

        Assert.Equal(16, others.Count);
        foreach (var part in others)
            Assert.All(part.Ports, p => Assert.Equal(PortKind.Power, p.Kind));
    }

    // ---- 既定値そのものへの網 ----

    /// <summary>
    /// <see cref="PortDef"/> の <c>Kind</c> の既定は赤。
    /// 上の「30件が赤」は、その大半が既定値に依っておる——ゆえに既定が変われば意味が変わる。
    /// <b>既定を変えた者がここで気づけるよう、直に押さえる。</b>
    /// </summary>
    [Fact]
    public void PortDefの種類の既定は赤()
        => Assert.Equal(PortKind.Power, new PortDef("X", 0, 0).Kind);
}
