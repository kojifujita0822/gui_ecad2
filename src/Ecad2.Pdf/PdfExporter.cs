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

        // 物理ページ総数(枠あり時はRowsPerPage行ごとに複数ページへ分割、主回路シートはmmベースの
        // 内容の広がりも加味する=RenderPageCount)。
        int totalPages = enableBorder
            ? document.Sheets.Sum(dr.RenderPageCount)
            : document.Sheets.Count;

        int physical = 0;
        foreach (var sheet in document.Sheets)
        {
            // クロスリファレンス表はシート図面には描かず、専用ページに分ける。
            int pages = enableBorder ? dr.RenderPageCount(sheet) : 1;
            for (int p = 0; p < pages; p++)
            {
                physical++;
                var renderer = surface.BeginPage(dr.PageSize(sheet, null, info, enableBorder));
                dr.Render(renderer, sheet, library, xref: null, info: info,
                          pageNumber: physical, totalPages: totalPages, enableBorder: enableBorder,
                          pageRowStart: p * dr.RowsPerPage,
                          pageRowCount: enableBorder ? dr.RowsPerPage : int.MaxValue);
                surface.EndPage();
            }
        }

        // クロスリファレンス一覧表を専用ページ(A4縦)として追加する。1ページに収まらない場合は
        // 複数ページへ分割する。
        int crPages = dr.CrossRefPageCount(xref);
        for (int cp = 0; cp < crPages; cp++)
        {
            var crRenderer = surface.BeginPage(dr.CrossRefPageSize());
            dr.RenderCrossRefPage(crRenderer, xref, cp);
            surface.EndPage();
        }

        // 機器表が1件以上あるときBOM専用ページを最後に追加する。
        var devices = document.Devices;
        if (devices.ByName.Count > 0 && document.Sheets.Count > 0)
        {
            int lastColumns = document.Sheets[^1].Grid.Columns;
            var bomRenderer = surface.BeginPage(dr.BomPageSize(lastColumns, devices.ByName.Count));
            dr.RenderBomPage(bomRenderer, devices, lastColumns);
            surface.EndPage();
        }
    }
}
