using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-041増分1隠密レビュー指摘(観点2 CONFIRMED4件)の回帰テスト。SelectedCellとSelectedConnectorは
/// 排他のはずが、個別の呼び出し元がSelectedConnectorのクリアを書き忘れると崩れ、幽霊ハイライト・
/// 意図しない削除・偽の未保存フラグを引き起こしうる(docs/ecad2-t041-increment1-review-onmitsu.md)。
/// 修正後はSelectedCellのsetter自身が常時SelectedConnectorをクリアする単一の真実源となったため、
/// 4経路それぞれを個別に再現して検証する。
/// </summary>
public class SelectedConnectorExclusivityTests : ViewModelTestBase
{
    private static VerticalConnector MakeConnector() => new() { Column = 1, TopRow = 0, BottomRow = 1 };

    [Fact]
    public void SettingSelectedCell_ClearsSelectedConnector_LikeArrowKeyMove()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedConnector = MakeConnector();

        // MoveSelectedCell(矢印キー移動)はSelectedCellへ新しい値を代入するのみ。
        vm.SelectedCell = new GridPos(2, 3);

        Assert.Null(vm.SelectedConnector);
    }

    [Fact]
    public void ClearingSelectedCellToNull_ClearsSelectedConnector_LikeActivateSelectDefault()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.SelectedConnector = MakeConnector();

        // ActivateSelectDefault(選択ツールボタン)はSelectedCell=nullを代入するのみ。
        vm.SelectedCell = null;

        Assert.Null(vm.SelectedConnector);
    }

    [Fact]
    public void SelectedCellAssignment_ClearsSelectedConnector_EvenWhenCurrentSheetIndexUnchanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedConnector = MakeConnector();

        // OutputPanelViewModel.JumpToのDRC同一シートジャンプを模す: CurrentSheetIndexを同値へ
        // 代入(SetPropertyの早期returnでシート内部のクリア処理はスキップされる)した後、
        // JumpTo自身がSelectedCellへ新しいセルを明示代入する。
        vm.CurrentSheetIndex = vm.CurrentSheetIndex;
        vm.SelectedCell = new GridPos(1, 1);

        Assert.Null(vm.SelectedConnector);
    }

    [Fact]
    public void ReplaceDocument_ClearsSelectedConnector_OnNewDocument()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedConnector = MakeConnector();

        // ReplaceDocument(NewDocument/LoadFromFile共通経路)は_selectedCellを直接代入するため、
        // SelectedCellのsetter経由の自動クリアが効かない。ReplaceDocument自身の明示クリアを検証する。
        vm.NewDocument();

        Assert.Null(vm.SelectedConnector);
    }

    [Fact]
    public void DeleteSelectedConnector_ReturnsFalse_WhenConnectorAlreadyRemoved()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var connector = MakeConnector();
        vm.CurrentSheet!.Connectors.Add(connector);
        vm.SelectedConnector = connector;
        vm.CurrentSheet!.Connectors.Remove(connector);   // シート側から既に無くなっている状況を再現

        bool result = vm.DeleteSelectedConnector();

        Assert.False(result);
    }
}
