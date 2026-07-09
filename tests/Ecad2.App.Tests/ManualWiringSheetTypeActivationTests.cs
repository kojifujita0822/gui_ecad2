using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-047: 手動配線系(F9/Shift+F9/F10)ツールバーボタンの活性制御に使う
/// <see cref="MainWindowViewModel.IsMainCircuitSheet"/>・<see cref="MainWindowViewModel.IsControlCircuitSheet"/>
/// を検証する。CurrentSheetIndexのSetProperty早期return再発トラップ(T-041増分5隠密レビュー指摘と
/// 同型、家老采配時の注意喚起)により、index数値が変化しない削除経路でも通知が飛ぶことを含める。
/// </summary>
public class ManualWiringSheetTypeActivationTests : ViewModelTestBase
{
    [Fact]
    public void NoProject_BothPropertiesAreFalse()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsMainCircuitSheet);
        Assert.False(vm.IsControlCircuitSheet);
    }

    [Fact]
    public void NewDocument_DefaultSheetIsControlCircuit()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.IsMainCircuitSheet);
        Assert.True(vm.IsControlCircuitSheet);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public void Properties_ReflectCurrentSheetMainCircuit(bool mainCircuit, bool expectedIsMain, bool expectedIsControl)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets[0].MainCircuit = mainCircuit;

        Assert.Equal(expectedIsMain, vm.IsMainCircuitSheet);
        Assert.Equal(expectedIsControl, vm.IsControlCircuitSheet);
    }

    /// <summary>
    /// 3枚[シート1(制御,index0)/シート2(主回路,index1・表示中)/シート3(制御,index2)]でシート2を
    /// 削除すると、残り2枚に対しCurrentSheetIndex = Math.Min(1, 2-1=1) = 1で数値上は変化しないが、
    /// 実体はシート2(主回路)からシート3(制御)へ入れ替わる。CurrentSheetIndexのsetterがSetProperty
    /// の戻り値でガードせず無条件通知する実装でなければ、この経路でIsMainCircuitSheet/
    /// IsControlCircuitSheetの変更通知が飛ばず、ボタンのグレーアウトが古い状態のまま固まる。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhenIndexNumberStaysSame_StillNotifiesSheetTypeProperties()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            MainCircuit = true,
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 3,
            Name = "シート3",
            MainCircuit = false,
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1; // シート2(主回路)を表示中
        Assert.True(vm.IsMainCircuitSheet);
        Assert.False(vm.IsControlCircuitSheet);

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal(1, vm.CurrentSheetIndex);
        Assert.False(vm.IsMainCircuitSheet);
        Assert.True(vm.IsControlCircuitSheet);
        Assert.Contains(nameof(MainWindowViewModel.IsMainCircuitSheet), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsControlCircuitSheet), raised);
    }
}
