using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Simulation;

namespace Ecad2.Core.Tests;

/// <summary>
/// タイマー接点上の残り時間リアルタイム表示(殿直接要求2026-07-15、GuiEcad完全踏襲、T-096拡張)の
/// 回帰テスト。DrawTimerCountdowns(DiagramRenderer)が、計時中の限時タイマ接点(TimerContactNO/NC)
/// の上に残り時間(Setpoint-TimerElapsed)を"X.Xs"形式で描画すること、瞬時接点(殿指摘=対象外)・
/// 非励磁・時限到達後は描画しないことを検証する。DiagramRendererTestModeColorTestsと同型の
/// 手法(母線間直結の単独コイル配置で常時励磁を作る)を使う。
/// </summary>
public class DiagramRendererTimerCountdownTests
{
    // コイル(DeviceName=deviceName、Params[Setpoint]設定、母線間直結で常時励磁)+
    // タイマ接点(kind、同DeviceName、宙に浮いた別行配置)の2要素シート。
    private static Sheet MakeSheet(ElementKind contactKind, string deviceName, string setpoint, bool includeCoil = true)
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 }, MainCircuit = true };
        if (includeCoil)
        {
            var coil = new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(0, 5), DeviceName = deviceName };
            coil.Params[ParamKeys.Setpoint] = setpoint;
            sheet.Elements.Add(coil);
        }
        sheet.Elements.Add(new ElementInstance { Kind = contactKind, Pos = new GridPos(1, 5), DeviceName = deviceName });
        return sheet;
    }

    [Fact]
    public void Render_限時接点_計時中は残り時間を描画する()
    {
        var sheet = MakeSheet(ElementKind.TimerContactNO, "T1", "5");
        var renderer = new RecordingRenderer();
        var sim = new SimState();
        sim.TimerElapsed["T1"] = 2.0;

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.Contains("3.0s", renderer.DrawnTexts);
    }

    [Fact]
    public void Render_限時接点_未経過なら設定時間そのままを描画する()
    {
        var sheet = MakeSheet(ElementKind.TimerContactNC, "T1", "5");
        var renderer = new RecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.Contains("5.0s", renderer.DrawnTexts);
    }

    [Fact]
    public void Render_限時接点_時限到達後は描画しない()
    {
        var sheet = MakeSheet(ElementKind.TimerContactNO, "T1", "5");
        var renderer = new RecordingRenderer();
        var sim = new SimState();
        sim.TimerElapsed["T1"] = 5.0;

        new DiagramRenderer().Render(renderer, sheet, sim: sim);

        Assert.DoesNotContain(renderer.DrawnTexts, t => t.EndsWith("s"));
    }

    [Fact]
    public void Render_限時接点_非励磁なら描画しない()
    {
        // コイルを配置しない=energized辞書にDeviceNameのエントリが存在しない(非励磁扱い)。
        var sheet = MakeSheet(ElementKind.TimerContactNO, "T1", "5", includeCoil: false);
        var renderer = new RecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.DoesNotContain(renderer.DrawnTexts, t => t.EndsWith("s"));
    }

    [Theory]
    [InlineData(ElementKind.TimerInstantContactNO)]
    [InlineData(ElementKind.TimerInstantContactNC)]
    public void Render_瞬時接点は対象外(ElementKind contactKind)
    {
        var sheet = MakeSheet(contactKind, "T1", "5");
        var renderer = new RecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet, sim: new SimState());

        Assert.DoesNotContain(renderer.DrawnTexts, t => t.EndsWith("s"));
    }

    [Fact]
    public void Render_作画モード_sim無しでは描画しない()
    {
        var sheet = MakeSheet(ElementKind.TimerContactNO, "T1", "5");
        var renderer = new RecordingRenderer();

        new DiagramRenderer().Render(renderer, sheet);   // sim=null

        Assert.DoesNotContain(renderer.DrawnTexts, t => t.EndsWith("s"));
    }
}
