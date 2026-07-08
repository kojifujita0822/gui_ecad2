# T-041 増分6 PoC — ドラッグUI基盤（マウスキャプチャ・移動・端点リサイズ・Escキャンセル）の検証

作成: 2026-07-08（侍）。家老采配「増分6(ドラッグUI基盤PoC)を`poc/`配下で先行検証」に対する成果。
対象: 実装プラン`docs/ecad2-t041-implementation-plan-samurai.md`4.2節「増分6 PoCで確認したい項目」。

## 目的

ecad2に前例の無い`CaptureMouse`/`MouseMove`によるドラッグ追従の実装（線分プリミティブの
本体移動・端点リサイズ・Escキャンセル・ドラッグ中の再描画）が技術的に成立するか、本実装
（増分7）前に検証する。増分4（幾何ヒットテスト）のPoCと異なり、マウスキャプチャ中の
インタラクション・キーボードイベントの受信という「UIありでなければ検証できない」項目が
中心のため、本PoCはWPFアプリとして実装し、UI Automation経由で実機的に対話操作させて検証した
（`.claude/skills/ecad2-ui-automation`と同じ手法をT041DragPoc向けに転用、詳細は「検証方法」節）。

対象プリミティブは`VerticalConnector`（グリッド系・線分、`GridGeometry`をそのまま使える）1本。
線分系（`VerticalConnector`/`FreeLine`）・点系（`WireBreak`/`ConnectionDot`）の構造的対称性
（実装プラン2節）により、線分1種で技術検証すれば知見は他3種にも展開できると判断した。

## 検証項目と結果（4.2節①〜④、いずれも実機確認済み）

| # | 確認項目 | 結果 |
|---|---|---|
| ① | マウスキャプチャ中のリアルタイム再描画コスト | **PASS**。実際の`DiagramRenderer.Render`を毎ドラッグ操作ごとに呼び出し実測。起動時ベンチマーク(500回平均)は0.44〜0.52ms/回、対話ドラッグ中も0.78〜0.93ms/回。60fps許容枠(16.6ms)に対して十分余裕があり、毎フレーム再描画しても性能上の懸念は無い。 |
| ② | ドラッグ開始判定(クリックと区別する移動量しきい値) | **PASS**。しきい値4px(DIP)を設け、しきい値未満の移動でボタンを離した場合は「クリック扱い(無変化)」、超えた場合のみドラッグとして状態を更新することを確認。 |
| ③ | 本体移動/端点リサイズの起点区別 | **PASS**。線分の上端点・下端点それぞれから2.0mm以内(既存`ConnectorHitToleranceMm`と同値)をリサイズ起点、それ以外の線分近傍を本体移動起点と判定するロジックで、意図通り区別できることを確認(本体移動→Top/Bottomとも平行移動、上端点リサイズ→Topのみ、下端点リサイズ→Bottomのみが変化)。 |
| ④ | Escによるドラッグ中キャンセル(掴んだ位置に戻す) | **PASS**（後述の所見対応後）。ドラッグ中にEsc押下で、ドラッグ開始時点の`TopRow`/`BottomRow`へ復元し、マウスキャプチャも解放されることを確認。 |

## 重要な所見: 親ScrollViewerに`FocusManager.IsFocusScope="True"`が無いとEscが届かない

④の検証で、当初Escキーがキャンバスへ全く届かない不具合に遭遇した（`Window`レベルの
`PreviewKeyDown`は受信できるが、`FocusManager.GetFocusedElement`が空、`Keyboard.Focus(this)`が
実際には効かず`ScrollViewer`側にフォーカスが残ってしまう）。

原因は、既存`src/Ecad2.App/Views/LadderCanvas.cs`のコメントに明記されている既知パターンと同一
だった: **キャンバスを含む親要素（本PoCでは`ScrollViewer`）へ`FocusManager.IsFocusScope="True"`
を明示的に設定しないと、`Keyboard.Focus`によるフォーカス取得が安定しない**。本体
`MainWindow.xaml`の`CanvasArea`(ScrollViewer)には既にこの設定が入っており（T-002/T-006で検証済み
のパターン）、**本実装（増分7）では同じ問題は再発しない**と考えられる。本PoCの
`MainWindow.xaml`にも同じ設定を追加したところEscが正しく届くようになった（`DragCanvas.cs`の
コメント参照）。

この所見自体が、増分7実装時に「FocusScope設定を忘れずに踏襲する」というチェック項目として
価値があると考える。

## 検証方法（UI Automation経由の実機操作）

`.claude/skills/ecad2-ui-automation`と同じ設計思想（Name/座標ではなくプロセス操作＋
UI Automationでの実機確認、`SendKeys`経由でのキー送信が確実に届く）を、プロセス名を
`T041DragPoc`に差し替えて転用した一時ヘルパー（本PoC検証専用、リポジトリには含めず）で、
実際にマウスドラッグ（`SetCursorPos`+`mouse_event`の複数ステップ合成）・Escキー送信
（`System.Windows.Forms.SendKeys`）を行い、ステータステキスト（`TopRow`/`BottomRow`の実値・
モード・Render1回あたりのコスト）とスクリーンショットで結果を確認した。

`keybd_event`直呼びでのEsc送信は屆かず（原因は上記所見のフォーカス問題、キー送信手法の問題
ではなかった）、`SendKeys`に切り替えて解決した。ドラッグ中にキー送信する場合は、
マウスダウン＋移動（`BeginDragMoveTo`）とマウスアップ（`EndDrag`）を分離し、その間で
`SendKeys`を呼ぶ2段階方式が必要（ネイティブの`mouse_event`/`keybd_event`だけを1メソッド内で
連続実行する方式では、Escがドラッグ中の状態に届かないケースがあった）。

## 侍の推奨（増分7本実装への引き継ぎ案）

1. `LadderCanvas`（`src/Ecad2.App/Views/LadderCanvas.cs`）に、本PoCの`DragCanvas`と同型の
   `MouseLeftButtonDown`/`MouseMove`/`MouseLeftButtonUp`/`PreviewKeyDown`ハンドラを追加する形で
   実装する。ヒットテスト（本体/端点の判定）は既存の`HitTestConnector`等と同じ許容誤差
   （2.0mm）を起点に、実機確認で見直す。
2. 親要素（`MainWindow.xaml`の`CanvasArea`）に`FocusManager.IsFocusScope="True"`が既に設定済み
   のため、Esc受信の問題は増分7では再発しない見込み。念のため実装後の忍者検証で確認する。
3. ドラッグ中の再描画コストは実測で十分な余裕（1ms未満）があり、`DiagramRenderer.Render`を
   毎`MouseMove`で呼ぶ既存パターン（`LadderCanvas.Draw`）をそのまま流用してよい。追加の
   最適化（差分描画等）は不要と判断する。
4. 線分系（`VerticalConnector`/`FreeLine`）は本PoCのロジックをそのまま横展開できる。点系
   （`WireBreak`/`ConnectionDot`）は端点リサイズが存在しないため、本体移動のみのシンプルな
   ドラッグ（本PoCの`DragMode.Move`相当のみ）で足りる。
5. キーボード等価操作（4.3節・6節、殿裁定待ち）は、本PoCのドラッグ状態モデル
   （`TopRow`/`BottomRow`の直接書き換え）とは独立に、矢印キー等から同じプロパティを更新する
   形で無理なく統合できる見込み。
6. 本PoCは`poc/`に隔離したまま残す（本体src/とは分離）。

## 実行方法

```
dotnet run --project poc/t041-drag-poc/T041DragPoc
```

起動直後にRenderベンチマーク（500回）が自動実行され、結果がウィンドウ上部に表示される。
マウスで線分中央付近をドラッグ→本体移動、上端/下端付近をドラッグ→端点リサイズ、ドラッグ中に
Escでキャンセル、を対話的に試せる。

## 構成

- `T041DragPoc/DragCanvas.cs` — ドラッグ状態機械（ヒットテスト・しきい値判定・移動/リサイズ・
  Escキャンセル・再描画コスト計測）。`src/Ecad2.App/Views/LadderCanvas.cs`の描画パターン
  （`DiagramRenderer`/`WpfRenderer`/`GridGeometry`）をそのまま流用。
- `T041DragPoc/MainWindow.xaml(.cs)` — ホストウィンドウ。ステータス表示・起動時ベンチマーク実行。
