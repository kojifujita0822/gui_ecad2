using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Ecad2.App.Views;

namespace Ecad2.App.Tests;

/// <summary>
/// T-077増分1(FlowDocument自作変換、殿裁定「案B」)・増分5(表構文対応)の回帰テスト。対応構文
/// (見出しH1-H3・段落・箇条書き・番号付きリスト・コードブロック・水平線・インライン強調/コード・表)。
/// </summary>
public class MarkdownFlowDocumentConverterTests
{
    private static string TextOf(Paragraph paragraph)
        => string.Concat(paragraph.Inlines.OfType<Run>().Select(r => r.Text));

    private static string TextOf(TableCell cell)
        => TextOf((Paragraph)cell.Blocks.Single());

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
    public void Convert_表をヘッダー行とデータ行を持つTableへ変換する()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("| a | b |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |");

        var table = Assert.IsType<Table>(doc.Blocks.Single());
        var rowGroup = table.RowGroups.Single();
        Assert.Equal(3, rowGroup.Rows.Count); // ヘッダー1行+データ2行
        Assert.Equal("a", TextOf(rowGroup.Rows[0].Cells[0]));
        Assert.Equal("b", TextOf(rowGroup.Rows[0].Cells[1]));
        Assert.Equal("1", TextOf(rowGroup.Rows[1].Cells[0]));
        Assert.Equal("4", TextOf(rowGroup.Rows[2].Cells[1]));
    }

    [Fact]
    public void Convert_表のヘッダー行は太字になる()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("| 見出し |\n|---|\n| データ |");

        var table = Assert.IsType<Table>(doc.Blocks.Single());
        var rowGroup = table.RowGroups.Single();
        var headerParagraph = (Paragraph)rowGroup.Rows[0].Cells[0].Blocks.Single();
        var dataParagraph = (Paragraph)rowGroup.Rows[1].Cells[0].Blocks.Single();
        Assert.Equal(FontWeights.Bold, headerParagraph.FontWeight);
        Assert.NotEqual(FontWeights.Bold, dataParagraph.FontWeight);
    }

    [Fact]
    public void Convert_データ行の列数がヘッダーより少ない場合は空セルで埋める()
    {
        // docs/usage実例(menu-toolbar)の継続行表記(先頭列以降を省略)に対応する。
        var doc = MarkdownFlowDocumentConverter.Convert("| メニュー | 項目 |\n|---|---|\n| ファイル | 新規 |\n| | 開く |");

        var table = Assert.IsType<Table>(doc.Blocks.Single());
        var rowGroup = table.RowGroups.Single();
        Assert.Equal(2, rowGroup.Rows[2].Cells.Count);
        Assert.Equal("", TextOf(rowGroup.Rows[2].Cells[0]));
        Assert.Equal("開く", TextOf(rowGroup.Rows[2].Cells[1]));
    }

    [Fact]
    public void Convert_表セル内のインラインコードも処理される()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("| 項目 | 説明 |\n|---|---|\n| 開く | `.gcad`ファイルを開く |");

        var table = Assert.IsType<Table>(doc.Blocks.Single());
        var cellParagraph = (Paragraph)table.RowGroups.Single().Rows[1].Cells[1].Blocks.Single();
        var codeRun = Assert.Single(cellParagraph.Inlines.OfType<Run>(), r => r.Text == ".gcad");
        Assert.Equal("Consolas", codeRun.FontFamily.Source);
    }

    [Fact]
    public void Convert_表の前後の段落は表と独立したブロックのまま残る()
    {
        var doc = MarkdownFlowDocumentConverter.Convert("前文。\n\n| a |\n|---|\n| 1 |\n\n後文。");

        var blocks = doc.Blocks.ToList();
        Assert.Equal(3, blocks.Count);
        Assert.IsType<Paragraph>(blocks[0]);
        Assert.IsType<Table>(blocks[1]);
        Assert.IsType<Paragraph>(blocks[2]);
    }

    [Fact]
    public void Convert_区切り線が続かないパイプ始まり行は通常段落として処理し無限ループしない()
    {
        // 増分5の教訓(段落結合ループの1行目除外条件による無限ループ回避)の直接検証。
        var doc = MarkdownFlowDocumentConverter.Convert("| これは表ではない行 |\n通常の続き。");

        var paragraph = Assert.IsType<Paragraph>(doc.Blocks.Single());
        Assert.Equal("| これは表ではない行 | 通常の続き。", TextOf(paragraph));
    }
}
