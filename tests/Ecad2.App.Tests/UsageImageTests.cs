using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ecad2.App.Views;

namespace Ecad2.App.Tests;

/// <summary>
/// 使い方へ説明図（スクリーンショット＋矢印）を載せるための土台のテスト（殿ご下命2026-10-09）。
/// 変換器の画像記法 <c>![説明](パス)</c> と、本文が参照する絵の埋め込み漏れを測る。
/// </summary>
public class UsageImageTests
{
    /// <summary>拡大率150%の画面で撮った絵を模す（144dpi と記録され、<c>Width</c> は画素数の 2/3 になる）。</summary>
    private static BitmapSource Bitmap(int pixelWidth, int pixelHeight, double dpi = 144)
    {
        var bitmap = BitmapSource.Create(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Bgra32, null,
            new byte[pixelWidth * pixelHeight * 4], pixelWidth * 4);
        bitmap.Freeze();
        return bitmap;
    }

    [Fact]
    public void Convert_ImageLine_BecomesImageBlockAtPixelSize()
        => StaTestRunner.Run(() =>
        {
            var source = Bitmap(600, 300);
            string? requested = null;

            var document = MarkdownFlowDocumentConverter.Convert(
                "# 見出し\n\n![画面の構成](images/overview.png)\n\n本文",
                path => { requested = path; return source; });

            Assert.Equal("images/overview.png", requested);
            var container = Assert.Single(document.Blocks.OfType<BlockUIContainer>());
            var image = Assert.IsType<Image>(container.Child);
            Assert.Same(source, image.Source);
            // 記録された DPI に依らず、撮った画素数が上限。拡大はせず、狭ければ縮む。
            Assert.Equal(600, image.MaxWidth);
            Assert.NotEqual(600, source.Width);
            Assert.Equal(StretchDirection.DownOnly, image.StretchDirection);
            Assert.Equal("画面の構成", image.ToolTip);
            Assert.Equal(3, document.Blocks.Count);
        });

    /// <summary>絵が引けぬ（解決関数が無い・null を返す）ときは説明文だけを出し、本文は壊さぬ。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Convert_ImageNotResolved_FallsBackToAltText(bool resolverGiven)
    {
        var document = MarkdownFlowDocumentConverter.Convert(
            "![画面の構成](images/missing.png)",
            resolverGiven ? _ => null : null);

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
        Assert.Equal("［図: 画面の構成］", new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
    }

    /// <summary>段落の直後の行に置いた画像は、段落へ呑まれず独立した段になる。</summary>
    [Fact]
    public void Convert_ImageLineRightAfterParagraph_IsNotMergedIntoParagraph()
    {
        var document = MarkdownFlowDocumentConverter.Convert("説明の文\n![図](images/a.png)\n続きの文");

        var texts = document.Blocks.OfType<Paragraph>()
            .Select(p => new TextRange(p.ContentStart, p.ContentEnd).Text).ToArray();
        Assert.Equal(new[] { "説明の文", "［図: 図］", "続きの文" }, texts);
    }

    /// <summary>文の途中に書かれた <c>![..](..)</c> は画像として扱わぬ（行全体が画像の記法である場合のみ）。</summary>
    [Fact]
    public void EnumerateImagePaths_OnlyWholeLineImages()
    {
        string markdown = "![一](images/1.png)\r\n文中の ![二](images/2.png) は対象外\n\n![三](images/3.png)  \n";

        Assert.Equal(new[] { "images/1.png", "images/3.png" }, MarkdownFlowDocumentConverter.EnumerateImagePaths(markdown));
    }

    /// <summary>
    /// <b>使い方の本文が参照する絵は、すべて埋め込まれておる。</b>
    /// 絵のファイルを置き忘れる・名を打ち誤ると、アプリでは説明文だけが出て絵が黙って消える
    /// ——ビルドは通るゆえ、ここで捕まえる。
    /// </summary>
    [Fact]
    public void AllImagesReferencedByUsageTopics_AreEmbedded()
    {
        var missing = UsageWindow.Topics
            .SelectMany(topic => MarkdownFlowDocumentConverter
                .EnumerateImagePaths(UsageWindow.LoadEmbeddedMarkdown(topic.ResourceFileName))
                .Select(path => (topic.ResourceFileName, path)))
            .Where(reference => UsageWindow.LoadEmbeddedImage(reference.path) is null)
            .Select(reference => $"{reference.ResourceFileName} -> {reference.path}")
            .ToArray();

        Assert.True(missing.Length == 0, "埋め込まれておらぬ絵: " + string.Join(", ", missing));
    }

    [Fact]
    public void LoadEmbeddedImage_UnknownFile_ReturnsNull()
    {
        Assert.Null(UsageWindow.LoadEmbeddedImage("images/この名の絵は無い.png"));
    }
}
