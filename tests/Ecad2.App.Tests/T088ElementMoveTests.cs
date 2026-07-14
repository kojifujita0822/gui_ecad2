using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-088(基本図形の配置後移動機能新設、調査書docs/ecad2-element-move-feature-survey-onmitsu.md)の
/// 回帰テスト。ViewModel層(BeginDragElement/UpdateDragElement/ConfirmDragElement/CancelDragElement/
/// MoveSelectedElement)を検証する。MainWindow.xaml.cs側のマウス配線・キー配線はコードビハインドの
/// ためテスト基盤が無く対象外(T-070 A-7/T-087と同事情)。
/// </summary>
public class T088ElementMoveTests : ViewModelTestBase
{
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    [Fact]
    public void MoveSelectedElement_空きセルへの移動_成功しUndo可能になる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        bool moved = vm.MoveSelectedElement(0, 1);

        Assert.True(moved);
        Assert.Equal(new GridPos(5, 6), element.Pos);
        Assert.True(vm.UndoCommand.CanExecute(null));
        // T-088隠密静的レビュー指摘(findings1): SelectedCellも新しい位置へ追随し、
        // SelectedElement(SelectedCellから算出)が引き続き同じ要素を指すこと。
        Assert.Equal(new GridPos(5, 6), vm.SelectedCell);
        Assert.Same(element, vm.SelectedElement);
    }

    [Fact]
    public void MoveSelectedElement_連続移動が2回とも成功する()
    {
        // T-088隠密静的レビュー指摘(findings1、最重要の機能不全の直接再現): SelectedCellが
        // 追随しないと1回目の移動後にSelectedElementがnullになり、2回目のMoveSelectedElementが
        // 常にfalseを返す(Ctrl+矢印キー連続移動が1回しか効かない)。
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        bool firstMove = vm.MoveSelectedElement(0, 1);
        bool secondMove = vm.MoveSelectedElement(0, 1);

        Assert.True(firstMove);
        Assert.True(secondMove);
        Assert.Equal(new GridPos(5, 7), element.Pos);
        Assert.Equal(new GridPos(5, 7), vm.SelectedCell);
    }

    [Fact]
    public void MoveSelectedElement_グリッド範囲外_移動しない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");

        bool moved = vm.MoveSelectedElement(-1, 0);

        Assert.False(moved);
        Assert.Equal(new GridPos(0, 0), vm.SelectedElement!.Pos);
    }

    [Fact]
    public void MoveSelectedElement_他要素が占有中のセル_移動しない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        PlaceAt(vm, 5, 6, BasicPartTemplates.ContactNOId, "X002");
        vm.SelectedCell = new GridPos(5, 5);

        bool moved = vm.MoveSelectedElement(0, 1);

        Assert.False(moved);
        Assert.Equal(new GridPos(5, 5), vm.SelectedElement!.Pos);
    }

    [Fact]
    public void BeginDragElement_UpdateDragElement_ConfirmDragElement_移動しUndo記録される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        vm.BeginDragElement(element);
        vm.UpdateDragElement(new GridPos(7, 8));
        vm.ConfirmDragElement();

        Assert.Equal(new GridPos(7, 8), element.Pos);
        Assert.False(vm.IsDraggingElement);
        Assert.True(vm.UndoCommand.CanExecute(null));
        // T-088隠密静的レビュー指摘(findings1): 確定後、SelectedCellも新しい位置へ追随すること。
        Assert.Equal(new GridPos(7, 8), vm.SelectedCell);
        Assert.Same(element, vm.SelectedElement);
    }

    [Fact]
    public void ConfirmDragElement後に再ドラッグできる()
    {
        // T-088隠密静的レビュー指摘(findings1): SelectedCellが追随しないと、確定直後に
        // SelectedElementがnullになり再ドラッグ(BeginDragElement)が実質不可能になる。
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;
        vm.BeginDragElement(element);
        vm.UpdateDragElement(new GridPos(7, 8));
        vm.ConfirmDragElement();

        Assert.NotNull(vm.SelectedElement);
        vm.BeginDragElement(vm.SelectedElement!);
        vm.UpdateDragElement(new GridPos(2, 2));
        vm.ConfirmDragElement();

        Assert.Equal(new GridPos(2, 2), element.Pos);
        Assert.Equal(new GridPos(2, 2), vm.SelectedCell);
    }

    [Fact]
    public void UpdateDragElement_占有先はその場に留まる_自分自身の元位置は占有扱いしない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        PlaceAt(vm, 5, 6, BasicPartTemplates.ContactNOId, "X002");
        vm.SelectedCell = new GridPos(5, 5);
        var element = vm.SelectedElement!;

        vm.BeginDragElement(element);
        // 他要素(X002)が既に占有しているセルへは移動しない。
        vm.UpdateDragElement(new GridPos(5, 6));
        Assert.Equal(new GridPos(5, 5), element.Pos);

        // 自分自身の元位置(占有チェックで自己ヒットしない)へは戻れる。
        vm.UpdateDragElement(new GridPos(6, 5));
        vm.UpdateDragElement(new GridPos(5, 5));
        Assert.Equal(new GridPos(5, 5), element.Pos);
    }

    [Fact]
    public void CancelDragElement_開始時位置へ復元する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        vm.BeginDragElement(element);
        vm.UpdateDragElement(new GridPos(7, 8));
        vm.CancelDragElement();

        Assert.Equal(new GridPos(5, 5), element.Pos);
        Assert.False(vm.IsDraggingElement);
        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void ConfirmDragElement_実際に動いていなければUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;

        vm.BeginDragElement(element);
        vm.ConfirmDragElement();

        Assert.Equal(new GridPos(5, 5), element.Pos);
        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedCellを外部から変更するとドラッグ中状態がキャンセルされる()
    {
        // T-088隠密静的レビュー指摘(findings2): SelectedCellのsetter(唯一のクリア入口一元化
        // パターン)にForceCancelDragElementIfAnyが組み込まれていることの直接検証。
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;
        vm.BeginDragElement(element);
        vm.UpdateDragElement(new GridPos(7, 8));

        vm.SelectedCell = new GridPos(0, 0);

        Assert.False(vm.IsDraggingElement);
        Assert.Equal(new GridPos(5, 5), element.Pos);
    }

    [Fact]
    public void DeleteSelectedElement_ドラッグ中の要素を削除するとドラッグ状態もクリアされる()
    {
        // T-088隠密静的レビュー指摘(findings5): DeleteSelectedElementはSelectedCellのsetterを
        // 経由しないため、findings2の対処だけでは横展開されない箇所への直接検証。BeginDragElement
        // 直後(まだ位置未変更、SelectedCellと要素実位置が一致する状態)でDeleteキー相当を呼ぶ
        // シナリオ(UpdateDragElementで位置を動かすとSelectedElementがSelectedCellから算出
        // できなくなり別のバグ(findings1)を誘発してしまうため、それとは独立に検証する)。
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 5, 5, BasicPartTemplates.ContactNOId, "X001");
        var element = vm.SelectedElement!;
        vm.BeginDragElement(element);

        bool deleted = vm.DeleteSelectedElement();

        Assert.True(deleted);
        Assert.False(vm.IsDraggingElement);
    }
}
