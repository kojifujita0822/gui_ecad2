using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-092回帰テスト(隠密指摘P-094、T-090/T-091静的レビューの経過観察)。記入中ドラフト
/// (縦コネクタ/自由線/画像挿入)を保持したままAddRow/DeleteRow/Undo/Redoを実行すると、
/// ドラフトの行インデックスが無警告でクランプされ誤った行・別シートへ確定されうる。
/// 殿裁定=ブロック方式、T-091(F5〜F10のHasAnyDraft見落とし)と同型の横展開として
/// 4コマンドのCanExecuteへHasAnyDraft不許可を追加する(MainWindowViewModel.cs)。
/// RED証明手法: 各CanExecuteの`!HasAnyDraft &&`を一時的にコメントアウトしてテスト実行
/// →ドラフト種別3種×4コマンド=12ケース全てがtrueへ戻りRED(実測確認済み)。戻すとGREEN。
/// </summary>
public class T092FixTests : ViewModelTestBase
{
    [Theory]
    [InlineData("Connector")]
    [InlineData("FreeLine")]
    [InlineData("Image")]
    public void AddRowCommand_CanExecute_ReturnsFalse_WhenHasAnyDraft(string draftType)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        BeginDraft(vm, draftType);

        Assert.False(vm.AddRowCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("Connector")]
    [InlineData("FreeLine")]
    [InlineData("Image")]
    public void DeleteRowCommand_CanExecute_ReturnsFalse_WhenHasAnyDraft(string draftType)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        BeginDraft(vm, draftType);

        Assert.False(vm.DeleteRowCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("Connector")]
    [InlineData("FreeLine")]
    [InlineData("Image")]
    public void UndoCommand_CanExecute_ReturnsFalse_WhenHasAnyDraft(string draftType)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        Assert.True(vm.UndoCommand.CanExecute(null)); // 前提: ドラフト無しならUndo可能であること
        BeginDraft(vm, draftType);

        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("Connector")]
    [InlineData("FreeLine")]
    [InlineData("Image")]
    public void RedoCommand_CanExecute_ReturnsFalse_WhenHasAnyDraft(string draftType)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.UndoCommand.Execute(null);
        Assert.True(vm.RedoCommand.CanExecute(null)); // 前提: ドラフト無しならRedo可能であること
        BeginDraft(vm, draftType);

        Assert.False(vm.RedoCommand.CanExecute(null));
    }

    /// <summary>対照ケース(退行検知): ドラフト無しなら従来どおり許可されること。</summary>
    [Fact]
    public void AddRowCommand_CanExecute_ReturnsTrue_WhenNoDraft()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.True(vm.AddRowCommand.CanExecute(null));
    }

    /// <summary>対照ケース(退行検知): ドラフト無しなら従来どおり許可されること。</summary>
    [Fact]
    public void DeleteRowCommand_CanExecute_ReturnsTrue_WhenNoDraft()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;

        Assert.True(vm.DeleteRowCommand.CanExecute(null));
    }

    private static void BeginDraft(MainWindowViewModel vm, string draftType)
    {
        switch (draftType)
        {
            case "Connector":
                vm.SelectedCell = new GridPos(0, 1);
                vm.BeginConnectorDraft();
                break;
            case "FreeLine":
                vm.BeginFreeLineDraft(horizontal: true, startXMm: 10, startYMm: 10, stepMm: 5);
                break;
            case "Image":
                vm.BeginImageInsertDraft("dummy.png", widthMm: 20, heightMm: 20, xMm: 10, yMm: 10);
                break;
        }
    }
}
