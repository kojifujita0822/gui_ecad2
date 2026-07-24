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
/// 【往復4周目修正(殿実機確認2026-07-24、見本比較で家老が一次ソード再分析)】上辺(y=0)・下辺(y=h)の
/// **両端**で接続先の直線と同じ向き(水平接線)になるよう制御点を置くのが原本の設計原理と判明
/// （原本は始点側制御点が始点と同じY、終点側制御点も終点と同じYで、両端水平接線の対称S字）。
/// 往復3周目までの実装は終点側の制御点`(0, h*0.6)`が終点と**同じX**(垂直接線)になっており、
/// 曲線が下辺付近で一方向に大きく逸脱し(直線対角線からt=0.25地点で0.096w/0.178h相当の乖離)、
/// 「左肩のみ内側へえぐれる」非対称な形状の原因だった。終点側制御点も終点と同じY(水平接線)へ
/// 修正し、幾何学的中心を通る対称なS字曲線(制御点比率0.6w/0.4w、原本の2/3・1/3とは異なる独自比率)
/// へ改めた——見本(山型・台形状に左右対称な傾斜/湾曲)に近い、両辺に滑らかに接続する緩やかな
/// 丸みとなる設計。
/// 【往復6周目修正(殿裁定・家老正式依頼2026-07-24)】対称構造(両端水平接線)は維持のまま、制御点の
/// 始点/終点からの距離(比率0.6w/0.4w→0.3w/0.7w)を広げ、曲率を強化した。両端の接線方向は不変
/// (幾何学的中心を通る点も不変)だが、制御点が対応する端点からより遠くへ離れることで中間部の
/// 局所的な曲がりが顕著になり、「一目で丸みとわかる」見た目になる(数値検証: 直線対角線からの
/// 逸脱量がt=0.25地点で旧比率-0.019w/+0.094hから新比率-0.103w/+0.094hへ、X方向の逸脱が約5.5倍)。
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

        // 塗り: 右下(w,h、タブ外周の頭の右端)を始点に、左上(0,0、コンテンツ接続部の左端)を終点とする
        // 3次ベジェ曲線1本で結ぶ。両端とも接続先の直線と同じ向き(制御点1が始点と同じY=水平接線、
        // 制御点2が終点と同じY=水平接線)とし、下辺(タブ外周)・上辺(コンテンツ接続部)双方へ滑らかに
        // 繋がりつつ中間だけ緩やかに膨らむ対称なS字にする。左端(0,0)→右上(w,0)は上辺の直線
        // (コンテンツ接続部)、右上(w,0)→始点(w,h)はPathFigure(IsClosed=true)による自動閉合線(右辺)。
        var fillGeometry = new PathGeometry();
        var fillFigure = new PathFigure { IsFilled = true, IsClosed = true, StartPoint = new Point(w, h - BottomBorderMargin) };
        fillFigure.Segments.Add(new BezierSegment(
            new Point(w * 0.3, h), new Point(w * 0.7, 0), new Point(0, 0), isStroked: false));
        fillFigure.Segments.Add(new LineSegment(new Point(w, 0), isStroked: false));
        fillGeometry.Figures.Add(fillFigure);
        drawingContext.DrawGeometry(Fill, null, fillGeometry);

        // 縁線: 曲線部分のみ描く(上辺・下辺・右辺の直線は別途Border要素側で描画される設計)。
        // 塗りと同じ両端水平接線の制御点比率を用い、線の太さ分だけ始点(下辺側、Y方向)・
        // 終点(上辺側、Y方向)・始点(右辺側、X方向)を内側へ寄せる。
        var borderGeometry = new PathGeometry();
        var borderFigure = new PathFigure { IsFilled = false, IsClosed = false, StartPoint = new Point(w - Thickness / 2, h) };
        borderFigure.Segments.Add(new BezierSegment(
            new Point(w * 0.3, h), new Point(w * 0.7, Thickness / 2), new Point(Thickness / 2, Thickness / 2), isStroked: true));
        borderGeometry.Figures.Add(borderFigure);
        drawingContext.DrawGeometry(null, new Pen(Stroke, Thickness), borderGeometry);

        base.OnRender(drawingContext);
    }
}
