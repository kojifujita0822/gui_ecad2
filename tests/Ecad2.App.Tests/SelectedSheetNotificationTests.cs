using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-050往復2周目(隠密テスト設計 docs/ecad2-t050-fix2-test-design-onmitsu.md)。
/// Sheets構成が変わる操作(追加・削除・丸ごと差し替え)は、SelectedSheetのPropertyChangedを
/// ちょうど1回だけ発火し、その旧値は操作直前に実際に選択されていたシートと一致しなければならない。
///
/// 検証は ViewModelBase.PropertyChangedForTest(層3フック)で旧値ごと観測する。標準の
/// PropertyChangedEventArgs は PropertyName しか運ばないため、発火回数だけの検証では
/// バグ2(ResetSheetsの旧値タイミング)を捕捉できない(旧値がTraceLog行にしか出ない=P-044前例)。
///
/// RED先行証明(stashで本体修正のみ戻して実測):
/// - バグ1(Add/Delete二重発火): Add/DeleteをSetCurrentSheetIndexCoreではなく公開セッタ経由に
///   戻すと、セッタ内RefreshSelectedSheetのネスト通知が挟まり発火回数が2になる→count assertがRED。
/// - バグ2(ResetSheetsの旧値): ReplaceDocmentの事前捕捉+RefreshSelectedSheetを戻し、ResetSheetsが
///   内部で旧値を捕捉する形へ戻すと、_currentSheetIndex=0先行代入により旧Doc先頭が旧値になる
///   →old-value assertがRED(発火回数は1のままなのでcountでは検出できない)。
/// </summary>
public class SelectedSheetNotificationTests : ViewModelTestBase
{
    /// <summary>SheetNavigation上のSelectedSheet変更通知の旧値を、発火順に記録し始める。
    /// arrange後・act直前に呼ぶこと(arrange中の通知を数に含めないため)。</summary>
    private static List<object?> SubscribeSelectedSheetOldValues(MainWindowViewModel vm)
    {
        var olds = new List<object?>();
        vm.SheetNavigation.PropertyChangedForTest += (name, old) =>
        {
            if (name == nameof(SheetNavigationViewModel.SelectedSheet)) olds.Add(old);
        };
        return olds;
    }

    /// <summary>Sheetsをcount枚に整える。0枚時はfresh vm(濃紺スタート=Document.Sheets空)のまま。
    /// count≥1はNewDocumentで1枚を作り、残りはDocument.Sheetsへ直接追加してResetSheetsでミラー同期する
    /// (既存テストの整え方に倣う)。整え後はCurrentSheetIndex=0(先頭選択)。</summary>
    private static void ArrangeSheets(MainWindowViewModel vm, int count)
    {
        if (count == 0) return;
        vm.NewDocument();
        for (int i = 2; i <= count; i++)
        {
            vm.Document.Sheets.Add(new Sheet
            {
                PageNumber = i,
                Name = $"シート{i}",
                Grid = new GridSpec { Rows = 10, Columns = 20 },
            });
        }
        if (count > 1) vm.SheetNavigation.ResetSheets();
    }

    // ---- ケース1・2: AddCommand ----

    /// <summary>ケース1(wasEmpty=0枚)・ケース2(N=1,3)。追加操作はSelectedSheetをちょうど1回発火し、
    /// 旧値は追加前の選択シート(0枚時はnull)。現行バグは公開セッタ経由のネスト通知で2回発火する。</summary>
    [Theory]
    [InlineData(0)]  // ケース1: wasEmpty → 旧値null
    [InlineData(1)]  // ケース2: N=1
    [InlineData(3)]  // ケース2: N=3
    public void AddCommand_RaisesSelectedSheetChanged_ExactlyOnce(int sheetsBeforeAdd)
    {
        var vm = CreateViewModel();
        ArrangeSheets(vm, sheetsBeforeAdd);
        var expectedOld = vm.SheetNavigation.SelectedSheet; // 追加前の選択(0枚時null)
        var olds = SubscribeSelectedSheetOldValues(vm);

        vm.SheetNavigation.AddCommand.Execute(("新シート", false));

        var only = Assert.Single(olds);
        Assert.Same(expectedOld, only);
    }

    // ---- ケース3: DeleteCommand(境界値: 先頭/中間/末尾/下限) ----

    /// <summary>ケース3a〜3d。削除操作はSelectedSheetをちょうど1回発火し、旧値は削除された(=選択中の)
    /// シート自身。現行バグは公開セッタ経由で縮小済みコレクションの誤った旧値のネスト通知が挟まり2回発火。
    /// 往復3周目補強(テストコード静的レビュー指摘): 発火回数・旧値だけでなく、削除後に実際に選択される
    /// べきシート(SelectedSheetの実値)まで検証する。第3引数は削除前indexで、削除前に期待シート参照を
    /// 捕捉しておく(実装式Math.Minの複製を避け、仕様を試験に書き下す)。</summary>
    [Theory]
    [InlineData(3, 0, 1)]  // 3a: 先頭削除 [A,B,C]→[B,C]、選択=B
    [InlineData(3, 1, 2)]  // 3b: 中間削除 [A,B,C]→[A,C]、選択=C
    [InlineData(3, 2, 1)]  // 3c: 末尾削除 [A,B,C]→[A,B]、選択=B
    [InlineData(2, 1, 0)]  // 3d: 下限 [A,B]→[A]、選択=A
    public void DeleteCommand_RaisesSelectedSheetChanged_ExactlyOnce(int totalSheets, int selectIndex, int expectedIndexBeforeDelete)
    {
        var vm = CreateViewModel();
        ArrangeSheets(vm, totalSheets);
        vm.CurrentSheetIndex = selectIndex;
        var deleted = vm.SheetNavigation.SelectedSheet;
        var expectedSelectedAfterDelete = vm.SheetNavigation.Sheets[expectedIndexBeforeDelete];
        var olds = SubscribeSelectedSheetOldValues(vm);

        vm.SheetNavigation.DeleteCommand.Execute(null);

        var only = Assert.Single(olds);
        Assert.Same(deleted, only);
        Assert.Same(expectedSelectedAfterDelete, vm.SheetNavigation.SelectedSheet);
    }

    // ---- ケース4: ResetSheets経由(ReplaceDocument) ----

    /// <summary>ケース4。Document丸ごと差し替え(新規/開く)はSelectedSheetをちょうど1回発火し、旧値は
    /// 旧Documentで実際に選択されていたシート(旧Doc先頭ではない)。バグ2は_currentSheetIndex=0の
    /// 先行代入により旧Doc先頭を誤った旧値として返す(発火回数は1のままゆえ旧値でしか検出できない)。</summary>
    [Fact]
    public void ReplaceDocument_RaisesSelectedSheetChanged_ExactlyOnceWithPreviousSelectionAsOldValue()
    {
        var vm = CreateViewModel();
        ArrangeSheets(vm, 3);
        vm.CurrentSheetIndex = 1;                              // 旧Docの2枚目を選択
        var previouslySelected = vm.SheetNavigation.SelectedSheet;
        var olds = SubscribeSelectedSheetOldValues(vm);

        vm.NewDocument();                                     // ReplaceDocument経由で差し替え

        var only = Assert.Single(olds);
        Assert.Same(previouslySelected, only);               // 旧Doc先頭ではなく実選択シート
    }

    // ---- ケース5a: 回帰(RenameCommand) ----

    /// <summary>ケース5a。改名は同一シートに留まる操作(index・参照とも不変)ゆえ、SelectedSheetは
    /// ちょうど1回発火し旧値=新値=当該シート(old==newは意図通り)。本修正で回帰していないことの回帰確認。</summary>
    [Fact]
    public void RenameCommand_RaisesSelectedSheetChanged_ExactlyOnce()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var selected = vm.SheetNavigation.SelectedSheet;
        var olds = SubscribeSelectedSheetOldValues(vm);

        vm.SheetNavigation.RenameCommand.Execute("新名称");

        var only = Assert.Single(olds);
        Assert.Same(selected, only);
    }

    // ---- ケース5c: 回帰(CurrentSheetIndexセッタの汎用直接代入=DRCジャンプ相当) ----

    /// <summary>ケース5c。AddCommand/DeleteCommand以外の呼び出し経路(DRC出力パネルのジャンプ等、
    /// CurrentSheetIndexへの外部直接代入)は、公開セッタが従来どおりRefreshSelectedSheetで
    /// ちょうど1回・正しい旧値で通知し続けること。SetCurrentSheetIndexCore抽出が公開挙動を
    /// 変えていないことの回帰確認(通知漏れならcountが0になる)。</summary>
    [Fact]
    public void CurrentSheetIndexSetter_DirectAssignment_RaisesSelectedSheetChanged_ExactlyOnceWithCorrectOldValue()
    {
        var vm = CreateViewModel();
        ArrangeSheets(vm, 3);                                 // シート1,2,3(先頭選択)
        var before = vm.SheetNavigation.SelectedSheet;       // シート1
        var olds = SubscribeSelectedSheetOldValues(vm);

        vm.CurrentSheetIndex = 2;                             // シート3へ直接ジャンプ

        var only = Assert.Single(olds);
        Assert.Same(before, only);
    }

    // ---- ケース6: Undo/Redo経由(T-051バグ修正#2) ----
    //
    // ApplyUndoRedoSnapshotの意味論(隠密テスト設計 docs/ecad2-t051-bugfix-test-design-onmitsu.md
    // §2.1): Undo/Redoは操作直前に選択していたシートへ戻すのではなく、CurrentSheetIndexを新しい
    // シート数の範囲へクランプするのみ。よって「クランプ後のindexが指すシート」が期待値になる。

    /// <summary>S-B2。【RED証明の中核】修正前コードはApplyUndoRedoSnapshotがRefreshSelectedSheet
    /// 相当を呼ばないため、SelectedSheetのPropertyChangedが一切発火しない(count=0でFAIL)。
    /// 実装で判明: AddCommandはBeginInvoke経由(テストはImmediateDispatcherServiceで同期実行)で
    /// 追加直後に新シートへ選択を自動移動する(T-050確立の既存仕様)ため、Undo実行直前の実際の選択は
    /// 「追加したシート3」になっている。旧値はこのタイミング(Undo直前)で捕捉する。</summary>
    [Fact]
    public void UndoCommand_Execute_AfterAddCommand_RaisesSelectedSheetChanged_ExactlyOnce()
    {
        var vm = CreateViewModel();
        ArrangeSheets(vm, 2);
        vm.CurrentSheetIndex = 1;                             // シート2を選択中
        vm.SheetNavigation.AddCommand.Execute(("シート3", false)); // 追加直後、選択はシート3へ自動移動
        var beforeUndo = vm.SheetNavigation.SelectedSheet;    // シート3(Undo実行直前の実際の選択)
        var olds = SubscribeSelectedSheetOldValues(vm);

        vm.UndoCommand.Execute(null);

        var only = Assert.Single(olds);
        Assert.Same(beforeUndo, only);
    }

    /// <summary>S-B2の続き。通知だけでなく、Undo後に実際に正しいシート(index=1、追加前のシート2)を
    /// 指すことを確認する。</summary>
    [Fact]
    public void UndoCommand_Execute_AfterAddCommand_SelectedSheetContentPreserved()
    {
        var vm = CreateViewModel();
        ArrangeSheets(vm, 2);
        vm.CurrentSheetIndex = 1;
        string expectedName = vm.SheetNavigation.SelectedSheet!.Name;
        vm.SheetNavigation.AddCommand.Execute(("シート3", false));

        vm.UndoCommand.Execute(null);

        Assert.Equal(expectedName, vm.SheetNavigation.SelectedSheet?.Name);
    }

    /// <summary>S-B3。削除時にクランプされた位置(index=1、B)がUndo後も維持され、削除対象だった
    /// C(index=2)へは戻らないことを明示する。</summary>
    [Fact]
    public void UndoCommand_Execute_AfterDeleteWithClamp_SelectedSheetStaysAtClampedIndex()
    {
        var vm = CreateViewModel();
        ArrangeSheets(vm, 3);                                 // [A, B, C]
        vm.CurrentSheetIndex = 2;                             // Cを選択中
        vm.SheetNavigation.DeleteCommand.Execute(null);       // クランプでBへ(index=1)
        Assert.Equal("シート2", vm.SheetNavigation.SelectedSheet?.Name); // 前提: クランプ後B

        vm.UndoCommand.Execute(null);                         // Cが復元、3枚に戻る

        Assert.Equal(3, vm.Document.Sheets.Count);
        Assert.Equal("シート2", vm.SheetNavigation.SelectedSheet?.Name); // Cへは戻らない
    }

    /// <summary>S-B4。対称性点検(Redo方向)。クランプ計算はUndo後の現在index(=1)を基準に行われる
    /// ため(意味論§2.1、追加時に自動移動した「シート3(index=2)」という選択位置自体はUndo/Redoの
    /// 対象外)、Redo後もindex=1のまま=Undo直後と同じシート(シート2)を指し続ける。</summary>
    [Fact]
    public void RedoCommand_Execute_AfterUndo_RaisesSelectedSheetChanged_ExactlyOnce()
    {
        var vm = CreateViewModel();
        ArrangeSheets(vm, 2);
        vm.CurrentSheetIndex = 1;
        vm.SheetNavigation.AddCommand.Execute(("シート3", false));
        vm.UndoCommand.Execute(null);
        var beforeRedo = vm.SheetNavigation.SelectedSheet;    // Undo後、クランプでindex=1(シート2)
        string beforeRedoName = beforeRedo!.Name;
        var olds = SubscribeSelectedSheetOldValues(vm);

        vm.RedoCommand.Execute(null);

        var only = Assert.Single(olds);
        Assert.Same(beforeRedo, only);
        Assert.Equal(beforeRedoName, vm.SheetNavigation.SelectedSheet?.Name); // index=1のまま維持
    }

    /// <summary>S-B1。境界値(1↔2枚の往復)。1シートへAddCommandで2枚目を追加後Undoすると、
    /// 残る唯一のシートが選択される。</summary>
    [Fact]
    public void UndoCommand_Execute_OnSingleSheetHistory_SelectsRemainingSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();                                     // シート1枚のみ
        string onlySheetName = vm.SheetNavigation.SelectedSheet!.Name;
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        vm.UndoCommand.Execute(null);

        Assert.Single(vm.Document.Sheets);
        Assert.Equal(onlySheetName, vm.SheetNavigation.SelectedSheet?.Name);
    }
}
