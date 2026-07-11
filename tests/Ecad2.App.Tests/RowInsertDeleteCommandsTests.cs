using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

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
    /// T-055増分3差し込み(殿裁定2026-07-11): 対象行に要素(広義5種)があっても拒否せず「要素ごと削除」
    /// する(GuiEcad同型)。旧挙動(拒否・Rows不変)からの転換確認。型ごとのシフト/削除規則自体は
    /// Ecad2.Core.Tests.RowOpsTestsで検証済みのため、ここではコマンドとしてRowsが減ることのみ確認する。
    /// </summary>
    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("GroupFrame")]
    [InlineData("RungComment")]
    public void DeleteRowAtCommand_Execute_WhenTargetRowOccupied_DeletesElementAndDecreasesRows(string elementType)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        PlaceElementAt(vm.CurrentSheet!, elementType, row: 3);

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal(9, vm.CurrentSheet!.Grid.Rows);
    }

    /// <summary>占有拒否撤廃の確認(旧テストの転換)。対象行に要素があっても拒否文言は出ない。</summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_WhenTargetRowOccupied_DoesNotSetRejectionMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(3, 1) });

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal("", vm.StatusMessage);
    }

    /// <summary>設計書B8: Grid.Rows=2(下限MinRows=1到達直前)での削除は、クランプではなく通常どおり
    /// 1へ減算されること(下限ガードは`Rows &lt;= MinRows`のみで弾くため、Rows=2はまだ許可範囲)。</summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_AtOneAboveMinRows_DecreasesToMinRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = GridSpec.MinRows + 1;

        vm.DeleteRowAtCommand.Execute(0);

        Assert.Equal(GridSpec.MinRows, vm.CurrentSheet!.Grid.Rows);
    }

    /// <summary>削除された要素のDeviceNameが他のどの要素からも参照されなくなった場合、機器表
    /// (Document.Devices.ByName)から該当エントリが除去されること(DeleteSelectedElementの既存
    /// クリーンアップパターンをRowOps.DeleteRowの複数削除へ拡張した回帰テスト)。</summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_RemovesDeviceTableEntry_WhenNoLongerReferenced()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.SelectedCell = new GridPos(3, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        vm.DeleteRowAtCommand.Execute(3);

        Assert.False(vm.Document.Devices.ByName.ContainsKey("X001"));
    }

    /// <summary>対照ケース: 削除したDeviceNameが他シートの別要素から参照されていれば機器表エントリは残る。</summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_KeepsDeviceTableEntry_WhenStillReferencedElsewhere()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.SelectedCell = new GridPos(3, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "X001", Pos = new GridPos(7, 0) });

        vm.DeleteRowAtCommand.Execute(3);

        Assert.True(vm.Document.Devices.ByName.ContainsKey("X001"));
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

    // ---- T-055増分3往復1周目: 隠密レビュー指摘a・bの回帰テスト ----
    // テスト設計書: docs/ecad2-t055-increment3-selectedcell-bugfix-test-design-onmitsu.md §2

    /// <summary>
    /// T-a1【RED証明の中核】(設計書§2.1、指摘a=B2)。削除対象行そのものを選択中に削除すると、
    /// SelectedElement系PropertyChangedが発火すること。
    /// RED証明: 修正前コードはsc.Row > rowがfalse(3>3=false)のためSelectedCellのsetterを
    /// 通らず、この発火が一切起きない(git stashで修正前コードへ戻し実測確認済み)。
    /// </summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_WhenSelectedCellIsTargetRow_RaisesSelectedElementDeviceNameChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "X001", Pos = new GridPos(3, 1) });
        vm.SelectedCell = new GridPos(3, 1);
        bool raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.SelectedElementDeviceName)) raised = true; };

        vm.DeleteRowAtCommand.Execute(3);

        Assert.True(raised);
    }

    /// <summary>T-a2(設計書§2.1)。削除対象行の要素が消え、シフトで別要素が来る場合、最終的に
    /// SelectedElementがその別要素を正しく指すこと。末尾はT-ab1(直接読み取りと最終発火時点の
    /// 値の一致)を兼ねる。</summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_WhenSelectedCellIsTargetRow_ShiftedElementBecomesSelected()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "X001", Pos = new GridPos(3, 1) });
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "Y001", Pos = new GridPos(4, 1) });
        vm.SelectedCell = new GridPos(3, 1);
        string? lastCaptured = null;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.SelectedElementDeviceName)) lastCaptured = vm.SelectedElementDeviceName; };

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal("Y001", vm.SelectedElementDeviceName);
        Assert.Equal(vm.SelectedElementDeviceName, lastCaptured); // T-ab1
    }

    /// <summary>T-a3(設計書§2.1)。削除対象行の要素が消え、シフトで来る要素が無い場合、
    /// SelectedElementがnullになること。</summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_WhenSelectedCellIsTargetRow_AndNoShiftedElement_ClearsSelectedElement()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "X001", Pos = new GridPos(3, 1) });
        vm.SelectedCell = new GridPos(3, 1);

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Null(vm.SelectedElement);
        Assert.False(vm.HasSelectedElement);
    }

    /// <summary>
    /// T-b1【RED証明の中核】(設計書§2.2、指摘b=B4一般ケース)。削除対象行より後ろを選択中、
    /// PropertyChangedの最終発火時点でのSelectedElementDeviceNameが正しい(シフト後)要素を
    /// 指すこと。末尾はT-ab1を兼ねる。
    /// RED証明: 修正前コードはSelectedCell = sc with { Row = 4 }の代入(RowOps.DeleteRow実行前)で
    /// 1回だけ発火し、その時点のsheet.Elementsは未シフトのため行4の要素("B001")が拾われる。
    /// 事後の再通知が無いため記録リストは["B001"]のみとなり期待値"A001"と一致せずFAIL
    /// (git stashで修正前コードへ戻し実測確認済み)。
    /// </summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_WhenSelectedCellIsAfterTargetRow_FinalNotificationReflectsShiftedElement()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "B001", Pos = new GridPos(4, 1) });
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "A001", Pos = new GridPos(5, 1) });
        vm.SelectedCell = new GridPos(5, 1);
        var captured = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.SelectedElementDeviceName)) captured.Add(vm.SelectedElementDeviceName); };

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal("A001", captured[^1]);
        Assert.Equal(vm.SelectedElementDeviceName, captured[^1]); // T-ab1
    }

    /// <summary>T-b2(設計書§2.2、指摘bの最小境界=B3)。削除対象の直後を選択中、shift幅=1の最小ケース。</summary>
    [Fact]
    public void DeleteRowAtCommand_Execute_WhenSelectedCellIsImmediatelyAfterTargetRow_FinalNotificationReflectsShiftedElement()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "A001", Pos = new GridPos(4, 1) });
        vm.SelectedCell = new GridPos(4, 1);
        var captured = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.SelectedElementDeviceName)) captured.Add(vm.SelectedElementDeviceName); };

        vm.DeleteRowAtCommand.Execute(3);

        Assert.Equal(new GridPos(3, 1), vm.SelectedCell);
        Assert.Equal("A001", captured[^1]);
        Assert.Equal(vm.SelectedElementDeviceName, captured[^1]); // T-ab1
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
