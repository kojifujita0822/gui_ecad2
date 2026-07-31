using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>
/// <see cref="DrawingTheme"/> の線幅算出の単体テスト。
/// </summary>
public class DrawingThemeTests
{
    private const int Precision = 9;

    // ===== ズームに依らぬ線幅（T-137、殿裁定2026-07-31） =====
    // 発端＝忍者の実測。倍率0.20では区切り線が背景に沈み、在るべき13本のうち5本が
    // 区別できなかった（PushTransform が ScaleTransform を積むゆえペンの太さにも倍率が掛かる）。
    //
    // 【入力値の選び方】base と zoom に別の値を採り、両者を取り違える改変が結果に現れるようにする。
    // 倍率も 1 未満・1・1 超を混ぜる——1 だけでは割り算が恒等写像に潰れ、実装が誤っていても通る。

    [Fact]
    public void ZoomInvariantWidthMm_倍率1なら素の値のまま()
        => Assert.Equal(0.10, DrawingTheme.ZoomInvariantWidthMm(0.10, zoom: 1.0), Precision);

    [Theory]
    [InlineData(0.10, 0.2, 0.50)]    // 引いた状態＝太くしておく
    [InlineData(0.12, 0.25, 0.48)]   // base と zoom を非対称に
    [InlineData(0.08, 1.6, 0.05)]    // 寄った状態＝細くしておく
    public void ZoomInvariantWidthMm_倍率の逆数を掛けた値になる(double baseWidth, double zoom, double expected)
        => Assert.Equal(expected, DrawingTheme.ZoomInvariantWidthMm(baseWidth, zoom), Precision);

    [Theory]
    [InlineData(0.10, 0.2)]
    [InlineData(0.12, 0.25)]
    [InlineData(0.08, 1.6)]
    [InlineData(0.10, 2.0)]
    public void ZoomInvariantWidthMm_画面上の太さが元の値と一致する(double baseWidth, double zoom)
    {
        // 本メソッドの目的そのもの。呼び出し側が zoom を掛けるゆえ、掛け戻せば元へ返る。
        double screenWidth = DrawingTheme.ZoomInvariantWidthMm(baseWidth, zoom) * zoom;

        Assert.Equal(baseWidth, screenWidth, Precision);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void ZoomInvariantWidthMm_倍率が0以下なら素の値を返す(double zoom)
    {
        // 退化入力。Zoom は 0.2〜8.0 にクランプされる（PartEditorCanvas.Zoom）ゆえ実際には来ぬが、
        // 純粋関数としてゼロ除算・負の太さを出さぬことを固定する。
        Assert.Equal(0.10, DrawingTheme.ZoomInvariantWidthMm(0.10, zoom), Precision);
    }

    // --- 射程（docコメントの「zoom <= 2.0 までが一定」を数値で固定する） ---
    //
    // 【この2件が写しているもの】描画バックエンドの下限クランプ
    // （WPF版は WpfRenderer.Pen の Math.Max(s.Width, MinStrokeWidthMm)）を、テスト側で模している。
    // Ecad2.Rendering.Wpf は net10.0-windows ゆえ Core.Tests からは参照できぬための代替であり、
    // <b>バックエンド側のクランプの仕方が変われば、このテストの前提も追随が要る</b>。

    [Theory]
    [InlineData(0.2)]
    [InlineData(1.0)]
    [InlineData(2.0)]   // 境界ちょうど＝0.10/2.0 が MinStrokeWidthMm と等しくなる倍率
    public void ZoomInvariantWidthMm_倍率2までは画面上の太さが完全に一定(double zoom)
    {
        const double baseWidth = 0.10;
        double clamped = Math.Max(DrawingTheme.ZoomInvariantWidthMm(baseWidth, zoom), DrawingTheme.MinStrokeWidthMm);

        Assert.Equal(baseWidth, clamped * zoom, Precision);
    }

    [Theory]
    [InlineData(4.0)]
    [InlineData(8.0)]   // Zoom の上限
    public void ZoomInvariantWidthMm_倍率2超はクランプで太くなるが改修前より細い(double zoom)
    {
        const double baseWidth = 0.10;
        double after = Math.Max(DrawingTheme.ZoomInvariantWidthMm(baseWidth, zoom), DrawingTheme.MinStrokeWidthMm) * zoom;
        double before = Math.Max(baseWidth, DrawingTheme.MinStrokeWidthMm) * zoom;   // 改修前＝割らずに描く

        Assert.True(after > baseWidth, "倍率2超ではクランプが効き、画面上の太さは一定にならぬ");
        Assert.True(after < before, "それでも改修前より細い——後退にはならぬ");
    }
}
