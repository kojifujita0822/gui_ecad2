using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Ecad2.App.Views;

namespace Ecad2.App.Tests;

/// <summary>
/// T-077増分1(FlowDocument自作変換、殿裁定「案B」)の回帰テスト。対応構文は限定的
/// (見出しH1-H3・段落・箇条書き・番号付きリスト・コードブロック・水平線・インライン強調/コード)。
/// </summary>
public class MarkdownFlowDocumentConverterTests
{
    private static string TextOf(Paragraph paragraph)
        => string.Concat(paragraph.Inlines.OfType<Run>().Select(r => r.Text));

    [Fact]
    public void Convert_見出しをParagraphへ変換しレベルに応じたFontSizeを持つ()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("# 見出し1\n\n## 見出し2");

        var blocks = doc.Blocks.OfType<Paragraph>().ToList();
        Assert.Equal(2, blocks.Count);
        Assert.Equal("見出し1", TextOf(blocks[0]));
        Assert.Equal("見出し2", TextOf(blocks[1]));
        Assert.True(blocks[0].FontSize > blocks[1].FontSize);
    }

    [Fact]
    public void Convert_連続する非空行を1つの段落へ結合する()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("1行目です。\n2行目です。\n\n次の段落。");

        var blocks = doc.Blocks.OfType<Paragraph>().ToList();
        Assert.Equal(2, blocks.Count);
        Assert.Equal("1行目です。 2行目です。", TextOf(blocks[0]));
        Assert.Equal("次の段落。", TextOf(blocks[1]));
    }

    [Fact]
    public void Convert_箇条書きをListへ変換する()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("- 項目1\n- 項目2\n- 項目3");

        var list = Assert.IsType<List>(doc.Blocks.Single());
        Assert.Equal(3, list.ListItems.Count);
        Assert.Equal(TextMarkerStyle.Disc, list.MarkerStyle);
    }

    [Fact]
    public void Convert_番号付きリストをDecimalマーカーのListへ変換する()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("1. 手順1\n2. 手順2");

        var list = Assert.IsType<List>(doc.Blocks.Single());
        Assert.Equal(2, list.ListItems.Count);
        Assert.Equal(TextMarkerStyle.Decimal, list.MarkerStyle);
    }

    [Fact]
    public void Convert_コードブロックをモノスペースフォントのParagraphへ変換する()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("```\nvar x = 1;\n```");

        var paragraph = Assert.IsType<Paragraph>(doc.Blocks.Single());
        Assert.Equal("var x = 1;", TextOf(paragraph));
        Assert.Equal("Consolas", paragraph.FontFamily.Source);
    }

    [Fact]
    public void Convert_水平線をBorderThickness付きの空Paragraphへ変換する()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("本文\n\n---\n\n続き");

        var blocks = doc.Blocks.OfType<Paragraph>().ToList();
        Assert.Equal(3, blocks.Count);
        Assert.Empty(blocks[1].Inlines);
        Assert.True(blocks[1].BorderThickness.Top > 0);
    }

    [Fact]
    public void Convert_インライン強調とインラインコードを区別して変換する()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("通常の**強調文字**と`インラインコード`を含む文。");

        var paragraph = Assert.IsType<Paragraph>(doc.Blocks.Single());
        var runs = paragraph.Inlines.OfType<Run>().ToList();
        var bold = Assert.Single(runs, r => r.Text == "強調文字");
        var code = Assert.Single(runs, r => r.Text == "インラインコード");
        Assert.Equal(System.Windows.FontWeights.Bold, bold.FontWeight);
        Assert.Equal("Consolas", code.FontFamily.Source);
    }

    [Fact]
    public void Convert_未対応のMarkdown表構文はプレーンテキストの段落として残る()
    {
        // PoC範囲外の構文(Markdown表)は言語仕様上パースされずそのままテキストとして残る仕様。
        var doc = MarkdownFlowDocumentConverter.Convert("| a | b |\n|---|---|\n| 1 | 2 |");

        var paragraph = Assert.IsType<Paragraph>(doc.Blocks.Single());
        Assert.Contains("| a | b |", TextOf(paragraph));
    }
}
