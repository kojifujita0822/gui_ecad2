using System.IO;
using System.Reflection;
using System.Windows;

namespace Ecad2.App.Views;

/// <summary>「使い方」ウィンドウ(T-077増分1、殿裁定=非モーダル別ウィンドウ、GX Works3ヘルプウィンドウ
/// 近似)。プロジェクト初の非モーダルWindow実装(呼び出し元がShow()で開き、多重起動防止は呼び出し元
/// 側のインスタンスキャッシュで行う、docs/ecad2-t077-plan-onmitsu.md 2節)。本増分はナビゲーションUI
/// (左目次+右コンテンツ)を含まず、docs/spec代表1領域を埋め込みリソースから読み込みFlowDocumentへ
/// 変換して表示する固定1画面のPoC。全11領域対応・ナビゲーションUIは増分2で追加する。</summary>
public partial class UsageWindow : Window
{
    private const string ResourceName = "Ecad2.App.UsageContent.ecad2-spec-statusbar.md";

    public UsageWindow()
    {
        InitializeComponent();
        ContentViewer.Document = MarkdownFlowDocumentConverter.Convert(LoadEmbeddedMarkdown());
    }

    private static string LoadEmbeddedMarkdown()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"埋め込みリソースが見つからない: {ResourceName}");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
