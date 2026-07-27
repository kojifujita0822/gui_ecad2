using AvalonDock.Layout;
using Ecad2.App.Views;

namespace Ecad2.App.Tests;

/// <summary>
/// T-130: AutoHideフライアウト幅の既定値強制（<see cref="DockingLayoutDefaults"/>）の回帰テスト。
/// <para>
/// 症状＝殿の実機でシートパネルのAutoHideフライアウトが細く（実測98px、通常ドック時は182px）
/// 現れた。原因は保存済み <c>main-layout.xml</c> に <c>AutoHideWidth</c> 属性が無く、AvalonDockが
/// <c>AutoHideMinWidth</c> の既定100.0を採るため（<c>LayoutAutoHideWindowControl.cs:306</c> /
/// <c>LayoutAnchorable.cs:34</c>）。
/// </para>
/// <para>
/// <b>本テストが成立するのは、対処をView層から純粋関数として切り出したため</b>——
/// <c>MainWindow</c> のメソッドに置いていれば <c>Window</c> 派生の型初期化を伴い単体テストは
/// 困難であった（<c>memory: feedback_hard_to_test_is_design_smell</c>）。
/// <c>LayoutRoot</c>/<c>LayoutAnchorable</c> はモデル層でありUIスレッドを要求しない。
/// </para>
/// </summary>
public class T130AutoHideWidthTests
{
    private static (LayoutRoot Layout, LayoutAnchorable Anchorable) BuildLayoutWith(string contentId)
    {
        var anchorable = new LayoutAnchorable { ContentId = contentId };
        var pane = new LayoutAnchorablePane();
        pane.Children.Add(anchorable);
        var layout = new LayoutRoot();
        layout.RootPanel.Children.Add(pane);
        return (layout, anchorable);
    }

    /// <summary>
    /// 【本命】保存レイアウトが <c>AutoHideWidth</c> を持たない状態（＝殿の環境そのもの）から、
    /// 既定値が適用されること。
    /// </summary>
    [Fact]
    public void ApplyAutoHideWidth_SetsSheetPanelWidth_WhenSavedLayoutOmitsTheAttribute()
    {
        var (layout, sheetPanel) = BuildLayoutWith(DockingLayoutDefaults.SheetPanelContentId);
        // AvalonDockの既定。この0.0こそが AutoHideMinWidth(100.0) を採らせていた原因。
        Assert.Equal(0.0, sheetPanel.AutoHideWidth);

        DockingLayoutDefaults.ApplyAutoHideWidth(layout);

        Assert.Equal(DockingLayoutDefaults.SheetPanelAutoHideWidth, sheetPanel.AutoHideWidth);
        Assert.Equal(190.0, sheetPanel.AutoHideWidth);
    }

    /// <summary>
    /// 【殿裁定2026-07-27で意図が反転したテスト】保存値があれば尊重し、上書きしない。
    /// <para>
    /// <b>初版はこの逆（無条件に190で上書き）を期待動作として固定していた。</b>それでは利用者が
    /// フライアウトの幅を変えても<b>再起動のたびに巻き戻る</b>——隠密の静的レビューで発覚。
    /// <c>AutoHideMinWidth</c> を190固定にする案を「リサイズの自由度を奪う」として却下しながら、
    /// 同じ問題を「起動毎」という別の形で再導入していた。
    /// </para>
    /// <para>
    /// <b>旧テストを消さず意図を反転させて残すのは、「かつてこう決めていた」ではなく
    /// 「今はこう決めている」が読み取れるようにするため</b>（家老指示）。
    /// </para>
    /// </summary>
    [Fact]
    public void ApplyAutoHideWidth_RespectsExistingValue_SoUserResizeSurvivesRestart()
    {
        var (layout, sheetPanel) = BuildLayoutWith(DockingLayoutDefaults.SheetPanelContentId);
        sheetPanel.AutoHideWidth = 320.0;   // 利用者が広げた幅が保存されていた状態

        DockingLayoutDefaults.ApplyAutoHideWidth(layout);

        Assert.Equal(320.0, sheetPanel.AutoHideWidth);
    }

    /// <summary>
    /// 【境界】保存値が「0でない極小値」であった場合の振る舞い。
    /// <para>
    /// <c>AutoHideWidth</c> の setter は <c>Math.Max(value, AutoHideMinWidth)</c> を通す
    /// （<c>LayoutAnchorable.cs:66</c>）ため、<b>100.0未満は設定した時点で100.0へ切り上げられる</b>。
    /// すなわち「0でない極小値」は実質存在しえず、利用者が100px未満へ縮めることもできない。
    /// 本テストはその前提を実測で固定する——前提が崩れれば「保存値を尊重する」判断の意味も変わる。
    /// </para>
    /// </summary>
    [Fact]
    public void AutoHideWidth_BelowMinWidth_IsClampedBySetter_SoTinySavedValuesCannotExist()
    {
        var (layout, sheetPanel) = BuildLayoutWith(DockingLayoutDefaults.SheetPanelContentId);

        sheetPanel.AutoHideWidth = 1.0;
        Assert.Equal(100.0, sheetPanel.AutoHideWidth);   // AutoHideMinWidthの既定へ切り上げ

        // 切り上げ後は0でないため、既定値の適用は行われない（＝保存値として尊重される）
        DockingLayoutDefaults.ApplyAutoHideWidth(layout);
        Assert.Equal(100.0, sheetPanel.AutoHideWidth);
    }

    /// <summary>
    /// 対象外のパネルには触れない（本増分の範囲＝シートパネルのみ、という境界の固定）。
    /// 機器表・プロパティ・出力パネルも同じ穴を持つが、殿裁可は「まず幅のみ・シートパネル」である。
    /// </summary>
    [Fact]
    public void ApplyAutoHideWidth_DoesNotTouchOtherPanels()
    {
        var (layout, sheetPanel) = BuildLayoutWith(DockingLayoutDefaults.SheetPanelContentId);
        var otherPane = new LayoutAnchorablePane();
        var other = new LayoutAnchorable { ContentId = "DeviceTable" };
        otherPane.Children.Add(other);
        layout.RootPanel.Children.Add(otherPane);

        DockingLayoutDefaults.ApplyAutoHideWidth(layout);

        Assert.Equal(190.0, sheetPanel.AutoHideWidth);
        Assert.Equal(0.0, other.AutoHideWidth);
    }

    /// <summary>
    /// 対象が見つからないレイアウトでも例外を投げない（起動途中・破損レイアウトへの耐性）。
    /// </summary>
    [Fact]
    public void ApplyAutoHideWidth_DoesNothing_WhenSheetPanelIsAbsent()
    {
        var (layout, _) = BuildLayoutWith("SomeOtherPanel");

        DockingLayoutDefaults.ApplyAutoHideWidth(layout);   // 例外を投げないことが検証内容
    }

    /// <summary>レイアウトがnullでも落ちない（Deserialize失敗直後の呼び出しに耐える）。</summary>
    [Fact]
    public void ApplyAutoHideWidth_DoesNothing_WhenLayoutIsNull()
        => DockingLayoutDefaults.ApplyAutoHideWidth(null);

    /// <summary>
    /// 既定幅は通常ドック時の <c>DockWidth="190"</c>（<c>MainWindow.xaml</c>）と揃っていること。
    /// 片方だけ変えると、ドック時とAutoHide時で幅が食い違う。
    /// </summary>
    [Fact]
    public void SheetPanelAutoHideWidth_MatchesDockWidthDeclaredInXaml()
        => Assert.Equal(190.0, DockingLayoutDefaults.SheetPanelAutoHideWidth);

    /// <summary>
    /// AvalonDockの <c>AutoHideWidth</c> setter は <c>Math.Max(value, AutoHideMinWidth)</c> を通す
    /// （<c>LayoutAnchorable.cs:66</c>）。既定の最小幅100.0より本値190.0は大きいため素通りする——
    /// この前提が崩れれば設定値が黙って切り上げられるため、前提そのものを固定しておく。
    /// </summary>
    [Fact]
    public void AutoHideWidthSetter_DoesNotClampOurValue_BecauseItExceedsDefaultMinWidth()
    {
        var anchorable = new LayoutAnchorable();
        Assert.Equal(100.0, anchorable.AutoHideMinWidth);   // AvalonDockの既定

        anchorable.AutoHideWidth = DockingLayoutDefaults.SheetPanelAutoHideWidth;

        Assert.Equal(190.0, anchorable.AutoHideWidth);
    }
}
