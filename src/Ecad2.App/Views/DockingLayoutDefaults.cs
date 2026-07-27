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
    /// <summary>シートパネルの <c>ContentId</c>（<c>MainWindow.xaml</c> の宣言と一致させること）。</summary>
    internal const string SheetPanelContentId = "LeftPalette";

    /// <summary>
    /// シートパネルのAutoHideフライアウト幅（DIP）。通常ドック時の <c>DockWidth="190"</c> に揃える。
    /// <para>
    /// <b>この値を入れないと何が起きるか</b>——<c>LayoutAutoHideWindowControl.cs:306</c>（左側の場合）は
    /// <c>AutoHideWidth == 0.0</c> のとき <c>AutoHideMinWidth</c> を採る。その既定は 100.0
    /// （<c>LayoutAnchorable.cs:34</c>）であり、通常ドック時の190pxと無関係な細い幅になる。
    /// 殿の実機で「シートパネルが細く現れる」と見えた症状の正体がこれ（実測98px、差2pxは枠線・
    /// スプリッタ分）。
    /// </para>
    /// <para>
    /// <b>留意</b>——<c>AutoHideWidth</c> の setter は <c>Math.Max(value, AutoHideMinWidth)</c> を
    /// 通す（<c>LayoutAnchorable.cs:66</c>）。本値190は既定の最小幅100より大きいため素通りする。
    /// 逆に <c>AutoHideMinWidth</c> 側を190にする案は採らない——それでは利用者が190px未満へ
    /// 縮められなくなり、リサイズの自由度を奪うため。
    /// </para>
    /// </summary>
    internal const double SheetPanelAutoHideWidth = 190.0;

    /// <summary>
    /// レイアウト内のシートパネルへAutoHide幅の既定値を適用する。
    /// 対象が見つからない場合・レイアウトがnullの場合は何もしない（起動途中の呼び出しに耐える）。
    /// </summary>
    internal static void ApplyAutoHideWidth(LayoutRoot? layout)
    {
        var anchorable = layout?.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == SheetPanelContentId);
        if (anchorable is null) return;
        anchorable.AutoHideWidth = SheetPanelAutoHideWidth;
    }
}
