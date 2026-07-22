using System.Linq;
using System.Windows.Threading;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Simulation;

namespace Ecad2.App.Tests;

/// <summary>
/// T-034: SheetNavigationViewModelのMarkDirty呼び忘れ検出。T-045(P-016対応)で
/// IDispatcherService抽象化を導入し、AddCommand/RenameCommandのWPF Application直接依存を
/// 解消した。CreateViewModel()(ViewModelTestBase)がImmediateDispatcherService(即時同期実行)を
/// 注入するため、従来try/catchで握りつぶしていたNullReferenceExceptionが発生しなくなり、
/// BeginInvoke経由の選択ハイライト同期(SelectedSheet設定・RefreshSelectedSheet)まで
/// 直接検証できるようになった。ただし本番<see cref="WpfDispatcherService"/>は実際のWPF
/// Dispatcherへ非同期でキューイングするのに対し、<see cref="ImmediateDispatcherService"/>は
/// actionを即時同期実行するため、タイミング特性が異なる(増分A補遺、隠密軽微指摘)。
/// </summary>
public class SheetNavigationViewModelTests : ViewModelTestBase
{
    [Fact]
    public void DeleteCommand_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        // 隠密レビュー指摘: CurrentSheetIndexを明示的に切替えないと既定値0のまま
        // (追加した「シート2」ではなく「シート1」が削除される)、テストの意図(追加した
        // シートを削除する)と実装が食い違う。
        vm.CurrentSheetIndex = 1;

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void DeleteCommand_CanExecute_FalseWhenOnlyOneSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.SheetNavigation.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteCommand_CanExecute_TrueWhenMultipleSheets()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();

        Assert.True(vm.SheetNavigation.DeleteCommand.CanExecute(null));
    }

    // --- T-084: シート削除時の後始末2件(P-067=DRC結果破棄案内、P-066=PageNumber欠番警告) ---

    /// <summary>
    /// P-067対応: T-082 MoveSheetCommandと同一パターン。削除によりモデル順序(シート構成)が変わる
    /// 以上、クリアすべきDRC診断が存在する場合はClearResults+殿指定文言をステータスバーへ表示する。
    /// 末尾(PageNumber最大)を削除するため欠番は生じない(P-066側の文言は含まれないことも併せて検証)。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhenDiagnosticsExist_ClearsResultsAndShowsStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;   // シート2(PageNumber=2、末尾)を選択
        vm.OutputPanel.Diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Warning, "DRC-XREF-001", "CR1", "テスト診断",
            new[] { new CircuitRef(1, 1) }));

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Empty(vm.OutputPanel.Diagnostics);
        Assert.Equal("DRC結果が削除されました。DRC再実行してください。", vm.StatusMessage);
    }

    /// <summary>
    /// 殿裁定「案A」の付帯条件(T-082と同型): 何も消えていないのに「削除されました」と表示するのは
    /// 偽になるため、クリアすべき診断が元から存在しない場合はStatusMessageを上書きしない。
    /// 末尾削除ゆえ欠番警告も出ないケース。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhenNoDiagnosticsAndDeletingLastSheet_DoesNotOverwriteStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;   // シート2(PageNumber=2、末尾)を選択
        vm.StatusMessage = "既存の案内";

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal("既存の案内", vm.StatusMessage);
    }

    /// <summary>
    /// P-066対応: PageNumberの実再採番は行わない(現状維持)。末尾以外(PageNumber=2、残り1,3の
    /// 途中)を削除すると、削除後もPageNumber=3のシートが残るため2が欠番になる。この場合のみ
    /// 警告メッセージを表示する。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhenDeletingNonLastSheet_ShowsPageNumberGapWarning()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;   // シート2(PageNumber=2、途中)を選択

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal("シート削除によりページ番号に欠番が生じました。", vm.StatusMessage);
    }

    /// <summary>
    /// P-066の誤表示防止側: 末尾(PageNumber最大=3)を削除した場合は欠番が生じない
    /// (残り1,2のまま連続)ため、警告を表示しない。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhenDeletingLastSheet_DoesNotShowPageNumberGapWarning()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 2;   // シート3(PageNumber=3、末尾)を選択
        vm.StatusMessage = "既存の案内";

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal("既存の案内", vm.StatusMessage);
    }

    /// <summary>
    /// P-067とP-066は独立事象のため、両方該当する場合は一方に上書きされず両方の文言が
    /// 連結して表示されることを検証する。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhenBothDiagnosticsExistAndDeletingNonLastSheet_ShowsBothMessages()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;   // シート2(PageNumber=2、途中)を選択
        vm.OutputPanel.Diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Warning, "DRC-XREF-001", "CR1", "テスト診断",
            new[] { new CircuitRef(1, 1) }));

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal(
            "DRC結果が削除されました。DRC再実行してください。 シート削除によりページ番号に欠番が生じました。",
            vm.StatusMessage);
    }

    // --- T-117(P-104対処): シート削除時の機器表クリーンアップ ---

    /// <summary>削除したシートが保持していた要素のDeviceNameが、他のどのシートのどの要素からも
    /// 参照されなくなった場合、機器表(Document.Devices.ByName)から該当エントリが除去されること
    /// (DeleteSelectedElement/DeleteRowAtCommandの既存クリーンアップパターンをシート削除へ拡張した
    /// 回帰テスト、RowInsertDeleteCommandsTests.DeleteRowAtCommand_Execute_RemovesDeviceTableEntry_
    /// WhenNoLongerReferencedと同型)。</summary>
    [Fact]
    public void DeleteCommand_RemovesDeviceTableEntry_WhenNoLongerReferenced()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 0;   // 要素配置済みのシート1を選択

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.False(vm.Document.Devices.ByName.ContainsKey("X001"));
    }

    /// <summary>対照ケース: 削除したシートのDeviceNameが他シートの別要素から参照されていれば
    /// 機器表エントリは残る。</summary>
    [Fact]
    public void DeleteCommand_KeepsDeviceTableEntry_WhenStillReferencedElsewhere()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        var sheet2 = AddSheet(vm, 2, "シート2");
        sheet2.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "X001", Pos = new GridPos(5, 0) });
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 0;   // 要素配置済みのシート1を選択

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.True(vm.Document.Devices.ByName.ContainsKey("X001"));
    }

    [Fact]
    public void AddCommand_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        // T-041(殿裁定「案1」): AddCommandは(名前, 主回路か)のタプルを受け取る呼び出し規約に変更。
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        Assert.True(vm.IsDirty);
        // 追加したシートが選択状態になることを検証する(BeginInvoke経由の同期、クラスコメント参照)。
        Assert.Same(vm.Document.Sheets[^1], vm.SheetNavigation.SelectedSheet?.Sheet);
    }

    /// <summary>
    /// 増分A補遺(隠密所見1、CONFIRMED)の回帰テスト。ImmediateDispatcherServiceはpriority引数を
    /// 無視して即時実行するため、AddCommand_MarksDirtyだけでは意図的に選ばれた
    /// DispatcherPriority.ContextIdle(T-026実機確認由来)が別の値に変わっても検出できなかった。
    /// </summary>
    [Fact]
    public void AddCommand_DispatchesSelectionSyncWithContextIdlePriority()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        Assert.Equal(DispatcherPriority.ContextIdle, Dispatcher.LastPriority);
    }

    /// <summary>
    /// 増分A補遺(隠密所見2、CONFIRMED)の回帰テスト。ImmediateDispatcherServiceが即時同期実行
    /// するため、AddCommand_MarksDirtyは最終状態(IsDirty)しか見ておらず、MarkDirty()が
    /// BeginInvoke呼び出し前(同期部分)からlambda内(非同期部分)へ移動しても検出できなかった。
    /// BeginInvoke呼び出し直前(action実行前)の時点で既にIsDirtyがtrueであることを検証する。
    /// </summary>
    [Fact]
    public void AddCommand_MarksDirtyBeforeDispatchingSelectionSync()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        bool isDirtyWhenDispatched = false;
        Dispatcher.BeforeInvoke = () => isDirtyWhenDispatched = vm.IsDirty;

        vm.SheetNavigation.AddCommand.Execute(("シート2", false));

        Assert.True(isDirtyWhenDispatched);
    }

    [Fact]
    public void AddCommand_WithMainCircuitTrue_CreatesMainCircuitSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.AddCommand.Execute(("主回路シート", true));

        var addedSheet = vm.Document.Sheets[^1];
        Assert.Equal("主回路シート", addedSheet.Name);
        Assert.True(addedSheet.MainCircuit);
    }

    [Fact]
    public void AddCommand_WithBlankName_FallsBackToAutoNumberedName()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.AddCommand.Execute(("  ", false));

        var addedSheet = vm.Document.Sheets[^1];
        Assert.Equal("シート2", addedSheet.Name);
    }

    /// <summary>
    /// T-041増分5隠密レビュー指摘(観点3 CONFIRMED重大)の回帰テスト。2シート中の先頭(表示中)を
    /// 削除すると、後続シートが繰り上がりCurrentSheetIndexの数値は0のまま変化しない
    /// (Math.Min(0, Sheets.Count-1)=0)。この「数値が偶然一致したまま実体が差し替わる」経路で
    /// 記入中状態(_connectorDraft)が残留し、削除後の(実体としては別の)シートへ誤って確定される
    /// 実害が無いことを検証する。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhileDraftingConnector_WhenIndexNumberStaysSame_CancelsDraftAndPreventsCrossSheetLeak()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        // CurrentSheetIndexは既定の0のまま(先頭シートを表示中)。
        vm.SelectedCell = new GridPos(0, 0);
        vm.BeginConnectorDraft();
        vm.MoveConnectorDraftRow(1);

        // 表示中の先頭シートを削除する。後続シートがindex0へ繰り上がり、
        // CurrentSheetIndex = Math.Min(0, Sheets.Count-1=0) = 0 (数値上は変化なし)。
        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.ConnectorDraftPreview);
        Assert.Empty(vm.CurrentSheet!.Connectors);
    }

    /// <summary>同上、自由線(FreeLine)版。</summary>
    [Fact]
    public void DeleteCommand_WhileDraftingFreeLine_WhenIndexNumberStaysSame_CancelsDraftAndPreventsCrossSheetLeak()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2(主回路)",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
            MainCircuit = true,
        });
        vm.SheetNavigation.ResetSheets();
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 38.5, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.FreeLineDraftPreview);
        bool confirmed = vm.ConfirmFreeLineDraft();
        Assert.False(confirmed);
        Assert.Empty(vm.CurrentSheet!.FreeLines);
    }

    /// <summary>
    /// T-041増分5隠密レビュー指摘(往復3周目、テストカバレッジの隙間対応)。往復1周目の回帰テスト2件は
    /// いずれも削除前にSelectedCellを明示セットしていたため、SelectedCell自身のPropertyChanged
    /// (null→値、削除でnullに戻る際に発火)がCurrentSheetの変更通知漏れを覆い隠してしまい、症状1
    /// (削除でindex数値が変化しない場合にCurrentSheetのPropertyChangedが発火せず再描画が飛ぶ)を
    /// 検出できていなかった。削除前にSelectedCellが既にnull(セル未選択のまま削除する、最も基本的な
    /// 操作)というケースで、CurrentSheetのPropertyChangedが確実に発火することを直接検証する。
    /// </summary>
    [Fact]
    public void DeleteCommand_WhenIndexNumberStaysSameAndSelectedCellAlreadyNull_RaisesCurrentSheetChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        vm.SheetNavigation.ResetSheets();
        // CurrentSheetIndexは既定の0のまま(先頭シートを表示中)。SelectedCellは未選択(null)のまま。
        Assert.Null(vm.SelectedCell);

        bool currentSheetChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.CurrentSheet)) currentSheetChanged = true;
        };

        // 表示中の先頭シートを削除する。後続シートがindex0へ繰り上がり、
        // CurrentSheetIndex = Math.Min(0, Sheets.Count-1=0) = 0 (数値上は変化なし)。
        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.True(currentSheetChanged);
    }

    [Fact]
    public void RenameCommand_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.RenameCommand.Execute("新シート名");

        Assert.True(vm.IsDirty);
    }

    /// <summary>T-118(P-109対処、殿裁定2026-07-22=案A採用)の回帰テスト。RenameCommandは
    /// SheetListItem.Nameのsetter経由でPropertyChanged(nameof(Name))を発火するだけで、
    /// ListBoxの表示更新に必要十分となる(旧実装のSheets.RemoveAt+Insert+BeginInvokeによる
    /// SelectedSheet自体の変更通知パターンは、選択状態管理と衝突し選択色消失の原因だったため廃止)。
    /// 旧テスト(RenameCommand_MarksDirty内でselectedSheetChangedを検証)・
    /// RenameCommand_DispatchesRefreshWithContextIdlePriority・
    /// RenameCommand_MarksDirtyBeforeDispatchingRefreshは、BeginInvoke自体が無くなったことに伴い
    /// 本テストへ置き換える。</summary>
    [Fact]
    public void RenameCommand_RaisesNamePropertyChangedOnSheetListItem_WithoutTouchingSelection()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheetItem = vm.SheetNavigation.SelectedSheet!;
        bool nameChanged = false;
        sheetItem.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SheetListItem.Name)) nameChanged = true;
        };
        bool selectedSheetChanged = false;
        vm.SheetNavigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SheetNavigation.SelectedSheet)) selectedSheetChanged = true;
        };

        vm.SheetNavigation.RenameCommand.Execute("新シート名");

        Assert.True(nameChanged);
        Assert.Equal("新シート名", sheetItem.Name);
        // 選択状態(SelectedSheet自体)には一切触れない設計であることの確認(選択色消失バグの根治)。
        Assert.False(selectedSheetChanged);
        Assert.Same(sheetItem, vm.SheetNavigation.SelectedSheet);
        Assert.Null(Dispatcher.LastPriority);
    }

    /// <summary>DoD(4): 改名対象が非末尾シートの場合でも同じ経路(SheetListItem.Nameのsetterのみ、
    /// RemoveAt+Insertを伴わない)を通ることを確認する。旧実装は「改名対象がリスト末尾でない場合に
    /// 選択色消失」が忍者追加検証で確定した再現条件だった(`docs/ecad2-p104-p109-cause-
    /// investigation-onmitsu.md`追記部)。Sheets[0]が同一インスタンスのまま(コンテナ再生成なし)
    /// であることを確認することで、位置に関わらず選択状態が保持される設計であることを裏付ける。</summary>
    [Fact]
    public void RenameCommand_ForNonLastSheet_DoesNotReplaceItemInstance()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 0;   // 非末尾(先頭、シート1)を選択
        var selectedItem = vm.SheetNavigation.Sheets[0];
        bool nameChanged = false;
        selectedItem.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SheetListItem.Name)) nameChanged = true;
        };

        vm.SheetNavigation.RenameCommand.Execute("改名後");

        Assert.True(nameChanged);
        Assert.Equal("改名後", selectedItem.Name);
        // RemoveAt+Insertされていれば別インスタンスに置き換わるが、新実装では同一インスタンスの
        // ままSheets[0]に留まる(=WPFのSelector選択状態管理と衝突しない)。
        Assert.Same(selectedItem, vm.SheetNavigation.Sheets[0]);
        Assert.Same(selectedItem, vm.SheetNavigation.SelectedSheet);
    }

    /// <summary>
    /// T-050修正(隠密指摘2/経路X)のRED先行証明。DetermineOldSelectedSheetForAddは、AddCommandが
    /// Sheets.Add実行前に呼んで「あるべきoldValue」を決める純粋関数。0枚(初回追加＝追加前は無選択)
    /// ならnull、1枚以上なら追加前に選択されていたシート。0枚のとき敢えて非nullなcurrentを渡し
    /// 「0枚なら常にnull」の契約を突く——旧バグ(Sheets.Add後にgetterを読み新シート自身をoldとして
    /// 返しold==newになる、隠密CONFIRMED)を、この境界(sheetsCountBeforeAdd==0)が検出できるように
    /// する。純粋関数の`sheetsCountBeforeAdd == 0`ガードを外すとInlineData(0)がREDになる。
    /// </summary>
    [Theory]
    [InlineData(0)]  // 追加前0枚 → oldValueはnull(無選択)
    [InlineData(1)]  // 追加前1枚 → oldValueは追加前の選択シート
    [InlineData(3)]  // 追加前3枚 → oldValueは追加前の選択シート
    public void DetermineOldSelectedSheetForAdd_ReturnsNullOnlyWhenSheetsEmpty(int sheetsCountBeforeAdd)
    {
        var current = new SheetListItem(new Sheet { Name = "既存シート", Grid = new GridSpec { Rows = 10, Columns = 20 } });

        var result = SheetNavigationViewModel.DetermineOldSelectedSheetForAdd(sheetsCountBeforeAdd, current);

        if (sheetsCountBeforeAdd == 0)
            Assert.Null(result);
        else
            Assert.Same(current, result);
    }

    // --- T-098(P-105起票、殿裁定2026-07-15): PageNumber採番方式見直し ---

    private static Sheet MakeSheetWithPageNumber(int pageNumber)
        => new() { PageNumber = pageNumber, Name = $"シート{pageNumber}", Grid = new GridSpec { Rows = 10, Columns = 20 } };

    [Fact]
    public void DetermineNextPageNumber_シートが0枚_1を返す()
    {
        var result = SheetNavigationViewModel.DetermineNextPageNumber(Array.Empty<Sheet>());

        Assert.Equal(1, result);
    }

    [Fact]
    public void DetermineNextPageNumber_欠番なし連番_最大PageNumberの次を返す()
    {
        var sheets = new[] { MakeSheetWithPageNumber(1), MakeSheetWithPageNumber(2), MakeSheetWithPageNumber(3) };

        var result = SheetNavigationViewModel.DetermineNextPageNumber(sheets);

        Assert.Equal(4, result);
    }

    /// <summary>
    /// 旧実装(Sheets.Count+1固定)のバグ再現条件。シート1,3が残る(2が欠番)状態でSheets.Count+1を
    /// 計算すると2+1=3となり、既存のPageNumber=3と重複してしまう。新実装は既存最大(3)+1=4を返す。
    /// </summary>
    [Fact]
    public void DetermineNextPageNumber_削除で欠番がある状態_既存最大PageNumberの次を返す()
    {
        var sheets = new[] { MakeSheetWithPageNumber(1), MakeSheetWithPageNumber(3) };

        var result = SheetNavigationViewModel.DetermineNextPageNumber(sheets);

        Assert.Equal(4, result);
    }

    [Fact]
    public void DetermineNextPageNumber_シートの列挙順序が入れ替わっていても最大を正しく検出する()
    {
        var sheets = new[] { MakeSheetWithPageNumber(3), MakeSheetWithPageNumber(1), MakeSheetWithPageNumber(2) };

        var result = SheetNavigationViewModel.DetermineNextPageNumber(sheets);

        Assert.Equal(4, result);
    }

    [Fact]
    public void AddCommand_連続追加_欠番なし状態では従来どおり連番になる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.SheetNavigation.AddCommand.Execute(("シート3", false));

        Assert.Equal(new[] { 1, 2, 3 }, vm.Document.Sheets.Select(s => s.PageNumber));
    }

    /// <summary>
    /// DoD1(殿裁定2026-07-15): 削除で欠番が生じた状態から新規追加した場合、既存最大PageNumber+1が
    /// 付くこと。旧実装(Sheets.Count+1固定)ではシート1,3(2削除後)にシート2が3+1=... ではなく
    /// Sheets.Count(2)+1=3を返し、既存のPageNumber=3と重複するバグがあった。
    /// </summary>
    [Fact]
    public void AddCommand_削除で欠番が生じた状態から追加すると既存最大PageNumberの次が付く()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1;   // シート2(PageNumber=2、途中)を選択
        vm.SheetNavigation.DeleteCommand.Execute(null);   // シート1,3が残り2が欠番になる

        vm.SheetNavigation.AddCommand.Execute(("新シート", false));

        var addedSheet = vm.Document.Sheets[^1];
        Assert.Equal(4, addedSheet.PageNumber);
        // 重複が生じていないことも明示的に確認する。
        Assert.Equal(new[] { 1, 3, 4 }, vm.Document.Sheets.Select(s => s.PageNumber).OrderBy(n => n));
    }

    /// <summary>
    /// DoD3(整合確認): T-084のDeleteCommand欠番警告ロジック(P-066)はPageNumberの値同士の比較のみで
    /// AddCommandの採番方式に依存しないため、T-098修正後も継続して正しく機能する。本テストは
    /// 新しい採番方式で追加したシートを削除した際も、欠番判定(P-066)が正しく動くことを確認する。
    /// </summary>
    [Fact]
    public void DeleteCommand_T098新方式で追加したシートを末尾削除しても欠番警告は出ない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.CurrentSheetIndex = 1;   // シート2(PageNumber=2、末尾)を選択
        vm.StatusMessage = "既存の案内";

        vm.SheetNavigation.DeleteCommand.Execute(null);

        Assert.Equal("既存の案内", vm.StatusMessage);
    }

    // --- T-082: シート並び替え(MoveSheetCommand) ---

    private static Sheet AddSheet(MainWindowViewModel vm, int pageNumber, string name)
    {
        var sheet = new Sheet { PageNumber = pageNumber, Name = name, Grid = new GridSpec { Rows = 10, Columns = 20 } };
        vm.Document.Sheets.Add(sheet);
        return sheet;
    }

    [Fact]
    public void MoveSheetCommand_CanExecute_FalseWhenOnlyOneSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.SheetNavigation.MoveSheetCommand.CanExecute((0, 0)));
    }

    [Fact]
    public void MoveSheetCommand_CanExecute_FalseWhenFromIndexAtStartMovingBeforeStart()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();

        // 先頭シート(index=0)をさらに上(toIndex=-1)へ動かそうとする端の境界。
        Assert.False(vm.SheetNavigation.MoveSheetCommand.CanExecute((0, -1)));
    }

    [Fact]
    public void MoveSheetCommand_CanExecute_FalseWhenFromIndexAtEndMovingPastEnd()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();

        // 末尾シート(index=1)をさらに下(toIndex=2、範囲外)へ動かそうとする端の境界。
        Assert.False(vm.SheetNavigation.MoveSheetCommand.CanExecute((1, 2)));
    }

    [Fact]
    public void MoveSheetCommand_CanExecute_FalseWhenFromEqualsToIndex()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();

        Assert.False(vm.SheetNavigation.MoveSheetCommand.CanExecute((1, 1)));
    }

    [Fact]
    public void MoveSheetCommand_MovesSheetInDocumentAndMirror()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet1 = vm.Document.Sheets[0];
        var sheet2 = AddSheet(vm, 2, "シート2");
        var sheet3 = AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();

        // 末尾(シート3、index=2)を先頭へ移動。
        vm.SheetNavigation.MoveSheetCommand.Execute((2, 0));

        Assert.Equal(new[] { sheet3, sheet1, sheet2 }, vm.Document.Sheets);
        Assert.Equal(new[] { sheet3, sheet1, sheet2 }, vm.SheetNavigation.Sheets.Select(s => s.Sheet));
    }

    [Fact]
    public void MoveSheetCommand_RenumbersPageNumberSequentially()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();

        vm.SheetNavigation.MoveSheetCommand.Execute((2, 0));

        Assert.Equal(new[] { 1, 2, 3 }, vm.Document.Sheets.Select(s => s.PageNumber));
    }

    [Fact]
    public void MoveSheetCommand_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();

        vm.SheetNavigation.MoveSheetCommand.Execute((0, 1));

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void MoveSheetCommand_WhenMovingSelectedSheet_CurrentSheetIndexFollows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet1 = vm.Document.Sheets[0];
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 0; // シート1(移動対象)を選択中
        vm.SelectedCell = new GridPos(0, 0); // 往復3周目(テスト設計書6.1): 保持と同時アサート化

        // 選択中のシート1(index=0)を末尾へ移動。
        vm.SheetNavigation.MoveSheetCommand.Execute((0, 2));

        Assert.Equal(2, vm.CurrentSheetIndex);
        Assert.Same(sheet1, vm.SheetNavigation.SelectedSheet?.Sheet);
        Assert.NotNull(vm.SelectedCell);
    }

    [Fact]
    public void MoveSheetCommand_WhenMovingOtherSheet_SelectedSheetStaysSame()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet1 = vm.Document.Sheets[0];
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 0; // シート1を選択中(移動対象ではない)

        // 選択中でないシート2・シート3(index 1, 2)を入れ替える。
        vm.SheetNavigation.MoveSheetCommand.Execute((2, 1));

        Assert.Same(sheet1, vm.SheetNavigation.SelectedSheet?.Sheet);
        Assert.Equal(0, vm.CurrentSheetIndex);
    }

    /// <summary>
    /// T-082(殿裁定): シート並び替えはUndo対象外(T-051 MVP現行方針、改名・行数変更等と同様)。
    /// RecordSnapshotを呼ばないため、実行後もUndoスタックは空(UndoCommandが不可能)のままとなる。
    /// </summary>
    [Fact]
    public void MoveSheetCommand_DoesNotRecordUndoSnapshot()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();

        vm.SheetNavigation.MoveSheetCommand.Execute((0, 1));

        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    /// <summary>DoD検証観点(保存/読込往復): 並び替え後の順序がGcadSerializer往復で保持される。</summary>
    [Fact]
    public void MoveSheetCommand_OrderIsPreservedAcrossSaveAndLoad()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();

        // 末尾(シート3)を先頭へ移動。
        vm.SheetNavigation.MoveSheetCommand.Execute((2, 0));

        string json = Ecad2.Persistence.GcadSerializer.Serialize(vm.Document);
        var restored = Ecad2.Persistence.GcadSerializer.Deserialize(json);

        Assert.Equal(new[] { "シート3", "シート1", "シート2" }, restored.Sheets.Select(s => s.Name));
        Assert.Equal(new[] { 1, 2, 3 }, restored.Sheets.Select(s => s.PageNumber));
    }

    // --- T-082往復1周目(隠密レビュー指摘): 修正1(所見L型再発)・修正3(通知欠落)の回帰テスト ---

    /// <summary>
    /// 隠密レビュー指摘・要修正1のRED先行証明用回帰テスト。無関係な2シートの入替では選択中シートの
    /// 添字(index)は変化しない。旧実装は`newIndex(選択中シートの新添字)>=0`という条件だけで
    /// 無条件に`SetCurrentSheetIndexCore`を呼んでおり、これは値変化の有無に関わらず常時
    /// `SelectedCell = null`を実行する(T-041由来の既存仕様)ため、選択中セルが理由なく消えていた。
    /// `RenameCommand`が既に対処済みの「所見L」型パターンの再発(同ファイル189-197行のコメント参照)。
    /// </summary>
    [Fact]
    public void MoveSheetCommand_WhenMovingUnrelatedSheets_DoesNotClearSelectedCell()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 0; // シート1を選択中(移動対象ではない)
        vm.SelectedCell = new GridPos(0, 0);

        // 選択中でないシート2・シート3(index 1, 2)を入れ替える。シート1のindexは0のまま不変。
        vm.SheetNavigation.MoveSheetCommand.Execute((2, 1));

        Assert.NotNull(vm.SelectedCell);
    }

    /// <summary>
    /// テストカバレッジ穴埋め(a): 4枚以上のシートで、移動対象でない選択中シートの添字が間接的に
    /// ずれる(=実際にindexが変化する)ケースでは、従来どおりCurrentSheetIndexが正しく追従することを
    /// 検証する(修正1のガードが「変化した時は呼ぶ」を正しく残していることの確認)。
    /// </summary>
    [Fact]
    public void MoveSheetCommand_WhenUnrelatedMoveShiftsSelectedSheetIndex_CurrentSheetIndexFollows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        var sheet3 = AddSheet(vm, 3, "シート3");
        AddSheet(vm, 4, "シート4");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 2; // シート3(index=2)を選択中
        vm.SelectedCell = new GridPos(0, 0); // 往復3周目(テスト設計書6.3): 保持と同時アサート化

        // シート1(index=0)を末尾(index=3)へ移動。シート2,3,4が1つずつ前へ詰まる
        // (結果: [シート2, シート3, シート4, シート1])。シート3の新添字は1。
        vm.SheetNavigation.MoveSheetCommand.Execute((0, 3));

        Assert.Equal(1, vm.CurrentSheetIndex);
        Assert.Same(sheet3, vm.SheetNavigation.SelectedSheet?.Sheet);
        Assert.NotNull(vm.SelectedCell);
    }

    /// <summary>
    /// 隠密レビュー指摘・要修正3のRED先行証明用回帰テスト。旧実装はMoveSheetCommand内で
    /// SelectedSheetのPropertyChangedを一度も発火しない。Add/Delete/Renameは全て
    /// (BeginInvoke経由または同期で)発火させる既定規約を持つ。
    /// </summary>
    [Fact]
    public void MoveSheetCommand_RaisesSelectedSheetPropertyChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();
        bool selectedSheetChanged = false;
        vm.SheetNavigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SheetNavigation.SelectedSheet)) selectedSheetChanged = true;
        };

        vm.SheetNavigation.MoveSheetCommand.Execute((0, 1));

        Assert.True(selectedSheetChanged);
    }

    /// <summary>増分A補遺と同型: MoveSheetCommandのSelectedSheet同期もContextIdle優先度で
    /// ディスパッチされることを検証する(Add/Renameと同一の遅延方式を採用したことの確認)。</summary>
    [Fact]
    public void MoveSheetCommand_DispatchesSelectionSyncWithContextIdlePriority()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();

        vm.SheetNavigation.MoveSheetCommand.Execute((0, 1));

        Assert.Equal(DispatcherPriority.ContextIdle, Dispatcher.LastPriority);
    }

    // --- T-082往復2周目(殿裁定「案A」): 修正2、DRC結果破棄+ステータスバー案内 ---

    /// <summary>
    /// 殿裁定「案A」のRED先行証明用回帰テスト。並び替えでモデル順序(PageNumber)が変わった以上、
    /// 旧文書に紐づくDRC結果は破棄する(ReplaceDocument/Undo-Redoと同じ既存規約)。クリアすべき
    /// 診断が存在する場合のみステータスバーへ殿指定文言を表示する。
    /// </summary>
    [Fact]
    public void MoveSheetCommand_WhenDiagnosticsExist_ClearsResultsAndShowsStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();
        vm.OutputPanel.Diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Warning, "DRC-XREF-001", "CR1", "テスト診断",
            new[] { new CircuitRef(1, 1) }));

        vm.SheetNavigation.MoveSheetCommand.Execute((0, 1));

        Assert.Empty(vm.OutputPanel.Diagnostics);
        Assert.Equal("DRC結果が削除されました。DRC再実行してください。", vm.StatusMessage);
    }

    /// <summary>
    /// 殿裁定「案A」の付帯条件: 何も消えていないのに「削除されました」と表示するのは偽になるため、
    /// クリアすべき診断が元から存在しない場合はStatusMessageを上書きしない。
    /// </summary>
    [Fact]
    public void MoveSheetCommand_WhenNoDiagnostics_DoesNotOverwriteStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();
        vm.StatusMessage = "既存の案内";

        vm.SheetNavigation.MoveSheetCommand.Execute((0, 1));

        Assert.Equal("既存の案内", vm.StatusMessage);
    }

    // --- テストカバレッジ穴埋め: 下限そのもの/上限そのものの実行結果検証 ---

    [Fact]
    public void MoveSheetCommand_MovesFirstSheetDownByOne()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet1 = vm.Document.Sheets[0];
        var sheet2 = AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();

        vm.SheetNavigation.MoveSheetCommand.Execute((0, 1));

        Assert.Equal(new[] { sheet2, sheet1 }, vm.Document.Sheets);
    }

    [Fact]
    public void MoveSheetCommand_MovesLastSheetUpByOne()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet1 = vm.Document.Sheets[0];
        var sheet2 = AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();

        vm.SheetNavigation.MoveSheetCommand.Execute((1, 0));

        Assert.Equal(new[] { sheet2, sheet1 }, vm.Document.Sheets);
    }

    // --- テストカバレッジ穴埋め(c): ドラッグ&ドロップのtoIndex算出ロジック(純粋関数抽出) ---

    /// <summary>
    /// `MainWindow.CalculateSheetDropIndex`(ドロップ位置からtoIndexを算出する純粋関数、隠密レビュー
    /// 指摘=既存のShouldOpenRungCommentEditor等のstatic抽出パターンに倣いテスト容易性のため分離)の
    /// 境界値検証。fromIndexを除去した後の座標系に合わせた補正(除去で後続要素が1つ前へ詰まる)が
    /// 正しいかを検証する。
    /// </summary>
    [Theory]
    [InlineData(0, 2, false, 1)] // 先頭を、末尾寄りシートの上半分(直前)へ→除去後座標系で1つ前倒し
    [InlineData(0, 2, true, 2)]  // 先頭を、末尾寄りシートの下半分(直後)へ→除去後座標系で1つ前倒し
    [InlineData(3, 0, true, 1)]  // 末尾を、先頭シートの下半分(直後)へ→fromIndexより後ろなので補正なし
    [InlineData(3, 0, false, 0)] // 末尾を、先頭シートの上半分(直前)へ→補正なし
    public void CalculateSheetDropIndex_ComputesCorrectInsertionIndex(
        int fromIndex, int targetIndex, bool insertAfter, int expectedToIndex)
    {
        int toIndex = MainWindow.CalculateSheetDropIndex(fromIndex, targetIndex, insertAfter);

        Assert.Equal(expectedToIndex, toIndex);
    }

    // --- T-082往復3周目(隠密テスト設計書、docs/ecad2-t082-fix1-test-design-onmitsu.md):
    //     修正1再修正「実体不変の原則」——移動対象が選択中シート自身でも、SelectedCell・記入中
    //     ドラフトは一切クリアされてはならない。CurrentSheetIndex追従と保持を1本のテストで両立検証。

    /// <summary>設計書6.1(P1、Theory境界値網羅、必須)。移動対象=選択中シート自身のケースで、
    /// SelectedCell保持とCurrentSheetIndex追従を同一テスト内で両立検証する。</summary>
    [Theory]
    [InlineData(2, 0, 1, 1)] // 先頭→末尾(下移動・隣接・端の両方を兼ねる最小ケース)
    [InlineData(2, 1, 0, 0)] // 末尾→先頭(上移動・隣接・端)
    [InlineData(4, 0, 3, 3)] // 先頭→末尾(下移動・端・最大距離)
    [InlineData(4, 3, 0, 0)] // 末尾→先頭(上移動・端・最大距離)
    [InlineData(4, 1, 2, 2)] // 中間シートの隣接下移動(端でも最大距離でもない代表点)
    [InlineData(4, 2, 1, 1)] // 中間シートの隣接上移動
    public void MoveSheetCommand_P1_WhenMovingSelectedSheetItself_PreservesSelectedCellAndFollowsIndex(
        int sheetCount, int fromIndex, int toIndex, int expectedNewIndex)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        for (int i = 1; i < sheetCount; i++) AddSheet(vm, i + 1, $"シート{i + 1}");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = fromIndex;
        vm.SelectedCell = new GridPos(0, 0);
        var movingSheet = vm.Document.Sheets[fromIndex];

        vm.SheetNavigation.MoveSheetCommand.Execute((fromIndex, toIndex));

        Assert.NotNull(vm.SelectedCell);
        Assert.Equal(expectedNewIndex, vm.CurrentSheetIndex);
        Assert.Same(movingSheet, vm.SheetNavigation.SelectedSheet?.Sheet);
    }

    /// <summary>設計書6.2(P1、記入中ドラフト保持、代表1件)。選択中シート自身の移動で、
    /// 記入中の縦コネクタドラフトが破棄されないことを検証する。</summary>
    [Fact]
    public void MoveSheetCommand_P1_WhenMovingSelectedSheetItself_PreservesConnectorDraft()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 0;
        vm.SelectedCell = new GridPos(0, 0);
        vm.BeginConnectorDraft();
        vm.MoveConnectorDraftRow(1);

        // 選択中のシート1(index=0、記入中ドラフト保持)自身を末尾へ移動。
        vm.SheetNavigation.MoveSheetCommand.Execute((0, 1));

        Assert.Equal(ToolMode.PlaceConnector, vm.Tool.Mode);
        Assert.NotNull(vm.ConnectorDraftPreview);
    }

    /// <summary>設計書6.3(P2、間接シフト、最小3枚構成)。選択中でないシートの移動によって
    /// 選択中シートの添字が間接的にシフトするケースでも、SelectedCell保持・CurrentSheetIndex追従・
    /// SelectedSheet実体維持の3点が同時に成立することを検証する。</summary>
    [Fact]
    public void MoveSheetCommand_P2_ThreeSheets_IndirectShift_PreservesSelectedCellAndFollowsIndex()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet2 = AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 1; // シート2(index=1)を選択中
        vm.SelectedCell = new GridPos(0, 0);

        // シート1(index=0)を末尾(index=2)へ移動([シート2, シート3, シート1])。シート2の新添字は0。
        vm.SheetNavigation.MoveSheetCommand.Execute((0, 2));

        Assert.NotNull(vm.SelectedCell);
        Assert.Equal(0, vm.CurrentSheetIndex);
        Assert.Same(sheet2, vm.SheetNavigation.SelectedSheet?.Sheet);
    }

    /// <summary>設計書6.4(P3、添字不変、任意の穴埋め)。対称性点検表の空欄(P3×記入中ドラフト)を
    /// 埋める。選択中でない2シートの入替(選択中シートの添字も不変)で、記入中の縦コネクタドラフトが
    /// 破棄されないことを検証する。</summary>
    [Fact]
    public void MoveSheetCommand_P3_WhenMovingUnrelatedSheets_PreservesConnectorDraft()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        AddSheet(vm, 2, "シート2");
        AddSheet(vm, 3, "シート3");
        vm.SheetNavigation.ResetSheets();
        vm.CurrentSheetIndex = 0; // シート1を選択中(移動対象ではない、添字も不変)
        vm.SelectedCell = new GridPos(0, 0);
        vm.BeginConnectorDraft();
        vm.MoveConnectorDraftRow(1);

        // 選択中でないシート2・シート3(index 1, 2)を入れ替える。シート1のindexは0のまま不変。
        vm.SheetNavigation.MoveSheetCommand.Execute((2, 1));

        Assert.Equal(ToolMode.PlaceConnector, vm.Tool.Mode);
        Assert.NotNull(vm.ConnectorDraftPreview);
    }
}
