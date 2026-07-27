using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-126（P-129対処、殿裁可2026-07-27）: 保存時の接続点バリデーション二本立ての回帰テスト。
/// <see cref="PartOptimizer.HasDuplicatePorts"/>（検証A＝完全同一座標の重複）と
/// <see cref="PartOptimizer.AllPortsOnSameBoundary"/>（検証B＝全接続点が同一境界＝左右の縮退）。
///
/// <para>二本立てである理由——一方だけでは穴が残る。検証Aだけでは「境界だけ一致・行は違う」形
/// （B-8）を見逃すが、<c>NetlistBuilder</c> は境界オフセットのみで最左・最右を選ぶため要素の左右が
/// 1点へ縮退し、左右のネットが繋がる（短絡に近い誤結線）。逆に検証Bだけでは「3点中2点だけが
/// 完全一致」（B-5）を見逃す。この対比はグループBの B-5／B-8 が担う。</para>
///
/// <para>ケース番号は隠密のテスト設計書
/// <c>docs/ecad2-t126-duplicate-port-validation-test-design-onmitsu.md</c> に対応する。</para>
///
/// <para>入力値の選び方: <c>samurai.md</c>「テスト入力の対称性・退化性チェック」に従い、
/// <c>RowOffset</c> と <c>BoundaryOffset</c> には常に異なる値を与える（両者が同値だと引数の
/// 取り違えが期待値と偶然一致して検出力を失う）。設計書が対称値を挙げているケース
/// （A-7/A-8/A-9/C-4）は、観点を保ったまま非対称な値へ置き換えてある。</para>
/// </summary>
public class PartOptimizerPortValidationTests
{
    private static List<PortDef> Ports(int[] rows, int[] boundaries)
        => rows.Zip(boundaries, (r, b) => new PortDef($"P{r}_{b}", r, b)).ToList();

    // ===== グループA: 検証A（重複＝完全同一座標）単体 =====

    /// <summary>
    /// 完全同一座標（行・境界とも一致）の対だけを重複とみなす。
    /// A-4／A-5 は片方の軸だけが一致する対で、<c>&amp;&amp;</c> を <c>||</c> と取り違えると
    /// 誤って true になる——過検出の検出が主眼（実測で両ケースとも RED 化を確認済み）。
    /// </summary>
    [Theory]
    [InlineData("A-1 空集合", new int[0], new int[0], false)]
    [InlineData("A-2 単独", new[] { 2 }, new[] { 3 }, false)]
    [InlineData("A-3 完全一致", new[] { 2, 2 }, new[] { 3, 3 }, true)]
    [InlineData("A-4 境界のみ一致(行は違う)", new[] { 2, 5 }, new[] { 3, 3 }, false)]
    [InlineData("A-5 行のみ一致(境界は違う)", new[] { 2, 2 }, new[] { 3, 7 }, false)]
    [InlineData("A-6 両方不一致", new[] { 2, 5 }, new[] { 3, 7 }, false)]
    [InlineData("A-7 3点中2点が完全一致", new[] { 1, 3, 1 }, new[] { 4, 6, 4 }, true)]
    [InlineData("A-8 3点とも不一致", new[] { 1, 2, 3 }, new[] { 4, 6, 8 }, false)]
    [InlineData("A-9 2組の完全一致ペア", new[] { 1, 1, 9, 9 }, new[] { 4, 4, 2, 2 }, true)]
    public void HasDuplicatePorts_DetectsOnlyExactCoordinateDuplicates(
        string caseId, int[] rows, int[] boundaries, bool expected)
    {
        _ = caseId;   // 失敗時にどの設計書ケースかを出力へ出すためだけの引数
        Assert.Equal(expected, PartOptimizer.HasDuplicatePorts(Ports(rows, boundaries)));
    }

    // ===== グループC: 検証B（縮退＝全ポートの境界が同一）単体 =====

    /// <summary>
    /// 「一部が一致」と「全部が一致」の取り違えが本グループの核心。
    /// C-5／C-6 は一部だけが同じ境界に載る正常な形状で、条件を「2点一致」と誤って実装すると
    /// 誤って true になる（過検出）。C-5 は侍が <c>NetlistBuilder.Build</c> を実走させ
    /// 実害なしを確かめた構成そのもの——<c>pl</c>=境界最小のL、<c>pr</c>=境界最大のうち
    /// 厳密不等号ゆえ先着のM、で <c>pl≠pr</c> となり縮退しない。
    /// </summary>
    [Theory]
    [InlineData("C-1 2点・境界一致(行は違う)", new[] { 2, 5 }, new[] { 3, 3 }, true)]
    [InlineData("C-2 2点・完全一致", new[] { 2, 2 }, new[] { 3, 3 }, true)]
    [InlineData("C-3 2点・境界不一致", new[] { 2, 5 }, new[] { 3, 7 }, false)]
    [InlineData("C-4 3点とも境界一致", new[] { 1, -3, 9 }, new[] { 4, 4, 4 }, true)]
    [InlineData("C-5 3点中2点のみ境界一致", new[] { 0, 1, -1 }, new[] { 0, 2, 2 }, false)]
    [InlineData("C-6 4点が2組に分かれる", new[] { 1, 2, 3, 4 }, new[] { 5, 5, 9, 9 }, false)]
    [InlineData("C-7 単独", new[] { 2 }, new[] { 3 }, false)]
    [InlineData("C-8 空集合", new int[0], new int[0], false)]
    public void AllPortsOnSameBoundary_RequiresEveryPortToShareTheBoundary(
        string caseId, int[] rows, int[] boundaries, bool expected)
    {
        _ = caseId;
        Assert.Equal(expected, PartOptimizer.AllPortsOnSameBoundary(Ports(rows, boundaries)));
    }

    // ===== グループB: 統合（クランプ → 検証A・B、保存経路と同じ順序） =====

    /// <summary>B-1: P-129実例の型。高さを縮めて行が収束し、完全同一座標になる（忍者が実機で再現）。</summary>
    [Fact]
    public void B1_HeightShrinkCollapsesRows_RejectedByBothChecks()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 0, -2 }, new[] { 1, 1 }), widthCells: 3, heightCells: 1);

        Assert.Equal(new[] { (0, 1), (0, 1) }, Coords(clamped));
        Assert.True(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.True(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>B-2: 対称形。幅を縮めて境界が収束し、行は元から一致しているため検証Aも発火する。</summary>
    [Fact]
    public void B2_WidthShrinkCollapsesBoundaries_RowsAlreadyEqual_RejectedByBothChecks()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 2, 2 }, new[] { 10, 8 }), widthCells: 2, heightCells: 5);

        Assert.Equal(new[] { (2, 2), (2, 2) }, Coords(clamped));
        Assert.True(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.True(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>B-3: 回帰の要。実運用パーツの型（左右1個ずつ）は拒否されてはならぬ。</summary>
    [Fact]
    public void B3_TypicalTwoTerminalPart_IsAccepted()
    {
        var ports = Ports(new[] { 0, 0 }, new[] { 0, 3 });

        var clamped = PartOptimizer.ClampPortsToFrame(ports, widthCells: 3, heightCells: 1);

        Assert.Equal(ports, clamped);   // クランプで動かぬこと
        Assert.False(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.False(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>
    /// B-4: 枠の変更が絡まずとも、既に同一座標の2点を持つ入力（分岐F＝編集中にドラッグで重ねた状態、
    /// または壊れた既存ファイルの読込）は保存時に拒否されるべき。
    /// </summary>
    [Fact]
    public void B4_AlreadyDuplicatedBeforeClamp_RejectedByBothChecks()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 1, 1 }, new[] { 2, 2 }), widthCells: 5, heightCells: 5);

        Assert.Equal(new[] { (1, 2), (1, 2) }, Coords(clamped));
        Assert.True(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.True(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>
    /// 【B-5・検証Aが唯一の関所となる形】3点中2点だけが完全同一座標へ収束し、残る1点は別の境界に載る。
    /// 全点が同一境界ではないため検証Bは false——検証Aが無ければこの部品は保存できてしまう。
    /// B-8 と対をなし、両検証を併せねば穴が残ることを示す。
    /// </summary>
    [Fact]
    public void B5_PartialCollapseToSameCoordinate_RejectedOnlyByCheckA()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 0, -2, 0 }, new[] { 1, 1, 5 }), widthCells: 6, heightCells: 1);

        Assert.Equal(new[] { (0, 1), (0, 1), (0, 5) }, Coords(clamped));
        Assert.True(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.False(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>B-6: 境界が1つ違うだけの隣接した接続点を誤って弾かぬこと。</summary>
    [Fact]
    public void B6_AdjacentBoundaries_IsAccepted()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 0, 0 }, new[] { 2, 3 }), widthCells: 3, heightCells: 1);

        Assert.False(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.False(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>
    /// B-7: 接続点0個・1個。既存の「接続点2つ以上」検査（<c>PartEditorDialog.OkButton_Click</c>）と
    /// 混線せぬこと。検証Bは家老裁定1によりポート2個未満を対象外とする。
    /// </summary>
    [Theory]
    [InlineData(new int[0], new int[0])]
    [InlineData(new[] { 0 }, new[] { 1 })]
    public void B7_FewerThanTwoPorts_IsAccepted(int[] rows, int[] boundaries)
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(rows, boundaries), widthCells: 3, heightCells: 1);

        Assert.False(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.False(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>
    /// 【B-8・検証Bが唯一の関所となる形】幅を縮めて境界だけが収束し、行は範囲内ゆえ異なる値が残る。
    /// 完全同一座標にはならぬので検証Aは素通りするが、<c>NetlistBuilder</c> 上は左右が1点へ縮退し
    /// 誤結線に至る（侍が実走させて確認）。B-5 と対をなす。
    /// </summary>
    [Fact]
    public void B8_CollapseToSameBoundaryOnly_RejectedOnlyByCheckB()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 1, -1 }, new[] { 10, 8 }), widthCells: 2, heightCells: 5);

        // 行は ±(5-1) の範囲内ゆえ 1 と -1 のまま残り、境界だけが両方 2 へ潰れる。
        Assert.Equal(new[] { (1, 2), (-1, 2) }, Coords(clamped));
        Assert.False(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.True(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>
    /// B-9: 過検出防止の回帰（C-5の統合版）。枠を縮めた後も一部の点だけが同じ境界に載る形は、
    /// 最左・最右が正しく分かれるため実害がなく、拒否してはならぬ。
    /// </summary>
    [Fact]
    public void B9_PartialBoundaryShareAfterClamp_IsAccepted()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 0, 1, -1 }, new[] { 0, 10, 10 }), widthCells: 2, heightCells: 3);

        Assert.Equal(new[] { (0, 0), (1, 2), (-1, 2) }, Coords(clamped));
        Assert.False(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.False(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>
    /// B-10: <see cref="PartRole.NonSimulated"/> も他の役割と同じ扱いとする（家老裁定2＝除外しない）。
    /// 検証A・Bはいずれも接続点の並びだけを見る純粋関数で <c>PartRole</c> を引数に取らぬ——
    /// この設計自体が「役割による分岐を持たぬ」ことの担保になる。
    /// View層（<c>OkButton_Click</c>）でも役割で分岐していないことは隠密の静的レビューで確かめる。
    /// 入力は B-4 と同一だが、確認している事柄が異なるため別ケースとして残す。
    /// </summary>
    [Fact]
    public void B10_NonSimulatedRole_IsNotExemptedFromEitherCheck()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 1, 1 }, new[] { 2, 2 }), widthCells: 5, heightCells: 5);

        Assert.True(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.True(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    // ===== グループE: 境界値分析（クランプの上限・下限ちょうどでの収束） =====

    /// <summary>E-1: 上限ちょうど（境界=幅）と上限+1。後者がクランプされ前者と一致する。</summary>
    [Fact]
    public void E1_BoundaryUpperLimitAndBeyond_CollapseAndAreRejected()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 0, 0 }, new[] { 3, 4 }), widthCells: 3, heightCells: 5);

        Assert.Equal(new[] { (0, 3), (0, 3) }, Coords(clamped));
        Assert.True(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.True(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    /// <summary>E-2: 下限ちょうど（境界=0）と下限-1。後者がクランプされ前者と一致する。</summary>
    [Fact]
    public void E2_BoundaryLowerLimitAndBelow_CollapseAndAreRejected()
    {
        var clamped = PartOptimizer.ClampPortsToFrame(
            Ports(new[] { 1, 1 }, new[] { 0, -1 }), widthCells: 5, heightCells: 5);

        Assert.Equal(new[] { (1, 0), (1, 0) }, Coords(clamped));
        Assert.True(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.True(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    // ===== 実データの回帰（T-126 DoD 4） =====

    /// <summary>
    /// 実運用パーツが拒否されぬこと。2026-07-27時点の実データ15件は侍が全件を機械的に検分し、
    /// 「幅1・高さ1で境界0と1の2端子」14件と「幅3・高さ1で境界0/1/2のモータ」1件の2型のみと確認した
    /// （組込みテンプレート <c>BasicPartTemplates</c>・<c>ElementCatalog</c> も同型。
    /// 実ファイルを読むテストは実ユーザーデータへ触れるため採らず、構成を写して固定する＝P-019の教訓）。
    /// </summary>
    [Theory]
    [InlineData(1, 1, new[] { 0, 0 }, new[] { 0, 1 })]
    [InlineData(3, 1, new[] { 0, 0, 0 }, new[] { 0, 1, 2 })]
    public void RealWorldPartShapes_AreAccepted(
        int widthCells, int heightCells, int[] rows, int[] boundaries)
    {
        var ports = Ports(rows, boundaries);

        var clamped = PartOptimizer.ClampPortsToFrame(ports, widthCells, heightCells);

        Assert.Equal(ports, clamped);
        Assert.False(PartOptimizer.HasDuplicatePorts(clamped));
        Assert.False(PartOptimizer.AllPortsOnSameBoundary(clamped));
    }

    private static (int Row, int Boundary)[] Coords(IEnumerable<PortDef> ports)
        => ports.Select(p => (p.RowOffset, p.BoundaryOffset)).ToArray();
}
