using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfList = System.Windows.Documents.List;

namespace Ecad2.App.Views;

/// <summary>Markdown→FlowDocument変換(T-077増分1、殿裁定=案B「FlowDocument自作変換、新規依存なし」の
/// PoC実装)。対応構文は限定的(見出しH1-H3・段落・箇条書き・番号付きリスト・コードブロック・水平線・
/// インライン強調/インラインコード)。Markdown表は非対応構文のためプレーンテキストのまま表示する
/// (docs/specの表を含む領域を選ぶと崩れて見えるため、増分1では表の少ない領域を選定する運用)。
/// 全11領域対応・表構文対応は増分2以降で拡張する。</summary>
public static class MarkdownFlowDocumentConverter
{
    public static FlowDocument Convert(string markdown)
    {
        var document = new FlowDocument();
        document.SetResourceReference(TextElement.ForegroundProperty, "DialogForegroundBrush");

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            var headingMatch = Regex.Match(line, @"^(#{1,3})\s+(.*)$");
            if (headingMatch.Success)
            {
                document.Blocks.Add(CreateHeading(headingMatch.Groups[2].Value, headingMatch.Groups[1].Value.Length));
                i++;
                continue;
            }

            if (Regex.IsMatch(line, @"^-{3,}$"))
            {
                document.Blocks.Add(CreateHorizontalRule());
                i++;
                continue;
            }

            if (line.TrimStart().StartsWith("```"))
            {
                i++;
                var codeLines = new System.Collections.Generic.List<string>();
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                {
                    codeLines.Add(lines[i]);
                    i++;
                }
                i++; // 終端の```をスキップ(範囲外なら次周のwhileが自然に終了する)
                document.Blocks.Add(CreateCodeBlock(string.Join("\n", codeLines)));
                continue;
            }

            if (Regex.IsMatch(line, @"^[-*]\s+"))
            {
                var items = new System.Collections.Generic.List<string>();
                while (i < lines.Length && Regex.IsMatch(lines[i], @"^[-*]\s+"))
                {
                    items.Add(Regex.Replace(lines[i], @"^[-*]\s+", ""));
                    i++;
                }
                document.Blocks.Add(CreateList(items, isNumbered: false));
                continue;
            }

            if (Regex.IsMatch(line, @"^\d+\.\s+"))
            {
                var items = new System.Collections.Generic.List<string>();
                while (i < lines.Length && Regex.IsMatch(lines[i], @"^\d+\.\s+"))
                {
                    items.Add(Regex.Replace(lines[i], @"^\d+\.\s+", ""));
                    i++;
                }
                document.Blocks.Add(CreateList(items, isNumbered: true));
                continue;
            }

            // 通常段落(空行・他の構文行に達するまでの連続行をMarkdown標準どおり1段落へ結合する)。
            var paragraphLines = new System.Collections.Generic.List<string>();
            while (i < lines.Length
                && !string.IsNullOrWhiteSpace(lines[i])
                && !Regex.IsMatch(lines[i], @"^(#{1,3})\s+")
                && !Regex.IsMatch(lines[i], @"^-{3,}$")
                && !lines[i].TrimStart().StartsWith("```")
                && !Regex.IsMatch(lines[i], @"^[-*]\s+")
                && !Regex.IsMatch(lines[i], @"^\d+\.\s+"))
            {
                paragraphLines.Add(lines[i]);
                i++;
            }
            document.Blocks.Add(CreateParagraph(string.Join(" ", paragraphLines)));
        }

        return document;
    }

    private static Paragraph CreateHeading(string text, int level)
    {
        var paragraph = new Paragraph
        {
            FontSize = level switch { 1 => 22, 2 => 18, _ => 15 },
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, level == 1 ? 4 : 12, 0, 6),
        };
        paragraph.Inlines.AddRange(CreateInlines(text));
        return paragraph;
    }

    private static Paragraph CreateHorizontalRule()
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 8, 0, 8), BorderThickness = new Thickness(0, 1, 0, 0) };
        paragraph.SetResourceReference(Paragraph.BorderBrushProperty, "PanelGridLineBrush");
        return paragraph;
    }

    private static Paragraph CreateCodeBlock(string code)
    {
        var paragraph = new Paragraph(new Run(code))
        {
            FontFamily = new FontFamily("Consolas"),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 4, 0, 4),
        };
        paragraph.SetResourceReference(Paragraph.BackgroundProperty, "InputBackgroundBrush");
        return paragraph;
    }

    private static WpfList CreateList(System.Collections.Generic.List<string> items, bool isNumbered)
    {
        var list = new WpfList { MarkerStyle = isNumbered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc };
        foreach (var item in items)
        {
            var itemParagraph = new Paragraph();
            itemParagraph.Inlines.AddRange(CreateInlines(item));
            list.ListItems.Add(new ListItem(itemParagraph));
        }
        return list;
    }

    private static Paragraph CreateParagraph(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
        paragraph.Inlines.AddRange(CreateInlines(text));
        return paragraph;
    }

    /// <summary>インライン強調(**bold**)・インラインコード(`code`)を処理する(PoC範囲、リンク等は非対応)。</summary>
    private static System.Collections.Generic.List<Inline> CreateInlines(string text)
    {
        var inlines = new System.Collections.Generic.List<Inline>();
        var matches = Regex.Matches(text, @"\*\*(.+?)\*\*|`(.+?)`");
        int lastIndex = 0;
        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
                inlines.Add(new Run(text[lastIndex..match.Index]));

            if (match.Groups[1].Success)
                inlines.Add(new Run(match.Groups[1].Value) { FontWeight = FontWeights.Bold });
            else
                inlines.Add(new Run(match.Groups[2].Value) { FontFamily = new FontFamily("Consolas") });

            lastIndex = match.Index + match.Length;
        }
        if (lastIndex < text.Length)
            inlines.Add(new Run(text[lastIndex..]));

        return inlines;
    }
}
