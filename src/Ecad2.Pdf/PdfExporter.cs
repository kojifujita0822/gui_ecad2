using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.Pdf;

/// <summary>
/// ドキュメント全体をPDFへ書き出すオーケストレーション(T-060)。GuiEcad
/// (<c>GuiEcad.App/MainPage.Menu.cs</c> OnMenuExportPdf)のロジックをCore層API面の一致を
/// 確認した上でそのまま移植した。出力範囲は常に全シート(現在シートのみの選択肢は設けない、
/// 殿裁定2026-07-12)。枠は常にあり(<see cref="DocumentSettings.EnableBorder"/>の値をそのまま
/// 使う、切替UIは設けない)。
/// </summary>
public static class PdfExporter
{
    public static void Export(LadderDocument document, PartLibrary? library, string path)
    {
        // 回路番号を付与してからクロスリファレンスを生成する(採番済みSheet.Linesを参照するため)。
        CircuitNumberer.Number(document);
        var xref = CrossReferenceBuilder.Build(document, library);

        var info = document.Info;
        bool enableBorder = document.Settings.EnableBorder;
        var dr = new DiagramRenderer(DrawingTheme.Default,
            new RenderOptions { PaperSize = document.Settings.PaperSize, IncludeTracingImages = false });
        using var surface = new PdfRenderSurface(path);

        // ページ構成(シート走査・枠ありページ分割・CrossRef/BOM有無判定)はPdfPageLayoutへ集約
        // (T-060隠密静的レビュー指摘A、往復1周目)。プレビューダイアログ(PdfPreviewDialog)も同じ
        // ヘルパーを参照するため、表題欄に印字される総ページ数(pageNumber/totalPages)が
        // プレビューと実出力とで常に一致する。
        var pages = PdfPageLayout.Build(document, dr, xref, enableBorder);
        var devices = document.Devices;

        foreach (var page in pages)
        {
            switch (page.Kind)
            {
                case PdfPageKind.Sheet:
                    var renderer = surface.BeginPage(dr.PageSize(page.Sheet!, null, info, enableBorder));
                    dr.Render(renderer, page.Sheet!, library, xref: null, info: info,
                              pageNumber: page.PageNumber, totalPages: page.TotalPages, enableBorder: enableBorder,
                              pageRowStart: page.PageRowStart,
                              pageRowCount: enableBorder ? dr.RowsPerPage : int.MaxValue);
                    surface.EndPage();
                    break;
                case PdfPageKind.CrossRef:
                    var crRenderer = surface.BeginPage(dr.CrossRefPageSize());
                    dr.RenderCrossRefPage(crRenderer, xref, page.CrPageIndex);
                    surface.EndPage();
                    break;
                case PdfPageKind.Bom:
                    int lastColumns = document.Sheets[^1].Grid.Columns;
                    var bomRenderer = surface.BeginPage(dr.BomPageSize(lastColumns, devices.ByName.Count));
                    dr.RenderBomPage(bomRenderer, devices, lastColumns);
                    surface.EndPage();
                    break;
            }
        }
    }
}
