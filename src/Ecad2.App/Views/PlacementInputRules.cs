namespace Ecad2.App.Views;

/// <summary>
/// T-153（殿ご下命2026-08-16）: 配置バーの入力欄は一つを「デバイス名」と「コメント」で共用する。
/// その切替・確定で値を取り違えぬための規則。
/// <para>
/// <b>【なぜ切り出したか】</b>この判定は WPF の何にも依存せぬ純粋な写像にて、
/// <c>MainWindow.xaml.cs</c> の中に置けば「View層ゆえ単体テスト困難」となる。切り出せば境界を
/// 実測で押さえられる（<c>samurai.md</c>「『テストしにくい』は設計の匂い——まず設計を変えて
/// 純粋関数にできぬかを問う」）。<b>動機は <see cref="PartEditorUndoRules"/>・
/// <see cref="PartEditorPortKindRules"/> と同じにござる。</b>
/// </para>
/// <para>
/// <b>【隠密が名指しした最重要の穴と、その塞ぎ方】</b>設計書2節は
/// <b>「確定時に、今どちらを表示しておるかを見て『表示中の値』を先に退避してから両方を渡せ。
/// これを忘れると、コメントを打った直後に OK を押すとコメントが落ちる」</b>と警告しておる
/// ——表示中の値がまだ変数へ移っておらぬゆえ。
/// </para>
/// <para>
/// <b>本クラスはその「退避してから」を、手順ではなく<see cref="Resolve"/> ただ一つの写像として置く。</b>
/// トグル切替も確定も同じ関数を通るゆえ、<b>「確定の時だけ退避を書き忘れる」という形が
/// 構造的に起こり得ぬ</b>——書き忘れうる手順が、そもそも存在せぬ。
/// </para>
/// </summary>
public static class PlacementInputRules
{
    /// <summary>
    /// 入力欄の表示中の値と、裏に退避してある値から、(デバイス名, コメント) の対を解く。
    /// <param name="isCommentMode">入力欄が今コメントを表示しておるか（トグルの押下状態）。</param>
    /// <param name="visibleText">入力欄が今表示しておる文字列。</param>
    /// <param name="savedText">裏へ退避してある、もう一方の文字列。</param>
    /// <para>
    /// <b>【対称であることが要】</b>コメント表示中なら表示中の値がコメント・退避中がデバイス名、
    /// デバイス名表示中ならその逆。<b>どちらのモードでも「表示中の値が捨てられぬ」</b>のが本写像の眼目にござる。
    /// </para>
    /// </summary>
    public static (string DeviceName, string Comment) Resolve(bool isCommentMode, string visibleText, string savedText)
        => isCommentMode ? (savedText, visibleText) : (visibleText, savedText);

    /// <summary>
    /// トグルを押した後の (表示すべき文字列, 退避すべき文字列) を解く。
    /// <param name="isCommentModeAfter">押した後のモード（コメント表示なら true）。</param>
    /// <param name="deviceName">現時点のデバイス名。</param>
    /// <param name="comment">現時点のコメント。</param>
    /// <para>
    /// <see cref="Resolve"/> と対を成す——あちらが「表示と退避」から「デバイス名とコメント」を解き、
    /// こちらが逆へ解く。<b>呼び手は必ず <see cref="Resolve"/> で現在の値を確定させてから本メソッドを呼ぶ</b>
    /// ——その順であれば、切替の瞬間に表示中の値が失われることはない。
    /// </para></summary>
    public static (string VisibleText, string SavedText) ForMode(bool isCommentModeAfter, string deviceName, string comment)
        => isCommentModeAfter ? (comment, deviceName) : (deviceName, comment);

    /// <summary>トグル OFF 時（デバイス名を表示）の入力欄ラベル。</summary>
    public const string DeviceNameLabel = "デバイス名:";

    /// <summary>トグル ON 時（コメントを表示）の入力欄ラベル。
    /// <para>
    /// 殿ご裁可2026-08-16＝語彙は「デバイス名」に揃える（「機器名」ではなく）。コメント側は
    /// プロパティパネルの「コメント:」へ揃えた——配置バーでは「デバイス名」と切り替わる形ゆえ、
    /// どちらのコメントかは文脈から明らかにござる。
    /// </para></summary>
    public const string CommentLabel = "コメント:";

    /// <summary>モードに応じた入力欄ラベル。</summary>
    public static string LabelFor(bool isCommentMode) => isCommentMode ? CommentLabel : DeviceNameLabel;
}
