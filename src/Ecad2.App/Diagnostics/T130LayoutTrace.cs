// T-130 診断ログ用の一時改変（本ファイルは一式まるごと一時計装。原因確定後に本ファイルごと除去する）
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
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
/// <b>【観測先の訂正・2周目】</b>初版は <c>LayoutAnchorable</c> 自身の <c>PreviousContainer</c> を
/// 見ていたが誤りであった。一次ソースで確認した実際の姿は次のとおり：
/// <list type="bullet">
/// <item>解除時（<c>LayoutAnchorable.cs:438-440</c>）＝<c>Parent as LayoutAnchorGroup</c> を取り、
/// <b>その</b> <c>PreviousContainer</c> を見る</item>
/// <item>AutoHide化時（同<c>:590-591</c>）＝新規 <c>LayoutAnchorGroup</c> を作り、<b>その</b>
/// <c>PreviousContainer</c> へ元の <c>LayoutAnchorablePane</c> を格納する</item>
/// <item><c>LayoutAnchorable</c> 自身（<c>this</c>）の <c>PreviousContainer</c> には両フェーズとも
/// 一切触れない＝<b>常にnullで正常</b></item>
/// </list>
/// ゆえに本命は <c>GroupPrev</c>（親グループが保持するもの）。<c>SelfPrev</c> は参考値として併記する。
/// </para>
/// <para>
/// <b>リフレクションを用いる理由</b>：<c>ILayoutPreviousContainer</c> は <c>internal</c>
/// インターフェースであり（一次ソースで確認）、ecad2側からは型でアクセスできない。AvalonDock本体には
/// 手を入れぬ方針（家老指示）ゆえ観測手段はこれのみ。失敗しても "?" を記録して先へ進む。
/// </para>
/// <para>
/// <b>【契機の取りこぼし対策・2周目】</b><c>DockingManager.LayoutChanged</c> は名に反して
/// <b><c>Layout</c>依存関係プロパティの差し替え時にしか発火しない</b>（<c>DockingManager.cs:160,181,184,265</c>
/// ＝<c>FrameworkPropertyMetadata</c> のコールバック経由）。レイアウト内部の変化では発火せず、
/// これがフライアウトのピン留めボタン経由の解除を取りこぼした原因である（忍者の実測で発覚）。
/// 対策として <c>LayoutRoot</c> の <c>Updated</c>/<c>ElementAdded</c>/<c>ElementRemoved</c> を購読し、
/// <b>さらに経路に一切依存しないポーリングを保険として併用する</b>——<c>FireLayoutUpdated()</c> の
/// 呼び出し元がローカル保存の一次ソース範囲内に存在せず、イベントだけでは網羅を保証できないため。
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

    /// <summary>ポーリング間隔。殿の実操作を捉えるに足り、かつ負荷にならぬ間隔。</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private static readonly object Gate = new();

    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ecad2", "diagnostics");

    private static readonly string LogPath = Path.Combine(LogDirectory, "t130-layout-trace.log");

    /// <summary>リフレクションでの取得自体が失敗したことを表す番人。null（＝本当にnull）と区別する。</summary>
    private static readonly object Unavailable = new();

    /// <summary>直近に書いた状態文字列。同一状態の連続を抑える（ポーリングが高頻度で回るため）。</summary>
    private static string? _lastState;

    private static DockingManager? _manager;
    private static LayoutRoot? _hookedRoot;
    private static DispatcherTimer? _pollTimer;

    /// <summary>
    /// 観測を開始する。<c>LayoutRoot</c>のイベント購読とポーリングを仕掛ける。
    /// <c>Layout</c>が差し替わった後にも呼ぶこと（購読先を張り直すため）。
    /// </summary>
    public static void Attach(DockingManager? manager)
    {
        try
        {
            if (manager is null) return;
            _manager = manager;
            HookLayoutRoot(manager.Layout);
            if (_pollTimer is not null) return;
            _pollTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = PollInterval };
            _pollTimer.Tick += (_, _) => Log(_manager, "Poll");
            _pollTimer.Start();
        }
        catch
        {
            // ベストエフォート。診断の失敗が殿の実運用を妨げてはならぬ。
        }
    }

    private static void HookLayoutRoot(LayoutRoot? root)
    {
        if (root is null || ReferenceEquals(root, _hookedRoot)) return;
        if (_hookedRoot is not null)
        {
            _hookedRoot.Updated -= OnRootUpdated;
            _hookedRoot.ElementAdded -= OnRootElementAdded;
            _hookedRoot.ElementRemoved -= OnRootElementRemoved;
        }
        _hookedRoot = root;
        root.Updated += OnRootUpdated;
        root.ElementAdded += OnRootElementAdded;
        root.ElementRemoved += OnRootElementRemoved;
    }

    private static void OnRootUpdated(object? sender, EventArgs e)
        => Log(_manager, "LayoutRoot.Updated");

    private static void OnRootElementAdded(object? sender, LayoutElementEventArgs e)
        => Log(_manager, "ElementAdded", note: e.Element?.GetType().Name);

    private static void OnRootElementRemoved(object? sender, LayoutElementEventArgs e)
        => Log(_manager, "ElementRemoved", note: e.Element?.GetType().Name);

    /// <summary>
    /// 対象パネルの現在の状態を1行記録する。
    /// </summary>
    /// <param name="manager">対象のDockingManager。nullでも落ちない。</param>
    /// <param name="eventName">契機の名前。</param>
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
            // ベストエフォート（家老指示DoD 4）。
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

        string parent = anchorable.Parent is null ? "null" : DescribeContainer(anchorable.Parent);
        string grandParent = anchorable.Parent?.Parent is null ? "null" : DescribeContainer(anchorable.Parent.Parent);
        string dockWidth = anchorable.Parent is LayoutAnchorablePane pane ? pane.DockWidth.ToString() : "-";

        return $"ContentId={anchorable.ContentId} IsAutoHidden={anchorable.IsAutoHidden} "
             + $"GroupPrev={DescribeGroupPreviousContainer(anchorable)} SelfPrev={DescribeContainer(TryGetPreviousContainer(anchorable))} "
             + $"PrevIndex={anchorable.PreviousContainerIndex} "
             + $"Parent={parent} GrandParent={grandParent} DockWidth={dockWidth} "
             + $"AutoHideWidth={anchorable.AutoHideWidth} Screen={DescribeScreenPosition(anchorable)}";
    }

    /// <summary>
    /// <b>本命の観測値</b>＝親の<c>LayoutAnchorGroup</c>が保持する<c>PreviousContainer</c>
    /// （<c>ToggleAutoHide()</c>が解除時に実際に参照する先）。
    /// 「親が無い」「親がAnchorGroupでない（＝AutoHide中でない）」「親のPreviousContainerがnull」は
    /// それぞれ意味が異なるため区別して表示する（家老指示）。
    /// </summary>
    private static string DescribeGroupPreviousContainer(LayoutAnchorable anchorable)
    {
        if (anchorable.Parent is null) return "n/a(Parentがnull)";
        if (anchorable.Parent is not LayoutAnchorGroup group)
            return $"n/a(親は{anchorable.Parent.GetType().Name}=AutoHide中でない)";
        return DescribeContainer(TryGetPreviousContainer(group));
    }

    /// <summary>
    /// <c>ILayoutPreviousContainer.PreviousContainer</c>をリフレクションで読む。
    /// 保持者が<c>LayoutAnchorable</c>（<c>LayoutContent</c>派生）か<c>LayoutAnchorGroup</c>かで
    /// 宣言型が異なるため、基底へ遡りつつ「明示的インターフェース実装のプロパティ」→
    /// 「バッキングフィールド」の順に探す。
    /// </summary>
    private static object? TryGetPreviousContainer(object owner)
    {
        try
        {
            for (var type = owner.GetType(); type is not null; type = type.BaseType)
            {
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                var explicitProperty = type.GetProperty(
                    "AvalonDock.Layout.ILayoutPreviousContainer.PreviousContainer", flags);
                if (explicitProperty is not null) return explicitProperty.GetValue(owner);
                var backingField = type.GetField("_previousContainer", flags);
                if (backingField is not null) return backingField.GetValue(owner);
            }
            return Unavailable;
        }
        catch
        {
            return Unavailable;
        }
    }

    private static string DescribeContainer(object? container)
    {
        if (ReferenceEquals(container, Unavailable)) return "?(reflection-failed)";
        if (container is null) return "null";
        string type = container.GetType().Name;
        int identity = RuntimeHelpers.GetHashCode(container);
        return $"{type}#{identity:X8}(Id={TryGetPaneId(container)})";
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
