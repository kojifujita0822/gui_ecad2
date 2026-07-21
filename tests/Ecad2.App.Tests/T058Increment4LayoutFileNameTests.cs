namespace Ecad2.App.Tests;

/// <summary>
/// T-058増分4(レイアウト永続化)・T-110増分1(単一DockingManagerへの統合、家老采配2026-07-22 B-1)の
/// 回帰テスト。統合前はDockingManagerのx:Name(4種)からファイル名を導出するswitch式だったが、
/// 単一Manager化により導出ロジック自体が不要になり単一の定数へ縮退した。旧4ファイル
/// (left-palette.xml等)は新ファイル名からは参照されず放置される(裁3、殿裁可済み)。
/// </summary>
public class T058Increment4LayoutFileNameTests
{
    [Fact]
    public void DockingLayoutFileName_単一のファイル名を返す()
    {
        Assert.Equal("main-layout.xml", MainWindow.DockingLayoutFileName);
    }
}
