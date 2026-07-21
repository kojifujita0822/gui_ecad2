using System.Linq;
using Ecad2.App.Views;

namespace Ecad2.App.Tests;

/// <summary>
/// T-077増分2(ナビゲーションUI、殿裁定「案1」左目次+右コンテンツ)・増分4(docs/usage平易版への
/// 参照先切替)の回帰テスト。UI操作(ListBox SelectionChanged等)はコードビハインドのためテスト
/// 基盤が無く対象外(T-088等と同事情)。Topics定義・埋め込みリソースの整合性(全11領域が実際に
/// 読み込めるか)を検証する。
/// </summary>
public class UsageWindowTests
{
    [Fact]
    public void Topics_11領域全て定義されている()
    {
        Assert.Equal(11, UsageWindow.Topics.Length);
    }

    [Fact]
    public void Topics_DisplayNameが重複しない()
    {
        var distinctCount = UsageWindow.Topics.Select(t => t.DisplayName).Distinct().Count();

        Assert.Equal(UsageWindow.Topics.Length, distinctCount);
    }

    [Fact]
    public void Topics_ResourceFileNameが重複しない()
    {
        var distinctCount = UsageWindow.Topics.Select(t => t.ResourceFileName).Distinct().Count();

        Assert.Equal(UsageWindow.Topics.Length, distinctCount);
    }

    [Theory]
    [InlineData("ecad2-usage-sheet-document.md")]
    [InlineData("ecad2-usage-menu-toolbar.md")]
    [InlineData("ecad2-usage-placement.md")]
    [InlineData("ecad2-usage-wiring.md")]
    [InlineData("ecad2-usage-undo-redo.md")]
    [InlineData("ecad2-usage-canvas-display.md")]
    [InlineData("ecad2-usage-part-management.md")]
    [InlineData("ecad2-usage-device-table.md")]
    [InlineData("ecad2-usage-statusbar.md")]
    [InlineData("ecad2-usage-drc-output.md")]
    [InlineData("ecad2-usage-pdf-testmode.md")]
    public void LoadEmbeddedMarkdown_全11領域の埋め込みリソースが実際に読み込める(string fileName)
    {
        // 増分4(docs/usage平易版)は各ファイルで見出し文言が異なる(共通接頭辞なし)ため、
        // 「#見出しで始まる」ことのみ検証する(増分1/2時代の「# ecad2 仕様書」固定接頭辞チェックより緩和)。
        string content = UsageWindow.LoadEmbeddedMarkdown(fileName);

        Assert.NotEmpty(content);
        Assert.StartsWith("# ", content);
    }

    [Fact]
    public void Topics_全項目のResourceFileNameが実際に読み込める()
    {
        // Topics配列とcsproj側EmbeddedResource設定の対応漏れを機械的に検出する
        // (InlineDataの列挙漏れがあってもこちらで拾える)。
        foreach (var topic in UsageWindow.Topics)
        {
            string content = UsageWindow.LoadEmbeddedMarkdown(topic.ResourceFileName);
            Assert.NotEmpty(content);
        }
    }
}
