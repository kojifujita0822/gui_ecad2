using System.Runtime.ExceptionServices;
using System.Windows;
using Ecad2.App.Views;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>T-116(P-107対処): 自由線の交点上に接続点を配置した場合、両方のヒットテストが同一座標で
/// 同時に候補を検出しうることの回帰テスト。MainWindow.xaml.cs側の優先順位判定(ConnectionDot先→
/// FreeLine後、GuiEcad原本踏襲)はコードビハインドのマウスイベントハンドラ内にあり、MainWindow自体の
/// インスタンス化(AvalonDock初期化等の重い副作用を伴う)を要するため単体テスト不可
/// (RED証明不可・View層依存、samurai.md該当節)。本テストは「両者が同一座標で同時にヒットしうる」
/// という優先順位判定が必要になる前提条件（今回のバグの根本原因）をLadderCanvas単体で保証し、
/// 実際の優先順位確認は忍者実機確認(DoD(2))に委ねる。</summary>
public class FreeLineConnectionDotHitTestOverlapTests
{
    private static Sheet CreateMainCircuitSheet()
        => new() { Grid = new GridSpec { Rows = 22, Columns = 40 }, MainCircuit = true };

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
    }

    [Fact]
    public void 自由線の交点上の接続点は両方のヒットテストで検出できる() => RunOnSta(() =>
    {
        var canvas = new LadderCanvas();
        var sheet = CreateMainCircuitSheet();
        var line = new FreeLine { X1Mm = 10, Y1Mm = 10, X2Mm = 30, Y2Mm = 10 };
        var dot = new ConnectionDot { XMm = 20, YMm = 10 };
        sheet.FreeLines.Add(line);
        sheet.ConnectionDots.Add(dot);

        var position = new Point(20 * (96.0 / 25.4), 10 * (96.0 / 25.4));

        Assert.Same(dot, canvas.HitTestConnectionDot(position, sheet));
        Assert.Same(line, canvas.HitTestFreeLine(position, sheet));
    });
}
