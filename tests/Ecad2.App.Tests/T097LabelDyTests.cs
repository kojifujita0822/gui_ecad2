using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-097(ラベル高さオフセット・LabelDy入力UI新設、殿裁定2026-07-15)の回帰テスト。
/// SelectedElementLabelDyは種別既定値(ElementCatalog.DefaultLabelDy)を0とした相対オフセット方式
/// (殿裁定=現在の高さを0として+-で上下)。実体のParams[LabelDy]は絶対値で保持する
/// (DrawElementLabelが個別値優先・無ければ既定値を使う設計のため)。T096SetpointTests/
/// T085LampColorTestsと同型の構成。
/// </summary>
public class T097LabelDyTests : ViewModelTestBase
{
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    [Fact]
    public void SelectedElementLabelDy_未設定なら相対0を返す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");

        Assert.Equal("0", vm.SelectedElementLabelDy);
    }

    [Theory]
    [InlineData("2", "2")]
    [InlineData("-3.5", "-3.5")]
    [InlineData("0.5", "0.5")]
    public void SelectedElementLabelDy_範囲内の値は反映されUndo可能になる(string input, string expected)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");

        vm.SelectedElementLabelDy = input;

        Assert.Equal(expected, vm.SelectedElementLabelDy);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedElementLabelDy_絶対値としてParamsへ反映される()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");   // DefaultLabelDy(Coil) = -5.72(T-097補正後)

        vm.SelectedElementLabelDy = "2";

        Assert.Equal("-3.72", vm.SelectedElement!.Params[ParamKeys.LabelDy]);
    }

    [Fact]
    public void SelectedElementLabelDy_種別が異なれば絶対値の基準点も異なる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");   // DefaultLabelDy(ContactNO) = -1.5

        vm.SelectedElementLabelDy = "1";

        Assert.Equal("-0.5", vm.SelectedElement!.Params[ParamKeys.LabelDy]);
    }

    [Fact]
    public void SelectedElementLabelDy_相対0を設定するとParamsエントリが削除され既定へ戻る()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");
        vm.SelectedElementLabelDy = "2";
        Assert.True(vm.SelectedElement!.Params.ContainsKey(ParamKeys.LabelDy));   // 前提: 値設定でエントリが作られる

        vm.SelectedElementLabelDy = "0";

        Assert.False(vm.SelectedElement!.Params.ContainsKey(ParamKeys.LabelDy));
        Assert.Equal("0", vm.SelectedElementLabelDy);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void SelectedElementLabelDy_非数値または非有限値は値を変更しない(string input)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");
        vm.SelectedElementLabelDy = "3";   // 前提となる既存値

        vm.SelectedElementLabelDy = input;

        Assert.Equal("3", vm.SelectedElementLabelDy);
    }

    /// <summary>
    /// T-097差し戻し2周目(忍者実機確認NG、docs-notes/ecad2-t097-verify-ninja.md観点(2))の回帰テスト。
    /// LabelDy欄をCtrl+A→Deleteで空文字列にしてTabで確定する操作(=クリア)は、明示的な「0」入力と
    /// 同じく既定へ戻る(Params[LabelDy]エントリ削除)べきだが、旧実装は空文字列をTryParse失敗経路
    /// (非数値として無効)に流してしまい直前値へロールバックしていた。
    /// </summary>
    [Fact]
    public void SelectedElementLabelDy_空文字列でクリアすると既定へ戻る()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");
        vm.SelectedElementLabelDy = "3";
        Assert.True(vm.SelectedElement!.Params.ContainsKey(ParamKeys.LabelDy));   // 前提: 値設定でエントリが作られる

        vm.SelectedElementLabelDy = "";

        Assert.False(vm.SelectedElement!.Params.ContainsKey(ParamKeys.LabelDy));
        Assert.Equal("0", vm.SelectedElementLabelDy);
    }

    [Fact]
    public void SelectedElementLabelDy_値未変化ならUndo履歴を作らない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");
        vm.SelectedElementLabelDy = "3";
        Assert.True(vm.UndoCommand.CanExecute(null));   // 前提: 値変更でUndo履歴が1件作られる
        vm.UndoCommand.Execute(null);
        Assert.False(vm.UndoCommand.CanExecute(null));   // 前提: Undoで履歴を使い切った

        vm.SelectedElementLabelDy = vm.SelectedElementLabelDy;   // 現在値をそのまま再設定(no-op)

        Assert.False(vm.UndoCommand.CanExecute(null));
    }
}
