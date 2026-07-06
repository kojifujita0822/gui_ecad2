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

        try { vm.SheetNavigation.AddCommand.Execute(null); }
        catch (NullReferenceException) { /* Application.Current.Dispatcher依存、P-016まで既知の制約 */ }

        Assert.True(vm.IsDirty);
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
