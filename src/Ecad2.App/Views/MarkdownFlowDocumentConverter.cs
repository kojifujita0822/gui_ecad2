using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfList = System.Windows.Documents.List;

namespace Ecad2.App.Views;

/// <summary>Markdown→FlowDocument変換(T-077増分1、殿裁定=案B「FlowDocument自作変換、新規依存なし」の
/// PoC実装)。対応構文(見出しH1-H3・段落・箇条書き・番号付きリスト・コードブロック・水平線・
/// インライン強調/インラインコード・表・画像)。
/// 増分5(家老采配2026-07-21): docs/usage平易版に表構文が多用されている(11領域中6領域)ことを受け、
/// Markdown表(ヘッダー行+`|---|---|`区切り線+データ行)をWPF Tableへ変換する対応を追加。
/// 対応範囲はヘッダー行1段+単純な左揃えセルのみ(`:---:`等の位置指定拡張構文は非対応、
/// docs/usage全11領域の実測範囲で必要十分と確認済み)。</summary>
public static class MarkdownFlowDocumentConverter
{
    /// <summary>行全体が画像の記法 <c>![説明](パス)</c> か（使い方へのスクリーンショット掲載、殿ご下命2026-10-09）。
    /// 段落の途中に埋まった画像は扱わぬ——説明図は文章と別の段に置くものゆえ、行単位で足りる。</summary>
    private static readonly Regex ImageLine = new(@"^!\[(.*?)\]\((.+?)\)\s*$", RegexOptions.Compiled);

    /// <summary>本文が参照する画像のパスを、現れる順に返す（重複は除かぬ）。埋め込み漏れを
    /// テストで確かめるために使う。</summary>
    public static IEnumerable<string> EnumerateImagePaths(string markdown) =>
        markdown.Replace("\r\n", "\n").Split('\n')
            .Select(line => ImageLine.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[2].Value);

    /// <param name="imageResolver">画像のパス（Markdown に書かれたまま）から絵を引く関数。
    /// null、または引けなんだ絵は、説明文だけを代わりに出す（絵が欠けても本文は読めるように）。</param>
    public static FlowDocument Convert(string markdown, Func<string, ImageSource?>? imageResolver = null)
    {
        var document = new FlowDocument();
        document.SetResourceReference(TextElement.ForegroundProperty, "DialogForegroundBrush");

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            var imageMatch = ImageLine.Match(line);
            if (imageMatch.Success)
            {
                document.Blocks.Add(CreateImageBlock(imageMatch.Groups[1].Value, imageMatch.Groups[2].Value, imageResolver));
                i++;
                continue;
            }

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

            if (line.TrimStart().StartsWith("|") && i + 1 < lines.Length && IsTableSeparatorRow(lines[i + 1]))
            {
                var headerCells = SplitTableRow(line);
                i += 2; // ヘッダー行+区切り線をスキップ
                var rows = new System.Collections.Generic.List<string[]>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
                {
                    rows.Add(SplitTableRow(lines[i]));
                    i++;
                }
                document.Blocks.Add(CreateTable(headerCells, rows));
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
            // 増分5の教訓: 1行目は外側のif連鎖で「表としてマッチしなかった(次行が区切り線でない
            // `|`始まり行等)」ことしか確定していないため、下記除外条件だけでは1行目自体が除外され
            // paragraphLinesが空のままiが進まず無限ループする恐れがある。1行目は必ず無条件で
            // 取り込み、2行目以降のみ除外条件を適用する(将来の除外条件追加でも安全な設計)。
            var paragraphLines = new System.Collections.Generic.List<string> { lines[i] };
            i++;
            while (i < lines.Length
                && !string.IsNullOrWhiteSpace(lines[i])
                && !ImageLine.IsMatch(lines[i])
                && !Regex.IsMatch(lines[i], @"^(#{1,3})\s+")
                && !Regex.IsMatch(lines[i], @"^-{3,}$")
                && !lines[i].TrimStart().StartsWith("```")
                && !lines[i].TrimStart().StartsWith("|")
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

    /// <summary>
    /// 画像の段を作る。絵は<b>元の大きさより拡大せず</b>、ウィンドウが狭ければ幅に合わせて縮む
    /// （<see cref="StretchDirection.DownOnly"/>）——スクリーンショットは拡大すると文字がぼやける。
    /// <para>
    /// <b>【画素数をそのまま幅にする】</b><c>BitmapSource.Width</c> は画像に記録された DPI で換算した
    /// 値を返すゆえ、拡大率 150% の画面で撮った絵（144dpi と記録される）は 2/3 の大きさに化ける。
    /// 説明図は「撮った画素数どおり」に見せたいので <c>PixelWidth</c> を上限にする。
    /// </para></summary>
    private static Block CreateImageBlock(string altText, string path, Func<string, ImageSource?>? imageResolver)
    {
        var source = imageResolver?.Invoke(path);
        if (source is null)
            return CreateParagraph(altText.Length > 0 ? $"［図: {altText}］" : "［図］");

        var image = new System.Windows.Controls.Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            StretchDirection = System.Windows.Controls.StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = altText.Length > 0 ? altText : null,
        };
        if (source is System.Windows.Media.Imaging.BitmapSource bitmap) image.MaxWidth = bitmap.PixelWidth;
        System.Windows.Automation.AutomationProperties.SetName(image, altText);
        return new BlockUIContainer(image) { Margin = new Thickness(0, 4, 0, 12) };
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

    /// <summary>表の区切り線(`|---|---|`等)か判定する(増分5)。対応範囲は単純な左揃えのみ
    /// (`:---:`等の位置指定拡張構文は非対応、docs/usage全11領域の実測範囲で不要と確認済み)。</summary>
    private static bool IsTableSeparatorRow(string line)
    {
        var cells = SplitTableRow(line);
        return cells.Length > 0 && cells.All(c => Regex.IsMatch(c, @"^:?-+:?$"));
    }

    /// <summary>表の行(`| a | b |`)をセル配列へ分割する(増分5)。先頭・末尾の`|`前後の空要素を除く。</summary>
    private static string[] SplitTableRow(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
        return trimmed.Split('|').Select(c => c.Trim()).ToArray();
    }

    /// <summary>Markdown表をWPF Tableへ変換する(増分5)。ヘッダー行は太字+ヘッダー背景色、
    /// データ行は下線のみの罫線(列区切りの縦線は簡略化のため省く)。データ行の列数がヘッダーより
    /// 少ない場合は残りを空セルで埋める(docs/usage実例=menu-toolbarの継続行表記に対応)。</summary>
    private static Table CreateTable(string[] headerCells, System.Collections.Generic.List<string[]> rows)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 8) };
        for (int c = 0; c < headerCells.Length; c++)
            table.Columns.Add(new TableColumn());

        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        var headerRow = new TableRow();
        foreach (var cellText in headerCells)
            headerRow.Cells.Add(CreateTableCell(cellText, isHeader: true));
        rowGroup.Rows.Add(headerRow);

        foreach (var rowCells in rows)
        {
            var row = new TableRow();
            for (int c = 0; c < headerCells.Length; c++)
                row.Cells.Add(CreateTableCell(c < rowCells.Length ? rowCells[c] : "", isHeader: false));
            rowGroup.Rows.Add(row);
        }

        return table;
    }

    private static TableCell CreateTableCell(string text, bool isHeader)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        paragraph.Inlines.AddRange(CreateInlines(text));
        if (isHeader) paragraph.FontWeight = FontWeights.Bold;

        var cell = new TableCell(paragraph)
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(6, 3, 6, 3),
        };
        cell.SetResourceReference(TableCell.BorderBrushProperty, "PanelGridLineBrush");
        if (isHeader)
        {
            cell.SetResourceReference(TableCell.BackgroundProperty, "PanelHeaderBackgroundBrush");
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "PanelHeaderForegroundBrush");
        }
        return cell;
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
