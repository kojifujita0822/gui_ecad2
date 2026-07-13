using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.Rendering;

/// <summary>描画オプション。</summary>
public sealed class RenderOptions
{
    public double CellMm { get; init; } = 9.0;
    public double MarginMm { get; init; } = 20.0;   // 上下左右の余白。A4枠の外枠線(端5mm)と母線名・電圧ラベルが重ならない値。
    public bool ShowDeviceNames { get; init; } = true;
    public bool ShowWireNumbers { get; init; } = true;
    /// <summary>左母線の左側に行番号（1 始まり）を表示する。</summary>
    public bool ShowRowNumbers { get; init; } = true;
    /// <summary>作図ガイドの薄いグリッド線を表示する（既定 false）。</summary>
    public bool ShowGrid { get; init; }
    /// <summary>接続検査モード: 接続済み配線を青、未結線を黒で表示（docs/rendering.md）。</summary>
    public bool ConnectivityCheck { get; init; }
    /// <summary>枠あり出力（PDF/画面ガイド）の用紙サイズ。既定 A4縦。</summary>
    public PaperSize PaperSize { get; init; } = PaperSize.A4;
    /// <summary>トレース用下絵画像（<see cref="ImageInsert.IsTracingOnly"/>=true）を描画するか。
    /// 画面表示は true、PDF出力は false（トレース用は画面のみ・恒久貼付のみPDFに出力）。</summary>
    public bool IncludeTracingImages { get; init; } = true;
}

/// <summary>
/// ドキュメント（幾何）を走査して <see cref="IRenderer"/> を呼ぶ上位描画器。画面/PDFで共用する。
/// 座標は mm。グリッド座標 (Row, 列境界) → mm へ変換して記号・配線・線番を描く。
/// </summary>
public sealed class DiagramRenderer
{
    private readonly DrawingTheme _theme;
    private readonly RenderOptions _opt;
    private readonly GridGeometry _geo;
    private PartLibrary? _lib;

    public DiagramRenderer(DrawingTheme? theme = null, RenderOptions? options = null)
    {
        _theme = theme ?? DrawingTheme.Default;
        _opt = options ?? new RenderOptions();
        _geo = new GridGeometry(_opt.CellMm, _opt.MarginMm);
    }

    /// <summary>描画に用いるグリッド幾何（ヒットテスト等で共有）。</summary>
    public GridGeometry Geometry => _geo;

    private double Cell => _geo.CellMm;
    private double BusPad => Cell * 0.5;   // 母線と端の要素列の間に設ける余白（mm）
    private double X(int boundary) => _geo.X(boundary);
    private double X(double boundary) => _geo.X(boundary);   // 0.5 刻み境界（縦コネクタ）用

    // 複数ページ分割時のページ先頭行（絶対行）。YRow がこれを引いてページ内ローカル座標へ変換する。
    private int _rowBase;
    private double YRow(int row) => _geo.YRow(row - _rowBase);

    /// <summary>枠出力時の1ページあたり行数。これを超える図面は複数ページへ分割する。
    /// 用紙の余白を除いた高さに収まる行数（表題欄は右下固定配置のため全幅では重ならない）。
    /// A4縦では 28（=(297-2*20)/9 の切り捨て）。用紙サイズ（<see cref="RenderOptions.PaperSize"/>）に応じて変わる。</summary>
    public int RowsPerPage => (int)((PageH - 2 * _opt.MarginMm) / Cell);

    /// <summary>シートが描画する総行数（グリッド行数と要素最大行+1 の大きい方）。</summary>
    public static int TotalRows(Sheet sheet)
    {
        int maxRow = 0;
        foreach (var e in sheet.Elements) maxRow = Math.Max(maxRow, e.Pos.Row);
        return Math.Max(sheet.Grid.Rows, maxRow + 1);
    }

    /// <summary>枠出力時にこのシートが必要とする物理ページ数（行分割）。</summary>
    public int PageCount(Sheet sheet) =>
        Math.Max(1, (TotalRows(sheet) + RowsPerPage - 1) / RowsPerPage);

    /// <summary>主回路（Sheet.MainCircuit）シートの仮想行数。自由直線・接続点・枠は mm 実座標で
    /// グリッド行範囲を超えて広がりうるため、その最大 Y を行数換算してページ分割に使う。
    /// ページ分割の内部計算専用の値であり、要素の描画位置には影響しない。</summary>
    private int MainCircuitVirtualRows(Sheet sheet)
    {
        double maxYmm = _opt.MarginMm;
        foreach (var fl in sheet.FreeLines) maxYmm = Math.Max(maxYmm, Math.Max(fl.Y1Mm, fl.Y2Mm));
        foreach (var d in sheet.ConnectionDots) maxYmm = Math.Max(maxYmm, d.YMm);
        foreach (var f in sheet.Frames)
        {
            double fy = f.VisualYMm ?? (_geo.YRow(f.TopLeft.Row) - Cell * 0.4);
            double fh = f.VisualHeightMm ?? f.Height * Cell;
            maxYmm = Math.Max(maxYmm, fy + fh);
        }
        return (int)Math.Ceiling((maxYmm - _opt.MarginMm) / Cell);
    }

    /// <summary>主回路（Sheet.MainCircuit）シートで、ページの行範囲[rowStart, rowEnd)に掛かる
    /// 自由直線・接続点・枠の mm 実座標の最大X。<see cref="MainCircuitVirtualRows"/>のX版
    /// (T-080往復1周目指摘D)。主回路シートは右母線が描画されず(グリッド列数による右端の縛りが
    /// 元々無い)、mm実座標の内容がグリッド幅を超えて広がりうるため、縮小フィット判定
    /// (RequiredContentWidthForScale)がこれを考慮する。行範囲はページ分割と整合させ、当該ページに
    /// 描画されない内容を縮小率へ影響させない(同指摘Cと同じ理)。</summary>
    private double MainCircuitContentMaxX(Sheet sheet, int rowStart, int rowEnd)
    {
        double bandTop = _opt.MarginMm + rowStart * Cell;
        double bandBot = _opt.MarginMm + (double)rowEnd * Cell;
        double maxXmm = 0;
        foreach (var fl in sheet.FreeLines)
            if (Math.Max(fl.Y1Mm, fl.Y2Mm) >= bandTop && Math.Min(fl.Y1Mm, fl.Y2Mm) <= bandBot)
                maxXmm = Math.Max(maxXmm, Math.Max(fl.X1Mm, fl.X2Mm));
        foreach (var d in sheet.ConnectionDots)
            if (d.YMm >= bandTop && d.YMm <= bandBot)
                maxXmm = Math.Max(maxXmm, d.XMm);
        foreach (var f in sheet.Frames)
        {
            double fy = f.VisualYMm ?? (_geo.YRow(f.TopLeft.Row) - Cell * 0.4);
            double fh = f.VisualHeightMm ?? f.Height * Cell;
            if (fy + fh < bandTop || fy > bandBot) continue;
            double fx = f.VisualXMm ?? X(f.TopLeft.Column);
            double fw = f.VisualWidthMm ?? f.Width * Cell;
            maxXmm = Math.Max(maxXmm, fx + fw);
        }
        return maxXmm;
    }

    /// <summary>シートが実際に描画する総行数。主回路シートは <see cref="MainCircuitVirtualRows"/> も加味する。</summary>
    private int EffectiveTotalRows(Sheet sheet)
    {
        int rows = TotalRows(sheet);
        return sheet.MainCircuit ? Math.Max(rows, MainCircuitVirtualRows(sheet)) : rows;
    }

    /// <summary>枠出力時の物理ページ数。制御回路シートは <see cref="PageCount"/> と同じ結果を返し、
    /// 主回路シートは自由直線・接続点・枠の mm 座標による内容の広がりも考慮する。</summary>
    public int RenderPageCount(Sheet sheet)
    {
        if (!sheet.MainCircuit) return PageCount(sheet);
        return Math.Max(1, (EffectiveTotalRows(sheet) + RowsPerPage - 1) / RowsPerPage);
    }
    private double LeftBusX => X(0) - BusPad;
    /// <summary>右母線のX座標(mm)。App層のヒットテスト(T-080行コメント記入領域判定)からも
    /// 参照するためpublic化する(DrawRungCommentsの描画位置=RightBusX+2mmと同じ基準)。</summary>
    public double RightBusX(int columns) => X(columns) + BusPad;

    // 用紙サイズ（縦固定）。A3 は A4 の長辺・短辺を単純に拡大した 297×420mm。
    private double PageW => _opt.PaperSize == PaperSize.A3 ? 297.0 : 210.0;
    private double PageH => _opt.PaperSize == PaperSize.A3 ? 420.0 : 297.0;

    /// <summary>枠あり出力の図面枠(外枠線)の用紙端からの余白(mm)。DrawBorderの描画位置と
    /// CalcPageScale(縮小フィット判定・縮小の固定点)で共有する(T-080往復1周目指摘A:
    /// 判定基準が用紙物理端(PageW)のままだと、縮小の有無に関わらず行コメントが枠の外側に描かれる)。</summary>
    public const double BorderMarginMm = 5.0;

    /// <summary>行コメントの右母線からのXオフセット(mm)。DrawRungCommentsの描画位置と
    /// App層の編集ボックス位置決め(LadderCanvas.RungCommentAnchorDip)で共有する(T-080)。</summary>
    public const double RungCommentXOffsetMm = 2.0;

    /// <summary>行コメントのフォントサイズ(mm)。描画(DrawRungComments、VAlign.Bottom=アンカーYが
    /// 文字下端で文字は上方向へ展開)と編集ボックスのY位置補正(T-080往復1周目指摘E)で共有する。</summary>
    public const double RungCommentFontSizeMm = 3.0;

    private const double TitleCompanyRowH = 6.0;                              // 社名欄の高さ (mm)
    private const double TitleDetailRowsH = 14.0;                             // 図面名称/図番/ページ行＋顧客等行の高さ (mm)
    private const double TitleBlockH = TitleCompanyRowH + TitleDetailRowsH;   // 表題欄全体の高さ (mm)
    private const double RevRowH     = 7.0;    // 改定欄 データ行の高さ (mm)
    private const double RevHdrH     = 5.0;    // 改定欄 ヘッダ行の高さ (mm)

    private double RevisionBlockH(DocumentInfo info)
        => info.Revisions.Count == 0 ? 0 : RevHdrH + info.Revisions.Count * RevRowH;

    // グリッド全幅+行コメント域(右母線の右側、実際に記入されている最大文字数から算出)の
    // 必要幅(mm)。PageSize(enableBorder=false、可変ページ)専用。行コメントが無くても
    // MarginMm分の右余白を確保する(既存仕様、T-060以前から変更なし)。
    private double RequiredContentWidth(Sheet sheet)
    {
        // 行コメント（右母線の右側）が長いとページ右にはみ出すため、その分の幅を確保する。
        // テキスト幅は概算（行コメントのフォント 3.0mm・全角想定で 1 文字 ≒ 3.3mm）。
        double maxRungLen = sheet.RungComments
            .Where(rc => !string.IsNullOrEmpty(rc.Text))
            .Select(rc => (double)rc.Text.Length)
            .DefaultIfEmpty(0).Max();
        double rightExtra = maxRungLen > 0 ? 2.0 + maxRungLen * 3.3 + _opt.MarginMm : _opt.MarginMm;
        return RightBusX(sheet.Grid.Columns) + rightExtra;
    }

    // 縮小フィット判定(CalcPageScale、T-080 DoD(6))専用の必要幅(mm)。PageSize(enableBorder=false)
    // 用のRequiredContentWidthとは異なり、行コメントが無い場合はMarginMm分の右余白を含めない
    // (母線より右は「何も描画されない空白」であり、そこが用紙外にはみ出しても実害が無いため、
    // 縮小要否の判定に含めるべきではない。検証観点(2)=行コメント無しでは不要な縮小をかけない)。
    // 行範囲[rowStart, rowEnd)はページ分割の窓と同じ(T-080往復1周目指摘C: 当該ページに描画されない
    // 行の行コメントを縮小率へ影響させない)。主回路シートは自由直線・接続点・枠のmm実座標の
    // 広がりも考慮する(同指摘D)。
    private double RequiredContentWidthForScale(Sheet sheet, int rowStart, int rowEnd)
    {
        double maxRungLen = sheet.RungComments
            .Where(rc => !string.IsNullOrEmpty(rc.Text) && rc.Row >= rowStart && rc.Row < rowEnd)
            .Select(rc => (double)rc.Text.Length)
            .DefaultIfEmpty(0).Max();
        double width = RightBusX(sheet.Grid.Columns);
        if (maxRungLen > 0) width += RungCommentXOffsetMm + maxRungLen * 3.3;
        if (sheet.MainCircuit) width = Math.Max(width, MainCircuitContentMaxX(sheet, rowStart, rowEnd));
        return width;
    }

    /// <summary>描画に必要なページサイズ(mm)。enableBorder=true のとき用紙サイズ（RenderOptions.PaperSize）固定。</summary>
    public Size2D PageSize(Sheet sheet, CrossReference? xref = null, DocumentInfo? info = null,
                           bool enableBorder = false)
    {
        if (enableBorder) return new Size2D(PageW, PageH);
        // TotalRows(sheet)はGrid.Rows(論理グリッド行数)と実際の要素最大行+1の大きい方を返す。
        // 以前はここで要素の最大行のみを見ていたため、要素が少ないシートでキャンバス実サイズが
        // Grid.Rowsより極端に小さくなり、空の行へのヒットテストが届かないバグがあった
        // (T-026 OR入力実機検証で発覚、忍者報告のCanvasHeight異常値から判明)。
        int maxRow = TotalRows(sheet) - 1;
        double w = RequiredContentWidth(sheet);
        double diagramH = _opt.MarginMm + (maxRow + 1) * Cell + _opt.MarginMm;
        double tableH = xref is not null ? CalcTableHeight(xref) : 0.0;
        double revH = info is not null ? RevisionBlockH(info) : 0.0;
        double titleH = info is not null ? TitleBlockH : 0.0;
        return new Size2D(w, diagramH + tableH + revH + titleH);
    }

    /// <summary>enableBorder=true時、ページ内容(グリッド全幅+行コメント域、主回路シートは自由直線
    /// 等のmm実座標内容も含む)が図面枠(外枠線、用紙端からBorderMarginMm内側)の幅を超える場合の
    /// 縮小率を計算する(1.0=縮小不要、T-080 DoD(6)、殿裁定=縮小フィット)。判定・縮小とも用紙物理端
    /// (PageW)ではなく図面枠が基準(T-080往復1周目指摘A)。縮小の固定点は図面枠左上(BorderMarginMm)
    /// で、Render側の変換式 x' = BorderMarginMm*(1-scale) + scale*x と対になり、縮小後の内容右端が
    /// 図面枠右端(PageW - BorderMarginMm)ちょうどに収まる(同指摘B)。行範囲(pageRowStart/
    /// pageRowCount)はページ分割と同じ窓でページごとに計算する(同指摘C、複数ページは
    /// PdfPageLayout.Buildがページ単位に渡す。既定値は全行=単一ページ用)。等倍が上限(拡大はしない)。
    /// 必要幅が図面枠内に収まる場合は1.0を返す(不要な縮小をかけない、検証観点(2))。</summary>
    public double CalcPageScale(Sheet sheet, int pageRowStart = 0, int pageRowCount = int.MaxValue)
    {
        int rowEnd = pageRowCount == int.MaxValue ? int.MaxValue : pageRowStart + pageRowCount;
        double neededWidth = RequiredContentWidthForScale(sheet, pageRowStart, rowEnd);
        if (neededWidth <= PageW - BorderMarginMm) return 1.0;
        return (PageW - 2 * BorderMarginMm) / (neededWidth - BorderMarginMm);
    }

    /// <summary>
    /// 図面を描画する。<paramref name="sim"/> を渡すとテストモード: 通電評価を行い、
    /// 通電配線・励磁要素を通電色でハイライトする（画面のテストモード用）。
    /// <paramref name="xref"/> を渡すと図面下部にクロスリファレンス一覧表を描画する。
    /// <paramref name="info"/> を渡すと最下部に表題欄を描画する。
    /// </summary>
    public void Render(IRenderer r, Sheet sheet, PartLibrary? library = null, SimState? sim = null,
                       CrossReference? xref = null, DocumentInfo? info = null,
                       int pageNumber = 1, int totalPages = 1, bool enableBorder = false,
                       int pageRowStart = 0, int pageRowCount = int.MaxValue,
                       double pageScale = 1.0)
    {
        _lib = library;
        var netlist = NetlistBuilder.Build(sheet, library);
        var report = _opt.ConnectivityCheck ? ConnectivityChecker.Check(netlist) : null;

        HashSet<int>? powered = null;
        Dictionary<string, bool>? energized = null;
        if (sim is not null)
        {
            var eval = new Evaluator(netlist).Evaluate(sim);
            powered = eval.PoweredNets;
            energized = eval.State.Energized;
        }

        // 要素 Id → (左net, 右net)
        var elemNet = new Dictionary<Guid, (int A, int B)>();
        foreach (var c in netlist.Components) elemNet[c.SourceElementId] = (c.NetA, c.NetB);

        int columns = sheet.Grid.Columns;

        // ページの行ウィンドウ [rowStart, rowEnd)。pageRowCount=MaxValue なら全行（単一ページ）。
        // 主回路シートは EffectiveTotalRows が自由直線・接続点・枠の広がりも加味した仮想行数を返す。
        int totalRows = EffectiveTotalRows(sheet);
        int rowStart = Math.Clamp(pageRowStart, 0, Math.Max(0, totalRows - 1));
        int rowEnd = pageRowCount == int.MaxValue
            ? totalRows
            : Math.Min(totalRows, rowStart + pageRowCount);
        int localRows = Math.Max(1, rowEnd - rowStart);
        _rowBase = rowStart;   // 以降の YRow はページ内ローカル座標になる
        bool InWindow(int row) => row >= rowStart && row < rowEnd;

        // T-080 DoD(6)(殿裁定=縮小フィット): グリッド全幅+行コメント域が図面枠の幅を超える場合
        // のみ、内容(グリッド本体〜行コメント)全体を図面枠左上(BorderMarginMm)を固定点として一様
        // スケールする(T-080往復1周目指摘B: 原点(0,0)基準では左上余白まで比例して縮み、内容が用紙
        // 左上へ偏る+右端が図面枠内へ戻らない)。x' = BorderMarginMm*(1-scale) + scale*x
        // (CalcPageScaleの縮小率導出と対の式)。表題欄・改定欄・外枠は絶対座標のまま
        // (スケール対象外、用紙の右下固定配置・外周を保つ)。
        bool scaled = pageScale != 1.0;
        if (scaled)
            r.PushTransform(BorderMarginMm * (1 - pageScale), BorderMarginMm * (1 - pageScale), pageScale);

        DrawImages(r, sheet, rowStart, rowEnd);   // 背面固定：他の描画要素より先に描く
        if (_opt.ShowGrid) DrawGrid(r, columns, localRows);

        // 主回路（動力回路）モードでは左右母線・母線名・自動横配線を描かない（自由直線で結線する）。
        if (!sheet.MainCircuit)
        {
            DrawRails(r, columns, localRows - 1);
            DrawBusLabels(r, sheet, columns);
        }
        DrawRowNumbers(r, rowStart, rowEnd);
        if (!sheet.MainCircuit)
            DrawRungWires(r, sheet, columns, elemNet, netlist, report, powered, rowStart, rowEnd);
        DrawConnectors(r, sheet, rowStart, rowEnd);
        DrawFreeLines(r, sheet, rowStart, rowEnd, totalRows);
        DrawDots(r, sheet, rowStart, rowEnd);
        DrawFrames(r, sheet, rowStart, rowEnd, totalRows);
        foreach (var e in sheet.Elements)
            if (InWindow(e.Pos.Row)) DrawElement(r, e, energized, sim?.Inputs);
        DrawRungComments(r, sheet, columns, rowStart, rowEnd);

        if (scaled) r.PopTransform();

        // 表題欄・改定欄: 枠ありは A4 右下に固定配置、枠なしは従来どおり内容の下に置く。
        if (info is not null)
        {
            if (enableBorder)
                DrawTitleAndRevisionBottomRight(r, info, pageNumber, totalPages);
            else
            {
                double contentBottom = _opt.MarginMm + localRows * Cell + _opt.MarginMm;
                double revH = RevisionBlockH(info);
                if (revH > 0)
                    DrawRevisionBlock(r, info, LeftBusX, RightBusX(columns), contentBottom);
                DrawTitleBlock(r, info, LeftBusX, RightBusX(columns), contentBottom + revH,
                               pageNumber, totalPages);
            }
        }
        if (enableBorder)
            DrawBorder(r);
        _rowBase = 0;   // 後始末（インスタンス再利用に備える）
    }

    // 母線（左右の縦線）
    private void DrawRails(IRenderer r, int columns, int maxRow)
    {
        var s = _theme.Get(StrokeRole.BusRail);
        double yTop = _opt.MarginMm;
        double yBot = _opt.MarginMm + (maxRow + 1) * Cell;
        r.DrawLine(new(LeftBusX, yTop), new(LeftBusX, yBot), s);
        r.DrawLine(new(RightBusX(columns), yTop), new(RightBusX(columns), yBot), s);
    }

    // 母線名（各母線の上端）と母線間電圧（最上部に左右を結ぶ両矢印）
    private void DrawBusLabels(IRenderer r, Sheet sheet, int columns)
    {
        double lx = LeftBusX, rx = RightBusX(columns);
        // 第1ステップ（1行目の機器名ラベルは母線上端付近に出る）と離すため、
        // ヘッダーは上端寄りに置いて余白を確保する。
        double yLabel = Math.Max(2.5, _opt.MarginMm - 8.5);

        var nameStyle = _theme.Text(TextRole.DeviceName) with
        {
            FontSizeMm = 2.4, Bold = true, HAlign = HAlign.Center, VAlign = VAlign.Middle,
        };
        if (!string.IsNullOrEmpty(sheet.Bus.LeftName))
            r.DrawText(sheet.Bus.LeftName, new(lx, yLabel), nameStyle);
        if (!string.IsNullOrEmpty(sheet.Bus.RightName))
            r.DrawText(sheet.Bus.RightName, new(rx, yLabel), nameStyle);

        // 母線間電圧の両矢印（PowerLabel が設定されている場合のみ）
        string? voltage = sheet.Bus.PowerLabel;
        if (!string.IsNullOrWhiteSpace(voltage))
        {
            double gap = Cell * 0.9;             // 母線名を避ける左右の余白
            double x1 = lx + gap, x2 = rx - gap;
            if (x2 > x1)
            {
                DrawDoubleArrow(r, x1, x2, yLabel, _theme.Get(StrokeRole.Wire));
                var voltStyle = _theme.Text(TextRole.DeviceName) with
                {
                    FontSizeMm = 2.2, HAlign = HAlign.Center, VAlign = VAlign.Bottom,
                };
                r.DrawText(voltage, new((x1 + x2) / 2, yLabel - 0.8), voltStyle);
            }
        }
    }

    // 水平の両矢印（左右端に矢じり）。
    private static void DrawDoubleArrow(IRenderer r, double x1, double x2, double y, StrokeStyle s)
    {
        const double hx = 1.8, hy = 1.0;   // 矢じりの大きさ(mm)
        r.DrawLine(new(x1, y), new(x2, y), s);
        r.DrawLine(new(x1, y), new(x1 + hx, y - hy), s);   // 左矢じり
        r.DrawLine(new(x1, y), new(x1 + hx, y + hy), s);
        r.DrawLine(new(x2, y), new(x2 - hx, y - hy), s);   // 右矢じり
        r.DrawLine(new(x2, y), new(x2 - hx, y + hy), s);
    }

    // 行番号（左母線の左側に 1 始まりで表示）。[rowStart, rowEnd) の絶対行を、ページ内ローカル位置に描く。
    private void DrawRowNumbers(IRenderer r, int rowStart, int rowEnd)
    {
        if (!_opt.ShowRowNumbers) return;
        var style = _theme.Text(TextRole.LineNumber) with
        {
            FontSizeMm = 2.0,
            HAlign = HAlign.Right,
            VAlign = VAlign.Middle,
        };
        double x = LeftBusX - 1.0;   // 左母線のさらに左（右寄せで左へ伸ばす）
        for (int row = rowStart; row < rowEnd; row++)
            r.DrawText((row + 1).ToString(), new(x, YRow(row)), style);
    }

    // 各行の横配線（要素間・母線端）。接続検査時はネット色（青/黒）で描く。
    private void DrawRungWires(IRenderer r, Sheet sheet, int columns,
        Dictionary<Guid, (int A, int B)> elemNet, Netlist netlist, ConnectivityReport? report, HashSet<int>? powered,
        int rowStart, int rowEnd)
    {
        var byRow = new Dictionary<int, List<ElementInstance>>();
        foreach (var e in sheet.Elements)
        {
            if (!byRow.TryGetValue(e.Pos.Row, out var list)) { list = new(); byRow[e.Pos.Row] = list; }
            list.Add(e);
        }

        foreach (var (row, list) in byRow)
        {
            if (row < rowStart || row >= rowEnd) continue;   // ページ外の行はスキップ
            list.Sort((a, b) => a.Pos.Column.CompareTo(b.Pos.Column));
            double y = YRow(row);

            for (int k = 0; k < list.Count; k++)
            {
                var e = list[k];
                int lb = LeftBoundary(e), rb = RightBoundary(e);
                int? leftNet = elemNet.TryGetValue(e.Id, out var n1) ? n1.A : null;
                int? rightNet = elemNet.TryGetValue(e.Id, out var n2) ? n2.B : null;

                // 端子台（passthrough）は左右が同一ネット。隣接配線の線番は抑制して記号真上に1個描く。
                bool isTerminal = ElementCatalog.IsPassthrough(e.Kind);
                bool prevIsTerminal = k > 0 && ElementCatalog.IsPassthrough(list[k - 1].Kind);

                // 左側: 先頭要素は左母線へ（パディング分外側）、それ以外は前要素との隙間。
                // 母線延長区間に縦コネクタ(分岐点)があればそこで終端し、母線へは延ばさない。
                if (k == 0)
                {
                    double? lt = LeftTerminator(sheet, row, lb);
                    // 母線側(左)・要素側(右)のネットを渡す。分断が無ければ要素は母線へ union 済みで両者同一IDのため挙動不変。
                    if (lt is null)
                        DrawRungSegment(r, sheet, row, LeftBusX, X(lb), y, netlist.LeftRailNet, leftNet, netlist, report, powered, isTerminal);
                    else if (lt.Value < lb)
                        DrawRungSegment(r, sheet, row, X(lt.Value), X(lb), y, leftNet, leftNet, netlist, report, powered, isTerminal);
                    // lt == lb: 要素端が分岐点 → 母線へ延ばさない
                }
                else
                {
                    int prevRb = RightBoundary(list[k - 1]);
                    int? prevRightNet = elemNet.TryGetValue(list[k - 1].Id, out var pn) ? pn.B : null;
                    // 前要素が端子台の場合もこのセグメントは端子台の右隣接配線なので線番を抑制する。
                    DrawRungSegment(r, sheet, row, X(prevRb), X(lb), y, prevRightNet, leftNet, netlist, report, powered, isTerminal || prevIsTerminal);
                }
                // 末尾要素は右母線へ。延長区間に縦コネクタ(分岐点)があればそこで終端する。
                if (k == list.Count - 1)
                {
                    double? rt = RightTerminator(sheet, row, rb, columns);
                    if (rt is null)
                        DrawRungSegment(r, sheet, row, X(rb), RightBusX(columns), y, rightNet, netlist.RightRailNet, netlist, report, powered, isTerminal);
                    else if (rt.Value > rb)
                        DrawRungSegment(r, sheet, row, X(rb), X(rt.Value), y, rightNet, rightNet, netlist, report, powered, isTerminal);
                    // rt == rb: 要素端が分岐点 → 母線へ延ばさない
                }

                // 端子台: 記号の水平中心・上辺に線番を1個描く。
                if (isTerminal && _opt.ShowWireNumbers && leftNet is int tnid && netlist.Nets[tnid].WireNumber > 0)
                {
                    double cx = (X(lb) + X(rb)) / 2;
                    r.DrawText(netlist.Nets[tnid].WireNumber.ToString(),
                        new(cx, y - Cell * 0.55 + 3.0), _theme.Text(TextRole.LineNumber));
                }
            }
        }
    }

    // 先頭要素の左母線延長区間 (0, lb] にある分岐点(縦コネクタ端点)のうち最も内側(右寄り)の境界。
    // 横線はそこで終端し母線へ延ばさない。境界0(母線)は対象外。なければ null（母線まで延ばす）。
    // BottomRow側の行のみを対象にする: TopRow側(縦コネクタの起点行)は通常通り母線へつながる
    // べきで、母線接続を省略してよいのは縦コネクタ経由でのみ接続されるBottomRow側だけ
    // (以前はTopRow/BottomRowを区別せず両方を対象にしており、OR入力で基準行まで母線から
    // 浮いて見えるバグになっていた。T-026実機検証で発覚)。
    private static double? LeftTerminator(Sheet sheet, int row, int lb)
    {
        double? best = null;
        foreach (var c in sheet.Connectors)
            if (c.BottomRow == row && c.Column > 0 && c.Column <= lb)
                best = best is null ? c.Column : Math.Max(best.Value, c.Column);
        return best;
    }

    // 末尾要素の右母線延長区間 [rb, columns) にある分岐点のうち最も内側(左寄り)の境界。
    // なければ null（母線まで延ばす）。rb 自身にある場合は rb を返し、横線は描かれない。
    // LeftTerminatorと同様、BottomRow側の行のみを対象にする。
    private static double? RightTerminator(Sheet sheet, int row, int rb, int columns)
    {
        double? best = null;
        foreach (var c in sheet.Connectors)
            if (c.BottomRow == row && c.Column >= rb && c.Column < columns)
                best = best is null ? c.Column : Math.Min(best.Value, c.Column);
        return best;
    }

    // 縦コネクタ（分岐）＋接合点ドット
    private void DrawConnectors(IRenderer r, Sheet sheet, int rowStart, int rowEnd)
    {
        var wire = _theme.Get(StrokeRole.Wire);
        foreach (var c in sheet.Connectors)
        {
            // ページの行範囲にクリップ（ページをまたぐ縦コネクタは各ページで分断して描く）。
            int top = Math.Max(c.TopRow, rowStart);
            int bot = Math.Min(c.BottomRow, rowEnd - 1);
            if (top > bot) continue;
            double x = X(c.Column);
            r.DrawLine(new(x, YRow(top)), new(x, YRow(bot)), wire);
            // 接合点ドットは合流点（上端）のみ。元の上端がこのページ内にある場合だけ描く。
            if (c.TopRow >= rowStart && c.TopRow < rowEnd)
                r.FillCircle(new(x, YRow(c.TopRow)), Cell * 0.07, _theme.Foreground);
        }
    }

    // 設置場所グルーピング枠（点線矩形＋左上ラベル）
    // 作図ガイドの薄いグリッド。背面に描く。
    // 縦線=列境界（要素の左右ポート位置）、横線=行中心（行番号・要素中心・横向き記号の極と一致）。
    private void DrawGrid(IRenderer r, int columns, int rows)
    {
        var s = _theme.Get(StrokeRole.Grid);
        // 縦線は行中心の上下半セルまで張る（横線が行中心にあるため枠が閉じるように）。
        double yTop = _opt.MarginMm + 0.5 * Cell, yBot = _opt.MarginMm + (rows - 0.5) * Cell;
        for (int c = 0; c <= columns; c++)
            r.DrawLine(new(X(c), yTop), new(X(c), yBot), s);
        double xL = X(0), xR = X(columns);
        for (int rw = 0; rw < rows; rw++)
        {
            double y = _opt.MarginMm + (rw + 0.5) * Cell;   // 行中心（行番号と同じ位置）
            r.DrawLine(new(xL, y), new(xR, y), s);
        }
    }

    // 複数ページ分割時の Y オフセット（絶対 mm 座標をページ内ローカルへ変換）。
    private double PageY(double mmY) => mmY - _rowBase * Cell;

    // 自由直線（主回路の母線・結線・注記線）。mm 実座標をページ Y 補正して描く。
    // 複数ページ分割時のみ、当該ページの行ウィンドウへ縦クリップして余白・表題欄へのはみ出しを防ぐ
    // （要素・コネクタは InWindow で行クリップ済みだが、自由直線は mm 座標のため別途クリップが要る）。
    private void DrawFreeLines(IRenderer r, Sheet sheet, int rowStart, int rowEnd, int totalRows)
    {
        if (sheet.FreeLines.Count == 0) return;

        bool partialPage = rowStart > 0 || rowEnd < totalRows;
        if (partialPage)
        {
            double bandTop = _opt.MarginMm;
            double bandH = Math.Max(1, rowEnd - rowStart) * Cell;
            // 横方向は十分広く取り（横はみ出しは元々問題にならない）、縦のみページバンドに制限する。
            r.PushClip(new Rect2D(-1000, bandTop, 100000, bandH));
        }

        foreach (var fl in sheet.FreeLines)
        {
            var s = _theme.Get(StrokeRole.Wire) with { Style = fl.Style };
            r.DrawLine(new(fl.X1Mm, PageY(fl.Y1Mm)), new(fl.X2Mm, PageY(fl.Y2Mm)), s);
        }

        if (partialPage) r.PopClip();
    }

    // 手動接続点（●）。mm 実座標をページ Y 補正して描く。ページ行ウィンドウ外の点は描かない。
    private void DrawDots(IRenderer r, Sheet sheet, int rowStart, int rowEnd)
    {
        if (sheet.ConnectionDots.Count == 0) return;
        double bandTop = _opt.MarginMm + rowStart * Cell, bandBot = _opt.MarginMm + rowEnd * Cell;
        var col = _theme.Get(StrokeRole.Wire).Color;
        foreach (var d in sheet.ConnectionDots)
            if (d.YMm >= bandTop && d.YMm <= bandBot)
                r.FillCircle(new(d.XMm, PageY(d.YMm)), Cell * 0.10, col);
    }

    // 挿入画像。トレース用下絵は IncludeTracingImages=false（PDF出力）のとき描かない。
    // ページ分割時は画像の Y 範囲が当該ページの行ウィンドウと重なるものだけ、PageY でローカル座標へ補正して描く。
    private void DrawImages(IRenderer r, Sheet sheet, int rowStart, int rowEnd)
    {
        if (sheet.Images.Count == 0) return;
        double bandTop = _opt.MarginMm + rowStart * Cell, bandBot = _opt.MarginMm + rowEnd * Cell;
        foreach (var img in sheet.Images)
        {
            if (img.IsTracingOnly && !_opt.IncludeTracingImages) continue;
            if (img.YMm + img.HeightMm < bandTop || img.YMm > bandBot) continue;
            r.DrawImage(img.FilePath, new Rect2D(img.XMm, PageY(img.YMm), img.WidthMm, img.HeightMm));
        }
    }

    // 設置場所グルーピング枠。DrawFreeLines と同じくページ行ウィンドウへ縦クリップする
    // （枠が VisualYMm/VisualHeightMm でページ境界を跨ぐと、無クリップでは表題欄領域まで描画されてしまうため）。
    private void DrawFrames(IRenderer r, Sheet sheet, int rowStart, int rowEnd, int totalRows)
    {
        var baseStroke = _theme.Get(StrokeRole.GroupFrame);
        var labelStyle = _theme.Text(TextRole.DeviceName) with
        {
            FontSizeMm = 2.2,
            VAlign = VAlign.Bottom,
            HAlign = HAlign.Left,
        };
        double labelOffY = Cell * 0.25;   // ラベルを枠上辺より少し上に配置

        bool partialPage = rowStart > 0 || rowEnd < totalRows;
        if (partialPage)
        {
            double bandTop = _opt.MarginMm;
            double bandH = Math.Max(1, rowEnd - rowStart) * Cell;
            r.PushClip(new Rect2D(-1000, bandTop, 100000, bandH));
        }

        foreach (var f in sheet.Frames)
        {
            double x = f.VisualXMm ?? X(f.TopLeft.Column);
            // VisualYMm は絶対 mm なのでページ Y 補正。TopLeft.Row 由来は YRow が補正済み。
            double y = f.VisualYMm is double vy ? PageY(vy) : (YRow(f.TopLeft.Row) - Cell * 0.4);
            double w = f.VisualWidthMm ?? f.Width * Cell;
            double h = f.VisualHeightMm ?? f.Height * Cell;

            var stroke = baseStroke with { Style = f.BorderStyle ?? LineStyle.Dashed };
            r.DrawRectangle(new(x, y, w, h), stroke);
            if (!string.IsNullOrEmpty(f.Label))
                r.DrawText(f.Label, new(x + 1.0, y - labelOffY), labelStyle);
        }

        if (partialPage) r.PopClip();
    }

    // 横配線セグメント。区間内に配線分断(WireBreak)があれば、その分断点を含む「ノード間セグメント」
    // （両隣の連結点＝要素端子・縦コネクタ・セグメント端）を丸ごと削除して空けにする（マーク無し・提出品質）。
    // 分断が無い通常時は netLeft==netRight（隣接要素は母線/相互に union 済みで同一ネットID）のため挙動は従来と不変。
    private void DrawRungSegment(IRenderer r, Sheet sheet, int row, double xL, double xR, double y,
        int? netLeft, int? netRight, Netlist netlist, ConnectivityReport? report, HashSet<int>? powered,
        bool suppressWireNumber)
    {
        if (xR <= xL) return;

        List<double>? cuts = null;
        foreach (var b in sheet.WireBreaks)
        {
            if (b.Row != row) continue;
            double bx = X(b.Boundary);
            if (bx > xL + 0.001 && bx < xR - 0.001) (cuts ??= new()).Add(bx);
        }
        if (cuts is null)
        {
            DrawWire(r, xL, y, xR, y, netLeft, netlist, report, powered, suppressWireNumber);
            return;
        }

        // 連結点（節）= セグメント両端＋この行を通る縦コネクタ列。分断点を含む節間を削除レンジにする。
        var junctions = new List<double> { xL, xR };
        foreach (var c in sheet.Connectors)
            if (c.TopRow == row || c.BottomRow == row)
            {
                double jx = X(c.Column);
                if (jx > xL + 0.001 && jx < xR - 0.001) junctions.Add(jx);
            }
        junctions.Sort();

        var deleted = new List<(double A, double B)>();
        foreach (var cx in cuts)
        {
            double jL = xL, jR = xR;
            foreach (var j in junctions) { if (j <= cx) jL = j; else { jR = j; break; } }
            deleted.Add((jL, jR));
        }
        deleted.Sort((p, q) => p.A.CompareTo(q.A));

        // 削除レンジを除いた区間だけ描く。ネットは削除位置を境に左右へ割り当てる。
        double mid = (xL + xR) / 2;
        double cursor = xL;
        foreach (var (da, db) in deleted)
        {
            if (da > cursor)
            {
                int? segNet = (cursor + da) / 2 <= mid ? netLeft : netRight;
                DrawWire(r, cursor, y, da, y, segNet, netlist, report, powered, suppressWireNumber);
            }
            cursor = Math.Max(cursor, db);
        }
        if (cursor < xR)
        {
            int? segNet = (cursor + xR) / 2 <= mid ? netLeft : netRight;
            DrawWire(r, cursor, y, xR, y, segNet, netlist, report, powered, suppressWireNumber);
        }
    }

    private void DrawWire(IRenderer r, double x1, double y1, double x2, double y2,
        int? net, Netlist netlist, ConnectivityReport? report, HashSet<int>? powered,
        bool suppressWireNumber = false)
    {
        var stroke = _theme.Get(StrokeRole.Wire);
        if (powered is not null && net is int pid && powered.Contains(pid))
            stroke = stroke with { Color = DrawingTheme.Powered, Width = DrawingTheme.PoweredWireWidth };   // テスト: 通電
        else if (report is not null && net is int nid)
            stroke = stroke with { Color = report.Of(nid) == WireStatus.Connected ? DrawingTheme.Blue : _theme.Foreground };
        r.DrawLine(new(x1, y1), new(x2, y2), stroke);

        // 線番（母線ネットは WireNumber=0 で非表示。端子台隣接配線は呼び出し側で抑制）
        if (!suppressWireNumber && _opt.ShowWireNumbers && net is int id2 && netlist.Nets[id2].WireNumber > 0)
        {
            double mx = (x1 + x2) / 2, my = (y1 + y2) / 2;
            r.DrawText(netlist.Nets[id2].WireNumber.ToString(), new(mx, my - Cell * 0.12), _theme.Text(TextRole.LineNumber));
        }
    }

    private int LeftBoundary(ElementInstance e)
    {
        var ports = PartResolver.Ports(e, _lib);
        if (ports.Count == 0) return e.Pos.Column;
        int min = ports[0].BoundaryOffset;
        foreach (var p in ports) min = Math.Min(min, p.BoundaryOffset);
        return e.Pos.Column + min;
    }

    private int RightBoundary(ElementInstance e)
    {
        var ports = PartResolver.Ports(e, _lib);
        if (ports.Count == 0) return e.Pos.Column + e.CellWidth;
        int max = ports[0].BoundaryOffset;
        foreach (var p in ports) max = Math.Max(max, p.BoundaryOffset);
        return e.Pos.Column + max;
    }

    // ---- クロスリファレンス一覧表 ----

    private const double DevColW = 15.0;       // 機器名列幅 (mm)
    private const double CoilColW = 28.0;      // コイル列幅 (mm)
    private const double XrefCommentColW = 40.0;  // コメント列幅 (mm・右端に確保)
    // 接点列は残り幅を使う

    private double TableRowH => Cell * 0.65;
    private double TableGap => Cell * 0.8;

    private double CalcTableHeight(CrossReference xref)
    {
        int rows = xref.CoilEntries.Count() + 1;   // +1 = ヘッダ行
        return TableGap + rows * TableRowH + Cell * 0.4;
    }

    // 専用ページの表は用紙幅に合わせた固定幅（図面の列数に依存しない）。
    private double XrefTableRightX => PageW - _opt.MarginMm;

    /// <summary>
    /// クロスリファレンス一覧表を描画する。<paramref name="rowStart"/>/<paramref name="rowCount"/> で
    /// 描画するデータ行のウィンドウを指定する（専用ページの A4縦 複数ページ分割用）。
    /// ヘッダ行は各ページに付ける。
    /// </summary>
    private void DrawCrossRefTable(IRenderer r, double startY, CrossReference xref, int rowStart, int rowCount)
    {
        var entries = xref.CoilEntries.ToList();
        if (entries.Count == 0) return;
        int rowEnd = Math.Min(entries.Count, rowStart + rowCount);
        if (rowStart >= rowEnd) return;

        double rh = TableRowH;
        double y0 = startY + TableGap;
        double x0 = X(0);
        double x1 = x0 + DevColW;            // 機器 / コイル 境界
        double x2 = x1 + CoilColW;           // コイル / 接点 境界
        double x4 = XrefTableRightX;         // 表の右端（A4横 用紙幅基準）
        // コメント列を右端に確保。ただし接点列が 20mm 未満にならない範囲で。
        double commentW = Math.Min(XrefCommentColW, Math.Max(0, (x4 - x2) - 20.0));
        double x3 = x4 - commentW;           // 接点 / コメント 境界

        var outline = _theme.Get(StrokeRole.SymbolOutline) with { Width = DrawingTheme.TableLineWidth };
        var headerText = _theme.Text(TextRole.CrossRef) with { Bold = true, FontSizeMm = 2.4, VAlign = VAlign.Middle };
        var cellText = _theme.Text(TextRole.CrossRef) with { FontSizeMm = 2.2, VAlign = VAlign.Middle };
        double pad = 1.0;   // セル内左余白(mm)

        // ヘッダ行
        double yh = y0;
        DrawTableRow(r, outline, x0, x4, yh, rh, fill: true, x1, x2, x3);
        DrawCellText(r, "機器", x0, yh, rh, pad, headerText);
        DrawCellText(r, "コイル", x1, yh, rh, pad, headerText);
        DrawCellText(r, "接点", x2, yh, rh, pad, headerText);
        DrawCellText(r, "コメント", x3, yh, rh, pad, headerText);

        // データ行（ウィンドウ内のみ。各ページ先頭に詰めて描画する）
        for (int i = rowStart; i < rowEnd; i++)
        {
            double yi = y0 + (i - rowStart + 1) * rh;
            DrawTableRow(r, outline, x0, x4, yi, rh, fill: false, x1, x2, x3);
            var e = entries[i];
            DrawCellText(r, e.DeviceName, x0, yi, rh, pad, cellText);
            DrawCellText(r, FormatRefs(e.Coils), x1, yi, rh, pad, cellText);
            DrawCellText(r, FormatRefs(e.Contacts), x2, yi, rh, pad, cellText);
            DrawCellText(r, string.Join(" / ", e.Comments), x3, yi, rh, pad, cellText);
        }
    }

    // 外枠（x0..x4）＋任意の縦区切り線を描く。
    private void DrawTableRow(IRenderer r, StrokeStyle s,
        double x0, double x4, double y, double rh, bool fill, params double[] dividers)
    {
        if (fill) r.FillRectangle(new(x0, y, x4 - x0, rh), _theme.TableHeaderFill);
        r.DrawRectangle(new(x0, y, x4 - x0, rh), s);
        foreach (var xd in dividers)
            r.DrawLine(new(xd, y), new(xd, y + rh), s);
    }

    private static void DrawCellText(IRenderer r, string text, double cellX, double rowY, double rh,
        double pad, TextStyle style)
        => r.DrawText(text, new(cellX + pad, rowY + rh / 2), style);

    private static string FormatRefs(IEnumerable<CircuitRef> refs)
    {
        var list = refs.ToList();
        if (list.Count == 0) return "—";
        // 常に「ページ-行番号」形式（例: 1ページ1行目 → "1-1"）。
        return string.Join("  ", list.Select(c => $"{c.PageNumber}-{c.CircuitNumber}"));
    }

    // ---- クロスリファレンス専用ページ ----

    /// <summary>クロスリファレンス専用ページのサイズ。用紙サイズ縦固定（行が少なくても縦配置。長ければ複数ページに分割）。</summary>
    public Size2D CrossRefPageSize() => new(PageW, PageH);

    /// <summary>1ページに収まるクロスリファレンス表のデータ行数（ヘッダ行を除く）。</summary>
    public int CrossRefRowsPerPage()
    {
        // 利用可能高さ = 用紙の高さ - 上下余白 - 表の上ギャップ。これを行高で割り、ヘッダ1行を差し引く。
        double usableH = PageH - 2 * _opt.MarginMm - TableGap;
        int rowsIncludingHeader = (int)(usableH / TableRowH);
        return Math.Max(1, rowsIncludingHeader - 1);
    }

    /// <summary>クロスリファレンス専用ページの総ページ数。エントリ0なら0。</summary>
    public int CrossRefPageCount(CrossReference xref)
    {
        int n = xref.CoilEntries.Count();
        if (n == 0) return 0;
        int per = CrossRefRowsPerPage();
        return (n + per - 1) / per;
    }

    /// <summary>
    /// クロスリファレンス専用ページ（A4横）を 1 枚描画する。<paramref name="pageIndex"/> は 0 始まり。
    /// エントリ0なら何もしない。
    /// </summary>
    public void RenderCrossRefPage(IRenderer r, CrossReference xref, int pageIndex)
    {
        if (!xref.CoilEntries.Any()) return;
        int per = CrossRefRowsPerPage();
        DrawCrossRefTable(r, _opt.MarginMm, xref, pageIndex * per, per);
    }

    // ---- BOM（部品表）ページ ----

    private const double BomDevColW   = 20.0;
    private const double BomClassColW = 20.0;
    private const double BomModelColW = 45.0;
    private const double BomMakerColW = 45.0;

    /// <summary>BOM 専用ページのサイズ。<paramref name="columns"/> は最終シートの列数。</summary>
    public Size2D BomPageSize(int columns, int deviceCount)
    {
        double w = RightBusX(columns) + _opt.MarginMm;
        double h = _opt.MarginMm + Cell * 1.5
                 + (deviceCount + 1) * TableRowH
                 + Cell * 0.4 + _opt.MarginMm;
        return new Size2D(w, h);
    }

    /// <summary>BOM ページを描画する。機器が 0 件なら何もしない。</summary>
    public void RenderBomPage(IRenderer r, DeviceTable devices, int columns)
    {
        var entries = devices.ByName.Values
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (entries.Count == 0) return;

        double rh = TableRowH;
        double x0 = X(0);
        double x1 = x0 + BomDevColW;
        double x2 = x1 + BomClassColW;
        double x3 = x2 + BomModelColW;
        double x4 = x3 + BomMakerColW;
        double x5 = X(columns);
        double pad = 1.0;

        var titleStyle = _theme.Text(TextRole.CrossRef) with
        {
            Bold = true, FontSizeMm = 3.5, HAlign = HAlign.Center, VAlign = VAlign.Middle,
        };
        r.DrawText("部品表", new((x0 + x5) / 2, _opt.MarginMm + Cell * 0.75), titleStyle);

        double y0 = _opt.MarginMm + Cell * 1.5;
        var outline = _theme.Get(StrokeRole.SymbolOutline) with { Width = DrawingTheme.TableLineWidth };
        var headerText = _theme.Text(TextRole.CrossRef) with { Bold = true, FontSizeMm = 2.4, VAlign = VAlign.Middle };
        var cellText = _theme.Text(TextRole.CrossRef) with { FontSizeMm = 2.2, VAlign = VAlign.Middle };

        DrawBomRow(r, outline, x0, y0, x1, x2, x3, x4, x5, rh, fill: true);
        DrawCellText(r, "機器名",   x0, y0, rh, pad, headerText);
        DrawCellText(r, "種別",     x1, y0, rh, pad, headerText);
        DrawCellText(r, "型式",     x2, y0, rh, pad, headerText);
        DrawCellText(r, "メーカー", x3, y0, rh, pad, headerText);
        DrawCellText(r, "数量",     x4, y0, rh, pad, headerText);

        for (int i = 0; i < entries.Count; i++)
        {
            double yi = y0 + (i + 1) * rh;
            var d = entries[i];
            DrawBomRow(r, outline, x0, yi, x1, x2, x3, x4, x5, rh, fill: false);
            DrawCellText(r, d.Name,                    x0, yi, rh, pad, cellText);
            DrawCellText(r, DeviceClassLabel(d.Class), x1, yi, rh, pad, cellText);
            DrawCellText(r, d.Model ?? "—",            x2, yi, rh, pad, cellText);
            DrawCellText(r, d.Maker ?? "—",            x3, yi, rh, pad, cellText);
            DrawCellText(r, d.Quantity.ToString(),     x4, yi, rh, pad, cellText);
        }
    }

    private void DrawBomRow(IRenderer r, StrokeStyle s,
        double x0, double y, double x1, double x2, double x3, double x4, double x5, double rh, bool fill)
    {
        if (fill) r.FillRectangle(new(x0, y, x5 - x0, rh), _theme.TableHeaderFill);
        r.DrawRectangle(new(x0, y, x5 - x0, rh), s);
        r.DrawLine(new(x1, y), new(x1, y + rh), s);
        r.DrawLine(new(x2, y), new(x2, y + rh), s);
        r.DrawLine(new(x3, y), new(x3, y + rh), s);
        r.DrawLine(new(x4, y), new(x4, y + rh), s);
    }

    /// <summary>DeviceClass→日本語表示名。PDF出力(機器表BOM)と画面表示(機器表「種別」列、T-053)の
    /// 双方から参照する単一の文言定義(表記統一、殿裁定2026-07-10)。App層のDeviceClassToTextConverterが
    /// 参照するためpublic化(T-053、文言をApp層へコピーせず単一箇所で保守する方式を採用)。</summary>
    public static string DeviceClassLabel(DeviceClass c) => c switch
    {
        DeviceClass.Relay        => "リレー",
        DeviceClass.PushButton   => "押しボタン",
        DeviceClass.SelectSwitch => "切替SW",
        DeviceClass.Lamp         => "表示灯",
        DeviceClass.Timer        => "タイマ",
        DeviceClass.Counter      => "カウンタ",
        DeviceClass.Terminal     => "端子台",
        DeviceClass.Other        => "その他",
        _                        => c.ToString(),
    };

    // 要素記号（ローカル座標へ平行移動して描く）。energized で通電色、inputs で手動強制の青塗りを制御。
    private void DrawElement(IRenderer r, ElementInstance e, Dictionary<string, bool>? energized,
                             Dictionary<string, bool>? inputs = null)
    {
        int lb = LeftBoundary(e), rb = RightBoundary(e);
        double width = e.CellWidth * Cell;
        var part = _lib?.Get(e.PartId);
        // T-071バグ修正: e.Kindは自作パーツ配置時は常に既定値(ContactNO)のままのため、PartId経由で
        // 配置された組込みパーツ(BasicPartTemplates)の種別判定にはPartResolver.ComponentKind経由の
        // 解決が必須(853-855行のisLoad判定と同型パターン、隠密テスト設計 表2)。CreatesComponent=false
        // (Role=NonSimulated、Motor等)はComponentKindが例外を投げるため事前にガードする。
        ElementKind resolvedKind = part is not null && PartResolver.CreatesComponent(e, _lib)
            ? PartResolver.ComponentKind(e, _lib)
            : e.Kind;

        // T-061(殿裁定(3)=LDmicro式「通電=赤/非通電=グレー」): energizedが非nullならテストモード中。
        // デバイス名を持つ要素(=命令として評価される記号)のみ対象、無印の配線・枠等は対象外。
        bool testMode = energized is not null;
        bool on = testMode && e.DeviceName is not null
                  && energized!.TryGetValue(e.DeviceName, out var v) && v;
        var stroke = on ? _theme.Get(StrokeRole.SymbolOutline) with { Color = DrawingTheme.Powered }
                   : testMode && e.DeviceName is not null
                        ? _theme.Get(StrokeRole.SymbolOutline) with { Color = DrawingTheme.NonEnergizedGray }
                        : _theme.Get(StrokeRole.SymbolOutline);

        // 組込み ContactNO/NC: 縦棒間を半透明青で塗る（手動強制の明示）
        bool isContact = e.Kind is ElementKind.ContactNO or ElementKind.ContactNC;
        bool manuallyForced = isContact && e.DeviceName is not null && inputs is not null
                              && inputs.TryGetValue(e.DeviceName, out var mv) && mv;
        Color? contactFill = manuallyForced ? DrawingTheme.ManualForced : null;

        // 負荷（コイル・ランプ等）か判定。自作パーツはライブラリ経由で解決。
        bool isLoad = part is not null
            ? PartResolver.CreatesComponent(e, _lib) && ElementCatalog.IsLoad(PartResolver.ComponentKind(e, _lib))
            : ElementCatalog.IsLoad(e.Kind);

        // 1×1 セル背景塗り: 負荷が通電中、またはカスタムパーツが手動強制中
        Color? bgFill = (on && isLoad) ? DrawingTheme.ManualForced
                      : (part is not null && manuallyForced) ? DrawingTheme.ManualForced
                      : null;

        r.PushTransform(X(lb), YRow(e.Pos.Row));
        string? orient = e.Params.GetValueOrDefault(ParamKeys.Orient);
        if (bgFill is Color bg)
            r.FillRectangle(new(0, -Cell / 2, Cell, Cell), bg);
        if (part is not null) PartDrawing.Draw(r, _theme, part, Cell, stroke);
        else SymbolGlyphs.Draw(r, stroke, e.Kind, width, Cell, contactFill,
                               e.Params.GetValueOrDefault(ParamKeys.Type), orient);
        r.PopTransform();

        // 主回路ブレーカは Params["Type"]（NFB/MCCB/ELB、既定 NFB）を記号脇に小さく記す。
        // 縦向きは記号右・中央、横向きは記号上に置く（横向きは縦に背が高いため）。
        if (e.Kind == ElementKind.Breaker3P)
        {
            var typ = e.Params.TryGetValue(ParamKeys.Type, out var t) && !string.IsNullOrEmpty(t) ? t : "NFB";
            bool horiz = orient == "H";
            var ts = _theme.Text(TextRole.DeviceName) with
            {
                FontSizeMm = 2.2,
                HAlign = horiz ? HAlign.Center : HAlign.Left,
                VAlign = horiz ? VAlign.Bottom : VAlign.Middle,
            };
            var at = horiz
                ? new Point2D(X(lb) + width / 2, YRow(e.Pos.Row) - Cell * 1.05)
                : new Point2D(X(rb) + 1.0, YRow(e.Pos.Row));
            r.DrawText(typ, at, ts);
        }

        // 表示灯の中央にランプ色（色記号）を記入
        if (resolvedKind == ElementKind.Lamp &&
            e.Params.TryGetValue(ParamKeys.LampColor, out var lampColor) && !string.IsNullOrEmpty(lampColor))
        {
            var cs = _theme.Text(TextRole.DeviceName) with
            {
                FontSizeMm = 2.4, HAlign = HAlign.Center, VAlign = VAlign.Middle,
            };
            r.DrawText(lampColor, new(X(lb) + width / 2, YRow(e.Pos.Row)), cs);
        }

        DrawElementLabel(r, e, lb, rb, width, resolvedKind);
    }

    /// <summary>配置プレビュー用に1要素を指定色（半透明可）で描く。Render の後に呼ぶこと
    /// （_lib・_rowBase=0 などの描画状態を再利用する）。グリッド座標 e.Pos に従う。
    /// libraryを渡すと、Renderを経ていない単発呼び出し(T-015: 部品選択リストのサムネイル生成)
    /// でも自作パーツを正しく解決できるよう、呼び出し中だけ_libを一時差し替える。DiagramRenderer
    /// はUIスレッド単一使用が前提(インスタンスは共有せず呼び出し元ごとに使う想定)であり、
    /// この一時差し替えはスレッドセーフではない(将来並行描画を導入する際は要再検討)。</summary>
    public void DrawPreview(IRenderer r, ElementInstance e, Color color, PartLibrary? library = null)
    {
        var savedLib = _lib;
        if (library is not null) _lib = library;
        try
        {
            int lb = LeftBoundary(e);
            double width = e.CellWidth * Cell;
            var stroke = _theme.Get(StrokeRole.SymbolOutline) with { Color = color };
            var part = _lib?.Get(e.PartId);
            r.PushTransform(X(lb), YRow(e.Pos.Row));
            if (part is not null) PartDrawing.Draw(r, _theme, part, Cell, stroke);
            else SymbolGlyphs.Draw(r, stroke, e.Kind, width, Cell, null,
                                   e.Params.GetValueOrDefault(ParamKeys.Type), e.Params.GetValueOrDefault(ParamKeys.Orient));
            r.PopTransform();
        }
        finally
        {
            _lib = savedLib;
        }
    }

    // 機器名ラベルを記号の上・中央に描く。
    // Params["LabelDy"] (mm, 正で上へ) で要素ごとに高さオフセットを調整できる（密集時の重なり回避）。
    // コメントは図面には描かない（PDF の機器欄に記載する）。
    private void DrawElementLabel(IRenderer r, ElementInstance e, int lb, int rb, double width, ElementKind resolvedKind)
    {
        if (!_opt.ShowDeviceNames) return;
        if (string.IsNullOrEmpty(e.DeviceName)) return;

        double cx = X(lb) + width / 2;
        // 個別の LabelDy があればそれ、無ければ種別の既定オフセット。
        double dy = e.Params.TryGetValue(ParamKeys.LabelDy, out var s) &&
            double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v)
            ? v : ElementCatalog.DefaultLabelDy(e.Kind);

        double yn = YRow(e.Pos.Row) - Cell * 0.50 - dy;   // dy>0 で上へ（機器名は記号の上）
        r.DrawText(e.DeviceName!, new(cx, yn), _theme.Text(TextRole.DeviceName));

        // タイマ接点は機器名の右肩に種別ミニラベル（限時=「限」/ 瞬時=「瞬」）を出して区別する。
        // 瞬時接点は素の接点と同形のため、記号だけでは判別しにくいのを補う。
        if (TimerContactMark(resolvedKind) is string mark)
            r.DrawText(mark, new(X(rb) + 0.3, yn),
                _theme.Text(TextRole.DeviceName) with { FontSizeMm = 1.7, HAlign = HAlign.Left });
    }

    // タイマ接点の種別ミニラベル。タイマ以外の接点・要素は null（ラベル無し）。
    private static string? TimerContactMark(ElementKind k) => k switch
    {
        ElementKind.TimerContactNO or ElementKind.TimerContactNC => "限",
        ElementKind.TimerInstantContactNO or ElementKind.TimerInstantContactNC => "瞬",
        _ => null,
    };

    // 右母線の右側コメント（ページの行範囲のみ）
    private void DrawRungComments(IRenderer r, Sheet sheet, int columns, int rowStart, int rowEnd)
    {
        if (sheet.RungComments.Count == 0) return;
        double x = RightBusX(columns) + RungCommentXOffsetMm;
        var style = _theme.Text(TextRole.DeviceName) with { HAlign = HAlign.Left, FontSizeMm = RungCommentFontSizeMm };
        foreach (var rc in sheet.RungComments)
            if (!string.IsNullOrEmpty(rc.Text) && rc.Row >= rowStart && rc.Row < rowEnd)
                r.DrawText(rc.Text, new(x, YRow(rc.Row)), style);
    }

    private const double TitleBlockW = 95.0;   // 右下表題欄の幅 (mm)

    // 表題欄（＋その上に改定欄）を用紙縦ページの右下に固定配置する（枠あり出力時）。
    // 絶対座標で描くため _rowBase（ページ行オフセット）の影響は受けない。
    private void DrawTitleAndRevisionBottomRight(IRenderer r, DocumentInfo info, int pageNumber, int totalPages)
    {
        const double pageMargin = 5.0;   // 外枠線と同じ用紙端余白
        double x0 = PageW - pageMargin - TitleBlockW;
        double x1 = PageW - pageMargin;
        double titleStartY = PageH - pageMargin - TitleBlockH;
        DrawTitleBlock(r, info, x0, x1, titleStartY, pageNumber, totalPages);
        double revH = RevisionBlockH(info);
        if (revH > 0)
            DrawRevisionBlock(r, info, x0, x1, titleStartY - revH);
    }

    // 表題欄（タイトルブロック）を startY 位置に描画する。
    // レイアウト: 3行×複数列のグリッド枠。行0=社名（全幅）、行1=図面名称+図番+ページ、行2=顧客/設計/製図/確認/日付。
    private void DrawTitleBlock(IRenderer r, DocumentInfo info, double x0, double x1, double startY,
                                int pageNumber = 1, int totalPages = 1)
    {
        double totalW = x1 - x0;
        double h0 = TitleCompanyRowH;            // 0行目（社名）高さ
        double h1 = TitleDetailRowsH * 0.45;      // 1行目高さ
        double h2 = TitleDetailRowsH - h1;        // 2行目高さ
        var outline = _theme.Get(StrokeRole.SymbolOutline) with { Width = DrawingTheme.TableLineWidth };
        var labelStyle = _theme.Text(TextRole.CrossRef) with { FontSizeMm = 1.8, VAlign = VAlign.Middle, Bold = true };
        var dataStyle  = _theme.Text(TextRole.CrossRef) with { FontSizeMm = 2.2, VAlign = VAlign.Middle };
        var companyStyle = _theme.Text(TextRole.CrossRef) with
        {
            FontSizeMm = 3.0, Bold = true, HAlign = HAlign.Center, VAlign = VAlign.Middle,
        };
        double pad = 1.0;

        // ---- 行0: 社名（全幅） ----
        double y0 = startY;
        r.DrawRectangle(new(x0, y0, totalW, h0), outline);
        if (!string.IsNullOrEmpty(info.CompanyName))
            r.DrawText(info.CompanyName, new(x0 + totalW / 2, y0 + h0 / 2), companyStyle);

        // ---- 行1: 図面名称 (50%) | 図番 (30%) | ページ (20%) ----
        double titleW = totalW * 0.50, drawNoW = totalW * 0.30, pageW = totalW * 0.20;
        double y1 = startY + h0;
        DrawTitleCell(r, outline, x0, y1, titleW, h1, "図面名称", info.Title, labelStyle, dataStyle, pad);
        DrawTitleCell(r, outline, x0 + titleW, y1, drawNoW, h1, "図番", info.DrawingNo, labelStyle, dataStyle, pad);
        DrawTitleCell(r, outline, x0 + titleW + drawNoW, y1, pageW, h1, "ページ",
                      $"{pageNumber} / {totalPages}", labelStyle, dataStyle, pad);

        // ---- 行2: 顧客 | 設計 | 製図 | 確認 | 日付 ----
        double[] ratios = { 0.28, 0.18, 0.18, 0.18, 0.18 };
        string[] labels = { "顧客", "設計", "製図", "確認", "日付" };
        string[] values = { info.Customer, info.Designer, info.Drafter, info.Checker, info.Date ?? "" };
        double y2 = startY + h0 + h1;
        double cx = x0;
        for (int i = 0; i < labels.Length; i++)
        {
            double cw = totalW * ratios[i];
            DrawTitleCell(r, outline, cx, y2, cw, h2, labels[i], values[i], labelStyle, dataStyle, pad);
            cx += cw;
        }
    }

    private static void DrawTitleCell(IRenderer r, StrokeStyle s,
        double x, double y, double w, double h,
        string label, string value, TextStyle labelStyle, TextStyle dataStyle, double pad)
    {
        r.DrawRectangle(new(x, y, w, h), s);
        // ラベル（左上小文字）
        r.DrawText(label, new(x + pad, y + h * 0.22), labelStyle with { FontSizeMm = 1.7, VAlign = VAlign.Middle });
        // 値（中央やや下）
        r.DrawText(value, new(x + pad, y + h * 0.65), dataStyle);
    }

    // 用紙縦の図面枠（外枠線）を描画する。用紙端からの余白は BorderMarginMm
    // (CalcPageScale の縮小フィット判定と共有、T-080往復1周目指摘A)。
    private void DrawBorder(IRenderer r)
    {
        var thick = _theme.Get(StrokeRole.BusRail) with { Width = 0.5 };
        r.DrawRectangle(new(BorderMarginMm, BorderMarginMm,
            PageW - BorderMarginMm * 2, PageH - BorderMarginMm * 2), thick);
    }

    // 改定欄（表題欄の上）を描画する。最新エントリが上に来るよう逆順表示。
    private void DrawRevisionBlock(IRenderer r, DocumentInfo info, double x0, double x1, double startY)
    {
        double totalW = x1 - x0;
        var s = _theme.Get(StrokeRole.SymbolOutline) with { Width = DrawingTheme.TableLineWidth };
        var hdrStyle  = _theme.Text(TextRole.CrossRef) with { FontSizeMm = 1.8, VAlign = VAlign.Middle, Bold = true };
        var dataStyle = _theme.Text(TextRole.CrossRef) with { FontSizeMm = 2.0, VAlign = VAlign.Middle };
        double pad = 1.0;

        double[] ratios  = { 0.12, 0.18, 0.55, 0.15 };
        string[] headers = { "Rev", "日付", "内容", "担当" };

        // ヘッダ行
        double y = startY;
        double cx = x0;
        for (int i = 0; i < headers.Length; i++)
        {
            double cw = totalW * ratios[i];
            r.DrawRectangle(new(cx, y, cw, RevHdrH), s);
            r.DrawText(headers[i], new(cx + pad, y + RevHdrH / 2), hdrStyle);
            cx += cw;
        }

        // データ行（最新エントリが上）
        y += RevHdrH;
        foreach (var rev in Enumerable.Reverse(info.Revisions))
        {
            string[] vals = { rev.Rev, rev.Date, rev.Description, rev.By };
            cx = x0;
            for (int i = 0; i < vals.Length; i++)
            {
                double cw = totalW * ratios[i];
                r.DrawRectangle(new(cx, y, cw, RevRowH), s);
                r.DrawText(vals[i], new(cx + pad, y + RevRowH / 2), dataStyle);
                cx += cw;
            }
            y += RevRowH;
        }
    }
}
