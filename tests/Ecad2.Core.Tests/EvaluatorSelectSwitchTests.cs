using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-061 A-1構造対処: セレクトSWの電気的導通判定(Evaluator.IsConducting)そのものを検証する回帰
/// テスト(docs/ecad2-t061-a1-select-switch-design-onmitsu.md 5節)。UI状態管理層
/// (tests/Ecad2.App.Tests/T061ModeFixTests.cs の TestModePress_SelectSwitch_CyclesNotchPosition)は
/// CurrentTestSession.State.Positionsのみ検証しており、電気的導通(EvalResult.ElementConducting)は
/// 別建てで確認する必要がある(旧実装ではPartRole.SelectSwitch不在によりElementKind.SelectSwitch
/// への解決経路自体が無く、ここで検証する導通判定はデッドコード化していた)。
/// </summary>
public class EvaluatorSelectSwitchTests
{
    private static PartLibrary MakeLibraryWithSelectSwitch()
    {
        var lib = new PartLibrary();
        foreach (var def in BasicPartTemplates.All())
            lib.ById[def.Id] = def;
        return lib;
    }

    private static ElementInstance MakeSelectSwitch(string deviceName, int switchPosition)
    {
        var element = new ElementInstance
        {
            PartId = BasicPartTemplates.SelectSwitchId,
            Pos = new GridPos(0, 0),
            DeviceName = deviceName,
        };
        element.Params[ParamKeys.Position] = switchPosition.ToString();
        return element;
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    public void Evaluate_SelectSwitchNotch_ConductsOnlyMatchingPosition(int notch, int switchPosition, bool expectConducting)
    {
        var lib = MakeLibraryWithSelectSwitch();
        var sheet = new Sheet();
        var element = MakeSelectSwitch("SW1", switchPosition);
        sheet.Elements.Add(element);

        var netlist = NetlistBuilder.Build(sheet, lib);
        var state = new SimState();
        state.Positions["SW1"] = notch;

        var result = new Evaluator(netlist).Evaluate(state);

        Assert.Equal(expectConducting, result.ElementConducting[element.Id]);
    }

    [Fact]
    public void Evaluate_SelectSwitchUnsetPosition_TreatedAsNotch0Contact()
    {
        // 境界値: Params[Position]キー無し(int.TryParse失敗)の要素は既定値0のまま=ノッチ0の接点
        // として扱われる(NetlistBuilder.BuildComponents)。
        var lib = MakeLibraryWithSelectSwitch();
        var sheet = new Sheet();
        var element = new ElementInstance
        {
            PartId = BasicPartTemplates.SelectSwitchId,
            Pos = new GridPos(0, 0),
            DeviceName = "SW1",
        };
        sheet.Elements.Add(element);

        var netlist = NetlistBuilder.Build(sheet, lib);
        var state = new SimState();
        state.Positions["SW1"] = 0;

        var result = new Evaluator(netlist).Evaluate(state);

        Assert.True(result.ElementConducting[element.Id]);
    }

    [Fact]
    public void ComponentKind_SelectSwitchPart_ResolvesToSelectSwitchElementKind()
    {
        // 5-1: 配置直後、PartResolver.ComponentKindがElementKind.SelectSwitchを返すこと
        // (旧実装ではRole=ContactNOのままElementKind.ContactNOに解決されていた)。
        var lib = MakeLibraryWithSelectSwitch();
        var element = new ElementInstance { PartId = BasicPartTemplates.SelectSwitchId };

        Assert.Equal(ElementKind.SelectSwitch, PartResolver.ComponentKind(element, lib));
    }

    [Fact]
    public void CreatesComponent_SelectSwitchPart_ReturnsTrue()
    {
        // 5-2: PartRole.SelectSwitchがCreatesComponentでtrueを返すこと(Role != NonSimulatedのみ
        // 見るため自動的にtrueになるはずだが明示テストで担保)。
        var lib = MakeLibraryWithSelectSwitch();
        var element = new ElementInstance { PartId = BasicPartTemplates.SelectSwitchId };

        Assert.True(PartResolver.CreatesComponent(element, lib));
    }

    [Fact]
    public void Evaluate_TwoNotchesSameDevice_OnlyMatchingNotchConducts()
    {
        // 5-3: 同一デバイス名で複数ノッチ(排他導通)を配置した場合、ノッチ位置に一致する接点のみ
        // 導通し、他ノッチは非導通のままであること(現実の切替スイッチの排他性)。
        var lib = MakeLibraryWithSelectSwitch();
        var sheet = new Sheet();
        var notch0 = MakeSelectSwitch("SW1", switchPosition: 0);
        notch0.Pos = new GridPos(0, 0);
        var notch1 = MakeSelectSwitch("SW1", switchPosition: 1);
        notch1.Pos = new GridPos(1, 0);
        sheet.Elements.Add(notch0);
        sheet.Elements.Add(notch1);

        var netlist = NetlistBuilder.Build(sheet, lib);
        var state = new SimState();
        state.Positions["SW1"] = 0;

        var result = new Evaluator(netlist).Evaluate(state);

        Assert.True(result.ElementConducting[notch0.Id]);
        Assert.False(result.ElementConducting[notch1.Id]);
    }

    [Fact]
    public void ComponentKind_ContactNOPart_StillResolvesToContactNO()
    {
        // 5-3ペア対称性: 通常接点(ContactNO)への非干渉を確認する(SelectSwitch専用Role追加が
        // 既存のContactNO判定に影響しないこと)。
        var lib = MakeLibraryWithSelectSwitch();
        var element = new ElementInstance { PartId = BasicPartTemplates.ContactNOId };

        Assert.Equal(ElementKind.ContactNO, PartResolver.ComponentKind(element, lib));
    }
}
