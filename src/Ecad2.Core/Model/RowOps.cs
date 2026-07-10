namespace Ecad2.Model;

/// <summary>
/// 任意位置への行挿入・削除に伴う、行番号を持つ要素(広義5種)の一括シフト処理(T-055増分3)。
/// 規則の出典: docs/ecad2-t055-increment3-precheck-onmitsu.md §2.2/§2.3
/// （GuiEcad実装 <c>RowOps.ShiftRows</c>・<c>InsertRowCommand</c>・<c>DeleteRowCommand</c> の方式踏襲、家老裁定2026-07-10）。
/// </summary>
public static class RowOps
{
    /// <summary>
    /// targetRowの前に1行挿入し、挿入点以降の4種(ElementInstance/VerticalConnector/WireBreak/RungComment)の
    /// Rowを+1シフトする。GroupFrameは、開始行が挿入点以降なら位置のみ+1シフト（Height不変）、
    /// 開始行が挿入点より前だが範囲が挿入点にかかるならHeight++（内部挿入、位置不変）。
    /// </summary>
    public static void InsertRow(Sheet sheet, int targetRow)
    {
        foreach (var e in sheet.Elements)
            if (e.Pos.Row >= targetRow) e.Pos = e.Pos with { Row = e.Pos.Row + 1 };
        foreach (var c in sheet.Connectors)
        {
            if (c.TopRow >= targetRow) c.TopRow += 1;
            if (c.BottomRow >= targetRow) c.BottomRow += 1;
        }
        foreach (var w in sheet.WireBreaks)
            if (w.Row >= targetRow) w.Row += 1;
        foreach (var rc in sheet.RungComments)
            if (rc.Row >= targetRow) rc.Row += 1;
        foreach (var f in sheet.Frames)
        {
            if (f.TopLeft.Row >= targetRow) f.TopLeft = f.TopLeft with { Row = f.TopLeft.Row + 1 };
            else if (f.TopLeft.Row + f.Height > targetRow) f.Height++;
        }
    }

    /// <summary>
    /// targetRow行を削除し、削除行より後ろの4種(ElementInstance/VerticalConnector/WireBreak/RungComment)の
    /// Rowを-1シフトする。GroupFrameは、開始行が削除行より後ろなら位置のみ-1シフトする。
    /// 【契約】targetRow行には要素(広義5種)が存在しないこと（呼び出し元で
    /// <c>MainWindowViewModel.IsRowOccupied(sheet, targetRow) == false</c> を確認済みであること）を前提とする。
    /// targetRow行に要素が存在する場合の扱い（拒否か要素ごと削除か）は未確定（殿確認待ち、2026-07-10）のため、
    /// 本メソッドの対象外（GroupFrameの「枠ごと削除」「内部削除によるHeight短縮」は未実装）。
    /// </summary>
    public static void DeleteRow(Sheet sheet, int targetRow)
    {
        foreach (var e in sheet.Elements)
            if (e.Pos.Row > targetRow) e.Pos = e.Pos with { Row = e.Pos.Row - 1 };
        foreach (var c in sheet.Connectors)
        {
            if (c.TopRow > targetRow) c.TopRow -= 1;
            if (c.BottomRow > targetRow) c.BottomRow -= 1;
        }
        foreach (var w in sheet.WireBreaks)
            if (w.Row > targetRow) w.Row -= 1;
        foreach (var rc in sheet.RungComments)
            if (rc.Row > targetRow) rc.Row -= 1;
        foreach (var f in sheet.Frames)
            if (f.TopLeft.Row > targetRow) f.TopLeft = f.TopLeft with { Row = f.TopLeft.Row - 1 };
    }
}
