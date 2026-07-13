using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-061第二歩+T-061修正(静的レビューB群): DrawElementの通電/非通電色分け(殿裁定「LDmicro式」
/// 通電=赤/非通電=グレー)の回帰テスト。DrawLine/DrawCircle等に渡されるStrokeStyle.Colorを記録し、
/// sim引数の有無・導通状態による色の出し分けを検証する。母線間に単独要素を配置する最小回路
/// (NetlistBuilderOrChainTests同型の手法)を使う。
///
/// B群修正の背景(修正方針設計書§5-5): 旧実装はDrawElementがenergized[DeviceName](コイルの励磁状態
/// のみ)を直接見て色を決めていたため、接点・押しボタン・セレクトSW(コイルではない)は強制ON操作を
/// しても常にグレー固定だった(B-1)。加えてNC系の反転(B-3)・限時タイマ接点の時期尚早な赤表示(B-2)も
/// 機能不全だった。修正後はEvaluator.EvalResult.ElementConducting(既存IsConductingの結果を要素単位で
/// 持ち帰る)を参照する。第二歩時点の既存テスト3件は実は新旧どちらの実装でも同じ結果になり
/// (コイル自体はenergized辞書に直接登録される対象、非導通の単独接点はどちらの実装でも非通電扱い)、
/// このB群バグを検出できていなかった――今回追加する導通中接点のテストがバグの実質的な再発防止線になる。
/// </summary>
public class DiagramRendererTestModeColorTests
{
    // T-061修正(隠密指摘、家老経由確認2026-07-13): MainCircuit=falseだとDrawRungWiresが自動配線
    // (母線間の配線)を描画し、その配線色はPoweredNets(floodL∪floodR、両母線から到達可能な全ネット)
    // 判定で常にPowered色になる(単独要素配置は必ず母線に直結するため)。Assert.Contains(Powered, ...)
    // は配線由来のPoweredと要素記号由来のPoweredを区別できず、要素自体が実際にはグレーのまま
    // (=旧実装のB群バグ)でも配線色混入によって偽陽性でPASSしてしまっていた(隠密の静的読解で発覚、
    // 実測でも6件中4件が旧実装で偶然PASSすると確認)。MainCircuit=trueにしてDrawRails/DrawBusLabels/
    // DrawRungWiresを全てスキップさせ、DrawElement(要素記号)由来の色のみが記録されるようにする
    // (NetlistBuilder/EvaluatorはMainCircuitを一切参照しないため評価結果への影響はない)。
    private static Sheet MakeSingleElementSheet(ElementKind kind, string deviceName)
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = true };
        sheet.Elements.Add(new ElementInstance { Kind = kind, Pos = new GridPos(0, 5), DeviceName = deviceName });
        return sheet;
    }

    [Fact]
    public void Render_DrawingMode_DoesNotUseTestModeColors()
    {
        var sheet = MakeSingleElementSheet(ElementKind.ContactNO, "X001");
        var renderer = new ColorRecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet);   // sim=null(作画モード)

        Assert.DoesNotContain(DrawingTheme.Powered, renderer.LineColors);
        Assert.DoesNotContain(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeEnergizedCoil_UsesPoweredColor()
    {
        var sheet = MakeSingleElementSheet(ElementKind.Coil, "Y001");
        var renderer = new ColorRecordingRenderer();

        // 単独要素は自動的に左右母線間へ直結される(DrawRungWires前提と同型)ため、コイルは常時励磁。
        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.Contains(DrawingTheme.Powered, renderer.LineColors);
        Assert.DoesNotContain(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeUnenergizedContact_UsesNonEnergizedGray()
    {
        var sheet = MakeSingleElementSheet(ElementKind.ContactNO, "X001");
        var renderer = new ColorRecordingRenderer();

        // 手動強制(Inputs)未設定・対応コイルも無いため常時非導通。配線区間自体はいずれかの母線から
        // 到達可能なため通電色(Powered)になりうる(電気的に正しい仕様動作、PoweredNets=floodL∪floodR)。
        // 検証対象は接点記号自体がグレー(非通電)で描かれることのみ。
        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.Contains(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeManuallyForcedContactNO_UsesPoweredColor()
    {
        // B-1再発防止(核心): コイルでない要素(接点)を手動強制ONにした場合に赤くなることを検証する。
        // 旧実装はenergized[DeviceName](コイル専用辞書)を直接見ており、接点のDeviceNameはこの辞書に
        // 登録されないため常にグレー固定だった。修正後はElementConducting(IsConducting経由、
        // Inputs=trueによる手動強制閉路を正しく解決)を見るため赤になる。
        var sheet = MakeSingleElementSheet(ElementKind.ContactNO, "X001");
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.Inputs["X001"] = true;

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains(DrawingTheme.Powered, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeUnforcedContactNC_UsesPoweredColor()
    {
        // B-3再発防止(NC反転): b接点は「非励磁時に閉路(導通)=赤」が正しい仕様。旧実装はNO/NCの区別を
        // 一切行わずenergized[DeviceName]のみを見ていたため、この反転が機能しなかった疑いがあった。
        var sheet = MakeSingleElementSheet(ElementKind.ContactNC, "X002");
        var renderer = new ColorRecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.Contains(DrawingTheme.Powered, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeForcedContactNC_UsesNonEnergizedGray()
    {
        // 正しい動作の確認(検出力なし、隠密指摘・家老経由確認2026-07-13): 手動強制ON(Inputs=true)なら
        // NCは開路(非導通)=グレーになるべき。ただし旧実装(energized[DeviceName]のみ参照、接点は
        // energizedに登録されないため常にfalse)でも「非導通」という同じ結論に偶然一致するため、
        // このケース単体は旧実装のB-3バグを検出できない(RED先行証明で実測確認済み、旧実装でもPASS)。
        // 検出力はUnforcedContactNC(Inputs未設定→旧実装は誤ってグレー、正しくは赤)側が担う。
        // このテストは反転の対称性(ON/OFF両方向)を正しい実装が満たすことの確認として残す。
        var sheet = MakeSingleElementSheet(ElementKind.ContactNC, "X002");
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.Inputs["X002"] = true;

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModePushButtonPressed_UsesPoweredColor()
    {
        // B-1再発防止(押しボタン自体の色): PushButtonNOはIsInputControlled=trueのためInputsで直接
        // 制御される。押している間(Inputs=true)は赤になるべき。
        var sheet = MakeSingleElementSheet(ElementKind.PushButtonNO, "PB1");
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.Inputs["PB1"] = true;

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains(DrawingTheme.Powered, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeTimerContactNOBeforeTimeout_UsesNonEnergizedGray()
    {
        // B-2再発防止: 限時タイマ接点はコイル励磁「かつ」経過時間>=設定時間で初めて導通する。
        // コイル励磁直後(経過時間未達)は非導通(グレー)のままであるべき。
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = true };
        var timerCoil = new ElementInstance { Kind = ElementKind.Timer, Pos = new GridPos(0, 0), DeviceName = "T001" };
        timerCoil.Params[ParamKeys.Setpoint] = "5";
        var timerContact = new ElementInstance { Kind = ElementKind.TimerContactNO, Pos = new GridPos(1, 0), DeviceName = "T001" };
        sheet.Elements.Add(timerCoil);
        sheet.Elements.Add(timerContact);
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.TimerElapsed["T001"] = 3;   // 設定値5秒に未達

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains(DrawingTheme.NonEnergizedGray, renderer.LineColors);
    }

    [Fact]
    public void Render_TestModeTimerContactNOAfterTimeout_UsesPoweredColor()
    {
        // 正しい動作の確認(検出力なし、隠密指摘・家老経由確認2026-07-13): 経過時間が設定値に達すれば
        // 導通(赤)になるべき。ただしこのケース(コイル励磁中+経過時間超過)は、旧実装(DeviceName一致の
        // energized[DeviceName]のみ参照、限時判定なし)でも「コイルは励磁中でTimerContactNOも同じ
        // DeviceNameを共有する」ため偶然Poweredに一致してしまい、旧実装のB-2バグを検出できない
        // (RED先行証明で実測確認済み、旧実装でもPASS)。検出力はBeforeTimeout(経過時間未達→旧実装は
        // 誤って赤、正しくはグレー)側が担う。このテストは限時判定の対称性(達成/未達両方)の確認として残す。
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = true };
        var timerCoil = new ElementInstance { Kind = ElementKind.Timer, Pos = new GridPos(0, 0), DeviceName = "T001" };
        timerCoil.Params[ParamKeys.Setpoint] = "5";
        var timerContact = new ElementInstance { Kind = ElementKind.TimerContactNO, Pos = new GridPos(1, 0), DeviceName = "T001" };
        sheet.Elements.Add(timerCoil);
        sheet.Elements.Add(timerContact);
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.TimerElapsed["T001"] = 10;   // 設定値5秒を超過

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains(DrawingTheme.Powered, renderer.LineColors);
    }
}

/// <summary>DrawLine/DrawCircle等に渡されるStrokeStyle.Colorのみを記録するIRendererのテストダブル
/// (DiagramRendererLabelTests.RecordingRendererのDrawText専用版と対になる色専用版)。</summary>
internal sealed class ColorRecordingRenderer : IRenderer
{
    public List<Color> LineColors { get; } = new();

    public void PushTransform(double translateX, double translateY, double scale = 1.0) { }
    public void PopTransform() { }
    public void PushClip(Rect2D rect) { }
    public void PopClip() { }
    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void DrawRectangle(Rect2D rect, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void FillRectangle(Rect2D rect, Color color) { }
    public void DrawCircle(Point2D center, double radius, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void FillCircle(Point2D center, double radius, Color color) { }
    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke) => LineColors.Add(stroke.Color);
    public void DrawText(string text, Point2D position, TextStyle style) { }
    public Size2D MeasureText(string text, TextStyle style) => new(0, 0);
    public void DrawImage(string filePath, Rect2D bounds) { }
}
