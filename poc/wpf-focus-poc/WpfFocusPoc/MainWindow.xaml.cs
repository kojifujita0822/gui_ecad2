using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfFocusPoc;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        BuildShapes();
    }

    private void BuildShapes()
    {
        for (int i = 1; i <= 5; i++)
        {
            var border = new Border
            {
                Width = 220,
                Height = 40,
                Margin = new Thickness(0, 0, 0, 8),
                Background = Brushes.LightSteelBlue,
                BorderBrush = Brushes.SteelBlue,
                BorderThickness = new Thickness(1),
                Focusable = true,
                Tag = $"Shape{i}",
                Child = new TextBlock
                {
                    Text = $"図形 {i}（クリックしてフォーカス）",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            border.PreviewMouseLeftButtonDown += Shape_PreviewMouseLeftButtonDown;
            border.PreviewMouseLeftButtonUp += Shape_PreviewMouseLeftButtonUp;
            border.GotKeyboardFocus += Shape_GotKeyboardFocus;
            border.LostKeyboardFocus += Shape_LostKeyboardFocus;
            border.PreviewLostKeyboardFocus += Shape_PreviewLostKeyboardFocus;

            ShapePanel.Children.Add(border);
        }
    }

    private void Shape_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Log($"PointerPressed: {((FrameworkElement)sender).Tag}");
    }

    private void Shape_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var element = (UIElement)sender;
        Log($"PointerReleased: {((FrameworkElement)sender).Tag}");

        // FocusManagerスコープAPIで明示的にフォーカスを当てる（宣言的・決定的な制御）
        Keyboard.Focus(element);
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(element), element);
    }

    private void Shape_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Log($"GotKeyboardFocus: {((FrameworkElement)sender).Tag}");
    }

    private void Shape_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var newFocusName = (e.NewFocus as FrameworkElement)?.Name;
        Log($"LostKeyboardFocus: {((FrameworkElement)sender).Tag} -> {newFocusName ?? e.NewFocus?.ToString() ?? "(none)"}");
    }

    private void Shape_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (EditModeCheckBox.IsChecked == true)
        {
            e.Handled = true;
            Log($"PreviewLostKeyboardFocus: {((FrameworkElement)sender).Tag} の喪失を編集モード中のためキャンセル (e.Handled=true)");
        }
    }

    private void Log(string message)
    {
        FocusLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    private void DrawSymbolsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(SymbolCountBox.Text, out int count) || count <= 0)
        {
            count = 1000;
        }

        var sw = Stopwatch.StartNew();
        SymbolCanvasControl.DrawSymbols(count);
        sw.Stop();
        DrawTimeText.Text = $"{count}個描画: {sw.ElapsedMilliseconds}ms";
    }

    private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PdfSymbolCountBox.Text, out int count) || count <= 0)
        {
            count = 200;
        }

        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "poc-output.pdf");
        try
        {
            PdfExporter.ExportSymbols(path, count);
            PdfResultText.Text = $"PDF出力成功: {path}";
        }
        catch (Exception ex)
        {
            PdfResultText.Text = $"PDF出力失敗: {ex.Message}";
        }
    }
}
