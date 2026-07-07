using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分3: 配線分断(WireBreak)の記入・選択・削除のViewModelロジックの回帰テスト。
/// SelectedConnectorと同型の排他制御(SelectedCellのsetterが常時クリア)・案A(選択→Delete)の
/// 検証を含む。
/// </summary>
public class WireBreakSelectionTests : ViewModelTestBase
{
    [Fact]
    public void PlaceWireBreakAtSelectedCell_AddsWireBreakAtCellCenterBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(2, 4);

        bool placed = vm.PlaceWireBreakAtSelectedCell();

        Assert.True(placed);
        Assert.True(vm.IsDirty);
        var wireBreak = Assert.Single(vm.CurrentSheet!.WireBreaks);
        Assert.Equal(2, wireBreak.Row);
        Assert.Equal(4.5, wireBreak.Boundary);
    }

    [Fact]
    public void PlaceWireBreakAtSelectedCell_DuplicatePosition_ReturnsFalseAndDoesNotAddTwice()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(2, 4);
        vm.PlaceWireBreakAtSelectedCell();

        bool placedAgain = vm.PlaceWireBreakAtSelectedCell();

        Assert.False(placedAgain);
        Assert.Single(vm.CurrentSheet!.WireBreaks);
    }

    [Fact]
    public void SettingSelectedCell_ClearsSelectedWireBreak()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedWireBreak = new WireBreak { Boundary = 4.5, Row = 2 };

        vm.SelectedCell = new GridPos(0, 0);

        Assert.Null(vm.SelectedWireBreak);
    }

    [Fact]
    public void ReplaceDocument_ClearsSelectedWireBreak_OnNewDocument()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedWireBreak = new WireBreak { Boundary = 4.5, Row = 2 };

        vm.NewDocument();

        Assert.Null(vm.SelectedWireBreak);
    }

    [Fact]
    public void DeleteSelectedWireBreak_RemovesFromSheetAndClearsSelection()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(2, 4);
        vm.PlaceWireBreakAtSelectedCell();
        var wireBreak = vm.CurrentSheet!.WireBreaks[0];
        vm.SelectedWireBreak = wireBreak;

        bool deleted = vm.DeleteSelectedWireBreak();

        Assert.True(deleted);
        Assert.Empty(vm.CurrentSheet!.WireBreaks);
        Assert.Null(vm.SelectedWireBreak);
    }

    [Fact]
    public void DeleteSelectedWireBreak_ReturnsFalse_WhenAlreadyRemoved()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var wireBreak = new WireBreak { Boundary = 4.5, Row = 2 };
        vm.CurrentSheet!.WireBreaks.Add(wireBreak);
        vm.SelectedWireBreak = wireBreak;
        vm.CurrentSheet!.WireBreaks.Remove(wireBreak);

        bool deleted = vm.DeleteSelectedWireBreak();

        Assert.False(deleted);
    }
}
