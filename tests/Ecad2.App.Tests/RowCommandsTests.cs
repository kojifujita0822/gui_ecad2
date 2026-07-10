using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-055増分1: 末尾行の追加・削除(AddRowCommand/DeleteRowCommand)。上限(GridSpec.MaxRows=60)・
/// 下限(GridSpec.MinRows=1)のクランプ、および削除拒否(最終行に広義5種の要素が存在する場合、
/// 殿裁定2026-07-10)を検証する。
/// </summary>
public class RowCommandsTests : ViewModelTestBase
{
    [Fact]
    public void AddRowCommand_Execute_IncreasesRowsByOne()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        int before = vm.CurrentSheet!.Grid.Rows;

        vm.AddRowCommand.Execute(null);

        Assert.Equal(before + 1, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void AddRowCommand_Execute_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.AddRowCommand.Execute(null);

        Assert.True(vm.IsDirty);
    }

    /// <summary>
    /// 上限クランプのRED先行証明対象。Rows=60(上限)でExecuteしても61へ増えず60のままであること。
    /// RED証明手法: AddRowCommandのExecute内ガード(`sheet.Grid.Rows >= GridSpec.MaxRows`)を
    /// 一時的にコメントアウトしてテスト実行→61に増えてしまいRED(実測確認済み)。ガードを戻すとGREEN。
    /// </summary>
    [Fact]
    public void AddRowCommand_Execute_AtMaxRows_DoesNotExceedMax()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MaxRows;

        vm.AddRowCommand.Execute(null);

        Assert.Equal(GridSpec.MaxRows, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void AddRowCommand_CanExecute_ReturnsFalse_WhenAtMaxRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MaxRows;

        Assert.False(vm.AddRowCommand.CanExecute(null));
    }

    [Fact]
    public void AddRowCommand_CanExecute_ReturnsTrue_WhenBelowMaxRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MaxRows - 1;

        Assert.True(vm.AddRowCommand.CanExecute(null));
    }

    [Fact]
    public void AddRowCommand_CanExecute_ReturnsFalse_WhenNoCurrentSheet()
    {
        var vm = CreateViewModel();

        Assert.False(vm.AddRowCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteRowCommand_Execute_DecreasesRowsByOne()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(9, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void DeleteRowCommand_Execute_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;

        vm.DeleteRowCommand.Execute(null);

        Assert.True(vm.IsDirty);
    }

    /// <summary>
    /// 下限クランプのRED先行証明対象。Rows=1(下限)でExecuteしても0へ減らず1のままであること。
    /// RED証明手法: DeleteRowCommandのExecute内ガード(`sheet.Grid.Rows <= GridSpec.MinRows`)を
    /// 一時的にコメントアウトしてテスト実行→0へ減ってしまいRED(実測確認済み)。ガードを戻すとGREEN。
    /// </summary>
    [Fact]
    public void DeleteRowCommand_Execute_AtMinRows_DoesNotGoBelowMin()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MinRows;

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(GridSpec.MinRows, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void DeleteRowCommand_CanExecute_ReturnsFalse_WhenAtMinRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MinRows;

        Assert.False(vm.DeleteRowCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteRowCommand_CanExecute_ReturnsTrue_WhenAboveMinRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MinRows + 1;

        Assert.True(vm.DeleteRowCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteRowCommand_CanExecute_ReturnsFalse_WhenNoCurrentSheet()
    {
        var vm = CreateViewModel();

        Assert.False(vm.DeleteRowCommand.CanExecute(null));
    }

    /// <summary>
    /// T-055増分1隠密レビュー指摘(CONFIRMED)のRED先行証明対象。削除される最終行(row9)を
    /// SelectedCellが指していた場合、削除後に選択解除(null)ではなく新しい末尾行(row8)へ
    /// クランプすること(殿裁定)。列(Column)は維持される。
    /// RED証明手法: DeleteRowCommandのExecute内クランプ処理(`if (SelectedCell is GridPos...)`)を
    /// 一時的にコメントアウトしてテスト実行→SelectedCellが範囲外(row9)のまま残りRED(実測確認済み)。
    /// 戻すとGREEN。
    /// </summary>
    [Fact]
    public void DeleteRowCommand_Execute_WhenSelectedCellAtDeletedRow_ClampsToNewLastRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.SelectedCell = new GridPos(9, 2);

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(new GridPos(8, 2), vm.SelectedCell);
    }

    /// <summary>対照ケース: 削除される行より前を選択している場合はクランプ不要(範囲内のまま維持)。</summary>
    [Fact]
    public void DeleteRowCommand_Execute_WhenSelectedCellBeforeDeletedRow_RemainsUnchanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.SelectedCell = new GridPos(3, 1);

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(new GridPos(3, 1), vm.SelectedCell);
    }

    /// <summary>対照ケース: 未選択(null)のまま削除しても例外なく完了する。</summary>
    [Fact]
    public void DeleteRowCommand_Execute_WhenSelectedCellIsNull_DoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        Assert.Null(vm.SelectedCell);

        vm.DeleteRowCommand.Execute(null);

        Assert.Null(vm.SelectedCell);
        Assert.Equal(9, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void DeleteRowCommand_Execute_WhenLastRowEmpty_DecreasesRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        // 最終行(9)以外に要素があっても削除に支障しないことの対照ケース。
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(3, 1) });

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(9, vm.CurrentSheet!.Grid.Rows);
    }

    /// <summary>
    /// 削除拒否のRED先行証明対象。最終行に広義5種(殿裁定2026-07-10)いずれかの要素があれば削除拒否。
    /// RED証明手法: DeleteRowCommandのExecute内`IsRowOccupied`呼び出しを一時的に`false`固定へ
    /// 差し替えてテスト実行→全InlineDataがRows減少してしまいRED(実測確認済み)。戻すとGREEN。
    /// </summary>
    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("GroupFrame")]
    [InlineData("RungComment")]
    public void DeleteRowCommand_Execute_WhenLastRowOccupied_RejectsAndDoesNotDecreaseRows(string elementType)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        int lastRow = vm.CurrentSheet!.Grid.Rows - 1;
        PlaceElementAt(vm.CurrentSheet!, elementType, lastRow);

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(10, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void DeleteRowCommand_Execute_WhenLastRowOccupied_SetsStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        int lastRow = vm.CurrentSheet!.Grid.Rows - 1;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(lastRow, 1) });

        vm.DeleteRowCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.StatusMessage));
    }

    /// <summary>
    /// T-055増分1往復2周目(隠密テスト設計書#2/#3/#5、忍者実機発見「拒否→要素除去→再削除で
    /// 成功するがメッセージが残る」の回帰テスト)。削除成功時にStatusMessageが直前の値
    /// (拒否警告文言/他文言/空)に関わらず必ず""へクリアされること。
    /// RED証明手法: DeleteRowCommandのExecute内成功パスの`StatusMessage = "";`を一時的に
    /// コメントアウトしてテスト実行→priorMessageが非空の2ケースでpriorMessageのまま残りRED
    /// (実測確認済み)。戻すとGREEN。
    /// </summary>
    [Theory]
    [InlineData("最終行に要素があるため削除できません")]
    [InlineData("配置するセルを先に選択してください")]
    [InlineData("")]
    public void DeleteRowCommand_Execute_OnSuccess_ClearsStatusMessage(string priorMessage)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.StatusMessage = priorMessage;

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(9, vm.CurrentSheet!.Grid.Rows);
        Assert.Equal("", vm.StatusMessage);
    }

    /// <summary>隠密テスト設計書#1。拒否パス自体は往復2周目の変更対象ではないが、状態遷移の
    /// 完全性のため退行検知の網羅性を確保する。</summary>
    [Fact]
    public void DeleteRowCommand_Execute_OnRejection_SetsWarningMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        int lastRow = vm.CurrentSheet!.Grid.Rows - 1;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(lastRow, 1) });

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(10, vm.CurrentSheet!.Grid.Rows);
        Assert.Equal("最終行に要素があるため削除できません", vm.StatusMessage);
    }

    /// <summary>隠密テスト設計書#4。連続拒否で意図せぬ変化(メッセージ消失等)が無いことの対照。</summary>
    [Fact]
    public void DeleteRowCommand_Execute_OnRepeatedRejection_KeepsWarningMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        int lastRow = vm.CurrentSheet!.Grid.Rows - 1;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(lastRow, 1) });
        vm.DeleteRowCommand.Execute(null);

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(10, vm.CurrentSheet!.Grid.Rows);
        Assert.Equal("最終行に要素があるため削除できません", vm.StatusMessage);
    }

    /// <summary>
    /// T-055増分1往復2周目(隠密テスト設計書#6/#7/#8、家老裁定でAddRowCommand側も対称吸収)。
    /// 行追加成功時にStatusMessageが直前の値に関わらず必ず""へクリアされること。
    /// RED証明手法: AddRowCommandのExecute内成功パスの`StatusMessage = "";`を一時的に
    /// コメントアウトしてテスト実行→priorMessageが非空の2ケースで残りRED(実測確認済み)。戻すとGREEN。
    /// </summary>
    [Theory]
    [InlineData("最終行に要素があるため削除できません")]
    [InlineData("配置するセルを先に選択してください")]
    [InlineData("")]
    public void AddRowCommand_Execute_OnSuccess_ClearsStatusMessage(string priorMessage)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.StatusMessage = priorMessage;

        vm.AddRowCommand.Execute(null);

        Assert.Equal(11, vm.CurrentSheet!.Grid.Rows);
        Assert.Equal("", vm.StatusMessage);
    }

    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("GroupFrame")]
    [InlineData("RungComment")]
    public void IsRowOccupied_ReturnsTrue_WhenElementAtRow(string elementType)
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        PlaceElementAt(sheet, elementType, row: 5);

        Assert.True(MainWindowViewModel.IsRowOccupied(sheet, 5));
    }

    [Fact]
    public void IsRowOccupied_ReturnsFalse_WhenRowEmpty()
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(3, 1) });

        Assert.False(MainWindowViewModel.IsRowOccupied(sheet, 5));
    }

    private static void PlaceElementAt(Sheet sheet, string elementType, int row)
    {
        switch (elementType)
        {
            case "ElementInstance":
                sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(row, 1) });
                break;
            case "VerticalConnector":
                sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = row - 1, BottomRow = row });
                break;
            case "WireBreak":
                sheet.WireBreaks.Add(new WireBreak { Boundary = 2.5, Row = row });
                break;
            case "GroupFrame":
                sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(row, 1), Width = 3, Height = 1 });
                break;
            case "RungComment":
                sheet.RungComments.Add(new RungComment { Row = row, Text = "注記" });
                break;
        }
    }
}
