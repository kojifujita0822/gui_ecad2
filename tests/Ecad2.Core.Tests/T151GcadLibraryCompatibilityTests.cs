using System.IO;
using Ecad2.Model;
using Ecad2.Pdf;
using Ecad2.Persistence;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-151（自作パーツ定義を図面へ埋め込む・`P-183`対処）の後方互換・シリアライズ往復テスト。
/// 隠密テスト設計 docs/ecad2-t151-test-design-onmitsu.md 5節・6-3節。
/// <para>
/// 台帳T-151節のDoD3「既存の <c>.gcad</c>（<c>Library</c> がnull）が従来どおり読めること」に対応する。
/// <see cref="LadderDocument.Library"/> は <c>GcadSerializer</c> が専用コードを持たず
/// <c>System.Text.Json</c> の自動反映で往復するため、<c>library</c> キーを持たぬ旧ファイルは
/// null のまま読まれる——その前提が実際に成り立っていることを実測で固定する。
/// </para>
/// </summary>
public class T151GcadLibraryCompatibilityTests : IDisposable
{
    private readonly string _tempDir;

    public T151GcadLibraryCompatibilityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ecad2-t151-core-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    /// <summary>library キーを持たぬレガシーJSON（T-151以前に ecad2 が書き出した .gcad と同形）。
    /// GcadCompatibilityTests の cellHeight 欠落テストと同じ流儀で、実ファイルの形をそのまま埋める。</summary>
    private const string LegacyJsonWithoutLibrary = """
        {
          "schemaVersion": 1,
          "info": { "title": "旧形式の図面", "drawingNo": "T151-LEGACY" },
          "sheets": [
            {
              "pageNumber": 1,
              "name": "シート1",
              "grid": { "rows": 10, "columns": 20 },
              "elements": [
                { "kind": "contactNO", "pos": { "row": 0, "column": 0 }, "deviceName": "X001" }
              ]
            }
          ]
        }
        """;

    /// <summary>5節1行目＝library キーが無い旧JSONは Library=null で読める（例外なし）。</summary>
    [Fact]
    public void Deserialize_LegacyJsonWithoutLibraryKey_LibraryIsNull()
    {
        var doc = GcadSerializer.Deserialize(LegacyJsonWithoutLibrary);

        Assert.Null(doc.Library);
        // 文書そのものは従来どおり読めていること（Library だけを見て「読めた」と判じぬため）。
        Assert.Single(doc.Sheets);
        Assert.Single(doc.Sheets[0].Elements);
        Assert.Equal("X001", doc.Sheets[0].Elements[0].DeviceName);
    }

    /// <summary>6-3節＝埋め込みライブラリがファイル往復で完全に復元される。
    /// <para>
    /// <c>PartPrimitive</c> は <c>[JsonPolymorphic]</c> で6種の派生型を型判別子付きに直列化する
    /// （<c>PartDefinition.cs:46-53</c>）。属性は既に付いているが、<b>ドキュメント内包という新しい文脈で
    /// 実際にファイルを往復させるのは本タスクが初めて</b>ゆえ、型と値の双方を実測する。
    /// </para></summary>
    [Fact]
    public void SaveThenLoad_EmbeddedLibraryWithPolymorphicPrimitives_RoundTripsExactly()
    {
        var part = new PartDefinition
        {
            Id = "t151-roundtrip",
            Name = "往復確認用パーツ",
            WidthCells = 7,
            HeightCells = 2,
            Role = PartRole.TimerContactNO,
            IsOrEligible = true,
            SheetAffinity = SheetAffinity.ControlOnly,
            Ports = { new PortDef("L", 0, 0), new PortDef("M", 0, 1, PortKind.DrcExempt), new PortDef("R", 1, 2) },
            Primitives =
            {
                new PartLine(0, 0, 7, 0),
                new PartRect(1, -0.4, 5, 0.8, Rot: 15),
                new PartText("銘", 2, 0.5, SizeCells: 0.5),
            },
        };
        var doc = new LadderDocument { Library = new PartLibrary() };
        doc.Library.ById[part.Id] = part;
        doc.Sheets.Add(new Sheet { PageNumber = 1, Grid = new GridSpec { Rows = 10, Columns = 20 } });
        string path = Path.Combine(_tempDir, "roundtrip.gcad");

        GcadSerializer.Save(doc, path);
        var loaded = GcadSerializer.Load(path);

        var restored = loaded.Library?.Get("t151-roundtrip");
        Assert.NotNull(restored);
        Assert.Equal("往復確認用パーツ", restored!.Name);
        Assert.Equal(7, restored.WidthCells);
        Assert.Equal(2, restored.HeightCells);
        Assert.Equal(PartRole.TimerContactNO, restored.Role);
        Assert.True(restored.IsOrEligible);
        Assert.Equal(SheetAffinity.ControlOnly, restored.SheetAffinity);

        Assert.Equal(3, restored.Ports.Count);
        Assert.Equal(new PortDef("M", 0, 1, PortKind.DrcExempt), restored.Ports[1]);
        Assert.Equal(new PortDef("R", 1, 2), restored.Ports[2]);

        // 多態プリミティブ＝型判別子が正しく往復していること（型と値の双方を見る）。
        Assert.Equal(3, restored.Primitives.Count);
        var line = Assert.IsType<PartLine>(restored.Primitives[0]);
        Assert.Equal(7, line.X2);
        var rect = Assert.IsType<PartRect>(restored.Primitives[1]);
        Assert.Equal(15, rect.Rot);
        var text = Assert.IsType<PartText>(restored.Primitives[2]);
        Assert.Equal("銘", text.Text);
        Assert.Equal(0.5, text.SizeCells);
    }

    /// <summary>5節3行目＝Library=null のまま各機能を呼んでも例外にならぬ（nullガードの実測）。
    /// <para>設計書は代表1件（DRC）を挙げ「他4関数は侍の実装時に同型で追加してよい」とするため、
    /// Core層で呼べる4用途すべてを1本に束ねた。プロパティパネル用途は App 層ゆえ
    /// <c>T151PartLibraryResolutionTests</c> の側が受け持つ。</para></summary>
    [Fact]
    public void CoreFunctions_NullLibrary_NoException()
    {
        var doc = GcadSerializer.Deserialize(LegacyJsonWithoutLibrary);
        Assert.Null(doc.Library);   // 前提の明示
        var sheet = doc.Sheets[0];
        string pdfPath = Path.Combine(_tempDir, "null-library.pdf");

        // DRC（未解決PartId・クロスリファレンス）
        var unresolved = DesignRuleCheck.CheckUnresolvedPartId(doc, doc.Library);
        var crossRef = DesignRuleCheck.CheckCrossReference(doc, doc.Library);
        // ネットリスト
        var netlist = NetlistBuilder.Build(sheet, doc.Library);
        // レンダリング
        var recorder = new RecordingRenderer();
        new DiagramRenderer().Render(recorder, sheet, doc.Library);
        // PDF出力
        PdfExporter.Export(doc, doc.Library, pdfPath);

        // PartId を持たぬ組込み要素ゆえ未解決診断は出ない（null ガードが効いていることの裏返し）。
        Assert.Empty(unresolved);
        Assert.NotNull(crossRef);
        Assert.NotNull(netlist);
        Assert.NotEmpty(recorder.Ops);
        Assert.True(File.Exists(pdfPath));
    }
}
