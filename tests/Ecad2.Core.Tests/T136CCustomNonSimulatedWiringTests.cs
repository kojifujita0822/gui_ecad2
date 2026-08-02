using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-136(C) 判断材料の実測——<b>自作パーツの非シミュレート部品にも同じ穴が空いておるか</b>。
/// <para>
/// <b>【本テスト群は「まだ塞がっておらぬ穴」を記録するものである】</b>
/// 殿裁定2026-08-02「モーターだけ除外すればいい」により、<c>2d272e8</c> が塞いだのは
/// <see cref="ElementKind.Motor"/> ただ一つ。自作パーツは <c>PartResolver.ParticipatesInWiring</c> が
/// 無条件に true を返すゆえ、従来どおり結線に参加する（＝非破壊）。
/// <b>本テスト群はその現状を写し取ったものであり、穴が開いておることを期待値としておる。</b>
/// 殿が対象を「非シミュレート全般」へ広げると裁定なされたら、
/// <b>本ファイルの期待値は反転させる</b>（<c>proposed.md</c> P-161）。
/// </para>
/// <para>
/// <b>部品の定義は現物に合わせてある</b>——<c>sample/big_sample.gcad</c> のライブラリに実在する
/// 「ソレノイド」（<c>Role=NonSimulated</c>・1セル幅・2端子＝境界0と1）を写した。
/// </para>
/// <para>
/// <b>【殿裁定2026-08-02＝この型は実運用に無い。P-161は closed】</b>
/// 上記ソレノイドは<b>殿の自作図形にて、実装が固まる前に作られた設定の名残</b>であり、
/// <b>本来はコイル属性のもの</b>と殿が仰せになった（「無視してよい。今後新規に作成するものは
/// 『コイル』として設定する」）。<b>他の <c>NonSimulated</c> かつ接続点を持つ部品は、いずれも
/// T-068/T-126の検証残置物</b>——<b>すなわち実運用の実例はゼロである。</b>
/// <b>ゆえに対象を非シミュレート全般へ広げる話は立ち消え、実装は Motor のみで確定した。</b>
/// </para>
/// <para>
/// <b>それでも本ファイルを残す理由</b>：<b>穴そのものは実測で確かめられた構造上の性質であり、
/// 「実例が無い」ことと「機序が無い」ことは別である。</b> 将来ふたたび <c>NonSimulated</c> かつ
/// 接続点を持つ部品が現れたとき、本ファイルが<b>その時点で何が起きるかを示す</b>。
/// 誰かが正しく塞げば6件が RED になり、冒頭の指示（期待値を反転させよ）へ辿り着ける。
/// </para>
/// <para>
/// <b>機序はモータと同一の見込み</b>：左右ポートを繋ぐ union は通過接続要素の枝ただ一つで、
/// これは <c>CreatesComponent</c> を見る。非シミュレートは通らぬゆえ左右ポートが結ばれず、
/// 行の最左（最右）に居ると母線 union を横取りして同じ行の負荷を切り離す。
/// <b>本テスト群はその見込みを実測で確かめるためのものである。</b>
/// </para>
/// <para>
/// <b>射程</b>：測っておるのは下記2配置のみ。縦コネクタ・配線分断を伴う配置は測っておらぬ。
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

    // ---- 穴の実在（ソレノイド在。現時点では塞がっておらぬ） ----

    /// <summary>
    /// <b>穴の実在（左）。</b> ソレノイドが行の最左に居ると、同じ行の負荷が左母線から切り離され、
    /// <c>DRC-LOAD-001</c> が誤って鳴る。モータで測ったのと同じ症状である。
    /// </summary>
    [Fact]
    public void T136C_S3_SolenoidLeftmostInRow_SeversLoadFromLeftRail_HoleStillOpen()
    {
        var sheet = MakeSheet(withSolenoid: true);
        var net = NetlistBuilder.Build(sheet, MakeLibrary());

        Assert.NotEqual(net.LeftRailNet, LoadM1(net).NetA);

        var d = Assert.Single(DesignRuleCheck.CheckLoadReachability(sheet, net));
        Assert.Equal(DesignRuleCheck.LoadNotReachableFromLeft, d.Code);
        Assert.Equal("M1", d.DeviceName);
    }

    /// <summary>
    /// <b>穴の実在（右）。</b> 右端にソレノイドを置くと <c>DRC-LOAD-002</c> が誤って鳴る。
    /// 右母線側は左とは別の経路が働くゆえ別に測る。
    /// </summary>
    [Fact]
    public void T136C_S4_SolenoidAtRightEdge_SeversLoadFromRightRail_HoleStillOpen()
    {
        var sheet = MakeSheetRightEdge(withSolenoid: true);
        var net = NetlistBuilder.Build(sheet, MakeLibrary());

        Assert.NotEqual(net.RightRailNet, LoadM1(net).NetB);

        var d = Assert.Single(DesignRuleCheck.CheckLoadReachability(sheet, net));
        Assert.Equal(DesignRuleCheck.LoadNotReachableFromRight, d.Code);
        Assert.Equal("M1", d.DeviceName);
    }

    // ---- 機序がモータと同一であることの裏づけ ----

    /// <summary>
    /// ソレノイドは <c>Role=NonSimulated</c> ゆえ Component にならぬ——モータと同じ立場である。
    /// にもかかわらず結線には参加しておることを、上の S3・S4 と併せて示す。
    /// </summary>
    [Fact]
    public void T136C_S5_Solenoid_DoesNotBecomeComponent()
    {
        var net = NetlistBuilder.Build(MakeSheet(withSolenoid: true), MakeLibrary());

        Assert.Single(net.Components);
        Assert.Equal("M1", net.Components[0].DeviceName);
    }

    /// <summary>
    /// ソレノイドはネットを増やす——すなわち結線に加わっておる。
    /// モータ側の同名テスト（<c>T136C_D2_Motor_ContributesNoNets</c>）が「増やさぬ」ことを
    /// 測っておるのと、ちょうど裏返しの関係にある。
    /// </summary>
    [Fact]
    public void T136C_S6_Solenoid_ContributesNets_UnlikeMotor_HoleStillOpen()
    {
        var withSolenoid = NetlistBuilder.Build(MakeSheet(withSolenoid: true), MakeLibrary());
        var control = NetlistBuilder.Build(MakeSheet(withSolenoid: false), MakeLibrary());

        Assert.True(withSolenoid.Nets.Count > control.Nets.Count);
    }
}
