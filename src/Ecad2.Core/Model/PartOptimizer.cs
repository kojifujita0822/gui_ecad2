namespace Ecad2.Model;

/// <summary>PartDefinition のプリミティブ最適化ユーティリティ。</summary>
public static class PartOptimizer
{
    /// <summary>
    /// 端点が接続し同一直線上にある <see cref="PartLine"/> を1本にマージする。
    /// 元のプリミティブの順序を維持する（描画順への影響を最小化）。
    /// 保存時・読み込み時の最適化として使用する。
    /// </summary>
    public static List<PartPrimitive> MergeCollinearLines(IEnumerable<PartPrimitive> prims)
    {
        const double Eps = 1e-5;
        var list = prims.ToList();

        bool anyMerged = true;
        while (anyMerged)
        {
            anyMerged = false;
            // PartLine が存在するインデックスのみを対象に操作（順序維持）
            for (int ii = 0; ii < list.Count && !anyMerged; ii++)
            {
                if (list[ii] is not PartLine a) continue;
                for (int jj = ii + 1; jj < list.Count; jj++)
                {
                    if (list[jj] is not PartLine b) continue;
                    if (TryMerge(a, b, Eps, out var merged))
                    {
                        list[ii] = merged;
                        list.RemoveAt(jj);
                        anyMerged = true;
                        break;
                    }
                }
            }
        }

        return list;
    }

    /// <summary>
    /// 接続点を基準枠（W×Hセル）の範囲内へ正規化する。範囲外の接続点をそのまま永続化すると、
    /// 配置先での実ノード座標（<c>NetlistBuilder</c>）が意図せぬ位置になり誤結線を招くため、
    /// 保存時にのみ適用する（T-068増分3-c、殿裁定2026-07-25＝UI/UX仮決定12点目）。
    /// 編集中はGuiEcad原本と同じくクランプしない——原本の <c>OnSizeChanged</c>・<c>OnSave</c> は
    /// いずれもポートに触れず、枠外へ出た接続点はそのまま保持される。ゆえに本処理は
    /// <see cref="MergeCollinearLines"/> と同じく「保存直前のみ適用・編集中の実体は不変」の流儀とし、
    /// 新しいリストを返す（呼び出し元のリストは変更しない）。
    /// </summary>
    public static List<PortDef> ClampPortsToFrame(IEnumerable<PortDef> ports, int widthCells, int heightCells)
    {
        var list = new List<PortDef>();
        foreach (var p in ports)
        {
            var (row, boundary) = PartShapeGeometry.ClampPort(p.BoundaryOffset, p.RowOffset, widthCells, heightCells);
            list.Add(row == p.RowOffset && boundary == p.BoundaryOffset
                ? p
                : p with { RowOffset = row, BoundaryOffset = boundary });
        }
        return list;
    }

    private static bool TryMerge(PartLine a, PartLine b, double eps, out PartLine result)
    {
        result = default!;
        double adx = a.X2 - a.X1, ady = a.Y2 - a.Y1;
        double bdx = b.X2 - b.X1, bdy = b.Y2 - b.Y1;
        if (Math.Abs(adx * bdy - ady * bdx) > eps) return false;

        (double ax, double ay, double bx, double by) conn = default;
        bool found = false;
        if      (Near(a.X2, a.Y2, b.X1, b.Y1, eps)) { conn = (a.X1, a.Y1, b.X2, b.Y2); found = true; }
        else if (Near(a.X2, a.Y2, b.X2, b.Y2, eps)) { conn = (a.X1, a.Y1, b.X1, b.Y1); found = true; }
        else if (Near(a.X1, a.Y1, b.X1, b.Y1, eps)) { conn = (a.X2, a.Y2, b.X2, b.Y2); found = true; }
        else if (Near(a.X1, a.Y1, b.X2, b.Y2, eps)) { conn = (a.X2, a.Y2, b.X1, b.Y1); found = true; }
        if (!found) return false;

        result = new PartLine(conn.ax, conn.ay, conn.bx, conn.by);
        return true;
    }

    private static bool Near(double x1, double y1, double x2, double y2, double eps)
        => Math.Abs(x1 - x2) < eps && Math.Abs(y1 - y2) < eps;
}
