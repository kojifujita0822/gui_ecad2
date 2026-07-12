using Ecad2.Model;
using Ecad2.Pdf;
using Ecad2.Persistence;
using Ecad2.Rendering;
using Ecad2.Simulation;
using PdfSharp.Pdf.IO;

namespace Ecad2.Core.Tests;

/// <summary>T-060: PdfExporter(全シート走査+枠ページ分割+CrossRef+BOM)のオーケストレーションを
/// 検証する。PDF内部描画の見た目自体は既存DiagramRendererのテスト・実機確認で担保済みのため、
/// ここでは「PdfReaderで再読込できるPDFが生成される」「ページ数が期待どおり」に絞って確認する。</summary>
public class PdfExporterTests : IDisposable
{
    private readonly string _tempDir;

    public PdfExporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ecad2-pdfexport-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static PartLibrary CreateLibrary()
    {
        var lib = new PartLibrary();
        foreach (var def in BasicPartTemplates.All())
            lib.ById[def.Id] = def;
        return lib;
    }

    [Fact]
    public void Export_全シートCrossRefBOMを含むPDFを生成する()
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(new ElementInstance { PartId = BasicPartTemplates.ContactNOId, Pos = new GridPos(0, 0), DeviceName = "X001" });
        sheet.Elements.Add(new ElementInstance { PartId = BasicPartTemplates.CoilId, Pos = new GridPos(0, 5), DeviceName = "Y001" });
        var doc = new LadderDocument();
        doc.Sheets.Add(sheet);
        doc.Devices.ByName["X001"] = new Device { Name = "X001", Class = DeviceClass.PushButton, Model = "AL6M" };
        doc.Devices.ByName["Y001"] = new Device { Name = "Y001", Class = DeviceClass.Relay };
        string path = Path.Combine(_tempDir, "out.pdf");

        PdfExporter.Export(doc, CreateLibrary(), path);

        Assert.True(File.Exists(path));
        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        // 図面(枠あり、小さい図面なので1ページ) + CrossRef(コイル/接点1件以上で1ページ) + BOM(機器2件で1ページ)。
        Assert.Equal(3, pdf.PageCount);
    }

    [Fact]
    public void Export_機器表示無しシートのみの場合BOMページを追加しない()
    {
        var doc = new LadderDocument();
        doc.Sheets.Add(new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } });
        string path = Path.Combine(_tempDir, "empty.pdf");

        PdfExporter.Export(doc, CreateLibrary(), path);

        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        // 図面1ページのみ(CrossRefエントリ0件・機器0件のためCrossRef/BOMページとも追加されない)。
        Assert.Equal(1, pdf.PageCount);
    }

    [Fact]
    public void Export_複数シートを全て走査する()
    {
        var doc = new LadderDocument();
        doc.Sheets.Add(new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } });
        doc.Sheets.Add(new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } });
        doc.Sheets.Add(new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } });
        string path = Path.Combine(_tempDir, "multi.pdf");

        PdfExporter.Export(doc, CreateLibrary(), path);

        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(3, pdf.PageCount);
    }

    /// <summary>T-060隠密静的レビュー指摘A回帰テスト: PdfExporter.Export(実出力)と
    /// PdfPageLayout.Build(プレビューが参照する構成)が同じ総ページ数を導出すること
    /// (修正前は独立2実装で、CrossRef/BOMページの加算漏れにより表題欄の総ページ数
    /// (pageNumber/totalPages)がプレビューと実出力とで食い違っていた=WYSIWYG違反)。</summary>
    [Fact]
    public void PdfPageLayout_TotalPagesは実出力の物理ページ数と一致する()
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(new ElementInstance { PartId = BasicPartTemplates.ContactNOId, Pos = new GridPos(0, 0), DeviceName = "X001" });
        sheet.Elements.Add(new ElementInstance { PartId = BasicPartTemplates.CoilId, Pos = new GridPos(0, 5), DeviceName = "Y001" });
        var doc = new LadderDocument();
        doc.Sheets.Add(sheet);
        doc.Devices.ByName["X001"] = new Device { Name = "X001", Class = DeviceClass.PushButton };
        doc.Devices.ByName["Y001"] = new Device { Name = "Y001", Class = DeviceClass.Relay };
        var lib = CreateLibrary();

        CircuitNumberer.Number(doc);
        var xref = CrossReferenceBuilder.Build(doc, lib);
        var dr = new DiagramRenderer(DrawingTheme.Default, new RenderOptions { IncludeTracingImages = false });
        var pages = PdfPageLayout.Build(doc, dr, xref, enableBorder: true);

        string path = Path.Combine(_tempDir, "wysiwyg.pdf");
        PdfExporter.Export(doc, lib, path);
        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);

        Assert.Equal(pdf.PageCount, pages.Count);
        Assert.All(pages, p => Assert.Equal(pages.Count, p.TotalPages));
    }

    /// <summary>T-080 DoD(6)回帰テスト: 20文字の行コメントを含むシートでもPDF生成自体は
    /// 例外なく完了し(縮小適用によるPushTransform/PopTransformの不整合が無いこと)、
    /// PdfPageLayout.Buildが計算するSheetページのScaleが縮小(<1.0)になっていること。</summary>
    [Fact]
    public void Export_20文字行コメント付きシート_例外なく出力しScaleが縮小になる()
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.RungComments.Add(new RungComment { Row = 0, Text = new string('あ', 20) });
        var doc = new LadderDocument();
        doc.Sheets.Add(sheet);
        var lib = CreateLibrary();
        string path = Path.Combine(_tempDir, "long-comment.pdf");

        var dr = new DiagramRenderer(DrawingTheme.Default, new RenderOptions { IncludeTracingImages = false });
        CircuitNumberer.Number(doc);
        var xref = CrossReferenceBuilder.Build(doc, lib);
        var pages = PdfPageLayout.Build(doc, dr, xref, enableBorder: true);

        PdfExporter.Export(doc, lib, path);

        Assert.True(File.Exists(path));
        var sheetPage = Assert.Single(pages.Where(p => p.Kind == PdfPageKind.Sheet));
        Assert.True(sheetPage.Scale < 1.0);
    }

    /// <summary>T-080往復1周目指摘Cの回帰テスト: 縮小率はシート単位でなくページ(行範囲)単位で
    /// 計算する。40行シート(A4縦・RowsPerPage=28で2ページ分割)の1ページ目にのみ長い行コメントが
    /// ある場合、1ページ目だけが縮小され、コメントの無い2ページ目は等倍のまま(修正前はシート単位の
    /// 一括計算で、無関係な2ページ目まで一律に縮小されていた)。</summary>
    [Fact]
    public void PdfPageLayout_行コメントの無いページは縮小しない()
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 40, Columns = 20 } };
        sheet.RungComments.Add(new RungComment { Row = 0, Text = new string('あ', 20) });
        var doc = new LadderDocument();
        doc.Sheets.Add(sheet);
        var lib = CreateLibrary();

        var dr = new DiagramRenderer(DrawingTheme.Default, new RenderOptions { IncludeTracingImages = false });
        CircuitNumberer.Number(doc);
        var xref = CrossReferenceBuilder.Build(doc, lib);
        var pages = PdfPageLayout.Build(doc, dr, xref, enableBorder: true);

        var sheetPages = pages.Where(p => p.Kind == PdfPageKind.Sheet).ToList();
        Assert.Equal(2, sheetPages.Count);
        Assert.True(sheetPages[0].Scale < 1.0);
        Assert.Equal(1.0, sheetPages[1].Scale);
    }

    /// <summary>T-080 DoD(6)検証観点(2): 行コメント無しの既存シートは縮小がかからない
    /// (Scale=1.0、従来と同じ見た目)。</summary>
    [Fact]
    public void Export_行コメント無し_Scaleは等倍のまま()
    {
        var doc = new LadderDocument();
        doc.Sheets.Add(new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } });
        var lib = CreateLibrary();

        var dr = new DiagramRenderer(DrawingTheme.Default, new RenderOptions { IncludeTracingImages = false });
        CircuitNumberer.Number(doc);
        var xref = CrossReferenceBuilder.Build(doc, lib);
        var pages = PdfPageLayout.Build(doc, dr, xref, enableBorder: true);

        var sheetPage = Assert.Single(pages.Where(p => p.Kind == PdfPageKind.Sheet));
        Assert.Equal(1.0, sheetPage.Scale);
    }
}
