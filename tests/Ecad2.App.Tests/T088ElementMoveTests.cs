using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-088(基本図形の配置後移動機能新設、調査書docs/ecad2-element-move-feature-survey-onmitsu.md)の
/// 回帰テスト。ViewModel層(BeginDragElement/UpdateDragElement/ConfirmDragElement/CancelDragElement/
/// MoveSelectedElement)を検証する。MainWindow.xaml.cs側のマウス配線・キー配線はコードビハインドの
/// ためテスト基盤が無く対象外(T-070 A-7/T-087と同事情)。
/// </summary>
public class T088ElementMoveTests : ViewModelTestBase
{
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    [Fact]
    public void MoveSelectedElement_空きセルへの移動_成功しUndo可能になる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        bool moved = vm.MoveSelectedElement(0, 1);

        Assert.True(moved);
        Assert.Equal(new GridPos(5, 6), element.Pos);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void MoveSelectedElement_グリッド範囲外_移動しない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");

        bool moved = vm.MoveSelectedElement(-1, 0);

        Assert.False(moved);
        Assert.Equal(new GridPos(0, 0), vm.SelectedElement!.Pos);
    }

    [Fact]
    public void MoveSelectedElement_他要素が占有中のセル_移動しない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        PlaceAt(vm, 5, 6, BasicPartTemplates.ContactNOId, "X002");
        vm.SelectedCell = new GridPos(5, 5);

        bool moved = vm.MoveSelectedElement(0, 1);

        Assert.False(moved);
        Assert.Equal(new GridPos(5, 5), vm.SelectedElement!.Pos);
    }

    [Fact]
    public void BeginDragElement_UpdateDragElement_ConfirmDragElement_移動しUndo記録される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        vm.BeginDragElement(element);
        vm.UpdateDragElement(new GridPos(7, 8));
        vm.ConfirmDragElement();

        Assert.Equal(new GridPos(7, 8), element.Pos);
        Assert.False(vm.IsDraggingElement);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void UpdateDragElement_占有先はその場に留まる_自分自身の元位置は占有扱いしない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        PlaceAt(vm, 5, 6, BasicPartTemplates.ContactNOId, "X002");
        vm.SelectedCell = new GridPos(5, 5);
        var element = vm.SelectedElement!;

        vm.BeginDragElement(element);
        // 他要素(X002)が既に占有しているセルへは移動しない。
        vm.UpdateDragElement(new GridPos(5, 6));
        Assert.Equal(new GridPos(5, 5), element.Pos);

        // 自分自身の元位置(占有チェックで自己ヒットしない)へは戻れる。
        vm.UpdateDragElement(new GridPos(6, 5));
        vm.UpdateDragElement(new GridPos(5, 5));
        Assert.Equal(new GridPos(5, 5), element.Pos);
    }

    [Fact]
    public void CancelDragElement_開始時位置へ復元する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        vm.BeginDragElement(element);
        vm.UpdateDragElement(new GridPos(7, 8));
        vm.CancelDragElement();

        Assert.Equal(new GridPos(5, 5), element.Pos);
        Assert.False(vm.IsDraggingElement);
        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void ConfirmDragElement_実際に動いていなければUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        vm.BeginDragElement(element);
        vm.ConfirmDragElement();

        Assert.Equal(new GridPos(5, 5), element.Pos);
        Assert.False(vm.UndoCommand.CanExecute(null));
    }
}
