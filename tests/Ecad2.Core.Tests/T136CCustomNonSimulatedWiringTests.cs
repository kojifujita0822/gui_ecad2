using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-142 自作パーツの非シミュレート部品が結線を断つ穴を塞いだことの回帰テスト
/// （殿ご下命2026-08-02「穴を発見したのに放置はしておけぬ」）。
/// <para>
/// <b>塞ぐ前は何が起きておったか</b>：<c>Role=NonSimulated</c> で接続点を持つ部品は、左右ポートを
/// 繋ぐ union が <c>CreatesComponent</c> を見るゆえ通らず、左右が結ばれぬ。にもかかわらず母線への
/// 直接 union と横配線の結合には参加するため、<b>行の最左（最右）に居ると母線接続を横取りし、
/// 同じ行の負荷を切り離しておった</b>——三相モータと寸分違わぬ「壁」である。
/// 症状は <c>DRC-LOAD-001/002</c> が誤って鳴ること（T-136(C)で実測。<c>bb20aee</c>）。
/// </para>
/// <para>
/// <b>塞いだ形</b>：<c>PartResolver.ParticipatesInWiring</c> の自作パーツ分岐を
/// <c>part.Role != PartRole.NonSimulated</c> へ改めた一行。組込み側（<see cref="ElementCatalog"/>）は
/// 触れておらぬ——組込みは殿裁定により <see cref="ElementKind.Motor"/> のみが対象で、
/// そちらは T-136(C)（<c>2d272e8</c>）で既に塞がっておる。
/// </para>
/// <para>
/// <b>本テスト群の要</b>：非シミュレート部品を置いても置かなくても、同じ行の負荷の結線は変わらぬこと。
/// 各観点に「部品不在」の対照を対で置いてある
/// （<c>memory: feedback_control_experiment_needs_naive_baseline</c>）。
/// </para>
/// <para>
/// <b>部品の定義は現物に合わせてある</b>——<c>sample/big_sample.gcad</c> のライブラリに在った
/// 「ソレノイド」（<c>Role=NonSimulated</c>・1セル幅・2端子＝境界0と1）を写した。
/// なお<b>そのソレノイド自体は殿の自作図形にて、実装が固まる前に作られた設定の名残</b>であり、
/// 殿は「今後新規に作成するものは『コイル』として設定する」と仰せである（既存データの手当ては殿の領分）。
/// <b>本テスト群が守っておるのは、その1件のデータではなく機序の側である</b>——
/// 「実例が無い」ことと「機序が無い」ことは別ゆえ。
/// <b>【後から判明・実例は在った】</b>同梱テンプレートの「モータ」
/// （<c>BasicPartTemplates</c>）も <c>Role=NonSimulated</c> かつ3端子にて本件に該当する。
/// 当初「実運用の実例はゼロ」と見積もられておったのは、実態確認が <c>sample/</c> 配下の
/// 検索であり、コードで定義され初回起動時に展開されるテンプレートが網に掛からなんだゆえ。
/// <b>すなわち本テスト群は、実例の無い機序を先回りして守るものではなく、
/// 同梱物に実在した穴を塞いだことの回帰テストである。</b>
/// </para>
/// <para>
/// <b>射程</b>：測っておるのは下記2配置（部品が行の最左／右端）のみである。縦コネクタ・配線分断を
/// 伴う配置は測っておらぬ。
/// </para>
/// </summary>
public class T136CCustomNonSimulatedWiringTests
{
    private const string SolenoidId = "solenoid-nonsimulated";

    /// <summary>sample/big_sample.gcad のソレノイドと同じ形（非シミュレート・1セル幅・境界0と1）。</summary>
    private static PartLibrary MakeLibrary()
    {
        var lib = new PartLibrary();
        lib.ById[SolenoidId] = new PartDefinition
        {
            Id = SolenoidId,
            Name = "ソレノイド",
            Role = PartRole.NonSimulated,
            WidthCells = 1,
            HeightCells = 1,
            Ports = { new PortDef("P2", 0, 0), new PortDef("P1", 0, 1) },
        };
        return lib;
    }

    private static ElementInstance MakeSolenoid(int row, int column)
        => new() { PartId = SolenoidId, Pos = new GridPos(row, column) };

    private static ElementInstance MakeCoil(int row, int column, string deviceName)
        => new() { Kind = ElementKind.Coil, Pos = new GridPos(row, column), DeviceName = deviceName };

    /// <summary>ソレノイドが行の最左に来る配置。在／不在で負荷 M1 の位置は同一。</summary>
    private static Sheet MakeSheet(bool withSolenoid)
    {
        var sheet = new Sheet();
        if (withSolenoid) sheet.Elements.Add(MakeSolenoid(0, 0));   // 境界0／境界1
        sheet.Elements.Add(MakeCoil(0, 3, "M1"));                   // L=境界3／R=境界4
        return sheet;
    }

    /// <summary>ソレノイドが右端に来る配置（右ポート=境界40=Columns）。</summary>
    private static Sheet MakeSheetRightEdge(bool withSolenoid)
    {
        var sheet = new Sheet();
        sheet.Elements.Add(MakeCoil(0, 35, "M1"));
        if (withSolenoid) sheet.Elements.Add(MakeSolenoid(0, 39));
        return sheet;
    }

    private static Component LoadM1(Netlist net) => net.Components.Single(c => c.DeviceName == "M1");

    // ---- フィクスチャそのものの健全性 ----

    /// <summary>
    /// <b>フィクスチャが接続点を持っておること自体を測る。</b>
    /// 下の S3・S4・S6 はいずれも「非シミュレート部品が結線に加わらぬ」ことを確かめるが、
    /// <b>接続点を1つも持たぬ部品もまた結線に加わらぬ</b>——ゆえに <see cref="MakeLibrary"/> の
    /// <c>Ports</c> が空になると、3件とも<b>塞いだからではなく接続点が無いゆえ</b>に通ってしまう。
    /// 共有フィクスチャが感度を左右する形ゆえ、ここで直に押さえる
    /// （<c>samurai.md</c>「RED証明を済ませたら、このテストの感度を失わせる編集は他に無いかを一度問う」）。
    /// </summary>
    [Fact]
    public void T136C_S0_Fixture_SolenoidHasTwoPortsAtBoundaryZeroAndOne()
    {
        var part = MakeLibrary().Get(SolenoidId);

        Assert.NotNull(part);
        Assert.Equal(PartRole.NonSimulated, part.Role);
        Assert.Equal([0, 1], part.Ports.Select(p => p.BoundaryOffset).Order());
    }

    // ---- 対照（ソレノイド不在） ----

    /// <summary>対照。ソレノイドを置かねば負荷は左右いずれの母線へも届く。</summary>
    [Fact]
    public void T136C_S1_Control_LoadAloneInRow_ReachesBothRails()
    {
        var sheet = MakeSheet(withSolenoid: false);
        var net = NetlistBuilder.Build(sheet, MakeLibrary());
        var m1 = LoadM1(net);

        Assert.Equal(net.LeftRailNet, m1.NetA);
        Assert.Equal(net.RightRailNet, m1.NetB);
        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    /// <summary>対照（右端）。ソレノイド不在では右端寄りの負荷も右母線へ届く。</summary>
    [Fact]
    public void T136C_S2_Control_LoadAloneNearRightEdge_ReachesRightRail()
    {
        var sheet = MakeSheetRightEdge(withSolenoid: false);
        var net = NetlistBuilder.Build(sheet, MakeLibrary());

        Assert.Equal(net.RightRailNet, LoadM1(net).NetB);
        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    // ---- 穴が塞がっておること ----

    /// <summary>
    /// ソレノイドが行の最左に居ても、同じ行の負荷は左母線から切り離されぬ。
    /// 塞ぐ前はここが別ネットとなり、<c>DRC-LOAD-001</c> が誤って鳴っておった。
    /// </summary>
    [Fact]
    public void T136C_S3_SolenoidLeftmostInRow_DoesNotSeverLoadFromLeftRail()
    {
        var sheet = MakeSheet(withSolenoid: true);
        var net = NetlistBuilder.Build(sheet, MakeLibrary());
        var m1 = LoadM1(net);

        Assert.Equal(net.LeftRailNet, m1.NetA);
        Assert.Equal(net.RightRailNet, m1.NetB);
        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    /// <summary>
    /// 右端にソレノイドを置いても負荷は右母線から切り離されぬ。
    /// 右母線側は左とは別の経路が働くゆえ別に測る。塞ぐ前は <c>DRC-LOAD-002</c> が誤って鳴っておった。
    /// </summary>
    [Fact]
    public void T136C_S4_SolenoidAtRightEdge_DoesNotSeverLoadFromRightRail()
    {
        var sheet = MakeSheetRightEdge(withSolenoid: true);
        var net = NetlistBuilder.Build(sheet, MakeLibrary());

        Assert.Equal(net.RightRailNet, LoadM1(net).NetB);
        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    // ---- 機序の側 ----

    /// <summary>
    /// ソレノイドは <c>Role=NonSimulated</c> ゆえ Component にならぬ。
    /// これは塞ぐ前後で変わらぬ挙動であり、対照として据え置く。
    /// </summary>
    [Fact]
    public void T136C_S5_Solenoid_DoesNotBecomeComponent()
    {
        var net = NetlistBuilder.Build(MakeSheet(withSolenoid: true), MakeLibrary());

        Assert.Single(net.Components);
        Assert.Equal("M1", net.Components[0].DeviceName);
    }

    /// <summary>
    /// ソレノイドはネットを一つも増やさぬ——すなわち結線に加わっておらぬ。
    /// 上の S3・S4 が「結果として無害か」を測るのに対し、本テストは「そもそも加わっておらぬか」を
    /// 直に測る。モータ側の <c>T136C_D2_Motor_ContributesNoNets</c> と同じ形である。
    /// </summary>
    [Fact]
    public void T136C_S6_Solenoid_ContributesNoNets_LikeMotor()
    {
        var withSolenoid = NetlistBuilder.Build(MakeSheet(withSolenoid: true), MakeLibrary());
        var control = NetlistBuilder.Build(MakeSheet(withSolenoid: false), MakeLibrary());

        Assert.Equal(control.Nets.Count, withSolenoid.Nets.Count);
    }
}
