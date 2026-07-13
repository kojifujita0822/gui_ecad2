# T-061 テストモードUI結線 静的レビュー（隠密、2026-07-13）

対象: コミット937e99a（親32cd56a）、9ファイル変更413行追加。
手法: 手動観点確認（家老指定5点）+ `code-review`スキル（--effort high、10角度finder→7件の検証エージェントで1票検証）。
全候補、検証エージェントによりCONFIRMEDと判定された（PLAUSIBLE/REFUTEDなし）。

## 結論（総評）

**要修正**。殿裁定6点のうち③（通電表示配色）が実質機能不全、かつ「テストモード＝観察専用」という
設計の核心がマウス経路にしか実装されておらずキーボード・ツールバー・メニュー・Undo/Redoの各経路
から回路データを改変できる。コミットの目玉機能の一つ（セレクトSWノッチ順送り）も実装上の型不整合で
完全に死んでおり常時単純トグルにフォールバックしている。

以下、severity順に記す（file:line、failure_scenario付き）。

---

## A群：テストモード中の回路データ改変（最重要、殿裁定「観察専用」の核心が崩壊）

### A-1. キーボード全域にMode==Testガード皆無

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1207`（`Window_PreviewKeyDown`全文）
- マウス側（`LadderCanvasHost_PreviewMouseLeftButtonDown:555-577`・`PreviewMouseRightButtonDown:978-987`）は
  関数冒頭で`Mode==Test`なら専用処理へ分岐し通常処理を完全にバイパスする。一方キーボード側にはMode参照が
  一つも無い（grep`AppMode|IsTestMode`ヒットなし）。
- **根本原因**: `MainWindowViewModel.cs:53-78`の`Mode`セッタがTest突入時に`Tool=SelectDefault`のみ
  リセットし、`SelectedCell`・`SelectedElement`（算出プロパティ、`SelectedCell`から都度再計算）・
  `SelectedConnector`等はクリアしない。
- **failure_scenario（マウス操作のみで再現可）**: (1)作画モードで要素をクリック選択 (2)ツールバーの
  テストモードToggleButtンをON (3)メニュー「編集(_E)→削除(_D)」（`MainWindow.xaml:147`、
  兄弟メニュー項目と違い`IsEnabled`バインド無し）をクリック→`DeleteSelectedElement()`が無条件実行され
  実要素が削除される。キーボード側でも同条件下でDelete(1510)・矢印キー(1430)・F5-F10配置(1365-1419)・
  Ctrl+Z/Y(1572-1585)・F2コメント編集(1497-1509)が全て素通しする。

### A-2. ツールバーの配置ボタン自体もMode非参照、AppMode/ToolModeの相互排他機構が無い

- **file**: `src/Ecad2.App/MainWindow.xaml:282-341`（2段目ツールバー、`IsEnabled="{Binding HasProject}"`のみ）
  + `MainWindow.xaml.cs:1876-1887`（`ActivateBuiltinTool`）
- **failure_scenario**: テストモードON中に2段目ツールバーの「a接点配置」ボタンを普通にクリックするだけで
  `Tool.Mode=PlaceElement`に変化し、「Mode==Test かつ Tool.Mode==PlaceElement」という設計上ありえない
  組み合わせが成立する。副作用として`IsPartSelectionVisible`(`MainWindowViewModel.cs:47`、
  `Tool.Mode==PlaceElement`のみで判定)が真になり、**テストモード中に右パネルが部品選択リストへ
  切り替わる**という目に見える表示不整合が発生する。

### A-3. Undo/RedoでTestSessionが無警告で作り直され、シミュレーション状態が消失

- **file**: `MainWindowViewModel.cs:87-101`（`_testSessions`辞書、Sheet参照キー）+ `:2432-2470`
  （`ApplyUndoRedoSnapshot`、T-051既存）
- `_testSessions`は`Sheet`オブジェクトの**参照**をキーにする（`Sheet`はEquals/GetHashCode未実装＝参照比較）。
  Undo/Redoは`Document = restored;`で文書全体を新規デシリアライズしたグラフに差し替えるため、Undo後の
  Sheetは別実体になる。UndoCommand自体もMode==Testを見るCanExecuteガードが無い。
- **failure_scenario**: 自己保持回路の押しボタンを押して離しコイルが通電保持されているのを確認→Ctrl+Zで
  Undo→`CurrentTestSession`ゲッターが新Sheet実体に対し未評価の空セッションを黙って生成→通電表示していた
  コイル・接点が警告なく即座に非通電表示へリセットされる。

---

## B群：殿裁定③（通電=赤/非通電=グレー）の視覚フィードバック機能不全

### B-1. コイル以外（接点・押しボタン・セレクトSW）は強制ON操作をしても線色不変

- **file**: `src/Ecad2.Core/Rendering/DiagramRenderer.cs:949-957`（`DrawElement`）
- `energized`辞書（`Evaluator.cs:52-62`）は`ComponentRole.Load`（コイル系）のみに書き込まれる。押しボタン・
  セレクトSW・単独接点のDeviceNameは対象外。手動強制（`TestSession.SetInput`等）は`State.Inputs`/
  `State.Positions`のみ更新し`State.Energized`は変更しない。
- **failure_scenario**: テストモードで押しボタンを押し続けても、あるいは接点を右クリックで「手動でON」
  にしても、その記号自体の線色（赤/グレー）は常にグレーのまま変化しない。接点(ContactNO/NC)限定で
  半透明青の塗り（`ManualForced`、別チャネル）は機能するが、押しボタン・セレクトSWはこの塗りの対象にも
  含まれず**完全に無フィードバック**。

### B-2. 限時タイマ接点が時期尚早に赤くなる（経過時間を無視）

- **file**: `DiagramRenderer.cs:949-957` + `src/Ecad2.Core/Simulation/Evaluator.cs:148-157`
- 実際の導通条件は`coilOn && timedOut`だが、`DrawElement`は`ElementKind`による分岐が無く
  `energized[DeviceName]`（コイル励磁のみ）で色を決める。`TimerElapsed`/`TimerSetpoints`を参照する手段が
  `DrawElement`のシグネチャ自体に無い。
- **failure_scenario**: オンディレイタイマでコイル励磁直後（設定時間未達、実際は非導通）から対応する
  限時接点シンボルが即座に赤表示される。瞬時接点（TimerInstantContactNO/NC）はコイル励磁のみで正しく
  導通するため対象外。

### B-3.（参考・次点調査）NC系接点の通電色反転ロジックが欠落している疑い

- `DrawElement`の`on`計算はNO/NCの区別を一切行わない。ContactNC等本来「コイル非励磁時に導通=赤」で
  あるべきところ、実装は「コイル励磁時に赤」という向きになっている可能性がある（検証4担当エージェントが
  判定対象外として書き添えた付随発見、要追加確認）。

---

## C群：接点/セレクトSW判定の型不整合（コミット目玉機能の機能不全）

### C-1. セレクトSWのノッチ順送りが完全に死んでいる（常時単純トグルにフォールバック）

- **file**: `MainWindowViewModel.cs:2024`（`CycleSelectSwitch`の`Where`条件）
- セレクトSWは実際には`PartRole.ContactNO`＋`PartId="basic-select-switch"`として自作パーツ的に配置され
  （`BasicPartTemplates.cs:151-169`）、`PlaceElementAtSelectedCell`（`MainWindowViewModel.cs:2095-2101`）は
  `ElementInstance.Kind`を一切設定しないためC#既定値`ContactNO`のまま固定される。
  **既存テスト`MainWindowViewModelTests.cs:349-353`（T-046由来）のコメントに「SelectSwitchは配置経路で
  到達不能な13値の一つ」と既に明記されていた**——今回のコミットはこの既知の制約を見落として
  `ElementKind.SelectSwitch`直接判定のロジック（GuiEcad移植）をそのまま持ち込んだ。
- **failure_scenario**: `positions.Count==0`が常に成立し`session.ToggleInput`（安全弁）に必ず
  フォールバックする。セレクトSWをクリックしてもノッチが順送りされず単純トグルとしてしか動作しない。

### C-2. 右クリックの「接点限定」ガードが機能せず、コイル等どの要素でも強制ON/OFFメニューが出る

- **file**: `MainWindow.xaml.cs:1086`（`ShowTestModeContextMenu`の`hit.Kind is not (ContactNO or ContactNC)`）
- C-1と同根：全ての新規配置要素の`Kind`が既定値`ContactNO`固定のため、このガードは常にfalse
  （素通り）になる。
- **failure_scenario**: テストモード中にコイル・ランプ・タイマ・端子台等どの要素を右クリックしても
  「接点: ○○」という誤った強制ON/OFFメニューが表示される。

### C-3. 左クリックでコイル等を押すと誤って強制導通してしまう

- **file**: `MainWindowViewModel.cs:1991-2009`（`TestModePress`のdefault分岐）+ `Evaluator.cs:159-164`
- `MapToDeviceClass`はContactNO/NC・Coil・ContactorMain3Pを全て`DeviceClass.Relay`に丸めるため、
  `default: session.ToggleInput(device)`がコイルにも無差別適用される。
- **failure_scenario**: 自己保持回路でコイル記号を左クリックすると`Inputs[コイル名]=true`になり、
  `IsConducting`が`Energized || Inputs`のORで判定するため、同名の補助接点が実際の励磁状態と無関係に
  強制導通してしまう。右クリック限定のはずの強制機構が、視覚的な「手動ON中」表示も無いまま左クリックで
  発動する。

---

## D群：中程度（実害あるが発生条件が狭い/見た目のみ）

### D-1. 未確定ドラフト残留によるゴースト描画＋HasAnyDraftガード迂回

- `Mode`セッタは`_connectorDraft`/`_freeLineDraft`/`_imageInsertDraft`をクリアしない
  （コメント自身は「進行中ドラフトの破棄」を謳うが実装が満たしていない、コメントと実装の乖離）。
- 加えて`PreviewMouseRightButtonDown`のTest分岐(978-987行)が既存の`HasAnyDraft`保護(989行、T-069確立)
  より前に置かれ、テストモード中は常にこの保護が迂回される。

### D-2. CaptureMouse()戻り値未チェック

- `MainWindow.xaml.cs:571`。同ファイル他5箇所は確立パターン（隠密レビュー所見C対応）で戻り値を
  確認するが、T-061新規コードだけ踏襲していない。キャプチャ失敗時に押しボタンのモーメンタリがON固定
  されうる。

### D-3. メニュー「テストモード」にHasProjectガード無し

- `MainWindow.xaml:159`。ツールバー側ToggleButtonは`IsEnabled="{Binding HasProject}"`を持つが
  メニュー項目には無い、非対称。

---

## E群：cleanup/efficiency（品質改善、DoDに直結せず）

- **E-1（PR-07該当）**: 行範囲チェック式`pos.Row>=0 && pos.Row<sheet.Grid.Rows`が
  `MainWindow.xaml.cs`の567/993/1083行目の3箇所に手書き重複。
- **E-2**: `OnRealtimeTick`(100ms間隔)が命令の有無に関わらず無条件に`NetlistBuilder.Build`3回+
  `Evaluate`3回+`RedrawCanvas`を実行し続ける。
- **E-3**: `Mode`セッタがモード切替のたびに無条件で`OutputPanel.RunDrcCommand.Execute(null)`を呼び
  全シートDRCを再実行する（モード切替は文書構造を変えないため論理的に無駄）。
- **E-4**: `Mode`セッタのif/else両分岐で`_testSessions.Clear()`を重複呼び出し（else節はこの1行のためだけ）。
- **E-5（是正済み）**: コード内コメントに丸数字（①②③④⑤⑦）が8箇所混入していたが、後続コミット
  `7fa43db`で`(1)(2)(3)`表記へ全て是正済み。

---

## パターン再発台帳照合

A-1（新モード導入時、マウス経路のみガードしキーボード・ツールバー・メニュー・Undo等の他経路を
見落とす）は、`docs-notes/pattern-recurrence-log.md`のPR-01（新規選択可能状態の横展開漏れ）と
性質は近いが対象が異なる（PR-01は「新しい選択可能状態」、今回は「新しい上位モード」）ため、
**新規パターン候補PR-12として台帳へ追記**した（家老の制度化検討を仰ぐ）。

---

## 不明点

- B-3（NC系接点の色反転）は判定対象外のため追加確認が必要。
- E-2/E-3の許容コスト（実機での体感パフォーマンス）は未計測、忍者の実機確認で体感確認が望ましい。

## 派生提案

なし（全て本コミットの範囲内の指摘のため`proposed.md`行きの対象外）。
