using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace T033InlineBarPoc;

/// <summary>
/// T-033 PoC。本体 MainWindow.xaml.cs の TryPlaceElement / Window_PreviewKeyDown の骨格を最小移植し、
/// モーダル ElementPlacementDialog(ShowDialog)の代わりに非モーダルバー(同一Window内オーバーレイ、
/// 隠密所見によりPopupから変更)を使った場合に
/// (1)TextBoxフォーカス中のEnter確定/Esc取消(IsDefault/IsCancel)が機能するか
/// (2)バーを閉じた後のキャンバスへのフォーカス復帰が確実に起きるか
/// (3)バー表示中に他のグローバルショートカット(F5)が誤って反応しないか
/// を検証する。
/// </summary>
public partial class MainWindow : Window
{
    private int _placeCounter;
    private bool _okClicked;
    private bool _cancelClicked;
    private bool _globalShortcutFiredWhileBarOpen;

    public MainWindow()
    {
        InitializeComponent();
        EventManager.RegisterClassHandler(typeof(UIElement),
            Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnAnyFocusChanged), true);
        Loaded += async (_, _) =>
        {
            Canvas.Focus();
            Canvas.SelectCell(0, 0);
            Log("PoC起動。キャンバスに初期フォーカス+セル(0,0)選択。");
            UpdateFocusStatus();

            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--auto"))
                await RunHeadlessAsync(args);
        };
    }

    private async Task RunHeadlessAsync(string[] args)
    {
        ExplicitFocusRestore.IsChecked = false;
        await RunAutoLoopAsync();
        ExplicitFocusRestore.IsChecked = true;
        await RunAutoLoopAsync();
        await RunEscCancelCheckAsync();
        await RunGlobalShortcutCheckAsync();

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
            TextBox => "TextBox(機器名入力欄)",
            FrameworkElement fe => $"{f.GetType().Name} '{fe.Name}'",
            null => "(なし)",
            _ => f.GetType().Name,
        };
        FocusStatus.Text = $"focus: {name}   selectedCell: {(Canvas.SelectedCell is { } c ? $"({c.Row},{c.Col})" : "なし")}   barOpen: {IsBarOpen}";
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

    private bool IsBarOpen => InlineBar.Visibility == Visibility.Visible;

    // バーを選択セルの真下に置く(本体CellRectDip+PointToScreen相当をRootGrid座標系への
    // TranslatePointで実現。同一Window内オーバーレイのためPopupのCustomPopupPlacementは不要)。
    private void PlaceInlineBar()
    {
        var cell = Canvas.SelectedCell ?? (0, 0);
        var rect = Canvas.CellRect(cell.Row, cell.Col);
        var topLeftInGrid = Canvas.TranslatePoint(new Point(rect.X, rect.Bottom), RootGrid);
        InlineBar.Margin = new Thickness(topLeftInGrid.X, topLeftInGrid.Y, 0, 0);
    }

    // 本体 Window_PreviewKeyDown の矢印/Enter/グローバルショートカット相当の最小移植。
    // F5は「ツール切替」を模したグローバルショートカット代表。バー表示中でも届いてしまうかを見る。
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
            case Key.F5:
                // 意図的にe.Handled=trueにしない・IsCanvasFocused条件も付けない。
                // 「グローバルショートカットは常時Windowレベルで拾う」という本体既存の設計(T-021根拠3の
                // グローバルショートカット群と同じ想定)をそのまま模して、バー表示中に本当に発火するか
                // を素の状態で確認する。
                Log($"[F5] グローバルショートカット発火(ツール切替を模す)。barOpen={IsBarOpen}");
                if (IsBarOpen) _globalShortcutFiredWhileBarOpen = true;
                break;
        }
        UpdateFocusStatus();
    }

    // 本体 TryPlaceElement 相当。配置対象セルを検査し、非モーダルバーを選択セル直下に開く。
    // 確定/取消の判定はOkButton_Click/CancelButton_Clickへ移譲する(非モーダルゆえ同期戻り値を
    // 待てない。本体は`if (dialog.ShowDialog()==true)`の同期構造だが、これがそのまま非モーダル化の
    // 最大の構造変更点であることをプラン3.3で明記済み)。
    private void TryPlace()
    {
        if (Canvas.SelectedCell is null)
        {
            Log("配置スキップ: セル未選択。");
            return;
        }
        if (Canvas.IsSelectedCellOccupied())
        {
            Log($"配置スキップ: セル{FormatCell()}は既に埋まっている。");
            return;
        }

        _okClicked = false;
        _cancelClicked = false;
        DeviceNameBox.Text = "";
        PlaceInlineBar();
        InlineBar.Visibility = Visibility.Visible;
        DeviceNameBox.Focus();
        Log($"バー表示: セル{FormatCell()}の真下に浮動インラインバーを開いた。");
        UpdateFocusStatus();
        Log($"   バー表示直後のフォーカス所在: {(Keyboard.FocusedElement is TextBox ? "TextBox ○" : "TextBox以外 ×")}");
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _okClicked = true;
        string name = string.IsNullOrEmpty(DeviceNameBox.Text) ? $"E{++_placeCounter}" : DeviceNameBox.Text;
        Canvas.Place(name);
        Log($"配置確定: セル{FormatCell()} <- \"{name}\"。");
        CloseBar();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancelClicked = true;
        Log("配置キャンセル(バーでEsc/キャンセルボタン)。");
        CloseBar();
    }

    private void CloseBar()
    {
        InlineBar.Visibility = Visibility.Collapsed;
        if (ExplicitFocusRestore.IsChecked == true)
        {
            Keyboard.Focus(Canvas);
            Log("-> Keyboard.Focus(Canvas)で明示復帰を実行。");
        }
        else
        {
            Log("-> 明示復帰なし(暗黙委譲任せ)。");
        }
        UpdateFocusStatus();
        Log($"   バー閉鎖後フォーカス所在: {(IsCanvasFocused() ? "キャンバス ○" : "キャンバス外 ×")}");
    }

    private string FormatCell() => Canvas.SelectedCell is { } c ? $"({c.Row},{c.Col})" : "(なし)";

    private async void RunAutoLoop_Click(object sender, RoutedEventArgs e) => await RunAutoLoopAsync();
    private async void RunEscCancelCheck_Click(object sender, RoutedEventArgs e) => await RunEscCancelCheckAsync();
    private async void RunGlobalShortcutCheck_Click(object sender, RoutedEventArgs e) => await RunGlobalShortcutCheckAsync();

    // 検証(1)(2): OK確定ループ×3。IsDefault(Enter)経由でOkButton_Clickが呼ばれるか、
    // 確定後にキャンバスへフォーカスが戻り続けて矢印移動が効くかを見る。
    private async Task RunAutoLoopAsync()
    {
        Log("========== 自動ループ×3(OK確定) 開始 ==========");
        Log($"明示Focus復帰: {(ExplicitFocusRestore.IsChecked == true ? "あり" : "なし")}");
        Canvas.ClearPlaced();
        Canvas.Focus();
        Canvas.SelectCell(0, 0);

        int okCount = 0;
        int focusRetained = 0;
        int isDefaultFired = 0;
        for (int i = 0; i < 3; i++)
        {
            Log($"--- ループ {i + 1}/3 ---");
            TryPlace();
            if (!IsBarOpen)
            {
                Log("   [異常] バーが開かなかった(配置スキップ)。ループ中断。");
                break;
            }
            DeviceNameBox.Text = $"AUTO{i + 1}";
            _okClicked = false;
            SendKeyToFocused(Key.Enter);
            await Dispatcher.Yield(DispatcherPriority.Background);

            if (_okClicked)
            {
                isDefaultFired++;
                okCount++;
            }
            else
            {
                Log("   [警告] TextBoxでEnterを送ってもOkButton_Click(IsDefault)が発火しなかった。IsDefaultはPopup内で機能しない可能性。");
                // IsDefaultが機能しない場合はテスト続行のため手動でOKボタンをクリックする。
                OkButton_Click(OkButton, new RoutedEventArgs());
                okCount++;
            }

            bool beforeCanvas = IsCanvasFocused();
            var before = Canvas.SelectedCell;
            SendKeyToFocused(Key.Right);
            await Dispatcher.Yield(DispatcherPriority.Background);
            var after = Canvas.SelectedCell;
            bool arrowWorked = before != after;
            if (arrowWorked) focusRetained++;
            Log($"   バー閉鎖後の矢印移動: {(arrowWorked ? "効いた ○" : "効かない ×")} " +
                $"(canvasFocus={(beforeCanvas ? "○" : "×")}, {before?.ToString() ?? "?"}->{after?.ToString() ?? "?"})");
        }

        Log($"========== 結果: 配置成功 {okCount}/3, IsDefault(Enter)発火 {isDefaultFired}/3, 矢印復帰 {focusRetained}/3 ==========");
        Log(isDefaultFired == 3 && focusRetained == 3
            ? "判定: PASS(IsDefault経由のEnter確定+フォーカス復帰とも成立)"
            : "判定: 要注意(IsDefault不発火またはフォーカス復帰不成立。詳細は上のログ参照)");
        Log("");
    }

    // 検証(1): Escキャンセル。IsCancel経由でCancelButton_Clickが呼ばれるか、要素が生成されないか、
    // バー閉鎖後にフォーカスがキャンバスへ戻るかを見る。
    private async Task RunEscCancelCheckAsync()
    {
        Log("========== Escキャンセル確認 開始 ==========");
        Canvas.ClearPlaced();
        Canvas.Focus();
        Canvas.SelectCell(1, 1);

        TryPlace();
        if (!IsBarOpen)
        {
            Log("   [異常] バーが開かなかった。中断。");
            return;
        }
        DeviceNameBox.Text = "SHOULD_NOT_BE_PLACED";
        _cancelClicked = false;
        SendKeyToFocused(Key.Escape);
        await Dispatcher.Yield(DispatcherPriority.Background);

        bool isCancelFired = _cancelClicked;
        if (!isCancelFired)
        {
            Log("   [警告] TextBoxでEscを送ってもCancelButton_Click(IsCancel)が発火しなかった。IsCancelはPopup内で機能しない可能性。");
            CancelButton_Click(CancelButton, new RoutedEventArgs());
        }

        bool placed = Canvas.IsSelectedCellOccupied();
        bool canvasFocused = IsCanvasFocused();
        Log($"IsCancel(Esc)発火: {(isCancelFired ? "した ○" : "しなかった ×(手動フォールバックで処理)")}");
        Log($"キャンセル後に要素が生成されていないか: {(!placed ? "生成なし ○(原子的取消成立)" : "生成された ×(異常)")}");
        Log($"キャンセル後のキャンバスフォーカス: {(canvasFocused ? "○" : "×")}");
        Log(!placed && canvasFocused ? "判定: PASS" : "判定: 要注意");
        Log("");
    }

    // 検証(3): バー表示中にF5(グローバルショートカット模擬)を押しても反応しないか。
    // 家老の仮既定=「モーダル同等に効かない」を安全側として、これと異なる挙動が出た場合は
    // 実装せず保留し殿へ諮る対象(本PoCでは事実の記録のみ行う)。
    private async Task RunGlobalShortcutCheckAsync()
    {
        Log("========== バー表示中F5無反応確認 開始 ==========");
        Canvas.ClearPlaced();
        Canvas.Focus();
        Canvas.SelectCell(2, 2);

        TryPlace();
        if (!IsBarOpen)
        {
            Log("   [異常] バーが開かなかった。中断。");
            return;
        }
        _globalShortcutFiredWhileBarOpen = false;
        SendKeyToFocused(Key.F5);
        await Dispatcher.Yield(DispatcherPriority.Background);

        Log($"バー表示中のF5: {(_globalShortcutFiredWhileBarOpen ? "反応してしまった ×(仮既定と相違、殿へ諮る対象)" : "反応しなかった ○(仮既定どおり)")}");
        Log(_globalShortcutFiredWhileBarOpen ? "判定: 要注意(仮既定と相違)" : "判定: PASS(仮既定どおり)");
        // 後始末: バーを閉じておく。
        CancelButton_Click(CancelButton, new RoutedEventArgs());
        Log("");
    }

    // フォーカス要素へキー入力を送る(自動検証用の補助計器)。最終判定は忍者の物理キー+
    // Automation Peerで二重確認する(T-021踏襲、偽結果の罠回避)。
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
