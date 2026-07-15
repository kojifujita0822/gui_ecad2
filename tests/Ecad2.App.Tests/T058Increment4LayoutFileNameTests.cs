namespace Ecad2.App.Tests;

/// <summary>
/// T-058増分4(レイアウト永続化、殿裁定=保存タイミング両方・保存先アプリ共通設定)の回帰テスト。
/// ファイルI/O・View層(DockingManager実インスタンス)依存の大半は単体テスト対象外(家老裁可、
/// T-058増分3の同型判断に倣う)だが、保存先パス生成ロジックはinternal static抽出済みの純粋関数
/// (MainWindow.GetDockingLayoutFileName)のため通常どおり検証する。
/// </summary>
public class T058Increment4LayoutFileNameTests
{
    [Theory]
    [InlineData("LeftPaletteDockingManager", "left-palette.xml")]
    [InlineData("OutputPanelDockingManager", "output-panel.xml")]
    [InlineData("RightPanelDockingManager", "right-panel.xml")]
    public void GetDockingLayoutFileName_既知のDockingManager名は対応するファイル名を返す(
        string managerName, string expectedFileName)
    {
        string actual = MainWindow.GetDockingLayoutFileName(managerName);

        Assert.Equal(expectedFileName, actual);
    }

    [Fact]
    public void GetDockingLayoutFileName_未知の名前は例外を投げる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainWindow.GetDockingLayoutFileName("UnknownDockingManager"));
    }
}
