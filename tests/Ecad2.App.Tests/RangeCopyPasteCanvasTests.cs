using System.Windows.Media;
using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// 範囲選択と貼り付けのゴースト（殿ご下命2026-10-09）が、キャンバスの再描画に結線されていることのテスト。
/// <para>
/// 観測点は <see cref="T133Increment5CanvasRedrawTests"/> と同じ——<c>LadderCanvas.Draw()</c> は呼ばれるたび
/// 新しい <c>DrawingVisual</c> を作るゆえ、先頭の子の参照が変わったか否かで「再描画されたか」を測る。
/// <b>SelectedCell が動かぬまま変わる場合</b>（範囲の解除・貼り付け位置の確認への出入り）を選んで測る
/// ——SelectedCell が動く場合は既存の契機で描き直されるゆえ、新しい契機の漏れを検出できぬ。
/// </para>
/// <para>
/// 併せて、範囲の塗りとゴースト（要素・縦コネクタ・配線分断・枠の全種）を実際に描かせ、
/// 描画の途中で例外が出ぬことを確かめる。
/// </para>
/// </summary>
public class RangeCopyPasteCanvasTests
{
    private static Visual FirstVisual(MainWindow window) =>
        (Visual)VisualTreeHelper.GetChild(window.LadderCanvasHost, 0);

    [Fact]
    public void 範囲の解除と貼り付け確認への出入りでキャンバスが再描画される()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;
            vm.NewDocument();
            var sheet = vm.CurrentSheet!;
            vm.SelectedCell = new GridPos(0, 0);
            vm.PlaceElementAtSelectedCell(ElementKind.ContactNO, orient: null);
            vm.SelectedCell = new GridPos(1, 2);
            vm.PlaceElementAtSelectedCell(ElementKind.Coil, orient: null);
            sheet.Connectors.Add(new VerticalConnector { Column = 1, TopRow = 0, BottomRow = 1 });
            sheet.WireBreaks.Add(new WireBreak { Row = 0, Boundary = 1.5 });
            sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(0, 0), Width = 2, Height = 2 });

            // 範囲を選ぶ(塗りを描かせる)。
            vm.SelectRange(new GridPos(0, 0), new GridPos(1, 2));
            Assert.NotNull(vm.SelectedRange);
            var withRange = FirstVisual(window);

            // 同じセルへの再代入: SelectedCell は変わらぬが範囲は解ける。
            vm.SelectedCell = new GridPos(1, 2);
            Assert.Null(vm.SelectedRange);
            var rangeCleared = FirstVisual(window);
            Assert.NotSame(withRange, rangeCleared);

            // コピーして貼り付け位置の確認へ: SelectedCell は変わらぬがゴーストが出る。
            vm.SelectRange(new GridPos(0, 0), new GridPos(1, 2));
            Assert.True(vm.CopySelection());
            vm.SelectedCell = new GridPos(4, 3);
            var beforePaste = FirstVisual(window);
            Assert.True(vm.BeginPaste());
            var preview = vm.PastePreview!;
            Assert.Equal((2, 1, 1, 1), (preview.Content.Elements.Count, preview.Content.Connectors.Count,
                                        preview.Content.WireBreaks.Count, preview.Content.Frames.Count));
            var withGhost = FirstVisual(window);
            Assert.NotSame(beforePaste, withGhost);

            // 取りやめ: SelectedCell は変わらぬがゴーストが消える。
            vm.CancelPaste();
            Assert.NotSame(withGhost, FirstVisual(window));
        });

    /// <summary>貼れぬ位置（赤枠）・行を挿入する位置（橙枠）のゴーストも描ける。</summary>
    [Fact]
    public void 貼れぬ位置と行挿入の位置でもゴーストを描ける()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();
            var vm = (MainWindowViewModel)window.DataContext;
            vm.NewDocument();
            var sheet = vm.CurrentSheet!;
            vm.SelectedCell = new GridPos(0, 0);
            vm.PlaceElementAtSelectedCell(ElementKind.ContactNO, orient: null);
            vm.SelectedCell = new GridPos(0, 1);
            vm.PlaceElementAtSelectedCell(ElementKind.Coil, orient: null);
            vm.SelectRange(new GridPos(0, 0), new GridPos(0, 1));
            Assert.True(vm.CopySelection());
            Assert.True(vm.BeginPaste());

            vm.SelectedCell = new GridPos(0, 0);                          // 塞がっておる → 行挿入
            Assert.Equal(1, vm.PastePreview!.Plan.RowsToInsert);
            vm.SelectedCell = new GridPos(0, sheet.Grid.Columns - 1);     // 右へはみ出す → 貼れぬ
            Assert.False(vm.PastePreview!.Plan.CanPaste);
            vm.SelectedCell = new GridPos(sheet.Grid.Rows - 1, 0);        // 最下行 → そのまま貼れる
            Assert.True(vm.PastePreview!.Plan.CanPaste);

            Assert.True(VisualTreeHelper.GetChildrenCount(window.LadderCanvasHost) > 0);
        });
}
