using System.Windows;
using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-155: LadderCanvas 側のリサイズハンドルのヒットテスト配線(<see cref="LadderCanvas"/> は
/// コードビハインド寄りだが、ハンドル座標の算出は純粋ゆえ STA で最小限だけ固定する)。
/// マウスの押下→BeginResizeFrame の結線そのものは忍者の実機確認が網。
/// </summary>
public class T155FrameResizeViewTests
{
    private const double MmToDip = 96.0 / 25.4;

    private static GroupFrame CreateFrame(MainWindowViewModel vm, int row, int col, int w, int h)
    {
        vm.BeginFrameDraft(new GridPos(row, col));
        for (int i = 1; i < w; i++) vm.AdjustFrameDraft(1, 0);
        for (int i = 1; i < h; i++) vm.AdjustFrameDraft(0, 1);
        vm.ConfirmFrameDraft();
        return vm.SelectedFrame!;
    }

    [Fact]
    public void 枠の左上角の点は左上ハンドルにヒットする()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;
            vm.NewDocument();
            var frame = CreateFrame(vm, 3, 4, 3, 2);

            var rectMm = window.LadderCanvasHost.FrameRectMm(frame);
            var topLeft = new Point(rectMm.X * MmToDip, rectMm.Y * MmToDip);

            Assert.Equal(FrameResizeHandle.TopLeft,
                window.LadderCanvasHost.HitTestFrameResizeHandle(topLeft, frame));
        });

    [Fact]
    public void 枠の右辺中点は右ハンドルにヒットする()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;
            vm.NewDocument();
            var frame = CreateFrame(vm, 3, 4, 4, 4);

            var rectMm = window.LadderCanvasHost.FrameRectMm(frame);
            var rightMid = new Point((rectMm.X + rectMm.Width) * MmToDip,
                                     (rectMm.Y + rectMm.Height / 2) * MmToDip);

            Assert.Equal(FrameResizeHandle.Right,
                window.LadderCanvasHost.HitTestFrameResizeHandle(rightMid, frame));
        });

    [Fact]
    public void 枠の内側の点はどのハンドルにもヒットしない()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;
            vm.NewDocument();
            var frame = CreateFrame(vm, 3, 4, 5, 5);

            var rectMm = window.LadderCanvasHost.FrameRectMm(frame);
            var center = new Point((rectMm.X + rectMm.Width / 2) * MmToDip,
                                   (rectMm.Y + rectMm.Height / 2) * MmToDip);

            Assert.Null(window.LadderCanvasHost.HitTestFrameResizeHandle(center, frame));
        });
}
