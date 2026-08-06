using System.IO;
using Ecad2.App.ViewModels;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分9（殿裁定9）: ピン留めの登録／解除（<see cref="PartPaletteViewModel.TogglePin"/>）。
/// <para>
/// <b>【本増分の射程＝状態と永続化まで】</b>掲出（ピン留め済みをメニューへ出す形）は掲出先が
/// 殿の裁可待ちゆえ含めておらぬ（家老裁定2026-08-06）。<b>ここで測るのは「集合が切り替わり、
/// ディスクへ残るところまで」</b>である。
/// </para>
/// <para>
/// <b>【一時フォルダの <see cref="PinnedPartStore"/> を注入する】</b>2引数コンストラクタを使う
/// ——1引数版は <see cref="PinnedPartStore.CreateDefault"/>（実MyDocuments）を掴むゆえ、
/// <see cref="PartPaletteViewModel.TogglePin"/> を呼ぶテストがそちらを使えば P-019 と同じ副作用が蘇る。
/// </para>
/// </summary>
public class PinnedPartToggleTests : IDisposable
{
    private readonly string _dir;
    private readonly PartFolderStore _folderStore;
    private readonly PinnedPartStore _pinnedStore;

    public PinnedPartToggleTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ecad2-pin-toggle-tests", Guid.NewGuid().ToString("N"));
        _folderStore = new PartFolderStore(_dir);
        _pinnedStore = new PinnedPartStore(Path.Combine(_dir, "pinned-parts.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private PartPaletteViewModel CreateViewModel() => new(_folderStore, _pinnedStore);

    [Fact]
    public void 初期状態ではどれもピン留めされておらぬ()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsPinned(BasicPartTemplates.MotorId));
    }

    [Fact]
    public void 一度押せばピン留めされる()
    {
        var vm = CreateViewModel();

        vm.TogglePin(BasicPartTemplates.MotorId);

        Assert.True(vm.IsPinned(BasicPartTemplates.MotorId));
    }

    /// <summary>同じ項目を二度押せば解除される（原本と同じトグル）。
    /// <b>「登録」と「解除」が別の導線ではないことを固定する</b>——原本は同一メニュー項目の
    /// ラベルを切り替えるのみで、解除専用の入口を持たぬ（侍の原本調査、2026-08-06）。</summary>
    [Fact]
    public void 二度押せば解除される()
    {
        var vm = CreateViewModel();

        vm.TogglePin(BasicPartTemplates.MotorId);
        vm.TogglePin(BasicPartTemplates.MotorId);

        Assert.False(vm.IsPinned(BasicPartTemplates.MotorId));
    }

    /// <summary>ピン留めは他の部品へ及ばぬ。<b>集合の取り違え（全件を一括で立てる等）を捕らえる</b>
    /// ——1件だけを見るテストでは、全件が立っても通ってしまう。</summary>
    [Fact]
    public void ピン留めは押した部品にのみ効く()
    {
        var vm = CreateViewModel();

        vm.TogglePin(BasicPartTemplates.MotorId);

        Assert.True(vm.IsPinned(BasicPartTemplates.MotorId));
        Assert.False(vm.IsPinned(BasicPartTemplates.ContactNOId));
        Assert.False(vm.IsPinned(BasicPartTemplates.LampId));
    }

    /// <summary><b>本増分の要</b>——ピン留めがディスクへ残り、次に立ち上げた ViewModel が読み直すこと。
    /// <c>PinnedPartStore</c> が参照0件のまま据え置かれていた器であり、<b>ここが繋がって初めて
    /// 「アプリを閉じても残る」が成り立つ</b>。</summary>
    [Fact]
    public void ピン留めは作り直したViewModelでも残る()
    {
        CreateViewModel().TogglePin(BasicPartTemplates.MotorId);

        Assert.True(CreateViewModel().IsPinned(BasicPartTemplates.MotorId));
    }

    /// <summary>解除もディスクへ残ること。<b>登録だけを測ると、解除が保存されておらぬ穴を素通しする</b>
    /// ——集合から消しても <c>Save</c> を呼び忘れれば、次の起動でピン留めが甦る。</summary>
    [Fact]
    public void 解除も作り直したViewModelへ残る()
    {
        CreateViewModel().TogglePin(BasicPartTemplates.MotorId);
        CreateViewModel().TogglePin(BasicPartTemplates.MotorId);

        Assert.False(CreateViewModel().IsPinned(BasicPartTemplates.MotorId));
    }

    /// <summary>存在せぬ部品Idでも例外を投げぬ（<see cref="PartPaletteViewModel.TogglePin"/> は
    /// 一覧との突き合わせを行わぬ）。<b>原本も同じく孤児Idを許す形</b>で、掲出側が
    /// 一覧と突き合わせるため表示には出ぬ——<b>掃除しておらぬこと自体は <c>proposed.md</c> の論点</b>。</summary>
    [Fact]
    public void 存在せぬIdを渡しても例外を投げぬ()
    {
        var vm = CreateViewModel();

        vm.TogglePin("no-such-part-id");

        Assert.True(vm.IsPinned("no-such-part-id"));
    }
}
