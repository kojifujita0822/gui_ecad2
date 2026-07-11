using System.Reflection;
using System.Windows;

namespace Ecad2.App.Views;

/// <summary>バージョン情報を表示する最小モーダルダイアログ（design-brief 4節#4: 非ネスト方針、単一階層のみ）。</summary>
public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "Ecad2" : $"Ecad2 v{version.Major}.{version.Minor}.{version.Build}";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
