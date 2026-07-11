# T-051 SelectedCell無条件クリアバグ テスト設計（隠密起草）

家老采配（2026-07-11、隠密再レビュー`docs/archive/ecad2-t051-round2-review-onmitsu.md`§2-1のCONFIRMED
対応）。バグ修正・往復案件のためテスト設計と実装を分離する制度【MUST】（`onmitsu.md`「テスト設計の
起草」節）に従い、仕様側から設計する。**侍はこの設計をコードへ落とす。設計に無いテスト追加は自由、
設計にあるものを省くのは不可。**

DoD（家老指定）: Undo/Redo実行後もSelectedCellが実行前の値を維持すること。シート数減少で
CurrentSheetIndexのクランプが必要な場合の期待値も、既存DeleteRowAtCommandのクランプ意味論と
整合させて設計する。RED証明可能な構成（修正前=null化でFAIL確実）を明記する。

---

## 0. 前提（実装方針、隠密提案）

### 0.1 バグの構造

`ApplyUndoRedoSnapshot`（`MainWindowViewModel.cs` 1802-1829行）は`SetCurrentSheetIndexCore`
（152-157行）を経由するが、このメソッドは`SelectedCell = null;`を**常時無条件**で実行する設計
（T-041由来、AddCommand/DeleteCommand/RenameCommand/DRC出力パネルのJumpTo等、複数の呼び出し元が
「シート切替時は選択状態を必ずクリアする」という不変条件に依存している）。

`ApplyUndoRedoSnapshot`のXMLコメント（1798-1801行）・設計書
（`docs/archive/ecad2-t051-implementation-plan-samurai.md` 93/147行）は「SelectedCell/Tool状態/
StatusMessageは巻き戻さず現状維持（殿裁定2026-07-11）」と明言しており、実装がこれに反している。

### 0.2 実装方針（提案、侍の裁量）

**`SetCurrentSheetIndexCore`自体は変更しない**（AddCommand/DeleteCommand等の他の呼び出し元が
依存する「シート切替時に選択状態を必ずクリアする」不変条件を壊すと、T-041が対策した「孤立オブジェクト
参照」バグ（別シート由来のSelectedConnector等を参照し続ける）が別経路で再発しかねないため）。

代わりに、`ApplyUndoRedoSnapshot`内で`SetCurrentSheetIndexCore`呼び出しの前後で`SelectedCell`を
退避・復元する：

```csharp
var oldSelectedCell = SelectedCell;          // SetCurrentSheetIndexCore呼び出し前に退避
...
SetCurrentSheetIndexCore(clampedIndex);       // 内部でSelectedCell=nullが発生する(既存仕様のまま)
...
SelectedCell = oldSelectedCell;               // 退避値を書き戻す(setter経由、通知は正しく発火する)
```

（退避・復元のどちらもsetterを経由させるか、内部フィールドへの直接代入にするかは侍の裁量。ただし
`SelectedCell`のsetterには`SelectedConnector`等のクリア・記入中ドラフト破棄という副作用があるため、
「一旦nullにされた後、復元時にもう一度setterを通す」実装だと、この副作用が2回走ることになる。
これによる実害は無い想定だが、詳細は§4参照）。

**座標値は変えない（シフトしない）**。CurrentSheetIndexがクランプされて実際に別のシートへ切り替わって
も、`SelectedCell`のGridPos値自体はそのまま据え置く。これはT-055増分3で確立した
`DeleteRowAtCommand`の意味論——「削除対象行そのものを選択中でも、SelectedCellの座標値自体は変えない
（シフト対象になるのは"後ろの行を選択中"のケースのみで、シート単位の増減にはそもそも"シフト"という
概念が無い）」——と整合する、最も単純で一貫した設計である。

---

## 1. 同値分割・境界値分析

| # | 分類 | CurrentSheetIndexの変化 | SelectedCellの期待値 |
|---|---|---|---|
| C0 | `SelectedCell == null`（未選択） | 問わず | `null`のまま（既存動作、退行なし確認） |
| **C1** | `SelectedCell != null`、Undo/Redoでシート数変化なし、クランプ非発動 | 変化なし | **実行前の値のまま維持（現状バグ）** |
| **C2** | `SelectedCell != null`、Undo/Redoでシート数減少、クランプが発動し実際に別シートへ切替 | 変化あり（より小さいindexへ） | **実行前の値のまま維持、座標値は変えない（現状バグ、DeleteRowAtCommandのクランプ意味論と整合）** |
| C3 | C1の状態からさらにRedo実行（対称性点検） | 元に戻る | 実行前の値のまま維持 |

C2の境界値をテストで確実に再現するには、「`_currentSheetIndex`が、Undo/Redo実行によって復元される
シート数の範囲を超えている」状態を作る必要がある。これは「シートを追加した直後（選択が自動で新シートへ
移動した状態）で、その追加をUndoする」操作列で確実に再現できる（2.3節参照）。

---

## 2. テストケース設計

配置場所は`tests/Ecad2.App.Tests/UndoRedoCommandsTests.cs`（T-051の既存Undo/Redo結合テスト群と
同ファイル）を推奨。`GridPos`は`Ecad2.Model`名前空間（`readonly record struct GridPos(int Row,
int Column)`）、既存テスト（`ConnectorDraftTests.cs`等）で`vm.SelectedCell = new GridPos(row,
col);`という代入パターンが確立している。

### 2.1 C1: シート数不変ケース（クランプ非発動）

**T-selcell-1【RED証明の中核】Undo実行後もSelectedCellが実行前の値を維持すること**

- Given:
  ```csharp
  var vm = CreateViewModel();
  vm.NewDocument();                                          // シート1枚(index=0)
  vm.SheetNavigation.AddCommand.Execute(("シート2", false));  // 2枚に増加、選択は自動でシート2へ移動(index=1)
  vm.CurrentSheetIndex = 0;                                  // シート1へ選択を戻す(この代入でSelectedCellは一旦nullになる、既存仕様)
  vm.SelectedCell = new GridPos(3, 2);                       // シート1のグリッド上で選択し直す
  ```
- When: `vm.UndoCommand.Execute(null);`（シート2追加を取り消す、1枚に戻る）。Undo実行直前の
  `_currentSheetIndex`は0のまま。Undo後のシート数は1、`Math.Clamp(0, 0, 0) == 0`でクランプは
  実質発生しない（0のまま）。
- Then: `Assert.Equal(new GridPos(3, 2), vm.SelectedCell);`
- **RED証明**: 修正前コードは`ApplyUndoRedoSnapshot`→`SetCurrentSheetIndexCore`→
  `SelectedCell = null`が無条件実行されるため、`vm.SelectedCell`は`null`になりFAILする。

**T-selcell-2 対称性点検（Redo方向）**

- Given: T-selcell-1のWhen実行後の状態から続ける（`SelectedCell == (3,2)`のまま、シート1枚）。
- When: `vm.RedoCommand.Execute(null);`（シート2追加をやり直す、2枚に戻る。`_currentSheetIndex`は
  0のまま、シート1を見続けている）
- Then: `Assert.Equal(new GridPos(3, 2), vm.SelectedCell);`

### 2.2 C2: シート数減少・クランプ発動ケース（境界値、DeleteRowAtCommand意味論との整合）

**T-selcell-3【RED証明の中核・境界値】CurrentSheetIndexがクランプされる場合でも、SelectedCellの座標値は変えずに維持すること**

- Given:
  ```csharp
  var vm = CreateViewModel();
  vm.NewDocument();                                          // シート1枚(index=0)
  vm.SheetNavigation.AddCommand.Execute(("シート2", false));  // 2枚、選択は自動でシート2へ(index=1)
  vm.SheetNavigation.AddCommand.Execute(("シート3", false));  // 3枚、選択は自動でシート3へ(index=2)
  vm.SelectedCell = new GridPos(7, 4);                       // シート3のグリッド上で選択(index=2のまま)
  ```
- When: `vm.UndoCommand.Execute(null);`（シート3追加を取り消す、2枚[シート1,シート2]に戻る）。
  Undo実行直前の`_currentSheetIndex`は2（シート3）。復元後のシート数は2、
  `Math.Clamp(2, 0, 1) == 1`にクランプされ、シート2側へ実際に切り替わる。
- Then:
  ```csharp
  Assert.Equal(1, vm.CurrentSheetIndex);              // クランプ済み(既存仕様、変更しない)
  Assert.Equal(new GridPos(7, 4), vm.SelectedCell);   // 座標値自体は変えない
  ```
- **RED証明**: 修正前コードは`SetCurrentSheetIndexCore`経由で`SelectedCell`が無条件`null`になり
  FAILする。
- **意味論の根拠**: `DeleteRowAtCommand`（T-055増分3）は「削除対象行そのものを選択中でも
  SelectedCellの座標値自体は変えない」（シフト対象は"削除対象行より後ろを選択中"のケースのみ）。
  シート単位の増減にはそもそも「行のシフト」に相当する概念が無いため、座標値をそのまま維持するのが
  最も単純にこの前例と整合する設計となる。

### 2.3 C0: 退行なし確認

**T-selcell-4 SelectedCellが未選択(null)の場合、Undo/Redo後もnullのまま（退行なし）**

- Given:
  ```csharp
  var vm = CreateViewModel();
  vm.NewDocument();
  vm.SheetNavigation.AddCommand.Execute(("シート2", false));
  // SelectedCellは選択しない(null のまま)
  ```
- When: `vm.UndoCommand.Execute(null);`
- Then: `Assert.Null(vm.SelectedCell);`

---

## 3. 状態遷移分析について

Undo/Redo単体の実行は「選択状態→Undo/Redo操作→選択状態」という1ステップの遷移であり、複数の中間
状態を持つ状態機械ではないため、本設計では状態遷移図・遷移表の技法は適用対象外と判断する。ただし
T-selcell-3の「CurrentSheetIndexがクランプされ、かつSelectedCellの座標値は変えない」という組み合わせ
は、C1（クランプ非発動）とは異なる経路を通るため、境界値分析（1節）で明示的に切り分けた。

---

## 4. 実装時の注意（侍向け、スコープ外の関連事項）

- **Tool状態・記入中ドラフト（`_connectorDraft`/`_freeLineDraft`）はDoD範囲外**。
  `ApplyUndoRedoSnapshot`のコメントは「SelectedCell/**Tool状態**/StatusMessageは巻き戻さず現状
  維持」と記しており、Tool状態も本来は維持対象に含まれる可能性があるが、家老指定のDoDは
  「SelectedCell」に限定されているため、本設計では対象外とする。ただし§0.2の実装方針
  （退避→`SetCurrentSheetIndexCore`→復元）を素朴に実装すると、`SelectedCell`のsetter
  （`ClearConnectorDraftIfAny`/`ClearFreeLineDraftIfAny`を呼ぶ、201行以降）が「一旦nullにされる
  →復元で再度setterを通る」という経路で2回発火し、記入中ドラフトが（本来維持されるべきかもしれない
  状況で）意図せず破棄される可能性がある。この点は今回の設計対象外だが、実装時に気づいた場合は
  家老へ別途報告されたい（新規の指摘としてテスト設計は別途起草する）。
- **家老指定DoDの「RED証明可能な構成」は、T-selcell-1・T-selcell-3が該当する**（RED証明の中核2件）。
  実装前に`git stash`等で修正前コードへ戻し、この2件が実際にFAILすることを実測してから着手すること
  （既存の「RED先行証明」制度に従う）。
- 既存の`SelectedSheetNotificationTests.cs`「ケース6」（T-051バグ修正#2のテスト群）との配置場所の
  住み分け: それらはSelectedSheet（シートオブジェクト自体）の通知を検証する。本設計はSelectedCell
  （グリッド内座標）を検証するため、`UndoRedoCommandsTests.cs`側に置く方が既存の粒度と整合する。

---

## 出典
- `docs/archive/ecad2-t051-round2-review-onmitsu.md`（§2-1、CONFIRMEDの詳細・混入時期の特定）
- `docs/archive/ecad2-t051-implementation-plan-samurai.md`（93行目・147行目「開かれた論点1」）
- `docs/archive/ecad2-t055-increment3-selectedcell-bugfix-test-design-onmitsu.md`（DeleteRowAtCommandの
  クランプ意味論、テスト設計の参考パターン）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`SetCurrentSheetIndexCore`:152-157、
  `SelectedCell`setter:201以降、`CurrentSheetIndex`公開setter:114-138、
  `ApplyUndoRedoSnapshot`:1798-1829）
- `tests/Ecad2.App.Tests/ConnectorDraftTests.cs`（`SelectedCell = new GridPos(...)`代入パターンの
  出典）
- `onmitsu.md`「テスト設計の起草」節
