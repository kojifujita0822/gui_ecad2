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

    // P-148（殿裁定2026-07-28）で行方向の半径が ±h/2 から ±((h-1)+0.5) セルへ広がった。
    // 高さ1は変わらず（±0.5）、高さ2以上で広がる。期待値は下記のとおり改めてある。
    [Theory]
    [InlineData(3, 5, 2.5, 7.5, 22.5, -11.25)]  // 幅<高さ。半径=(5-1+0.5)*2.5=11.25
    [InlineData(5, 3, 2.0, 10.0, 10.0, -5.0)]   // 幅>高さ（上と逆にして取り違えを炙る）。半径=(3-1+0.5)*2.0=5.0
    [InlineData(1, 4, 3.0, 3.0, 21.0, -10.5)]   // 幅1（列方向の退化）。半径=(4-1+0.5)*3.0=10.5
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
        // 高さ0（退化ケース）。P-148で式が ±((h-1)+0.5) になったため、素直に計算すると
        // 半径が負（-0.5セル）になり矩形が反転する。ClampPort の rowLimit と同じく
        // Math.Max で0段へ潰し、高さ1と同じ1セル分の枠を返すことを固定する。
        var (x, y, w, h) = PartShapeGeometry.FrameRect(widthCells: 3, heightCells: 0, cellMm: 9.0);

        Assert.Equal(0.0, x, Precision);
        Assert.Equal(-4.5, y, Precision);
        Assert.Equal(27.0, w, Precision);
        Assert.Equal(9.0, h, Precision);
        Assert.True(h > 0, "枠の高さが負や0になってはならぬ（矩形の反転・消失を防ぐ）");
    }

    [Fact]
    public void FrameRect_HeightOne_P148の前後で変わらない()
    {
        // 殿がP-148を裁可された際の材料＝「既存18件への影響なし（組込み15件は高さ1）」。
        // 素直な ±(h-1) を採ると高さ1で枠が ±0 となり15件すべてで枠が消えるため、
        // +0.5 を含む形が選ばれた。<b>その前提をここで固定する</b>——
        // 将来 +0.5 を落とす変更が入れば、このテストが鳴る。
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells: 4, heightCells: 1, cellMm: 9.0);

        Assert.Equal(-4.5, y, Precision);   // P-148以前と同値
        Assert.Equal(9.0, h, Precision);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(12)]   // GuiEcadのHeightBoxが許す上限（Math.Clamp(...,1,12)）
    public void FrameRect_接続点の可動範囲を必ず覆う(int heightCells)
    {
        // P-148の本題。枠・接続点の可動範囲・メイン図面の占有範囲（殿裁定11=H-2）の3者が
        // 揃うことを、枠と接続点の2者について直接測る。
        // 接続点は行の中心に描かれる（PartEditorCanvas の CellToLocalMm(x, RowOffset)）。
        const double cellMm = 9.0;
        const int widthCells = 4;
        var (_, y, _, h) = PartShapeGeometry.FrameRect(widthCells, heightCells, cellMm);

        // ClampPort が返しうる上端・下端の RowOffset（枠外を大きく叩いてクランプさせる）
        var (bottomRow, _) = PartShapeGeometry.ClampPort(0, 999, widthCells, heightCells);
        var (topRow, _) = PartShapeGeometry.ClampPort(0, -999, widthCells, heightCells);

        Assert.InRange(topRow * cellMm, y, y + h);
        Assert.InRange(bottomRow * cellMm, y, y + h);
        // 端の接続点は枠の内側に「半セル分」の余裕を持つ（線上に重ならぬ）。
        Assert.True(topRow * cellMm - y >= cellMm / 2 - 1e-9, "上端の接続点が枠の外か線上にある");
        Assert.True(y + h - bottomRow * cellMm >= cellMm / 2 - 1e-9, "下端の接続点が枠の外か線上にある");
    }

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
