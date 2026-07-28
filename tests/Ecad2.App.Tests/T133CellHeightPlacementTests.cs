using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分3「占有・ヒットテストの縦方向対応」の単体テスト。
/// 殿裁定11＝<b>H-2（中心基準3行）</b>——高さ H の要素は <c>Pos.Row</c> を中心に上下へ <c>H-1</c> 行ずつ、
/// 計 <c>2(H-1)+1</c> 行を占める。H=1 なら1行（従来と同一）、H=2 なら3行。
///
/// <para><b>【入力値の選び方】計画書§増分3の勘所3点を事前適用した。</b>
/// (a) <b>高さ1と高さ2を混ぜる</b>——高さ1どうしだけでは行の区間判定が <c>[r,r]</c> に潰れ、
/// 縦方向の実装が丸ごと誤っていても「正しく見える」。
/// (b) <b>行と列を非対称に</b>——グリッドを <c>Rows=5 / Columns=10</c>、要素も幅1・高さ2 とし、
/// 幅と高さを取り違えれば結果が変わるようにした（幅2・高さ2 では取り違えても同じ値になる）。
/// (c) <b>上下の隣接行それぞれ</b>——片側だけでは符号の取り違え（<c>+</c> と <c>-</c>）が消える。</para>
/// </summary>
public class T133CellHeightPlacementTests : ViewModelTestBase
{
    // 行と列で別の値を選ぶ。行は5行しか無いグリッドの中央付近、列は行と重ならぬ値。
    private const int Rows = 5;
    private const int Columns = 10;
    private static readonly GridPos TallElementPos = new(2, 6);   // 高さ2の要素＝行1,2,3を占める

    /// <summary>Rows=5 / Columns=10 の非対称なグリッドを持つ文書を作る。</summary>
    private MainWindowViewModel ArrangeSmallGrid()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid = new GridSpec { Rows = Rows, Columns = Columns };
        return vm;
    }

    /// <summary>高さ2・幅1の要素を1つ置いた状態を作る（配置経路は増分4ゆえ、ここでは直接足す）。</summary>
    private MainWindowViewModel ArrangeWithTallElement()
    {
        var vm = ArrangeSmallGrid();
        vm.CurrentSheet!.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.ContactNO,
            Pos = TallElementPos,
            CellWidth = 1,
            CellHeight = 2,
            DeviceName = "X001",
        });
        return vm;
    }

    // ==================================================================
    // グリッド範囲判定（置く側の高さ）
    // ==================================================================

    [Theory]
    [InlineData(0, true)]    // 高さ1は上端行にも置ける（従来どおり＝回帰の網）
    [InlineData(2, true)]
    [InlineData(4, true)]    // 高さ1は最終行にも置ける
    public void 高さ1は従来どおり全行に置ける(int row, bool expected)
    {
        var vm = ArrangeSmallGrid();
        vm.SelectedCell = new GridPos(row, 6);

        Assert.Equal(expected, vm.IsSelectedCellWithinGrid(cellWidth: 1, cellHeight: 1));
    }

    [Theory]
    [InlineData(0, false)]   // 上端＝r-1 が -1 になり、はみ出す
    [InlineData(1, true)]    // 行0,1,2 を占める＝収まる（上側の境界ちょうど）
    [InlineData(2, true)]
    [InlineData(3, true)]    // 行2,3,4 を占める＝収まる（下側の境界ちょうど）
    [InlineData(4, false)]   // 下端＝r+1 が Rows になり、はみ出す
    public void 高さ2は上下1行ずつの余地を要する(int row, bool expected)
    {
        // 上下それぞれを測る。片側だけなら符号の取り違えが消える。
        var vm = ArrangeSmallGrid();
        vm.SelectedCell = new GridPos(row, 6);

        Assert.Equal(expected, vm.IsSelectedCellWithinGrid(cellWidth: 1, cellHeight: 2));
    }

    [Fact]
    public void 高さと幅は別の基準で判定される()
    {
        // 列は左上アンカー基準 [c, c+W-1]、行は中心基準 [r-(H-1), r+(H-1)]。
        // 同じ式で扱っていれば、幅3・高さ1 の要素が行方向にもはみ出すと誤判定するはず。
        var vm = ArrangeSmallGrid();
        vm.SelectedCell = new GridPos(0, 7);   // 行は上端、列は右寄り

        Assert.True(vm.IsSelectedCellWithinGrid(cellWidth: 3, cellHeight: 1));   // 列7,8,9＝収まる
        Assert.False(vm.IsSelectedCellWithinGrid(cellWidth: 4, cellHeight: 1));  // 列7〜10＝はみ出す
    }

    // ==================================================================
    // 占有判定（既存要素の高さ・置く側の高さの双方）
    // ==================================================================

    [Theory]
    [InlineData(1, true)]    // 高さ2要素の1行上（上側）
    [InlineData(2, true)]    // 中心行
    [InlineData(3, true)]    // 1行下（下側）
    [InlineData(0, false)]   // 2行上＝届かぬ
    [InlineData(4, false)]   // 2行下＝届かぬ
    public void 高さ2の既存要素は上下1行ずつも占有する(int row, bool expected)
    {
        var vm = ArrangeWithTallElement();
        vm.SelectedCell = new GridPos(row, 6);   // 同じ列

        Assert.Equal(expected, vm.IsSelectedCellOccupied(cellWidth: 1, cellHeight: 1));
    }

    [Fact]
    public void 列が違えば高さ2の要素とは衝突しない()
    {
        // 行だけで判定していれば、列違いでも占有と誤る（行と列の取り違えを炙る）。
        var vm = ArrangeWithTallElement();
        vm.SelectedCell = new GridPos(2, 3);   // 中心行だが列が違う

        Assert.False(vm.IsSelectedCellOccupied(cellWidth: 1, cellHeight: 1));
    }

    [Theory]
    [InlineData(0, true)]    // 置く側が高さ2＝行-1..1 を要し、既存の行1と重なる（上側から寄る）
    [InlineData(4, true)]    // 置く側が高さ2＝行3..5 を要し、既存の行3と重なる（下側から寄る）
    public void 置く側の高さも占有判定に効く(int row, bool expected)
    {
        // 既存要素側だけ高さを見ていると、この2件は「衝突せぬ」と誤判定される。
        var vm = ArrangeWithTallElement();
        vm.SelectedCell = new GridPos(row, 6);

        Assert.Equal(expected, vm.IsSelectedCellOccupied(cellWidth: 1, cellHeight: 2));
        // 対照＝同じ位置でも高さ1なら届かぬ（上のTheoryで実証済みの境界）
        Assert.False(vm.IsSelectedCellOccupied(cellWidth: 1, cellHeight: 1));
    }

    [Fact]
    public void 高さ1どうしは従来どおり同一行のみ衝突する()
    {
        // 回帰の網。既存要素も置く側も高さ1なら [r,r] 同士の一致比較に潰れる。
        var vm = ArrangeSmallGrid();
        vm.CurrentSheet!.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.ContactNO, Pos = new GridPos(2, 6), DeviceName = "X001",
        });

        vm.SelectedCell = new GridPos(2, 6);
        Assert.True(vm.IsSelectedCellOccupied());
        vm.SelectedCell = new GridPos(1, 6);
        Assert.False(vm.IsSelectedCellOccupied());
        vm.SelectedCell = new GridPos(3, 6);
        Assert.False(vm.IsSelectedCellOccupied());
    }

    // ==================================================================
    // ヒットテスト（既存要素の高さ）
    // ==================================================================

    [Theory]
    [InlineData(1, true)]    // 上側
    [InlineData(2, true)]    // 中心
    [InlineData(3, true)]    // 下側
    [InlineData(0, false)]
    [InlineData(4, false)]
    public void 高さ2の要素は上下の行でもヒットする(int row, bool expectHit)
    {
        var vm = ArrangeWithTallElement();

        var hit = vm.HitTestElement(new GridPos(row, 6));

        if (expectHit) Assert.Equal("X001", hit?.DeviceName);
        else Assert.Null(hit);
    }

    [Fact]
    public void 高さ2の要素も列が違えばヒットしない()
    {
        var vm = ArrangeWithTallElement();

        Assert.Null(vm.HitTestElement(new GridPos(2, 5)));
        Assert.Null(vm.HitTestElement(new GridPos(2, 7)));
    }

    [Fact]
    public void 高さ1の要素は中心行だけがヒットする()
    {
        // 回帰の網。
        var vm = ArrangeSmallGrid();
        vm.CurrentSheet!.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.ContactNO, Pos = new GridPos(2, 6), DeviceName = "X001",
        });

        Assert.NotNull(vm.HitTestElement(new GridPos(2, 6)));
        Assert.Null(vm.HitTestElement(new GridPos(1, 6)));
        Assert.Null(vm.HitTestElement(new GridPos(3, 6)));
    }

    // ==================================================================
    // 呼び出し側——述語だけでなく繋ぎ込みも測る
    // （samurai.md「述語を切り出したら呼び出し側にもテストを置く」）
    // ==================================================================

    [Fact]
    public void 要素の移動でも高さが判定に通っている()
    {
        // ValidatePlacement は private ゆえ、MoveSelectedElement 経由で繋ぎ込みを確かめる。
        // 高さ2の要素を、上端に寄せて移動しようとしても収まらぬ。
        var vm = ArrangeWithTallElement();
        vm.SelectedCell = TallElementPos;
        Assert.NotNull(vm.SelectedElement);

        // 行2→行1へ（行0,1,2を占める＝収まる）
        Assert.True(vm.MoveSelectedElement(-1, 0));
        Assert.Equal(new GridPos(1, 6), vm.CurrentSheet!.Elements[0].Pos);

        // さらに1行上へ（行-1を要する＝収まらぬ）。選択は移動先へ追随している前提を確かめてから測る。
        vm.SelectedCell = new GridPos(1, 6);
        Assert.False(vm.MoveSelectedElement(-1, 0));
        Assert.Equal(new GridPos(1, 6), vm.CurrentSheet!.Elements[0].Pos);   // 動いておらぬ
    }
}
