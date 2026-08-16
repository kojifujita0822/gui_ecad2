namespace Ecad2.Model;

/// <summary>
/// 要素の接続点・電気的役割を「組込み種別」と「自作パーツ」で統一的に解決する。
/// 自作パーツ（<see cref="ElementInstance.PartId"/> 指定）は <see cref="PartLibrary"/> から、
/// それ以外は <see cref="ElementCatalog"/> から取得する。
/// </summary>
public static class PartResolver
{
    public static IReadOnlyList<PortDef> Ports(ElementInstance e, PartLibrary? lib)
    {
        var part = lib?.Get(e.PartId);
        if (part is not null) return part.Ports;
        return ElementCatalog.Ports(e.Kind, Math.Max(1, e.CellWidth));
    }

    /// <summary>要素が占める列境界の左端・右端（最左/最右ポート境界）。</summary>
    public static (int Left, int Right) BoundarySpan(ElementInstance e, PartLibrary? lib)
    {
        var ports = Ports(e, lib);
        // 接続点を持たない記号（主回路3極記号・ゼロポート自作パーツ）は CellWidth で占有幅を決める。
        if (ports.Count == 0) return (e.Pos.Column, e.Pos.Column + Math.Max(1, e.CellWidth));
        int min = ports[0].BoundaryOffset, max = ports[0].BoundaryOffset;
        foreach (var p in ports) { min = Math.Min(min, p.BoundaryOffset); max = Math.Max(max, p.BoundaryOffset); }
        return (e.Pos.Column + min, e.Pos.Column + max);
    }

    /// <summary>ネットリストの電気要素(Component)を生成するか。false は記号のみ（非シミュレート）。</summary>
    public static bool CreatesComponent(ElementInstance e, PartLibrary? lib)
    {
        var part = lib?.Get(e.PartId);
        if (part is not null) return part.Role != PartRole.NonSimulated;
        return ElementCatalog.CreatesComponent(e.Kind);
    }

    /// <summary>
    /// ネットリストの結線へ参加するか。false は<b>非シミュレートの部品</b>——
    /// 組込みの三相モータ（T-136(C)、殿裁定2026-08-02）と、
    /// 自作パーツの <see cref="PartRole.NonSimulated"/>（T-142、殿ご下命2026-08-02）。
    /// <para>
    /// <b>なぜ結線から外すか</b>：非シミュレート部品は左右ポートを繋ぐ union
    /// （<see cref="Ecad2.Simulation.NetlistBuilder"/> の通過接続要素の枝）が
    /// <see cref="CreatesComponent"/> を見るゆえ通らず、左右のポートが結ばれぬ。
    /// にもかかわらず母線への直接 union と横配線の結合には参加するため、
    /// <b>行の最左（最右）に居ると母線接続を横取りし、同じ行の負荷を切り離しておった</b>
    /// ——DRC が <c>DRC-LOAD-001/002</c> を誤って鳴らす
    /// （<c>tests/Ecad2.Core.Tests/T136C{,Custom}NonSimulatedWiringTests.cs</c> で実測）。
    /// </para>
    /// <para>
    /// <b>組込みと自作で判定の軸が違う</b>のは、殿裁定が組込み側を
    /// <see cref="ElementKind.Motor"/> ただ一つに限られたゆえである
    /// （他の非シミュレート組込み＝主回路3極記号は接続点を0個しか持たず、元より結線に加わらぬ）。
    /// </para>
    /// <para>
    /// 生の <see cref="ElementInstance.Kind"/> でなく本メソッドを経るのは、上の <see cref="Ports"/>・
    /// <see cref="CreatesComponent"/>・<see cref="SheetAffinityOf"/> と同じ二分岐の形に揃えるためである
    /// ——自作パーツの <see cref="ElementInstance.Kind"/> は定義と食い違いうる。
    /// </para></summary>
    public static bool ParticipatesInWiring(ElementInstance e, PartLibrary? lib)
    {
        var part = lib?.Get(e.PartId);
        if (part is not null) return part.Role != PartRole.NonSimulated;
        return ElementCatalog.ParticipatesInWiring(e.Kind);
    }

    /// <summary>要素が置けるシートの種別（T-136(A)増分1）。自作パーツは
    /// <see cref="PartDefinition.SheetAffinity"/>、組込み種別は <see cref="ElementCatalog.SheetAffinityOf"/> から。
    /// <para>
    /// 本メソッドがあることで、呼び出し側（配置・移動の関門）は<b>組込みか自作かを知らずに済む</b>。
    /// 台帳の「T-133裁定4もこの箱に乗る」という物差しは、箱を <see cref="PartDefinition"/> と読めば
    /// 3極記号へ届かぬが、<b>箱を本クラスと読めば届く</b>——上の <see cref="Ports"/>・
    /// <see cref="CreatesComponent"/>・<see cref="ComponentKind"/> と同じ二分岐の形にしてある。
    /// </para>
    /// <para>
    /// 未解決の PartId（<see cref="IsUnresolvedPartId"/>）は <see cref="ElementCatalog.SheetAffinityOf"/> へ
    /// フォールバックする——<see cref="ComponentKind"/> が同じ場合に <see cref="ElementInstance.Kind"/> へ
    /// 静かに落ちるのと同じ扱いにて、ライブラリを失った要素が移動すらできなくなるのを避ける。
    /// </para></summary>
    public static SheetAffinity SheetAffinityOf(ElementInstance e, PartLibrary? lib)
    {
        var part = lib?.Get(e.PartId);
        if (part is not null) return part.SheetAffinity;
        return ElementCatalog.SheetAffinityOf(e.Kind);
    }

    /// <summary>その種別のシートへ置けるか（T-136(A)増分1）。
    /// <paramref name="mainCircuit"/> は <see cref="Sheet.MainCircuit"/> をそのまま渡す。</summary>
    public static bool IsAllowedOnSheet(SheetAffinity affinity, bool mainCircuit) => affinity switch
    {
        SheetAffinity.ControlOnly => !mainCircuit,
        SheetAffinity.MainCircuitOnly => mainCircuit,
        _ => true,   // Any
    };

    /// <summary>PartId が設定されているのに <paramref name="lib"/> で解決できないか（未解決参照）。
    /// true の場合 <see cref="ComponentKind"/> は静かに <see cref="ElementInstance.Kind"/>
    /// （既定値=ContactNO）へフォールバックする（<see cref="Ecad2.Simulation.DesignRuleCheck.CheckUnresolvedPartId"/> 対象）。</summary>
    public static bool IsUnresolvedPartId(ElementInstance e, PartLibrary? lib) =>
        !string.IsNullOrEmpty(e.PartId) && lib?.Get(e.PartId) is null;

    /// <summary>Component に用いる種別。自作パーツは役割から組込み種別へ写像（Evaluator が評価できる形）。</summary>
    public static ElementKind ComponentKind(ElementInstance e, PartLibrary? lib)
    {
        var part = lib?.Get(e.PartId);
        if (part is null) return e.Kind;
        return part.Role switch
        {
            PartRole.ContactNO => ElementKind.ContactNO,
            PartRole.ContactNC => ElementKind.ContactNC,
            PartRole.Coil => ElementKind.Coil,
            PartRole.Lamp => ElementKind.Lamp,
            PartRole.Terminal => ElementKind.Terminal,
            PartRole.InputNO => ElementKind.PushButtonNO,
            PartRole.InputNC => ElementKind.PushButtonNC,
            PartRole.TimerContactNO => ElementKind.TimerContactNO,
            PartRole.TimerContactNC => ElementKind.TimerContactNC,
            PartRole.TimerInstantContactNO => ElementKind.TimerInstantContactNO,
            PartRole.TimerInstantContactNC => ElementKind.TimerInstantContactNC,
            PartRole.ThermalOverload => ElementKind.ThermalOverload,
            PartRole.EmergencyStop => ElementKind.EmergencyStop,
            // T-061 A-1構造対処: SelectSwitchロールをElementKind.SelectSwitchへマッピングし、
            // Evaluator.IsConducting/NetlistBuilderのノッチ判定を到達可能にする。
            PartRole.SelectSwitch => ElementKind.SelectSwitch,
            _ => throw new InvalidOperationException(
                $"ComponentKind called for role '{part.Role}'. Check CreatesComponent before calling ComponentKind."),
        };
    }

    /// <summary>
    /// 機器名ラベルの位置を決めるのに用いる種別（T-145、殿ご下命2026-08-06）。
    /// <para>
    /// <b>【<see cref="ComponentKind"/> と別に要る理由】</b>あちらは「ネットリストの電気要素として
    /// 何か」を答えるゆえ、<see cref="PartRole.NonSimulated"/> には答が無く例外を投げる。
    /// <b>されどラベルは非シミュレートの記号にも付く</b>——組込みモータがまさにそれである。
    /// </para>
    /// <para>
    /// <b>【モータだけを迂回する】</b><c>PartId</c> 経路で置かれた要素の
    /// <see cref="ElementInstance.Kind"/> は既定値 <see cref="ElementKind.ContactNO"/> のままにて
    /// （T-046由来の構造的制約）、そのままでは<b>モータのラベルが接点用の位置へ寄る</b>。
    /// <b>殿裁定は「根であるT-046の構造は断たず、モータに限って迂回する」</b>——
    /// <b>承知の短所として、同じ構造から出る別の不具合は残る</b>（ラベル以外に及ぶかは未調査）。
    /// </para>
    /// <para>
    /// <b>【未解決の PartId は迂回せぬ】</b>ライブラリで実体を引けぬときは絵の側も
    /// <see cref="ElementInstance.Kind"/> で描かれるゆえ、ラベルだけモータの位置へ寄れば
    /// <b>絵と名が食い違う</b>。実体を引けることを条件にしてある。
    /// </para></summary>
    public static ElementKind LabelKind(ElementInstance e, PartLibrary? lib)
    {
        if (CreatesComponent(e, lib)) return ComponentKind(e, lib);
        if (lib?.Get(e.PartId) is not null && e.PartId == ElementCatalog.MotorPartId) return ElementKind.Motor;
        return e.Kind;
    }

    /// <summary>
    /// 機器名ラベルの位置を決めるのに用いる向き（T-145）。<see cref="LabelKind"/> と対を成す。
    /// <para>
    /// <b>【PartId 経路では向きを見ぬ】</b>PartId 経路の絵は <c>PartDrawing.Draw</c> が描き、
    /// <b>あちらは向きを引数に持たぬ</b>——定義どおりの形で固定される。
    /// <b>ここでラベルだけが向きを見れば、絵は元の向きのまま名だけが動く。</b>
    /// </para>
    /// <para>
    /// <b>【この規則を一箇所に置く理由】</b>描画（<c>DiagramRenderer</c>）とプロパティパネルの
    /// 相対値（<c>MainWindowViewModel</c>）が同じ規則で解かねば、<b>入力欄に0と出ておるのに
    /// 絵は別の場所という食い違いが生じる</b>。<see cref="LabelKind"/> と揃えてここへ置いた。
    /// </para></summary>
    public static string? LabelOrient(ElementInstance e, PartLibrary? lib)
        => lib?.Get(e.PartId) is not null ? null : e.Params.GetValueOrDefault(ParamKeys.Orient);

    /// <summary>
    /// 機器名ラベルの既定高さオフセット(mm、正で上)。要素に <c>Params["LabelDy"]</c> が無い場合に使う値。
    /// <para>
    /// <b>【自作パーツには種別既定を適用せぬ・殿ご裁可2026-08-16（T-151）】</b>
    /// <see cref="ElementCatalog.DefaultLabelDy"/> の値は<b>組込み記号の幾何に較正されたもの</b>にて、
    /// たとえばコイルの -5.72 は「組込みコイルの丸の中心」を指す（原本GuiEcadのコメント
    /// 「コイルの丸の中あたりに表示（重なり回避）」がその意図を明言しておる）。<b>自作パーツは絵が違う</b>
    /// ゆえ同じ位置が図形と重なる——実際、<c>Role=Coil</c> の自作ソレノイド（折れ線）で機器名が絵に重なった。
    /// </para>
    /// <para>
    /// <b>【機構は元より正しかった】</b><see cref="LabelKind"/> が Role 由来の種別を返すこと自体は
    /// 意図どおりにござる。噛み合わなんだのは<b>較正値と実形状</b>——ゆえに直すのは種別の解決ではなく、
    /// 較正値を当てはめてよい相手の側にござる。
    /// </para>
    /// <para>
    /// <b>【なぜ二箇所でなくここへ集めたか】</b>この既定値の消費者は<b>描画</b>（<c>DiagramRenderer</c>）と
    /// <b>プロパティパネルの相対値の基準</b>（<c>MainWindowViewModel.DefaultLabelDyOf</c>）の二つあり、
    /// T-145 が「片方だけ直せば、入力欄に0と出ておるのに絵は別の場所という食い違いが生じる」として
    /// 一元化した経緯がある。<b>その一元化を崩さぬよう、分岐そのものを両者の手前へ置いた</b>
    /// ——呼び手が二つに分かれておっても、規則は一つしか無い形にござる。
    /// </para>
    /// <para>
    /// <b>【弁別の範囲】</b>自作と判ずるのは<b>ライブラリで実体を引けて、かつIdが組込みの固定Id集合に
    /// 無い</b>場合のみ。未解決の <c>PartId</c>（<see cref="IsUnresolvedPartId"/>）と <c>Kind</c> 経路は
    /// <b>従来どおり</b>にござる——前者は <c>DRC-PART-001</c> が警告しておる最中の要素にて、
    /// 見た目まで変えれば別の混乱を招くゆえ。判定に Category（フォルダの物理配置）を用いておらぬのは
    /// 家老裁定2026-08-15の方式に従うたもの（<see cref="BuiltinPartIds"/> 参照）。
    /// </para></summary>
    public static double DefaultLabelDy(ElementInstance e, PartLibrary? lib)
    {
        bool isCustomPart = lib?.Get(e.PartId) is not null && !BuiltinPartIds.Contains(e.PartId);
        if (isCustomPart) return 0.0;
        return ElementCatalog.DefaultLabelDy(LabelKind(e, lib), LabelOrient(e, lib));
    }
}
