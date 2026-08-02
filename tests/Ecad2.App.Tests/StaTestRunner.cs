namespace Ecad2.App.Tests;

/// <summary>
/// WPF の <c>FrameworkElement</c>／<c>Window</c> は STA スレッドでしか生成できぬため、
/// テスト本体を STA スレッドで走らせる小さな道具。
/// <para>
/// <b>【この道具を使う前に問うこと・<c>samurai.md</c>】</b>「テストしにくい」は設計の匂いである。
/// STA で包むのは<b>最後の手段</b>——まず判定ロジックを純粋関数へ切り出せぬかを問い、
/// 切り出してなお残る「WPF の部品そのものの振る舞い」だけをここへ持ち込む。
/// </para>
/// <para>
/// 初出は T-144往復1周目（<c>PartEditorCanvasNotifyTests</c>）。T-132増分3で
/// <c>SheetSettingsDialogTests</c> が二人目の使い手となったため、同じ10行が散らぬよう切り出した。
/// </para>
/// </summary>
internal static class StaTestRunner
{
    /// <summary>STA スレッドで <paramref name="action"/> を走らせ、完了まで待つ。
    /// <b>例外は呼び出し元へ運び直す</b>——でなければ失敗が「テスト成功」として素通りする。</summary>
    public static void Run(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured is not null) throw captured;
    }
}
