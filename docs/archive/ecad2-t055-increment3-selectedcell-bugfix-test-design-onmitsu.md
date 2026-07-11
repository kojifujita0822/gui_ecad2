# T-055増分3 バグ修正a・b テスト設計（隠密起草）

家老采配（2026-07-11、隠密レビュー`docs/ecad2-t055-increment3-round2-review-onmitsu.md`の要修正
a・b対応）。バグ修正・往復案件のためテスト設計と実装を分離する制度【MUST】（`onmitsu.md`
「テスト設計の起草」節）に従い、仕様側から設計する。**侍はこの設計をコードへ落とす。設計に無い
テスト追加は自由、設計にあるものを省くのは不可。**

---

## 0. 前提（恒久対応の実装方針、隠密提案）

`docs/ecad2-t055-increment3-round2-review-onmitsu.md`§2-1で提案した恒久対応：
**`DeleteRowAtCommand`の`RowOps.DeleteRow`実行後、SelectedCellの状態(null/row以下/rowより後ろの
いずれでも)に関わらず無条件で`SelectedElement`系4プロパティ（`SelectedElement`・
`HasSelectedElement`・`SelectedElementKindDisplay`・`SelectedElementDeviceName`）の
`PropertyChanged`を発火させる**ことを前提として期待値を設計する。

実装方法（侍の裁量、以下いずれでもテスト仕様は変わらない）：
- (i) 削除処理完了後に明示的な通知ヘルパー（例: `NotifySelectedElementChanged()`)を新設し無条件で呼ぶ
- (ii) `SelectedCell`のsetterを常に経由させる形に処理順序を変更する

**注意**：bの原因（`SelectedCell`の-1シフト代入が`RowOps.DeleteRow`より先に実行され、シフト前の
座標系で一度評価される）は、(i)方式では「誤った値での一時発火→正しい値での再発火」という2回発火に
なり、(ii)方式（シフト代入自体を削除処理の後へ移す等）では最初から1回で正しく発火しうる。どちらの
実装でも**最終的に（最後に）発火する`PropertyChanged`時点での値が正しいこと**を検証すれば、実装方式に
依存しない頑健なテストになる。下記テスト仕様はこの前提で設計する。

---

## 1. 同値分割・境界値分析

`DeleteRowAtCommand.Execute(row)`実行時の`SelectedCell`の状態を分類する。

| # | 分類 | SelectedCellの変化 | 実データの変化 | 通知の要否 |
|---|---|---|---|---|
| B0 | `SelectedCell == null` | 変化なし | 対象行の要素は削除されるが選択とは無関係 | 不要（既存動作のまま） |
| B1 | `SelectedCell.Row < row`（削除対象行より前） | 変化なし | 選択中要素は影響を受けない | 不要（既存動作のまま） |
| **B2** | `SelectedCell.Row == row`（**指摘a**、削除対象行そのもの） | 値は変化しない（setterを通らない） | 選択中要素は消滅、シフトにより別要素が同じ座標に来る場合あり | **必要（現状バグ）** |
| **B3** | `SelectedCell.Row == row + 1`（**指摘bの最小境界**、削除対象の直後） | -1されrowになる | シフトによりrowの位置に来る要素が変わる | **必要（現状バグ）** |
| **B4** | `SelectedCell.Row > row + 1`（**指摘bの一般ケース**） | -1される | 同上、シフト後の座標に来る要素が正しく反映される必要 | **必要（現状バグ）** |
| B5 | `row == 0`（先頭行削除） | B2/B3/B4のいずれかと直交する境界 | 同上 | B2〜B4と同じ規則 |
| B6 | 削除後`Grid.Rows`が下限に達する（`FinishRowCountChange`のクランプと競合） | クランプで別途-1されうる | - | クランプ後も最終的に正しい要素を指すこと |

---

## 2. テストケース設計

配置場所は`tests/Ecad2.App.Tests/RowInsertDeleteCommandsTests.cs`（既存のDeleteRowAtCommand関連
テストと同ファイル）を推奨。

### 2.1 指摘a（B2: 削除対象行そのものを選択中）

**T-a1【RED証明の中核】削除対象行を選択中に削除すると、PropertyChanged(SelectedElementDeviceName等)が発火すること**

- Given: 行3に要素（DeviceName="X001"）を配置し`SelectedCell=(3,列)`を選択（右パネルにX001が表示された状態を模す）
- When: `DeleteRowAtCommand.Execute(3)`
- Then: `PropertyChanged`イベントで`nameof(SelectedElementDeviceName)`（または`SelectedElement`）が
  少なくとも1回発火したことを確認する（`vm.PropertyChanged += (s,e) => ...`で記録するか、
  `PropertyChangedForTest`フック(T-050で実績あり)を利用してもよい）。
- **RED証明**: 修正前コードは`sc.Row > row`がfalse（3>3=false）のためSelectedCellのsetterを
  通らず、この発火が一切起きない→本テストは修正前コードで確実に失敗する。

**T-a2 削除対象行の要素が消え、シフトで別要素が来る場合、最終的にSelectedElementがその別要素を正しく指すこと**

- Given: 行3に要素A（DeviceName="X001"）、行4に要素B（DeviceName="Y001"、Aと同じ列）を配置。
  `SelectedCell=(3,列)`を選択。
- When: `DeleteRowAtCommand.Execute(3)`
- Then: 削除後、要素Bが行3へシフトされる。最終状態で`vm.SelectedElementDeviceName == "Y001"`
  であること（`SelectedElement`のgetterは都度算出のため、実行完了後に読めば元々このアサーションは
  常に成立する——これは「データモデル自体は正しい」ことの確認であり、T-a1（通知が飛ぶこと）と
  合わせて初めて「UIが正しく追随する」ことの証明になる）。

**T-a3 削除対象行の要素が消え、シフトで来る要素が無い場合、SelectedElementがnullになること**

- Given: 行3に要素A（DeviceName="X001"）のみ配置（行4以降その列に要素なし）。`SelectedCell=(3,列)`。
- When: `DeleteRowAtCommand.Execute(3)`
- Then: `vm.SelectedElement is null`、`vm.HasSelectedElement == false`。

### 2.2 指摘b（B3/B4: 削除対象行より後ろを選択中）

**T-b1【RED証明の中核】削除対象行より後ろを選択中、PropertyChangedの最終発火時点でのSelectedElementDeviceNameが正しい(シフト後)要素を指すこと**

- Given: 行3（削除対象、空行）、行4に要素B（DeviceName="B001"）、行5に要素A（DeviceName="A001"、
  Bとは別の要素、Aと同じ列にBもある配置）を用意。`SelectedCell=(5,列)`を選択（要素Aを選択中）。
- When: `vm.PropertyChanged`を購読し`nameof(SelectedElementDeviceName)`発火の**都度**
  `vm.SelectedElementDeviceName`の値を記録するリストを用意した上で、`DeleteRowAtCommand.Execute(3)`
  を実行する。
- Then: 記録した値リストの**最後の要素**が`"A001"`であること
  （`Assert.Equal("A001", capturedValues.Last())`）。
- **RED証明**: 修正前コードは`SelectedCell = sc with { Row = 4 }`の代入時点（`RowOps.DeleteRow`
  実行前）で1回だけ`OnPropertyChanged(SelectedElementDeviceName)`が発火し、その時点の
  `sheet.Elements`は未シフトのため行4にある要素B（"B001"）が拾われる。事後の再通知が無いため、
  記録リストは`["B001"]`のみとなり最後の値も`"B001"`——期待値`"A001"`と一致せず本テストは
  修正前コードで確実に失敗する。
  修正後（0節の恒久対応）は、削除後の無条件通知により記録リストへ`"A001"`（正しい値）を含む
  発火が追加され、最後の値が`"A001"`になる。

**T-b2 境界値B3（削除対象の直後を選択、shift幅=1の最小ケース）**

- Given: 行3（削除対象）、行4に要素A（DeviceName="A001"）のみ。`SelectedCell=(4,列)`を選択。
- When: T-b1と同じ記録方式で`DeleteRowAtCommand.Execute(3)`を実行。
- Then: 最終的に`vm.SelectedCell == (3,列)`かつ記録リストの最後の値が`"A001"`
  （シフト後、要素Aが行3へ来て、選択もそれに追随している）。

### 2.3 対称性点検（a・b共通の最終状態検証）

**T-ab1 a・bいずれの経路でも、削除完了後の`vm.SelectedElement`（都度算出）と、最後に発火した
PropertyChanged時点で観測された値が一致すること**

- 目的：「データモデルは正しいがUI通知が追随しない」という不整合の再発防止を汎用的にカバーする。
- Given/When: T-a1〜T-a3、T-b1〜T-b2の各シナリオを流用。
- Then: 各シナリオについて、`(削除完了後にvm.SelectedElementDeviceNameを直接読んだ値)` ==
  `(PropertyChanged経由で記録した最後の値)` が一致すること。
- これは`[Theory]`化せず、各既存Factの末尾アサーションとして追加する形を推奨する
  （シナリオごとにGiven条件が大きく異なり、Theoryのパラメータ化に適さないため）。

---

## 3. 状態遷移分析について

`DeleteRowAtCommand`単体の実行は「選択状態→削除操作→選択状態」という1ステップの遷移であり、複数の
中間状態を持つ状態機械ではないため、本設計では状態遷移図・遷移表の技法は適用対象外と判断する
（誤って技法を機械的に当てはめない）。ただし、T-b1で検証する「PropertyChanged発火の時系列（誤った
値での一時発火→正しい値での最終発火）」は、広義には時間軸上の状態遷移の一種であり、これをテストの
記録リスト方式（発火の都度キャプチャ）で捕捉する設計とした。

---

## 4. 実装時の注意（侍向け）

- T-b1/T-b2は`PropertyChanged`イベントの**発火回数**そのものを厳密にアサートしない（実装方式(i)/(ii)
  どちらでも合格できるように、「最後の発火時点の値」のみを見る設計）。発火回数を検証したい場合は
  追加のテストとして自由に足してよいが、削除は不可。
- 家老指定DoDの「修正前コードで確実に失敗する構成」は、T-a1・T-b1が該当する（RED証明の中核2件）。
  実装前に`git stash`等で修正前コードへ戻し、この2件が実際にFAILすることを実測してから着手すること
  （既存の「RED先行証明」制度に従う）。
- 既存の`RowInsertDeleteCommandsTests.cs`内の他のDeleteRowAtCommandテスト（B8のGrid.Rows下限テスト等）
  との整合を確認し、Given配置が既存テストと衝突しないこと。

---

## 出典
- `docs/ecad2-t055-increment3-round2-review-onmitsu.md`（指摘a・bの詳細、verify結果）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`SelectedCell`setter:201-239、
  `SelectedElement`getter:1161-1162、`DeleteRowAtCommand`:1733-1748、
  `NotifyCurrentSheetDependentPropertiesChanged`:165-170）
- `onmitsu.md`「テスト設計の起草」節
