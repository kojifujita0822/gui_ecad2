using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分5(T-131のP-100、プロパティパネルへBreaker3P種別ComboBox新設)の回帰テスト。
/// IsSelectedElementBreaker3P/SelectedElementBreakerTypeの新設プロパティを検証する。
/// T086NotchPositionTestsと同型(GuiEcad CommitBreakerType踏襲、ComboBoxのSelectionChangedが
/// 「確定した一回」と対応する)。配置はPlaceElementByKindTestsに倣いElementKind経路
/// (PlaceElementAtSelectedCell(ElementKind, orient))を使う——Breaker3PはPartDefinitionを
/// 持たぬためPartId経路には乗らぬ(殿裁定2026-07-28=案B)。
/// </summary>
public class T133Increment5BreakerTypeTests : ViewModelTestBase
{
    private const int Rows = 5;
    private const int Columns = 10;
    private static readonly GridPos PlacePos = new(2, 6);

    private MainWindowViewModel ArrangeWithBreaker3P()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid = new GridSpec { Rows = Rows, Columns = Columns };
        vm.CurrentSheet!.MainCircuit = true;
        vm.SelectedCell = PlacePos;
        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");
        return vm;
    }

    [Fact]
    public void IsSelectedElementBreaker3P_Breaker3P選択時true()
    {
        var vm = ArrangeWithBreaker3P();

        Assert.True(vm.IsSelectedElementBreaker3P);
    }

    [Fact]
    public void IsSelectedElementBreaker3P_ContactNO選択時false()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.SelectedCell = new GridPos(0, 0);

        Assert.False(vm.IsSelectedElementBreaker3P);
    }

    [Fact]
    public void SelectedElementBreakerType_未設定なら既定値NFBを返す()
    {
        var vm = ArrangeWithBreaker3P();

        // PlaceElementByKindTests「型はParamsへ入れぬ」が示すとおり、配置直後はParams[Type]が
        // 存在しない。DiagramRenderer.cs:1046のフォールバックと同じ既定値を返すことを確かめる。
        Assert.Equal(BreakerTypeOptions.Default, vm.SelectedElementBreakerType);
        Assert.False(vm.CurrentSheet!.Elements[0].Params.ContainsKey(ParamKeys.Type));
    }

    [Theory]
    [InlineData("MCCB")]
    [InlineData("ELB")]
    public void SelectedElementBreakerType_選択肢の値は反映されUndo可能になる(string input)
    {
        var vm = ArrangeWithBreaker3P();

        vm.SelectedElementBreakerType = input;

        Assert.Equal(input, vm.SelectedElementBreakerType);
        Assert.Equal(input, vm.CurrentSheet!.Elements[0].Params[ParamKeys.Type]);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nfb")]
    [InlineData("XYZ")]
    public void SelectedElementBreakerType_選択肢に無い値は変更しない(string input)
    {
        var vm = ArrangeWithBreaker3P();
        vm.SelectedElementBreakerType = "MCCB";   // 前提となる既存値

        vm.SelectedElementBreakerType = input;

        Assert.Equal("MCCB", vm.SelectedElementBreakerType);
    }

    [Fact]
    public void SelectedElementBreakerType_既定値と同じ値を明示設定してもParamsへ書き込まずUndo履歴も作らない()
    {
        var vm = ArrangeWithBreaker3P();
        vm.UndoManager.Clear();   // 配置自体で積まれた履歴を除いて起点を揃える(T086と同型の配慮)

        vm.SelectedElementBreakerType = BreakerTypeOptions.Default;   // 未設定=効果値NFBと同値

        Assert.False(vm.UndoCommand.CanExecute(null));
        Assert.False(vm.CurrentSheet!.Elements[0].Params.ContainsKey(ParamKeys.Type));
    }

    [Fact]
    public void SelectedElementBreakerType_値未変化ならUndo履歴を作らない()
    {
        var vm = ArrangeWithBreaker3P();
        vm.UndoManager.Clear();
        vm.SelectedElementBreakerType = "ELB";
        Assert.True(vm.UndoCommand.CanExecute(null));   // 前提: 値変更でUndo履歴が1件作られる
        vm.UndoCommand.Execute(null);
        Assert.False(vm.UndoCommand.CanExecute(null));   // 前提: Undoで履歴を使い切った

        vm.SelectedElementBreakerType = vm.SelectedElementBreakerType;   // 現在値をそのまま再設定(no-op)

        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void BreakerTypeChoices_選択肢一覧がBreakerTypeOptionsAllと一致する()
    {
        var vm = CreateViewModel();

        Assert.Equal(BreakerTypeOptions.All, vm.BreakerTypeChoices);
    }

    // ===== 隠密の静的レビュー指摘(docs/ecad2-t133-increment5-review-onmitsu.md)への対処 =====

    /// <summary>要修正2: 値の直読みだけでなく、setterが実際にPropertyChangedを発火することを
    /// 観測する(ViewModelBase.PropertyChangedForTest、SelectedSheetNotificationTestsと同型)。</summary>
    [Fact]
    public void SelectedElementBreakerType_値変更時にPropertyChangedを通知する()
    {
        var vm = ArrangeWithBreaker3P();
        var notified = new List<(string? Name, object? Old)>();
        vm.PropertyChangedForTest += (name, old) => notified.Add((name, old));

        vm.SelectedElementBreakerType = "MCCB";

        Assert.Contains(notified, n => n.Name == nameof(MainWindowViewModel.SelectedElementBreakerType)
            && (string?)n.Old == BreakerTypeOptions.Default);
    }

    /// <summary>
    /// 要修正1(隠密指摘・家老采配)のRED先行証明: <c>SelectedCell</c>のsetter(直接クリック等で
    /// 選択セルが変わる経路。<see cref="PlaceElementAtSelectedCell(ElementKind, string?)"/>が使う
    /// <c>NotifySelectedElementChanged</c>とは別のコード路)は、字下げが一段深い独自の通知ブロックを
    /// 持つ——他の5プロパティ(<c>IsSelectedElementSelectSwitch</c>等)と同じ字面の一括置換では
    /// 一致せず、新設2プロパティが漏れていた(隠密が既存5件=各4箇所・新設2件=3箇所と数え直して発見)。
    /// 台帳DoD「Breaker3P選択時のみ出現」に直に触れる欠陥ゆえ、SelectedCellを動かして戻すだけの
    /// 操作(要素の配置・削除を伴わない)で通知が漏れることを固定する。
    /// </summary>
    [Fact]
    public void SelectedCellの移動で戻った時もIsSelectedElementBreaker3PとSelectedElementBreakerTypeが通知される()
    {
        var vm = ArrangeWithBreaker3P();
        vm.SelectedCell = new GridPos(0, 0);   // Breaker3Pの占有範囲外の空セルへ一旦離れる
        var notified = new List<string?>();
        vm.PropertyChangedForTest += (name, _) => notified.Add(name);

        vm.SelectedCell = PlacePos;   // Breaker3Pへ戻る——SelectedCellのsetter経由のみを通る

        Assert.Contains(nameof(MainWindowViewModel.IsSelectedElementBreaker3P), notified);
        Assert.Contains(nameof(MainWindowViewModel.SelectedElementBreakerType), notified);
    }
}
