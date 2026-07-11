# T-055増分2（シート設定ダイアログ）静的レビュー（隠密）

> 2026-07-10 隠密調査。家老采配、対象=commit 4a91c38（6ファイル・+251行）。
> 5観点で検証。code-reviewスキルはeffort=medium【必須】で実施
> （8フィンダー角度×最大6候補→重複排除8候補→1票検証、CONFIRMED1/PLAUSIBLE2/REFUTED2＋その他2件は元々指摘なし扱い）。

---

## 総合判定

**要修正候補1件（PLAUSIBLE、正しさ観点）＋cleanup指摘1件（CONFIRMED）あり**。
計画整合・RED証明整合・便乗拡大なしの3観点は妥当。実害は「データ消失」ではなく
表示・操作性の不整合に留まるが、DeleteRowCommandが持つ安全設計が新規コマンドに
引き継がれていない非対称であり、要修正か経過観察かの判断を家老に委ねる。

---

## 観点別判定

### (1) 計画書・殿裁定2点との整合 — 妥当

- Bus名（LeftName/RightName）空文字許容: `SheetSettingsDialog.xaml.cs`のOkButton_Clickでバリデーション
  なし、殿裁定（`docs/todo.md`T-055節「増分2着手前の殿裁定」）と一致。
- トリガーUI=左パレット設定ボタン: `MainWindow.xaml`に既存の＋/－/名前変更ボタン列と同型
  （Content+Margin+ToolTip+Click）で追加、殿裁定（「増分2トリガーUIの殿裁定」）と一致。
- 計画書（`docs/archive/ecad2-t055-implementation-plan-samurai.md`増分2節）の新設ダイアログ方針・
  専用DTO(record)方針とも一致（RenameDialog/AddSheetDialog同型を明記どおり踏襲）。

### (2) RED証明の整合 — 妥当

`Execute_OutOfRange_RejectsAndDoesNotChangeRows`（Theory: 0/61）のRED証明手法（範囲チェック
一時除去→不正値がそのまま代入されRED→復元でGREEN）を実装コードと突合し、論理的整合を確認した。

### (3) ダイアログ側/ViewModel側の二重ガード — **要修正候補あり（PLAUSIBLE）**

**Rows範囲（1〜60）の二重ガード自体は正しく機能している**（ダイアログ・ViewModel両方が
`GridSpec.MinRows`/`MaxRows`を参照、片方だけのザル穴なし）。

しかし、code-reviewスキル（3独立フィンダー角度=Angle A・C・Altitudeが一致して発見、1票検証で
**PLAUSIBLE**）により、**別種のガードが完全に欠落している**ことが判明した:

`UpdateSheetSettingsCommand`（`MainWindowViewModel.cs:1649-1664`）は`DeleteRowCommand`が持つ
`IsRowOccupied`（要素占有）チェックを一切行わずに`sheet.Grid.Rows = settings.Rows`を実行する。
DeleteRowCommandは1行の縮小でも最終行に要素があれば拒否するが、UpdateSheetSettingsCommandは
ダイアログ経由で一気に大きく縮小（例: 20→5）でき、その際に縮小範囲内（旧Rows〜新Rows）に
要素があっても一切チェックしない。

**実害の性質（検証エージェントが実装コードから確定、当初のfinding想定より軽微）**:
「データ消失」や「要素が見えなくなる」わけではない——`DiagramRenderer.TotalRows`
（`Math.Max(Grid.Rows, maxElementRow+1)`）が縮小後も要素の存在する行まで自動的に描画範囲を
再拡張するため、要素は引き続き描画される。実害は2点:
1. 見た目上は「行数を5に設定した」つもりでも、要素が残っていれば実際の描画範囲は縮小されない
   （ユーザーの意図と実際の表示が食い違う）。
2. キーボードによるセル移動（矢印キー）は新しい`Grid.Rows`でクランプされるため、縮小範囲外に
   残った要素へキーボードのみでは到達できなくなる（マウスクリックでは到達可能、
   `LadderCanvas.ToGridPos`にクランプなし）。本プロジェクトはキーボードファーストを主眼とするため、
   この非対称は無視できない可能性がある。

計画書の「開かれた論点2」（削除される最終行に要素がある場合の扱い）は「増分1・3共通の論点」と
明記されており、増分2（複数行を一気に縮小できるダイアログ経路）は論点整理の対象に含まれていない
——検証エージェントの判定では、意図的なスコープアウトというより単純な検討漏れの可能性が高い。

### (4) 増分1の教訓（SelectedCellクランプ・StatusMessageクリア）の先取り適用 — 機能面は妥当、実装方法にcleanup指摘あり（CONFIRMED）

**機能としては正しく先取り適用されている**（SelectedCellクランプ・StatusMessageクリアとも
DeleteRowCommand同型のロジックがUpdateSheetSettingsCommandにも実装済み、テストも用意されている）。

ただし、code-reviewスキル（1票検証、**CONFIRMED**）により、実装方法が**コピペによる3箇所目の
重複**であることが指摘された:
- SelectedCellクランプ式: `DeleteRowCommand`(1637-1638)・`UpdateSheetSettingsCommand`(1658-1659)の2箇所
- `StatusMessage = ""; MarkDirty(); NotifyCurrentSheetChanged();`トリプレット:
  `AddRowCommand`(1615-1617)・`DeleteRowCommand`(1642-1644)・`UpdateSheetSettingsCommand`(1660-1662)の3箇所

増分1レビュー時点（2箇所）では「尚早な一般化」としてREFUTEDだったが、今回3箇所目が加わり
rule of three閾値を超えた。さらに、この重複箇所自体がT-055増分1往復2周目で実際に「書き漏れ」
バグ（StatusMessage残留）を1度発生させた実績があり、共有ヘルパー抽出（例:
`ApplyRowCountChange(Sheet, int newRows)`）による再発防止の実効性がある、との判定。

### (5) 便乗拡大なし — 妥当

変更6ファイル（MainWindow.xaml/.xaml.cs・MainWindowViewModel.cs・
Views/SheetSettingsDialog.xaml/.xaml.cs・SheetSettingsCommandTests.cs）は計画書スコープ内。
SheetNavigationViewModel.cs等、増分1関連ファイルへの波及なし。

---

## code-review（effort=medium）まとめ

| 候補 | 判定 |
|---|---|
| UpdateSheetSettingsCommandにIsRowOccupiedガードが無い（Angle A・C・Altitude三重発見） | **PLAUSIBLE**（実害は表示・操作性の不整合、データ消失ではない） |
| 範囲外拒否パスでStatusMessageが未クリア（Angle A） | PLAUSIBLE（現行の唯一の呼び出し経路では到達不可能、理論上の安全弁の欠陥） |
| SelectedCellクランプ+StatusMessage/MarkDirty/Notifyトリプレットの重複（Reuse/Simplification/Altitude三重発見） | **CONFIRMED**（3箇所到達、抽出推奨、実際の書き漏れバグ実績あり） |
| パラメータ渡し方式の不統一（ValueTuple vs record） | REFUTED（計画書で事前検討済みの意図的選択） |
| dialog/ViewModel間のRange-check二重実装 | REFUTED（増分1で既にKISS原則により却下済みの設計判断の蒸し返し） |
| efficiency観点 | 該当なし |
| conventions(CLAUDE.md)観点 | 該当なし |

---

## 出典

- 差分: `git show 4a91c38`直読
- 計画書: `docs/archive/ecad2-t055-implementation-plan-samurai.md`増分2節
- 殿裁定: `docs/todo.md` T-055節
- 増分1レビュー先例: `docs/archive/ecad2-t055-increment1-review-onmitsu.md`
- code-reviewスキル実行記録: 本セッション内、Agent(finder)×8＋Agent(verifier)×5を並列実行
