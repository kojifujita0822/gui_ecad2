using System.Windows;
using System.Windows.Controls;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分4-C: 「その他図形」メニューから主回路3極記号を配置する導線。
/// <b>増分6（殿裁定8）で既存4件の再掲を加えた</b>——同じサブメニューに <c>PartId</c> 経路が同居する。
/// <para>
/// <b>【ここで初めて 4-A・4-B が呼ばれる】</b>タグ解析（4-A）も <c>Kind</c> 経路の配置（4-B）も
/// 器としては測り済みだが、<b>器の単体テストだけでは「作ったのに一度も呼ばれておらぬ」を素通しする</b>
/// （T-125増分αで実証、本日T-144でも実機まで露見せず起きた＝<c>Notify()</c> の欠落）。
/// <b>本テスト群が繋ぎ込みを測る役を負う。</b>
/// </para>
/// <para>
/// <b>【<c>MainWindow</c> を STA で立てられた】</b>本日、<c>PartEditorCanvas</c>（T-144）・
/// <c>SheetSettingsDialog</c>（T-132）に続き三度目である。<b>「View 層ゆえ測れぬ」という見立ては、
/// 問うてみるまで確かめられておらぬ思い込みであった</b>——<c>samurai.md</c>「『既存に無い』は
/// 『立てられぬ』ではない」。<c>ShowDialog()</c> も <c>Show()</c> も呼ばずに済むゆえ、窓は画面に出ぬ。
/// </para>
/// <para>
/// <b>【測れておらぬこと・侍が自ら区切る】</b>キャンバスのクリックから
/// <c>TryPlaceActiveTool</c> へ至る経路（マウス座標→セル選択）は測っておらぬ。
/// <c>TryPlaceKindElement</c> のプレチェック（範囲外・占有済みの案内文言）も <c>private</c> ゆえ測れぬ
/// ——いずれも実機確認に委ねる。
/// <b>増分6で加わった分についても同様に、押した後に実際の要素が生まれるところは測っておらぬ</b>
/// ——ここで測るのは<b>「ツール状態へ正しい <c>PartId</c> が載るところまで」</b>である。
/// その先（<c>TryPlaceElement</c> が配置バーを開き、部品リスト経由と同一の要素を作ること）は
/// 既存経路そのものゆえ、<b>素通しされうるのは「載る値が違う」場合に限られる</b>。
/// </para>
/// </summary>
public class MenuPlacementToolTests
{
    /// <summary>原本 GuiEcad の <c>OtherBuiltins</c> 配列（<c>MainPage.Tools.cs:238-252</c>）の並びどおり。
    /// <b>文言・並びは暫定であり殿の裁可を要する</b>——変わればここも直す。
    /// <para>
    /// <b>【増分6で添字が 0〜5 から 4〜9 へ、増分8段2で 5〜10 へずれた】</b>既存4件が手前に入り、
    /// さらに「三相モータ 縦」が手前へ加わったゆえ。
    /// <b>添字を持たせたまま直したのは、並び順そのものを測る網でもあるため</b>
    /// ——タグで引く形に改めれば、この網は消える。
    /// </para></summary>
    public static IEnumerable<object[]> ExpectedEntries() => new[]
    {
        new object[] { 5, "Breaker3P#V", ElementKind.Breaker3P, "V" },
        new object[] { 6, "Breaker3P#H", ElementKind.Breaker3P, "H" },
        new object[] { 7, "ContactorMain3P#V", ElementKind.ContactorMain3P, "V" },
        new object[] { 8, "ContactorMain3P#H", ElementKind.ContactorMain3P, "H" },
        new object[] { 9, "ThermalOverload3P#V", ElementKind.ThermalOverload3P, "V" },
        new object[] { 10, "ThermalOverload3P#H", ElementKind.ThermalOverload3P, "H" },
    };

    /// <summary>T-133増分8 段2（殿裁定15＝案A）で足した「三相モータ 縦」。
    /// <b>3極記号と同じ <c>Kind#Orient</c> タグの形だが、別の <c>MemberData</c> へ分けてある</b>
    /// ——<c>IsEnabled</c> の束ね先が違う（3極記号＝<c>CanPlaceOnMainCircuit</c>／本件＝<c>CanEditDiagram</c>）ゆえ、
    /// <see cref="ExpectedEntries"/> へ混ぜれば主回路限定を測るテストが誤って本件にも当たる。
    /// <para>
    /// <b>【文言は殿裁定2026-08-06で確定】</b>「三相モータ 縦」——既存側も「三相モータ 横」へ改め、
    /// 対にした。侍が段2で「既存側は無印ゆえ名から横向きと読めぬ」と申し送った見立てが、
    /// 家老の検分（忍者の撮ったメニュー画）を経て殿の裁定となった形。
    /// <b>3極記号6項目（<see cref="ExpectedEntries"/>）の文言は依然として暫定である</b>
    /// ——増分4-C の裁定のままにて、確定したのはモータ2項目のみ。
    /// </para></summary>
    public static IEnumerable<object[]> ExpectedMotorEntries() => new[]
    {
        new object[] { 4, "Motor#V", ElementKind.Motor, "V" },
    };

    /// <summary><see cref="ExpectedMotorEntries"/> の添字だけを要するテスト向け。</summary>
    public static IEnumerable<object[]> MotorIndices() => ExpectedMotorEntries().Select(e => new[] { e[0] });

    /// <summary>T-133増分6（殿裁定8）で再掲した既存4件。原本 <c>OtherBuiltins</c> の 1〜4 番と同じ並び。
    /// <b>Id は <see cref="BasicPartTemplates"/> の定数から取る</b>——XAML に書いた綴りが
    /// テンプレート側の Id と食い違えば、ここで鳴る。</summary>
    public static IEnumerable<object[]> ExpectedPartEntries() => new[]
    {
        new object[] { 0, "part:" + BasicPartTemplates.SelectSwitchId, BasicPartTemplates.SelectSwitchId },
        new object[] { 1, "part:" + BasicPartTemplates.ThermalOverloadId, BasicPartTemplates.ThermalOverloadId },
        new object[] { 2, "part:" + BasicPartTemplates.EmergencyStopId, BasicPartTemplates.EmergencyStopId },
        new object[] { 3, "part:" + BasicPartTemplates.MotorId, BasicPartTemplates.MotorId },
    };

    /// <summary>添字だけを要するテスト向け。<b>上の2つから派生させ、並びの定義を二重に持たぬ。</b></summary>
    public static IEnumerable<object[]> SymbolIndices() => ExpectedEntries().Select(e => new[] { e[0] });

    /// <summary><see cref="SymbolIndices"/> と同じ理由で <see cref="ExpectedPartEntries"/> から派生させる。</summary>
    public static IEnumerable<object[]> PartIndices() => ExpectedPartEntries().Select(e => new[] { e[0] });

    private static MenuItem ItemAt(MainWindow window, int index)
        => (MenuItem)window.OtherSymbolsMenu.Items[index];

    /// <summary>3極記号の先頭（＝ブレーカ縦）の添字。
    /// <b>添字を直に書いたテストが増分8段2で対象を取り違えた</b>——手前へ「三相モータ 縦」が
    /// 入って添字が1つずれ、<c>ItemAt(window, 4)</c> が3極記号でなくモータを指すようになった。
    /// <b>一方は案内文言で鳴ったが、もう一方（<c>PartId</c> を持たぬことの確認）は
    /// モータも <c>PartId</c> を持たぬゆえ鳴らずに素通りした</b>——<b>測る対象がずれても通ってしまう形</b>ゆえ、
    /// 名前を与えて一箇所へ寄せる。</summary>
    private const int FirstSymbolIndex = 5;

    /// <summary>既存4件の末尾（＝「三相モータ 横」、<c>PartId</c> 経路）の添字。
    /// <b>隠密の検分（2026-08-06）を受けて名前を与えた</b>——挿入位置（添字4）より手前ゆえ増分8段2では
    /// ずれなんだが、<b><see cref="FirstSymbolIndex"/> と同じ弱さを抱えておる</b>（将来また前方へ項目が
    /// 挿入されれば黙って対象がずれる）。
    /// <b>「次のついでに」を選ばなんだのは、ついでの宛先が無ければ宛先ごと消えるゆえ</b>
    /// ——増分9がメニューへ手を入れる保証は無い。
    /// <para>
    /// なお他の期待値テストは <c>MemberData</c> 経由の派生ゆえ直書きを持たず、この型の穴は構造的に無い
    /// （隠密の検分）。<b>直書きが残っておったのは本件と <see cref="FirstSymbolIndex"/> の2箇所のみ。</b>
    /// </para></summary>
    private const int MotorPartIndex = 3;

    /// <summary>3極記号6項目（増分4-C分）だけを取り出す。既存4件は <c>part:</c> タグゆえ解析の対象が違い、
    /// 添字4の「三相モータ 縦」（増分8段2）は <c>Kind</c> タグだが主回路限定の枷を負わぬゆえ、いずれも外す。</summary>
    private static IEnumerable<MenuItem> SymbolItems(MainWindow window)
        => window.OtherSymbolsMenu.Items.Cast<MenuItem>().Skip(5);

    // ===== 観点A: メニューの中身 =====

    /// <summary>
    /// 既存4件（増分6）＋三相モータ縦1件（増分8段2）＋3種×縦横6件（増分4-C）で11項目。
    /// <b>増減があれば鳴る。</b>
    /// <para>
    /// <b>【原本の10エントリとは員数が揃わなくなった】</b>増分6までは原本 <c>OtherBuiltins</c> の
    /// 10エントリと一致しておったが、<b>原本に縦向きモータは無い</b>（侍が原本を実測）。
    /// <b>殿裁定5により ecad2 が新たに設けた1件ゆえ、原本より1件多いのが正である。</b>
    /// </para>
    /// </summary>
    [Fact]
    public void その他図形は十一項目を持つ()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();

            Assert.Equal(11, window.OtherSymbolsMenu.Items.Count);
        });

    /// <summary>
    /// 各項目のタグが <see cref="SymbolTagParser"/> で解け、期待どおりの種別と向きになること。
    /// <b>XAML に書いたタグの綴り誤りを捕らえる網</b>——綴りを誤れば解析が失敗し、
    /// メニューを押しても何も起きぬ（実機でしか気づけぬ形になる）。
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpectedEntries))]
    public void 各項目のタグが種別と向きへ解ける(int index, string expectedTag, ElementKind expectedKind, string expectedOrient)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var item = ItemAt(window, index);

            Assert.Equal(expectedTag, item.Tag);
            Assert.True(SymbolTagParser.TryParse((string)item.Tag, out var kind, out var orient));
            Assert.Equal(expectedKind, kind);
            Assert.Equal(expectedOrient, orient);
        });

    /// <summary>
    /// 縦横が対で揃っていること。<b>片方だけ足す／片方だけ消す誤りへの網</b>
    /// ——3種それぞれに V と H が1つずつ在る。
    /// </summary>
    [Fact]
    public void 三種それぞれに縦横が対で在る()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var parsed = SymbolItems(window)
                .Select(i => { SymbolTagParser.TryParse((string)i.Tag, out var k, out var o); return (Kind: k, Orient: o); })
                .ToList();

            foreach (var kind in new[] { ElementKind.Breaker3P, ElementKind.ContactorMain3P, ElementKind.ThermalOverload3P })
            {
                Assert.Single(parsed.Where(p => p.Kind == kind && p.Orient == "V"));
                Assert.Single(parsed.Where(p => p.Kind == kind && p.Orient == "H"));
            }
        });

    /// <summary>
    /// T-133増分6: 再掲した4件のタグが <c>"part:&lt;PartId&gt;"</c> 形式で、
    /// <see cref="BasicPartTemplates"/> の Id を指すこと。
    /// <b>綴り誤りは実機でも「押しても何も起きぬ」形で現れるゆえ気づきにくい</b>——ここで捕らえる。
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpectedPartEntries))]
    public void 再掲した四件のタグが既存パーツのIdを指す(int index, string expectedTag, string expectedPartId)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var item = ItemAt(window, index);

            Assert.Equal(expectedTag, item.Tag);
            Assert.StartsWith("part:", (string)item.Tag);
            Assert.Equal(expectedPartId, ((string)item.Tag)["part:".Length..]);
        });

    /// <summary>
    /// T-133増分6: 再掲した4件が <see cref="BasicPartTemplates.All"/> に実在すること。
    /// <b>「メニューには在るがテンプレートには無い」を捕らえる</b>——Id を打ち間違えても
    /// 上のテストは <see cref="ExpectedPartEntries"/> 側も同じ定数を使うゆえ鳴らぬが、
    /// テンプレートから当該パーツが消えればこちらが鳴る。
    /// </summary>
    [Theory]
    [InlineData(BasicPartTemplates.SelectSwitchId)]
    [InlineData(BasicPartTemplates.ThermalOverloadId)]
    [InlineData(BasicPartTemplates.EmergencyStopId)]
    [InlineData(BasicPartTemplates.MotorId)]
    public void 再掲した四件はテンプレートに実在する(string expectedPartId)
        => Assert.Single(BasicPartTemplates.All().Where(p => p.Id == expectedPartId));

    // ===== 観点B: Click の配線（本テスト群の主題） =====

    /// <summary>
    /// メニュー項目を押せば配置ツールが切り替わること。
    /// <b>これが「4-A・4-B が実際に呼ばれる」ことの証である。</b>
    /// タグ解析（4-A）を通り、<c>ToolState</c> へ種別と向きが載る。
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpectedEntries))]
    public void 項目を押せば配置ツールが切り替わる(int index, string _, ElementKind expectedKind, string expectedOrient)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;

            ItemAt(window, index).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(ToolMode.PlaceElement, vm.Tool.Mode);
            Assert.Equal(expectedKind, vm.Tool.Kind);
            Assert.Equal(expectedOrient, vm.Tool.Orient);
        });

    /// <summary>
    /// 切り替えた配置ツールは <c>PartId</c> を持たぬこと。
    /// <b>Kind 経路と PartId 経路が混ざっておらぬこと</b>を押さえる
    /// ——両方が載れば <c>TryPlaceActiveTool</c> の分岐が意図せぬ側へ流れる。
    /// </summary>
    [Fact]
    public void 切り替えた配置ツールはPartIdを持たぬ()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;

            ItemAt(window, FirstSymbolIndex).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Null(vm.Tool.PartId);
            Assert.False(vm.Tool.IsOr);
        });

    /// <summary>押せば案内が出ること（既存の配置ツール選択と同じ作法）。</summary>
    [Fact]
    public void 項目を押せば案内が出る()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;

            ItemAt(window, FirstSymbolIndex).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Contains("配置ツール", vm.StatusMessage);
            Assert.Contains("ブレーカ", vm.StatusMessage);
        });

    /// <summary>
    /// T-133増分6: 再掲した4件を押せば <c>PartId</c> 経路の配置ツールへ切り替わること。
    /// <b>本増分の主題</b>——タグから Id を解き、部品ライブラリで引き当てるところまでが繋がる。
    /// <c>Kind</c> が載らぬことも併せて押さえる（3極記号側と混ざれば
    /// <c>TryPlaceActiveTool</c> の分岐が Kind 側へ流れてしまう）。
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpectedPartEntries))]
    public void 再掲した四件を押せばパーツ配置ツールへ切り替わる(int index, string _, string expectedPartId)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;

            ItemAt(window, index).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(ToolMode.PlaceElement, vm.Tool.Mode);
            Assert.Equal(expectedPartId, vm.Tool.PartId);
            Assert.Null(vm.Tool.Kind);
            Assert.Null(vm.Tool.Orient);
            Assert.False(vm.Tool.IsOr);
        });

    /// <summary>T-133増分6: 再掲した4件も押せば案内が出ること。<b>表示名がそのまま出る</b>
    /// ——メニューの表示名（「三相モータ 横」）であり、テンプレートの <c>Name</c>（「モータ」）ではない。
    /// <para>
    /// <b>【期待値に「横」まで含めるのは、部分一致で素通りするのを防ぐため】</b>殿裁定2026-08-06で
    /// 「三相モータ」→「三相モータ 横」へ改まったが、<b>旧文言は新文言の部分文字列ゆえ、
    /// <c>Contains("三相モータ")</c> のままでは改名を差し戻しても鳴らぬ</b>——測る対象が変わったのに
    /// 期待値がそれを追わぬ形になる。
    /// </para></summary>
    [Fact]
    public void 再掲した四件を押せば表示名のまま案内が出る()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;

            ItemAt(window, MotorPartIndex).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Contains("配置ツール", vm.StatusMessage);
            Assert.Contains("三相モータ 横", vm.StatusMessage);
        });

    // ===== 観点C: (f) 主回路限定の予防側 =====

    /// <summary>
    /// 3極記号6項目の <c>IsEnabled</c> が <c>CanPlaceOnMainCircuit</c> へ束ねられていること。
    /// <b>殿裁定2026-07-28＝「制御回路シートではメニュー項目を無効化（グレーアウト）」の予防側。</b>
    /// <para>
    /// <b>【増分6で束ねる場所がサブメニューから項目へ移った】</b>殿裁可2026-08-06。
    /// 射程外の既存4件が配下へ入るゆえ、枷を殿裁定4の射程（3極記号のみ）へ合わせ直した。
    /// </para>
    /// <para>
    /// バインドの有無そのものを見るのは、<b>値だけを見ると「たまたま両方 false」で通ってしまう</b>ため
    /// ——文書が無い起動直後は <c>CanPlaceOnMainCircuit</c> も <c>IsEnabled</c> も偽になる。
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(SymbolIndices))]
    public void 三極記号は主回路シートでのみ有効になるよう束ねられている(int index)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();

            var binding = System.Windows.Data.BindingOperations.GetBinding(
                ItemAt(window, index), UIElement.IsEnabledProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CanPlaceOnMainCircuit), binding!.Path.Path);
        });

    /// <summary>
    /// T-133増分6: 再掲した4件は <c>CanEditDiagram</c> へ束ねられていること。
    /// <b>主回路限定の枷を負わぬ</b>——これらは <c>SheetAffinity.Any</c>（<c>PartDefinition.cs:87</c> の既定）
    /// にて制御回路シートにも置けるゆえ、部品リスト経由と可否を揃える。
    /// </summary>
    [Theory]
    [MemberData(nameof(PartIndices))]
    public void 再掲した四件は編集可否のみで束ねられている(int index)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();

            var binding = System.Windows.Data.BindingOperations.GetBinding(
                ItemAt(window, index), UIElement.IsEnabledProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CanEditDiagram), binding!.Path.Path);
        });

    // ===== 観点D: T-133増分8 段2 —— 縦向きモータの掲出（殿裁定15＝案A） =====

    /// <summary>
    /// 「三相モータ 縦」のタグが <see cref="SymbolTagParser"/> で <see cref="ElementKind.Motor"/> と
    /// 向き <c>"V"</c> へ解けること。
    /// <b><see cref="SymbolTagParser"/> は種別を制限せぬ設計ゆえ、解析側は無改修で通る</b>
    /// （同クラスのコメント＝「その種別に向きを付けてよいかまでは検査せぬ。組み合わせの正しさは
    /// タグを書くメニュー定義の側が負う」）——<b>ならばその責を負うのは本テストである。</b>
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpectedMotorEntries))]
    public void モータ縦のタグが種別と向きへ解ける(int index, string expectedTag, ElementKind expectedKind, string expectedOrient)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var item = ItemAt(window, index);

            Assert.Equal(expectedTag, item.Tag);
            Assert.True(SymbolTagParser.TryParse((string)item.Tag, out var kind, out var orient));
            Assert.Equal(expectedKind, kind);
            Assert.Equal(expectedOrient, orient);
        });

    /// <summary>
    /// 「三相モータ 縦」を押せば <c>Kind</c> 経路の配置ツールへ切り替わること。
    /// <b><c>PartId</c> が載らぬことを併せて押さえる</b>——直前の項目「三相モータ」は <c>PartId</c> 経路にて、
    /// 両者は隣り合いながら別の経路へ流れる。<b>取り違えれば、置かれる要素そのものが変わる</b>
    /// （<c>PartId</c> 経路なら接続点なし・高さ2、<c>Kind</c> 経路なら青の3点・縦向き描画）。
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpectedMotorEntries))]
    public void モータ縦を押せばKind経路の配置ツールへ切り替わる(int index, string _, ElementKind expectedKind, string expectedOrient)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;

            ItemAt(window, index).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(ToolMode.PlaceElement, vm.Tool.Mode);
            Assert.Equal(expectedKind, vm.Tool.Kind);
            Assert.Equal(expectedOrient, vm.Tool.Orient);
            Assert.Null(vm.Tool.PartId);
            Assert.False(vm.Tool.IsOr);
        });

    /// <summary>
    /// 「三相モータ 縦」は <c>CanEditDiagram</c> へ束ねられていること。
    /// <b>3極記号と同じ <c>Kind</c> 経路でありながら、枷の束ね先が違う</b>——
    /// <c>ElementCatalog.SheetAffinityOf(Motor)</c> は既定の <c>Any</c> にて主回路限定ではないゆえ、
    /// 部品リスト経由・既存4件と可否を揃える。
    /// <b>「Kind 経路だから主回路限定」と早合点して <c>CanPlaceOnMainCircuit</c> を束ねれば、
    /// 制御回路シートで置けなくなる</b>——増分6で解いた齟齬と同型の穴が、こちらへ移るだけになる。
    /// </summary>
    [Theory]
    [MemberData(nameof(MotorIndices))]
    public void モータ縦は編集可否のみで束ねられている(int index)
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();

            var binding = System.Windows.Data.BindingOperations.GetBinding(
                ItemAt(window, index), UIElement.IsEnabledProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CanEditDiagram), binding!.Path.Path);
        });

    /// <summary>
    /// T-133増分6: サブメニュー自体には <c>IsEnabled</c> のバインドが残っておらぬこと。
    /// <b>枷を「移した」ことの証である</b>——項目側へ足しただけでサブメニュー側を外し忘れれば、
    /// 制御回路シートで既存4件ごとグレーアウトしたままになる（<b>それこそが本増分で解いた齟齬</b>）。
    /// <b>項目側のバインドを見るテストだけでは、この取り零しを素通しする。</b>
    /// </summary>
    [Fact]
    public void サブメニュー自体には主回路限定の枷が残っておらぬ()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();

            var binding = System.Windows.Data.BindingOperations.GetBinding(
                window.OtherSymbolsMenu, UIElement.IsEnabledProperty);

            Assert.Null(binding);
        });
}
