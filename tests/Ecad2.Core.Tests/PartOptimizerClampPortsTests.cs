using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-068増分3-c（殿裁定2026-07-25＝UI/UX仮決定12点目）: <see cref="PartOptimizer.ClampPortsToFrame"/> の
/// 回帰テスト。忍者が実機確認中に発見した「heightCells=1のパーツにRowOffset=-2が保存されていた」事象
/// （枠を縮めても既存の接続点が追従しない）への対処を、保存時の正規化として守る。
///
/// 編集中はGuiEcad原本と同じくクランプしない方式ゆえ、本関数が範囲外値を食い止める唯一の関所となる。
///
/// 入力値の選び方: `samurai.md`「テスト入力の対称性・退化性チェック」に従う。
/// - 行・境界の許容範囲が異なる（行=±(H-1)の対称範囲、境界=[0,W]の非対称範囲）ため、
///   RowOffsetとBoundaryOffsetは常に異なる値を与え、両方を検証する。
///   同値・同範囲だと <see cref="PartShapeGeometry.ClampPort"/> のcellX/cellY引数の取り違えを見逃す。
/// - 高さは極力 heightCells>=2（rowLimit>=1）を使う。heightCells=1はrowLimit=0の退化設定で、
///   行の許容範囲が{0}のみに潰れ、上記の取り違えが<em>行側では</em>期待値と偶然一致してしまう。
///   退化ケース自体の確認は専用のテストで別に持つ。
/// </summary>
public class PartOptimizerClampPortsTests
{
    [Fact]
    public void ClampPortsToFrame_RowOffsetBelowRange_IsClampedToLowerLimit()
    {
        var result = PartOptimizer.ClampPortsToFrame(new[] { new PortDef("P1", -3, 1) }, widthCells: 4, heightCells: 2);

        var port = Assert.Single(result);
        Assert.Equal(-1, port.RowOffset);      // rowLimit = 2-1 = 1
        Assert.Equal(1, port.BoundaryOffset);  // 範囲内ゆえ不変
    }

    [Fact]
    public void ClampPortsToFrame_RowOffsetAboveRange_IsClampedToUpperLimit()
    {
        var result = PartOptimizer.ClampPortsToFrame(new[] { new PortDef("P1", 5, 2) }, widthCells: 4, heightCells: 3);

        var port = Assert.Single(result);
        Assert.Equal(2, port.RowOffset);       // rowLimit = 3-1 = 2
        Assert.Equal(2, port.BoundaryOffset);
    }

    [Fact]
    public void ClampPortsToFrame_BoundaryOffsetAboveWidth_IsClampedToWidth()
    {
        var result = PartOptimizer.ClampPortsToFrame(new[] { new PortDef("P1", 1, 7) }, widthCells: 3, heightCells: 2);

        var port = Assert.Single(result);
        Assert.Equal(1, port.RowOffset);
        Assert.Equal(3, port.BoundaryOffset);  // 上限=widthCells（境界は幅と同数まで取れる）
    }

    [Fact]
    public void ClampPortsToFrame_NegativeBoundaryOffset_IsClampedToZero()
    {
        var result = PartOptimizer.ClampPortsToFrame(new[] { new PortDef("P1", 1, -4) }, widthCells: 3, heightCells: 3);

        var port = Assert.Single(result);
        Assert.Equal(1, port.RowOffset);
        Assert.Equal(0, port.BoundaryOffset);
    }

    /// <summary>忍者が実機で発見した実例そのもの（heightCells=1のパーツにRowOffset=-2）。
    /// rowLimit=0の退化設定ゆえ<em>行側の検証は無力化される</em>——引数を取り違えても
    /// RowOffsetは期待値0と偶然一致してしまう。ただしBoundaryOffsetを併せて検証しているため
    /// 検出自体は効く（引数取り違えの実測で本テストもREDになった）。</summary>
    [Fact]
    public void ClampPortsToFrame_HeightOne_AllowsOnlyRowZero()
    {
        var result = PartOptimizer.ClampPortsToFrame(new[] { new PortDef("P1", -2, 1) }, widthCells: 3, heightCells: 1);

        var port = Assert.Single(result);
        Assert.Equal(0, port.RowOffset);
        Assert.Equal(1, port.BoundaryOffset);
    }

    [Fact]
    public void ClampPortsToFrame_InRangePorts_AreLeftUntouched()
    {
        var ports = new[] { new PortDef("P1", -1, 0), new PortDef("P2", 2, 4) };

        var result = PartOptimizer.ClampPortsToFrame(ports, widthCells: 4, heightCells: 3);

        Assert.Equal(ports, result);
    }

    /// <summary>元のリストを書き換えないこと（<see cref="PartOptimizer.MergeCollinearLines"/> と同じ流儀＝
    /// 保存直前のみ適用し編集中の実体は不変、が成り立つ前提）。</summary>
    [Fact]
    public void ClampPortsToFrame_DoesNotMutateInputList()
    {
        var ports = new List<PortDef> { new("P1", -3, 7) };

        PartOptimizer.ClampPortsToFrame(ports, widthCells: 3, heightCells: 2);

        Assert.Equal(-3, ports[0].RowOffset);
        Assert.Equal(7, ports[0].BoundaryOffset);
    }

    /// <summary>
    /// 保存経路（<c>PartEditorDialog.OkButton_Click</c>）は「クランプ→BoundaryOffset昇順の並べ替え」の
    /// 順で処理する。この順序自体に意味があることを守るテスト。
    ///
    /// クランプは複数の接続点を同一のBoundaryOffsetへ潰しうる（<see cref="Math.Clamp(int,int,int)"/> は
    /// 単調非減少ゆえ大小関係自体は保たれるが、同値への収束は起きる）。<c>OrderBy</c>は安定ソートゆえ
    /// 同値どうしの並びは入力順のまま残るが、その「入力順」がクランプの前か後かで変わる。
    /// 並べ替えは先頭=NetA・末尾=NetBの規約を作る処理ゆえ、順序が入れ替わればどちらの接続点が
    /// NetAになるかという電気的意味が変わってしまう。
    ///
    /// 検出力の実測: 本テストの処理順を逆（並べ替え→クランプ）にすると結果が["B","A"]となりREDになる。
    /// </summary>
    [Fact]
    public void ClampBeforeOrderBy_PortsCollapsingToSameBoundary_KeepsCanvasOrder()
    {
        // 幅2に対しどちらも範囲外。クランプ後は両方がBoundaryOffset=2へ潰れる。
        // 行は範囲内かつ非対称な値を選び、引数取り違えが混ざらないようにする。
        var ports = new[] { new PortDef("A", 1, 5), new PortDef("B", -1, 3) };

        var result = PartOptimizer.ClampPortsToFrame(ports, widthCells: 2, heightCells: 2)
            .OrderBy(p => p.BoundaryOffset).ToList();

        Assert.Equal(new[] { "A", "B" }, result.Select(p => p.Name));
        Assert.All(result, p => Assert.Equal(2, p.BoundaryOffset));
        Assert.Equal(1, result[0].RowOffset);
        Assert.Equal(-1, result[1].RowOffset);
    }

    [Fact]
    public void ClampPortsToFrame_PreservesPortNameAndOrder()
    {
        var result = PartOptimizer.ClampPortsToFrame(
            new[] { new PortDef("NetA", -3, 9), new PortDef("NetB", 4, -2) }, widthCells: 2, heightCells: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("NetA", result[0].Name);
        Assert.Equal(-1, result[0].RowOffset);
        Assert.Equal(2, result[0].BoundaryOffset);
        Assert.Equal("NetB", result[1].Name);
        Assert.Equal(1, result[1].RowOffset);
        Assert.Equal(0, result[1].BoundaryOffset);
    }
}
