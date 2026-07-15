# T-096拡張（タイマー接点上の残り時間リアルタイム表示）設計叩き台（侍）

作成日: 2026-07-15
対象: 殿直接要求「GuiEcadでは経過時間をタイマー接点上にリアルタイム表示される仕様になっている、
踏襲してほしい」への対応。T-096の拡張スコープとして同一タスク内で進める（家老裁可）。

## 0. 前提（GuiEcad原本調査結果・殿確認済み）

explorer調査（GuiEcad原本`C:\Users\kojif\Desktop\生産物\gui_ecad\`）で以下を確認した：

- 表示箇所: `GuiEcad.App/MainPage.Drawing.cs` `DrawTimerCountdowns`（194-240行目）、`OnDraw`
  末尾（189-190行目）から呼ばれる。
- **表示値は「経過時間」ではなく「残り時間」**（`remaining = Math.Max(0, sp - elapsed)`）。
  殿へ確認済み＝**残り時間表示（GuiEcad原本どおり）を踏襲**。
- 対象: 限時接点(TimerContactNO/NC)のみ、励磁中(計時中)かつ残り時間>0の場合のみ表示。
  時限到達後（残り時間0以下）は非表示。
- 見た目: 接点シンボル真上の淡黄色バッジ、"0.0"+"s"形式（例"2.3s"）、FontSizeMm=2.4・Bold・
  中央揃え。
- リアルタイム更新: `DispatcherTimer(100ms)`→`Stopwatch`実測dt→`Tick(dt)`→`Canvas.Invalidate()`
  という経路。

## 1. ecad2側の実装方針（GuiEcadとの構造差異）

GuiEcadは「通電色分け描画」をCore層(`DiagramRenderer`相当)、「タイマー残り時間バッジ描画」を
App層(`MainPage.Drawing.cs`)に分離している。**ecad2は既に通電色分け描画がCore層の
`DiagramRenderer.Render()`に統合されている**ため、一貫性を優先しCore層へ統合する（GuiEcadの
層分離を踏襲する必要はない、内部実装の構造差異でありUI/UX上の違いは生まない）。

**確認した結果、必要なデータは全てCore層に既存**：
- `netlist.TimerSetpoints`（`NetlistBuilder.BuildComponents`で既に構築、Setpoint値）
- `sim.TimerElapsed`（`SimState`、経過時間）
- `energized`（`Render()`内で`eval.State.Energized`から取得済み、励磁状態）

新規データ経路の追加は不要、`DiagramRenderer.Render()`内に`DrawTimerCountdowns`メソッドを
新設し、既存の要素描画ループ（306-307行目）の直後から呼ぶだけで実装できる。

## 2. App層は無改修（重要）

`MainWindow.xaml.cs`の`OnRealtimeTick`(474-484行目)は既に`session.Tick(dt)`→`RedrawCanvas()`
という経路で100ms周期の再描画を行っている。`RedrawCanvas()`→`LadderCanvasHost.Draw()`→
`DiagramRenderer.Render()`という既存の呼び出し経路にそのまま乗るため、**Core層への実装のみで
リアルタイム更新まで完結する**。App層の変更は不要と判断する。

## 3. 実装イメージ（DiagramRenderer.cs）

```csharp
// Render()内、要素描画ループ(306-307行目)の直後に追加
if (sim is not null)
    DrawTimerCountdowns(r, sheet, netlist, sim, energized, rowStart, rowEnd);
```

```csharp
/// <summary>テストモード中、計時中の限時タイマ接点(TimerContactNO/NC)の上に残り時間
/// (Setpoint-TimerElapsed)を小窓表示する(殿直接要求、GuiEcad完全踏襲)。時限到達後
/// (残り時間0以下)は接点が既に動作済みのため非表示。</summary>
private void DrawTimerCountdowns(IRenderer r, Sheet sheet, Netlist netlist, SimState sim,
                                  Dictionary<string, bool>? energized, int rowStart, int rowEnd)
{
    if (energized is null) return;
    foreach (var e in sheet.Elements)
    {
        if (e.Pos.Row < rowStart || e.Pos.Row >= rowEnd) continue;
        if (e.DeviceName is not string dev) continue;
        if (!PartResolver.CreatesComponent(e, _lib)) continue;
        var kind = PartResolver.ComponentKind(e, _lib);
        if (kind is not (ElementKind.TimerContactNO or ElementKind.TimerContactNC)) continue;
        if (!netlist.TimerSetpoints.TryGetValue(dev, out double sp)) continue;
        if (!energized.TryGetValue(dev, out var on) || !on) continue;

        double elapsed = sim.TimerElapsed.TryGetValue(dev, out var t) ? t : 0;
        double remaining = Math.Max(0, sp - elapsed);
        if (remaining <= 0) continue;

        var (l, right) = PartResolver.BoundarySpan(e, _lib);
        double cx = (X(l) + X(right)) / 2;
        double cy = YRow(e.Pos.Row) - Cell * 1.15;
        double w = Cell * 1.15, h = Cell * 0.55;
        var rect = new Rect2D(cx - w / 2, cy - h / 2, w, h);
        r.FillRectangle(rect, new Color(230, 255, 246, 200));
        r.DrawRectangle(rect, new StrokeStyle(new Color(255, 235, 170, 70), 0.25));
        var ts = _theme.Text(TextRole.LineNumber) with
        {
            FontSizeMm = 2.4, Bold = true, HAlign = HAlign.Center, VAlign = VAlign.Middle,
            Color = new Color(255, 30, 30, 30),
        };
        r.DrawText(remaining.ToString("0.0") + "s", new Point2D(cx, cy), ts);
    }
}
```

色・フォント・位置の数値はGuiEcad原本の値をそのまま踏襲する（完全踏襲、殿裁定）。

## 4. スコープ境界・影響範囲

- 対象は`DiagramRenderer.cs`のみ（Core層、テスト以外の追加変更なし）。
- PDF出力（`sim=null`で呼ばれる）には影響しない（`energized is null`で早期return、テスト
  モード画面表示専用の機能）。
- 既存の通電色分け・ラベル描画ロジックとは独立した追加描画のため、既存機能への影響なし。

## 5. 単体テスト方針

既存の`DiagramRendererLabelTests`と同型（`RecordingRenderer`でDrawText呼び出しを記録・検証）。
検証観点：
- 限時接点・励磁中・残り時間>0 → バッジテキスト("X.Xs"形式)が描画される
- 限時接点・非励磁 → 描画されない
- 限時接点・残り時間0以下(時限到達) → 描画されない
- 瞬時接点(TimerInstantContactNO/NC) → 対象外、描画されない（殿指摘の反映確認）
- sim=null（通常表示・PDF出力） → 描画されない

## 6. 実装規模の見立て（家老確認事項への回答）

Core層へのメソッド1つ追加＋単体テスト新設で完結する見込み。既存のリアルタイム更新機構
（DispatcherTimer→Tick→RedrawCanvas）にそのまま乗るため、**App層の変更・新規描画更新機構の
新設は不要**。増分分割の必要はないと判断する。

## 7. 未確定事項

- なし（GuiEcad完全踏襲・殿確認済みのため、数値・条件とも確定）。
