using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分2: 縦コネクタ手動記入(sF9→矢印キー調整→Enter確定/Esc取消)のViewModelロジックの
/// 回帰テスト。増分1がテスト0件で4件の見落としを招いた反省(docs/ecad2-t041-increment1-review
/// -onmitsu.md)を踏まえ、キーボード操作を介さずViewModel単体で検証する。
/// </summary>
public class ConnectorDraftTests : ViewModelTestBase
{
    [Fact]
    public void BeginConnectorDraft_WithSelectedCell_StartsPlaceConnectorModeWithZeroLengthPreview()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 5);

        vm.BeginConnectorDraft();

        Assert.Equal(ToolMode.PlaceConnector, vm.Tool.Mode);
        var preview = vm.ConnectorDraftPreview;
        Assert.NotNull(preview);
        Assert.Equal(3, preview!.TopRow);
        Assert.Equal(3, preview.BottomRow);
        Assert.Equal(5, preview.Column);
    }

    [Fact]
    public void BeginConnectorDraft_WithoutSelectedCell_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.BeginConnectorDraft();

        Assert.Null(vm.ConnectorDraftPreview);
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
    }

    [Fact]
    public void MoveConnectorDraftRow_ExtendsPreviewDownward()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 5);
        vm.BeginConnectorDraft();

        vm.MoveConnectorDraftRow(1);
        vm.MoveConnectorDraftRow(1);

        var preview = vm.ConnectorDraftPreview;
        Assert.Equal(3, preview!.TopRow);
        Assert.Equal(5, preview.BottomRow);
    }

    [Fact]
    public void MoveConnectorDraftRow_ExtendsPreviewUpwardAndKeepsTopBottomOrdered()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 5);
        vm.BeginConnectorDraft();

        vm.MoveConnectorDraftRow(-1);

        var preview = vm.ConnectorDraftPreview;
        Assert.Equal(2, preview!.TopRow);
        Assert.Equal(3, preview.BottomRow);
    }

    [Fact]
    public void MoveConnectorDraftRow_ClampsAtGridBounds()
    {
        var vm = CreateViewModel();
        vm.NewDocument();   // GridSpec既定Rows=10(行0〜9)
        vm.SelectedCell = new GridPos(9, 0);
        vm.BeginConnectorDraft();

        vm.MoveConnectorDraftRow(1);   // 行10は範囲外、9でクランプされ変化なし

        var preview = vm.ConnectorDraftPreview;
        Assert.Equal(9, preview!.TopRow);
        Assert.Equal(9, preview.BottomRow);
    }

    [Fact]
    public void MoveConnectorDraftColumn_MovesByWholeStepAndHalfStep()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 5);
        vm.BeginConnectorDraft();

        vm.MoveConnectorDraftColumn(1.0);
        Assert.Equal(6.0, vm.ConnectorDraftPreview!.Column);

        vm.MoveConnectorDraftColumn(-0.5);
        Assert.Equal(5.5, vm.ConnectorDraftPreview!.Column);
    }

    [Fact]
    public void ConfirmConnectorDraft_WithZeroLengthSpan_ReturnsFalseAndKeepsDrafting()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 5);
        vm.BeginConnectorDraft();

        bool confirmed = vm.ConfirmConnectorDraft();

        Assert.False(confirmed);
        Assert.Equal(ToolMode.PlaceConnector, vm.Tool.Mode);
        Assert.Empty(vm.CurrentSheet!.Connectors);
    }

    [Fact]
    public void ConfirmConnectorDraft_AfterExtending_AddsConnectorAndReturnsToSelectMode()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 5);
        vm.BeginConnectorDraft();
        vm.MoveConnectorDraftRow(1);

        bool confirmed = vm.ConfirmConnectorDraft();

        Assert.True(confirmed);
        Assert.True(vm.IsDirty);
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.ConnectorDraftPreview);
        var connector = Assert.Single(vm.CurrentSheet!.Connectors);
        Assert.Equal(5, connector.Column);
        Assert.Equal(3, connector.TopRow);
        Assert.Equal(4, connector.BottomRow);
    }

    [Fact]
    public void CancelConnectorDraft_DiscardsDraftAndReturnsToSelectMode()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 5);
        vm.BeginConnectorDraft();
        vm.MoveConnectorDraftRow(1);

        vm.CancelConnectorDraft();

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.ConnectorDraftPreview);
        Assert.Empty(vm.CurrentSheet!.Connectors);
    }
}
