using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ecad2.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ViewModels.MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new ViewModels.MainWindowViewModel();
        DataContext = _viewModel;
        LadderCanvasHost.Draw(_viewModel.CurrentSheet, _viewModel.PartLibrary);
    }

    // Ctrl+マウスホイールでキャンバスを拡大縮小する。Ctrl無しは通常のスクロールに委ねる。
    private void CanvasArea_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        _viewModel.CanvasScale += e.Delta > 0 ? 0.1 : -0.1;
        e.Handled = true;
    }

    // 左パレットの図形をクリックすると配置ツールを選択する。
    private void PartPaletteItem_Selected(object sender, RoutedEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: Ecad2.Persistence.PartFolderEntry entry })
            _viewModel.PartPalette.SelectCommand.Execute(entry);
    }

    // 配置ツール選択中(Tool.Mode==PlaceElement)にキャンバスをクリックすると、その位置へ要素を
    // 追加する(T-016)。占有マス重複は最小限のみ判定(既に要素がある位置には追加しない)。
    // デバイス名の入力(浮動インラインボックス等)は別タスク(T-021)。
    // 配置は単発とし、成功したらTool=SelectDefaultへ自動遷移する(design-brief 4節#2
    // 「Enter/Escの一枚岩」のうちEnter=確定に相当する挙動の最小反映。連続配置はEnterによる
    // 明示確定込みで別タスクT-021の検討事項とする)。
    private void LadderCanvasHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.Tool.Mode != ViewModels.ToolMode.PlaceElement) return;

        var position = e.GetPosition(LadderCanvasHost);
        var gridPos = LadderCanvasHost.ToGridPos(position);

        if (_viewModel.CurrentSheet.Elements.Any(el => el.Pos == gridPos)) return;

        _viewModel.CurrentSheet.Elements.Add(new Ecad2.Model.ElementInstance
        {
            Pos = gridPos,
            PartId = _viewModel.Tool.PartId,
        });

        LadderCanvasHost.Draw(_viewModel.CurrentSheet, _viewModel.PartLibrary);
        _viewModel.Tool = ViewModels.ToolState.SelectDefault;
    }

    // design-brief 4節の7原則の全体配線（段階8、最小実装）:
    // #2「Enter/Escの一枚岩の意味テーブル」→ Escは常に1階層キャンセルとして配置ツールを選択モードへ戻す
    // #3「パネル間ナビゲーションをTabと分離」→ F6で左パレット/キャンバス/右パネルを循環移動する
    // Enterによる「配置確定」は配置機能自体が未実装のため今回は対象外（将来タスク）。
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                _viewModel.Tool = ViewModels.ToolState.SelectDefault;
                e.Handled = true;
                break;
            case Key.F6:
                CyclePanelFocus();
                e.Handled = true;
                break;
        }
    }

    private void CyclePanelFocus()
    {
        UIElement[] panels = { PartPaletteList, LadderCanvasHost, DeviceTableGrid };
        var current = FocusManager.GetFocusedElement(this) as DependencyObject;

        int index = -1;
        for (int i = 0; i < panels.Length; i++)
        {
            if (IsWithin(panels[i], current)) { index = i; break; }
        }
        int next = (index + 1) % panels.Length;
        panels[next].Focus();
    }

    private static bool IsWithin(DependencyObject root, DependencyObject? element)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, root)) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }
}