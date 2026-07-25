using Ecad2.App.Views;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-068増分3-c 残欠陥修正: 基準枠(WidthCells/HeightCells)変更時に既存の接続点(Ports)が
/// 新しい範囲へ再クランプされることの回帰テスト。忍者が実機確認中に発見した
/// 「heightCells=1のパーツにRowOffset=-2が保存されていた」事象の再現・修正確認。
/// PartEditorCanvasはFrameworkElement派生でありコンストラクタがInputManager初期化を要求する
/// ため、xUnit既定のMTAスレッドでは`STAである必要があります`で落ちる。専用STAスレッド上で
/// 実行することでSTAThread必須の制約を満たしつつ単体テストする。
/// </summary>
public class PartEditorCanvasPortReclampTests
{
    private static void RunOnSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured is not null) throw captured;
    }

    [Fact]
    public void HeightCells_Shrink_ReclampsRowOffsetOutsideNewRange() => RunOnSta(() =>
    {
        var canvas = new PartEditorCanvas { WidthCells = 3, HeightCells = 3 };
        canvas.LoadContent(Enumerable.Empty<PartPrimitive>(), new[] { new PortDef("P1", -2, 1) });

        canvas.HeightCells = 1;

        Assert.Equal(0, canvas.Ports[0].RowOffset);
    });

    [Fact]
    public void WidthCells_Shrink_ReclampsBoundaryOffsetOutsideNewRange() => RunOnSta(() =>
    {
        var canvas = new PartEditorCanvas { WidthCells = 5, HeightCells = 1 };
        canvas.LoadContent(Enumerable.Empty<PartPrimitive>(), new[] { new PortDef("P1", 0, 4) });

        canvas.WidthCells = 2;

        Assert.Equal(2, canvas.Ports[0].BoundaryOffset);
    });

    [Fact]
    public void HeightCells_Shrink_LeavesInRangePortUntouched() => RunOnSta(() =>
    {
        var canvas = new PartEditorCanvas { WidthCells = 3, HeightCells = 3 };
        canvas.LoadContent(Enumerable.Empty<PartPrimitive>(), new[] { new PortDef("P1", 1, 2) });

        canvas.HeightCells = 2;

        Assert.Equal(1, canvas.Ports[0].RowOffset);
        Assert.Equal(2, canvas.Ports[0].BoundaryOffset);
    });

    /// <summary>Undo/Redo整合性の核心を検証する冪等性テスト。ApplySnapshotで_portsを直接復元した
    /// 直後、RestoreExternalStateがWidthCells/HeightCellsのsetterを再発火させる経路があるが
    /// （ApplySnapshot 594-601行目→Dialog側SizeBox_TextChanged）、既に目的の寸法へクランプ済みの
    /// _portsに同じ寸法を再度セットしてもReclampPortsはno-opであるべき（さもなくばUndo直後に
    /// ポート位置が意図せずズレる）。同じ寸法へ「一旦別値を経由してから戻す」ことで、setter内の
    /// 早期return（値が同一なら何もしない）をすり抜けさせ、ReclampPortsが実際に複数回通っても
    /// 最終位置が保たれることを確認する。</summary>
    [Fact]
    public void ReclampPorts_ReappliedWithSameDimensions_IsIdempotent() => RunOnSta(() =>
    {
        var canvas = new PartEditorCanvas { WidthCells = 3, HeightCells = 1 };
        canvas.LoadContent(Enumerable.Empty<PartPrimitive>(), new[] { new PortDef("P1", 0, 2) });

        canvas.WidthCells = 5;   // 別値を経由(この時点でクランプは働かない=範囲内)
        canvas.WidthCells = 3;   // 元の寸法へ戻す(ApplySnapshot直後の再セットを模擬)

        Assert.Equal(2, canvas.Ports[0].BoundaryOffset);
    });
}
