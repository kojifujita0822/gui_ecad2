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
}
