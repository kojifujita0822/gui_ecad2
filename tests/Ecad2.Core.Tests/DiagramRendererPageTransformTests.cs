using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>T-080往復1周目指摘Bの回帰テスト: 縮小フィット(pageScale&lt;1.0)の変換は図面枠左上
/// (BorderMarginMm)を固定点とする translate=BorderMarginMm*(1-scale) 付きで適用されること
/// (修正前は原点(0,0)基準の一様縮小で、左上余白まで比例して縮み内容が用紙左上へ偏っていた)。
/// 併せて外枠(DrawBorder)がPopTransform後=スケール対象外の絶対座標で描画されること(構造検証、
/// 隠密レビュー経過観察Mのカバレッジ推奨分)。</summary>
public class DiagramRendererPageTransformTests
{
    /// <summary>IRendererの呼び出し記録スタブ(描画はしない)。PushTransformの引数と、
    /// PopTransform・外枠矩形の呼び出し順序の構造検証に使う。</summary>
    private sealed class RecordingRenderer : IRenderer
    {
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
        public void DrawText(string text, Point2D position, TextStyle style) { }
        public Size2D MeasureText(string text, TextStyle style) => new(text.Length * style.FontSizeMm, style.FontSizeMm);
        public void DrawImage(string filePath, Rect2D bounds) { }
    }

    private static Sheet CreateSheet() => new() { Grid = new GridSpec { Rows = 10, Columns = 20 } };

    [Fact]
    public void Render_縮小時の変換は図面枠左上を固定点とするtranslate付き()
    {
        var dr = new DiagramRenderer();
        var r = new RecordingRenderer();

        dr.Render(r, CreateSheet(), enableBorder: true, pageScale: 0.8);

        // translate = 図面枠余白5mm * (1 - 0.8) = 1.0mm。仕様値のピン留めのためリテラルで書く。
        var t = Assert.Single(r.Transforms);
        Assert.Equal(1.0, t.Tx, 12);
        Assert.Equal(1.0, t.Ty, 12);
        Assert.Equal(0.8, t.Scale);
    }

    [Fact]
    public void Render_等倍時は変換を積まない()
    {
        var dr = new DiagramRenderer();
        var r = new RecordingRenderer();

        dr.Render(r, CreateSheet(), enableBorder: true, pageScale: 1.0);

        Assert.Empty(r.Transforms);
    }

    [Fact]
    public void Render_外枠はPopTransform後に絶対座標で描画される()
    {
        var dr = new DiagramRenderer();
        var r = new RecordingRenderer();

        dr.Render(r, CreateSheet(), enableBorder: true, pageScale: 0.8);

        // 外枠=余白5mm起点・A4縦(210x297)から余白5mmx2を引いた200x287の矩形(仕様値リテラル)。
        int borderRectIndex = r.Rectangles.FindIndex(rect =>
            rect.X == 5.0 && rect.Y == 5.0 && rect.Width == 200.0 && rect.Height == 287.0);
        Assert.True(borderRectIndex >= 0, "外枠の矩形が絶対座標(5,5,200,287)で描画されていない");

        // スケールのPop後(=変換の外)で描かれていること(表題欄・外枠はスケール対象外の構造検証)。
        int popIndex = r.Ops.IndexOf("Pop");
        int borderOpIndex = r.Ops.IndexOf($"Rect:{borderRectIndex}");
        Assert.True(popIndex >= 0);
        Assert.True(borderOpIndex > popIndex, "外枠がPopTransformより前(=スケール対象内)で描画されている");
    }
}
