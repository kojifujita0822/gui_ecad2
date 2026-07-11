using Ecad2.App.ViewModels;
using Ecad2.Model;

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

    // ---- T-051バグ修正#1: ReplaceDocument(新規/開く)でUndo/Redo履歴がクリアされること ----

    /// <summary>【RED証明の中核】修正前コードはReplaceDocumentがUndoManagerに触れないため、
    /// 別ファイルへの切替後も旧文書のUndo履歴が残存し、無関係な旧状態への復元・誤上書き保存
    /// (隠密レビュー#1、データ破損)が起こりうる。</summary>
    [Fact]
    public void NewDocument_ClearsUndoHistory_UndoCommandBecomesDisabled()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        Assert.True(vm.UndoCommand.CanExecute(null)); // 前提: Undo履歴がある

        vm.NewDocument(); // ReplaceDocument経由で無関係な新文書へ差し替え

        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void NewDocument_ClearsRedoHistory_RedoCommandBecomesDisabled()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.UndoCommand.Execute(null);
        Assert.True(vm.RedoCommand.CanExecute(null)); // 前提: Undo直後はRedo履歴がある

        vm.NewDocument();

        Assert.False(vm.RedoCommand.CanExecute(null));
    }

    // ---- T-051バグ修正#4: Undo/Redoで出力パネルの診断残留が消えること ----

    /// <summary>DRC-PART-001(部品参照未解決、T-052)を誘発する最小配置でDiagnostics≥1件を作る
    /// (OutputPanelJumpToTestsと同型)。</summary>
    private static void PlaceUnresolvedPartIdElement(MainWindowViewModel vm)
        => vm.CurrentSheet!.Elements.Add(new Model.ElementInstance
        {
            PartId = "missing-id",
            DeviceName = null,
            Pos = new Model.GridPos(1, 1),
        });

    /// <summary>【RED証明の中核】修正前コードはApplyUndoRedoSnapshotがOutputPanel.ClearResults()を
    /// 呼ばないため、シート構成が変わっても存在しないページ番号を指す診断が出力パネルに残留し、
    /// クリック時にJumpToが無言returnする「沈黙」不整合(隠密レビュー#4、T-019の教訓の再発)が起こる。</summary>
    [Fact]
    public void UndoCommand_Execute_ClearsOutputPanelDiagnostics()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceUnresolvedPartIdElement(vm);
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.OutputPanel.RunDrcCommand.Execute(null);
        Assert.True(vm.OutputPanel.Diagnostics.Count > 0); // 前提: 診断が残っている

        vm.UndoCommand.Execute(null);

        Assert.Empty(vm.OutputPanel.Diagnostics);
    }

    [Fact]
    public void RedoCommand_Execute_ClearsOutputPanelDiagnostics()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceUnresolvedPartIdElement(vm);
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.UndoCommand.Execute(null);
        vm.OutputPanel.RunDrcCommand.Execute(null);
        Assert.True(vm.OutputPanel.Diagnostics.Count > 0); // 前提: Undo後に再度診断を作っておく

        vm.RedoCommand.Execute(null);

        Assert.Empty(vm.OutputPanel.Diagnostics);
    }

    [Fact]
    public void UndoCommand_Execute_WhenNoDiagnostics_DoesNotThrow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        Assert.Empty(vm.OutputPanel.Diagnostics); // 前提: DRC未実行

        var exception = Record.Exception(() => vm.UndoCommand.Execute(null));

        Assert.Null(exception);
        Assert.Empty(vm.OutputPanel.Diagnostics);
    }

    [Fact]
    public void UndoCommand_Execute_ClearsSelectedDiagnostic()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceUnresolvedPartIdElement(vm);
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.OutputPanel.RunDrcCommand.Execute(null);
        Assert.True(vm.OutputPanel.Diagnostics.Count > 0); // 前提
        vm.OutputPanel.SelectedDiagnostic = vm.OutputPanel.Diagnostics[0];
        Assert.NotNull(vm.OutputPanel.SelectedDiagnostic);

        vm.UndoCommand.Execute(null);

        Assert.Null(vm.OutputPanel.SelectedDiagnostic);
    }

    // ---- T-051往復2周目: ApplyUndoRedoSnapshotがSelectedCellを無条件nullリセットするバグの修正
    // (隠密再レビューCONFIRMED、docs/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md) ----

    /// <summary>【RED証明の中核】T-selcell-1: シート数不変・クランプ非発動でも、修正前コードは
    /// SetCurrentSheetIndexCore経由でSelectedCellが無条件nullになりFAILする。</summary>
    [Fact]
    public void UndoCommand_Execute_WithoutClamp_PreservesSelectedCell()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.CurrentSheetIndex = 0;
        vm.SelectedCell = new GridPos(3, 2);

        vm.UndoCommand.Execute(null);

        Assert.Equal(new GridPos(3, 2), vm.SelectedCell);
    }

    /// <summary>T-selcell-2: 対称性点検(Redo方向)。</summary>
    [Fact]
    public void RedoCommand_Execute_WithoutClamp_PreservesSelectedCell()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.CurrentSheetIndex = 0;
        vm.SelectedCell = new GridPos(3, 2);
        vm.UndoCommand.Execute(null);

        vm.RedoCommand.Execute(null);

        Assert.Equal(new GridPos(3, 2), vm.SelectedCell);
    }

    /// <summary>【RED証明の中核・境界値】T-selcell-3: CurrentSheetIndexがクランプされ実際に別シートへ
    /// 切り替わる場合でも、SelectedCellの座標値自体は変えない(DeleteRowAtCommandのクランプ意味論と
    /// 整合、T-055増分3)。修正前コードはSetCurrentSheetIndexCore経由でSelectedCellが無条件nullに
    /// なりFAILする。</summary>
    [Fact]
    public void UndoCommand_Execute_WithClamp_PreservesSelectedCellCoordinates()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.SheetNavigation.AddCommand.Execute(("シート3", false));
        vm.SelectedCell = new GridPos(7, 4);

        vm.UndoCommand.Execute(null);

        Assert.Equal(1, vm.CurrentSheetIndex);
        Assert.Equal(new GridPos(7, 4), vm.SelectedCell);
    }

    /// <summary>T-selcell-4: 退行なし確認。SelectedCellが未選択(null)の場合、Undo後もnullのまま。</summary>
    [Fact]
    public void UndoCommand_Execute_WhenSelectedCellIsNull_RemainsNull()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        vm.UndoCommand.Execute(null);

        Assert.Null(vm.SelectedCell);
    }

    // ---- T-051往復3周目: SelectedCell復元時のGrid.Rows範囲クランプ欠如の修正
    // (隠密再々レビューPLAUSIBLE、docs/ecad2-t051-selectedcell-clamp-test-design-onmitsu.md) ----

    /// <summary>【RED証明の中核・境界値D3】Undo実行でGrid.Rowsが縮小する場合、SelectedCellが
    /// 新しい範囲内へクランプされること(大幅超過)。</summary>
    [Fact]
    public void UndoCommand_Execute_WhenRowFarExceedsRestoredGridRows_ClampsToLastRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.CurrentSheetIndex = 0;
        for (int i = 0; i < 5; i++) vm.AddRowCommand.Execute(null);
        vm.SelectedCell = new GridPos(14, 2);

        vm.UndoCommand.Execute(null);

        Assert.Equal(new GridPos(9, 2), vm.SelectedCell);
    }

    /// <summary>【RED証明・境界値D2】ちょうど1行超過のケースでもクランプされること。</summary>
    [Fact]
    public void UndoCommand_Execute_WhenRowExceedsRestoredGridRowsByOne_ClampsToLastRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.CurrentSheetIndex = 0;
        vm.AddRowCommand.Execute(null);
        vm.SelectedCell = new GridPos(10, 2);

        vm.UndoCommand.Execute(null);

        Assert.Equal(new GridPos(9, 2), vm.SelectedCell);
    }

    /// <summary>T-selclamp-3: 範囲内の最終行ちょうどの場合はクランプされず値を維持すること
    /// (境界のすぐ内側、退行防止確認)。</summary>
    [Fact]
    public void UndoCommand_Execute_WhenRowIsLastValidRow_PreservesSelectedCell()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.CurrentSheetIndex = 0;
        vm.SelectedCell = new GridPos(9, 2);

        vm.UndoCommand.Execute(null);

        Assert.Equal(new GridPos(9, 2), vm.SelectedCell);
    }

    /// <summary>T-selclamp-4: 対称性点検(Redo方向)。クランプ後の値がRedo実行でも維持されること
    /// (Redo後の復元先シートはRows拡張後に戻るため、クランプは発生しない)。</summary>
    [Fact]
    public void RedoCommand_Execute_AfterClamp_PreservesSelectedCell()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.CurrentSheetIndex = 0;
        for (int i = 0; i < 5; i++) vm.AddRowCommand.Execute(null);
        vm.SelectedCell = new GridPos(14, 2);
        vm.UndoCommand.Execute(null);

        vm.RedoCommand.Execute(null);

        Assert.Equal(new GridPos(9, 2), vm.SelectedCell);
    }

    /// <summary>【RED証明の中核・複合ケース】CurrentSheetIndexのクランプとGrid.Rowsのクランプが
    /// 同時に発生する場合、Rowクランプの基準は「クランプ後に確定した新しいCurrentSheet」であること。</summary>
    [Fact]
    public void UndoCommand_Execute_WithSheetIndexClampAndRowClamp_UsesRestoredSheetGridRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.SheetNavigation.AddCommand.Execute(("シート3", false));
        for (int i = 0; i < 5; i++) vm.AddRowCommand.Execute(null);
        vm.SelectedCell = new GridPos(14, 4);

        vm.UndoCommand.Execute(null);

        Assert.Equal(1, vm.CurrentSheetIndex);
        Assert.Equal(new GridPos(9, 4), vm.SelectedCell);
    }
}
