// T-041増分4 PoC: FreeLine(線分)・ConnectionDot(点)のヒットテスト計算の検証。
// 本体src/の型には依存せず、同型の計算ロジックをここに再実装して検証する(poc/隔離ルール)。
// 本実装(増分5)時は、この検証結果を踏まえてLadderCanvas.HitTestFreeLine/HitTestConnectionDotへ
// 反映する想定。

const double ToleranceMm = 2.0; // HitTestConnector/HitTestWireBreakと同値を出発点にする

double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
{
    double dx = x2 - x1, dy = y2 - y1;
    double lenSq = dx * dx + dy * dy;
    if (lenSq < 1e-9) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
    double t = ((px - x1) * dx + (py - y1) * dy) / lenSq;
    t = Math.Clamp(t, 0.0, 1.0);
    double cx = x1 + t * dx, cy = y1 + t * dy;
    return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
}

double DistancePointToPoint(double px, double py, double x, double y)
    => Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));

var results = new List<(string Name, bool Pass, string Detail)>();

void Check(string name, bool expectHit, double distance)
{
    bool actualHit = distance <= ToleranceMm;
    bool pass = actualHit == expectHit;
    results.Add((name, pass, $"distance={distance:F3}mm, expectHit={expectHit}, actualHit={actualHit}"));
}

// --- FreeLine(線分)のケース ---

// 1. セグメント中点への直撃(距離ほぼ0) → 選択されるべき
{
    double d = DistancePointToSegment(px: 50, py: 20, x1: 40, y1: 20, x2: 60, y2: 20);
    Check("FreeLine_ClickAtMidpoint", expectHit: true, d);
}

// 2. セグメントから垂直に1.5mm(許容誤差2.0mm以内) → 選択されるべき
{
    double d = DistancePointToSegment(px: 50, py: 21.5, x1: 40, y1: 20, x2: 60, y2: 20);
    Check("FreeLine_1_5mmPerpendicular_WithinTolerance", expectHit: true, d);
}

// 3. セグメントから垂直に3.0mm(許容誤差2.0mm超) → 選択されないべき
{
    double d = DistancePointToSegment(px: 50, py: 23.0, x1: 40, y1: 20, x2: 60, y2: 20);
    Check("FreeLine_3_0mmPerpendicular_BeyondTolerance", expectHit: false, d);
}

// 4. セグメント端点の外側(延長線上ではなく実際の端点距離で判定されるべき)
//    端点(60,20)から右へ5mm・上へ1mmの位置。単純な直線距離への射影ではなく、
//    クランプ後の最近点(=端点そのもの)からの距離になっているか確認する。
{
    double d = DistancePointToSegment(px: 65, py: 21, x1: 40, y1: 20, x2: 60, y2: 20);
    double expectedDistance = DistancePointToPoint(65, 21, 60, 20); // 端点からの直線距離
    Check("FreeLine_BeyondEndpoint_ClampsToEndpoint",
        expectHit: expectedDistance <= ToleranceMm, d);
    if (Math.Abs(d - expectedDistance) > 1e-6)
        results.Add(("FreeLine_BeyondEndpoint_DistanceMatchesEndpointCalc", false,
            $"計算値{d:F3}が端点直線距離{expectedDistance:F3}と不一致"));
    else
        results.Add(("FreeLine_BeyondEndpoint_DistanceMatchesEndpointCalc", true, "端点距離と一致"));
}

// 5. 近接する2本のセグメント(3mm離れて平行)から、より近い方が選ばれるべき(nearest-wins設計)
//    HitTestConnectorの「先頭一致」方式(隠密レビュー所見、severity低)を踏まえ、
//    FreeLineでは全候補の中から最短距離のものを選ぶ設計を検証する。
{
    var segments = new (double X1, double Y1, double X2, double Y2)[]
    {
        (40, 20, 60, 20),   // セグメントA: y=20
        (40, 23, 60, 23),   // セグメントB: y=23 (Aより3mm下)
    };
    double clickY = 21.2; // Aまで1.2mm、Bまで1.8mm → Aが近い
    double best = double.MaxValue;
    int bestIndex = -1;
    for (int i = 0; i < segments.Length; i++)
    {
        double d = DistancePointToSegment(50, clickY, segments[i].X1, segments[i].Y1, segments[i].X2, segments[i].Y2);
        if (d < best) { best = d; bestIndex = i; }
    }
    bool pass = bestIndex == 0; // セグメントAが選ばれるべき
    results.Add(("FreeLine_NearestWins_TwoCloseSegments", pass,
        $"selected=segment{bestIndex}(expected=segment0), distA={DistancePointToSegment(50, clickY, 40, 20, 60, 20):F3}, distB={DistancePointToSegment(50, clickY, 40, 23, 60, 23):F3}"));
}

// --- ConnectionDot(点)のケース ---

// 6. 点への直撃
{
    double d = DistancePointToPoint(50, 20, 50, 20);
    Check("ConnectionDot_ClickAtPoint", expectHit: true, d);
}

// 7. 点から1.5mm(許容誤差以内)
{
    double d = DistancePointToPoint(51.0, 21.1, 50, 20);
    Check("ConnectionDot_1_5mmAway_WithinTolerance", expectHit: true, d);
}

// 8. 点から3.0mm(許容誤差超)
{
    double d = DistancePointToPoint(50, 23.0, 50, 20);
    Check("ConnectionDot_3_0mmAway_BeyondTolerance", expectHit: false, d);
}

// --- 出力 ---

int passCount = results.Count(r => r.Pass);
var lines = new List<string>
{
    "T-041増分4 PoC結果: FreeLine/ConnectionDot ヒットテスト計算の検証",
    $"許容誤差(ToleranceMm) = {ToleranceMm}mm (HitTestConnector/HitTestWireBreakと同値の出発点)",
    "",
};
foreach (var (name, pass, detail) in results)
    lines.Add($"[{(pass ? "PASS" : "FAIL")}] {name} — {detail}");
lines.Add("");
lines.Add($"合計: {passCount}/{results.Count} PASS");
lines.Add("");
lines.Add("--- ズーム安全性についての所見(実行検証ではなく構造分析) ---");
lines.Add("MainWindow.xaml: <views:LadderCanvas.LayoutTransform><ScaleTransform .../></views:LadderCanvas.LayoutTransform>");
lines.Add("クリックハンドラは e.GetPosition(LadderCanvasHost) を使用しており、WPFの仕様上");
lines.Add("GetPosition(対象自身) は対象要素自身のLayoutTransform適用前のローカル座標を返す");
lines.Add("(既存コードのToGridPos/HitTestConnectorのコメントでも明記済み)。");
lines.Add("したがってCanvasScale(ズーム倍率)が変化しても、position引数は既にLayoutTransform");
lines.Add("適用前のDIP座標であり、本PoCで検証した距離計算・しきい値判定への影響は無い。");
lines.Add("実機でのズーム時挙動確認は、既存のHitTestConnector(増分1)と同型のためリスクは");
lines.Add("低いと判断し、本PoCでは計算ロジックの正しさ検証を主眼とした。");

string outPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "poc-result.txt");
File.WriteAllLines(Path.GetFullPath(outPath), lines);
foreach (var line in lines) Console.WriteLine(line);
