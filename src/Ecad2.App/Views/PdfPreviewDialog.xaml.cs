using System.Windows;
using Ecad2.Model;
using Ecad2.Pdf;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.App.Views;

/// <summary>
/// PDF出力のプレビューダイアログ(T-060)。GuiEcad(<c>PdfPreviewDialog.xaml.cs</c>)の
/// ページ送り・ズーム操作をそのまま移植し、描画基盤のみWPF(<see cref="PdfPreviewCanvas"/>、
/// DrawingVisualベース)へ置き換えた。ページ構成(シート走査・枠ありページ分割・CrossRef/BOM
/// 有無判定)は<see cref="PdfPageLayout"/>を<see cref="PdfExporter"/>と共有し、表題欄の総ページ数
/// (pageNumber/totalPages)がプレビューと実出力とで一致することを保証する(T-060隠密静的レビュー
/// 指摘A対応、往復1周目)。出力範囲は常に全シート・枠は常にあり(殿裁定2026-07-12、切替UIは
/// 設けない)。
/// </summary>
public partial class PdfPreviewDialog : Window
{
    private readonly LadderDocument _document;
    private readonly PartLibrary? _library;
    private readonly CrossReference _xref;
    private readonly bool _enableBorder;
    private readonly DiagramRenderer _dr;
    private readonly IReadOnlyList<PdfPage> _pages;

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
        _pages = PdfPageLayout.Build(document, _dr, xref, enableBorder);

        InitializeComponent();
        UpdateUI();
    }

    private Size2D GetPageSize(PdfPage page) => page.Kind switch
    {
        PdfPageKind.Sheet => _dr.PageSize(page.Sheet!, xref: null, info: _document.Info, enableBorder: _enableBorder),
        PdfPageKind.CrossRef => _dr.CrossRefPageSize(),
        PdfPageKind.Bom => _dr.BomPageSize(_document.Sheets[^1].Grid.Columns, _document.Devices.ByName.Count),
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

    // T-060隠密静的レビュー指摘D対応: GuiEcad原本と同じ「ダイアログ幅に対する相対フィット」で
    // scaleを算出する(zoom=1.0のとき常にScrollViewerの表示幅いっぱいに収まる)。ecad2は
    // WpfRenderer側でmm→DIP変換(PdfPreviewCanvas.MmToDip)を行うため、GuiEcad原本の
    // scale = zoom*(availW-40)/pageWidthMm をそのままDIP変換後の値に換算し直す必要がある。
    private void RefreshCanvas()
    {
        if (_pages.Count == 0) return;
        var page = _pages[_currentIndex];
        var size = GetPageSize(page);

        double availW = Math.Max(Scroll.ActualWidth, 400);
        double scale = _zoom * (availW - 40) / (size.Width * PdfPreviewCanvas.MmToDip);

        PreviewCanvas.DrawPage(size, scale, renderer =>
        {
            switch (page.Kind)
            {
                case PdfPageKind.Sheet:
                    // page.Scale(T-080 DoD(6)、印刷時の縮小フィット)はPdfExporterと同じ
                    // PdfPageLayoutが計算するため、プレビューと実出力で常に一致する(WYSIWYG)。
                    // 上記の表示用scale(ダイアログ幅フィット)とは独立したレイヤーで、両方が
                    // 掛け合わさって最終的な画面表示サイズになる。
                    _dr.Render(renderer, page.Sheet!, _library, sim: null, xref: null, info: _document.Info,
                               pageNumber: page.PageNumber, totalPages: page.TotalPages,
                               enableBorder: _enableBorder, pageRowStart: page.PageRowStart,
                               pageRowCount: _enableBorder ? _dr.RowsPerPage : int.MaxValue,
                               pageScale: page.Scale, devices: _document.Devices);
                    break;
                case PdfPageKind.CrossRef:
                    _dr.RenderCrossRefPage(renderer, _xref, page.CrPageIndex);
                    break;
                case PdfPageKind.Bom:
                    _dr.RenderBomPage(renderer, _document.Devices, _document.Sheets[^1].Grid.Columns);
                    break;
            }
        });
    }

    private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e) => RefreshCanvas();

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
    // エラー表示はTrySaveToFile/OpenButton_Click(T-024隠密調査推奨、忍者実機検出で2度確立済み)と
    // 同型パターンへ統一する(T-060隠密静的レビュー指摘E対応、ex.Messageの生の技術文面は出さない)。
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
            PdfExporter.Export(_document, _library, dialog.FileName);
            DialogResult = true;
        }
        catch (Exception)
        {
            MessageBox.Show(this,
                $"PDFを出力できませんでした。出力先の権限やディスクの空き容量、ファイルが他のアプリで開かれていないかご確認ください。\n{dialog.FileName}",
                "PDF出力エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
