using System.IO;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-148（殿ご裁可2026-08-08）: 自作部品を削除したとき、ピン留めの JSON からもその Id を落とす。
/// 原本 GuiEcad は掃除せず孤児 Id が残る実装だが、ecad2 は掃除する。
/// <para>
/// 掲出（<see cref="PartPaletteViewModel.PinnedEntries"/>）は <c>Entries</c> の側を回すため、
/// 孤児 Id が画面に出ることは元より無い。直す理由は JSON が際限なく太ることのみで、実害は極小。
/// </para>
/// <para>
/// <b>2引数コンストラクタを使う</b>——1引数版は実 MyDocuments を掴む（P-019 の再来を防ぐ、
/// <see cref="PartPaletteViewModel"/> の申し合わせ）。
/// </para>
/// </summary>
public class T148PinnedOrphanCleanupTests : IDisposable
{
    private readonly string _dir;
    private readonly PartFolderStore _folderStore;
    private readonly PinnedPartStore _pinnedStore;

    public T148PinnedOrphanCleanupTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ecad2-t148-tests", Guid.NewGuid().ToString("N"));
        _folderStore = new PartFolderStore(_dir);
        _pinnedStore = new PinnedPartStore(Path.Combine(_dir, "pinned-parts.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private PartPaletteViewModel CreateViewModel() => new(_folderStore, _pinnedStore);

    /// <summary>自作部品を1件作り、その保存先パスを返す。</summary>
    private string SaveCustom(string id, string name) =>
        _folderStore.SaveCustom(new PartDefinition { Id = id, Name = name });

    [Fact]
    public void ピン留めした部品を削除するとJSONからもIdが落ちる()
    {
        string path = SaveCustom("custom-a", "あ");
        var vm = CreateViewModel();
        vm.TogglePin("custom-a");
        Assert.Contains("custom-a", _pinnedStore.Load());   // 前提の確認（掃除の前に在ること）

        vm.DeletePart(path);

        Assert.DoesNotContain("custom-a", _pinnedStore.Load());
        Assert.False(vm.IsPinned("custom-a"));
    }

    /// <summary>他の部品のピン留めまで巻き込んで消さぬこと。掃除の実装が集合ごと空にする形でも
    /// 上の1件は通ってしまうため、残る側を併せて押さえる。</summary>
    [Fact]
    public void 削除は他の部品のピン留めを巻き込まない()
    {
        string pathA = SaveCustom("custom-a", "あ");
        SaveCustom("custom-b", "い");
        var vm = CreateViewModel();
        vm.TogglePin("custom-a");
        vm.TogglePin("custom-b");

        vm.DeletePart(pathA);

        var pinned = _pinnedStore.Load();
        Assert.DoesNotContain("custom-a", pinned);
        Assert.Contains("custom-b", pinned);
        Assert.True(vm.IsPinned("custom-b"));
    }

    /// <summary>素朴なベースライン（memory: feedback_control_experiment_needs_naive_baseline）。
    /// ピン留めしておらぬ部品を削除しても、JSON は書き換わらず他のピン留めも残る。これが無いと、
    /// 上の2件が「削除された Id を落とした」のか「削除のたびに集合を作り直した」のかを弁別できぬ。</summary>
    [Fact]
    public void ピン留めしておらぬ部品を削除してもJSONは変わらない()
    {
        SaveCustom("custom-a", "あ");
        string pathB = SaveCustom("custom-b", "い");
        var vm = CreateViewModel();
        vm.TogglePin("custom-a");

        vm.DeletePart(pathB);

        Assert.Contains("custom-a", _pinnedStore.Load());
        Assert.True(vm.IsPinned("custom-a"));
    }

    /// <summary>ピン留めが一件も無い状態で削除しても例外を投げぬ（JSON が存在せぬ経路）。</summary>
    [Fact]
    public void ピン留めが無い状態での削除も例外を投げぬ()
    {
        string path = SaveCustom("custom-a", "あ");
        var vm = CreateViewModel();

        vm.DeletePart(path);

        Assert.Empty(_pinnedStore.Load());
    }
}
