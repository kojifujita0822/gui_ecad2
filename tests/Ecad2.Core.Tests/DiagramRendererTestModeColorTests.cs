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
    // 測定系の変遷(この経緯を消すと、次の者が MainCircuit=true へ戻して測定を壊す):
    //
    // T-061修正(隠密指摘、家老経由確認2026-07-13)で、当初は MainCircuit=true を使っていた。
    // 理由は DrawRungWires が描く自動横配線の色が測定を汚すこと——配線色は PoweredNets
    // (floodL∪floodR、両母線から到達可能な全ネット)判定で常にPowered色になり(単独要素配置は必ず
    // 母線に直結するため)、Assert.Contains(Powered, ...) が配線由来と要素記号由来を区別できず、
    // 要素自体がグレーのまま(=旧実装のB群バグ)でも偽陽性でPASSしていた(実測で6件中4件が旧実装で
    // 偶然PASS)。MainCircuit=true にすれば DrawRails/DrawBusLabels/DrawRungWires が全てスキップ
    // され、要素記号由来の色のみが記録される、という理屈だった。
    //
    // T-146(殿裁定2026-08-08)でこの道具は使えなくなった。主回路シートではテストモード評価自体を
    // 走らせないため、MainCircuit=true では要素にも色が付かない(旧のまま残せば全件が自明に通り、
    // 測る力を失う)。そこで測定系を ColorRecordingRenderer.SymbolColors へ移した——DrawElement が
    // 要素記号を PushTransform〜PopTransform で囲むことを使い、シート種別ではなく描画の入れ子で
    // 配線と要素記号を切り分ける。制御回路シート(MainCircuit=false)のまま要素記号の色だけを測れる。
    private static Sheet MakeSingleElementSheet(ElementKind kind, string deviceName)
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(new ElementInstance { Kind = kind, Pos = new GridPos(0, 5), DeviceName = deviceName });
        return sheet;
    }

    [Fact]
    public void Render_DrawingMode_DoesNotUseTestModeColors()
    {
        var sheet = MakeSingleElementSheet(ElementKind.ContactNO, "X001");
        var renderer = new ColorRecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet);   // sim=null(作画モード)

        // ここだけ LineColors を見る(他は SymbolColors)。作画モードではテストモード色が図面の
        // どこにも——要素記号だけでなく配線にも——現れないことを主張したいため、範囲の広い方が強い。
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

        Assert.Contains(DrawingTheme.Powered, renderer.SymbolColors);
        Assert.DoesNotContain(DrawingTheme.NonEnergizedGray, renderer.SymbolColors);
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

        Assert.Contains(DrawingTheme.NonEnergizedGray, renderer.SymbolColors);
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

        Assert.Contains(DrawingTheme.Powered, renderer.SymbolColors);
    }

    [Fact]
    public void Render_TestModeUnforcedContactNC_UsesPoweredColor()
    {
        // B-3再発防止(NC反転): b接点は「非励磁時に閉路(導通)=赤」が正しい仕様。旧実装はNO/NCの区別を
        // 一切行わずenergized[DeviceName]のみを見ていたため、この反転が機能しなかった疑いがあった。
        var sheet = MakeSingleElementSheet(ElementKind.ContactNC, "X002");
        var renderer = new ColorRecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.Contains(DrawingTheme.Powered, renderer.SymbolColors);
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

        Assert.Contains(DrawingTheme.NonEnergizedGray, renderer.SymbolColors);
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

        Assert.Contains(DrawingTheme.Powered, renderer.SymbolColors);
    }

    [Fact]
    public void Render_TestModeTimerContactNOBeforeTimeout_UsesNonEnergizedGray()
    {
        // B-2再発防止: 限時タイマ接点はコイル励磁「かつ」経過時間>=設定時間で初めて導通する。
        // コイル励磁直後(経過時間未達)は非導通(グレー)のままであるべき。
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        var timerCoil = new ElementInstance { Kind = ElementKind.Timer, Pos = new GridPos(0, 0), DeviceName = "T001" };
        timerCoil.Params[ParamKeys.Setpoint] = "5";
        var timerContact = new ElementInstance { Kind = ElementKind.TimerContactNO, Pos = new GridPos(1, 0), DeviceName = "T001" };
        sheet.Elements.Add(timerCoil);
        sheet.Elements.Add(timerContact);
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.TimerElapsed["T001"] = 3;   // 設定値5秒に未達

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains(DrawingTheme.NonEnergizedGray, renderer.SymbolColors);
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
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        var timerCoil = new ElementInstance { Kind = ElementKind.Timer, Pos = new GridPos(0, 0), DeviceName = "T001" };
        timerCoil.Params[ParamKeys.Setpoint] = "5";
        var timerContact = new ElementInstance { Kind = ElementKind.TimerContactNO, Pos = new GridPos(1, 0), DeviceName = "T001" };
        sheet.Elements.Add(timerCoil);
        sheet.Elements.Add(timerContact);
        var renderer = new ColorRecordingRenderer();
        var sim = new SimState();
        sim.TimerElapsed["T001"] = 10;   // 設定値5秒を超過

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains(DrawingTheme.Powered, renderer.SymbolColors);
    }
}

/// <summary>DrawLine/DrawCircle等に渡されるStrokeStyle.Colorのみを記録するIRendererのテストダブル
/// (DiagramRendererLabelTests.RecordingRendererのDrawText専用版と対になる色専用版)。
/// <para>
/// T-146(2026-08-08): 要素記号由来の色を <see cref="SymbolColors"/> へ分けて記録する。
/// DiagramRenderer.DrawElement は要素記号を PushTransform〜PopTransform で囲む
/// (r.PushTransform(X(lb), YRow(e.Pos.Row)) → SymbolGlyphs.Draw/PartDrawing.Draw → r.PopTransform())
/// ため、この入れ子の内側で記録された色だけが要素記号由来であり、外側は母線・自動横配線・枠・
/// 縦コネクタ等の色である。要素記号の色を測るテストはこちらを見ること。
/// </para></summary>
internal sealed class ColorRecordingRenderer : IRenderer
{
    /// <summary>PushTransform の入れ子の深さ。0 より大きい間は要素記号を描画中。</summary>
    private int _transformDepth;

    /// <summary>描画された全ての線色(母線・自動横配線・枠・要素記号を含む)。</summary>
    public List<Color> LineColors { get; } = new();

    /// <summary>要素記号のみの線色(DrawElement の PushTransform 内で記録されたもの)。</summary>
    public List<Color> SymbolColors { get; } = new();

    private void Record(Color color)
    {
        LineColors.Add(color);
        if (_transformDepth > 0) SymbolColors.Add(color);
    }

    public void PushTransform(double translateX, double translateY, double scale = 1.0) => _transformDepth++;
    public void PopTransform() => _transformDepth--;
    public void PushClip(Rect2D rect) { }
    public void PopClip() { }
    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) => Record(stroke.Color);
    public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke) => Record(stroke.Color);
    public void DrawRectangle(Rect2D rect, StrokeStyle stroke) => Record(stroke.Color);
    public void FillRectangle(Rect2D rect, Color color) { }
    public void DrawCircle(Point2D center, double radius, StrokeStyle stroke) => Record(stroke.Color);
    public void FillCircle(Point2D center, double radius, Color color) { }
    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke) => Record(stroke.Color);
    public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke) => Record(stroke.Color);
    public void DrawText(string text, Point2D position, TextStyle style) { }
    public Size2D MeasureText(string text, TextStyle style) => new(0, 0);
    public void DrawImage(string filePath, Rect2D bounds) { }
}
