using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// 自作パーツの役割「その他」（殿ご下命2026-10-09）の機器表まわりのテスト。
/// 発端は殿ご指摘——テストで動かすために種別をコイルにすると、機器表にリレーと出てしまう。
/// </summary>
public class OtherRoleDeviceClassTests : ViewModelTestBase
{
    private const string PartId = "other-role-device-class";

    private static PartDefinition Part(PartRole role) => new()
    {
        Id = PartId,
        Name = "その他役割_検体",
        WidthCells = 1,
        HeightCells = 1,
        Role = role,
        Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
        Primitives = new() { new PartLine(0, 0, 1, 0) },
    };

    private MainWindowViewModel CreateViewModelWithPlacedPart(PartRole role, string deviceName)
    {
        var vm = CreateViewModel();
        vm.PartPalette.SaveNewPart(Part(role));
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(PartId, deviceName, isOr: false);
        return vm;
    }

    private static string PathOfPart(MainWindowViewModel vm) =>
        vm.PartPalette.Entries.Single(e => e.Definition.Id == PartId).FilePath;

    /// <summary>対照つき: コイルはリレー、その他はその他。</summary>
    [Theory]
    [InlineData(PartRole.Coil, DeviceClass.Relay)]
    [InlineData(PartRole.Other, DeviceClass.Other)]
    public void PlaceElement_RegistersDeviceClassByRole(PartRole role, DeviceClass expected)
    {
        var vm = CreateViewModelWithPlacedPart(role, "SOL1");

        Assert.Equal(expected, vm.Document.Devices.ByName["SOL1"].Class);
    }

    /// <summary>殿の手順そのもの。コイルで置いた後に「その他」へ直すと、機器表の種別も付いて変わる。</summary>
    [Fact]
    public void SaveEditedPart_CoilToOther_UpdatesDeviceClass_AndUndoRestoresIt()
    {
        var vm = CreateViewModelWithPlacedPart(PartRole.Coil, "SOL1");
        Assert.Equal(DeviceClass.Relay, vm.Document.Devices.ByName["SOL1"].Class);

        vm.SaveEditedPart(Part(PartRole.Other), PathOfPart(vm));

        Assert.Equal(DeviceClass.Other, vm.Document.Devices.ByName["SOL1"].Class);
        vm.UndoCommand.Execute(null);
        Assert.Equal(DeviceClass.Relay, vm.Document.Devices.ByName["SOL1"].Class);
    }

    /// <summary>逆向きも同じ機構で合う（非シミュレートで置いた後にコイルへ直す、T-162 の残りであった所）。</summary>
    [Fact]
    public void SaveEditedPart_NonSimulatedToCoil_UpdatesDeviceClass()
    {
        var vm = CreateViewModelWithPlacedPart(PartRole.NonSimulated, "SOL1");
        Assert.Equal(DeviceClass.Other, vm.Document.Devices.ByName["SOL1"].Class);

        vm.SaveEditedPart(Part(PartRole.Coil), PathOfPart(vm));

        Assert.Equal(DeviceClass.Relay, vm.Document.Devices.ByName["SOL1"].Class);
    }

    /// <summary>旧い役割から導かれた種別と違う値を持つ機器は、別の事情で決まった値ゆえ書き換えぬ。</summary>
    [Fact]
    public void SaveEditedPart_DeviceClassAlreadyDiffers_IsLeftUntouched()
    {
        var vm = CreateViewModelWithPlacedPart(PartRole.Coil, "SOL1");
        vm.Document.Devices.ByName["SOL1"].Class = DeviceClass.Lamp;

        vm.SaveEditedPart(Part(PartRole.Other), PathOfPart(vm));

        Assert.Equal(DeviceClass.Lamp, vm.Document.Devices.ByName["SOL1"].Class);
    }

    /// <summary>種別が変わらぬ役割の変更（a接点→b接点＝どちらもリレー）では機器表に触れぬ。</summary>
    [Fact]
    public void SaveEditedPart_RoleChangeWithinSameClass_KeepsDeviceClass()
    {
        var vm = CreateViewModelWithPlacedPart(PartRole.ContactNO, "CR1");

        vm.SaveEditedPart(Part(PartRole.ContactNC), PathOfPart(vm));

        Assert.Equal(DeviceClass.Relay, vm.Document.Devices.ByName["CR1"].Class);
    }
}
