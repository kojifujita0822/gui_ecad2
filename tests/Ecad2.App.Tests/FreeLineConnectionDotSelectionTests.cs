using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分5: 自由線(FreeLine)・接続点(ConnectionDot)の選択・削除・記入のViewModelロジックの
/// 回帰テスト。SelectedConnector/SelectedWireBreakと同型の排他制御(SelectedCellのsetterが常時
/// クリア)・案A(選択→Delete)の検証を含む。
/// </summary>
public class FreeLineConnectionDotSelectionTests : ViewModelTestBase
{
    private static Sheet MainCircuitSheet(MainWindowViewModel vm)
    {
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        return vm.CurrentSheet!;
    }

    [Fact]
    public void SettingSelectedCell_ClearsSelectedFreeLineAndSelectedConnectionDot()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedFreeLine = new FreeLine { X1Mm = 10, Y1Mm = 10, X2Mm = 20, Y2Mm = 10 };
        vm.SelectedConnectionDot = new ConnectionDot { XMm = 10, YMm = 10 };

        vm.SelectedCell = new GridPos(0, 0);

        Assert.Null(vm.SelectedFreeLine);
        Assert.Null(vm.SelectedConnectionDot);
    }

    [Fact]
    public void ReplaceDocument_ClearsSelectedFreeLineAndSelectedConnectionDot_OnNewDocument()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedFreeLine = new FreeLine { X1Mm = 10, Y1Mm = 10, X2Mm = 20, Y2Mm = 10 };
        vm.SelectedConnectionDot = new ConnectionDot { XMm = 10, YMm = 10 };

        vm.NewDocument();

        Assert.Null(vm.SelectedFreeLine);
        Assert.Null(vm.SelectedConnectionDot);
    }

    [Fact]
    public void DeleteSelectedFreeLine_RemovesFromSheetAndClearsSelection()
    {
        var vm = CreateViewModel();
        var sheet = MainCircuitSheet(vm);
        var line = new FreeLine { X1Mm = 10, Y1Mm = 10, X2Mm = 20, Y2Mm = 10 };
        sheet.FreeLines.Add(line);
        vm.SelectedFreeLine = line;

        bool deleted = vm.DeleteSelectedFreeLine();

        Assert.True(deleted);
        Assert.Empty(sheet.FreeLines);
        Assert.Null(vm.SelectedFreeLine);
    }

    [Fact]
    public void DeleteSelectedFreeLine_ReturnsFalse_WhenAlreadyRemoved()
    {
        var vm = CreateViewModel();
        var sheet = MainCircuitSheet(vm);
        var line = new FreeLine { X1Mm = 10, Y1Mm = 10, X2Mm = 20, Y2Mm = 10 };
        sheet.FreeLines.Add(line);
        vm.SelectedFreeLine = line;
        sheet.FreeLines.Remove(line);

        bool deleted = vm.DeleteSelectedFreeLine();

        Assert.False(deleted);
    }

    [Fact]
    public void PlaceConnectionDot_AddsDotAtGivenMmPosition()
    {
        var vm = CreateViewModel();
        var sheet = MainCircuitSheet(vm);

        bool placed = vm.PlaceConnectionDot(15.5, 24.0);

        Assert.True(placed);
        Assert.True(vm.IsDirty);
        var dot = Assert.Single(sheet.ConnectionDots);
        Assert.Equal(15.5, dot.XMm);
        Assert.Equal(24.0, dot.YMm);
    }

    [Fact]
    public void PlaceConnectionDot_DuplicatePosition_ReturnsFalseAndDoesNotAddTwice()
    {
        var vm = CreateViewModel();
        var sheet = MainCircuitSheet(vm);
        vm.PlaceConnectionDot(15.5, 24.0);

        bool placedAgain = vm.PlaceConnectionDot(15.5, 24.0);

        Assert.False(placedAgain);
        Assert.Single(sheet.ConnectionDots);
    }

    [Fact]
    public void DeleteSelectedConnectionDot_RemovesFromSheetAndClearsSelection()
    {
        var vm = CreateViewModel();
        var sheet = MainCircuitSheet(vm);
        var dot = new ConnectionDot { XMm = 15.5, YMm = 24.0 };
        sheet.ConnectionDots.Add(dot);
        vm.SelectedConnectionDot = dot;

        bool deleted = vm.DeleteSelectedConnectionDot();

        Assert.True(deleted);
        Assert.Empty(sheet.ConnectionDots);
        Assert.Null(vm.SelectedConnectionDot);
    }

    [Fact]
    public void DeleteSelectedConnectionDot_ReturnsFalse_WhenAlreadyRemoved()
    {
        var vm = CreateViewModel();
        var sheet = MainCircuitSheet(vm);
        var dot = new ConnectionDot { XMm = 15.5, YMm = 24.0 };
        sheet.ConnectionDots.Add(dot);
        vm.SelectedConnectionDot = dot;
        sheet.ConnectionDots.Remove(dot);

        bool deleted = vm.DeleteSelectedConnectionDot();

        Assert.False(deleted);
    }
}
