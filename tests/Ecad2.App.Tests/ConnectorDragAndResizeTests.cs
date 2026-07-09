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

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        Assert.False(vm.IsDirty);   // ドラッグ中はまだMarkDirty()しない

        vm.UpdateDragConnector(currentRow: 5, currentColumn: 4);   // +2行

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

        vm.BeginDragConnector(c, isEndpoint: true, isTop: true, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 10, currentColumn: 4);   // Bottom(6)を超えて下げようとする

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

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 6, currentColumn: 4);   // +3行、途中まで動かす
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

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 3, currentColumn: 4);   // 移動量ゼロ(クリックのみ相当)
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
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        Assert.True(vm.IsDraggingConnector);

        // T-045補遺2(Stryker棚卸し、ForceCancelIfAnyのnotify()生存ミュータント対応): 最終値
        // だけでなくPropertyChangedイベント自体が発火することを検証する。
        bool isDraggingConnectorChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsDraggingConnector)) isDraggingConnectorChanged = true;
        };

        // Delete相当: DeleteSelectedConnectorがSelectedConnector=nullを代入する経路。
        bool deleted = vm.DeleteSelectedConnector();

        Assert.True(deleted);
        Assert.False(vm.IsDraggingConnector);
        Assert.True(isDraggingConnectorChanged);
        // 強制クリア後、UpdateDragConnectorを呼んでも削除済みの実体を書き換えない(例外も起きない)。
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 8, currentColumn: 4));
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
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);

        bool isDraggingConnectorChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsDraggingConnector)) isDraggingConnectorChanged = true;
        };

        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;   // SelectedCell=null経由でSelectedConnectorのsetterへ波及する

        Assert.False(vm.IsDraggingConnector);
        Assert.True(isDraggingConnectorChanged);
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 8, currentColumn: 4));
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
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);

        bool isDraggingConnectorChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsDraggingConnector)) isDraggingConnectorChanged = true;
        };

        vm.NewDocument();   // ReplaceDocument経由

        Assert.False(vm.IsDraggingConnector);
        Assert.True(isDraggingConnectorChanged);
        Assert.False(vm.IsDirty);   // 新規文書がドラッグの残骸で理由なく未保存化しない
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 8, currentColumn: 4));
        Assert.Null(ex);
    }

    // ---- 所見Y: 強制クリア時にUpdateDrag*済みの半端な位置が復元されるか(旧実装=null化のみでは
    // 検出できない、忍者テストレビュー指摘) ----

    [Fact]
    public void SelectedConnectorAssignment_WithPositionChanged_RestoresOriginalPosition()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();   // TopRow=3, BottomRow=6
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 6, currentColumn: 4);   // +3行、位置をずらす
        Assert.Equal(6, c.TopRow);

        vm.DeleteSelectedConnector();

        // 旧実装(nullにするだけ)ではTopRow=6のまま残る。新実装は開始時位置へ復元する。
        Assert.Equal(3, c.TopRow);
        Assert.Equal(6, c.BottomRow);
    }

    [Fact]
    public void SheetSwitch_WithPositionChanged_RestoresOriginalPositionWithoutMarkingDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 6, currentColumn: 4);   // +3行、位置をずらす
        Assert.Equal(6, c.TopRow);

        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;

        // 所見Y: 復元せずnull化のみだと、生きたシート上のコネクタが半端な位置(TopRow=6)のまま
        // MarkDirty()もされず黙って確定してしまう。開始時位置(3)へ復元されIsDirtyもfalseのまま。
        Assert.Equal(3, c.TopRow);
        Assert.Equal(6, c.BottomRow);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void ReplaceDocument_WithPositionChanged_RestoresOriginalPositionWithoutMarkingDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 6, currentColumn: 4);   // +3行、位置をずらす

        vm.NewDocument();   // ReplaceDocument経由

        Assert.Equal(3, c.TopRow);   // 破棄済みオブジェクトだが位置復元されていること(旧実装との区別)
        Assert.Equal(6, c.BottomRow);
        Assert.False(vm.IsDirty);
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

        vm.BeginDragConnector(c, isEndpoint: true, isTop: true, startRow: 5, startColumn: 4);
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 2, currentColumn: 4));

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

        vm.BeginDragConnector(c, isEndpoint: true, isTop: false, startRow: 5, startColumn: 4);
        var ex = Record.Exception(() => vm.UpdateDragConnector(currentRow: 7, currentColumn: 4));

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

    // ---- P-039(殿裁定): VerticalConnectorの列(Column)ドラッグ対応。
    // 隠密テスト設計書(docs/ecad2-p039-test-design-onmitsu.md)に基づく、新制度初適用。 ----

    // 4.1 UpdateDragConnector(本体移動)でColumn方向が正しく更新される(設計書2.1表B1〜B8)。
    [Theory]
    [InlineData(10.0, 2.0, 20, 12.0)]       // B1: 中間値、正の小さいdelta
    [InlineData(10.0, -3.0, 20, 7.0)]       // B1: 中間値、負の小さいdelta
    [InlineData(0.0, -3.0, 20, 0.0)]        // B2: 下限、さらに左→クランプで下限維持
    [InlineData(0.0, 2.0, 20, 2.0)]         // B3: 下限、右へ離れる→クランプ不要
    [InlineData(20.0, 3.0, 20, 20.0)]       // B4: 上限、さらに右→クランプで上限維持
    [InlineData(20.0, -2.0, 20, 18.0)]      // B5: 上限、左へ離れる→クランプ不要
    [InlineData(10.0, -10.0, 20, 0.0)]      // B6: ちょうど下限に到達
    [InlineData(10.0, 10.0, 20, 20.0)]      // B7: ちょうど上限に到達
    [InlineData(10.0, 100.0, 20, 20.0)]     // B8: 境界を大きく超える→クランプ(上限)
    public void UpdateDragConnector_Move_UpdatesColumnWithClamp(double origColumn, double delta, int gridColumns, double expectedColumn)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Columns = gridColumns;
        var c = new VerticalConnector { Column = origColumn, TopRow = 3, BottomRow = 6 };
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        // startColumn=0に固定し、currentColumn=deltaとすることでdeltaColumn=deltaに一致させる。
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 0);
        vm.UpdateDragConnector(currentRow: 3, currentColumn: delta);

        Assert.Equal(expectedColumn, c.Column);
    }

    [Fact]
    public void UpdateDragConnector_Move_RowAndColumnAreIndependent()
    {
        var vm = CreateViewModel();
        vm.NewDocument();   // GridSpec既定Rows=10(行0〜9)
        var c = new VerticalConnector { Column = 10, TopRow = 0, BottomRow = 2 };   // TopRowは既に上限(0)
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 10);
        // Row方向はさらに上へ(クランプされ動けない)、Column方向は+5(独立して正常に動く)。
        vm.UpdateDragConnector(currentRow: 0, currentColumn: 15);

        Assert.Equal(0, c.TopRow);
        Assert.Equal(2, c.BottomRow);
        Assert.Equal(15, c.Column);
    }

    // 4.2 端点リサイズ時はColumn不変(殿裁定「VerticalConnectorは常に縦線のため端点伸縮は列に無関係」)。
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateDragConnector_Resize_NeverChangesColumn(bool isTop)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();   // Column=4, TopRow=3, BottomRow=6
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: true, isTop: isTop, startRow: isTop ? 3 : 6, startColumn: 4);
        vm.UpdateDragConnector(currentRow: isTop ? 4 : 5, currentColumn: 100);   // 列を大きく変えようとする

        Assert.Equal(4, c.Column);
    }

    // 4.3 ConfirmDragConnectorがColumn変化も検知する(設計書申し送り: 現状実装ではRED想定)。
    [Fact]
    public void ConfirmDragConnector_WhenOnlyColumnChanged_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();   // Column=4, TopRow=3, BottomRow=6
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 3, currentColumn: 6);   // Row系不変、Columnのみ変化
        vm.ConfirmDragConnector();

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void ConfirmDragConnector_WhenNothingChanged_DoesNotMarkDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 3, currentColumn: 4);   // Row系・Column系とも変化なし
        vm.ConfirmDragConnector();

        Assert.False(vm.IsDirty);
    }

    // 4.4 CancelDragConnectorがColumnも復元する(設計書申し送り: 現状実装ではRED想定)。
    [Fact]
    public void CancelDragConnector_RestoresColumnToOriginalPosition()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();   // Column=4
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;

        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 3, currentColumn: 10);
        Assert.Equal(10, c.Column);

        vm.CancelDragConnector();

        Assert.Equal(4, c.Column);
        Assert.False(vm.IsDirty);
    }

    // 4.5 ForceCancelDragConnectorIfAny(所見Y型)がColumnも復元する(Delete経由・シート切替経由)。
    [Fact]
    public void SelectedConnectorAssignment_ForceCancelRestoresColumn_ViaDelete()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();   // Column=4
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 3, currentColumn: 10);
        Assert.Equal(10, c.Column);

        vm.DeleteSelectedConnector();

        Assert.Equal(4, c.Column);
    }

    [Fact]
    public void SelectedConnectorAssignment_ForceCancelRestoresColumn_ViaSheetSwitch()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var c = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(c);
        vm.SelectedConnector = c;
        vm.BeginDragConnector(c, isEndpoint: false, isTop: false, startRow: 3, startColumn: 4);
        vm.UpdateDragConnector(currentRow: 3, currentColumn: 10);
        Assert.Equal(10, c.Column);

        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;

        Assert.Equal(4, c.Column);
        Assert.False(vm.IsDirty);
    }

    // 4.6 キーボード版(MoveSelectedConnectorColumn)と同じ結果に到達する(案Xの核心要件)。
    [Theory]
    [InlineData(10.0, 3.0)]     // 中間値
    [InlineData(0.0, -2.0)]     // 下限、さらに負→クランプされて動かない
    [InlineData(20.0, 5.0)]     // 上限、さらに正→クランプされて動かない
    [InlineData(1.0, -5.0)]     // 下限近傍を超えてクランプされる
    [InlineData(19.0, 5.0)]     // 上限近傍を超えてクランプされる
    public void DragAndKeyboardColumnMove_ConvergeToSameResult(double origColumn, double delta)
    {
        var vmDrag = CreateViewModel();
        vmDrag.NewDocument();   // GridSpec既定Columns=20
        var cDrag = new VerticalConnector { Column = origColumn, TopRow = 3, BottomRow = 6 };
        vmDrag.CurrentSheet!.Connectors.Add(cDrag);
        vmDrag.SelectedConnector = cDrag;
        vmDrag.BeginDragConnector(cDrag, isEndpoint: false, isTop: false, startRow: 3, startColumn: 0);
        vmDrag.UpdateDragConnector(currentRow: 3, currentColumn: delta);
        vmDrag.ConfirmDragConnector();

        var vmKey = CreateViewModel();
        vmKey.NewDocument();
        var cKey = new VerticalConnector { Column = origColumn, TopRow = 3, BottomRow = 6 };
        vmKey.CurrentSheet!.Connectors.Add(cKey);
        vmKey.SelectedConnector = cKey;
        vmKey.MoveSelectedConnectorColumn(delta);

        Assert.Equal(cKey.Column, cDrag.Column);
    }
}
