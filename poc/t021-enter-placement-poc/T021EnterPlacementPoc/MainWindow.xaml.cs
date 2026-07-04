using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace T021EnterPlacementPoc;

/// <summary>
/// T-021 筆頭PoC。本体 MainWindow.xaml.cs の Window_PreviewKeyDown / TryPlaceElement /
/// MoveSelectedCell の骨格を最小移植し、「選択セルEnter→配置→モーダル命名→Enter確定→
/// キャンバスへフォーカス復帰」の一気通貫ループでフォーカスが迷子にならないかを検証する。
/// 明示Focus復帰(ExplicitFocusRestore)の有無を切替えて、暗黙委譲で戻るか明示Focusが必須かを判定する。
/// </summary>
public partial class MainWindow : Window
{
    private int _placeCounter;

    public MainWindow()
    {
        InitializeComponent();
        // フォーカス所在をライブ表示(本体にはない、PoC検証用の計器)。
        EventManager.RegisterClassHandler(typeof(UIElement),
            Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnAnyFocusChanged), true);
        Loaded += async (_, _) =>
        {
            Canvas.Focus();
            Canvas.SelectCell(0, 0);
            Log("PoC起動。キャンバスに初期フォーカス＋セル(0,0)選択。");
            UpdateFocusStatus();

            // --auto: 無人自動検証モード。明示Focus復帰あり/なしの両方を走らせ、ログをファイルへ
            // 書き出して終了する(GUIを人が操作せずCIライクに判定するため)。
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--auto"))
                await RunHeadlessAsync(args);
        };
    }

    private async Task RunHeadlessAsync(string[] args)
    {
        // 明示復帰なし → あり の順で両条件を検証する。
        ExplicitFocusRestore.IsChecked = false;
        await RunAutoLoopAsync();
        ExplicitFocusRestore.IsChecked = true;
        await RunAutoLoopAsync();

        string outPath = "poc-result.txt";
        int idx = Array.IndexOf(args, "--out");
        if (idx >= 0 && idx + 1 < args.Length) outPath = args[idx + 1];
        try
        {
            System.IO.File.WriteAllText(outPath, _log.ToString());
            Log($"[auto] ログを {outPath} へ書き出しました。");
        }
        catch (Exception ex)
        {
            Log($"[auto] ログ書き出し失敗: {ex.Message}");
        }
        Application.Current.Shutdown();
    }

    private void OnAnyFocusChanged(object sender, KeyboardFocusChangedEventArgs e) => UpdateFocusStatus();

    private void UpdateFocusStatus()
    {
        var f = Keyboard.FocusedElement;
        string name = f switch
        {
            PocCanvas => "PocCanvas(キャンバス) ★",
            System.Windows.Controls.TextBox => "TextBox(命名入力欄)",
            FrameworkElement fe => $"{f.GetType().Name} '{fe.Name}'",
            null => "(なし)",
            _ => f.GetType().Name,
        };
        FocusStatus.Text = $"focus: {name}   selectedCell: {(Canvas.SelectedCell is { } c ? $"({c.Row},{c.Col})" : "なし")}";
    }

    private bool IsCanvasFocused()
    {
        var f = Keyboard.FocusedElement as DependencyObject;
        while (f is not null)
        {
            if (ReferenceEquals(f, Canvas)) return true;
            f = VisualTreeHelper.GetParent(f);
        }
        return false;
    }

    // 本体 Window_PreviewKeyDown の矢印/Enterケースの最小移植。
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        switch (e.Key)
        {
            case Key.Up when IsCanvasFocused(): Canvas.MoveSelection(-1, 0); e.Handled = true; break;
            case Key.Down when IsCanvasFocused(): Canvas.MoveSelection(1, 0); e.Handled = true; break;
            case Key.Left when IsCanvasFocused(): Canvas.MoveSelection(0, -1); e.Handled = true; break;
            case Key.Right when IsCanvasFocused(): Canvas.MoveSelection(0, 1); e.Handled = true; break;
            case Key.Enter when IsCanvasFocused():
                TryPlace();
                e.Handled = true;
                break;
        }
        UpdateFocusStatus();
    }

    // 本体 TryPlaceElement 相当。配置→モーダル命名→確定→(トグルに応じ)明示Focus復帰。
    // 戻り値は配置が確定したか。
    private bool TryPlace()
    {
        if (Canvas.SelectedCell is null)
        {
            Log("配置スキップ: セル未選択。");
            return false;
        }
        if (Canvas.IsSelectedCellOccupied())
        {
            Log($"配置スキップ: セル{FormatCell()}は既に埋まっている。");
            return false;
        }

        var dialog = new NameDialog { Owner = this };
        bool ok = dialog.ShowDialog() == true;

        if (ok)
        {
            string name = string.IsNullOrEmpty(dialog.DeviceName) ? $"E{++_placeCounter}" : dialog.DeviceName;
            Canvas.Place(name);
            Log($"配置確定: セル{FormatCell()} ← \"{name}\"。");
        }
        else
        {
            Log("配置キャンセル(ダイアログでEsc/キャンセル)。");
        }

        // ここが検証の核心。明示Focus復帰の有無で戻り先が変わるかを見る。
        if (ExplicitFocusRestore.IsChecked == true)
        {
            Keyboard.Focus(Canvas);
            Log("→ Keyboard.Focus(Canvas)で明示復帰を実行。");
        }
        else
        {
            Log("→ 明示復帰なし(暗黙委譲任せ)。");
        }

        UpdateFocusStatus();
        Log($"   ダイアログ後フォーカス所在: {(IsCanvasFocused() ? "キャンバス ○" : "キャンバス外 ×")}");
        return ok;
    }

    private string FormatCell() => Canvas.SelectedCell is { } c ? $"({c.Row},{c.Col})" : "(なし)";

    // 判定基準: 連続3回の配置ループでフォーカスがキャンバスから外れない/ダイアログに毎回入る。
    // 自動ループは配置後に矢印移動を挟み、「復帰したフォーカスで次の矢印/Enterが効くか」を確認する。
    // ダイアログはモーダルのため、表示直後にDispatcherで自動的にOK確定させて無人実行する。
    private async void RunAutoLoop_Click(object sender, RoutedEventArgs e) => await RunAutoLoopAsync();

    private async Task RunAutoLoopAsync()
    {
        Log("========== 自動ループ×3 開始 ==========");
        Log($"明示Focus復帰: {(ExplicitFocusRestore.IsChecked == true ? "あり" : "なし")}");
        // 各条件をクリーンな状態で比較するため配置済みを消去してから開始する。
        Canvas.ClearPlaced();
        Canvas.Focus();
        Canvas.SelectCell(0, 0);

        int okCount = 0;
        int focusRetained = 0;
        for (int i = 0; i < 3; i++)
        {
            Log($"--- ループ {i + 1}/3 ---");
            // ダイアログが開いたら自動でデバイス名を入れてOKするフックを仕込む。
            ArmDialogAutoConfirm($"AUTO{i + 1}");
            bool placed = TryPlace();
            if (placed) okCount++;
            // _dialogFocusObserved はダイアログ表示時にTextBoxへフォーカスが入っていたか(増分ii)。
            if (_dialogFocusObserved) focusRetained++;
            else Log("   [警告] 命名ダイアログのTextBoxにフォーカスが入らなかった。");

            // 復帰したフォーカスで矢印移動が効くか(＝キャンバスにフォーカスが戻っている証拠)。
            bool beforeCanvas = IsCanvasFocused();
            var before = Canvas.SelectedCell;
            SendKeyToFocused(Key.Right);
            await Dispatcher.Yield(DispatcherPriority.Background);
            var after = Canvas.SelectedCell;
            bool arrowWorked = before != after;
            Log($"   復帰後の矢印移動: {(arrowWorked ? "効いた ○" : "効かない ×")} " +
                $"(canvasFocus={(beforeCanvas ? "○" : "×")}, {before?.ToString() ?? "?"}→{after?.ToString() ?? "?"})");
        }

        Log($"========== 結果: 配置成功 {okCount}/3, ダイアログ入焦 {focusRetained}/3 ==========");
        Log(okCount == 3 && focusRetained == 3
            ? "判定: PASS(この設定で一気通貫ループ成立)"
            : "判定: FAIL(この設定では成立せず。トグルを切替えて再試行、なおダメなら要PreviewLostKeyboardFocus対策)");
        Log("");
    }

    // モーダルダイアログはShowDialogでブロックするため、表示された瞬間にDispatcher経由で
    // 入力欄へ文字を入れOKボタンを押す(無人自動確定)。戻り値は「ダイアログのTextBoxにフォーカスが
    // 入っていたか」＝増分(ii)のフォーカス委譲確認。
    private bool _dialogFocusObserved;
    private void ArmDialogAutoConfirm(string deviceName)
    {
        _dialogFocusObserved = false;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w is NameDialog dlg)
                {
                    // ダイアログ表示直後のフォーカス所在(増分ii: 浮動インライン入力への委譲確認)。
                    _dialogFocusObserved = Keyboard.FocusedElement is System.Windows.Controls.TextBox;
                    var box = FindTextBox(dlg);
                    if (box is not null) box.Text = deviceName;
                    // OK(IsDefault)相当: Enterを模してDialogResultを立てる。
                    dlg.ConfirmForAutomation();
                    break;
                }
            }
        }));
    }

    private static System.Windows.Controls.TextBox? FindTextBox(DependencyObject root)
    {
        if (root is System.Windows.Controls.TextBox tb) return tb;
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var found = FindTextBox(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }

    // フォーカス要素へキー入力を送る(自動ループ用)。実キーではなくWPFイベント注入だが、
    // これはあくまで補助計器。最終判定は忍者の物理キー＋Automation Peerで二重確認する(偽結果の罠回避)。
    private void SendKeyToFocused(Key key)
    {
        var target = Keyboard.FocusedElement;
        if (target is null) return;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        InputManager.Current.ProcessInput(args);
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

    private readonly StringBuilder _log = new();
    private void Log(string message)
    {
        _log.AppendLine(message);
        LogBox.Text = _log.ToString();
        LogBox.ScrollToEnd();
    }
}
