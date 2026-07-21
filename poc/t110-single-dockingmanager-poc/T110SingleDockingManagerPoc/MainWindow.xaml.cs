using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace T110SingleDockingManagerPoc;

public partial class MainWindow : Window
{
    private bool _isDarkMode;
    private bool _isTitleBarHidden;
    private bool _isDocumentTabHidden;

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme();
    }

    // T-083踏襲: Theme切替のたびに(e)統合タイトルスタイルを再登録する。
    // 本実装のApplyDockingManagerThemesと異なりManagerは1つのみのため出し分けは不要。
    private void ApplyTheme()
    {
        MainDockingManager.Theme = _isDarkMode
            ? new AvalonDock.Themes.Vs2013DarkTheme()
            : new AvalonDock.Themes.Vs2013LightTheme();
        MainDockingManager.Resources[typeof(AnchorablePaneTitle)] = (Style)FindResource("UnifiedAnchorablePaneTitleStyle");
    }

    private void LightTheme_Click(object sender, RoutedEventArgs e)
    {
        _isDarkMode = false;
        ApplyTheme();
        StatusText.Text = "Lightテーマへ切替。(e)タイトルスタイルのContentId分岐を再確認されたし。";
    }

    private void DarkTheme_Click(object sender, RoutedEventArgs e)
    {
        _isDarkMode = true;
        ApplyTheme();
        StatusText.Text = "Darkテーマへ切替。(e)タイトルスタイルのContentId分岐を再確認されたし。";
    }

    // (h)検証: DockingManager.Resourcesへ暗黙的スタイル(typeof(LayoutAnchorableControl))を
    // 登録/解除することで、単一ペインタイトルバーの完全非表示On/Offを切り替える。
    // 解除時はキー自体を削除し、VS2013テーマのMergedDictionaries経由の既定スタイルへ
    // フォールバックさせる(ClearValueではなくResources.Remove、暗黙的スタイルの解決規則に依る)。
    private void ToggleTitleBarHidden_Click(object sender, RoutedEventArgs e)
    {
        _isTitleBarHidden = !_isTitleBarHidden;
        if (_isTitleBarHidden)
        {
            MainDockingManager.Resources[typeof(LayoutAnchorableControl)] = (Style)FindResource("TitleBarHiddenAnchorableControlStyle");
        }
        else
        {
            MainDockingManager.Resources.Remove(typeof(LayoutAnchorableControl));
        }
        StatusText.Text = $"(h)タイトルバー完全非表示: {(_isTitleBarHidden ? "ON" : "OFF")}(単一ペイン4領域が対象、配置ツールは対象外)";
    }

    // (f)検証: DocumentPaneControlStyleへコピースタイルを設定/解除しドキュメントタブの
    // 表示・非表示を切り替える。解除はClearValueでローカル値を外しStyle経由の既定値へ戻す。
    private void ToggleDocumentTabHidden_Click(object sender, RoutedEventArgs e)
    {
        _isDocumentTabHidden = !_isDocumentTabHidden;
        if (_isDocumentTabHidden)
        {
            MainDockingManager.DocumentPaneControlStyle = (Style)FindResource("DocumentTabHiddenPaneControlStyle");
        }
        else
        {
            MainDockingManager.ClearValue(DockingManager.DocumentPaneControlStyleProperty);
        }
        StatusText.Text = $"(f)ドキュメントタブ非表示: {(_isDocumentTabHidden ? "ON" : "OFF")}";
    }

    // (c)検証: 配置ツール相当ペインを標準Dock()/Float()で往復させ、タブ自己複製バグの
    // 再現有無を目視確認できるようにする(t058 PoCのFloat/Dockボタンパターンを踏襲)。
    private void FloatPlacementToolBar_Click(object sender, RoutedEventArgs e)
    {
        var anchorable = FindAnchorable("PlacementToolBar");
        if (anchorable is null)
        {
            StatusText.Text = "配置ツールが見つかりません(既にフロート化済み等)。";
            return;
        }
        anchorable.Float();
        StatusText.Text = "配置ツールをFloat()しました。(c)タブ自己複製・縦長化・空白化の有無を目視確認されたし。";
    }

    private void DockPlacementToolBar_Click(object sender, RoutedEventArgs e)
    {
        var anchorable = FindAnchorable("PlacementToolBar");
        if (anchorable is null)
        {
            StatusText.Text = "配置ツールが見つかりません。";
            return;
        }
        anchorable.Dock();
        StatusText.Text = "配置ツールを標準Dock()で再ドッキングしました。(c)タブ自己複製の有無を目視確認されたし。";
    }

    private LayoutAnchorable? FindAnchorable(string contentId)
        => MainDockingManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == contentId);
}
