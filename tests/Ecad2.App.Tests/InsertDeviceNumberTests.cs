using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// 「機器番号を挿入（以降を順送り）」のViewModel側のテスト（殿ご下命2026-10-09）。
/// 送る規則そのものは Core 側の <c>DeviceNumberShifterTests</c> が測る。ここで測るのは
/// 選択要素を起点にすること・Undo が一回で戻ること・実行できぬ時に図面へ触れぬこと。
/// </summary>
public class InsertDeviceNumberTests : ViewModelTestBase
{
    /// <summary>組込みa接点を行ごとに置き、順に機器名を付けた図面を作る。</summary>
    private MainWindowViewModel CreateViewModelWithDevices(params string[] deviceNames)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        for (int row = 0; row < deviceNames.Length; row++)
        {
            vm.SelectedCell = new GridPos(row, 0);
            vm.PlaceElementAtSelectedCell(ElementKind.ContactNO, orient: null);
            vm.SelectedElementDeviceName = deviceNames[row];
        }
        return vm;
    }

    private static string?[] NamesOf(MainWindowViewModel vm) =>
        vm.Document.Sheets[0].Elements.OrderBy(e => e.Pos.Row).Select(e => e.DeviceName).ToArray();

    [Fact]
    public void InsertDeviceNumberAtSelectedElement_ShiftsFromSelectedName()
    {
        var vm = CreateViewModelWithDevices("CR1", "CR2", "CR3", "CR4", "CR5", "CR6", "CR7");
        vm.SelectedCell = new GridPos(3, 0);

        bool changed = vm.InsertDeviceNumberAtSelectedElement();

        Assert.True(changed);
        Assert.Equal(new[] { "CR1", "CR2", "CR3", "CR5", "CR6", "CR7", "CR8" }, NamesOf(vm));
        // 選択中の要素は送られた後の名を見せる。
        Assert.Equal("CR5", vm.SelectedElementDeviceName);
        // 機器表も揃って送られ、CR4 が空いておる。
        Assert.False(vm.Document.Devices.ByName.ContainsKey("CR4"));
        Assert.True(vm.Document.Devices.ByName.ContainsKey("CR8"));
    }

    [Fact]
    public void InsertDeviceNumberAtSelectedElement_ThenUndoOnce_RestoresAllNames()
    {
        var vm = CreateViewModelWithDevices("CR4", "CR5", "CR6");
        vm.SelectedCell = new GridPos(0, 0);
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        vm.InsertDeviceNumberAtSelectedElement();

        Assert.Equal(undoDepthBefore + 1, vm.UndoManager.UndoDepth);
        vm.UndoCommand.Execute(null);
        Assert.Equal(new[] { "CR4", "CR5", "CR6" }, NamesOf(vm));
    }

    [Theory]
    [InlineData("PB")]
    [InlineData("")]
    public void InsertDeviceNumberAtSelectedElement_SelectedNameNotNumbered_DoesNothing(string selectedName)
    {
        var vm = CreateViewModelWithDevices("CR4", "CR5", selectedName);
        vm.SelectedCell = new GridPos(2, 0);
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        bool changed = vm.InsertDeviceNumberAtSelectedElement();

        Assert.False(changed);
        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);
        Assert.Equal(new[] { "CR4", "CR5", selectedName.Length > 0 ? selectedName : null }, NamesOf(vm));
    }

    [Fact]
    public void InsertDeviceNumberAtSelectedElement_NoElementSelected_DoesNothing()
    {
        var vm = CreateViewModelWithDevices("CR4", "CR5");
        vm.SelectedCell = new GridPos(5, 0);
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        Assert.False(vm.InsertDeviceNumberAtSelectedElement());
        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);
        Assert.Equal(new[] { "CR4", "CR5" }, NamesOf(vm));
    }

    [Fact]
    public void InsertDeviceNumberAtSelectedElement_TargetsCollide_DoesNothingAndReportsReason()
    {
        var vm = CreateViewModelWithDevices("CR9", "CR09");
        vm.SelectedCell = new GridPos(0, 0);
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        Assert.False(vm.InsertDeviceNumberAtSelectedElement());

        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);
        Assert.Equal(new[] { "CR9", "CR09" }, NamesOf(vm));
        Assert.Contains("挿入できません", vm.StatusMessage);
    }

    /// <summary>殿のお示しの例。CR1 を削除した後、CR2 を選んで詰めれば CR2 以降が一つずつ前へ出る。</summary>
    [Fact]
    public void RemoveDeviceNumberAtSelectedElement_AfterDeletingLowerDevice_ShiftsDown()
    {
        var vm = CreateViewModelWithDevices("CR1", "CR2", "CR3");
        vm.SelectedCell = new GridPos(0, 0);
        Assert.True(vm.DeleteSelectedElement());
        vm.SelectedCell = new GridPos(1, 0);
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        bool changed = vm.RemoveDeviceNumberAtSelectedElement();

        Assert.True(changed);
        Assert.Equal(new[] { "CR1", "CR2" }, NamesOf(vm));
        Assert.Equal("CR1", vm.SelectedElementDeviceName);
        Assert.False(vm.Document.Devices.ByName.ContainsKey("CR3"));
        // Undo 一回で詰める前へ戻る（削除までは巻き戻らぬ）。
        Assert.Equal(undoDepthBefore + 1, vm.UndoManager.UndoDepth);
        vm.UndoCommand.Execute(null);
        Assert.Equal(new[] { "CR2", "CR3" }, NamesOf(vm));
    }

    /// <summary>詰める先の機器がまだ残っておれば実行せず、理由を出す。</summary>
    [Fact]
    public void RemoveDeviceNumberAtSelectedElement_TargetStillInUse_DoesNothingAndReportsReason()
    {
        var vm = CreateViewModelWithDevices("CR1", "CR2", "CR3");
        vm.SelectedCell = new GridPos(1, 0);
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        Assert.False(vm.RemoveDeviceNumberAtSelectedElement());

        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);
        Assert.Equal(new[] { "CR1", "CR2", "CR3" }, NamesOf(vm));
        Assert.Contains("詰められません", vm.StatusMessage);
    }

    [Fact]
    public void InsertDeviceNumberAtSelectedElement_InTestMode_DoesNothing()
    {
        var vm = CreateViewModelWithDevices("CR4", "CR5");
        vm.SelectedCell = new GridPos(0, 0);
        vm.IsTestMode = true;

        Assert.False(vm.InsertDeviceNumberAtSelectedElement());
        Assert.Equal(new[] { "CR4", "CR5" }, NamesOf(vm));
    }
}
