using Ecad2.Model;

namespace Ecad2.Persistence;

/// <summary>走査結果の1件。<paramref name="Category"/> は「図形/」からの相対カテゴリ（直下="" / 自作="自作"）。</summary>
public sealed record PartFolderEntry(string Category, string FilePath, PartDefinition Definition);

/// <summary>Enumerate()内でPartDefinition.Idの重複が検出・再採番された1件分の記録(T-035、
/// 呼び出し元がTraceLog等へ記録する際に使う)。Savedはファイルへの書き戻し(SaveOne)が成功したか。</summary>
public readonly record struct PartIdReassignment(string FilePath, string OldId, string NewId, bool Saved);

/// <summary>Enumerate()の結果。Reassignmentsは重複検出・再採番が発生した分だけ含まれる。</summary>
public readonly record struct PartEnumerationResult(IReadOnlyList<PartFolderEntry> Entries, IReadOnlyList<PartIdReassignment> Reassignments);

/// <summary>
/// 図形を実フォルダで一元管理するストア。
/// ルート「図形/」直下に基本図形、「図形/自作/」に自作図形を 1図形=1ファイル（.gcadpart）で置く。
/// .GCAD への埋め込み（ドキュメント自己完結）とは別に、フォルダを「マスター／呼び出し元」とする。
/// </summary>
public sealed class PartFolderStore
{
    public const string PartExtension = ".gcadpart";
    private const string CustomFolderName = "自作";

    /// <summary>図形ルート（基本図形を置く）。</summary>
    public string RootDir { get; }
    /// <summary>自作図形フォルダ（図形/自作/）。</summary>
    public string CustomDir { get; }

    public PartFolderStore(string rootDir)
    {
        RootDir = rootDir;
        CustomDir = Path.Combine(rootDir, CustomFolderName);
    }

    /// <summary>既定の保存先（マイドキュメント\Ecad2\図形）でストアを作る。</summary>
    public static PartFolderStore CreateDefault()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return new PartFolderStore(Path.Combine(docs, "Ecad2", "図形"));
    }

    /// <summary>ルートと自作フォルダを作成（既存なら何もしない）。</summary>
    public void EnsureFolders()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(CustomDir);
    }

    /// <summary>全 .gcadpart を相対カテゴリ付きで列挙する（UI 階層構築の元データ）。読込失敗ファイルは無視。
    /// ファイルコピー等でPartDefinition.Idが重複していた場合、先に見つかったファイルのIdを維持し、
    /// 後発のファイルのみ新しいIdへ再採番してファイルへ書き戻す(T-035、殿裁定「読込時に重複検出+
    /// 再採番」)。「先/後」の判定はファイル作成日時(CreationTimeUtc)の最古優先とする(同時刻タイは
    /// パス辞書順)。隠密レビュー指摘: パス辞書順のみだと、Windowsの標準コピー命名「元 - コピー.拡張子」
    /// で半角スペース(U+0020)がピリオド(U+002E)よりコードポイントが小さいため、コピー側が辞書順で
    /// 先に来てしまい、オリジナル側が誤って再採番される致命的な逆転が起きる。Windowsのコピー操作は
    /// 新しいCreationTimeを持つ(LastWriteTimeは元のまま引き継ぐ)ため、CreationTime最古優先なら
    /// この逆転を避けられる。</summary>
    public PartEnumerationResult Enumerate()
    {
        var result = new List<PartFolderEntry>();
        var reassignments = new List<PartIdReassignment>();
        if (!Directory.Exists(RootDir)) return new PartEnumerationResult(result, reassignments);

        var files = Directory.EnumerateFiles(RootDir, "*" + PartExtension, SearchOption.AllDirectories)
            .OrderBy(f => File.GetCreationTimeUtc(f))
            .ThenBy(f => f, StringComparer.OrdinalIgnoreCase);

        var seenIds = new HashSet<string>();

        foreach (var file in files)
        {
            PartDefinition def;
            try { def = PartLibrarySerializer.LoadOne(file); }
            catch { continue; }   // 壊れたファイルはスキップ（起動を止めない）

            // 隠密レビュー指摘: Idがnull/空文字列(壊れた/旧形式ファイル)の場合、HashSet.Addは
            // 最初の1件を「非重複」として通してしまい無効なIdのまま放置される
            // (後続のDictionary登録でArgumentNullException等の恐れ)。無条件で再採番扱いにする。
            bool needsReassign = string.IsNullOrEmpty(def.Id) || !seenIds.Add(def.Id);
            if (needsReassign)
            {
                // ID重複(またはId欠落)検出。先に見つかった方のIdは維持し、このファイルのみ新Idへ
                // 再採番する。書き戻し(SaveOne)は1ファイル単位で例外隔離する(T-039の教訓: 読み取り
                // 専用フォルダ・ロック中ファイル等で起動時列挙が丸ごと死んではならない)。書き戻しに
                // 失敗した場合、今回のセッションはメモリ上のdef.Idのみ再採番された状態で継続し
                // (重複は解消される)、次回起動時にはファイルが未更新のためまた同じ重複が検出され、
                // その時また別Idへ再採番され得る。
                string oldId = def.Id;
                def.Id = Guid.NewGuid().ToString("N");
                seenIds.Add(def.Id);
                bool saved;
                try { PartLibrarySerializer.SaveOne(def, file); saved = true; }
                catch { saved = false; /* ベストエフォート。次回起動時の再解決に委ねる */ }
                reassignments.Add(new PartIdReassignment(file, oldId, def.Id, saved));
            }

            var dir = Path.GetDirectoryName(file) ?? RootDir;
            string category = Path.GetRelativePath(RootDir, dir);
            if (category == ".") category = "";
            result.Add(new PartFolderEntry(category.Replace('\\', '/'), file, def));
        }

        var sorted = result
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new PartEnumerationResult(sorted, reassignments);
    }

    /// <summary>自作図形を「図形/自作/&lt;名前&gt;.gcadpart」へ保存し、書き出したパスを返す。</summary>
    public string SaveCustom(PartDefinition part)
    {
        EnsureFolders();
        string path = Path.Combine(CustomDir, SafeFileName(part.Name) + PartExtension);
        PartLibrarySerializer.SaveOne(part, path);
        return path;
    }

    /// <summary>指定パスの図形ファイルを削除する（存在しなければ何もしない）。</summary>
    public void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>同梱の基本図形を「図形/」直下へ展開する（冪等：既存ファイルは上書きしない）。展開した数を返す。</summary>
    public int SeedBasics()
    {
        EnsureFolders();
        int seeded = 0;
        foreach (var def in BasicPartTemplates.All())
        {
            string path = Path.Combine(RootDir, SafeFileName(def.Name) + PartExtension);
            if (File.Exists(path)) continue;
            PartLibrarySerializer.SaveOne(def, path);
            seeded++;
        }
        return seeded;
    }

    /// <summary>図形名をファイル名に使える形へ正規化する（無効文字を '_' に置換・空名は "part"）。</summary>
    private static string SafeFileName(string name)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0) return "part";
        foreach (char c in Path.GetInvalidFileNameChars())
            trimmed = trimmed.Replace(c, '_');
        return trimmed;
    }
}
