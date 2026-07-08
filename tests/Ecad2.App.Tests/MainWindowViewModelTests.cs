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

    /// <summary>T-045増分B(P-020種別マッピング未実装の解消、殿裁可済み案A)の回帰テスト。
    /// 接点・コイル系(Role=ContactNO/Coil)はRelayへ分類される。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_WithContactPart_SetsDeviceClassRelay()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        Assert.Equal(DeviceClass.Relay, vm.Document.Devices.ByName["X001"].Class);
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

    /// <summary>セレクトSWはRole=ContactNO(電気的にはa接点と同一、T-037往復2周目の既知制約)だが、
    /// 機器表分類上はSelectSwitchへ区別される(固定Id判定、殿裁可済み案A)。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_WithSelectSwitchPart_SetsDeviceClassSelectSwitch()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(BasicPartTemplates.SelectSwitchId, "SW1", isOr: false);

        Assert.Equal(DeviceClass.SelectSwitch, vm.Document.Devices.ByName["SW1"].Class);
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
