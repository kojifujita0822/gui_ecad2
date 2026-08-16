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
}
