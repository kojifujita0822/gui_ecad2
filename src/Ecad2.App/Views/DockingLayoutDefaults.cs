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
    /// AutoHideフライアウトで意味を持つ寸法の軸。ドック先のサイドで決まる。
    /// <para>
    /// AvalonDock一次ソース <c>LayoutAutoHideWindowControl.cs</c> の <c>_side</c> 分岐がこの対応を
    /// 決めている——右(<c>:297</c>)・左(<c>:306</c>)は <c>AutoHideWidth</c>、
    /// 上(<c>:316</c>)・下(<c>:327</c>)は <c>AutoHideHeight</c> を見る。いずれも
    /// <b>値が 0.0 のとき対応する <c>AutoHideMinXxx</c>（既定100.0）を採る</b>という
    /// <b>完全に同型の構造</b>であり、軸が違うだけで穴の性質は同一である。
    /// </para>
    /// </summary>
    internal enum FlyoutAxis
    {
        /// <summary>左右にドックされたパネル（幅が意味を持つ）。</summary>
        Width,

        /// <summary>上下にドックされたパネル（高さが意味を持つ）。</summary>
        Height,
    }

    /// <summary>AutoHideフライアウトの既定寸法1件分。</summary>
    /// <param name="ContentId"><c>MainWindow.xaml</c> の宣言と一致させること。</param>
    /// <param name="Axis">ドック先のサイドで決まる軸。</param>
    /// <param name="Size">通常ドック時の <c>DockWidth</c>／<c>DockHeight</c> に揃える値（DIP）。</param>
    internal readonly record struct FlyoutDefault(string ContentId, FlyoutAxis Axis, double Size);

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
    /// <b>各値は <c>MainWindow.xaml</c> の <c>DockWidth</c>／<c>DockHeight</c> と揃えること。</b>
    /// 片方だけ変えるとドック時とAutoHide時で寸法が食い違う。この不変条件は
    /// <c>T130AutoHideSizesTests</c> が全行について固定している。
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
        new("LeftPalette", FlyoutAxis.Width, 190.0),          // シート（左）
        new("DeviceTable", FlyoutAxis.Width, 280.0),          // 機器表（右上）
        new("RightPanelBottom", FlyoutAxis.Width, 280.0),     // プロパティ（右下）
        new("OutputPanel", FlyoutAxis.Height, 160.0),         // 出力（下）——軸が高さである点に注意
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

    /// <summary>1件分の適用。保存値（0.0以外）があれば触れない。</summary>
    private static void ApplyOne(LayoutAnchorable anchorable, FlyoutDefault target)
    {
        if (target.Axis == FlyoutAxis.Width)
        {
            if (anchorable.AutoHideWidth != 0.0) return;
            anchorable.AutoHideWidth = target.Size;
        }
        else
        {
            if (anchorable.AutoHideHeight != 0.0) return;
            anchorable.AutoHideHeight = target.Size;
        }
    }
}
