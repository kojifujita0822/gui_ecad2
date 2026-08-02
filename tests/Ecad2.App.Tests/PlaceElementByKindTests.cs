using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分4-B: 組込み種別（<see cref="ElementKind"/>）による配置経路。
/// <para>
/// <b>【この経路が要る訳】</b>既存の <c>PlaceElementAtSelectedCell(string partId, ...)</c> は
/// <c>PartId</c> 専用で、生成する要素の <c>Kind</c> を設定せず常に既定値 <c>ContactNO</c> のまま固定される
/// （T-046由来の構造的制約）。<b>主回路3極記号は <c>PartDefinition</c> を持たぬゆえ既存フローに乗らぬ</b>
/// ——殿裁定2026-07-28＝案B。
/// </para>
/// <para>
/// <b>【呼び手はまだ無い＝意図した中間状態】</b>メニューからの導線は増分4-C で繋ぐ。
/// <b>本テスト群は「器が正しく働くか」を測るもので、「実際に呼ばれるか」は増分4-C が測る</b>
/// ——この二段に分けるのが <c>samurai.md</c>【MUST】の求めるところ（T-125増分α・T-144の実例）。
/// </para>
/// <para>
/// <b>【入力値の選び方】</b><c>T136SheetAffinityTests</c> に倣い、グリッドを非対称に（<c>Rows=5 / Columns=10</c>）、
/// 位置も行と列で別の値とする。<b>加えて幅と高さが異なる種別（<c>Motor</c>＝幅3・高さ2）を必ず混ぜる</b>
/// ——3極記号は 2×2 で正方ゆえ、それだけでは幅と高さの取り違えが結果に現れぬ。
/// </para>
/// </summary>
public class PlaceElementByKindTests : ViewModelTestBase
{
    private const int Rows = 5;
    private const int Columns = 10;
    /// <summary>行と列で別の値。3極記号（2×2）なら行1〜3・列6〜7を占め、いずれもグリッド内。</summary>
    private static readonly GridPos PlacePos = new(2, 6);

    private MainWindowViewModel Arrange(bool mainCircuit = true)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid = new GridSpec { Rows = Rows, Columns = Columns };
        vm.CurrentSheet!.MainCircuit = mainCircuit;
        vm.SelectedCell = PlacePos;
        return vm;
    }

    // ===== 観点A: 生成される要素の中身 =====

    /// <summary>
    /// <c>Kind</c> が実際に設定されること——<b>これが本経路の存在理由そのもの</b>。
    /// PartId 経路では常に既定値 <c>ContactNO</c> のままであった。
    /// </summary>
    [Theory]
    [InlineData(ElementKind.Breaker3P)]
    [InlineData(ElementKind.ContactorMain3P)]
    [InlineData(ElementKind.ThermalOverload3P)]
    public void 種別が要素へ設定される(ElementKind kind)
    {
        var vm = Arrange();

        vm.PlaceElementAtSelectedCell(kind, "V");

        Assert.Equal(kind, Assert.Single(vm.CurrentSheet!.Elements).Kind);
    }

    /// <summary>
    /// 幅と高さが <see cref="ElementCatalog"/> の既定値から入ること。
    /// <b><c>Motor</c> を混ぜているのが要点</b>——幅3・高さ2と値が異なるゆえ、
    /// <c>DefaultCellWidth</c> と <c>DefaultCellHeight</c> を取り違えれば必ず落ちる。
    /// 3極記号（2×2）だけでは取り違えが結果に現れぬ。
    /// </summary>
    [Theory]
    [InlineData(ElementKind.Breaker3P, 2, 2)]
    [InlineData(ElementKind.ContactorMain3P, 2, 2)]
    [InlineData(ElementKind.ThermalOverload3P, 2, 2)]
    [InlineData(ElementKind.Motor, 3, 2)]
    [InlineData(ElementKind.ContactNO, 1, 1)]
    public void 幅と高さがElementCatalogの既定値から入る(ElementKind kind, int expectedWidth, int expectedHeight)
    {
        var vm = Arrange();

        vm.PlaceElementAtSelectedCell(kind, null);

        var element = Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal(expectedWidth, element.CellWidth);
        Assert.Equal(expectedHeight, element.CellHeight);
    }

    /// <summary>向きが <c>Params[Orient]</c> へ入ること（配置時に確定し、以後変えられぬ設計）。</summary>
    [Theory]
    [InlineData("V")]
    [InlineData("H")]
    public void 向きがParamsへ入る(string orient)
    {
        var vm = Arrange();

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, orient);

        Assert.Equal(orient, Assert.Single(vm.CurrentSheet!.Elements).Params[ParamKeys.Orient]);
    }

    /// <summary>向きが <c>null</c> なら <c>Params</c> へ入れぬ（空文字を入れる誤りへの網）。</summary>
    [Fact]
    public void 向きがnullならParamsへ入れぬ()
    {
        var vm = Arrange();

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, null);

        Assert.False(Assert.Single(vm.CurrentSheet!.Elements).Params.ContainsKey(ParamKeys.Orient));
    }

    /// <summary>
    /// <c>Params[Type]</c>（NFB/MCCB/ELB）は入れぬこと。
    /// <b>【書き漏らしではない】</b><c>DiagramRenderer</c> が「未設定なら NFB」のフォールバックを既に持つゆえ、
    /// ここで既定値を書き込めば <c>"NFB"</c> が2箇所に散る。原本もタグに型を載せておらぬ。
    /// <b>型の切替UIは増分5（＝T-131 の P-100）で設ける。</b>
    /// この観点を測るのは、後の者が「既定値が入っておらぬのは漏れだ」と足しにかかるのを防ぐためである。
    /// </summary>
    [Fact]
    public void 型はParamsへ入れぬ()
    {
        var vm = Arrange();

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        Assert.False(Assert.Single(vm.CurrentSheet!.Elements).Params.ContainsKey(ParamKeys.Type));
    }

    /// <summary>
    /// <c>PartId</c> と <c>DeviceName</c> はいずれも <c>null</c>。
    /// <b>デバイス名を取らぬのは原本の作法</b>——メニューで選びキャンバス単クリックで即配置し、
    /// 名前は後からプロパティパネルで付ける（殿裁定2026-07-28＝B-1）。
    /// </summary>
    [Fact]
    public void PartIdとデバイス名は持たぬ()
    {
        var vm = Arrange();

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        var element = Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Null(element.PartId);
        Assert.Null(element.DeviceName);
    }

    /// <summary>デバイス名を持たぬゆえ機器表にも載らぬ（PartId 経路の作法と揃う）。</summary>
    [Fact]
    public void 機器表には載らぬ()
    {
        var vm = Arrange();

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        Assert.Empty(vm.Document.Devices.ByName);
    }

    // ===== 観点B: (f) 主回路限定の防御側 =====

    /// <summary>
    /// 3極記号は主回路シートにのみ置ける（<c>ElementCatalog.SheetAffinityOf</c> が
    /// <c>MainCircuitOnly</c> と宣言しておる）。<b>拒否はサイレント</b>（殿裁定2026-07-31）。
    /// </summary>
    [Theory]
    [InlineData(ElementKind.Breaker3P, true, true)]
    [InlineData(ElementKind.Breaker3P, false, false)]
    [InlineData(ElementKind.ContactorMain3P, true, true)]
    [InlineData(ElementKind.ContactorMain3P, false, false)]
    [InlineData(ElementKind.ThermalOverload3P, true, true)]
    [InlineData(ElementKind.ThermalOverload3P, false, false)]
    public void 三極記号は主回路シートにのみ置ける(ElementKind kind, bool mainCircuit, bool expectPlaced)
    {
        var vm = Arrange(mainCircuit);

        vm.PlaceElementAtSelectedCell(kind, "V");

        Assert.Equal(expectPlaced ? 1 : 0, vm.CurrentSheet!.Elements.Count);
    }

    /// <summary>
    /// 枷を持たぬ種別（<c>SheetAffinity.Any</c>）はどちらのシートにも置ける。
    /// <b>「置ける」側を必ず測る</b>——拒否がサイレントゆえ、枷が効きすぎて全部拒否になっても
    /// 拒否側のテストだけでは気づけぬ（<c>T136SheetAffinityTests</c> と同じ構え）。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 枷を持たぬ種別はどちらのシートにも置ける(bool mainCircuit)
    {
        var vm = Arrange(mainCircuit);

        vm.PlaceElementAtSelectedCell(ElementKind.ContactNO, null);

        Assert.Single(vm.CurrentSheet!.Elements);
    }

    // ===== 観点C: Undo の順序（T-134殿裁定(U-1)） =====

    [Fact]
    public void 配置に成功すればUndo履歴を一つ積む()
    {
        var vm = Arrange();
        int before = vm.UndoManager.UndoDepth;

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        Assert.Equal(before + 1, vm.UndoManager.UndoDepth);
    }

    /// <summary>
    /// 枷に反した配置は履歴を積まぬ。
    /// <b>ValidatePlacement を通過した後に RecordSnapshot する順序（T-134殿裁定(U-1)）が、
    /// 新設の経路でも保たれることを固定する</b>——拒否でも積めば「押しても何も変わらぬUndo」が1回挟まる。
    /// </summary>
    [Fact]
    public void 枷に反した配置はUndo履歴を積まない()
    {
        var vm = Arrange(mainCircuit: false);
        int before = vm.UndoManager.UndoDepth;

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        Assert.Equal(before, vm.UndoManager.UndoDepth);
    }

    /// <summary>
    /// グリッド範囲外での拒否でも履歴を積まぬ。
    /// <b>拒否の理由が違えば通る道も違う</b>——枷だけを測れば、境界での拒否が積む形になっていても気づけぬ。
    /// 高さ2ゆえ <c>[r-1, r+1]</c> を占め、行0（最上行）には置けぬ。
    /// </summary>
    [Fact]
    public void 範囲外での拒否もUndo履歴を積まない()
    {
        var vm = Arrange();
        vm.SelectedCell = new GridPos(0, 6);   // 高さ2ゆえ行-1へはみ出す
        int before = vm.UndoManager.UndoDepth;

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        Assert.Empty(vm.CurrentSheet!.Elements);
        Assert.Equal(before, vm.UndoManager.UndoDepth);
    }

    /// <summary>占有済みでの拒否でも履歴を積まぬ（拒否の三つ目の道）。</summary>
    [Fact]
    public void 占有済みでの拒否もUndo履歴を積まない()
    {
        var vm = Arrange();
        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");
        int before = vm.UndoManager.UndoDepth;

        vm.PlaceElementAtSelectedCell(ElementKind.ContactorMain3P, "V");   // 同じ位置

        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal(before, vm.UndoManager.UndoDepth);
    }

    // ===== 観点D: 配置直後の通知（T-079 の教訓） =====

    /// <summary>
    /// 配置直後に <c>SelectedElement</c> の変更が通知されること。
    /// <para>
    /// <b>【なぜ要るか・T-079(P-058)の実例】</b><c>SelectedCell</c> 自体は配置前後で値が変わらぬゆえ、
    /// setter 経由の通知が発火せぬ。放置するとプロパティパネルの表示が配置前の古い値のまま残り、
    /// 配置直後に Ctrl+S 等で編集確定が走ると<b>古い表示値が新要素のデバイス名として書き込まれ、
    /// 機器表エントリが消失する</b>——実機で起きた事故である。
    /// </para>
    /// </summary>
    [Fact]
    public void 配置直後に選択要素の変更が通知される()
    {
        var vm = Arrange();
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        Assert.Contains(nameof(MainWindowViewModel.SelectedElement), notified);
    }

    /// <summary>拒否されたときは通知を出さぬ（何も起きておらぬのに画面を揺らさぬ）。</summary>
    [Fact]
    public void 拒否されたときは通知を出さぬ()
    {
        var vm = Arrange(mainCircuit: false);
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        Assert.DoesNotContain(nameof(MainWindowViewModel.SelectedElement), notified);
    }

    // ===== 観点E: 前提の確認 =====

    /// <summary>選択セルが無ければ何も起きぬ（退化入力）。</summary>
    [Fact]
    public void 選択セルが無ければ何も起きぬ()
    {
        var vm = Arrange();
        vm.SelectedCell = null;

        vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");

        Assert.Empty(vm.CurrentSheet!.Elements);
    }
}
