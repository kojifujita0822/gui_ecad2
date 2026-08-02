using System.Windows;
using System.Windows.Controls;
using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分4-C: 「その他図形」メニューから主回路3極記号を配置する導線。
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
/// </para>
/// </summary>
public class MenuPlacementToolTests
{
    /// <summary>原本 GuiEcad の <c>OtherBuiltins</c> 配列（<c>MainPage.Tools.cs:238-252</c>）の並びどおり。
    /// <b>文言・並びは暫定であり殿の裁可を要する</b>——変わればここも直す。</summary>
    public static IEnumerable<object[]> ExpectedEntries() => new[]
    {
        new object[] { 0, "Breaker3P#V", ElementKind.Breaker3P, "V" },
        new object[] { 1, "Breaker3P#H", ElementKind.Breaker3P, "H" },
        new object[] { 2, "ContactorMain3P#V", ElementKind.ContactorMain3P, "V" },
        new object[] { 3, "ContactorMain3P#H", ElementKind.ContactorMain3P, "H" },
        new object[] { 4, "ThermalOverload3P#V", ElementKind.ThermalOverload3P, "V" },
        new object[] { 5, "ThermalOverload3P#H", ElementKind.ThermalOverload3P, "H" },
    };

    private static MenuItem ItemAt(MainWindow window, int index)
        => (MenuItem)window.OtherSymbolsMenu.Items[index];

    // ===== 観点A: メニューの中身 =====

    /// <summary>3種 × 縦横で6項目。<b>増減があれば鳴る</b>——原本の6エントリと員数を合わせる。</summary>
    [Fact]
    public void その他図形は六項目を持つ()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();

            Assert.Equal(6, window.OtherSymbolsMenu.Items.Count);
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
            var parsed = window.OtherSymbolsMenu.Items.Cast<MenuItem>()
                .Select(i => { SymbolTagParser.TryParse((string)i.Tag, out var k, out var o); return (Kind: k, Orient: o); })
                .ToList();

            foreach (var kind in new[] { ElementKind.Breaker3P, ElementKind.ContactorMain3P, ElementKind.ThermalOverload3P })
            {
                Assert.Single(parsed.Where(p => p.Kind == kind && p.Orient == "V"));
                Assert.Single(parsed.Where(p => p.Kind == kind && p.Orient == "H"));
            }
        });

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

            ItemAt(window, 0).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

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

            ItemAt(window, 0).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Contains("配置ツール", vm.StatusMessage);
            Assert.Contains("ブレーカ", vm.StatusMessage);
        });

    // ===== 観点C: (f) 主回路限定の予防側 =====

    /// <summary>
    /// サブメニューの <c>IsEnabled</c> が <c>CanPlaceOnMainCircuit</c> へ束ねられていること。
    /// <b>殿裁定2026-07-28＝「制御回路シートではメニュー項目を無効化（グレーアウト）」の予防側。</b>
    /// <para>
    /// バインドの有無そのものを見るのは、<b>値だけを見ると「たまたま両方 false」で通ってしまう</b>ため
    /// ——文書が無い起動直後は <c>CanPlaceOnMainCircuit</c> も <c>IsEnabled</c> も偽になる。
    /// </para>
    /// </summary>
    [Fact]
    public void サブメニューは主回路シートでのみ有効になるよう束ねられている()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();

            var binding = System.Windows.Data.BindingOperations.GetBinding(
                window.OtherSymbolsMenu, UIElement.IsEnabledProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CanPlaceOnMainCircuit), binding!.Path.Path);
        });
}
