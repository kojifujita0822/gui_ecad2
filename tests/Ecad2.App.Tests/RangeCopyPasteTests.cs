using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// 範囲選択とコピー・貼り付け（殿ご下命2026-10-09）の ViewModel 側のテスト。
/// 写す・貼る規則そのものは Core 側の <c>RangeClipboardTests</c> が測る。ここで測るのは
/// 範囲の起点が解ける経路、貼り付け位置の確認モードへの出入り、Undo、シート跨ぎ、定義の埋め込み。
/// </summary>
public class RangeCopyPasteTests : ViewModelTestBase
{
    private const string CustomId = "range-copy-custom";

    private MainWindowViewModel CreateViewModelWithDocument()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        return vm;
    }

    private static void Place(MainWindowViewModel vm, int row, int column, string name,
                              ElementKind kind = ElementKind.ContactNO)
    {
        vm.SelectedCell = new GridPos(row, column);
        vm.PlaceElementAtSelectedCell(kind, orient: null);
        vm.SelectedElementDeviceName = name;
    }

    /// <summary>行0に X1(列0)・CR1(列2)、行1に X2(列0) を置き、行0〜1・列0〜2 を写した状態。</summary>
    private MainWindowViewModel CreateViewModelWithCopiedBlock()
    {
        var vm = CreateViewModelWithDocument();
        Place(vm, 0, 0, "X1");
        Place(vm, 0, 2, "CR1", ElementKind.Coil);
        Place(vm, 1, 0, "X2");
        vm.SelectRange(new GridPos(0, 0), new GridPos(1, 2));
        Assert.True(vm.CopySelection());
        return vm;
    }

    // ---- 範囲選択 ----

    [Fact]
    public void ExtendSelectionTo_KeepsAnchorWhileCursorMoves()
    {
        var vm = CreateViewModelWithDocument();
        vm.SelectedCell = new GridPos(2, 3);

        vm.ExtendSelectionTo(new GridPos(2, 4));
        vm.ExtendSelectionTo(new GridPos(3, 4));

        Assert.Equal(new CellRange(2, 3, 3, 4), vm.SelectedRange);
        Assert.Equal(new GridPos(3, 4), vm.SelectedCell);
        // カーソルが起点の左上へ回っても、起点は動かぬ。
        vm.ExtendSelectionTo(new GridPos(1, 1));
        Assert.Equal(new CellRange(1, 1, 2, 3), vm.SelectedRange);
    }

    /// <summary>カーソルが起点へ戻れば範囲ではない（1セルの選択）。</summary>
    [Fact]
    public void ExtendSelectionTo_BackToAnchor_IsNotARange()
    {
        var vm = CreateViewModelWithDocument();
        vm.SelectedCell = new GridPos(2, 3);
        vm.ExtendSelectionTo(new GridPos(2, 4));

        vm.ExtendSelectionTo(new GridPos(2, 3));

        Assert.Null(vm.SelectedRange);
    }

    /// <summary>範囲はグリッドの内へ収まる（マウスが外へ出ても）。</summary>
    [Fact]
    public void SelectRange_ClampsBothCornersToGrid()
    {
        var vm = CreateViewModelWithDocument();
        var grid = vm.CurrentSheet!.Grid;

        vm.SelectRange(new GridPos(-3, -5), new GridPos(999, 999));

        Assert.Equal(new CellRange(0, 0, grid.Rows - 1, grid.Columns - 1), vm.SelectedRange);
    }

    /// <summary>
    /// 範囲を広げる途中で、View にマウスキャプチャを放させる通知が飛ばぬこと。
    /// <para>
    /// <c>MainWindow.ViewModel_PropertyChanged</c> は <c>IsDragging*</c>／<c>IsResizing*</c>／
    /// <c>FrameDraftPreview</c> の通知を「外から強制キャンセルされた」合図と見てキャプチャを放す。
    /// 範囲選択のドラッグはマウス移動のたびに <see cref="MainWindowViewModel.SelectRange"/> を呼び、
    /// それは SelectedCell の setter（ドラッグ・記入中の状態を畳む唯一の入口）を通る
    /// ——<b>畳む相手が居らぬのに通知だけ飛べば、ドラッグが一歩目で切れる</b>。
    /// </para></summary>
    [Fact]
    public void SelectRange_DoesNotRaiseNotificationsThatReleaseMouseCapture()
    {
        var vm = CreateViewModelWithDocument();
        vm.SelectedCell = new GridPos(1, 1);
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.SelectRange(new GridPos(1, 1), new GridPos(2, 2));
        vm.SelectRange(new GridPos(1, 1), new GridPos(3, 4));

        Assert.DoesNotContain(notified, n => n is not null
            && (n.StartsWith("IsDragging") || n.StartsWith("IsResizing") || n == nameof(MainWindowViewModel.FrameDraftPreview)));
        Assert.Equal(new CellRange(1, 1, 3, 4), vm.SelectedRange);
    }

    /// <summary>範囲選択でない SelectedCell の代入（矢印移動・クリック・Esc）は範囲を解く。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectedCell_PlainAssignment_ClearsRange(bool assignNull)
    {
        var vm = CreateViewModelWithDocument();
        vm.SelectRange(new GridPos(0, 0), new GridPos(2, 2));
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.SelectedCell = assignNull ? null : new GridPos(2, 2);

        Assert.Null(vm.SelectedRange);
        // 同じセルへの再代入でも範囲は解ける。その再描画の契機として通知が要る。
        Assert.Contains(nameof(MainWindowViewModel.SelectedRange), notified);
        // 解けた後に広げ直すと、新しい起点から始まる（古い起点が残っておらぬ）。
        vm.SelectedCell = new GridPos(5, 5);
        vm.ExtendSelectionTo(new GridPos(5, 6));
        Assert.Equal(new CellRange(5, 5, 5, 6), vm.SelectedRange);
    }

    [Fact]
    public void NewDocument_ClearsRangeAndLeavesPasteMode_ButKeepsClipboard()
    {
        var vm = CreateViewModelWithCopiedBlock();
        vm.BeginPaste();

        vm.NewDocument();

        Assert.Null(vm.SelectedRange);
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.True(vm.HasClipboard);
    }

    // ---- コピー ----

    [Fact]
    public void CopySelection_EmptyRange_FailsAndKeepsPreviousClipboard()
    {
        var vm = CreateViewModelWithCopiedBlock();
        vm.SelectRange(new GridPos(5, 5), new GridPos(6, 6));

        Assert.False(vm.CopySelection());

        // 前のコピーが残っておること＝貼れば3つ入る。
        vm.SelectedCell = new GridPos(5, 5);
        vm.BeginPaste();
        Assert.True(vm.ConfirmPaste());
        Assert.Equal(6, vm.CurrentSheet!.Elements.Count);
    }

    [Fact]
    public void CopySelection_NothingSelected_Fails()
    {
        var vm = CreateViewModelWithDocument();
        vm.SelectedCell = null;

        Assert.False(vm.CopySelection());
        Assert.False(vm.HasClipboard);
    }

    /// <summary>範囲が無くとも、選択セルの要素は写せる。幅を持つ部品は占有セル全体が範囲になる。</summary>
    [Fact]
    public void CopySelection_SingleCellOnWideElement_CopiesThatElement()
    {
        var vm = CreateViewModelWithDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(new ElementInstance { Pos = new GridPos(1, 2), Kind = ElementKind.Motor, CellWidth = 3 });
        vm.SelectedCell = new GridPos(1, 3);   // 基準セル(列2)でない占有セル

        Assert.True(vm.CopySelection());

        vm.SelectedCell = new GridPos(4, 0);
        vm.BeginPaste();
        vm.ConfirmPaste();
        var pasted = sheet.Elements.Single(e => e.Pos.Row == 4);
        Assert.Equal((new GridPos(4, 0), 3, ElementKind.Motor), (pasted.Pos, pasted.CellWidth, pasted.Kind));
    }

    /// <summary>コピーは図面を変えぬ（Undo を積まぬ）。</summary>
    [Fact]
    public void CopySelection_DoesNotTouchDocument()
    {
        var vm = CreateViewModelWithDocument();
        Place(vm, 0, 0, "X1");
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        vm.CopySelection();

        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);
        Assert.Single(vm.CurrentSheet!.Elements);
    }

    // ---- 貼り付け ----

    [Fact]
    public void BeginPaste_WithoutClipboard_DoesNotEnterPasteMode()
    {
        var vm = CreateViewModelWithDocument();
        vm.SelectedCell = new GridPos(0, 0);

        Assert.False(vm.BeginPaste());
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.PastePreview);
    }

    /// <summary>確認モードへ入っただけでは図面は変わらず、ゴーストが選択セルに付いて動く。</summary>
    [Fact]
    public void BeginPaste_ShowsGhostAtSelectedCell_WithoutChangingDocument()
    {
        var vm = CreateViewModelWithCopiedBlock();
        int undoDepthBefore = vm.UndoManager.UndoDepth;
        vm.SelectedCell = new GridPos(5, 4);

        Assert.True(vm.BeginPaste());

        Assert.Equal(ToolMode.Paste, vm.Tool.Mode);
        Assert.Null(vm.SelectedRange);
        var preview = vm.PastePreview!;
        Assert.Equal(new CellRange(5, 4, 6, 6), preview.Rect);
        Assert.Equal(new[] { new GridPos(5, 4), new GridPos(5, 6), new GridPos(6, 4) },
                     preview.Content.Elements.Select(e => e.Pos).OrderBy(p => p.Row).ThenBy(p => p.Column));
        Assert.True(preview.Plan.CanPaste);
        Assert.Equal(3, vm.CurrentSheet!.Elements.Count);
        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);

        vm.SelectedCell = new GridPos(7, 0);
        Assert.Equal(new CellRange(7, 0, 8, 2), vm.PastePreview!.Rect);
    }

    /// <summary>ゴーストの出入りは再描画の契機として通知される（Tool 自体は契機でない）。</summary>
    [Fact]
    public void PasteMode_EnterAndCancel_NotifyPastePreview()
    {
        var vm = CreateViewModelWithCopiedBlock();
        vm.SelectedCell = new GridPos(5, 4);
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.BeginPaste();
        int afterBegin = notified.Count(n => n == nameof(MainWindowViewModel.PastePreview));
        vm.CancelPaste();
        int afterCancel = notified.Count(n => n == nameof(MainWindowViewModel.PastePreview));

        Assert.True(afterBegin >= 1);
        Assert.True(afterCancel > afterBegin);
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.PastePreview);
        Assert.Equal(3, vm.CurrentSheet!.Elements.Count);
    }

    [Fact]
    public void ConfirmPaste_EmptyDestination_AddsElementsWithEmptyNames_AndReturnsToSelectMode()
    {
        var vm = CreateViewModelWithCopiedBlock();
        var sheet = vm.CurrentSheet!;
        int rowsBefore = sheet.Grid.Rows;
        vm.SelectedCell = new GridPos(5, 4);
        vm.BeginPaste();

        Assert.True(vm.ConfirmPaste());

        var pasted = sheet.Elements.Where(e => e.Pos.Row >= 5).OrderBy(e => e.Pos.Row).ThenBy(e => e.Pos.Column).ToList();
        Assert.Equal(new[] { new GridPos(5, 4), new GridPos(5, 6), new GridPos(6, 4) }, pasted.Select(e => e.Pos));
        Assert.Equal(ElementKind.Coil, pasted[1].Kind);
        Assert.All(pasted, e => Assert.Null(e.DeviceName));
        // 機器名が空ゆえ、機器表は増えぬ。
        Assert.Equal(3, vm.Document.Devices.ByName.Count);
        Assert.Equal(rowsBefore, sheet.Grid.Rows);
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.PastePreview);
        Assert.True(vm.IsDirty);
    }

    /// <summary>塞がっておる所へ貼ると行を挿入して割り込み（殿ご裁可）、Undo 一回で行ごと戻る。</summary>
    [Fact]
    public void ConfirmPaste_OccupiedDestination_InsertsRows_AndSingleUndoRestoresEverything()
    {
        var vm = CreateViewModelWithCopiedBlock();
        var sheet = vm.CurrentSheet!;
        int rowsBefore = sheet.Grid.Rows;
        int undoDepthBefore = vm.UndoManager.UndoDepth;
        vm.SelectedCell = new GridPos(1, 0);   // X2 の居る行
        vm.BeginPaste();
        Assert.Equal(2, vm.PastePreview!.Plan.RowsToInsert);

        Assert.True(vm.ConfirmPaste());

        Assert.Equal(rowsBefore + 2, sheet.Grid.Rows);
        // 元の X2 は2行下へ送られ、貼った3つは行1〜2に居る。
        Assert.Equal(new GridPos(3, 0), sheet.Elements.Single(e => e.DeviceName == "X2").Pos);
        Assert.Equal(new[] { new GridPos(1, 0), new GridPos(1, 2), new GridPos(2, 0) },
                     sheet.Elements.Where(e => e.DeviceName is null).Select(e => e.Pos).OrderBy(p => p.Row).ThenBy(p => p.Column));
        Assert.Equal(undoDepthBefore + 1, vm.UndoManager.UndoDepth);

        vm.UndoCommand.Execute(null);

        var restored = vm.CurrentSheet!;
        Assert.Equal(rowsBefore, restored.Grid.Rows);
        Assert.Equal(3, restored.Elements.Count);
        Assert.Equal(new GridPos(1, 0), restored.Elements.Single(e => e.DeviceName == "X2").Pos);
    }

    /// <summary>貼れぬ位置では何も積まず、確認モードのまま留まって理由を出す。</summary>
    [Fact]
    public void ConfirmPaste_ColumnsOverflow_RejectsAndStaysInPasteMode()
    {
        var vm = CreateViewModelWithCopiedBlock();
        var sheet = vm.CurrentSheet!;
        int undoDepthBefore = vm.UndoManager.UndoDepth;
        vm.SelectedCell = new GridPos(5, sheet.Grid.Columns - 1);
        vm.BeginPaste();

        Assert.False(vm.ConfirmPaste());

        Assert.Equal(ToolMode.Paste, vm.Tool.Mode);
        Assert.Equal(3, sheet.Elements.Count);
        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);
        Assert.Contains("貼り付けできません", vm.StatusMessage);
    }

    [Fact]
    public void ConfirmPaste_NotInPasteMode_DoesNothing()
    {
        var vm = CreateViewModelWithCopiedBlock();
        vm.SelectedCell = new GridPos(5, 4);

        Assert.False(vm.ConfirmPaste());
        Assert.Equal(3, vm.CurrentSheet!.Elements.Count);
    }

    /// <summary>シートを跨いで貼れる（殿ご所望）。同じコピーを何度でも貼れる。</summary>
    [Fact]
    public void Paste_OntoAnotherSheet_AndRepeatedly()
    {
        var vm = CreateViewModelWithCopiedBlock();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        Assert.Equal(1, vm.CurrentSheetIndex);
        var second = vm.CurrentSheet!;
        Assert.Empty(second.Elements);

        vm.SelectedCell = new GridPos(0, 0);
        vm.BeginPaste();
        Assert.True(vm.ConfirmPaste());
        vm.SelectedCell = new GridPos(4, 0);
        vm.BeginPaste();
        Assert.True(vm.ConfirmPaste());

        Assert.Equal(6, second.Elements.Count);
        Assert.Equal(6, second.Elements.Select(e => e.Id).Distinct().Count());
        Assert.Equal(3, vm.Document.Sheets[0].Elements.Count);
    }

    /// <summary>テストモード中は貼り付けへ入れぬ。</summary>
    [Fact]
    public void BeginPaste_InTestMode_DoesNothing()
    {
        var vm = CreateViewModelWithCopiedBlock();
        vm.SelectedCell = new GridPos(5, 4);
        vm.IsTestMode = true;

        Assert.False(vm.BeginPaste());
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
    }

    /// <summary>確認モード中にテストモードへ入れば、確認モードは解ける（ゴーストが残らぬ）。</summary>
    [Fact]
    public void EnteringTestMode_WhilePasting_LeavesPasteMode()
    {
        var vm = CreateViewModelWithCopiedBlock();
        vm.SelectedCell = new GridPos(5, 4);
        vm.BeginPaste();

        vm.IsTestMode = true;

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.PastePreview);
    }

    // ---- 自作パーツの定義 ----

    private static PartDefinition CustomPart() => new()
    {
        Id = CustomId,
        Name = "範囲コピー_自作",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.Coil,
        Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
        Primitives = new() { new PartLine(0, 0, 1, 0) },
    };

    /// <summary>
    /// 別の図面へ貼っても自作パーツの定義が付いて行く。ローカルのカタログからパーツが消えた後でも
    /// 貼れること＝コピーの時点で定義を抱えておることを測る（カタログから引き直しておれば落ちる）。
    /// Undo で貼り付けを取り消せば、埋め込みも一緒に戻る。
    /// </summary>
    [Fact]
    public void Paste_IntoAnotherDocument_EmbedsCopiedCustomPartDefinition()
    {
        var vm = CreateViewModel();
        vm.PartPalette.SaveNewPart(CustomPart());
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(CustomId, "SOL1", isOr: false);
        Assert.True(vm.CopySelection());

        vm.PartPalette.DeletePart(vm.PartPalette.Entries.Single(e => e.Definition.Id == CustomId).FilePath);
        vm.NewDocument();
        Assert.Null(vm.Document.Library);
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginPaste();
        // 確定前のゴーストも、写しておいた定義で解決できる。
        Assert.Equal(PartRole.Coil, vm.PastePreview!.Library!.Get(CustomId)!.Role);

        Assert.True(vm.ConfirmPaste());

        var pasted = Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal(CustomId, pasted.PartId);
        Assert.Equal(PartRole.Coil, vm.Document.Library!.Get(CustomId)!.Role);
        Assert.False(PartResolver.IsUnresolvedPartId(pasted, vm.PartLibrary));

        vm.UndoCommand.Execute(null);
        Assert.Null(vm.Document.Library?.Get(CustomId));
    }
}
