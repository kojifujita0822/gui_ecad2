using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-107(機器コメント表示・入力機能、殿裁定2026-07-21)の回帰テスト。SelectedElementCommentは
/// Element.Commentへ直書きする単純なstring?プロパティ(SelectedElementLabelDyのような相対値変換は
/// 不要)。UndoManager.RecordSnapshot呼び出しはSelectedElementNotchPosition/LampColor等の標準
/// パターンに倣う(値未変化ならRecordSnapshotしない)。
/// </summary>
public class T107CommentTests : ViewModelTestBase
{
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    [Fact]
    public void SelectedElementComment_未設定なら空文字を返す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");

        Assert.Equal("", vm.SelectedElementComment);
    }

    [Fact]
    public void SelectedElementComment_値を設定するとElement_Commentへ反映されUndo可能になる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");

        vm.SelectedElementComment = "手動リセット";

        Assert.Equal("手動リセット", vm.SelectedElementComment);
        Assert.Equal("手動リセット", vm.SelectedElement!.Comment);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedElementComment_空文字列でクリアするとComment_がnullに戻る()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");
        vm.SelectedElementComment = "手動リセット";

        vm.SelectedElementComment = "";

        Assert.Null(vm.SelectedElement!.Comment);
        Assert.Equal("", vm.SelectedElementComment);
    }

    [Fact]
    public void SelectedElementComment_前後の空白はトリムされる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");

        vm.SelectedElementComment = "  手動リセット  ";

        Assert.Equal("手動リセット", vm.SelectedElementComment);
    }

    [Fact]
    public void SelectedElementComment_値未変化ならUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");
        vm.SelectedElementComment = "手動リセット";
        Assert.True(vm.UndoCommand.CanExecute(null));   // 前提: 値変更でUndo履歴が1件作られる
        vm.UndoCommand.Execute(null);
        Assert.False(vm.UndoCommand.CanExecute(null));   // 前提: Undoで履歴を使い切った

        vm.SelectedElementComment = vm.SelectedElementComment;   // 現在値をそのまま再設定(no-op)

        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedElementComment_未選択時はsetterが無視される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SelectedElementComment = "手動リセット";   // SelectedElement=null、例外を投げず無視される

        Assert.Equal("", vm.SelectedElementComment);
    }
}
