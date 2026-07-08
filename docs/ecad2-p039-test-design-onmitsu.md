# P-039 テスト設計（隠密起草、新制度初適用）

> 2026-07-08 隠密起草。仕様「縦コネクタ(VerticalConnector)をドラッグで列方向(Column)にも
> 移動できる。キーボードの`MoveSelectedConnectorColumn`と同じ結果に到達する（案X）」に対する
> テスト設計。`docs-notes/roles/onmitsu.md`「テスト設計の起草」に基づき、実装コードの書き方
> ではなく仕様側から検証観点・境界値・状態遷移・対称性を先に定める。侍はこの設計をコードに
> 落とす（設計にないテストの追加は自由、設計にあるものを省くのは不可）。

---

## 0. 現状把握（設計の前提）

`MainWindowViewModel.cs`を実読した時点（コミット767325b以降の作業ツリー）で、以下を確認した：

- `BeginDragConnector`/`UpdateDragConnector`は既に`startColumn`/`currentColumn`引数を受け取る
  シグネチャに変更済み（316行目・334行目）。
- `_dragConnectorOrigColumn`/`_dragConnectorStartColumn`フィールドも追加済み（292-293行目）。
- しかし**`UpdateDragConnector`の本体移動分岐（358-366行目）はRow方向のみを更新しており、
  Column方向の実更新ロジックが未実装**。
- **`ConfirmDragConnector`（371-377行目）はTopRow/BottomRowの変化のみをチェックしており、
  Columnの変化を見ていない**（このままだとColumnだけ動かした場合にMarkDirty()されない）。
- **`CancelDragConnector`（380-388行目）はTopRow/BottomRowのみ復元しており、Columnを復元
  しない**（このままだとキャンセルしてもColumnが動いたままになる）。

これらは実装漏れであり、以下の設計はこれを明示的に検出する内容を含む。

**参照した対称モデル**：
- `WireBreak`の2軸ドラッグ（`UpdateDragWireBreak`520-539行目、`ConfirmDragWireBreak`
  542-547行目、`CancelDragWireBreak`551-558行目）：Row+Boundaryの2値を独立クランプし、
  Confirm/Cancelとも両方を見る設計。
- `MoveSelectedConnectorColumn`（既存キーボード版、413-421行目）：
  `Math.Clamp(c.Column + delta, 0, sheet.Grid.Columns)`という単純クランプ。ドラッグ版も
  最終的にこれと同じ値域・同じクランプ結果に到達すべき（仕様の核心要件）。

---

## 1. 同値分割・境界値分析（Column軸）

`VerticalConnector.Column`は`double`、有効域`0 <= Column <= sheet.Grid.Columns`
（0.5刻みでセル中央にも置ける）。ドラッグは「開始位置からの相対差分」方式のため、
**「元のColumn位置」×「移動量(delta)」の組み合わせ**で境界を分析する。

| # | 元Column | delta | 期待されるColumn（Clamp後） | 分類 |
|---|---|---|---|---|
| B1 | 中間値(例: 10.0) | 小さい負/正 | 元Column+delta（クランプ不要） | 同値分割・正常域 |
| B2 | 0（下限） | 負（さらに左へ） | 0（クランプで下限維持） | 境界値・下限 |
| B3 | 0（下限） | 正（右へ離れる） | 0+delta（クランプ不要、正常に動く） | 境界値・下限+1 |
| B4 | Grid.Columns（上限） | 正（さらに右へ） | Grid.Columns（クランプで上限維持） | 境界値・上限 |
| B5 | Grid.Columns（上限） | 負（左へ離れる） | Grid.Columns+delta（クランプ不要） | 境界値・上限-1 |
| B6 | 中間値 | ちょうど0に到達するdelta | 0（ぴったり境界） | 境界値・下限ぴったり |
| B7 | 中間値 | ちょうどGrid.Columnsに到達するdelta | Grid.Columns（ぴったり境界） | 境界値・上限ぴったり |
| B8 | 中間値 | 境界を大きく超える大きさのdelta | 0またはGrid.Columns（クランプ） | 無効域 |

`[Theory]`+`[InlineData]`でB1〜B8を1つのテストメソッドに集約することを推奨する（入力=
`origColumn, delta, gridColumns`、期待値=`expectedColumn`の組）。

---

## 2. 状態遷移設計

### 2.1 遷移表（本体移動、isEndpoint=false）

| 状態 | 操作 | 事前条件 | 事後条件 |
|---|---|---|---|
| 未ドラッグ | `BeginDragConnector(c, isEndpoint:false, isTop:false, startRow, startColumn)` | - | `_dragConnectorOrigColumn = c.Column`（スナップショット）、`IsDraggingConnector=true` |
| ドラッグ中 | `UpdateDragConnector(currentRow, currentColumn)` | Begin済み | `c.Column = Clamp(_dragConnectorOrigColumn + (currentColumn - _dragConnectorStartColumn), 0, Grid.Columns)`。**Row方向の更新（既存のspan保持クランプ）とは独立に動作する**（片方が境界でロックされても他方は正常に動く、WireBreakのRow/Boundary独立クランプと同型） |
| 確定 | `ConfirmDragConnector()` | Update済み、Column変化あり | `MarkDirty()`が呼ばれる、`IsDraggingConnector=false` |
| 確定 | `ConfirmDragConnector()` | Update済み、Column変化なし（Row系も変化なし） | `MarkDirty()`は呼ばれない |
| 取消 | `CancelDragConnector()` | Update済み | `c.Column`が`_dragConnectorOrigColumn`へ復元される、`IsDraggingConnector=false`、`MarkDirty()`は呼ばれない |
| 強制取消 | `ForceCancelDragConnectorIfAny()`（Delete/シート切替/ReplaceDocument経由） | Update済み | `CancelDragConnector()`と同じ復元、`MarkDirty()`は呼ばれない |

### 2.2 遷移表（端点リサイズ、isEndpoint=true）

| 状態 | 操作 | 事後条件 |
|---|---|---|
| Begin | `BeginDragConnector(c, isEndpoint:true, isTop:true/false, startRow, startColumn)` | スナップショット取得は本体移動と同じ |
| Update | `UpdateDragConnector(currentRow, currentColumn)` | **殿裁定「端点は上下伸縮のみ」により、`currentColumn`の値に関わらず`c.Column`は不変**（TopRow/BottomRowのみ変化） |
| Confirm | `ConfirmDragConnector()` | Row系のみ変化チェック対象（Columnは常に不変のため実質関与しない） |

**あり得ない遷移（明示的に拒否を確認すべき）**：端点リサイズモード中に列方向の移動量
（`currentColumn`）を大きく変えても、Columnが変化しないこと。

---

## 3. ペア構成の対称性チェック表（4種同型実装、T-041増分7のカバレッジ不整合の再発防止）

| 観点 | WireBreak(Row+Boundary) | FreeLine(4値mm) | ConnectionDot(X+Y mm) | **VerticalConnector(Row系+Column、今回対象)** |
|---|---|---|---|---|
| Begin時、全軸スナップショット取得 | 済み | 済み | 済み | Row系は既存、**Column追加分の実装確認必須** |
| Update時、各軸が独立にクランプされる | 済み(Row/Boundary独立) | 済み(向き保持+境界) | 済み | Row系は既存(span保持)、**Column独立クランプの実装が抜けている（現状未実装）** |
| Confirm時、全軸の変化を見てMarkDirty判定 | 済み(Row\|\|Boundary) | 済み | 済み | **Columnの変化チェックが抜けている（現状の実装はTopRow\|\|BottomRowのみ）** |
| Cancel時、全軸を復元 | 済み | 済み | 済み | **Column復元が抜けている（現状の実装はTopRow/BottomRowのみ）** |
| ForceCancel(所見Y型)が全軸を正しく復元 | 済み(CancelDragWireBreak経由) | 済み | 済み | CancelDragConnector経由のため、**Cancel側が直れば自動的に連動するはず（要確認）** |
| 境界のmin>maxガード要否 | 不要(Row/Boundary独立、単純0〜上限) | 必要(所見AB、本体移動の間隔保持クランプのため) | 不要(単一点) | Row方向は既存(所見B、span保持のため必要)、**Column方向は単純0〜Grid.Columnsのため不要**(WireBreak.Boundaryと同型と判断) |
| キーボード版と同じ最終値に到達するか | （Row+Boundary版キーボードは`MoveSelectedWireBreak`で既存確認済み） | （既存確認済み） | （既存確認済み） | **これが案Xの核心要件。ドラッグ経由とキーボード経由(`MoveSelectedConnectorColumn`)で同じ移動量を適用した際、最終Column値が一致することを直接検証するテストが必要** |

太字箇所が、今回のテスト設計で重点的にカバーすべき「対称性の穴」である。

---

## 4. 具体的なテストケース一覧（設計、侍はこれをコード化する）

`ConnectorDragAndResizeTests.cs`への追加を想定（既存の`MakeConnector()`ヘルパーを流用可）。

### 4.1 UpdateDragConnector（本体移動）でColumn方向が正しく更新される

- **[Theory]** `UpdateDragConnector_Move_UpdatesColumnWithClamp`
  `InlineData`で上記2.1表のB1〜B8を網羅（origColumn, delta, gridColumns, expectedColumn）。
  `BeginDragConnector(c, isEndpoint:false, isTop:false, startRow:任意, startColumn:0)` →
  `UpdateDragConnector(currentRow:同じ, currentColumn: delta)` → `c.Column == expectedColumn`
  を検証。

- **[Fact]** `UpdateDragConnector_Move_RowAndColumnAreIndependent`
  Row方向が境界でクランプされる状況（例: TopRow=0で上方向へドラッグ）でも、Column方向は
  独立して正常に動くことを確認（WireBreakの独立クランプパターンとの対称性確認）。

### 4.2 端点リサイズ時はColumn不変

- **[Theory]** `UpdateDragConnector_Resize_NeverChangesColumn`
  `InlineData`で`isTop: true/false`の両方を確認。`BeginDragConnector(c, isEndpoint:true,
  isTop: true/false, ...)` → `UpdateDragConnector(currentRow, currentColumn: 大きく変化)` →
  `c.Column`が`_dragConnectorOrigColumn`（＝Begin時の値）のまま変化しないことを確認。

### 4.3 ConfirmDragConnectorがColumn変化も検知する

- **[Fact]** `ConfirmDragConnector_WhenOnlyColumnChanged_MarksDirty`
  Row系は不変、Columnのみ変化させて`ConfirmDragConnector()` → `IsDirty == true`を確認
  （現状の実装はここが漏れているため、この設計どおりに実装すればREDになるはずのテスト）。

- **[Fact]** `ConfirmDragConnector_WhenNothingChanged_DoesNotMarkDirty`
  Row系・Column系とも変化なしで`ConfirmDragConnector()` → `IsDirty == false`（回帰確認）。

### 4.4 CancelDragConnectorがColumnも復元する

- **[Fact]** `CancelDragConnector_RestoresColumnToOriginalPosition`
  ドラッグでColumnを変更した状態から`CancelDragConnector()` → `c.Column`が元の値へ復元、
  `IsDirty == false`を確認（現状の実装はここが漏れているため、この設計どおりに実装すれば
  REDになるはずのテスト）。

### 4.5 ForceCancelDragConnectorIfAny（所見Y型）がColumnも復元する

- **[Fact]** `SelectedConnectorAssignment_ForceCancelRestoresColumn`
  （既存の`所見A`系テストと同型）ドラッグでColumnを変更した状態から`SelectedConnector = null`
  等で強制キャンセル → Columnが元の値へ復元されIsDirtyが立たないことを確認。
  シート切替経由（`CurrentSheetIndex`変更）・Delete経由の両方で確認（4.1の対称性チェック表
  「ForceCancel」行に対応）。

### 4.6 キーボード版と同じ結果に到達する（案Xの核心要件）

- **[Theory]** `DragAndKeyboardColumnMove_ConvergeToSameResult`
  同じ初期Columnから、同じ移動量をa)ドラッグ経由(`BeginDragConnector`→`UpdateDragConnector`
  →`ConfirmDragConnector`)、b)キーボード経由(`MoveSelectedConnectorColumn`)でそれぞれ適用し、
  最終的な`c.Column`値が一致することを確認。`InlineData`で境界値（0近傍・Grid.Columns近傍・
  中間値）を含む複数ケースを網羅。

---

## 5. 侍への申し送り

- 4.3・4.4のテストは、現状の実装（`ConfirmDragConnector`/`CancelDragConnector`がColumn未対応）
  のままではRED（失敗）になることを想定している。これは意図的——設計を先にコード化し、
  RED確認後に実装を完成させる、という新制度のフロー通りに進めてよい。
- 4.1のUpdateDragConnector本体移動分岐は、WireBreakの`b.Boundary = Math.Clamp(_dragWireBreakOrigBoundary
  + deltaBoundary, 0, sheet.Grid.Columns);`と同型の単純クランプでよいと考える（Row方向のような
  span保持ロジックは不要、Columnは単一値のため）。
- 4.2（端点リサイズ時のColumn不変）は、`UpdateDragConnector`の`_draggingConnectorIsEndpoint`
  分岐（338-357行目）に一切Column更新コードを書かない、という「何もしない」対応で満たされる
  はずである。

---

## 出典・参照

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`BeginDragConnector`/`UpdateDragConnector`
  /`ConfirmDragConnector`/`CancelDragConnector`278-421行目、`WireBreak`版499-566行目、
  `MoveSelectedConnectorColumn`411-421行目）
- `src/Ecad2.Core/Model/Element.cs`（`VerticalConnector.Column`は`double`、0.5刻み対応の
  コメントあり）
- `docs-notes/roles/onmitsu.md`「テスト設計の起草」（本設計が従う新制度の規定）
- `tests/Ecad2.App.Tests/ConnectorDragAndResizeTests.cs`（既存テスト、追加先）
- `docs/ecad2-t041-increment7-review-onmitsu.md`〜`-4.md`（T-041増分7の一連のレビュー、
  カバレッジ不整合の実例）
