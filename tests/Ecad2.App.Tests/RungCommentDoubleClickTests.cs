namespace Ecad2.App.Tests;

/// <summary>T-080往復2周目(a)修正: 行コメントダブルクリック検知ロジック(MainWindow.
/// ShouldOpenRungCommentEditor)の回帰テスト。隠密テスト設計
/// (docs/ecad2-t080-doubleclick-root-cause-onmitsu.md 観点1)のClickCount軸を網羅する。
/// ヒット領域内外・主回路シート判定はLadderCanvas.HitTestRungCommentRowの責務のため
/// RungCommentHitTestTestsで別途検証する(2層に分けることで観点1の全組み合わせを重複なくカバーする)。
/// </summary>
public class RungCommentDoubleClickTests
{
    [Theory]
    [InlineData(1, 5, null)]   // 境界値: 1回目単独クリックでは開かない
    [InlineData(2, 5, 5)]      // 主要な正常系: 2回目でヒット行が返れば開く
    [InlineData(2, null, null)] // ヒット領域外(HitTestRungCommentRowがnull)なら開かない
    [InlineData(3, 5, null)]   // 家老裁定(2026-07-12): トリプルクリックは現行==2を維持し開かない
    [InlineData(0, 5, null)]   // 境界値: ClickCount=0(理論上非発生だが安全側を確認)
    public void ShouldOpenRungCommentEditor_ClickCountとヒット結果の組み合わせ(
        int clickCount, int? hitTestRow, int? expectedRow)
    {
        int? actual = MainWindow.ShouldOpenRungCommentEditor(clickCount, hitTestRow);

        Assert.Equal(expectedRow, actual);
    }
}
