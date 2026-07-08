using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-034: SheetNavigationViewModelのMarkDirty呼び忘れ検出。AddCommand/RenameCommandは
/// System.Windows.Application.Current.Dispatcherへ直接依存しており(選択ハイライト遅延反映)、
/// WPF Applicationが起動していないテストプロセスで実行するとNullReferenceExceptionになることを
/// 確認済み(ViewModelのテスト容易性=Window依存の分離、家老委任事項。詳細は家老への報告参照)。
/// AddCommand/RenameCommandのMarkDirty呼び出しはNREの原因となるDispatcher.BeginInvoke呼び出し
/// より前に同期実行されるため、NREをtry/catchで握りつぶした上でMarkDirty検証のみ行う
/// (隠密レビューT-034往復1周目で実機実証済みの手法。P-016=Dispatcher依存の根本解消が
/// 完了するまでの暫定策)。
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
        try { vm.SheetNavigation.AddCommand.Execute(("シート2", false)); }
        catch (NullReferenceException) { /* Application.Current.Dispatcher依存、P-016まで既知の制約 */ }

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void AddCommand_WithMainCircuitTrue_CreatesMainCircuitSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        try { vm.SheetNavigation.AddCommand.Execute(("主回路シート", true)); }
        catch (NullReferenceException) { /* Application.Current.Dispatcher依存、P-016まで既知の制約 */ }

        var addedSheet = vm.Document.Sheets[^1];
        Assert.Equal("主回路シート", addedSheet.Name);
        Assert.True(addedSheet.MainCircuit);
    }

    [Fact]
    public void AddCommand_WithBlankName_FallsBackToAutoNumberedName()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        try { vm.SheetNavigation.AddCommand.Execute(("  ", false)); }
        catch (NullReferenceException) { /* Application.Current.Dispatcher依存、P-016まで既知の制約 */ }

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

        try { vm.SheetNavigation.RenameCommand.Execute("新シート名"); }
        catch (NullReferenceException) { /* Application.Current.Dispatcher依存、P-016まで既知の制約 */ }

        Assert.True(vm.IsDirty);
    }
}
