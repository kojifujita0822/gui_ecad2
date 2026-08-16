namespace Ecad2.Model;

/// <summary>
/// 組込みパーツ（<c>BasicPartTemplates</c> が同梱する）の固定Id。<b>綴りの本体はここに在る。</b>
/// <para>
/// 【なぜ <c>Model</c> 層に置くか】組込みか自作かの弁別が <see cref="PartResolver"/> で要るようになった
/// （T-151期・ラベル既定オフセットの適用可否）。<c>BasicPartTemplates</c> は <c>Persistence</c> 層にて
/// <c>using Ecad2.Model;</c> しておるゆえ、<c>Model</c> から参照し返せば依存が相互に絡む。
/// <b>ゆえに綴りだけを下層へ移し、<c>BasicPartTemplates</c> の既存定数はここへ転送する形に改めた。</b>
/// </para>
/// <para>
/// 【綴りを複製せぬのが要】<c>ElementCatalog.MotorPartId</c> は同じ事情から綴りを二重に持っており、
/// <b>食い違えば迂回が黙って効かなくなる</b>という罠を抱えておった（T-145のdocコメントが警告しておる）。
/// 本クラスの新設に合わせて、そちらも転送へ改めてある——<b>二重管理を17件へ広げるのではなく、
/// 元から在った1件も畳んだ</b>。
/// </para>
/// <para>
/// 【弁別に用いる際の注意】本集合は<b>フォルダの物理配置（Category）に依らぬ</b>。手で「図形/」直下へ
/// 置かれた自作パーツも、組込みをコピーして再採番されたパーツ（T-035）も、Idが本集合に無い以上
/// 自作として扱われる。これは家老裁定2026-08-15の方式をそのまま踏襲したもの。
/// </para>
/// <para>
/// <b>【追加時の作法】</b>組込みパーツを増やすときは、<c>BasicPartTemplates.All()</c> と本クラスの
/// 両方へ足す要がある。<b>片方だけでは黙って自作扱いになる</b>——ラベルの既定オフセットが外れ、
/// 図面への埋め込み対象にもなる。<c>BuiltinPartIdsConsistencyTests</c> が両者の一致を固定しており、
/// 足し忘れれば必ず落ちる。
/// </para>
/// </summary>
public static class BuiltinPartIds
{
    public const string ContactNO = "basic-contact-no";
    public const string ContactNC = "basic-contact-nc";
    public const string Coil = "basic-coil";
    public const string Terminal = "basic-terminal";
    public const string SelectSwitch = "basic-select-switch";
    public const string PushButtonNO = "basic-pushbutton-no";
    public const string PushButtonNC = "basic-pushbutton-nc";
    public const string Lamp = "basic-lamp";
    public const string Motor = "basic-motor";
    public const string TimerContactNO = "basic-timer-contact-no";
    public const string TimerContactNC = "basic-timer-contact-nc";
    public const string TimerInstantContactNO = "basic-timer-instant-contact-no";
    public const string TimerInstantContactNC = "basic-timer-instant-contact-nc";
    public const string ThermalOverload = "basic-thermal-overload";
    public const string EmergencyStop = "basic-emergency-stop";
    public const string ThermalRelayNO = "basic-thermal-relay-no";
    public const string ThermalRelayNC = "basic-thermal-relay-nc";

    /// <summary>全組込みId。並びは現在 <c>BasicPartTemplates.All()</c> と同じにしてある——照合を目で
    /// 追えるようにという便宜にて、<b>順序そのものは要件ではない</b>（テストも集合と員数のみを固定しておる）。</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        ContactNO, ContactNC, Coil, Terminal, SelectSwitch,
        PushButtonNO, PushButtonNC, Lamp, Motor,
        TimerContactNO, TimerContactNC, TimerInstantContactNO, TimerInstantContactNC,
        ThermalOverload, EmergencyStop, ThermalRelayNO, ThermalRelayNC,
    };

    private static readonly HashSet<string> Set = new(All, StringComparer.Ordinal);

    /// <summary>その PartId が組込みパーツのものか。null・空は false（Kind経路には組込み／自作の概念が無い）。</summary>
    public static bool Contains(string? partId) => partId is not null && Set.Contains(partId);
}
