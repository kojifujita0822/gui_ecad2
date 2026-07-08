using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分7横展開: 配線分断(WireBreak)のドラッグ移動・キーボード等価操作(平行移動)の
/// ViewModelロジックの回帰テスト。VerticalConnectorと異なり点系のため端点概念は無く、
/// 本体移動のみを検証する(ConnectorDragAndResizeTests.csと同じ方針、View操作を介さない)。
/// </summary>
public class WireBreakDragTests : ViewModelTestBase
{
    private static WireBreak MakeWireBreak() => new() { Boundary = 4.5, Row = 3 };

    // ---- MoveSelectedWireBreak(キーボード平行移動) ----

    [Fact]
    public void MoveSelectedWireBreak_ShiftsRowAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var b = MakeWireBreak();
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedCell = null;
        vm.SelectedWireBreak = b;

        bool moved = vm.MoveSelectedWireBreak(1, 0);

        Assert.True(moved);
        Assert.Equal(4, b.Row);
        Assert.Equal(4.5, b.Boundary);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void MoveSelectedWireBreak_ShiftsBoundaryAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var b = MakeWireBreak();
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedWireBreak = b;

        bool moved = vm.MoveSelectedWireBreak(0, 1);

        Assert.True(moved);
        Assert.Equal(3, b.Row);
        Assert.Equal(5.5, b.Boundary);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void MoveSelectedWireBreak_ClampsAtGridBounds()
    {
        var vm = CreateViewModel();
        vm.NewDocument();   // GridSpec既定Rows=10(行0〜9)/Columns=20
        var b = new WireBreak { Boundary = 0, Row = 0 };
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedWireBreak = b;

        bool moved = vm.MoveSelectedWireBreak(-1, -1);   // 既にRow=0/Boundary=0(いずれも下限)

        Assert.False(moved);
        Assert.Equal(0, b.Row);
        Assert.Equal(0, b.Boundary);
    }

    [Fact]
    public void MoveSelectedWireBreak_WithoutSelection_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.MoveSelectedWireBreak(1, 0));
        Assert.False(vm.IsDirty);
    }

    // ---- ドラッグ(BeginDragWireBreak/UpdateDragWireBreak/ConfirmDragWireBreak/CancelDragWireBreak) ----

    [Fact]
    public void DragWireBreak_UpdatesModelDuringDragAndMarksDirtyOnConfirm()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var b = MakeWireBreak();
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedWireBreak = b;

        vm.BeginDragWireBreak(b, startRow: 3, startBoundary: 4.5);
        Assert.False(vm.IsDirty);   // ドラッグ中はまだMarkDirty()しない

        vm.UpdateDragWireBreak(currentRow: 5, currentBoundary: 6.5);   // +2行, +2列

        Assert.Equal(5, b.Row);
        Assert.Equal(6.5, b.Boundary);
        Assert.False(vm.IsDirty);   // 確定前はまだ

        vm.ConfirmDragWireBreak();

        Assert.True(vm.IsDirty);
        Assert.False(vm.IsDraggingWireBreak);
    }

    [Fact]
    public void DragWireBreak_ClampsAtGridBounds()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var b = MakeWireBreak();
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedWireBreak = b;

        vm.BeginDragWireBreak(b, startRow: 3, startBoundary: 4.5);
        vm.UpdateDragWireBreak(currentRow: 20, currentBoundary: 4.5);   // Grid.Rows=10を大きく超える

        Assert.Equal(9, b.Row);   // Rows-1でクランプ
    }

    [Fact]
    public void DragWireBreak_Cancel_RestoresOriginalPositionWithoutMarkingDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var b = MakeWireBreak();
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedWireBreak = b;

        vm.BeginDragWireBreak(b, startRow: 3, startBoundary: 4.5);
        vm.UpdateDragWireBreak(currentRow: 6, currentBoundary: 4.5);   // 途中まで動かす
        Assert.Equal(6, b.Row);

        vm.CancelDragWireBreak();

        Assert.Equal(3, b.Row);   // 開始時の位置へ復元
        Assert.Equal(4.5, b.Boundary);
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsDraggingWireBreak);
    }

    [Fact]
    public void ConfirmDragWireBreak_WhenPositionUnchanged_DoesNotMarkDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var b = MakeWireBreak();
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedWireBreak = b;

        vm.BeginDragWireBreak(b, startRow: 3, startBoundary: 4.5);
        vm.UpdateDragWireBreak(currentRow: 3, currentBoundary: 4.5);   // 移動量ゼロ
        vm.ConfirmDragWireBreak();

        Assert.False(vm.IsDirty);
    }

    // ---- 所見A横展開: ドラッグ中の外部要因(Delete/シート切替/文書差し替え)による強制クリア ----

    [Fact]
    public void SelectedWireBreakAssignment_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var b = MakeWireBreak();
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedWireBreak = b;
        vm.BeginDragWireBreak(b, startRow: 3, startBoundary: 4.5);
        Assert.True(vm.IsDraggingWireBreak);

        bool deleted = vm.DeleteSelectedWireBreak();

        Assert.True(deleted);
        Assert.False(vm.IsDraggingWireBreak);
        var ex = Record.Exception(() => vm.UpdateDragWireBreak(currentRow: 6, currentBoundary: 6.5));
        Assert.Null(ex);
        Assert.Equal(3, b.Row);   // 書き換わっていない
    }

    [Fact]
    public void ReplaceDocument_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var b = MakeWireBreak();
        vm.CurrentSheet!.WireBreaks.Add(b);
        vm.SelectedWireBreak = b;
        vm.BeginDragWireBreak(b, startRow: 3, startBoundary: 4.5);

        vm.NewDocument();

        Assert.False(vm.IsDraggingWireBreak);
        Assert.False(vm.IsDirty);
        var ex = Record.Exception(() => vm.UpdateDragWireBreak(currentRow: 6, currentBoundary: 6.5));
        Assert.Null(ex);
    }
}
