using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-070: 検索・置換バー(FindViewModel)の回帰テスト。殿裁定「置換1件は現在ハイライト中の
/// 要素のみ、機器名の一意性は保証しない」が全置換(DeviceRenamer.Rename)と混同されず
/// 独立して動くことを検証する(調査書DoD(3)5の「1件だけ置換」実装が要る根拠部分)。
/// </summary>
public class FindViewModelTests : ViewModelTestBase
{
    private static ElementInstance MakeContact(int row, int column, string deviceName)
        => new() { Kind = ElementKind.ContactNO, Pos = new GridPos(row, column), DeviceName = deviceName };

    [Fact]
    public void Query_MatchingDevice_JumpsToFirstMatchAndReportsCount()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        var a = MakeContact(0, 0, "X001");
        var b = MakeContact(1, 0, "X001");
        sheet.Elements.Add(a);
        sheet.Elements.Add(b);

        vm.Find.Query = "X001";

        Assert.Equal(2, vm.Find.Matches.Count);
        Assert.Equal("1 / 2", vm.Find.StatusText);
        Assert.Equal(a.Pos, vm.SelectedCell);
    }

    [Fact]
    public void Next_CyclesBackToFirstMatchAfterLast()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        var a = MakeContact(0, 0, "X001");
        var b = MakeContact(1, 0, "X001");
        sheet.Elements.Add(a);
        sheet.Elements.Add(b);
        vm.Find.Query = "X001";

        vm.Find.NextCommand.Execute(null);   // a -> b
        Assert.Equal(b.Pos, vm.SelectedCell);

        vm.Find.NextCommand.Execute(null);   // b -> 循環してa
        Assert.Equal(a.Pos, vm.SelectedCell);
        Assert.Equal("1 / 2", vm.Find.StatusText);
    }

    [Fact]
    public void ReplaceOneCommand_RenamesOnlyCurrentMatch_LeavesOtherSameNameElementsUntouched()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        var a = MakeContact(0, 0, "X001");
        var b = MakeContact(1, 0, "X001");
        sheet.Elements.Add(a);
        sheet.Elements.Add(b);
        vm.Find.Query = "X001";
        vm.Find.ReplaceWith = "X999";

        vm.Find.ReplaceOneCommand.Execute(null);

        Assert.Equal("X999", a.DeviceName);
        Assert.Equal("X001", b.DeviceName);   // 全置換(DeviceRenamer.Rename)と違い、他要素は不変
    }

    [Fact]
    public void ReplaceAllCommand_RenamesEveryMatchingElement()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        var a = MakeContact(0, 0, "X001");
        var b = MakeContact(1, 0, "X001");
        sheet.Elements.Add(a);
        sheet.Elements.Add(b);
        vm.Find.Query = "X001";
        vm.Find.ReplaceWith = "X999";

        vm.Find.ReplaceAllCommand.Execute(null);

        Assert.Equal("X999", a.DeviceName);
        Assert.Equal("X999", b.DeviceName);
        Assert.Empty(vm.Find.Matches);   // 置換後は"X001"に一致する要素がもう無い(再検索の確認)
    }

    [Fact]
    public void Query_Change_RaisesPropertyChangedForMatchesAndStatusText()
    {
        // Matchesは自動実装プロパティのまま代入すると検索結果パネル(DataGrid)のバインドが更新
        // されない実装漏れがあった(静的読解で発見、修正済み)。PropertyChangedの発火自体を検証する
        // 回帰テスト(Matches.Countの直接参照だけでは通知漏れを検出できないため)。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));

        var raised = new List<string?>();
        vm.Find.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Find.Query = "X001";

        Assert.Contains(nameof(FindViewModel.Matches), raised);
        Assert.Contains(nameof(FindViewModel.StatusText), raised);
    }

    [Fact]
    public void IsVisible_SetFalse_ClearsQueryAndMatches()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));
        vm.Find.IsVisible = true;
        vm.Find.Query = "X001";
        Assert.NotEmpty(vm.Find.Matches);

        vm.Find.IsVisible = false;

        Assert.Equal("", vm.Find.Query);
        Assert.Empty(vm.Find.Matches);
    }

    // ===== T-070往復2周目(隠密静的レビュー指摘A-1〜A-9、B-1〜B-3)の回帰テスト =====

    [Fact]
    public void ReplaceOneCommand_TestMode_CanExecuteIsFalse()
    {
        // A-1(最重要): テストモード中は「置換」ボタンが無効化されること(T-061「観察専用」原則)。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));
        vm.Find.Query = "X001";
        vm.Find.ReplaceWith = "X999";
        Assert.True(vm.Find.ReplaceOneCommand.CanExecute(null));

        vm.Mode = AppMode.Test;

        Assert.False(vm.Find.ReplaceOneCommand.CanExecute(null));
    }

    [Fact]
    public void ReplaceAllCommand_TestMode_CanExecuteIsFalse()
    {
        // A-1(最重要): テストモード中は「全置換」ボタンが無効化されること。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));
        vm.Find.Query = "X001";
        vm.Find.ReplaceWith = "X999";
        Assert.True(vm.Find.ReplaceAllCommand.CanExecute(null));

        vm.Mode = AppMode.Test;

        Assert.False(vm.Find.ReplaceAllCommand.CanExecute(null));
    }

    [Fact]
    public void ReplaceOneCommand_PreservesExistingDeviceBomInfo_WhenOldNameNoLongerReferenced()
    {
        // A-2: 参照要素が1個のみの状態で置換すると、旧Deviceオブジェクトがそのまま新名へ移行され
        // BOM情報(Model/Maker/Quantity)が保持されること(新規Deviceで作り直され消失しないこと)。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));
        vm.Document.Devices.ByName["X001"] = new Device { Name = "X001", Model = "MODEL-1", Maker = "MAKER-1", Quantity = 3 };
        vm.Find.Query = "X001";
        vm.Find.ReplaceWith = "X002";

        vm.Find.ReplaceOneCommand.Execute(null);

        Assert.True(vm.Document.Devices.ByName.TryGetValue("X002", out var newDevice));
        Assert.Equal("MODEL-1", newDevice!.Model);
        Assert.Equal("MAKER-1", newDevice.Maker);
        Assert.Equal(3, newDevice.Quantity);
        Assert.False(vm.Document.Devices.ByName.ContainsKey("X001"));
    }

    [Fact]
    public void ReplaceOneCommand_CaseOnlyChange_DoesNotLeaveDuplicateDeviceEntry()
    {
        // A-3: "m1"->"M1"のような大文字小文字違いの置換で、機器表に2エントリ残らないこと
        // (Ordinal比較のContainsKeyのままだと"m1"削除判定がOrdinalIgnoreCaseで誤って
        // 「まだ参照あり」と見なし、旧キーが残留してしまっていた)。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "m1"));
        vm.Document.Devices.ByName["m1"] = new Device { Name = "m1", Model = "MODEL-1" };
        vm.Find.Query = "m1";
        vm.Find.ReplaceWith = "M1";

        vm.Find.ReplaceOneCommand.Execute(null);

        Assert.Single(vm.Document.Devices.ByName);
        Assert.True(vm.Document.Devices.ByName.ContainsKey("M1"));
    }

    [Fact]
    public void ReplaceAllCommand_NotifiesSelectedElementDeviceNameChanged()
    {
        // A-4: 選択中要素を全置換で改名しても、右パネルのDeviceNameBox表示(SelectedElementDeviceName)
        // へ通知が飛ぶこと(ReplaceAllがDeviceRenamer.Renameを直接呼ぶだけで通知漏れしていた)。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        var a = MakeContact(0, 0, "X001");
        sheet.Elements.Add(a);
        vm.SelectedCell = a.Pos;
        vm.Find.Query = "X001";
        vm.Find.ReplaceWith = "X999";

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Find.ReplaceAllCommand.Execute(null);

        Assert.Contains(nameof(MainWindowViewModel.SelectedElementDeviceName), raised);
    }

    [Fact]
    public void ReplaceOneCommand_NoOpReplace_DoesNotDiscardRedoHistory()
    {
        // A-5: 置換後欄に現在名と同じ文字列を入れて「置換」を押しても、実際には何も変わらない
        // ため既存のRedo履歴を破棄しないこと。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.UndoCommand.Execute(null);
        Assert.True(vm.RedoCommand.CanExecute(null));   // 前提: Redo可能状態

        vm.Find.Query = "X001";
        vm.Find.ReplaceWith = "X001";   // 現在名と同じ = no-op

        vm.Find.ReplaceOneCommand.Execute(null);

        Assert.True(vm.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void ReplaceAllCommand_NoOpReplace_DoesNotDiscardRedoHistory()
    {
        // A-5: 全置換版も同様、from==toのno-op置換ではRedo履歴を破棄しないこと。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.UndoCommand.Execute(null);
        Assert.True(vm.RedoCommand.CanExecute(null));

        vm.Find.Query = "X001";
        vm.Find.ReplaceWith = "X001";

        vm.Find.ReplaceAllCommand.Execute(null);

        Assert.True(vm.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void IsVisible_SetFalse_ClearsReplaceWith()
    {
        // A-8: 閉じるとQueryだけでなくReplaceWithもクリアされ、残留した置換後文字列への
        // 意図しない一括置換を防ぐこと。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Find.IsVisible = true;
        vm.Find.ReplaceWith = "X999";

        vm.Find.IsVisible = false;

        Assert.Equal("", vm.Find.ReplaceWith);
    }

    [Fact]
    public void Undo_RefreshesFindResultsAgainstCurrentDocumentWithoutStaleReferences()
    {
        // B-1/D-3(往復2周目指摘): Undo/RedoでDocumentが差し替わっても、Queryに一致する要素が実在
        // するなら再検索により正しくMatchesへ反映されること。B-1初版は単純クリアのみだったため、
        // 検索結果が実データと食い違う「0/0」誤表示が残っていた(D-3)。Matchesの中身も新Documentの
        // 実オブジェクトを指しており、古いSheet参照を保持していないこと(B-1本来の検証観点、
        // 古い参照のままだと検索結果パネルの行クリックでJumpToが無言returnする沈黙不整合が起きる)。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        vm.Find.Query = "X001";
        Assert.NotEmpty(vm.Find.Matches);

        vm.UndoCommand.Execute(null);

        var match = Assert.Single(vm.Find.Matches);
        Assert.NotSame(sheet, match.Sheet);   // 古いDocumentのSheet参照ではない
        Assert.Same(vm.CurrentSheet!.Elements[0], match.Element);
    }

    [Fact]
    public void Query_MatchWhileConnectorDraftPending_DoesNotDiscardDraft()
    {
        // B-2: 縦コネクタ記入中にCtrl+Fで検索し既存機器名を入力しても、自動JumpToが記入中ドラフトを
        // 無警告で破棄しないこと。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(2, 2, "X001"));
        vm.SelectedCell = new GridPos(3, 5);
        vm.BeginConnectorDraft();
        Assert.NotNull(vm.ConnectorDraftPreview);

        vm.Find.Query = "X001";

        Assert.NotNull(vm.ConnectorDraftPreview);
    }

    // ===== T-070往復3周目(隠密2周目レビュー指摘D-1〜D-5)の回帰テスト =====

    [Fact]
    public void ReplaceOneCommand_CaseOnlyChange_MultipleElementsShareOldName_DoesNotLeaveDuplicateDeviceEntry()
    {
        // D-1(A-3修正の不完全性): 旧名を複数要素が共有している場合、大文字小文字違いの置換で
        // 重複エントリ("m1"と"M1")が残らないこと。前回のA-3テストは要素1個のケースのみ
        // カバーしており、複数要素同名という別条件下での再発は未検証だった。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        var a = MakeContact(0, 0, "m1");
        var b = MakeContact(1, 0, "m1");
        sheet.Elements.Add(a);
        sheet.Elements.Add(b);
        vm.Document.Devices.ByName["m1"] = new Device { Name = "m1", Model = "MODEL-1" };
        vm.Find.Query = "m1";
        vm.Find.ReplaceWith = "M1";

        vm.Find.ReplaceOneCommand.Execute(null);

        Assert.Equal("M1", a.DeviceName);
        Assert.Equal("m1", b.DeviceName);   // Bは変更されない(置換1件のみ)
        Assert.Single(vm.Document.Devices.ByName);
        Assert.True(vm.Document.Devices.ByName.ContainsKey("m1"));   // Bがまだ参照するため維持される
    }

    [Fact]
    public void ReplaceAllCommand_ExistingTargetDevice_PreservesTargetBomInfo()
    {
        // D-2: 全置換の置換先が既存Deviceの場合、そのBOM情報を無条件上書きしないこと(単発置換側の
        // MigrateOrRegisterDeviceは既存Device保護を持つが、ReplaceAllDeviceNameはDeviceRenamer.Rename
        // 直呼びのみでこの保護が無く非対称だった)。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "M1"));
        vm.Document.Devices.ByName["M1"] = new Device { Name = "M1", Model = "OLD" };
        vm.Document.Devices.ByName["M2"] = new Device { Name = "M2", Model = "NEW" };
        vm.Find.Query = "M1";
        vm.Find.ReplaceWith = "M2";

        vm.Find.ReplaceAllCommand.Execute(null);

        Assert.Equal("NEW", vm.Document.Devices.ByName["M2"].Model);
        Assert.False(vm.Document.Devices.ByName.ContainsKey("M1"));
    }

    [Fact]
    public void NewDocument_RefreshesFindResults_DoesNotKeepStaleMatches()
    {
        // D-4(PR-05型): 新規作成・開く(ReplaceDocument)経路でも、Find.Matchesが旧Document参照の
        // まま取り残されないこと(B-1対応がApplyUndoRedoSnapshotのみに適用され、ReplaceDocumentへの
        // 横展開が漏れていた)。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(0, 0, "X001"));
        vm.Find.Query = "X001";
        Assert.NotEmpty(vm.Find.Matches);

        vm.NewDocument();

        Assert.Empty(vm.Find.Matches);
    }

    [Fact]
    public void Next_WhileConnectorDraftPending_DoesNotDiscardDraft()
    {
        // D-5: B-2のドラフト保護(RunSearch内の自動JumpToのみ)がNext/Prev/JumpToMatchには適用され
        // ていなかった。「次へ」ボタン等の明示操作経由でも記入中ドラフトを破棄しないこと。
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Elements.Add(MakeContact(2, 2, "X001"));
        sheet.Elements.Add(MakeContact(3, 2, "X001"));
        vm.Find.Query = "X001";
        vm.SelectedCell = new GridPos(5, 5);
        vm.BeginConnectorDraft();
        Assert.NotNull(vm.ConnectorDraftPreview);

        vm.Find.NextCommand.Execute(null);

        Assert.NotNull(vm.ConnectorDraftPreview);
    }
}
