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

    /// <summary>トグル OFF 時（デバイス名を表示）の、支援技術・UIA へ出す名。
    /// <para>
    /// 殿ご裁可2026-08-16＝語彙は「デバイス名」に揃える（「機器名」ではなく）。
    /// </para></summary>
    public const string DeviceNameAutomationName = "デバイス名";

    /// <summary>トグル ON 時（コメントを表示）の、支援技術・UIA へ出す名。
    /// <para>
    /// プロパティパネルの「コメント」へ揃えた——配置バーでは「デバイス名」と切り替わる形ゆえ、
    /// どちらのコメントかは文脈から明らかにござる。
    /// </para></summary>
    public const string CommentAutomationName = "コメント";

    /// <summary>トグル OFF 時の入力欄ラベル（見出しの体裁としてコロンを付す）。
    /// <para>
    /// <b>綴りは UIA 名の側が本体にて、ここはそれへコロンを足すのみ</b>——二つに分けて持てば、
    /// 片方だけ直した折に「画面には『コメント:』と出るが UIA には『デバイス名』」という
    /// 食い違いが生まれる。<c>const</c> の連結はコンパイル時に解けるゆえ、実体は一つにござる。
    /// </para></summary>
    public const string DeviceNameLabel = DeviceNameAutomationName + ":";

    /// <summary>トグル ON 時の入力欄ラベル。綴りの扱いは <see cref="DeviceNameLabel"/> と同じ。</summary>
    public const string CommentLabel = CommentAutomationName + ":";

    /// <summary>モードに応じた入力欄ラベル（画面表示用。コロン付き）。</summary>
    public static string LabelFor(bool isCommentMode) => isCommentMode ? CommentLabel : DeviceNameLabel;

    /// <summary>モードに応じた入力欄の UIA 名（コロンなし）。
    /// <para>
    /// <b>【なぜラベルと分けるか・隠密の指摘2026-08-16】</b>視覚のラベルだけを切り替えて UIA 名を
    /// 据え置くと、<b>コメントを入れておる最中も支援技術には「デバイス名」と伝わる</b>。
    /// 忍者が実機で UIA から引く折の取り違えの因にもなる
    /// （<c>memory: ecad2_comparison_target_identity_pitfall</c>＝比較対象の同定を先に固定せよ、の型）。
    /// </para>
    /// <para>
    /// <b>コロンを落としてあるのは既存の作法に揃えたゆえ</b>——XAML の
    /// <c>AutomationProperties.Name</c> は元より「デバイス名」（コロンなし）にて、
    /// コロンは画面上の見出しの体裁にすぎ申さぬ。
    /// </para></summary>
    public static string AutomationNameFor(bool isCommentMode)
        => isCommentMode ? CommentAutomationName : DeviceNameAutomationName;
}
