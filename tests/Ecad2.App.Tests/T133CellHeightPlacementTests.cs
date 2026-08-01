using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分3「占有・ヒットテストの縦方向対応」の単体テスト。
/// 殿裁定11＝<b>H-2（中心基準3行）</b>——高さ H の要素は <c>Pos.Row</c> を中心に上下へ <c>H-1</c> 行ずつ、
/// 計 <c>2(H-1)+1</c> 行を占める。H=1 なら1行（従来と同一）、H=2 なら3行。
///
/// <para><b>【T-139(C)で式が改まった 2026-08-01】</b>殿裁定2026-07-31により
/// <b>奇数は <c>h</c> 行、偶数は <c>h+1</c> 行</b>（半径 <c>h/2</c>）となった。<b>直上の
/// 「計 2(H-1)+1 行」は旧仕様の記述である</b>——高さ1・2は新旧同値ゆえ本ファイルの既存テストは
/// 影響を受けぬが、<b>高さ3以上を扱う際にそのまま引くと期待値が狂う</b>。</para>
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

    /// <summary>
    /// T-139(C)の網（殿裁定2026-07-31＝奇数は <c>h</c> 行、偶数は <c>h+1</c> 行）。
    /// <b>高さ3は3行を占める</b>ゆえ、必要な余地は高さ2と同じ上下1行ずつになる（旧仕様では5行）。
    /// <para><b>【なぜApp層にも置くか】</b>本ファイルの既存テストは<b>高さ1・2しか使うておらず</b>、
    /// T-139(C)で式を <c>h-1</c> → <c>h/2</c> へ改めた際、<b>旧式へ戻す壊す実測でApp層は0件RED</b>で
    /// あった——高さ1・2は<b>新旧同値</b>ゆえ。結線（<c>IsWithinGridBounds</c> が <c>RowSpanOf</c> を
    /// 通ること）は高さ2でも測れるが、<b>式の改めそのものはこの層で一つも守られておらなんだ。</b>
    /// 「テストは在り、経路も通っておるのに、与える入力値の選び方だけが検出力を奪う」型にござる。</para>
    /// <para><b>【検出力の所在】</b>鳴るのは行1・行3のケースのみ。行0・行4は<b>両仕様とも false</b>で
    /// あり対照として置く（枷が効きすぎて全部 false になっても気づけるように）。</para>
    /// </summary>
    [Theory]
    [InlineData(0, false)]   // 行-1 へはみ出す（旧仕様でも false ＝対照）
    [InlineData(1, true)]    // 行0,1,2 を占める＝収まる（旧仕様なら行-1まで要り false）
    [InlineData(3, true)]    // 行2,3,4 を占める＝収まる（旧仕様なら行5まで要り false）
    [InlineData(4, false)]   // 行5 へはみ出す（旧仕様でも false ＝対照）
    public void 高さ3が要する余地は高さ2と同じ上下1行ずつ(int row, bool expected)
    {
        var vm = ArrangeSmallGrid();
        vm.SelectedCell = new GridPos(row, 6);

        Assert.Equal(expected, vm.IsSelectedCellWithinGrid(cellWidth: 1, cellHeight: 3));
    }

    [Fact]
    public void 高さと幅は別の基準で判定される()
    {
        // 列は左上アンカー基準 [c, c+W-1]、行は中心基準 [r-(H-1), r+(H-1)]。
        // 【射程】本テストが炙るのは「幅を行の判定にも使う」型の取り違えのみである
        // (隠密レビュー所見8、2026-07-28)。行を左上アンカー基準 [r, r+H-1] と取り違える型は
        // H=1 のとき [r,r] へ潰れるゆえ、ここでは鳴らぬ——そちらは上の高さ2のTheoryが受け持つ。
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

        // 選択は移動先へ追随している(MainWindowViewModel.MoveSelectedElement の SelectedCell = newPos)。
        // 以前はここで代入していたが、それでは「追随している前提」を確かめたことにならぬため
        // Assert へ改めた(隠密レビュー所見9、2026-07-28)。追随が壊れれば本行が鳴る。
        Assert.Equal(new GridPos(1, 6), vm.SelectedCell);

        // さらに1行上へ（行-1を要する＝収まらぬ）。
        Assert.False(vm.MoveSelectedElement(-1, 0));
        Assert.Equal(new GridPos(1, 6), vm.CurrentSheet!.Elements[0].Pos);   // 動いておらぬ
    }

    // ==================================================================
    // 行削除と高さ（増分4、殿裁定2026-07-28＝(D-1) 占有範囲にかかれば削除）
    //
    // 隠密の死角調査「漏れ1（IsRowOccupied）・漏れ2（RowOps.DeleteRow）」——いずれも
    // ElementInstance だけが行の一致比較であった。忍者が実機側から独立に同じ領域を
    // 「誰も測っておらぬ交差」として挙げており、采配の前にWチェックが成立しておる。
    // ==================================================================

    /// <summary>行削除を試すための器（10行取る。要素は行と列を非対称に置く）。</summary>
    private MainWindowViewModel ArrangeForRowDelete(int anchorRow, int cellHeight)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.ContactNO,
            Pos = new GridPos(anchorRow, 7),
            CellHeight = cellHeight,
            DeviceName = "X001",
        });
        return vm;
    }

    [Theory]
    [InlineData(3)]   // 真上の行
    [InlineData(4)]   // アンカー行
    [InlineData(5)]   // 真下の行
    public void 高さ2の要素は占有行のどれを削除しても消える(int targetRow)
    {
        // 殿裁定(D-1)。アンカー行の一致だけを見る実装なら、真上・真下の削除で要素が消えず
        // -1シフトして残る——「画面に描かれておるのに消えぬ」食い違いが出る。
        var vm = ArrangeForRowDelete(anchorRow: 4, cellHeight: 2);

        vm.DeleteRowAtCommand.Execute(targetRow);

        Assert.Empty(vm.CurrentSheet!.Elements);
        // 機器表の掃除も既存経路（削除されたElementInstanceの一覧を辿る）に乗る。
        Assert.False(vm.Document.Devices.ByName.ContainsKey("X001"));
    }

    [Theory]
    [InlineData(2, 3)]   // 2行上＝占有外。削除行より下ゆえ繰り上がる
    [InlineData(6, 4)]   // 2行下＝占有外。削除行より上ゆえ動かぬ
    public void 高さ2の要素は占有外の行を削除しても残る(int targetRow, int expectedRow)
    {
        // 対照。上下の両側を測る——片側だけでは「常に消える／常に残る」型の誤りが見えぬ。
        var vm = ArrangeForRowDelete(anchorRow: 4, cellHeight: 2);

        vm.DeleteRowAtCommand.Execute(targetRow);

        var survivor = Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal(expectedRow, survivor.Pos.Row);
    }

    [Fact]
    public void 高さ1の要素は従来どおりアンカー行の削除でのみ消える()
    {
        // 回帰の網。高さ1では ContainsRow が Pos.Row との一致比較に潰れる。
        var vm = ArrangeForRowDelete(anchorRow: 4, cellHeight: 1);

        vm.DeleteRowAtCommand.Execute(3);   // 真上＝高さ1なら占有外
        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal(3, vm.CurrentSheet!.Elements[0].Pos.Row);   // 繰り上がっておる

        vm.DeleteRowAtCommand.Execute(3);   // 繰り上がった先のアンカー行
        Assert.Empty(vm.CurrentSheet!.Elements);
    }

    [Fact]
    public void 最終行に高さ2の要素の占有がかかっていれば行数を減らせない()
    {
        // 漏れ1（IsRowOccupied）。アンカー行の一致だけを見る実装では「空き」と判じて削除が通り、
        // 削除後に要素の占有がグリッド範囲外へはみ出す。
        var vm = ArrangeForRowDelete(anchorRow: 8, cellHeight: 2);   // 行7,8,9を占める＝最終行9にかかる

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(10, vm.CurrentSheet!.Grid.Rows);
        Assert.Single(vm.CurrentSheet!.Elements);
    }

    [Fact]
    public void 最終行が高さ2の要素の占有外なら従来どおり行数を減らせる()
    {
        // 対照。潰し方が過剰でないかの逆側の網——占有にかからぬ限り削除は通らねばならぬ。
        var vm = ArrangeForRowDelete(anchorRow: 4, cellHeight: 2);   // 行3,4,5を占める＝最終行9は空き

        vm.DeleteRowCommand.Execute(null);

        Assert.Equal(9, vm.CurrentSheet!.Grid.Rows);
        Assert.Single(vm.CurrentSheet!.Elements);
    }

    // ==================================================================
    // 自作パーツ（PartId経路）の高さ結線（増分4、殿裁定2026-07-28）
    //
    // 増分3までは幅のみ PartDefinition から取り、高さは1固定であった。組込み15件はすべて
    // HeightCells=1 ゆえ実害は無かったが、使い手が高さ2以上の自作パーツを作ると
    // 「パーツエディタでは枠が 2h-1 行に広がるのに、図面では1行しか占有せぬ」食い違いが出る。
    // ==================================================================

    /// <summary>指定の高さを持つ自作パーツを PartLibrary へ登録する。</summary>
    private static void RegisterCustomPart(MainWindowViewModel vm, string id, int heightCells, int widthCells = 1)
        => vm.PartLibrary.ById[id] = new PartDefinition
        {
            Id = id,
            Name = $"高さ{heightCells}の検体",
            WidthCells = widthCells,
            HeightCells = heightCells,
            Role = PartRole.NonSimulated,
        };

    [Fact]
    public void 自作パーツの高さが配置した要素へ入る()
    {
        var vm = ArrangeSmallGrid();
        RegisterCustomPart(vm, "custom-h2", heightCells: 2);
        vm.SelectedCell = new GridPos(2, 6);

        vm.PlaceElementAtSelectedCell("custom-h2", "", isOr: false);

        var placed = Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal(2, placed.CellHeight);
        // 対照＝幅は増分3以前から供給源(WidthCells)を見ておる。高さだけが1固定であった。
        Assert.Equal(1, placed.CellWidth);
    }

    [Theory]
    [InlineData(0, false)]   // 上端＝r-1 が -1 になり置けぬ
    [InlineData(1, true)]    // 行0,1,2 を占める（上側の境界ちょうど）
    [InlineData(3, true)]    // 行2,3,4 を占める（下側の境界ちょうど）
    [InlineData(4, false)]   // 下端＝r+1 が Rows になり置けぬ
    public void 高さ2の自作パーツは上下端の行へ置けない(int row, bool expectPlaced)
    {
        var vm = ArrangeSmallGrid();
        RegisterCustomPart(vm, "custom-h2", heightCells: 2);
        vm.SelectedCell = new GridPos(row, 6);

        vm.PlaceElementAtSelectedCell("custom-h2", "", isOr: false);

        Assert.Equal(expectPlaced ? 1 : 0, vm.CurrentSheet!.Elements.Count);
    }

    [Fact]
    public void 高さ2の自作パーツの真上の行には別要素を置けない()
    {
        // 衝突条件＝アンカー行の差が (h1-1)+(h2-1) 以下（忍者の期待値表）。
        // 既存が高さ2・置く側が高さ1なら差1までが衝突＝真上・真下が塞がり、2行離れて初めて置ける。
        var vm = ArrangeSmallGrid();
        RegisterCustomPart(vm, "custom-h2", heightCells: 2);
        RegisterCustomPart(vm, "custom-h1", heightCells: 1);
        vm.SelectedCell = new GridPos(2, 6);
        vm.PlaceElementAtSelectedCell("custom-h2", "", isOr: false);

        vm.SelectedCell = new GridPos(1, 6);   // 真上＝塞がっておる
        vm.PlaceElementAtSelectedCell("custom-h1", "", isOr: false);
        Assert.Single(vm.CurrentSheet!.Elements);

        vm.SelectedCell = new GridPos(0, 6);   // 2行上＝届かぬゆえ置ける
        vm.PlaceElementAtSelectedCell("custom-h1", "", isOr: false);
        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
    }

    [Fact]
    public void 高さ1の自作パーツは従来どおり隣接行へ置ける()
    {
        // 回帰の網。組込み15件はすべて HeightCells=1 ゆえ、この経路の挙動が変わってはならぬ。
        var vm = ArrangeSmallGrid();
        RegisterCustomPart(vm, "custom-h1", heightCells: 1);
        vm.SelectedCell = new GridPos(2, 6);
        vm.PlaceElementAtSelectedCell("custom-h1", "", isOr: false);

        vm.SelectedCell = new GridPos(1, 6);
        vm.PlaceElementAtSelectedCell("custom-h1", "", isOr: false);

        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.All(vm.CurrentSheet!.Elements, e => Assert.Equal(1, e.CellHeight));
    }

    // ==================================================================
    // 退化入力（高さ0以下）——P-148 の FrameRect/ClampPort と対称のガード
    // （隠密レビュー所見2、2026-07-28）
    //
    // 通常経路では0は入らぬ（既定1・設定経路なし）。手書き/破損した .gcad の JSON、あるいは
    // 将来 DefaultCellHeight が0を返す実装ミスでのみ顕在化する。ガードを持つ箇所は4つ
    // （IsWithinGridBounds の rowSpan／IsOccupied の rowSpan・elRowSpan／HitTestElement の
    // elRowSpan）で、下記3テストがその4つすべてを通る。
    // 入力値は 0 と -3 の双方を通す（0だけでは「負のとき」の振る舞いを測れぬ）。
    // ==================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void 高さ0以下でもグリッド範囲判定は緩まない(int degenerateHeight)
    {
        // ガードが無いと rowSpan が負になり、範囲【外】の行でも「収まる」と誤判定して範囲が緩む。
        // 【入力値の選び方】上端の行0では差が出ぬ——ガードの有無にかかわらず true になるゆえ
        // （0-0>=0 も 0+1>=0 も真）。緩みが現れるのは範囲の外側であり、そこを測らねばならぬ。
        // 行-1 は選択自体は仕様として取りうる（P-022/P-024、殿教示2026-07-07）が、配置は弾かれねばならぬ。
        var vm = ArrangeSmallGrid();

        vm.SelectedCell = new GridPos(-1, 6);
        Assert.False(vm.IsSelectedCellWithinGrid(cellWidth: 1, cellHeight: degenerateHeight));

        // 内側は従来どおり通る（0段へ潰す処置が効きすぎておらぬかの逆側の網）。
        vm.SelectedCell = new GridPos(0, 6);
        Assert.True(vm.IsSelectedCellWithinGrid(cellWidth: 1, cellHeight: degenerateHeight));

        // 対照＝列側は退化ガードの対象外ゆえ、従来どおりはみ出しを弾く（行だけを潰したことの確認）。
        vm.SelectedCell = new GridPos(0, 9);
        Assert.False(vm.IsSelectedCellWithinGrid(cellWidth: 2, cellHeight: degenerateHeight));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void 高さ0以下の既存要素は幽霊にならず占有しヒットする(int degenerateHeight)
    {
        // ガードが無いと elRowSpan が負になり、行の交差条件が常に false ＝
        // 「占有もヒットもせぬ幽霊要素」（選択も削除もできぬ）が生じる。
        var vm = ArrangeSmallGrid();
        vm.CurrentSheet!.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.ContactNO,
            Pos = new GridPos(2, 6),
            CellHeight = degenerateHeight,
            DeviceName = "X001",
        });

        vm.SelectedCell = new GridPos(2, 6);
        Assert.True(vm.IsSelectedCellOccupied());
        Assert.Equal("X001", vm.HitTestElement(new GridPos(2, 6))?.DeviceName);

        // 上下の行までは占有せぬ（高さ1と同じ＝0段へ潰れたことの確認。潰し方が過剰でないかの逆側の網）。
        vm.SelectedCell = new GridPos(1, 6);
        Assert.False(vm.IsSelectedCellOccupied());
        Assert.Null(vm.HitTestElement(new GridPos(1, 6)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void 置く側の高さが0以下でも占有判定は高さ1と同じになる(int degenerateHeight)
    {
        // IsOccupied の「置く側」の rowSpan を測る（上のテストは「既存要素側」の elRowSpan）。
        var vm = ArrangeSmallGrid();
        vm.CurrentSheet!.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.ContactNO, Pos = new GridPos(2, 6), DeviceName = "X001",
        });

        vm.SelectedCell = new GridPos(2, 6);
        Assert.True(vm.IsSelectedCellOccupied(cellWidth: 1, cellHeight: degenerateHeight));
        vm.SelectedCell = new GridPos(1, 6);
        Assert.False(vm.IsSelectedCellOccupied(cellWidth: 1, cellHeight: degenerateHeight));
    }
}
