using Ecad2.Model;

namespace Ecad2.Persistence;

/// <summary>
/// 同梱する基本図形テンプレート（接点・コイル・端子台・セレクトSW 等）。
/// 初回起動時に <see cref="PartFolderStore"/> が「図形/」へ展開する元データ。
/// 座標は PartDefinition の規約に一致: 原点 = 最左ポート境界・行中心線、+x 右 / +y 下、単位 = セル。
/// （組込み記号 <c>SymbolGlyphs</c> の中心基準座標に +0.5 して左端基準へ移したもの。）
/// Id は固定値とし、再シードや埋め込みで重複・齟齬が出ないようにする。
/// </summary>
public static class BasicPartTemplates
{
    /// <summary>a接点の固定Id。T-037往復3周目: 旧版JSON(IsOrEligible導入前)の後方互換補正
    /// (<see cref="PartFolderStore.Enumerate"/>)で参照する。</summary>
    public const string ContactNOId = "basic-contact-no";
    /// <summary>b接点の固定Id。用途は<see cref="ContactNOId"/>と同じ。</summary>
    public const string ContactNCId = "basic-contact-nc";
    /// <summary>コイルの固定Id。T-033増分4: 配置バー種別選択のシンボル表示で、既知5種を
    /// Idで判別するために公開する(ContactNOId/ContactNCIdと同じ用途)。</summary>
    public const string CoilId = "basic-coil";
    /// <summary>端子台の固定Id。用途は<see cref="CoilId"/>と同じ。</summary>
    public const string TerminalId = "basic-terminal";
    /// <summary>セレクトSWの固定Id。用途は<see cref="CoilId"/>と同じ。</summary>
    public const string SelectSwitchId = "basic-select-switch";

    // T-071: 経路B部品追加（10種）。固定Idの用途は上記5種と同じ。
    public const string PushButtonNOId = "basic-pushbutton-no";
    public const string PushButtonNCId = "basic-pushbutton-nc";
    public const string LampId = "basic-lamp";
    public const string MotorId = "basic-motor";
    public const string TimerContactNOId = "basic-timer-contact-no";
    public const string TimerContactNCId = "basic-timer-contact-nc";
    public const string TimerInstantContactNOId = "basic-timer-instant-contact-no";
    public const string TimerInstantContactNCId = "basic-timer-instant-contact-nc";
    public const string ThermalOverloadId = "basic-thermal-overload";
    public const string EmergencyStopId = "basic-emergency-stop";

    // T-133増分7: 原本の組み込みパーツ2件（thermal-relay a/b）の移植。固定Idの用途は上記と同じ。
    /// <summary>サーマルリレーa接点の固定Id。
    /// <para>
    /// <b>【名称が紛らわしい。<see cref="ThermalOverloadId"/> とは別物である】</b>
    /// 原本に「サーマル」と名の付く実体が三つあり、本件はその三番目にござる（隠密の調査、2026-08-06）。
    /// <list type="bullet">
    /// <item>単独「サーマル(OL)」＝コの字形。<see cref="ThermalOverloadId"/>（T-133増分6でメニュー再掲）</item>
    /// <item>「サーマル(OL) 2極」＝3極記号。<c>ElementKind.ThermalOverload3P</c>（T-133増分4）</item>
    /// <item><b>本件＝thermal-relay a/b</b>。円2つ＋横線＋×印の接点記号（原本の <c>.gcadpart</c> 実体）</item>
    /// </list>
    /// <b>前二者は組込み描画（<c>SymbolGlyphs</c>）にて実装形態からして別系統</b>ゆえ、Id も表示名も
    /// 取り違えぬよう区別できる語を採ってある。
    /// </para></summary>
    public const string ThermalRelayNOId = "basic-thermal-relay-no";
    /// <summary>サーマルリレーb接点の固定Id。用途・注意は<see cref="ThermalRelayNOId"/>と同じ。</summary>
    public const string ThermalRelayNCId = "basic-thermal-relay-nc";

    /// <summary>2端子（左=NetA / 右=NetB）の標準ポート。1セル幅の図形で共通。</summary>
    private static List<PortDef> TwoPorts() => new()
    {
        new PortDef("L", 0, 0),
        new PortDef("R", 0, 1),
    };

    /// <summary>三相モータの3端子（U/V/W、境界0/1/2）。ElementCatalog.Ports(Motor,...)と同型。
    /// <para>
    /// T-136(B)増分5 論点5（殿裁定2026-08-02）: <b>モータのみ青、3端子とも同色</b>。
    /// 組込み側（<c>ElementCatalog</c>）のモータと同じ扱いに揃えてある。
    /// 他の14件は赤＝<see cref="PortDef"/> の既定値のまま（明示引数は置かず、
    /// <c>T136Increment5PortKindAssignmentTests</c> で固定する）。
    /// </para></summary>
    private static List<PortDef> MotorPorts() => new()
    {
        new PortDef("U", 0, 0, PortKind.DrcExempt),
        new PortDef("V", 0, 1, PortKind.DrcExempt),
        new PortDef("W", 0, 2, PortKind.DrcExempt),
    };

    /// <summary>同梱テンプレート一式（基本図形）。</summary>
    public static IReadOnlyList<PartDefinition> All() => new[]
    {
        ContactNO(),
        ContactNC(),
        Coil(),
        Terminal(),
        SelectSwitch(),
        PushButtonNO(),
        PushButtonNC(),
        Lamp(),
        Motor(),
        TimerContactNO(),
        TimerContactNC(),
        TimerInstantContactNO(),
        TimerInstantContactNC(),
        ThermalOverload(),
        EmergencyStop(),
        ThermalRelayNO(),
        ThermalRelayNC(),
    };

    // a接点(NO): 2ブレード＋左右リード
    private static PartDefinition ContactNO() => new()
    {
        Id = ContactNOId,
        Name = "a接点",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.ContactNO,
        IsOrEligible = true,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.342, 0),
            new PartLine(0.658, 0, 1.0, 0),
            new PartLine(0.342, -0.317, 0.342, 0.317),
            new PartLine(0.658, -0.317, 0.658, 0.317),
        },
    };

    // b接点(NC): 斜線＋左右リード＋2ブレード（b接点NEW.gcadpart 由来）
    private static PartDefinition ContactNC() => new()
    {
        Id = ContactNCId,
        Name = "b接点",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.ContactNC,
        IsOrEligible = true,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0.92, -0.42, 0.08, 0.42),       // 斜線
            new PartLine(0.3125, 0, 0, 0),               // 左リード
            new PartLine(0.6875, 0, 1, 0),               // 右リード
            new PartLine(0.6875, 0, 0.6875, -0.3125),    // 右ブレード上
            new PartLine(0.6875, 0, 0.6875, 0.3125),     // 右ブレード下
            new PartLine(0.3125, 0, 0.3125, -0.3125),    // 左ブレード上
            new PartLine(0.3125, 0, 0.3125, 0.3125),     // 左ブレード下
        },
    };

    // コイル: 円＋左右リード
    private static PartDefinition Coil() => new()
    {
        Id = CoilId,
        Name = "コイル",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.Coil,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.08, 0),
            new PartLine(0.92, 0, 1.0, 0),
            new PartCircle(0.5, 0, 0.420),
        },
    };

    // 端子台: 小円＋斜線＋左右リード
    private static PartDefinition Terminal() => new()
    {
        Id = TerminalId,
        Name = "端子台",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.Terminal,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.35, 0),
            new PartLine(0.65, 0, 1.0, 0),
            new PartCircle(0.5, 0, 0.15),
            new PartLine(0.33, 0.17, 0.67, -0.17),
        },
    };

    // セレクトSW: 2端子円＋切替バー（接点として扱う・セレクトSW-NEW.gcadpart 由来）
    private static PartDefinition SelectSwitch() => new()
    {
        Id = SelectSwitchId,
        Name = "セレクトSW",
        WidthCells = 1,
        HeightCells = 1,
        // T-061 A-1構造対処: 旧Role=ContactNOは「電気的にはa接点と同一」という当初設計だったが、
        // ノッチ位置による排他導通判定(Evaluator.IsConducting)にElementKind.SelectSwitch専用分岐が
        // 到達できなくなっていた。専用Roleへ変更しマッピングを完成させる
        // (docs/ecad2-t061-a1-select-switch-design-onmitsu.md 2-1節)。
        Role = PartRole.SelectSwitch,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartCircle(0.875, 0, 0.125),             // 右端子円
            new PartCircle(0.125, 0, 0.125),             // 左端子円
            new PartLine(0.25, 0, 0.375, 0),             // 左リード
            new PartLine(0.5625, 0, 0.5625, -0.1875),    // 右接片（縦）
            new PartLine(0.375, 0, 0.4375, 0),           // 中央リード
            new PartLine(0.4375, 0, 0.4375, -0.1875),    // 左接片（縦）
            new PartLine(0.5625, 0, 0.75, 0),            // 右リード
        },
    };

    // T-071: 経路B部品追加（10種）。座標は上記コメント規約どおり、SymbolGlyphs.cs（中心cx=width/2=0.5
    // 基準）の各記号ローカル座標に x+0.5（y はそのまま）して左端基準へ変換したもの
    // （着手前調査 docs/ecad2-t071-part-addition-design-onmitsu2.md 2節のグループ分けに対応）。

    // 押釦(NO): 端子円(中心線上)＋上の可動バー＋ステム（SymbolGlyphs.PushButtonNO由来）
    private static PartDefinition PushButtonNO() => new()
    {
        Id = PushButtonNOId,
        Name = "押釦NO",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.InputNO,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.08, 0),
            new PartLine(0.92, 0, 1.0, 0),
            new PartCircle(0.220, 0, 0.140),
            new PartCircle(0.780, 0, 0.140),
            new PartLine(0.08, -0.280, 0.92, -0.280),
            new PartLine(0.5, -0.280, 0.5, -0.420),
        },
    };

    // 押釦(NC): 端子円(中心線上)＋下の橋絡バー＋ステム（SymbolGlyphs.PushButtonNC由来）
    private static PartDefinition PushButtonNC() => new()
    {
        Id = PushButtonNCId,
        Name = "押釦NC",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.InputNC,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.04, 0),
            new PartLine(0.96, 0, 1.0, 0),
            new PartCircle(0.193, 0, 0.153),
            new PartCircle(0.807, 0, 0.153),
            new PartLine(0.04, 0.153, 0.96, 0.153),
            new PartLine(0.5, 0.153, 0.5, -0.307),
        },
    };

    // 表示灯: 円＋外向き4放射線（SymbolGlyphs.Lamp由来）
    private static PartDefinition Lamp() => new()
    {
        Id = LampId,
        Name = "表示灯",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.Lamp,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.177, 0),
            new PartLine(0.823, 0, 1.0, 0),
            new PartCircle(0.5, 0, 0.323),
            new PartLine(0.775, 0.275, 0.92, 0.420),
            new PartLine(0.225, 0.275, 0.08, 0.420),
            new PartLine(0.225, -0.275, 0.08, -0.420),
            new PartLine(0.775, -0.275, 0.92, -0.420),
        },
    };

    // 三相モータ: 大円＋左に縦3端子(⊘)＋リード（SymbolGlyphs.Motor由来。3セル幅・非シミュレート、
    // 3端子(U/V/W)のためTwoPorts()は使えずMotorPorts()を使う。座標系はSymbolGlyphs.Motorが
    // 中心オフセットを使わない左端原点方式のためx+0.5変換は不要、そのまま移植）。
    private static PartDefinition Motor() => new()
    {
        Id = MotorId,
        Name = "モータ",
        WidthCells = 3,
        HeightCells = 1,
        Role = PartRole.NonSimulated,
        Ports = MotorPorts(),
        Primitives =
        {
            new PartLine(0, -1, 0.12, -1),
            new PartCircle(0.30, -1, 0.18),
            new PartLine(0.12, -0.82, 0.48, -1.18),

            new PartLine(0, 0, 0.12, 0),
            new PartCircle(0.30, 0, 0.18),
            new PartLine(0.12, 0.18, 0.48, -0.18),

            new PartLine(0, 1, 0.12, 1),
            new PartCircle(0.30, 1, 0.18),
            new PartLine(0.12, 1.18, 0.48, 0.82),

            new PartCircle(2.05, 0, 0.92),
            new PartLine(0.48, 0, 1.13, 0),
            new PartLine(0.48, -1, 1.25, -0.46),
            new PartLine(0.48, 1, 1.25, 0.46),
        },
    };

    // タイマ接点(NO・限時): 端子円＋上の限時バー＋上向き△（SymbolGlyphs.TimerContactNO由来）
    private static PartDefinition TimerContactNO() => new()
    {
        Id = TimerContactNOId,
        Name = "タイマ接点NO",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.TimerContactNO,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.04, 0),
            new PartLine(0.96, 0, 1.0, 0),
            new PartCircle(0.193, 0, 0.153),
            new PartCircle(0.807, 0, 0.153),
            new PartLine(0.04, -0.245, 0.96, -0.245),
            new PartLine(0.5, -0.399, 0.411, -0.245),
            new PartLine(0.5, -0.399, 0.589, -0.245),
        },
    };

    // タイマ接点(NC・限時): 端子円＋下の限時バー＋下向き△（SymbolGlyphs.TimerContactNC由来）
    private static PartDefinition TimerContactNC() => new()
    {
        Id = TimerContactNCId,
        Name = "タイマ接点NC",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.TimerContactNC,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.04, 0),
            new PartLine(0.96, 0, 1.0, 0),
            new PartCircle(0.193, 0, 0.153),
            new PartCircle(0.807, 0, 0.153),
            new PartLine(0.04, 0.153, 0.96, 0.153),
            new PartLine(0.5, 0, 0.411, 0.153),
            new PartLine(0.5, 0, 0.589, 0.153),
        },
    };

    // タイマ瞬時接点(NO): 記号は通常接点と同形（JIS慣行、SymbolGlyphs.csコメント準拠）。
    // 機器名(TIM)で判別するため図形はContactNOと同一、Role/Idのみ別。
    private static PartDefinition TimerInstantContactNO() => new()
    {
        Id = TimerInstantContactNOId,
        Name = "タイマ瞬時接点NO",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.TimerInstantContactNO,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.342, 0),
            new PartLine(0.658, 0, 1.0, 0),
            new PartLine(0.342, -0.317, 0.342, 0.317),
            new PartLine(0.658, -0.317, 0.658, 0.317),
        },
    };

    // タイマ瞬時接点(NC): 記号は通常接点と同形（上記と同じ理由でContactNCと同一図形）。
    private static PartDefinition TimerInstantContactNC() => new()
    {
        Id = TimerInstantContactNCId,
        Name = "タイマ瞬時接点NC",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.TimerInstantContactNC,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0.92, -0.42, 0.08, 0.42),
            new PartLine(0.3125, 0, 0, 0),
            new PartLine(0.6875, 0, 1, 0),
            new PartLine(0.6875, 0, 0.6875, -0.3125),
            new PartLine(0.6875, 0, 0.6875, 0.3125),
            new PartLine(0.3125, 0, 0.3125, -0.3125),
            new PartLine(0.3125, 0, 0.3125, 0.3125),
        },
    };

    // サーマル(OL): コの字形（SymbolGlyphs.Thermal由来。専用DXF未提供のため暫定形、同コメント準拠）
    private static PartDefinition ThermalOverload() => new()
    {
        Id = ThermalOverloadId,
        Name = "サーマル",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.ThermalOverload,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.24, 0),
            new PartLine(0.76, 0, 1.0, 0),
            new PartLine(0.24, 0, 0.24, -0.30),
            new PartLine(0.24, -0.30, 0.76, -0.30),
            new PartLine(0.76, -0.30, 0.76, 0),
        },
    };

    // 非常停止: 押釦(NC形)＋ドーム(キノコ頭)（SymbolGlyphs.EmergencyStop由来）
    private static PartDefinition EmergencyStop() => new()
    {
        Id = EmergencyStopId,
        Name = "非常停止",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.EmergencyStop,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartLine(0, 0, 0.04, 0),
            new PartLine(0.96, 0, 1.0, 0),
            new PartCircle(0.193, 0, 0.153),
            new PartCircle(0.807, 0, 0.153),
            new PartLine(0.04, 0.153, 0.96, 0.153),
            new PartLine(0.5, 0.153, 0.5, -0.380),
            new PartPolyline(new[]
            {
                0.127, -0.239, 0.189, -0.289, 0.261, -0.329, 0.340, -0.358, 0.424, -0.375,
                0.510, -0.380, 0.596, -0.372, 0.679, -0.352, 0.756, -0.321, 0.826, -0.278, 0.873, -0.239,
            }),
        },
    };

    // T-133増分7: 原本の組み込みパーツ2件（thermal-relay a/b）。
    //
    // 【座標は変換せず原本のまま移した】上記T-071の10種は SymbolGlyphs（中心基準）由来ゆえ x+0.5 の
    // 変換を要したが、本件は原本の .gcadpart（JSON実体）由来にて、その座標規約は PartDefinition と
    // 同一である——ecad2 は原本の .gcadpart をそのまま逆シリアライズできる設計になっており
    // （PartDefinition.cs:85「旧 .gcadpart に本フィールドが無ければ既定値のまま読まれる」）、
    // JSONのフィールド名も PartCircle/PartLine のプロパティと一致する。ゆえに変換は要らぬ。
    //
    // 【a と b は y の符号だけが違う同型である】どちらも「円2つ＋横線1本＋斜線2本（×印）」の計5件で、
    // a は横線が上側(y=-0.1875)・b は下側(y=+0.1875)。×印は横線の上下に在るのではなく、
    // 横線を中心にまたぐ（a なら y=-0.3125〜-0.0625、その中心が横線の -0.1875）。
    // ※下ごしらえの調査書には「a=斜線1本 / b=斜線2本」「×印は横線の上／下」とあるが、
    //   原本を直読した結果は上記のとおり（侍、2026-08-06）。両調査書の結論（別実体・移植可）は正しい。
    //
    // 【primitives の並びは原本のまま保つ】a は横線が3番目・b は5番目と原本自体が不揃いだが、
    // 線と円のみで塗りが無いゆえ描画順は見た目に響かぬ。移植の忠実さを優先し、揃えずに写した。

    // サーマルリレーa: 端子円2つ＋上側の横線＋×印（thermal-relay-a.gcadpart 由来。role=contactNO）
    private static PartDefinition ThermalRelayNO() => new()
    {
        Id = ThermalRelayNOId,
        Name = "サーマルリレーa",
        WidthCells = 1,
        HeightCells = 1,
        // 原本の role フィールドどおり。図面上の記号だけがサーマル用に異なり、電気的な振る舞いは
        // 通常のa接点と同一である（隠密の原本調査）。PartRole.ThermalOverload は上記の
        // 「サーマル(OL)」（コの字形）専用ゆえ、こちらへは用いぬ。
        //
        // 【IsOrEligible を付けぬのは意図である。付け忘れではない】Role=ContactNO を持ちながら
        // OR対象でないのは本テンプレート群で初の組み合わせにて、読み手が疑いやすい箇所ゆえ書き残す。
        // IsOrEligible は Role とは独立の明示フラグであり（PartDefinition.cs:78-81、T-037往復2周目）、
        // 原本にも該当フィールドは無い。付ければ部品リストへ ORa/ORb の論理エントリが増えて
        // 表示件数が19ではなく21になる（PartPaletteViewModel.cs:75-76）。
        Role = PartRole.ContactNO,
        Ports = TwoPorts(),
        Primitives =
        {
            new PartCircle(0.875, 0, 0.125),                  // 右端子円
            new PartCircle(0.125, 0, 0.125),                  // 左端子円
            new PartLine(0.125, -0.1875, 0.875, -0.1875),     // 横線（上側）
            new PartLine(0.625, -0.3125, 0.375, -0.0625),     // ×印（右上→左下）
            new PartLine(0.375, -0.3125, 0.625, -0.0625),     // ×印（左上→右下）
        },
    };

    // サーマルリレーb: 端子円2つ＋下側の横線＋×印（thermal-relay-b.gcadpart 由来。role=contactNC）
    private static PartDefinition ThermalRelayNC() => new()
    {
        Id = ThermalRelayNCId,
        Name = "サーマルリレーb",
        WidthCells = 1,
        HeightCells = 1,
        Role = PartRole.ContactNC,     // 原本の role フィールドどおり。理由は ThermalRelayNO と同じ
        Ports = TwoPorts(),
        Primitives =
        {
            new PartCircle(0.875, 0, 0.125),                  // 右端子円
            new PartCircle(0.125, 0, 0.125),                  // 左端子円
            new PartLine(0.625, 0.0625, 0.375, 0.3125),       // ×印（右上→左下）
            new PartLine(0.375, 0.0625, 0.625, 0.3125),       // ×印（左上→右下）
            new PartLine(0.125, 0.1875, 0.875, 0.1875),       // 横線（下側）
        },
    };
}
