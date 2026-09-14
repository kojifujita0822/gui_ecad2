using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.Core.Tests;

/// <summary>
/// 外部接点A/Bの組込み昇格（殿ご依頼2026-09-14）の回帰テスト。
/// <para>
/// <b>背景</b>：使用率の高い自作パーツ「外部接点A」（旧 <c>Role=InputNO</c>）・「外部接点B」
/// （<c>Role=ContactNC</c>）を組込み図形へ格上げする。当初の「外部接点A」は<c>InputNO</c>で
/// 作られていたが、殿ご指摘＝「外部接点はinput系ではない」により<c>ContactNO</c>へ正した。
/// <c>InputNO/NC</c>は押釦と同じ<c>ElementKind</c>へ写像されテストモードでモーメンタリ
/// （押している間だけON）動作になるが、外部接点が求める「クリックでON/OFFをトグル保持」する挙動は、
/// <c>ContactNO/NC</c>が唯一持つ既存経路（<c>MainWindowViewModel.IsRealContactElement</c>→
/// <c>TestSession.ToggleInput</c>）でのみ実現される。
/// </para>
/// <para>
/// <b>員数の網</b>は<see cref="BuiltinPartIdsConsistencyTests"/>・
/// <see cref="T136Increment5PortKindAssignmentTests"/>が既に押さえておるゆえ、本クラスは
/// 「役割・直交フラグが意図どおりであること」に絞る。
/// </para>
/// </summary>
public class ExternalContactPartsTests
{
    private static PartDefinition ExternalContactA()
        => BasicPartTemplates.All().Single(p => p.Id == BasicPartTemplates.ExternalContactNOId);

    private static PartDefinition ExternalContactB()
        => BasicPartTemplates.All().Single(p => p.Id == BasicPartTemplates.ExternalContactNCId);

    [Fact]
    public void 外部接点Aの役割はContactNOである()
        => Assert.Equal(PartRole.ContactNO, ExternalContactA().Role);

    [Fact]
    public void 外部接点Bの役割はContactNCである()
        => Assert.Equal(PartRole.ContactNC, ExternalContactB().Role);

    /// <summary>ContactNO/NCとして解決されることが、テストモードのクリックトグル
    /// （<c>MainWindowViewModel.IsRealContactElement</c>判定）が効くための前提条件。</summary>
    [Theory]
    [InlineData("ExternalContactA", ElementKind.ContactNO)]
    [InlineData("ExternalContactB", ElementKind.ContactNC)]
    public void ComponentKindは通常接点へ解決される(string which, ElementKind expected)
    {
        var part = which == "ExternalContactA" ? ExternalContactA() : ExternalContactB();
        var lib = new PartLibrary();
        lib.ById[part.Id] = part;
        var elem = new ElementInstance { PartId = part.Id, DeviceName = "TEST" };

        Assert.Equal(expected, PartResolver.ComponentKind(elem, lib));
    }

    /// <summary>外部接点は現場の物理入力信号であり、対応する駆動コイルを図面上に持たぬのが正常。
    /// これがtrueでないと、素のContactNO/NC同様にDRC-XREF-001（コイルなし警告）が誤って出る
    /// （殿ご指摘2026-09-14、T-152で導入された直交フラグの適用）。</summary>
    [Fact]
    public void 外部接点A_Bはクロスリファレンス検査から除外される()
    {
        Assert.True(ExternalContactA().IsExcludedFromCrossReference);
        Assert.True(ExternalContactB().IsExcludedFromCrossReference);
    }

    /// <summary>OR a接点/OR b接点（Shift+F5/F6・部品選択リストのOR論理エントリ）の対象には含めぬ
    /// （殿からその要求は無い。ThermalRelayNO/NCと同じ理由、表示件数を無用に増やさぬため）。</summary>
    [Fact]
    public void 外部接点A_BはOR入力対象ではない()
    {
        Assert.False(ExternalContactA().IsOrEligible);
        Assert.False(ExternalContactB().IsOrEligible);
    }
}
