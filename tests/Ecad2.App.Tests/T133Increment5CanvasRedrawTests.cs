using System.Windows.Media;
using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分5往復1周目(忍者の実機再検証・家老采配): 種別ComboBoxで値を変えても、選択を外すまで
/// キャンバスの絵(ラベル・ELBのテストボタン)が更新されない欠陥のRED先行証明。
/// <para>
/// <b>【MainWindowをSTAで立てられた】</b>T-144・T-132・T-133増分4-Cに続く例
/// (<see cref="MenuPlacementToolTests"/>)。「View層ゆえ測れぬ」と即断せず、まず問うた。
/// </para>
/// <para>
/// <b>【観測点の選び方】</b><c>LadderCanvas.Draw()</c>はシート全体を1枚の<c>DrawingVisual</c>へ
/// まとめて描画し(<c>_children.Add(visual)</c>は全体で1箇所のみ)、呼ばれるたび新しい
/// <c>DrawingVisual</c>インスタンスを生成する(<c>LadderCanvas.cs:206</c>)。ゆえに
/// <c>VisualTreeHelper.GetChild(canvas, 0)</c>の参照が変わったか否かで「再描画されたか」を
/// 個々の描画プリミティブを数えずに判定できる——<c>Draw()</c>自体はprivateな<c>RedrawCanvas()</c>
/// からしか呼ばれず直接検めようがないため、この間接的な signal が測れる限界にござる。
/// </para>
/// </summary>
public class T133Increment5CanvasRedrawTests
{
    private const int Rows = 5;
    private const int Columns = 10;
    private static readonly GridPos PlacePos = new(2, 6);

    [Fact]
    public void SelectedElementBreakerType変更でキャンバスが再描画される()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;
            vm.NewDocument();
            vm.CurrentSheet!.Grid = new GridSpec { Rows = Rows, Columns = Columns };
            vm.CurrentSheet!.MainCircuit = true;
            vm.SelectedCell = PlacePos;
            vm.PlaceElementAtSelectedCell(ElementKind.Breaker3P, "V");
            // SelectedCellはRedrawCanvasの固定リストに載っておる(既存)ため、動かして戻すだけで
            // 確実に一度Draw()させ、以降の比較の基準(baseline)を確立する。
            vm.SelectedCell = new GridPos(0, 0);
            vm.SelectedCell = PlacePos;
            Assert.True(VisualTreeHelper.GetChildrenCount(window.LadderCanvasHost) > 0);   // 前提: 初回描画済み
            var before = VisualTreeHelper.GetChild(window.LadderCanvasHost, 0);

            vm.SelectedElementBreakerType = "MCCB";

            var after = VisualTreeHelper.GetChild(window.LadderCanvasHost, 0);
            Assert.NotSame(before, after);   // 再描画されたなら新しいDrawingVisualインスタンスになる
        });
}
