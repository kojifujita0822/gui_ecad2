using Ecad2.Model;

namespace Ecad2.Rendering;

/// <summary>線の役割。実際の線スタイルは <see cref="DrawingTheme"/> から引く（1か所変更で全体反映）。</summary>
public enum StrokeRole { Wire, BusRail, SymbolOutline, GroupFrame, Grid }

/// <summary>文字の役割。</summary>
public enum TextRole { DeviceName, LineNumber, CrossRef, Title, Comment }

/// <summary>
/// 役割ごとの線・文字プリセット（画面/PDF共通）。太さ・サイズは mm 固定。
///
/// カラー: 色を「テーマ非依存の素材定数（static）」と「テーマで切り替わるパレット（インスタンス）」に分離する。
/// - パレット（<see cref="Foreground"/>/<see cref="Background"/>/<see cref="GridColor"/>/<see cref="TableHeaderFill"/>）は
///   テーマごとに差し替える。線・記号・文字は <see cref="Foreground"/> を使う。
/// - <see cref="Powered"/>（通電）・<see cref="Blue"/>（接続済み）・<see cref="ManualForced"/>（手動強制）は
///   状態を表す「意味色」なのでテーマ間で固定（static のまま）。
///
/// 画面の作図色は <see cref="Default"/>（白地・黒線）と <see cref="Dark"/>（暗地・明線）の2種。
/// 画面はメニューの「ダークモード(作図色)」で切替、PDF は常に <see cref="Default"/> を使う。
/// </summary>
public sealed class DrawingTheme
{
    // ===== テーマ非依存の素材色（パレットの既定値・意味色）=====
    public static readonly Color Black = new(255, 0, 0, 0);
    public static readonly Color White = new(255, 255, 255, 255);
    public static readonly Color GridGray = new(255, 210, 210, 210);
    // 状態を表す意味色（テーマ間で固定）。
    public static readonly Color Blue = new(255, 0, 80, 220);          // 接続検査: 接続済み
    public static readonly Color Powered = new(255, 230, 60, 0);       // テストモード: 通電/励磁
    public static readonly Color ManualForced = new(110, 0, 80, 220);  // テストモード: 接点手動強制（半透明青）
    // T-061(殿裁定(3)=LDmicro式): 通電=赤(Powered、既存)/非通電=グレー。テストモード中のみ使う
    // (作画モードでは従来どおりForegroundのまま、DiagramRenderer.DrawElement参照)。
    public static readonly Color NonEnergizedGray = new(255, 150, 150, 150);
    // T-107(殿裁定=GX Works3同様の緑色): 機器コメント表示。ライト/ダーク両テーマで固定
    // (背景とのコントラストのみ確認、色自体はテーマ非依存)。
    public static readonly Color Comment = new(255, 0, 128, 0);
    // T-136(B)増分4(殿裁定2026-08-02): パーツエディタの接続点の種類色。ライト/ダーク両テーマで固定。
    // 赤(PortPower)は論点7(docs/ecad2-t136-increment4-plan-samurai.md)——実装後にLight/Dark
    // 双方の実機の絵を殿へお見せし、殿裁可2026-08-02=実機の絵をご覧のうえ確定(選択リング色=論点3の
    // 前例に倣う)。青(PortDrcExempt)は増分3以前からの塗り色(DodgerBlue)をそのまま流用、確定値。
    public static readonly Color PortPower = new(255, 220, 20, 20);       // 確定: 電源に接続される点
    public static readonly Color PortDrcExempt = new(255, 30, 144, 255); // 確定: 制御配線でDRC無効な点
    // 【往復2周目・家老裁定】PortColorのフォールバック専用。throw化は「開発時の実装ミスを実行時の
    // 危険（描画中の例外＝画面が落ちパーツ喪失）で買う」ため撤回(隠密指摘)。既存配色に現れぬ
    // マゼンタとし、case行が消えても即座に目に立つ形で異常時のみ検出できるようにする。
    public static readonly Color PortUnknown = new(255, 255, 0, 255);

    // 表（機器表・クロスリファレンス・表題欄）の罫線幅と、テスト通電配線の強調線幅(mm)。
    public const double TableLineWidth = 0.18;
    // 殿直接要望(2026-07-15)=通電線(オレンジ色)の視認性向上のため0.45→0.8へ太く調整
    // (画面DPI96想定で1px≈0.26mm、目安+0.26〜0.53mm=0.7〜1.0mm程度の中間値。実機の見た目は
    // 忍者確認で最終判断)。
    public const double PoweredWireWidth = 0.8;

    /// <summary>線幅の最小クランプ(mm)。画面(Win2D)とPDFで同一に保ち、極細線がどちらでも消えないようにする。</summary>
    public const double MinStrokeWidthMm = 0.05;

    /// <summary>
    /// ズーム倍率に依らず<b>画面上の太さを一定に保つ</b>ための線幅(mm)を返す（T-137、殿裁定2026-07-31）。
    /// <para>
    /// 呼び出し側が <c>PushTransform</c> でズーム倍率を掛ける前提にて、その分をあらかじめ割っておく。
    /// 作図の実体（記号・配線）は縮尺に従うべきだが、<b>格子・区切り線のような補助線は縮尺に依らぬ方が
    /// 読みやすい</b>——ズームアウトすると線が細って背景に沈むため。
    /// </para>
    /// <para>
    /// <b>【射程】高倍率では一定にならぬ。</b> 描画バックエンドが <see cref="MinStrokeWidthMm"/> で
    /// 下限クランプするため（WPF版は <c>WpfRenderer.Pen</c>）、<c>baseWidthMm / zoom</c> がそれを下回る
    /// 倍率から先は太くなっていく。既定の 0.10mm なら <c>zoom &lt;= 2.0</c> までが一定の範囲。
    /// <b>それを超えてもクランプ無しの場合より常に細い</b>ゆえ、後退にはならぬ。
    /// </para>
    /// <param name="baseWidthMm">ズーム1.0のときに見せたい太さ(mm)。</param>
    /// <param name="zoom">呼び出し側が掛けるズーム倍率。0以下は退化入力として素の値を返す。</param>
    /// </summary>
    public static double ZoomInvariantWidthMm(double baseWidthMm, double zoom)
        => zoom > 0 ? baseWidthMm / zoom : baseWidthMm;

    // 破線の ON,OFF 長（線幅の倍数）。全バックエンドで同一比率にして見た目を揃える。
    public const double DashOn = 4.0, DashOff = 2.0;   // Dashed
    public const double DotOn = 1.0, DotOff = 2.0;     // Dotted

    // ===== テーマで切り替わるパレット（インスタンス）=====
    /// <summary>UI に出すテーマ名（カラーテーマ一覧の表示・永続化キー）。</summary>
    public string Name { get; init; } = "ライト";
    public string FontFamily { get; init; } = "Yu Gothic UI";
    /// <summary>線・記号・文字の前景色。</summary>
    public Color Foreground { get; init; } = Black;
    /// <summary>キャンバス背景色（画面のみ。PDFは Default を使う運用）。</summary>
    public Color Background { get; init; } = White;
    /// <summary>作図ガイドの薄い格子色。</summary>
    public Color GridColor { get; init; } = GridGray;
    /// <summary>機器表・各種表のヘッダ背景色。</summary>
    public Color TableHeaderFill { get; init; } = new(255, 230, 230, 230);

    public StrokeStyle Get(StrokeRole role) => role switch
    {
        StrokeRole.BusRail => new(Foreground, 0.35),
        StrokeRole.GroupFrame => new(Foreground, 0.18, LineStyle.Dashed),
        StrokeRole.Grid => new(GridColor, 0.10),
        _ => new(Foreground, 0.25),   // Wire / SymbolOutline
    };

    /// <summary>T-136(B)増分4: 接続点の種類→色。View層(PartEditorCanvas)に色分岐を持たせず
    /// 純粋関数として切り出すことでテスト可能にする(samurai.md「テストしにくいは設計の匂い」)。
    /// テーマ非依存の意味色ゆえインスタンスに依らずstaticでよいが、Text/Getと並びを揃えるためstaticメソッドとする。
    /// 【往復1周目訂正・家老の静的レビュー指摘】既定値へ寄せるフォールバック(`_ => PortPower`)は
    /// 「Power行そのものが削除される」誤りを隠蔽することが壊す実測で判明した(全11件PASSのまま=
    /// 検出力ゼロ)。
    /// 【往復2周目訂正・家老裁定】いったんthrow化したが撤回した——本メソッドは`PartEditorCanvas.Draw()`
    /// から呼ばれ、描画中の例外は画面が落ちパーツが失われうる(隠密指摘「開発時の実装ミスを実行時の
    /// 危険で買っておる」)。`Kind`はJSON経由で数値表記(`"kind": 99`)を許すため未知の値が実際に
    /// 届く経路がある(JsonStringEnumConverterは文字列のみ検める、JsonOptions.cs:18)。ゆえに
    /// throwではなく<see cref="PortUnknown"/>（既存配色に無いマゼンタ）へ寄せ、case行削除を
    /// 検出しつつ実行時は落とさない形とする。</summary>
    public static Color PortColor(PortKind kind) => kind switch
    {
        PortKind.Power => PortPower,
        PortKind.DrcExempt => PortDrcExempt,
        _ => PortUnknown,
    };

    public TextStyle Text(TextRole role) => role switch
    {
        TextRole.LineNumber => new(FontFamily, 2.2, Foreground, HAlign: HAlign.Center, VAlign: VAlign.Bottom),
        TextRole.DeviceName => new(FontFamily, 2.0, Foreground, HAlign: HAlign.Center, VAlign: VAlign.Bottom),
        TextRole.Title => new(FontFamily, 4.0, Foreground, Bold: true, HAlign: HAlign.Left, VAlign: VAlign.Baseline),
        // T-107: 機器シンボル直下(同一セル内)に表示するコメント。DeviceNameと対称に
        // VAlign=Topでオリジン(記号下端)から下方向へ伸ばす。色はComment(意味色、テーマ非依存)固定。
        TextRole.Comment => new(FontFamily, 2.0, Comment, HAlign: HAlign.Center, VAlign: VAlign.Top),
        _ => new(FontFamily, 2.5, Foreground, HAlign: HAlign.Left, VAlign: VAlign.Middle),
    };

    // ===== 既定テーマ＋カラーテーマのひな形 =====
    /// <summary>標準（白地・黒線）。PDF 出力は常にこれを使う（提出図面は白地・黒線が基本）。</summary>
    public static DrawingTheme Default { get; } = new();

    /// <summary>ダーク（暗い背景・明色の線）。作図エリア用のダークモード。</summary>
    public static DrawingTheme Dark { get; } = new()
    {
        Name = "ダーク",
        Foreground = new(255, 225, 225, 225),
        Background = new(255, 32, 34, 38),
        GridColor = new(255, 70, 74, 80),
        TableHeaderFill = new(255, 55, 58, 64),
    };
}
