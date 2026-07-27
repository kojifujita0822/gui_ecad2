using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.App.Tests;

/// <summary>
/// T-125増分α: シートへの実体追加経路のうち境界ガードを持たなかった3経路
/// (配線分断の記入・接続点の記入・自由線の確定)に加えたガードの回帰テスト。
/// <para>
/// <b>ガードの強さが経路ごとに意図的に異なる。</b>配線分断はグリッド境界(行・列とも上下限)を
/// 課すが、接続点・自由線は<b>下限のみ</b>で上限を課さない。これは踏襲元GuiEcadの設計に倣った
/// もので、接続点・自由線はグリッドに依存しないmm実座標プリミティブとして設計されており、
/// 描画側もグリッド範囲を超えて広がる前提で実装されているため
/// (詳細はMainWindowViewModel.IsWithinPaperLowerBoundのdocコメント)。
/// <b>ゆえに本テスト群の存在をもって「3経路とも上限まで担保された」と読んではならぬ。</b>
/// その旨を固定するテストを各経路に置いてある(UpperBoundIsIntentionallyNotEnforced系)。
/// </para>
/// <para>
/// グリッドは意図的に<b>Rows=5・Columns=10の非対称</b>とする。正方形グリッドでは行と列を
/// 取り違えるバグが偶然通ってしまうため(samurai.md「テスト入力の対称性・退化性チェック」)。
/// 同じ理由で、mm座標のテストはX・Yに異なる値を与える。
/// </para>
/// </summary>
public class T125BoundaryGuardTests : ViewModelTestBase
{
    private const int Rows = 5;
    private const int Columns = 10;

    /// <summary>Rows=5・Columns=10の非対称グリッドを持つシートを用意する。</summary>
    private static Sheet PrepareSheet(MainWindowViewModel vm, bool mainCircuit)
    {
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;
        sheet.Grid = new GridSpec { Rows = Rows, Columns = Columns };
        sheet.MainCircuit = mainCircuit;
        return sheet;
    }

    // ---------------------------------------------------------------------
    // α-1: 配線分断(WireBreak)の記入 — グリッド境界(行・列とも上下限)
    // ---------------------------------------------------------------------

    /// <summary>
    /// 列の境界値。SelectedCellは仕様として列-2まで選択できるため(P-022/P-024、殿教示2026-07-07)、
    /// 負の列は実際に到達しうる。行は範囲内の2に固定し、列だけを動かして列判定を単独で検証する。
    /// </summary>
    [Theory]
    [InlineData(-2, false)]             // SelectedCellの仕様下限。記入は拒む
    [InlineData(-1, false)]
    [InlineData(0, true)]               // 下限ちょうど
    [InlineData(Columns - 1, true)]     // 上限ちょうど(列9)
    [InlineData(Columns, false)]        // 上限+1(列10)。原本GuiEcadも col < Columns で弾く
    [InlineData(Columns + 1, false)]
    public void PlaceWireBreak_ColumnBoundary(int column, bool expected)
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: false);
        vm.SelectedCell = new GridPos(2, column);

        Assert.Equal(expected, vm.PlaceWireBreakAtSelectedCell());
        Assert.Equal(expected ? 1 : 0, sheet.WireBreaks.Count);
    }

    /// <summary>
    /// 行の境界値。列は範囲内の3に固定する。<b>行上限(Row &lt; Rows)は原本GuiEcadには無く、
    /// ecad2の要素配置に揃えた独自の規則である</b>(殿裁定2026-07-27=C-1)。この裁定が覆れば
    /// Rows・Rows+1の期待値が変わるため、意図を明記しておく。
    /// </summary>
    [Theory]
    [InlineData(-1, false)]         // SelectedCellの仕様下限(行-1)。記入は拒む
    [InlineData(0, true)]           // 下限ちょうど
    [InlineData(Rows - 1, true)]    // 上限ちょうど(行4)
    [InlineData(Rows, false)]       // 上限+1(行5)。殿裁定C-1により弾く
    [InlineData(Rows + 1, false)]
    public void PlaceWireBreak_RowBoundary(int row, bool expected)
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: false);
        vm.SelectedCell = new GridPos(row, 3);

        Assert.Equal(expected, vm.PlaceWireBreakAtSelectedCell());
        Assert.Equal(expected ? 1 : 0, sheet.WireBreaks.Count);
    }

    /// <summary>
    /// 行と列の取り違え検出。Rows=5・Columns=10の非対称ゆえ、値7は<b>行としては範囲外・
    /// 列としては範囲内</b>となる。ガードが行と列を取り違えていれば、この2件のどちらかが落ちる。
    /// </summary>
    [Fact]
    public void PlaceWireBreak_Value7_IsOutOfRangeAsRowButInRangeAsColumn()
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: false);

        vm.SelectedCell = new GridPos(7, 3);
        Assert.False(vm.PlaceWireBreakAtSelectedCell());     // 行7 > Rows-1 ゆえ拒む
        Assert.Empty(sheet.WireBreaks);

        vm.SelectedCell = new GridPos(3, 7);
        Assert.True(vm.PlaceWireBreakAtSelectedCell());      // 列7 <= Columns-1 ゆえ通る
        Assert.Single(sheet.WireBreaks);
    }

    /// <summary>範囲内での正常記入と重複拒否が、ガード追加で壊れていないこと(既存挙動の回帰)。</summary>
    [Fact]
    public void PlaceWireBreak_WithinGrid_StillPlacesAndStillRejectsDuplicate()
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: false);
        vm.SelectedCell = new GridPos(1, 4);

        Assert.True(vm.PlaceWireBreakAtSelectedCell());
        var placed = Assert.Single(sheet.WireBreaks);
        Assert.Equal(1, placed.Row);
        Assert.Equal(4.5, placed.Boundary);          // Boundary = Column + 0.5

        Assert.False(vm.PlaceWireBreakAtSelectedCell());   // 同一位置は重複ゆえ拒む
        Assert.Single(sheet.WireBreaks);
    }

    // ---------------------------------------------------------------------
    // α-2: 接続点(ConnectionDot)の記入 — 下限のみ
    // ---------------------------------------------------------------------

    /// <summary>
    /// 下限の境界値。<b>XとYに異なる値を与える</b>——同値だとX/Yの取り違えが偶然通るため
    /// (samurai.md「テスト入力の対称性・退化性チェック」)。片方だけ負のケースを両方向とも置く。
    /// </summary>
    [Theory]
    [InlineData(-0.1, 33.0, false)]     // Xのみ負
    [InlineData(20.0, -0.1, false)]     // Yのみ負
    [InlineData(-0.1, -4.2, false)]     // 両方負(値も互いに異なる)
    [InlineData(0.0, 33.0, true)]       // X下限ちょうど
    [InlineData(20.0, 0.0, true)]       // Y下限ちょうど
    [InlineData(0.1, 33.0, true)]
    public void PlaceConnectionDot_LowerBound(double xMm, double yMm, bool expected)
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: true);
        // 往復2周目で追加: 起点セルの範囲も見るようになったため、mm下限だけを問う本テストでは
        // セルを範囲内に固定しておく必要がある。旧版がこれを設定していなかったこと自体が、
        // 「セル→mm変換を一度も通していないテスト」であったことの証左でもある。
        vm.SelectedCell = new GridPos(2, 3);

        Assert.Equal(expected, vm.PlaceConnectionDot(xMm, yMm));
        Assert.Equal(expected ? 1 : 0, sheet.ConnectionDots.Count);
    }

    /// <summary>
    /// <b>上限は意図的に課していない</b>ことを固定する。グリッド(5行10列)をはるかに超えるmm座標でも
    /// 記入できる。これは瑕疵ではなく踏襲元GuiEcadの設計——接続点はグリッド非依存のmm実座標
    /// プリミティブであり、上限を課すと主回路作図の設計思想と衝突する。
    /// 将来この挙動を変える場合は、必ず殿の裁可を経ること。
    /// </summary>
    [Fact]
    public void PlaceConnectionDot_FarBeyondGrid_IsAccepted_BecauseUpperBoundIsIntentionallyNotEnforced()
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(2, 3);

        Assert.True(vm.PlaceConnectionDot(9999.0, 8888.0));
        Assert.Single(sheet.ConnectionDots);
    }

    /// <summary>範囲内での正常記入と重複拒否が壊れていないこと(既存挙動の回帰)。</summary>
    [Fact]
    public void PlaceConnectionDot_WithinPaper_StillPlacesAndStillRejectsDuplicate()
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(2, 3);

        Assert.True(vm.PlaceConnectionDot(20.0, 33.0));
        Assert.Single(sheet.ConnectionDots);

        Assert.False(vm.PlaceConnectionDot(20.0, 33.0));   // 同一座標は重複ゆえ拒む
        Assert.Single(sheet.ConnectionDots);
    }

    // ---------------------------------------------------------------------
    // α-2: 自由線(FreeLine)の確定 — 下限のみ
    // ---------------------------------------------------------------------

    /// <summary>
    /// 水平線を負方向(左)へ伸ばして用紙原点より外へ出た場合、確定を拒む。
    /// 起点X=5.0からstepMm=9.0で1つ戻すと終端は-4.0となり、正規化後のX1が負になる。
    /// </summary>
    [Fact]
    public void ConfirmFreeLineDraft_ExtendedLeftBeyondOrigin_IsRejected()
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(2, 1);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 5.0, startYMm: 33.0, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(-1);

        Assert.False(vm.IsFreeLineDraftWithinPaperBounds);
        Assert.False(vm.ConfirmFreeLineDraft());
        Assert.Empty(sheet.FreeLines);
        Assert.Equal(ToolMode.PlaceLine, vm.Tool.Mode);    // 記入モードに留まる
    }

    /// <summary>
    /// 垂直線を負方向(上)へ伸ばした場合も同様に拒む。水平版とは<b>軸が異なる</b>ため別ケースとして
    /// 置く——X判定だけ実装してY判定を落とす型の瑕疵は、水平版だけでは検出できない。
    /// </summary>
    [Fact]
    public void ConfirmFreeLineDraft_ExtendedUpBeyondOrigin_IsRejected()
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(2, 1);
        vm.BeginFreeLineDraft(horizontal: false, startXMm: 20.0, startYMm: 4.0, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(-1);

        Assert.False(vm.IsFreeLineDraftWithinPaperBounds);
        Assert.False(vm.ConfirmFreeLineDraft());
        Assert.Empty(sheet.FreeLines);
    }

    /// <summary>
    /// FreeLineDraftPreviewがMath.Min/Maxで正規化する事実を固定する。負方向へ伸ばしても
    /// <b>X1が負・X2は起点のまま</b>となり、「終点だけが負」という状態は原理的に作れない。
    /// ゆえに本テスト群は始点側で検出している——この前提が崩れれば境界テストの意味も変わるため、
    /// 前提そのものをテストで押さえておく。
    /// </summary>
    [Fact]
    public void FreeLineDraftPreview_NormalizesEndpoints_SoNegativeAlwaysAppearsAtStartPoint()
    {
        var vm = CreateViewModel();
        PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(2, 1);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 5.0, startYMm: 33.0, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(-1);

        var preview = vm.FreeLineDraftPreview!;
        Assert.Equal(-4.0, preview.X1Mm);       // 伸ばした先(負)がX1へ回る
        Assert.Equal(5.0, preview.X2Mm);        // 起点がX2へ回る
        Assert.True(preview.X1Mm <= preview.X2Mm);
        Assert.True(preview.Y1Mm <= preview.Y2Mm);
    }

    /// <summary>
    /// <b>上限は意図的に課していない</b>ことを固定する(接続点と同じ理由)。Columns=10・stepMm=9.0で
    /// 右へ限界まで伸ばすと終端は 47.0+90.0=137.0mm となり、グリッド幅(10列×9mm=90mm)を超えるが
    /// 確定できる。将来この挙動を変える場合は、必ず殿の裁可を経ること。
    /// </summary>
    [Fact]
    public void ConfirmFreeLineDraft_ExtendedBeyondGridWidth_IsAccepted_BecauseUpperBoundIsIntentionallyNotEnforced()
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(2, 1);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 47.0, startYMm: 33.0, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(Columns);       // MoveFreeLineDraftEndのクランプ上限いっぱい

        Assert.True(vm.ConfirmFreeLineDraft());
        var line = Assert.Single(sheet.FreeLines);
        Assert.Equal(137.0, line.X2Mm);         // 47.0 + 10*9.0、グリッド幅90mmを超えている
    }

    /// <summary>範囲内での正常確定が壊れていないこと(既存挙動の回帰)。</summary>
    [Fact]
    public void ConfirmFreeLineDraft_WithinPaper_StillConfirms()
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(2, 1);
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 20.0, startYMm: 33.0, stepMm: 9.0);
        vm.MoveFreeLineDraftEnd(2);

        Assert.True(vm.IsFreeLineDraftWithinPaperBounds);
        Assert.True(vm.ConfirmFreeLineDraft());
        var line = Assert.Single(sheet.FreeLines);
        Assert.Equal(20.0, line.X1Mm);
        Assert.Equal(38.0, line.X2Mm);          // 20.0 + 2*9.0
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
    }

    /// <summary>
    /// 記入中でなければIsFreeLineDraftWithinPaperBoundsはtrue(判定対象が無い)。
    /// View側がこのプロパティで拒否理由を出し分けるため、記入していない状態で偽を返すと
    /// 無関係な場面で「範囲外」の案内が出てしまう。
    /// </summary>
    [Fact]
    public void IsFreeLineDraftWithinPaperBounds_IsTrue_WhenNotDrafting()
    {
        var vm = CreateViewModel();
        PrepareSheet(vm, mainCircuit: true);

        Assert.Null(vm.FreeLineDraftPreview);
        Assert.True(vm.IsFreeLineDraftWithinPaperBounds);
    }

    // ---------------------------------------------------------------------
    // 下限ガードの述語そのもの
    // ---------------------------------------------------------------------

    /// <summary>
    /// IsWithinPaperLowerBoundの単体検証。X・Yに異なる値を与え、片方だけ負のケースを両方向とも置く
    /// (X/Yの取り違え検出)。上限を見ていないことも併せて固定する。
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, true)]
    [InlineData(0.1, 33.0, true)]
    [InlineData(9999.0, 8888.0, true)]      // 上限は見ない
    [InlineData(-0.1, 33.0, false)]         // Xのみ負
    [InlineData(20.0, -0.1, false)]         // Yのみ負
    [InlineData(-0.1, -4.2, false)]
    public void IsWithinPaperLowerBound_ChecksBothAxesIndependently(double xMm, double yMm, bool expected)
        => Assert.Equal(expected, MainWindowViewModel.IsWithinPaperLowerBound(xMm, yMm));

    // ---------------------------------------------------------------------
    // 往復2周目：セル→mm変換を通した経路（初版の穴。忍者の実機確認で発覚）
    // ---------------------------------------------------------------------

    /// <summary>View（<c>LadderCanvas.CellToMm</c>）と同じ変換を再現する。</summary>
    private static (double XMm, double YMm) CellToMm(GridPos cell)
    {
        var geometry = ActualGeometry();
        return (geometry.X(cell.Column), geometry.YRow(cell.Row));
    }

    /// <summary>
    /// 画面描画で実際に使われる幾何。<b><c>GridGeometry</c>のコンストラクタ既定（<c>MarginMm=15.0</c>）
    /// ではない</b>——<c>DiagramRenderer</c>が常に<c>RenderOptions.MarginMm</c>（20.0）で上書きする
    /// （<c>DiagramRenderer.cs:44</c>）ため、コンストラクタ既定は実質使われない。
    /// </summary>
    private static GridGeometry ActualGeometry()
    {
        var options = new RenderOptions();
        return new GridGeometry(options.CellMm, options.MarginMm);
    }

    /// <summary>
    /// <b>【真因の記録】</b>mm変換は<c>MarginMm</c>を足すため、グリッド範囲外のセルでも
    /// mm座標が正になる。<b>「mm &gt;= 0」は「グリッド内」を意味しない</b>——これが初版の穴の正体。
    /// <para>
    /// 実際に使われる<c>MarginMm</c>は<b>20.0</b>（<c>RenderOptions</c>の既定）。ゆえに
    /// 列-2でX=2.0、列-1でX=11.0、行-1でY=15.5と、<b><c>SelectedCell</c>の仕様範囲
    /// （行-1・列-2まで、P-022/P-024）が丸ごと正の値になる</b>。初版は接続点・自由線を
    /// mm下限だけで守っていたため、この範囲がすべて素通りした。
    /// </para>
    /// <para>
    /// 配線分断が同じ範囲外セルで正しく弾けていたのは、グリッド境界（整数の行・列）で判定しており
    /// 変換を挟まなかったからである。<b>同じ範囲外セルでも尺度が違えば結果が違う。</b>
    /// </para>
    /// <para>
    /// <b>【罠】</b><c>GridGeometry</c>のコンストラクタ既定は15.0であり、これを実際の値と
    /// 取り違えると「列-2は負（X=-3.0）ゆえ弾ける」という誤った結論に至る（侍が実際に誤った）。
    /// 既定値が2箇所にあり片方が使われない構造ゆえ、差異そのものを本テストで固定しておく。
    /// </para>
    /// </summary>
    [Fact]
    public void ActualMargin_MapsOutOfGridCellsToPositiveMm_WhichIsWhyLowerBoundAlone_WasNotEnough()
    {
        var options = new RenderOptions();
        Assert.Equal(20.0, options.MarginMm);       // 実際に使われる値

        var geometry = ActualGeometry();
        Assert.Equal(11.0, geometry.X(-1));         // 列-1 → 正。mm下限では弾けない
        Assert.Equal(2.0, geometry.X(-2));          // 列-2 → 正。仕様下限すら素通りする
        Assert.Equal(15.5, geometry.YRow(-1));      // 行-1 → 正。同上
        Assert.Equal(20.0, geometry.X(0));          // 範囲内の下限セル
        Assert.Equal(24.5, geometry.YRow(0));

        // 【罠その2】GridGeometryはreadonly structゆえ、引数なしの new GridGeometry() では
        // structに常に存在する暗黙のパラメータレスコンストラクタ（全フィールドゼロ初期化）が
        // 選ばれ、宣言された既定値(cellMm=9.0 / marginMm=15.0)は適用されない。
        // すなわち「コンストラクタ既定は15.0」という読み方自体が、書き方によっては成り立たない。
        Assert.Equal(0.0, new GridGeometry().MarginMm);
        Assert.Equal(15.0, new GridGeometry(9.0).MarginMm);   // 引数を1つでも渡せば既定値が効く
    }

    /// <summary>
    /// 【往復2周目の本体】範囲外セルからセル→mm変換を通した座標で接続点を記入しようとしても拒む。
    /// <para>
    /// <b>本テストの要は、検体が「mm下限ガードを素通りする値」であることを事前アサートしている点</b>
    /// にある。初版のテストは<c>PlaceConnectionDot(-0.1, 33.0)</c>のようにmm座標を直接与えており、
    /// <b>セル→mm変換を一度も通していなかった</b>。変換の後の値でいくら境界を刻んでも、
    /// 変換そのものが持つズレは永久に現れない。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(2, -1)]         // 列-1（X=11.0で正）
    [InlineData(2, -2)]         // 列-2（X=2.0で正）。SelectedCellの仕様下限すら素通りしていた
    [InlineData(-1, 3)]         // 行-1（Y=15.5で正）
    [InlineData(-1, -2)]        // 両軸とも仕様下限
    [InlineData(2, Columns)]    // 上限側（列10）。mm下限では原理的に弾けない
    [InlineData(Rows, 3)]       // 上限側（行5）。同上
    public void PlaceConnectionDot_FromOutOfGridCell_IsRejected_EvenWhenConvertedMmIsPositive(int row, int column)
    {
        var vm = CreateViewModel();
        var sheet = PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(row, column);

        var (xMm, yMm) = CellToMm(new GridPos(row, column));

        // 【重要】この検体は「mm下限ガードを素通りする値」でなければ本テストの意味がない。
        // すなわち旧実装がなぜ通してしまったかを、テスト自身が証拠として持つ。
        Assert.True(MainWindowViewModel.IsWithinPaperLowerBound(xMm, yMm),
            "検体が負のmmでは、mm下限ガードが弾いてしまい今回の穴を突けない");

        Assert.False(vm.PlaceConnectionDot(xMm, yMm));
        Assert.Empty(sheet.ConnectionDots);
    }

    /// <summary>
    /// 自由線も同型の穴を持っていた（忍者の初回確認では「拒まれた」と出たが、
    /// それは試した検体がたまたま弾ける条件だったためと見る）。起点セルが範囲外なら
    /// <b>記入モードに入らせない</b>——要素配置が配置バーを出す前に弾くのと同じ作法。
    /// </summary>
    [Theory]
    [InlineData(2, -1)]
    [InlineData(2, -2)]
    [InlineData(-1, 3)]
    [InlineData(2, Columns)]
    public void BeginFreeLineDraft_FromOutOfGridCell_IsRejected_AndDoesNotEnterDraftMode(int row, int column)
    {
        var vm = CreateViewModel();
        PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(row, column);

        var (xMm, yMm) = CellToMm(new GridPos(row, column));
        Assert.True(MainWindowViewModel.IsWithinPaperLowerBound(xMm, yMm),
            "検体が負のmmでは、mm下限ガードが弾いてしまい今回の穴を突けない");

        Assert.False(vm.BeginFreeLineDraft(horizontal: true, xMm, yMm, stepMm: 9.0));
        Assert.Null(vm.FreeLineDraftPreview);
        Assert.NotEqual(ToolMode.PlaceLine, vm.Tool.Mode);
    }

    /// <summary>範囲内セルからの記入開始は従来どおり通る（回帰）。</summary>
    [Fact]
    public void BeginFreeLineDraft_FromCellWithinGrid_StillStartsDraft()
    {
        var vm = CreateViewModel();
        PrepareSheet(vm, mainCircuit: true);
        vm.SelectedCell = new GridPos(2, 1);

        var (xMm, yMm) = CellToMm(new GridPos(2, 1));
        Assert.True(vm.BeginFreeLineDraft(horizontal: true, xMm, yMm, stepMm: 9.0));
        Assert.Equal(ToolMode.PlaceLine, vm.Tool.Mode);
        Assert.NotNull(vm.FreeLineDraftPreview);
    }
}
