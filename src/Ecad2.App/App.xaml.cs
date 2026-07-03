using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Ecad2.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    // 暫定の未処理例外ハンドラ。原因究明のため詳細をログファイルへ残す。
    // ユーザー向けのエラー表示とログの分離(design-brief 3節#8)は本格実装時に対応する。
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string logPath = Path.Combine(Path.GetTempPath(), "ecad2-crash.log");
        File.AppendAllText(logPath, $"{DateTime.Now:O}: {e.Exception}\n\n");
        MessageBox.Show(e.Exception.ToString(), "予期しないエラー", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
