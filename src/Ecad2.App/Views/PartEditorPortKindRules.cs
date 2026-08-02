using Ecad2.Model;

namespace Ecad2.App.Views;

/// <summary>
/// T-136(B)増分5（殿裁定2026-08-02＝案イ）: 接続点の種類を切り替える際の判定。
/// <para>
/// <b>なぜ <see cref="PartEditorCanvas"/> の中に書かず切り出したか</b>：この判定は WPF の何にも
/// 依存せぬ純粋な条件にて、キャンバスの中に置けば「View層ゆえ単体テスト困難」となる。
/// 切り出せば境界（選択なし・範囲外・同値）を実測で押さえられる
/// （<c>samurai.md</c>「『テストしにくい』は設計の匂い——まず設計を変えて純粋関数にできぬかを問う」）。
/// </para>
/// <para>
/// <b>本クラスが測れぬもの</b>：ComboBox の <c>SelectionChanged</c> が
/// プログラムからの表示合わせで誤発火する経路は WPF の挙動そのものゆえ、ここでは扱えぬ。
/// そちらは呼び出し側の再入ガードが受け持ち、確認は実機に委ねる。
/// </para>
/// </summary>
public static class PartEditorPortKindRules
{
    /// <summary>
    /// 選択中の接続点へ種類 <paramref name="kind"/> を書き込むべきか。
    /// <list type="bullet">
    /// <item>接続点が選ばれておらぬ（<paramref name="selectedIndex"/> が負）なら false</item>
    /// <item>添字が範囲外なら false——選択の解除と一覧の更新が前後しうるゆえ、
    /// 呼び出し側の順序に頼らず、ここで弾く</item>
    /// <item><b>現在の種類と同じなら false</b>——同値の書き込みで Undo 履歴が積まれるのを防ぐ。
    /// 接続点を選び替えるたびに履歴が伸びれば、利用者の Undo が本当の編集まで戻らなくなる</item>
    /// </list>
    /// </summary>
    public static bool ShouldApply(IReadOnlyList<PortDef> ports, int selectedIndex, PortKind kind)
        => selectedIndex >= 0
        && selectedIndex < ports.Count
        && ports[selectedIndex].Kind != kind;
}
