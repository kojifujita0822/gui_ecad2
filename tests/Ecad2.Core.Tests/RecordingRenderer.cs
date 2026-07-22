using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>IRendererの呼び出しを記録するテストダブル(T-114 P-064対処: DiagramRendererLabelTests・
/// DiagramRendererPageTransformTestsで独立に重複定義されていた同名クラスを統合)。DrawText呼び出しの
/// 記録(DrawnTexts/DrawnTextEntries)と、PushTransform/PopTransform/DrawRectangleの呼び出し順序・
/// 引数の記録(Transforms/Ops/Rectangles)の両方を1つのテストダブルへ集約する。</summary>
internal sealed class RecordingRenderer : IRenderer
{
    public List<string> DrawnTexts { get; } = new();
    public List<(string Text, Point2D Position, TextStyle Style)> DrawnTextEntries { get; } = new();
    public List<(double Tx, double Ty, double Scale)> Transforms { get; } = new();
    public List<string> Ops { get; } = new();
    public List<Rect2D> Rectangles { get; } = new();

    public void PushTransform(double translateX, double translateY, double scale = 1.0)
    {
        Transforms.Add((translateX, translateY, scale));
        Ops.Add("Push");
    }
    public void PopTransform() => Ops.Add("Pop");
    public void PushClip(Rect2D rect) { }
    public void PopClip() { }
    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) { }
    public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke) { }
    public void DrawRectangle(Rect2D rect, StrokeStyle stroke)
    {
        Rectangles.Add(rect);
        Ops.Add($"Rect:{Rectangles.Count - 1}");
    }
    public void FillRectangle(Rect2D rect, Color color) { }
    public void DrawCircle(Point2D center, double radius, StrokeStyle stroke) { }
    public void FillCircle(Point2D center, double radius, Color color) { }
    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke) { }
    public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke) { }
    public void DrawText(string text, Point2D position, TextStyle style)
    {
        DrawnTexts.Add(text);
        DrawnTextEntries.Add((text, position, style));
    }
    public Size2D MeasureText(string text, TextStyle style) => new(text.Length * style.FontSizeMm, style.FontSizeMm);
    public void DrawImage(string filePath, Rect2D bounds) { }
}
