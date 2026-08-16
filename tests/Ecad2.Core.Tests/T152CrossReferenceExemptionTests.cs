using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-152（`P-187` 対処・殿ご裁可2026-08-16＝案B）＝役割を正した結果に残る消せぬ警告を塞ぐ。
/// 隠密テスト設計 <c>docs/ecad2-t152-cross-reference-exemption-test-design-onmitsu.md</c> に対応。
/// <para>
/// 【二件は形が違う】サーマルリレーa/b は組込みゆえ固定Idで弁別でき（<c>DRC-XREF-001</c>）、
/// ソレノイド等の自作パーツは Id が可変ゆえ新設マーカー
/// <see cref="PartDefinition.IsExcludedFromCrossReference"/> が要る（<c>DRC-XREF-002</c>）。
/// </para>
/// <para>
/// 【本テスト群の眼目は「除外されてはならぬものが除外されておらぬ」側にござる】
/// 除外の実装は、書き方を誤れば検査の検出力をまるごと失わせうる——自作コイル全般や
/// 通常のリレー接点まで巻き込めば、警告は消えるが不具合も見えなくなる。ゆえに各節で
/// <b>除外される群と、除外されてはならぬ群を同じ形で並べて測る</b>
/// （<c>memory: feedback_control_experiment_needs_naive_baseline</c> の型）。
/// </para>
/// </summary>
public class T152CrossReferenceExemptionTests
{
    private const string CustomCoilId = "t152-custom-coil";

    /// <summary>組込み定義一式を積んだライブラリ。PartId 経路の要素はこれで解決される。</summary>
    private static PartLibrary BuiltinLibrary()
    {
        var lib = new PartLibrary();
        foreach (var part in BasicPartTemplates.All()) lib.ById[part.Id] = part;
        return lib;
    }

    /// <summary>自作パーツ定義。<paramref name="excluded"/> でマーカーの有無を分ける。</summary>
    private static PartDefinition CustomPart(string id, PartRole role, bool excluded) => new()
    {
        Id = id,
        Name = "T152自作" + role,
        WidthCells = 1,
        HeightCells = 1,
        Role = role,
        IsExcludedFromCrossReference = excluded,
        Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
        Primitives = new() { new PartLine(0, 0, 1, 0) },
    };

    /// <summary>制御回路シート1枚の文書を作る（主回路シートは T-146 で走査対象外ゆえ避ける）。</summary>
    private static LadderDocument MakeDoc(params ElementInstance[] elements)
    {
        var sheet = new Sheet { PageNumber = 1, Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = false };
        foreach (var e in elements) sheet.Elements.Add(e);
        return new LadderDocument { Sheets = { sheet } };
    }

    private static ElementInstance PartElement(string partId, string deviceName, int row, int col = 0) =>
        new() { PartId = partId, DeviceName = deviceName, Pos = new GridPos(row, col) };

    // ==================================================================
    // 1. サーマルリレー側（DRC-XREF-001）——眼目1「他の組込み接点を巻き込まぬこと」
    // ==================================================================

    /// <summary>設計書1-2。三群を同じ形（コイル無し・接点のみの機器）で並べて測る。
    /// <para>
    /// <b>除外される側と、除外されてはならぬ側を一つのTheoryで対にしてある</b>——
    /// 「警告が消えた」だけを測れば、実装が接点全般を巻き込んでおっても気づけぬゆえ。
    /// </para>
    /// <list type="bullet">
    /// <item>サーマルリレーa/b＝本タスクで除外する側</item>
    /// <item>サーマル(OL)＝<c>IsInputControlled</c> ゆえ<b>元より集計に入らぬ</b>対照。二重除外にならず無傷であること</item>
    /// <item>通常のa接点/b接点＝巻き込み検出の対照。引き続き警告が出ねばならぬ</item>
    /// </list></summary>
    [Theory]
    [InlineData(BasicPartTemplates.ThermalRelayNOId, false)]
    [InlineData(BasicPartTemplates.ThermalRelayNCId, false)]
    [InlineData(BasicPartTemplates.ThermalOverloadId, false)]
    [InlineData(BasicPartTemplates.ContactNOId, true)]
    [InlineData(BasicPartTemplates.ContactNCId, true)]
    public void 単一機器がコイルを伴わぬ時_種別ごとに警告の有無が決まる(string partId, bool expectWarning)
    {
        var doc = MakeDoc(PartElement(partId, "X1", row: 0));

        var diagnostics = DesignRuleCheck.CheckCrossReference(doc, BuiltinLibrary());

        bool warned = diagnostics.Any(d => d.Code == DesignRuleCheck.ContactWithoutCoil);
        Assert.Equal(expectWarning, warned);
    }

    /// <summary>設計書1-3＝境界。同一機器名にサーマルリレーと通常接点が混在する場合。
    /// <para>
    /// 集計は機器名単位ゆえ、サーマルリレー側を除いても<b>通常接点が1件でも残れば警告は出続ける</b>
    /// ——サーマルリレーを除いてもなお「本物の接点」が駆動元不明のまま在るゆえ、これが正しい。
    /// 除外の実装が機器名単位で効いてしまう（＝一つでも除外対象が在れば機器ごと除外する）誤りを
    /// 犯せば、本テストがそれを捕らえる。
    /// </para></summary>
    [Fact]
    public void サーマルリレーと通常接点が同一機器名で混在する時_通常接点側の警告は消えぬ()
    {
        var doc = MakeDoc(
            PartElement(BasicPartTemplates.ThermalRelayNOId, "X1", row: 0),
            PartElement(BasicPartTemplates.ContactNOId, "X1", row: 1));

        var diagnostics = DesignRuleCheck.CheckCrossReference(doc, BuiltinLibrary());

        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.ContactWithoutCoil && d.DeviceName == "X1");
    }

    // ==================================================================
    // 2. ソレノイド側（DRC-XREF-002）——眼目2「検出力をまるごと失わぬこと」
    // ==================================================================

    /// <summary>設計書2-2。マーカーを立てた自作コイル（ソレノイド想定）は死にリレー警告から外れる。</summary>
    [Fact]
    public void マーカーtrueの自作コイルは死にリレー警告から除外される()
    {
        var lib = BuiltinLibrary();
        lib.ById[CustomCoilId] = CustomPart(CustomCoilId, PartRole.Coil, excluded: true);
        var doc = MakeDoc(PartElement(CustomCoilId, "SOL1", row: 0));

        var diagnostics = DesignRuleCheck.CheckCrossReference(doc, lib);

        Assert.DoesNotContain(diagnostics, d => d.Code == DesignRuleCheck.CoilWithoutContact);
    }

    /// <summary>設計書2-2＝<b>眼目2の核心</b>。マーカーを立てておらぬ自作コイルは引き続き警告が出る。
    /// <para>
    /// 実装がマーカーの真偽を取り違える、あるいは「自作パーツのコイル」を一律に除外してしまえば、
    /// 本テストが捕らえる。<b>付け忘れたパーツが黙って検査から漏れる</b>のが最も危うい形にござる。
    /// </para></summary>
    [Fact]
    public void マーカーfalseの自作コイルは死にリレー警告が引き続き出る()
    {
        var lib = BuiltinLibrary();
        lib.ById[CustomCoilId] = CustomPart(CustomCoilId, PartRole.Coil, excluded: false);
        var doc = MakeDoc(PartElement(CustomCoilId, "SOL1", row: 0));

        var diagnostics = DesignRuleCheck.CheckCrossReference(doc, lib);

        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.CoilWithoutContact && d.DeviceName == "SOL1");
    }

    /// <summary>設計書2-2＝対照。組込みコイルにはマーカーの概念が無く、従来どおり警告が出る。</summary>
    [Fact]
    public void 組込みコイルはマーカー導入後も従来どおり警告が出る()
    {
        var doc = MakeDoc(PartElement(BasicPartTemplates.CoilId, "Y1", row: 0));

        var diagnostics = DesignRuleCheck.CheckCrossReference(doc, BuiltinLibrary());

        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.CoilWithoutContact && d.DeviceName == "Y1");
    }

    /// <summary>マーカーは接点側（<c>DRC-XREF-001</c>）にも同じく効くこと。
    /// <para>
    /// 設計書は自作コイル（<c>DRC-XREF-002</c>）のみを挙げておるが、
    /// <b>実装が両方の集計を一つの門で塞ぐ形になるなら、接点側の振る舞いも固定しておかねば
    /// 片側だけ効く実装を素通しする</b>——侍の判断で網を補うたもの（家老DoD1「網羅は実装者の判断」）。
    /// </para></summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void マーカーは自作接点の駆動元不明警告にも効く(bool excluded, bool expectWarning)
    {
        var lib = BuiltinLibrary();
        lib.ById[CustomCoilId] = CustomPart(CustomCoilId, PartRole.ContactNO, excluded);
        var doc = MakeDoc(PartElement(CustomCoilId, "X9", row: 0));

        var diagnostics = DesignRuleCheck.CheckCrossReference(doc, lib);

        Assert.Equal(expectWarning, diagnostics.Any(d => d.Code == DesignRuleCheck.ContactWithoutCoil));
    }

    /// <summary>除外されたコイルは、対になる接点の側の判定にも影響を与えぬこと
    /// （除外＝集計から外れるのであって、接点を「持っておる」ことにはならぬ）。
    /// <para>
    /// 同一機器名に「除外された自作コイル」と「通常のリレー接点」を置く。コイルが集計から外れる以上、
    /// 接点の側は<b>駆動コイル不明として警告が出る</b>のが筋にござる。除外の実装が
    /// 「機器ごと検査対象から外す」形であれば、この警告まで消えてしまい本テストが捕らえる。
    /// </para></summary>
    [Fact]
    public void 除外されたコイルは対になる接点側の警告を消さぬ()
    {
        var lib = BuiltinLibrary();
        lib.ById[CustomCoilId] = CustomPart(CustomCoilId, PartRole.Coil, excluded: true);
        var doc = MakeDoc(
            PartElement(CustomCoilId, "SOL1", row: 0),
            PartElement(BasicPartTemplates.ContactNOId, "SOL1", row: 1));

        var diagnostics = DesignRuleCheck.CheckCrossReference(doc, lib);

        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.ContactWithoutCoil && d.DeviceName == "SOL1");
        Assert.DoesNotContain(diagnostics, d => d.Code == DesignRuleCheck.CoilWithoutContact);
    }

    // ==================================================================
    // 3. 後方互換（眼目3）
    // ==================================================================

    /// <summary>設計書3-1。マーカーのキー自体を持たぬ旧 <c>.gcadpart</c> は既定 false で読まれる。
    /// <b>すなわち既存のパーツはすべて従来どおり検査の対象に留まる。</b></summary>
    [Fact]
    public void マーカーを持たぬJSONを読むと既定falseになる()
    {
        const string legacyJson = """
            {
              "id": "t152-legacy",
              "name": "旧世代パーツ",
              "widthCells": 1,
              "heightCells": 1,
              "role": "Coil",
              "ports": [],
              "primitives": []
            }
            """;

        var part = PartLibrarySerializer.DeserializeOne(legacyJson);

        Assert.False(part.IsExcludedFromCrossReference);
    }

    /// <summary>設計書3-2。保存して読み直しても値が保たれること（true/false の両側）。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void マーカーは保存して読み直しても保たれる(bool value)
    {
        var part = CustomPart("t152-roundtrip", PartRole.Coil, value);

        var restored = PartLibrarySerializer.DeserializeOne(PartLibrarySerializer.SerializeOne(part));

        Assert.Equal(value, restored.IsExcludedFromCrossReference);
    }

    /// <summary>マーカーを持つ定義が、ライブラリ単位の往復でも保たれること
    /// （<c>SerializeOne</c> と <c>Serialize</c> は別経路ゆえ、片方だけ通っても他方は保証されぬ）。</summary>
    [Fact]
    public void マーカーはライブラリ単位の往復でも保たれる()
    {
        var lib = new PartLibrary();
        lib.ById[CustomCoilId] = CustomPart(CustomCoilId, PartRole.Coil, excluded: true);

        var restored = PartLibrarySerializer.Deserialize(PartLibrarySerializer.Serialize(lib));

        Assert.True(Assert.Single(restored).IsExcludedFromCrossReference);
    }

    /// <summary>図面（<c>.gcad</c>）の <c>library</c> へ埋め込まれた定義でも、マーカー欠落は既定 false で読まれること。
    /// <para>
    /// <b>【なぜ `.gcadpart` 単体の網では足りぬか】</b>上の後方互換テストが測るのは
    /// <c>PartLibrarySerializer</c> の経路にて、<b>図面へ埋め込む経路（<c>GcadSerializer</c>）は別物</b>にござる。
    /// 機序は同じ（いずれも <c>System.Text.Json</c> の自動反映）と読めるが、<b>経路が違えば別途測る要がある</b>
    /// ——忍者が明日の実機検体を <c>library</c> 埋め込みの形で組まれた（`MK3`＝フィールドを書かぬ検体）ゆえ、
    /// 実機の前に机上でこの経路を押さえておけば、万一食い違うた折に「実装が違う」のか「経路が違う」のかを
    /// 切り分けられる。
    /// </para></summary>
    [Fact]
    public void 図面へ埋め込まれた定義でもマーカー欠落は既定falseで読まれる()
    {
        // library 配下の定義から isExcludedFromCrossReference のキー自体を落としてある（MK3 と同型）。
        const string gcadWithoutMarker = """
            {
              "schemaVersion": 1,
              "info": { "title": "T152マーカー欠落", "drawingNo": "T152-MK3" },
              "library": {
                "byId": {
                  "t152-embedded-coil": {
                    "id": "t152-embedded-coil",
                    "name": "埋め込みコイル",
                    "widthCells": 1,
                    "heightCells": 1,
                    "role": "Coil",
                    "ports": [],
                    "primitives": []
                  }
                }
              },
              "sheets": [
                {
                  "pageNumber": 1,
                  "name": "シート1",
                  "grid": { "rows": 10, "columns": 20 },
                  "elements": [
                    { "partId": "t152-embedded-coil", "pos": { "row": 0, "column": 0 }, "deviceName": "MK3" }
                  ]
                }
              ]
            }
            """;

        var doc = GcadSerializer.Deserialize(gcadWithoutMarker);

        var part = doc.Library!.Get("t152-embedded-coil");
        Assert.NotNull(part);
        Assert.False(part!.IsExcludedFromCrossReference);

        // 既定 false ゆえ、検査は従来どおり働く（死にリレー警告が出る）。
        Assert.Contains(DesignRuleCheck.CheckCrossReference(doc, doc.Library),
            d => d.Code == DesignRuleCheck.CoilWithoutContact && d.DeviceName == "MK3");
    }

    // ==================================================================
    // 6. 追補＝DRC-TYPE-001 誤発火（設計書6節）
    //    本節は手当ての形（除外／再分類）に依らず成り立つ回帰網のみを置く。
    //    両案で結果が分かれるケースは、方式が定まってから足す。
    // ==================================================================

    /// <summary>設計書6-2＝対照。サーマル(OL)単体は従来どおり警告を出さぬ
    /// （<c>IsInputControlled</c> ゆえ入力系ただ一つに属し、混在にならぬ）。</summary>
    [Fact]
    public void サーマル単体は従来どおり種別混在の警告を出さぬ()
    {
        var doc = MakeDoc(PartElement(BasicPartTemplates.ThermalOverloadId, "OL1", row: 0));

        var diagnostics = DesignRuleCheck.CheckDeviceTypeConsistency(doc, BuiltinLibrary());

        Assert.DoesNotContain(diagnostics, d => d.Code == DesignRuleCheck.TypeConflictEnergizedVsInput);
    }

    /// <summary>設計書6-3＝回帰。サーマル(OL)本体と無関係な励磁系接点が同一機器名なら、従来どおり警告が出る。
    /// <para>
    /// <b>サーマル本体の分類は本追補で一切変えておらぬ</b>ことを固定する対照にござる
    /// ——手当てがサーマル本体まで巻き込めば、ここが鳴らなくなる。
    /// </para></summary>
    [Fact]
    public void サーマル本体と無関係な励磁系接点の混在は従来どおり警告が出る()
    {
        var doc = MakeDoc(
            PartElement(BasicPartTemplates.ThermalOverloadId, "OL9", row: 0),
            PartElement(BasicPartTemplates.ContactNOId, "OL9", row: 1));

        var diagnostics = DesignRuleCheck.CheckDeviceTypeConsistency(doc, BuiltinLibrary());

        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.TypeConflictEnergizedVsInput && d.DeviceName == "OL9");
    }

    /// <summary>設計書6-3＝回帰。サーマル系とは無関係な、通常の励磁系接点と入力系接点の混在は引き続き警告が出る。
    /// <b>本追補の変更が、無関係な判定へ波及しておらぬこと</b>の直接の固定にござる。</summary>
    [Fact]
    public void 通常の励磁系接点と入力系接点の混在は引き続き警告が出る()
    {
        var doc = MakeDoc(
            PartElement(BasicPartTemplates.ContactNOId, "M1", row: 0),
            PartElement(BasicPartTemplates.PushButtonNOId, "M1", row: 1));

        var diagnostics = DesignRuleCheck.CheckDeviceTypeConsistency(doc, BuiltinLibrary());

        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.TypeConflictEnergizedVsInput && d.DeviceName == "M1");
    }

    /// <summary>設計書6-5の主眼＝現場の通例（OL本体と補助接点は同一機器名）で誤発火せぬこと。
    /// <para>
    /// <b>これが本追補の発端</b>にござる。T-133増分7でサーマルリレーa/bが新設され、既存のサーマル(OL)本体と
    /// 組み合わせて初めて生じた形にて、<b>いずれか単体の欠陥ではない</b>。
    /// 補助接点は <c>Role=ContactNO/NC</c> ゆえ励磁系へ、本体は <c>IsInputControlled</c> ゆえ入力系へ
    /// 振り分けられ、同一機器名で混在と判ぜられておった。
    /// </para></summary>
    [Theory]
    [InlineData(BasicPartTemplates.ThermalRelayNOId)]
    [InlineData(BasicPartTemplates.ThermalRelayNCId)]
    public void OL本体と補助接点が同一機器名でも種別混在の警告が出ぬ(string relayPartId)
    {
        var doc = MakeDoc(
            PartElement(BasicPartTemplates.ThermalOverloadId, "OL1", row: 0),
            PartElement(relayPartId, "OL1", row: 1));

        var diagnostics = DesignRuleCheck.CheckDeviceTypeConsistency(doc, BuiltinLibrary());

        Assert.DoesNotContain(diagnostics, d => d.Code == DesignRuleCheck.TypeConflictEnergizedVsInput);
    }

    /// <summary>
    /// 設計書6-3後半＝<b>除外方式の既知の限界を記録する</b>。検出漏れではなく、承知のうえで受け入れた仕様にござる。
    /// <para>
    /// サーマルリレーを門で除いた結果、<b>それが無関係な接点と同じ機器名を付けられた場合の命名衝突</b>は
    /// 検出できなくなる——除外された側はどちらの系統にも算入されぬゆえ、混在が成立せぬ。
    /// </para>
    /// <para>
    /// <b>【二つのケースは性質が違う。壊す実測が示した】</b>初稿は「同型ゆえ両方を並べる」と書いたが、
    /// 改変G（門を落とす）を当てたところ<b>押釦のケースだけが鳴り、a接点のケースは鳴らなんだ</b>。
    /// <list type="bullet">
    /// <item><b>押釦（入力系）</b>＝門が無ければサーマルリレー（励磁系）と混在して鳴る。
    /// <b>門によって検出が失われる真の限界</b>にござる</item>
    /// <item><b>a接点（励磁系）</b>＝門が無くとも両方が励磁系ゆえ元より混在にならぬ。
    /// <b>門は何も失っておらぬ</b>——失うのは「再分類方式を採れば得られたはずの検出力」にすぎぬ</item>
    /// </list>
    /// ゆえに後者は<b>門の検出力を測る網ではなく、方式の違いを記録する網</b>にござる
    /// （<c>samurai.md</c>「鳴らなかったテストを一つずつ見よ」に従い、実測してから書き改めたもの）。
    /// </para>
    /// <para>
    /// <b>【この2件は「鳴らぬのが正しい」テストにござる】</b>将来これが鳴るようになったなら、それは
    /// 実装が<b>再分類方式</b>（<c>DesignRuleCheck</c> の中でサーマルリレーを入力系へ寄せる）へ
    /// 変わったことを意味する——その道なら励磁系との衝突は検出できる。
    /// <b>すなわち本テストが落ちたときは、直す前に「方式が変わったのではないか」を疑われたい。</b>
    /// 二案の違いは家老が殿へお示ししたうえで除外方式が裁可されており（2026-08-16）、
    /// 方式を変えるならその裁可から改める筋にござる。
    /// </para></summary>
    [Theory]
    [InlineData(BasicPartTemplates.PushButtonNOId)]   // 入力系との衝突（設計書が挙げた形）
    [InlineData(BasicPartTemplates.ContactNOId)]      // 励磁系との衝突（同型ゆえ侍が補うた）
    public void サーマルリレーと無関係な接点の命名衝突は検出できぬ_承知のうえの限界(string otherPartId)
    {
        var doc = MakeDoc(
            PartElement(BasicPartTemplates.ThermalRelayNOId, "X1", row: 0),
            PartElement(otherPartId, "X1", row: 1));

        var diagnostics = DesignRuleCheck.CheckDeviceTypeConsistency(doc, BuiltinLibrary());

        Assert.DoesNotContain(diagnostics, d => d.Code == DesignRuleCheck.TypeConflictEnergizedVsInput);
    }

    /// <summary>
    /// <b>自作パーツのマーカーは、機器種別の整合性検査には効かぬこと</b>（侍の判断で補うた網）。
    /// <para>
    /// <b>【なぜこれを固定するか】</b>マーカーは<b>「クロスリファレンス検査から除外する」という文言で
    /// パーツエディタに出ておる</b>（殿ご裁可の文言）。もし実装が <c>CheckDeviceTypeConsistency</c> でも
    /// 同じマーカーを見れば、<b>利用者が承知したのと違う検査まで黙って外れる</b>——UIの文言と実際の
    /// 効果が食い違う形にござる。
    /// </para>
    /// <para>
    /// 本テストは、マーカーを立てた自作の励磁系接点が<b>引き続き種別混在の判定に算入される</b>ことを
    /// 確かめる。<c>DesignRuleCheck</c> が二つの門を持ち、<b>一方だけがマーカーを見る</b>という
    /// 非対称そのものを固定しておる。
    /// </para></summary>
    [Fact]
    public void マーカーは機器種別の整合性検査には効かぬ()
    {
        var lib = BuiltinLibrary();
        lib.ById[CustomCoilId] = CustomPart(CustomCoilId, PartRole.ContactNO, excluded: true);
        var doc = MakeDoc(
            PartElement(CustomCoilId, "MX1", row: 0),                          // 自作の励磁系接点（マーカーtrue）
            PartElement(BasicPartTemplates.PushButtonNOId, "MX1", row: 1));    // 入力系

        var diagnostics = DesignRuleCheck.CheckDeviceTypeConsistency(doc, lib);

        // マーカーが効いておれば混在が成立せず鳴らぬ。効いておらぬゆえ鳴るのが正しい。
        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.TypeConflictEnergizedVsInput && d.DeviceName == "MX1");
    }
}
