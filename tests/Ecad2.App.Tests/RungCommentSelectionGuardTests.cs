namespace Ecad2.App.Tests;

/// <summary>T-080追加往復 課題2の修正: MainWindow.ShouldSkipSelectionForRungCommentAreaClickの
/// 回帰テスト。隠密テスト設計(docs/ecad2-t080-issue2-3-root-cause-onmitsu.md 課題2テスト設計)の
/// 同値分割を検証する。「選択状態が実際にクリアされる/されない」という結果側は既存の
/// SelectedConnectorExclusivityTests.SettingSelectedCell_ClearsSelectedConnector_LikeArrowKeyMove
/// (通常のグリッドクリックでは従来どおりSelectedCellのsetterがSelectedConnector等をクリアする)が
/// 既に回帰確認済みのため、本テストは新設の判定ロジック(ガードすべきか)のみを対象とする。
/// </summary>
public class RungCommentSelectionGuardTests
{
    [Theory]
    [InlineData(5, true)]     // ヒット領域内(行5) → 選択状態を変更しない(スキップ)
    [InlineData(0, true)]     // 境界値: 行0でも同様
    [InlineData(null, false)] // ヒット領域外(通常のグリッドクリック) → 従来どおり選択処理を続行
    public void ShouldSkipSelectionForRungCommentAreaClick_ヒットテスト結果による判定(
        int? hitTestRow, bool expectedSkip)
    {
        bool actual = MainWindow.ShouldSkipSelectionForRungCommentAreaClick(hitTestRow);

        Assert.Equal(expectedSkip, actual);
    }
}
