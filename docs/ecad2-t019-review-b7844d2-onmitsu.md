# T-019合流分(コミットb7844d2)静的レビュー（隠密）

対象: 起動濃紺スタート(CreateDummyDocument廃止)・濃紺色調整(#1E2A47→#24325A)・
Window_Closing未保存確認・RenameCommand同値ガード。`code-review`スキル(medium、
8観点finder→1-vote verify)を併用。

## 家老指定4観点への回答

| # | 観点 | 判定 |
|---|------|------|
| 1 | Closing経路が新規/開くと同一挙動か | 良好。`ConfirmDiscardIfDirty()`を完全流用、キャンセル/保存中止時は`e.Cancel=true`で正しく中止。文言・3択・戻り値の意味とも一致。 |
| 2 | 同値ガードの対称性 | 良好。`SelectedElementDeviceName`セッターの`oldName==newName`ガードと表現パターンが一貫している。 |
| 3 | 起動濃紺化のダミー生成取り残し・null防御 | ダミー生成メソッドの残留参照なし。既存のnull防御(MoveSelectedCell等)は機能するが、**別の重大な回帰を発見**（下記#1）。 |
| 4 | 色変更の適用範囲漏れ | 漏れなし。`#1E2A47`はApp.xamlの1箇所のみで完結。 |

## code-reviewスキル所見（verify後、要修正級2件を発見）

| # | 判定 | 内容 |
|---|------|------|
| 1 | **CONFIRMED（最重要・回帰）** | 起動直後(Sheets=0)から左パネル「追加」ボタンで最初のシートを追加すると、**キャンバスが再描画されない**。`SheetNavigationViewModel.SelectedSheet`セッターが`_owner.CurrentSheetIndex = index`(index=0)を呼ぶが、`_currentSheetIndex`の既定値も既に0のため`SetProperty`が「変化なし」と判定し`OnPropertyChanged(nameof(CurrentSheet))`が発火しない。`MainWindow.ViewModel_PropertyChanged`はCurrentSheet/SelectedCellの変更時のみ`RedrawCanvas()`を呼ぶため、`HasProject`通知(濃紺→白の背景切替)だけが発火してキャンバスは`Clear()`直後(Width=0/Height=0)のまま——**背景は白くなるが、グリッド・要素が一切描画されない空白画面**になる。ダミー3シートで起動していた旧実装ではCurrentSheetIndexが必ず変化する(0→1以降)経路のため到達不能だったが、b7844d2の「起動時Sheets=0化」で新たに到達可能になった。 |
| 2 | **CONFIRMED（見落とし）** | 起動直後(Sheets=0)でも保存操作(Ctrl+S・上書き保存ボタン・メニュー)にIsEnabled/CanExecuteガードが無く常時実行可能。Sheets=0のJSONがそのまま`GcadSerializer.Save`で正常に書き出される(クラッシュはしない)。禁止する設計記述はdocs/コードいずれにも見当たらず、見落としと判断。 |
| 3 | 軽微(UX) | `RenameSheetButton`はCommandではなくClickイベント直結のためCanExecute連動が無く、起動直後(SelectedSheet=null)でも押下可能に見えるが押しても無反応(コードビハインド側でガード済み、クラッシュなし)。 |
| 4 | 設計メモ | `CurrentSheetIndex`の既定値0が「シート無し」を表す番兵値になっていない(-1等ではない)。候補1の根本原因はこの「0 vs 空」の意味論的重複に起因する。 |
| 5 | 低優先度(将来リスク) | `LadderCanvasHost_PreviewMouseLeftButtonUp`が`CurrentSheet`のnullチェックをしていないが、現状`Clear()`後はWidth/Height=0でヒットテスト自体が発生せず実害なし。将来キャンバスの空状態表示方法が変わった場合のみ顕在化しうる。 |
| 6 | 低確度 | システムシャットダウン/ログオフ時、`Window_Closing`内のMessageBox/SaveFileDialogがOSのメッセージポンプをブロックし、応答遅延と判断され強制終了→保護が効かない可能性(実測未検証)。 |

## 隠密所見

**要修正候補として家老の判断を仰ぐべきは#1・#2の2件、いずれも忍者の一括実機検証前に対応すべき。**
特に#1は「シート追加という最も基本的な操作の直後に画面が壊れて見える」という、起動直後の一等地で
発生する重大な回帰であり最優先。#2は見落としレベルだが、対応方針(禁止するか許容するか)は
UI/UX判断の要素もあるため家老・殿の判断を仰ぐのが妥当。#3〜#6は経過観察〜設計メモ相当。
