using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-044修正(2026-07-19、殿裁定=自動結線方針への転換)の回帰テスト。
/// 旧実装の<c>DiagramRenderer.RightTerminator</c>はBottomRow側の縦コネクタのみを終端判定の対象に
/// しており、row自身がTopRow(起点)となる下流分岐を無視していた。row行に「BottomRow側の手前の
/// 縦コネクタ」と「TopRow側のより遠い下流分岐」の両方が存在すると、旧実装は手前で早期終端し、
/// 本来延ばすべき下流分岐まで横線が届かなかった(殿実機報告「線番2まで自動で結線して欲しい」)。
/// 「A-B縦積み+B-C横ずれ連鎖」構図(隠密設計・家老承認)で検証する。
/// </summary>
public class DiagramRendererRightTerminatorTests
{
    [Fact]
    public void Render_DownstreamBranchAtFartherColumn_ExtendsWireToDownstreamBranch()
    {
        // A(行0)-B(行1)は同一列(3)で縦積み。B(行1)-C(行2)は列がずれた横ずれ連鎖(列3→列6)。
        // B行の右側配線は、旧実装だとA-B間の縦コネクタ(BottomRow側、列3=Bの右端そのもの)で
        // 早期終端し(rt==rb、横線ゼロ幅で描画されない)、本来到達すべきB-C間の下流分岐(列6)まで
        // 届かない。新実装はrowをTopRowとする下流分岐(列6)を優先し、そこまで延ばす。
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(0, 2), DeviceName = "A" });
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 2), DeviceName = "B" });
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(2, 5), DeviceName = "C" });
        sheet.Connectors.Add(new VerticalConnector { Column = 3, TopRow = 0, BottomRow = 1 });   // A-B縦積み
        sheet.Connectors.Add(new VerticalConnector { Column = 6, TopRow = 1, BottomRow = 2 });   // B-C横ずれ連鎖

        var renderer = new LineRecordingRenderer();
        var diagramRenderer = new DiagramRenderer();
        diagramRenderer.Render(renderer, sheet);

        double y = diagramRenderer.Geometry.YRow(1);
        double xFrom = diagramRenderer.Geometry.X(3);   // Bの右端(rb)
        double xTo = diagramRenderer.Geometry.X(6);     // B-C間の下流分岐(到達すべき列)

        Assert.Contains(renderer.Lines, l =>
            Approximately(l.A.Y, y) && Approximately(l.B.Y, y) &&
            Approximately(Math.Min(l.A.X, l.B.X), xFrom) && Approximately(Math.Max(l.A.X, l.B.X), xTo));
    }

    private static bool Approximately(double a, double b) => Math.Abs(a - b) < 0.01;
}

/// <summary>DrawLineの始終点(Point2D)を記録するIRendererのテストダブル。</summary>
internal sealed class LineRecordingRenderer : IRenderer
{
    public List<(Point2D A, Point2D B)> Lines { get; } = new();

    public void PushTransform(double translateX, double translateY, double scale = 1.0) { }
    public void PopTransform() { }
    public void PushClip(Rect2D rect) { }
    public void PopClip() { }
    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) => Lines.Add((a, b));
    public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke) { }
    public void DrawRectangle(Rect2D rect, StrokeStyle stroke) { }
    public void FillRectangle(Rect2D rect, Color color) { }
    public void DrawCircle(Point2D center, double radius, StrokeStyle stroke) { }
    public void FillCircle(Point2D center, double radius, Color color) { }
    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke) { }
    public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke) { }
    public void DrawText(string text, Point2D position, TextStyle style) { }
    public Size2D MeasureText(string text, TextStyle style) => new(0, 0);
    public void DrawImage(string filePath, Rect2D bounds) { }
}
