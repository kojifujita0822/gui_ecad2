using System.Windows;

namespace Ecad2.App.Tests;

/// <summary>T-080追加往復 課題3の修正: MainWindow.ClampToViewportの回帰テスト。隠密テスト設計
/// (docs/ecad2-t080-issue2-3-root-cause-onmitsu.md 課題3テスト設計)の「部分的に単体テスト化できる
/// 範囲」に基づき、クランプ計算式自体(境界値: 範囲内/左右上下それぞれの範囲外/バーが
/// ビューポートより大きい場合)を検証する。ActualWidth・TranslatePoint等のレイアウト依存値の
/// 取得そのものは単体テスト化できないため対象外(実機確認は忍者マター、5観点を家老経由で依頼)。
/// </summary>
public class RungCommentEditorPositionClampTests
{
    private static readonly Size BarSize = new(120, 40);
    private static readonly Point ViewportOrigin = new(50, 30);
    private const double ViewportWidth = 800;
    private const double ViewportHeight = 600;

    [Fact]
    public void ClampToViewport_範囲内ならそのまま()
    {
        var topLeft = new Point(200, 200);

        Point result = MainWindow.ClampToViewport(topLeft, ViewportOrigin, ViewportWidth, ViewportHeight, BarSize);

        Assert.Equal(topLeft, result);
    }

    [Fact]
    public void ClampToViewport_右端超過はバー幅を引いたビューポート右端へ収める()
    {
        // スクロール範囲外の対象を指した場合の巨大値を想定(課題3の忍者実測相当)。
        var topLeft = new Point(5000, 200);

        Point result = MainWindow.ClampToViewport(topLeft, ViewportOrigin, ViewportWidth, ViewportHeight, BarSize);

        Assert.Equal(ViewportOrigin.X + ViewportWidth - BarSize.Width, result.X);
        Assert.Equal(200, result.Y);
    }

    [Fact]
    public void ClampToViewport_下端超過はバー高さを引いたビューポート下端へ収める()
    {
        var topLeft = new Point(200, 5000);

        Point result = MainWindow.ClampToViewport(topLeft, ViewportOrigin, ViewportWidth, ViewportHeight, BarSize);

        Assert.Equal(200, result.X);
        Assert.Equal(ViewportOrigin.Y + ViewportHeight - BarSize.Height, result.Y);
    }

    [Fact]
    public void ClampToViewport_左端未満はビューポート原点Xへ収める()
    {
        var topLeft = new Point(-100, 200);

        Point result = MainWindow.ClampToViewport(topLeft, ViewportOrigin, ViewportWidth, ViewportHeight, BarSize);

        Assert.Equal(ViewportOrigin.X, result.X);
    }

    [Fact]
    public void ClampToViewport_上端未満はビューポート原点Yへ収める()
    {
        var topLeft = new Point(200, -100);

        Point result = MainWindow.ClampToViewport(topLeft, ViewportOrigin, ViewportWidth, ViewportHeight, BarSize);

        Assert.Equal(ViewportOrigin.Y, result.Y);
    }

    [Fact]
    public void ClampToViewport_バーがビューポートより大きい場合は原点を優先する()
    {
        // Math.Maxの安全弁(maxX/maxYがviewportOriginを下回らない)の境界値。
        var hugeBar = new Size(2000, 2000);
        var topLeft = new Point(5000, 5000);

        Point result = MainWindow.ClampToViewport(topLeft, ViewportOrigin, ViewportWidth, ViewportHeight, hugeBar);

        Assert.Equal(ViewportOrigin.X, result.X);
        Assert.Equal(ViewportOrigin.Y, result.Y);
    }
}
