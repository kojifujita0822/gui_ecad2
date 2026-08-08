using Ecad2.Model;

namespace Ecad2.Simulation;

/// <summary>
/// テストモードの実行状態を管理する。入力（押ボタン・セレクトSW位置・補助）を操作して評価し、
/// 励磁状態を次回評価へ持ち越す（実リレーの自己保持を再現）。
/// UI はこの State を <see cref="Ecad2.Rendering.DiagramRenderer.Render"/> に渡して通電ハイライトする。
/// </summary>
public sealed class TestSession
{
    private readonly Sheet _sheet;
    private readonly PartLibrary? _lib;

    public SimState State { get; } = new();
    public EvalResult? Result { get; private set; }

    public TestSession(Sheet sheet, PartLibrary? lib = null)
    {
        _sheet = sheet;
        _lib = lib;
    }

    /// <summary>
    /// 現在の入力＋持ち越した励磁状態で評価する。
    /// 主回路シート（<see cref="Sheet.MainCircuit"/>）では評価を走らせず、空の結果を返す
    /// （T-146、殿裁定2026-08-08「主回路でテストモード評価を止める」）。
    /// 画面の通電ハイライトは <see cref="Ecad2.Rendering.DiagramRenderer.Render"/> が
    /// 独立に評価しており、本メソッドを止めるだけでは消えない（あちらにも同じガードがある）。
    /// </summary>
    public EvalResult Evaluate()
    {
        if (_sheet.MainCircuit) return Result = new EvalResult();

        var net = NetlistBuilder.Build(_sheet, _lib);
        var res = new Evaluator(net).Evaluate(State);
        State.Energized.Clear();
        foreach (var kv in res.State.Energized) State.Energized[kv.Key] = kv.Value;  // 自己保持の持ち越し
        Result = res;
        return res;
    }

    public void SetInput(string device, bool on) { State.Inputs[device] = on; Evaluate(); }

    public void ToggleInput(string device)
        => SetInput(device, !(State.Inputs.TryGetValue(device, out var v) && v));

    public void SetPosition(string device, int notch) { State.Positions[device] = notch; Evaluate(); }

    /// <summary>
    /// 経過時間を <paramref name="dt"/> 秒進める。励磁中のタイマコイルの経過時間を加算し、
    /// 消磁されたタイマコイルの経過時間をリセットする。その後評価を実行する。
    /// 主回路シートでは時間も進めない（<see cref="Evaluate"/> のガードと対、T-146）。
    /// </summary>
    public EvalResult Tick(double dt)
    {
        if (_sheet.MainCircuit) return Evaluate();

        var net = NetlistBuilder.Build(_sheet, _lib);
        foreach (var (device, _) in net.TimerSetpoints)
        {
            bool on = State.Energized.TryGetValue(device, out var e) && e;
            if (on)
            {
                State.TimerElapsed[device] =
                    (State.TimerElapsed.TryGetValue(device, out var prev) ? prev : 0.0) + dt;
            }
            else
            {
                State.TimerElapsed.Remove(device);
            }
        }
        return Evaluate();
    }

    public bool IsEnergized(string device)
        => Result is not null && Result.State.Energized.TryGetValue(device, out var v) && v;

    /// <summary>不定発振（収束も周期も検出できなかった）。</summary>
    public bool IsOscillating => Result?.Status == EvalStatus.Diverging;
    /// <summary>周期的振動（フリッカ・ブザー等）。</summary>
    public bool IsCyclic => Result?.Status == EvalStatus.Cyclic;
}
