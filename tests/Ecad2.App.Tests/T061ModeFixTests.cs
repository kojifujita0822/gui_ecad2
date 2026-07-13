using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-061修正(隠密静的レビュー全指摘CONFIRMED、修正方針設計書ecad2-t061-fix-design-onmitsu.md §5)。
/// A群(テストモード中の回路データ改変)・C群(型不整合)の回帰テスト。実際の配置経路
/// (PlaceElementAtSelectedCell、ElementInstance.Kindを設定しない=C群バグの実条件)を使うことで、
/// バグ再現条件を正確に踏まえる。
/// </summary>
public class T061ModeFixTests : ViewModelTestBase
{
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
    }

    // ===== 5-1 状態遷移 =====

    [Fact]
    public void Mode_SetTest_ResetsToolToSelectDefault()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");
        vm.Tool = new ToolState(ToolMode.PlaceElement, PartId: BasicPartTemplates.ContactNOId);

        vm.Mode = AppMode.Test;

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
    }

    [Fact]
    public void Tool_SetWhileInTestMode_RejectsNonSelectMode()
    {
        // A-2の二重安全網: CanEditDiagram(ツールバーIsEnabled)が万一漏れても、Toolのsetter自体が
        // Test中のSelectDefault以外への変更を拒否する。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Mode = AppMode.Test;

        vm.Tool = new ToolState(ToolMode.PlaceElement, PartId: BasicPartTemplates.ContactNOId);

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
    }

    [Fact]
    public void Mode_SetTest_DisablesUndoRedoCanExecute()
    {
        // A-3確認事項2(案A確定): テストモード中はUndo/Redo自体を無効化する。
        // Undo/Redo基盤のMVP対象範囲(T-051)はシート追加/削除のみのため、履歴生成にはシート追加を使う。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        Assert.True(vm.UndoCommand.CanExecute(null));   // 前提: Undo履歴が存在する

        vm.Mode = AppMode.Test;

        Assert.False(vm.UndoCommand.CanExecute(null));
        Assert.False(vm.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void Mode_SetDrawingAfterTest_RestoresUndoRedoCanExecute()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));
        Assert.True(vm.UndoCommand.CanExecute(null));   // 前提: Undo履歴が存在する
        vm.Mode = AppMode.Test;

        vm.Mode = AppMode.Drawing;

        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void CurrentTestSession_SheetSwitch_LazyCreatesEvaluatedSessionForNewSheet()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SheetNavigation.AddCommand.Execute(("シート2", false));   // 2枚目のシートを追加
        vm.Mode = AppMode.Test;

        vm.CurrentSheetIndex = 1;
        var session = vm.CurrentTestSession;

        Assert.NotNull(session);
        Assert.NotNull(session!.Result);   // Evaluate()済み(第一歩の既存挙動、シートまたぎでも保証)
    }

    [Fact]
    public void CanEditDiagram_NoProjectOrTestMode_IsFalse()
    {
        var vm = CreateViewModel();
        Assert.False(vm.CanEditDiagram);   // HasProject=falseの間

        vm.NewDocument();
        Assert.True(vm.CanEditDiagram);

        vm.Mode = AppMode.Test;
        Assert.False(vm.CanEditDiagram);
    }

    [Fact]
    public void Mode_SetTest_ClearsConnectorDraft()
    {
        // D-1(静的レビュー指摘): Modeセッタのコメントは「ドラッグ/パン状態の破棄」を謳いながら
        // 実装が記入中ドラフトをクリアしていなかった(コメントと実装の乖離)。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(3, 5);
        vm.BeginConnectorDraft();
        Assert.NotNull(vm.ConnectorDraftPreview);

        vm.Mode = AppMode.Test;

        Assert.Null(vm.ConnectorDraftPreview);
        Assert.False(vm.HasAnyDraft);
    }

    [Fact]
    public void AddRowCommand_TestMode_CanExecuteIsFalse()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        Assert.True(vm.AddRowCommand.CanExecute(null));

        vm.Mode = AppMode.Test;

        Assert.False(vm.AddRowCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteRowCommand_TestMode_CanExecuteIsFalse()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        Assert.True(vm.DeleteRowCommand.CanExecute(null));

        vm.Mode = AppMode.Test;

        Assert.False(vm.DeleteRowCommand.CanExecute(null));
    }

    // ===== 5-2 境界値・同値分割(DeviceClass判定、C群) =====

    [Fact]
    public void IsRealContactElement_Coil_IsFalse()
    {
        // C-2/C-3共通判定。コイルは接点ではない。
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");

        Assert.False(vm.IsRealContactElement(vm.CurrentSheet!.Elements[0]));
    }

    [Theory]
    [InlineData(BasicPartTemplates.ContactNOId)]
    [InlineData(BasicPartTemplates.ContactNCId)]
    public void IsRealContactElement_ContactNOOrNC_IsTrue(string partId)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, partId, "X001");

        Assert.True(vm.IsRealContactElement(vm.CurrentSheet!.Elements[0]));
    }

    // ===== 5-4 Theory活用(DeviceClass×操作のマトリクス、TestModePress) =====

    [Fact]
    public void TestModePress_PushButton_MomentarilyOnAndReturnsDeviceName()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.PushButtonNOId, "PB1");
        vm.Mode = AppMode.Test;

        string? result = vm.TestModePress(new GridPos(0, 0));

        Assert.Equal("PB1", result);
        Assert.True(vm.CurrentTestSession!.State.Inputs["PB1"]);
    }

    [Fact]
    public void TestModePress_ContactNO_TogglesInputAndReturnsNull()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.ContactNOId, "X001");
        vm.Mode = AppMode.Test;

        string? result = vm.TestModePress(new GridPos(0, 0));

        Assert.Null(result);
        Assert.True(vm.CurrentTestSession!.State.Inputs["X001"]);
    }

    [Fact]
    public void TestModePress_Coil_NoReaction()
    {
        // C-3再発防止(核心): 旧実装はDeviceClass.Relay(Coil含む)がdefaultへ丸められ、コイルにも
        // 無差別にToggleInputが適用され強制導通してしまっていた。
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");
        vm.Mode = AppMode.Test;

        string? result = vm.TestModePress(new GridPos(0, 0));

        Assert.Null(result);
        Assert.False(vm.CurrentTestSession!.State.Inputs.ContainsKey("Y001"));
    }

    [Fact]
    public void TestModePress_Lamp_NoReaction()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.LampId, "L001");
        vm.Mode = AppMode.Test;

        string? result = vm.TestModePress(new GridPos(0, 0));

        Assert.Null(result);
        Assert.False(vm.CurrentTestSession!.State.Inputs.ContainsKey("L001"));
    }

    [Fact]
    public void TestModePress_Terminal_NoReaction()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.TerminalId, "T001");
        vm.Mode = AppMode.Test;

        string? result = vm.TestModePress(new GridPos(0, 0));

        Assert.Null(result);
        Assert.False(vm.CurrentTestSession!.State.Inputs.ContainsKey("T001"));
    }

    [Fact]
    public void TestModePress_SelectSwitch_CyclesNotchPosition()
    {
        // C-1再発防止(核心): 旧実装はe.Kind==ElementKind.SelectSwitchで直接判定していたが、
        // PlaceElementAtSelectedCellはElementInstance.Kindを設定しない(常時既定値ContactNOのまま)
        // ため常にfalseとなりノッチ順送りが死んでいた(常時ToggleInputへフォールバック)。
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.SelectSwitchId, "SW1");
        vm.CurrentSheet!.Elements[0].Params[ParamKeys.Position] = "0";
        PlaceAt(vm, 1, 0, BasicPartTemplates.SelectSwitchId, "SW1");
        vm.CurrentSheet!.Elements[1].Params[ParamKeys.Position] = "1";
        vm.Mode = AppMode.Test;

        vm.TestModePress(new GridPos(0, 0));

        Assert.Equal(1, vm.CurrentTestSession!.State.Positions["SW1"]);
        // 単純トグル(旧実装のフォールバック挙動)ならInputsに書き込まれるはずだが、正しいノッチ
        // 順送りではInputsは触らずPositionsのみ更新される。
        Assert.False(vm.CurrentTestSession!.State.Inputs.ContainsKey("SW1"));
    }

    [Fact]
    public void TestModePress_HitNothing_ReturnsNull()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Mode = AppMode.Test;

        string? result = vm.TestModePress(new GridPos(5, 5));

        Assert.Null(result);
    }
}
