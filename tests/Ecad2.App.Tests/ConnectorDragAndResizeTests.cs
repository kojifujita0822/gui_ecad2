using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分7: 縦コネクタのドラッグ(本体移動/端点リサイズ)・キーボード等価操作(平行移動/
/// Tab+Shift矢印での端点伸縮)のViewModelロジックの回帰テスト。増分1がテスト0件で4件の見落としを
/// 招いた反省を踏まえ、View(マウス/キーボード操作)を介さずViewModel単体で検証する。
/// </summary>
public class ConnectorDragAndResizeTests : ViewModelTestBase
{
    private static VerticalConnector MakeConnector() => new() { Column = 4, TopRow = 3, BottomRow = 6 };

    // ---- MoveSelectedConnector(キーボード平行移動、行方向) ----

    [Fact]
    public void MoveSelectedConnector_ShiftsTopAndBottomByDeltaAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedCell = null;
        vm.SelectedConnector = c;

        bool moved = vm.MoveSelectedConnector(1);

        Assert.True(moved);
        Assert.Equal(4, c.TopRow);
        Assert.Equal(7, c.BottomRow);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void MoveSelectedConnector_ClampsAtGridBoundsWithoutDistortingSpan()
    {
        var vm = CreateViewModel();
        vm.NewDocument();   // GridSpec既定Rows=10(行0〜9)
        var c = new VerticalConnector { Column = 4, TopRow = 0, BottomRow = 2 };
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        bool moved = vm.MoveSelectedConnector(-1);   // 既にTopRow=0、これ以上は上へ動けない

        Assert.False(moved);
        Assert.Equal(0, c.TopRow);
        Assert.Equal(2, c.BottomRow);   // 間隔(span=2)が歪んでいないこと
    }

    [Fact]
    public void MoveSelectedConnector_WithoutSelection_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.MoveSelectedConnector(1));
        Assert.False(vm.IsDirty);
    }

    // ---- MoveSelectedConnectorColumn(キーボード平行移動、列方向) ----

    [Fact]
    public void MoveSelectedConnectorColumn_ShiftsColumnAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        bool moved = vm.MoveSelectedConnectorColumn(1);

        Assert.True(moved);
        Assert.Equal(5, c.Column);
        Assert.True(vm.IsDirty);
    }

    // ---- ResizeSelectedConnectorEndpoint(Tab+Shift矢印、端点伸縮) ----

    [Fact]
    public void ResizeSelectedConnectorEndpoint_WhenStartSelected_MovesTopRowOnly()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;   // 選択直後はSelectedEndpointIsStart=true(既定)

        bool resized = vm.ResizeSelectedConnectorEndpoint(-1);

        Assert.True(resized);
        Assert.Equal(2, c.TopRow);
        Assert.Equal(6, c.BottomRow);   // Bottom側は不変
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void ResizeSelectedConnectorEndpoint_WhenEndSelected_MovesBottomRowOnly()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.ToggleSelectedEndpoint();   // 始点→終点へ切替え

        bool resized = vm.ResizeSelectedConnectorEndpoint(1);

        Assert.True(resized);
        Assert.Equal(3, c.TopRow);   // Top側は不変
        Assert.Equal(7, c.BottomRow);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void ResizeSelectedConnectorEndpoint_DoesNotInvertTopAndBottom()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = new VerticalConnector { Column = 4, TopRow = 3, BottomRow = 4 };
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;   // 始点(Top)選択中

        bool resized = vm.ResizeSelectedConnectorEndpoint(1);   // Top(3)をBottom(4)より下げようとする

        Assert.False(resized);
        Assert.Equal(3, c.TopRow);
        Assert.Equal(4, c.BottomRow);
    }

    // ---- SelectedEndpointIsStart / ToggleSelectedEndpoint ----

    [Fact]
    public void SelectedConnectorAssignment_ResetsSelectedEndpointToStart()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c1 = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c1);
        vm.SelectedConnector = c1;
        vm.ToggleSelectedEndpoint();
        Assert.False(vm.SelectedEndpointIsStart);

        var c2 = new VerticalConnector { Column = 8, TopRow = 1, BottomRow = 2 };
        vm.CurrentSheet!.Connectors.Add(c2);
        vm.SelectedCell = null;
        vm.SelectedConnector = c2;   // 新規選択のたびに既定(始点)へリセット

        Assert.True(vm.SelectedEndpointIsStart);
    }

    [Fact]
    public void ToggleSelectedEndpoint_WithoutSelection_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.ToggleSelectedEndpoint();

        Assert.True(vm.SelectedEndpointIsStart);   // 既定のまま変化しない
    }

    // ---- ドラッグ(BeginDragConnector/UpdateDragConnector/ConfirmDragConnector/CancelDragConnector) ----

    [Fact]
    public void DragConnector_Move_UpdatesModelDuringDragAndMarksDirtyOnConfirm()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3);
        Assert.False(vm.IsDirty);   // ドラッグ中はまだMarkDirty()しない

        vm.UpdateDragConnector(currentRow: 5);   // +2行

        Assert.Equal(5, c.TopRow);
        Assert.Equal(8, c.BottomRow);
        Assert.False(vm.IsDirty);   // 確定前はまだ

        vm.ConfirmDragConnector();

        Assert.True(vm.IsDirty);
        Assert.False(vm.IsDraggingConnector);
    }

    [Fact]
    public void DragConnector_ResizeTop_ClampsAtBottomRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: true, isTop: true, startRow: 3);
        vm.UpdateDragConnector(currentRow: 10);   // Bottom(6)を超えて下げようとする

        Assert.Equal(5, c.TopRow);   // Bottom-1でクランプ(ゼロ長禁止)
        Assert.Equal(6, c.BottomRow);
    }

    [Fact]
    public void DragConnector_Cancel_RestoresOriginalPositionWithoutMarkingDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3);
        vm.UpdateDragConnector(currentRow: 6);   // +3行、途中まで動かす
        Assert.Equal(6, c.TopRow);

        vm.CancelDragConnector();

        Assert.Equal(3, c.TopRow);   // 開始時の位置へ復元
        Assert.Equal(6, c.BottomRow);
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsDraggingConnector);
    }

    [Fact]
    public void ConfirmDragConnector_WhenPositionUnchanged_DoesNotMarkDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3);
        vm.UpdateDragConnector(currentRow: 3);   // 移動量ゼロ(クリックのみ相当)
        vm.ConfirmDragConnector();

        Assert.False(vm.IsDirty);
    }

    // ---- 所見A: ドラッグ中の外部要因(Delete/シート切替/文書差し替え)による強制クリア ----

    [Fact]
    public void SelectedConnectorAssignment_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3);
        Assert.True(vm.IsDraggingConnector);

        // Delete相当: DeleteSelectedConnectorがSelectedConnector=nullを代入する経路。
        bool deleted = vm.DeleteSelectedConnector();

        Assert.True(deleted);
        Assert.False(vm.IsDraggingConnector);
        // 強制クリア後、UpdateDragConnectorを呼んでも削除済みの実体を書き換えない(例外も起きない)。
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 8));
        Assert.Null(ex);
        Assert.Equal(3, c.TopRow);   // 書き換わっていない
    }

    [Fact]
    public void SheetSwitch_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3);

        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;   // SelectedCell=null経由でSelectedConnectorのsetterへ波及する

        Assert.False(vm.IsDraggingConnector);
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 8));
        Assert.Null(ex);
    }

    [Fact]
    public void ReplaceDocument_ForceCancelsInProgressDrag()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3);

        vm.NewDocument();   // ReplaceDocument経由

        Assert.False(vm.IsDraggingConnector);
        Assert.False(vm.IsDirty);   // 新規文書がドラッグの残骸で理由なく未保存化しない
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 8));
        Assert.Null(ex);
    }

    // ---- 所見B: 外部データ由来でTopRow>=BottomRowが既に崩れているケースへの防御 ----

    [Fact]
    public void UpdateDragConnector_ResizeTop_WithBottomRowAtZero_DoesNotThrowAndDoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        // 手編集ファイル等由来の不正データ(BottomRow=0、Top操作の下限クランプがmin>maxになりうる)。
        var c = new VerticalConnector { Column = 4, TopRow = 5, BottomRow = 0 };
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: true, isTop: true, startRow: 5);
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 2));

        Assert.Null(ex);
        Assert.Equal(5, c.TopRow);   // 直せない状態のため変更されない
    }

    [Fact]
    public void UpdateDragConnector_ResizeBottom_WithTopRowAtGridMax_DoesNotThrowAndDoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();   // GridSpec既定Rows=10(行0〜9)
        var c = new VerticalConnector { Column = 4, TopRow = 9, BottomRow = 5 };
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: true, isTop: false, startRow: 5);
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 7));

        Assert.Null(ex);
        Assert.Equal(5, c.BottomRow);
    }

    [Fact]
    public void ResizeSelectedConnectorEndpoint_WithInvertedTopBottom_DoesNotThrowAndDoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = new VerticalConnector { Column = 4, TopRow = 5, BottomRow = 0 };
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;   // 始点(Top)選択中

        var ex = Record.Exception(() => vm.ResizeSelectedConnectorEndpoint(-1));

        Assert.Null(ex);
        Assert.Equal(5, c.TopRow);
    }
}
