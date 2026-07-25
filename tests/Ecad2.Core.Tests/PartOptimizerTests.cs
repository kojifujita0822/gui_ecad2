using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-068増分3-b3（家老追加采配2026-07-25）: <see cref="PartOptimizer.MergeCollinearLines"/> の
/// 回帰テスト。本関数はT-007でGuiEcadから移植されて以来テストが無かったが、増分3-b3で
/// パーツ保存経路へ組み込んだ（裁可7）ため、保存結果を左右する関数として守りを付ける。
/// 対象は MergeCollinearLines のみ（家老采配、スコープ膨張の防止）。
///
/// 入力値の選び方: `samurai.md`「テスト入力の対称性・退化性チェック」に従い、線は**斜め**を使う。
/// 水平・垂直の線では方向ベクトルの成分の片方が0になり、平行判定（外積 adx*bdy - ady*bdx）の
/// 項を取り違えても結果が変わらず、検出力が消えるため。座標もx≠yとして成分の取り違えを拾う。
///
/// RED証明は「実装のガードを一時的に壊すとREDになる」実測で代替する（切り出しでなく既存関数への
/// 後付けテストのため、旧実装との差分によるRED証明は成立しない）。
/// </summary>
public class PartOptimizerTests
{
    private const int Precision = 9;

    [Fact]
    public void MergeCollinearLines_TwoCollinearSegmentsSharingEnd_BecomesOne()
    {
        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(1, 1, 3, 2),
            new PartLine(3, 2, 5, 3),
        });

        var line = Assert.IsType<PartLine>(Assert.Single(result));
        Assert.Equal(1.0, line.X1, Precision);
        Assert.Equal(1.0, line.Y1, Precision);
        Assert.Equal(5.0, line.X2, Precision);
        Assert.Equal(3.0, line.Y2, Precision);
    }

    [Theory]
    [InlineData(1.0, 1.0, 3.0, 2.0, 3.0, 2.0, 5.0, 3.0)]   // aの終点 = bの始点
    [InlineData(1.0, 1.0, 3.0, 2.0, 5.0, 3.0, 3.0, 2.0)]   // aの終点 = bの終点（bが逆向き）
    [InlineData(3.0, 2.0, 1.0, 1.0, 3.0, 2.0, 5.0, 3.0)]   // aの始点 = bの始点（aが逆向き）
    [InlineData(3.0, 2.0, 1.0, 1.0, 5.0, 3.0, 3.0, 2.0)]   // aの始点 = bの終点（両方逆向き）
    public void MergeCollinearLines_AnyEndpointPairing_KeepsOuterEnds(
        double ax1, double ay1, double ax2, double ay2, double bx1, double by1, double bx2, double by2)
    {
        // 線の向きの組み合わせ4通り（実装の端点照合4分岐）。どの向きでも外側の2端点が残る。
        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(ax1, ay1, ax2, ay2),
            new PartLine(bx1, by1, bx2, by2),
        });

        var line = Assert.IsType<PartLine>(Assert.Single(result));
        var ends = new[] { (line.X1, line.Y1), (line.X2, line.Y2) };
        Assert.Contains((1.0, 1.0), ends);
        Assert.Contains((5.0, 3.0), ends);
    }

    [Fact]
    public void MergeCollinearLines_ParallelButDetached_KeepsBoth()
    {
        // 同一直線上ではあるが端点が触れていない（間が空いている）ものは繋げない
        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(1, 1, 3, 2),
            new PartLine(5, 3, 7, 4),
        });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeCollinearLines_SharingEndButNotParallel_KeepsBoth()
    {
        // 端点は接するが折れ曲がっている（平行でない）ものは繋げない
        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(1, 1, 3, 2),
            new PartLine(3, 2, 5, 2),
        });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeCollinearLines_ThreeSegmentChain_BecomesOne()
    {
        // 1回の走査では2本ずつしか繋がらないため、繰り返し走査されることを確かめる
        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(1, 1, 3, 2),
            new PartLine(3, 2, 5, 3),
            new PartLine(5, 3, 7, 4),
        });

        var line = Assert.IsType<PartLine>(Assert.Single(result));
        Assert.Equal(1.0, line.X1, Precision);
        Assert.Equal(1.0, line.Y1, Precision);
        Assert.Equal(7.0, line.X2, Precision);
        Assert.Equal(4.0, line.Y2, Precision);
    }

    [Theory]
    [InlineData(3.0, 8.0, 5.0, 9.0)]    // X座標だけが a の終点(3,2)と一致し、Y座標は離れている
    [InlineData(9.0, 2.0, 11.0, 3.0)]   // Y座標だけが a の終点(3,2)と一致し、X座標は離れている
    public void MergeCollinearLines_OnlyOneAxisMatches_KeepsBoth(double bx1, double by1, double bx2, double by2)
    {
        // 端点の一致は「XもYも」揃って初めて成立する。方向は平行なので平行判定は通り、
        // 端点の一致判定だけで弾かれる経路を通る。
        // 注: 片方の軸だけが揃うケースを置かないと、一致判定の && を || と取り違えても
        // 全テストが緑のままになる（実測で確認済み。`samurai.md`「テスト入力の対称性・退化性」）。
        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(1, 1, 3, 2),
            new PartLine(bx1, by1, bx2, by2),
        });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeCollinearLines_EndpointsWithinTolerance_AreTreatedAsConnected()
    {
        // 端点の一致判定には許容誤差(1e-5)がある。丸め誤差で僅かにずれた端点は繋ぐ。
        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(1, 1, 3, 2),
            new PartLine(3.000001, 2.000001, 5, 3),
        });

        Assert.Single(result);
    }

    [Fact]
    public void MergeCollinearLines_EndpointsBeyondTolerance_AreNotConnected()
    {
        // 許容誤差を超えるずれは別の点として扱う（同一直線上ではあるので平行判定は通る＝
        // 端点の一致判定だけで弾かれることを確かめる境界ケース）
        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(1, 1, 3, 2),
            new PartLine(3.001, 2.0005, 5, 3),
        });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeCollinearLines_NonLinePrimitives_ArePassedThrough()
    {
        var circle = new PartCircle(9, 8, 2);
        var text = new PartText("あ", 7, 6);

        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            circle,
            new PartLine(1, 1, 3, 2),
            new PartLine(3, 2, 5, 3),
            text,
        });

        Assert.Equal(3, result.Count);
        Assert.Same(circle, result[0]);
        Assert.IsType<PartLine>(result[1]);
        Assert.Same(text, result[2]);
    }

    [Fact]
    public void MergeCollinearLines_MergedLine_StaysAtPositionOfFirstSegment()
    {
        // 描画順への影響を最小にするため、繋いだ結果は1本目があった位置に置かれる
        var rect = new PartRect(9, 8, 2, 3);

        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[]
        {
            new PartLine(1, 1, 3, 2),
            rect,
            new PartLine(3, 2, 5, 3),
        });

        Assert.Equal(2, result.Count);
        Assert.IsType<PartLine>(result[0]);
        Assert.Same(rect, result[1]);
    }

    [Fact]
    public void MergeCollinearLines_Empty_ReturnsEmpty()
        => Assert.Empty(PartOptimizer.MergeCollinearLines(Array.Empty<PartPrimitive>()));

    [Fact]
    public void MergeCollinearLines_SingleLine_IsUnchanged()
    {
        var only = new PartLine(1, 1, 3, 2);

        var result = PartOptimizer.MergeCollinearLines(new PartPrimitive[] { only });

        Assert.Same(only, Assert.Single(result));
    }

    [Fact]
    public void MergeCollinearLines_DoesNotMutateInput()
    {
        var input = new List<PartPrimitive>
        {
            new PartLine(1, 1, 3, 2),
            new PartLine(3, 2, 5, 3),
        };

        PartOptimizer.MergeCollinearLines(input);

        Assert.Equal(2, input.Count);   // 保存直前に掛けても編集中のリストは変わらない前提
    }
}
