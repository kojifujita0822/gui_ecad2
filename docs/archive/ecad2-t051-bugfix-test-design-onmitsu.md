# T-051 バグ修正#1〜#4 テスト設計（隠密起草）

家老采配（2026-07-11、`docs/archive/ecad2-t051-review-onmitsu.md`の要修正4件対応）。バグ修正・往復案件の
ためテスト設計と実装を分離する制度【MUST】（`onmitsu.md`「テスト設計の起草」節）に従い、仕様側から
設計する。**侍はこの設計をコードへ落とす。設計に無いテスト追加は自由、設計にあるものを省くのは不可。**

---

## 0. 前提（実装方針、家老指定）

- **#1**: `UndoManager`へ`Clear()`（または同等のリセット手段）を新設し、`ReplaceDocument`
  （新規作成/開く共通経路）の入口で呼ぶことを前提とする。
- **#2**: `ApplyUndoRedoSnapshot`が、既存の公開メソッド`SheetNavigationViewModel.
  RefreshSelectedSheet(Sheet? oldValue)`（`SheetNavigationViewModel.cs:63`、T-050で確立済み・
  `CurrentSheetIndex`が外部から変更された際に使う既存フック）を呼ぶことを前提とする。ただし実装方式
  そのものは縛らず、「`SelectedSheet`のPropertyChangedがちょうど1回・正しい旧値/新値で発火する」
  という期待値のみをテストの検証対象とする。
- **#3**: `MainWindow.xaml.cs`のCtrl+Z/Yケースへ、既存Ctrl+S/O/Nと同型の`CommitDeviceNameEdit()`
  呼び出しを追加することを前提とする。ただし**この観点は下記2.3節の理由によりViewModelレベルの
  単体テストが技術的に構成できない**（詳細は該当節）。
- **#4**: `ApplyUndoRedoSnapshot`が`OutputPanel.ClearResults()`（既存、`OutputPanelViewModel.cs`、
  T-019由来）を呼ぶことを前提とする。

`ApplyUndoRedoSnapshot`は`SheetNavigation.ResetSheets()`と別に、上記3つの追加呼び出し
（UndoManager.Clearは`ReplaceDocument`側、RefreshSelectedSheetと`OutputPanel.ClearResults()`は
`ApplyUndoRedoSnapshot`側）を担うことになる。

---

## 1. #1: ReplaceDocument後にCanUndo=false・Undo実行不能

### 1.1 同値分割・境界値

| # | 状態 | 期待結果 |
|---|---|---|
| U-B1 | Undo履歴のみある状態でClear | `CanUndo=false` |
| U-B2 | Redo履歴のみある状態（Undo実行後）でClear | `CanRedo=false` |
| U-B3 | Undo/Redo両方ある状態でClear | 両方false |
| U-B4 | 既に空の状態でClear（冪等性、境界値） | 例外を投げず何も起きない |

### 1.2 テストケース（`UndoManager`単体、`tests/Ecad2.App.Tests/UndoManagerTests.cs`へ追加）

```
Clear_WithUndoHistory_MakesCanUndoFalse            (U-B1)
Clear_WithRedoHistory_MakesCanRedoFalse             (U-B2、Undo実行後の状態から)
Clear_WithBothHistories_MakesBothFalse              (U-B3)
Clear_WhenAlreadyEmpty_DoesNotThrow                 (U-B4)
```

### 1.3 テストケース（ViewModel結合、`tests/Ecad2.App.Tests/UndoRedoCommandsTests.cs`へ追加）

**T-051bugfix-1【RED証明の中核】`NewDocument_ClearsUndoHistory_UndoCommandBecomesDisabled`**
- Given: `vm.NewDocument()`→`AddCommand.Execute(...)`（Undo履歴を作る、`UndoCommand.CanExecute(null)
  == true`）
- When: `vm.NewDocument()`を再度呼ぶ（`ReplaceDocument`経由）
- Then: `vm.UndoCommand.CanExecute(null) == false`
- **RED証明**: 修正前コードは`ReplaceDocument`が`UndoManager`に触れないため、履歴が残存し
  `CanExecute`は`true`のまま→本テストは修正前コードで確実に失敗する。

**T-051bugfix-2 `NewDocument_ClearsRedoHistory_RedoCommandBecomesDisabled`**
- Given: `AddCommand`実行→`UndoCommand`実行（`RedoCommand.CanExecute(null) == true`）
- When: `vm.NewDocument()`
- Then: `vm.RedoCommand.CanExecute(null) == false`

### 1.4 LoadFromFile（開く）経路について

`ReplaceDocument`は`NewDocument`・`LoadFromFile`双方から呼ばれる単一の共通経路（既存実装で確認済み、
`MainWindowViewModel.cs`）。ファイルI/Oを伴う`LoadFromFile`専用のテストは、`NewDocument`のテスト
（1.3節）が同じ`ReplaceDocument`を通る以上、実質的に重複したカバレッジになるため追加不要と判断する
（既存の`ReplaceDocument`呼び出し元が1箇所に集約されているという設計自体の妥当性は、コードレビューの
静的確認で担保する）。

---

## 2. #2: Undo/Redo後にSelectedSheet通知が発火し選択が維持される

既存`tests/Ecad2.App.Tests/SelectedSheetNotificationTests.cs`（T-050由来、`PropertyChangedForTest`
フックで旧値ごと観測する確立済みパターン）に「ケース6: Undo/Redo経由」として追加することを推奨する
（新規ファイルを起こすより、既存の「Sheets構成が変わる操作は正しく通知する」という検証テーマに
統合する方が一貫性が高い）。

### 2.1 SelectedSheetの意味論（重要な前提）

`ApplyUndoRedoSnapshot`の`CurrentSheetIndex`復元は「クランプのみ」（計画書1.3節設計案）であり、
**Undo/Redoは操作直前に選択していたシートへ選択を戻すものではない**。あくまで「削除操作時にクランプで
選択されていたインデックス」を維持したままシート構成が変わる。この意味論を踏まえ期待値を設計する。

### 2.2 同値分割・境界値

| # | 操作列 | 選択位置の期待値 |
|---|---|---|
| S-B1 | 1シート→追加(2枚)→Undo(1枚に戻る) | index=0のまま、唯一のシートを指す |
| S-B2 | 2シート、index=1選択中→追加(3枚)→Undo | index=1のまま、内容(Name等)が追加前のシート2と一致 |
| S-B3 | 3シート[A,B,C]、C(index=2)選択中→削除（クランプでB=index1へ）→Undo(Cが復元、3枚に戻る) | index=1のまま（Cへは戻らない、クランプ後の位置を維持） |
| S-B4 | S-B2と同じ操作列→Undo→Redo（往復、対称性点検） | Redo後もindex=1のまま、内容がシート3(追加した方)と一致 |

### 2.3 テストケース

```csharp
// ケース6: Undo/Redo経由(T-051バグ修正#2)
[Fact]
public void UndoCommand_Execute_AfterAddCommand_RaisesSelectedSheetChanged_ExactlyOnce()
// S-B2のGiven/When/Then。SubscribeSelectedSheetOldValuesパターンを流用し、
// PropertyChangedForTestで発火回数=1・旧値=Undo実行直前のSelectedSheetであることを検証。
// 【RED証明の中核】修正前コードはRefreshSelectedSheet相当の呼び出しが無いため発火数=0でFAIL。

[Fact]
public void UndoCommand_Execute_AfterAddCommand_SelectedSheetContentPreserved()
// S-B2のThen追加分。vm.SheetNavigation.SelectedSheet?.Name が追加前のシート2名と一致することを検証
// (通知だけでなく、実際に正しいシートを指すことの確認)。

[Fact]
public void UndoCommand_Execute_AfterDeleteWithClamp_SelectedSheetStaysAtClampedIndex()
// S-B3。削除時にクランプされた位置(B)がUndo後も維持され、削除対象だったCへは戻らないことを明示。

[Fact]
public void RedoCommand_Execute_AfterUndo_RaisesSelectedSheetChanged_ExactlyOnce()
// S-B4。対称性点検(Redo方向)。

[Fact]
public void UndoCommand_Execute_OnSingleSheetHistory_SelectsRemainingSheet()
// S-B1。境界値(1↔2枚の往復)。
```

---

## 3. #3: DeviceNameBox編集中のCtrl+Z/YでCommitDeviceNameEdit()が先行

### 3.1 既存Ctrl+S/O/Nテストパターンの調査結果

`tests/Ecad2.App.Tests/`配下を全数確認したが、**`MainWindow`（Viewのコードビハインド）を直接
インスタンス化・操作するテストは1件も存在しない**（既存テストは全てViewModel層・Converter層のみ）。
`CommitDeviceNameEdit()`は`DeviceNameBox.GetBindingExpression(...)`というWPF UI要素への直接操作を
含むため、`MainWindowViewModel`側の単体テストでは検証対象に含められない。既存のCtrl+S/O/N
（`SaveDocument()`・`ConfirmDiscardIfDirty()`内で呼ばれる、`MainWindow.xaml.cs:208,280`）についても
**専用の自動テストは存在せず**、忍者の実機確認（UI Automation経由）でカバーされてきた領域と判断する。

### 3.2 設計判断

**ViewModelレベルの単体テストは構成しない**（構成不可能なため、設計に含めないのが正直な報告）。
既存Ctrl+S/O/Nと同型の配線（`Window_PreviewKeyDown`のcaseへ`CommitDeviceNameEdit();`を1行追加する
だけの変更）であることは、**コードレビューでの静的な行単位確認**で担保する（隠密が侍実装後に確認）。
実際の動作確認（DeviceNameBox編集中→Ctrl+Z押下→編集内容が正しく確定されること）は、
**忍者実機確認（`ecad2-ui-automation`スキル）に委ねる**ことを推奨する。家老采配時に忍者への確認観点
として以下を明記されたい：
- DeviceNameBoxに文字入力（未確定）→Ctrl+Zを押す→編集内容が意図通り確定される（消失しない）こと
- 確定後、Undo自体も正しく実行される（デバイス名編集とUndo操作が競合しない）こと

---

## 4. #4: Undo/Redo後にOutputPanel.ClearResults()相当が走り診断残留が消える

配置先は`tests/Ecad2.App.Tests/UndoRedoCommandsTests.cs`（既存ファイル）を推奨。

### 4.1 同値分割・境界値

| # | 状態 | 期待結果 |
|---|---|---|
| O-B1 | DRC実行後（Diagnostics≥1件）にUndo | Diagnostics.Count == 0 |
| O-B2 | O-B1と同様の状態でRedo（対称性点検） | Diagnostics.Count == 0 |
| O-B3 | DRC未実行（Diagnostics=0件、既に空）の状態でUndo | 例外なく0件のまま（冪等性） |
| O-B4 | 診断を1件選択中（SelectedDiagnostic≠null）でUndo | SelectedDiagnosticもnullになる（ClearResults()の副作用確認） |

### 4.2 テストケース

```csharp
[Fact]
public void UndoCommand_Execute_ClearsOutputPanelDiagnostics()
// O-B1【RED証明の中核】。Given: シート追加→接点のみ配置(コイル無し、DRC-XREF系を誘発)→
// OutputPanel.RunDrcCommand実行(Diagnostics≥1件)。When: vm.UndoCommand.Execute(null)。
// Then: vm.OutputPanel.Diagnostics.Count == 0。
// RED証明: 修正前コードはApplyUndoRedoSnapshotがClearResults()を呼ばないため診断が残留しFAIL。

[Fact]
public void RedoCommand_Execute_ClearsOutputPanelDiagnostics()
// O-B2。対称性点検(Redo方向)。

[Fact]
public void UndoCommand_Execute_WhenNoDiagnostics_DoesNotThrow()
// O-B3。境界値(空状態での冪等性)。

[Fact]
public void UndoCommand_Execute_ClearsSelectedDiagnostic()
// O-B4。ClearResults()はDiagnostics.Clear()に加えSelectedDiagnostic=nullも行う
// (OutputPanelViewModel.cs既存実装)、その副作用も確認する。
```

---

## 5. 状態遷移分析について

#1〜#4いずれも「操作→状態リセット/通知」という1ステップの遷移であり、複数の中間状態を持つ状態機械
ではないため、本設計では状態遷移図・遷移表の技法は適用対象外と判断する。ただし#2の「クランプ後の
選択位置がUndo/Redoでどう扱われるか」（2.1節）は、Undo/Redo往復という時系列上の整合性を要するため、
S-B4（往復）で対称性を確認する形とした。

---

## 6. T-049/T-019既存テストとの対称性点検

- #3（T-049由来のCommitDeviceNameEdit()パターン）：既存Ctrl+S/O/Nにも専用テストが無い
  （3.1節で確認済み）ため、「既存のテスト有無に対称性を合わせる」という観点では、新規にテストを
  追加しないことがむしろ既存パターンとの整合になる。
- #4（T-019由来のOutputPanel.ClearResults()パターン）：`ReplaceDocument`側に対応する専用テストの
  有無を確認したが、`docs/todo.md`T-019節・既存コードのコメント（「隠密レビュー指摘」）を見る限り
  専用テストは無く、コードレビューでの確認のみだった模様。#4はUndo/Redoという新機能に対する回帰
  テストとして今回新規に追加する位置づけであり、既存パターンより手厚い検証を課すことになるが、
  過剰品証ではなくT-051という新規機能に対する適正なカバレッジと判断する。

---

## 出典
- `docs/archive/ecad2-t051-review-onmitsu.md`（指摘#1〜#4の詳細、verify結果）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`RefreshSelectedSheet`:63、
  `ResetSheets`:70-74、`AddCommand`/`DeleteCommand`:90-166）
- `src/Ecad2.App/MainWindow.xaml.cs`（`CommitDeviceNameEdit`:154-158、`SaveDocument`:199-214）
- `tests/Ecad2.App.Tests/SelectedSheetNotificationTests.cs`（既存パターンの出典）
- `onmitsu.md`「テスト設計の起草」節
