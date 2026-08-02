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
    /// 自作テンプレートは15件。理由は上と同じ。
    /// </summary>
    [Fact]
    public void 自作テンプレートは15件()
        => Assert.Equal(15, BasicPartTemplates.All().Count);

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

    /// <summary>自作テンプレートのモータは3端子とも青。</summary>
    [Fact]
    public void 自作テンプレート_モータの3端子はいずれも青()
    {
        var motor = BasicPartTemplates.All().Single(p => p.Id == BasicPartTemplates.MotorId);

        Assert.Equal(3, motor.Ports.Count);
        Assert.All(motor.Ports, p => Assert.Equal(PortKind.DrcExempt, p.Kind));
    }

    /// <summary>自作テンプレートのモータ以外14件は、すべての接続点が赤。</summary>
    [Fact]
    public void 自作テンプレート_モータ以外14件の接続点はすべて赤()
    {
        var others = BasicPartTemplates.All().Where(p => p.Id != BasicPartTemplates.MotorId).ToList();

        Assert.Equal(14, others.Count);
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
