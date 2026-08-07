using System.IO;
using System.Windows;
using System.Windows.Controls;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分9の掲出（殿裁定2026-08-07＝案A）: ピン留め済み自作図形を「その他図形」メニューの末尾へ出す。
/// <para>
/// <b>【登録・解除は <see cref="PinnedPartToggleTests"/> の受け持ち】</b>あちらが「集合が切り替わり
/// ディスクへ残るところまで」を測り、こちらは<b>「その集合がメニューの形になるところ」</b>を測る。
/// </para>
/// <para>
/// <b>【三層に分けて測る】</b>(A) どの図形を出すか＝<see cref="PartPaletteViewModel.PinnedEntries"/>／
/// (B) それをどう <c>MenuItem</c> へ変えるか＝<see cref="MainWindow.RebuildPinnedSection"/>／
/// (C) 開いた時に実際に呼ばれるか＝<see cref="MainWindow"/> を立てて <c>SubmenuOpened</c> を起こす。
/// <b>(A)(B) を分けたのは <c>TogglePin</c> が実MyDocumentsへ書くゆえ</b>——
/// <see cref="MainWindow"/> を立てるテストではピン留め済みの状態を作れぬ
/// （<see cref="PartPaletteViewModel"/> の申し合わせ、P-019 の再来を防ぐため）。
/// </para>
/// <para>
/// <b>【測っておらぬこと・侍が自ら区切る】</b>(C) は<b>殿の環境に実際のピン留めが在るか否かに
/// 結果が左右されぬ形に限った</b>——その代償として、<b>ピン留めが0件の環境では (C) の2件は
/// 網として働かぬ。</b>
/// <b>これは見立てではなく壊す実測で判じた</b>——改変A（0件ガードを除く）・改変B（作り直しの際に
/// 前回分を除かぬ）・改変G（末尾でなく先頭へ挿れる）のいずれを当てても、観点B の各件は鳴ったが
/// <b>(C) の2件は GREEN のまま通った</b>（測った環境のピン留めが0件ゆえ、そもそも掲出分が生えぬ）。
/// <b>当初この節へ「殿の環境に左右されぬ」とだけ書いたのは、通る側だけを見た誤りであった</b>
/// ——<b>「何件でも通る」と「壊れた時に鳴る」は別の主張にござる。</b>
/// <b>ピン留めが在る状態で末尾に正しく生えるところは、(B) で生成を測り、実機確認へ委ねる。</b>
/// 素通しされうるのは<b>「生成は正しいが、ハンドラが <see cref="PartPaletteViewModel.PinnedEntries"/> でない
/// 別の一覧を渡しておる」</b>という形である。
/// <b>Bubble 再発火のガード（<c>e.OriginalSource</c> の同一性判定）も測れておらぬ</b>——
/// <b>今の配下は葉の <c>MenuItem</c> のみで子孫が <c>SubmenuOpened</c> を発火せぬゆえ、
/// ガードの有無で結果が変わらぬ</b>（将来サブメニューを持つ項目が入って初めて差が出る）。
/// </para>
/// </summary>
public class PinnedPartMenuTests : IDisposable
{
    private readonly string _dir;
    private readonly PartFolderStore _folderStore;
    private readonly PinnedPartStore _pinnedStore;

    public PinnedPartMenuTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ecad2-pin-menu-tests", Guid.NewGuid().ToString("N"));
        _folderStore = new PartFolderStore(_dir);
        _pinnedStore = new PinnedPartStore(Path.Combine(_dir, "pinned-parts.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private PartPaletteViewModel CreateViewModel() => new(_folderStore, _pinnedStore);

    private static PartFolderEntry EntryOf(string id, string name)
        => new("自作", $"{id}.gcadpart", new PartDefinition { Id = id, Name = name });

    // ===== 観点A: どの図形を出すか（選定） =====

    [Fact]
    public void ピン留めが無ければ掲出は空()
    {
        var vm = CreateViewModel();

        Assert.Empty(vm.PinnedEntries);
    }

    /// <summary>ピン留めしたものだけが出る。<b>1件だけを見るテストでは「全件が出る」誤りを素通しする</b>
    /// ゆえ、出ぬ側も併せて押さえる。</summary>
    [Fact]
    public void ピン留めしたものだけが掲出される()
    {
        var vm = CreateViewModel();

        vm.TogglePin(BasicPartTemplates.MotorId);

        Assert.Single(vm.PinnedEntries);
        Assert.Equal(BasicPartTemplates.MotorId, vm.PinnedEntries[0].Definition.Id);
    }

    /// <summary>
    /// <b>並びは一覧（<see cref="PartPaletteViewModel.Entries"/>）の順に従い、ピン留めした順ではない。</b>
    /// 原本 GuiEcad も <c>HashSet</c> の順を使わず <c>_folderEntries</c> を回す（<c>MainPage.Parts.cs:76-78</c>）。
    /// <para>
    /// <b>【入力の選び方】</b>自作図形の名を <c>"あ"</c>（U+3042）とした——<b>基本図形のどの名よりも
    /// 文字コードが小さい</b>ゆえ、<c>Category</c> を見ずに名だけで並べ替える実装なら自作が先へ来て鳴る。
    /// <b>一覧は Category昇順→名前昇順</b>（<c>PartFolderStore.cs:151-152</c>）にて、
    /// 基本図形（<c>Category==""</c>）が自作より先に来るのが正である。
    /// <b>ピン留めは自作を先に打つ</b>——集合の挿入順で並べる実装ならこれで鳴る。
    /// </para></summary>
    [Fact]
    public void 掲出の並びは一覧の順に従いピン留めした順ではない()
    {
        _folderStore.SaveCustom(new PartDefinition { Id = "custom-a", Name = "あ" });

        var vm = CreateViewModel();
        vm.TogglePin("custom-a");
        vm.TogglePin(BasicPartTemplates.MotorId);

        Assert.Equal(
            new[] { BasicPartTemplates.MotorId, "custom-a" },
            vm.PinnedEntries.Select(e => e.Definition.Id));
        Assert.Equal("", vm.PinnedEntries[0].Category);
        Assert.Equal("自作", vm.PinnedEntries[1].Category);
    }

    /// <summary>消えた図形の Id は掲出されぬ（一覧の側を回すゆえ自然に落ちる）。
    /// <b>掃除はされておらぬ</b>——集合には残る。<see cref="PinnedPartToggleTests"/> の
    /// 「存在せぬIdを渡しても例外を投げぬ」と対を成し、<b>あちらが「残る」ことを、
    /// こちらが「出ぬ」ことを押さえる。</b></summary>
    [Fact]
    public void 存在せぬIdは掲出されぬ()
    {
        var vm = CreateViewModel();

        vm.TogglePin("no-such-part-id");

        Assert.True(vm.IsPinned("no-such-part-id"));
        Assert.Empty(vm.PinnedEntries);
    }

    // ===== 観点B: どう MenuItem へ変えるか（生成） =====

    /// <summary>静的項目に見立てた土台を作る。<b>員数は3で足りる</b>——生成側は静的項目の数を
    /// 一切見ぬ形にしてあり、実際の11という数に依存させると本テストが XAML の増減で鳴くようになる。</summary>
    private static MenuItem StaticMenu()
    {
        var menu = new MenuItem();
        menu.Items.Add(new MenuItem { Header = "静的1" });
        menu.Items.Add(new MenuItem { Header = "静的2" });
        menu.Items.Add(new MenuItem { Header = "静的3" });
        return menu;
    }

    /// <summary>ピン留めが無ければセパレータも出さぬ（原本 <c>MainPage.Parts.cs:79</c> と同じ）。
    /// <b>0件でも区切り線だけが出る形は、使い手には「何かが在るはずの空欄」に見える。</b></summary>
    [Fact]
    public void ピン留めが無ければセパレータも出さぬ()
        => StaTestRunner.Run(() =>
        {
            var menu = StaticMenu();
            var tracked = new List<object>();

            MainWindow.RebuildPinnedSection(menu, tracked, Array.Empty<PartFolderEntry>(), (_, _) => { });

            Assert.Equal(3, menu.Items.Count);
            Assert.Empty(tracked);
        });

    /// <summary>セパレータ1つに続けて各項目が並ぶこと。<b>静的項目の後ろに付く</b>ことも併せて見る
    /// ——手前へ挿れれば既存の添字がずれる（増分8で二度踏んだ形）。</summary>
    [Fact]
    public void セパレータに続けて掲出分が末尾へ並ぶ()
        => StaTestRunner.Run(() =>
        {
            var menu = StaticMenu();
            var tracked = new List<object>();

            MainWindow.RebuildPinnedSection(menu, tracked,
                new[] { EntryOf("p1", "いろは"), EntryOf("p2", "にほへ") }, (_, _) => { });

            Assert.Equal(6, menu.Items.Count);
            Assert.Equal("静的3", ((MenuItem)menu.Items[2]).Header);
            Assert.IsType<Separator>(menu.Items[3]);
            Assert.Equal("いろは", ((MenuItem)menu.Items[4]).Header);
            Assert.Equal("にほへ", ((MenuItem)menu.Items[5]).Header);
        });

    /// <summary>見出しは図形の名そのまま（原本 <c>Text = e.Definition.Name</c>）、
    /// タグは <c>"part:"</c> を冠した Id。
    /// <b>接頭辞は増分6で定めた経路の弁別</b>——同じサブメニューに <c>Kind#Orient</c> 形式が同居するゆえ、
    /// 冠を落とせば <see cref="MainWindow.OtherPartMenuItem_Click"/> が解析に失敗し
    /// <b>押しても何も起きぬ（実機でしか気づけぬ形になる）。</b></summary>
    [Fact]
    public void 掲出項目の見出しは図形の名でタグはpart接頭辞つきのId()
        => StaTestRunner.Run(() =>
        {
            var menu = StaticMenu();
            var tracked = new List<object>();

            MainWindow.RebuildPinnedSection(menu, tracked, new[] { EntryOf("custom-a", "あ") }, (_, _) => { });

            var item = (MenuItem)menu.Items[4];
            Assert.Equal("あ", item.Header);
            Assert.Equal("part:custom-a", item.Tag);
        });

    /// <summary>押せば渡したハンドラが呼ばれること。<b>Click を配線し忘れれば、
    /// 見た目は正しく並ぶのに押しても無反応になる</b>——生成の形だけを見るテストでは素通しする。</summary>
    [Fact]
    public void 掲出項目を押せば渡したハンドラが呼ばれる()
        => StaTestRunner.Run(() =>
        {
            var menu = StaticMenu();
            var tracked = new List<object>();
            object? clicked = null;

            MainWindow.RebuildPinnedSection(menu, tracked, new[] { EntryOf("custom-a", "あ") },
                (sender, _) => clicked = sender);

            ((MenuItem)menu.Items[4]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Same(menu.Items[4], clicked);
        });

    /// <summary>
    /// <c>IsEnabled</c> が <c>CanEditDiagram</c> へ束ねられていること（既存4件・「三相モータ 縦」と同じ）。
    /// <b>自作図形は主回路限定の枷を負わぬ</b>ゆえ、部品リスト経由と可否を揃える。
    /// <para>
    /// バインドの有無そのものを見るのは <c>MenuPlacementToolTests</c> と同じ理由——
    /// <b>値だけを見ると「たまたま両方 false」で通ってしまう。</b>
    /// </para></summary>
    [Fact]
    public void 掲出項目は編集可否へ束ねられている()
        => StaTestRunner.Run(() =>
        {
            var menu = StaticMenu();
            var tracked = new List<object>();

            MainWindow.RebuildPinnedSection(menu, tracked, new[] { EntryOf("custom-a", "あ") }, (_, _) => { });

            var binding = System.Windows.Data.BindingOperations.GetBinding(
                (MenuItem)menu.Items[4], UIElement.IsEnabledProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CanEditDiagram), binding!.Path.Path);
        });

    /// <summary><b>作り直しても重ならぬこと</b>——メニューは開くたびに作り直されるゆえ、
    /// 前回の掲出分を除かねば開くたびに増える。<b>使い手の目には「同じ図形が二つ三つ並ぶ」形で現れる。</b></summary>
    [Fact]
    public void 作り直しても掲出分は重ならぬ()
        => StaTestRunner.Run(() =>
        {
            var menu = StaticMenu();
            var tracked = new List<object>();
            var pinned = new[] { EntryOf("p1", "いろは") };

            MainWindow.RebuildPinnedSection(menu, tracked, pinned, (_, _) => { });
            MainWindow.RebuildPinnedSection(menu, tracked, pinned, (_, _) => { });
            MainWindow.RebuildPinnedSection(menu, tracked, pinned, (_, _) => { });

            Assert.Equal(5, menu.Items.Count);
        });

    /// <summary>ピン留めが減れば掲出も減り、<b>0件になれば静的項目だけへ戻る</b>（セパレータも消える）。
    /// <b>増える側だけを測ると、解除しても残り続ける穴を素通しする。</b></summary>
    [Fact]
    public void ピン留めを解けば掲出も消える()
        => StaTestRunner.Run(() =>
        {
            var menu = StaticMenu();
            var tracked = new List<object>();

            MainWindow.RebuildPinnedSection(menu, tracked,
                new[] { EntryOf("p1", "いろは"), EntryOf("p2", "にほへ") }, (_, _) => { });
            MainWindow.RebuildPinnedSection(menu, tracked, Array.Empty<PartFolderEntry>(), (_, _) => { });

            Assert.Equal(3, menu.Items.Count);
            Assert.Equal("静的1", ((MenuItem)menu.Items[0]).Header);
            Assert.Equal("静的3", ((MenuItem)menu.Items[2]).Header);
        });

    /// <summary>作り直しの際に静的項目を巻き込まぬこと。
    /// <b>「n件より後ろを消す」形にすればここが壊れうる</b>——静的項目が増減すれば n がずれ、
    /// 静的項目を消すか古い掲出分を残すかのどちらかで黙って壊れる。
    /// <b>足したものを覚えておく形にしてあるゆえ、静的項目の員数に関わりが無い</b>ことを固定する。</summary>
    [Fact]
    public void 作り直しても静的項目は残る()
        => StaTestRunner.Run(() =>
        {
            var menu = StaticMenu();
            var tracked = new List<object>();

            MainWindow.RebuildPinnedSection(menu, tracked, new[] { EntryOf("p1", "いろは") }, (_, _) => { });
            MainWindow.RebuildPinnedSection(menu, tracked, new[] { EntryOf("p2", "にほへ") }, (_, _) => { });

            Assert.Equal(5, menu.Items.Count);
            Assert.Equal("静的1", ((MenuItem)menu.Items[0]).Header);
            Assert.Equal("静的2", ((MenuItem)menu.Items[1]).Header);
            Assert.Equal("静的3", ((MenuItem)menu.Items[2]).Header);
            Assert.Equal("にほへ", ((MenuItem)menu.Items[4]).Header);
        });

    // ===== 観点C: 開いた時に実際に呼ばれるか（繋ぎ込み） =====

    /// <summary>
    /// 「その他図形」を開いても<b>静的11項目は先頭に残る</b>こと。
    /// <b>掲出は末尾へ付く（殿裁定＝案A）</b>ゆえ、増分8までに測った添字がそのまま生きる。
    /// <para>
    /// <b>【殿の環境のピン留めに左右されぬ形にしてある】</b>末尾に何件付くかは実 MyDocuments の
    /// <c>pinned-parts.json</c> 次第ゆえ、<b>総数ではなく先頭11項目のタグを見る</b>。
    /// <b>ただしその代償として、0件の環境では何を壊しても鳴らぬ</b>——クラスの冒頭に実測の記録あり。
    /// <b>掲出が手前へ挿れられる誤りは、観点B の「セパレータに続けて掲出分が末尾へ並ぶ」が負う</b>
    /// （改変G を当てて実測済み）。
    /// </para></summary>
    [Fact]
    public void その他図形を開いても静的十一項目は先頭に残る()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var before = window.OtherSymbolsMenu.Items.Cast<object>().Take(11)
                .Select(i => ((MenuItem)i).Tag).ToList();

            window.OtherSymbolsMenu.RaiseEvent(
                new RoutedEventArgs(MenuItem.SubmenuOpenedEvent, window.OtherSymbolsMenu));

            var after = window.OtherSymbolsMenu.Items.Cast<object>().Take(11)
                .Select(i => ((MenuItem)i).Tag).ToList();

            Assert.Equal(11, before.Count);
            Assert.Equal(before, after);
        });

    /// <summary>
    /// <b>二度開いても項目数が変わらぬこと</b>——これが「開くたびに増える」を捕らえる網である。
    /// <b>ピン留めが何件であっても「通る」形</b>にしてある（0件なら両方とも11、n件なら両方とも 11+1+n）。
    /// <para>
    /// <b>【されど0件の環境では鳴らぬ・実測で判じた】</b>改変B（作り直しの際に前回分を除かぬ）を
    /// 当てたところ、観点B の3件は鳴ったが<b>本テストは GREEN のまま通った</b>——増える分が無いゆえ。
    /// <b>「何件でも通る」ことは、「壊れた時に鳴る」ことを意味せぬ。</b>
    /// 重複そのものの検出は観点B の「作り直しても掲出分は重ならぬ」が負う。
    /// </para>
    /// <para>
    /// <b>【観点B の「作り直しても重ならぬ」との違い】</b>あちらは生成そのものを、
    /// <b>こちらはハンドラが同じ器（<c>_pinnedMenuItems</c>）を持ち回っておることを測る</b>
    /// ——毎回新しい空の器を渡す実装なら、あちらは通り、こちらだけが鳴る
    /// （<b>ただしこれもピン留めが1件以上ある環境に限る</b>）。
    /// </para></summary>
    [Fact]
    public void その他図形を二度開いても項目は増えぬ()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var opened = new RoutedEventArgs(MenuItem.SubmenuOpenedEvent, window.OtherSymbolsMenu);

            window.OtherSymbolsMenu.RaiseEvent(opened);
            var first = window.OtherSymbolsMenu.Items.Count;
            window.OtherSymbolsMenu.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent, window.OtherSymbolsMenu));
            var second = window.OtherSymbolsMenu.Items.Count;

            Assert.Equal(first, second);
        });
}
