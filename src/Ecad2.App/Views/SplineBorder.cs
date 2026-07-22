using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ecad2.App.Views;

/// <summary>
/// T-119(殿直接指示2026-07-22): タブ左20px幅の曲線縁を描画するコントロール。
/// 【往復2周目修正(殿実機確認2026-07-22、2点指摘)】
/// (1) 配置ツールバーは`TabStripPlacement="Bottom"`（既存仕様）のため、コンテンツ（ツールバー
///     本体）への接続部はタブの**上辺**側にある。旧実装はAvalonDock.Themes.Aero原本
///     （`TabStripPlacement="Top"`前提）と同じ向きのまま描画しており、曲線が下端で垂れ下がる
///     不自然な形になっていた（殿所見）。
/// (2) ライセンス懸念対応（隠密指摘・殿裁定）：旧実装（2本のQuadraticBezierSegment、制御点比率
///     2/3・1/2・1/3）はAvalonDock.Themes.Aero（Ms-PL）のSplineBorder
///     （source/Components/AvalonDock.Themes.Aero/Controls/SplineBorder.cs、
///     docs-notes/vendor-reference/avalondock-v4.74.1配下）とアルゴリズムがほぼ完全一致する
///     「移植」だったため、視覚効果（幅20px・緩やかな曲線）は維持しつつ、3次ベジェ曲線1本による
///     独自の数式で再実装した（一次ソースの座標計算式は参照・使用していない）。
/// 【往復3周目修正(殿実機確認2026-07-22、家老原因特定)】上記(1)の反転は制御点の"形"のみ変更し
/// 始点/終点(w,0)→(0,h)自体を変えていなかったため実質無反転だった。正しくはY軸を反転し、
/// 始点(w,h)〔右下、タブ外周の頭〕→終点(0,0)〔左上、コンテンツ接続部の左端〕とする。これにより
/// 上辺(y=0)がほぼ水平でコンテンツ接続部として機能し、下辺(y=h)側が曲線でタブ外周の丸みを描く。
/// </summary>
public class SplineBorder : Control
{
    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(nameof(Thickness), typeof(double), typeof(SplineBorder),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(SplineBorder),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(SplineBorder),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>下辺(タブ外周の頭)側の右寄り制御点をわずかに上げる補正量。選択中タブが隣接タブへ
    /// わずかに食い込む視覚効果に使う想定(往復3周目で反転基準を下辺基準へ修正)。</summary>
    public static readonly DependencyProperty BottomBorderMarginProperty =
        DependencyProperty.Register(nameof(BottomBorderMargin), typeof(double), typeof(SplineBorder),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double BottomBorderMargin
    {
        get => (double)GetValue(BottomBorderMarginProperty);
        set => SetValue(BottomBorderMarginProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        double w = ActualWidth;
        double h = ActualHeight;

        // 塗り: 右下(w,h、タブ外周の頭の右端)を始点に、下辺側をほぼ水平に保ちつつ左上へ緩やかに
        // 膨らむ3次ベジェ曲線1本で左端(0,0、コンテンツ接続部の左端)へ結ぶ。左端(0,0)→右上(w,0)は
        // 上辺の直線(コンテンツ接続部)、右上(w,0)→始点(w,h)はPathFigure(IsClosed=true)による
        // 自動閉合線(右辺)。
        var fillGeometry = new PathGeometry();
        var fillFigure = new PathFigure { IsFilled = true, IsClosed = true, StartPoint = new Point(w, h - BottomBorderMargin) };
        fillFigure.Segments.Add(new BezierSegment(
            new Point(w * 0.55, h), new Point(0, h * 0.6), new Point(0, 0), isStroked: false));
        fillFigure.Segments.Add(new LineSegment(new Point(w, 0), isStroked: false));
        fillGeometry.Figures.Add(fillFigure);
        drawingContext.DrawGeometry(Fill, null, fillGeometry);

        // 縁線: 曲線部分のみ描く(上辺・下辺・右辺の直線は別途Border要素側で描画される設計)。
        // 線の太さ分、始点・終点をThickness/2だけ内側へ寄せる。
        var borderGeometry = new PathGeometry();
        var borderFigure = new PathFigure { IsFilled = false, IsClosed = false, StartPoint = new Point(w - Thickness / 2, h) };
        borderFigure.Segments.Add(new BezierSegment(
            new Point(w * 0.55, h), new Point(Thickness / 2, h * 0.6), new Point(Thickness / 2, 0), isStroked: true));
        borderGeometry.Figures.Add(borderFigure);
        drawingContext.DrawGeometry(null, new Pen(Stroke, Thickness), borderGeometry);

        base.OnRender(drawingContext);
    }
}
