using System.IO;
using System.Linq;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-034: MainWindowViewModelのDirty追跡(MarkDirty呼び忘れ検出)・HasProject切替の回帰テスト。
/// 明示MarkDirty方式は変更操作の入口ごとに呼び忘れる構造的リスクがあるため(docs/todo.md T-034備考)、
/// 各入口で最低限の検証を行う。
/// </summary>
public class MainWindowViewModelTests : ViewModelTestBase
{
    [Fact]
    public void Constructor_InitialState_HasProjectIsFalseAndNotDirty()
    {
        var vm = CreateViewModel();

        Assert.False(vm.HasProject);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void NewDocument_SetsHasProjectTrueAndNotDirty()
    {
        var vm = CreateViewModel();

        vm.NewDocument();

        Assert.True(vm.HasProject);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void PlaceElementAtSelectedCell_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void DeleteSelectedElement_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);
        ResetDirtyViaSave(vm);

        bool deleted = vm.DeleteSelectedElement();

        Assert.True(deleted);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void SelectedElementDeviceName_Set_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);
        ResetDirtyViaSave(vm);

        vm.SelectedElementDeviceName = "X002";

        Assert.True(vm.IsDirty);
    }

    /// <summary>IsDirtyのsetterはprivateのため、公開APIであるSaveToFileの副作用を借りてfalseへ戻す。</summary>
    private static void ResetDirtyViaSave(MainWindowViewModel vm)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ecad2-test-{Guid.NewGuid():N}.gcad");
        try
        {
            vm.SaveToFile(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SaveToFile_ClearsDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);
        string path = Path.Combine(Path.GetTempPath(), $"ecad2-test-{Guid.NewGuid():N}.gcad");

        try
        {
            vm.SaveToFile(path);

            Assert.False(vm.IsDirty);
            Assert.Equal(path, vm.CurrentFilePath);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_ReplacesDocumentAndClearsDirty()
    {
        var source = CreateViewModel();
        source.NewDocument();
        source.SelectedCell = new GridPos(0, 0);
        source.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);
        string path = Path.Combine(Path.GetTempPath(), $"ecad2-test-{Guid.NewGuid():N}.gcad");
        source.SaveToFile(path);

        try
        {
            var vm = CreateViewModel();
            vm.LoadFromFile(path);

            Assert.True(vm.HasProject);
            Assert.False(vm.IsDirty);
            Assert.Equal(path, vm.CurrentFilePath);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// T-045増分B(P-022/P-024境界ガード欠如の解消)の回帰テスト。NewDocument()の既定グリッドは
    /// Rows=10・Columns=20。行-1・列-2はSelectedCellとしては選択可能な仕様範囲(殿教示2026-07-07、
    /// docs/proposed.md P-022/P-024)だが、配置経路のみをガードする(殿裁定2026-07-09=下限0)。
    /// </summary>
    [Theory]
    [InlineData(-1, 5, false)]   // 行-1: 仕様上選択可能だが配置は範囲外
    [InlineData(0, 5, true)]     // 行0: 下限、配置可
    [InlineData(9, 5, true)]     // 行Rows-1=9: 上限直下、配置可
    [InlineData(10, 5, false)]   // 行Rows=10: 範囲外
    [InlineData(0, -2, false)]   // 列-2: 仕様上選択可能な下限だが配置は範囲外
    [InlineData(0, 0, true)]     // 列0: 下限、配置可
    [InlineData(0, 19, true)]    // 列Columns-1=19: 上限直下、配置可
    [InlineData(0, 20, false)]   // 列Columns=20: 範囲外
    public void PlaceElementAtSelectedCell_BoundaryRowAndColumn_PlacesOnlyWithinGridRange(int row, int column, bool expectPlaced)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(row, column);

        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        Assert.Equal(expectPlaced, vm.CurrentSheet!.Elements.Any(el => el.Pos == new GridPos(row, column)));
    }

    /// <summary>
    /// T-045増分C(所見B=TryPlaceElementの境界チェック未追随の解消)の回帰テスト。IsSelectedCellWithinGrid
    /// はValidatePlacementと境界判定ロジックを共有する(IsWithinGridBounds)。境界は
    /// PlaceElementAtSelectedCell_BoundaryRowAndColumnと同じ8ケース。
    /// </summary>
    [Theory]
    [InlineData(-1, 5, false)]
    [InlineData(0, 5, true)]
    [InlineData(9, 5, true)]
    [InlineData(10, 5, false)]
    [InlineData(0, -2, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 19, true)]
    [InlineData(0, 20, false)]
    public void IsSelectedCellWithinGrid_BoundaryRowAndColumn_ReturnsExpected(int row, int column, bool expected)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(row, column);

        Assert.Equal(expected, vm.IsSelectedCellWithinGrid());
    }

    /// <summary>SelectedCell未選択(null)の場合はfalseを返す(TryPlaceElementは占有チェック同様、
    /// null選択を別ガードで先に弾くため到達しない想定だが、単体としての境界値を確認する)。</summary>
    [Fact]
    public void IsSelectedCellWithinGrid_WhenNoCellSelected_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.IsSelectedCellWithinGrid());
    }

    /// <summary>T-045増分B(P-021占有再チェック欠如の解消)の回帰テスト。既に要素があるセルへの
    /// 再配置は無視され、先着の要素・機器名がそのまま残ることを検証する。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_WhenCellAlreadyOccupied_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.CoilId, "X002", isOr: false);

        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal("X001", vm.CurrentSheet!.Elements[0].DeviceName);
        Assert.False(vm.Document.Devices.ByName.ContainsKey("X002"));
    }

    /// <summary>端子台(Role=Terminal)はTerminalへ分類される。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_WithTerminalPart_SetsDeviceClassTerminal()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(BasicPartTemplates.TerminalId, "TB1", isOr: false);

        Assert.Equal(DeviceClass.Terminal, vm.Document.Devices.ByName["TB1"].Class);
    }

    /// <summary>
    /// T-045増分B修正(隠密レビューCONFIRMED、ecad2-t045-increment-b-review-onmitsu.md=セレクトSW
    /// 誤分類バグ)の回帰テスト。テスト設計書(ecad2-t045-increment-b-fix-test-design-onmitsu.md)の
    /// 同値分割4分類(A/B/C/D)。既存の`PlaceElementAtSelectedCell_With{Contact,SelectSwitch}Part_
    /// SetsDeviceClass{Relay,SelectSwitch}`をケースA/Cとして統合した(設計書6節、侍判断で統合可)。
    /// A/Bは「Idの違いのみ」(固定Id判定の脆さを直接突く対)、C/Dは「IsOrEligibleの違いのみ」
    /// (IsOrEligible単独判定の弁別力を試す対)、B/Dは「Role=ContactNO・IsOrEligible=falseで一致
    /// するが期待結果が異なる」最も厳しい対。
    /// </summary>
    public static IEnumerable<object[]> SelectSwitchClassificationCases()
    {
        // A: セレクトSW・元Id(既存テスト相当)。
        yield return new object[] { BasicPartTemplates.SelectSwitchId, null!, false, DeviceClass.SelectSwitch };

        // B: セレクトSW・再採番Id相当(T-035Explorerコピー後、基本図形フォルダ直下、RED対象)。
        // Role/IsOrEligible/PortsはSelectSwitchと同一、Idのみ異なる。
        yield return new object[]
        {
            "reassigned-select-switch-guid",
            new PartDefinition
            {
                Id = "reassigned-select-switch-guid", Name = "セレクトSW", Role = PartRole.ContactNO,
                IsOrEligible = false, Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
            },
            false, // 基本図形フォルダ直下(Category=="")
            DeviceClass.SelectSwitch,
        };

        // C: 純正ContactNO(a接点、既存テスト相当、退行防止)。
        yield return new object[] { BasicPartTemplates.ContactNOId, null!, false, DeviceClass.Relay };

        // D: 自作パーツ(自作フォルダ、Role=ContactNO・IsOrEligible=false、対応案の弁別力を試す。
        // Category="自作"のためCategory==""ゲートで弾かれ、IsOrEligible単独判定だと誤ってSelectSwitch
        // へ分類されるところをこのケースが検出する)。
        yield return new object[]
        {
            "custom-contact-no-guid",
            new PartDefinition
            {
                Id = "custom-contact-no-guid", Name = "自作接点", Role = PartRole.ContactNO,
                IsOrEligible = false, Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
            },
            true, // 自作フォルダ(Category=="自作")
            DeviceClass.Relay,
        };
    }

    /// <summary>customDefinitionが指定されたケース(B/D)は、PartFolderStoreの一時フォルダへ実際に
    /// .gcadpartファイルを書き出し、store.Enumerate()経由でPartPalette.Entriesへ反映させる
    /// (vm.PartLibrary.ById直接追加ではEntriesに載らずCategory判定を再現できないため、設計書
    /// ecad2-t045-increment-b-fix-test-design-onmitsu.md 5節注記に従い実装方式に追随)。</summary>
    [Theory]
    [MemberData(nameof(SelectSwitchClassificationCases))]
    public void PlaceElementAtSelectedCell_ClassifiesSelectSwitchByDataFieldsNotFixedId(
        string partId, PartDefinition? customDefinition, bool isCustomFolder, DeviceClass expected)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ecad2-apptest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = new PartFolderStore(tempDir);
            if (customDefinition is not null)
            {
                store.EnsureFolders();
                string dir = isCustomFolder ? store.CustomDir : store.RootDir;
                PartLibrarySerializer.SaveOne(customDefinition, Path.Combine(dir, $"{partId}.gcadpart"));
            }

            var vm = new MainWindowViewModel(store, new ImmediateDispatcherService());
            vm.NewDocument();
            vm.SelectedCell = new GridPos(0, 0);

            vm.PlaceElementAtSelectedCell(partId, "X001", isOr: false);

            Assert.Equal(expected, vm.Document.Devices.ByName["X001"].Class);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>自作パーツRole=NonSimulated(主回路記号等)はComponentKindが呼べない
    /// (CreatesComponent=false)ため、Otherへフォールバックする。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_WithNonSimulatedCustomPart_SetsDeviceClassOther()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.PartLibrary.ById["custom-nonsim"] = new PartDefinition
        {
            Id = "custom-nonsim",
            Name = "テスト非シミュレート",
            Role = PartRole.NonSimulated,
        };
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell("custom-nonsim", "M1", isOr: false);

        Assert.Equal(DeviceClass.Other, vm.Document.Devices.ByName["M1"].Class);
    }

    /// <summary>SelectedElementDeviceNameセッター経由でも同じマッピングが効くことを確認する
    /// (配置時にデバイス名を空欄のままにし、後から命名する経路)。</summary>
    [Fact]
    public void SelectedElementDeviceName_Set_WithNewDeviceName_UsesResolvedDeviceClass()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.TerminalId, "", isOr: false);

        vm.SelectedElementDeviceName = "TB2";

        Assert.Equal(DeviceClass.Terminal, vm.Document.Devices.ByName["TB2"].Class);
    }
}
