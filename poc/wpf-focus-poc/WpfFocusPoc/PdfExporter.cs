using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace WpfFocusPoc;

public static class PdfExporter
{
    public static void ExportSymbols(string path, int count)
    {
        using var document = new PdfDocument();
        PdfPage page = document.AddPage();
        using XGraphics gfx = XGraphics.FromPdfPage(page);

        var pen = XPens.Black;
        const double spacing = 14;
        int cols = (int)Math.Sqrt(count) + 1;

        for (int i = 0; i < count; i++)
        {
            double x = (i % cols) * spacing;
            double y = (i / cols) * spacing;
            if (y > page.Height.Point - spacing)
            {
                break;
            }

            gfx.DrawLine(pen, x, y + 2, x + 3.5, y + 2);
            gfx.DrawLine(pen, x + 7, y + 2, x + 10.5, y + 2);
            gfx.DrawEllipse(pen, x + 2.5, y + 1, 2, 2);
            gfx.DrawEllipse(pen, x + 7.5, y + 1, 2, 2);
            gfx.DrawLine(pen, x + 3, y, x + 8, y + 4);
        }

        document.Save(path);
    }
}
