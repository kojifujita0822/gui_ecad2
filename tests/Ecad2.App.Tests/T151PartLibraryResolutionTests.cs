using System.IO;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Pdf;
using Ecad2.Persistence;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.App.Tests;

/// <summary>
/// T-151（自作パーツ定義を図面へ埋め込む・`P-183`対処）の読込側テスト。
/// 隠密テスト設計 docs/ecad2-t151-test-design-onmitsu.md 4節（読込側の対称確認表・5用途）。
/// <para>
/// <b>【本テスト群が模す状況】</b>ローカルの「図形/自作」に当該定義が<b>一切存在しない</b>状態で、
/// 図面に埋め込まれた定義だけを頼りに5用途すべてが正しく動くこと——すなわち
/// 「他所へ持っていける」ことの実測である。<see cref="ViewModelTestBase"/> がテストごとに
/// 新しい一時フォルダを発行するため、何も置かねばそれがそのまま「よその環境」になる。
/// </para>
/// <para>
/// <b>【設計書からの補正2点・いずれも一次ソースで裏取り済み】</b>
/// (1) 設計書4-1は「Ports を2点（組込みContactNOの既定と数を変えておく）」とするが、
/// <c>ElementCatalog.Ports</c>（<c>ElementCatalog.cs:60-80</c>）の既定枝は <c>L</c>(境界0)・<c>R</c>(境界width) の
/// <b>2点</b>であり、数が変わっておらぬ。フォールバックと弁別できぬため<b>3点</b>へ改めた。
/// (2) 設計書4-2のPDF観点「クロスリファレンス結果にCR1の行が現れる」は、
/// <c>ElementCatalog.IsContact</c>（<c>:191-197</c>）が TimerContactNO も ContactNO も true とするため、
/// 解決に失敗して既定の ContactNO へ落ちても同じく現れる。<b><c>Role=Coil</c> の部品を足し</b>、
/// <c>Coils</c> 側か <c>Contacts</c> 側かで弁別する形にした。
/// </para>
/// </summary>
public class T151PartLibraryResolutionTests : ViewModelTestBase
{
    private const string TimerPartId = "t151-embedded-timer";
    private const string CoilPartId = "t151-embedded-coil";
    private const string InputPartId = "t151-embedded-input";
    private const string EmbeddedPartName = "埋込専用テスト部品";

    /// <summary>設計書4-1の識別用定義。組込みの既定値と意図的にずらしてある——
    /// 幅7（組込み17種のいずれとも異なる）・Role=TimerContactNO（既定のContactNOと異なる）・
    /// ポート3点（組込み既定の2点と異なる）・Primitives2種（多態シリアライズの実地確認を兼ねる）。</summary>
    private static PartDefinition EmbeddedTimerPart() => new()
    {
        Id = TimerPartId,
        Name = EmbeddedPartName,
        WidthCells = 7,
        HeightCells = 1,
        Role = PartRole.TimerContactNO,
        Ports = { new PortDef("L", 0, 0), new PortDef("M", 0, 1), new PortDef("R", 0, 2) },
        Primitives = { new PartLine(0, 0, 7, 0), new PartRect(1, -0.4, 5, 0.8) },
    };

    /// <summary>PDF/クロスリファレンス弁別用（上の補正(2)）。Role=Coil は <c>IsLoad</c>＝Coils側へ入り、
    /// 解決失敗時のフォールバック（ContactNO＝Contacts側）と一撃で見分けがつく。</summary>
    private static PartDefinition EmbeddedCoilPart() => new()
    {
        Id = CoilPartId,
        Name = "埋込コイル部品",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.Coil,
        Ports = { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
        Primitives = { new PartCircle(0.5, 0, 0.3) },
    };

    /// <summary>忍者の実測（docs/ecad2-t151-before-fix-symptom-ninja.md）を承けて足した観点用。
    /// Role=InputNO は <c>PushButtonNO</c> へ写像され <c>IsInputControlled</c>＝true ゆえ
    /// <c>DRC-XREF-001</c>（駆動コイル無し）の対象から外れる（<c>DesignRuleCheck.cs:65-71</c>）。
    /// 解決に失敗すると既定の ContactNO へ落ち、対象に入って鳴る。</summary>
    private static PartDefinition EmbeddedInputPart() => new()
    {
        Id = InputPartId,
        Name = "埋込押釦部品",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.InputNO,
        Ports = { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
        Primitives = { new PartLine(0, 0, 1, 0) },
    };

    /// <summary>指定の定義を <see cref="LadderDocument.Library"/> へ埋め込み、
    /// それぞれを1要素ずつ配置した文書を作る（行を分けて置く）。</summary>
    private static LadderDocument MakeEmbeddedDocument(params (PartDefinition Part, string DeviceName)[] items)
    {
        var doc = new LadderDocument();
        var sheet = new Sheet { PageNumber = 1, Name = "シート1", Grid = new GridSpec { Rows = 10, Columns = 20 } };
        doc.Sheets.Add(sheet);
        doc.Library = new PartLibrary();

        for (int i = 0; i < items.Length; i++)
        {
            var (part, deviceName) = items[i];
            doc.Library.ById[part.Id] = part;
            sheet.Elements.Add(new ElementInstance
            {
                PartId = part.Id,
                Pos = new GridPos(i * 2, 0),
                DeviceName = deviceName,
            });
        }
        return doc;
    }

    /// <summary>文書を .gcad へ書き出し、ViewModel で開いた状態で検証を行う。
    /// 「保存して、定義を持たぬ環境で開き直す」という本タスクの眼目そのものを模す。</summary>
    private void WithLoadedDocument(LadderDocument doc, Action<MainWindowViewModel, string> verify)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"ecad2-t151-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "embedded.gcad");
            GcadSerializer.Save(doc, path);

            var vm = CreateViewModel();
            vm.LoadFromFile(path);
            verify(vm, dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>用途1＝DRC。埋め込み定義で解決できるなら DRC-PART-001 は出ない。</summary>
    [Fact]
    public void CheckUnresolvedPartId_EmbeddedDefinitionWithEmptyLocalFolder_NoWarning()
    {
        var doc = MakeEmbeddedDocument((EmbeddedTimerPart(), "CR1"));

        WithLoadedDocument(doc, (vm, _) =>
        {
            var diagnostics = DesignRuleCheck.CheckUnresolvedPartId(vm.Document, vm.PartLibrary);

            Assert.Empty(diagnostics);
        });
    }

    /// <summary>用途2＝レンダリング。埋め込み定義の図形・名前が描かれる
    /// （組込みContactNOの定形へ退化していないことを直接示す）。</summary>
    [Fact]
    public void Render_EmbeddedDefinitionWithEmptyLocalFolder_DrawsCorrectPrimitives()
    {
        var doc = MakeEmbeddedDocument((EmbeddedTimerPart(), "CR1"));

        WithLoadedDocument(doc, (vm, _) =>
        {
            var recorder = new PrimitiveRecordingRenderer();
            new DiagramRenderer().Render(recorder, vm.Document.Sheets[0], vm.PartLibrary);

            // 【対照を置く】同じ文書を「定義を解決できぬ状態」（ライブラリ無し）で描いた絵と比べる。
            // 寸法や本数を先に見積もって当てにいくと、座標変換を読み違えた時に穴が空く
            // （memory: feedback_control_experiment_needs_naive_baseline＝素朴なベースラインを混ぜよ）。
            var control = new PrimitiveRecordingRenderer();
            new DiagramRenderer().Render(control, vm.Document.Sheets[0], null);

            // 埋め込み定義は矩形プリミティブを持つ。解決できねば既定の接点記号へ落ちる。
            Assert.True(recorder.Rectangles.Count > control.Rectangles.Count,
                        $"埋込解決時の矩形数={recorder.Rectangles.Count}、未解決時={control.Rectangles.Count}");
            // タイマ限時接点のミニラベル「限」は Role=TimerContactNO を解決できた時のみ描かれる
            // （DiagramRendererLabelTests と同型の観点）。未解決なら既定のContactNOゆえ描かれぬ。
            Assert.Contains("限", recorder.DrawnTexts);
            Assert.DoesNotContain("限", control.DrawnTexts);
        });
    }

    /// <summary>用途3＝ネットリスト。埋め込み定義の Role・ポート数が反映される。
    /// <para><c>Component.Kind</c> は解決できれば TimerContactNO、失敗すれば要素の既定値 ContactNO ゆえ、
    /// これが最も直接の弁別になる。ポート3点は Nets の数へ効く（フォールバックは2点）。</para></summary>
    [Fact]
    public void NetlistBuild_EmbeddedDefinitionWithEmptyLocalFolder_ReflectsPortCount()
    {
        var doc = MakeEmbeddedDocument((EmbeddedTimerPart(), "CR1"));

        WithLoadedDocument(doc, (vm, _) =>
        {
            var netlist = NetlistBuilder.Build(vm.Document.Sheets[0], vm.PartLibrary);

            var component = Assert.Single(netlist.Components);
            Assert.Equal(ElementKind.TimerContactNO, component.Kind);
            // ポート3点（境界0/1/2）を解決できていること。要素の接続点は PartResolver 経由で引く。
            Assert.Equal(3, PartResolver.Ports(vm.Document.Sheets[0].Elements[0], vm.PartLibrary).Count);
        });
    }

    /// <summary>用途4＝プロパティパネル。Role=TimerContactNO を反映する。
    /// <c>IsSelectedElementTimerRelated</c> は <c>PartResolver.CreatesComponent</c>/<c>ComponentKind</c> のみに
    /// 依存し Category を用いぬ（設計書0-4節の指定どおり）。</summary>
    [Fact]
    public void IsSelectedElementTimerRelated_EmbeddedDefinitionWithEmptyLocalFolder_ReflectsRole()
    {
        var doc = MakeEmbeddedDocument((EmbeddedTimerPart(), "CR1"));

        WithLoadedDocument(doc, (vm, _) =>
        {
            vm.SelectedCell = new GridPos(0, 0);

            Assert.NotNull(vm.SelectedElement);
            Assert.True(vm.IsSelectedElementTimerRelated);
        });
    }

    /// <summary>用途5＝PDF出力。例外なく完了し、クロスリファレンスへ機器名が現れる。
    /// <para>弁別は Role=Coil の部品で行う（本ファイル冒頭の補正(2)）——解決できれば Coils 側、
    /// 失敗して ContactNO へ落ちれば Contacts 側に入る。</para></summary>
    [Fact]
    public void PdfExport_EmbeddedDefinitionWithEmptyLocalFolder_IncludesDeviceNameInCrossReference()
    {
        var doc = MakeEmbeddedDocument((EmbeddedCoilPart(), "CR1"));

        WithLoadedDocument(doc, (vm, dir) =>
        {
            string pdfPath = Path.Combine(dir, "out.pdf");
            PdfExporter.Export(vm.Document, vm.PartLibrary, pdfPath);
            Assert.True(File.Exists(pdfPath));

            var xref = CrossReferenceBuilder.Build(vm.Document, vm.PartLibrary);
            Assert.True(xref.TryGet("CR1", out var entry));
            // Role=Coil を解決できていれば負荷（Coils）側。既定のContactNOへ落ちればContacts側になる。
            Assert.Single(entry.Coils);
            Assert.Empty(entry.Contacts);
        });
    }

    /// <summary>設計書4-3＝文書を開き直した時、埋め込みライブラリが新しい文書のものへ切り替わり、
    /// 旧文書の内容が残留しない（「残留」型の穴を狙う）。</summary>
    [Fact]
    public void ReplaceDocument_SwitchesPartLibraryToNewDocumentsEmbeddedLibrary_NoStaleLeak()
    {
        var docA = MakeEmbeddedDocument((EmbeddedTimerPart(), "CR1"));

        var partB = EmbeddedTimerPart();
        partB.WidthCells = 3;
        partB.Name = "文書Bの定義";
        var docB = MakeEmbeddedDocument((partB, "CR1"));

        string dir = Path.Combine(Path.GetTempPath(), $"ecad2-t151-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string pathA = Path.Combine(dir, "a.gcad");
            string pathB = Path.Combine(dir, "b.gcad");
            GcadSerializer.Save(docA, pathA);
            GcadSerializer.Save(docB, pathB);

            var vm = CreateViewModel();
            vm.LoadFromFile(pathA);
            Assert.Equal(7, vm.PartLibrary.Get(TimerPartId)!.WidthCells);

            vm.LoadFromFile(pathB);

            var resolved = vm.PartLibrary.Get(TimerPartId);
            Assert.NotNull(resolved);
            Assert.Equal(3, resolved!.WidthCells);
            Assert.Equal("文書Bの定義", resolved.Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>設計書に無い追加観点（出所＝忍者の修正前症状の実測、
    /// docs/ecad2-t151-before-fix-symptom-ninja.md）。埋め込み定義から <c>Role=InputNO</c> を
    /// 解決できていれば、駆動コイルが無くとも <c>DRC-XREF-001</c> は鳴らぬ。
    /// <para>解決に失敗すると要素の既定値 ContactNO へ落ち、<c>isRelayContact=true</c> となって
    /// 「駆動元不明」の警告が鳴る（<c>DesignRuleCheck.cs:65-71</c>／<c>ElementCatalog.cs:210-212</c>を直読して確認）。
    /// ゆえに埋め込みが効いておらねば必ず落ちる＝検出力を持つ。</para></summary>
    [Fact]
    public void CheckCrossReference_EmbeddedInputRolePart_DoesNotWarnContactWithoutCoil()
    {
        var doc = MakeEmbeddedDocument((EmbeddedInputPart(), "PB1"));

        WithLoadedDocument(doc, (vm, _) =>
        {
            var diagnostics = DesignRuleCheck.CheckCrossReference(vm.Document, vm.PartLibrary);

            Assert.DoesNotContain(diagnostics, d => d.Code == DesignRuleCheck.ContactWithoutCoil);
        });
    }

    /// <summary>本ファイル専用の記録レンダラ。<c>Ecad2.Core.Tests</c> の <c>RecordingRenderer</c> は
    /// 別アセンブリの internal ゆえ使えぬため、必要な記録（テキスト・矩形・線）だけを持つ最小の写しを置く
    /// （<see cref="ViewModelTestBase"/> がプロジェクト跨ぎの共有を避けて独立させたのと同じ判断・家老裁定の前例に倣う）。</summary>
    private sealed class PrimitiveRecordingRenderer : IRenderer
    {
        public List<string> DrawnTexts { get; } = new();
        public List<Rect2D> Rectangles { get; } = new();
        public List<(Point2D A, Point2D B)> Lines { get; } = new();
        public List<(Point2D Center, double Radius)> Circles { get; } = new();

        public void PushTransform(double translateX, double translateY, double scale = 1.0) { }
        public void PopTransform() { }
        public void PushClip(Rect2D rect) { }
        public void PopClip() { }
        public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) => Lines.Add((a, b));
        public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke) { }
        public void DrawRectangle(Rect2D rect, StrokeStyle stroke) => Rectangles.Add(rect);
        public void FillRectangle(Rect2D rect, Color color) { }
        public void DrawCircle(Point2D center, double radius, StrokeStyle stroke) => Circles.Add((center, radius));
        public void FillCircle(Point2D center, double radius, Color color) { }
        public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke) { }
        public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke) { }
        public void DrawText(string text, Point2D position, TextStyle style) => DrawnTexts.Add(text);
        public Size2D MeasureText(string text, TextStyle style) => new(text.Length * style.FontSizeMm, style.FontSizeMm);
        public void DrawImage(string filePath, Rect2D bounds) { }
    }
}
