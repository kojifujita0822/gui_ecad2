using System.Windows.Threading;
using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-034: SheetNavigationViewModelのMarkDirty呼び忘れ検出。T-045(P-016対応)で
/// IDispatcherService抽象化を導入し、AddCommand/RenameCommandのWPF Application直接依存を
/// 解消した。CreateViewModel()(ViewModelTestBase)がImmediateDispatcherService(即時同期実行)を
/// 注入するため、従来try/catchで握りつぶしていたNullReferenceExceptionが発生しなくなり、
/// BeginInvoke経由の選択ハイライト同期(SelectedSheet設定・RefreshSelectedSheet)まで
/// 直接検証できるようになった。ただし本番<see cref="WpfDispatcherService"/>は実際のWPF
/// Dispatcherへ非同期でキューイングするのに対し、<see cref="ImmediateDispatcherService"/>は
/// actionを即時同期実行するため、タイミング特性が異なる(増分A補遺、隠密軽微指摘)。
/// </summary>
public class SheetNavigationViewModelTests : ViewModelTestBase
{
    [Fact]
    public void DeleteCommand_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        // 隠密レビュー指摘: CurrentSheetIndexを明示的に切替えないと既定値0のまま
        // (追加した「シート2」ではなく「シート1」が削除される)、テストの意図(追加した
        // シートを削除する)と実装が食い違う。
        vm.CurrentSheetIndex = 1;

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void DeleteCommand_CanExecute_FalseWhenOnlyOneSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.SheetNavigation.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteCommand_CanExecute_TrueWhenMultipleSheets()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();

        Assert.True(vm.SheetNavigation.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void AddCommand_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        // T-041(殿裁定「案1」): AddCommandは(名前, 主回路か)のタプルを受け取る呼び出し規約に変更。
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        Assert.True(vm.IsDirty);
        // 追加したシートが選択状態になることを検証する(BeginInvoke経由の同期、クラスコメント参照)。
        Assert.Equal(vm.Document.Sheets[^1], vm.SheetNavigation.SelectedSheet);
    }

    /// <summary>
    /// 増分A補遺(隠密所見1、CONFIRMED)の回帰テスト。ImmediateDispatcherServiceはpriority引数を
    /// 無視して即時実行するため、AddCommand_MarksDirtyだけでは意図的に選ばれた
    /// DispatcherPriority.ContextIdle(T-026実機確認由来)が別の値に変わっても検出できなかった。
    /// </summary>
    [Fact]
    public void AddCommand_DispatchesSelectionSyncWithContextIdlePriority()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        Assert.Equal(DispatcherPriority.ContextIdle, Dispatcher.LastPriority);
    }

    /// <summary>
    /// 増分A補遺(隠密所見2、CONFIRMED)の回帰テスト。ImmediateDispatcherServiceが即時同期実行
    /// するため、AddCommand_MarksDirtyは最終状態(IsDirty)しか見ておらず、MarkDirty()が
    /// BeginInvoke呼び出し前(同期部分)からlambda内(非同期部分)へ移動しても検出できなかった。
    /// BeginInvoke呼び出し直前(action実行前)の時点で既にIsDirtyがtrueであることを検証する。
    /// </summary>
    [Fact]
    public void AddCommand_MarksDirtyBeforeDispatchingSelectionSync()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        bool isDirtyWhenDispatched = false;
        Dispatcher.BeforeInvoke = () => isDirtyWhenDispatched = vm.IsDirty;

        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        Assert.True(isDirtyWhenDispatched);
    }

    [Fact]
    public void AddCommand_WithMainCircuitTrue_CreatesMainCircuitSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.AddCommand.Execute(("主回路シート", true));

        var addedSheet = vm.Document.Sheets[^1];
        Assert.Equal("主回路シート", addedSheet.Name);
        Assert.True(addedSheet.MainCircuit);
    }

    [Fact]
    public void AddCommand_WithBlankName_FallsBackToAutoNumberedName()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.AddCommand.Execute(("  ", false));

        var addedSheet = vm.Document.Sheets[^1];
        Assert.Equal("シート2", addedSheet.Name);
    }

    /// <summary>
    /// T-041増分5隠密レビュー指摘(観点3 CONFIRMED重大)の回帰テスト。2シート中の先頭(表示中)を
    /// 削除すると、後続シートが繰り上がりCurrentSheetIndexの数値は0のまま変化しない
    /// (Math.Min(0, Sheets.Count-1)=0)。この「数値が偶然一致したまま実体が差し替わる」経路で
    /// 記入中状態(_connectorDraft)が残留し、削除後の(実体としては別の)シートへ誤って確定される
    /// 実害が無いことを検証する。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhileDraftingConnector_WhenIndexNumberStaysSame_CancelsDraftAndPreventsCrossSheetLeak()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        // CurrentSheetIndexは既定の0のまま(先頭シートを表示中)。
        vm.SelectedCell = new GridPos(0, 0);
        vm.BeginConnectorDraft();
        vm.MoveConnectorDraftRow(1);

        // 表示中の先頭シートを削除する。後続シートがindex0へ繰り上がり、
        // CurrentSheetIndex = Math.Min(0, Sheets.Count-1=0) = 0 (数値上は変化なし)。
        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.ConnectorDraftPreview);
        Assert.Empty(vm.CurrentSheet!.Connectors);
    }

    /// <summary>同上、自由線(FreeLine)版。</summary>
    [Fact]
    public void DeleteCommand_WhileDraftingFreeLine_WhenIndexNumberStaysSame_CancelsDraftAndPreventsCrossSheetLeak()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2(主回路)",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
            MainCircuit = true,
        });
        vm.SheetNavigation.ResetSheets();
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.FreeLineDraftPreview);
        bool confirmed = vm.ConfirmFreeLineDraft();
        Assert.False(confirmed);
        Assert.Empty(vm.CurrentSheet!.FreeLines);
    }

    /// <summary>
    /// T-041増分5隠密レビュー指摘(往復3周目、テストカバレッジの隙間対応)。往復1周目の回帰テスト2件は
    /// いずれも削除前にSelectedCellを明示セットしていたため、SelectedCell自身のPropertyChanged
    /// (null→値、削除でnullに戻る際に発火)がCurrentSheetの変更通知漏れを覆い隠してしまい、症状1
    /// (削除でindex数値が変化しない場合にCurrentSheetのPropertyChangedが発火せず再描画が飛ぶ)を
    /// 検出できていなかった。削除前にSelectedCellが既にnull(セル未選択のまま削除する、最も基本的な
    /// 操作)というケースで、CurrentSheetのPropertyChangedが確実に発火することを直接検証する。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhenIndexNumberStaysSameAndSelectedCellAlreadyNull_RaisesCurrentSheetChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        // CurrentSheetIndexは既定の0のまま(先頭シートを表示中)。SelectedCellは未選択(null)のまま。
        Assert.Null(vm.SelectedCell);

        bool currentSheetChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.CurrentSheet)) currentSheetChanged = true;
        };

        // 表示中の先頭シートを削除する。後続シートがindex0へ繰り上がり、
        // CurrentSheetIndex = Math.Min(0, Sheets.Count-1=0) = 0 (数値上は変化なし)。
        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.True(currentSheetChanged);
    }

    [Fact]
    public void RenameCommand_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        bool selectedSheetChanged = false;
        vm.SheetNavigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SheetNavigation.SelectedSheet)) selectedSheetChanged = true;
        };

        vm.SheetNavigation.RenameCommand.Execute("新シート名");

        Assert.True(vm.IsDirty);
        // RefreshSelectedSheet()経由でSelectedSheetのPropertyChangedが発火することを検証する
        // (BeginInvoke経由の同期、クラスコメント参照)。
        Assert.True(selectedSheetChanged);
    }

    /// <summary>増分A補遺(隠密所見1、CONFIRMED)の回帰テスト。RenameCommand版。</summary>
    [Fact]
    public void RenameCommand_DispatchesRefreshWithContextIdlePriority()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.RenameCommand.Execute("新シート名");

        Assert.Equal(DispatcherPriority.ContextIdle, Dispatcher.LastPriority);
    }

    /// <summary>増分A補遺(隠密所見2、CONFIRMED)の回帰テスト。RenameCommand版。</summary>
    [Fact]
    public void RenameCommand_MarksDirtyBeforeDispatchingRefresh()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        bool isDirtyWhenDispatched = false;
        Dispatcher.BeforeInvoke = () => isDirtyWhenDispatched = vm.IsDirty;

        vm.SheetNavigation.RenameCommand.Execute("新シート名");

        Assert.True(isDirtyWhenDispatched);
    }
}
