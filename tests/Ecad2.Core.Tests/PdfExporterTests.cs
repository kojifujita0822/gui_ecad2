using Ecad2.Model;
using Ecad2.Pdf;
using Ecad2.Persistence;
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
}
