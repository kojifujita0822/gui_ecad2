# T-051往復1周目修正 再レビュー（隠密）

対象: コミット`8b1b734`（隠密レビュー`docs/ecad2-t051-review-onmitsu.md`#1〜#4対応）。テスト設計書=
`docs/ecad2-t051-bugfix-test-design-onmitsu.md`。家老指定4観点の手動確認＋`code-review`スキル
（xhigh、9角度→verify、1角度はAPIセッション上限でエラー終了のため未実施）を併用。

**結論を先に：家老指定4観点(a)〜(d)はいずれも妥当。ただしレビュー過程で、T-051初回実装
(`0693755`)由来の未発見の重大バグ1件（CONFIRMED）を新たに検出した。今回の往復修正(#1〜#4)の
スコープ外だが、Undo/Redo機能全体としては「クリーン」と言い切れない状態にある。**

---

## 1. 家老指定4観点

### (a) #1〜#4がテスト設計書どおり実装されたか

**OK、全項目一致。**

| 設計書 | 実装（テスト名） | 判定 |
|---|---|---|
| U-B1〜U-B4 | `Clear_WithUndoHistory_MakesCanUndoFalse`他3件（UndoManagerTests.cs） | 一致 |
| T-051bugfix-1/2相当 | `NewDocument_ClearsUndoHistory_UndoCommandBecomesDisabled`/`ClearsRedoHistory...` | 一致 |
| S-B1〜S-B4 | `UndoCommand_Execute_OnSingleSheetHistory_SelectsRemainingSheet`他4件（SelectedSheetNotificationTests.cs） | 一致（S-B2/S-B4は後述(c)の訂正あり） |
| #3 | ViewModelレベルテスト無し（設計書どおり、コードレビュー＋忍者実機確認に委譲） | 一致 |
| O-B1〜O-B4 | `UndoCommand_Execute_ClearsOutputPanelDiagnostics`他3件（UndoRedoCommandsTests.cs） | 一致 |

省くべきでない項目の欠落なし、設計に無い追加（`PlaceUnresolvedPartIdElement`ヘルパー等）は許容範囲。

### (b) RED証明3件（#1・#2・#4）の整合

**OK。** いずれも根本原因の経路を正しく突いている。
- #1: `NewDocument_ClearsUndoHistory_UndoCommandBecomesDisabled` — `UndoManager.Clear()`が無ければ
  `ReplaceDocument`後も履歴が残存し`CanExecute==true`のままでFAILする経路。
- #2: `UndoCommand_Execute_AfterAddCommand_RaisesSelectedSheetChanged_ExactlyOnce` —
  `RefreshSelectedSheet`呼び出しが無ければ発火数0でFAILする経路。
- #4: `UndoCommand_Execute_ClearsOutputPanelDiagnostics` — `OutputPanel.ClearResults()`が無ければ
  診断残留でFAILする経路。

### (c) 侍による設計訂正の妥当性（S-B2/S-B4、重要）

**妥当。バグの隠蔽ではない。**

`SheetNavigationViewModel.cs`のAddCommand実装（90-142行）を実物確認した。`_dispatcher.BeginInvoke
(ContextIdle, () => { ... SetCurrentSheetIndexCore(index) ... })`により、シート追加直後に選択を
新シートへ自動移動する処理はT-050由来の既存仕様（コメント48-51行に明記）であり、コミット`8b1b734`
で新設されたものではない。テストが使う`ImmediateDispatcherService`（`BeginInvoke`を同期`action()`
実行するテスト用ダミー）により、この自動移動は`AddCommand.Execute(...)`呼び出し完了時点で既に
反映済みとなる。

侍の訂正は「テスト実装時に判明した、Given条件（AddCommand実行後もindexが変わらない）という誤前提の
是正」であり、`ApplyUndoRedoSnapshot`本体のUndo/Redoロジック自体（#2の修正）は一切変更されていない。
S-B2のUndo後の最終状態検証（`SelectedSheetContentPreserved`、シート2の名前と一致）は訂正されず維持
されている。S-B4はRedo後の期待値が「シート3」から「シート2」に訂正されたが、これも`_currentSheetIndex`
がクランプのみで動く（Undo/Redo自体は選択位置を戻す仕組みを持たない）という実装ロジックから論理的に
導出でき、矛盾はない。

### (d) #3のCtrl+Z/Yガードが既存Ctrl+S/O/Nと完全同型か

**機能的には妥当（実マウス操作は無害）。ただし実装位置は既存パターンと異なり、UI Automation経由には
別の既知の穴がある。**

`MainWindow.xaml.cs`145-146行のコメント「LostKeyboardFocus(物理フォーカス喪失)はスコープを跨いでも
必ず発火する」という既存の実測知見、およびWPFの`Control`基底クラスが`MouseDownEvent`で自動的に
`Focus()`を実行する仕組み（verify確認済み）により、メニュー/ツールバーのUndo/Redoボタン
（`Command="{Binding UndoCommand}"`でClickハンドラを経由しない設計）をマウスクリックした場合も、
`DeviceNameBox_LostKeyboardFocus`→`CommitDeviceNameEdit()`が自然に発火し、実害は無い。これは
`AddRowCommand`/`DeleteRowCommand`等、既存の全Commandバインディングボタンが明示的な
`CommitDeviceNameEdit()`呼び出しを持たない理由でもあり、アプリ全体で確立された暗黙パターンの範囲内。

ただし、既存Ctrl+S/O/Nは「呼び出し先の関数（`SaveDocument`/`ConfirmDiscardIfDirty`）内部でガードする」
設計であるのに対し、今回のCtrl+Z/Yは「呼び出し元のcaseブロックで直接ガードする」という異なる置き場所
になっている（`UndoCommand`/`RedoCommand`はViewModel層のためView層の`CommitDeviceNameEdit`を内部で
呼べないという構造上の制約による。他に選択肢は無い）。**「完全同型」ではなく「機能的に等価だが実装
機構は異なる」が正確な評価。**

さらに、`ecad2-ui-automation`スキル自体のSKILL.md（52-57行、227-232行）に「UI Automation経由の操作
（InvokePattern等）はClickハンドラ・LostFocus駆動のbinding反映を経由しない」既知の制約が既に記録
されている。忍者がこのスキルでCtrl+Z/Yのボタンを検証する際、**UI Automation経由だとマウスクリックとは
異なりフォーカス遷移を伴わない可能性がある**ため、キーボードショートカット（SendKeys等）での確認を
優先するよう申し送りたい。

---

## 2. 新規発見（`code-review`スキル、xhigh、9角度→verify）

### 2-1. 【CONFIRMED・最重要】`ApplyUndoRedoSnapshot`が自身の設計意図に反しSelectedCellを無条件クリアする

`MainWindowViewModel.cs`。`ApplyUndoRedoSnapshot`のXMLコメント（1798-1801行）は「新規/開く専用の
ReplaceDocumentとは意味論が異なる（**SelectedCell/Tool状態/StatusMessageは巻き戻さず現状維持、
殿裁定2026-07-11**）」と明言している。設計書`docs/ecad2-t051-implementation-plan-samurai.md`93行目・
147行目（「開かれた論点1」として明記、殿裁可を要する論点だった）にも同じ方針が記載されている。

しかし実装は1814行で`SetCurrentSheetIndexCore(clampedIndex)`を呼んでおり、このメソッド自体
（152-157行）は`SelectedCell = null;`を**常時無条件**で実行する設計（140-151行のコメントにT-041由来の
「常時無条件」設計と明記）。`SelectedCell`のsetter（201行以降）はこの代入により`SelectedConnector`/
`SelectedWireBreak`/`SelectedFreeLine`/`SelectedConnectionDot`のnull化と記入中ドラフト
（`ClearConnectorDraftIfAny`/`ClearFreeLineDraftIfAny`）の破棄まで連鎖させる「唯一の入口」。

**再現手順**: 要素を選択中（`SelectedCell != null`）に、無関係なシート（別シートへの要素追加等）の
Undo/Redoを実行する→`ApplyUndoRedoSnapshot`→`SetCurrentSheetIndexCore`経由で`SelectedCell`が強制的に
nullへ→選択状態・プロパティパネル表示が消え、記入中だった縦コネクタ/自由線のドラフトも無警告で破棄
される。

**混入時期**: `git show 0693755`で確認したところ、`SetCurrentSheetIndexCore(clampedIndex)`呼び出しは
**T-051初回実装コミット`0693755`から存在**しており、今回の往復修正`8b1b734`が新規に混入させたもの
ではない。**隠密自身のT-051初回レビュー（`docs/ecad2-t051-review-onmitsu.md`観点(d)「範囲自体はOK、
`SelectedCell`/`Tool`/`StatusMessage`への直接的な巻き込みは無い」）が誤りだった。** `SetCurrentSheetIndexCore`
経由の間接的な巻き込みを見落としていたことをここに訂正する。

新設テスト（`UndoRedoCommandsTests.cs`等）にもSelectedCell/SelectedConnectorの現状維持を検証する
ケースは無く、この回帰は現状のテストスイートでは検出されない。

### 2-2. 【PLAUSIBLE・忍者実機確認事項】デバイス名編集確定直後のUndo実行で、確定内容が見た目上消える

`MainWindow.xaml.cs`937/942行。DeviceNameBox編集中（未確定）にCtrl+Zを押すと、`CommitDeviceNameEdit()`
がまず現在のDocumentへ確定書き込みするが、直後の`UndoCommand.Execute`が「確定直後のDocument」を
Redoスタックへ退避しつつ、より古いUndoスナップショットへ丸ごと差し替える。結果、ユーザーが今しがた
確定させたデバイス名変更が画面から一瞬で消える（データはRedoスタックに残るため恒久喪失ではない）。

verify結果：`UndoManager.Undo`の実装・`RecordSnapshot`呼び出し箇所（シート追加/削除のみ、MVP対象範囲）
から見て実装は一貫しており、データ破損ではなく「MVPスコープ外操作（デバイス名編集）の確定タイミングと
Undoの全文書スナップショット差し替えが重なった際の視覚的体験」に過ぎないと評価できる。バグと言い切る
には忍者の実機確認が必要（`docs/ecad2-t051-bugfix-test-design-onmitsu.md`§3.2が挙げた確認観点
「編集内容が消失しないこと」と重なる論点であり、既存の申し送り範囲内）。

### 2-3. 経過観察（cleanup、複数角度が同一箇所を指摘）

**ApplyUndoRedoSnapshot/ReplaceDocumentの重複ロジック**: 「Document差し替え前にSelectedSheetを捕捉→
リセット後にRefreshSelectedSheetで通知」という2行パターンが両メソッドに独立に複製されている
（`MainWindowViewModel.cs` 1612/1651行 vs 1808/1820行）。将来この手順を変更する際、2箇所を同時に
直す必要があり修正漏れの温床になる。コミット自身が`RemoveDeviceIfUnreferenced`で同種の3箇所重複を
一本化した実績があり、同基準を適用する余地がある。severity low〜medium。

**軽微な二重描画/二重通知**: Ctrl+Z/Y押下時、`CommitDeviceNameEdit()`内の`RedrawCanvas()`と
`ApplyUndoRedoSnapshot`末尾の`NotifyCurrentSheetChanged()`起因の`RedrawCanvas()`が最大2〜3回連続
実行される（機能的不整合は無い、無駄な再描画のみ）。`SetCurrentSheetIndexCore`が内部で既に
`NotifyCurrentSheetDependentPropertiesChanged()`を呼んでいるのに、直後の明示的`NotifyCurrentSheetChanged()`
が同じ通知を二重発火させている点も同根。

**UndoCommand/RedoCommand CanExecuteChanged未発火**: `UndoManager.Clear()`呼び出し後、WPFの
`CommandManager.RequerySuggested`任せの暗黙的な自動requeryに依存している。既存の`RelayCommand`全体が
同じパターンを採用しており、今回のClear()固有の回帰ではない。実害は低い（誤クリックしても
`UndoManager`内部のCanUndoガードで早期return、無害）。

---

## 3. ビルド・テスト実測

```
dotnet build src/Ecad2.sln --no-incremental → 0エラー・0警告
dotnet test src/Ecad2.sln --no-build
  Ecad2.Core.Tests: 64件 合格
  Ecad2.App.Tests: 410件 合格（失敗0）
```

ただし2-1で指摘したSelectedCell矛盾の回帰テストは含まれていない（テストスイートはこの問題を検出
できない状態）。

---

## 4. 結論・推奨

- 家老指定4観点(a)〜(d)は全て妥当と判定。#1〜#4の往復修正自体はテスト設計書どおり正しく実装されている。
- **2-1（SelectedCell矛盾、CONFIRMED）はT-051初回実装由来の重大バグであり、今回の往復修正の対象外
  だが、Undo/Redo機能全体としては未解決のまま残っている。** 「殿裁定2026-07-11」の意味論
  （SelectedCellは現状維持）に実装が追随できていないため、対応要否・優先度は殿・家老の裁定を仰ぎたい。
- 2-2は忍者実機確認の追加観点として申し送り（§3.2の既存確認事項と統合可能）。
- (d)の指摘どおり、忍者はUI Automation経由でCtrl+Z/Yボタンを検証する際、フォーカス遷移非依存の
  InvokePattern特有の穴（スキル既知の制約）を踏まえ、キーボードショートカット確認を優先されたい。
- 2-3は経過観察のみ、着手不要。

---

## 出典
- `docs/ecad2-t051-review-onmitsu.md`（今回の起点、指摘#1〜#4）
- `docs/ecad2-t051-bugfix-test-design-onmitsu.md`（テスト設計、§3.2の忍者確認事項）
- `docs/ecad2-t051-implementation-plan-samurai.md`（93行目・147行目「開かれた論点1」）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`SetCurrentSheetIndexCore`:152-157、
  `SelectedCell`setter:201以降、`ApplyUndoRedoSnapshot`:1798-1829）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`AddCommand`:90-142）
- `src/Ecad2.App/MainWindow.xaml.cs`（`CommitDeviceNameEdit`:162-166、Ctrl+Z/Y:932-945）
- `.claude/skills/ecad2-ui-automation/SKILL.md`（52-57行、227-232行、UI Automation経由の既知制約）
