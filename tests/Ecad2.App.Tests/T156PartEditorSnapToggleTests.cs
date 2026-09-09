using System.Reflection;
using Ecad2.App.Views;
using Ecad2.Rendering;

namespace Ecad2.App.Tests;

/// <summary>
/// T-156(殿ご下命2026-09-09): 作図スナップのトグル。既定オフ＝自由（連続座標）。ONで1辺4等分へ吸着。
/// 接続点は本トグルと無関係に <c>ClampPort</c> で常に整数へ丸められる（別経路ゆえ本テストの対象外）。
/// <para>
/// <c>SnapCell</c> は private ゆえリフレクションで直に測る（<c>ManualWiringFocusContinuationTests</c> 等と同じ手）。
/// ツールバーの「スナップ」トグル→<c>SnapEnabled</c> の結線はコードビハインドゆえ忍者の実機が網
/// （<c>PartEditorCanvasNotifyTests</c> 末尾の断りと同じ事情）。
/// </para>
/// </summary>
public class T156PartEditorSnapToggleTests
{
    private static Point2D InvokeSnapCell(PartEditorCanvas canvas, double x, double y)
    {
        var m = typeof(PartEditorCanvas).GetMethod("SnapCell", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Point2D)m.Invoke(canvas, new object[] { new Point2D(x, y) })!;
    }

    [Fact]
    public void SnapEnabledの既定は偽()
        => StaTestRunner.Run(() => Assert.False(new PartEditorCanvas().SnapEnabled));

    [Fact]
    public void 既定_自由_では座標を丸めない()
        => StaTestRunner.Run(() =>
        {
            var canvas = new PartEditorCanvas();   // SnapEnabled=false
            var p = InvokeSnapCell(canvas, 0.31415, -0.7182);
            Assert.Equal(0.31415, p.X, 6);
            Assert.Equal(-0.7182, p.Y, 6);
        });

    [Fact]
    public void スナップON_では1辺4等分へ丸める()
        => StaTestRunner.Run(() =>
        {
            var canvas = new PartEditorCanvas { SnapEnabled = true };
            var p = InvokeSnapCell(canvas, 0.31415, -0.7182);
            Assert.Equal(0.25, p.X, 6);    // 0.31415 → 1/4 格子
            Assert.Equal(-0.75, p.Y, 6);
        });
}
