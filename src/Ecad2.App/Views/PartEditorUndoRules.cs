namespace Ecad2.App.Views;

/// <summary>
/// T-144（殿ご裁可2026-08-02＝「undo対象に含めて」）: パーツエディタの入力欄（幅・高さ・役割・
/// シート種別）の変更を Undo 履歴へ積むべきかの判定。
/// <para>
/// <b>本タスクは原本からの意図的な逸脱である。</b> 原本 GuiEcad の <c>PartEditorWindow.xaml.cs:795</c> の
/// コメントから、<b>サイズ・役割がスナップショットに入るのは <c>LoadTemplate</c> がそれらを変えるからであって、
/// 入力欄の直接変更を Undo 対象にする意図は原本に無い</b>ことが確認されている（侍の原本調査2026-07-31）。
/// そのうえで「配置先を変えたのに Undo で戻せぬのは使い勝手として違和感がある」という所見を添えて諮り、
/// 殿が「undo対象に含めて」とご裁定なされた。
/// <b>後の者が「原本ではどうか」を根拠に本実装を差し戻さぬよう、ここに経緯を残す。</b>
/// </para>
/// <para>
/// <b>なぜ <see cref="PartEditorDialog"/> の中に書かず切り出したか</b>：この判定は WPF の何にも依存せぬ
/// 純粋な条件にて、ダイアログの中に置けば「View層ゆえ単体テスト困難」となる。切り出せば境界を実測で
/// 押さえられる（<c>samurai.md</c>「『テストしにくい』は設計の匂い——まず設計を変えて純粋関数にできぬかを問う」）。
/// <b>切り出しの動機は T-136(B)増分5 の <see cref="PartEditorPortKindRules"/> と同じである。</b>
/// <b>ただし「二層で守る」形まで同じと読んではならぬ</b>——増分5の第二の守りは WPF の ComboBox 標準挙動
/// （値が変わらねば <c>SelectionChanged</c> が発火せぬ）に支えられるのに対し、T-144 のそれは
/// ダイアログ側の独自のステート管理に支えられる。詳細は <c>PartEditorDialog._lastRecordedExternal</c> の説明を参照。
/// </para>
/// <para>
/// <b>本クラスが測れぬもの</b>：<c>RestoreExternalState</c> が入力欄を書き換えることで
/// <c>TextChanged</c>／<c>SelectionChanged</c> が誤発火する経路は WPF の挙動そのものゆえ、ここでは扱えぬ。
/// そちらは呼び出し側の再入ガードが受け持ち、確認は実機に委ねる（増分5と同じ切り分け）。
/// </para>
/// </summary>
public static class PartEditorUndoRules
{
    /// <summary>
    /// 入力欄の状態が <paramref name="before"/> から <paramref name="after"/> へ変わったとき、
    /// Undo 履歴へ積むべきか。<b>4項目のいずれかが実際に変わった場合のみ true。</b>
    /// <para>
    /// <b>同値なら積まぬ理由</b>：欄にフォーカスを入れて何も変えずに離れる、あるいは同じ値を
    /// 選び直すたびに履歴が伸びれば、利用者の Undo が本当の編集まで戻らなくなる
    /// （<see cref="PartEditorPortKindRules.ShouldApply"/> の同値判定と同じ筋）。
    /// </para>
    /// <para>
    /// <b>レコードの値等価性に頼らず4項目を明示的に比べてはいない</b>——<see cref="PartEditorExternalState"/> は
    /// <c>record</c> ゆえ <c>!=</c> が値比較になる。項目が増えた際に比較の書き漏らしが起きぬよう、
    /// 意図してレコードの等価性へ委ねる。
    /// </para>
    /// </summary>
    public static bool ShouldRecord(PartEditorExternalState before, PartEditorExternalState after)
        => before != after;
}
