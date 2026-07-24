using System.IO;
using System.Linq;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-068増分1: PartPaletteViewModelの新規作成/編集/削除と、それに伴う一覧(Entries/Library/
/// SelectionEntries)再構築を検証する。
/// </summary>
public class PartPaletteViewModelCrudTests : ViewModelTestBase
{
    [Fact]
    public void SaveNewPart_AddsEntryToPaletteAndLibrary()
    {
        var vm = CreateViewModel();
        var palette = vm.PartPalette;
        int beforeCount = palette.Entries.Count;

        var newPart = new PartDefinition { Name = "テスト部品A", WidthCells = 1, HeightCells = 1, Role = PartRole.ContactNO };
        palette.SaveNewPart(newPart);

        Assert.Equal(beforeCount + 1, palette.Entries.Count);
        Assert.Contains(palette.Entries, e => e.Definition.Name == "テスト部品A" && e.Category == "自作");
        Assert.True(palette.Library.ById.ContainsKey(newPart.Id));
    }

    [Fact]
    public void SaveEditedPart_RenamesFile_RemovesOldFile()
    {
        var vm = CreateViewModel();
        var palette = vm.PartPalette;

        var original = new PartDefinition { Name = "旧名前", WidthCells = 1, HeightCells = 1, Role = PartRole.ContactNO };
        palette.SaveNewPart(original);
        var entry = palette.Entries.Single(e => e.Definition.Id == original.Id);
        string oldPath = entry.FilePath;
        Assert.True(File.Exists(oldPath));

        var renamed = new PartDefinition { Id = original.Id, Name = "新名前", WidthCells = 1, HeightCells = 1, Role = PartRole.ContactNO };
        palette.SaveEditedPart(renamed, oldPath);

        Assert.False(File.Exists(oldPath));
        var updatedEntry = palette.Entries.Single(e => e.Definition.Id == original.Id);
        Assert.Equal("新名前", updatedEntry.Definition.Name);
        Assert.True(File.Exists(updatedEntry.FilePath));
    }

    [Fact]
    public void SaveEditedPart_SameName_KeepsSingleFile()
    {
        var vm = CreateViewModel();
        var palette = vm.PartPalette;

        var original = new PartDefinition { Name = "同名部品", WidthCells = 1, HeightCells = 1, Role = PartRole.ContactNO };
        palette.SaveNewPart(original);
        var entry = palette.Entries.Single(e => e.Definition.Id == original.Id);
        string path = entry.FilePath;

        var edited = new PartDefinition { Id = original.Id, Name = "同名部品", WidthCells = 2, HeightCells = 1, Role = PartRole.Coil };
        palette.SaveEditedPart(edited, path);

        Assert.True(File.Exists(path));
        var updated = palette.Entries.Single(e => e.Definition.Id == original.Id);
        Assert.Equal(2, updated.Definition.WidthCells);
        Assert.Equal(PartRole.Coil, updated.Definition.Role);
    }

    [Fact]
    public void DeletePart_RemovesEntryAndFile()
    {
        var vm = CreateViewModel();
        var palette = vm.PartPalette;

        var part = new PartDefinition { Name = "削除対象", WidthCells = 1, HeightCells = 1, Role = PartRole.ContactNO };
        palette.SaveNewPart(part);
        var entry = palette.Entries.Single(e => e.Definition.Id == part.Id);
        string path = entry.FilePath;

        palette.DeletePart(path);

        Assert.DoesNotContain(palette.Entries, e => e.Definition.Id == part.Id);
        Assert.False(File.Exists(path));
    }

    /// <summary>T-068増分1核心: Refresh(内部Load再実行)後もLibraryインスタンス自体は差し替わらない
    /// ことを保証する(MainWindowViewModel.PartLibraryが同一インスタンス参照のまま最新化される
    /// 設計の根拠、実装中にUndoスタック同様の見落としを避けるため明示的に固定した回帰テスト)。</summary>
    [Fact]
    public void SaveNewPart_KeepsSameLibraryInstance()
    {
        var vm = CreateViewModel();
        var palette = vm.PartPalette;
        var libraryBefore = palette.Library;

        palette.SaveNewPart(new PartDefinition { Name = "参照確認用", WidthCells = 1, HeightCells = 1, Role = PartRole.ContactNO });

        Assert.Same(libraryBefore, palette.Library);
        Assert.Same(libraryBefore, vm.PartLibrary);
    }
}
