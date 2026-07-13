# T-064 矢印キー画像平行移動 再レビュー(隠密・フル観点)

- 対象コミット: `ba09bed`(侍、隠密静的調査`docs/ecad2-t064-arrow-key-investigation-onmitsu.md`・殿裁定2026-07-13に基づく実装)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: DoD整合確認+`code-review`スキル(フル観点、2並列エージェント)
- スコープ境界: レビューのみ、書き込みなし

## 結論サマリ

境界クランプ整合性(`UpdateDragImage`と完全一致)・呼び出し位置(他のSelected*と同じ優先順位)・横展開完全性(全6種のSelected*状態を全数点検し漏れなし)は確認OK。新規テスト4件も手計算で実装と一致。

**ただし1件、CONFIRMEDレベルの重大な見落としを発見**——`MoveSelectedImage`が殿裁定「画像操作は全てUndo対象」に反し`UndoManager.RecordSnapshot`を呼んでいない。加えて制度化提案2件(チェックリスト拡充・複製コードの共通化検討)。

## DoD確認結果

| 観点 | 結果 |
|---|---|
| 境界クランプ整合性(UpdateDragImageとの一致) | 確認OK。`Math.Clamp(base+delta, 0, Math.Max(0, maxMm-Width/HeightMm))`という式が`UpdateDragImage`と完全一致。呼び出し元の`maxXMm`/`maxYMm`算出方式も`MoveSelectedFreeLineByKey`/`MoveSelectedConnectionDotByKey`と同一。 |
| 他のMoveSelected*との対称性・呼び出し位置 | 確認OK。矢印キーswitch文内で`SelectedConnectionDot`分岐の直後・`MoveSelectedCell`フォールスルーの直前という正しい優先順位に挿入。方向規約(Up=-step等)も一致。 |
| RED証明報告とテスト内容の整合 | 新規4件は手計算で実装出力と一致することを確認(25/27・境界クランプ20・falseガード等)。ただし下記「軽微な指摘」参照(RED証明の実施範囲が申告と一部乖離)。 |

## 新規発見1(CONFIRMED・最重要): `MoveSelectedImage`が殿裁定「画像操作は全てUndo対象」に違反

**該当**: `MainWindowViewModel.cs:1253`(`MoveSelectedImage`新設メソッド)

同ファイル内の画像操作系メソッドは全て「殿裁定(画像操作は全てUndo対象、他要素との非対称は許容)」という同一文言のコメント付きで明示的に`UndoManager.RecordSnapshot(Document)`を呼んでいる: `SelectedImageIsTracingOnly`(1089行目)・`DeleteSelectedImage`(1103行目)・`ConfirmImageInsertDraft`(1153行目)・`ConfirmDragImage`(1233行目、「殿裁定により画像操作はUndo対象のため」と明記)・`ConfirmResizeImage`(1372行目)。

**原因**: コミットメッセージは「他の選択可能状態(SelectedConnector/WireBreak/FreeLine/ConnectionDot)と同じMoveSelected*パターンで新設」と説明しているが、参照元の`MoveSelectedConnectionDot`/`MoveSelectedFreeLine`は元々「Undo機能無し」が仕様(点系・線系はUndo対象外、既存設計)。つまり「他のSelected*と同じパターンを踏襲する」という対称性の意識が、皮肉にも「画像だけはUndo対象という非対称な原則を持つ」という、まさに殿裁定が明示した「他要素との非対称は許容」という例外を踏み外す結果を招いた。

**失敗シナリオ**: 画像を挿入(Undoスナップショット記録)→矢印キーで数回移動(記録なし)→Ctrl+Zを押すと、直前の移動1回分だけが戻るのではなく、挿入前の状態まで一気に巻き戻る(ナッジ操作と挿入操作が一体の1手として扱われる)。ユーザーの「1手だけ戻したい」という意図と乖離する。

**テストでの裏付け**: 既存の確定系テスト5箇所(ドラッグ確定等)は`Assert.True(vm.UndoCommand.CanExecute(null))`等でUndo対応を明示的に検証しているが、今回追加した4件のテストにはこの検証が一切無い(検証していれば失敗していたはずの観点)。

**結論**: CONFIRMED。往復修正での対応を推奨(`UndoManager.RecordSnapshot(Document)`を値変更前に追加するだけの軽微な修正と見込む)。

## 新規発見2(制度化提案): 既存チェックリスト項目3では今回のパターンを捕捉できない

**該当**: `docs-notes/roles/samurai.md`「新規選択可能状態の横展開チェックリスト」項目3

項目3「矢印キー等、記入中ドラフト中の入力に対する分岐」は`ToolMode.PlaceConnector/PlaceLine/PlaceImage`の**未確定placement中**の矢印キー処理を指す。今回発覚した漏れは「確定済み・選択済み(Selected*)状態」に対する矢印キー平行移動であり、コード上も対象領域・タイミングが異なる(記入中 vs 選択中)。項目3の文言だけでは読者の意識が「記入中ドラフト」に向き、「選択済み状態のMoveSelectedXxxByKey分岐漏れ」は見落とされうる。

**推奨**: チェックリストへ7項目目「矢印キーによる選択状態自体の平行移動(MoveSelectedXxxByKey): 新状態が独立した位置(mm座標または行/列)を持つ場合、同switch内のSelected*連鎖の末尾に分岐を追加したか」を追加検討。項目3とは対象コードが異なる旨を明記すること。制度化要否は家老判断。

## 新規発見3(Altitude、蓄積債務): View側で4箇所目の複製、PR-07(rule of three)未実施

**該当**: `MainWindow.xaml.cs`の`MoveSelectedFreeLineByKey`(1600行目)・`ResizeSelectedFreeLineByKey`(1621行目)・`MoveSelectedConnectionDotByKey`(1641行目)・新設`MoveSelectedImageByKey`(1662行目)

4メソッドとも「CurrentSheetのnull検査→step/maxXMm/maxYMm算出→4方向switchでVM側メソッド呼び出し→RedrawCanvas()」という骨格がVM側メソッド名以外ほぼ一字一句同一。パターン再発台帳PR-07(コピペ重複、3箇所目到達で共通化検討)に照らせば3箇所目(ConnectionDot、本コミット以前から存在)の時点で検討されるべきだったが、その手続きが踏まれた形跡は無く、本コミットは4箇所目をそのまま複製した形。本コミット単体の欠陥ではなく蓄積債務。台帳PR-07への再発履歴追記を推奨。

## 確認して問題なしと判定した点

- **横展開完全性(全数点検)**: `MainWindowViewModel.cs`の`public.*Selected\w+`パターンを全数確認。独立した位置状態を持つSelected*は6種(Cell/Connector/WireBreak/FreeLine/ConnectionDot/Image)で全て矢印キー横展開済み。`SelectedElement`は`SelectedCell`から都度算出する派生プロパティのため対象外(除外漏れではない)。
- **VM側の共通化(Reuse/Simplification)**: `MoveSelectedImage`/`MoveSelectedConnectionDot`は骨格が似るが、上限値の式が異なる(点=maxXMmそのまま、矩形=maxXMm-Width)。現状2箇所のみで「3箇所目で検討」規約(PR-07)にはまだ達しておらず、共通化は時期尚早と判断。

## 軽微な指摘(参考)

RED証明の実施範囲: コミットメッセージ申告の「変化なしならfalseガードを一時的に外し実測」は、新規テスト4件中`MoveSelectedImage_AlreadyAtBoundary_DoesNothing`1件のみが対象で、残り3件(移動量・クランプ値の算術そのもの)にはfault-injectionによるバグ検出力の実証が無い。新規メソッドのため通常の「旧実装比較」ができない事情は理解できるが、「RED証明済み」という申告が実質1/4のみをカバーする点は、記述と実施内容の間に精度の差がある。実害は無し(隠密の手計算で実装と期待値の一致は別途確認済み)。

## 派生提案の有無

あり(新規発見1=CONFIRMED重大、新規発見2・3=制度化提案)。自らは着手せず家老へ報告のみ。
