using System.IO;
using Ecad2.App.ViewModels;
using Ecad2.Model;

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
}
