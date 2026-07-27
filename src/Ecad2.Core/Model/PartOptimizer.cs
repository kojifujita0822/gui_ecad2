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

    /// <summary>
    /// 検証A（重複）: 完全に同じ格子位置（行・境界とも一致）にある接続点が2つ以上あるか。
    /// <see cref="ClampPortsToFrame"/> は範囲外の接続点を枠内へ寄せるため、複数の接続点が同一座標へ
    /// 収束しうる（T-126＝P-129。忍者が実機で再現）。重複した接続点は同一ノードに載って区別が付かず、
    /// データとしても意味を持たぬため、保存時に拒否する。
    /// 「同じ場所か」の判定は <see cref="PartShapeGeometry.IndexOfPortAt"/>（接続点の追加時に
    /// 重複を防ぐのに使っているもの）と同一の定義を用いる——編集中に置けぬものが保存時に通っては
    /// 辻褄が合わぬため、定義を1箇所に集約する。
    /// </summary>
    public static bool HasDuplicatePorts(IReadOnlyList<PortDef> ports)
    {
        for (int i = 0; i < ports.Count; i++)
        {
            // 自分より前に同じ座標の接続点があれば、i 番目は重複。
            if (PartShapeGeometry.IndexOfPortAt(ports, ports[i].RowOffset, ports[i].BoundaryOffset) != i)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 検証B（左右の縮退）: 接続点2つ以上が、すべて同じ境界オフセット（＝同じ左右位置）に載っているか。
    ///
    /// <para>検証Aだけでは実害を防げぬためこちらが要る（T-126、侍の実測で確認）。
    /// <c>NetlistBuilder.BuildElementConnections</c> は最左・最右の接続点を
    /// <c>BoundaryOffset</c> の厳密不等号比較のみで選ぶため、全ての接続点の境界が同値だと
    /// <c>pl</c>・<c>pr</c> がともに先頭の1点へ落ちる。すると要素の左右が同一ノードへ縮退し、
    /// <c>AddHorizontalWireUnions</c> が左母線・隣接要素をその1点へまとめて繋ぐ——
    /// 断線ではなく「本来分かれるべき左右のネットが1つに統合される」誤結線（短絡）になる。
    /// 行が違えば完全同一座標ではないので検証Aをすり抜けるが、実害は重複と全く同じである。</para>
    ///
    /// <para>「2点の境界が一致」ではなく「<em>すべて</em>の境界が同値」で判ずるのが肝。
    /// 3点以上のうち一部だけが同じ境界に載るのは正常な形状であり（最左・最右は正しく分かれ、
    /// 残りは中間ポートとして扱われる）、これを弾くと過検出になる。</para>
    ///
    /// <para>接続点が1つ以下なら左右の縮退という概念自体が成り立たぬため false を返す。
    /// この場合は既存の「接続点2つ以上」検査（<c>PartEditorDialog.OkButton_Click</c>）が働き、
    /// すり抜けるのは <see cref="PartRole.NonSimulated"/> のみ——電気的に寄与せぬ役割ゆえ
    /// 縮退の実害も生じない（家老裁定2026-07-27）。</para>
    /// </summary>
    public static bool AllPortsOnSameBoundary(IReadOnlyList<PortDef> ports)
    {
        if (ports.Count < 2) return false;

        int boundary = ports[0].BoundaryOffset;
        for (int i = 1; i < ports.Count; i++)
            if (ports[i].BoundaryOffset != boundary) return false;
        return true;
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
