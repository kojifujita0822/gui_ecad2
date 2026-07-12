using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.Pdf;

/// <summary>PDF出力の1物理ページの種別(T-060)。</summary>
public enum PdfPageKind { Sheet, CrossRef, Bom }

/// <summary>PDF出力の1物理ページのメタ情報(T-060)。<see cref="PdfPageLayout.Build"/>が構築する。
/// <paramref name="Scale"/>はSheetページの縮小率(T-080 DoD(6)、殿裁定=縮小フィット、
/// enableBorder=false時・CrossRef/BOMページは常に1.0)。</summary>
public sealed record PdfPage(
    PdfPageKind Kind, Sheet? Sheet, int PageRowStart, int PageNumber, int TotalPages, int CrPageIndex,
    double Scale = 1.0);

/// <summary>
/// PDF出力のページ構成(シート走査・枠ありページ分割・クロスリファレンス/BOM有無判定)を
/// 一箇所に集約する(T-060隠密静的レビュー指摘A、往復1周目)。<see cref="PdfExporter"/>の実出力と
/// プレビューダイアログの両方が本ヘルパーを参照することで、表題欄に印字される総ページ数
/// (pageNumber/totalPages)が常に一致することを保証する(修正前はPdfExporter.Exportと
/// PdfPreviewDialog.BuildPageListが独立に同じ計算を2箇所実装しており、totalPagesの算出範囲が
/// 食い違っていた=WYSIWYG違反)。
/// </summary>
public static class PdfPageLayout
{
    public static IReadOnlyList<PdfPage> Build(LadderDocument document, DiagramRenderer dr, CrossReference xref, bool enableBorder)
    {
        int crPages = dr.CrossRefPageCount(xref);
        bool hasBom = document.Devices.ByName.Count > 0 && document.Sheets.Count > 0;
        int sheetPages = enableBorder ? document.Sheets.Sum(dr.RenderPageCount) : document.Sheets.Count;
        int totalPages = sheetPages + crPages + (hasBom ? 1 : 0);

        var pages = new List<PdfPage>();
        int physical = 0;
        foreach (var sheet in document.Sheets)
        {
            int pageCount = enableBorder ? dr.RenderPageCount(sheet) : 1;
            // T-080 DoD(6): 縮小フィットはenableBorder=true(用紙固定)時のみ意味を持つ。
            // enableBorder=false(可変ページ)はそもそも必要幅ぶんページが広がるため縮小不要。
            double scale = enableBorder ? dr.CalcPageScale(sheet) : 1.0;
            for (int p = 0; p < pageCount; p++)
            {
                physical++;
                pages.Add(new PdfPage(PdfPageKind.Sheet, sheet, p * dr.RowsPerPage, physical, totalPages, 0, scale));
            }
        }
        for (int cp = 0; cp < crPages; cp++)
        {
            physical++;
            pages.Add(new PdfPage(PdfPageKind.CrossRef, null, 0, physical, totalPages, cp));
        }
        if (hasBom)
        {
            physical++;
            pages.Add(new PdfPage(PdfPageKind.Bom, null, 0, physical, totalPages, 0));
        }
        return pages;
    }
}
