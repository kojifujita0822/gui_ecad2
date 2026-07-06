using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-035: PartFolderStore.Enumerate()のPartDefinition.Id重複検出・再採番の回帰テスト。
/// ファイルコピーでIdが重複したまま残ると、PartPaletteViewModelでの辞書登録時に後勝ち上書きされる
/// 問題への対処(殿裁定「読込時に重複検出+再採番」)。
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

    [Fact]
    public void Enumerate_DuplicateId_ReassignsLaterFileAndKeepsFirst()
    {
        string tempDir = CreateTempDir();
        try
        {
            const string duplicateId = "dup-id-001";
            string pathA = Path.Combine(tempDir, "a-part.gcadpart");
            string pathB = Path.Combine(tempDir, "b-part.gcadpart");
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "PartA"), pathA);
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "PartB"), pathB);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.Equal(1, result.ReassignedCount);
            Assert.Equal(2, result.Entries.Count);

            var entryA = result.Entries.Single(e => e.FilePath == pathA);
            var entryB = result.Entries.Single(e => e.FilePath == pathB);
            // ファイルパス順(アルファベット順)で先に見つかる"a-part"のIdは維持される。
            Assert.Equal(duplicateId, entryA.Definition.Id);
            Assert.NotEqual(duplicateId, entryB.Definition.Id);

            // ディスク上のファイルも新Idへ書き換わっていること。
            var reloadedB = PartLibrarySerializer.LoadOne(pathB);
            Assert.Equal(entryB.Definition.Id, reloadedB.Id);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_NoDuplicateIds_ReassignedCountIsZero()
    {
        string tempDir = CreateTempDir();
        try
        {
            PartLibrarySerializer.SaveOne(MakePart("id-1", "PartA"), Path.Combine(tempDir, "a-part.gcadpart"));
            PartLibrarySerializer.SaveOne(MakePart("id-2", "PartB"), Path.Combine(tempDir, "b-part.gcadpart"));

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.Equal(0, result.ReassignedCount);
            Assert.Equal(2, result.Entries.Count);
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
            const string duplicateId = "dup-id-002";
            string pathA = Path.Combine(tempDir, "a-part.gcadpart");
            string pathB = Path.Combine(tempDir, "b-part.gcadpart");
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "PartA"), pathA);
            PartLibrarySerializer.SaveOne(MakePart(duplicateId, "PartB"), pathB);
            File.SetAttributes(pathB, FileAttributes.ReadOnly);

            var store = new PartFolderStore(tempDir);
            PartEnumerationResult result;
            try
            {
                result = store.Enumerate();
            }
            finally
            {
                File.SetAttributes(pathB, FileAttributes.Normal);
            }

            // 書き戻し(SaveOne)は読み取り専用のため失敗するが、1ファイル単位の例外隔離により
            // 例外は外へ伝播せず、列挙自体はメモリ上で再採番済みの状態のまま継続する
            // (家老指摘の注文1、T-039の教訓を踏まえた設計)。
            Assert.Equal(1, result.ReassignedCount);
            var entryB = result.Entries.Single(e => e.FilePath == pathB);
            Assert.NotEqual(duplicateId, entryB.Definition.Id);

            // ディスク上のファイルは書き戻し失敗のため旧Idのまま残る。
            var reloadedB = PartLibrarySerializer.LoadOne(pathB);
            Assert.Equal(duplicateId, reloadedB.Id);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
