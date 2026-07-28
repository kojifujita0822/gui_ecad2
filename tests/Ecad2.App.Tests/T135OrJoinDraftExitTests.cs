using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-135（P-144＝別セルクリックでの巻き戻し、殿裁定2026-07-28＝案(1)）の回帰テスト。
/// 隠密のテスト設計書 <c>docs/ecad2-t135-test-design-onmitsu.md</c> のケースを実装する。
///
/// <para><b>本タスクの本体は「呼び分けの正しさ」である</b>——合流先確認モードを抜ける経路は4つあり、
/// 要素を巻き戻してよいのは<b>取消の意図がある2つ（Esc・文書差し替え）だけ</b>。
/// 別セルクリック・AppMode切替は「取消の意図が無い」ゆえドラフトだけを畳む。</para>
///
/// <para><b>【入力値の選び方】</b>行と列に異なる値を選び、別セルクリックの移動先も行・列とも
/// 配置位置と違う値にした（設計書§2-4）。同一行・同一列で試すと行と列の取り違えが結果に現れぬ。
/// 既定グリッドは <c>NewDocument</c> が作る Rows=10 / Columns=20。</para>
/// </summary>
public class T135OrJoinDraftExitTests : ViewModelTestBase
{
    // 合流先候補になる上の要素（配置行より上のdistinct行が候補になる）。
    private static readonly GridPos UpperElementPos = new(2, 6);
    private static readonly GridPos SecondUpperPos = new(3, 6);
    // OR配置する要素。上の2つと同じ列に置く。
    private static readonly GridPos OrElementPos = new(5, 6);
    // 別セルクリックの移動先。行・列とも他のどれとも違う値にする（対称性を崩す）。
    private static readonly GridPos OtherCellPos = new(8, 13);

    /// <summary>
    /// View層（<c>MainWindow.xaml.cs</c> の <c>PlacementOkButton_Click</c>）がモード遷移時に
    /// <c>StatusMessage</c> へ設定する案内文言。
    /// <para>
    /// <b>【射程の断り】ViewModelのテストからView層は呼べぬゆえ、同じ文字列を手で置いて模倣する。</b>
    /// ゆえに本テスト群が保証するのは「<b>モードを抜けるとき StatusMessage が空になる</b>」ことまでで
    /// あり、「View層が実際に設定した文言が消える」ことまでは保証せぬ——そこは実機確認に委ねる。
    /// </para>
    /// </summary>
    private const string OrJoinHint = "上下キーで合流先候補を切替、Enterで確定、Escで配置ごと取消";

    /// <summary>合流先確認モードに入った状態を作る。候補は1件（上の要素1つ）。</summary>
    private MainWindowViewModel ArrangeInConfirmMode(string orDeviceName = "X002")
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = UpperElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.SelectedCell = OrElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, orDeviceName, isOr: true);

        // 前提の成立を確かめる（ここが崩れれば以降は何も見ていない）。
        Assert.Equal(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);
        vm.StatusMessage = OrJoinHint;   // View層が設定する案内文言を模倣する
        return vm;
    }

    /// <summary>候補が2件ある状態で合流先確認モードに入る（設計書§2-4の境界値）。</summary>
    private MainWindowViewModel ArrangeInConfirmModeWithTwoCandidates()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = UpperElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.SelectedCell = SecondUpperPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X002", isOr: false);
        vm.SelectedCell = OrElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X003", isOr: true);

        Assert.Equal(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);
        vm.StatusMessage = OrJoinHint;
        return vm;
    }

    // ==================================================================
    // 設計書§2-3 状態遷移の両側——出口だけを変えた全経路を1つの表に並べる
    // ==================================================================

    /// <summary>合流先確認モードの出口。<c>ReplaceDocument</c> は旧文書ごと破棄され観測手段が無い
    /// ため（設計書§4）、本Theoryには含めぬ。</summary>
    public enum ExitRoute { Enter, Escape, OtherCellClick, AppModeSwitch }

    private static void Exit(MainWindowViewModel vm, ExitRoute route)
    {
        switch (route)
        {
            case ExitRoute.Enter: vm.ConfirmOrJoinTarget(); break;
            case ExitRoute.Escape: vm.CancelOrJoinTarget(); break;
            case ExitRoute.OtherCellClick: vm.SelectedCell = OtherCellPos; break;
            case ExitRoute.AppModeSwitch: vm.Mode = AppMode.Test; break;
            default: throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    [Theory]
    // 取消の意図がある経路＝要素も機器表エントリも巻き戻る
    [InlineData(ExitRoute.Escape, false)]
    // 取消の意図が無い経路＝いずれも残る（殿裁可＝「残してよい」）
    [InlineData(ExitRoute.Enter, true)]
    [InlineData(ExitRoute.OtherCellClick, true)]
    [InlineData(ExitRoute.AppModeSwitch, true)]
    public void 出口ごとに要素と機器表が残るか否かが分かれる(ExitRoute route, bool expectKept)
    {
        var vm = ArrangeInConfirmMode();
        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.Equal(2, vm.Document.Devices.ByName.Count);

        Exit(vm, route);

        Assert.Equal(expectKept ? 2 : 1, vm.CurrentSheet!.Elements.Count);
        Assert.Equal(expectKept ? 2 : 1, vm.Document.Devices.ByName.Count);
        Assert.Equal(expectKept, vm.Document.Devices.ByName.ContainsKey("X002"));
    }

    [Theory]
    [InlineData(ExitRoute.Enter)]
    [InlineData(ExitRoute.Escape)]
    [InlineData(ExitRoute.OtherCellClick)]
    [InlineData(ExitRoute.AppModeSwitch)]
    public void 出口がどれであれ合流先確認モードからは抜ける(ExitRoute route)
    {
        var vm = ArrangeInConfirmMode();

        Exit(vm, route);

        Assert.NotEqual(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);
        Assert.False(vm.HasAnyDraft);
    }

    // ==================================================================
    // 設計書§2-1 ステータスバーのヒント文言（忍者検出、殿裁定4）
    // ==================================================================

    [Theory]
    [InlineData(ExitRoute.Enter)]
    [InlineData(ExitRoute.Escape)]
    [InlineData(ExitRoute.OtherCellClick)]
    [InlineData(ExitRoute.AppModeSwitch)]
    public void 出口がどれであれヒント文言は消える(ExitRoute route)
    {
        // 忍者の実機所見＝別セルクリックで抜けた後も「上下キーで…」が出たままで、
        // 「モード: 作画／ツール: 選択」と食い違っていた。
        // 出口ごとに直すのではなく、モードを抜ける共通部で消すことで4経路とも揃う。
        var vm = ArrangeInConfirmMode();
        Assert.Equal(OrJoinHint, vm.StatusMessage);

        Exit(vm, route);

        Assert.Equal("", vm.StatusMessage);
    }

    [Fact]
    public void 候補を一度も操作せずに抜けてもヒント文言は消える()
    {
        // 設計書§2-1の境界値＝モードに入った直後（候補未操作）。
        // ArrangeInConfirmMode は MoveOrJoinTargetCandidate を呼んでおらぬゆえ、この状態にあたる。
        var vm = ArrangeInConfirmMode();

        vm.SelectedCell = OtherCellPos;

        Assert.Equal("", vm.StatusMessage);
    }

    // ==================================================================
    // 設計書§2-2 DiscardLastSnapshot を呼ばぬことを UndoDepth で直接測る
    // ==================================================================

    [Theory]
    [InlineData(ExitRoute.OtherCellClick)]
    [InlineData(ExitRoute.AppModeSwitch)]
    public void 非破壊で抜ける経路はUndoDepthを減らさない(ExitRoute route)
    {
        // 設計書§2-2＝「Undoが配置前へ戻る」という結果ではなく、
        // 「DiscardLastSnapshot という関数自体が呼ばれておらぬ」ことを深さで直接測る。
        var vm = ArrangeInConfirmMode();
        int depthAfterPlacement = vm.UndoManager.UndoDepth;

        Exit(vm, route);

        Assert.Equal(depthAfterPlacement, vm.UndoManager.UndoDepth);
    }

    [Fact]
    public void Escで抜ける経路はUndoDepthを一つ減らす()
    {
        // 対で置く。片側だけ直して他方を壊す回帰を検出する（T-134の破棄APIの保全）。
        var vm = ArrangeInConfirmMode();
        int depthAfterPlacement = vm.UndoManager.UndoDepth;

        vm.CancelOrJoinTarget();

        Assert.Equal(depthAfterPlacement - 1, vm.UndoManager.UndoDepth);
    }

    [Fact]
    public void Enter確定はスナップショットを積まない()
    {
        // T-134の意図（配置と合流先確定は1つの操作）が保たれておるかの回帰。
        var vm = ArrangeInConfirmMode();
        int depthAfterPlacement = vm.UndoManager.UndoDepth;

        vm.ConfirmOrJoinTarget();

        Assert.Equal(depthAfterPlacement, vm.UndoManager.UndoDepth);
    }

    [Fact]
    public void 別セルクリックの後もUndoは配置前へ戻る()
    {
        // 侍のT-5＝空振りにならぬこと。要素が残るようになったゆえ、
        // 配置時のスナップショットは「押せば実際に何かが変わる」意味を持つ。
        var vm = ArrangeInConfirmMode();

        vm.SelectedCell = OtherCellPos;
        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);

        vm.UndoCommand.Execute(null);

        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal("X001", vm.CurrentSheet!.Elements[0].DeviceName);
    }

    // ==================================================================
    // 設計書§2-4 対称性・退化性——候補複数件・SelectedIndex≠0・デバイス名空欄
    // ==================================================================

    [Fact]
    public void 候補二件で二番目を選んだ状態から別セルクリックしても要素は残る()
    {
        var vm = ArrangeInConfirmModeWithTwoCandidates();
        vm.MoveOrJoinTargetCandidate(1);   // SelectedIndex を 0 以外へ動かす

        vm.SelectedCell = OtherCellPos;

        Assert.Equal(3, vm.CurrentSheet!.Elements.Count);
        Assert.True(vm.Document.Devices.ByName.ContainsKey("X003"));
        Assert.Equal("", vm.StatusMessage);
    }

    [Fact]
    public void 候補二件で二番目を選んだ状態からEscすると要素は巻き戻る()
    {
        var vm = ArrangeInConfirmModeWithTwoCandidates();
        vm.MoveOrJoinTargetCandidate(1);

        vm.CancelOrJoinTarget();

        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.False(vm.Document.Devices.ByName.ContainsKey("X003"));
    }

    [Fact]
    public void デバイス名空欄で配置した場合も別セルクリックで要素は残る()
    {
        // 機器表への新規登録が起きぬ経路（DeviceWasNewlyRegistered=false）。
        // 巻き戻し側の分岐が機器名の有無で割れるゆえ、非破壊側でも押さえる。
        var vm = ArrangeInConfirmMode(orDeviceName: "");
        Assert.Single(vm.Document.Devices.ByName);   // X001 のみ

        vm.SelectedCell = OtherCellPos;

        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.Single(vm.Document.Devices.ByName);
    }

    [Fact]
    public void デバイス名空欄で配置した場合のEscは機器表に触れずに要素だけ巻き戻す()
    {
        var vm = ArrangeInConfirmMode(orDeviceName: "");

        vm.CancelOrJoinTarget();

        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Single(vm.Document.Devices.ByName);
        Assert.True(vm.Document.Devices.ByName.ContainsKey("X001"));
    }
}
