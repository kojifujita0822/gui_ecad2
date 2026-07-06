using Ecad2.App.Diagnostics;
using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Rendering.Wpf;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 部品選択（自作パーツ含む全図形）用の ViewModel。MainWindowViewModel の子プロパティとして
/// 持たせ、God Class化を避ける（design-brief 3節#1）。Ecad2.Core.Persistence.PartFolderStore
/// （T-007で移植済み）から図形一覧を読み込む。配置操作自体はMainWindow.xaml.csのTryPlaceElement
/// (T-026段階4新配置フロー: セル選択→種別選択→浮動ダイアログ経由で配置)が担う。
/// </summary>
public sealed class PartPaletteViewModel : ViewModelBase
{
    public IReadOnlyList<PartFolderEntry> Entries { get; }

    /// <summary>Entriesから構築したPartLibrary(T-015隠密レビュー指摘#2: 従来MainWindowViewModel.
    /// BuildPartLibraryが同一ロジックを重複実装していたため、構築元であるここへ一本化した)。
    /// DiagramRenderer.Render/要素配置時のPartResolver解決、SelectionEntriesのサムネイル生成の
    /// 両方で共有する。</summary>
    public PartLibrary Library { get; }

    /// <summary>右パネル「部品選択」リスト(PartSelectionList)表示専用(T-015、サムネイル付き)。
    /// Entriesと1:1対応。ElementPlacementDialog等の他の利用箇所への影響を避けるため、Entries自体
    /// の型は変えずここに並行して持たせる。起動時一括生成(パーツ数が少数のためKISS、T-002 PoCの
    /// 実績から見て軽量と推定。増えて問題化したら遅延生成へ切替を検討)。</summary>
    public IReadOnlyList<PartSelectionEntryViewModel> SelectionEntries { get; }

    public PartPaletteViewModel()
    {
        var store = PartFolderStore.CreateDefault();
        store.EnsureFolders();
        store.SeedBasics();
        var enumeration = store.Enumerate();
        Entries = enumeration.Entries;
        // T-035: ファイルコピー等によるPartDefinition.Id重複が検出・再採番された件数をトレースする。
        if (enumeration.ReassignedCount > 0)
            TraceLog.LogPartIdReassigned(enumeration.ReassignedCount);

        Library = new PartLibrary();
        foreach (var entry in Entries) Library.ById[entry.Definition.Id] = entry.Definition;
        SelectionEntries = Entries
            .Select(entry => new PartSelectionEntryViewModel(entry, PartThumbnailRenderer.Render(entry.Definition.Id, Library)))
            .ToList();
    }
}
