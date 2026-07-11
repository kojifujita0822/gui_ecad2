# T-055増分1（末尾行の追加・削除）静的レビュー（隠密）

> 2026-07-10 隠密調査。家老采配、対象=commit 6a6eaf7（5ファイル・+326行）。
> 5観点（計画整合・RED証明整合・早期returnトラップ/CanExecute再評価漏れ・便乗拡大なし・
> code-reviewスキル併用）で検証。code-reviewスキルはeffort=highで実施
> （8フィンダー角度×最大6候補→重複排除8候補→1票検証、CONFIRMED1/PLAUSIBLE1/REFUTED6）。

---

## 総合判定

**要修正指摘1件あり**（正しさ観点、CONFIRMED）。他4観点は妥当。実害はデータ破損・誤配置ではなく
表示層の陳腐化に限定される。便乗拡大禁止の原則も踏まえ、要修正とするか軽微指摘として残置するかの
判断は家老に委ねる。

---

## 観点別判定

### (1) 計画書・殿裁定との整合 — 妥当

- ツールバー大型ボタン様式（`ToolBarButtonStyle`同型）: `MainWindow.xaml`の新規2ボタンは殿裁定1
  どおり実装。
- 削除拒否=広義5種すべて: `IsRowOccupied`（`MainWindowViewModel.cs:1306-1311`）は
  ElementInstance/VerticalConnector/WireBreak/GroupFrame/RungCommentの5種を判定、殿裁定
  （`docs/todo.md`補足裁定）と一致。
- クランプ上限60/下限1: `GridSpec.MaxRows=60`/`MinRows=1`（`Sheet.cs`）と一致。
- 既定10不変: `GridSpec`クラス自体のプロパティ初期値は22のままだが、実際のシート作成経路
  （`SheetNavigationViewModel.AddCommand`・`MainWindowViewModel.NewDocument`）はいずれも
  明示的に`new GridSpec{Rows=10,...}`を指定しており、既定値10は変更されていない（引き継ぎ書の
  確認どおり、増分1のスコープ外）。
- 警告はStatusMessage: `DeleteRowCommand`は`StatusMessage = "最終行に要素があるため削除できません";`
  を実装、方針と一致。

### (2) RED証明3件の整合 — 妥当

- 上限クランプ（`AddRowCommand_Execute_AtMaxRows_DoesNotExceedMax`）・下限クランプ
  （`DeleteRowCommand_Execute_AtMinRows_DoesNotGoBelowMin`）・削除拒否5種Theory
  （`DeleteRowCommand_Execute_WhenLastRowOccupied_RejectsAndDoesNotDecreaseRows`）の3件とも、
  コミットメッセージが述べるガード除去手順（`>=/<=`ガード削除、`IsRowOccupied`をfalse固定）で
  当該Assertが実際に破綻する論理を、実装コードとテストコードの突合により確認した。
  RED証明はガード経路を正しく突いている。

### (3) 新規プロパティ/セッタの早期returnトラップ・CanExecuteの再評価漏れ — **要修正指摘1件**

**CONFIRMED（code-review Angle B/C重複検出、1票検証で確定）**:
`DeleteRowCommand`（`MainWindowViewModel.cs:1607-1620`）は`sheet.Grid.Rows--`実行後、
`SelectedCell`/`SelectedCellDisplay`をクリア・再クランプしない。`NotifyCurrentSheetChanged()`
（=`NotifyCurrentSheetDependentPropertiesChanged`）は`CurrentSheet`/`IsMainCircuitSheet`/
`IsControlCircuitSheet`のみ通知し`SelectedCell`系は含まない。同ファイル内の類似処理
（`SetCurrentSheetIndexCore`・`ReplaceDocument`）は選択状態の連鎖クリアを明示的に行っており、
`DeleteRowCommand`だけがこのパターンから逸脱している。

**再現シナリオ**: Grid.Rows=10の状態で空の最終行(row9)を選択→行を削除(Ctrl+Shift+Downまたは
ボタン)実行→`IsRowOccupied`はfalse（選択セル自体は空）なので削除は進行しRows=9になるが、
`SelectedCell`はrow9を指したまま(範囲外)残る。`LadderCanvas.Draw`（`LadderCanvas.cs:128-129`）は
`IsWithinGridBounds`相当のチェック無しに選択ハイライト矩形を無条件描画するため、縮小後グリッドの
範囲外に古い選択ハイライトが表示され続ける（次のクリック・矢印キー操作で自己修正されるまで）。

**実害の範囲**: データ破損・誤配置には至らない——配置操作自体は`ValidatePlacement`
（`IsWithinGridBounds`込み）が別途ガードするため安全。実害は表示層（ハイライト矩形・
`SelectedCellDisplay`のステータスバー表示）の一時的な陳腐化に限定される。

CanExecute再評価漏れについては指摘なし——`RelayCommand.CanExecuteChanged`は
`CommandManager.RequerySuggested`委譲（既存の確立済み方式）で、`AddRowCommand`/
`DeleteRowCommand`のCanExecuteはExecute内ガードと対称的に一致しており問題なし。

### (4) 便乗拡大なし — 妥当

変更5ファイル（`MainWindow.xaml`・`MainWindow.xaml.cs`・`MainWindowViewModel.cs`・`Sheet.cs`・
`RowCommandsTests.cs`）はいずれも計画書「3. スコープ境界」の想定範囲内。
`SheetNavigationViewModel.cs`（増分1では触らない設計）への波及なし。

### (5) code-reviewスキル併用（effort=high） — 実施済み、8候補中1件CONFIRMED

Phase1（8フィンダー角度）で挙がった候補を重複排除し8件に整理、Phase2（1票検証、recall-biased）:

| 候補 | 判定 |
|---|---|
| DeleteRowCommandがSelectedCellをクリアしない | **CONFIRMED**（上記(3)と同一） |
| IsRowOccupiedがmm座標系要素(FreeLine等)を見落とす | REFUTED（該当型に行の概念自体が無く、殿裁定で明示的に5種スコープ確定済み、データ損失なしと実測確認） |
| IsRowOccupiedが既存IsSelectedCellOccupied/ValidatePlacementと重複 | REFUTED（単一セル判定と行全体判定は問いが根本的に異なり、意味のある共有は非現実的） |
| AddRowCommand/DeleteRowCommandのICommand配線がMainWindowViewModel内で唯一 | REFUTED（RelayCommandはT-009以来の確立済みプロジェクト規約、Click=/Command=混在は本コミット以前から存在） |
| IsRowOccupiedが増分3で再利用できない一回限りの実装 | PLAUSIBLE（増分1の欠陥ではないが増分3設計時の申し送り事項として妥当） |
| 行数変更+MarkDirty+NotifyCurrentSheetChangedの共有プリミティブ化なし | REFUTED（2行×2箇所のみ、尚早な一般化＝KISS原則に反する） |
| ツールバーXAML構造のコピペ | REFUTED（既存21箇所の確立済み慣習の踏襲、本コミットのスコープ外） |

---

## 出典

- 差分: `git show 6a6eaf7`直読
- 計画書: `docs/archive/ecad2-t055-implementation-plan-samurai.md`
- 引き継ぎ書: `docs-notes/handover-samurai-t055-round1.md`
- 殿裁定: `docs/todo.md` T-055節
- code-reviewスキル実行記録: 本セッション内、Agent(finder)×8＋Agent(verifier)×7を並列実行
