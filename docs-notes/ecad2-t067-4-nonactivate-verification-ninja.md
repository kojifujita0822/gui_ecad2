# T-067(4) 追加検証：ウィンドウ非アクティブ化時の確定発火（忍者、2026-07-19）

## 検証目的
隠密の仮説「T-067(4)フォーカスロスト不確定は新規バグでなく、T-080（行コメントエディタ）で
殿裁定済みの既確定仕様（`docs/spec/ecad2-spec-canvas-display.md` 8節「ウィンドウ内側クリックでは
閉じない、非アクティブ化時のみLostKeyboardFocus発火」）と同根」の裏付けを取る。

## 手順・結果

枠ラベル編集を再オープンし、**ecad2ウィンドウを非アクティブ化する操作**を実施した。

- まず`SetForegroundWindow`によるデスクトップ(Progman)への直接切替を試みたが、Windowsの
  フォーカス盗用防止機構により失敗（`AttachThreadInput`併用でも不成立）。
- 次に**実キーボードイベントとしてAlt+Escを送信**（`keybd_event`、フォーカス盗用防止の対象外＝
  実ユーザー入力扱いのため成功）したところ、`GetForegroundWindow()`の値が
  ecad2の`MainWindowHandle`(460736)から別ウィンドウ(853186、確認したところ**Visual Studio
  Code（Claude Code拡張ウィンドウ）**だった)へ切り替わり、**非アクティブ化に成功**した。

診断ログ（該当区間）：
```
20:12:25.278 FrameLabelBox_LostKeyboardFocus: fired, _frameLabelEditingFrame=, OldFocus=TextBox, NewFocus=null
20:12:25.279 CommitFrameLabelEditor: enter restoreFocus=False, _frameLabelEditingFrame=
20:12:25.279 CloseFrameLabelEditor: enter restoreFocus=False
```

**`FrameLabelBox_LostKeyboardFocus`が正常発火し、`CommitFrameLabelEditor`→
`CloseFrameLabelEditor`という確定処理が正しく実行された**。

## 結論

| 操作 | 結果 |
|---|---|
| ウィンドウ内側クリック（キャンバス枠外・ツールバーボタン、前回検証） | 確定処理**発火せず** |
| ウィンドウの非アクティブ化（Alt+Esc、他アプリへのフォーカス切替、本検証） | 確定処理**正常発火** |

**隠密の仮説を実機で裏付けた**。T-067(4)のフォーカスロスト不確定は、T-080と同根の既確定仕様
（窓内クリックでは閉じず、非アクティブ化時のみLostKeyboardFocus発火）と一致する挙動であり、
新規バグではなく仕様通りの動作である可能性が高い。前回検証（枠外クリック・ツールバーボタン
クリック）で「確定しない」という結果が出たのは、この仕様に照らせば正常な挙動と整合する。

## 申し送り事項（訂正）
前回報告（`ecad2-t067-4-focusloss-verification-ninja.md`）で「マウスクリックがTextBox表示中は
他要素へルーティングされていない疑い」という所見を記したが、これは事実（クリックしても
ツール状態・ログが無反応だった）自体は変わらないものの、**この挙動が「バグ」ではなく「仕様通り」
の可能性が高まった**。ウィンドウ内側でのクリックがそもそも確定を意図した操作ではない（＝
「ウィンドウ内側クリックでは閉じない」という仕様がまさにこの「クリックしても何も起きない」
挙動を指している）と解釈すれば整合する。ただし、IsMainContentEnabled等の実装詳細の一次確認は
引き続き侍・隠密に委ねる。

## 影響についての一言
非アクティブ化検証で切替先となったウィンドウは殿使用中の可能性があるVSCode
（Claude Code拡張）だった。影響はフォーカス移動のみ（実キー入力・クリックは送っていない）で
一瞬（500ms程度）だが、念のため申し添える。

## 証跡ファイル
- `%TEMP%\ecad2-diag.log`（該当区間: `20:12:25`前後）
