using System.IO;
using Ecad2.App.ViewModels;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-149（殿ご裁可2026-08-08）: <see cref="PartPaletteViewModel"/> の引数なし版・1引数版を廃し、
/// 保存先を2つとも呼び手に選ばせる。従前は「<c>TogglePin</c> を呼ぶテストは必ず一時フォルダの
/// <see cref="PinnedPartStore"/> を注入すること」が docコメントによる申し合わせにすぎず、
/// コンパイラに強制されていなかった（P-177、隠密の検分2026-08-06）。
/// <para>
/// <b>型で強制されるようになったこと自体はテストでは測れぬ</b>——コンパイルエラーになる形は
/// テストとして表現できぬゆえ。測れるのは「渡した <see cref="PinnedPartStore"/> が実際に使われるか」
/// までであり、本テスト群はそこを押さえる。
/// </para>
/// <para>
/// <b>読む側で測る（書く側では測らぬ）</b>——注入が効いていない場合、書く側で測ると壊す実測の際に
/// 実MyDocuments へ書き込んでしまう（P-019 の再来）。読む側なら、注入が効いていなくとも
/// 実MyDocuments を読むだけで済む。
/// </para>
/// <para>
/// <b>案Aの射程＝塞いだのは片方のみ</b>。本変更が塞ぐのは <see cref="PartPaletteViewModel"/> を
/// 直に作る経路であって、「テストが実MyDocuments を掴む経路」を塞ぎ切るものではない。
/// <c>new MainWindow()</c> を呼ぶテスト（実測19箇所・3ファイル）は本番コンストラクタを通るため、
/// 今も実MyDocuments を掴む。ただし現時点でその経路から <c>TogglePin</c> を呼ぶテストは0件
/// （侍の実測2026-08-08）。
/// </para>
/// </summary>
public class T149PinnedStoreInjectionTests : IDisposable
{
    private readonly string _dir;

    public T149PinnedStoreInjectionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ecad2-t149-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private PinnedPartStore TempPinnedStore() => new(Path.Combine(_dir, "pinned-parts.json"));

    [Fact]
    public void MainWindowViewModelへ渡したPinnedPartStoreがPartPaletteで使われる()
    {
        var pinnedStore = TempPinnedStore();
        pinnedStore.Save(new[] { "injected-id" });

        var vm = new MainWindowViewModel(new PartFolderStore(_dir), new ImmediateDispatcherService(), pinnedStore);

        Assert.True(vm.PartPalette.IsPinned("injected-id"));
    }

    /// <summary>素朴なベースライン（memory: feedback_control_experiment_needs_naive_baseline）。
    /// 空の store を渡せばピン留めは無い。これが無いと、上の1件が「渡した store を読んだ」のか
    /// 「たまたま何かが true を返した」のかを弁別できぬ。
    /// <para>
    /// <b>【この1件は測る力が環境に左右される・侍が自ら区切る】</b>注入を壊す改変（渡された store を
    /// 無視して <see cref="PinnedPartStore.CreateDefault"/> を使う）を当てても、<b>実MyDocuments に
    /// ピン留めが1件も無ければ本件は鳴らぬ</b>——空の store を渡した場合と同じ「ピン留め無し」に
    /// 落ちるゆえ。2026-08-08 の実測では実際に鳴らなかった（測った環境のピン留めが0件）。
    /// <b>鳴ったのは上の1件のみ</b>であり、そちらは実在せぬ Id を使うため環境に左右されぬ。
    /// これは <c>PinnedPartMenuTests</c> の (C) が踏んだのと同型の限界にござる。
    /// </para></summary>
    [Fact]
    public void 空のPinnedPartStoreを渡せばピン留めは無い()
    {
        var vm = new MainWindowViewModel(new PartFolderStore(_dir), new ImmediateDispatcherService(),
                                         TempPinnedStore());

        Assert.False(vm.PartPalette.IsPinned("injected-id"));
        Assert.Empty(vm.PartPalette.PinnedEntries);
    }

    /// <summary><see cref="PartPaletteViewModel"/> を直に作る経路でも同じであること。
    /// 上の2件は <c>MainWindowViewModel</c> 越しに測っており、受け渡しの途中で取り違えても
    /// 気づけぬ形ではないかを、直の経路と突き合わせて確かめる。</summary>
    [Fact]
    public void PartPaletteViewModelへ直に渡した場合も同じ結果になる()
    {
        var pinnedStore = TempPinnedStore();
        pinnedStore.Save(new[] { "injected-id" });

        var palette = new PartPaletteViewModel(new PartFolderStore(_dir), pinnedStore);

        Assert.True(palette.IsPinned("injected-id"));
    }
}
