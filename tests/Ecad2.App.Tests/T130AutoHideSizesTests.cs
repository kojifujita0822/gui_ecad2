using System.Linq;
using AvalonDock.Layout;
using Ecad2.App.Views;

namespace Ecad2.App.Tests;

/// <summary>
/// T-130: AutoHideフライアウトの既定寸法強制（<see cref="DockingLayoutDefaults"/>）の回帰テスト。
/// <para>
/// 症状＝殿の実機でシートパネルのAutoHideフライアウトが細く（実測98px、通常ドック時は182px）
/// 現れた。原因は保存済み <c>main-layout.xml</c> に <c>AutoHideWidth</c> 属性が無く、AvalonDockが
/// <c>AutoHideMinWidth</c> の既定100.0を採るため（<c>LayoutAutoHideWindowControl.cs:306</c> /
/// <c>LayoutAnchorable.cs:34</c>）。
/// </para>
/// <para>
/// <b>【殿裁可2026-07-27・第2段】対象をシートパネルから、AutoHideしうる全4パネルへ広げた。</b>
/// 裁定は「シートパネルと同様に直す＝同じ穴を残さぬ」。<b>出力パネルは下ドックのため軸が「高さ」で
/// あり、幅ではない</b>——AvalonDock一次ソースは右(<c>:297</c>)・左(<c>:306</c>)が
/// <c>AutoHideWidth</c>、上(<c>:316</c>)・下(<c>:327</c>)が <c>AutoHideHeight</c> を見るが、
/// <b>いずれも「0.0なら AutoHideMinXxx（既定100.0）を採る」という完全に同型の構造</b>であり、
/// 軸が違うことは穴を分ける理由にならぬと判じた。本クラスは幅・高さの双方を扱う（第1段では幅のみ
/// だったため <c>T130AutoHideWidthTests</c> という名であったが、実態に合わせて改称した）。
/// </para>
/// <para>
/// <b>本テストが成立するのは、対処をView層から純粋関数として切り出したため</b>——
/// <c>MainWindow</c> のメソッドに置いていれば <c>Window</c> 派生の型初期化を伴い単体テストは
/// 困難であった（<c>memory: feedback_hard_to_test_is_design_smell</c>）。
/// <c>LayoutRoot</c>/<c>LayoutAnchorable</c> はモデル層でありUIスレッドを要求しない。
/// </para>
/// </summary>
public class T130AutoHideSizesTests
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

    private static LayoutAnchorable AddAnchorable(LayoutRoot layout, string contentId)
    {
        var anchorable = new LayoutAnchorable { ContentId = contentId };
        var pane = new LayoutAnchorablePane();
        pane.Children.Add(anchorable);
        layout.RootPanel.Children.Add(pane);
        return anchorable;
    }

    /// <summary>
    /// 【本命】保存レイアウトが属性を持たない状態（＝殿の環境そのもの）から、既定値が適用されること。
    /// <para>
    /// 幅のパネル3件を対象とする。値が 190／280／280 と揃っていない（非対称）ため、
    /// ContentIdの取り違えがあれば期待値と食い違って現れる
    /// （<c>samurai.md</c>「テスト入力の対称性・退化性チェック」）。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("LeftPalette", 190.0)]
    [InlineData("DeviceTable", 280.0)]
    [InlineData("RightPanelBottom", 280.0)]
    public void ApplyAutoHideSizes_幅のパネルは属性が無いとき既定幅が入る(string contentId, double expected)
    {
        var (layout, anchorable) = BuildLayoutWith(contentId);
        // AvalonDockの既定。この0.0こそが AutoHideMinWidth(100.0) を採らせていた原因。
        Assert.Equal(0.0, anchorable.AutoHideWidth);

        DockingLayoutDefaults.ApplyAutoHideSizes(layout);

        Assert.Equal(expected, anchorable.AutoHideWidth);
    }

    /// <summary>
    /// 【第2段の本命】出力パネル（下ドック）は幅ではなく高さに既定値が入ること。
    /// <para>
    /// 160.0 は他3件の 190／280 と異なる値を選んである——軸と値の対応を取り違えれば必ず露見する。
    /// </para>
    /// </summary>
    [Fact]
    public void ApplyAutoHideSizes_出力パネルは属性が無いとき既定高さが入る()
    {
        var (layout, outputPanel) = BuildLayoutWith("OutputPanel");
        Assert.Equal(0.0, outputPanel.AutoHideHeight);

        DockingLayoutDefaults.ApplyAutoHideSizes(layout);

        Assert.Equal(160.0, outputPanel.AutoHideHeight);
    }

    /// <summary>
    /// 【第3段の本命・T-130機序調査（隠密）】機器表・プロパティは幅・高さ両方に既定値が入ること（案4）。
    /// <para>
    /// 機序調査で判明したとおり、AvalonDockはドック先の見た目でなく親<c>LayoutPanel</c>の
    /// <c>Orientation</c>だけで軸を決めるため、右列に在る両パネルも<c>AnchorSide.Top</c>へ解決され
    /// <c>AutoHideHeight</c>を見る。<c>AutoHideWidth</c>だけでは効かない実害があった
    /// （<c>docs/ecad2-t130-otherpanels-verification-ninja.md</c>）。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("DeviceTable")]
    [InlineData("RightPanelBottom")]
    public void ApplyAutoHideSizes_機器表とプロパティは幅高さ両方に既定値が入る(string contentId)
    {
        var (layout, anchorable) = BuildLayoutWith(contentId);
        Assert.Equal(0.0, anchorable.AutoHideWidth);
        Assert.Equal(0.0, anchorable.AutoHideHeight);

        DockingLayoutDefaults.ApplyAutoHideSizes(layout);

        Assert.Equal(280.0, anchorable.AutoHideWidth);
        Assert.Equal(160.0, anchorable.AutoHideHeight);
    }

    /// <summary>
    /// 【軸の取り違え検出】片軸のみを持つパネル（シート・出力）は、持たぬ軸には触れないこと。
    /// <para>
    /// 案4以降も、片軸のみのパネル（<c>Height</c>が<c>null</c>のシート、<c>Width</c>が<c>null</c>の
    /// 出力）は従来どおり片方に限る。実装が<c>null</c>判定を見ず両方へ代入すれば、ここが落ちる。
    /// </para>
    /// </summary>
    [Fact]
    public void ApplyAutoHideSizes_シートパネルの高さには触れない()
    {
        var (layout, anchorable) = BuildLayoutWith("LeftPalette");

        DockingLayoutDefaults.ApplyAutoHideSizes(layout);

        Assert.Equal(0.0, anchorable.AutoHideHeight);
    }

    /// <summary>【軸の取り違え検出】高さのみのパネル（出力）へ幅を入れていないこと。</summary>
    [Fact]
    public void ApplyAutoHideSizes_出力パネルの幅には触れない()
    {
        var (layout, outputPanel) = BuildLayoutWith("OutputPanel");

        DockingLayoutDefaults.ApplyAutoHideSizes(layout);

        Assert.Equal(0.0, outputPanel.AutoHideWidth);
    }

    /// <summary>
    /// 【殿裁定2026-07-27で意図が反転したテスト】保存値があれば尊重し、上書きしない。
    /// <para>
    /// <b>初版はこの逆（無条件に上書き）を期待動作として固定していた。</b>それでは利用者が
    /// フライアウトの寸法を変えても<b>再起動のたびに巻き戻る</b>——隠密の静的レビューで発覚。
    /// <c>AutoHideMinWidth</c> を固定値にする案を「リサイズの自由度を奪う」として却下しながら、
    /// 同じ問題を「起動毎」という別の形で再導入していた。
    /// </para>
    /// <para>
    /// <b>旧テストを消さず意図を反転させて残すのは、「かつてこう決めていた」ではなく
    /// 「今はこう決めている」が読み取れるようにするため</b>（家老指示）。
    /// </para>
    /// </summary>
    [Fact]
    public void ApplyAutoHideSizes_保存値があれば尊重し利用者のリサイズが再起動を越えて残る()
    {
        var (layout, sheetPanel) = BuildLayoutWith("LeftPalette");
        var outputPanel = AddAnchorable(layout, "OutputPanel");
        sheetPanel.AutoHideWidth = 320.0;    // 利用者が広げた幅が保存されていた状態
        outputPanel.AutoHideHeight = 240.0;  // 高さ側も同様に尊重されること

        DockingLayoutDefaults.ApplyAutoHideSizes(layout);

        Assert.Equal(320.0, sheetPanel.AutoHideWidth);
        Assert.Equal(240.0, outputPanel.AutoHideHeight);
    }

    /// <summary>
    /// 【境界】保存値が「0でない極小値」であった場合の振る舞い。
    /// <para>
    /// <c>AutoHideWidth</c>／<c>AutoHideHeight</c> の setter はいずれも
    /// <c>Math.Max(value, AutoHideMinXxx)</c> を通す（<c>LayoutAnchorable.cs:66,94</c>）ため、
    /// <b>100.0未満は設定した時点で100.0へ切り上げられる</b>。すなわち「0でない極小値」は実質
    /// 存在しえず、利用者が100px未満へ縮めることもできない。本テストはその前提を実測で固定する
    /// ——前提が崩れれば「保存値を尊重する」判断の意味も変わる。
    /// </para>
    /// </summary>
    [Fact]
    public void 最小値未満の保存値はsetterで切り上げられるゆえ極小値は存在しえない()
    {
        var (layout, sheetPanel) = BuildLayoutWith("LeftPalette");
        var outputPanel = AddAnchorable(layout, "OutputPanel");

        sheetPanel.AutoHideWidth = 1.0;
        outputPanel.AutoHideHeight = 1.0;
        Assert.Equal(100.0, sheetPanel.AutoHideWidth);     // AutoHideMinWidthの既定へ切り上げ
        Assert.Equal(100.0, outputPanel.AutoHideHeight);   // AutoHideMinHeightの既定へ切り上げ

        // 切り上げ後は0でないため、既定値の適用は行われない（＝保存値として尊重される）
        DockingLayoutDefaults.ApplyAutoHideSizes(layout);
        Assert.Equal(100.0, sheetPanel.AutoHideWidth);
        Assert.Equal(100.0, outputPanel.AutoHideHeight);
    }

    /// <summary>
    /// 【意図を反転させた旧テスト】かつては「シートパネル以外に触れない」ことを固定していたが、
    /// 殿裁可の第2段で機器表・プロパティ・出力も対象になった。
    /// <para>
    /// <b>今なお触れてはならないのは、AutoHideの入口を持たないツールバー2件である。</b>
    /// 表示メニュー「パネルを自動的に隠す」に項目が無く、タイトルバー常時非表示ゆえピン操作も
    /// できない（<c>MainWindow.xaml</c> の同メニュー宣言のコメントが「4ペインへ代替動線を提供する」と
    /// 明記）。寸法を入れても意味がないため対象外とする。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("MainToolBar")]
    [InlineData("PlacementToolBar")]
    public void ApplyAutoHideSizes_AutoHideの入口を持たぬツールバーには触れない(string contentId)
    {
        var (layout, sheetPanel) = BuildLayoutWith("LeftPalette");
        var toolBar = AddAnchorable(layout, contentId);

        DockingLayoutDefaults.ApplyAutoHideSizes(layout);

        Assert.Equal(190.0, sheetPanel.AutoHideWidth);   // 対象は従来どおり適用される
        Assert.Equal(0.0, toolBar.AutoHideWidth);
        Assert.Equal(0.0, toolBar.AutoHideHeight);
    }

    /// <summary>対象が見つからないレイアウトでも例外を投げない（起動途中・破損レイアウトへの耐性）。</summary>
    [Fact]
    public void ApplyAutoHideSizes_対象が一つも無くても例外を投げない()
    {
        var (layout, _) = BuildLayoutWith("SomeOtherPanel");

        DockingLayoutDefaults.ApplyAutoHideSizes(layout);   // 例外を投げないことが検証内容
    }

    /// <summary>レイアウトがnullでも落ちない（Deserialize失敗直後の呼び出しに耐える）。</summary>
    [Fact]
    public void ApplyAutoHideSizes_レイアウトがnullでも落ちない()
        => DockingLayoutDefaults.ApplyAutoHideSizes(null);

    /// <summary>
    /// 幅の既定値は <c>MainWindow.xaml</c> の <c>DockWidth</c> と揃っていること。
    /// 片方だけ変えると、ドック時とAutoHide時で寸法が食い違う。
    /// <para>
    /// シートパネル1件だった頃の単数形テストを、幅を持つ3行へ広げたもの。
    /// 【2026-08-02改訂】高さ（案4で機器表・プロパティへ追加した160.0）は、通常ドック時が
    /// <c>DockMinHeight="80"</c>のみで分割により決まる動的値のため「揃える」対象が無く、
    /// 本テストの対象外（別テストで殿裁定値そのものを固定する）。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("LeftPalette", 190.0)]        // 親ペインの DockWidth="190"
    [InlineData("DeviceTable", 280.0)]        // 親LayoutPanelの DockWidth="280"
    [InlineData("RightPanelBottom", 280.0)]   // 同上
    public void 幅の既定値はMainWindowXamlのDockWidthと揃っている(string contentId, double expected)
    {
        var target = DockingLayoutDefaults.All.Single(d => d.ContentId == contentId);

        Assert.Equal(expected, target.Width);
    }

    /// <summary>出力パネルの高さは <c>MainWindow.xaml</c> の <c>DockHeight="160"</c> と揃っている。</summary>
    [Fact]
    public void 出力パネルの高さはMainWindowXamlのDockHeightと揃っている()
    {
        var target = DockingLayoutDefaults.All.Single(d => d.ContentId == "OutputPanel");

        Assert.Equal(160.0, target.Height);
    }

    /// <summary>
    /// 【T-130機序調査（隠密）・殿裁定2026-08-02】機器表・プロパティの高さは案(あ)＝出力パネルと
    /// 同じ160.0。通常ドック時の高さに揃える相手が無いため、殿裁定値そのものを固定する。
    /// </summary>
    [Theory]
    [InlineData("DeviceTable")]
    [InlineData("RightPanelBottom")]
    public void 機器表とプロパティの高さは殿裁定の160である(string contentId)
    {
        var target = DockingLayoutDefaults.All.Single(d => d.ContentId == contentId);

        Assert.Equal(160.0, target.Height);
    }

    /// <summary>
    /// 対象は4件——AutoHideの入口を持つパネルに限る。
    /// <para>
    /// 件数だけでなく、入口を持たないツールバー2件が紛れ込んでいないことも併せて固定する。
    /// <b>件数のみを見るテストは、1件足して1件抜けた入れ替わりを検出できない。</b>
    /// </para>
    /// </summary>
    [Fact]
    public void 対象はAutoHideの入口を持つ4パネルに限る()
    {
        Assert.Equal(4, DockingLayoutDefaults.All.Count);
        Assert.DoesNotContain(DockingLayoutDefaults.All, d => d.ContentId == "MainToolBar");
        Assert.DoesNotContain(DockingLayoutDefaults.All, d => d.ContentId == "PlacementToolBar");
    }

    /// <summary>
    /// 【2026-08-02改訂・案4】軸の持ち方——シートは幅のみ、出力は高さのみ、機器表・プロパティは
    /// 両方を持つこと。
    /// <para>
    /// ドック先のサイドが変わっても軸の解決を当てにしない（機序調査の結論）。
    /// 表の軸欄が実際のドック位置と食い違えば、寸法を入れても効かない
    /// （幅を入れても、AnchorSide.Top/Bottomへ解決されるフライアウトは高さしか見ない）。
    /// </para>
    /// </summary>
    [Fact]
    public void シートは幅のみ出力は高さのみ機器表とプロパティは両方持つ()
    {
        var leftPalette = DockingLayoutDefaults.All.Single(d => d.ContentId == "LeftPalette");
        Assert.NotNull(leftPalette.Width);
        Assert.Null(leftPalette.Height);

        var outputPanel = DockingLayoutDefaults.All.Single(d => d.ContentId == "OutputPanel");
        Assert.Null(outputPanel.Width);
        Assert.NotNull(outputPanel.Height);

        foreach (var contentId in new[] { "DeviceTable", "RightPanelBottom" })
        {
            var target = DockingLayoutDefaults.All.Single(d => d.ContentId == contentId);
            Assert.NotNull(target.Width);
            Assert.NotNull(target.Height);
        }
    }

    /// <summary>
    /// AvalonDockの setter は <c>Math.Max(value, AutoHideMinXxx)</c> を通す
    /// （<c>LayoutAnchorable.cs:66,94</c>）。既定の最小値100.0より本表の値はいずれも大きいため
    /// 素通りする——この前提が崩れれば設定値が黙って切り上げられるため、前提そのものを固定しておく。
    /// </summary>
    [Fact]
    public void setterは本表の値を切り上げない_いずれも既定の最小値を上回るゆえ()
    {
        var probe = new LayoutAnchorable();
        Assert.Equal(100.0, probe.AutoHideMinWidth);    // AvalonDockの既定
        Assert.Equal(100.0, probe.AutoHideMinHeight);   // 同上

        foreach (var target in DockingLayoutDefaults.All)
        {
            var anchorable = new LayoutAnchorable();
            if (target.Width is { } width)
            {
                anchorable.AutoHideWidth = width;
                Assert.Equal(width, anchorable.AutoHideWidth);
            }
            if (target.Height is { } height)
            {
                anchorable.AutoHideHeight = height;
                Assert.Equal(height, anchorable.AutoHideHeight);
            }
        }
    }
}
