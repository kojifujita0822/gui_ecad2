using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-146(殿裁定2026-08-08): 主回路シート(<see cref="Sheet.MainCircuit"/>)では診断・接続判定・
/// テストモード評価を走らせない。増分1＝シート単位の系統。
/// <para>
/// 各テストは対の形で書く——同じ図を制御回路シートで測って診断が出ることを先に示し、
/// その上で主回路シートでは出ないことを測る。片側だけでは「図がそもそも診断を生まない」のか
/// 「ガードが効いた」のかを弁別できないため(memory: feedback_control_experiment_needs_naive_baseline)。
/// </para>
/// <para>
/// 測っていない範囲＝実機での見え方(要素の背景色・接点記号の線色が主回路で変わらなくなること)。
/// 本テストが測るのは <see cref="DiagramRenderer.Render"/> が描画へ渡す色までであり、
/// WPF 側の実描画は忍者の実機確認に委ねる。
/// </para>
/// </summary>
public class T146MainCircuitDiagnosticsTests
{
    // ---- 図の組み立て（NetlistBuilder の接続モデルに沿う） ----

    /// <summary>二重コイル(負荷2つが直列)の図。行0に Coil を列0・列2へ並べると、横配線で
    /// 左母線-[Y1]-N-[Y2]-右母線 となり、共有節点 N は接点経由でどちらの母線からも到達できない
    /// ＝直列接続点として <see cref="DesignRuleCheck.CheckSeriesCoils"/> が拾う。</summary>
    private static Sheet MakeSeriesCoilSheet(bool mainCircuit)
    {
        var sheet = new Sheet { PageNumber = 1, Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = mainCircuit };
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.Coil, DeviceName = "Y1", Pos = new GridPos(0, 0) });
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.Coil, DeviceName = "Y2", Pos = new GridPos(0, 2) });
        return sheet;
    }

    /// <summary>負荷の入力側が左母線から到達できない図。コイルを列2へ置き、その左の区間
    /// (0〜2)へ配線分断(セル中央=1.5)を挟むと左母線への union がスキップされる
    /// ＝<see cref="DesignRuleCheck.CheckLoadReachability"/> の DRC-LOAD-001 が出る。</summary>
    private static Sheet MakeUnreachableLoadSheet(bool mainCircuit)
    {
        var sheet = new Sheet { PageNumber = 1, Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = mainCircuit };
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.Coil, DeviceName = "Y1", Pos = new GridPos(0, 2) });
        sheet.WireBreaks.Add(new WireBreak { Row = 0, Boundary = 1.5 });
        return sheet;
    }

    /// <summary>縦コネクタが中間行の横配線と非接続で交差する図(ドット無し交差、P7)。
    /// 行0・行2の接点を列2の縦コネクタで結び、中間の行1には列2に境界を持つコイルを置く。
    /// 行1のコイルは左母線側のネットに属し、縦コネクタが運ぶネットとは別ゆえ交差と判定される
    /// ＝<see cref="DesignRuleCheck.CheckVerticalCrossings"/> が拾う。</summary>
    private static Sheet MakeVerticalCrossingSheet(bool mainCircuit)
    {
        var sheet = new Sheet { PageNumber = 1, Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = mainCircuit };
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "X1", Pos = new GridPos(0, 0) });
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.Coil, DeviceName = "Y1", Pos = new GridPos(1, 2) });
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = "X2", Pos = new GridPos(2, 0) });
        sheet.Connectors.Add(new VerticalConnector { Column = 2, TopRow = 0, BottomRow = 2 });
        return sheet;
    }

    /// <summary>押ボタンでコイルを駆動する最小回路(行0に PB1 → Y1)。</summary>
    private static Sheet MakePushButtonCoilSheet(bool mainCircuit)
    {
        var sheet = new Sheet { PageNumber = 1, Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = mainCircuit };
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.PushButtonNO, DeviceName = "PB1", Pos = new GridPos(0, 0) });
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.Coil, DeviceName = "Y1", Pos = new GridPos(0, 1) });
        return sheet;
    }

    // ---- (1) シート単位DRC 3件 ----

    [Fact]
    public void CheckSeriesCoils_制御回路シートでは二重コイルを検出する()
    {
        var sheet = MakeSeriesCoilSheet(mainCircuit: false);
        var net = NetlistBuilder.Build(sheet);

        var diagnostics = DesignRuleCheck.CheckSeriesCoils(sheet, net);

        var d = Assert.Single(diagnostics);
        Assert.Equal(DesignRuleCheck.SeriesCoils, d.Code);
    }

    [Fact]
    public void CheckSeriesCoils_主回路シートでは検出しない()
    {
        var sheet = MakeSeriesCoilSheet(mainCircuit: true);
        var net = NetlistBuilder.Build(sheet);

        Assert.Empty(DesignRuleCheck.CheckSeriesCoils(sheet, net));
    }

    [Fact]
    public void CheckLoadReachability_制御回路シートでは母線到達不可を検出する()
    {
        var sheet = MakeUnreachableLoadSheet(mainCircuit: false);
        var net = NetlistBuilder.Build(sheet);

        var diagnostics = DesignRuleCheck.CheckLoadReachability(sheet, net);

        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.LoadNotReachableFromLeft);
    }

    [Fact]
    public void CheckLoadReachability_主回路シートでは検出しない()
    {
        var sheet = MakeUnreachableLoadSheet(mainCircuit: true);
        var net = NetlistBuilder.Build(sheet);

        Assert.Empty(DesignRuleCheck.CheckLoadReachability(sheet, net));
    }

    [Fact]
    public void CheckVerticalCrossings_制御回路シートではドット無し交差を検出する()
    {
        var sheet = MakeVerticalCrossingSheet(mainCircuit: false);
        var net = NetlistBuilder.Build(sheet);

        var diagnostics = DesignRuleCheck.CheckVerticalCrossings(sheet, net);

        Assert.Contains(diagnostics, d => d.Code == DesignRuleCheck.VerticalCrossing);
    }

    [Fact]
    public void CheckVerticalCrossings_主回路シートでは検出しない()
    {
        var sheet = MakeVerticalCrossingSheet(mainCircuit: true);
        var net = NetlistBuilder.Build(sheet);

        Assert.Empty(DesignRuleCheck.CheckVerticalCrossings(sheet, net));
    }

    // ---- (2) テストモード評価（TestSession） ----

    [Fact]
    public void TestSession_制御回路シートでは押ボタンでコイルが励磁する()
    {
        var session = new TestSession(MakePushButtonCoilSheet(mainCircuit: false));

        session.SetInput("PB1", on: true);

        Assert.True(session.IsEnergized("Y1"));
    }

    [Fact]
    public void TestSession_主回路シートでは評価を走らせない()
    {
        var session = new TestSession(MakePushButtonCoilSheet(mainCircuit: true));

        session.SetInput("PB1", on: true);

        Assert.False(session.IsEnergized("Y1"));
        Assert.Empty(session.Result!.PoweredNets);
    }

    [Fact]
    public void TestSession_主回路シートではTickも時間を進めない()
    {
        var sheet = MakePushButtonCoilSheet(mainCircuit: true);
        var timerCoil = new ElementInstance { Kind = ElementKind.Timer, DeviceName = "T1", Pos = new GridPos(1, 0) };
        timerCoil.Params[ParamKeys.Setpoint] = "5";
        sheet.Elements.Add(timerCoil);
        var session = new TestSession(sheet);
        session.State.Energized["T1"] = true;   // 励磁中とみなせる状態を先に置く

        session.Tick(1.0);

        Assert.DoesNotContain("T1", session.State.TimerElapsed.Keys);
    }

    // ---- (3) 描画時のテストモード評価（DiagramRenderer.Render） ----

    [Fact]
    public void Render_制御回路シートではテストモード色が要素記号に出る()
    {
        var sheet = MakePushButtonCoilSheet(mainCircuit: false);
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.Inputs["PB1"] = true;

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains(DrawingTheme.Powered, renderer.SymbolColors);
    }

    [Fact]
    public void Render_主回路シートではテストモード色が一切出ない()
    {
        var sheet = MakePushButtonCoilSheet(mainCircuit: true);
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.Inputs["PB1"] = true;

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        // 要素記号だけでなく図面のどこにも出ないことを主張する（配線・母線も含めて LineColors で見る）。
        Assert.DoesNotContain(DrawingTheme.Powered, renderer.LineColors);
        Assert.DoesNotContain(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }

    // ---- (4) 文書単位DRC 3件（増分2） ----
    //
    // 増分1のシート単位と違い、これらは LadderDocument を受けて全シートを走査する。
    // ゆえに対の形をシート2枚の混在で作る——同じ文書に制御回路シート(ページ1)と主回路シート
    // (ページ2)を置き、同型の不備を両方に仕込む。制御回路の分だけが診断に出れば、
    // 「走査から主回路だけが抜ける」ことを1件のテストで弁別できる。

    /// <summary>ページ1＝制御回路・ページ2＝主回路の2枚組み文書。両シートへ同型の要素を置く。</summary>
    private static LadderDocument MakeMixedDoc(Func<string, ElementInstance[]> makeElements)
    {
        var control = new Sheet { PageNumber = 1, Grid = new GridSpec { Rows = 10, Columns = 20 } };
        control.Elements.AddRange(makeElements("CR1"));
        var main = new Sheet { PageNumber = 2, Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = true };
        main.Elements.AddRange(makeElements("CR2"));
        return new LadderDocument { Sheets = { control, main } };
    }

    [Fact]
    public void CheckCrossReference_主回路シートの要素は駆動元不明として挙がらない()
    {
        // 接点だけあってコイルが無い＝DRC-XREF-001（駆動元不明）。
        var doc = MakeMixedDoc(name =>
            [new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = name, Pos = new GridPos(0, 0) }]);

        var diagnostics = DesignRuleCheck.CheckCrossReference(doc);

        var d = Assert.Single(diagnostics);
        Assert.Equal(DesignRuleCheck.ContactWithoutCoil, d.Code);
        Assert.Equal("CR1", d.DeviceName);   // 主回路側の CR2 は挙がらない
    }

    [Fact]
    public void CheckDeviceTypeConsistency_主回路シートの要素は種別不整合として挙がらない()
    {
        // 同一機器名に励磁系接点(ContactNO)と入力系接点(押釦)が混在＝DRC-TYPE-001。
        var doc = MakeMixedDoc(name =>
        [
            new ElementInstance { Kind = ElementKind.ContactNO, DeviceName = name, Pos = new GridPos(0, 0) },
            new ElementInstance { Kind = ElementKind.PushButtonNO, DeviceName = name, Pos = new GridPos(1, 0) },
        ]);

        var diagnostics = DesignRuleCheck.CheckDeviceTypeConsistency(doc);

        var d = Assert.Single(diagnostics);
        Assert.Equal(DesignRuleCheck.TypeConflictEnergizedVsInput, d.Code);
        Assert.Equal("CR1", d.DeviceName);
    }

    [Fact]
    public void CheckUnresolvedPartId_主回路シートの要素は未解決参照として挙がらない()
    {
        var doc = MakeMixedDoc(name =>
            [new ElementInstance { PartId = "missing-id", DeviceName = name, Pos = new GridPos(0, 0) }]);

        var diagnostics = DesignRuleCheck.CheckUnresolvedPartId(doc, new PartLibrary());

        var d = Assert.Single(diagnostics);
        Assert.Equal(DesignRuleCheck.UnresolvedPartId, d.Code);
        Assert.Equal("CR1", d.DeviceName);
    }

    [Fact]
    public void Render_主回路シートではタイマ残り時間を描かない()
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = true };
        var coil = new ElementInstance { Kind = ElementKind.Coil, DeviceName = "T1", Pos = new GridPos(0, 5) };
        coil.Params[ParamKeys.Setpoint] = "5";
        sheet.Elements.Add(coil);
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.TimerContactNO, DeviceName = "T1", Pos = new GridPos(1, 5) });
        var renderer = new RecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.DoesNotContain(renderer.DrawnTexts, t => t.EndsWith("s"));
    }
}
