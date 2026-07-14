using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;

namespace T058AvalonDockPoc;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string LayoutFilePath => Path.Combine(AppContext.BaseDirectory, "poc-layout.xml");

    private readonly Dictionary<string, object?> _contentRegistry = new();

    public MainWindow()
    {
        InitializeComponent();
        RegisterContents();
    }

    private void RegisterContents()
    {
        foreach (var anchorable in DockManager.Layout.Descendents().OfType<LayoutAnchorable>())
        {
            _contentRegistry[anchorable.ContentId] = anchorable.Content;
        }
        foreach (var document in DockManager.Layout.Descendents().OfType<LayoutDocument>())
        {
            _contentRegistry[document.ContentId] = document.Content;
        }
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        var serializer = new XmlLayoutSerializer(DockManager);
        using var writer = new StreamWriter(LayoutFilePath);
        serializer.Serialize(writer);
        StatusText.Text = $"レイアウトを保存しました: {LayoutFilePath}";
    }

    private void LoadLayout_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(LayoutFilePath))
        {
            StatusText.Text = "保存済みレイアウトが見つかりません。先に「レイアウト保存」を押してください。";
            return;
        }

        var serializer = new XmlLayoutSerializer(DockManager);
        serializer.LayoutSerializationCallback += (s, args) =>
        {
            if (args.Model.ContentId != null && _contentRegistry.TryGetValue(args.Model.ContentId, out var content))
            {
                args.Content = content;
            }
        };
        using var reader = new StreamReader(LayoutFilePath);
        serializer.Deserialize(reader);
        StatusText.Text = "レイアウトを復元しました(コンテンツ再バインド込み)。";
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs e)
    {
        // 簡易PoCにつき、ウィンドウ再起動を促す形で既定レイアウトへ戻す
        StatusText.Text = "既定レイアウトへ戻すにはアプリを再起動してください(PoC簡易実装)。";
    }

    private void FloatLeftPalette_Click(object sender, RoutedEventArgs e) => Float("LeftPalette", "左パレット");

    private void DockLeftPalette_Click(object sender, RoutedEventArgs e) => Dock("LeftPalette", "左パレット");

    private void FloatToolPalette_Click(object sender, RoutedEventArgs e) => Float("ToolPalette", "ツールパレット");

    private void DockToolPalette_Click(object sender, RoutedEventArgs e) => Dock("ToolPalette", "ツールパレット");

    private void Float(string contentId, string displayName)
    {
        var anchorable = DockManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == contentId);
        if (anchorable == null)
        {
            StatusText.Text = $"{displayName}が見つかりません(既にフロート化済み等)。";
            return;
        }
        anchorable.Float();
        StatusText.Text = $"{displayName}をプログラム的にフロート化しました。";
    }

    private void Dock(string contentId, string displayName)
    {
        var anchorable = DockManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == contentId);
        if (anchorable == null)
        {
            StatusText.Text = $"{displayName}が見つかりません。";
            return;
        }
        anchorable.Dock();
        StatusText.Text = $"{displayName}を再ドッキングしました。";
    }
}
