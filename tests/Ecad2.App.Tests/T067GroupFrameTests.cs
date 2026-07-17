using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-067(GroupFrame作成・編集UI)の回帰テスト。ViewModel層(BeginFrameDraft/AdjustFrameDraft/
/// ConfirmFrameDraft/CancelFrameDraft、BeginDragFrame/UpdateDragFrame/ConfirmDragFrame/
/// CancelDragFrame、DeleteSelectedFrame/RenameSelectedFrame)を検証する。MainWindow.xaml.cs側の
/// マウス配線・キー配線・ヒットテスト・ラベル編集オーバーレイはコードビハインドのためテスト
/// 基盤が無く対象外(T-088/T-070 A-7/T-087と同事情)。
/// </summary>
public class T067GroupFrameTests : ViewModelTestBase
{
    [Fact]
    public void ConfirmFrameDraft_既定サイズ1x1で確定しUndo可能かつ選択状態になる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginFrameDraft(new GridPos(3, 4));

        bool confirmed = vm.ConfirmFrameDraft();

        Assert.True(confirmed);
        var frame = vm.SelectedFrame;
        Assert.NotNull(frame);
        Assert.Equal(new GridPos(3, 4), frame!.TopLeft);
        Assert.Equal(1, frame.Width);
        Assert.Equal(1, frame.Height);
        Assert.True(vm.UndoCommand.CanExecute(null));
        Assert.Null(vm.SelectedCell);
        Assert.Contains(frame, vm.CurrentSheet!.Frames);
    }

    [Fact]
    public void AdjustFrameDraft_矢印キー相当で幅高さを増減する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginFrameDraft(new GridPos(3, 4));

        vm.AdjustFrameDraft(1, 0);   // Right
        vm.AdjustFrameDraft(1, 0);   // Right
        vm.AdjustFrameDraft(0, 1);   // Down

        var preview = vm.FrameDraftPreview;
        Assert.NotNull(preview);
        Assert.Equal(3, preview!.Width);
        Assert.Equal(2, preview.Height);
    }

    [Fact]
    public void AdjustFrameDraft_最小1x1未満には縮小しない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginFrameDraft(new GridPos(3, 4));

        vm.AdjustFrameDraft(-1, -1);   // Left/Up、既に1x1なので変化しないはず

        var preview = vm.FrameDraftPreview;
        Assert.Equal(1, preview!.Width);
        Assert.Equal(1, preview.Height);
    }

    [Fact]
    public void AdjustFrameDraft_グリッド境界を超える拡大は無視する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        // 既定Grid.Columns=40。右端近くから開始し、境界超えの拡大が無視されることを確認する。
        vm.BeginFrameDraft(new GridPos(0, 39));

        vm.AdjustFrameDraft(1, 0);   // Right、Columns範囲外になるため無視されるはず

        var preview = vm.FrameDraftPreview;
        Assert.Equal(1, preview!.Width);
    }

    [Fact]
    public void CancelFrameDraft_何も生成せず取り消す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginFrameDraft(new GridPos(3, 4));

        vm.CancelFrameDraft();

        Assert.Null(vm.FrameDraftPreview);
        Assert.Empty(vm.CurrentSheet!.Frames);
        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedCellを外部から変更すると記入中の枠がキャンセルされる()
    {
        // P-080(3種ドラフトクリア責務分散)対応の直接検証: SelectedCellのsetterにClearFrameDraftIfAny
        // が組み込まれていることを確認する(既存の_connectorDraft/_freeLineDraft/_imageInsertDraftと同型)。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginFrameDraft(new GridPos(3, 4));
        vm.AdjustFrameDraft(1, 1);

        vm.SelectedCell = new GridPos(0, 0);

        Assert.Null(vm.FrameDraftPreview);
        Assert.Empty(vm.CurrentSheet!.Frames);
    }

    private static GroupFrame CreateAndSelectFrame(MainWindowViewModel vm, int row, int col, int width = 2, int height = 2)
    {
        vm.BeginFrameDraft(new GridPos(row, col));
        for (int i = 1; i < width; i++) vm.AdjustFrameDraft(1, 0);
        for (int i = 1; i < height; i++) vm.AdjustFrameDraft(0, 1);
        vm.ConfirmFrameDraft();
        return vm.SelectedFrame!;
    }

    [Fact]
    public void DeleteSelectedFrame_削除しUndo可能になり選択解除される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateAndSelectFrame(vm, 5, 5);

        bool deleted = vm.DeleteSelectedFrame();

        Assert.True(deleted);
        Assert.DoesNotContain(frame, vm.CurrentSheet!.Frames);
        Assert.Null(vm.SelectedFrame);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void RenameSelectedFrame_ラベル変更しUndo可能になる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateAndSelectFrame(vm, 5, 5);

        bool renamed = vm.RenameSelectedFrame("中継BOX");

        Assert.True(renamed);
        Assert.Equal("中継BOX", frame.Label);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void RenameSelectedFrame_同じラベルでは変更扱いにしない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateAndSelectFrame(vm, 5, 5);
        vm.RenameSelectedFrame("中継BOX");
        // Undo履歴を一旦クリアした状態を作れないため、2回目呼び出しの戻り値のみで判定する。

        bool renamedAgain = vm.RenameSelectedFrame("中継BOX");

        Assert.False(renamedAgain);
    }

    [Fact]
    public void BeginDragFrame_UpdateDragFrame_ConfirmDragFrame_移動しUndo記録される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateAndSelectFrame(vm, 5, 5);

        vm.BeginDragFrame(frame);
        vm.UpdateDragFrame(new GridPos(8, 9));
        vm.ConfirmDragFrame();

        Assert.Equal(new GridPos(8, 9), frame.TopLeft);
        Assert.False(vm.IsDraggingFrame);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void UpdateDragFrame_グリッド境界外へは移動しない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateAndSelectFrame(vm, 5, 5);

        vm.BeginDragFrame(frame);
        vm.UpdateDragFrame(new GridPos(-1, 5));

        Assert.Equal(new GridPos(5, 5), frame.TopLeft);
    }

    [Fact]
    public void UpdateDragFrame_他要素との重複は許容する()
    {
        // GroupFrameはグルーピング表示のため占有判定の対象外(要素との重複を許容する)。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(8, 9);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        var frame = CreateAndSelectFrame(vm, 5, 5);

        vm.BeginDragFrame(frame);
        vm.UpdateDragFrame(new GridPos(8, 9));

        Assert.Equal(new GridPos(8, 9), frame.TopLeft);
    }

    [Fact]
    public void CancelDragFrame_開始時位置へ復元する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateAndSelectFrame(vm, 5, 5);

        vm.BeginDragFrame(frame);
        vm.UpdateDragFrame(new GridPos(8, 9));
        vm.CancelDragFrame();

        Assert.Equal(new GridPos(5, 5), frame.TopLeft);
        Assert.False(vm.IsDraggingFrame);
        // CreateAndSelectFrame自体がConfirmFrameDraft(Undo対象)を呼ぶため、CanExecuteは既にtrue。
        // キャンセルで新たなUndo履歴が積まれていないことを、Undo1回で枠作成自体が取り消される
        // (Framesが空になる)ことで検証する。
        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.CurrentSheet!.Frames);
    }

    [Fact]
    public void ConfirmDragFrame_実際に動いていなければUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateAndSelectFrame(vm, 5, 5);

        vm.BeginDragFrame(frame);
        vm.ConfirmDragFrame();

        Assert.Equal(new GridPos(5, 5), frame.TopLeft);
        // CancelDragFrame_開始時位置へ復元するテストと同じ理由でUndo実行結果により検証する。
        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.CurrentSheet!.Frames);
    }

    [Fact]
    public void SelectedCellを外部から変更するとドラッグ中の枠がキャンセルされる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateAndSelectFrame(vm, 5, 5);
        vm.BeginDragFrame(frame);
        vm.UpdateDragFrame(new GridPos(8, 9));

        vm.SelectedCell = new GridPos(0, 0);

        Assert.False(vm.IsDraggingFrame);
        Assert.Equal(new GridPos(5, 5), frame.TopLeft);
    }

    [Fact]
    public void SelectedFrameを選択中に他要素を選択すると排他的に解除される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        CreateAndSelectFrame(vm, 5, 5);
        Assert.True(vm.HasSelectedFrame);

        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        Assert.False(vm.HasSelectedFrame);
        Assert.Null(vm.SelectedFrame);
        Assert.True(vm.HasSelectedElement);
    }

    [Fact]
    public void HasNoPropertySelection_枠選択中はfalseになる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        Assert.True(vm.HasNoPropertySelection);

        CreateAndSelectFrame(vm, 5, 5);

        Assert.False(vm.HasNoPropertySelection);
    }
}
