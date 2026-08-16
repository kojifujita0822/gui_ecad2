using System.Globalization;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Rendering;

namespace Ecad2.App.Tests;

/// <summary>
/// T-151 ラベル重なり修正（殿ご裁可2026-08-16＝自作パーツには種別既定のラベルオフセットを適用せぬ）の
/// 回帰テスト。隠密テスト設計 <c>docs/ecad2-t151-label-overlap-test-design-onmitsu.md</c> の9節に対応する。
/// <para>
/// 【発端】自作パーツ「ソレノイド」を <c>Role=Coil</c> として正しく解決した結果、機器名ラベルが
/// <c>DefaultLabelDy(Coil) = -5.72</c>（＝組込みコイルの丸の中心）へ寄り、折れ線の絵と重なった。
/// 既定値は組込みコイルの円の幾何に較正されたものにて、任意形状の自作パーツには当たらぬ。
/// </para>
/// <para>
/// 【本テスト群が固定する不変条件】弁別は<b>フォルダの物理配置（Category）に依らず</b>、
/// 組込みの固定Id集合との照合で行う（設計書0-2）。ゆえに本ファイルはいずれのケースでも
/// Category を模した経路を作らず、<b>Idの出自だけ</b>で結果が決まることを外部から観測する。
/// </para>
/// </summary>
public class T151CustomPartLabelOffsetTests : ViewModelTestBase
{
    /// <summary>修正後、自作パーツに適用されるラベル既定オフセット（殿ご裁可＝読みA「オフセット無し」）。
    /// <para>
    /// 設計書3節が読みA（0.0）／読みB（-1.5＝<c>e.Kind</c>=ContactNO の値）の二案を立て、
    /// <b>殿が読みAをお選びになった</b>（家老経由2026-08-16）。既に調整無しで置かれておる6種
    /// （タイマ接点・サーマル・非常停止等）と揃うこと、実装が最も単純であることが理由にござる。
    /// </para>
    /// <b>期待値をここへ一本化してある</b>——数値が覆っても本定数1行で追随できる。</summary>
    private const double ExpectedCustomPartLabelDy = 0.0;

    /// <summary>組込みコイルの既定オフセット（T-097の実測補正済み）。本修正で<b>変わってはならぬ</b>値。</summary>
    private const double BuiltinCoilLabelDy = -5.72;

    /// <summary>未解決PartId・Kind経路が落ちる先（<c>e.Kind</c>＝構造的既定値 ContactNO の値）。</summary>
    private const double ContactNoLabelDy = -1.5;

    private const string CustomCoilId = "t151-label-custom-coil";

    /// <summary>自作パーツ定義。組込みコイル（円）とは異なる折れ線の絵を持たせてある
    /// ——発端となったソレノイドと同じ形にて、「たまたま組込みと同じ絵」で弁別が誤魔化されるのを防ぐ。</summary>
    private static PartDefinition CustomPart(string id, string name, PartRole role) => new()
    {
        Id = id,
        Name = name,
        WidthCells = 1,
        HeightCells = 1,
        Role = role,
        Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
        Primitives = new() { new PartPolyline(new[] { 0.0, 0.0, 0.35, -0.3, 0.65, 0.3, 1.0, 0.0 }) },
    };

    private MainWindowViewModel CreateViewModelWithLocalParts(params PartDefinition[] parts)
    {
        var vm = CreateViewModel();
        foreach (var part in parts) vm.PartPalette.SaveNewPart(part);
        vm.NewDocument();
        return vm;
    }

    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string partId, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(partId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    /// <summary>選択中要素に適用されておる「既定オフセットの絶対値」を、プロパティパネルの相対値から復元する。
    /// <para>
    /// <c>SelectedElementLabelDy</c> は既定値を0とした相対値にて、<c>Params[LabelDy]</c> 未設定なら
    /// 常に "0" を返す。ゆえに相対値そのものからは既定値が読めぬ——<b>既知の相対値を一度書き込み、
    /// その折に <c>Params[LabelDy]</c> へ入る絶対値から逆算する</b>（<c>T097LabelDyTests</c> と同じ手法）。
    /// </para></summary>
    private static double DefaultLabelDyViaPropertyPanel(MainWindowViewModel vm)
    {
        const double probe = 1.0;
        vm.SelectedElementLabelDy = probe.ToString(CultureInfo.InvariantCulture);
        double absolute = double.Parse(vm.SelectedElement!.Params[ParamKeys.LabelDy], CultureInfo.InvariantCulture);
        vm.SelectedElementLabelDy = "0";   // 既定へ戻す（相対0で Params エントリ自体が消える）
        return absolute - probe;
    }

    // ------------------------------------------------------------------
    // 9-1. 主眼＝自作 Role=Coil がコイル用オフセットを引きずらぬこと
    // ------------------------------------------------------------------

    /// <summary>設計書9-1。本タスクの発端そのもの（ソレノイド＝自作の Role=Coil）。</summary>
    [Fact]
    public void 自作パーツのRoleCoilはコイル用オフセットを用いぬ()
    {
        var vm = CreateViewModelWithLocalParts(CustomPart(CustomCoilId, "T151ソレノイド", PartRole.Coil));
        PlaceAt(vm, 0, 0, CustomCoilId, "SOL1");

        double dy = DefaultLabelDyViaPropertyPanel(vm);

        Assert.NotEqual(BuiltinCoilLabelDy, dy);
        Assert.Equal(ExpectedCustomPartLabelDy, dy);
    }

    // ------------------------------------------------------------------
    // 9-2. 弁別の境界（設計書1節の同値分割）
    // ------------------------------------------------------------------

    /// <summary>設計書9-2 #1。組込みパーツは従来どおり——本修正が波及してはならぬ側。</summary>
    [Fact]
    public void 組込みコイルは従来どおりコイル用オフセットを用いる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, BasicPartTemplates.CoilId, "Y001");

        Assert.Equal(BuiltinCoilLabelDy, DefaultLabelDyViaPropertyPanel(vm));
    }

    /// <summary>設計書9-2 #3。<b>Categoryに依らぬこと</b>の直接の固定（設計書0-2の不変条件）。
    /// <para>
    /// ローカルの「図形/自作」フォルダを一切経由せず、図面の埋め込みライブラリへ直に定義を置いて配置する。
    /// フォルダ構造を判定に用いる実装であればここで落ちる——<b>置き場所ではなくIdの出自だけで決まる</b>
    /// ことを外部から観測しておる。
    /// </para></summary>
    [Fact]
    public void フォルダを経ずに埋め込まれた自作パーツも自作として扱われる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.Document.Library ??= new PartLibrary();
        vm.Document.Library.ById[CustomCoilId] = CustomPart(CustomCoilId, "T151埋め込みのみ", PartRole.Coil);

        PlaceAt(vm, 0, 0, CustomCoilId, "SOL2");

        Assert.Equal(ExpectedCustomPartLabelDy, DefaultLabelDyViaPropertyPanel(vm));
    }

    /// <summary>設計書9-2 #4。未解決PartIdは<b>対象外・現状維持</b>——`DRC-PART-001` が警告しておる
    /// 最中の要素の見た目まで変えてはならぬ（設計書1節の但し書き）。</summary>
    [Fact]
    public void 未解決のPartIdは現状維持で構造的既定値へ落ちる()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, "t151-no-such-part-id", "X999");

        Assert.Equal(ContactNoLabelDy, DefaultLabelDyViaPropertyPanel(vm));
    }

    // ------------------------------------------------------------------
    // 9-3. 対称性・組込み非波及の回帰網
    // ------------------------------------------------------------------

    /// <summary>設計書9-3前半。既に0の6種のうち3種（Timer系・熱動系・切替系と写像先を横断）。
    /// 修正の前後で不変であることを固定する。</summary>
    [Theory]
    [InlineData(PartRole.TimerContactNO)]
    [InlineData(PartRole.ThermalOverload)]
    [InlineData(PartRole.SelectSwitch)]
    public void 元より0の役割は自作でも0のまま(PartRole role)
    {
        var vm = CreateViewModelWithLocalParts(CustomPart("t151-zero-" + role, "T151ゼロ" + role, role));
        PlaceAt(vm, 0, 0, "t151-zero-" + role, "Z001");

        Assert.Equal(ExpectedCustomPartLabelDy, DefaultLabelDyViaPropertyPanel(vm));
    }

    /// <summary>設計書9-3後半。組込み側の回帰網——自作向けの修正が組込みへ波及しておらぬこと。
    /// 端子台(-2.0)は既定値の中で最大の移動幅を持つゆえ代表に含めてある。</summary>
    [Theory]
    [InlineData(BasicPartTemplates.CoilId, -5.72)]
    [InlineData(BasicPartTemplates.ContactNOId, -1.5)]
    [InlineData(BasicPartTemplates.TerminalId, -2.0)]
    public void 組込みパーツの既定オフセットは修正の影響を受けぬ(string builtinId, double expected)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 0, 0, builtinId, "B001");

        Assert.Equal(expected, DefaultLabelDyViaPropertyPanel(vm));
    }

    // ------------------------------------------------------------------
    // 9-4. 二消費者の食い違い検証（侍の所見1）
    // ------------------------------------------------------------------

    /// <summary>設計書9-4。<b>描画側だけを直してプロパティパネル側を直し忘れた実装を検出する網。</b>
    /// <para>
    /// <c>ElementCatalog.DefaultLabelDy</c> の実呼び出しは二箇所ある——描画（<c>DiagramRenderer</c>）と
    /// プロパティパネルの相対値の基準（<c>MainWindowViewModel.DefaultLabelDyOf</c>）。
    /// T-145 はこの二つを意図的に一元化しており、<c>MainWindowViewModel</c> のdocコメントが
    /// 「片方だけ直せば、入力欄に0と出ておるのに絵は別の場所という食い違いが生じる」と警告しておる。
    /// 本テストはその食い違いを機械的に突く。
    /// </para></summary>
    [Fact]
    public void 自作パーツの描画位置とプロパティパネルの基準が一致する()
    {
        var vm = CreateViewModelWithLocalParts(CustomPart(CustomCoilId, "T151ソレノイド", PartRole.Coil));
        PlaceAt(vm, 0, 0, CustomCoilId, "SOL1");
        // 前提＝個別値は未設定（既定値がそのまま効いておる状態を測る）。
        Assert.False(vm.SelectedElement!.Params.ContainsKey(ParamKeys.LabelDy));
        Assert.Equal("0", vm.SelectedElementLabelDy);

        double drawnDy = DrawnLabelDy(vm, "SOL1");
        double panelDy = DefaultLabelDyViaPropertyPanel(vm);

        Assert.Equal(ExpectedCustomPartLabelDy, drawnDy);
        Assert.Equal(panelDy, drawnDy, 6);
    }

    // ------------------------------------------------------------------
    // 9-5. Role横断の対称性（Coilだけを特別扱いした実装を検出する）
    // ------------------------------------------------------------------

    /// <summary>設計書9-5。影響を受ける8種のうち4種を代表として選ぶ
    /// （-1.5系の代表ContactNO・最大値のCoil・次点のTerminal・構造的既定値経由で異質なNonSimulated）。
    /// <b>Coil以外の1種でもズレておれば、Coil限定の場当たり実装であったことが判る。</b></summary>
    [Theory]
    [InlineData(PartRole.ContactNO)]
    [InlineData(PartRole.Coil)]
    [InlineData(PartRole.Terminal)]
    [InlineData(PartRole.NonSimulated)]
    public void 影響を受ける役割はいずれも同じ既定値へ揃う(PartRole role)
    {
        var vm = CreateViewModelWithLocalParts(CustomPart("t151-role-" + role, "T151役割" + role, role));
        PlaceAt(vm, 0, 0, "t151-role-" + role, "R001");

        Assert.Equal(ExpectedCustomPartLabelDy, DefaultLabelDyViaPropertyPanel(vm));
    }

    // ------------------------------------------------------------------
    // 9-6. 個別値の優先（既存契約の保持）
    // ------------------------------------------------------------------

    /// <summary>設計書9-6。「既定値を適用せぬ」の射程は<b>既定のみ</b>にて、
    /// 使い手が手で調整した個別値（密集回避の既存機能）を殺してはならぬ。</summary>
    [Fact]
    public void 自作パーツでも個別のLabelDyは既定より優先される()
    {
        var vm = CreateViewModelWithLocalParts(CustomPart(CustomCoilId, "T151ソレノイド", PartRole.Coil));
        PlaceAt(vm, 0, 0, CustomCoilId, "SOL1");

        vm.SelectedElementLabelDy = "3";

        // 既定が0ゆえ絶対値も3。既定値の変更に引きずられぬことを、描画側の実測で確かめる。
        Assert.Equal("3", vm.SelectedElement!.Params[ParamKeys.LabelDy]);
        Assert.Equal(3.0, DrawnLabelDy(vm, "SOL1"), 6);
    }

    // ------------------------------------------------------------------
    // 描画側の実測ヘルパー
    // ------------------------------------------------------------------

    /// <summary>実際に描かれた機器名ラベルから、適用されたdyを逆算する。
    /// <para>
    /// <c>DrawElementLabel</c> は <c>yn = YRow(row) - Cell * 0.50 - dy</c> で描く（dy&gt;0で上へ）。
    /// ゆえに同じ行の基準線を別途求めれば dy が解ける。基準線は<b>行コメントでも機器名でもない</b>
    /// 独立の値が要るゆえ、<b>既定値が0と分かっておる要素を同じ行に置いて基準とはせず</b>、
    /// 描画そのものの式から解く——<c>dy = (YRow(row) - Cell*0.50) - yn</c>。
    /// <c>YRow</c>・<c>Cell</c> は <c>DiagramRenderer</c> の内部値ゆえ、
    /// <b>同一要素を個別値0で描いた場合との差分</b>で消去する。
    /// </para></summary>
    private static double DrawnLabelDy(MainWindowViewModel vm, string deviceName)
    {
        double ynDefault = DrawnLabelY(vm, deviceName);

        // 基準＝同じ要素に個別値0を明示した場合の描画位置。dy=0 ゆえ yn0 = YRow - Cell*0.50。
        var element = vm.CurrentSheet!.Elements.First(e => e.DeviceName == deviceName);
        bool hadExplicit = element.Params.TryGetValue(ParamKeys.LabelDy, out string? saved);
        element.Params[ParamKeys.LabelDy] = "0";
        double ynZero = DrawnLabelY(vm, deviceName);
        if (hadExplicit) element.Params[ParamKeys.LabelDy] = saved!;
        else element.Params.Remove(ParamKeys.LabelDy);

        return ynZero - ynDefault;   // yn = 基準 - dy ゆえ、基準 - yn = dy
    }

    private static double DrawnLabelY(MainWindowViewModel vm, string deviceName)
    {
        var recorder = new LabelRecordingRenderer();
        var renderer = new DiagramRenderer(options: new RenderOptions { ShowDeviceNames = true });
        renderer.Render(recorder, vm.CurrentSheet!, vm.PartLibrary);
        var entry = recorder.Texts.FirstOrDefault(t => t.Text == deviceName);
        Assert.False(entry.Text is null, $"機器名ラベル '{deviceName}' が描かれておらぬ");
        return entry.Position.Y;
    }

    /// <summary><c>DrawText</c> だけを記録する最小のテストダブル。
    /// <c>Ecad2.Core.Tests</c> の <c>RecordingRenderer</c> は internal ゆえ本プロジェクトからは使えぬ。
    /// ここで要るのは文字と座標のみにて、他の描画命令は捨ててよい。</summary>
    private sealed class LabelRecordingRenderer : IRenderer
    {
        public List<(string Text, Point2D Position, TextStyle Style)> Texts { get; } = new();

        public void DrawText(string text, Point2D position, TextStyle style) => Texts.Add((text, position, style));
        public Size2D MeasureText(string text, TextStyle style) => new(text.Length * style.FontSizeMm, style.FontSizeMm);

        public void PushTransform(double translateX, double translateY, double scale = 1.0) { }
        public void PopTransform() { }
        public void PushClip(Rect2D rect) { }
        public void PopClip() { }
        public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) { }
        public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke) { }
        public void DrawRectangle(Rect2D rect, StrokeStyle stroke) { }
        public void FillRectangle(Rect2D rect, Color color) { }
        public void DrawCircle(Point2D center, double radius, StrokeStyle stroke) { }
        public void FillCircle(Point2D center, double radius, Color color) { }
        public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke) { }
        public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke) { }
        public void DrawImage(string filePath, Rect2D bounds) { }
    }
}
