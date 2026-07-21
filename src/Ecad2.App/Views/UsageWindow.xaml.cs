using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Ecad2.App.Views;

/// <summary>「使い方」ウィンドウ(T-077、殿裁定=非モーダル別ウィンドウ、GX Works3ヘルプウィンドウ近似)。
/// プロジェクト初の非モーダルWindow実装(呼び出し元がShow()で開き、多重起動防止は呼び出し元側の
/// インスタンスキャッシュで行う、docs/ecad2-t077-plan-onmitsu.md 2節)。
/// 増分2(殿裁定=案1): 左目次(ListBox)+右コンテンツ(FlowDocumentScrollViewer)のレイアウト。
/// 増分4(家老采配2026-07-21): 参照先をdocs/spec原文(開発者向け)からdocs/usage平易版(隠密作成、
/// ユーザー向け)へ切替。</summary>
public partial class UsageWindow : Window
{
    /// <summary>1領域=1埋め込みリソースファイル+目次表示名の組。internalはIVT経由のテスト用。</summary>
    internal sealed record UsageTopic(string ResourceFileName, string DisplayName);

    /// <summary>目次の並び順(GX Works3的な論理順序=基本操作→編集→表示→部品/機器管理→検査・出力、
    /// 侍判断、殿裁定「順序・初期選択は侍判断でよい」)。初期選択は先頭(シート/ドキュメント管理)。
    /// DisplayNameは増分4でdocs/usage平易版の見出し文言に合わせて更新(undo-redo・drc-outputの
    /// 2件はタイトルが変更されている)。</summary>
    internal static readonly UsageTopic[] Topics =
    {
        new("ecad2-usage-sheet-document.md", "シート/ドキュメント管理"),
        new("ecad2-usage-menu-toolbar.md", "メニュー・ツールバー全体構成"),
        new("ecad2-usage-placement.md", "配置操作"),
        new("ecad2-usage-wiring.md", "結線操作"),
        new("ecad2-usage-undo-redo.md", "元に戻す・やり直し（Undo/Redo）"),
        new("ecad2-usage-canvas-display.md", "キャンバス表示"),
        new("ecad2-usage-part-management.md", "部品選択・自作パーツ管理"),
        new("ecad2-usage-device-table.md", "機器表・BOM"),
        new("ecad2-usage-statusbar.md", "ステータスバー・モード表示"),
        new("ecad2-usage-drc-output.md", "設計チェック(DRC)・出力パネル・検索"),
        new("ecad2-usage-pdf-testmode.md", "PDF出力・テストモード"),
    };

    public UsageWindow()
    {
        InitializeComponent();
        TopicList.ItemsSource = Topics;
        TopicList.SelectedIndex = 0;
    }

    private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TopicList.SelectedItem is not UsageTopic topic) return;
        ContentViewer.Document = MarkdownFlowDocumentConverter.Convert(LoadEmbeddedMarkdown(topic.ResourceFileName));
    }

    /// <summary>internalはIVT経由のテスト用(全11リソースが実際に読み込めることの検証)。</summary>
    internal static string LoadEmbeddedMarkdown(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"Ecad2.App.UsageContent.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"埋め込みリソースが見つからない: {resourceName}");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
