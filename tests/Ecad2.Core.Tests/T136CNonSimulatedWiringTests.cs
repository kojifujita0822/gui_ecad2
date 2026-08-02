using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-136(C) 三相モータを結線から外す（殿裁定2026-08-02「モーターだけ除外すればいい」）の回帰テスト。
/// <para>
/// <b>直す前に何が起きておったか（工程1の実測）</b>：モータは <c>CreatesComponent=false</c> ゆえ
/// Component にはならぬが、それが効くのは Component 生成の段だけで、ノード生成・母線 union・
/// 横配線結合には参加しておった。モータの左ポート U（境界0）と右ポート W（境界2）を結ぶ union は
/// <c>NetlistBuilder</c> の通過接続要素の枝ただ一つで、これは <c>CreatesComponent</c> を見る——
/// ゆえにモータの U と W は決して結ばれぬ。結果、モータが行の最左に居ると母線 union を U が取り、
/// 右隣へ渡すのは W ——<b>同じ行の負荷が母線から切り離され、DRC が誤って鳴っておった。</b>
/// </para>
/// <para>
/// <b>隠密の調査書（docs/ecad2-t136-drc-vs-portkind-investigation-onmitsu.md §2.2-2.3）の推論とは
/// 向きが逆であった</b>——同書は「モータが負荷を母線へ繋ぎ、DRC を黙らせる」と読んでおったが、
/// 実測では繋がず、逆に断っておった（同書自身が「実測しており申さぬ」と明記しておる）。
/// </para>
/// <para>
/// <b>本テスト群の要</b>：モータを置いても置かなくても、同じ行の負荷の結線は変わらぬこと。
/// 各観点に「モータ不在」の対照を対で置いてある
/// （<c>memory: feedback_control_experiment_needs_naive_baseline</c>）。
/// </para>
/// <para>
/// <b>射程</b>：測っておるのは下記2配置（モータが行の最左／右端）のみである。縦コネクタ・配線分断を
/// 伴う配置は測っておらぬ。また自作パーツで <c>PartRole.NonSimulated</c> かつ接続点を持つものは
/// 本裁定の対象外ゆえ測っておらぬ（<c>proposed.md</c> P-161）。
/// </para>
/// </summary>
public class T136CNonSimulatedWiringTests
{
    private static ElementInstance MakeMotor(int row, int column)
        => new() { Kind = ElementKind.Motor, Pos = new GridPos(row, column), CellWidth = 3 };

    private static ElementInstance MakeCoil(int row, int column, string deviceName)
        => new() { Kind = ElementKind.Coil, Pos = new GridPos(row, column), DeviceName = deviceName };

    /// <summary>モータが行の最左に来る配置。モータ在／不在で負荷 M1 の位置は同一。</summary>
    private static Sheet MakeSheet(bool withMotor)
    {
        var sheet = new Sheet();
        if (withMotor) sheet.Elements.Add(MakeMotor(0, 0));   // U=境界0／V=境界1／W=境界2
        sheet.Elements.Add(MakeCoil(0, 3, "M1"));             // L=境界3／R=境界4
        return sheet;
    }

    /// <summary>モータが右端に来る配置（W=境界40=Columns）。左とは働く経路が別ゆえ分けてある。</summary>
    private static Sheet MakeSheetRightEdge(bool withMotor)
    {
        var sheet = new Sheet();
        sheet.Elements.Add(MakeCoil(0, 35, "M1"));
        if (withMotor) sheet.Elements.Add(MakeMotor(0, 38));
        return sheet;
    }

    private static Component LoadM1(Netlist net) => net.Components.Single(c => c.DeviceName == "M1");

    // ---- 観点A: 負荷の左右ネット ----

    /// <summary>
    /// 観点A・対照（素朴なベースライン）。モータを置かねば負荷は左右いずれの母線へも届く。
    /// これが成り立たねば、下の A2 の結果を「モータが無害になったゆえ」と帰属できぬ。
    /// </summary>
    [Fact]
    public void T136C_A1_Control_LoadAloneInRow_ReachesBothRails()
    {
        var net = NetlistBuilder.Build(MakeSheet(withMotor: false));
        var m1 = LoadM1(net);

        Assert.Equal(net.LeftRailNet, m1.NetA);
        Assert.Equal(net.RightRailNet, m1.NetB);
    }

    /// <summary>
    /// 観点A。モータが行の最左に居ても、負荷は左母線から切り離されぬ。
    /// 直す前はここが <b>別ネット</b>になっており、これが不具合の核であった。
    /// </summary>
    [Fact]
    public void T136C_A2_MotorLeftmostInRow_DoesNotSeverLoadFromLeftRail()
    {
        var net = NetlistBuilder.Build(MakeSheet(withMotor: true));
        var m1 = LoadM1(net);

        Assert.Equal(net.LeftRailNet, m1.NetA);
        Assert.Equal(net.RightRailNet, m1.NetB);
    }

    // ---- 観点B: DRC の出力 ----

    /// <summary>観点B・対照。モータ不在では負荷到達性チェックは何も言わぬ。</summary>
    [Fact]
    public void T136C_B1_Control_LoadAloneInRow_DrcReportsNothing()
    {
        var sheet = MakeSheet(withMotor: false);
        var net = NetlistBuilder.Build(sheet);

        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    /// <summary>
    /// 観点B。モータを置いても DRC は黙ったまま。
    /// 直す前はここで <c>DRC-LOAD-001</c>（左母線から到達不可）が誤って1件出ておった。
    /// </summary>
    [Fact]
    public void T136C_B2_MotorLeftmostInRow_DrcStaysSilent()
    {
        var sheet = MakeSheet(withMotor: true);
        var net = NetlistBuilder.Build(sheet);

        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    /// <summary>観点B鏡像・対照。右端寄りに負荷を単独で置いた場合も DRC は黙る。</summary>
    [Fact]
    public void T136C_B3_Control_LoadAloneNearRightEdge_DrcReportsNothing()
    {
        var sheet = MakeSheetRightEdge(withMotor: false);
        var net = NetlistBuilder.Build(sheet);

        Assert.Equal(net.RightRailNet, LoadM1(net).NetB);
        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    /// <summary>
    /// 観点B鏡像。右端にモータを置いても DRC は黙ったまま。
    /// 右母線側は左とは別の経路（境界==Columns の直接 union と末尾要素の右母線自動接続）が
    /// 働くゆえ別に測る。直す前はここで <c>DRC-LOAD-002</c> が誤って1件出ておった。
    /// </summary>
    [Fact]
    public void T136C_B4_MotorAtRightEdge_DrcStaysSilent()
    {
        var sheet = MakeSheetRightEdge(withMotor: true);
        var net = NetlistBuilder.Build(sheet);

        Assert.Equal(net.RightRailNet, LoadM1(net).NetB);
        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    // ---- 観点D: モータが結線そのものから外れておるか ----

    /// <summary>
    /// モータは Component にならぬ（本タスク以前からの挙動。工程1で再現済み）。
    /// 上の観点Bで「黙る」のが、モータが検査対象から外れておるゆえでないことを示す
    /// ——モータは元より検査対象ではなく、鳴っておったのは負荷 M1 についての診断であった。
    /// </summary>
    [Fact]
    public void T136C_D1_Motor_DoesNotBecomeComponent()
    {
        var net = NetlistBuilder.Build(MakeSheet(withMotor: true));

        Assert.Single(net.Components);
        Assert.Equal("M1", net.Components[0].DeviceName);
    }

    /// <summary>
    /// モータがノードを一つも作らぬこと。観点A・Bは「結果として無害か」を測るが、
    /// 本テストは「そもそも結線に加わっておらぬか」を直に測る
    /// ——母線 union だけを塞ぐような部分的な直し方では、ノードが残るゆえここで露見する。
    /// </summary>
    [Fact]
    public void T136C_D2_Motor_ContributesNoNets()
    {
        var withMotor = NetlistBuilder.Build(MakeSheet(withMotor: true));
        var control = NetlistBuilder.Build(MakeSheet(withMotor: false));

        Assert.Equal(control.Nets.Count, withMotor.Nets.Count);
    }
}
