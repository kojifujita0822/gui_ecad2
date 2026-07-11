# T-051 Undo/Redo基盤 コードレビュー（隠密）

対象: コミット`0693755`（Undo/Redo基盤・案C、MVP対象範囲=シート追加/削除のみ）。設計書=
`docs/archive/ecad2-t051-implementation-plan-samurai.md`。家老指定5観点の手動確認＋`code-review`
スキル（high、2角度→5候補→1-vote verify）を併用。

**結論を先に：4件全てCONFIRMED。うち2件（#1・#3）は明確なデータ破損リスクを伴う重大バグ。
忍者実機確認より先に修正が必須。**

---

## 1. 家老指定5観点（手動確認）

| 観点 | 判定 | 根拠 |
|---|---|---|
| (a) 計画書1.1〜1.4節との突合 | **OK** | `UndoManager`構造・`RecordSnapshot`呼び出し位置・`ApplyUndoRedoSnapshot`設計案・UIバインドとも計画書どおり実装 |
| (b) IsDirtyテスト省略判断の正当性 | **条件付きOK（軽微な代替案あり）** | `SaveToFile`(一時ファイル使用)経由でIsDirtyをfalseへリセットしてからUndoの効果を検証する代替テストは技術的に可能だった。ただし機能自体は正しく実装されており（`ApplyUndoRedoSnapshot`内`MarkDirty()`確認済み）、テストカバレッジの向上余地に留まる軽微な指摘 |
| (c) UndoManagerのスタック整合 | **OK** | `UndoManagerTests.cs`で往復・Redoクリア・履歴0件時null等を網羅 |
| (d) ApplyUndoRedoSnapshotの復元範囲・SetProperty早期returnトラップ | **範囲自体はOK、ただし通知漏れ2件を検出（#2・#4）** | `SelectedCell`/`Tool`/`StatusMessage`への直接的な巻き込みは無い。`Document`は自動実装プロパティで早期returnトラップ非該当。ただし`SelectedSheet`・`OutputPanel`への通知漏れが別途判明（下記） |
| (e) Ctrl+Z/Yの既存ショートカット衝突 | **キー割当の衝突自体はOK、ただし別種の重大バグを検出（#3）** | `Window_PreviewKeyDown`内に他のKey.Z/Key.Y使用箇所なし。ただしDeviceNameBox編集中の相互作用に問題あり |

---

## 2. `code-review`スキル（high、2角度→5候補→verify）

### 2-1. 要修正（CONFIRMED、正しさバグ、最重要）

**#1. 別ファイルを開いてUndoすると、旧ファイルの内容が現在のファイルパスへ誤って上書き保存されうる**

`MainWindowViewModel.cs`（`ReplaceDocument`1584行付近、`UndoManager`1403行付近）。`ReplaceDocument`
（新規作成/開く共通経路）は`UndoManager`のスタックを一切クリアしない。`UndoManager`自体にも
`Clear`等のリセット手段が存在しない。

再現手順：文書A（1シート）で「シート追加」実行（Undoスタックに旧A状態が積まれる）→Ctrl+Oで無関係の
文書Bを開く（`ReplaceDocument`は`UndoManager`に触れないため、旧Aのスナップショットが残存。
`CurrentFilePath`はBのパスに更新）→Ctrl+Zを押すと、`UndoManager.Undo`が残存していた旧Aの
スナップショットをpopして`Document`へ復元（`ApplyUndoRedoSnapshot`は`CurrentFilePath`に触れない）
→この状態でCtrl+S（上書き保存）すると、**Aの内容がBのファイルパスへ書き込まれ、Bのデータが
消失・破損する**。

**#3. DeviceNameBox編集中にCtrl+Z/Yを押すと、未確定入力が消失または無関係な要素へ誤書き込みされる**

`MainWindow.xaml.cs`（`Window_PreviewKeyDown`のKey.Z/Key.Yケース、924-931行）。既存のCtrl+S/O/N
（T-049/P-013で確立済み）は入口で`CommitDeviceNameEdit()`を呼び、デバイス名編集中のフォーカス保持を
保存前に確定させるガードを持つが、今回追加されたCtrl+Z/Yのケースにはこのガード呼び出しが無い。

再現手順：要素を選択しDeviceNameBox（`UpdateSourceTrigger=Explicit`）に文字入力（未確定）、直前に
シート追加のUndo履歴がある状態でCtrl+Zを押す→`UndoCommand`が同期的に実行され`ApplyUndoRedoSnapshot`
が`Document`を丸ごと差し替えるが、`SelectedElementDeviceName`等のPropertyChangedは一切発火しない
（`Document`/`CurrentSheet`系/`HasProject`のみ通知）ため、DeviceNameBoxの表示（未確定入力）は
その裏でDocumentが差し替わったまま残る→後にフォーカスが外れ`CommitDeviceNameEdit()`が呼ばれると、
`SelectedElementDeviceName`セッターは**差し替え後のDocument**に対して`SelectedElement`を再評価する。
同じ座標に要素が無くなっていれば未確定編集が無言で破棄、別の要素があれば**その要素へ誤って書き込まれる**
（T-055増分3の指摘a・bと同型のデータ破損パターン）。

### 2-2. 要修正（CONFIRMED、表示不整合・機能退行）

**#2. Undo/Redo実行の度に、左パレットのシート選択ハイライトが崩れる**

`MainWindowViewModel.cs`（`ApplyUndoRedoSnapshot`1787-1801行）。`SheetNavigation.ResetSheets()`は
内部で`Sheets.Clear()`→`foreach Add`という実装のため、`ObservableCollection`の`Reset`通知が発火する。
WPFの`Selector`（`ListBox`の基底）は`Reset`通知を選択状態の復元不能と見なし`SelectedItem`をクリアする
既知の挙動（`RenameCommand`が`Clear`でなく`RemoveAt+Insert`を使っている設計上の理由もこれ）。
`ApplyUndoRedoSnapshot`は`SetCurrentSheetIndexCore`を呼ぶのみで、`AddCommand`/`DeleteCommand`/
`RenameCommand`等の既存コマンド群が律儀に発火している`SelectedSheet`変更通知（T-050で確立済みの
不変条件）を一切発火させない。**verify検証の結果、当初の想定より広範——CurrentSheetIndexの数値が
変化するか否かに関わらず、Undo/Redoのあらゆる実行で発生する。**

**#4. DRC実行後にシートをUndo/Redoすると、出力パネルに存在しないシートを指す診断が残留し「沈黙」不整合が再発する**

同ファイル`ApplyUndoRedoSnapshot`。既存`ReplaceDocument`は`OutputPanel.ClearResults()`を呼び
「新規/開くでDocument差し替え時、旧文書の診断結果が残留し誤ジャンプ・沈黙する」というT-019の教訓
（コメントに明記）に対応済みだが、`ApplyUndoRedoSnapshot`にはこの呼び出しが無い。DRC実行→シート追加
→そのシートに要素配置→DRC実行→Undo（シート削除）という操作列で、存在しないページ番号を指す診断が
出力パネルに残留し、クリックしても`JumpTo`が`FindIndex`失敗で無言return（T-019がまさに防ごうとした
「沈黙」不整合そのものの再発）。

### 2-3. 経過観察（PLAUSIBLE、設計所見）

**#5. `UndoManager`の全体JSONスナップショット方式は、将来Undo対象を高頻度操作へ拡張する際に再設計が必要**

`src/Ecad2.App/Commands/UndoManager.cs`。現状MVP（シート追加/削除、低頻度操作）では許容範囲だが、
差分ベースでない全体スナップショット方式のため、対象拡大時はスケーラビリティの再検討が要る
（設計書でも既知のトレードオフとして記載済み、新規の懸念ではない）。

---

## 3. IsDirtyテスト省略判断について（観点b、補足）

侍の判断理由（「IsDirtyのsetterがprivateで直接リセットできず、直前のAddCommand自体が既にMarkDirty
済みでUndo単独の効果をRED証明できない」）は機能実装上は妥当だが、**`SaveToFile(path)`（一時ファイル
使用）を経由すれば`IsDirty=false`へリセットしてからUndoの効果を検証する代替テストは技術的に可能**
だった。ただし、これはテストカバレッジの完全性に関する軽微な指摘であり、`ApplyUndoRedoSnapshot`内の
`MarkDirty()`呼び出し自体はコードレビューで確認済み・正しく実装されているため、追加を強制するほどの
重要度ではないと判断する（家老・侍の裁量に委ねる）。

---

## 4. 結論

**#1・#3はデータ破損に直結する重大バグのため、忍者実機確認より先に修正が必須。#2・#4は表示不整合・
機能退行として要修正。#5は経過観察のみ。** T-055増分3の指摘a・bと同型のパターン（PropertyChanged
通知漏れによるデータ破損）が本タスクでも複数箇所で再発しており、**Document/Sheet構成を差し替える
処理（ApplyUndoRedoSnapshot等）を新設する際は、ReplaceDocumentが担っている全ての「文書差し替え時の
状態リセット責務」（UndoManagerクリア・OutputPanelクリア・SelectedSheet通知）を横展開でチェックリスト
化することを推奨する**（今回はUndoManager自身が「文書差し替え」の当事者であるにも関わらず自分自身を
クリアしていない、という盲点も含む）。
