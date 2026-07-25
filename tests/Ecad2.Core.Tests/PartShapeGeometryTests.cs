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
/// 各テストがどのガードを突いているかは実装側のコメントと対応させてある。
/// </summary>
public class PartShapeGeometryTests
{
    private const int Precision = 9;

    // ===== Snap（丸めを素通しにするとRED） =====

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.03, 0.0)]        // 1/32未満は0へ落ちる
    [InlineData(0.04, 0.0625)]     // 1/32超は1/16へ上がる
    [InlineData(0.5, 0.5)]         // 刻みの整数倍はそのまま
    [InlineData(-0.04, -0.0625)]   // 負値も対称に丸まる
    [InlineData(1.2345, 1.25)]
    public void Snap_RoundsToSixteenthOfCell(double input, double expected)
        => Assert.Equal(expected, PartShapeGeometry.Snap(input), Precision);

    [Fact]
    public void Snap_CustomFraction_UsesGivenStep()
        => Assert.Equal(0.5, PartShapeGeometry.Snap(0.3, fractionCells: 0.5), Precision);

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(7.0, 0.0)]         // 半刻み未満は0度
    [InlineData(8.0, 15.0)]        // 半刻み超は15度
    [InlineData(-8.0, -15.0)]
    [InlineData(44.0, 45.0)]
    public void SnapAngleDeg_RoundsToFifteenDegrees(double input, double expected)
        => Assert.Equal(expected, PartShapeGeometry.SnapAngleDeg(input), Precision);

    // ===== IsDegenerate（閾値ガードを外すとRED） =====

    [Fact]
    public void IsDegenerate_ZeroLengthLine_ReturnsTrue()
        => Assert.True(PartShapeGeometry.IsDegenerate(new PartLine(2, 3, 2, 3)));

    [Fact]
    public void IsDegenerate_NormalLine_ReturnsFalse()
        => Assert.False(PartShapeGeometry.IsDegenerate(new PartLine(2, 3, 2, 4)));

    [Theory]
    [InlineData(0.0, 2.0, true)]    // 幅ゼロ
    [InlineData(2.0, 0.0, true)]    // 高さゼロ
    [InlineData(2.0, 2.0, false)]
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
        Assert.Equal(2.0, r.Y, Precision);
        Assert.Equal(2.0, r.W, Precision);
        Assert.Equal(3.0, r.H, Precision);
    }

    // ===== BuildCircle / BuildArc =====

    [Fact]
    public void BuildCircle_RadiusIsDistanceFromCenterToEdgePoint()
    {
        var c = PartShapeGeometry.BuildCircle(0, 0, 3, 4);

        Assert.Equal(0.0, c.Cx, Precision);
        Assert.Equal(0.0, c.Cy, Precision);
        Assert.Equal(5.0, c.R, Precision);
    }

    [Fact]
    public void BuildArc_FromBoundingBox_IsHalfEllipse()
    {
        var a = PartShapeGeometry.BuildArc(0, 0, 4, 2);

        Assert.Equal(2.0, a.Cx, Precision);
        Assert.Equal(1.0, a.Cy, Precision);
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

    [Theory]
    [InlineData(0.0, 7.0, 2.0)]     // 円の外側
    [InlineData(0.0, 0.0, 5.0)]     // 中心は輪郭から半径分だけ離れている（塗りではなく輪郭で測る）
    [InlineData(5.0, 0.0, 0.0)]     // 輪郭上
    public void DistanceToPrimitive_Circle_MeasuresToOutline(double x, double y, double expected)
        => Assert.Equal(expected, PartShapeGeometry.DistanceToPrimitive(new PartCircle(0, 0, 5), x, y), Precision);

    [Theory]
    [InlineData(5.0, 2.0, 2.0)]     // 内側（最も近い辺まで）
    [InlineData(-3.0, 2.0, 3.0)]    // 左外側
    [InlineData(0.0, 0.0, 0.0)]     // 角の上
    [InlineData(13.0, 8.0, 5.0)]    // 斜め外側（3-4-5）
    public void DistanceToPrimitive_Rect_HandlesInsideAndOutside(double x, double y, double expected)
        => Assert.Equal(expected, PartShapeGeometry.DistanceToPrimitive(new PartRect(0, 0, 10, 4), x, y), Precision);

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
        var pl = new PartPolyline(new double[] { 0, 0, 10, 0, 10, 10 });

        Assert.Equal(2.0, PartShapeGeometry.DistanceToPrimitive(pl, 5, 2), Precision);   // 1本目の真横
        Assert.Equal(3.0, PartShapeGeometry.DistanceToPrimitive(pl, 7, 5), Precision);   // 2本目の真横
    }

    [Fact]
    public void DistanceToPrimitive_Arc_ApproximatesOutline()
    {
        // 半径2の半円弧（StartDeg=180・SweepDeg=180、+y下ゆえ上半分）。頂点(0,-2)の近くで測る。
        var arc = new PartArc(0, 0, 2, 180, 180);

        Assert.True(PartShapeGeometry.DistanceToPrimitive(arc, 0, -2) < 0.05);
        Assert.True(PartShapeGeometry.DistanceToPrimitive(arc, 0, 0) > 1.9);   // 弦の中点は輪郭から遠い
    }

    [Fact]
    public void DistanceToPrimitive_Text_MeasuresToAnchorPoint()
        => Assert.Equal(5.0, PartShapeGeometry.DistanceToPrimitive(new PartText("あ", 0, 0), 3, 4), Precision);

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

    // ===== Translate =====

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
        var moved = (PartPolyline)PartShapeGeometry.Translate(new PartPolyline(new double[] { 0, 0, 2, 2 }), 1, 1);

        Assert.Equal(new double[] { 1, 1, 3, 3 }, moved.Points);
    }

    [Fact]
    public void Translate_Rect_MovesOriginKeepsSize()
    {
        var moved = (PartRect)PartShapeGeometry.Translate(new PartRect(1, 1, 4, 2, Rot: 30), 2, 3);

        Assert.Equal(3.0, moved.X, Precision);
        Assert.Equal(4.0, moved.Y, Precision);
        Assert.Equal(4.0, moved.W, Precision);
        Assert.Equal(2.0, moved.H, Precision);
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
        var rotated = (PartLine)PartShapeGeometry.Rotate(new PartLine(0, 0, 2, 0), 0, 0, 90);

        Assert.Equal(0.0, rotated.X2, Precision);
        Assert.Equal(2.0, rotated.Y2, Precision);   // +y下ゆえ90度で真下へ回る
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
        var rotated = (PartPolyline)PartShapeGeometry.Rotate(new PartPolyline(new double[] { 1, 0, 2, 0 }), 0, 0, 90);

        Assert.Equal(0.0, rotated.Points[0], Precision);
        Assert.Equal(1.0, rotated.Points[1], Precision);
        Assert.Equal(0.0, rotated.Points[2], Precision);
        Assert.Equal(2.0, rotated.Points[3], Precision);
    }

    [Theory]
    [InlineData(90.0, 0.0, 1.0)]
    [InlineData(180.0, -1.0, 0.0)]
    [InlineData(270.0, 0.0, -1.0)]
    [InlineData(360.0, 1.0, 0.0)]
    public void RotatePoint_AboutOrigin_TurnsClockwiseInScreenCoords(double deg, double expectedX, double expectedY)
    {
        var (x, y) = PartShapeGeometry.RotatePoint(1, 0, 0, 0, deg);

        Assert.Equal(expectedX, x, Precision);
        Assert.Equal(expectedY, y, Precision);
    }

    [Fact]
    public void RotatePoint_AboutNonOriginCenter_KeepsCenterFixed()
    {
        var (x, y) = PartShapeGeometry.RotatePoint(5, 5, 5, 5, 123.4);

        Assert.Equal(5.0, x, Precision);
        Assert.Equal(5.0, y, Precision);
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
