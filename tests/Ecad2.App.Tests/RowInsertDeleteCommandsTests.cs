using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-055増分3: 右クリックコンテキストメニュー経由の任意位置行挿入・削除
/// (InsertRowBeforeCommand/DeleteRowAtCommand)。型ごとのシフト規則自体は
/// Ecad2.Core.Tests.RowOpsTestsで検証済みのため、ここではコマンドとしての振る舞い
/// (Grid.Rows増減・上限下限クランプ・削除拒否・StatusMessage・MarkDirty)を検証する。
/// </summary>
public class RowInsertDeleteCommandsTests : ViewModelTestBase
{
    [Fact]
    public void InsertRowBeforeCommand_Execute_IncreasesRowsByOne()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        int before = vm.CurrentSheet!.Grid.Rows;

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.Equal(before + 1, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void InsertRowBeforeCommand_Execute_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void InsertRowBeforeCommand_Execute_ShiftsElementAtOrAfterTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(5, 1) });

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.Equal(6, vm.CurrentSheet!.Elements[0].Pos.Row);
    }

    [Fact]
    public void InsertRowBeforeCommand_Execute_DoesNotShiftElementBeforeTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 1) });

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.Equal(1, vm.CurrentSheet!.Elements[0].Pos.Row);
    }

    /// <summary>
    /// 上限クランプのRED先行証明対象。Rows=60(上限)でExecuteしても61へ増えず60のままであること。
    /// RED証明手法: InsertRowBeforeCommandのExecute内ガード(`sheet.Grid.Rows >= GridSpec.MaxRows`)を
    /// 一時的にコメントアウトしてテスト実行→61に増えてしまいRED(実測確認済み)。ガードを戻すとGREEN。
    /// </summary>
    [Fact]
    public void InsertRowBeforeCommand_Execute_AtMaxRows_DoesNotExceedMax()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MaxRows;

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.Equal(GridSpec.MaxRows, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void InsertRowBeforeCommand_CanExecute_ReturnsFalse_WhenAtMaxRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MaxRows;

        Assert.False(vm.InsertRowBeforeCommand.CanExecute(3));
    }

    [Fact]
    public void InsertRowBeforeCommand_CanExecute_ReturnsTrue_WhenBelowMaxRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MaxRows - 1;

        Assert.True(vm.InsertRowBeforeCommand.CanExecute(3));
    }

    [Fact]
    public void InsertRowBeforeCommand_CanExecute_ReturnsFalse_WhenNoCurrentSheet()
    {
        var vm = CreateViewModel();

        Assert.False(vm.InsertRowBeforeCommand.CanExecute(3));
    }

    [Fact]
    public void InsertRowBeforeCommand_CanExecute_ReturnsFalse_WhenParamNotInt()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.InsertRowBeforeCommand.CanExecute(null));
    }

    [Fact]
    public void InsertRowBeforeCommand_Execute_OnSuccess_ClearsStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.StatusMessage = "行9に要素があるため削除できません";

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.Equal("", vm.StatusMessage);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_DecreasesRowsByOne()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal(9, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;

        vm.DeleteRowAtCommand.Execute(3);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_ShiftsElementAfterTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(5, 1) });

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal(4, vm.CurrentSheet!.Elements[0].Pos.Row);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_DoesNotShiftElementAtOrBeforeTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 1) });

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal(1, vm.CurrentSheet!.Elements[0].Pos.Row);
    }

    /// <summary>
    /// 下限クランプのRED先行証明対象。Rows=1(下限)でExecuteしても0へ減らず1のままであること。
    /// RED証明手法: DeleteRowAtCommandのExecute内ガード(`sheet.Grid.Rows <= GridSpec.MinRows`)を
    /// 一時的にコメントアウトしてテスト実行→0へ減ってしまいRED(実測確認済み)。ガードを戻すとGREEN。
    /// </summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_AtMinRows_DoesNotGoBelowMin()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MinRows;

        vm.DeleteRowAtCommand.Execute(0);

        Assert.Equal(GridSpec.MinRows, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void DeleteRowAtCommand_CanExecute_ReturnsFalse_WhenAtMinRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MinRows;

        Assert.False(vm.DeleteRowAtCommand.CanExecute(0));
    }

    [Fact]
    public void DeleteRowAtCommand_CanExecute_ReturnsTrue_WhenAboveMinRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MinRows + 1;

        Assert.True(vm.DeleteRowAtCommand.CanExecute(0));
    }

    [Fact]
    public void DeleteRowAtCommand_CanExecute_ReturnsFalse_WhenNoCurrentSheet()
    {
        var vm = CreateViewModel();

        Assert.False(vm.DeleteRowAtCommand.CanExecute(3));
    }

    [Fact]
    public void DeleteRowAtCommand_CanExecute_ReturnsFalse_WhenParamNotInt()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.DeleteRowAtCommand.CanExecute(null));
    }

    /// <summary>
    /// 対象行に要素(広義5種、殿裁定2026-07-10)があれば削除拒否。RowOps.DeleteRowの契約
    /// (targetRow行に要素なし)を満たさない入力を、コマンド側のIsRowOccupiedガードで弾く。
    /// RED証明手法: DeleteRowAtCommandのExecute内`IsRowOccupied`呼び出しを一時的に`false`固定へ
    /// 差し替えてテスト実行→全InlineDataがRows減少してしまいRED(実測確認済み)。戻すとGREEN。
    /// </summary>
    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("GroupFrame")]
    [InlineData("RungComment")]
    public void DeleteRowAtCommand_Execute_WhenTargetRowOccupied_RejectsAndDoesNotDecreaseRows(string elementType)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        PlaceElementAt(vm.CurrentSheet!, elementType, row: 3);

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal(10, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_WhenTargetRowOccupied_SetsStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(3, 1) });

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal("行4に要素があるため削除できません", vm.StatusMessage);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_OnSuccess_ClearsStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.StatusMessage = "行4に要素があるため削除できません";

        vm.DeleteRowAtCommand.Execute(5);

        Assert.Equal("", vm.StatusMessage);
    }

    /// <summary>対照ケース: 対象行以外に要素があっても削除に支障しない。</summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_WhenOtherRowOccupied_DecreasesRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(7, 1) });

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal(9, vm.CurrentSheet!.Grid.Rows);
    }

    /// <summary>
    /// 隠密レビュー指摘a(往復1周目、CONFIRMED)のRED先行証明対象。SelectedCellが挿入点(targetRow)以降を
    /// 指していた場合、実要素(RowOpsでシフトされる5種)と同じ規則で追随すること(据え置きだと選択カーソルが
    /// 指す座標と実要素の対応がずれ、直後の操作で誤操作を招く)。
    /// RED証明手法: InsertRowBeforeCommandのExecute内SelectedCellシフト処理を一時的にコメントアウトして
    /// テスト実行→SelectedCellが挿入前の値のまま変化せずRED(実測確認済み)。戻すとGREEN。
    /// </summary>
    [Fact]
    public void InsertRowBeforeCommand_Execute_ShiftsSelectedCellAtTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 2);

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.Equal(new GridPos(4, 2), vm.SelectedCell);
    }

    [Fact]
    public void InsertRowBeforeCommand_Execute_ShiftsSelectedCellAfterTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(5, 2);

        vm.InsertRowBeforeCommand.Execute(2);

        Assert.Equal(new GridPos(6, 2), vm.SelectedCell);
    }

    [Fact]
    public void InsertRowBeforeCommand_Execute_DoesNotShiftSelectedCellBeforeTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(1, 2);

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.Equal(new GridPos(1, 2), vm.SelectedCell);
    }

    [Fact]
    public void InsertRowBeforeCommand_Execute_WhenSelectedCellIsNull_DoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        Assert.Null(vm.SelectedCell);

        vm.InsertRowBeforeCommand.Execute(3);

        Assert.Null(vm.SelectedCell);
    }

    /// <summary>
    /// 削除側の対称ケース(隠密レビュー指摘a)。SelectedCellが削除点(targetRow)より後ろを指していれば-1追随。
    /// RowOps.DeleteRowの4種要素と同じ規則(targetRowより後ろのみシフト)をSelectedCellにも適用する。
    /// RED証明手法: DeleteRowAtCommandのExecute内SelectedCellシフト処理を一時的にコメントアウトして
    /// テスト実行→SelectedCellが削除前の値のまま変化せずRED(実測確認済み)。戻すとGREEN。
    /// </summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_ShiftsSelectedCellAfterTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.SelectedCell = new GridPos(5, 2);

        vm.DeleteRowAtCommand.Execute(2);

        Assert.Equal(new GridPos(4, 2), vm.SelectedCell);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_DoesNotShiftSelectedCellAtTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.SelectedCell = new GridPos(2, 2);

        vm.DeleteRowAtCommand.Execute(2);

        Assert.Equal(new GridPos(2, 2), vm.SelectedCell);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_DoesNotShiftSelectedCellBeforeTargetRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.SelectedCell = new GridPos(1, 2);

        vm.DeleteRowAtCommand.Execute(2);

        Assert.Equal(new GridPos(1, 2), vm.SelectedCell);
    }

    [Fact]
    public void DeleteRowAtCommand_Execute_WhenSelectedCellIsNull_DoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        Assert.Null(vm.SelectedCell);

        vm.DeleteRowAtCommand.Execute(2);

        Assert.Null(vm.SelectedCell);
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
