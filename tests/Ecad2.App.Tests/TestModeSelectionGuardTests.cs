using Ecad2.App.ViewModels;

namespace Ecad2.App.Tests;

/// <summary>T-061往復修正: MainWindow.ShouldSkipSelectionInTestModeの回帰テスト。
/// LadderCanvasHost_PreviewMouseLeftButtonUpにMode==Testのガードが無く、TestModePressがnullを
/// 返すケース(SelectSwitch/Relay(ContactNO/NC以外)/ヒット無し等)で通常編集モードの選択処理
/// (セル選択等)へ意図せずフォールスルーしていたバグの修正(忍者実機確認・隠密静的レビュー合同発見)。
/// ShouldSkipSelectionForRungCommentAreaClickと同型の判定関数として抽出する(テスト容易性)。
/// </summary>
public class TestModeSelectionGuardTests
{
    [Theory]
    [InlineData(AppMode.Test, true)]
    [InlineData(AppMode.Drawing, false)]
    public void ShouldSkipSelectionInTestMode_ModeがTestのときのみtrue(AppMode mode, bool expectedSkip)
    {
        bool actual = MainWindow.ShouldSkipSelectionInTestMode(mode);

        Assert.Equal(expectedSkip, actual);
    }
}
