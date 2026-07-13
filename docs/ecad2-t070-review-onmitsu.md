# T-070(fab6d51) 静的レビュー(隠密、2026-07-14)

対象: コミットfab6d51(検索・置換機能のUI結線、5ファイル変更441行)。手動観点確認+`code-review`スキル
(--effort high相当、7角度finder→12件検証、うち9件CONFIRMED)+sweep追加3件(未verify、静的読解で高確度)。

## 結論

ビルド・テスト全合格(Core92・App549→555、家老報告)だが、**要修正9件(全てCONFIRMED)**。うち1件は
T-061「テストモード=観察専用」の核心原則を破る重大度の高い指摘。sweep追加3件も静的読解で確度が高く
併記する。

---

## A群: 最重要(correctness、code-reviewスキルCONFIRMED)

### A-1(最重要・DoD抵触): ReplaceOne/ReplaceAllCommandにCanEditDiagramガードが無い

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs:33-34`
- `ReplaceOneCommand`/`ReplaceAllCommand`のCanExecuteは`CurrentMatch is not null && ReplaceWith.Trim().Length > 0`
  等の業務条件のみで、`_owner.CanEditDiagram`(テストモード中の編集禁止統一ゲート、T-061確立)への参照が無い。
  XAML側(`MainWindow.xaml`)にも`IsEnabled="{Binding CanEditDiagram}"`の追加が無く、二重に漏れている。
- **failure_scenario**: テストモード中(Mode==Test、CanEditDiagram=false)でもCtrl+Fで検索バーは開け(検索自体は
  観察の範疇として妥当)、一致する機器名を検索して置換後欄に新名を入力すると「置換」「全置換」ボタンが
  IsEnabled=trueのまま実行でき、Documentの機器名が書き換わってしまう。「テストモード＝観察専用」という
  T-061の核心原則を破る。
- **パターン再発台帳照合**: PR-12(新規上位モード導入時の経路対応漏れ)と根本原因の型は同一だが、T-070は
  「モード導入時」ではなく「モード確立後に追加された新機能がゲート接続を忘れた」ケース。疑いあり程度で
  家老へ申し添える(断定は避ける)。

### A-2: ReplaceOneDeviceNameが機器表のBOM情報(Model/Maker/Quantity)を消失させる

- **file**: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1789-1803`
- 新名が未登録の場合`new Device{...}`で作り直すため、既存Deviceオブジェクトが持つModel/Maker/Quantityが
  初期値にリセットされる。同ファイルの`SelectedElementDeviceName`セッター(1721行)は非空→非空の通常リネーム
  時に`DeviceRenamer.Rename`(既存Deviceオブジェクトをキー移行、BOM情報保持)を呼ぶのと非対称。
- **failure_scenario**: 機器表でX001にModel/Maker/Quantityを登録済み、参照要素が1個のみの状態で検索バーから
  X001→X002へ「置換」(1件)を実行すると、X002用に空のDeviceが新規生成されBOM情報が静かに消失する。

### A-3: ByName.ContainsKeyがOrdinal比較、大文字小文字違いの置換で重複Deviceエントリが残る

- **file**: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1796`
- `DeviceRenamer.Find`/`RemoveDeviceIfUnreferenced`はOrdinalIgnoreCaseで判定するが、
  `ReplaceOneDeviceName`内の`ByName.ContainsKey`は既定のOrdinal(大文字小文字区別あり)。
- **failure_scenario**: 機器"m1"のみ存在する状態で"M1"へ置換すると、ContainsKey("M1")がfalse判定となり
  新規Device("M1")を追加、かつRemoveDeviceIfUnreferenced("m1")はOrdinalIgnoreCaseで「まだ参照あり」と
  誤判定し旧キー"m1"を削除しない。結果、機器表に"m1"(BOM情報あり・未参照)と"M1"(BOM情報空)の2行が残る。

### A-4: ReplaceAll()がSelectedElementDeviceName等の通知を発火させずプロパティパネルとズレる

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs:176-186`
- `ReplaceOne()`は`_owner.ReplaceOneDeviceName`経由で`NotifySelectedElementChanged()`
  (`OnPropertyChanged(nameof(SelectedElementDeviceName))`含む)を呼ぶが、`ReplaceAll()`は
  `DeviceRenamer.Rename`を直接呼ぶだけでこれを一切呼ばない。
- **failure_scenario**: 選択中要素の機器名『X001』を検索・全置換で『X999』に変えても、右パネルの
  DeviceNameBoxは『X001』のまま表示が取り残される(内部データと画面表示の食い違い)。

### A-5: ReplaceOne/ReplaceAllがno-op置換でもRecordSnapshotを呼びRedo履歴を無意味に破棄する

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs:169,181`
- 実際に変更が起きるかの判定(oldName==newName等)より前に`UndoManager.RecordSnapshot`を呼んでいる。
  同ファイル内`MoveSelectedImage`等(1349行)は「値が実際に変化する場合のみRecordSnapshotを呼ぶ」規約を
  確立済みだが、本コミットの2箇所はこれに反する。
- **failure_scenario**: 置換後欄に現在の機器名と同じ文字列を入力して置換を押すと、実処理は何も変えない
  にもかかわらずUndoスタックへ無意味なスナップショットが積まれ、既存のRedo履歴が消去される。

### A-6: FindBar表示中にF5等のグローバル配置ショートカットが素通しで誤配置が起きる

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1416`付近(F5〜F10ケース)、`Window_PreviewKeyDown`冒頭
- 既存の`IsPlacementBarVisible`/`_rungCommentEditingRow`は関数冒頭で早期returnし他の全ショートカットを
  無効化する設計だが、`Find.IsVisible`にはこれが無い。F5等のケース自体も`IsCanvasFocused()`を持たない
  (F2/Deleteは持つのに非対称)。
- **failure_scenario**: FindQueryBoxにフォーカスがある状態でF5を押すと、Window_PreviewKeyDownのcase Key.F5が
  素通しで成立しTryPlaceBuiltinが実行され、SelectedCellへ要素が配置されてしまう。

### A-7: Escape処理、配置バー表示中は検索バーのEscapeが機能しない

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1257`(早期return)/`1283-1289`(T-070追加のFindBar Escapeケース)
- `if (_viewModel.IsPlacementBarVisible) return;`がswitch文より前にあるため、検索バーと要素配置バーが
  同時に表示された状態ではEscapeが配置バー側にのみ作用し、検索バー閉じ処理(switch内)に到達しない。
- **failure_scenario**: 検索バーを開いたまま(SelectedCellはジャンプ済み)部品パレットから配置バーも開くと
  両バーが同時表示状態になり、Escapeを1回押しても検索バーは閉じず、配置バー消滅後にもう一度押す必要がある。

### A-8: ReplaceWithがIsVisible=false時にクリアされず残留する

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs:41-49`
- `IsVisible`セッターは`Query`のみクリアし`ReplaceWith`に触れない。直上のコメント「閉じると検索状態を
  クリアする...再度開いたら毎回まっさらな状態から始める」の契約に反する。
- **failure_scenario**: 置換後欄に文字列を入力したままバーを閉じ、別目的で再度開いてQueryだけ入力し
  置換後欄を確認せず全置換を押すと、残留した文字列への意図しない一括置換が実行される恐れがある。

### A-9: FindResultsGridのDockPanel配置でStar列が正しく展開されない

- **file**: `src/Ecad2.App/MainWindow.xaml`(`OutputPanelArea`内、`FindResultsGrid`)
- `FindResultsGrid`が`OutputGrid`(DockPanel最後の子=Fill対象)より前にあり`DockPanel.Dock`未指定のため、
  DockPanel仕様上Dock.Left扱いとなりFill配置を受けない。DataGridのStar列(該当箇所列)はFill配置時にしか
  正しく比例展開されないため、検索結果パネルが左寄せの狭い帯として表示されるおそれがある。
- 忍者の実機確認(スクリーンショット)での見た目確認を推奨。

---

## B群: sweep追加(隠密静的読解、未verify・高確度)

### B-1: Undo/Redo後、Find.Matchesが古いDocument参照のまま取り残される

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs:143`(JumpTo)
- Undo/Redoで`Document`が丸ごと差し替わっても、`Find.Matches`は差し替え前のSheet/ElementInstance参照を
  保持したまま。`ApplyUndoRedoSnapshot`は`OutputPanel.ClearResults`相当の明示クリアをFindには行わない。
- **failure_scenario**: 検索実行後にUndoすると、検索結果パネルは古い一致件数を表示し続け、行クリックで
  `JumpToMatch`→`JumpTo`が`Sheets.IndexOf(match.Sheet)`で-1を返し無言でreturnする(沈黙のバグ)。

### B-2: 検索入力中の自動JumpToが記入中ドラフトを無警告で破棄する

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs:121`(RunSearch→JumpTo)
- 縦コネクタ等の記入中ドラフト中でもCtrl+F自体はブロックされておらず、Query入力で完全一致が成立した
  瞬間`SelectedCell`セッターが無条件で`ClearConnectorDraftIfAny`等を呼ぶ。Enterでの確定時は案内メッセージが
  出る設計と非対称で、検索バー経由の破棄は完全に無言。
- **failure_scenario**: 縦コネクタ記入中にCtrl+Fで検索し既存機器名を入力すると、確認なくドラフトが消える。

### B-3(軽微): Ctrl+FでFindBarを閉じるトグル経路のみFocusCanvas()が欠如

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1676`付近
- 閉じるボタン・Escapeでの終了はFocusCanvas()を呼ぶが、Ctrl+Fトグルオフ経路だけ呼ばない非対称。
- **failure_scenario**: 2回目のCtrl+Fで閉じた際、フォーカスが非表示化したFindQueryBoxに残り、以後の
  キャンバス操作(矢印キー等)が意図通り効かない可能性がある。

---

## C群: cleanup/efficiency(参考、優先度低)

- Query変更のたびに全シート・全要素を走査(`DeviceRenamer.Find`)、大規模文書での応答性低下懸念(デバウンス
  無し)。
- `ReplaceOneDeviceName`の新規Device登録ロジックが`SelectedElementDeviceName`・要素配置処理に続く3箇所目の
  複製(PR-07 rule of three該当の疑い)。
- `FindBar`用Borderのインラインスタイルが`ElementPlacementBar`・`RungCommentEditor`と同一パターンの3例目の
  複製(共有Styleリソース抽出の余地)。
- `JumpToMatch`が`Matches.ToList().IndexOf(match)`と、既にList化済みの結果を毎回コピーしてから線形探索。

---

## 不明点

なし。A群9件はcode-reviewスキルの検証エージェントが全てCONFIRMEDと判定(該当行引用込み)。B群3件は
隠密の静的読解のみ(未verify、ただし機序は具体的で確度は高いと判断)。

## 派生提案

なし(A-1のPR-12類似指摘は上記本文に記載、断定はせず家老確認を仰ぐ形とした)。
