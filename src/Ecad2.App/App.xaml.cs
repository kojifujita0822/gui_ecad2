using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Ecad2.App.Diagnostics;

namespace Ecad2.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // T-039: TraceLogの初期化・クラスハンドラ登録はbase.OnStartup(e)（StartupUriによる
        // MainWindow構築）より前に行い、起動直後のフォーカス遷移も取りこぼさないようにする。
        TraceLog.Initialize(e.Args);
        if (TraceLog.IsEnabled) RegisterTraceClassHandlers();

        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    // T-039(案B、殿裁定2026-07-05): (b)フォーカス遷移・(c)Clickハンドラ発火をクラスハンドラで
    // 横断捕捉する。EventManager.RegisterClassHandlerはルーテッドイベントの経路上にある対象型の
    // 全インスタイベントで発火する(バブリング途中の祖先要素でも発火する)ため、
    // sender==e.OriginalSourceの箇所(実際にイベントが発生した要素そのもの)でのみ記録し、
    // 祖先を辿るたびの重複記録(忍者の現場意見「高頻度・低有用イベントの垂れ流し」懸念)を防ぐ。
    private static void RegisterTraceClassHandlers()
    {
        EventManager.RegisterClassHandler(typeof(UIElement), UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnTraceFocusChanged));
        EventManager.RegisterClassHandler(typeof(UIElement), UIElement.LostKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnTraceFocusChanged));
        EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent,
            new RoutedEventHandler(OnTraceButtonClick));
    }

    private static void OnTraceFocusChanged(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.OriginalSource) || e.OriginalSource is not UIElement element) return;
        string eventName = e.RoutedEvent == UIElement.GotKeyboardFocusEvent ? "GotKeyboardFocus" : "LostKeyboardFocus";
        TraceLog.LogFocus(eventName, ElementIdentity(element), element.GetType().Name);
    }

    private static void OnTraceButtonClick(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.OriginalSource) || e.OriginalSource is not UIElement element) return;
        TraceLog.LogClick(ElementIdentity(element), element.GetType().Name);
    }

    // 忍者の実機検証はUI Automation経由のName指定操作が主体のため、要素識別子はAutomationProperties.Name
    // (UIAが見るName、ツールバーボタン等はx:Name未設定でもこちらは設定済み)を優先し、次いでx:Name、
    // いずれも無ければ型名にフォールバックする(検証記録との突き合わせを容易にするための判断)。
    private static string ElementIdentity(UIElement element)
    {
        string automationName = AutomationProperties.GetName(element);
        if (automationName.Length > 0) return automationName;
        if (element is FrameworkElement { Name.Length: > 0 } fe) return fe.Name;
        return element.GetType().Name;
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
