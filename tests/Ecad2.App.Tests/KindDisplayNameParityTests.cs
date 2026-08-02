using Ecad2.App.ViewModels;
using Ecad2.App.Views;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分4-A: 種別の日本語表示名。
/// <para>
/// <b>【一致を測るのが本題である】</b>同じ switch が <c>MainWindowViewModel.KindDisplayName</c> と
/// <c>LadderCanvas.KindDisplayName</c> の2箇所に在る（家老裁定2026-07-28＝rule of three 未達ゆえ
/// 共通化はせず個別に足す）。<b>片方だけへ種別を足せば、プロパティパネルの表示と
/// UI Automation の Name が食い違う</b>——目で見て気づける食い違いではない。
/// </para>
/// <para>
/// <b>全種別を回すゆえ、将来どの種別を片方だけへ足しても必ず鳴る。</b>
/// 「3極記号3種を足したこと」だけを測る形では、次に足される種別を守れぬ。
/// </para>
/// <para>
/// <b>静的メソッドゆえ STA は要らぬ</b>——<c>LadderCanvas</c> は <c>FrameworkElement</c> 派生だが、
/// インスタンスを立てずに呼べる。
/// </para>
/// </summary>
public class KindDisplayNameParityTests
{
    public static IEnumerable<object[]> AllKinds()
        => Enum.GetValues<ElementKind>().Select(k => new object[] { k });

    /// <summary>2箇所の表示名が全種別で一致すること。<b>片方だけへ足す誤りへの網。</b></summary>
    [Theory]
    [MemberData(nameof(AllKinds))]
    public void 両者の表示名は全種別で一致する(ElementKind kind)
        => Assert.Equal(MainWindowViewModel.KindDisplayName(kind), LadderCanvas.KindDisplayName(kind));

    /// <summary>
    /// 主回路3極記号の表示名（T-133増分4-A で追加）。
    /// <b>文言の出所は原本 GuiEcad のメニュー文言（<c>MainPage.Tools.cs:238-252</c>）から向きを除いたもの。</b>
    /// <para>
    /// <b>【<c>ThermalOverload3P</c> が「2極」なのは書き誤りではない】</b>型名は 3P だが、
    /// 原本のメニュー文言が「サーマル(OL) 2極」である。<b>ここを「3極」へ直すのは原本からの逸脱にあたる</b>
    /// ——直すならば殿の裁可を要する。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(ElementKind.Breaker3P, "ブレーカ")]
    [InlineData(ElementKind.ContactorMain3P, "電磁接触器 主接点")]
    [InlineData(ElementKind.ThermalOverload3P, "サーマル(OL) 2極")]
    public void 主回路3極記号の表示名が定まっている(ElementKind kind, string expected)
        => Assert.Equal(expected, MainWindowViewModel.KindDisplayName(kind));

    /// <summary>
    /// 既存10種の表示名は変えていないこと（回帰の網）。
    /// <b>3極記号を足す際に既存の行を触っていないか</b>を測る——switch の並び替えや
    /// 誤った上書きは、実機では「別の記号の名前が出る」形でしか現れぬ。
    /// </summary>
    [Theory]
    [InlineData(ElementKind.ContactNO, "a接点")]
    [InlineData(ElementKind.ContactNC, "b接点")]
    [InlineData(ElementKind.Coil, "コイル")]
    [InlineData(ElementKind.Lamp, "ランプ")]
    [InlineData(ElementKind.PushButtonNO, "押しボタン(NO)")]
    [InlineData(ElementKind.PushButtonNC, "押しボタン(NC)")]
    [InlineData(ElementKind.SelectSwitch, "セレクトSW")]
    [InlineData(ElementKind.Terminal, "端子台")]
    [InlineData(ElementKind.Timer, "タイマ")]
    [InlineData(ElementKind.Counter, "カウンタ")]
    public void 既存種別の表示名は変わっていない(ElementKind kind, string expected)
        => Assert.Equal(expected, MainWindowViewModel.KindDisplayName(kind));

    /// <summary>
    /// 日本語名を持たぬ種別は種別名がそのまま返る（既定の枝）。
    /// <b>3極記号がこの枝へ落ちておらぬことの対照</b>——落ちていれば "Breaker3P" が返り、
    /// 上の表示名テストが鳴る。ここでは<b>既定の枝そのものが生きている</b>ことを押さえる。
    /// </summary>
    [Theory]
    [InlineData(ElementKind.Motor)]
    [InlineData(ElementKind.EmergencyStop)]
    public void 日本語名を持たぬ種別は種別名がそのまま返る(ElementKind kind)
        => Assert.Equal(kind.ToString(), MainWindowViewModel.KindDisplayName(kind));
}
