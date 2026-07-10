# T-055 増分3 往復1周目 コードレビュー（隠密）

対象: コミット `33a59f9`（ContextMenu基盤・RowOps・InsertRowBeforeCommand/DeleteRowAtCommand）。
家老采配（2026-07-10）の4観点手動確認＋`code-review`スキル（high）を併用。

---

## 1. 家老指定4観点（手動確認）

| 観点 | 判定 | 根拠 |
|---|---|---|
| 1. 挿入処理の5種要素シフトが調査書§2.2のGuiEcad規則と整合するか | **OK** | `RowOps.InsertRow`はGuiEcad `RowOps.ShiftRows`(inclusive=true)の閾値・加算方向と完全一致（実物照合済み） |
| 2. GroupFrame内部挿入時のHeight伸縮規則(§2.3)、計画書追記の整合性 | **OK** | `RowOps.cs:28-32`のGroupFrame分岐がGuiEcad `InsertRowCommand.Execute`と一致。計画書差分(`docs/ecad2-t055-implementation-plan-samurai.md`)も同規則を正確に追記 |
| 3. ContextMenu基盤が推奨アプローチ(§1)に沿っているか | **OK** | `ToGridPos`で行確定→コードビハインドで動的生成→`RelayCommand`実行、調査書推奨どおり |
| 4. 「削除対象行に要素なし」ケースの削除処理が正しいか | **OK** | `IsRowOccupied`（開始行・内部行とも占有判定）で事前ガードしており、`RowOps.DeleteRow`の契約（対象行に要素なし）と整合。契約違反経路は現状存在しない |

4観点はいずれも要修正なし。ただし`code-review`スキル併用で、4観点の範囲外から重大な見落としが1件見つかった（§2-1参照）。

---

## 2. `code-review`スキル（high、8角度→10候補→1-vote verify）

### 2-1. 要修正（CONFIRMED、正しさバグ）

**a. `SelectedCell`がRowシフトに追随しない**
`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1685`付近。
`InsertRowBeforeCommand`/`DeleteRowAtCommand`は`RowOps`で5種要素のRowを実際にシフトするが、`SelectedCell`（`GridPos?`、キーボード操作の選択カーソル座標そのもの）は一切更新しない。`SelectedElement`は`SelectedCell`から都度座標照合で算出するため、行挿入・削除後は選択カーソルが指す座標と実要素の対応がずれる。

再現: 行5の要素を選択（`SelectedCell=(5,2)`）した状態で行2の前に挿入すると、実要素は行6へ移動するが`SelectedCell`は(5,2)のまま。直後のDelete操作等で意図と異なる要素を誤操作しうる。`FinishRowCountChange`はGrid.Rows超過時のクランプのみでシフト追随はしていない。既存の`AddRowCommand`/`DeleteRowCommand`（末尾操作）は要素のRowが変わらないため本問題が構造的に発生せず、増分3で`RowOps`導入により初めて顕在化。

**b. テストが未実装挙動を確定仕様のごとくGREEN固定**
`tests/Ecad2.Core.Tests/RowOpsTests.cs:249`付近（`DeleteRow_GroupFrame_TargetBeforeFrameStart_NoChangeWhenNotAfter`）。
このテストは実際には`TopLeft.Row == targetRow`（枠開始行=削除対象行そのもの、殿確認待ちで未実装のケース）を検証しているが、テスト名は`TargetBeforeFrameStart`で意味が逆（「開始行が対象行より前」に読める）。テスト自体にも「これは未実装の暫定挙動」という注記がなく、`Assert.Equal`で無変化を確定仕様のように固定している。将来「枠ごと削除」を実装する際、このテストが検証済み仕様と誤読され実装漏れ・修正見送りを誘発するリスクが具体的にある。

### 2-2. 経過観察（CONFIRMED、cleanup）

**c. `DeleteRowAtCommand`の占有拒否ブロックがrule of three到達**
`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1699`付近。
`IsRowOccupied`判定→`StatusMessage`設定→`return`という同一パターンが`DeleteRowCommand`(1639)・`UpdateSheetSettingsCommand`(1665)・`DeleteRowAtCommand`(1699、今回)の3箇所に到達した。過去裁定（`docs/ecad2-t055-increment2-round1-review-onmitsu.md:63`）は「2箇所間の不統一」指摘をrule of three未達で見送っており、恒久却下ではなく「3箇所到達で再検討」という条件付きだった。今回その条件を満たした。共通private helper（例: `TryRejectOccupiedRow(Sheet, int)`）への抽出を検討する好機。

### 2-3. 経過観察（PLAUSIBLE、現状無害・将来の脆さ）

**d. CanExecuteの行番号範囲チェック不足**
`InsertRowBeforeCommand`/`DeleteRowAtCommand`のCanExecuteは`param is int`とGrid.Rows上限/下限のみ判定し、`0<=row<Grid.Rows`は検証しない。現状唯一の呼び出し元（`LadderCanvasHost_PreviewMouseRightButtonDown`）が事前ガード済みのため実害なし。ただし安全性が「呼び出し元が1箇所だけ」という運用上の偶然に依存しており、将来別の呼び出し元（新規XAMLバインド等）が追加されると即座に顕在化する構造的な脆さ。

**e. GroupFrameの`Visual*Mm`座標オーバーライドがシフト未追随**
`RowOps.InsertRow`/`DeleteRow`はGroupFrameの`TopLeft.Row`/`Height`をシフトするが、同じGroupFrameが持つmm絶対座標オーバーライド（`VisualXMm`/`VisualYMm`/`VisualWidthMm`/`VisualHeightMm`、T-007のGuiEcad移植由来フィールド）は更新しない。`DiagramRenderer`は`VisualYMm ?? (TopLeft.Rowから計算)`という優先順位で描画するため、これが非nullの場合は論理座標と描画位置がずれる。ただし枠ドラッグ機能・GuiEcadインポート機能とも現状未実装のため、通常操作でこの値が非nullになる到達経路が存在しない。将来ドラッグ移動機能を実装する際に顕在化する潜在バグとして記録。

### 2-4. 指摘不要（REFUTED、5件）

- 表示行番号`pos.Row+1`の2箇所独立計算 — プロジェクト全体で「+1はインライン計算」が確立された既存流儀、この2箇所だけ問題視する根拠薄い
- 右クリックごとのContextMenu/MenuItem都度生成 — 数百バイト規模、右クリックはホットパスでない、体感差なし
- `IsRowOccupied`+`RowOps.DeleteRow`の2重走査（O(2n)） — Grid.Rows上限60・実運用要素数も小規模、単発呼び出しで実害なし
- `RowOps.DeleteRow`の契約がassert未強制 — プロジェクト全体で`Debug.Assert`使用例ゼロ、コメントのみでの契約表明が既存流儀と整合。Undo(T-051)は前提崩れで凍結中、具体的な将来懸念材料なし
- `RowOps.cs`の13箇所しきい値比較を共通関数へ抽出 — 現状1行/箇ses、関数抽出しても行数削減効果なし（誤った見積りに基づく提案だった）。GuiEcad由来の型別直書き方針（調査書§2.4）とも整合

---

## 3. 侍の自己点検との照合

侍報告のbuild/test全合格（Core45件・App336件、新規58件）は妥当。ただし新規テスト58件のうち`RowOpsTests.cs`の1件（§2-1-b）はテスト設計自体に注記漏れの懸念があり、テスト数の合格と設計の正しさは別軸である点を申し添える。

---

## 4. 総括

要修正2件（a: SelectedCell追随、b: テスト注記漏れ）は増分3のDoD（型ごとのテスト実測）そのものには影響しないが、aはユーザー体感バグ（誤削除リスク）に直結するため優先度高。侍への差し戻しを推奨。c/d/eは経過観察で足りる。

---

## 5. 往復1周目修正の再レビュー（コミット `e9d062a`）

家老采配（再レビュー、2026-07-10）の3観点を確認。

### 5-1. 指摘aの修正（SelectedCell追随）

`InsertRowBeforeCommand`: `SelectedCell.Row >= row` で `+1`。`RowOps.InsertRow`の閾値規則（`e.Pos.Row >= targetRow`）と一致。
`DeleteRowAtCommand`: `SelectedCell.Row > row` で `-1`。`RowOps.DeleteRow`の閾値規則（`e.Pos.Row > targetRow`）と一致。**規則は完全一致、判定OK。**

RED先行証明の再現テストは実際には8件（Insert側4件・Delete側4件、コミットメッセージの「3件」は要約上の簡略化と見受けられる）。境界値（挿入点そのもの／削除点そのもの、対象前、対象後、`SelectedCell`が`null`のケース）を過不足なく突いている。`DeleteRowAtCommand_Execute_DoesNotShiftSelectedCellAtTargetRow`（削除対象行そのものを選択中のケース、`>`なので不変が正）も含め、同値分割・境界値分析の観点で網羅的。**aの経路を正しく突いている。**

`dotnet build`（`--no-incremental`）→`dotnet test`（`--no-build`）で実測: **Core 45件・App 344件、全合格**（家老報告の数値と一致）。

### 5-2. 指摘bの修正（テスト名・注記）

テスト名を`DeleteRow_GroupFrame_TargetBeforeFrameStart_NoChangeWhenNotAfter`（誤読しうる旧名）から
`DeleteRow_GroupFrame_TargetEqualsFrameStartRow_CurrentlyNoChange_PendingDecision`へ変更。XMLコメントで
「本テストは現状の暫定挙動を記録するのみで確定仕様ではない」「将来枠ごと削除を実装する際は本テストを更新すること」を明記。**未実装挙動である旨が明確になった。適切。**

### 5-3. 指摘cの修正（TryRejectOccupiedRow共通化）

`TryRejectOccupiedRow(Sheet sheet, int row, string message)`を新設し、`DeleteRowCommand`（"最終行に..."固定文言）・
`UpdateSheetSettingsCommand`（`$"行{row+1}に..."`、ループ内呼び出し）・`DeleteRowAtCommand`（`$"行{row+1}に..."`）の
3箇所を置き換え。**文言は呼び出し元ごとに維持されたまま（差分で確認済み）、判定→設定→returnの構造も変化なし。
振る舞いに変化なし、正しく共通化されている。**

### 5-4. 結論

**3件ともクリーン。往復2周目の指摘なし。** d（CanExecute境界チェック、P-049）・e（GroupFrame Visual*Mm、P-050）は
pending合意のとおり本レビューでも対応不要として扱った。
