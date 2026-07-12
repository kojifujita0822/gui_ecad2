using System.Runtime.ExceptionServices;
using System.Windows;
using Ecad2.App.Views;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>T-080往復2周目(a)修正: LadderCanvas.HitTestRungCommentRowの回帰テスト。隠密テスト設計
/// (docs/ecad2-t080-doubleclick-root-cause-onmitsu.md 観点1)のヒット領域内外・シート種別
/// (制御回路/主回路)軸を検証する。ClickCount軸はRungCommentDoubleClickTestsで別途検証する。
/// LadderCanvasはFrameworkElement派生でありコンストラクタがInputManager経由でSTAスレッドを要求する
/// (xUnitの既定実行スレッドはMTAのため、そのままでは"呼び出しスレッドはSTAである必要があります"で
/// 失敗する)ため、RunOnStaで専用STAスレッド上に実行する。
/// </summary>
public class RungCommentHitTestTests
{
    // WPF標準の96DPI換算(LadderCanvas内部のprivate MmToDipと同値、往復1周目T-041増分7等でも
    // 前提とされている一般的なDIP<->mm変換率のため、ここで独自定義しても実装詳細への依存にはならない)。
    private const double MmToDip = 96.0 / 25.4;

    private static Sheet CreateSheet(bool mainCircuit = false)
        => new() { Grid = new GridSpec { Rows = 22, Columns = 40 }, MainCircuit = mainCircuit };

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
    public void HitTestRungCommentRow_ヒット領域内_行番号を返す() => RunOnSta(() =>
    {
        var canvas = new LadderCanvas();
        var sheet = CreateSheet();
        double rightBusXMm = canvas.RightBusXMm(sheet.Grid.Columns);
        var (_, yMm) = canvas.CellToMm(new GridPos(Row: 3, Column: 0));
        var position = new Point((rightBusXMm + 1.0) * MmToDip, yMm * MmToDip);

        int? row = canvas.HitTestRungCommentRow(position, sheet);

        Assert.Equal(3, row);
    });

    [Fact]
    public void HitTestRungCommentRow_ヒット領域外_右母線以内はnullを返す() => RunOnSta(() =>
    {
        var canvas = new LadderCanvas();
        var sheet = CreateSheet();
        double rightBusXMm = canvas.RightBusXMm(sheet.Grid.Columns);
        var (_, yMm) = canvas.CellToMm(new GridPos(Row: 3, Column: 0));
        var position = new Point((rightBusXMm - 1.0) * MmToDip, yMm * MmToDip);

        int? row = canvas.HitTestRungCommentRow(position, sheet);

        Assert.Null(row);
    });

    [Fact]
    public void HitTestRungCommentRow_主回路シート_ヒット領域内でもnullを返す() => RunOnSta(() =>
    {
        var canvas = new LadderCanvas();
        var sheet = CreateSheet(mainCircuit: true);
        double rightBusXMm = canvas.RightBusXMm(sheet.Grid.Columns);
        var (_, yMm) = canvas.CellToMm(new GridPos(Row: 3, Column: 0));
        var position = new Point((rightBusXMm + 1.0) * MmToDip, yMm * MmToDip);

        int? row = canvas.HitTestRungCommentRow(position, sheet);

        Assert.Null(row);
    });

    [Fact]
    public void HitTestRungCommentRow_境界値_行範囲外はnullを返す() => RunOnSta(() =>
    {
        var canvas = new LadderCanvas();
        var sheet = CreateSheet();
        double rightBusXMm = canvas.RightBusXMm(sheet.Grid.Columns);
        var position = new Point((rightBusXMm + 1.0) * MmToDip, -1000.0 * MmToDip);

        int? row = canvas.HitTestRungCommentRow(position, sheet);

        Assert.Null(row);
    });
}
