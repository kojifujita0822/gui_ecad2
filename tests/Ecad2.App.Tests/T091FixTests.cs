namespace Ecad2.App.Tests;

/// <summary>
/// T-091回帰テスト(隠密指摘P-093、T-087往復5周目最終レビューの副次発見)。F5〜F10グローバル
/// ショートカットのcase guardが!HasAnyDraftを見ておらず、記入中(縦コネクタ/自由線/画像挿入
/// ドラフト中)でも配置バーが開いてしまいドラフトがキャンセルされず宙に浮いていた。
/// MainWindow.xaml.csのcase guard自体はコードビハインドでテスト基盤が無いため、
/// T-087のShouldSuppressPartSelectionActivationと同型に判定ロジックのみをinternal static抽出した
/// ShouldAllowShortcutPlacementを検証する。
/// </summary>
public class T091FixTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void ShouldAllowShortcutPlacement_CanEditDiagramがtrueかつHasAnyDraftがfalseのときのみtrue(
        bool canEditDiagram, bool hasAnyDraft, bool expectedAllow)
    {
        bool actual = MainWindow.ShouldAllowShortcutPlacement(canEditDiagram, hasAnyDraft);

        Assert.Equal(expectedAllow, actual);
    }
}
