namespace Ecad2.Model;

/// <summary>要素種別ごとの既定値・分類。</summary>
public static class ElementCatalog
{
    /// <summary>既定の占有セル数。</summary>
    public static int DefaultCellWidth(ElementKind kind) => kind switch
    {
        // セレクトSWはノッチ毎の2端子接点（1セル）。位置は ElementInstance.Params["Position"]。
        ElementKind.Motor => 3,   // 三相 U/V/W の3端子を横並びに確保（暫定レイアウト）
        // 主回路3極記号は 2×2 セル（sample.png 準拠）。
        ElementKind.Breaker3P or ElementKind.ContactorMain3P or ElementKind.ThermalOverload3P => 2,
        _ => 1,
    };

    /// <summary>既定の占有セル高さ（T-133増分2）。縦2セルを占める記号のために置く。
    /// <para>
    /// <b>増分2の時点では呼び出し元が無かった（意図的）</b>——判定へ通すのは増分3（占有・ヒットテストの
    /// 縦方向対応）、配置時に入れるのは増分4（<c>Kind</c> 経路の新設）という段取りゆえ。
    /// <b>T-133増分4-B で <c>MainWindowViewModel.PlaceElementAtSelectedCell(ElementKind, string?)</c> が
    /// 呼び手となり、結線を果たした</b>（<see cref="DefaultCellWidth"/> も同じくこの経路が初めての呼び手。
    /// 既存の配置はすべて <c>PartId</c> 経由で <c>PartDefinition</c> から幅を取っておった）。
    /// <c>docs/proposed.md</c> P-143「型はあるが結線が無い」を残さぬよう、結線先を先に書き留めておいたもの。
    /// </para></summary>
    public static int DefaultCellHeight(ElementKind kind) => kind switch
    {
        // 三相モータの記号は縦2セル分を占める（計画書§2-2、描画の実測による）。
        ElementKind.Motor => 2,
        // 主回路3極記号は 2×2 セル（sample.png 準拠）。上の DefaultCellWidth の 2 と対になる。
        ElementKind.Breaker3P or ElementKind.ContactorMain3P or ElementKind.ThermalOverload3P => 2,
        _ => 1,
    };

    /// <summary>組込み種別が置けるシートの種別（T-136(A)増分1）。
    /// <para>
    /// <b>主回路3極記号3種を主回路シート専用と宣言する</b>——T-133の殿裁定4（3極記号は主回路シート限定）が
    /// ここに乗る。<see cref="PartDefinition.SheetAffinity"/> は自作パーツにしか届かぬゆえ、組込み種別の側は
    /// 本メソッドが受け持ち、両者を <see cref="PartResolver.SheetAffinityOf(ElementInstance, PartLibrary)"/> が
    /// 一元的に解決する（<c>Ports</c>／<c>CreatesComponent</c>／<c>ComponentKind</c> と同じ二分岐の形）。
    /// </para>
    /// <para>
    /// <b>結線の現状</b>：移動の2経路（ドラッグ・矢印キー）には本増分で結線済み。
    /// 一方 <b>Kind 経路の配置導線はまだ無い</b>——3極記号を新規配置する導線自体が未実装（T-133増分4で新設予定）
    /// ゆえ、そちらは増分4で <c>ValidatePlacement</c> へ渡す形になる。<c>ValidatePlacement</c> の
    /// 当該引数に既定値を置いておらぬのは、その折の渡し忘れをコンパイラに検出させるため。
    /// </para></summary>
    public static SheetAffinity SheetAffinityOf(ElementKind kind) => kind switch
    {
        ElementKind.Breaker3P or ElementKind.ContactorMain3P or ElementKind.ThermalOverload3P
            => SheetAffinity.MainCircuitOnly,
        _ => SheetAffinity.Any,
    };

    /// <summary>
    /// 種別ごとの接続点（ポート）定義。実効幅 <paramref name="width"/> から端子境界を算出する。
    /// 2端子種別: 左端子=境界0／右端子=境界 width（先頭=NetA・末尾=NetB）。
    /// 三相モータ: U/V/W の3端子（境界 0/1/2）。非シミュレート（接続のみ生成）。
    /// ポートは境界オフセット昇順で返す（先頭=最左・末尾=最右）。
    /// </summary>
    public static IReadOnlyList<PortDef> Ports(ElementKind kind, int width) => kind switch
    {
        // T-136(B)増分5 論点5（殿裁定2026-08-02）: モータのみ青、3端子とも同色。他の組込み16種は
        // 赤＝PortDef の既定値のまま（明示引数は置かず、T136Increment5PortKindAssignmentTests で固定する）。
        // 青とした理由＝モータは T-136(C) で結線から外れ、いかなる母線にも繋がらぬ。ゆえに赤の定義
        // 「電源に接続される点」が実態と合わず、青の定義「DRC無効な点」が現状をそのまま言い表す。
        ElementKind.Motor => new[]
        {
            new PortDef("U", 0, 0, PortKind.DrcExempt),
            new PortDef("V", 0, 1, PortKind.DrcExempt),
            new PortDef("W", 0, 2, PortKind.DrcExempt),
        },
        // 主回路3極記号は自由配線（FreeLine）で結線するため接続点を持たない（ネットリスト非関与）。
        ElementKind.Breaker3P or ElementKind.ContactorMain3P or ElementKind.ThermalOverload3P
            => System.Array.Empty<PortDef>(),
        _ => new[]
        {
            new PortDef("L", 0, 0),
            new PortDef("R", 0, width),
        },
    };

    /// <summary>組込みモータ（<c>BasicPartTemplates</c> の「モータ」）の PartId（T-145）。
    /// <para>
    /// <b>【なぜラベルの解決に Id が要るか】</b>組込みモータは <c>PartId</c> 経路で配置されるが、
    /// その要素の <see cref="ElementInstance.Kind"/> は既定値 <see cref="ElementKind.ContactNO"/> のまま
    /// である（T-046由来の構造的制約）。<b>ゆえにラベル位置の解決が接点用の値を拾ってしまう</b>
    /// ——「モータの既定値を入れれば直る」は誤りにて、経路ごと迂回せねば届かぬ。
    /// <b>殿裁定2026-08-06＝モータに限って迂回する（根であるT-046の構造は断たぬ）。</b>
    /// </para>
    /// <para>
    /// <b>【綴りの二重管理は T-151期に解消した】</b>かつては本定数と <c>BasicPartTemplates.MotorId</c> が
    /// 各々に綴りを持ち、<b>二つが食い違えば迂回が黙って効かなくなる</b>——絵は正しいのにラベルだけ
    /// 元の位置へ戻り、テストも実機も「何も起きておらぬ」ように見える——という罠を抱えておった。
    /// <b>組込みIdの弁別が <see cref="PartResolver"/> でも要るようになったのを機に、綴りの本体を
    /// <see cref="BuiltinPartIds"/>（<c>Model</c>層）へ集め、双方をそこへの転送に改めてある。</b>
    /// </para>
    /// <para>
    /// <b>【一致を固定しておった既存テストは、検出力を失った】</b>両者が同じ const を指す以上、
    /// 食い違いは原理的に起こらぬゆえ常にGREENとなる。<b>網を消したのではなく、守るべき対象の方が
    /// 構造から消えた</b>——テストは記録として残してある。
    /// </para></summary>
    public const string MotorPartId = BuiltinPartIds.Motor;

    /// <summary>ラベル位置の解決で「縦向き」と見なす <see cref="ParamKeys.Orient"/> の値か（T-145）。
    /// <b><c>SymbolGlyphs</c> のモータ描画と同じ規則でなければ、絵とラベルがずれる</b>
    /// ——あちらは <c>Rendering</c> 層の private ゆえ共有できず、同じ規則をここへ写した
    /// （<b>既定は横向きにて、"V" のときだけ縦</b>）。</summary>
    private static bool IsMotorVertical(string? orient) => orient == "V";

    // T-145（殿ご下命2026-08-06「モーターのデバイス名が大円の中央に表示されない」）。
    // 殿裁定＝x・y両方を本体大円の中心へ寄せる。
    //
    // 【座標の出どころ】SymbolGlyphs の Motor / MotorV は「原点＝左境界・行中心、+x右・+y下」、
    // 単位 k = width/3 ＝ 1セル（モータは CellWidth=3）。本体大円の中心は
    //   横向き … (x, y) = (2.0, 0)    セル
    //   縦向き … (x, y) = (1.0, 1.75) セル
    // 一方ラベルの既定位置は、x が要素幅の中央（1.5セル）、y が行中心の Cell*0.50 上。
    //
    // 【ゆえに】
    //   dx（正で右） 横 = 2.0 - 1.5 = +0.5セル ／ 縦 = 1.0 - 1.5 = -0.5セル
    //   dy（正で上） DrawElementLabel は yn = YRow - Cell*0.50 - dy ゆえ、
    //     行中心へ置くには dy = -0.50セル、行中心の1.75セル下へ置くには dy = -(0.50 + 1.75)セル
    //
    // 【mm の生値である】既存の LabelDy（Coil の -5.72 等）と同じくセル 9mm を前提とする。
    // CellMm を変えれば合わなくなるが、ここだけセル比にすれば Params[LabelDy]（mm 絶対値）と
    // 意味論が食い違うゆえ、既存へ揃えた。
    //
    // 【コイルに要った実測補正は入れておらぬ】Coil は理論値 -5.5 から -5.72 へ、テキスト中心が
    // 円中心より約0.22mm上にずれる分を忍者の実測で補正してある（下記コメント）。同じ描画系ゆえ
    // モータにも同型のずれが出る見込みだが、値まで同じとは限らぬ——理論値を入れ、実機の画素採取で
    // 確かめる（外れておれば、その差がコイルの補正値と揃うかどうかも併せて見られる）。
    private const double MotorLabelDxHorizontal = 4.5;      // +0.5セル
    private const double MotorLabelDxVertical = -4.5;       // -0.5セル
    private const double MotorLabelDyHorizontal = -4.5;     // -0.50セル（行中心）
    private const double MotorLabelDyVertical = -20.25;     // -2.25セル（行中心の1.75セル下）

    /// <summary>
    /// 機器名ラベルの既定横オフセット(mm、正で右)。要素に <c>Params["LabelDx"]</c> が無い場合に使う。
    /// <b>T-145 で新設した</b>——それまで x は要素幅の中央に固定で、寄せる器が無かった。
    /// <b>モータ以外はすべて 0</b>（従来どおり幅の中央）にて、既存の見た目は変わらぬ。
    /// </summary>
    public static double DefaultLabelDx(ElementKind k, string? orient) => k switch
    {
        ElementKind.Motor => IsMotorVertical(orient) ? MotorLabelDxVertical : MotorLabelDxHorizontal,
        _ => 0.0,
    };

    /// <summary>
    /// 機器名ラベルの既定高さオフセット(mm、正で上)。要素に Params["LabelDy"] が無い場合に使う。
    /// 密集時の重なり回避の標準位置（実図面の規定図に基づく調整値が由来）。
    /// <para>
    /// <b>【T-145で向きを受けるようになった】</b>モータは向きによって本体大円の位置が変わるゆえ、
    /// <b>種別ごとの1値では足りぬ</b>。<b>引数を省略可能にしておらぬのは</b>、省略できる形にすると
    /// 呼び手が向きを渡し忘れても黙って旧挙動になり、<b>誤りが実機まで露見せぬ</b>ゆえである。
    /// </para></summary>
    public static double DefaultLabelDy(ElementKind k, string? orient) => k switch
    {
        ElementKind.ContactNO or ElementKind.ContactNC => -1.5,
        ElementKind.PushButtonNC => -1.5,
        ElementKind.PushButtonNO => -0.5,
        // 忍者実機実測(2026-07-15、docs-notes/ecad2-t097-verify-ninja.md観点(1))で旧値-5.5では
        // テキスト中心が円中心より約0.22mm(バウンディングボックス法-0.215mm/重心法-0.228mmの
        // 中間)上にズレていたため、その分だけラベルを下げる方向(DrawElementLabel: yn = YRow(row)
        // - Cell*0.50 - dy、dy>0で上へ)へ補正。dyを0.22減らす(より負にする)とynが0.22増え
        // (下方向)、ズレが解消される。
        ElementKind.Coil => -5.72,   // コイルの丸の中心に表示(実測補正済み、重なり回避)
        ElementKind.Lamp => -1.5,
        ElementKind.Terminal => -2.0,
        ElementKind.Motor => IsMotorVertical(orient) ? MotorLabelDyVertical : MotorLabelDyHorizontal,
        _ => 0.0,
    };

    /// <summary>ネットリストの電気要素（Component）を生成する種別か。false は記号のみ（非シミュレート）。</summary>
    public static bool CreatesComponent(ElementKind k) => IsContact(k) || IsLoad(k) || IsPassthrough(k);

    /// <summary>
    /// ネットリストの結線（ノード生成・母線接続・横配線）へ参加する種別か（T-136(C)、殿裁定2026-08-02）。
    /// <para>
    /// <b>false は三相モータのみ。</b> モータは <see cref="CreatesComponent"/> が false ゆえ Component には
    /// ならぬが、<b>それが効くのは Component 生成の段だけ</b>で、ノード生成・母線 union・横配線結合には
    /// 参加しておった。その結果、モータが行の最左（最右）に居ると母線への接続を横取りし、同じ行の負荷を
    /// 母線から切り離す——DRC が <c>DRC-LOAD-001/002</c> を誤って鳴らす（工程1の実測。
    /// <c>tests/Ecad2.Core.Tests/T136CNonSimulatedWiringTests.cs</c>）。
    /// </para>
    /// <para>
    /// 主回路3極記号（<see cref="ElementKind.Breaker3P"/> 等）は接続点を0個しか持たぬゆえ
    /// <see cref="NetlistBuilder"/> の入口で既に除外されており、本述語の対象外である
    /// （<see cref="Ports"/> 参照）。<b>本述語が真に効くのはモータただ一つ。</b>
    /// </para>
    /// <para>
    /// <b>射程</b>：自作パーツで <c>PartRole.NonSimulated</c> かつ接続点を持つものは、モータと同じ形で
    /// 結線に参加する。殿裁定は対象をモータのみと定めており、そちらは <c>proposed.md</c> P-161 で別途判ずる。
    /// </para></summary>
    public static bool ParticipatesInWiring(ElementKind k) => k is not ElementKind.Motor;

    /// <summary>接点（導通/非導通を切り替える要素）か。</summary>
    public static bool IsContact(ElementKind k) => k is
        ElementKind.ContactNO or ElementKind.ContactNC or
        ElementKind.PushButtonNO or ElementKind.PushButtonNC or
        ElementKind.SelectSwitch or
        ElementKind.TimerContactNO or ElementKind.TimerContactNC or
        ElementKind.TimerInstantContactNO or ElementKind.TimerInstantContactNC or
        ElementKind.EmergencyStop or ElementKind.ThermalOverload;

    /// <summary>負荷（コイル・ランプ・タイマコイル・カウンタコイル）か。</summary>
    public static bool IsLoad(ElementKind k) => k is
        ElementKind.Coil or ElementKind.Lamp or ElementKind.Timer or ElementKind.Counter;

    /// <summary>常時導通の通過要素（端子台）か。</summary>
    public static bool IsPassthrough(ElementKind k) => k is ElementKind.Terminal;

    /// <summary>
    /// 接点の制御元が外部入力（押ボタン・セレクトSW・OL手動）か。false ならリレーコイル状態で制御。
    /// タイマ接点（TimerContactNO/NC）はタイマコイル励磁＋経過時間で制御するため含まない。
    /// </summary>
    public static bool IsInputControlled(ElementKind k) => k is
        ElementKind.PushButtonNO or ElementKind.PushButtonNC or ElementKind.SelectSwitch or
        ElementKind.EmergencyStop or ElementKind.ThermalOverload;
}
