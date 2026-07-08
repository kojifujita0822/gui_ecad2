# T-041増分7 テストコード静的レビュー(忍者、殿指示)

> 2026-07-08 忍者。通常は隠密のcode-review領域だが、殿の指示により忍者が担当。読み取り専用の
> コードリーディングであり実機操作は伴わない。general-purposeエージェントへ調査を委譲し、
> 結果を検証・整理した。
>
> 対象：`tests/Ecad2.App.Tests/ConnectorDragAndResizeTests.cs`・`WireBreakDragTests.cs`・
> `FreeLineDragAndResizeTests.cs`・`ConnectionDotDragTests.cs`（コミット`cbc74ac`時点）
> 背景：`docs/ecad2-t041-increment7-review-onmitsu-2.md`の所見X/Y/Z/AAに対する侍の修正
> （`cbc74ac`）が、テストでどこまで裏付けられているかを確認する。

---

## 結論：所見X・Yとも「回帰を検出できない」既存テストのパターンが`cbc74ac`後もほぼ手つかずで残存。
所見AAのみ新規テスト2件で実質的に埋まっている。

---

## 観点1：テスト名とアサーション内容の整合性

大部分は整合しているが、1件乖離あり。

- `FreeLineDragAndResizeTests.cs:110-125`
  `ResizeSelectedFreeLineEndpoint_Vertical_IgnoresXDelta`は`deltaXMm=5, deltaYMm=-3`と
  **両軸とも非ゼロ**で呼んでいる。しかし実際の呼び出し元（`MainWindow.xaml.cs`の
  `ResizeSelectedFreeLineByKey`）は線の向きに応じて必ず片方を`0`にして渡す設計
  （`MainWindowViewModel.cs:756-757`のコメントにも明記）。このテストは「逆軸は無視される」
  という機構自体は検証できているが、所見Zの実際の発火条件（逆軸delta=**0**での自己代入→
  偽陽性MarkDirty）には触れておらず、テスト名が示唆するほどの保証はない。

他3ファイル（Connector/WireBreak/ConnectionDot）にテスト名とアサーションの乖離は見当たらず。

---

## 観点2：所見X・所見Yの穴埋め状況（最重要）

### 所見X：**原理的に単体テストでは検出不可能と判定**

4テストファイルはすべて`MainWindowViewModel`を直接生成するのみで、`MainWindow`（WPF Window）
や`LadderCanvasHost`を一切インスタンス化しない（`ViewModelTestBase.cs:24`）。所見Xの本体は
`MainWindow.xaml.cs`の`LadderCanvasHost_PreviewMouseLeftButtonUp`における`ConfirmDrag*()`と
`ReleaseMouseCapture()`の**呼び出し順序**、およびWPFの`Mouse.Capture`機構が`LostMouseCapture`
を同一コールスタック内で同期発火する挙動に起因する、View層（WPFのルーティングイベント配線）
の話。ViewModel単体を直接呼ぶ現行のテスト設計では、どのようなアサーションを足しても到達し
えない。`cbc74ac`でもこの種のテストは一切追加されていない（diffは`FreeLineDragAndResizeTests.cs`
のみ変更、他3ファイルは無変更）。**実機確認が必須。**

### 所見Y：**4種すべてで未だ検出不可能なまま（`cbc74ac`でも埋まっていない）**

各ファイルの「強制クリア」系テストは、以下すべて`BeginDrag*()`のみを呼び、`UpdateDrag*()`を
挟まずに強制キャンセル要因（Delete/シート切替/NewDocument）を発生させている：

- `ConnectorDragAndResizeTests.cs:245-265, 267-289, 291-307`
- `WireBreakDragTests.cs:158-176, 178-194`
- `FreeLineDragAndResizeTests.cs:232-251, 253-270`
- `ConnectionDotDragTests.cs:114-133, 135-152`

いずれも`Update`で位置を動かしていないため「開始位置」＝「現在位置」のまま強制キャンセル
される。`ForceCancelDrag*IfAny()`が旧実装（`_dragging*=null`のみ）でも新実装（`CancelDrag*()`
で復元）でも、これらのテストのアサーション結果は完全に同一になる（実際にトレースして確認）。
`cbc74ac`の新規2テストはどちらも所見AA（境界クランプ）用であり、**所見Y用のテストは4種
いずれにも追加されていない**。

所見Yは所見Xと異なり**ViewModelのみで検証可能**なロジックのため、実機確認は必須ではなく、
`BeginDrag*`→`UpdateDrag*`(位置をずらす)→強制クリア要因発生→`Assert.Equal(元の位置, ...)`
かつ`Assert.False(vm.IsDirty)`という追加テストを4種×主要経路へ書くだけで埋まるが、**現状は
埋まっていない**。

---

## 観点3：4種間のテストカバレッジ不整合

1. `SheetSwitch_ForceCancelsInProgressDrag`は**Connectorにしか無い**
   （`ConnectorDragAndResizeTests.cs:267-289`）。WireBreak/FreeLine/ConnectionDotに対応
   テストなし。実装（`SelectedCell`のsetter）は4種共通クリアだが、テストとしての明示確認漏れ。
2. `ConfirmDrag*_WhenPositionUnchanged_DoesNotMarkDirty`相当が**FreeLineだけに無い**。
   Connector・WireBreak・ConnectionDotにはあるが`FreeLineDragAndResizeTests.cs`に無し。
3. 所見AA（境界クランプ）の回帰テストはFreeLineのみ。ConnectionDot側
   （`UpdateDragConnectionDot`/`MoveSelectedConnectionDot`）には境界クランプの**実装自体が
   存在しない**。レビュー文書は所見AAを明示的に「FreeLine固有」としており意図的判断と見えるが、
   ConnectionDotもmm実座標系でUndo機能が無い点はFreeLineと同条件であり、実装スコープの疑問点
   として申し送る（テストの穴というより実装側の検討余地）。
4. 所見B（Top/Bottom相互反転防御）テストはConnectorのみだが、これはレビュー文書で「対象外は
   妥当」と確認済みのため問題なし。

---

## 観点4：その他のテスト漏れ

- 所見Zの真の再発防止テストが無い。「水平線にdeltaXMm=0・deltaYMm≠0」「垂直線にdeltaYMm=0・
  deltaXMm≠0」という実際の呼び出しパターンで`ResizeSelectedFreeLineEndpoint`を呼び
  `Assert.False(resized)`かつ`Assert.False(vm.IsDirty)`を確認するテストが無い。所見Yと同様
  ViewModel単体で検証可能な範囲であり、実機確認無しで埋められる。
- `WireBreakDragTests.cs:104-117`（`DragWireBreak_ClampsAtGridBounds`）は`Row`上限クランプ
  のみ検証、`Boundary`（Grid.Columns）側の上限クランプは未検証。同様に
  `MoveSelectedWireBreak_ClampsAtGridBounds`(53-66行)も下限側のみ。優先度は低いが漏れとして記す。

---

## 単体テストでは検証不可能なため実機確認が必須な項目

1. **所見X本体**：マウスでドラッグして指を離した瞬間、要素が元位置へスナップバックしない
   こと（Connector/WireBreak/FreeLine/ConnectionDotの4種すべて）。**最優先で確認すべき項目。**
2. Escによるドラッグキャンセルとその後のMouseUpの相互作用（二重処理にならないことの裏取り）。
3. Alt+Tab等の外的要因によるマウスキャプチャ喪失時の挙動（所見C対応分）。
4. シート切替（左パレットの実際のクリック操作）がドラッグ中に発生した場合、所見Y修正が実機
   でも正しく機能する（黙って半端な位置が確定しない）ことの最終確認。

---

## 出典・参照

- `docs/ecad2-t041-increment7-review-onmitsu-2.md`（所見X/Y/Z/AAの原本）
- 対象コミット`cbc74ac`（`git show`で全差分確認）
- `tests/Ecad2.App.Tests/ConnectorDragAndResizeTests.cs`・`WireBreakDragTests.cs`・
  `FreeLineDragAndResizeTests.cs`・`ConnectionDotDragTests.cs`・`ViewModelTestBase.cs`
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`・`src/Ecad2.App/MainWindow.xaml.cs`
