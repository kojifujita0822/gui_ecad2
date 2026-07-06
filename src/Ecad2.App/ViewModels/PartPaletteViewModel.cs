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
    /// ElementPlacementDialog等の他の利用箇所への影響を避けるため、Entries自体の型は変えずここに
    /// 並行して持たせる。起動時一括生成(パーツ数が少数のためKISS、T-002 PoCの実績から見て軽量と
    /// 推定。増えて問題化したら遅延生成へ切替を検討)。T-037(殿裁定=案A)によりORa/ORb論理エントリを
    /// 追加するため、Entriesとの1:1対応はここで崩れる(隠密調査所見、実害なし)。</summary>
    public IReadOnlyList<PartSelectionEntryViewModel> SelectionEntries { get; }

    /// <summary>本番用。実MyDocuments配下(PartFolderStore.CreateDefault())を使う。</summary>
    public PartPaletteViewModel() : this(PartFolderStore.CreateDefault()) { }

    /// <summary>T-042: テスト等から一時フォルダのPartFolderStoreを注入できるようにするための
    /// コンストラクタ(P-019=App層テストが実MyDocumentsを叩く副作用の解消)。</summary>
    public PartPaletteViewModel(PartFolderStore store)
    {
        store.EnsureFolders();
        store.SeedBasics();
        var enumeration = store.Enumerate();
        Entries = enumeration.Entries;
        // T-035: ファイルコピー等によるPartDefinition.Id重複検出・再採番の詳細(対象ファイル・
        // 旧Id・新Id・書き戻し成否)をトレースする(隠密レビュー指摘: 件数のみでは事後調査不能)。
        foreach (var r in enumeration.Reassignments)
            TraceLog.LogPartIdReassigned(r.FilePath, r.OldId, r.NewId, r.Saved);

        Library = new PartLibrary();
        foreach (var entry in Entries) Library.ById[entry.Definition.Id] = entry.Definition;

        var selectionEntries = Entries
            .Select(entry => new PartSelectionEntryViewModel(entry, PartThumbnailRenderer.Render(entry.Definition.Id, Library)))
            .ToList();

        // T-037(殿裁定=案A): ツールバーのOR a接点/OR b接点(Shift+F5/F6)と同じ選択肢を部品選択
        // リストにも追加する(隠密調査案1)。専用図形は持たず、既存a接点/b接点のPartFolderEntryを
        // IsOr=trueでラップした論理エントリを追加するのみ。対象判定はName文字列一致ではなく
        // PartDefinition.IsOrEligible(往復2周目: Role判定だとセレクトSWもContactNO扱いのため
        // 巻き込まれ「ORセレクトSW」が出現した。殿裁定=ORa/ORbのみに絞るため、電気的Role非依存の
        // 専用フラグへ置換。Id・Nameに依存しないためコピー・再採番・リネームでも判定が揺らがない)。
        foreach (var entry in Entries.Where(e => e.Category == "" && e.Definition.IsOrEligible))
            selectionEntries.Add(new PartSelectionEntryViewModel(entry, PartThumbnailRenderer.Render(entry.Definition.Id, Library, isOr: true), isOr: true));

        SelectionEntries = selectionEntries;
    }
}
