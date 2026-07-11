# T-051往復2周目修正 再々レビュー（隠密）

対象: コミット`f2aaaad`（隠密再レビュー`docs/ecad2-t051-round2-review-onmitsu.md`§2-1のCONFIRMED
対応、テスト設計書=`docs/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md`）。家老指定4観点の
手動確認＋`code-review`スキル残り1角度（language-pitfall、前回API上限で中断分の補完）を実施。

**結論を先に：主目的（SelectedCell無条件nullリセットの解消）は正しく機能、T-selcell-1〜4全て設計書
どおり実装・合格。ただし補完レビューで新規PLAUSIBLE1件（今回の修正自体が持ち込んだ回帰、表示不整合
止まりでクラッシュ無し）を検出。既知の残存課題（記入中ドラフト消失、設計書§4で言及済み・DoD範囲外）
も実装未対応のまま残る。忍者実機確認へ回すこと自体は可能なレベルだが、正直に報告する。**

---

## 1. 家老指定4観点

### (a) 設計書突合（T-selcell-1〜4全実装、退避→復元の局所対応が提案どおりか）

**OK、完全一致。**

| 設計書 | 実装（テスト名） | Given/When/Then |
|---|---|---|
| T-selcell-1 | `UndoCommand_Execute_WithoutClamp_PreservesSelectedCell` | 一致 |
| T-selcell-2 | `RedoCommand_Execute_WithoutClamp_PreservesSelectedCell` | 一致 |
| T-selcell-3 | `UndoCommand_Execute_WithClamp_PreservesSelectedCellCoordinates` | 一致（`CurrentSheetIndex==1`・`SelectedCell==(7,4)`双方アサート） |
| T-selcell-4 | `UndoCommand_Execute_WhenSelectedCellIsNull_RemainsNull` | 一致 |

本体修正（`MainWindowViewModel.cs` 1815/1822行）も提案どおり：`SetCurrentSheetIndexCore`呼び出し前に
`var oldSelectedCell = SelectedCell;`で退避、呼び出し直後に`SelectedCell = oldSelectedCell;`で復元。
`SetCurrentSheetIndexCore`本体は無変更。

### (b) RED証明の整合（1/2/3 FAIL・4 PASSが設計書想定どおりか）

**OK。** コミットメッセージに「RED証明: T-selcell-1/2/3は修正前コードでFAIL実測済み」と明記。
T-selcell-4（`SelectedCell==null`のまま、C0ケース）は修正前でも当然PASSする設計（`null`は
`SetCurrentSheetIndexCore`が実行してもnullのまま変化しないため）——設計書の想定どおり。

T-selcell-2（Redo対称性）が修正前でFAILする理由も論理的に妥当：GivenがT-selcell-1のWhen実行後の
状態を引き継ぐため、修正前コードなら`UndoCommand.Execute`の時点で既に`SelectedCell`が`null`に
なっており、続く`RedoCommand.Execute`でも同様に`null`のまま、最終アサート
（期待値`(3,2)`）と一致せずFAILする。

### (c) 退避→復元方式の新たな穴（RefreshSelectedSheet・ResetSheets・DeviceTable.Rebind等との相互作用）

**直接の相互作用は無し。ただし、他の既存コマンド群との整合漏れを1件検出（下記2-1）。**

`SheetNavigationViewModel.RefreshSelectedSheet`（63行）は`OnPropertyChanged(nameof(SelectedSheet),
oldValue)`のみでSelectedCellに触れない。`DeviceTableViewModel.Rebind`（36-40行）は`Devices`一覧の
再構築のみ。`OutputPanel.ClearResults()`も診断パネル専用。いずれも`SelectedCell`復元（1822行）との
直接的な競合は無い。

一方、`ApplyUndoRedoSnapshot`内の`SelectedCell = oldSelectedCell;`（1822行）は、他の行数変更コマンド
（`AddRowCommand`/`DeleteRowCommand`/`DeleteRowAtCommand`等）が課している`FinishRowCountChange`の
範囲クランプを経由しない。§2-1で詳述。

### (d) code-review残り1角度（language-pitfall）の補完

実施完了（コミット8b1b734・f2aaaad両方を対象）。4件の候補のうち2件は既知の軽微な経過観察
（BeginInvokeタイミング競合による陳腐化通知＝実機限定・診断ログのみに影響、二重RedrawCanvas＝
機能破綻なし、いずれも往復1周目再レビューで経過観察済みの範囲）。残り2件は下記§2に記載。

---

## 2. 新規発見（`code-review`スキル、language-pitfall角度→verify）

### 2-1. 【PLAUSIBLE・新規回帰】SelectedCell復元時にGrid.Rows/Columns範囲へのクランプが欠落

`MainWindowViewModel.cs` 1822行。`SelectedCell = oldSelectedCell;`は、他の行数変更コマンド群が
一律で課している`FinishRowCountChange`（1364-1371行）の「`selectedCell.Row >= sheet.Grid.Rows`なら
`SelectedCell`を`Grid.Rows-1`へクランプする」処理を経由しない。

**再現手順**: シート1（初期`Grid.Rows`=22）を選択中にシート2を追加（`AddCommand`、この時点で
`UndoManager.RecordSnapshot`によりRows=22のシート1を含むDocumentがスナップショットされる）→
シート1へ戻り`AddRowCommand`を複数回実行してRows=26へ拡張（この操作はUndo管理対象外、MVP範囲=
シート追加/削除のみ）→`SelectedCell=(25,2)`を選択（Rows=26では有効）→Ctrl+Zで「シート2追加」を
Undo→復元される`restored`はRows=22のシート1（手順1時点のスナップショット）→`SelectedCell`は
`(25,2)`のまま復元されるが、復元後のシート1は`Grid.Rows`=22（有効行0-21）のため座標が範囲外になる。

verify結果：シナリオは技術的に成立（`RecordSnapshot`呼び出しは`AddCommand`/`DeleteCommand`の2箇所
のみ、`AddRowCommand`は`RecordSnapshot`を呼ばないことを実装で確認済み）。ただし実害は
**クラッシュではなく表示不整合止まり**（`GridPos`は無検証の値型、`SelectedElement`は
`FirstOrDefault`で安全にnullを返す、`SelectedCellDisplay`は「行26/列2」のような範囲外文字列を表示
するだけ、キャンバスのハイライト矩形もScrollViewerのクリップで実質不可視化される可能性が高い）。
Downキー操作で一発修復されるが、Up/Left/Rightでは修復されない。

**重要**: これは**今回のコミット`f2aaaad`が新たに持ち込んだ回帰**。直前（`8b1b734`時点）は
`SetCurrentSheetIndexCore`が常時無条件で`SelectedCell=null`にしていたため、範囲外座標が残留する
経路自体が存在しなかった。今回「退避・復元」でnull化を意図的に上書きした際、姉妹コマンド群が持つ
クランプ処理の移植が漏れた形。T-selcell-1〜4はいずれも既定10行グリッド内の座標のみを使っており、
このケースは未網羅。

### 2-2. 【既知・DoD範囲外】記入中ドラフト（Tool状態）が退避・復元の過程で無警告に破棄される

隠密自身のテスト設計書（`docs/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md` §4）で
「実装方式次第で記入中ドラフトが巻き込まれるリスクがある、DoD範囲外だが気づいたら家老へ別途報告
されたい」と明記していた懸念が、実際に実装どおりに顕在化していることをfinderが確認した。

`SelectedCell`のsetter（212-250行）は`SelectedConnector`等のクリアに加え`ClearConnectorDraftIfAny`/
`ClearFreeLineDraftIfAny`を無条件で呼ぶ。退避→`SetCurrentSheetIndexCore`（内部で`SelectedCell=null`）
→復元、という往復は、このsetterを2回通す。1回目の`null`代入で記入中の縦コネクタ/自由線ドラフトが
無警告に破棄され、2回目の復元で`SelectedCell`自体は元の座標に戻るため「選択位置は維持された」ように
見えるが、記入中だった作業内容は失われている。

家老指定DoDは「SelectedCell」の維持に限定されており、実装はDoDを満たしている。Tool状態全体の維持
まで求めるかは別途裁定が必要な論点として、往復1周目の時点から一貫して未着手のまま持ち越されている。

---

## 3. ビルド・テスト実測

```
dotnet build src/Ecad2.sln --no-incremental → 0エラー・0警告
dotnet test src/Ecad2.sln --no-build
  Ecad2.Core.Tests: 64件 合格
  Ecad2.App.Tests: 414件 合格（失敗0、T-selcell-1〜4の4件増加を確認）
```

---

## 4. 結論・推奨

- 家老指定4観点(a)〜(d)は全て妥当。往復2周目修正は主目的（SelectedCell無条件nullリセットの解消）を
  正しく達成しており、テスト設計書どおり実装されている。
- §2-1（Grid.Rows範囲外クランプ欠落）は今回の修正自体が持ち込んだ新規回帰だが、severityは表示
  不整合止まりでクラッシュ・データ破損は無い。対応要否・優先度は家老の裁定を仰ぎたい（往復3周目で
  即時対応するか、経過観察として許容し先送りするかの判断）。
- §2-2（記入中ドラフト消失）はDoD範囲外として当初から明記済みの既知課題。SelectedCellというDoDは
  満たしているため、往復修正自体の合否には影響しないが、未解決のまま残っている事実として申し送る。
- これらを踏まえても、今回の往復2周目修正を「クリーン」と判定してよいかは家老の基準次第——重大な
  データ破損は無いため忍者実機確認へ回すこと自体は可能と考えるが、正直に両論点を提示する。

---

## 出典
- `docs/ecad2-t051-round2-review-onmitsu.md`（起点、SelectedCell矛盾のCONFIRMED詳細）
- `docs/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md`（テスト設計、§4の既知課題）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`ApplyUndoRedoSnapshot`:1802-1837、
  `FinishRowCountChange`:1364-1371、`AddRowCommand`:1692-1699、`SelectedCell`setter:212-250）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`RecordSnapshot`呼び出し:111/151行、
  `RefreshSelectedSheet`:63行）
- `src/Ecad2.App/ViewModels/DeviceTableViewModel.cs`（`Rebind`:36-40行）
