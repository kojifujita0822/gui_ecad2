using Ecad2.App.ViewModels;

namespace Ecad2.App.Tests;

/// <summary>
/// T-051: UndoCommand/RedoCommandのViewModel結合テスト。MVP対象範囲=SheetNavigationViewModelの
/// シート追加/削除のみ(殿裁定)。UndoManager自体の単体テストはUndoManagerTests参照。
/// 設計出典: docs/ecad2-t051-implementation-plan-samurai.md。
/// </summary>
public class UndoRedoCommandsTests : ViewModelTestBase
{
    [Fact]
    public void UndoCommand_CanExecute_ReturnsFalse_WhenNoHistory()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void RedoCommand_CanExecute_ReturnsFalse_WhenNoHistory()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void UndoCommand_CanExecute_ReturnsTrue_AfterAddCommand()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void UndoCommand_Execute_AfterAddCommand_RestoresSheetCount()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        int before = vm.Document.Sheets.Count;
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        vm.UndoCommand.Execute(null);

        Assert.Equal(before, vm.Document.Sheets.Count);
    }

    [Fact]
    public void UndoCommand_Execute_AfterDeleteCommand_RestoresDeletedSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Model.Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new Model.GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;
        int before = vm.Document.Sheets.Count;

        vm.SheetNavigation.DeleteCommand.Execute(null);
        vm.UndoCommand.Execute(null);

        Assert.Equal(before, vm.Document.Sheets.Count);
        Assert.Contains(vm.Document.Sheets, s => s.Name == "シート2");
    }

    [Fact]
    public void RedoCommand_Execute_AfterUndo_ReappliesAdd()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        int afterAdd = vm.Document.Sheets.Count;
        vm.UndoCommand.Execute(null);

        vm.RedoCommand.Execute(null);

        Assert.Equal(afterAdd, vm.Document.Sheets.Count);
    }

    /// <summary>Undo後に新規操作を行うと、分岐したRedo履歴は無効になりRedoできなくなること
    /// (UndoManagerTests.RecordSnapshot_ClearsRedoHistoryのViewModel結合版)。</summary>
    [Fact]
    public void RedoCommand_CanExecute_ReturnsFalse_AfterNewOperationFollowingUndo()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.UndoCommand.Execute(null);
        Assert.True(vm.RedoCommand.CanExecute(null)); // 前提: Undo直後はRedo可能

        vm.SheetNavigation.AddCommand.Execute(("シート3", false));

        Assert.False(vm.RedoCommand.CanExecute(null));
    }

    // 「Undo後もIsDirty=true」(開かれた論点2、殿裁定)は、IsDirtyのsetterがprivateで
    // テストからの直接リセットができず、かつ直前のAddCommand自体が既にMarkDirty()済みのため
    // Undo単独の効果としてRED証明可能な形では検証できない。ApplyUndoRedoSnapshot内の
    // MarkDirty()呼び出しはコード上の対応で足り、専用テストは追加しない(無意味なテストを
    // 追加しない方針)。
}
