namespace Ecad2.Model;

/// <summary>
/// <see cref="PartPrimitive"/> の幾何演算（自作パーツの形状編集キャンバスを下支えする）。
/// T-068増分3-b1（家老裁可6・2026-07-25）: 増分0のPoC（<c>poc/t068-part-editor-poc/</c>）では
/// これらがキャンバス（<c>FrameworkElement</c>派生）の private メソッドとして埋め込まれており
/// 単体テスト不能だった。UI から独立した純粋関数として切り出し、テストで守れるようにする。
/// 置き場所は同性質の <see cref="PartOptimizer"/>（PartPrimitive を操作するユーティリティ）に倣う。
///
/// 座標はすべてパーツローカル座標（セル単位、原点=最左ポート点・行中心線=y0、+x右/+y下）。
/// <c>Ecad2.Rendering</c> の <c>Point2D</c> は用いず double で受け渡す——Model 名前空間から
/// Rendering 名前空間への依存を持ち込まないため（既存の Model 配下は Rendering を参照していない）。
/// ツール種別の分岐・選択状態・Undo/Redo といった UI 状態は本クラスの責務外（App 層に置く）。
/// </summary>
public static class PartShapeGeometry
{
    /// <summary>既定のスナップ刻み（<b>1辺を4等分</b>＝1セル内に 4×4＝16点）。
    /// <para>
    /// T-138（殿裁定2026-07-31）で <c>1/16</c> から改めた。<c>1/16</c> は増分0のPoC由来にて、
    /// <b>原本GuiEcad の <c>SnapStep = 1.0/16</c> と完全に同型</b>であった（隠密の一次ソース調査）。
    /// すなわち本件は移植の誤りを正すものではなく、<b>殿が原本から離れる道をお選びになった仕様変更</b>である。
    /// </para>
    /// <para>
    /// 訴えは「マウスで狙った位置になかなか置けぬ」——1辺16等分では1セル内に 16×16＝256点あり、
    /// 手ぶれに対して点の間隔が狭すぎた。<b>4等分にすると点は16点となり、1辺あたり4倍・面積あたり16倍粗くなる。</b>
    /// </para></summary>
    public const double DefaultSnapFractionCells = 1.0 / 4.0;

    /// <summary>ヒット判定の既定許容距離（セル単位）。GuiEcad原本の点-線分距離しきい値と同値。</summary>
    public const double DefaultHitToleranceCells = 0.3;

    /// <summary>回転スナップの既定角度。GuiEcad原本と同値。</summary>
    public const double DefaultRotateSnapDeg = 15.0;

    /// <summary>縮退（面積・長さゼロ）とみなす許容誤差。</summary>
    private const double DegenerateEps = 1e-6;

    // ===== 接続点の描画寸法（T-136(B)増分3、殿裁定2026-07-31＝選択表現をリングへ） =====

    /// <summary>接続点（塗り円）の半径（セル単位）。従来からの値を保つ。</summary>
    public const double PortFillRadiusCells = 0.14;

    /// <summary>選択リングの半径（セル単位）。<b>塗り円より一回り大きく取る</b>——
    /// リングが塗りを覆えば、増分4で入る「種類色」が隠れてしまう（隠密の調査書 案A）。</summary>
    public const double PortRingRadiusCells = 0.20;

    /// <summary>選択リングの線幅（mm）。既存のストローク幅（0.3／0.5mm）と同じく<b>mm固定</b>にて、
    /// ズーム倍率に依らず一定に見える（T-137の殿裁定5「ズームに依らず一定」と同じ考え方）。</summary>
    public const double PortRingStrokeMm = 0.15;

    /// <summary>接続点の描画半径（塗り円と選択リング、いずれもmm）を求める。
    /// <para>
    /// <b>選択を色ではなく「形」で表す</b>——殿裁定2026-07-31（T-136(B)）。従来は塗り色そのものを
    /// 選択で切り替えており（選択＝OrangeRed／非選択＝DodgerBlue）、<b>一つの色軸が「選択」と
    /// 「種類」の二つを兼ねられぬ</b>のが変更の理由にござる。
    /// </para>
    /// <para>
    /// <b>【この関数の要】リング半径は塗り半径より必ず大きい。</b> 逆転すれば種類色が隠れる——
    /// 大小関係そのものをテストで固定する（値の一致だけを見ると、両方を同じ率にする改変を見逃す）。
    /// </para>
    /// <para>
    /// <b>原本GuiEcadは選択を色で表しており（青＝選択／赤＝非選択、<c>PartEditorWindow.xaml.cs</c>の
    /// <c>DrawPort</c>）、常時描かれる白い輪郭は縁取りであって選択表現ではない。</b>
    /// すなわちリングで選択を表すのはecad2独自の形であり、<b>殿が原本からの逸脱を承知のうえで
    /// 裁可なされた</b>（2026-08-01）。
    /// </para></summary>
    public static (double FillRadiusMm, double RingRadiusMm) PortVisualRadiiMm(double cellMm)
        => (cellMm * PortFillRadiusCells, cellMm * PortRingRadiusCells);

    // ===== スナップ =====

    /// <summary>座標値を刻み幅の倍数へ丸める。</summary>
    public static double Snap(double value, double fractionCells = DefaultSnapFractionCells)
        => Math.Round(value / fractionCells) * fractionCells;

    /// <summary>角度を刻み角の倍数へ丸める。</summary>
    public static double SnapAngleDeg(double deg, double snapDeg = DefaultRotateSnapDeg)
        => Math.Round(deg / snapDeg) * snapDeg;

    // ===== 縮退判定 =====

    /// <summary>ドラッグ量がゼロのまま確定された等、実体を持たないプリミティブか判定する
    /// （true のものは追加せず捨てる）。判定対象を持たない種別は false を返す。</summary>
    public static bool IsDegenerate(PartPrimitive p) => p switch
    {
        PartLine l => Math.Abs(l.X1 - l.X2) < DegenerateEps && Math.Abs(l.Y1 - l.Y2) < DegenerateEps,
        PartRect r => r.W < DegenerateEps || r.H < DegenerateEps,
        PartCircle c => c.R < DegenerateEps,
        PartArc a => a.R < DegenerateEps,
        _ => false,
    };

    // ===== 形状構築（2点のドラッグ入力から形を組み立てる） =====

    /// <summary>対角2点から矩形を作る（どちらの向きにドラッグしても正の幅・高さになるよう正規化する）。</summary>
    public static PartRect BuildRect(double x1, double y1, double x2, double y2)
        => new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));

    /// <summary>中心と円周上の1点から円を作る。</summary>
    public static PartCircle BuildCircle(double centerX, double centerY, double edgeX, double edgeY)
        => new(centerX, centerY, Distance(centerX, centerY, edgeX, edgeY));

    /// <summary>外接矩形の対角2点から半楕円弧を作る（殿裁定=案A、SweepDeg=180固定）。
    /// 半径は縮退を避けるため下限 0.05 セルでクランプする。</summary>
    public static PartArc BuildArc(double x1, double y1, double x2, double y2)
        => new(
            Cx: (x1 + x2) / 2,
            Cy: (y1 + y2) / 2,
            R: Math.Max(0.05, Math.Abs(x2 - x1) / 2),
            StartDeg: 180,
            SweepDeg: 180,
            Ry: Math.Max(0.05, Math.Abs(y2 - y1) / 2),
            Rot: 0);

    // ===== 基準枠（T-133増分1、殿裁定6=基準点は中央） =====

    /// <summary>
    /// 基準枠の矩形（余白を含まない mm）。<b>行は中心基準・列は境界基準</b>で返す。
    /// <para>
    /// T-133増分1（殿裁定6）以前は左上原点 <c>(0,0)</c> から下へ描いていたため、
    /// 接続点の <see cref="PortDef.RowOffset"/>（中心行を0とする上下、<see cref="ClampPort"/> 参照）と
    /// 基準が食い違っていた。本メソッドは行だけを中心基準へ移して両者を揃える。
    /// </para>
    /// <para>
    /// <b>列（X）を 0 のままにするのは意図的である。</b> 境界オフセットは
    /// <see cref="ClampPort"/> が <c>0〜幅</c> で扱う左端基準ゆえ、枠の左辺も 0 でなければ揃わぬ。
    /// 「中央基準」は行についてのみ言う。
    /// </para>
    /// </summary>
    public static (double X, double Y, double Width, double Height) FrameRect(
        int widthCells, int heightCells, double cellMm)
    {
        // T-139（殿裁定2026-07-31）: 行方向の半径は ±h/2 セル＝高さ h セルちょうど。
        // <b>原本GuiEcad と同じ形へ戻したものである</b>（PartEditorWindow.xaml.cs:265 の
        // DrawRectangle(Sx(0), Sy(-_h/2), _w*_cellPx, _h*_cellPx) と、:196 の Sy(cy) が同型）。
        //
        // 【P-148 は本裁定で覆った】P-148（2026-07-28）は ±((h-1)+0.5) を採り、枠・接続点の
        // 可動範囲・図面の占有範囲の3者を揃えていた。だが枠が 2h-1 セルへ広がるため
        // 「高さ2の設定で枠が3セル」という名と実体の食い違いが生じ、殿の
        // 「設定値と見た目の差異が大きい」というご指摘を招いた。
        // <b>本裁定では「枠は目安であって柵ではない」を採り、接続点が枠外へ出ることを許す</b>
        // （h≧3 の奇数で ClampPort の可動範囲が枠を越える。それが原本の姿である）。
        //
        // Math.Max(1, ...) は高さ0以下の退化入力を高さ1へ潰す（原本の ApplyDefinition が
        // _h = Math.Max(1, def.HeightCells) でクランプするのと同じ扱い）。
        double w = widthCells * cellMm;
        double halfSpanMm = Math.Max(1, heightCells) / 2.0 * cellMm;
        return (0.0, -halfSpanMm, w, halfSpanMm * 2);
    }

    // ===== パーツエディタの作図グリッド（T-137新設、T-139で原本の形へ組み直し） =====

    /// <summary>
    /// 指定の刻みで、範囲に入る線の位置を昇順で返す（セル単位。原点 <c>0</c> を基準とする）。
    /// <para>
    /// T-139（殿裁定2026-07-31）で<b>原本GuiEcadの作図グリッドへ揃えた</b>
    /// （<c>PartEditorWindow.xaml.cs:244-266</c> の <c>DrawGrid</c>）。原本は次の3種を重ねて描く——
    /// <list type="number">
    /// <item><b>0.25刻みの薄線</b>（縦・横とも。1セルを4×4に割る）</item>
    /// <item><b>整数境界の縦線</b>（やや濃く。要素の左右ポート位置）</item>
    /// <item><b>行中心線 <c>y=0</c></b>（最も濃く。配線が通る基準線）</item>
    /// </list>
    /// 本メソッドは 1・2 の位置を返す（刻みを引数で切り替える）。3 は単線ゆえ呼び出し側で直に引く。
    /// </para>
    /// <para>
    /// <b>T-137で用いた「行の境目＝半整数」の線は本裁定で取り除いた</b>——原本に無い線であり、
    /// かつ枠が <c>h</c> セルへ戻ると（<see cref="FrameRect"/>）<b>h が偶数のとき枠の辺が整数位置に来て
    /// 半整数の線と一致せぬ</b>ため、端が合わなくなる。
    /// </para>
    /// <param name="stepCells">刻み（セル単位）。0以下は退化入力として空を返す。</param>
    /// </summary>
    public static double[] GridLinesAt(double minCell, double maxCell, double stepCells)
    {
        if (stepCells <= 0) return [];
        int first = (int)Math.Ceiling(minCell / stepCells);
        int last = (int)Math.Floor(maxCell / stepCells);
        // 線が1本も入らぬ狭い範囲・範囲の逆転（max<min）とも、ここで last<first になり捕まる
        // （逆転を別途弾く要は無い——実測で確かめた）。外すと配列長が負になり例外。
        if (last < first) return [];
        var xs = new double[last - first + 1];
        for (int i = 0; i < xs.Length; i++) xs[i] = (first + i) * stepCells;
        return xs;
    }

    // ===== 接続点（T-068増分3-c、家老裁可2026-07-25） =====

    /// <summary>接続点の位置をセル格子（整数）へ丸め、基準枠の範囲へ収める。
    /// 境界オフセットは 0〜幅、行オフセットは中心行を0として上下 (高さ-1) まで（GuiEcad原本と同じ範囲）。
    /// 高さ1のパーツでは行オフセットは0のみを取る。</summary>
    public static (int RowOffset, int BoundaryOffset) ClampPort(double cellX, double cellY, int widthCells, int heightCells)
    {
        int boundary = Math.Clamp((int)Math.Round(cellX), 0, Math.Max(0, widthCells));
        int rowLimit = Math.Max(0, heightCells - 1);
        int row = Math.Clamp((int)Math.Round(cellY), -rowLimit, rowLimit);
        return (row, boundary);
    }

    /// <summary>指定の格子位置にある接続点の添字（無ければ -1）。同じ場所への重複追加を防ぐのに使う。</summary>
    public static int IndexOfPortAt(IReadOnlyList<PortDef> ports, int rowOffset, int boundaryOffset)
    {
        for (int i = 0; i < ports.Count; i++)
            if (ports[i].RowOffset == rowOffset && ports[i].BoundaryOffset == boundaryOffset) return i;
        return -1;
    }

    /// <summary>点に当たる接続点の添字（無ければ -1）。後から置いたものを手前とみなす。
    /// 座標の対応に注意——接続点の x は <see cref="PortDef.BoundaryOffset"/>、y は
    /// <see cref="PortDef.RowOffset"/> であり、<see cref="PortDef"/> の宣言順とは逆になる。</summary>
    public static int HitTestPort(IReadOnlyList<PortDef> ports, double x, double y,
        double toleranceCells = DefaultHitToleranceCells)
    {
        for (int i = ports.Count - 1; i >= 0; i--)
        {
            double dx = ports[i].BoundaryOffset - x, dy = ports[i].RowOffset - y;
            if (Math.Sqrt(dx * dx + dy * dy) <= toleranceCells) return i;
        }
        return -1;
    }

    // ===== 距離・ヒットテスト =====

    /// <summary>点からプリミティブの輪郭までの距離（セル単位）。矩形・弧は回転を考慮する。</summary>
    public static double DistanceToPrimitive(PartPrimitive p, double x, double y) => p switch
    {
        PartLine l => DistancePointToSegment(x, y, l.X1, l.Y1, l.X2, l.Y2),
        PartCircle c => Math.Abs(Distance(x, y, c.Cx, c.Cy) - c.R),
        PartArc a => DistanceToArc(a, x, y),
        PartRect r => DistanceToRect(r, x, y),
        PartPolyline pl => DistanceToPolyline(pl, x, y),
        PartText t => Distance(x, y, t.X, t.Y),
        _ => double.MaxValue,
    };

    /// <summary>点に最も手前（リスト末尾側）で当たるプリミティブの添字を返す。当たらなければ -1。
    /// 描画は先頭から順に重ねるため、末尾から走査して最前面を優先する。</summary>
    public static int HitTest(IReadOnlyList<PartPrimitive> primitives, double x, double y,
        double toleranceCells = DefaultHitToleranceCells)
    {
        for (int i = primitives.Count - 1; i >= 0; i--)
            if (DistanceToPrimitive(primitives[i], x, y) <= toleranceCells) return i;
        return -1;
    }

    // ===== 平行移動 =====

    /// <summary>プリミティブ全体を平行移動した新しいインスタンスを返す（record ゆえ元は不変）。</summary>
    public static PartPrimitive Translate(PartPrimitive p, double dx, double dy) => p switch
    {
        PartLine l => l with { X1 = l.X1 + dx, Y1 = l.Y1 + dy, X2 = l.X2 + dx, Y2 = l.Y2 + dy },
        PartCircle c => c with { Cx = c.Cx + dx, Cy = c.Cy + dy },
        PartArc a => a with { Cx = a.Cx + dx, Cy = a.Cy + dy },
        PartRect r => r with { X = r.X + dx, Y = r.Y + dy },
        PartPolyline pl => pl with { Points = TranslatePoints(pl.Points, dx, dy) },
        PartText t => t with { X = t.X + dx, Y = t.Y + dy },
        _ => p,
    };

    // ===== 回転 =====

    /// <summary>プリミティブを中心 (centerX, centerY) まわりに deg 度回転する。
    /// GuiEcad原本どおり型ごとに実装が異なる（殿裁定=案A）——線・折れ線は座標を直接回転して焼き込み、
    /// 矩形・弧は <c>Rot</c> フィールドへ加算するのみ（座標自体は変えない）。円は回転しても
    /// 見た目が変わらないため無変化、文字の回転は <see cref="PartText"/> が角度を持たないため非対応。</summary>
    public static PartPrimitive Rotate(PartPrimitive p, double centerX, double centerY, double deg) => p switch
    {
        PartLine l => RotateLine(l, centerX, centerY, deg),
        PartPolyline pl => RotatePolyline(pl, centerX, centerY, deg),
        PartRect r => r with { Rot = r.Rot + deg },
        PartArc a => a with { Rot = a.Rot + deg },
        _ => p,
    };

    /// <summary>点 (x, y) を中心 (centerX, centerY) まわりに deg 度回転する。</summary>
    public static (double X, double Y) RotatePoint(double x, double y, double centerX, double centerY, double deg)
    {
        double rad = deg * Math.PI / 180.0;
        double dx = x - centerX, dy = y - centerY;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);
        return (centerX + dx * cos - dy * sin, centerY + dx * sin + dy * cos);
    }

    /// <summary>回転の中心として用いるプリミティブの代表点。折れ線は全頂点の相加平均。</summary>
    public static (double X, double Y) CenterOf(PartPrimitive p) => p switch
    {
        PartLine l => ((l.X1 + l.X2) / 2, (l.Y1 + l.Y2) / 2),
        PartCircle c => (c.Cx, c.Cy),
        PartArc a => (a.Cx, a.Cy),
        PartRect r => (r.X + r.W / 2, r.Y + r.H / 2),
        PartPolyline pl => (AverageAt(pl.Points, 0), AverageAt(pl.Points, 1)),
        PartText t => (t.X, t.Y),
        _ => (0, 0),
    };

    // ===== 下請け =====

    private static double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));

    private static double DistancePointToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return Distance(px, py, ax, ay);   // 長さゼロの線分は端点との距離
        double t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / lenSq, 0, 1);
        return Distance(px, py, ax + t * dx, ay + t * dy);
    }

    private static double DistanceToPolyline(PartPolyline pl, double x, double y)
    {
        double min = double.MaxValue;
        for (int i = 0; i + 3 < pl.Points.Length; i += 2)
            min = Math.Min(min, DistancePointToSegment(x, y, pl.Points[i], pl.Points[i + 1], pl.Points[i + 2], pl.Points[i + 3]));
        return min;
    }

    private static double DistanceToRect(PartRect r, double x, double y)
    {
        // 回転を打ち消した局所座標へ移してから軸並行矩形として測る
        double cx = r.X + r.W / 2, cy = r.Y + r.H / 2;
        var (lx, ly) = RotatePoint(x, y, cx, cy, -r.Rot);
        double dx = Math.Max(Math.Max(r.X - lx, lx - (r.X + r.W)), 0);
        double dy = Math.Max(Math.Max(r.Y - ly, ly - (r.Y + r.H)), 0);
        if (dx == 0 && dy == 0)
        {
            // 矩形の内側にいる場合は最も近い辺までの距離
            double left = lx - r.X, right = (r.X + r.W) - lx, top = ly - r.Y, bottom = (r.Y + r.H) - ly;
            return Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        }
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double DistanceToArc(PartArc a, double x, double y)
    {
        // 楕円弧の厳密な距離は解析的に求めにくいため、描画（PartDrawing）と同じ分割数の
        // ポリライン近似で測り、見た目と判定を一致させる
        int seg = Math.Max(8, (int)Math.Ceiling(Math.Abs(a.SweepDeg) / 6.0));
        double a0 = a.StartDeg * Math.PI / 180.0, sw = a.SweepDeg * Math.PI / 180.0;
        double min = double.MaxValue;
        double prevX = 0, prevY = 0;
        for (int i = 0; i <= seg; i++)
        {
            double t = a0 + sw * i / seg;
            var (cx, cy) = RotatePoint(a.Cx + a.R * Math.Cos(t), a.Cy + a.EffRy * Math.Sin(t), a.Cx, a.Cy, a.Rot);
            if (i > 0) min = Math.Min(min, DistancePointToSegment(x, y, prevX, prevY, cx, cy));
            prevX = cx; prevY = cy;
        }
        return min;
    }

    private static PartLine RotateLine(PartLine l, double centerX, double centerY, double deg)
    {
        var (x1, y1) = RotatePoint(l.X1, l.Y1, centerX, centerY, deg);
        var (x2, y2) = RotatePoint(l.X2, l.Y2, centerX, centerY, deg);
        return l with { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };
    }

    private static PartPolyline RotatePolyline(PartPolyline pl, double centerX, double centerY, double deg)
    {
        var pts = new double[pl.Points.Length];
        for (int i = 0; i + 1 < pl.Points.Length; i += 2)
        {
            var (rx, ry) = RotatePoint(pl.Points[i], pl.Points[i + 1], centerX, centerY, deg);
            pts[i] = rx; pts[i + 1] = ry;
        }
        return pl with { Points = pts };
    }

    private static double[] TranslatePoints(double[] pts, double dx, double dy)
    {
        var result = new double[pts.Length];
        for (int i = 0; i + 1 < pts.Length; i += 2) { result[i] = pts[i] + dx; result[i + 1] = pts[i + 1] + dy; }
        return result;
    }

    /// <summary>flat な座標配列（x0,y0,x1,y1,...）の X 成分（offset=0）または Y 成分（offset=1）の平均。</summary>
    private static double AverageAt(double[] pts, int offset)
    {
        double sum = 0; int n = 0;
        for (int i = offset; i < pts.Length; i += 2) { sum += pts[i]; n++; }
        return n == 0 ? 0 : sum / n;
    }
}
