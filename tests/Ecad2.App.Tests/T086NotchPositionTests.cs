using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-086(セレクトSWのノッチ位置設定UI新設、調査書docs/ecad2-t086-select-switch-position-ui-survey-onmitsu.md
/// DoD3)の回帰テスト。IsSelectedElementSelectSwitch/SelectedElementNotchPositionの新設プロパティを検証する。
/// </summary>
public class T086NotchPositionTests : ViewModelTestBase
{
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    [Fact]
    public void IsSelectedElementSelectSwitch_SelectSwitch選択時true()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.SelectSwitchId, "SW1");

        Assert.True(vm.IsSelectedElementSelectSwitch);
    }

    [Fact]
    public void IsSelectedElementSelectSwitch_ContactNO選択時false()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");

        Assert.False(vm.IsSelectedElementSelectSwitch);
    }

    [Fact]
    public void SelectedElementNotchPosition_未設定なら空文字列を返す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.SelectSwitchId, "SW1");

        Assert.Equal("", vm.SelectedElementNotchPosition);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("99")]
    public void SelectedElementNotchPosition_範囲内の値は反映されUndo可能になる(string input)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.SelectSwitchId, "SW1");

        vm.SelectedElementNotchPosition = input;

        Assert.Equal(input, vm.SelectedElementNotchPosition);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("100")]
    [InlineData("abc")]
    [InlineData("")]
    public void SelectedElementNotchPosition_範囲外または非数値は値を変更しない(string input)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.SelectSwitchId, "SW1");
        vm.SelectedElementNotchPosition = "5";   // 前提となる既存値

        vm.SelectedElementNotchPosition = input;

        Assert.Equal("5", vm.SelectedElementNotchPosition);
    }

    [Fact]
    public void SelectedElementNotchPosition_値未変化ならUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.SelectSwitchId, "SW1");
        vm.SelectedElementNotchPosition = "3";
        Assert.True(vm.UndoCommand.CanExecute(null));   // 前提: 値変更でUndo履歴が1件作られる
        vm.UndoCommand.Execute(null);
        Assert.False(vm.UndoCommand.CanExecute(null));   // 前提: Undoで履歴を使い切った

        vm.SelectedElementNotchPosition = vm.SelectedElementNotchPosition;   // 現在値をそのまま再設定(no-op)

        Assert.False(vm.UndoCommand.CanExecute(null));
    }
}
