using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-085(表示灯(Lamp)の色記号入力UI新設、殿裁定2026-07-13、P-057起票)の回帰テスト。
/// IsSelectedElementLamp/SelectedElementLampColorの新設プロパティを検証する
/// (T086NotchPositionTestsと同型の構成)。
/// </summary>
public class T085LampColorTests : ViewModelTestBase
{
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    [Fact]
    public void IsSelectedElementLamp_Lamp選択時true()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.LampId, "L1");

        Assert.True(vm.IsSelectedElementLamp);
    }

    [Fact]
    public void IsSelectedElementLamp_ContactNO選択時false()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");

        Assert.False(vm.IsSelectedElementLamp);
    }

    [Fact]
    public void SelectedElementLampColor_未設定なら空文字列を返す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.LampId, "L1");

        Assert.Equal("", vm.SelectedElementLampColor);
    }

    [Theory]
    [InlineData("R")]
    [InlineData("赤")]
    [InlineData("RD")]
    public void SelectedElementLampColor_1から2文字は反映されUndo可能になる(string input)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.LampId, "L1");

        vm.SelectedElementLampColor = input;

        Assert.Equal(input, vm.SelectedElementLampColor);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedElementLampColor_3文字以上は値を変更しない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.LampId, "L1");
        vm.SelectedElementLampColor = "R";   // 前提となる既存値

        vm.SelectedElementLampColor = "RED";

        Assert.Equal("R", vm.SelectedElementLampColor);
    }

    [Fact]
    public void SelectedElementLampColor_空文字設定でクリアされUndo可能になる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.LampId, "L1");
        vm.SelectedElementLampColor = "R";   // 前提となる既存値

        vm.SelectedElementLampColor = "";

        Assert.Equal("", vm.SelectedElementLampColor);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedElementLampColor_値未変化ならUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.LampId, "L1");
        vm.SelectedElementLampColor = "R";
        Assert.True(vm.UndoCommand.CanExecute(null));   // 前提: 値変更でUndo履歴が1件作られる
        vm.UndoCommand.Execute(null);
        Assert.False(vm.UndoCommand.CanExecute(null));   // 前提: Undoで履歴を使い切った

        vm.SelectedElementLampColor = vm.SelectedElementLampColor;   // 現在値をそのまま再設定(no-op)

        Assert.False(vm.UndoCommand.CanExecute(null));
    }
}
