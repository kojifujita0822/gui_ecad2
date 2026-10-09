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
        CopyrightText.Text = ReadCopyright();
    }

    /// <summary>著作権表記（P-191、殿ご承認2026-09-14）。csproj の <c>&lt;Copyright&gt;</c> が
    /// <see cref="AssemblyCopyrightAttribute"/> として刻まれたものを読む——文言を XAML へも書けば、
    /// exe のプロパティに出る表記と二箇所で食い違いうる。internal はIVT経由のテスト用。</summary>
    internal static string ReadCopyright() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
