# T-051（Undo/Redo基盤MVP）実機確認記録（忍者）

対象コミット: `8a6eb13`（往復3周分込み、隠密最終レビュー`docs/archive/ecad2-t051-round4-final-review-onmitsu.md`でクリーン確定済み）。
家老指定8観点(a)〜(h)を`ecad2-ui-automation`スキルで検証。

**結論を先に：8観点すべてOK（回帰なし）。(h)はPLAUSIBLE事項として再現を確認したが、家老指定どおり「再現すれば記録のみ」の扱いとする（恒久喪失でないことも確認済み）。**

---

## 1. 観点別結果

### (a) シート追加→Ctrl+Z→Ctrl+Yの基本動作 — OK

- シート追加（AddSheetDialog経由）→シート数2件、Undo IsEnabled=True
- Undo実行→シート数1件に復元、Redo IsEnabled=True
- Redo実行→シート数2件に復元、Undo IsEnabled=True
- シート名・選択状態とも正しく往復

### (b) 元に戻す/やり直しボタンのIsEnabled連動 — OK

- 初期状態（履歴なし）：Undo/Redoとも IsEnabled=False
- シート追加後：Undo=True/Redo=False
- Undo後：Undo=False/Redo=True
- Redo後：Undo=True/Redo=False
- 履歴の有無と正確に連動することを確認（IsEnabled=Falseのボタンは`InvokePattern.Invoke()`が
  「認識できないエラーです」で例外になることも確認済み＝無効化が実効していることの傍証）

### (c) 新規作成/別文書オープン後のUndoで旧文書が化けない — OK

- シート2枚・Undo履歴ありの状態から「新規作成」実行→「保存しますか？」確認ダイアログで
  「いいえ」選択→新規文書化
- 新規文書はシート1枚のみ、Undo IsEnabled=False（履歴クリア）
- 旧文書（シート2枚、行11に拡張済み）の内容は一切表示されず、化けもない

### (d) Undo/Redo後の左パレットのシート選択ハイライト — OK

- シート追加直後は新シートがハイライト、Undo後はシート1がハイライト、Redo後もシート1のまま
  ハイライト（round2レビュー(c)節の説明どおり「Undo/Redoは選択位置を積極的に追わずクランプの
  みで動く」既存仕様と一致、崩れ・異常表示なし）

### (e) DeviceNameBox編集中（未確定）にCtrl+Z→編集確定後にUndoが走ること — OK（副次挙動あり、後述）

- a接点要素(X1)のDeviceNameBoxで"X2"に未確定編集→Ctrl+Z送信
- Ctrl+Z直後：シート数が2→1に変化＝Undo実行を確認。DeviceNameBoxの表示は"X1"に戻る
- 編集（X1→X2）自体は実際に確定されていたことをRedoで確認（後述(h)参照）＝設計意図どおり
  「編集確定→Undo実行」の順序で動いている

### (f) DRC実行→シート追加→Ctrl+Zで出力パネルに古い診断が残留しない — OK

- a接点(X1、コイルなし)を配置しDRC実行→`DRC-XREF-001`警告1件が出力パネルに表示
- シート追加→出力パネルの診断は1件のまま残留（正常、シート追加はDRC結果に無関係）
- Undo実行（シート追加取り消し）→出力パネルは0件にクリア（`OutputPanel.ClearResults()`が
  正しく機能）

### (g) Undo/Redo後もSelectedCellが維持される・シート数減少時も異常座標にならない — OK

- シート1でX1選択中（行3/列2）に、無関係な別シート追加操作のUndo/Redoを実行しても選択枠・
  プロパティパネルとも崩れず維持（round2レビュー2-1のCONFIRMEDバグがround3修正
  `ClampSelectedCellToSheetRows`で解消済みであることの実機的な裏付け）
- クランプ動作（T-selclamp-1相当）：シート1をRows=10→15に拡張→行15/列2を選択→シート2追加を
  Undo（復元後シート1はRows=10のスナップショットへ巻き戻る）→選択セルは「行10/列2」へ正しく
  クランプ（異常座標にならない）

### (h) PLAUSIBLE追試：デバイス名編集確定直後のUndo実行で編集内容が見た目上消えるか — 再現（記録のみ）

`docs/archive/ecad2-t051-round2-review-onmitsu.md`§2-2で指摘されたPLAUSIBLE事項を実機で再現した。

- (e)の手順の直後、Redoを実行→シート2が復元されると同時に、シート1のX1要素も「X2」に
  正しく復元された（機器表・キャンバス表示・プロパティパネルの3箇所すべてで確認）
- 隠密の推測「CommitDeviceNameEdit()が確定直後のDocumentをRedoスタックへ退避しつつ、より古い
  Undoスナップショットへ丸ごと差し替えるため、確定済みの変更が画面から一瞬で消えるが、
  データはRedoスタックに残るため恒久喪失ではない」を実機で完全に裏付け
- 家老指定どおり、対応要否は判断せず記録のみとする

---

## 2. 副次発見：AddSheetButtonのモーダルダイアログとUI Automation Invokeの罠（スキル改修候補）

検証序盤、`AddSheetButton`（Click="AddSheetButton_Click"のコードビハインドで`AddSheetDialog`を
`ShowDialog()`表示するボタン）に対し、`InvokePattern.Invoke()`を短時間に複数回呼び出したところ、
**モーダル制約を無視して背後のボタンが連打され、同一の「シート追加」ダイアログが3枚重なって開く**
事故が発生した。

- 症状：ダイアログ1枚目が開いた状態を知らずに他のボタン（ツール切替等）をInvokeすると
  「認識できないエラーです」という例外が断続的に発生し、UIツリー探索（`SheetNavList`の
  `FindAll`等）も一見「空」に見えるなど、原因不明の不安定挙動が連鎖した
- 判明した根本原因：`Save-Ecad2Screenshot`はメインウィンドウのハンドルしか描画しないため、
  ダイアログの存在がスクリーンショットに写らない（既知の罠、SKILL.md「ダイアログ・ポップアップが
  FindAllで検出できない」の類例）。EnumWindows（SKILL.md6節記載の手法）で確認したところ、
  「シート追加」ダイアログが3枚重なって存在していた
- **新知見**：通常のマウスクリックならモーダルダイアログが背後のボタンへの入力をブロックするが、
  UI Automationの`InvokePattern.Invoke()`はこのモーダル制約を無視してボタンのClickハンドラを
  直接発火させてしまう。ダイアログを開くボタンを誤って連続Invokeすると、同一ダイアログが
  何重にも開く事故に繋がりうる
- **改修提案**：ダイアログを開く可能性のあるボタン（`AddSheetButton`/`RenameSheetButton`等、
  Click=コードビハインドで`ShowDialog()`するもの）をInvokeした後は、必ず本記録2節の手順で
  EnumWindowsによりダイアログの出現有無を確認してから次の操作へ進む、という手順をSKILL.mdへ
  明記することを提案する（家老へ改修要否の判断を委ねる）

---

## 3. 範囲外の気づき（着手せず記録のみ）

- `SheetNavList`・`DeviceTableGrid`・`OutputGrid`の`ListItem`/`DataItem`の`Name`プロパティが
  `Ecad2.Model.Sheet`/`Ecad2.Model.Device`のようなToString()既定表示になっている（UI
  Automationからの可読性が低いのみで、画面表示自体は正しい文字列。実害なし・修正不要と判断）

---

## 4. 総合判定

家老指定8観点すべてOK、回帰なし。T-051（Undo/Redo基盤MVP）はクローズ可能な水準にあると判断する。

## 出典
- `docs/archive/ecad2-t051-round2-review-onmitsu.md`（観点(d)申し送り・§2-1/2-2 PLAUSIBLE事項の出典）
- `docs/archive/ecad2-t051-round4-final-review-onmitsu.md`（往復3周目クリーン確定）
- `docs/archive/ecad2-t051-selectedcell-clamp-test-design-onmitsu.md`（T-selclamp系シナリオ、(g)クランプ検証の再現手順出典）
- `.claude/skills/ecad2-ui-automation/SKILL.md`（既知の罠、EnumWindows手法）
