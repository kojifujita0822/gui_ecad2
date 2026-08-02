using Ecad2.App.Views;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-144（殿ご裁可2026-08-02）: パーツエディタの入力欄（幅・高さ・役割・シート種別）の変更を
/// Undo 履歴へ積むべきかの判定テスト。
/// <para>
/// <b>この判定を純粋関数へ切り出した理由</b>は <see cref="PartEditorUndoRules"/> の説明に記した。
/// 本テスト群が押さえるのは<b>述語そのもの</b>である。
/// </para>
/// <para>
/// <b>【呼び出し側は測っておらぬ】</b><c>samurai.md</c>「述語を切り出したら、呼び出し側にもテストを置く」
/// が求める2つ目——<b>ダイアログの側で実際にこの述語が呼ばれておるか、再入ガードが効いておるか</b>——は、
/// <c>PartEditorDialog</c> が <c>Window</c> 派生ゆえ本テスト群では測れておらぬ。
/// <b>すなわち「述語は正しいが繋ぎ込みが漏れておる」状態を、本テスト群は素通しする。</b>
/// そこは忍者の実機確認に委ねる（完了報告に明記する）。
/// </para>
/// </summary>
public class PartEditorUndoRulesTests
{
    /// <summary>基準となる状態。各テストはここから1項目だけ変えて対称性を見る。
    /// <b>幅と高さに違う値（3と5）を入れてあるのは、両者を取り違える実装を検出するため</b>
    /// （同じ値だと入れ替えても等価になり、誤りが素通りする）。</summary>
    private static PartEditorExternalState Base()
        => new(WidthCells: 3, HeightCells: 5, PartRole.ContactNO, SheetAffinity.Any);

    [Fact]
    public void 何も変わっておらねば積まぬ()
        => Assert.False(PartEditorUndoRules.ShouldRecord(Base(), Base()));

    [Fact]
    public void 幅が変われば積む()
        => Assert.True(PartEditorUndoRules.ShouldRecord(Base(), Base() with { WidthCells = 4 }));

    [Fact]
    public void 高さが変われば積む()
        => Assert.True(PartEditorUndoRules.ShouldRecord(Base(), Base() with { HeightCells = 6 }));

    [Fact]
    public void 役割が変われば積む()
        => Assert.True(PartEditorUndoRules.ShouldRecord(Base(), Base() with { Role = PartRole.Coil }));

    [Fact]
    public void シート種別が変われば積む()
        => Assert.True(PartEditorUndoRules.ShouldRecord(
            Base(), Base() with { SheetAffinity = SheetAffinity.MainCircuitOnly }));

    /// <summary>
    /// 幅と高さを入れ替えた状態は「変わった」と判ずる。
    /// <b>両者を同じ値にしたフィクスチャでは、この誤りが素通りする</b>——
    /// <c>PR-27</c>（テスト入力の対称性・退化性）が説く、対称な入力を避ける理由そのものである。
    /// </summary>
    [Fact]
    public void 幅と高さを入れ替えれば積む()
        => Assert.True(PartEditorUndoRules.ShouldRecord(
            Base(), Base() with { WidthCells = 5, HeightCells = 3 }));

    /// <summary>
    /// 2項目が同時に変わっても積む（片方だけを見る実装への網）。
    /// </summary>
    [Fact]
    public void 複数の項目が同時に変われば積む()
        => Assert.True(PartEditorUndoRules.ShouldRecord(
            Base(), Base() with { WidthCells = 4, Role = PartRole.Coil }));

    /// <summary>
    /// <b>戻した場合も「変わった」と判ずる</b>——履歴を積むか否かは直前の状態との差だけで決まり、
    /// 「元の値に戻ったから積まぬ」といった経路依存の判断は持たない。
    /// この一件を固定しておくと、将来「元へ戻ったなら履歴を消す」型の最適化を入れた者が
    /// ここで気づける。
    /// </summary>
    [Fact]
    public void 元の値へ戻す変更も積む()
        => Assert.True(PartEditorUndoRules.ShouldRecord(
            Base() with { WidthCells = 4 }, Base()));
}
