namespace Ecad2.Rendering;

/// <summary>
/// グリッド座標（行・列境界）↔ mm ワールド座標の相互変換。描画とヒットテストで共有する。
/// 列境界 b の x = Margin + b*Cell、行 r の中心線 y = Margin + (r+0.5)*Cell。
/// </summary>
public readonly struct GridGeometry
{
    /// <summary>ラダー図面の既定セル寸法（mm）。<see cref="DiagramRenderer"/> の既定と揃える。
    /// App 層は図面描画でこの値を上書きしないため、mm 絶対座標（<c>GroupFrame.Visual*Mm</c> 等）と
    /// セル数の換算にはこの定数を用いてよい。</summary>
    public const double DefaultCellMm = 9.0;

    public double CellMm { get; init; }
    public double MarginMm { get; init; }

    public GridGeometry(double cellMm = DefaultCellMm, double marginMm = 15.0)
    {
        CellMm = cellMm;
        MarginMm = marginMm;
    }

    public double X(int boundary) => MarginMm + boundary * CellMm;
    public double X(double boundary) => MarginMm + boundary * CellMm;   // 0.5 刻み境界（縦コネクタ）用
    public double YRow(int row) => MarginMm + (row + 0.5) * CellMm;

    /// <summary>GroupFrame 上辺が行中心線より上に置かれる比率（セル数）。
    /// <c>DiagramRenderer.FrameRectMm</c>・<c>LadderCanvas.FrameRectMm</c> の <c>YRow(row) - Cell*0.4</c> と同値。</summary>
    public const double FrameTopAboveRowCenterCellRatio = 0.4;

    /// <summary>グリッド行 <paramref name="row"/> の枠上辺 Y(mm)。<c>YRow(row) - CellMm*0.4</c> と同値。</summary>
    public double FrameTopMm(int row) => YRow(row) - CellMm * FrameTopAboveRowCenterCellRatio;

    /// <summary>枠上辺の Y(mm) に最も近いグリッド行。<see cref="FrameTopMm"/> の逆算。</summary>
    public int FrameRowAt(double yTopMm) => (int)Math.Round((yTopMm - MarginMm) / CellMm - (0.5 - FrameTopAboveRowCenterCellRatio));

    /// <summary>x(mm) が属するセル列。</summary>
    public int ColAt(double xMm) => (int)Math.Floor((xMm - MarginMm) / CellMm);

    /// <summary>y(mm) が属するセル行。</summary>
    public int RowAt(double yMm) => (int)Math.Floor((yMm - MarginMm) / CellMm);

    /// <summary>x(mm) に最も近い列境界（縦コネクタの配置スナップ等に使う）。</summary>
    public int BoundaryAt(double xMm) => (int)Math.Round((xMm - MarginMm) / CellMm);

    /// <summary>x(mm) に最も近い 0.5 セル刻みの列境界（縦コネクタはセル中央にも置ける）。</summary>
    public double BoundaryAtHalf(double xMm) => Math.Round((xMm - MarginMm) / CellMm * 2) / 2.0;
}
