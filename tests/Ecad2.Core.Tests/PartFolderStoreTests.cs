using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-035: PartFolderStore.Enumerate()のPartDefinition.Id重複検出・再採番の回帰テスト。
/// ファイルコピーでIdが重複したまま残ると、PartPaletteViewModelでの辞書登録時に後勝ち上書きされる
/// 問題への対処(殿裁定「読込時に重複検出+再採番」)。「先勝ち」の判定はファイル作成日時
/// (CreationTimeUtc)最古優先(隠密レビュー指摘: パス辞書順のみだとWindowsの標準コピー命名
/// 「元 - コピー.拡張子」で半角スペース(U+0020)がピリオド(U+002E)よりコードポイントが小さいため
/// コピー側が先着してしまい、オリジナル側が誤って再採番される致命的な逆転が起きる)。
/// </summary>
public class PartFolderStoreTests
{
    private static PartDefinition MakePart(string id, string name) => new()
    {
        Id = id,
        Name = name,
    };

    private static string CreateTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"ecad2-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>2ファイルのCreationTimeUtcを明示的に設定し、olderPathが確実に先勝ちする状態を作る
    /// (ファイルシステムの実書き込みタイミング差に依存しないテストにするため)。</summary>
    private static void SetCreationOrder(string olderPath, string newerPath)
    {
        var older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetCreationTimeUtc(olderPath, older);
        File.SetCreationTimeUtc(newerPath, older.AddMinutes(10));
    }

    [Fact]
    public void Enumerate_DuplicateId_ReassignsNewerFileAndKeepsOlderByCreationTime()
    {
        string tempDir = CreateTempDir();
        try
        {
            const string duplicateId = "dup-id-001";
            // ファイル名はあえてパス辞書順とCreationTime順が逆になるようにする(実装が本当に
            // CreationTimeを見ているか、パス辞書順への取り違えでないかを検証するため)。
            string pathOlder = Path.Combine(tempDir, "z-part.gcadpart");
            string pathNewer = Path.Combine(tempDir, "a-part.gcadpart");
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "PartOlder"), pathOlder);
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "PartNewer"), pathNewer);
            SetCreationOrder(pathOlder, pathNewer);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.Single(result.Reassignments);
            Assert.Equal(2, result.Entries.Count);

            var entryOlder = result.Entries.Single(e => e.FilePath == pathOlder);
            var entryNewer = result.Entries.Single(e => e.FilePath == pathNewer);
            // CreationTimeが最古の"z-part"(パス辞書順では後)のIdが維持される。
            Assert.Equal(duplicateId, entryOlder.Definition.Id);
            Assert.NotEqual(duplicateId, entryNewer.Definition.Id);

            var reloadedNewer = PartLibrarySerializer.LoadOne(pathNewer);
            Assert.Equal(entryNewer.Definition.Id, reloadedNewer.Id);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_WindowsCopyNamingPattern_KeepsOriginalIdByCreationTime()
    {
        string tempDir = CreateTempDir();
        try
        {
            const string duplicateId = "dup-id-copy";
            string pathOriginal = Path.Combine(tempDir, "部品.gcadpart");
            string pathCopy = Path.Combine(tempDir, "部品 - コピー.gcadpart");
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "部品"), pathOriginal);
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "部品のコピー"), pathCopy);
            // Windowsのコピー操作は新しいCreationTimeを持つ挙動を模擬する。パス辞書順だけなら
            // 半角スペース(U+0020)<ピリオド(U+002E)によりコピー側が先着してしまう(隠密実機確認済み)。
            SetCreationOrder(pathOriginal, pathCopy);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            var entryOriginal = result.Entries.Single(e => e.FilePath == pathOriginal);
            var entryCopy = result.Entries.Single(e => e.FilePath == pathCopy);
            Assert.Equal(duplicateId, entryOriginal.Definition.Id);
            Assert.NotEqual(duplicateId, entryCopy.Definition.Id);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_NoDuplicateIds_NoReassignments()
    {
        string tempDir = CreateTempDir();
        try
        {
            PartLibrarySerializer.SaveOne(MakePart("id-1", "PartA"), Path.Combine(tempDir, "a-part.gcadpart"));
            PartLibrarySerializer.SaveOne(MakePart("id-2", "PartB"), Path.Combine(tempDir, "b-part.gcadpart"));

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.Empty(result.Reassignments);
            Assert.Equal(2, result.Entries.Count);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_NullOrEmptyId_ReassignsBothWithoutThrowing()
    {
        string tempDir = CreateTempDir();
        try
        {
            // PartDefinition.Idの既定値はGuidのため、意図的にIdなしの壊れた/旧形式ファイルを
            // JSON直書きで再現する(JsonOptions.Default: camelCase・大小文字非依存)。
            string path1 = Path.Combine(tempDir, "empty-id-1.gcadpart");
            string path2 = Path.Combine(tempDir, "empty-id-2.gcadpart");
            File.WriteAllText(path1, """{"id":"","name":"PartA"}""");
            File.WriteAllText(path2, """{"id":"","name":"PartB"}""");

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            // 隠密レビュー指摘: 従来は最初の1件がHashSet.Addで「非重複」として素通りし、
            // 無効なIdのまま放置されていた。両方とも再採番されることを確認する。
            Assert.Equal(2, result.Reassignments.Count);
            Assert.Equal(2, result.Entries.Count);
            Assert.All(result.Entries, e => Assert.False(string.IsNullOrEmpty(e.Definition.Id)));
            Assert.NotEqual(result.Entries[0].Definition.Id, result.Entries[1].Definition.Id);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_LegacyContactJsonWithoutIsOrEligible_BackfillsTrueAndSaves()
    {
        string tempDir = CreateTempDir();
        try
        {
            // T-037往復3周目: IsOrEligible導入(往復2周目)より前の旧版JSON(当該キー無し)を
            // 固定Id(a接点)で再現する。実機(殿PC OneDriveリダイレクト先)で検出された実例。
            string path = Path.Combine(tempDir, "a接点.gcadpart");
            File.WriteAllText(path, $$"""{"id":"{{BasicPartTemplates.ContactNOId}}","name":"a接点"}""");

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            var entry = result.Entries.Single();
            Assert.True(entry.Definition.IsOrEligible);

            var reloaded = PartLibrarySerializer.LoadOne(path);
            Assert.True(reloaded.IsOrEligible);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_LegacySelectSwitchJsonWithoutIsOrEligible_StaysFalse()
    {
        string tempDir = CreateTempDir();
        try
        {
            // セレクトSWはRole=ContactNOだがOR対象外(往復2周目の主題)。固定Id補正の対象を
            // a接点/b接点のみに限定していることの再混入防止を確認する。
            string path = Path.Combine(tempDir, "セレクトSW.gcadpart");
            File.WriteAllText(path, """{"id":"basic-select-switch","name":"セレクトSW"}""");

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.False(result.Entries.Single().Definition.IsOrEligible);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_LegacyContactJsonReadOnly_BackfillsInMemoryWithoutThrowing()
    {
        string tempDir = CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "b接点.gcadpart");
            File.WriteAllText(path, $$"""{"id":"{{BasicPartTemplates.ContactNCId}}","name":"b接点"}""");
            File.SetAttributes(path, FileAttributes.ReadOnly);

            var store = new PartFolderStore(tempDir);
            PartEnumerationResult result;
            try
            {
                result = store.Enumerate();
            }
            finally
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            // 書き戻し失敗(読み取り専用)でも例外は外へ伝播せず、メモリ上は補正済みで継続する
            // (OneDrive同期中のロック等を想定、家老指摘の安全側処理)。
            Assert.True(result.Entries.Single().Definition.IsOrEligible);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_LegacyContactCopy_ReassignsIdButKeepsIsOrEligibleTrueForBoth()
    {
        string tempDir = CreateTempDir();
        try
        {
            // コピー耐性の確認: 旧版JSON(isOrEligibleキー無し)の複製で同一Idが重複したケース。
            // Id重複チェックより前に固定Id補正を行うため、後発ファイル(コピー)が新Idへ再採番
            // されても、書き戻される内容には補正後のtrue(a接点由来)が引き継がれるはず。
            string pathOriginal = Path.Combine(tempDir, "a接点.gcadpart");
            string pathCopy = Path.Combine(tempDir, "a接点 - コピー.gcadpart");
            File.WriteAllText(pathOriginal, $$"""{"id":"{{BasicPartTemplates.ContactNOId}}","name":"a接点"}""");
            File.WriteAllText(pathCopy, $$"""{"id":"{{BasicPartTemplates.ContactNOId}}","name":"a接点のコピー"}""");
            SetCreationOrder(pathOriginal, pathCopy);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            var entryOriginal = result.Entries.Single(e => e.FilePath == pathOriginal);
            var entryCopy = result.Entries.Single(e => e.FilePath == pathCopy);
            Assert.Equal(BasicPartTemplates.ContactNOId, entryOriginal.Definition.Id);
            Assert.NotEqual(BasicPartTemplates.ContactNOId, entryCopy.Definition.Id);
            Assert.True(entryOriginal.Definition.IsOrEligible);
            Assert.True(entryCopy.Definition.IsOrEligible);

            var reloadedCopy = PartLibrarySerializer.LoadOne(pathCopy);
            Assert.True(reloadedCopy.IsOrEligible);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_ReadOnlyFile_ReassignsInMemoryWithoutThrowing()
    {
        string tempDir = CreateTempDir();
        try
        {
            const string duplicateId = "dup-id-readonly";
            string pathOlder = Path.Combine(tempDir, "a-part.gcadpart");
            string pathNewer = Path.Combine(tempDir, "b-part.gcadpart");
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "PartA"), pathOlder);
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "PartB"), pathNewer);
            SetCreationOrder(pathOlder, pathNewer);
            File.SetAttributes(pathNewer, FileAttributes.ReadOnly);

            var store = new PartFolderStore(tempDir);
            PartEnumerationResult result;
            try
            {
                result = store.Enumerate();
            }
            finally
            {
                File.SetAttributes(pathNewer, FileAttributes.Normal);
            }

            // 書き戻し(SaveOne)は読み取り専用のため失敗するが、1ファイル単位の例外隔離により
            // 例外は外へ伝播せず、列挙自体はメモリ上で再採番済みの状態のまま継続する
            // (家老指摘の注文1、T-039の教訓を踏まえた設計)。
            Assert.Single(result.Reassignments);
            Assert.False(result.Reassignments[0].Saved);
            var entryNewer = result.Entries.Single(e => e.FilePath == pathNewer);
            Assert.NotEqual(duplicateId, entryNewer.Definition.Id);

            // ディスク上のファイルは書き戻し失敗のため旧Idのまま残る。
            var reloadedNewer = PartLibrarySerializer.LoadOne(pathNewer);
            Assert.Equal(duplicateId, reloadedNewer.Id);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
