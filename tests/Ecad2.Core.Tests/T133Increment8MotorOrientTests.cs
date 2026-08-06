using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-133増分8（殿裁定5）: 縦向きモータ図形の描画分岐。
/// <para>
/// <b>測る対象は3つ</b>——(a) <c>Orient="V"</c> で縦向き（端子が横に3つ）が描かれること、
/// (b) それ以外の値・未設定では従来の横向きが保たれること、
/// (c) <c>PartId</c> を持つ既存のモータは <c>Orient</c> を付けても描画が変わらぬこと。
/// </para>
/// <para>
/// <b>(c) が本増分の後方互換の要である。</b> 既存ファイルのモータはすべて
/// <c>PartId="basic-motor"</c> 経由で置かれており、<see cref="DiagramRenderer"/> の
/// <c>if (part is not null) PartDrawing.Draw(...) else SymbolGlyphs.Draw(...)</c> 分岐により
/// <c>SymbolGlyphs.Motor</c> へは到達せぬ——<b>すなわち後方互換は構造が守っておる。</b>
/// 侍・隠密が独立に一次ソースで裏を取った事実であるが、<b>構造は崩れうる</b>ゆえ、
/// 崩れたときに気づく手立てとしてここへ固定する（家老のDoD＝「構造が守っておるうちは、
/// 崩れたときに気づく手立てが要る」2026-08-06）。
/// </para>
/// <para>
/// <b>【意匠は未確定である】</b>本増分の絵は「原本の横向きを時計回りに90度回した形」＝第一案にすぎぬ
/// （回す向きのみ殿裁定2026-08-06で確定＝大円が下。寸法・形は絵をご覧いただいてから）。
/// 殿が実機の絵をご覧になって確定なされる段取りゆえ、<b>座標を固定する下2件（端子・大円の位置）は
/// 意匠が改まれば期待値も改まる</b>。そのとき直すべきは期待値の側にて、これは堰ではない。
/// </para>
/// </summary>
public class T133Increment8MotorOrientTests
{
    private const double Cell = 9.0;                // RenderOptions.CellMm の既定
    // 端子 ⊘ と本体大円の半径（横向き・縦向きで共通）。値は殿がお描きになった意匠
    // sample/モーター(横).gcadparts に依る（殿裁定2026-08-06。旧意匠は 0.18 / 0.92 であった）。
    private const double TerminalR = 0.125 * Cell;
    private const double BodyR = 0.75 * Cell;
    private const string MotorPartId = "basic-motor";

    /// <summary>対称・退化した値を避けた配置（行0・列0や行と列が等しい位置を使わぬ）。</summary>
    private static readonly GridPos Where = new(3, 4);

    [Fact]
    public void OrientV_TerminalsAlignHorizontallyOnColumnBoundaries()
    {
        var terminals = TerminalCircles(RenderMotor("V"));

        // 端子が列境界 0/1/2・行中心 y=0 に並ぶ＝ElementCatalog.Ports(Motor) の U/V/W と重なる形。
        Assert.Equal(3, terminals.Count);
        Assert.Contains(terminals, c => Near(c.X, 0) && Near(c.Y, 0));
        Assert.Contains(terminals, c => Near(c.X, Cell) && Near(c.Y, 0));
        Assert.Contains(terminals, c => Near(c.X, 2 * Cell) && Near(c.Y, 0));
    }

    [Fact]
    public void OrientV_BodyCircleSitsBelowTerminals()
    {
        var body = BodyCircle(RenderMotor("V"));

        // 大円は端子列の中央（x=1セル）の下（y=+1.75セル。y は下向き正）。
        // 上下は殿裁定2026-08-06＝「回転方向が逆。大円が下」——主回路の流れが上から下ゆえ、
        // モータは外部配線を上から受けて本体が下に来る。
        Assert.True(Near(body.X, Cell), $"大円の x が食い違う: {body.X}");
        Assert.True(Near(body.Y, 1.75 * Cell), $"大円の y が食い違う: {body.Y}");
    }

    [Fact]
    public void OrientUnset_KeepsLegacyVerticalTerminals()
    {
        AssertLegacyHorizontalMotor(RenderMotor(null));
    }

    /// <summary><c>"H"</c> は主回路3極記号の値であってモータには効かぬ。
    /// 綴りを取り違えても従来の絵へ倒れることを固定する。</summary>
    [Fact]
    public void OrientH_KeepsLegacyVerticalTerminals()
    {
        AssertLegacyHorizontalMotor(RenderMotor("H"));
    }

    /// <summary><b>後方互換の要。</b><c>PartId</c> 経由の既存モータ（<c>Kind</c> は配置経路の既定値
    /// <see cref="ElementKind.ContactNO"/> のまま固定される＝T-046由来の構造的制約）は
    /// <c>PartDrawing</c> で描かれるゆえ、<c>Orient</c> を付けても絵が変わらぬ。</summary>
    [Fact]
    public void PartIdMotor_OrientV_DrawsIdenticallyToOrientUnset()
    {
        var withOrient = RenderMotor("V", MotorPartId, ElementKind.ContactNO);

        // 【同一性だけでは足りぬ】経路分離が崩れて SymbolGlyphs へ流れても、Kind=ContactNO の
        // グリフは Orient に反応せぬゆえ「Orient で変わらぬ」は真のまま通ってしまう
        // ——改変Bの壊す実測で、この一件だけGREENのまま残って露見した穴である。
        // ゆえに「描かれておるのがモータの意匠か」まで測る。
        AssertLegacyHorizontalMotor(withOrient);
        AssertSameGeometry(RenderMotor(null, MotorPartId, ElementKind.ContactNO), withOrient);
    }

    /// <summary>上と同じ経路分離を、<c>Kind</c> が <see cref="ElementKind.Motor"/> の側からも測る
    /// ——<c>PartId</c> が在れば <c>Kind</c> によらず <c>PartDrawing</c> が勝つ。
    /// <para>
    /// <b>【射程＝本件は同一性しか測っておらぬ】</b>意匠そのものが壊れても、比べる両者が揃って
    /// 壊れるゆえ鳴らぬ（<c>BasicPartTemplates</c> の座標を旧値へ戻す改変Eで、本件だけGREENのまま残ると実測）。
    /// <b>意匠の検出は上の <see cref="PartIdMotor_OrientV_DrawsIdenticallyToOrientUnset"/> が担う</b>
    /// ——あちらは <see cref="AssertLegacyHorizontalMotor"/> を併せて呼んでおる。<b>役割を分けてあり、
    /// 本件に同じ判定を足すのは冗長</b>ゆえ足しておらぬ。
    /// </para></summary>
    [Fact]
    public void PartIdMotor_WithKindMotor_OrientV_DrawsIdenticallyToOrientUnset()
    {
        AssertSameGeometry(
            RenderMotor(null, MotorPartId, ElementKind.Motor),
            RenderMotor("V", MotorPartId, ElementKind.Motor));
    }

    // ===== ヘルパ =====

    private static RecordingRenderer RenderMotor(string? orient, string? partId = null,
                                                 ElementKind kind = ElementKind.Motor)
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(new ElementInstance
        {
            Kind = kind,
            Pos = Where,
            CellWidth = 3,    // 描画の実効幅（width = CellWidth * Cell）はここから決まる。
            CellHeight = 2,   // 描画には効かぬ（占有判定用）。両経路で揃えて差を作らぬ。
            PartId = partId,
            Params = orient is null ? new() : new() { [ParamKeys.Orient] = orient },
        });

        var library = new PartLibrary();
        foreach (var p in BasicPartTemplates.All()) library.ById[p.Id] = p;

        var recorder = new RecordingRenderer();
        new DiagramRenderer().Render(recorder, sheet, library);
        return recorder;
    }

    /// <summary>横向き（既定）＝端子が x=0.25セルの縦一列（y=-1/0/+1セル）、大円は右（x=2.0セル）。
    /// <para>
    /// <b>円のみを見て線を見ておらぬのは意図である</b>——PartId 経路と Kind 経路では入線3本の有無が
    /// 食い違う（殿裁定2026-08-06＝案C）。<b>円の位置は両経路で同一</b>ゆえ、本判定は両方に使える。
    /// </para></summary>
    private static void AssertLegacyHorizontalMotor(RecordingRenderer recorder)
    {
        var terminals = TerminalCircles(recorder);
        Assert.Equal(3, terminals.Count);
        Assert.Contains(terminals, c => Near(c.X, 0.25 * Cell) && Near(c.Y, -Cell));
        Assert.Contains(terminals, c => Near(c.X, 0.25 * Cell) && Near(c.Y, 0));
        Assert.Contains(terminals, c => Near(c.X, 0.25 * Cell) && Near(c.Y, Cell));

        var body = BodyCircle(recorder);
        Assert.True(Near(body.X, 2.0 * Cell) && Near(body.Y, 0), $"大円が食い違う: {body}");
    }

    private static List<Point2D> TerminalCircles(RecordingRenderer recorder) =>
        recorder.Circles.Where(c => Near(c.Radius, TerminalR)).Select(c => c.Center).ToList();

    private static Point2D BodyCircle(RecordingRenderer recorder) =>
        recorder.Circles.Single(c => Near(c.Radius, BodyR)).Center;

    /// <summary>2つの描画結果が図形の並び・座標まで同一であることを確かめる。</summary>
    private static void AssertSameGeometry(RecordingRenderer expected, RecordingRenderer actual)
    {
        Assert.Equal(expected.Circles.Count, actual.Circles.Count);
        Assert.Equal(expected.Lines.Count, actual.Lines.Count);

        for (int i = 0; i < expected.Circles.Count; i++)
        {
            var (e, a) = (expected.Circles[i], actual.Circles[i]);
            Assert.True(Near(e.Center.X, a.Center.X) && Near(e.Center.Y, a.Center.Y) && Near(e.Radius, a.Radius),
                $"円 {i} が食い違う: 期待 {e.Center}/r={e.Radius} 実際 {a.Center}/r={a.Radius}");
        }
        for (int i = 0; i < expected.Lines.Count; i++)
        {
            var (e, a) = (expected.Lines[i], actual.Lines[i]);
            Assert.True(Near(e.A.X, a.A.X) && Near(e.A.Y, a.A.Y) && Near(e.B.X, a.B.X) && Near(e.B.Y, a.B.Y),
                $"線 {i} が食い違う: 期待 {e.A}-{e.B} 実際 {a.A}-{a.B}");
        }
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.01;
}
