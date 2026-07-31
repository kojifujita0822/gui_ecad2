using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-054: PartPaletteViewModel.ResolveEntryの照合ロジック(配置バー初期選択・選択中部品表示の
/// 双方で共有)。IsOr込み優先・PartId一致のみへのフォールバック・未知IDでnullを検証する。
/// </summary>
public class PartPaletteViewModelTests : ViewModelTestBase
{
    [Fact]
    public void ResolveEntry_ExactMatchWithOr_ReturnsOrEntry()
    {
        var vm = CreateViewModel();

        var entry = vm.PartPalette.ResolveEntry(BasicPartTemplates.ContactNOId, isOr: true);

        Assert.NotNull(entry);
        Assert.True(entry!.IsOr);
        Assert.Equal(BasicPartTemplates.ContactNOId, entry.Definition.Id);
    }

    [Fact]
    public void ResolveEntry_ExactMatchWithoutOr_ReturnsNormalEntry()
    {
        var vm = CreateViewModel();

        var entry = vm.PartPalette.ResolveEntry(BasicPartTemplates.ContactNOId, isOr: false);

        Assert.NotNull(entry);
        Assert.False(entry!.IsOr);
        Assert.Equal(BasicPartTemplates.ContactNOId, entry.Definition.Id);
    }

    /// <summary>OR版エントリが存在しない部品(Coil等、IsOrEligibleでない)にisOr=trueで問い合わせても、
    /// PartId一致のみへフォールバックし通常版を返す。</summary>
    [Fact]
    public void ResolveEntry_IsOrRequestedButNoOrEntryExists_FallsBackToNormalEntry()
    {
        var vm = CreateViewModel();

        var entry = vm.PartPalette.ResolveEntry(BasicPartTemplates.CoilId, isOr: true);

        Assert.NotNull(entry);
        Assert.False(entry!.IsOr);
        Assert.Equal(BasicPartTemplates.CoilId, entry.Definition.Id);
    }

    [Fact]
    public void ResolveEntry_UnknownPartId_ReturnsNull()
    {
        var vm = CreateViewModel();

        var entry = vm.PartPalette.ResolveEntry("unknown-part-id", isOr: false);

        Assert.Null(entry);
    }

    // ===== 配置可否（T-136(A)増分2、殿裁定2026-07-31） =====
    // 部品パレットの項目は、現在のシートへ置けぬ部品を無効化する（予防）。
    // 【入力値の選び方】組込みテンプレートは全て既定の Any ゆえ、そのままでは
    // シート種別を変えても結果が動かず検出力が出ぬ。<b>枷を持つ部品を1件仕立てて測る。</b>

    [Theory]
    [InlineData(SheetAffinity.Any, false, true)]
    [InlineData(SheetAffinity.Any, true, true)]
    [InlineData(SheetAffinity.ControlOnly, false, true)]        // 制御シート＝MainCircuit が false
    [InlineData(SheetAffinity.ControlOnly, true, false)]
    [InlineData(SheetAffinity.MainCircuitOnly, false, false)]
    [InlineData(SheetAffinity.MainCircuitOnly, true, true)]
    public void RefreshPlaceability_枷に合うシートでのみ置ける(
        SheetAffinity affinity, bool sheetIsMainCircuit, bool expectPlaceable)
    {
        var vm = CreateViewModel();
        var entry = vm.PartPalette.SelectionEntries.First(e => e.Definition.Id == BasicPartTemplates.ContactNOId);
        entry.Definition.SheetAffinity = affinity;

        vm.PartPalette.RefreshPlaceability(sheetIsMainCircuit);

        Assert.Equal(expectPlaceable, entry.IsPlaceable);
    }

    [Fact]
    public void RefreshPlaceability_枷を持たぬ部品は巻き込まれぬ()
    {
        // 対照。1件へ枷を掛けた際、他の部品まで無効化されておらぬことを確かめる
        // （エントリ単位で判ずる仕組みが、実は一律に効いておらぬかを見る網）。
        var vm = CreateViewModel();
        var constrained = vm.PartPalette.SelectionEntries.First(e => e.Definition.Id == BasicPartTemplates.ContactNOId);
        var untouched = vm.PartPalette.SelectionEntries.First(e => e.Definition.Id == BasicPartTemplates.CoilId);
        constrained.Definition.SheetAffinity = SheetAffinity.MainCircuitOnly;

        vm.PartPalette.RefreshPlaceability(sheetIsMainCircuit: false);

        Assert.False(constrained.IsPlaceable);
        Assert.True(untouched.IsPlaceable);
        Assert.Equal(SheetAffinity.Any, untouched.Definition.SheetAffinity);   // 既定のままであることも押さえる
    }

    [Fact]
    public void 保存で一覧を作り直した後も配置可否が当たる()
    {
        // Load は保存・削除でも走る。その末尾で当て直しを抜かすと、<b>新しく加わったエントリだけが
        // 次のシート切替まで「置ける」ままになる</b>。
        // 【この網を足した経緯】壊す実測（Load 末尾の ApplyPlaceability を外す）で0件REDとなり、
        // 守れておらぬことが露見したため後から補った。実装した本人が要ると判じて入れた一行でも、
        // テストが無ければ次の者に消される。
        var vm = CreateViewModel();
        var palette = vm.PartPalette;
        palette.RefreshPlaceability(sheetIsMainCircuit: false);   // 現在＝制御回路シート

        var mainOnly = new PartDefinition
        {
            Name = "主回路専用の検体",
            WidthCells = 1,
            HeightCells = 1,
            Role = PartRole.NonSimulated,
            SheetAffinity = SheetAffinity.MainCircuitOnly,
        };
        palette.SaveNewPart(mainOnly);

        var entry = palette.SelectionEntries.Single(e => e.Definition.Id == mainOnly.Id);
        Assert.False(entry.IsPlaceable);
    }

    [Fact]
    public void シート切替でパレットの配置可否が追随する()
    {
        // 繋ぎ込みの検証。RefreshPlaceability を直に呼ぶのではなく、シートを切り替えて
        // NotifyCurrentSheetDependentPropertiesChanged 経由で届くことを測る。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Sheets.Add(new Sheet { Name = "主回路", MainCircuit = true, Grid = new GridSpec { Rows = 5, Columns = 10 } });
        var entry = vm.PartPalette.SelectionEntries.First(e => e.Definition.Id == BasicPartTemplates.ContactNOId);
        entry.Definition.SheetAffinity = SheetAffinity.MainCircuitOnly;

        // 主回路シートへ切替（初期値0からの変化ゆえ SetProperty の早期returnに掛からぬ）
        vm.CurrentSheetIndex = 1;
        Assert.True(entry.IsPlaceable);

        // 制御回路シートへ戻す
        vm.CurrentSheetIndex = 0;
        Assert.False(entry.IsPlaceable);
    }
}
