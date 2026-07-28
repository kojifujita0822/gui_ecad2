using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-134(Undo対象の穴、殿裁定2026-07-28)の回帰テスト。
/// 設計出典: docs/ecad2-t134-test-design-onmitsu.md(隠密起草)。対象＝明記なき漏れ7件のうち1〜6
/// (7=機器表の型式列は殿裁定で様子見のため対象外)。
/// <para>
/// 設計書0節の骨格に従い、各件について「積むか」「積まぬか(拒否・同値)」「戻るか」の3つを対で持つ。
/// 「積むか」だけを見るテストは、拒否経路で空のスナップショットが積まれる欠陥を検出できない。
/// </para>
/// <para>
/// <b>PR-27対策(設計書6節)</b>: 座標は行と列を取り違えても通る対称な値(0,0)・(2,2)を既定にせず、
/// 非対称な(1,3)を既定とする。原点・端は境界値テストでのみ使う。要素の復元は件数だけでなく
/// Id・Pos・PartId・DeviceNameまでアサーションへ書き込む(件数一致は中身の一致を意味しない)。
/// </para>
/// <para>
/// <b>ドラフト中はUndoCommand.CanExecuteが使えない</b>: 同CanExecuteは!HasAnyDraftを条件に含み
/// (MainWindowViewModel.cs:1995でHasAnyDraftは_orJoinTargetDraftを見る)、合流先確認モード中は
/// 常にfalseになる。よってスナップショットの有無そのものを問う箇所ではUndoManager.CanUndoを直接見る。
/// </para>
/// </summary>
public class T134UndoCoverageTests : ViewModelTestBase
{
    // 非対称な既定座標(PR-27対策)。行1・列3ゆえ、行と列を取り違えれば必ず別のセルを指す。
    private const int Row = 1;
    private const int Col = 3;

    private MainWindowViewModel NewVm()
    {
        var vm = CreateViewModel();
        vm.NewDocument();   // Rows=10, Columns=20 のシート1枚
        return vm;
    }

    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName = "")
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    // ============================================================
    // 2-1. 要素の配置(明記なき漏れ#1)
    // ============================================================

    [Fact]
    public void 配置_P1_空きセルへ置くと要素が増えUndo可能になる()
    {
        var vm = NewVm();
        int before = vm.CurrentSheet!.Elements.Count;

        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X001");

        Assert.Equal(before + 1, vm.CurrentSheet.Elements.Count);
        Assert.True(vm.UndoManager.CanUndo);
    }

    [Fact]
    public void 配置_P2_Undo一回で要素と機器表エントリの双方が戻る()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X001");
        Assert.True(vm.Document.Devices.ByName.ContainsKey("X001"));

        vm.UndoCommand.Execute(null);

        Assert.Empty(vm.CurrentSheet!.Elements);
        // 件数だけでなく機器表からも消えること(設計書P2)
        Assert.False(vm.Document.Devices.ByName.ContainsKey("X001"));
    }

    [Fact]
    public void 配置_P3_占有済みセルでは拒否されスナップショットを積まない()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X001");
        vm.UndoManager.Clear();   // 1件目の配置で積まれた分を除き、以降の増減だけを見る

        // 同じセルへ重ねて置く(ValidatePlacementのIsOccupiedで拒否される)
        vm.SelectedCell = new GridPos(Row, Col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNCId, "X002", isOr: false);

        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.False(vm.UndoManager.CanUndo);   // 拒否経路では積まない
    }

    [Theory]
    // グリッドは Rows=10, Columns=20。行・列を非対称に振り、取り違えを検出できるようにする。
    [InlineData(10, 3, false)]    // 行が下限外(Rows以上)
    [InlineData(1, 20, false)]    // 列が右外(Columns以上)
    [InlineData(-1, 3, false)]    // 行-1: 選択の仕様範囲だが配置は拒否(現仕様の実測、設計書P1注記)
    [InlineData(1, -2, false)]    // 列-2: 同上
    [InlineData(0, 0, true)]      // 左上隅
    [InlineData(9, 19, true)]     // 右下隅
    public void 配置_P4_グリッド境界の内外で配置可否とスナップショットの有無が一致する(int row, int col, bool expectPlaced)
    {
        var vm = NewVm();

        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        Assert.Equal(expectPlaced, vm.CurrentSheet!.Elements.Count == 1);
        // 「置けたら積む・置けねば積まぬ」が一致することが本テストの主眼
        Assert.Equal(expectPlaced, vm.UndoManager.CanUndo);
    }

    [Theory]
    [InlineData(17, true)]    // [17,18,19] ちょうど収まる
    [InlineData(18, false)]   // [18,19,20] 20が範囲外
    public void 配置_P5_幅3パーツの右端はみ出しでも可否と積む積まぬが一致する(int col, bool expectPlaced)
    {
        var vm = NewVm();

        vm.SelectedCell = new GridPos(Row, col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.MotorId, "M1", isOr: false);

        Assert.Equal(expectPlaced, vm.CurrentSheet!.Elements.Count == 1);
        Assert.Equal(expectPlaced, vm.UndoManager.CanUndo);
    }

    [Fact]
    public void 配置_P6_デバイス名なしでも積まれ機器表には触れない()
    {
        var vm = NewVm();
        int devicesBefore = vm.Document.Devices.ByName.Count;

        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, deviceName: "");

        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal(devicesBefore, vm.Document.Devices.ByName.Count);
        Assert.True(vm.UndoManager.CanUndo);

        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.CurrentSheet.Elements);
    }

    [Fact]
    public void 配置_P7_既存デバイス名で配置してもUndoで既存エントリが消えない()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X001");
        // 同じデバイス名で2つ目を配置(新規登録は起きない)
        PlaceAt(vm, Row + 2, Col + 1, BasicPartTemplates.ContactNCId, "X001");

        vm.UndoCommand.Execute(null);   // 2つ目の配置だけを取り消す

        Assert.Single(vm.CurrentSheet!.Elements);
        // 1つ目がまだ参照しているため、機器表エントリは残らねばならぬ
        Assert.True(vm.Document.Devices.ByName.ContainsKey("X001"));
    }

    [Fact]
    public void 配置_C6_新規操作でRedo履歴が消える()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X001");
        vm.UndoCommand.Execute(null);
        Assert.True(vm.UndoManager.CanRedo);

        PlaceAt(vm, Row + 1, Col, BasicPartTemplates.ContactNOId, "X002");

        Assert.False(vm.UndoManager.CanRedo);
    }

    // ============================================================
    // 2-2. 要素の削除(明記なき漏れ#2)
    // ============================================================

    [Fact]
    public void 削除_D1D2_Undo一回で要素がId_Pos_PartId_DeviceNameまで戻る()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X001");
        var original = vm.CurrentSheet!.Elements[0];
        var originalId = original.Id;
        var originalPos = original.Pos;

        Assert.True(vm.DeleteSelectedElement());
        Assert.Empty(vm.CurrentSheet.Elements);

        vm.UndoCommand.Execute(null);

        // 件数一致では「戻った」と言えぬ(PR-27)。中身まで見る
        var restored = Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal(originalId, restored.Id);
        Assert.Equal(originalPos, restored.Pos);
        Assert.Equal(new GridPos(Row, Col), restored.Pos);   // 行と列の取り違えを直に弾く
        Assert.Equal(BasicPartTemplates.ContactNOId, restored.PartId);
        Assert.Equal("X001", restored.DeviceName);
    }

    [Fact]
    public void 削除_D3_未選択では戻り値falseでスナップショットを積まない()
    {
        var vm = NewVm();
        vm.SelectedCell = new GridPos(Row, Col);   // 要素の無いセル
        vm.UndoManager.Clear();

        Assert.False(vm.DeleteSelectedElement());
        Assert.False(vm.UndoManager.CanUndo);
    }

    [Fact]
    public void 削除_D4_他要素も参照する機器名は機器表に残りUndoで二重登録されない()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X001");
        PlaceAt(vm, Row + 2, Col + 1, BasicPartTemplates.ContactNCId, "X001");
        vm.SelectedCell = new GridPos(Row, Col);

        Assert.True(vm.DeleteSelectedElement());
        Assert.True(vm.Document.Devices.ByName.ContainsKey("X001"));   // まだ参照されている

        vm.UndoCommand.Execute(null);

        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.Single(vm.Document.Devices.ByName.Where(kv => kv.Key == "X001"));
    }

    [Fact]
    public void 削除_D5_参照が無い機器名は機器表からも消えUndoで復活する()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X001");
        var deviceClass = vm.Document.Devices.ByName["X001"].Class;

        Assert.True(vm.DeleteSelectedElement());
        Assert.False(vm.Document.Devices.ByName.ContainsKey("X001"));

        vm.UndoCommand.Execute(null);

        // 要素だけ戻って機器表が戻らねば文書が壊れる(設計書D5=Undoの真価)
        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.True(vm.Document.Devices.ByName.ContainsKey("X001"));
        Assert.Equal(deviceClass, vm.Document.Devices.ByName["X001"].Class);
    }

    [Fact]
    public void 削除_D6_デバイス名なしの要素も機器表に触れずUndoで戻る()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, deviceName: "");
        int devicesBefore = vm.Document.Devices.ByName.Count;

        Assert.True(vm.DeleteSelectedElement());
        vm.UndoCommand.Execute(null);

        var restored = Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Null(restored.DeviceName);
        Assert.Equal(devicesBefore, vm.Document.Devices.ByName.Count);
    }

    // ============================================================
    // 2-3. 要素の機器名(明記なき漏れ#3)
    // ============================================================

    [Theory]
    [InlineData("", "CR1")]        // N1: 未設定から命名
    [InlineData("CR1", "")]        // N2: 命名を手放す
    [InlineData("CR1", "CR2")]     // N3: 別名へ改名(未登録先)
    public void 機器名_N1N2N3_変更は積まれUndoで元の名へ戻る(string oldName, string newName)
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, oldName);
        vm.UndoManager.Clear();

        vm.SelectedElementDeviceName = newName;

        Assert.Equal(newName, vm.SelectedElementDeviceName);
        Assert.True(vm.UndoManager.CanUndo);

        vm.UndoCommand.Execute(null);
        vm.SelectedCell = new GridPos(Row, Col);

        Assert.Equal(oldName, vm.SelectedElementDeviceName);
    }

    [Theory]
    [InlineData("CR1")]         // N4: 完全同値
    [InlineData("  CR1  ")]     // N5: Trim後に同値(境界値)
    public void 機器名_N4N5_同値および前後空白のみでは積まない(string newName)
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "CR1");
        vm.UndoManager.Clear();

        vm.SelectedElementDeviceName = newName;

        Assert.False(vm.UndoManager.CanUndo);
        Assert.Equal("CR1", vm.SelectedElementDeviceName);
    }

    [Fact]
    public void 機器名_N3_一括リネームがUndo一回で全要素まとめて戻る()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "CR1");
        PlaceAt(vm, Row + 2, Col + 1, BasicPartTemplates.ContactNCId, "CR1");
        vm.SelectedCell = new GridPos(Row, Col);
        vm.UndoManager.Clear();

        vm.SelectedElementDeviceName = "CR9";   // DeviceRenamer経由で同名の他要素も巻き込む

        Assert.All(vm.CurrentSheet!.Elements, e => Assert.Equal("CR9", e.DeviceName));

        vm.UndoCommand.Execute(null);   // 1回だけ

        // 2件とも戻らねばならぬ。1回で戻らねばここで落ちる(設計書5節=戻る単位の検証)
        Assert.All(vm.CurrentSheet!.Elements, e => Assert.Equal("CR1", e.DeviceName));
        Assert.True(vm.Document.Devices.ByName.ContainsKey("CR1"));
        Assert.False(vm.Document.Devices.ByName.ContainsKey("CR9"));
    }

    [Fact]
    public void 機器名_N6_既存名への統合をUndoすると2エントリが戻る()
    {
        var vm = NewVm();
        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "CR1");
        PlaceAt(vm, Row + 2, Col + 1, BasicPartTemplates.ContactNCId, "CR2");
        vm.SelectedCell = new GridPos(Row, Col);
        vm.UndoManager.Clear();

        vm.SelectedElementDeviceName = "CR2";   // 既に登録済みの名へ統合

        vm.UndoCommand.Execute(null);

        Assert.True(vm.Document.Devices.ByName.ContainsKey("CR1"));
        Assert.True(vm.Document.Devices.ByName.ContainsKey("CR2"));
    }

    // ============================================================
    // 2-4 / 3-4. 合流先の確定(明記なき漏れ#4)と、配置との一体性
    // ============================================================

    [Fact]
    public void 合流先_J1_ドラフトが無い状態で呼んでも何も起きない()
    {
        var vm = NewVm();
        vm.UndoManager.Clear();

        vm.ConfirmOrJoinTarget();

        Assert.False(vm.UndoManager.CanUndo);
        Assert.Empty(vm.CurrentSheet!.Connectors);
    }

    [Fact]
    public void 一体性_X1_isOr配置と合流先確定はUndo一回で配置前へ戻る()
    {
        var vm = NewVm();
        PlaceAt(vm, 0, Col, BasicPartTemplates.ContactNOId, "");   // 合流先となる基準行の要素
        vm.UndoManager.Clear();

        vm.SelectedCell = new GridPos(1, Col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X1", isOr: true);
        Assert.Equal(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);
        vm.ConfirmOrJoinTarget();

        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.NotEmpty(vm.CurrentSheet.Connectors);

        vm.UndoCommand.Execute(null);   // 1回だけ

        // 二重に積んでいれば、この1回では「コネクタ無し・要素あり」の中間状態にしか戻らず落ちる
        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Empty(vm.CurrentSheet.Connectors);
    }

    [Fact]
    public void 一体性_X2_Escによる取消でスナップショットが破棄される()
    {
        var vm = NewVm();
        PlaceAt(vm, 0, Col, BasicPartTemplates.ContactNOId, "");
        vm.UndoManager.Clear();

        vm.SelectedCell = new GridPos(1, Col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X1", isOr: true);
        Assert.True(vm.UndoManager.CanUndo);   // 配置時に積まれている

        vm.CancelOrJoinTarget();   // Esc=要素配置ごと取消

        Assert.Single(vm.CurrentSheet!.Elements);   // 配置した要素は取り消された
        // 残すと「押しても何も変わらぬUndo」が1回生じる(殿裁定=(U-1))
        Assert.False(vm.UndoManager.CanUndo);
    }

    [Fact]
    public void 一体性_X3_isOr_falseの通常配置でも必ず1回積まれる()
    {
        var vm = NewVm();
        vm.UndoManager.Clear();

        PlaceAt(vm, Row, Col, BasicPartTemplates.ContactNOId, "X1");

        Assert.True(vm.UndoManager.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.CurrentSheet!.Elements);
        Assert.False(vm.UndoManager.CanUndo);   // 積まれたのは1回だけ
    }

    [Fact]
    public void 一体性_X4_合流先候補0件では遷移せず通常配置と同じ1回で戻る()
    {
        var vm = NewVm();
        vm.UndoManager.Clear();

        // 上の行に要素が無いため合流先候補は生じない
        vm.SelectedCell = new GridPos(Row, Col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X1", isOr: true);

        Assert.NotEqual(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);
        Assert.True(vm.UndoManager.CanUndo);

        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.CurrentSheet!.Elements);
        Assert.False(vm.UndoManager.CanUndo);
    }

    [Fact]
    public void 一体性_X5_別セルクリックによる巻き戻しではスナップショットを破棄しない()
    {
        // 設計書には無い追加ケース。殿裁定(U-1)の「破棄はEsc経由に限る」という射程を守る網。
        // SelectedCellのsetter経由の巻き戻し(docs/proposed.md P-144)は使い手が取消を意図して
        // おらぬため、スナップショットを残す。
        var vm = NewVm();
        PlaceAt(vm, 0, Col, BasicPartTemplates.ContactNOId, "");
        vm.UndoManager.Clear();

        vm.SelectedCell = new GridPos(1, Col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X1", isOr: true);
        Assert.Equal(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);

        vm.SelectedCell = new GridPos(5, 7);   // 別セルをクリック(取消を意図せぬ巻き戻し)

        Assert.NotEqual(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);
        Assert.True(vm.UndoManager.CanUndo);   // Escと違い破棄しない
    }

    [Fact]
    public void 破棄_深さが一致しなければ破棄しない()
    {
        // DiscardLastSnapshotの安全弁。積んだ後に別の操作が積んでいれば、その操作の
        // スナップショットを誤って捨てぬこと。
        var mgr = new Ecad2.App.Commands.UndoManager();
        var doc = new LadderDocument();
        mgr.RecordSnapshot(doc);
        int depth = mgr.UndoDepth;
        mgr.RecordSnapshot(doc);   // 別の操作が積んだ

        Assert.False(mgr.DiscardLastSnapshot(depth));
        Assert.Equal(depth + 1, mgr.UndoDepth);

        Assert.True(mgr.DiscardLastSnapshot(depth + 1));
        Assert.Equal(depth, mgr.UndoDepth);
    }

    // ============================================================
    // 2-5. 行コメント(明記なき漏れ#5)
    // ============================================================

    [Theory]
    [InlineData("", "起動回路")]           // R1: 新規追加
    [InlineData("起動回路", "停止回路")]    // R2: 書き換え
    [InlineData("起動回路", "")]           // R3: 空文字列による削除
    public void 行コメント_R1R2R3_変更は積まれUndoで元へ戻る(string oldText, string newText)
    {
        var vm = NewVm();
        if (oldText.Length > 0) vm.SetRungComment(Row, oldText);
        vm.UndoManager.Clear();

        vm.SetRungComment(Row, newText);

        Assert.Equal(newText, vm.GetRungComment(Row));
        Assert.True(vm.UndoManager.CanUndo);

        vm.UndoCommand.Execute(null);

        Assert.Equal(oldText, vm.GetRungComment(Row));
    }

    [Theory]
    [InlineData("", "")]                    // R4: エントリ不在＋空入力(退化ケース)
    [InlineData("起動回路", "起動回路")]      // R5: 完全同値
    [InlineData("起動回路", "  起動回路  ")]  // R6: Trim後に同値(境界値)
    public void 行コメント_R4R5R6_同値では積まない(string oldText, string newText)
    {
        var vm = NewVm();
        if (oldText.Length > 0) vm.SetRungComment(Row, oldText);
        vm.UndoManager.Clear();

        vm.SetRungComment(Row, newText);

        Assert.False(vm.UndoManager.CanUndo);
        Assert.Equal(oldText, vm.GetRungComment(Row));
    }

    [Fact]
    public void 行コメント_R7_行範囲の検査は無く範囲外の行でも記録される()
    {
        // 設計書R7=「現仕様を実測して期待値を定めること」への回答。SetRungComment(:2607-2628)に
        // 行範囲の検査は無く、Grid.Rows(=10)を超える行番号でもエントリが作られる。呼び出し元
        // (MainWindow.xaml.cs)で守られている可能性はあるが、ViewModel単体の現仕様はこれである。
        var vm = NewVm();
        vm.UndoManager.Clear();

        vm.SetRungComment(999, "範囲外の行");

        Assert.Equal("範囲外の行", vm.GetRungComment(999));
        Assert.True(vm.UndoManager.CanUndo);

        vm.UndoCommand.Execute(null);
        Assert.Equal("", vm.GetRungComment(999));
    }

    // ============================================================
    // 2-6. 文書情報(明記なき漏れ#6)
    // ============================================================

    private static DocumentInfo SampleInfo() => new()
    {
        CompanyName = "会社甲",
        Title = "配電盤",
        DrawingNo = "D-100",
        Customer = "客先乙",
        Designer = "設計丙",
        Drafter = "製図丁",
        Checker = "検図戊",
        Date = "2026-07-28",
    };

    [Fact]
    public void 文書情報_I1_1項目だけの変更でも積まれUndoで戻る()
    {
        var vm = NewVm();
        vm.ApplyDocumentInfo(SampleInfo());
        vm.UndoManager.Clear();

        var changed = SampleInfo();
        changed.Title = "制御盤";
        vm.ApplyDocumentInfo(changed);

        Assert.Equal("制御盤", vm.Document.Info.Title);
        Assert.True(vm.UndoManager.CanUndo);

        vm.UndoCommand.Execute(null);

        Assert.Equal("配電盤", vm.Document.Info.Title);
    }

    [Fact]
    public void 文書情報_I2_8項目すべての変更がUndo一回で全て戻る()
    {
        var vm = NewVm();
        vm.ApplyDocumentInfo(SampleInfo());
        vm.UndoManager.Clear();

        vm.ApplyDocumentInfo(new DocumentInfo
        {
            CompanyName = "会社己", Title = "分電盤", DrawingNo = "D-200", Customer = "客先庚",
            Designer = "設計辛", Drafter = "製図壬", Checker = "検図癸", Date = "2026-08-01",
        });

        vm.UndoCommand.Execute(null);

        var info = vm.Document.Info;
        Assert.Equal("会社甲", info.CompanyName);
        Assert.Equal("配電盤", info.Title);
        Assert.Equal("D-100", info.DrawingNo);
        Assert.Equal("客先乙", info.Customer);
        Assert.Equal("設計丙", info.Designer);
        Assert.Equal("製図丁", info.Drafter);
        Assert.Equal("検図戊", info.Checker);
        Assert.Equal("2026-07-28", info.Date);
    }

    [Fact]
    public void 文書情報_I3_何も変えなければ積まずMarkDirtyもしない()
    {
        var vm = NewVm();
        vm.ApplyDocumentInfo(SampleInfo());
        vm.UndoManager.Clear();
        bool dirtyBefore = vm.IsDirty;

        vm.ApplyDocumentInfo(SampleInfo());   // 全項目同値

        // 同値で積むと空のUndoが増えるだけでなく、Redo履歴まで消える
        // (FindViewModel.cs:210-213の同値ガード規約、殿裁定2026-07-28=両方正す)
        Assert.False(vm.UndoManager.CanUndo);
        Assert.Equal(dirtyBefore, vm.IsDirty);
    }

    [Fact]
    public void 文書情報_I3_同値では既存のRedo履歴を消さない()
    {
        var vm = NewVm();
        vm.ApplyDocumentInfo(SampleInfo());
        vm.UndoCommand.Execute(null);
        Assert.True(vm.UndoManager.CanRedo);

        vm.ApplyDocumentInfo(vm.Document.Info);   // 現在値と同値

        Assert.True(vm.UndoManager.CanRedo);   // Redo履歴が生き残る
    }

    [Fact]
    public void 文書情報_I4_Revisionsは編集対象外ゆえ変わらない()
    {
        var vm = NewVm();
        vm.Document.Info.Revisions.Add(new RevisionEntry { Rev = "A", Date = "2026-07-01", Description = "初版", By = "甲" });

        var info = SampleInfo();
        info.Revisions.Add(new RevisionEntry { Rev = "Z", Date = "2026-07-28", Description = "無視される", By = "乙" });
        vm.ApplyDocumentInfo(info);

        var rev = Assert.Single(vm.Document.Info.Revisions);
        Assert.Equal("A", rev.Rev);
    }
}
