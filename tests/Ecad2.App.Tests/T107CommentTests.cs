using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-107(機器コメント表示・入力機能、殿裁定2026-07-21)の回帰テスト。増分2(殿裁定=デバイス単位で
/// 共有、GX3準拠)によりSelectedElementCommentはDevice.Comment(Document.Devices.ByName経由)への
/// 読み書きに変更された(Element側には持たない、同一デバイス名の全要素間で共有される)。
/// UndoManager.RecordSnapshot呼び出しはSelectedElementNotchPosition/LampColor等の標準パターンに
/// 倣う(値未変化ならRecordSnapshotしない)。
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
        Assert.Equal("手動リセット", vm.Document.Devices.ByName["X001"].Comment);
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

        Assert.Null(vm.Document.Devices.ByName["X001"].Comment);
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

    /// <summary>T-107増分2 DoD(2)の主目的: 同一デバイス名(M1)を持つ複数要素間でコメントが
    /// 共有される(GX3準拠)。要素Aへの入力が、同一デバイス名の要素Bを選択した際にも見えること。</summary>
    [Fact]
    public void SelectedElementComment_同一デバイス名の別要素へ切替ても共有された値が見える()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "M1");
        PlaceAt(vm, 0, 5, BasicPartTemplates.ContactNCId, "M1");   // 同一デバイス名、別要素・別種別

        vm.SelectedCell = new GridPos(0, 0);
        vm.SelectedElementComment = "共有コメント";

        vm.SelectedCell = new GridPos(0, 5);   // 同一デバイス名の別要素へ選択切替

        Assert.Equal("共有コメント", vm.SelectedElementComment);
    }

    /// <summary>DeviceNameが未設定の要素はDeviceに紐づかないため、コメント欄は空文字を返し
    /// setterは無視される(編集不可)。</summary>
    [Fact]
    public void SelectedElementComment_DeviceName未設定の要素は編集不可()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "");   // DeviceName空

        vm.SelectedElementComment = "手動リセット";

        Assert.Equal("", vm.SelectedElementComment);
    }

    /// <summary>
    /// T-107修正差し戻し(隠密静的レビュー、T-079(P-058)同型の通知漏れ)DoD(8)(9)の回帰テスト。
    /// SelectedCellのsetter(要素切替の主経路)でSelectedElementCommentのPropertyChangedが
    /// 発火することを確認する。修正前は発火せずCommentBoxの表示が前の要素の値のまま残留し、
    /// Enter/フォーカス外しで誤って別要素へコミットされる実害があった。
    /// </summary>
    [Fact]
    public void SelectedCell_SwitchingToDifferentElement_RaisesSelectedElementCommentChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");
        vm.SelectedElementComment = "要素Aのコメント";
        PlaceAt(vm, 0, 5, BasicPartTemplates.ContactNOId, "X002");
        vm.SelectedCell = new GridPos(0, 0);   // いったんAへ戻す(次の切替を検出可能にする前提)

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.SelectedCell = new GridPos(0, 5);   // 要素Bへ選択切替

        Assert.Contains(nameof(vm.SelectedElementComment), raised);
    }

    /// <summary>DoD(9)相当: 上記の切替後、SelectedElementCommentのgetterが実際に要素Bの値
    /// (未設定なら空文字)を返すこと(表示更新の実効性、通知が発火するだけでなく値も正しいこと)。</summary>
    [Fact]
    public void SelectedCell_SwitchingToDifferentElement_CommentReflectsNewElement()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");
        vm.SelectedElementComment = "要素Aのコメント";
        PlaceAt(vm, 0, 5, BasicPartTemplates.ContactNOId, "X002");   // 要素Bはコメント未設定

        vm.SelectedCell = new GridPos(0, 5);

        Assert.Equal("", vm.SelectedElementComment);
    }

    /// <summary>DeleteSelectedElement内のSelectedElement系通知箇所への横展開確認。</summary>
    [Fact]
    public void DeleteSelectedElement_RaisesSelectedElementCommentChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");
        vm.SelectedElementComment = "コメント";

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.DeleteSelectedElement();

        Assert.Contains(nameof(vm.SelectedElementComment), raised);
    }

    /// <summary>NotifySelectedElementChanged(共通メソッド)経由の横展開確認。PlaceElementAtSelectedCellが
    /// T-079(P-058)修正で明示的にこれを呼ぶ既存パス(MainWindowViewModelTests.
    /// PlaceElementAtSelectedCell_RaisesSelectedElementDeviceNameChangedと同型)。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_RaisesSelectedElementCommentChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        Assert.Contains(nameof(vm.SelectedElementComment), raised);
    }

    /// <summary>ReplaceDocument系(Document差し替え)経由の横展開確認。</summary>
    [Fact]
    public void NewDocument_RaisesSelectedElementCommentChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");
        vm.SelectedElementComment = "コメント";

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.NewDocument();

        Assert.Contains(nameof(vm.SelectedElementComment), raised);
    }
}
