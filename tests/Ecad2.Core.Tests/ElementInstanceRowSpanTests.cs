using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-133増分4（家老裁定2026-07-28）: 占有行範囲の判定を <see cref="ElementInstance"/> へ一元化した
/// ことの単体テスト。同じ式が増分3〜4で4箇所（占有判定・ヒットテスト・行占有判定・行削除）へ
/// 現れ、<c>Math.Max</c> による退化ガードの書き忘れが起きうるため寄せたもの。
///
/// <para><b>【期待値の要】占有行数は「奇数の高さは h 行、偶数の高さは h+1 行」</b>——
/// T-139(C)の殿裁定2026-07-31。中心基準ゆえ上下対称で行数は必ず奇数になり、半径は
/// <c>h/2</c>（整数除算）という一つの式に収まる。高さ2は<b>3行</b>、高さ3も<b>3行</b>、高さ4は<b>5行</b>。</para>
///
/// <para><b>【従前は 2H-1 行（半径 h-1）であった】</b>T-133増分2の殿裁定11「H-2（中心基準）」に由来し、
/// T-139で枠の描画を原本の形（<c>h</c>セル）へ戻したのに伴い改めた。<b>旧仕様では高さ3が5行</b>で
/// あった点に注意——忍者の期待値表 `docs/ecad2-t133-increment4-expected-values-ninja.md` §0 は
/// 旧仕様の記述ゆえ、そのまま引くと期待値が狂う。</para>
///
/// <para><b>【この式を測る者への注意】高さ1・2では新旧が同値</b>（半径0・1）。
/// <b>違いが現れるのは高さ3以上のみ</b>ゆえ、高さ2だけを見て確かめても式の改めは検出できぬ。</para>
///
/// <para><b>【入力値の選び方】</b>要素の位置を <c>(行3, 列7)</c> と<b>非対称</b>に取る。
/// 行と列を取り違える実装であれば結果が変わる（T-125増分α・T-134で、非対称入力が
/// 行と列の取り違えを実際に検出した実績がある）。また<b>上下の両側</b>を測る——
/// 片側だけでは符号の取り違えが消える。</para>
/// </summary>
public class ElementInstanceRowSpanTests
{
    /// <summary>行と列で別の値を選ぶ（取り違えを炙るため）。</summary>
    private static ElementInstance At(int cellHeight)
        => new() { Pos = new GridPos(3, 7), CellHeight = cellHeight };

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]     // 旧仕様では 2。ここが T-139(C) で改まった
    [InlineData(4, 2)]     // 偶数の非境界（h=2 だけでは偶数側が境界に偏る）
    [InlineData(5, 2)]     // 奇数の非境界
    [InlineData(12, 6)]    // 仕様上の上限（原本GuiEcadの HeightBox が Math.Clamp(...,1,12) で許す端）。旧仕様では 11
    public void 高さから求めた行半径は高さの半分になる(int cellHeight, int expectedSpan)
    {
        Assert.Equal(expectedSpan, ElementInstance.RowSpanOf(cellHeight));
        Assert.Equal(expectedSpan, At(cellHeight).RowSpan);
    }

    /// <summary>
    /// 殿裁定の言葉（<b>奇数は h 行、偶数は h+1 行</b>）を、そのままの単位＝<b>行数</b>で固定する。
    /// 上の半径は実装の内部表現ゆえ、裁定の言葉に近い側でも押さえておく。
    /// <para><b>【偶奇という軸を持ち込んだゆえ、境界と非境界の両方を置く】</b>
    /// 奇数側は h=1（境界）と h=3・5、偶数側は h=2（境界）と h=4・12。
    /// 片側が境界のみだと、境界に特有の振る舞いを軸の性質と読み違える
    /// （<c>samurai.md</c>「分類軸を持ち込んだら境界と非境界の両方を確かめよ」）。</para>
    /// <para><b>【集約値ゆえ、これ1つでは足りぬ】</b>行数は位置を潰す。どの行を占めるかは
    /// 下の <c>ContainsRow</c> 群が受け持つ。</para>
    /// </summary>
    [Theory]
    [InlineData(1, 1)]     // 奇数 → h 行
    [InlineData(2, 3)]     // 偶数 → h+1 行
    [InlineData(3, 3)]     // 奇数
    [InlineData(4, 5)]     // 偶数
    [InlineData(5, 5)]     // 奇数
    [InlineData(12, 13)]   // 偶数・仕様上の上限
    public void 占有行数は奇数の高さでh行_偶数の高さでh足す1行になる(int cellHeight, int expectedRows)
    {
        Assert.Equal(expectedRows, 2 * ElementInstance.RowSpanOf(cellHeight) + 1);
    }

    /// <summary>
    /// <b>【T-139(C)で境界が動いた 2026-08-01】</b>旧式 <c>h-1</c> ではガードは <c>h≦0</c> のすべてで
    /// 効いておったが、新式 <c>h/2</c> は<b>整数除算が0方向へ丸める</b>ゆえ <c>h=0</c>・<c>h=-1</c> は
    /// ガード無しでも0になる。<b>負に振れる最初の入力は <c>h=-2</c></b>（<c>-2/2 = -1</c>）——
    /// すなわち<b>ガードが効かなくなったのではなく、効く要のある範囲が狭まった</b>（隠密の裏づけ）。
    /// <para><b>ゆえに新しい境界そのもの（-2）を突く。</b> 従前は -1 と -3 のみを持っており、
    /// <b>-3 が実害を防いではおったが、境界ちょうどは測れておらなんだ</b>（隠密の静的レビュー采配）。</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2)]   // 新しい境界ちょうど。ガード無しなら -1 になる最初の入力
    [InlineData(-3)]
    public void 高さ0以下の退化入力は0段へ潰れる(int degenerateHeight)
    {
        // 負のまま扱うと、行の判定が常にfalseになる（占有もヒットもせぬ「幽霊要素」）か、
        // 逆に範囲判定が緩む。0段＝高さ1と同じ扱いへ潰すのが正。
        Assert.Equal(0, ElementInstance.RowSpanOf(degenerateHeight));
        Assert.Equal(0, At(degenerateHeight).RowSpan);
    }

    [Theory]
    [InlineData(2, false)]   // 1行上＝届かぬ
    [InlineData(3, true)]    // アンカー
    [InlineData(4, false)]   // 1行下＝届かぬ
    public void 高さ1はアンカー行だけを占める(int row, bool expected)
        => Assert.Equal(expected, At(1).ContainsRow(row));

    [Theory]
    [InlineData(1, false)]   // 2行上＝届かぬ
    [InlineData(2, true)]    // 1行上（上側）
    [InlineData(3, true)]    // アンカー
    [InlineData(4, true)]    // 1行下（下側）
    [InlineData(5, false)]   // 2行下＝届かぬ
    public void 高さ2は上下1行ずつを含めた3行を占める(int row, bool expected)
    {
        // 上下の両側を測る。片側だけなら符号の取り違え（+と-）が消える。
        Assert.Equal(expected, At(2).ContainsRow(row));
    }

    [Theory]
    [InlineData(1, false)]   // 2行上＝届かぬ（旧仕様では届いておった）
    [InlineData(2, true)]    // 1行上
    [InlineData(3, true)]    // アンカー
    [InlineData(4, true)]    // 1行下
    [InlineData(5, false)]   // 2行下＝届かぬ（旧仕様では届いておった）
    public void 高さ3は上下1行ずつを含めた3行を占める(int row, bool expected)
        => Assert.Equal(expected, At(3).ContainsRow(row));

    /// <summary>偶数の非境界（h=4）。<b>位置</b>を測る側で偶奇の軸を押さえる——
    /// 上の行数テストは集約値ゆえ、上下どちらかへ寄った実装を検出できぬ。</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]    // 2行上
    [InlineData(3, true)]    // アンカー
    [InlineData(5, true)]    // 2行下
    [InlineData(6, false)]
    public void 高さ4は上下2行ずつを含めた5行を占める(int row, bool expected)
        => Assert.Equal(expected, At(4).ContainsRow(row));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void 退化入力でもアンカー行は必ず含む(int degenerateHeight)
    {
        // 「幽霊要素」（占有もヒットもせぬ＝選択も削除もできぬ）にならぬことの網。
        Assert.True(At(degenerateHeight).ContainsRow(3));
        Assert.False(At(degenerateHeight).ContainsRow(2));
        Assert.False(At(degenerateHeight).ContainsRow(4));
    }

    [Theory]
    [InlineData(0, 1, false)]   // 区間が上に離れておる
    [InlineData(0, 2, true)]    // 下端が占有の上端（行2）に触れる
    [InlineData(3, 3, true)]    // アンカーのみの区間
    [InlineData(4, 9, true)]    // 上端が占有の下端（行4）に触れる
    [InlineData(5, 9, false)]   // 区間が下に離れておる
    public void 高さ2の占有範囲と行区間の交差を判定できる(int topRow, int bottomRow, bool expected)
    {
        // 境界ちょうど（触れる）を両側とも測る。片側だけでは不等号の向きの誤りが残る。
        Assert.Equal(expected, At(2).OverlapsRows(topRow, bottomRow));
    }

    /// <summary>
    /// <b>殿裁定2026-07-31(C)の「根拠」を固定する網</b>（<c>samurai.md</c>「裁定そのものでなく、
    /// 裁定の根拠を回帰テストの対象にする」）。裁定の材料は<b>「3極記号(V)が触れる行帯は3本であり、
    /// 偶数の h+1 行と数として一致する」</b>という忍者・隠密の調査であった。
    /// <para>
    /// ゆえに固定するのは <c>RowSpanOf</c> の式だけではなく、<b>「3極記号の高さは2」という
    /// <see cref="ElementCatalog"/> 側の宣言と、そこから導かれる占有行数3の結線</b>である——
    /// カタログ側の高さが将来変われば、裁定の前提そのものが崩れる。
    /// </para>
    /// <para>
    /// <b>【鳴らぬのが正しい網である】</b>本テストは今回の改め（<c>h-1</c> → <c>h/2</c>）では
    /// RED にならぬ。高さ2は新旧いずれも半径1ゆえ——それを承知のうえで、崩れうる前提の側に置く。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(ElementKind.Breaker3P)]
    [InlineData(ElementKind.ContactorMain3P)]
    [InlineData(ElementKind.ThermalOverload3P)]
    public void T139C_組込み3極記号は3行を占める(ElementKind kind)
    {
        int cellHeight = ElementCatalog.DefaultCellHeight(kind);

        Assert.Equal(2, cellHeight);                                        // 裁定の材料＝高さ2
        Assert.Equal(3, 2 * ElementInstance.RowSpanOf(cellHeight) + 1);      // ゆえに3行（＝h+1）
    }

    [Fact]
    public void 行の判定は列に影響されない()
    {
        // 位置を(行3,列7)と非対称に取ってあるゆえ、行と列を取り違える実装なら結果が変わる。
        var el = At(2);
        Assert.True(el.ContainsRow(3));
        Assert.False(el.ContainsRow(7));   // 列の値を行として渡しても含まぬ
    }
}
