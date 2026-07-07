# T-041 増分4 PoC — FreeLine/ConnectionDot ヒットテスト計算の検証

作成: 2026-07-07（侍）。家老采配「増分4(FreeLine/ConnectionDot幾何ヒットテストPoC)を
poc/配下で先行検証、隠密レビュー待ちにせず並行着手可」に対する成果。

## 目的

`FreeLine`（mm実座標の2点線分）・`ConnectionDot`（mm実座標の1点）のクリック選択に必要な
幾何ヒットテスト（点と線分の距離・点と点の距離）が、ecad2に前例の無いロジックであるため
（実装プラン`docs/ecad2-t041-implementation-plan-samurai.md`4.2節PoC対象）、本実装前に
計算の正しさ・しきい値の妥当性・近接プリミティブでの選択優先を検証する。

## 検証範囲の見直し（重要な所見）

当初プランでは「ズーム時（150%等）のクリック許容誤差が破綻しないか」もPoC対象としていたが、
`MainWindow.xaml`を確認したところ`LadderCanvas.LayoutTransform`に`ScaleTransform`（`CanvasScale`
バインド）が適用されており、クリックハンドラは`e.GetPosition(LadderCanvasHost)`でローカル座標を
取得している。WPFの仕様上`GetPosition(対象自身)`は**その要素自身のLayoutTransform適用前の
ローカルDIP座標**を返すため（既存の`ToGridPos`/`HitTestConnector`のコメントでも明記済み、
増分1で実装済みの縦コネクタヒットテストも同じ前提で正しく動作している）、ズーム倍率は
本PoCで検証する距離計算・しきい値判定に影響しない。**したがって本PoCはUI付きの対話的検証は
行わず、計算ロジック単体をコンソールで検証する形とした**（ズーム安全性は構造的に保証される
という所見自体が本PoCの成果の一つ）。

## 検証シナリオ（`Program.cs`、いずれもコンソール出力・`poc-result.txt`へも保存）

1. **FreeLine**: セグメント中点への直撃／垂直1.5mm(許容誤差2.0mm以内)／垂直3.0mm(許容誤差超)／
   端点の外側（延長線ではなく実際の端点距離で判定されるか、クランプ処理の正しさ）
2. **FreeLine近接優先**: 3mm離れた平行な2セグメントから、より近い方を選ぶ「nearest-wins」設計の
   検証（`HitTestConnector`の「先頭一致」方式に対する改善点、隠密レビュー所見#B相当の反映）
3. **ConnectionDot**: 点への直撃／1.5mm(許容誤差以内)／3.0mm(許容誤差超)

## 実行方法

`dotnet run --project T041FreeLineHitTestPoc`

## 結果（2026-07-07、`poc-result.txt`参照）

全9件PASS。許容誤差はHitTestConnector/HitTestWireBreakと同値の2.0mmを出発点とした
（実機での使用感確認は増分5実装後、忍者検証で行う想定）。

## 侍の推奨（増分5本実装への引き継ぎ案）

1. `LadderCanvas`に`HitTestFreeLine`/`HitTestConnectionDot`を新設し、本PoCの
   `DistancePointToSegment`/`DistancePointToPoint`と同型のロジックを移植する。
2. `HitTestFreeLine`は本PoCで検証した「nearest-wins」（全候補中の最短距離を選ぶ）方式を採用する。
   `HitTestConnector`の「先頭一致」方式は変更しない（増分1の既存挙動を崩さない、隠密レビューでも
   severity低と判断済み）。
3. しきい値2.0mmは暫定値のまま増分5へ引き継ぎ、忍者実機確認で見直しの要否を判断する。
4. ズーム安全性の検証は不要（本README「検証範囲の見直し」参照、構造的に保証されるため）。
5. 本PoCは`poc/`に隔離したまま残す（本体src/とは分離）。

## 構成

- `T041FreeLineHitTestPoc/Program.cs` — 距離計算ロジック＋検証シナリオ（top-level statements）
- `T041FreeLineHitTestPoc/poc-result.txt` — 実行結果（`dotnet run`で都度上書き）
