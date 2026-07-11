using System.Windows;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.App.Views;

/// <summary>
/// PDF出力のプレビューダイアログ(T-060)。GuiEcad(<c>PdfPreviewDialog.xaml.cs</c>)の
/// ページ構成ロジック(<c>BuildPageList</c>)・ページ送り・ズーム操作をそのまま移植し、
/// 描画基盤のみWPF(<see cref="PdfPreviewCanvas"/>、DrawingVisualベース)へ置き換えた。
/// 出力範囲は常に全シート・枠は常にあり(殿裁定2026-07-12、切替UIは設けない)。
/// </summary>
public partial class PdfPreviewDialog : Window
{
    private enum PageKind { Sheet, CrossRef, Bom }

    private sealed record PreviewPage(
        PageKind Kind, Sheet? Sheet, int PageRowStart, int PageNumber, int TotalPages, int CrPageIndex);

    private readonly LadderDocument _document;
    private readonly PartLibrary? _library;
    private readonly CrossReference _xref;
    private readonly bool _enableBorder;
    private readonly DiagramRenderer _dr;
    private readonly List<PreviewPage> _pages = new();

    private int _currentIndex;
    private double _zoom = 1.0;

    public PdfPreviewDialog(LadderDocument document, PartLibrary? library, CrossReference xref, bool enableBorder)
    {
        _document = document;
        _library = library;
        _xref = xref;
        _enableBorder = enableBorder;
        _dr = new DiagramRenderer(DrawingTheme.Default,
            new RenderOptions { PaperSize = document.Settings.PaperSize, IncludeTracingImages = false });

        InitializeComponent();
        BuildPageList();
        UpdateUI();
    }

    private void BuildPageList()
    {
        int sheetPages = _enableBorder ? _document.Sheets.Sum(_dr.RenderPageCount) : _document.Sheets.Count;
        int crPages = _dr.CrossRefPageCount(_xref);
        bool hasBom = _document.Devices.ByName.Count > 0 && _document.Sheets.Count > 0;
        int totalPages = sheetPages + crPages + (hasBom ? 1 : 0);

        int physical = 0;
        foreach (var sheet in _document.Sheets)
        {
            int pages = _enableBorder ? _dr.RenderPageCount(sheet) : 1;
            for (int p = 0; p < pages; p++)
            {
                physical++;
                _pages.Add(new PreviewPage(PageKind.Sheet, sheet, p * _dr.RowsPerPage, physical, totalPages, 0));
            }
        }
        for (int cp = 0; cp < crPages; cp++)
        {
            physical++;
            _pages.Add(new PreviewPage(PageKind.CrossRef, null, 0, physical, totalPages, cp));
        }
        if (hasBom)
        {
            physical++;
            _pages.Add(new PreviewPage(PageKind.Bom, null, 0, physical, totalPages, 0));
        }
    }

    private Size2D GetPageSize(PreviewPage page) => page.Kind switch
    {
        PageKind.Sheet => _dr.PageSize(page.Sheet!, xref: null, info: _document.Info, enableBorder: _enableBorder),
        PageKind.CrossRef => _dr.CrossRefPageSize(),
        PageKind.Bom => _dr.BomPageSize(_document.Sheets[^1].Grid.Columns, _document.Devices.ByName.Count),
        _ => _dr.CrossRefPageSize(),
    };

    private void UpdateUI()
    {
        int total = _pages.Count;
        PageLabel.Text = total == 0 ? "ページなし" : $"{_currentIndex + 1} / {total} ページ";
        PrevBtn.IsEnabled = _currentIndex > 0;
        NextBtn.IsEnabled = _currentIndex < total - 1;
        ZoomLabel.Text = $"{_zoom * 100:F0}%";
        ZoomOutBtn.IsEnabled = _zoom > 0.25 + 0.01;
        ZoomInBtn.IsEnabled = _zoom < 4.0 - 0.01;

        RefreshCanvas();
    }

    private void RefreshCanvas()
    {
        if (_pages.Count == 0) return;
        var page = _pages[_currentIndex];
        var size = GetPageSize(page);
        PreviewCanvas.DrawPage(size, _zoom, renderer =>
        {
            switch (page.Kind)
            {
                case PageKind.Sheet:
                    _dr.Render(renderer, page.Sheet!, _library, sim: null, xref: null, info: _document.Info,
                               pageNumber: page.PageNumber, totalPages: page.TotalPages,
                               enableBorder: _enableBorder, pageRowStart: page.PageRowStart,
                               pageRowCount: _enableBorder ? _dr.RowsPerPage : int.MaxValue);
                    break;
                case PageKind.CrossRef:
                    _dr.RenderCrossRefPage(renderer, _xref, page.CrPageIndex);
                    break;
                case PageKind.Bom:
                    _dr.RenderBomPage(renderer, _document.Devices, _document.Sheets[^1].Grid.Columns);
                    break;
            }
        });
    }

    private void OnPrev(object sender, RoutedEventArgs e)
    {
        if (_currentIndex <= 0) return;
        _currentIndex--;
        UpdateUI();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_currentIndex >= _pages.Count - 1) return;
        _currentIndex++;
        UpdateUI();
    }

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(0.25, Math.Round(_zoom - 0.25, 2));
        UpdateUI();
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(4.0, Math.Round(_zoom + 0.25, 2));
        UpdateUI();
    }

    private void OnFitWidth(object sender, RoutedEventArgs e)
    {
        _zoom = 1.0;
        UpdateUI();
    }

    // PDF出力ボタン: 保存ダイアログは既存パターン(SaveAsMenuItem_Click等)を踏襲。
    // エラー表示はUI/UX分岐ではなく技術確認事項のため既存のMessageBoxパターンで足りると判断
    // (家老采配2026-07-12でスコープ内と明記)。
    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF ファイル (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = string.IsNullOrWhiteSpace(_document.Info.Title) ? "diagram" : _document.Info.Title,
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            Ecad2.Pdf.PdfExporter.Export(_document, _library, dialog.FileName);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"PDF出力に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
