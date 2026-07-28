using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-133増分4（家老裁定2026-07-28）: 占有行範囲の判定を <see cref="ElementInstance"/> へ一元化した
/// ことの単体テスト。同じ式が増分3〜4で4箇所（占有判定・ヒットテスト・行占有判定・行削除）へ
/// 現れ、<c>Math.Max</c> による退化ガードの書き忘れが起きうるため寄せたもの。
///
/// <para><b>【期待値の要】占有行数は 2H-1 である</b>——殿裁定11＝H-2（中心基準）ゆえ、
/// 高さ2は<b>3行</b>、高さ3は<b>5行</b>を占める。「高さ2＝2行」と読めば期待値が狂う
/// （忍者の期待値表 `docs/ecad2-t133-increment4-expected-values-ninja.md` §0）。</para>
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
    [InlineData(3, 2)]
    [InlineData(12, 11)]   // 仕様上の上限（原本GuiEcadの HeightBox が Math.Clamp(...,1,12) で許す端）
    public void 高さから求めた行半径はH引く1になる(int cellHeight, int expectedSpan)
    {
        Assert.Equal(expectedSpan, ElementInstance.RowSpanOf(cellHeight));
        Assert.Equal(expectedSpan, At(cellHeight).RowSpan);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
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
    [InlineData(0, false)]
    [InlineData(1, true)]    // 2行上
    [InlineData(3, true)]    // アンカー
    [InlineData(5, true)]    // 2行下
    [InlineData(6, false)]
    public void 高さ3は上下2行ずつを含めた5行を占める(int row, bool expected)
        => Assert.Equal(expected, At(3).ContainsRow(row));

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

    [Fact]
    public void 行の判定は列に影響されない()
    {
        // 位置を(行3,列7)と非対称に取ってあるゆえ、行と列を取り違える実装なら結果が変わる。
        var el = At(2);
        Assert.True(el.ContainsRow(3));
        Assert.False(el.ContainsRow(7));   // 列の値を行として渡しても含まぬ
    }
}
