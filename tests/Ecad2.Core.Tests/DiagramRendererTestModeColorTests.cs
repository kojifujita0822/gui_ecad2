using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-061第二歩: DrawElementの通電/非通電色分け(殿裁定「LDmicro式」通電=赤/非通電=グレー)の回帰テスト。
/// DrawLine/DrawCircle等に渡されるStrokeStyle.Colorを記録し、sim引数の有無・励磁状態による
/// 色の出し分けを検証する。母線間に単独要素を配置する最小回路(NetlistBuilderOrChainTests同型の
/// 手法)で、コイル(Y001)は常時励磁・ガード無し接点(X001)は常時非励磁という単純な構図にする。
/// </summary>
public class DiagramRendererTestModeColorTests
{
    private static Sheet MakeSingleElementSheet(ElementKind kind, string deviceName)
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(new ElementInstance { Kind = kind, Pos = new GridPos(0, 5), DeviceName = deviceName });
        return sheet;
    }

    [Fact]
    public void Render_DrawingMode_DoesNotUseTestModeColors()
    {
        var sheet = MakeSingleElementSheet(ElementKind.ContactNO, "X001");
        var renderer = new ColorRecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet);   // sim=null(作画モード)

        Assert.DoesNotContain(DrawingTheme.Powered, renderer.LineColors);
        Assert.DoesNotContain(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeEnergizedCoil_UsesPoweredColor()
    {
        var sheet = MakeSingleElementSheet(ElementKind.Coil, "Y001");
        var renderer = new ColorRecordingRenderer();

        // 単独要素は自動的に左右母線間へ直結される(DrawRungWires前提と同型)ため、コイルは常時励磁。
        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.Contains(DrawingTheme.Powered, renderer.LineColors);
        Assert.DoesNotContain(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeUnenergizedContact_UsesNonEnergizedGray()
    {
        var sheet = MakeSingleElementSheet(ElementKind.ContactNO, "X001");
        var renderer = new ColorRecordingRenderer();

        // 手動強制(Inputs)未設定・対応コイルも無いため常時非導通。配線区間自体はいずれかの母線から
        // 到達可能なため通電色(Powered)になりうる(電気的に正しい仕様動作、PoweredNets=floodL∪floodR)。
        // 検証対象は接点記号自体がグレー(非通電)で描かれることのみ。
        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.Contains(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }
}

/// <summary>DrawLine/DrawCircle等に渡されるStrokeStyle.Colorのみを記録するIRendererのテストダブル
/// (DiagramRendererLabelTests.RecordingRendererのDrawText専用版と対になる色専用版)。</summary>
internal sealed class ColorRecordingRenderer : IRenderer
{
    public List<Color> LineColors { get; } = new();

    public void PushTransform(double translateX, double translateY, double scale = 1.0) { }
    public void PopTransform() { }
    public void PushClip(Rect2D rect) { }
    public void PopClip() { }
    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void DrawRectangle(Rect2D rect, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void FillRectangle(Rect2D rect, Color color) { }
    public void DrawCircle(Point2D center, double radius, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void FillCircle(Point2D center, double radius, Color color) { }
    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void DrawText(string text, Point2D position, TextStyle style) { }
    public Size2D MeasureText(string text, TextStyle style) => new(0, 0);
    public void DrawImage(string filePath, Rect2D bounds) { }
}
