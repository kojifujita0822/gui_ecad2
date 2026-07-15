using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-096(タイマー設定時間・Setpoint入力UI新設、殿裁定2026-07-15、P-099起票)の回帰テスト。
/// IsSelectedElementTimerRelated/SelectedElementSetpoint/SelectedElementSetpointSliderValueの
/// 新設プロパティを検証する(T085LampColorTests/T086NotchPositionTestsと同型の構成)。
/// 殿訂正(2026-07-15)＝タイマーコイル本体(ElementKind.Timer)は仕様として存在しないため、
/// Setpoint編集対象は選択要素自身。殿直接指摘(同日)＝瞬時接点(TimerInstantContactNO/NC)に
/// 設定時間は不要のため、対象は限時接点NO/NC計2種のみに絞り込み(瞬時接点は対照ケースへ移動)。
/// </summary>
public class T096SetpointTests : ViewModelTestBase
{
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    [Theory]
    [InlineData(BasicPartTemplates.TimerContactNOId)]
    [InlineData(BasicPartTemplates.TimerContactNCId)]
    public void IsSelectedElementTimerRelated_限時接点選択時true(string partId)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, partId, "T1");

        Assert.True(vm.IsSelectedElementTimerRelated);
    }

    [Theory]
    [InlineData(BasicPartTemplates.TimerInstantContactNOId)]
    [InlineData(BasicPartTemplates.TimerInstantContactNCId)]
    public void IsSelectedElementTimerRelated_瞬時接点選択時false(string partId)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, partId, "T1");

        Assert.False(vm.IsSelectedElementTimerRelated);
    }

    [Fact]
    public void IsSelectedElementTimerRelated_ContactNO選択時false()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");

        Assert.False(vm.IsSelectedElementTimerRelated);
    }

    [Fact]
    public void SelectedElementSetpoint_未設定なら空文字列を返す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.TimerContactNOId, "T1");

        Assert.Equal("", vm.SelectedElementSetpoint);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("10", "10")]
    [InlineData("9999", "9999")]
    [InlineData("3.7", "4")]   // GuiEcad仕様=整数丸め(Math.Round)
    public void SelectedElementSetpoint_範囲内の値は反映されUndo可能になる(string input, string expected)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.TimerContactNOId, "T1");

        vm.SelectedElementSetpoint = input;

        Assert.Equal(expected, vm.SelectedElementSetpoint);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("10000")]
    [InlineData("abc")]
    [InlineData("")]
    public void SelectedElementSetpoint_範囲外または非数値は値を変更しない(string input)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.TimerContactNOId, "T1");
        vm.SelectedElementSetpoint = "5";   // 前提となる既存値

        vm.SelectedElementSetpoint = input;

        Assert.Equal("5", vm.SelectedElementSetpoint);
    }

    [Fact]
    public void SelectedElementSetpoint_値未変化ならUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.TimerContactNOId, "T1");
        vm.SelectedElementSetpoint = "3";
        Assert.True(vm.UndoCommand.CanExecute(null));   // 前提: 値変更でUndo履歴が1件作られる
        vm.UndoCommand.Execute(null);
        Assert.False(vm.UndoCommand.CanExecute(null));   // 前提: Undoで履歴を使い切った

        vm.SelectedElementSetpoint = vm.SelectedElementSetpoint;   // 現在値をそのまま再設定(no-op)

        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedElementSetpointSliderValue_未設定なら0を返す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.TimerContactNOId, "T1");

        Assert.Equal(0, vm.SelectedElementSetpointSliderValue);
    }

    [Fact]
    public void SelectedElementSetpointSliderValue_10秒超のSetpointは表示のみ10にクランプされる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.TimerContactNOId, "T1");
        vm.SelectedElementSetpoint = "9999";

        Assert.Equal(10, vm.SelectedElementSetpointSliderValue);
        Assert.Equal("9999", vm.SelectedElementSetpoint);   // 本体側の値はクランプされない
    }

    [Fact]
    public void SelectedElementSetpointSliderValue_設定はSelectedElementSetpointへ反映される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.TimerContactNOId, "T1");

        vm.SelectedElementSetpointSliderValue = 7;

        Assert.Equal("7", vm.SelectedElementSetpoint);
        Assert.Equal(7, vm.SelectedElementSetpointSliderValue);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }
}
