using System.IO;
using Ecad2.App.Diagnostics;
using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Rendering;
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
    private readonly PartFolderStore _store;

    private IReadOnlyList<PartFolderEntry> _entries = Array.Empty<PartFolderEntry>();
    public IReadOnlyList<PartFolderEntry> Entries { get => _entries; private set => SetProperty(ref _entries, value); }

    /// <summary>Entriesから構築したPartLibrary(T-015隠密レビュー指摘#2: 従来MainWindowViewModel.
    /// BuildPartLibraryが同一ロジックを重複実装していたため、構築元であるここへ一本化した)。
    /// DiagramRenderer.Render/要素配置時のPartResolver解決、SelectionEntriesのサムネイル生成の
    /// 両方で共有する。T-068増分1: Refresh()後もMainWindowViewModel.PartLibrary(同一インスタンス
    /// 参照)が追従できるよう、インスタンス自体は生成時のまま維持しById辞書の中身だけを差し替える。</summary>
    public PartLibrary Library { get; } = new();

    /// <summary>右パネル「部品選択」リスト(PartSelectionList)表示専用(T-015、サムネイル付き)。
    /// ElementPlacementDialog等の他の利用箇所への影響を避けるため、Entries自体の型は変えずここに
    /// 並行して持たせる。起動時一括生成(パーツ数が少数のためKISS、T-002 PoCの実績から見て軽量と
    /// 推定。増えて問題化したら遅延生成へ切替を検討)。T-037(殿裁定=案A)によりORa/ORb論理エントリを
    /// 追加するため、Entriesとの1:1対応はここで崩れる(隠密調査所見、実害なし)。</summary>
    private IReadOnlyList<PartSelectionEntryViewModel> _selectionEntries = Array.Empty<PartSelectionEntryViewModel>();
    public IReadOnlyList<PartSelectionEntryViewModel> SelectionEntries { get => _selectionEntries; private set => SetProperty(ref _selectionEntries, value); }

    /// <summary>本番用。実MyDocuments配下(PartFolderStore.CreateDefault())を使う。</summary>
    public PartPaletteViewModel() : this(PartFolderStore.CreateDefault()) { }

    /// <summary>T-042: テスト等から一時フォルダのPartFolderStoreを注入できるようにするための
    /// コンストラクタ(P-019=App層テストが実MyDocumentsを叩く副作用の解消)。</summary>
    public PartPaletteViewModel(PartFolderStore store)
    {
        _store = store;
        store.EnsureFolders();
        store.SeedBasics();
        Load();
    }

    /// <summary>フォルダを走査し、Entries/Library/SelectionEntriesを構築する(初回構築・T-068増分1の
    /// Refresh双方から呼ばれる共通ロジック)。</summary>
    private void Load()
    {
        var enumeration = _store.Enumerate();
        Entries = enumeration.Entries;
        // T-035: ファイルコピー等によるPartDefinition.Id重複検出・再採番の詳細(対象ファイル・
        // 旧Id・新Id・書き戻し成否)をトレースする(隠密レビュー指摘: 件数のみでは事後調査不能)。
        foreach (var r in enumeration.Reassignments)
            TraceLog.LogPartIdReassigned(r.FilePath, r.OldId, r.NewId, r.Saved);

        Library.ById.Clear();
        foreach (var entry in Entries) Library.ById[entry.Definition.Id] = entry.Definition;

        var selectionEntries = Entries
            .Select(entry => new PartSelectionEntryViewModel(entry, PartThumbnailRenderer.Render(entry.Definition, Library)))
            .ToList();

        // T-037(殿裁定=案A): ツールバーのOR a接点/OR b接点(Shift+F5/F6)と同じ選択肢を部品選択
        // リストにも追加する(隠密調査案1)。専用図形は持たず、既存a接点/b接点のPartFolderEntryを
        // IsOr=trueでラップした論理エントリを追加するのみ。対象判定はName文字列一致ではなく
        // PartDefinition.IsOrEligible(往復2周目: Role判定だとセレクトSWもContactNO扱いのため
        // 巻き込まれ「ORセレクトSW」が出現した。殿裁定=ORa/ORbのみに絞るため、電気的Role非依存の
        // 専用フラグへ置換。Id・Nameに依存しないためコピー・再採番・リネームでも判定が揺らがない)。
        foreach (var entry in Entries.Where(e => e.Category == "" && e.Definition.IsOrEligible))
            selectionEntries.Add(new PartSelectionEntryViewModel(entry, PartThumbnailRenderer.Render(entry.Definition, Library, isOr: true), isOr: true));

        SelectionEntries = selectionEntries;
    }

    /// <summary>T-068増分1: 新規自作パーツを保存し一覧を最新化する。</summary>
    public void SaveNewPart(PartDefinition part)
    {
        _store.SaveCustom(part);
        Load();
    }

    /// <summary>T-068増分1: 既存自作パーツの編集を保存する。名前変更等でファイル名が変わった場合は
    /// 旧ファイルを削除する(GuiEcad原本OpenFolderPartEditor踏襲、自作の上書き編集を保つ)。</summary>
    public void SaveEditedPart(PartDefinition part, string oldPath)
    {
        string newPath = _store.SaveCustom(part);
        if (!string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase))
            _store.Delete(oldPath);
        Load();
    }

    /// <summary>T-068増分1: 自作パーツを削除し一覧を最新化する。</summary>
    public void DeletePart(string filePath)
    {
        _store.Delete(filePath);
        Load();
    }

    /// <summary>PartId+IsOrから一致するSelectionEntryを解決する(T-033増分5のComboBox初期選択・
    /// T-054の配置バー内選択中部品表示、双方で共有する照合ロジック)。IsOr込みの完全一致を優先し、
    /// 無ければPartId一致のみへフォールバックする(OR系ツールバーボタンから開いた場合でも、
    /// リストにOR版エントリが無い部品では通常版を初期表示するため)。</summary>
    public PartSelectionEntryViewModel? ResolveEntry(string partId, bool isOr) =>
        SelectionEntries.FirstOrDefault(e => e.Definition.Id == partId && e.IsOr == isOr)
        ?? SelectionEntries.FirstOrDefault(e => e.Definition.Id == partId);

    /// <summary>T-083新規発見5(家老采配2026-07-17): 部品選択パネルのサムネイルはビットマップ事前
    /// レンダリング(RenderTargetBitmap)ゆえブラシ差替えでは対応できず、テーマ切替時に全件再生成
    /// する必要がある。MainWindow.xaml.csのIsDarkMode変更ハンドラから呼ばれる想定。</summary>
    public void RefreshThumbnails(Color foreground)
    {
        foreach (var entry in SelectionEntries)
            entry.Thumbnail = PartThumbnailRenderer.Render(entry.Definition, Library, isOr: entry.IsOr, foreground: foreground);
    }
}
