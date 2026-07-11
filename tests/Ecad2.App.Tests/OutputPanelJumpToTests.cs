using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.App.Tests;

/// <summary>
/// T-052往復1周目・隠密指摘#1: DRC-PART-001診断はDeviceName未入力要素も除外せず警告するため、
/// JumpToの「DeviceName未一致→行内先頭要素」フォールバックだけでは同一行の別要素へ誤ジャンプする。
/// PartResolver.IsUnresolvedPartIdで実際の未解決要素を優先させた修正の回帰テスト。
/// </summary>
public class OutputPanelJumpToTests : ViewModelTestBase
{
    [Fact]
    public void JumpToDiagnostic_UnresolvedPartIdWithoutDeviceName_SelectsUnresolvedElement_NotNamedElementInSameRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;

        var named = new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "CR1", Pos = new GridPos(2, 1) };
        var unresolved = new ElementInstance { PartId = "missing-id", DeviceName = null, Pos = new GridPos(2, 5) };
        sheet.Elements.Add(named);
        sheet.Elements.Add(unresolved);

        var diagnostic = new Diagnostic(DiagnosticSeverity.Warning, DesignRuleCheck.UnresolvedPartId, "",
            "機器 (無名): 部品参照が見つからず、a接点として扱われています。部品の再選択をご確認ください。",
            [new CircuitRef(sheet.PageNumber, 3)]);

        vm.OutputPanel.JumpToDiagnostic(diagnostic);

        Assert.Equal(unresolved.Pos, vm.SelectedCell);
    }

    [Fact]
    public void JumpToDiagnostic_OtherCodeWithoutDeviceName_FallsBackToFirstElementInRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var sheet = vm.CurrentSheet!;

        var first = new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "CR1", Pos = new GridPos(2, 1) };
        sheet.Elements.Add(first);

        var diagnostic = new Diagnostic(DiagnosticSeverity.Warning, DesignRuleCheck.VerticalCrossing, "",
            "テスト用", [new CircuitRef(sheet.PageNumber, 3)]);

        vm.OutputPanel.JumpToDiagnostic(diagnostic);

        Assert.Equal(first.Pos, vm.SelectedCell);
    }
}
