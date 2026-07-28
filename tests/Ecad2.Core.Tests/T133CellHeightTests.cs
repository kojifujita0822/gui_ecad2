using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-133増分2「CellHeight新設」の単体テスト。器（<see cref="ElementInstance.CellHeight"/> と
/// <see cref="ElementCatalog.DefaultCellHeight"/>）を置くところまでが本増分の範囲であり、
/// 判定へ通すのは増分3、配置時に既定値を入れるのは増分4である。
///
/// <para><b>【入力値の選び方】主軸に Motor を使う。</b> Motor は 幅3 × 高さ2 で<b>幅と高さが違う</b>ゆえ、
/// 取り違えれば結果に現れる。<b>3極記号は 2×2 で対称</b>にて、幅と高さを入れ替えても値が変わらぬ——
/// これだけで確かめると取り違えが丸ごと消える（samurai.md「テスト入力の対称性・退化性チェック」）。
/// 3極記号は「値が2であること」の確認に留め、取り違えの検出は Motor に負わせる。</para>
/// </summary>
public class T133CellHeightTests
{
    // ===== ElementCatalog.DefaultCellHeight =====

    [Theory]
    [InlineData(ElementKind.Motor, 2)]              // 三相モータ（幅3・高さ2＝非対称）
    [InlineData(ElementKind.Breaker3P, 2)]
    [InlineData(ElementKind.ContactorMain3P, 2)]
    [InlineData(ElementKind.ThermalOverload3P, 2)]
    [InlineData(ElementKind.ContactNO, 1)]
    [InlineData(ElementKind.Coil, 1)]
    [InlineData(ElementKind.SelectSwitch, 1)]       // 幅も1（ノッチ毎の2端子接点、ElementCatalog.cs:9）
    [InlineData(ElementKind.Terminal, 1)]
    public void DefaultCellHeight_種別ごとの既定高さを返す(ElementKind kind, int expected)
    {
        Assert.Equal(expected, ElementCatalog.DefaultCellHeight(kind));
    }

    [Fact]
    public void DefaultCellHeight_Motorは幅と高さが異なる()
    {
        // 幅と高さの取り違えを検出できる唯一の種別。ここが同値になったら、
        // 上の[Theory]は取り違えに対して無力になる（見張りとして置く）。
        Assert.Equal(3, ElementCatalog.DefaultCellWidth(ElementKind.Motor));
        Assert.Equal(2, ElementCatalog.DefaultCellHeight(ElementKind.Motor));
    }

    // ===== ElementInstance.CellHeight =====

    [Fact]
    public void CellHeight_既定は1()
    {
        // 旧ファイル互換の土台。JSONに cellHeight が無ければこの既定値のまま読まれる。
        var element = new ElementInstance();

        Assert.Equal(1, element.CellHeight);
        Assert.Equal(1, element.CellWidth);
    }

    [Fact]
    public void DeepClone_CellHeightを引き継ぐ()
    {
        // 回帰の本命。DeepCloneへの追加が漏れるとUndo/Redoで高さが失われる
        // （samurai.md「新規選択可能状態の横展開チェックリスト」項目8と同型の穴）。
        // 幅3・高さ2と非対称にして、取り違えたクローンも検出できるようにする。
        var original = new ElementInstance
        {
            Kind = ElementKind.Motor,
            Pos = new GridPos(2, 5),
            CellWidth = 3,
            CellHeight = 2,
            DeviceName = "M1",
        };

        var clone = original.DeepClone();

        Assert.Equal(3, clone.CellWidth);
        Assert.Equal(2, clone.CellHeight);
    }

    [Fact]
    public void DeepClone_既定値のままの要素も高さ1を保つ()
    {
        // 退化ケース（1,1）。ここだけで確かめると取り違えが消えるため、上のテストと対で置く。
        var original = new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 3) };

        var clone = original.DeepClone();

        Assert.Equal(1, clone.CellHeight);
    }

    [Fact]
    public void DeepClone_複製後に元を変えてもクローンの高さは動かない()
    {
        // 値型ゆえ当然だが、将来CellHeightが参照型の器（例：範囲を表す構造体）へ変わった際に
        // 共有参照を作り込む変更を検出する網として置く。
        var original = new ElementInstance { CellHeight = 2 };
        var clone = original.DeepClone();

        original.CellHeight = 5;

        Assert.Equal(2, clone.CellHeight);
    }
}
