using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-068増分3-b1（家老裁可6）: 形状編集キャンバスの幾何演算を <see cref="PartShapeGeometry"/> へ
/// 純粋関数として切り出したことに伴う単体テスト。増分0のPoCではキャンバス内の private メソッドで
/// テスト不能だったため、本増分が初めての検証となる。
///
/// RED証明について: 切り出し元はPoCの private メソッドであり本実装に存在しないため、
/// 「修正前のコードでREDになる」形の証明は原理的に成立しない（memory:
/// feedback_red_proof_new_api_limitation と同型、家老裁定2026-07-25で代替を承認済み）。
/// 代わりに samurai.md 推奨の「実装の該当ガードを一時的に壊すとREDになる」実測で検出力を確認する。
///
/// 入力値の選び方【重要】: 対称な入力（dx=dy・軸上の点・原点・正方形など）を使うと、
/// 成分の取り違えや符号の誤りが結果に現れず、テストが緑のまま穴が残る。往復1周目の隠密レビューで
/// この型の穴が3件見つかった（RotatePoint の dy 符号・Translate の X/Y 取り違え・IsDegenerate の
/// 論理演算子）ため、幾何演算のテストでは意図的に非対称な値を選んでいる。
/// </summary>
public class PartShapeGeometryTests
{
    private const int Precision = 9;

    // ===== Snap（丸めを素通しにするとRED） =====
    // T-138（殿裁定2026-07-31）で刻みを 1/16 から 1/4 へ改めた。
    // 【入力値の選び方】<b>1/16 と 1/4 で結果が異なる値を選ぶ</b>——両方で同じ結果になる値
    // （例：旧テストの 1.2345 は 1/16 でも 1/4 でも 1.25）では、刻みが元へ戻る変更を検出できぬ。

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.12, 0.0)]     // 1/8未満は0へ落ちる（1/16刻みなら 0.125 になり、ここで分かれる）
    [InlineData(0.13, 0.25)]    // 1/8超は1/4へ上がる（1/16刻みなら 0.125）
    [InlineData(0.5, 0.5)]      // 刻みの整数倍はそのまま
    [InlineData(-0.13, -0.25)]  // 負値も対称に丸まる
    [InlineData(0.3, 0.25)]     // 1/16刻みなら 0.3125——刻みが戻れば必ず落ちる
    public void Snap_RoundsToQuarterOfCell(double input, double expected)
        => Assert.Equal(expected, PartShapeGeometry.Snap(input), Precision);

    [Fact]
    public void Snap_既定の刻みは1辺4等分()
    {
        // 殿裁定2026-07-31そのものを固定する網（samurai.md「裁定の根拠を回帰テストの対象にする」）。
        // 上の Theory は「丸めが正しいか」を測るが、本件は「刻みの値が裁定どおりか」を測る。
        Assert.Equal(0.25, PartShapeGeometry.DefaultSnapFractionCells, Precision);
    }

    [Fact]
    public void Snap_CustomFraction_UsesGivenStep()
    {
        // 明示指定は既定値に依らぬ（ゆえに本件は刻みの変更を検出せぬ——それが正しい）。
        Assert.Equal(0.5, PartShapeGeometry.Snap(0.3, fractionCells: 0.5), Precision);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(7.0, 0.0)]         // 半刻み未満は0度
    [InlineData(8.0, 15.0)]        // 半刻み超は15度
    [InlineData(-8.0, -15.0)]
    [InlineData(44.0, 45.0)]
    public void SnapAngleDeg_RoundsToFifteenDegrees(double input, double expected)
        => Assert.Equal(expected, PartShapeGeometry.SnapAngleDeg(input), Precision);

    // ===== IsDegenerate（閾値ガードを外す・論理演算子を取り違えるとRED） =====

    [Theory]
    [InlineData(2.0, 3.0, 2.0, 3.0, true)]    // 完全に同一点
    [InlineData(2.0, 3.0, 2.0, 4.0, false)]   // 縦線（X差のみゼロ）
    [InlineData(2.0, 3.0, 5.0, 3.0, false)]   // 横線（Y差のみゼロ）——|| への取り違えはこの2件で落ちる
    [InlineData(2.0, 3.0, 5.0, 4.0, false)]   // 斜線
    public void IsDegenerate_Line_RequiresBothAxesToCollapse(double x1, double y1, double x2, double y2, bool expected)
        => Assert.Equal(expected, PartShapeGeometry.IsDegenerate(new PartLine(x1, y1, x2, y2)));

    [Theory]
    [InlineData(0.0, 2.0, true)]    // 幅ゼロ
    [InlineData(2.0, 0.0, true)]    // 高さゼロ
    [InlineData(2.0, 3.0, false)]   // 幅≠高さ（正方形にすると W/H の取り違えが隠れる）
    public void IsDegenerate_Rect_JudgesByEitherSide(double w, double h, bool expected)
        => Assert.Equal(expected, PartShapeGeometry.IsDegenerate(new PartRect(0, 0, w, h)));

    [Fact]
    public void IsDegenerate_ZeroRadiusCircle_ReturnsTrue()
        => Assert.True(PartShapeGeometry.IsDegenerate(new PartCircle(0, 0, 0)));

    [Fact]
    public void IsDegenerate_ZeroRadiusArc_ReturnsTrue()
        => Assert.True(PartShapeGeometry.IsDegenerate(new PartArc(0, 0, 0, 180, 180)));

    [Fact]
    public void IsDegenerate_JudgementFreeKind_ReturnsFalse()
        => Assert.False(PartShapeGeometry.IsDegenerate(new PartText("あ", 0, 0)));

    // ===== BuildRect（Math.Min/Abs の正規化を外すとRED） =====

    [Theory]
    [InlineData(1.0, 2.0, 3.0, 5.0)]   // 左上→右下
    [InlineData(3.0, 5.0, 1.0, 2.0)]   // 右下→左上（逆ドラッグ）
    [InlineData(3.0, 2.0, 1.0, 5.0)]   // 右上→左下
    [InlineData(1.0, 5.0, 3.0, 2.0)]   // 左下→右上
    public void BuildRect_AnyDragDirection_NormalizesToPositiveSize(double x1, double y1, double x2, double y2)
    {
        var r = PartShapeGeometry.BuildRect(x1, y1, x2, y2);

        Assert.Equal(1.0, r.X, Precision);
        Assert.Equal(2.0, r.Y, Precision);   // X≠Y・W≠H ゆえ成分の取り違えも落ちる
        Assert.Equal(2.0, r.W, Precision);
        Assert.Equal(3.0, r.H, Precision);
    }

    // ===== BuildCircle / BuildArc =====

    [Fact]
    public void BuildCircle_RadiusIsDistanceFromCenterToEdgePoint()
    {
        // 中心を原点から外す（原点だと Cx/Cy に別の値が入っても気付けない場合がある）
        var c = PartShapeGeometry.BuildCircle(1, 2, 4, 6);

        Assert.Equal(1.0, c.Cx, Precision);
        Assert.Equal(2.0, c.Cy, Precision);
        Assert.Equal(5.0, c.R, Precision);
    }

    [Fact]
    public void BuildArc_FromBoundingBox_IsHalfEllipse()
    {
        // 外接矩形を原点から外し、幅≠高さにする
        var a = PartShapeGeometry.BuildArc(1, 3, 5, 5);

        Assert.Equal(3.0, a.Cx, Precision);
        Assert.Equal(4.0, a.Cy, Precision);
        Assert.Equal(2.0, a.R, Precision);
        Assert.Equal(1.0, a.Ry, Precision);
        Assert.Equal(180.0, a.StartDeg, Precision);
        Assert.Equal(180.0, a.SweepDeg, Precision);
        Assert.Equal(0.0, a.Rot, Precision);
    }

    [Fact]
    public void BuildArc_ZeroDrag_ClampsRadiiToLowerBound()
    {
        var a = PartShapeGeometry.BuildArc(1, 1, 1, 1);

        Assert.Equal(0.05, a.R, Precision);
        Assert.Equal(0.05, a.Ry, Precision);
    }

    // ===== DistanceToPrimitive =====

    [Theory]
    [InlineData(5.0, 3.0, 3.0)]     // 線分の真横
    [InlineData(5.0, 0.0, 0.0)]     // 線分上
    [InlineData(-5.0, 0.0, 5.0)]    // 線分の外側（端点までの距離へクランプ）
    [InlineData(14.0, 0.0, 4.0)]
    public void DistanceToPrimitive_Line_ClampsToSegmentEnds(double x, double y, double expected)
        => Assert.Equal(expected, PartShapeGeometry.DistanceToPrimitive(new PartLine(0, 0, 10, 0), x, y), Precision);

    [Fact]
    public void DistanceToPrimitive_ObliqueLine_MeasuresPerpendicularDistance()
    {
        // 斜めの線分。水平・垂直の線分だけでは線分ベクトルの dx/dy 取り違えが結果に現れにくい。
        // (0,0)-(4,3) への点(0,5)からの垂線の足は (2.4,1.8)、距離はちょうど 4。
        Assert.Equal(4.0, PartShapeGeometry.DistanceToPrimitive(new PartLine(0, 0, 4, 3), 0, 5), Precision);
    }

    [Theory]
    [InlineData(3.0, 8.0, 2.0)]     // 円の外側
    [InlineData(3.0, 1.0, 5.0)]     // 中心は輪郭から半径分だけ離れている（塗りではなく輪郭で測る）
    [InlineData(8.0, 1.0, 0.0)]     // 輪郭上
    public void DistanceToPrimitive_Circle_MeasuresToOutline(double x, double y, double expected)
        => Assert.Equal(expected, PartShapeGeometry.DistanceToPrimitive(new PartCircle(3, 1, 5), x, y), Precision);

    [Theory]
    [InlineData(6.0, 4.0, 2.0)]     // 内側（最も近い辺まで）
    [InlineData(-2.0, 4.0, 3.0)]    // 左外側
    [InlineData(1.0, 2.0, 0.0)]     // 角の上
    [InlineData(14.0, 10.0, 5.0)]   // 斜め外側（3-4-5）
    public void DistanceToPrimitive_Rect_HandlesInsideAndOutside(double x, double y, double expected)
        // 原点始点・正方形を避ける（X/Y・W/H の取り違えを検出するため）
        => Assert.Equal(expected, PartShapeGeometry.DistanceToPrimitive(new PartRect(1, 2, 10, 4), x, y), Precision);

    [Fact]
    public void DistanceToPrimitive_RotatedRect_MeasuresInRotatedFrame()
    {
        // 中心(0,0)の 4x2 矩形を90度回転すると、見た目は 2x4。回転を考慮しなければ
        // 点(0,3) は「上辺の外側1.0」だが、考慮すれば辺の上（距離0）になる。
        // 注: 本ケースは「回転を考慮するか否か」は検出できるが、打ち消しの符号までは検出できない
        //（矩形が中心対称・測定点が軸上のため ±90度で結果が一致する）。符号は次のテストで押さえる。
        var rect = new PartRect(-2, -1, 4, 2, Rot: 90);

        Assert.Equal(0.0, PartShapeGeometry.DistanceToPrimitive(rect, 0, 2), Precision);
        Assert.Equal(1.0, PartShapeGeometry.DistanceToPrimitive(rect, 0, 3), Precision);
    }

    [Fact]
    public void DistanceToPrimitive_RotatedRect_UndoesRotationInCorrectDirection()
    {
        // 45度回転・測定点も軸から外した非対称な配置。回転の打ち消しを +Rot と誤ると
        // 局所座標が (1.414, 2.828) となり距離1.828、正しく -Rot なら (2.828, -1.414) で距離0.926。
        var rect = new PartRect(-2, -1, 4, 2, Rot: 45);

        Assert.Equal(0.926, PartShapeGeometry.DistanceToPrimitive(rect, 3, 1), 3);
    }

    [Fact]
    public void DistanceToPrimitive_Polyline_TakesNearestSegment()
    {
        // 原点から外した折れ線。(1,1)-(11,1)-(11,11)
        var pl = new PartPolyline(new double[] { 1, 1, 11, 1, 11, 11 });

        Assert.Equal(2.0, PartShapeGeometry.DistanceToPrimitive(pl, 6, 3), Precision);   // 1本目の真横
        Assert.Equal(3.0, PartShapeGeometry.DistanceToPrimitive(pl, 8, 6), Precision);   // 2本目の方が近い
    }

    [Fact]
    public void DistanceToPrimitive_Arc_ApproximatesOutline()
    {
        // 中心(1,2)・半径2の半円弧（StartDeg=180・SweepDeg=180、+y下ゆえ上半分）。頂点は(1,0)。
        var arc = new PartArc(1, 2, 2, 180, 180);

        Assert.True(PartShapeGeometry.DistanceToPrimitive(arc, 1, 0) < 0.05);
        Assert.True(PartShapeGeometry.DistanceToPrimitive(arc, 1, 2) > 1.9);   // 弦の中点は輪郭から遠い
    }

    [Fact]
    public void DistanceToPrimitive_Text_MeasuresToAnchorPoint()
        // アンカーを原点から外す（距離計算は対称ゆえ、原点だと引数の取り違えが隠れる）
        => Assert.Equal(5.0, PartShapeGeometry.DistanceToPrimitive(new PartText("あ", 1, 2), 4, 6), Precision);

    // ===== HitTest（末尾からの走査をやめるとRED） =====

    [Fact]
    public void HitTest_OverlappingPrimitives_PrefersFrontmost()
    {
        var list = new List<PartPrimitive>
        {
            new PartLine(0, 0, 10, 0),
            new PartLine(0, 0, 10, 0),   // 同じ位置に重ねた2本目（最前面）
        };

        Assert.Equal(1, PartShapeGeometry.HitTest(list, 5, 0));
    }

    [Fact]
    public void HitTest_BeyondTolerance_ReturnsMinusOne()
    {
        var list = new List<PartPrimitive> { new PartLine(0, 0, 10, 0) };

        Assert.Equal(-1, PartShapeGeometry.HitTest(list, 5, 0.31));   // 既定許容0.3セルの外
        Assert.Equal(0, PartShapeGeometry.HitTest(list, 5, 0.29));
    }

    [Fact]
    public void HitTest_EmptyList_ReturnsMinusOne()
        => Assert.Equal(-1, PartShapeGeometry.HitTest(new List<PartPrimitive>(), 0, 0));

    // ===== Translate（X/Y の取り違えを検出するため dx≠dy を使う） =====

    [Fact]
    public void Translate_Line_MovesBothEndpoints()
    {
        var moved = (PartLine)PartShapeGeometry.Translate(new PartLine(1, 2, 3, 4), 10, 20);

        Assert.Equal(11.0, moved.X1, Precision);
        Assert.Equal(22.0, moved.Y1, Precision);
        Assert.Equal(13.0, moved.X2, Precision);
        Assert.Equal(24.0, moved.Y2, Precision);
    }

    [Fact]
    public void Translate_Polyline_MovesEveryVertex()
    {
        // dx≠dy かつ頂点も対角線上に置かない（dx=dy=1・頂点(0,0),(2,2) では取り違えが隠れる）
        var moved = (PartPolyline)PartShapeGeometry.Translate(new PartPolyline(new double[] { 0, 1, 2, 5 }), 10, 3);

        Assert.Equal(new double[] { 10, 4, 12, 8 }, moved.Points);
    }

    [Fact]
    public void Translate_Rect_MovesOriginKeepsSize()
    {
        var moved = (PartRect)PartShapeGeometry.Translate(new PartRect(1, 2, 4, 3, Rot: 30), 10, 20);

        Assert.Equal(11.0, moved.X, Precision);
        Assert.Equal(22.0, moved.Y, Precision);
        Assert.Equal(4.0, moved.W, Precision);
        Assert.Equal(3.0, moved.H, Precision);
        Assert.Equal(30.0, moved.Rot, Precision);
    }

    [Fact]
    public void Translate_DoesNotMutateOriginal()
    {
        var original = new PartLine(1, 2, 3, 4);

        PartShapeGeometry.Translate(original, 10, 20);

        Assert.Equal(1.0, original.X1, Precision);   // record ゆえ元は不変（Undoスタックの前提）
    }

    // ===== Rotate（型ごとの実装差を潰すとRED） =====

    [Fact]
    public void Rotate_Line_BakesRotationIntoCoordinates()
    {
        // 両端とも軸から外す。軸上の点（dy=0 等）では回転行列の -dy*sin 項が消え、
        // 符号の誤りを検出できない。(1,2)→(-2,1)、(3,1)→(-1,3)。
        var rotated = (PartLine)PartShapeGeometry.Rotate(new PartLine(1, 2, 3, 1), 0, 0, 90);

        Assert.Equal(-2.0, rotated.X1, Precision);
        Assert.Equal(1.0, rotated.Y1, Precision);
        Assert.Equal(-1.0, rotated.X2, Precision);
        Assert.Equal(3.0, rotated.Y2, Precision);
    }

    [Fact]
    public void Rotate_Rect_AccumulatesRotFieldOnly()
    {
        var rotated = (PartRect)PartShapeGeometry.Rotate(new PartRect(1, 2, 4, 2, Rot: 15), 0, 0, 90);

        Assert.Equal(105.0, rotated.Rot, Precision);
        Assert.Equal(1.0, rotated.X, Precision);   // 座標自体は動かさない（GuiEcad原本の実装差）
        Assert.Equal(2.0, rotated.Y, Precision);
    }

    [Fact]
    public void Rotate_Arc_AccumulatesRotFieldOnly()
    {
        var rotated = (PartArc)PartShapeGeometry.Rotate(new PartArc(1, 1, 2, 180, 180, Ry: 1, Rot: 10), 0, 0, 15);

        Assert.Equal(25.0, rotated.Rot, Precision);
        Assert.Equal(1.0, rotated.Cx, Precision);
    }

    [Fact]
    public void Rotate_Circle_IsUnchanged()
    {
        var circle = new PartCircle(3, 4, 5);

        Assert.Same(circle, PartShapeGeometry.Rotate(circle, 0, 0, 90));   // 回しても見た目が変わらない
    }

    [Fact]
    public void Rotate_Polyline_BakesRotationIntoEveryVertex()
    {
        // 頂点を軸から外す（(1,0),(2,0) のような軸上の点では dy 符号の誤りが現れない）
        var rotated = (PartPolyline)PartShapeGeometry.Rotate(new PartPolyline(new double[] { 1, 2, 3, 1 }), 0, 0, 90);

        Assert.Equal(-2.0, rotated.Points[0], Precision);
        Assert.Equal(1.0, rotated.Points[1], Precision);
        Assert.Equal(-1.0, rotated.Points[2], Precision);
        Assert.Equal(3.0, rotated.Points[3], Precision);
    }

    [Theory]
    [InlineData(90.0, 0.0, 1.0)]
    [InlineData(180.0, -1.0, 0.0)]
    [InlineData(270.0, 0.0, -1.0)]
    [InlineData(360.0, 1.0, 0.0)]
    public void RotatePoint_FromXAxis_TurnsClockwiseInScreenCoords(double deg, double expectedX, double expectedY)
    {
        // X軸上の点（dy=0）。回転行列のうち dx 側の項だけを突く。
        var (x, y) = PartShapeGeometry.RotatePoint(1, 0, 0, 0, deg);

        Assert.Equal(expectedX, x, Precision);
        Assert.Equal(expectedY, y, Precision);
    }

    [Theory]
    [InlineData(90.0, -1.0, 0.0)]
    [InlineData(180.0, 0.0, -1.0)]
    [InlineData(270.0, 1.0, 0.0)]
    public void RotatePoint_FromYAxis_AppliesCorrectSignToDy(double deg, double expectedX, double expectedY)
    {
        // Y軸上の点（dx=0）。X軸上の点だけでは -dy*sin の符号が結果に現れず、
        // 符号を誤ったまま緑になる（往復1周目の隠密指摘）。
        var (x, y) = PartShapeGeometry.RotatePoint(0, 1, 0, 0, deg);

        Assert.Equal(expectedX, x, Precision);
        Assert.Equal(expectedY, y, Precision);
    }

    [Fact]
    public void RotatePoint_ObliquePoint_RotatesBothComponents()
    {
        // dx・dy とも非ゼロ。成分の取り違え・符号の誤りのいずれも結果に現れる。
        var (x, y) = PartShapeGeometry.RotatePoint(3, 1, 0, 0, 90);

        Assert.Equal(-1.0, x, Precision);
        Assert.Equal(3.0, y, Precision);
    }

    [Fact]
    public void RotatePoint_AboutNonOriginCenter_RotatesAroundThatCenter()
    {
        // 中心(2,3)まわりに点(4,4)を90度。相対(2,1)→(-1,2)、絶対では(1,5)。
        var (x, y) = PartShapeGeometry.RotatePoint(4, 4, 2, 3, 90);

        Assert.Equal(1.0, x, Precision);
        Assert.Equal(5.0, y, Precision);
    }

    [Fact]
    public void RotatePoint_AboutNonOriginCenter_KeepsCenterFixed()
    {
        var (x, y) = PartShapeGeometry.RotatePoint(5, 5, 5, 5, 123.4);

        Assert.Equal(5.0, x, Precision);
        Assert.Equal(5.0, y, Precision);
    }

    // ===== 基準枠（T-133増分1、殿裁定6=基準点は中央） =====
    // 入力値の選び方: 幅≠高さ・セル寸法も割り切れぬ値を選ぶ。幅と高さを取り違えても、
    // 正方形なら結果が変わらず穴が残るため（samurai.md「テスト入力の対称性・退化性チェック」）。
    // また「行は中心基準・列は境界基準」という非対称そのものが本メソッドの要ゆえ、
    // X と Y を別々に検証する。

    // T-139（殿裁定2026-07-31）で行方向の半径を ±((h-1)+0.5) から ±h/2 セルへ改めた
    // ＝高さ h セルちょうど（原本GuiEcadと同じ形）。P-148 はこの裁定で覆っている。
    [Theory]
    [InlineData(3, 5, 2.5, 7.5, 12.5, -6.25)]   // 幅<高さ。半径=5/2*2.5=6.25
    [InlineData(5, 3, 2.0, 10.0, 6.0, -3.0)]    // 幅>高さ（上と逆にして取り違えを炙る）。半径=3/2*2.0=3.0
    [InlineData(1, 4, 3.0, 3.0, 12.0, -6.0)]    // 幅1（列方向の退化）。半径=4/2*3.0=6.0
    public void FrameRect_RowIsCentered_ColumnStartsAtZero(
        int widthCells, int heightCells, double cellMm,
        double expectedWidth, double expectedHeight, double expectedY)
    {
        var (x, y, w, h) = PartShapeGeometry.FrameRect(widthCells, heightCells, cellMm);

        Assert.Equal(0.0, x, Precision);                  // 列は境界基準ゆえ左辺は常に0
        Assert.Equal(expectedY, y, Precision);            // 行は中心基準ゆえ上辺は -高さ/2
        Assert.Equal(expectedWidth, w, Precision);
        Assert.Equal(expectedHeight, h, Precision);
    }

    [Theory]
    [InlineData(3, 5, 2.5)]
    [InlineData(5, 3, 2.0)]
    [InlineData(2, 7, 1.5)]
    public void FrameRect_VerticalCenterSitsOnRowZero(int widthCells, int heightCells, double cellMm)
    {
        // 接続点の RowOffset=0 と枠の中心が同じ高さに来ることが本増分の眼目。
        // 上辺+高さ/2 が 0 になることで、上辺と高さのどちらが誤っても検出できる。
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells, heightCells, cellMm);

        Assert.Equal(0.0, y + h / 2, Precision);
    }

    [Fact]
    public void FrameRect_HeightOne_SpansHalfCellEachWay()
    {
        // 高さ1（退化ケース）。ClampPort が行オフセット0のみを許す高さと対応する。
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells: 4, heightCells: 1, cellMm: 9.0);

        Assert.Equal(-4.5, y, Precision);
        Assert.Equal(9.0, h, Precision);
    }

    [Fact]
    public void FrameRect_ZeroHeight_ClampsToOneRow()
    {
        // 高さ0（退化ケース）。T-139の式 ±h/2 では素直に計算すると半径0＝枠が消える。
        // Math.Max(1, h) で高さ1へ潰し、1セル分の枠を返すことを固定する
        // （原本の ApplyDefinition が _h = Math.Max(1, def.HeightCells) とするのと同じ扱い）。
        var (x, y, w, h) = PartShapeGeometry.FrameRect(widthCells: 3, heightCells: 0, cellMm: 9.0);

        Assert.Equal(0.0, x, Precision);
        Assert.Equal(-4.5, y, Precision);
        Assert.Equal(27.0, w, Precision);
        Assert.Equal(9.0, h, Precision);
        Assert.True(h > 0, "枠の高さが負や0になってはならぬ（矩形の反転・消失を防ぐ）");
    }

    [Fact]
    public void FrameRect_HeightOne_歴代の裁定を通じて不変()
    {
        // 高さ1の枠は ±0.5 セルであり、<b>P-148以前・P-148・T-139のいずれでも同値</b>。
        // これは殿がP-148を裁可された際の材料「既存18件への影響なし（組込み15件は高さ1）」を
        // 支えた前提であり、T-139で式が ±h/2 へ変わった後もそのまま活きる
        // （1/2 = (1-1)+0.5 = 0.5 ゆえ）。
        // <b>組込み15件が高さ1である限り、枠の式を変えても影響が出ぬことを固定する網である。</b>
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells: 4, heightCells: 1, cellMm: 9.0);

        Assert.Equal(-4.5, y, Precision);
        Assert.Equal(9.0, h, Precision);
    }

    // T-139（殿裁定2026-07-31）＝<b>「枠は目安であって柵ではない」</b>。
    // P-148 は「枠が接続点の可動範囲を必ず覆う」ことを求めたが、本裁定でそれは覆った。
    // 原本GuiEcad が枠 h セル・接続点 ±(h-1) と定めており、高さ3以上で接続点が枠を越える
    // ——その姿へ戻したものである。<b>下の2件は、越えることと越えぬことの両方を固定する。</b>

    [Theory]
    [InlineData(1)]   // 接続点は0のみ＝枠(±0.5セル)の中心
    [InlineData(2)]   // 接続点±1＝枠(±1.0セル)の辺のちょうど上（はみ出しはせぬ）
    public void FrameRect_T139_高さ2までは接続点が枠に収まる(int heightCells)
    {
        const double cellMm = 9.0;
        const int widthCells = 4;
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells, heightCells, cellMm);

        var (bottomRow, _) = PartShapeGeometry.ClampPort(0, 999, widthCells, heightCells);
        var (topRow, _) = PartShapeGeometry.ClampPort(0, -999, widthCells, heightCells);

        Assert.InRange(topRow * cellMm, y, y + h);
        Assert.InRange(bottomRow * cellMm, y, y + h);
    }

    [Theory]
    [InlineData(3, 2, 1.5)]     // 接続点±2 に対し枠は±1.5セル
    [InlineData(5, 4, 2.5)]
    [InlineData(12, 11, 6.0)]   // HeightBox が許す上限
    public void FrameRect_T139_高さ3以上では接続点が枠の外へ出る(
        int heightCells, int expectedRowLimit, double expectedHalfSpanCells)
    {
        // 「はみ出す」という事実だけでなく、<b>どこまで出るか</b>も固定する
        // ——可動範囲(h-1)と枠(h/2)の両方が誤れば、はみ出しの有無だけでは気づけぬため。
        const double cellMm = 9.0;
        const int widthCells = 4;
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells, heightCells, cellMm);

        var (bottomRow, _) = PartShapeGeometry.ClampPort(0, 999, widthCells, heightCells);

        Assert.Equal(expectedRowLimit, bottomRow);
        Assert.Equal(expectedHalfSpanCells * cellMm, y + h, Precision);
        Assert.True(bottomRow * cellMm > y + h, "高さ3以上では接続点が枠の下辺を越えるはず");
        Assert.True(-bottomRow * cellMm < y, "上側も同じだけ越えるはず（中心基準ゆえ対称）");
    }

    // ===== パーツエディタの作図グリッド（T-137新設、T-139で原本の形へ組み直し） =====
    // T-139（殿裁定2026-07-31）＝原本GuiEcadの DrawGrid へ揃える。原本は「0.25刻みの薄線」と
    // 「整数境界の縦線」を重ね、行中心線 y=0 を別途最も濃く引く。
    // T-137の「行の境目＝半整数」の線は取り除いた——原本に無く、かつ枠が h セルへ戻ると
    // h が偶数のとき枠の辺（整数位置）と半整数の線が一致せぬため。
    //
    // 【入力値の選び方】幅と高さに別の値を採り、取り違えが結果に現れるようにする。
    // 刻みも 1.0 と 0.25 の両方を測る——片方だけでは刻みを引数化した意味が検証できぬ。

    [Theory]
    [InlineData(1, 2)]    // 幅1（列方向の退化）
    [InlineData(3, 4)]
    [InlineData(12, 13)]  // WidthBox が許す上限（Math.Clamp(...,1,12)）
    public void GridLinesAt_刻み1では枠の幅プラス1本になる(int widthCells, int expectedCount)
    {
        const double cellMm = 9.0;
        // 高さは幅と別の値を採る（取り違えを炙るため）。
        var (x, _, w, _) = PartShapeGeometry.FrameRect(widthCells, heightCells: 5, cellMm);

        var xs = PartShapeGeometry.GridLinesAt(x / cellMm, (x + w) / cellMm, 1.0);

        Assert.Equal(expectedCount, xs.Length);
    }

    [Theory]
    [InlineData(1, 5)]    // 枠1セル -> 0.25刻みで 4区画+1本
    [InlineData(2, 9)]    // 枠2セル -> 8区画+1本
    [InlineData(3, 13)]   // 枠3セル -> 12区画+1本
    [InlineData(12, 49)]  // HeightBox が許す上限
    public void GridLinesAt_刻み025では枠の高さの4倍プラス1本になる(int heightCells, int expectedCount)
    {
        // T-139で枠は h セルちょうど（旧 2h-1 から改めた）。1セルを4分割ゆえ 4h+1 本。
        const double cellMm = 9.0;
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells: 4, heightCells, cellMm);

        var ys = PartShapeGeometry.GridLinesAt(y / cellMm, (y + h) / cellMm, 0.25);

        Assert.Equal(expectedCount, ys.Length);
        // 殿の御要望「1セルを4×4に割った位置」——枠の中の区画は 4h 個になる。
        Assert.Equal(4 * heightCells, ys.Length - 1);
    }

    [Fact]
    public void GridLinesAt_刻み1は整数の位置に入る()
        => Assert.Equal([0.0, 1.0, 2.0, 3.0], PartShapeGeometry.GridLinesAt(0.0, 3.0, 1.0));

    [Fact]
    public void GridLinesAt_刻み025は4分の1の位置に入る()
        => Assert.Equal([0.0, 0.25, 0.5, 0.75, 1.0], PartShapeGeometry.GridLinesAt(0.0, 1.0, 0.25));

    [Fact]
    public void GridLinesAt_同じ範囲でも刻みで本数が変わる()
    {
        // 刻みを引数化した意味そのもの。両者が同数なら刻みが効いておらぬ。
        var coarse = PartShapeGeometry.GridLinesAt(-1.0, 1.0, 1.0);
        var fine = PartShapeGeometry.GridLinesAt(-1.0, 1.0, 0.25);

        Assert.Equal([-1.0, 0.0, 1.0], coarse);
        Assert.Equal(9, fine.Length);
        Assert.Equal(-0.75, fine[1], Precision);
    }

    [Fact]
    public void GridLinesAt_高さ0の枠は高さ1と同じ本数になる()
    {
        // 高さ0（退化ケース）。FrameRect が Math.Max(1, h) で高さ1へ潰すのと歩調を合わせる。
        const double cellMm = 9.0;
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells: 3, heightCells: 0, cellMm);

        Assert.Equal([-0.5, -0.25, 0.0, 0.25, 0.5],
            PartShapeGeometry.GridLinesAt(y / cellMm, (y + h) / cellMm, 0.25));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(1, 5)]   // 幅と高さを入れ替えた組（取り違えを炙る）
    [InlineData(12, 1)]
    public void GridLinesAt_端の線が基準枠の辺と一致する(int widthCells, int heightCells)
    {
        // 枠の内側へ濃い線を重ねる描画が成り立つ前提。端がずれれば二重線・欠けになる。
        // T-139の枠は ±h/2 ゆえ h が偶数なら整数・奇数なら半整数——いずれも 0.25 の倍数ゆえ一致する。
        const double cellMm = 9.0;
        var (x, y, w, h) = PartShapeGeometry.FrameRect(widthCells, heightCells, cellMm);

        var xs = PartShapeGeometry.GridLinesAt(x / cellMm, (x + w) / cellMm, 1.0);
        var ys = PartShapeGeometry.GridLinesAt(y / cellMm, (y + h) / cellMm, 0.25);

        Assert.Equal(x, xs[0] * cellMm, Precision);
        Assert.Equal(x + w, xs[^1] * cellMm, Precision);
        Assert.Equal(y, ys[0] * cellMm, Precision);
        Assert.Equal(y + h, ys[^1] * cellMm, Precision);
    }

    [Fact]
    public void GridLinesAt_範囲の内側の線だけを返す()
    {
        // キャンバス全域へ引く際は、パン・ズームにより範囲の端が半端な値になる。
        Assert.Equal([-2.0, -1.0, 0.0, 1.0, 2.0, 3.0], PartShapeGeometry.GridLinesAt(-2.3, 3.7, 1.0));
        Assert.Equal([-2.25, -2.0, -1.75], PartShapeGeometry.GridLinesAt(-2.3, -1.7, 0.25));
    }

    [Theory]
    [InlineData(0.2, 0.4, 1.0)]    // 刻みの倍数を1つも含まぬ狭い範囲
    [InlineData(3.0, 1.0, 1.0)]    // 範囲が逆転（退化）
    [InlineData(0.0, 3.0, 0.0)]    // 刻み0（退化）——ゼロ除算も無限ループも起こさぬ
    [InlineData(0.0, 3.0, -1.0)]   // 刻みが負（退化）
    public void GridLinesAt_線が入らぬ範囲や退化した刻みでは空を返す(double min, double max, double step)
        => Assert.Empty(PartShapeGeometry.GridLinesAt(min, max, step));

    // ===== 接続点（T-068増分3-c） =====
    // 座標の対応に注意: 接続点の x は BoundaryOffset、y は RowOffset であり、PortDef の宣言順
    // (Name, RowOffset, BoundaryOffset) とは逆になる。入力値は幅≠高さ・x≠y を選び、取り違えが
    // 結果に現れるようにする。

    [Theory]
    [InlineData(2.0, 1.0, 1, 2)]      // 枠の内側（丸め不要）
    [InlineData(-3.0, 0.0, 0, 0)]     // 境界オフセットが左外 -> 0 へ
    [InlineData(9.0, 0.0, 0, 5)]      // 境界オフセットが右外 -> 幅へ
    [InlineData(0.0, -7.0, -2, 0)]    // 行オフセットが上外 -> -(高さ-1) へ
    [InlineData(0.0, 7.0, 2, 0)]      // 行オフセットが下外 -> 高さ-1 へ
    [InlineData(2.4, 1.4, 1, 2)]      // 近い格子へ丸める（下）
    [InlineData(2.6, 1.6, 2, 3)]      // 近い格子へ丸める（上）
    [InlineData(-0.4, -1.6, -2, 0)]   // 負の値の丸めとクランプ
    public void ClampPort_KeepsPortWithinFrame(double cellX, double cellY, int expectedRow, int expectedBoundary)
    {
        var (row, boundary) = PartShapeGeometry.ClampPort(cellX, cellY, widthCells: 5, heightCells: 3);

        Assert.Equal(expectedRow, row);
        Assert.Equal(expectedBoundary, boundary);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(-5.0)]
    public void ClampPort_HeightOne_AllowsOnlyCenterRow(double cellY)
    {
        // 高さ1のパーツでは行オフセットの取りうる値が0だけになる（退化ケース）
        var (row, _) = PartShapeGeometry.ClampPort(0, cellY, widthCells: 3, heightCells: 1);

        Assert.Equal(0, row);
    }

    [Fact]
    public void ClampPort_ZeroWidth_AllowsOnlyBoundaryZero()
    {
        var (_, boundary) = PartShapeGeometry.ClampPort(5, 0, widthCells: 0, heightCells: 3);

        Assert.Equal(0, boundary);
    }

    [Fact]
    public void IndexOfPortAt_FindsPortAtGivenGridPosition()
    {
        var ports = new List<PortDef>
        {
            new("P1", 0, 0),
            new("P2", 1, 2),   // 行1・境界2
            new("P3", 2, 1),   // 行と境界を入れ替えた位置（成分の取り違えを検出するため）
        };

        Assert.Equal(1, PartShapeGeometry.IndexOfPortAt(ports, rowOffset: 1, boundaryOffset: 2));
        Assert.Equal(2, PartShapeGeometry.IndexOfPortAt(ports, rowOffset: 2, boundaryOffset: 1));
        Assert.Equal(-1, PartShapeGeometry.IndexOfPortAt(ports, rowOffset: 3, boundaryOffset: 3));
    }

    [Fact]
    public void IndexOfPortAt_EmptyList_ReturnsMinusOne()
        => Assert.Equal(-1, PartShapeGeometry.IndexOfPortAt(new List<PortDef>(), 0, 0));

    [Fact]
    public void HitTestPort_MapsBoundaryToXAndRowToY()
    {
        var ports = new List<PortDef> { new("P1", 1, 4) };   // 行1・境界4 -> 画面上は (x=4, y=1)

        Assert.Equal(0, PartShapeGeometry.HitTestPort(ports, 4, 1));
        Assert.Equal(-1, PartShapeGeometry.HitTestPort(ports, 1, 4));   // x/yを取り違えると当たる位置
    }

    [Fact]
    public void HitTestPort_BeyondTolerance_ReturnsMinusOne()
    {
        var ports = new List<PortDef> { new("P1", 1, 4) };

        Assert.Equal(0, PartShapeGeometry.HitTestPort(ports, 4.29, 1));
        Assert.Equal(-1, PartShapeGeometry.HitTestPort(ports, 4.31, 1));
    }

    [Fact]
    public void HitTestPort_OverlappingPorts_PrefersLastPlaced()
    {
        var ports = new List<PortDef> { new("P1", 1, 4), new("P2", 1, 4) };

        Assert.Equal(1, PartShapeGeometry.HitTestPort(ports, 4, 1));
    }

    [Fact]
    public void HitTestPort_EmptyList_ReturnsMinusOne()
        => Assert.Equal(-1, PartShapeGeometry.HitTestPort(new List<PortDef>(), 0, 0));

    // ===== 接続点の描画寸法（T-136(B)増分3、殿裁定2026-07-31＝選択表現をリングへ） =====

    /// <summary>
    /// <b>この関数の要は「リングは塗りより大きい」という大小関係</b>にござる——
    /// 逆転すれば、増分4で入る「種類色」をリングが覆い隠す。値の一致だけを測ると、
    /// <b>両方を同じ率にする改変</b>（リング率を塗り率と同値にする等）を見逃す。
    /// <para><b>【入力値の選び方】</b>既定の 9.0 だけでなく<b>拡大・縮小の両側</b>を測る。
    /// 単一の値では「<c>cellMm</c> を掛け忘れて定数をそのまま返す」型の改変が、
    /// たまたま近い値になって通り抜けうる。</para>
    /// </summary>
    [Theory]
    [InlineData(9.0)]    // 既定のセル寸法
    [InlineData(2.5)]    // 縮小側
    [InlineData(20.0)]   // 拡大側
    public void 選択リングは塗り円より大きい(double cellMm)
    {
        var (fill, ring) = PartShapeGeometry.PortVisualRadiiMm(cellMm);

        Assert.True(ring > fill, $"リング({ring})が塗り({fill})以下では、増分4で入る種類色が隠れる");
    }

    /// <summary>半径はセル寸法に比例する（ズームに追随する）。上の大小関係とは別軸——
    /// あちらは順序、こちらは値そのもの。</summary>
    [Theory]
    [InlineData(9.0, 1.26, 1.80)]     // 0.14／0.20 セル
    [InlineData(2.5, 0.35, 0.50)]     // 比例していなければ、この行が合わぬ
    public void 接続点の半径はセル寸法に比例する(double cellMm, double expectedFill, double expectedRing)
    {
        var (fill, ring) = PartShapeGeometry.PortVisualRadiiMm(cellMm);

        Assert.Equal(expectedFill, fill, 6);
        Assert.Equal(expectedRing, ring, 6);
    }

    [Fact]
    public void セル寸法が0なら両半径とも0になる()
    {
        // GridGeometry は readonly struct にて、new GridGeometry() では宣言した既定値が効かず
        // CellMm=0 になりうる（T-125増分αで判明した罠。samurai.md の境界検証チェックリスト項目0）。
        // その場合も負にならず「描画が消えるだけ」で済むことを固定する——負の半径は
        // 描画バックエンドによっては例外を招く。
        var (fill, ring) = PartShapeGeometry.PortVisualRadiiMm(0.0);

        Assert.Equal(0.0, fill);
        Assert.Equal(0.0, ring);
    }

    // ===== CenterOf =====

    [Fact]
    public void CenterOf_Line_IsMidpoint()
    {
        var (x, y) = PartShapeGeometry.CenterOf(new PartLine(0, 0, 4, 2));

        Assert.Equal(2.0, x, Precision);
        Assert.Equal(1.0, y, Precision);
    }

    [Fact]
    public void CenterOf_Rect_IsGeometricCenter()
    {
        var (x, y) = PartShapeGeometry.CenterOf(new PartRect(1, 2, 4, 6));

        Assert.Equal(3.0, x, Precision);
        Assert.Equal(5.0, y, Precision);
    }

    [Fact]
    public void CenterOf_Polyline_IsAverageOfVertices()
    {
        var (x, y) = PartShapeGeometry.CenterOf(new PartPolyline(new double[] { 0, 0, 2, 3, 4, 6 }));

        Assert.Equal(2.0, x, Precision);
        Assert.Equal(3.0, y, Precision);
    }

    [Fact]
    public void CenterOf_EmptyPolyline_ReturnsOrigin()
    {
        var (x, y) = PartShapeGeometry.CenterOf(new PartPolyline(Array.Empty<double>()));

        Assert.Equal(0.0, x, Precision);   // ゼロ除算を避けるガード
        Assert.Equal(0.0, y, Precision);
    }

    [Fact]
    public void CenterOf_CircleAndArc_AreTheirCenters()
    {
        var (cx, cy) = PartShapeGeometry.CenterOf(new PartCircle(3, 4, 5));
        var (ax, ay) = PartShapeGeometry.CenterOf(new PartArc(7, 8, 2, 0, 90));

        Assert.Equal(3.0, cx, Precision);
        Assert.Equal(4.0, cy, Precision);
        Assert.Equal(7.0, ax, Precision);
        Assert.Equal(8.0, ay, Precision);
    }
}
