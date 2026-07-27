// T-130 診断ログ用の一時改変（本ファイルは一式まるごと一時計装。原因確定後に本ファイルごと除去する）
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using AvalonDock;
using AvalonDock.Layout;

namespace Ecad2.App.Diagnostics;

/// <summary>
/// T-130（シートパネルのドッキング位置ずれ、P-122）の原因確定用の一時診断ログ。
/// <para>
/// 隠密の仮説＝AutoHide解除時に <c>PreviousContainer</c> がnullだと位置情報を捨てて
/// <c>RootPanel</c>端へ新規ペインを生成する分岐（AvalonDock <c>LayoutAnchorable.cs:442-555</c>）を
/// 踏むか否か。この参照は <c>CollectGarbage()</c>（<c>LayoutRoot.cs:361-365</c>）が強制nullクリア
/// しうるため、<b>「いつnullになったか」</b>が判る粒度で記録する。
/// </para>
/// <para>
/// <b>リフレクションを用いる理由</b>：<c>PreviousContainer</c>はAvalonDockの
/// <c>LayoutContent</c>で<c>protected</c>、その実体である<c>ILayoutPreviousContainer</c>は
/// <c>internal</c>インターフェースであり（一次ソースで確認）、ecad2側からは型でアクセスできない。
/// AvalonDock本体には手を入れぬ方針（家老指示）ゆえ、観測手段はリフレクションのみとなる。
/// 取得に失敗しても "?" を記録して先へ進む（診断が本処理を妨げてはならぬ）。
/// </para>
/// <para>
/// <b>【一時計装につき、原因確定後に本ファイルごと除去する】</b>除去時は
/// <c>T-130 診断ログ用の一時改変</c> の文字列検索で呼び出し箇所を洗い出すこと。
/// 他の一時計装と並行する場合は横断で確認する（memory: feedback_temp_instrumentation_removal_discipline）。
/// </para>
/// </summary>
internal static class T130LayoutTrace
{
    /// <summary>追跡対象＝シートパネル（MainWindow.xaml の LayoutAnchorable、Title="シート"）。</summary>
    private const string TargetContentId = "LeftPalette";

    private static readonly object Gate = new();

    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ecad2", "diagnostics");

    private static readonly string LogPath = Path.Combine(LogDirectory, "t130-layout-trace.log");

    /// <summary><c>LayoutContent.PreviousContainer</c>（protected）。1度だけ解決して使い回す。</summary>
    private static readonly PropertyInfo? PreviousContainerProperty =
        typeof(LayoutContent).GetProperty("PreviousContainer", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>直近に書いた状態文字列。同一状態の連続を抑える（LayoutChangedは高頻度で発火しうるため）。</summary>
    private static string? _lastState;

    /// <summary>
    /// 対象パネルの現在の状態を1行記録する。
    /// </summary>
    /// <param name="manager">対象のDockingManager。nullでも落ちない。</param>
    /// <param name="eventName">契機の名前（LayoutChanged・AutoHide前後・起動・終了等）。</param>
    /// <param name="force">
    /// trueなら状態が前回と同一でも必ず記録する。AutoHideの前後・起動・終了など、
    /// 「その時点で確かに通った」ことを残したい打点で使う。
    /// </param>
    /// <param name="note">補足（任意）。</param>
    public static void Log(DockingManager? manager, string eventName, bool force = false, string? note = null)
    {
        try
        {
            string state = BuildState(manager);
            lock (Gate)
            {
                if (!force && string.Equals(state, _lastState, StringComparison.Ordinal)) return;
                _lastState = state;
                string suffix = string.IsNullOrEmpty(note) ? "" : $" note={note}";
                WriteLine($"[{eventName,-24}] {state}{suffix}");
            }
        }
        catch
        {
            // ベストエフォート。診断ログの失敗が殿の実運用を妨げてはならぬ（家老指示DoD 4）。
        }
    }

    /// <summary>セッションの区切りを記録する（アプリ起動ごとに1行）。</summary>
    public static void LogSessionStart()
    {
        try
        {
            lock (Gate)
            {
                _lastState = null;
                WriteLine($"==== session start {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} pid={Environment.ProcessId} ====");
            }
        }
        catch
        {
        }
    }

    private static string BuildState(DockingManager? manager)
    {
        if (manager is null) return "manager=null";
        var layout = manager.Layout;
        if (layout is null) return "layout=null";

        var anchorable = layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == TargetContentId);
        if (anchorable is null) return $"anchorable({TargetContentId})=NOT_FOUND";

        string prev = DescribeContainer(TryGetPreviousContainer(anchorable));
        string parent = anchorable.Parent is null ? "null" : DescribeContainer(anchorable.Parent);
        string grandParent = anchorable.Parent?.Parent is null ? "null" : DescribeContainer(anchorable.Parent.Parent);
        string dockWidth = anchorable.Parent is LayoutAnchorablePane pane ? pane.DockWidth.ToString() : "-";

        return $"ContentId={anchorable.ContentId} IsAutoHidden={anchorable.IsAutoHidden} "
             + $"PrevContainer={prev} PrevIndex={anchorable.PreviousContainerIndex} "
             + $"Parent={parent} GrandParent={grandParent} DockWidth={dockWidth} "
             + $"AutoHideWidth={anchorable.AutoHideWidth} Screen={DescribeScreenPosition(anchorable)}";
    }

    /// <summary>
    /// <c>PreviousContainer</c>をリフレクションで読む。読めなければnullでなく例外を投げず、
    /// 呼び出し元が "?" として扱えるよう <see cref="Unavailable"/> を返す。
    /// </summary>
    private static object? TryGetPreviousContainer(LayoutAnchorable anchorable)
    {
        if (PreviousContainerProperty is null) return Unavailable;
        try
        {
            return PreviousContainerProperty.GetValue(anchorable);
        }
        catch
        {
            return Unavailable;
        }
    }

    /// <summary>リフレクションでの取得自体が失敗したことを表す番人。null（＝本当にnull）と区別する。</summary>
    private static readonly object Unavailable = new();

    private static string DescribeContainer(object? container)
    {
        if (ReferenceEquals(container, Unavailable)) return "?(reflection-failed)";
        if (container is null) return "null";
        string type = container.GetType().Name;
        int identity = RuntimeHelpers.GetHashCode(container);
        string id = TryGetPaneId(container);
        return $"{type}#{identity:X8}(Id={id})";
    }

    /// <summary>
    /// <c>ILayoutPaneSerializable.Id</c>を読む。同インターフェースも明示的実装ゆえ完全修飾名で引く。
    /// 取れなければ "-"（識別子が無くとも、型名＋実体ハッシュで同一性の追跡はできる）。
    /// </summary>
    private static string TryGetPaneId(object container)
    {
        try
        {
            var property = container.GetType().GetProperty(
                "AvalonDock.Layout.ILayoutPaneSerializable.Id",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return property?.GetValue(container) as string ?? "-";
        }
        catch
        {
            return "-";
        }
    }

    /// <summary>
    /// パネル中身の画面座標と実寸。<b>症状（位置ずれ）そのものを数値で採る</b>ための項目。
    /// 未接続・未表示のときは取得できないため "-"。
    /// </summary>
    private static string DescribeScreenPosition(LayoutAnchorable anchorable)
    {
        try
        {
            if (anchorable.Content is not FrameworkElement element) return "-";
            if (!element.IsLoaded || PresentationSource.FromVisual(element) is null) return "-(not-connected)";
            var origin = element.PointToScreen(new Point(0, 0));
            return $"({origin.X:F0},{origin.Y:F0}) {element.ActualWidth:F0}x{element.ActualHeight:F0}";
        }
        catch
        {
            return "-";
        }
    }

    /// <summary>1行追記する。呼び出し元で <see cref="Gate"/> を取得済みであること。</summary>
    private static void WriteLine(string line)
    {
        Directory.CreateDirectory(LogDirectory);
        File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
    }
}
