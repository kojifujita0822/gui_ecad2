# T-055 増分3（任意位置行挿入・削除） 実機確認記録（忍者）

最終更新: 2026-07-10（忍者記す）

対象コミット: `e9d062a`（main、push済み）。侍実装（`33a59f9`初回＋`e9d062a`往復1周目修正）、
隠密静的レビュー2周ともクリーン確定後の実機確認。

## 検証方法

`ecad2-ui-automation`スキルで実機起動。GroupFrame/RungCommentはUI上に配置コマンドが
未実装（`RowInsertDeleteCommandsTests.cs`のテストヘルパーでのみモデル直接追加されている
と判明）のため、5種要素（ElementInstance/VerticalConnector/WireBreak/GroupFrame/
RungComment）を含む`.gcad`ファイルをJSON手書きで作成し「開く」機能でロードして検証した
（配置UIの検証ではなく、既存データに対する挿入・削除・シフト処理の検証が目的のため、この
代替手段で計画書DoD「型ごとにテストで実測確認済みの裏付けを実機の目視で取る」の主旨を
満たすと判断）。挿入・削除操作後は「上書き保存」で書き戻し、保存JSONの実測値でも二重に
検証した（UI目視＋データ実測のWチェック）。

## 観点別結果

### 1. 右クリックコンテキストメニューの表示・動作 — OK

- 文言は計画書どおり「行{N}の前に行を挿入」「末尾に行を追加」「行{N}を削除」を確認
- 要素なし行: 3項目ともEnabled
- 要素あり行（ElementInstance在り）: 削除項目はEnabledのまま（CanExecuteでは弾かない設計、
  テストコードの設計意図と一致）だが、実行すると「行2に要素があるため削除できません」の
  拒否メッセージが正しく表示され、Grid.Rowsが変化しないことを確認

### 2. 任意位置への行挿入（先頭・中間・末尾）で5種要素シフト — OK

- 先頭挿入（targetRow=0相当）: ElementInstance(row 1→2)/VerticalConnector(3-4→4-5)/
  WireBreak(5→6)/GroupFrame(topLeft.row 7→8、height 3不変=位置のみシフト)/
  RungComment(11→12)、全て保存JSONの実測値で+1シフトを確認
- 中間挿入（GroupFrame内部、targetRow=9相当）: GroupFrame.Height 3→4（内部挿入で伸長、
  topLeft.row=8不変）、ElementInstance/VerticalConnector/WireBreak（挿入点より前）は不変、
  RungComment（挿入点より後）は+1、を保存JSONの実測値で確認
- 末尾追加: Grid.Rows +1を確認（簡易）

### 3. 任意位置での行削除（対象行に要素なしケース） — OK

- 要素なし行の削除でGrid.Rows -1、削除点より後ろの要素が-1シフトすることを保存JSONの
  実測値で確認（GroupFrame: topLeft.row 8→7、height 4不変=「枠が削除行より完全に下」の
  位置のみシフトケースを確認）

### 4. SelectedCell追随 — OK

- 挿入（targetRow=3相当）: SelectedCell(row10→11)、+1シフトを確認
- 削除（targetRow=3相当）: SelectedCell(row11→10)、-1シフトを確認
- いずれもステータスバー表示（「選択セル: 行N/列M」）で実測

### 5. GroupFrame内部挿入・削除でHeight伸縮 — 挿入は実測OK、削除側は実機到達不能（下記6参照）

### 6. 境界値クランプ（下限1・上限60） — OK

- 上限60: 「行1の前に行を挿入」「末尾に行を追加」ともCanExecute=falseで自動グレーアウト、
  「行1を削除」はEnabled（削除は許可）
- 下限1: 「行1を削除」がCanExecute=falseで自動グレーアウト、挿入系はEnabled

## 範囲外検出（家老・侍・殿の確認要）

**GroupFrame内部からの削除（Height--による内部詰め）ロジックが実機で到達不能と判明した。**

計画書（`docs/archive/ecad2-t055-implementation-plan-samurai.md`増分3節）には「削除対象行が枠の
内部（`TopLeft.Row < targetRow`かつ`TopLeft.Row + Height - 1 >= targetRow`）→ `Height--`
（内部詰め、位置不変）」という分岐ロジックが明記されている。しかし実機でGroupFrame内部の
行（開始行そのものではない、範囲内の中間行）を削除しようとすると、`IsRowOccupied`判定
（`MainWindowViewModel.cs:1306`、`sheet.Frames.Any(f => row >= f.TopLeft.Row && row <
f.TopLeft.Row + f.Height)`）によりGroupFrame範囲全体が「占有」とみなされ、削除操作自体が
「行10に要素があるため削除できません」で拒否される。

つまり`RowOps.DeleteRow`（Core層）側にHeight--ロジックが実装され単体テストで動作確認されて
いたとしても、コマンド層（`DeleteRowAtCommand`）の占有チェックが先に働くため、実際のUI
操作からはこの分岐に到達できない。設計意図（そもそも到達しない想定の保険的ロジックか、
将来的に到達させる想定だが現状のガードと矛盾しているか）は不明なため、忍者からは判断・
修正せず「範囲外として検出」の報告に留める。

## 回帰の有無

増分1・2の機能（末尾行±ツールバー・シート設定ダイアログ）には今回直接触れていないため
未確認。ただし検証過程でシート追加ダイアログ（増分2以前からの既存機能）が正常に開閉できる
ことは確認できた（副次的な動作確認）。

## 検証中に発見した技術知見（スキル改修に反映済み）

- `ecad2-ui-automation`スキルへ右クリック機能（`Invoke-Ecad2CanvasRightClick`）を新設した
  （ContextMenuはPreviewMouseRightButtonDownハンドラで開くため、UI Automationパターンでは
  代替できず座標右クリックが本質的に必要）
- ダイアログ・ポップアップ（OpenFileDialog、コードビハインド生成のContextMenu等）が
  `AutomationElement.RootElement.FindAll(Children, Window条件)`では検出できないことがある
  と判明（Win32 `EnumWindows`で直接列挙すれば確実）。検証序盤でこれに気づかず「開く」
  ダイアログ・「シート追加」ダイアログが計3件見えないまま滞留する事態が発生したが、
  EnumWindows経由で発見しEscで正常クローズ、実害なし
- ContextMenu表示中は`Process.MainWindowHandle`がメニュー自身のハンドルを指すことがある
  と判明（`Get-Ecad2WindowRect`/`Save-Ecad2Screenshot`が誤動作しうる）
- 上記2点はSKILL.mdの「6. トラブルシュート」節へ追記済み（コミットは家老の裁量）
