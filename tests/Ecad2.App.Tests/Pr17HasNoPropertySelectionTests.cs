using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// PR-17段2（`P-169` の手当て）: <c>HasNoPropertySelection</c> を基準
/// <c>NotifySelectedElementChanged()</c> へ足したことを測る
/// （隠密のテスト設計書 <c>docs/ecad2-pr17-consolidation-test-design-onmitsu.md</c> §3）。
/// <para>
/// <b>【値ではなく通知を見る】</b><c>HasNoPropertySelection</c> はバッキングフィールドを持たぬ
/// 算出プロパティ（<c>= !HasSelectedElement &amp;&amp; !HasSelectedImage &amp;&amp; !HasSelectedFrame</c>）にて、
/// <b>直接読めば修正前でも常に「今の正しい値」が返る</b>。
/// <b>バグの正体は値の誤りではなく、<c>PropertyChanged</c> が飛ばぬためバインドが再評価されず、
/// 画面が古い表示のまま取り残されること</b>——ゆえに値の比較では検出力を持たぬ。
/// </para>
/// <para>
/// <b>【集合ではなく回数を見る】</b>段1のテスト（<c>Pr17ConsolidationTests</c>）は集合で見たが、
/// 本件は<b>単一のプロパティ名の発火回数</b>を数える。<c>ReplaceDocument</c> は段2の前から
/// <c>SelectedImage</c>／<c>SelectedFrame</c> setter 経由で暗黙に2回飛んでおり、
/// <b>「飛んだか否か」では段2を実装せずとも通ってしまう</b>——回数の増分だけが段2の寄与を
/// 暗黙の救いから切り離す（設計書§3-4）。
/// <b>対象が単一の名ゆえ、段1で戒めた「異種混合の件数比較」には当たらぬ。</b>
/// </para>
/// <para>
/// <b>【<c>SelectedCell</c> を不変に保つ理由】</b>選択セルが変われば setter 自身が
/// <c>HasNoPropertySelection</c> を飛ばすため、<b>対象経路固有の欠落が覆い隠される</b>。
/// ガードを回避するためではなく、<b>他の正しい経路に紛れさせぬための実験条件の分離</b>である。
/// </para>
/// </summary>
public class Pr17HasNoPropertySelectionTests : ViewModelTestBase
{
    /// <summary><paramref name="act"/> の間に <c>HasNoPropertySelection</c> が何回飛んだかを数える。</summary>
    private static int CountRaises(MainWindowViewModel vm, Action act)
    {
        var raised = new List<string>();
        void Handler(object? _, System.ComponentModel.PropertyChangedEventArgs e)
            => raised.Add(e.PropertyName ?? "");

        vm.PropertyChanged += Handler;
        try { act(); }
        finally { vm.PropertyChanged -= Handler; }
        return raised.Count(n => n == nameof(MainWindowViewModel.HasNoPropertySelection));
    }

    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    // ===== 3-3: 欠落4経路（P-169の射程は起票の四倍） =====

    /// <summary>
    /// 3-3-a【空白型】要素を削除すれば通知が飛ぶこと。<b><c>P-169</c> の起票そのもの</b>
    /// ——実機で確定済み（削除後、右下が完全な空白）。
    /// </summary>
    [Fact]
    public void 要素の削除で通知が飛ぶ()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 2, 3, "X001");

        int count = CountRaises(vm, () => vm.DeleteSelectedElement());

        Assert.Equal(1, count);
    }

    /// <summary>
    /// 3-3-b【空白型】<b>削除対象行そのものを選択中</b>の行削除で通知が飛ぶこと。
    /// <c>SelectedCell</c> の値自体は動かぬ（<c>sc.Row &gt; row</c> が偽）ゆえ setter を通らぬ
    /// ——<c>RowInsertDeleteCommandsTests</c> の T-a1 と同一の境界にござる。
    /// </summary>
    [Fact]
    public void 削除対象行を選択中の行削除で通知が飛ぶ()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.ContactNO,
            DeviceName = "X001",
            Pos = new GridPos(3, 1),
        });
        vm.SelectedCell = new GridPos(3, 1);

        int count = CountRaises(vm, () => vm.DeleteRowAtCommand.Execute(3));

        Assert.Equal(1, count);
    }

    /// <summary>
    /// 3-3-c【二重型】<b>空セルへ配置</b>したとき通知が飛ぶこと（<c>PartId</c> 経路）。
    /// <para>
    /// <b>忍者の実機実測が示した症状の単体版</b>——空セル選択（案内文が出ておる）→同セルへ配置→
    /// <b>案内文が消えぬまま要素詳細も横並びで出る</b>
    /// （<c>scratchpad\pr17-54-confirmed.png</c>、2026-08-06）。
    /// <c>HasSelectedElement</c> は基準に含まれ正しく飛ぶゆえ詳細側は現れ、
    /// <b><c>HasNoPropertySelection</c> だけが取り残される。</b>
    /// </para>
    /// </summary>
    [Fact]
    public void 空セルへの配置で通知が飛ぶ_PartId経路()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(2, 3);

        int count = CountRaises(vm,
            () => vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false));

        Assert.Equal(1, count);
    }

    /// <summary>3-3-d【二重型】同上、<c>Kind</c> 経路（T-133増分4-Bで新設された配置経路）。</summary>
    [Fact]
    public void 空セルへの配置で通知が飛ぶ_Kind経路()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(2, 3);

        int count = CountRaises(vm, () => vm.PlaceElementAtSelectedCell(ElementKind.ContactNO, null));

        Assert.Equal(1, count);
    }

    // ===== 3-5: 回帰確認と、暗黙の依存の切り分け =====

    /// <summary>
    /// 3-5-a【回帰】選択セルを変えたときの発火は<b>3回のまま増えぬ</b>こと。
    /// <para>
    /// 段2で <c>SelectedCell</c> setter 自身の既存呼び出しを<b>消し忘れれば4回に膨らむ</b>
    /// ——<b>二重発火という新種の不具合</b>。「増えていないこと」を確かめる意味でも回数が要る。
    /// </para>
    /// <para>
    /// <b>【3回の内訳】</b>setter 冒頭の <c>SelectedImage = null</c>・<c>SelectedFrame = null</c> で
    /// <b>暗黙に2回</b>（両 setter が <c>SetProperty</c> の戻り値を見ず無条件通知するため）、
    /// 加えて明示の1回。<b>段2ではこの明示1回が基準の内側へ移るだけゆえ、合計は3回のまま。</b>
    /// </para>
    /// <para>
    /// <b>【設計書§3-5-a の「1回→依然1回」も、侍の当初の期待値も誤りであった】</b>
    /// いずれも<b>暗黙の2回を勘定に入れておらなんだ</b>——設計書§2-4-c・§2-4-d と同じ機序による
    /// <b>三例目</b>にござる。侍は段1でこの機序を自ら見つけて報じておきながら、
    /// <b>己の段2の期待値へ反映しきれなんだ</b>（実装前の実測が3を返して露見した）。
    /// </para>
    /// </summary>
    [Fact]
    public void 選択セル変更の発火は三回のまま増えぬ()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 2, 3, "X001");

        int count = CountRaises(vm, () => vm.SelectedCell = new GridPos(5, 1));

        Assert.Equal(3, count);
    }

    /// <summary>
    /// 3-5-b【暗黙の依存の切り分け】文書差し替えでの発火が<b>2回から3回へ増える</b>こと。
    /// <para>
    /// <b>家老の問い「暗黙の依存が壊れたことを捕らえるテストが在るか」への答えがこれである。</b>
    /// <c>ReplaceDocument</c> は段2の前から <c>SelectedImage = null</c>・<c>SelectedFrame = null</c> の
    /// 両 setter 経由で<b>暗黙に2回</b>飛ばしておる（侍の設計書§5-2）。
    /// <b>「飛んだか」を見る形のテストは段2を実装せずとも通ってしまい、段2の寄与を測れておらぬ。</b>
    /// 段2で基準経由の明示1回が加わって3回になる——<b>この増分だけが、偶然の救いと設計とを切り分ける。</b>
    /// </para>
    /// </summary>
    [Fact]
    public void 文書差し替えの発火は暗黙二回に明示一回が加わる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 2, 3, "X001");

        int count = CountRaises(vm, () => vm.NewDocument());

        Assert.Equal(3, count);
    }

    /// <summary>
    /// 3-5-c【代表1件】<b>値が変わらぬ3経路</b>（<c>ReplaceOneDeviceName</c>／
    /// <c>ReplaceAllDeviceName</c>／<c>EndOrJoinTargetDraft</c>）の代表として、
    /// <b>基準関数を経由するだけで恩恵を受ける</b>ことを示す。
    /// <para>
    /// <b>この3経路に実害は無い</b>（選択の有無が変わらぬゆえ <c>HasNoPropertySelection</c> の値も動かぬ）。
    /// 測るのは<b>「基準へ足せば全経路へ均一に及ぶ」という構造</b>であり、症状の再現ではない。
    /// </para>
    /// </summary>
    [Fact]
    public void 基準を呼ぶだけの経路にも通知が及ぶ()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 2, 3, "X001");

        int count = CountRaises(vm, () => vm.ReplaceAllDeviceName("X001", "X002"));

        Assert.Equal(1, count);
    }
}
