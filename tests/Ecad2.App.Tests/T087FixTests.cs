using Ecad2.App.ViewModels;

namespace Ecad2.App.Tests;

/// <summary>
/// T-087往復修正(PR-13、隠密静的レビュー指摘)の回帰テスト。Ctrl+Tガード漏れ・F11反応漏れ・
/// Shift+Tab循環漏れの3件はMainWindow.xaml.cs側のコードビハインドが対象でありテスト基盤が無い
/// (T-070 A-7/T-087初回と同事情)。ここではReplaceDocument経由の二次被害対処(Mode=Testのまま
/// 新規作成/開くを行った場合、新Documentが編集不能状態から始まる懸念)のみViewModelレベルで検証する。
/// </summary>
public class T087FixTests : ViewModelTestBase
{
    [Fact]
    public void NewDocument_直前にModeがTestだった場合_Drawingへリセットされる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Mode = AppMode.Test;

        vm.NewDocument();

        Assert.Equal(AppMode.Drawing, vm.Mode);
        Assert.True(vm.CanEditDiagram);
    }

    // T-087往復5周目修正(隠密設計書、殿裁定): ActivateAndFocusPartSelectionの判定ロジックのみを
    // internal static抽出したShouldSuppressPartSelectionActivation/ShouldReactivateToolForPartSelection
    // の回帰テスト(ShouldSkipSelectionInTestModeと同型パターン)。
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void ShouldSuppressPartSelectionActivation_CanEditDiagramがfalseまたはHasAnyDraftがtrueならtrue(
        bool canEditDiagram, bool hasAnyDraft, bool expectedSuppress)
    {
        bool actual = MainWindow.ShouldSuppressPartSelectionActivation(canEditDiagram, hasAnyDraft);

        Assert.Equal(expectedSuppress, actual);
    }

    [Theory]
    [InlineData(ToolMode.PlaceElement, false)]
    [InlineData(ToolMode.Select, true)]
    [InlineData(ToolMode.PlaceConnector, true)]
    public void ShouldReactivateToolForPartSelection_ModeがPlaceElement以外のときのみtrue(
        ToolMode currentMode, bool expectedReactivate)
    {
        bool actual = MainWindow.ShouldReactivateToolForPartSelection(currentMode);

        Assert.Equal(expectedReactivate, actual);
    }
}
