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
    /// targetRow行を削除する。targetRow行にある要素(広義5種)は「要素ごと削除」する
    /// （GuiEcad同型、殿裁定2026-07-11。設計出典: docs/ecad2-t055-increment3-delete-occupied-design-onmitsu.md §1/§2）。
    /// 実行順序【重要、変更禁止】: (1)4種(ElementInstance/VerticalConnector/WireBreak/RungComment)の
    /// うちtargetRow該当分を先に削除 (2)GroupFrameの削除/Height--判定（まだシフトしていない元の座標系で判定）
    /// (3)4種の残存要素をtargetRowより後ろのみ-1シフト (4)GroupFrameの位置シフト（同じく元の座標系での
    /// 判定→シフト実行）。この順序を崩すと、GroupFrame判定がシフト後の座標を見てしまい誤判定する。
    /// VerticalConnectorは端点(TopRow/BottomRowいずれか)がtargetRowと一致する場合のみ削除する。範囲が
    /// targetRowを跨ぐだけで端点が一致しない場合は削除されず、該当する端点のみ-1シフトして残る。
    /// GroupFrameは開始行(TopLeft.Row)がtargetRowと一致すれば枠ごと削除、開始行がtargetRowより前で
    /// 終端行(TopLeft.Row+Height-1)がtargetRow以降なら内部詰め(Height--)。
    /// </summary>
    /// <returns>削除されたElementInstanceの一覧（呼び出し元での機器表クリーンアップ用）。</returns>
    public static IReadOnlyList<ElementInstance> DeleteRow(Sheet sheet, int targetRow)
    {
        var removedElements = sheet.Elements.Where(e => e.Pos.Row == targetRow).ToList();
        foreach (var e in removedElements) sheet.Elements.Remove(e);

        var removedConnectors = sheet.Connectors
            .Where(c => c.TopRow == targetRow || c.BottomRow == targetRow).ToList();
        foreach (var c in removedConnectors) sheet.Connectors.Remove(c);

        var removedBreaks = sheet.WireBreaks.Where(w => w.Row == targetRow).ToList();
        foreach (var w in removedBreaks) sheet.WireBreaks.Remove(w);

        var removedComments = sheet.RungComments.Where(rc => rc.Row == targetRow).ToList();
        foreach (var rc in removedComments) sheet.RungComments.Remove(rc);

        foreach (var f in sheet.Frames.ToList())
        {
            if (f.TopLeft.Row == targetRow) sheet.Frames.Remove(f);
            else if (f.TopLeft.Row < targetRow && f.TopLeft.Row + f.Height - 1 >= targetRow) f.Height--;
        }

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

        return removedElements;
    }
}
