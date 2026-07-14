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

## 不明点(初稿時点)

なし。A群9件はcode-reviewスキルの検証エージェントが全てCONFIRMEDと判定(該当行引用込み)。B群3件は
隠密の静的読解のみ(未verify、ただし機序は具体的で確度は高いと判断)。

## 派生提案(初稿時点)

なし(A-1のPR-12類似指摘は上記本文に記載、断定はせず家老確認を仰ぐ形とした)。

---

## D群: 往復2周目レビュー(コミット218a769、2026-07-14、新規発見・全てCONFIRMED)

A-1〜A-9・B-1〜B-3の12件対応後の再レビュー。`code-review`スキル(--effort high、7エージェント)+
sweep1件、新規候補6件を検証エージェントに掛けた結果、**全6件CONFIRMED**(cleanup3件は簡易確認)。

### D-1(A-3修正の不完全性): 旧名が他要素からも参照されている場合、大文字小文字違いの重複が再発する

- **file**: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`(`MigrateOrRegisterDevice`、1810行付近)
- 新設`MigrateOrRegisterDevice`のin-place移行分岐は`oldStillReferenced==false`(旧名がこの要素以外から
  参照されなくなった)場合にしか働かない。旧名を複数要素が共有している状態(機器名の一意性は保証しない
  設計のため許容される)で大文字小文字違いの置換を行うと、`oldStillReferenced==true`となりフォールバック
  (新規Device作成)に落ち、`Dictionary<string,Device>`の既定Ordinal比較のため旧キーが削除されず
  機器表に2エントリ(例: "m1"と"M1")が並立する。**A-3が解消しようとした症状そのものが、複数要素同名
  という別条件下で再発する**。
- **failure_scenario**: 機器名"m1"の要素A・Bが存在(BOM情報付き"m1"エントリ登録済み)、Aのみを
  "m1"→"M1"へ置換すると、"m1"(旧・Bが参照)と"M1"(新・空Device)の2行が機器表に残る。
- 新設テスト`ReplaceOneCommand_CaseOnlyChange_DoesNotLeaveDuplicateDeviceEntry`は要素1個のケースのみ
  検証しており未カバー。

### D-2: ReplaceAllDeviceName(全置換)は置換先が既存Deviceの場合、BOM情報を無条件上書きする

- **file**: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`(`ReplaceAllDeviceName`)+
  `src/Ecad2.Core/Simulation/DeviceRenamer.cs`(`Rename`)
- 単発置換側(`MigrateOrRegisterDevice`)は`existingNewKey`チェックで「新名が既に別Deviceとして存在する
  場合は上書きしない」保護を持つが、`ReplaceAllDeviceName`は`DeviceRenamer.Rename`を呼ぶのみでこの保護
  が無い。`Rename`内部の`doc.Devices.ByName[to] = device;`は既存の`to`エントリを無条件上書きする。
- **failure_scenario**: 機器表に"M1"(Model=OLD)と"M2"(Model=NEW)が別々に登録済みの状態で"M1"→"M2"へ
  全置換すると、"M2"のBOM情報(NEW)が"M1"由来のBOM情報(OLD)で上書きされ消失する。単発置換とのガード
  非対称(A-2/A-3対応時に単発側のみ保護を作り、全置換側への横展開が漏れた疑い、`onmitsu.md`「修正の
  横展開確認」節に該当)。

### D-3: ClearResults()が再検索しないため、Undo後に実データと食い違う「0/0」誤表示が残る

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs`(`ClearResults`)
- B-1対応の`ClearResults()`は`Matches`を空にするのみで`RunSearch()`を呼ばない。`Query`テキストも
  クリアしない。Undo/Redo後、Documentに現在のQueryへ一致する要素が実在していても検索結果パネルは
  「0/0」のまま固定され、実データとの食い違いが生じる。
- **failure_scenario**: Query="X001"で2件ヒット中に無関係な編集をUndoすると、復元後もX001要素は
  実在するのに検索結果は「0/0」と誤表示され、Next/Prev/置換も使えなくなる(Queryを再入力しない限り
  復旧しない)。B-1の対処(沈黙不整合の解消)自体は妥当だが、副作用として新しい別の不整合(誤表示)を
  生んでいる。

### D-4(PR-05型の疑い): B-1修正(Find.ClearResults)がApplyUndoRedoSnapshotのみに適用され、ReplaceDocument(新規作成・開く)には未適用

- **file**: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`
- `Find.ClearResults()`の呼び出しは`ApplyUndoRedoSnapshot`内の1箇所のみ。`ReplaceDocument`
  (新規作成・ファイルを開く経路、`OutputPanel.ClearResults()`は呼ぶ)には同型の追従が漏れている。
- **failure_scenario**: 検索結果表示中に「新規作成」または「開く」を実行すると、旧Document参照を
  保持した`Find.Matches`が残り、検索結果パネルの行クリックで`JumpTo`が無言returnする(B-1が解消
  しようとした沈黙不整合と同型)。
- **パターン再発台帳照合**: PR-05(状態リセット処理の横展開漏れ、Document/Sheet構成変更時)と同型
  (方向は逆=既存処理への新規責務追加時の横展開漏れ)。家老確認のうえ台帳追記を検討されたい。

### D-5(新規パターン候補、同一タスク内2件目): Next/Prev/JumpToMatchがB-2のドラフト保護を受けていない

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs`(`Next`/`Prev`/`JumpToMatch`)
- B-2のドラフト保護(`!_owner.HasAnyDraft`)は`RunSearch()`内の自動JumpToにしか適用されておらず、
  ユーザーが明示的に押す「次」「前」ボタン・検索結果行クリックの経路(`JumpToMatch`)は無保護のまま。
- **failure_scenario**: 縦コネクタ記入中にCtrl+Fで検索(自動JumpToは正しく抑制されドラフト保持)、
  その後「次へ」ボタンを押すとドラフトが無警告で破棄される(B-2が防ごうとした症状の別経路での再発)。
- **パターン再発候補**: D-1(A-3)・D-5(B-2)はいずれも「同一メソッド内の類似呼び出し経路のうち一部
  にしか修正が適用されない」という型で、本タスク内で2回発生している。新規パターン候補として台帳へ
  記帳するか家老判断を仰ぎたい(DoD4「B-C群でのPR-13型ゲート接続漏れは該当なし」という侍の判断自体は
  妥当、D-1/D-5はPR-13=CanEditDiagramゲートとは別の型)。

### D群cleanup(簡易確認、優先度低)

- Escapeを閉じる処理(`_viewModel.Find.IsVisible = false; FocusCanvas(); e.Handled = true;`)が
  `IsPlacementBarVisible`早期return内とswitch文内Escapeケースの2箇所で複製。
- F5〜F10へ`IsCanvasFocused()`を9箇所個別追加した結果、前回T-070初回レビューで指摘した
  `noModifier/shift && IsCanvasFocused()`パターンの複製箇所が11→20箇所に増加(`docs/proposed.md`
  P-083関連、共有bool変数化の提案が未採用のまま拡大)。
- `MigrateOrRegisterDevice`(oldKey/existingNewKey検索)と`RemoveDeviceIfUnreferenced`
  (呼び出し元から無条件で続けて呼ばれる)が同じ「oldNameがまだ参照されているか」を独立に2回線形走査。

### RED先行証明の妥当性(DoD3)

新規10件のテスト全てを検証エージェントに個別精査させた結果、**旧実装(fab6d51、A-1〜A-9/B-1〜B-3の
修正が無い状態)で確実にFAILする**ことを論理的に確認(検出力に疑義のあるテストは無し)。侍のRED実測
(git stash→旧実装で10件FAIL→pop→GREEN復帰)の報告と矛盾しない。

### DoD4(PR-13型ゲート接続漏れの有無)の判断

侍の「B-C群での同種ゲート接続漏れ(PR-13型)は該当なし」という判断は**妥当**(FindViewModel内で
Document変更を伴う操作はReplaceOne/ReplaceAllのみで、両者ともA-1でCanEditDiagramガード済み)。
ただしD-1/D-2/D-4/D-5は「PR-13とは別の型」の横展開漏れであり、往復3周目の修正が必要。

## 結論(往復2周目時点)

**要再修正5件(D-1〜D-5、全てCONFIRMED)+cleanup3件**。侍への差し戻しを推奨する。

---

## E群: 往復3周目レビュー(コミットaf9d1fa、2026-07-14、D-1〜D-5修正への追加検証)

D-1〜D-5修正(af9d1fa)の実装確認+`code-review`スキル(--effort high、5エージェント)+追加verify2件。
**新規に1件の重大バグ(E-1)を発見**。4周目回避のため特に慎重に検証したが、報告を避けられない重大度。

### E-1(最重要・新規、CONFIRMED): RefreshAfterDocumentReplacedのCurrentIndexリセットが、Undo後の誤爆置換を招く

- **file**: `src/Ecad2.App/ViewModels/FindViewModel.cs`(`RunSearch`/`RefreshAfterDocumentReplaced`)+
  `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`(`ApplyUndoRedoSnapshot`)
- D-3対応で追加された`RunSearch(bool allowJump = true)`は、`allowJump=false`でも`CurrentIndex = Matches.Count > 0 ? 0 : -1;`を無条件実行する(スキップされるのは`JumpTo`呼び出しのみ)。一方
  `ApplyUndoRedoSnapshot`はSelectedCellを明示的に保持する設計(殿裁定、Undo/RedoはSelectedCellを
  巻き戻さない)。この結果、Next/Prevで検索結果の2件目(B)へジャンプ済み(SelectedCell=B、
  CurrentIndex=1)の状態で無関係な編集をUndoすると、`Find.RefreshAfterDocumentReplaced()`が
  `CurrentIndex`を強制的に0(A)へ巻き戻す一方、SelectedCellはBのまま据え置かれる。
- **failure_scenario**: 上記の状態で、画面上はB(SelectedCellのハイライト)が選択されて見えるのに、
  ここで「置換」ボタン(ReplaceOneCommand)を押すと`CurrentMatch`(=Matches[0]=A)が対象になり、
  **ユーザーが意図していないA(画面上非選択)が書き換わり、選択しているつもりのBは変更されない**。
  検証エージェントが`FindResultsGrid`の行ハイライトバインディング有無も確認したが、検索結果パネル
  には`SelectedItem`連動が無く、キャンバス上のSelectedCellが実質唯一の視覚的「現在位置」手掛かりの
  ため、「表示とCurrentMatchは一致するので実害なし」という反論は成立しないと判定。
- D-3(RefreshAfterDocumentReplaced導入)が生んだ新しい副作用であり、D-1〜D-5そのものの再発ではない。

### E-2(軽微、CONFIRMED、実害は表示のみ): D-1の保護分岐でDevice.Nameの表記が更新されないまま残る

- **file**: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`(`MigrateOrRegisterDevice`、1849行付近)
- 旧名を複数要素が共有し(oldStillReferenced=true)、かつ大文字小文字違いの自己リネームで
  `existingNewKey`が`oldKey`自身にヒットする場合、`if (existingNewKey is not null) return;`が
  成立し`Device.Name`もキーも更新されない。要素側の表示(盤面、"M1")と機器表側の表示
  (`Device.Name`、"m1"のまま)が食い違って残る。
- **実害評価**: シミュレーション側は`Device.Name`ではなく要素自身の`DeviceName`文字列を直接見るため
  機能的な動作(通電判定等)には影響しない。機器表(BOM)の表示列が食い違うのみ。「置換1件のみ」
  という殿裁定(機器名の一意性は保証しない設計)の一時的な副産物であり、D-1が解決した「重複エントリ」
  問題そのものの再発ではないため、修正の要否は家老・殿判断でよい(緊急性は低い)。

### E群cleanup(簡易確認、優先度低)

- `MigrateOrRegisterDevice`の`Document.Devices.ByName.Remove(oldKey, out var device); device!.Name = newName;`が、Removeの戻り値(bool)を無視しnull免除演算子(!)で握りつぶしている。現状は到達不能だが将来の変更でNullReferenceExceptionの罠になりうる。
- 「置換先が既存Deviceなら上書きしない」保護ロジックが`MigrateOrRegisterDevice`(App層)と
  `DeviceRenamer.Rename`(Core層)に独立実装され重複(Reuse)。将来3本目のリネーム経路が追加された際に
  同型の保護漏れが再発するリスク(rule of three関連)。
- OrdinalIgnoreCaseキー線形探索(`Keys.FirstOrDefault`)が計5箇所(RemoveDeviceIfUnreferenced・
  MigrateOrRegisterDevice2箇所・DeviceRenamer.Rename2箇所)に複製、rule of three超過の疑い。

### DoD確認結果

- DoD1(D-1〜D-5の実装確認): 5件とも指摘どおり実装されている(E-1はD-3実装が生んだ別の新規副作用)。
- DoD2(D-2自己リネームケース): `DeviceRenamer.Rename`の`existingToKey == key`分岐で自己リネームを
  正しく通常のキー移行として扱えることを確認、妥当。
- DoD3(RED先行証明5件の妥当性): 5件全て個別精査、直前コミット(218a769)で確実にFAILすることを
  論理確認、検出力に疑義なし。
- DoD4(code-review併用): 実施済み(5エージェント+追加verify2件)。
- DoD5(4周目回避への配慮): 結果としてE-1という新規の重大バグが見つかり、往復4周目の修正が必要な
  状況になった。これはD-1〜D-5の往復3周目修正そのものではなく、D-3(RefreshAfterDocumentReplaced)
  という新設ロジックが生んだ別の副作用であり、見逃せば実データ破損(意図しない機器名の書き換え)に
  直結するため報告する。

## 結論(往復3周目時点)

**D-1〜D-5は概ね正しく修正されている**が、**E-1(誤爆置換、重大)の追加修正が必要**。E-2・cleanupは
緊急性低。E-1の対処案としては、`RunSearch(allowJump: false)`実行時に`CurrentIndex`を「Matchesの中に
SelectedCellと一致する要素があればそのインデックス、無ければ0」へ設定する(SelectedCellとの整合性を
保つ)方式が単純か。侍への差し戻しを推奨する。

---

## F群: 往復4周目レビュー(コミット6184e29、2026-07-14、E-1修正の確認・決着)

`IndexOfSelectedCellOrZero`新設によるE-1修正を`code-review`スキル(--effort high、3エージェント+
sweep)で検証。**correctness bugは0件**(3エージェント独立確認、実装は隠密の対処案どおり正しい)。

### RED先行証明2件の妥当性・侍の自己申告の正確性

- `ReplaceOneCommand_AfterUndoWhileJumpedToSecondMatch_ReplacesSelectedCellNotFirstMatch`(誤爆置換
  再現): 直前実装(af9d1fa、CurrentIndexが無条件で0)で確実にFAILすることを論理確認、検出力あり。
- `RunSearch_AfterUndo_NoMatchAtSelectedCell_FallsBackToFirstMatch`(境界値): **侍の自己申告(「旧実装
  でも偶然合格し検出力なし」)は正確と確認**。「一致無し→0を返す」という新実装のフォールバック
  分岐が、旧実装の「無条件0」と数学的に一致するため構造的に判別不能(要素数・SelectedCell位置に
  関わらず常に成立)。回帰確認テストとして残す判断は妥当。

### 気づき(修正不要、参考記録)

- `IndexOfSelectedCellOrZero`のシート比較条件(`Document.Sheets.IndexOf(...) == CurrentSheetIndex`)を
  専用に検証するテストが無い(複数シートに同座標・同名要素があるケースが未検証)。現状のロジック
  自体は正しいと確認済みだが、将来この条件がリファクタリングで誤って削除された場合、E-1と同型の
  誤爆が再発してもテストでは検出できない隙間がある。緊急性なし、次にこの周辺へ手を入れる際の
  参考情報として記録。
- cleanup(三項演算子ネストの可読性・既存SelectedElement解決ロジックとの重複・CurrentIndexと
  SelectedCellの二重状態管理)は軽微、E-2・往復2周目cleanupと合わせて家老裁定どおり対象外のまま。

## 結論(往復4周目・最終)

**E-1修正は正しく実装されており、correctness bugは無し。T-070は決着**。侍の自己申告(検出力なし
テストの正直な報告)も正確と確認。テストギャップ1件(気づき)を記録したが修正は不要、次回この周辺に
手を入れる際の参考情報に留める。実機確認は殿指示により後日持ち越し。
