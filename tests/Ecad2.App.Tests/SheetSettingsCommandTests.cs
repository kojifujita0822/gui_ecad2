using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-055増分2: シート設定ダイアログ経由のUpdateSheetSettingsCommand。Grid.Rows(1〜60)と
/// Bus.LeftName/RightNameをまとめて更新する。境界値クランプはAddRowCommand/DeleteRowCommand
/// (T-055増分1)と同じGridSpec.MinRows/MaxRowsを再利用する。Bus名の空文字は殿裁定により許容。
/// </summary>
public class SheetSettingsCommandTests : ViewModelTestBase
{
    [Fact]
    public void Execute_UpdatesGridRows()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(15, "N24", "P24"));

        Assert.Equal(15, vm.CurrentSheet!.Grid.Rows);
    }

    [Fact]
    public void Execute_UpdatesBusNames()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(10, "L1", "R1"));

        Assert.Equal("L1", vm.CurrentSheet!.Bus.LeftName);
        Assert.Equal("R1", vm.CurrentSheet!.Bus.RightName);
    }

    /// <summary>殿裁定(GuiEcad踏襲): Bus名の空文字は許容する。</summary>
    [Fact]
    public void Execute_AllowsEmptyBusNames()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(10, "", ""));

        Assert.Equal("", vm.CurrentSheet!.Bus.LeftName);
        Assert.Equal("", vm.CurrentSheet!.Bus.RightName);
    }

    [Fact]
    public void Execute_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(10, "N24", "P24"));

        Assert.True(vm.IsDirty);
    }

    /// <summary>境界値(下限1・上限60)は正しく適用されること。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public void Execute_AtBoundary_AppliesRows(int boundaryRows)
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(boundaryRows, "N24", "P24"));

        Assert.Equal(boundaryRows, vm.CurrentSheet!.Grid.Rows);
    }

    /// <summary>
    /// 境界値(下限-1=0・上限+1=61)は拒否されGrid.Rowsが変更されないこと(ダイアログ側のUI制約を
    /// すり抜けた場合の安全弁)。
    /// RED証明手法: UpdateSheetSettingsCommandのExecute内範囲チェック(`settings.Rows < GridSpec.MinRows
    /// || settings.Rows > GridSpec.MaxRows`のreturn)を一時的にコメントアウトしてテスト実行→
    /// Rowsが不正値(0/61)に変わってしまいRED(実測確認済み)。戻すとGREEN。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Execute_OutOfRange_RejectsAndDoesNotChangeRows(int invalidRows)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        int before = vm.CurrentSheet!.Grid.Rows;

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(invalidRows, "N24", "P24"));

        Assert.Equal(before, vm.CurrentSheet!.Grid.Rows);
    }

    /// <summary>範囲外拒否時はBus名も含め一切変更しない(全体としてアトミックに拒否する)。</summary>
    [Fact]
    public void Execute_OutOfRange_DoesNotChangeBusNames()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Bus.LeftName = "元L";
        vm.CurrentSheet!.Bus.RightName = "元R";

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(61, "新L", "新R"));

        Assert.Equal("元L", vm.CurrentSheet!.Bus.LeftName);
        Assert.Equal("元R", vm.CurrentSheet!.Bus.RightName);
    }

    /// <summary>
    /// T-055増分2往復1周目(隠密レビュー指摘、殿裁定): DeleteRowCommandは最終行のみ判定するが、
    /// UpdateSheetSettingsCommandはダイアログ経由で一気に大きく縮小できるため、縮小される全行
    /// (新Rows〜旧Rows-1)のいずれかに要素があれば拒否すること。先頭行・中間行・末尾行いずれの
    /// 位置でも検出できることを境界値として確認する。
    /// RED証明手法: UpdateSheetSettingsCommandのExecute内範囲チェックループを一時的に
    /// コメントアウトしてテスト実行→全InlineDataでRowsが縮小されてしまいRED(実測確認済み)。
    /// 戻すとGREEN。
    /// </summary>
    [Theory]
    [InlineData(5)]  // 縮小範囲の先頭行(新Rows)
    [InlineData(7)]  // 縮小範囲の中間行
    [InlineData(9)]  // 縮小範囲の末尾行(旧Rows-1)
    public void Execute_WhenShrinkRangeHasElement_RejectsAndDoesNotChangeRows(int elementRow)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(elementRow, 1) });

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(5, "N24", "P24"));

        Assert.Equal(10, vm.CurrentSheet!.Grid.Rows);
    }

    /// <summary>
    /// T-055増分2往復2周目(隠密レビュー指摘、CONFIRMED): DeleteRowCommand由来の「最終行に」固定
    /// 文言をそのまま流用すると、縮小範囲内の先頭・中間行で拒否された場合にユーザーが実際の元凶
    /// ではなく旧最終行付近を確認しに行き誤誘導する。拒否理由の行番号(1始まり表示)が先頭・中間・
    /// 末尾いずれの位置でも実際の占有行と一致することを確認する。
    /// RED証明手法: StatusMessageの行番号埋め込みを一時的に固定文言("最終行に要素があるため
    /// 削除できません")へ戻してテスト実行→中間行(elementRow=7)で不一致となりRED(実測確認済み)。
    /// 戻すとGREEN。
    /// </summary>
    [Theory]
    [InlineData(5)]  // 縮小範囲の先頭行(新Rows)
    [InlineData(7)]  // 縮小範囲の中間行
    [InlineData(9)]  // 縮小範囲の末尾行(旧Rows-1)
    public void Execute_WhenShrinkRangeHasElement_SetsWarningMessageWithActualRow(int elementRow)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(elementRow, 1) });

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(5, "N24", "P24"));

        Assert.Equal($"行{elementRow + 1}に要素があるため削除できません", vm.StatusMessage);
    }

    /// <summary>対照ケース: 縮小後も範囲内に残る行(縮小対象外)の要素は拒否理由にならない。</summary>
    [Fact]
    public void Execute_WhenElementOutsideShrinkRange_AllowsShrink()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(2, 1) });

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(5, "N24", "P24"));

        Assert.Equal(5, vm.CurrentSheet!.Grid.Rows);
    }

    /// <summary>
    /// T-055増分1(SelectedCellクランプ)と同じ理由: Rows縮小でSelectedCellが範囲外になった場合、
    /// 選択解除ではなく新しい末尾行へクランプする(列は維持)。
    /// </summary>
    [Fact]
    public void Execute_WhenSelectedCellExceedsNewRows_ClampsSelectedCell()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.SelectedCell = new GridPos(9, 3);

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(5, "N24", "P24"));

        Assert.Equal(new GridPos(4, 3), vm.SelectedCell);
    }

    [Fact]
    public void CanExecute_ReturnsFalse_WhenNoCurrentSheet()
    {
        var vm = CreateViewModel();

        Assert.False(vm.UpdateSheetSettingsCommand.CanExecute(
            new MainWindowViewModel.SheetSettings(10, "N24", "P24")));
    }

    /// <summary>T-055増分1往復2周目の教訓(Add/DeleteRowCommand成功パスのStatusMessage残留)を
    /// 新規コマンドへ最初から適用する。</summary>
    [Fact]
    public void Execute_OnSuccess_ClearsStatusMessage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.StatusMessage = "最終行に要素があるため削除できません";

        vm.UpdateSheetSettingsCommand.Execute(new MainWindowViewModel.SheetSettings(10, "N24", "P24"));

        Assert.Equal("", vm.StatusMessage);
    }
}
