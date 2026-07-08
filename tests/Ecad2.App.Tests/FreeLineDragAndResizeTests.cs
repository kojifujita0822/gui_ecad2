using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分7横展開: 自由線(FreeLine)のドラッグ(本体移動/端点リサイズ)・キーボード等価操作
/// (平行移動/Tab+Shift矢印での端点伸縮)のViewModelロジックの回帰テスト。ConnectorDragAndResizeTests
/// と同じ方針(View操作を介さない)。mm実座標系のため、水平・垂直の向きを保つ制約とゼロ長禁止を
/// 併せて検証する(VerticalConnectorには無いFreeLine固有の制約)。
/// </summary>
public class FreeLineDragAndResizeTests : ViewModelTestBase
{
    private static FreeLine MakeHorizontal() => new() { X1Mm = 10, Y1Mm = 30, X2Mm = 40, Y2Mm = 30 };
    private static FreeLine MakeVertical() => new() { X1Mm = 20, Y1Mm = 10, X2Mm = 20, Y2Mm = 50 };

    // ---- MoveSelectedFreeLine(キーボード平行移動) ----

    [Fact]
    public void MoveSelectedFreeLine_ShiftsBothEndpointsAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedCell = null;
        vm.SelectedFreeLine = line;

        bool moved = vm.MoveSelectedFreeLine(5, 2, maxXMm: 1000, maxYMm: 1000);

        Assert.True(moved);
        Assert.Equal(15, line.X1Mm);
        Assert.Equal(32, line.Y1Mm);
        Assert.Equal(45, line.X2Mm);
        Assert.Equal(32, line.Y2Mm);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void MoveSelectedFreeLine_WithoutSelection_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.MoveSelectedFreeLine(5, 0, maxXMm: 1000, maxYMm: 1000));
        Assert.False(vm.IsDirty);
    }

    // T-041増分7隠密レビュー所見AA対応: グリッド・ページ境界へのクランプ回帰テスト
    [Fact]
    public void MoveSelectedFreeLine_ClampsToPageBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();   // X1=10,Y1=30 - X2=40,Y2=30
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;

        // ページ境界50mmに対し+100mm移動を試みても、終点(X2=40)が境界に達するところで止まる
        bool moved = vm.MoveSelectedFreeLine(100, 0, maxXMm: 50, maxYMm: 1000);

        Assert.True(moved);
        Assert.Equal(20, line.X1Mm);
        Assert.Equal(50, line.X2Mm);
        Assert.True(vm.IsDirty);
    }

    // ---- ResizeSelectedFreeLineEndpoint(Tab+Shift矢印、端点伸縮) ----

    [Fact]
    public void ResizeSelectedFreeLineEndpoint_Horizontal_StartSelected_MovesX1Only()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();   // X1=10,Y1=30 - X2=40,Y2=30
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;   // 既定=始点

        bool resized = vm.ResizeSelectedFreeLineEndpoint(-5, 0);

        Assert.True(resized);
        Assert.Equal(5, line.X1Mm);
        Assert.Equal(30, line.Y1Mm);
        Assert.Equal(40, line.X2Mm);   // 終点は不変
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void ResizeSelectedFreeLineEndpoint_Horizontal_EndSelected_MovesX2Only()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;
        vm.ToggleSelectedEndpoint();   // 始点→終点

        bool resized = vm.ResizeSelectedFreeLineEndpoint(5, 0);

        Assert.True(resized);
        Assert.Equal(10, line.X1Mm);   // 始点は不変
        Assert.Equal(45, line.X2Mm);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void ResizeSelectedFreeLineEndpoint_Vertical_IgnoresXDelta()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeVertical();   // X1=20,Y1=10 - X2=20,Y2=50
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;

        bool resized = vm.ResizeSelectedFreeLineEndpoint(deltaXMm: 5, deltaYMm: -3);

        Assert.True(resized);
        Assert.Equal(20, line.X1Mm);   // 垂直線なのでX方向のdeltaは無視
        Assert.Equal(7, line.Y1Mm);
    }

    [Fact]
    public void ResizeSelectedFreeLineEndpoint_RejectsZeroLength()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = new FreeLine { X1Mm = 10, Y1Mm = 30, X2Mm = 10.5, Y2Mm = 30 };   // 長さ0.5mm
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;   // 始点選択中

        // 始点(X1=10)を終点(X2=10.5)へ0.6mm近づけると長さが1.0mm未満になる
        bool resized = vm.ResizeSelectedFreeLineEndpoint(0.6, 0);

        Assert.False(resized);
        Assert.Equal(10, line.X1Mm);
    }

    // ---- ドラッグ(BeginDragFreeLine/UpdateDragFreeLine/ConfirmDragFreeLine/CancelDragFreeLine) ----

    [Fact]
    public void DragFreeLine_Move_UpdatesModelDuringDragAndMarksDirtyOnConfirm()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;

        vm.BeginDragFreeLine(line, isEndpoint: false, isStart: false, startXMm: 25, startYMm: 30, maxXMm: 1000, maxYMm: 1000);
        Assert.False(vm.IsDirty);

        vm.UpdateDragFreeLine(currentXMm: 30, currentYMm: 32);   // +5,+2

        Assert.Equal(15, line.X1Mm);
        Assert.Equal(32, line.Y1Mm);
        Assert.Equal(45, line.X2Mm);
        Assert.Equal(32, line.Y2Mm);
        Assert.False(vm.IsDirty);

        vm.ConfirmDragFreeLine();

        Assert.True(vm.IsDirty);
        Assert.False(vm.IsDraggingFreeLine);
    }

    [Fact]
    public void DragFreeLine_ResizeEndpoint_PreservesHorizontalOrientation()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();   // X1=10,Y1=30 - X2=40,Y2=30
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;

        vm.BeginDragFreeLine(line, isEndpoint: true, isStart: true, startXMm: 10, startYMm: 30, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateDragFreeLine(currentXMm: 15, currentYMm: 35);   // Y方向にもズレて掴んだ想定

        Assert.Equal(15, line.X1Mm);
        Assert.Equal(30, line.Y1Mm);   // 水平線なのでY1は動かない(向きを保つ)
    }

    [Fact]
    public void DragFreeLine_Cancel_RestoresOriginalPositionWithoutMarkingDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;

        vm.BeginDragFreeLine(line, isEndpoint: false, isStart: false, startXMm: 25, startYMm: 30, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateDragFreeLine(currentXMm: 30, currentYMm: 30);
        Assert.Equal(15, line.X1Mm);

        vm.CancelDragFreeLine();

        Assert.Equal(10, line.X1Mm);
        Assert.Equal(40, line.X2Mm);
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsDraggingFreeLine);
    }

    // T-041増分7隠密レビュー所見AA対応: グリッド・ページ境界へのクランプ回帰テスト
    [Fact]
    public void DragFreeLine_Move_ClampsToPageBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();   // X1=10,Y1=30 - X2=40,Y2=30
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;

        vm.BeginDragFreeLine(line, isEndpoint: false, isStart: false, startXMm: 25, startYMm: 30, maxXMm: 50, maxYMm: 1000);
        vm.UpdateDragFreeLine(currentXMm: 125, currentYMm: 30);   // +100を試みても境界50で止まる

        Assert.Equal(20, line.X1Mm);
        Assert.Equal(50, line.X2Mm);
    }

    // ---- 所見A横展開: ドラッグ中の外部要因(Delete/文書差し替え)による強制クリア ----

    [Fact]
    public void SelectedFreeLineAssignment_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;
        vm.BeginDragFreeLine(line, isEndpoint: false, isStart: false, startXMm: 25, startYMm: 30, maxXMm: 1000, maxYMm: 1000);
        Assert.True(vm.IsDraggingFreeLine);

        bool deleted = vm.DeleteSelectedFreeLine();

        Assert.True(deleted);
        Assert.False(vm.IsDraggingFreeLine);
        var ex = Record.Exception(() => vm.UpdateDragFreeLine(currentXMm: 30, currentYMm: 30));
        Assert.Null(ex);
        Assert.Equal(10, line.X1Mm);   // 書き換わっていない
    }

    [Fact]
    public void ReplaceDocument_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        var line = MakeHorizontal();
        vm.CurrentSheet!.FreeLines.Add(line);
        vm.SelectedFreeLine = line;
        vm.BeginDragFreeLine(line, isEndpoint: false, isStart: false, startXMm: 25, startYMm: 30, maxXMm: 1000, maxYMm: 1000);

        vm.NewDocument();

        Assert.False(vm.IsDraggingFreeLine);
        Assert.False(vm.IsDirty);
        var ex = Record.Exception(() => vm.UpdateDragFreeLine(currentXMm: 30, currentYMm: 30));
        Assert.Null(ex);
    }
}
