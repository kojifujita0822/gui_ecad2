namespace Ecad2.App.Tests;

/// <summary>
/// 出力パネルの「異常なし」プレースホルダ表示制御（殿ご指摘2026-09-14）の回帰テスト。
/// <para>
/// <b>背景</b>：設計チェック（DRC）で指摘が0件のとき、出力パネルのDataGridは空のままで、
/// 「判定済みで問題なし」なのか「まだ実行していない」のかが見た目で区別できなかった。
/// <see cref="OutputPanelViewModel.ShowNoIssuesMessage"/> を新設し、実行済みかつ0件の時だけ
/// trueにすることで、XAML側のプレースホルダ表示（MainWindow.xaml「異常なし」TextBlock）を制御する。
/// </para>
/// </summary>
public class OutputPanelShowNoIssuesTests : ViewModelTestBase
{
    [Fact]
    public void 未実行のときはfalseである()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.OutputPanel.ShowNoIssuesMessage);
    }

    [Fact]
    public void 指摘0件の図面を実行するとtrueになる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.OutputPanel.RunDrcCommand.Execute(null);

        Assert.Empty(vm.OutputPanel.Diagnostics);
        Assert.True(vm.OutputPanel.ShowNoIssuesMessage);
    }

    [Fact]
    public void ClearResultsを呼ぶとfalseへ戻る()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.OutputPanel.RunDrcCommand.Execute(null);

        vm.OutputPanel.ClearResults();

        Assert.False(vm.OutputPanel.ShowNoIssuesMessage);
    }
}
