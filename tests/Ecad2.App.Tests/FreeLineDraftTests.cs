using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分5: 自由線(FreeLine)手動記入(F9横線/sF9縦線→矢印キー調整→Enter確定/Esc取消)の
/// ViewModelロジックの回帰テスト。増分2隠密レビュー(観点3 CONFIRMED)で「記入中状態がSelectedCellの
/// setter経由で確実にクリアされるか」が実害を伴うバグの温床だったため、シート切替絡みのケースを
/// 手厚く検証する。
/// </summary>
public class FreeLineDraftTests : ViewModelTestBase
{
    private static Sheet MainCircuitSheet(MainWindowViewModel vm)
    {
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        return vm.CurrentSheet!;
    }

    [Fact]
    public void BeginFreeLineDraft_Horizontal_StartsPlaceLineModeWithZeroLengthPreview()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedCell = new GridPos(2, 3);

        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);

        Assert.Equal(ToolMode.PlaceLine, vm.Tool.Mode);
        Assert.True(vm.IsFreeLineDraftHorizontal);
        var preview = vm.FreeLineDraftPreview;
        Assert.NotNull(preview);
        Assert.Equal(47.0, preview!.X1Mm);
        Assert.Equal(47.0, preview.X2Mm);
        Assert.Equal(38.5, preview.Y1Mm);
        Assert.Equal(38.5, preview.Y2Mm);
    }

    [Fact]
    public void MoveFreeLineDraftEnd_Horizontal_ExtendsAlongXOnly()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);

        vm.MoveFreeLineDraftEnd(2);

        var preview = vm.FreeLineDraftPreview!;
        Assert.Equal(47.0, preview.X1Mm);
        Assert.Equal(65.0, preview.X2Mm);   // 47.0 + 2*9.0
        Assert.Equal(38.5, preview.Y1Mm);
        Assert.Equal(38.5, preview.Y2Mm);
    }

    [Fact]
    public void MoveFreeLineDraftEnd_Vertical_ExtendsAlongYOnly()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: false, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);

        vm.MoveFreeLineDraftEnd(1);

        var preview = vm.FreeLineDraftPreview!;
        Assert.Equal(47.0, preview.X1Mm);
        Assert.Equal(47.0, preview.X2Mm);
        Assert.Equal(38.5, preview.Y1Mm);
        Assert.Equal(47.5, preview.Y2Mm);   // 38.5 + 1*9.0
    }

    [Fact]
    public void ConfirmFreeLineDraft_WithZeroLength_ReturnsFalseAndKeepsDrafting()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);

        bool confirmed = vm.ConfirmFreeLineDraft();

        Assert.False(confirmed);
        Assert.Equal(ToolMode.PlaceLine, vm.Tool.Mode);
        Assert.Empty(vm.CurrentSheet!.FreeLines);
    }

    [Fact]
    public void ConfirmFreeLineDraft_AfterExtending_AddsFreeLineAndReturnsToSelectMode()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        bool confirmed = vm.ConfirmFreeLineDraft();

        Assert.True(confirmed);
        Assert.True(vm.IsDirty);
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.FreeLineDraftPreview);
        var line = Assert.Single(vm.CurrentSheet!.FreeLines);
        Assert.Equal(47.0, line.X1Mm);
        Assert.Equal(65.0, line.X2Mm);
    }

    [Fact]
    public void ConfirmFreeLineDraft_ReturnsFalse_WhenCurrentSheetIsNotMainCircuit()
    {
        var vm = CreateViewModel();
        vm.NewDocument();   // 既定はMainCircuit=false(制御回路)
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        bool confirmed = vm.ConfirmFreeLineDraft();

        Assert.False(confirmed);
        Assert.Empty(vm.CurrentSheet!.FreeLines);
    }

    [Fact]
    public void CancelFreeLineDraft_DiscardsDraftAndReturnsToSelectMode()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        vm.CancelFreeLineDraft();

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.FreeLineDraftPreview);
        Assert.Empty(vm.CurrentSheet!.FreeLines);
    }

    /// <summary>
    /// T-041増分2隠密レビュー(観点3 CONFIRMED)と同型の回帰テスト。記入開始後にシートを切替えると、
    /// 切替先シートへ誤ってFreeLineが確定・混入する実害が無いことを検証する。
    /// </summary>
    [Fact]
    public void SwitchingCurrentSheetIndex_WhileDraftingFreeLine_CancelsDraftAndPreventsCrossSheetLeak()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2(制御回路)",
            Grid = new GridSpec { Rows = 5, Columns = 10 },
        });
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        vm.CurrentSheetIndex = 1;

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.FreeLineDraftPreview);
        bool confirmed = vm.ConfirmFreeLineDraft();
        Assert.False(confirmed);
        Assert.Empty(vm.CurrentSheet!.FreeLines);
    }

    [Fact]
    public void SettingSelectedCell_WhileDraftingFreeLine_CancelsDraft()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        vm.SelectedCell = new GridPos(5, 1);

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.FreeLineDraftPreview);
        Assert.Empty(vm.CurrentSheet!.FreeLines);
    }

    [Fact]
    public void ReplaceDocument_ClearsFreeLineDraft_OnNewDocument()
    {
        var vm = CreateViewModel();
        MainCircuitSheet(vm);
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        vm.NewDocument();

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.FreeLineDraftPreview);
    }
}
