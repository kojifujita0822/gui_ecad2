using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分7横展開: 接続点(ConnectionDot)のドラッグ移動・キーボード等価操作(平行移動)の
/// ViewModelロジックの回帰テスト。WireBreakDragTestsのmm実座標系版(点系・本体移動のみ)。
/// 所見A(外部要因による強制クリア)も併せて検証する。
/// </summary>
public class ConnectionDotDragTests : ViewModelTestBase
{
    private static ConnectionDot MakeDot() => new() { XMm = 20, YMm = 30 };

    // ---- MoveSelectedConnectionDot(キーボード平行移動) ----

    [Fact]
    public void MoveSelectedConnectionDot_ShiftsPositionAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var dot = MakeDot();
        vm.CurrentSheet!.ConnectionDots.Add(dot);
        vm.SelectedCell = null;
        vm.SelectedConnectionDot = dot;

        bool moved = vm.MoveSelectedConnectionDot(5, -3);

        Assert.True(moved);
        Assert.Equal(25, dot.XMm);
        Assert.Equal(27, dot.YMm);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void MoveSelectedConnectionDot_WithoutSelection_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.MoveSelectedConnectionDot(5, 0));
        Assert.False(vm.IsDirty);
    }

    // ---- ドラッグ(BeginDragConnectionDot/UpdateDragConnectionDot/ConfirmDragConnectionDot/CancelDragConnectionDot) ----

    [Fact]
    public void DragConnectionDot_UpdatesModelDuringDragAndMarksDirtyOnConfirm()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var dot = MakeDot();
        vm.CurrentSheet!.ConnectionDots.Add(dot);
        vm.SelectedConnectionDot = dot;

        vm.BeginDragConnectionDot(dot, startXMm: 20, startYMm: 30);
        Assert.False(vm.IsDirty);

        vm.UpdateDragConnectionDot(currentXMm: 25, currentYMm: 32);

        Assert.Equal(25, dot.XMm);
        Assert.Equal(32, dot.YMm);
        Assert.False(vm.IsDirty);

        vm.ConfirmDragConnectionDot();

        Assert.True(vm.IsDirty);
        Assert.False(vm.IsDraggingConnectionDot);
    }

    [Fact]
    public void DragConnectionDot_Cancel_RestoresOriginalPositionWithoutMarkingDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var dot = MakeDot();
        vm.CurrentSheet!.ConnectionDots.Add(dot);
        vm.SelectedConnectionDot = dot;

        vm.BeginDragConnectionDot(dot, startXMm: 20, startYMm: 30);
        vm.UpdateDragConnectionDot(currentXMm: 25, currentYMm: 32);
        Assert.Equal(25, dot.XMm);

        vm.CancelDragConnectionDot();

        Assert.Equal(20, dot.XMm);
        Assert.Equal(30, dot.YMm);
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsDraggingConnectionDot);
    }

    [Fact]
    public void ConfirmDragConnectionDot_WhenPositionUnchanged_DoesNotMarkDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var dot = MakeDot();
        vm.CurrentSheet!.ConnectionDots.Add(dot);
        vm.SelectedConnectionDot = dot;

        vm.BeginDragConnectionDot(dot, startXMm: 20, startYMm: 30);
        vm.UpdateDragConnectionDot(currentXMm: 20, currentYMm: 30);
        vm.ConfirmDragConnectionDot();

        Assert.False(vm.IsDirty);
    }

    // ---- 所見A横展開: ドラッグ中の外部要因(Delete/文書差し替え)による強制クリア ----

    [Fact]
    public void SelectedConnectionDotAssignment_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var dot = MakeDot();
        vm.CurrentSheet!.ConnectionDots.Add(dot);
        vm.SelectedConnectionDot = dot;
        vm.BeginDragConnectionDot(dot, startXMm: 20, startYMm: 30);
        Assert.True(vm.IsDraggingConnectionDot);

        bool deleted = vm.DeleteSelectedConnectionDot();

        Assert.True(deleted);
        Assert.False(vm.IsDraggingConnectionDot);
        var ex = Record.Exception(() => vm.UpdateDragConnectionDot(currentXMm: 25, currentYMm: 32));
        Assert.Null(ex);
        Assert.Equal(20, dot.XMm);   // 書き換わっていない
    }

    [Fact]
    public void ReplaceDocument_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var dot = MakeDot();
        vm.CurrentSheet!.ConnectionDots.Add(dot);
        vm.SelectedConnectionDot = dot;
        vm.BeginDragConnectionDot(dot, startXMm: 20, startYMm: 30);

        vm.NewDocument();

        Assert.False(vm.IsDraggingConnectionDot);
        Assert.False(vm.IsDirty);
        var ex = Record.Exception(() => vm.UpdateDragConnectionDot(currentXMm: 25, currentYMm: 32));
        Assert.Null(ex);
    }
}
