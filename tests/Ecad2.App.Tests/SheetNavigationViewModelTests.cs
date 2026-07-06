using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-034: SheetNavigationViewModelのMarkDirty呼び忘れ検出。AddCommand/RenameCommandは
/// System.Windows.Application.Current.Dispatcherへ直接依存しており(選択ハイライト遅延反映)、
/// WPF Applicationが起動していないテストプロセスで実行するとNullReferenceExceptionになることを
/// 確認済み(ViewModelのテスト容易性=Window依存の分離、家老委任事項。詳細は家老への報告参照)。
/// そのためAddCommand経由を避け、Document.Sheetsへ直接追加した状態でDeleteCommand(Dispatcher非依存)
/// のみを検証する。
/// </summary>
public class SheetNavigationViewModelTests
{
    [Fact]
    public void DeleteCommand_MarksDirty()
    {
        var vm = new MainWindowViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.True(vm.IsDirty);
    }
}
