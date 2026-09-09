using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.App.Tests;

/// <summary>
/// T-155(殿ご下命2026-09-09): グループ枠のリサイズ機能(8ハンドル・mm自由)と、作成/移動/リサイズの
/// ゴースト表示。ViewModel層(BeginResizeFrame/UpdateResizeFrame/ConfirmResizeFrame/CancelResizeFrame、
/// FrameDragPreview、ShiftFrameMm 経由の自由式追随)を検証する。ハンドルのヒットテスト・マウス配線は
/// コードビハインドのためテスト基盤が無く対象外(T-067と同事情)。
/// </summary>
public class T155FrameResizeTests : ViewModelTestBase
{
    // 実アプリの図面レンダラと同じ幾何(セル9mm・余白20mm)。
    private static readonly GridGeometry Geo = new(GridGeometry.DefaultCellMm, 20.0);

    private static GroupFrame CreateFrame(MainWindowViewModel vm, int row, int col, int w, int h)
    {
        vm.BeginFrameDraft(new GridPos(row, col));
        for (int i = 1; i < w; i++) vm.AdjustFrameDraft(1, 0);
        for (int i = 1; i < h; i++) vm.AdjustFrameDraft(0, 1);
        vm.ConfirmFrameDraft();
        return vm.SelectedFrame!;
    }

    // ---- リサイズ ----

    [Fact]
    public void ConfirmResizeFrame_右下ハンドルを広げるとVisualサイズが確定しUndo可能になる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: 100, startYMm: 100, startWMm: 40, startHMm: 30, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeFrame(mouseXMm: 175, mouseYMm: 160);   // 右下を (175,160) へ
        vm.ConfirmResizeFrame(Geo);

        Assert.Equal(100, frame.VisualXMm);
        Assert.Equal(100, frame.VisualYMm);
        Assert.Equal(75, frame.VisualWidthMm);    // 175 - 100
        Assert.Equal(60, frame.VisualHeightMm);   // 160 - 100
        Assert.True(vm.UndoCommand.CanExecute(null));
        Assert.False(vm.IsResizingFrame);
    }

    [Fact]
    public void UpdateResizeFrame_左上ハンドルは右下を固定して左上だけ動かす()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        vm.BeginResizeFrame(frame, FrameResizeHandle.TopLeft,
            startXMm: 100, startYMm: 100, startWMm: 40, startHMm: 40, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeFrame(mouseXMm: 120, mouseYMm: 130);

        var p = vm.FrameResizePreview!;
        Assert.Equal(120, p.VisualXMm);
        Assert.Equal(130, p.VisualYMm);
        Assert.Equal(20, p.VisualWidthMm);    // 右辺 140 は固定 → 140-120
        Assert.Equal(10, p.VisualHeightMm);   // 下辺 140 は固定 → 140-130
    }

    [Fact]
    public void UpdateResizeFrame_右辺ハンドルは横だけ変え縦は不変()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        vm.BeginResizeFrame(frame, FrameResizeHandle.Right,
            startXMm: 50, startYMm: 50, startWMm: 30, startHMm: 30, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeFrame(mouseXMm: 200, mouseYMm: 999);   // Yは無視されるはず

        var p = vm.FrameResizePreview!;
        Assert.Equal(50, p.VisualXMm);
        Assert.Equal(50, p.VisualYMm);
        Assert.Equal(150, p.VisualWidthMm);
        Assert.Equal(30, p.VisualHeightMm);
    }

    [Fact]
    public void UpdateResizeFrame_最小辺長より小さくはできない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 3, 3);

        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: 100, startYMm: 100, startWMm: 50, startHMm: 50, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeFrame(mouseXMm: 0, mouseYMm: 0);   // 潰そうとする

        var p = vm.FrameResizePreview!;
        Assert.Equal(GridGeometry.DefaultCellMm, p.VisualWidthMm);
        Assert.Equal(GridGeometry.DefaultCellMm, p.VisualHeightMm);
    }

    [Fact]
    public void UpdateResizeFrame_ページ境界の外へは広げられない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: 100, startYMm: 100, startWMm: 20, startHMm: 20, maxXMm: 300, maxYMm: 250);
        vm.UpdateResizeFrame(mouseXMm: 9999, mouseYMm: 9999);

        var p = vm.FrameResizePreview!;
        Assert.Equal(200, p.VisualWidthMm);    // 300 - 100
        Assert.Equal(150, p.VisualHeightMm);   // 250 - 100
    }

    [Fact]
    public void ConfirmResizeFrame_グリッド近似値も振り直される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        // 左上を X(2)=38mm, 行3の上辺へ、幅=4セル(36mm)・高さ=5セル(45mm)相当へ。
        double x = Geo.X(2), y = Geo.FrameTopMm(3);
        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: x, startYMm: y, startWMm: 18, startHMm: 18, maxXMm: 2000, maxYMm: 2000);
        vm.UpdateResizeFrame(x + 36, y + 45);
        vm.ConfirmResizeFrame(Geo);

        Assert.Equal(new GridPos(3, 2), frame.TopLeft);
        Assert.Equal(4, frame.Width);
        Assert.Equal(5, frame.Height);
    }

    [Fact]
    public void ConfirmResizeFrame_変化なしならUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: 100, startYMm: 100, startWMm: 30, startHMm: 30, maxXMm: 1000, maxYMm: 1000);
        vm.ConfirmResizeFrame(Geo);   // Updateを挟まず確定

        Assert.Null(frame.VisualWidthMm);
        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.CurrentSheet!.Frames);   // 枠作成そのものが戻る=リサイズのUndoは積まれていない
    }

    [Fact]
    public void CancelResizeFrame_モデルへ触れずゴーストを消す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: 100, startYMm: 100, startWMm: 30, startHMm: 30, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeFrame(200, 200);
        vm.CancelResizeFrame();

        Assert.False(vm.IsResizingFrame);
        Assert.Null(vm.FrameResizePreview);
        Assert.Null(frame.VisualWidthMm);
    }

    [Fact]
    public void SelectedFrameを外すとリサイズが中断される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: 100, startYMm: 100, startWMm: 30, startHMm: 30, maxXMm: 1000, maxYMm: 1000);
        vm.SelectedCell = new GridPos(0, 0);   // 選択を外す経路

        Assert.False(vm.IsResizingFrame);
        Assert.Null(frame.VisualWidthMm);
    }

    // ---- 移動のゴースト化・自由式の追随 ----

    [Fact]
    public void FrameDragPreview_ドラッグ中だけ非nullで確定前はモデル不変()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);

        Assert.Null(vm.FrameDragPreview);
        vm.BeginDragFrame(frame);
        vm.UpdateDragFrame(new GridPos(7, 8));

        Assert.Equal(new GridPos(7, 8), vm.FrameDragPreview!.TopLeft);
        Assert.Equal(new GridPos(5, 5), frame.TopLeft);   // 確定前

        vm.ConfirmDragFrame();
        Assert.Equal(new GridPos(7, 8), frame.TopLeft);
        Assert.Null(vm.FrameDragPreview);
    }

    [Fact]
    public void 自由式の枠を移動するとVisual座標もセル差分ぶん動く()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);
        // リサイズで自由式へ移行させる。
        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: 100, startYMm: 100, startWMm: 30, startHMm: 30, maxXMm: 2000, maxYMm: 2000);
        vm.UpdateResizeFrame(150, 140);
        vm.ConfirmResizeFrame(Geo);
        double x0 = frame.VisualXMm!.Value, y0 = frame.VisualYMm!.Value;

        vm.BeginDragFrame(frame);
        vm.UpdateDragFrame(new GridPos(frame.TopLeft.Row + 2, frame.TopLeft.Column + 3));
        vm.ConfirmDragFrame();

        Assert.Equal(x0 + 3 * GridGeometry.DefaultCellMm, frame.VisualXMm);
        Assert.Equal(y0 + 2 * GridGeometry.DefaultCellMm, frame.VisualYMm);
    }

    [Fact]
    public void 自由式の枠を矢印キー移動するとVisual座標も追随する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm, 5, 5, 2, 2);
        vm.BeginResizeFrame(frame, FrameResizeHandle.BottomRight,
            startXMm: 100, startYMm: 100, startWMm: 30, startHMm: 30, maxXMm: 2000, maxYMm: 2000);
        vm.UpdateResizeFrame(150, 140);
        vm.ConfirmResizeFrame(Geo);
        double x0 = frame.VisualXMm!.Value;

        vm.MoveSelectedFrame(0, 1);   // 右へ1セル

        Assert.Equal(x0 + GridGeometry.DefaultCellMm, frame.VisualXMm);
    }
}
