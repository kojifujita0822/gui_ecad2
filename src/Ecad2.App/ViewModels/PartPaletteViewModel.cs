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

    /// <summary>ピン留めの永続化（T-133増分9、殿裁定9）。<see cref="PinnedPartStore"/> は T-007 の移植で
    /// Core層へ入りながら参照0件のまま据え置かれており、本増分が初めての呼び手である。</summary>
    private readonly PinnedPartStore _pinnedStore;

    /// <summary>ピン留めされた部品Idの集合。<see cref="Load"/> のたびにディスクから読み直す。
    /// <para>
    /// <b>順序は持たぬ</b>——原本 GuiEcad も <c>HashSet</c> で保持し、メニューへ出す順は掲出側が
    /// <c>_folderEntries</c>（Category昇順→名前昇順）を回して決めておる。<b>ピン留めした順ではない。</b>
    /// </para></summary>
    private HashSet<string> _pinnedIds = new();

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
    /// コンストラクタ(P-019=App層テストが実MyDocumentsを叩く副作用の解消)。
    /// <para>
    /// <b>【T-133増分9・ピン留めの保存先は実MyDocuments のままである】</b>本オーバーロードは
    /// <see cref="PinnedPartStore.CreateDefault"/> を用いる。<b><see cref="TogglePin"/> を呼ぶテストは
    /// 必ず下の2引数版を使い、一時フォルダの <see cref="PinnedPartStore"/> を注入すること</b>
    /// ——さもなくば P-019 と同じ副作用（テストが実MyDocumentsへ書く）が、ピン留めの側から蘇る。
    /// <b>読むだけなら副作用は無い</b>（<see cref="PinnedPartStore.Load"/> はファイルが無ければ空を返す）。
    /// </para>
    /// <para>
    /// <b>【この定めはコンパイラに強制されておらぬ】</b>docコメントによる申し合わせにすぎぬ。
    /// <c>MainWindowViewModel</c> が本オーバーロードで <c>PartPalette</c> を構築しておるゆえ、
    /// <b><c>MainWindowViewModelTests</c> 側へ <c>.PartPalette.TogglePin(...)</c> が書き足されれば、
    /// 誰も気づかぬまま実MyDocumentsへ書く経路が開く</b>（隠密の検分、2026-08-06。
    /// 現時点で <see cref="TogglePin"/> を呼ぶ9箇所はすべて2引数版を使うており申し合わせは守られておる）。
    /// <b>型で塞ぐ道は在る</b>——本オーバーロードを廃して2引数版のみにすれば、コンパイラが強制する。
    /// <b>波及は実測で src 1箇所＋テスト5ファイル程度</b>にて手が届く規模だが、
    /// <b>増分9の主題（未配線のものを繋ぐ）とは別軸ゆえ <c>proposed.md</c> へ切り離した。</b>
    /// </para></summary>
    public PartPaletteViewModel(PartFolderStore store) : this(store, PinnedPartStore.CreateDefault()) { }

    /// <summary>T-133増分9: ピン留めの保存先も注入できる形。テストはこちらを使う。</summary>
    public PartPaletteViewModel(PartFolderStore store, PinnedPartStore pinnedStore)
    {
        _store = store;
        _pinnedStore = pinnedStore;
        store.EnsureFolders();
        store.SeedBasics();
        Load();
    }

    /// <summary>フォルダを走査し、Entries/Library/SelectionEntriesを構築する(初回構築・T-068増分1の
    /// Refresh双方から呼ばれる共通ロジック)。</summary>
    private void Load()
    {
        // T-133増分9: ピン留めもここで読み直す。Load は保存・削除でも呼ばれるゆえ、
        // 別プロセスがファイルを書き換えても次の Load で追従する（原本は起動時1回のみ）。
        _pinnedIds = _pinnedStore.Load();

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
        // T-136(A)増分2: 一覧を作り直した直後にも配置可否を当て直す。Load は保存・削除でも
        // 呼ばれるゆえ、ここを抜かすとシート切替まで新しいエントリが常に「置ける」ままになる。
        ApplyPlaceability();
    }

    // T-136(A)増分2: 直近に通知されたシート種別。Load がシート種別を引数に取らぬため保持する。
    private bool _lastSheetIsMainCircuit;

    /// <summary>現在のシート種別に応じて各エントリの配置可否を更新する(T-136(A)増分2)。
    /// <see cref="RefreshThumbnails"/> と同型——シート切替のたびに MainWindowViewModel から呼ぶ
    /// (<c>NotifyCurrentSheetDependentPropertiesChanged</c>)。</summary>
    public void RefreshPlaceability(bool sheetIsMainCircuit)
    {
        _lastSheetIsMainCircuit = sheetIsMainCircuit;
        ApplyPlaceability();
    }

    private void ApplyPlaceability()
    {
        foreach (var entry in SelectionEntries)
            entry.IsPlaceable = PartResolver.IsAllowedOnSheet(entry.Definition.SheetAffinity, _lastSheetIsMainCircuit);
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

    /// <summary>その部品がピン留めされているか（T-133増分9、殿裁定9）。</summary>
    public bool IsPinned(string partId) => _pinnedIds.Contains(partId);

    /// <summary>
    /// ピン留めを登録／解除する（原本 GuiEcad <c>OnTogglePinPart</c> 踏襲、<c>MainPage.Parts.cs:196-208</c>）。
    /// <para>
    /// <b>【本増分の射程はここまで】</b>状態を書き換えてディスクへ保存するのみで、<b>通知も再構築も行わぬ</b>
    /// ——原本は直後に <c>RebuildShapeMenu(); RebuildOtherPartMenu();</c> を呼んで掲出先を即座に更新するが、
    /// <b>ecad2 のメニューは <c>SubmenuOpened</c> で開くたびに動的構築される</b>ゆえ、次に開いた時点で
    /// 最新の状態が反映される（ラベルの「ピン留め」⇔「解除」も同様）。<b>原本の即時再構築は要らぬ。</b>
    /// </para>
    /// <para>
    /// <b>掲出（ピン留め済みをメニューへ出す形）は殿の裁可待ちにて、本増分に含めておらぬ</b>
    /// ——掲出先が定まれば、その更新をここへ足すか、掲出側が都度読むかを決める（家老裁定2026-08-06）。
    /// </para>
    /// <para>
    /// <b>保存の失敗は握りつぶされる</b>——<see cref="PinnedPartStore.Save"/> が <c>catch { }</c> ゆえ
    /// （原本も同じ）。<b>使い手には「ピン留めした」と見えて実際は保存されておらぬ場合がありうる。</b>
    /// 手当ては <c>proposed.md</c> へ起票済み（家老、2026-08-06）——本増分では触れず、ここに書き残す。
    /// </para></summary>
    public void TogglePin(string partId)
    {
        if (_pinnedIds.Contains(partId)) _pinnedIds.Remove(partId);
        else _pinnedIds.Add(partId);
        _pinnedStore.Save(_pinnedIds);
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
