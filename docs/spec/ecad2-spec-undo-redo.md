# ecad2 仕様書：Undo/Redo

T-075（殿裁定、2026-07-11起票）体系の第3号、第1弾最終領域。実装コード・殿裁定記録
（`docs/todo.md`/`docs/todo-archive.md`）・忍者実機検証記録（`docs-notes/`配下）を突き合わせ、
「仕様として確定している挙動」を出典付きで明文化する。

---

## 0. 前提：MVP範囲の限定仕様（最重要）

**Undo/Redoの対象はシート追加/削除のみ**（`SheetNavigationViewModel`の`AddCommand`/
`DeleteCommand`）。要素配置・削除、結線プリミティブの記入・削除・ドラッグ、シート改名、行数変更、
母線名変更、デバイス名変更等は**すべてUndo対象外**（`MarkDirty()`のみでスナップショット記録なし）。

この限定は「まだ実装が追いついていない未完成」ではなく、**殿裁定によるMVP段階導入の設計**である
（1節参照）。将来の対象拡大は計画済みだが未着手（6節参照）。

**本仕様書はまさにこの限定範囲が本日T-062検証で「退行か仕様か」の切り分けに難航した領域であり、
T-075起票の直接の動機の一つ。**

---

## 1. 設計の経緯（裁定根拠）

### 起票

起票元はP-032（忍者T-041増分5実機確認、`docs/proposed.md:46`「シート追加・削除操作がCtrl+Zの
対象外に見える」）。殿裁定で承認しT-051着手も、**着手前の侍調査（2026-07-10）で前提が崩壊**：
ecad2にはUndo機構自体が未実装だった（`MainWindowViewModel.cs:82-84`）。忍者の再検証
（`docs/archive/ecad2-t051-precheck-undo-verification-ninja.md`、4試行すべてUndo不作動）で確定し、
P-032の原観測は「配置・削除はUndo可能」という誤認と忍者自身が訂正。一旦保留（殿裁定2026-07-10）
となったが、殿直接指示（2026-07-11）「先行でUndo機能を検討してほしい」で再開。

### 設計方式の選択（案C採用）

3案を比較検討（`docs/archive/ecad2-t051-undo-redo-design-survey-onmitsu.md:91-125`）：

| 案 | 方式 | コスト |
|---|---|---|
| 案A | コマンドパターン（逆操作記録型、GuiEcad方式踏襲） | 大。対象範囲は理論上全操作可能だがテスト0件の脆さを継承しうる |
| 案B | Memento（Sheet単位スナップショット） | 中。`DeepClone`の新規実装が前提 |
| **案C（採用）** | **GcadSerializer流用のドキュメント全体スナップショット** | 小〜中。既存機構の流用で最小 |

案C採用理由：「MVP候補1（シート追加/削除のみ）と組み合わせると、Document.Sheetsという単一リストへの
操作のみで、JSON全体スナップショットでも1回保存するだけで足りる。操作頻度も低く性能懸念が薄い」
（同ファイル:134-136）。

### MVP対象範囲の確定（候補1採用）

3候補（候補1=シート追加削除のみ／候補2=要素配置削除／候補3=行挿入削除）のうち、起票背景P-032に
直結し案Cと相性が良い候補1を隠密が推奨、殿裁定でそのまま確定（`docs/todo.md:614-622`）。

### 往復修正（3周）

- 初回レビューで重大4件CONFIRMED：#1データ消失（`ReplaceDocument`が`UndoManager`をクリアしない）
  ／#2表示崩れ（シート選択ハイライト）／#3データ破損（`DeviceNameBox`編集中Ctrl+Zガード欠如）／
  #4表示不整合（DRC結果残留）→往復1周目修正（`8b1b734`）。
- 再レビューで新発見1件：`ApplyUndoRedoSnapshot`が`SelectedCell`を無条件nullリセット（殿裁定違反）
  →往復2周目修正（`f2aaaad`）。
- 再々レビューで新規PLAUSIBLE1件（`SelectedCell`がGrid.Rows範囲外を指しうる、往復2周目自体が
  持ち込んだ新規回帰）→殿裁定「往復3周目で修正してからクローズ」→往復3周目修正（`8a6eb13`、
  `ClampSelectedCellToSheetRows`新設）。
- 完全Done：隠密最終レビュークリーン確定→忍者実機8観点全OK
  （`docs-notes/ecad2-t051-realmachine-verification-ninja.md`）。

---

## 2. `UndoManager`の実装

`src/Ecad2.App/Commands/UndoManager.cs`：

- 保持方式：`Stack<string>`を2本（`_undoStack`/`_redoStack`）。中身はJSON文字列
  （`GcadSerializer.Serialize`の結果）。**件数上限はコード上存在しない**。
- `CanUndo => _undoStack.Count > 0`、`CanRedo => _redoStack.Count > 0`（単純な件数判定のみ）。
- `RecordSnapshot(LadderDocument doc)`（22-26行）：現在の`doc`をシリアライズしてUndoスタックへ
  `Push`、**Redoスタックを`Clear()`**（コメント「操作実行の直前に呼ぶ」）。
- `Undo`/`Redo`（29-42行）：スタックが空ならnull、そうでなければ対称的にPush/Popしてデシリアライズ。
- `Clear()`（47-51行）：両スタックをClear（コメント「文書差し替え(新規/開く)の入口で呼ぶ」）。

### スナップショット記録箇所（プロジェクト全体で2箇所のみ、コードで網羅確認済み）

- `SheetNavigationViewModel.cs:111`：`AddCommand`内、`Sheets.Add(sheet)`の**直前**。
- `SheetNavigationViewModel.cs:151`：`DeleteCommand`内、`Sheets.RemoveAt(index)`の**直前**。

`MainWindowViewModel.cs`側には呼び出しなし（1780行コメント「RecordSnapshotの呼び出しは
SheetNavigationViewModel.AddCommand/DeleteCommand側で行う」）。`RenameCommand`（174-200行）にも
呼び出しなし——**シート改名はUndo対象外**であることがコードで確定。

---

## 3. Undo/Redo実行時の処理

`UndoCommand`/`RedoCommand`（`MainWindowViewModel.cs:1781-1795`）は`UndoManager.Undo/Redo`の
戻り値を`ApplyUndoRedoSnapshot`へ渡すのみ。

### `ApplyUndoRedoSnapshot`（1812-1849行）——新規/開く用の`ReplaceDocument`とは別メソッド

| 項目 | 挙動 |
|---|---|
| `Document` | 復元されたドキュメントへ差替 |
| `SheetNavigation` | `ResetSheets()`でミラー再同期 |
| `CurrentSheetIndex` | 復元後シート数へクランプ |
| `SelectedCell` | **巻き戻さず現状維持**。ただし復元先シートの`Grid.Rows`超過時は末尾行へクランプ（`ClampSelectedCellToSheetRows`） |
| `Tool`状態 | **維持**（`ReplaceDocument`は`ToolState.SelectDefault`へ明示リセットするが、`ApplyUndoRedoSnapshot`にはこの代入がない） |
| `StatusMessage` | **維持**（同様にクリアされない） |
| `IsDirty` | **無条件で`MarkDirty()`**（殿裁定：「戻した内容が未保存という事実は変わらないため」） |
| `OutputPanel` | `ClearResults()`（旧文書に紐づくDRC結果を破棄） |

コメント（1808-1811行）：「新規/開く専用の`ReplaceDocument`とは意味論が異なる（`SelectedCell`/
`Tool`状態/`StatusMessage`は巻き戻さず現状維持、殿裁定2026-07-11＝シート構成のみ復元）」。

**Undo/Redoは「ドキュメント全体を過去のスナップショットへ戻す」のではなく「シート構成（Sheets）
だけを過去へ戻し、UI操作状態（選択セル・ツール・メッセージ）は現在のまま維持する」という
限定的な意味論**である点に注意。

---

## 4. `IsEnabled`連動

**（2026-07-21更新、T-061・T-092反映）** `UndoCommand`/`RedoCommand`のCanExecuteは
`CanEditDiagram && !HasAnyDraft && UndoManager.CanUndo`（Redoは`CanRedo`）の3条件
（`MainWindowViewModel.cs:3185-3199`）で判定される：
- `CanEditDiagram`（`HasProject && Mode==AppMode.Drawing`）：**テストモード中はUndo/Redoが
  無効化される**（T-061、2026-07-14、「テストモード＝観察専用」の一貫性のため）。
- `!HasAnyDraft`：**縦コネクタ/自由線/画像挿入のドラフト記入中はUndo/Redoが無効化される**
  （T-092、2026-07-15。巻き戻り先でドラフトが指す行・シートの前提が崩れることを防ぐブロック方式、
  `AddRowCommand`/`DeleteRowCommand`も同型のガードが追加されている）。
- `UndoManager.CanUndo`/`CanRedo`：スタックの件数判定（2節参照）。

**旧版本節はこれら3条件のうち`UndoManager.CanUndo`のみを前提としており、`CanEditDiagram`・
`!HasAnyDraft`の2条件が欠落していた。**

- メニュー・ツールバーとも`Command="{Binding UndoCommand}"`のみをバインド、**明示的な`IsEnabled`
  バインディングは存在しない**（`SaveButton`の`IsEnabled="{Binding HasProject}"`のような個別
  バインディングはない）。WPFの`Button`/`MenuItem`が`Command`バインディング時に`CanExecute`の
  結果を自動反映する標準機構による。
- `RelayCommand`の`CanExecuteChanged`は`CommandManager.RequerySuggested`にadd/remove委譲する
  実装で、**`UndoManager`側の状態変化時に個別に`RaiseCanExecuteChanged()`を呼んでいる箇所は
  存在しない**。再評価タイミングはWPFの`CommandManager`が自動トリガする既定のUIイベント
  （フォーカス変更・キー入力・マウス操作等）依存であり、即時通知ではない。
- キーボードショートカット（Ctrl+Z/Ctrl+Y、`MainWindow.xaml.cs:948-961`）は`CanExecute`チェックを
  経由せず直接`Execute(null)`を呼ぶ（内部で`Undo`/`Redo`がnullを返す早期returnのため実害なし）。
  `CommitDeviceNameEdit()`を実行前に呼ぶ（T-051バグ修正#3）。

---

## 5. 履歴クリア

`UndoManager.Clear()`の呼び出しはプロジェクト全体で**1箇所のみ**：`MainWindowViewModel.cs:1663`、
`ReplaceDocument`メソッド内。`ReplaceDocument`は`NewDocument()`（新規作成）と`LoadFromFile`
（ファイル読込）の共通ゲートウェイのため、**新規作成・読込どちらでも履歴がクリアされる**。

理由（コメント1660-1663行、T-051バグ修正#1）：「無関係な旧文書のUndo/Redo履歴を持ち越すと、
別ファイルへの切替後にUndoで旧文書の状態が復元され、それを保存すると新ファイルパスへ誤って
上書きされるデータ破損事故になる」。`Clear()`直後に`IsDirty = false`も設定。

---

## 6. 将来の対象範囲拡大計画（未着手）

明確な着手決定はないが、設計調査時点で以下が「基盤を先に立ち上げてから対象を広げる二段階目」として
位置づけられている（`docs/archive/ecad2-t051-undo-redo-design-survey-onmitsu.md:129-146`）：

- 候補2：要素配置/削除
- 候補3：行挿入/削除（T-055直後ゆえ仕様確定待ちが妥当、との記載）

P-055（デバイス名編集確定直後のUndoで内容が見た目上消える現象、`docs/proposed.md:64`）は
「Undo対象範囲の拡張（候補2以降）時に自然解消する見込み」としてpending扱い。

---

## 7. 既知の罠・関連する提案記録

- **P-032**（`docs/proposed.md:46`）：シート追加削除がUndo対象外との観測→誤認と判明、T-051起票の
  直接契機（1節参照）。
- **P-042**（`docs/proposed.md:52`）：境界外ドラッグ時のUndo不整合の両論併記→T-051前提検証で
  「通常操作でのUndo可能」自体が誤認と確定、論点はドラッグ挙動のみ残存（結線操作領域の話、
  `docs/ecad2-spec-wiring.md`2節参照）。
- **P-055**（`docs/proposed.md:64`）：デバイス名編集確定直後のUndoで内容が見た目上消える
  （恒久喪失ではない）。pending、候補2以降で自然解消見込み。

---

## 8. 本日T-062検証での確認（実機記録）

`docs-notes/ecad2-t062-main-operations-regression-ninja.md:143-168`：試行1で要素配置直後に
「元に戻す」ボタンが`IsEnabled=False`のままだったことを検知→コード確認で「MVP対象範囲は
`SheetNavigationViewModel`のシート追加/削除のみ」と判明→**退行ではなく既存仕様どおりと結論**。
以降シート追加/削除のUndo/Redoのみ検証し全OK。

## 忍者実機検証（8観点全OK）

`docs-notes/ecad2-t051-realmachine-verification-ninja.md`：(a)シート追加→Ctrl+Z→Ctrl+Yの往復＝
正常 (b)IsEnabled連動＝正確 (c)新規作成/別文書オープン後の履歴クリア＝旧文書化けなし
(d)左パレット選択ハイライト＝崩れなし (e)DeviceNameBox未確定編集中Ctrl+Z＝編集確定後にUndo実行
(f)DRC残留なし (g)SelectedCell維持+クランプ＝正常 (h)デバイス名編集確定直後Undoで見た目上消える
現象を再現（P-055として記録のみ）。

## 不明点

- コード内コメント（`MainWindowViewModel.cs:93-99`）に「変更操作の入口(要素配置/削除/デバイス名
  変更、シート追加/削除/改名)で明示的にMarkDirty()を呼ぶ方式」という記述が残るが、これはT-051
  以前（シート追加/削除がUndo対象化される前）の古い記述と見られ、現状（シート追加/削除は
  RecordSnapshot経由でUndo対象）とは厳密には整合しない。コード動作自体に誤りはないが、**コメントの
  更新漏れ**の可能性がある（気づきとして記録、隠密からの修正提案は`docs/proposed.md`経由）。
