using System.Collections.Generic;
using System.Linq;
using AvalonDock.Layout;

namespace Ecad2.App.Views;

/// <summary>
/// T-130: 保存済みレイアウト（<c>main-layout.xml</c>）が持たない既定値を、
/// Deserialize 直後にコード側から強制適用する。
/// <para>
/// <b>なぜXAMLでは足りないか</b>——<c>XmlLayoutSerializer.Deserialize</c> はモデルツリーを
/// 丸ごと差し替えるため、XAMLで宣言した値は失われる。保存ファイルに当該属性が書かれていなければ
/// AvalonDockの既定値が入る。**XAMLに書くと「効いているように見えて効かない」**という、
/// P-132（XAML既定値を変えても保存レイアウトが勝つ構造）そのものの罠になる。
/// ゆえに本クラスへ一元化し、XAML側には書かない。
/// </para>
/// <para>
/// <b>前例</b>——同じ問題への同じ対処が既にある。<c>MainWindow.TryDeserializeDockingLayout</c> は
/// Deserialize 成功直後に <c>RootPanel.CanDock = false</c> を再強制している（旧XMLに CanDock 属性が
/// 無いと既定値 true へ復元され、XAML側の指定が消える問題への対処、T-110増分1）。本クラスはその
/// 作法を踏襲する。次回保存時には正規化された値が書き出される。
/// </para>
/// </summary>
internal static class DockingLayoutDefaults
{
    /// <summary>
    /// AutoHideフライアウトの既定寸法1件分。
    /// <para>
    /// 【2026-08-02改訂・T-130機序調査（隠密）】当初は「ドック先のサイドで軸が1つに決まる」前提で
    /// <c>FlyoutAxis</c>単一軸の設計だったが、機序調査（<c>docs/ecad2-t130-flyout-axis-mechanism-onmitsu.md</c>）
    /// で誤りと判明した——AvalonDockが軸を決める材料は「親<c>LayoutPanel</c>の<c>Orientation</c>」
    /// のみであり、「そのパネルが画面のどちらに在るか」は一切見ない。ecad2の右列
    /// （機器表・プロパティ）は<c>Orientation="Vertical"</c>の下に在るため、画面上は右にありながら
    /// <c>AnchorSide.Top</c>/<c>Bottom</c>へ解決され、<c>AutoHideWidth</c>ではなく
    /// <c>AutoHideHeight</c>を見る——「値は正しいが描画に反映されない」型（PR-20系）の一種。
    /// <b>案4（殿裁可2026-08-02）＝軸の解決を当てにせず、幅・高さの両方に値を入れる。</b>
    /// どちらに解決されても既定100.0にはならず、レイアウト構造を変えても破綻しない。
    /// </para>
    /// </summary>
    /// <param name="ContentId"><c>MainWindow.xaml</c> の宣言と一致させること。</param>
    /// <param name="Width">幅側の既定値。この軸を持たない（触れさせぬ）パネルは<c>null</c>。</param>
    /// <param name="Height">高さ側の既定値。同上。</param>
    internal readonly record struct FlyoutDefault(string ContentId, double? Width, double? Height);

    /// <summary>
    /// AutoHideしうる全パネルの既定寸法。
    /// <para>
    /// <b>この値を入れないと何が起きるか</b>——AvalonDockは <c>AutoHideWidth</c>／
    /// <c>AutoHideHeight</c> が 0.0 のとき <c>AutoHideMinWidth</c>／<c>AutoHideMinHeight</c> を採る。
    /// その既定はいずれも 100.0（<c>LayoutAnchorable.cs:34,36</c>）であり、通常ドック時の寸法とは
    /// 無関係な細さ・低さになる。殿の実機で「シートパネルが細く現れる」と見えた症状の正体がこれ
    /// （実測98px、差2pxは枠線・スプリッタ分）。
    /// </para>
    /// <para>
    /// <b>対象の選び方</b>——<c>MainWindow.xaml</c> の <c>LayoutAnchorable</c> は全6件だが、
    /// うち <c>MainToolBar</c>／<c>PlacementToolBar</c> はAutoHideの入口を持たない
    /// （表示メニューの「パネルを自動的に隠す」に項目が無く、タイトルバー常時非表示ゆえピン操作も
    /// できない。<c>MainWindow.xaml</c> の同メニュー宣言のコメントが「4ペインへ代替動線を提供する」と
    /// 明記している）。ゆえに残る4件を対象とする。
    /// </para>
    /// <para>
    /// <b>機器表・プロパティは幅・高さ両方を持つ（案4）。</b>
    /// 高さの値<c>160.0</c>は殿裁定（2026-08-02、案(あ)＝出力パネルと同じ値）。通常ドック時の高さは
    /// <c>DockMinHeight="80"</c>のみで分割により決まる動的値のため、通常時に揃えるという従来の根拠
    /// （<c>MainWindow.xaml</c>の<c>DockWidth</c>／<c>DockHeight</c>）が使えず、目安として定めたもの。
    /// 幅の値<c>280.0</c>は従来どおり親<c>LayoutPanel</c>の<c>DockWidth="280"</c>と揃える。
    /// シート（左）・出力（下）は現に正しく解決されている（軸の食い違いが無い）ため、片軸のみ据え置く。
    /// </para>
    /// <para>
    /// <b>留意</b>——<c>AutoHideWidth</c>／<c>AutoHideHeight</c> の setter はいずれも
    /// <c>Math.Max(value, AutoHideMinXxx)</c> を通す（<c>LayoutAnchorable.cs:66,94</c>）。
    /// 本表の値はいずれも既定の最小値100より大きいため素通りする。逆に <c>AutoHideMinWidth</c> 側を
    /// 引き上げる案は採らない——それでは利用者がその値未満へ縮められなくなり、リサイズの自由度を
    /// 奪うため。
    /// </para>
    /// </summary>
    internal static readonly IReadOnlyList<FlyoutDefault> All = new FlyoutDefault[]
    {
        new("LeftPalette", Width: 190.0, Height: null),          // シート（左）
        new("DeviceTable", Width: 280.0, Height: 160.0),         // 機器表（右上）——案4：両軸とも設定
        new("RightPanelBottom", Width: 280.0, Height: 160.0),    // プロパティ（右下）——案4：両軸とも設定
        new("OutputPanel", Width: null, Height: 160.0),          // 出力（下）
    };

    /// <summary>
    /// レイアウト内の各パネルへAutoHideフライアウトの既定寸法を適用する。
    /// 対象が見つからない場合・レイアウトがnullの場合は何もしない（起動途中の呼び出しに耐える）。
    /// <para>
    /// <b>【殿裁定2026-07-27】保存値が無いとき（<c>0.0</c>）だけ入れる。既に値があれば尊重する。</b>
    /// 初版は無条件で上書きしていたが、それでは<b>利用者がフライアウトの寸法を変えても再起動のたびに
    /// 巻き戻る</b>（隠密の静的レビューで発覚）。<c>AutoHideMinWidth</c> を固定値にする案を
    /// 「リサイズの自由度を奪う」として却下しておきながら、<b>同じ問題を「起動毎」という別の形で
    /// 再導入していた</b>——却下の理由が自分の実装に跳ね返っていたことになる。
    /// </para>
    /// </summary>
    internal static void ApplyAutoHideSizes(LayoutRoot? layout)
    {
        if (layout is null) return;
        var anchorables = layout.Descendents().OfType<LayoutAnchorable>().ToList();
        foreach (var target in All)
        {
            foreach (var anchorable in anchorables.Where(a => a.ContentId == target.ContentId))
                ApplyOne(anchorable, target);
        }
    }

    /// <summary>1件分の適用。各軸とも、その軸に既定値が定義されており、かつ保存値（0.0以外）が
    /// 無ければ入れる。両軸を持つパネル（案4）では、それぞれ独立に判定する。</summary>
    private static void ApplyOne(LayoutAnchorable anchorable, FlyoutDefault target)
    {
        if (target.Width is { } width && anchorable.AutoHideWidth == 0.0)
            anchorable.AutoHideWidth = width;
        if (target.Height is { } height && anchorable.AutoHideHeight == 0.0)
            anchorable.AutoHideHeight = height;
    }
}
