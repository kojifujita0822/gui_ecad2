using System.IO;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Simulation;

namespace Ecad2.App.Tests;

/// <summary>
/// T-151 案Y'（開封時バックフィル）のテスト。
/// 隠密テスト設計 docs/ecad2-t151-test-design-onmitsu.md 9-6節、殿ご裁可2026-08-15。
/// <para>
/// <b>【案Y'が何を防ぐか】</b>T-151以前に作られた図面は <c>Library</c> を持たぬ。解決の基準を
/// 図面側へ移しただけでは、そこへ部品を一つ置いた瞬間に基準が切り替わり、<b>元から在った要素が
/// 揃って解決不能になる</b>——本タスクが直そうとしている症状を、修正自体が新たに生む形になる。
/// ゆえに文書を開いた瞬間、まだ埋め込まれておらぬ定義をローカルから一括で写す。
/// </para>
/// <para>
/// 発火時機を「配置の瞬間」ではなく「開いた瞬間」に前倒ししたのは殿のご裁可による——
/// 配置時では、無関係な1個の配置が既存要素のローカル現在値まで問答無用で凍結する不意打ちがあるため。
/// </para>
/// </summary>
public class T151OpenTimeBackfillTests : ViewModelTestBase
{
    private const string PartAId = "t151-backfill-a";
    private const string PartBId = "t151-backfill-b";
    private const string MissingPartId = "t151-backfill-missing";

    private static PartDefinition PartA() => new()
    {
        Id = PartAId,
        Name = "バックフィルA",
        WidthCells = 5,
        HeightCells = 1,
        Role = PartRole.TimerContactNO,
        Ports = { new PortDef("L", 0, 0), new PortDef("R", 0, 5) },
        Primitives = { new PartLine(0, 0, 5, 0) },
    };

    private static PartDefinition PartB() => new()
    {
        Id = PartBId,
        Name = "バックフィルB",
        WidthCells = 3,
        HeightCells = 1,
        Role = PartRole.Coil,
        Ports = { new PortDef("L", 0, 0), new PortDef("R", 0, 3) },
        Primitives = { new PartCircle(1.5, 0, 0.4) },
    };

    /// <summary>T-151以前の図面と同形＝<c>Library</c> を持たず、要素の <c>PartId</c> だけが並ぶ文書。</summary>
    private static LadderDocument MakeLegacyDocument(params string[] partIds)
    {
        var doc = new LadderDocument();
        var sheet = new Sheet { PageNumber = 1, Name = "シート1", Grid = new GridSpec { Rows = 10, Columns = 20 } };
        doc.Sheets.Add(sheet);
        for (int i = 0; i < partIds.Length; i++)
        {
            sheet.Elements.Add(new ElementInstance
            {
                PartId = partIds[i],
                Pos = new GridPos(i * 2, 0),
                DeviceName = $"CR{i + 1}",
            });
        }
        return doc;   // Library は null のまま（＝レガシー）
    }

    /// <summary>ローカルへ定義を置いたViewModelで、指定の文書を保存し開く。</summary>
    private void WithLocalPartsAndDocument(PartDefinition[] localParts, LadderDocument doc,
                                           Action<MainWindowViewModel, string> verify)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"ecad2-t151-bf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var vm = CreateViewModel();
            foreach (var part in localParts) vm.PartPalette.SaveNewPart(part);

            string path = Path.Combine(dir, "legacy.gcad");
            GcadSerializer.Save(doc, path);
            vm.LoadFromFile(path);

            verify(vm, path);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>9-6節1行目＝開いた直後（追加操作なし）に、ローカルで解決できる定義が全て埋め込まれる。</summary>
    [Fact]
    public void LoadFromFile_LegacyDocWithResolvableParts_BackfillsAllIntoDocumentLibrary()
    {
        var doc = MakeLegacyDocument(PartAId, PartBId);

        WithLocalPartsAndDocument([PartA(), PartB()], doc, (vm, _) =>
        {
            Assert.NotNull(vm.Document.Library);
            Assert.Equal(2, vm.Document.Library!.ById.Count);
            Assert.Equal("バックフィルA", vm.Document.Library.Get(PartAId)!.Name);
            Assert.Equal(5, vm.Document.Library.Get(PartAId)!.WidthCells);
            Assert.Equal("バックフィルB", vm.Document.Library.Get(PartBId)!.Name);
            Assert.Equal(PartRole.Coil, vm.Document.Library.Get(PartBId)!.Role);
        });
    }

    /// <summary>9-6節2行目＝ローカルにも存在しないPartIdは埋め込まれず、従来どおり
    /// <c>DRC-PART-001</c> が鳴る。定義が失われている以上どこからも復元できぬゆえ、
    /// 案Y'でも解消しないことを明示的に固定する（設計書9-5(a)の除外）。</summary>
    [Fact]
    public void LoadFromFile_LegacyDocWithUnresolvableParts_LeavesThatIdUnembedded_StillReportsUnresolved()
    {
        var doc = MakeLegacyDocument(PartAId, MissingPartId);

        WithLocalPartsAndDocument([PartA()], doc, (vm, _) =>
        {
            // 解決できたAだけが埋め込まれる。
            Assert.NotNull(vm.Document.Library);
            Assert.True(vm.Document.Library!.ById.ContainsKey(PartAId));
            Assert.False(vm.Document.Library.ById.ContainsKey(MissingPartId));

            // 失われた定義は従来どおり警告として挙がる。
            var diagnostics = DesignRuleCheck.CheckUnresolvedPartId(vm.Document, vm.PartLibrary);
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(DesignRuleCheck.UnresolvedPartId, diagnostic.Code);
            Assert.Equal("CR2", diagnostic.DeviceName);
        });
    }

    /// <summary>9-6節3行目＝バックフィルが起きたら <c>IsDirty=true</c>（隠密の推奨、殿ご裁可）。
    /// <para>
    /// <b>【なぜ汚れさせるか】</b>移行が保存されて初めて移植性が回復する。
    /// <c>IsDirty=false</c> のままだと、閲覧だけで閉じられた場合に移行がファイルへ残らず、
    /// 次に他所で開いたとき同じ症状が再発する。
    /// </para>
    /// <para>
    /// <b>【実装上の罠】</b><c>ReplaceDocument</c> は末尾で <c>IsDirty=false</c> を明示するため、
    /// バックフィルをその前に置くと黙って打ち消される。本テストはその順序を固定する網でもある。
    /// </para></summary>
    [Fact]
    public void LoadFromFile_LegacyDocWithResolvableParts_SetsIsDirtyTrue()
    {
        var doc = MakeLegacyDocument(PartAId);

        WithLocalPartsAndDocument([PartA()], doc, (vm, _) => Assert.True(vm.IsDirty));
    }

    /// <summary>9-6節4行目＝バックフィル後にローカルを書き換えても、図面側は開いた時点の値のまま。
    /// 3節の対称性チェック（参照共有か複製かの弁別）を案Y'固有の経路へ適用したもの。</summary>
    [Fact]
    public void LoadFromFile_ThenMutateLocalDefinition_BackfilledValueStaysAtOpenTime()
    {
        var doc = MakeLegacyDocument(PartAId);

        WithLocalPartsAndDocument([PartA()], doc, (vm, _) =>
        {
            // ローカル側の定義インスタンスを直接書き換える（ディスク往復を挟まぬゆえ、
            // 参照を共有していれば図面側も一緒に変わる＝一撃で弁別できる）。
            var local = vm.PartPalette.Library.Get(PartAId);
            Assert.NotNull(local);
            local!.WidthCells = 99;
            local.Name = "書き換え後の名";

            Assert.Equal(5, vm.Document.Library!.Get(PartAId)!.WidthCells);
            Assert.Equal("バックフィルA", vm.Document.Library.Get(PartAId)!.Name);
        });
    }

    /// <summary>9-6節5行目＝新規文書（要素0件）はバックフィルの対象が無く <c>Library</c> は null のまま。
    /// 遅延初期化を不必要に起こさぬことの境界確認。</summary>
    [Fact]
    public void NewDocument_NoElements_LibraryStaysNull()
    {
        var vm = CreateViewModel();
        vm.PartPalette.SaveNewPart(PartA());

        vm.NewDocument();

        Assert.Null(vm.Document.Library);
        // 新規作成直後は汚れていないこと（バックフィルが空振りしてIsDirtyを立てぬ）。
        Assert.False(vm.IsDirty);
    }

    /// <summary>9-6節6行目（任意）＝一部だけ埋め込み済みの文書を開いたとき、
    /// バックフィルは足りぬ分だけを加え、<b>既に埋め込まれている定義は書き換えぬ</b>。
    /// <para>
    /// これは移植性の芯にあたる——図面に入っている定義こそが真実源ゆえ、手元のローカル値で
    /// 上書きすれば「他所から受け取った図面を開いただけで手元の同名パーツへすり替わる」ことになる。
    /// </para></summary>
    [Fact]
    public void LoadFromFile_AlreadyEmbeddedLegacyMix_MergesWithoutOverwritingExistingEmbeddedValues()
    {
        var doc = MakeLegacyDocument(PartAId, PartBId);
        // Aだけが既に埋め込まれており、しかもローカルとは異なる値を持つ（＝他所で作られた図面）。
        var embeddedA = PartA();
        embeddedA.WidthCells = 2;
        embeddedA.Name = "他所で作られたA";
        doc.Library = new PartLibrary();
        doc.Library.ById[PartAId] = embeddedA;

        WithLocalPartsAndDocument([PartA(), PartB()], doc, (vm, _) =>
        {
            Assert.Equal(2, vm.Document.Library!.ById.Count);
            // 既に在ったAは手元のローカル値（幅5・「バックフィルA」）で上書きされていない。
            Assert.Equal(2, vm.Document.Library.Get(PartAId)!.WidthCells);
            Assert.Equal("他所で作られたA", vm.Document.Library.Get(PartAId)!.Name);
            // 欠けていたBはローカルから補われている。
            Assert.Equal("バックフィルB", vm.Document.Library.Get(PartBId)!.Name);
        });
    }
}
