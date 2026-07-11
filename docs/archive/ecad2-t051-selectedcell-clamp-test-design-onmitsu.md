# T-051 SelectedCell復元の範囲クランプ欠如 テスト設計（隠密起草）

家老采配（2026-07-11、殿裁定「往復3周目で新規回帰を修正してからクローズ」、隠密再々レビュー
`docs/archive/ecad2-t051-round3-review-onmitsu.md`§2-1のPLAUSIBLE対応）。バグ修正・往復案件のためテスト
設計と実装を分離する制度【MUST】（`onmitsu.md`「テスト設計の起草」節）に従い、仕様側から設計する。
**侍はこの設計をコードへ落とす。設計に無いテスト追加は自由、設計にあるものを省くのは不可。**

DoD（家老指定）: シート追加Undo等でSelectedCell復元時、復元先シートのGrid.Rows/Columns範囲へ
クランプされること（AddRowCommand等が課すFinishRowCountChangeの意味論と整合）。RED証明可能な構成
（修正前=範囲外座標のままでFAIL確実）を明記。既存T-selcell系との対称性点検も。

---

## 0. 前提（実装方針、隠密提案）

### 0.1 既存クランプ意味論の確認（`FinishRowCountChange`）

`MainWindowViewModel.cs`の`FinishRowCountChange`（1364-1371行、`AddRowCommand`/`DeleteRowCommand`/
`UpdateSheetSettingsCommand`/`InsertRowBeforeCommand`/`DeleteRowAtCommand`の5箇所から呼ばれる
共通処理）の意味論：

```csharp
private void FinishRowCountChange(Sheet sheet)
{
    if (SelectedCell is GridPos selectedCell && selectedCell.Row >= sheet.Grid.Rows)
        SelectedCell = selectedCell with { Row = sheet.Grid.Rows - 1 };
    StatusMessage = "";
    MarkDirty();
    NotifyCurrentSheetChanged();
}
```

**Rowのみ**をクランプする（`selectedCell.Row >= sheet.Grid.Rows`なら`Grid.Rows - 1`へ切り詰め）。
`Column`には一切触れない。

### 0.2 Columnsは対象外（現状不変のため）

`Grid.Columns`はシート生成時（`SheetNavigationViewModel.cs:107`／`MainWindowViewModel.cs:1592`、
いずれも`new GridSpec { Rows = 10, Columns = 20 }`）に固定値20で設定され、**後から変更する
コマンドが存在しない**（`UpdateSheetSettingsCommand`はRows・Bus名のみを対象とし、Columnsには
触れない、grep確認済み）。よって、Undo/RedoでSelectedCell.Columnが範囲外になることは現状の
実装では起こりえない。家老指定DoDの「Grid.Rows/Columns」のうち、**実際にテスト・実装すべきは
Rowsのみ**と判断する（Columnsは将来Grid.Columnsが可変になった場合の拡張ポイントとして§4に
注記するに留める）。

### 0.3 実装方針（提案、侍の裁量）

`ApplyUndoRedoSnapshot`（`MainWindowViewModel.cs` 1802-1837行）内、`SelectedCell = oldSelectedCell;`
（1822行）の代入を、`FinishRowCountChange`と同じRowクランプ処理でラップする。ただし
`FinishRowCountChange`自体は`StatusMessage`クリア・`MarkDirty`・`NotifyCurrentSheetChanged`まで
含む複合処理であり、そのまま呼ぶのは責務が異なりすぎる（Undo/Redoは`StatusMessage`を巻き戻さず
現状維持、殿裁定2026-07-11）。クランプ判定部分のみを共通ヘルパーへ切り出す
（例: `private static GridPos ClampToSheetRows(GridPos cell, Sheet sheet) => cell.Row >=
sheet.Grid.Rows ? cell with { Row = sheet.Grid.Rows - 1 } : cell;`）か、同等のインライン処理を
`ApplyUndoRedoSnapshot`内に追加するか、実装方式は侍の裁量とする。

**クランプの基準となる`Sheet`は、`SetCurrentSheetIndexCore(clampedIndex)`実行後に確定する
「復元後のCurrentSheet」**（`CurrentSheetIndex`のクランプ自体は既存仕様のまま変更しない、
T-selcell-3で検証済み）。`SelectedCell`の代入は`SetCurrentSheetIndexCore`呼び出しの**後**に
行う必要がある（現状の実装順序どおり）。

---

## 1. 同値分割・境界値分析

Undo/Redo実行後、復元先CurrentSheetの`Grid.Rows`に対する`oldSelectedCell.Row`の関係：

| # | 分類 | `oldSelectedCell.Row` vs 復元後`Grid.Rows` | 期待値 |
|---|---|---|---|
| D0 | `SelectedCell == null` | - | `null`のまま（既存`T-selcell-4`でカバー済み、再掲不要） |
| D1 | `Row < Grid.Rows`（範囲内） | 範囲内 | クランプなし、値そのまま維持（既存`T-selcell-1`/`3`でカバー済み） |
| D4 | `Row == Grid.Rows - 1`（範囲内の最終行、境界値） | 範囲内境界 | クランプなし、値そのまま維持（新規、D1の境界値強化） |
| **D2** | `Row == Grid.Rows`（ちょうど1つ超過、境界値） | 範囲外 | `Grid.Rows - 1`へクランプ（新規・RED証明の中核） |
| **D3** | `Row > Grid.Rows`（大幅に超過） | 範囲外 | `Grid.Rows - 1`へクランプ（新規・RED証明の中核） |

D2/D3を再現するには、「シート追加→そのシートでGrid.Rowsを拡張（Undo管理対象外）→シート追加を
Undo（復元後のGrid.Rowsは拡張前の値に戻る）」という操作列が必要（`docs/archive/ecad2-t051-round3-review-onmitsu.md`
§2-1の再現手順と同型）。

---

## 2. テストケース設計

配置場所は`tests/Ecad2.App.Tests/UndoRedoCommandsTests.cs`（既存`T-selcell-*`系と同ファイル）を
推奨。`AddCommand`実行時の既定`Grid.Rows`=10（`GridSpec.MinRows`=1、`GridSpec.MaxRows`=60）。

### 2.1 D3・D2: RED証明の中核（境界値）

**T-selclamp-1【RED証明の中核・境界値D3】Undo実行でGrid.Rowsが縮小する場合、SelectedCellが新しい範囲内へクランプされること**

```csharp
var vm = CreateViewModel();
vm.NewDocument();                                            // シート1枚(Rows=10, index=0)
vm.SheetNavigation.AddCommand.Execute(("シート2", false));    // 2枚、選択は自動でシート2へ(index=1)
                                                               // ↑この時点でRecordSnapshot、シート1はRows=10のまま記録される
vm.CurrentSheetIndex = 0;                                     // シート1へ選択を戻す
for (int i = 0; i < 5; i++) vm.AddRowCommand.Execute(null);   // Rows=10→15(Undo管理対象外の操作)
vm.SelectedCell = new GridPos(14, 2);                         // Rows=15範囲内の最終行

vm.UndoCommand.Execute(null);                                 // シート2追加をUndo、復元後シート1はRows=10

Assert.Equal(new GridPos(9, 2), vm.SelectedCell);             // Grid.Rows-1=9へクランプ
```

- **RED証明**: 修正前コード（`f2aaaad`時点）はクランプ処理が無いため`SelectedCell`は`(14, 2)`の
  まま残り、期待値`(9, 2)`と一致せずFAILする。

**T-selclamp-2【RED証明・境界値D2】ちょうど1行超過のケースでもクランプされること**

```csharp
var vm = CreateViewModel();
vm.NewDocument();
vm.SheetNavigation.AddCommand.Execute(("シート2", false));
vm.CurrentSheetIndex = 0;
vm.AddRowCommand.Execute(null);                               // Rows=10→11
vm.SelectedCell = new GridPos(10, 2);                         // Rows=11範囲内の最終行(ちょうど境界)

vm.UndoCommand.Execute(null);                                 // 復元後Rows=10

Assert.Equal(new GridPos(9, 2), vm.SelectedCell);
```

- **RED証明**: 修正前コードは`SelectedCell`が`(10, 2)`のまま残りFAILする。D3（大幅超過）とは
  別に、境界値ちょうど（`Row == Grid.Rows`）でもクランプ条件（`>=`）が正しく効くことを確認する。

### 2.2 D4: 退行なし確認（境界値のすぐ内側）

**T-selclamp-3 範囲内の最終行ちょうどの場合はクランプされず値を維持すること**

```csharp
var vm = CreateViewModel();
vm.NewDocument();
vm.SheetNavigation.AddCommand.Execute(("シート2", false));
vm.CurrentSheetIndex = 0;
vm.SelectedCell = new GridPos(9, 2);                          // Rows拡張なし、Rows=10の最終行ちょうど

vm.UndoCommand.Execute(null);                                 // 復元後もRows=10、変化なし

Assert.Equal(new GridPos(9, 2), vm.SelectedCell);             // クランプされず値そのまま維持
```

- 既存`T-selcell-1`と同種のカバレッジだが、「クランプ境界のすぐ内側（`Row == Grid.Rows - 1`）」を
  明示的に確認する点に意義がある。修正前コードでもFAILしない（`T-selcell-1`と同じ経路）ため、
  RED証明の中核ではなく退行防止の確認として位置づける。

### 2.3 対称性点検（家老指定）

**T-selclamp-4 対称性点検（Redo方向）：クランプ後の値がRedo実行でも維持されること**

```csharp
// T-selclamp-1のWhen実行後の状態から続ける(SelectedCell==(9,2)、シート1枚、Rows=10)
vm.RedoCommand.Execute(null);                                 // シート2追加をやり直す、2枚に戻る
                                                                // 復元されるシート1はUndo実行直前の状態=Rows=15

Assert.Equal(new GridPos(9, 2), vm.SelectedCell);              // Rows=15範囲内なのでクランプ不要、(9,2)を維持
```

- **意味論の根拠**: Redo後の復元先シートはRows=15（拡張後）に戻るため、`(9, 2)`は範囲内であり
  クランプは発生しない。ここで`(14, 2)`（Undo前の元の値）に戻ることは期待しない——Undo/Redoは
  「操作直前の選択位置へ戻す」のではなく「クランプ位置を維持する」という既存の確立された意味論
  （`docs/archive/ecad2-t051-bugfix-test-design-onmitsu.md` §2.1、S-B4と同じ発想）に従う。

**T-selclamp-5【既存T-selcell系との複合ケース】CurrentSheetIndexのクランプとGrid.Rowsのクランプが同時に発生する場合**

```csharp
var vm = CreateViewModel();
vm.NewDocument();                                             // シート1(Rows=10, index=0)
vm.SheetNavigation.AddCommand.Execute(("シート2", false));     // 2枚、選択は自動でシート2へ(index=1)
                                                                // ↑RecordSnapshot、1枚構成[シート1(Rows=10)]を記録
vm.SheetNavigation.AddCommand.Execute(("シート3", false));     // 3枚、選択は自動でシート3へ(index=2)
                                                                // ↑RecordSnapshot、2枚構成[シート1,シート2(共にRows=10)]を記録
for (int i = 0; i < 5; i++) vm.AddRowCommand.Execute(null);    // シート3のRows=10→15(Undo管理対象外)
vm.SelectedCell = new GridPos(14, 4);                          // シート3上で選択(index=2のまま)

vm.UndoCommand.Execute(null);                                  // シート3追加をUndo、2枚[シート1,シート2]に戻る

Assert.Equal(1, vm.CurrentSheetIndex);                         // 既存T-selcell-3型: index=2→1へクランプ(シート2)
Assert.Equal(new GridPos(9, 4), vm.SelectedCell);              // 本設計: シート2のGrid.Rows=10基準で14→9へクランプ
```

- CurrentSheetIndexのクランプ（シート3→シート2への切替、既存`T-selcell-3`と同型）と、
  SelectedCellのRowクランプ（クランプ後に確定した新しいCurrentSheet=シート2のGrid.Rowsを基準に
  行う、本設計の新規分）が同時に発生する複合シナリオ。クランプの基準シートが「Undo実行前に選択
  していたシート（シート3）」ではなく「クランプ後の新しいCurrentSheet（シート2）」であることを
  明示的に検証する（§0.3の実装方針どおり、`SetCurrentSheetIndexCore`実行後に確定したシートを
  基準にする必要がある）。
- **RED証明**: 修正前コード（`f2aaaad`時点）は`CurrentSheetIndex`のクランプ自体は正しく行う
  （`T-selcell-3`は既にPASSしている）が、`SelectedCell`のRowクランプが無いため`(14, 4)`のまま
  残りFAILする。

---

## 3. 状態遷移分析について

本設計は既存`T-selcell-*`系（`docs/archive/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md`）と
同じくUndo/Redo単体の1ステップ遷移が対象のため、状態遷移図・遷移表の技法は適用対象外と判断する。
T-selclamp-5の複合ケースのみ、「CurrentSheetIndexクランプ確定後にRowクランプの基準が定まる」という
処理順序の依存関係があるため、実装時の順序（§0.3）を明記した。

---

## 4. 実装時の注意（侍向け）

- **Columnsは対象外**（§0.2）。テストケース化しない。将来`Grid.Columns`を変更するコマンドが
  追加された場合は、本設計と同じ発想（`Column >= Grid.Columns`ならクランプ）で別途拡張されたい。
- **家老指定DoDの「RED証明可能な構成」は、T-selclamp-1・T-selclamp-2・T-selclamp-5が該当する**
  （RED証明の中核3件）。実装前に`git stash`等で修正前コード（`f2aaaad`）へ戻し、この3件が実際に
  FAILすることを実測してから着手すること（既存の「RED先行証明」制度に従う）。
- T-selclamp-3は退行防止確認であり修正前でもPASSする（RED証明の対象外）。
- 既存`T-selcell-1`〜`4`（`docs/archive/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md`）が今回の
  修正で壊れないこと（回帰していないこと）も、テスト全体実行で併せて確認されたい（新規の
  テストケースとしては追加不要、既存スイートの再実行で足りる）。
- `docs/archive/ecad2-t051-round3-review-onmitsu.md`§2-2で言及した「記入中ドラフトが退避・復元の過程で
  無警告に破棄される」問題は、本設計のDoD（SelectedCellの範囲クランプ）とは別論点のため対象外
  のまま。

---

## 出典
- `docs/archive/ecad2-t051-round3-review-onmitsu.md`（§2-1、新規回帰の詳細・再現手順）
- `docs/archive/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md`（既存`T-selcell-1`〜`4`、対称性点検の
  出典）
- `docs/archive/ecad2-t055-increment3-delete-occupied-design-onmitsu.md`（クランプ意味論の前例、T-055増分3）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`FinishRowCountChange`:1364-1371、
  `AddRowCommand`:1692-1699、`ApplyUndoRedoSnapshot`:1802-1837、`GridSpec`生成:1592）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`GridSpec`生成:107、`RecordSnapshot`
  呼び出し:111/151行）
- `src/Ecad2.Core/Model/Sheet.cs`（`GridSpec.MinRows`=1・`MaxRows`=60:34/36行）
- `onmitsu.md`「テスト設計の起草」節
