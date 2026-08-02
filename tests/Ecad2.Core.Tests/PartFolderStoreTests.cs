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

    // ===== T-061 A-1構造対処: セレクトSWのRole(ContactNO->SelectSwitch)マイグレーション回帰テスト =====
    // (docs/ecad2-t061-a1-select-switch-design-onmitsu.md 3-5節、Enumerate_LegacySelectSwitchJson
    // WithoutIsOrEligible_StaysFalseと同じ固定Id・同じ手法)

    [Fact]
    public void Enumerate_LegacySelectSwitchJsonWithContactNORole_BackfillsToSelectSwitchRoleAndSaves()
    {
        string tempDir = CreateTempDir();
        try
        {
            // PartRole.SelectSwitch追加(A-1構造対処)より前に生成された旧版JSONを再現
            // (固定Id=SelectSwitchId、role="contactNO")。
            string path = Path.Combine(tempDir, "セレクトSW.gcadpart");
            File.WriteAllText(path, $$"""{"id":"{{BasicPartTemplates.SelectSwitchId}}","name":"セレクトSW","role":"contactNO"}""");

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.Equal(PartRole.SelectSwitch, result.Entries.Single().Definition.Role);

            var reloaded = PartLibrarySerializer.LoadOne(path);
            Assert.Equal(PartRole.SelectSwitch, reloaded.Role);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_SelectSwitchAlreadyMigrated_NoChangeIdempotent()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 既に補正済み(role="selectSwitch")のファイルを再実行しても変化しないこと(冪等性)。
            string path = Path.Combine(tempDir, "セレクトSW.gcadpart");
            File.WriteAllText(path, $$"""{"id":"{{BasicPartTemplates.SelectSwitchId}}","name":"セレクトSW","role":"selectSwitch"}""");

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.Equal(PartRole.SelectSwitch, result.Entries.Single().Definition.Role);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_UserCustomizedSelectSwitchRole_NotOverwritten()
    {
        string tempDir = CreateTempDir();
        try
        {
            // ユーザーが意図的に別Role(ContactNC)へ変更済みのセレクトSW.gcadpartは、
            // ContactNOのままの場合のみ対象とするマイグレーション条件から外れ上書きされない。
            string path = Path.Combine(tempDir, "セレクトSW.gcadpart");
            File.WriteAllText(path, $$"""{"id":"{{BasicPartTemplates.SelectSwitchId}}","name":"セレクトSW","role":"contactNC"}""");

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.Equal(PartRole.ContactNC, result.Entries.Single().Definition.Role);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_OtherBasicPartWithContactNORole_NotAffected()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 固定Id=ContactNOId(通常のa接点)は対象外(Id不一致)でRole=ContactNOのまま変化しない
            // (誤爆防止、IsOrEligible固定Id補正の対象がContactNOId/ContactNCIdの2件限定である
            // ことの再混入防止テストと同型)。
            string path = Path.Combine(tempDir, "a接点.gcadpart");
            File.WriteAllText(path, $$"""{"id":"{{BasicPartTemplates.ContactNOId}}","name":"a接点","role":"contactNO"}""");

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.Equal(PartRole.ContactNO, result.Entries.Single().Definition.Role);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_LegacySelectSwitchJsonReadOnly_BackfillsInMemoryWithoutThrowing()
    {
        string tempDir = CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "セレクトSW.gcadpart");
            File.WriteAllText(path, $$"""{"id":"{{BasicPartTemplates.SelectSwitchId}}","name":"セレクトSW","role":"contactNO"}""");
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

            // 書き戻し失敗(読み取り専用)でも例外は外へ伝播せず、メモリ上は補正済みで継続する。
            Assert.Equal(PartRole.SelectSwitch, result.Entries.Single().Definition.Role);
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

    // ===== T-143: 展開済みモータの kind 欠落を DrcExempt へ補正するマイグレーション回帰テスト =====
    // (docs/ecad2-t143-portkind-fixup-test-design-onmitsu.md、上記 Role 補正テストと同じ固定Id・同じ手法)
    //
    // 【背景】T-136(B)増分5(殿裁定7=モータの3端子は青)より前に展開された モータ.gcadpart は
    // ports に kind を持たず、デシリアライズで PortDef.Kind の既定値 Power(赤)へ落ちる。
    // SeedBasics は既存ファイルを上書きせぬ設計(冪等)ゆえ、コード側の青化が実運用データへ届かない。
    //
    // 【測れておらぬ範囲・設計書§1】「kind 欠落」と「意図的に power と書かれた」は JSON 上区別が
    // つかぬ(PortKind は2値ゆえ、前例 T-061 の Role 補正が使う「既定値以外なら意図的」の判定が
    // 成り立たない)。ゆえに後者を保護するテストは原理的に書けない。実装は「基本図形はパーツエディタの
    // 編集対象に含まれぬ」という構造を根拠に割り切っており、その構造が崩れれば本テスト群では検出できない。

    /// <summary>kind 欠落の3端子を持つ旧版モータJSON(展開済み実データそのものの形)。
    /// ports 以外のフィールドは非破壊性(観点B)の測定対象として意図的に持たせてある。</summary>
    private static string LegacyMotorJson(string id) =>
        $$"""
        {"id":"{{id}}","name":"モータ","widthCells":3,"heightCells":1,"role":"nonSimulated","ports":[{"name":"U","rowOffset":0,"boundaryOffset":0},{"name":"V","rowOffset":0,"boundaryOffset":1},{"name":"W","rowOffset":0,"boundaryOffset":2}],"primitives":[{"type":"circle","cx":2.05,"cy":0,"r":0.92}]}
        """;

    [Fact]
    public void Enumerate_LegacyMotorJsonWithoutKind_BackfillsToDrcExemptAndSaves()
    {
        string tempDir = CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "モータ.gcadpart");
            File.WriteAllText(path, LegacyMotorJson(BasicPartTemplates.MotorId));

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            var ports = result.Entries.Single().Definition.Ports;
            // 観点D(対称性): 3端子を1つだけ見て済ませない。かつ件数だけでは「在るべき端子に
            // 付いているか」を測れぬゆえ、端子名で引いて個別に確認する。
            Assert.Equal(3, ports.Count);
            Assert.All(ports, p => Assert.Equal(PortKind.DrcExempt, p.Kind));
            Assert.Equal(PortKind.DrcExempt, ports.Single(p => p.Name == "U").Kind);
            Assert.Equal(PortKind.DrcExempt, ports.Single(p => p.Name == "V").Kind);
            Assert.Equal(PortKind.DrcExempt, ports.Single(p => p.Name == "W").Kind);

            // 観点C1: ファイルへ書き戻されている(次回起動でも維持される)。
            var reloaded = PartLibrarySerializer.LoadOne(path);
            Assert.All(reloaded.Ports, p => Assert.Equal(PortKind.DrcExempt, p.Kind));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_MotorAlreadyDrcExempt_NoChangeIdempotent()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 既に補正済み(kind="drcExempt")のファイルを再実行しても変化しないこと(冪等性)。
            string path = Path.Combine(tempDir, "モータ.gcadpart");
            File.WriteAllText(path, $$"""
                {"id":"{{BasicPartTemplates.MotorId}}","name":"モータ","ports":[{"name":"U","rowOffset":0,"boundaryOffset":0,"kind":"drcExempt"},{"name":"V","rowOffset":0,"boundaryOffset":1,"kind":"drcExempt"},{"name":"W","rowOffset":0,"boundaryOffset":2,"kind":"drcExempt"}]}
                """);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.All(result.Entries.Single().Definition.Ports,
                p => Assert.Equal(PortKind.DrcExempt, p.Kind));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_MotorBackfillTwice_FileUnchangedOnSecondRun()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 観点C2: 1回目で補正・書き戻しが起きた後、2回目の走査でファイルが変わらないこと。
            // 起動のたびに Enumerate が走る設計(PartPaletteViewModel のコンストラクタ)ゆえ、
            // 補正が収束せず毎回書き込み続ける形になっていないかを測る。
            //
            // 【内容比較だけでは足りぬ】書き戻す内容は補正後も同一ゆえ、内容の一致は「書き込みが
            // 起きなかった」ことを意味しない(実測: 要否判定を外して毎回書き戻す改変を当てても
            // 内容比較はGREENのまま通った)。ゆえに最終更新時刻でも測る。時刻は1回目の直後に
            // 既知の過去値へ落としておく——2回の走査が同一時刻内に収まって偽陰性になるのを防ぐ
            // (SetCreationOrder が作成時刻を明示設定するのと同じ発想)。
            string path = Path.Combine(tempDir, "モータ.gcadpart");
            File.WriteAllText(path, LegacyMotorJson(BasicPartTemplates.MotorId));

            var store = new PartFolderStore(tempDir);
            store.Enumerate();
            string afterFirst = File.ReadAllText(path);

            var marker = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, marker);

            store.Enumerate();
            string afterSecond = File.ReadAllText(path);

            Assert.Equal(afterFirst, afterSecond);
            // 2回目では書き戻し自体が起きぬ(補正の要否判定が効いておる)。
            Assert.Equal(marker, File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_MotorPartiallyMigrated_BackfillsOnlyMissingPortsAndKeepsExisting()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 観点A4(境界): 3端子中 U のみ kind 欠落、V/W は既に drcExempt。
            // 欠落分だけが補正され、既に青の2件も青のまま保たれること。
            string path = Path.Combine(tempDir, "モータ.gcadpart");
            File.WriteAllText(path, $$"""
                {"id":"{{BasicPartTemplates.MotorId}}","name":"モータ","ports":[{"name":"U","rowOffset":0,"boundaryOffset":0},{"name":"V","rowOffset":0,"boundaryOffset":1,"kind":"drcExempt"},{"name":"W","rowOffset":0,"boundaryOffset":2,"kind":"drcExempt"}]}
                """);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            var ports = result.Entries.Single().Definition.Ports;
            Assert.Equal(PortKind.DrcExempt, ports.Single(p => p.Name == "U").Kind);
            Assert.Equal(PortKind.DrcExempt, ports.Single(p => p.Name == "V").Kind);
            Assert.Equal(PortKind.DrcExempt, ports.Single(p => p.Name == "W").Kind);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_OtherBasicPartWithoutKind_NotAffected()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 観点A3(誤爆防止): 固定Id=ContactNOId(a接点)は対象外(Id不一致)で Kind=Power のまま。
            // 殿裁定7は「モータのみ青・他は赤」ゆえ、赤の側が黙って青へ流れぬことを固定する。
            string path = Path.Combine(tempDir, "a接点.gcadpart");
            File.WriteAllText(path, $$"""
                {"id":"{{BasicPartTemplates.ContactNOId}}","name":"a接点","ports":[{"name":"L","rowOffset":0,"boundaryOffset":0},{"name":"R","rowOffset":0,"boundaryOffset":1}]}
                """);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            Assert.All(result.Entries.Single().Definition.Ports,
                p => Assert.Equal(PortKind.Power, p.Kind));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_SelectSwitchWithoutKind_NotAffected()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 観点E1(誤爆防止・別の固定Id): Role 補正の対象である セレクトSW も、Kind は
            // 触られず Power のまま。「固定Idごとの補正」同士が混線しておらぬことを測る。
            string path = Path.Combine(tempDir, "セレクトSW.gcadpart");
            File.WriteAllText(path, $$"""
                {"id":"{{BasicPartTemplates.SelectSwitchId}}","name":"セレクトSW","role":"contactNO","ports":[{"name":"L","rowOffset":0,"boundaryOffset":0},{"name":"R","rowOffset":0,"boundaryOffset":1}]}
                """);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            var def = result.Entries.Single().Definition;
            Assert.All(def.Ports, p => Assert.Equal(PortKind.Power, p.Kind));
            // Role 補正の側は従来どおり効いていること(本タスクが既存の補正を壊しておらぬ確認)。
            Assert.Equal(PartRole.SelectSwitch, def.Role);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_MotorBackfill_PreservesAllOtherFields()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 観点B(破壊性・DoD2の核心): Kind 以外は一字一句変わらぬこと。
            // 標準と異なる name・セル数・primitives・端子座標を入れ、補正後もそれらが
            // 保たれることをフィールド単位で測る。
            string path = Path.Combine(tempDir, "モータ.gcadpart");
            File.WriteAllText(path, $$"""
                {"id":"{{BasicPartTemplates.MotorId}}","name":"三相モータ改","widthCells":5,"heightCells":3,"role":"nonSimulated","ports":[{"name":"U2","rowOffset":1,"boundaryOffset":2},{"name":"V2","rowOffset":-1,"boundaryOffset":3},{"name":"W2","rowOffset":0,"boundaryOffset":4}],"primitives":[{"type":"circle","cx":3.5,"cy":0.25,"r":1.75}]}
                """);

            var store = new PartFolderStore(tempDir);
            var result = store.Enumerate();

            var def = result.Entries.Single().Definition;
            // B2: 名前・セル数・役割
            Assert.Equal("三相モータ改", def.Name);
            Assert.Equal(5, def.WidthCells);
            Assert.Equal(3, def.HeightCells);
            Assert.Equal(PartRole.NonSimulated, def.Role);
            // B1: 図形プリミティブ
            var circle = Assert.IsType<PartCircle>(Assert.Single(def.Primitives));
            Assert.Equal(3.5, circle.Cx);
            Assert.Equal(0.25, circle.Cy);
            Assert.Equal(1.75, circle.R);
            // B3: PortDef の Kind 以外(名前・行・境界)。Kind だけが変わっていること。
            var u = def.Ports.Single(p => p.Name == "U2");
            Assert.Equal(1, u.RowOffset);
            Assert.Equal(2, u.BoundaryOffset);
            Assert.Equal(PortKind.DrcExempt, u.Kind);
            var v = def.Ports.Single(p => p.Name == "V2");
            Assert.Equal(-1, v.RowOffset);
            Assert.Equal(3, v.BoundaryOffset);
            var w = def.Ports.Single(p => p.Name == "W2");
            Assert.Equal(0, w.RowOffset);
            Assert.Equal(4, w.BoundaryOffset);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_LegacyMotorJsonReadOnly_BackfillsInMemoryWithoutThrowing()
    {
        string tempDir = CreateTempDir();
        try
        {
            // 観点E2: 書き戻し失敗(読み取り専用)でも例外は外へ伝播せず、メモリ上は補正済みで継続する
            // (OneDrive同期中のロック等を想定。前例2件と同型のベストエフォート方針)。
            string path = Path.Combine(tempDir, "モータ.gcadpart");
            File.WriteAllText(path, LegacyMotorJson(BasicPartTemplates.MotorId));
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

            Assert.All(result.Entries.Single().Definition.Ports,
                p => Assert.Equal(PortKind.DrcExempt, p.Kind));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
