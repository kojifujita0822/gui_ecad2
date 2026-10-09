using Ecad2.Rendering;

namespace Ecad2.Model;

/// <summary>セルの矩形範囲（両端を含む）。範囲選択とコピー・貼り付けで使う（殿ご下命2026-10-09）。</summary>
public readonly record struct CellRange(int Top, int Left, int Bottom, int Right)
{
    public int Rows => Bottom - Top + 1;
    public int Columns => Right - Left + 1;

    /// <summary>2つの角から範囲を作る（どちらが左上でもよい）。</summary>
    public static CellRange FromCorners(GridPos a, GridPos b) =>
        new(Math.Min(a.Row, b.Row), Math.Min(a.Column, b.Column), Math.Max(a.Row, b.Row), Math.Max(a.Column, b.Column));

    /// <summary>左上の角を <paramref name="topLeft"/> に置いた、同じ大きさの範囲。</summary>
    public CellRange MovedTo(GridPos topLeft) =>
        new(topLeft.Row, topLeft.Column, topLeft.Row + Rows - 1, topLeft.Column + Columns - 1);

    /// <summary>要素の占有セルがすべて範囲に収まるか。占有は列 <c>[Column, Column+CellWidth-1]</c>・
    /// 行 <c>[Row-RowSpan, Row+RowSpan]</c>（高さを持つ要素は中心基準）。</summary>
    public bool Contains(ElementInstance e) =>
        Top <= e.Pos.Row - e.RowSpan && e.Pos.Row + e.RowSpan <= Bottom
        && Left <= e.Pos.Column && e.Pos.Column + Math.Max(1, e.CellWidth) - 1 <= Right;

    /// <summary>要素の占有セルが範囲と一つでも重なるか。</summary>
    public bool Overlaps(ElementInstance e) =>
        e.OverlapsRows(Top, Bottom)
        && e.Pos.Column <= Right && Left <= e.Pos.Column + Math.Max(1, e.CellWidth) - 1;

    /// <summary>列境界が範囲の左端〜右端の境界に載るか。境界 b は列 b-1 と列 b の間ゆえ、
    /// 範囲の右端の境界は <c>Right + 1</c> である。</summary>
    public bool ContainsBoundary(double boundary) => Left <= boundary && boundary <= Right + 1;
}

/// <summary>
/// 範囲からコピーした中身（殿ご下命2026-10-09）。要素・縦コネクタ・配線分断・グループ枠を持つ
/// ——行コメントは写さぬ（殿ご裁可）。自由線・接続点・画像はグリッドに載らぬ mm 座標ゆえ対象外。
/// <para>
/// <b>【元の図面から切り離してある】</b>すべて複製であり、元の実体への参照は持たぬ。Undo は図面を
/// 丸ごと入れ替えるゆえ、参照を持てば古い図面の実体を指し続ける。貼るたびに <see cref="RangePaste"/> が
/// もう一度複製する（同じ <see cref="ElementInstance.Id"/> が二つ生まれぬように）。
/// </para>
/// <para>
/// <b>【座標は元の位置のまま持つ】</b><see cref="Origin"/>（範囲の左上）と併せて持ち、貼る時に
/// 差分だけずらす。自由式の枠が持つ mm 座標も同じ差分で動かせる。
/// </para>
/// <para>
/// <b>【機器名は空】</b>貼った先で同じ名の機器が勝手に増えぬよう、コピーの時点で落とす（殿ご裁可）。
/// </para>
/// </summary>
public sealed class RangeClipboard
{
    public GridPos Origin { get; private init; }
    public int Rows { get; private init; }
    public int Columns { get; private init; }
    public IReadOnlyList<ElementInstance> Elements { get; private init; } = Array.Empty<ElementInstance>();
    public IReadOnlyList<VerticalConnector> Connectors { get; private init; } = Array.Empty<VerticalConnector>();
    public IReadOnlyList<WireBreak> WireBreaks { get; private init; } = Array.Empty<WireBreak>();
    public IReadOnlyList<GroupFrame> Frames { get; private init; } = Array.Empty<GroupFrame>();

    public int ItemCount => Elements.Count + Connectors.Count + WireBreaks.Count + Frames.Count;
    public bool IsEmpty => ItemCount == 0;

    /// <summary>
    /// 範囲に<b>丸ごと収まる</b>ものを写し取る。はみ出すもの（範囲を跨ぐ縦コネクタ、一部だけ入った枠、
    /// 占有セルの一部が外に出る要素）は写さぬ——切り詰めて写せば、元と違う形のものが貼られる。
    /// </summary>
    public static RangeClipboard Copy(Sheet sheet, CellRange range) => new()
    {
        Origin = new GridPos(range.Top, range.Left),
        Rows = range.Rows,
        Columns = range.Columns,
        Elements = sheet.Elements.Where(range.Contains).Select(e => RangePaste.Clone(e, 0, 0)).ToList(),
        Connectors = sheet.Connectors
            .Where(c => range.Top <= c.TopRow && c.BottomRow <= range.Bottom && range.ContainsBoundary(c.Column))
            .Select(c => RangePaste.Clone(c, 0, 0)).ToList(),
        WireBreaks = sheet.WireBreaks
            .Where(w => range.Top <= w.Row && w.Row <= range.Bottom && range.ContainsBoundary(w.Boundary))
            .Select(w => RangePaste.Clone(w, 0, 0)).ToList(),
        Frames = sheet.Frames
            .Where(f => range.Top <= f.TopLeft.Row && f.TopLeft.Row + f.Height - 1 <= range.Bottom
                     && range.Left <= f.TopLeft.Column && f.TopLeft.Column + f.Width - 1 <= range.Right)
            .Select(f => RangePaste.Clone(f, 0, 0)).ToList(),
    };
}

/// <summary>貼り付けの可否。</summary>
public enum RangePasteStatus
{
    Ok,
    /// <summary>貼り付け先のセルがグリッドの外。</summary>
    AnchorOutOfGrid,
    /// <summary>右へはみ出す（行は挿入で増やせるが、列は増やせぬ）。</summary>
    ColumnsOverflow,
    /// <summary>行を増やすと上限（<see cref="GridSpec.MaxRows"/>）を超える。</summary>
    RowLimitExceeded,
    /// <summary>複数行を占める部品の途中へは割り込めぬ。</summary>
    BlockedByTallElement,
    /// <summary>このシートの種別には置けぬものを含む。</summary>
    NotAllowedOnSheet,
}

/// <summary>貼り付けの段取り。</summary>
/// <param name="RowsToInsert">貼り付け先が塞がっておるため、貼り付け行の前へ挿入する行数。</param>
/// <param name="RowsToAppend">貼り付け先は空いておるが下へはみ出すため、末尾へ足す行数。</param>
public sealed record RangePastePlan(RangePasteStatus Status, int RowsToInsert = 0, int RowsToAppend = 0)
{
    public bool CanPaste => Status == RangePasteStatus.Ok;
}

/// <summary>貼り付け位置へずらした中身（複製）。確定前のゴースト表示と、確定時の追加の双方に使う。</summary>
public sealed record RangePasteContent(
    IReadOnlyList<ElementInstance> Elements,
    IReadOnlyList<VerticalConnector> Connectors,
    IReadOnlyList<WireBreak> WireBreaks,
    IReadOnlyList<GroupFrame> Frames);

/// <summary>確定前の貼り付けの見た目（ゴースト）に要る一式。</summary>
/// <param name="Content">貼り付け位置へずらした中身。</param>
/// <param name="Rect">貼り付け先の範囲。</param>
/// <param name="Plan">その位置へ貼れるか、行を挿入するか。</param>
/// <param name="Library">中身の自作パーツを描くためのライブラリ（別の図面からコピーした定義を含む）。</param>
public sealed record RangePastePreview(RangePasteContent Content, CellRange Rect, RangePastePlan Plan, PartLibrary? Library);

/// <summary>
/// コピーした中身の貼り付け（殿ご下命2026-10-09）。
/// <para>
/// <b>【塞がっておれば行を挿入して割り込む】</b>（殿ご裁可）貼り付け先の矩形に既存の要素・縦コネクタ・
/// 配線分断が一つでも掛かっておれば、コピーした行数ぶんを貼り付け行の前へ挿入し、空いた所へ貼る。
/// 空いておれば挿入せず、下へはみ出すぶんだけ末尾へ行を足す。
/// </para>
/// <para>
/// <b>【枠は塞がりに数えぬ】</b>グループ枠は元より他のものとの重なりを許すゆえ。
/// </para>
/// </summary>
public static class RangePaste
{
    /// <summary>貼り付けの段取りを立てる（図面は書き換えぬ）。<paramref name="anchor"/> は貼り付け先の左上。</summary>
    public static RangePastePlan Plan(Sheet dest, RangeClipboard clip, GridPos anchor, PartLibrary? lib)
    {
        var grid = dest.Grid;
        if (anchor.Row < 0 || anchor.Row >= grid.Rows || anchor.Column < 0 || anchor.Column >= grid.Columns)
            return new(RangePasteStatus.AnchorOutOfGrid);
        if (anchor.Column + clip.Columns > grid.Columns)
            return new(RangePasteStatus.ColumnsOverflow);

        // 縦コネクタと配線分断は制御回路シートのもの。要素はパーツごとのシート種別の枷に従う。
        if (dest.MainCircuit && (clip.Connectors.Count > 0 || clip.WireBreaks.Count > 0))
            return new(RangePasteStatus.NotAllowedOnSheet);
        if (clip.Elements.Any(e => !PartResolver.IsAllowedOnSheet(PartResolver.SheetAffinityOf(e, lib), dest.MainCircuit)))
            return new(RangePasteStatus.NotAllowedOnSheet);

        var rect = new CellRange(0, 0, clip.Rows - 1, clip.Columns - 1).MovedTo(anchor);
        int rowsToInsert = 0, rowsToAppend = 0;
        if (IsOccupied(dest, rect))
        {
            // RowOps.InsertRow は要素を基準行だけで送る。基準行が貼り付け行より上に在って下へ張り出す
            // 部品は動かず、挿入した行へ居座る——そこへ貼れば重なる。
            bool straddles = dest.Elements.Any(e => e.Pos.Row < anchor.Row && rect.Overlaps(e));
            if (straddles) return new(RangePasteStatus.BlockedByTallElement);
            rowsToInsert = clip.Rows;
        }
        else
        {
            rowsToAppend = Math.Max(0, rect.Bottom + 1 - grid.Rows);
        }

        if (grid.Rows + rowsToInsert + rowsToAppend > GridSpec.MaxRows)
            return new(RangePasteStatus.RowLimitExceeded);
        return new(RangePasteStatus.Ok, rowsToInsert, rowsToAppend);
    }

    /// <summary>貼り付け位置へずらした複製を作る。呼ぶたびに新しい実体を返す。</summary>
    public static RangePasteContent Materialize(RangeClipboard clip, GridPos anchor)
    {
        int dRow = anchor.Row - clip.Origin.Row, dCol = anchor.Column - clip.Origin.Column;
        return new(
            clip.Elements.Select(e => Clone(e, dRow, dCol)).ToList(),
            clip.Connectors.Select(c => Clone(c, dRow, dCol)).ToList(),
            clip.WireBreaks.Select(w => Clone(w, dRow, dCol)).ToList(),
            clip.Frames.Select(f => Clone(f, dRow, dCol)).ToList());
    }

    /// <summary>段取りどおりに貼る。<paramref name="plan"/> が貼れぬ段取りなら何もせず null を返す。
    /// Undo・変更ありの印・通知には触れぬ（呼び手の責）。</summary>
    public static RangePasteContent? Apply(Sheet dest, RangeClipboard clip, GridPos anchor, RangePastePlan plan)
    {
        if (!plan.CanPaste) return null;

        for (int i = 0; i < plan.RowsToInsert; i++) RowOps.InsertRow(dest, anchor.Row);
        dest.Grid.Rows += plan.RowsToInsert + plan.RowsToAppend;

        var content = Materialize(clip, anchor);
        dest.Elements.AddRange(content.Elements);
        dest.Connectors.AddRange(content.Connectors);
        dest.WireBreaks.AddRange(content.WireBreaks);
        dest.Frames.AddRange(content.Frames);
        return content;
    }

    private static bool IsOccupied(Sheet sheet, CellRange rect) =>
        sheet.Elements.Any(rect.Overlaps)
        || sheet.Connectors.Any(c => c.TopRow <= rect.Bottom && rect.Top <= c.BottomRow && rect.ContainsBoundary(c.Column))
        || sheet.WireBreaks.Any(w => rect.Top <= w.Row && w.Row <= rect.Bottom && rect.ContainsBoundary(w.Boundary));

    internal static ElementInstance Clone(ElementInstance e, int dRow, int dCol)
    {
        var clone = e.DeepClone();
        clone.Pos = new GridPos(e.Pos.Row + dRow, e.Pos.Column + dCol);
        clone.DeviceName = null;
        return clone;
    }

    internal static VerticalConnector Clone(VerticalConnector c, int dRow, int dCol) =>
        new() { Column = c.Column + dCol, TopRow = c.TopRow + dRow, BottomRow = c.BottomRow + dRow };

    internal static WireBreak Clone(WireBreak w, int dRow, int dCol) =>
        new() { Boundary = w.Boundary + dCol, Row = w.Row + dRow };

    /// <summary>自由式の枠（<c>Visual*Mm</c> 設定済み）は mm 座標も同じ差分で動かす。グリッドの
    /// 位置だけずらせば、絵が元の場所に残る。</summary>
    internal static GroupFrame Clone(GroupFrame f, int dRow, int dCol, double cellMm = GridGeometry.DefaultCellMm) => new()
    {
        Label = f.Label,
        TopLeft = new GridPos(f.TopLeft.Row + dRow, f.TopLeft.Column + dCol),
        Width = f.Width,
        Height = f.Height,
        BorderStyle = f.BorderStyle,
        VisualXMm = f.VisualXMm + dCol * cellMm,
        VisualYMm = f.VisualYMm + dRow * cellMm,
        VisualWidthMm = f.VisualWidthMm,
        VisualHeightMm = f.VisualHeightMm,
    };
}
