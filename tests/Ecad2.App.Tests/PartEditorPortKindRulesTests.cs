using Ecad2.App.Views;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-136(B)増分5（殿裁定2026-08-02＝案イ）: 接続点の種類を切り替える判定の境界テスト。
/// <para>
/// <b>この判定を純粋関数へ切り出した理由</b>は <see cref="PartEditorPortKindRules"/> の説明に記した。
/// 本テスト群が押さえるのは<b>述語そのもの</b>である。
/// </para>
/// <para>
/// <b>【呼び出し側は測っておらぬ】</b><c>samurai.md</c>「述語を切り出したら、呼び出し側にもテストを置く」
/// が求める2つ目——<b>キャンバス・ダイアログの側で実際にこの述語が呼ばれておるか</b>——は、
/// WPF のコントロールを立てる要があるゆえ本テスト群では測れておらぬ。
/// <b>すなわち「述語は正しいが繋ぎ込みが漏れておる」状態を、本テスト群は素通しする。</b>
/// そこは忍者の実機確認に委ねる（完了報告に明記する）。
/// </para>
/// </summary>
public class PartEditorPortKindRulesTests
{
    private static IReadOnlyList<PortDef> Ports(params PortKind[] kinds)
        => kinds.Select((k, i) => new PortDef($"P{i}", 0, i, k)).ToList();

    [Fact]
    public void 接続点が選ばれておらねば書き込まぬ()
        => Assert.False(PartEditorPortKindRules.ShouldApply(
            Ports(PortKind.Power), selectedIndex: -1, PortKind.DrcExempt));

    [Fact]
    public void 添字が範囲外なら書き込まぬ()
        => Assert.False(PartEditorPortKindRules.ShouldApply(
            Ports(PortKind.Power), selectedIndex: 1, PortKind.DrcExempt));

    [Fact]
    public void 接続点が一つも無ければ書き込まぬ()
        => Assert.False(PartEditorPortKindRules.ShouldApply(
            Ports(), selectedIndex: 0, PortKind.DrcExempt));

    /// <summary>
    /// 同値なら書き込まぬ。これが無いと、接続点を選び替えるたびに表示合わせが同値の書き込みを起こし、
    /// Undo 履歴が編集していないのに伸びる。
    /// </summary>
    [Fact]
    public void 現在の種類と同じなら書き込まぬ()
        => Assert.False(PartEditorPortKindRules.ShouldApply(
            Ports(PortKind.Power), selectedIndex: 0, PortKind.Power));

    /// <summary>同値判定は青の側でも効く（片方の値でしか試しておらぬ、を避ける）。</summary>
    [Fact]
    public void 現在の種類と同じなら書き込まぬ_青の側()
        => Assert.False(PartEditorPortKindRules.ShouldApply(
            Ports(PortKind.DrcExempt), selectedIndex: 0, PortKind.DrcExempt));

    [Fact]
    public void 種類が違えば書き込む()
        => Assert.True(PartEditorPortKindRules.ShouldApply(
            Ports(PortKind.Power), selectedIndex: 0, PortKind.DrcExempt));

    /// <summary>
    /// 判定は<b>選択中の接続点</b>を見る。先頭だけを見ておらぬことを、
    /// 先頭と選択中で種類が食い違う並びで確かめる
    /// （<c>samurai.md</c>「テスト入力の対称性・退化性チェック」＝退化した入力を避ける）。
    /// </summary>
    [Fact]
    public void 判定は先頭でなく選択中の接続点を見る()
    {
        var ports = Ports(PortKind.Power, PortKind.DrcExempt);

        // 添字1は既に青ゆえ書き込まぬ。先頭(赤)を見ておれば「違う」と誤判定する。
        Assert.False(PartEditorPortKindRules.ShouldApply(ports, selectedIndex: 1, PortKind.DrcExempt));
        // 添字1へ赤を入れるなら書き込む。
        Assert.True(PartEditorPortKindRules.ShouldApply(ports, selectedIndex: 1, PortKind.Power));
    }
}
