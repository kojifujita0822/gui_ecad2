# T-061修正(a9008d3) 再レビュー(隠密、2026-07-13)

対象: コミットa9008d3(親937e99a)、7ファイル変更614行。手動観点(家老指定5点)+`code-review`スキル
(--effort high、10角度finder→7件検証)。全12候補、検証エージェントによりCONFIRMEDと判定。

## 結論

ビルド・テスト全合格(Core98/App574)、設計書見落とし訂正(コイル=energized・接点等=elementConducting
使い分け)は`ElementCatalog.IsLoad`経由で正しく実装。**ただし新たに致命的な機能不全1件・A群の核心に
再度抵触する抜け穴2件・優先度中2件・cleanup3件を検出。要再修正**。

---

## 家老指定確認点への回答

1. **設計書見落とし訂正の実装**: 正しく実装されている(`DiagramRenderer.cs`の`isLoad`判定を
   `ElementCatalog.IsLoad`経由で先出しし、負荷は`energized`・非負荷は`elementConducting`を見る
   分岐)。
2. **RED先行証明の報告内容と実際のテスト整合**: 静的読解で疑義提起済み、侍の再実測で6件中4件正しく
   RED・2件検出力なし(偽陽性)と確定(コミットd671d2c、`samurai.md`へ制度化済み)。
3. **D/E群の反映確認**: D-1/D-2/D-3、E-1/E-4とも差分で反映確認済み。
4. **矢印キー(選択中プリミティブ移動ケース)がCanEditDiagram対象内=禁止のまま実装**: 確認済み、
   正しく実装されている(`!CanEditDiagram`分岐でMoveSelectedCellのみ実行しbreak、既存の
   Shift+矢印プリミティブ移動ケースには到達しない)。
5. **Enterキー新規結線(PreviewKeyUp新設・IsRepeat多重発火防止)**: 実装は確認できたが、
   **新たな欠陥を伴っていた**(A-2/A-3参照)。

---

## A群: 最重要(correctness、新規発見)

### A-1(致命的・DoD未達の疑い): セレクトSWの電気的導通判定が構造的に機能しない

- **file**: `src/Ecad2.Core/Simulation/Evaluator.cs:148-150`(`IsConducting`のSelectSwitch分岐)
- 実際に配置されたセレクトSW要素は`PartRole.ContactNO`(`BasicPartTemplates.cs:157`)として
  生成され、`PartResolver.ComponentKind`(Role起点解決、`PartResolver.cs:49`)により
  `Component.Kind`は常に`ElementKind.ContactNO`に解決される。`ElementKind.SelectSwitch`という
  値へは、現行の唯一の生成経路(`NetlistBuilder.cs:308`、`Component`生成箇所はコード全体で
  この1箇所のみ)から到達不可能。よって`IsConducting`のSelectSwitch専用分岐(ノッチ位置による
  排他導通)はデッドコードであり、`NetlistBuilder.cs:313`の`SwitchPosition`取り込みゲートも
  常にfalseで`SwitchPosition`は常に0のまま。
- **failure_scenario**: セレクトSW"SW1"をノッチ0(→Y1駆動)・ノッチ1(→Y2駆動)の2接点で配置し
  テスト実行、ノッチをUI上で切り替えても(`State.Positions`は正しく更新される)、電気的な
  導通判定は`ElementCatalog.IsInputControlled(ContactNO)=false`の一般分岐へ落ち
  `Energized["SW1"]||Inputs["SW1"]`のみで決まる。`SetPosition`はどちらも書き換えないため、
  デフォルトでは**ノッチ位置に関わらず常時非導通**(コイルが一切励磁されない)。かつ
  `CycleSelectSwitch`のフォールバック分岐(該当ノッチ要素が見つからない等の条件)で
  `ToggleInput`が発動した場合は、同一デバイス名の**全ノッチが同時導通**してしまう(現実の
  切替スイッチではあり得ない状態)。コミットメッセージ自身が謳う「セレクトSWノッチ判定込み」
  の通電色表示(B群修正)も、この根本問題により意味をなさない。
- 既存テストのコメント(`MainWindowViewModelTests.cs:346-353`、T-046由来)に「配置経路では
  SelectSwitch等13値に対応するPartRoleが存在せず到達不能」と既に明記されており、今回のC-1
  修正(UI状態管理側)がこの既知の構造的制約の電気的シミュレーション側への波及を見落としていた。
- **これはT-061のスコープ内(テストモード機能そのもの)の欠陥であり、既存の構造的制約(T-046)を
  理由に見送ってよい話ではないと判断する。殿確認要。**

### A-2: DeviceNameBox(機器名編集欄)経由でテストモード中に回路データを改変できる

- **file**: `src/Ecad2.App/MainWindow.xaml:572-573` + `MainWindowViewModel.cs:1721-1757`
  (`SelectedElementDeviceName`セッタ)
- `DeviceNameBox`には`CanEditDiagram`のIsEnabledバインドが無く、対応するセッタにも
  Mode/CanEditDiagram参照が皆無。`HasSelectedElement`/`SelectedElement`は`SelectedCell`から
  導出されるプロパティで、テストモード中の矢印キーによる`SelectedCell`移動は今回のA-1修正で
  明示的に許可されている。
- **failure_scenario**: (1)作画モードで機器名付き要素を配置 (2)テストモードON (3)矢印キーで
  その要素へSelectedCellを移動(A-1修正で許可済み)→DeviceNameBoxが表示・活性化 (4)機器名を
  書き換えTab等で確定→`DeviceRenamer.Rename`が本番Documentに対して実行される。今回の修正の
  主眼(A群、テストモード中の回路データ改変防止)が、この経路だけ素通しになっている。

### A-3: Enterキー新設ケースにフォーカススコープガードが無く、DeviceNameBox編集中のEnterを横取りする

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1588-1600`
- 他の同種ケース(F2/Delete/Enter配置確定)は全て`IsCanvasFocused()`を条件に含むが、新設の
  テスト通電Enterケースだけこれが無い。`Window_PreviewKeyDown`はTunnelingでWindow→子要素の
  順に発火するため、DeviceNameBox編集中にEnterを押すと本ケースが先に`e.Handled=true`で消費し、
  `DeviceNameBox_PreviewKeyDown`の`CommitDeviceNameEdit()`まで到達しない。
- A-2の修正(DeviceNameBoxをCanEditDiagramでゲート)を入れれば、テストモード中はDeviceNameBox
  自体が編集不能になり本件の実害シナリオも同時に解消される見込みだが、コード構造上の非対称性
  (`IsCanvasFocused()`欠如)自体は独立して直すべき。

### A-4: Enterキーのモーメンタリ解除に「スタックキー」の脆弱性がある(Alt+Tab等)

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:71`(`_testModeEnterPressedDevice`)+
  `:1691-1700`(`Window_PreviewKeyUp`、解除経路がここ1つのみ)
- マウス版のモーメンタリは`MouseUp`に加え`LadderCanvasHost_LostMouseCapture`(Alt+Tab等の
  外的要因によるキャプチャ喪失フェイルセーフ、開発陣が明確に意識して実装済み)を持つが、
  Enter版にはこれに相当する保険が無い。`Window.Deactivated`等のハンドラも存在しない
  (grep確認)。Win32のKeyUpルーティング仕様上、ウィンドウが非アクティブ化されるとそのウィンドウ
  はKeyUpを受け取れない。
- **failure_scenario**: テストモード中にEnterで押しボタンをON→キーを離す前にAlt+Tab等で
  ウィンドウが非アクティブ化→`_testModeEnterPressedDevice`がクリアされずTestSession内の
  `Inputs[device]=true`が固定される(押しボタンが押されっぱなしのまま回路に固着)。

### A-5: 画像挿入メニューにCanEditDiagramガードが無く、テスト用右クリックメニューが恒久的に使えなくなる

- **file**: `src/Ecad2.App/MainWindow.xaml:154` + `MainWindowViewModel.cs:1216-1221`
  (`BeginImageInsertDraft`)+ `MainWindow.xaml.cs:992-1006`(D-1修正後のガード順序)
- 「画像挿入」メニューだけ`IsEnabled="{Binding HasProject}"`のまま(他の編集系メニューは
  `CanEditDiagram`へ統一済み)。テストモード中でもクリックでき、`BeginImageInsertDraft`が
  `_imageInsertDraft`を無条件セット→`Tool`セッタの安全網(Test中はSelectDefault以外拒否)で
  `Tool.Mode`はSelectのまま→`HasAnyDraft`がtrue固定→今回のD-1修正で`HasAnyDraft`ガードが
  `Mode==Test`分岐より前に来るよう順序変更されたため、以後の右クリックは全て早期returnし
  テスト用コンテキストメニュー(接点手動強制ON/OFF)に二度と到達できない。
- **さらに確認**: Escapeキーによる復旧も効かない(`Tool.Mode==PlaceImage`条件の分岐に
  ヒットしないため)。唯一の復旧経路はテストモードをOFF→ONし直すことのみ。
- D-1本体の前提コメント(「Modeセッタで既にドラフトをクリアするので実害は塞がった」)が、
  「画像挿入メニュー自体がCanEditDiagram非ガードのまま」という別の穴を見落としていたことによる。

### A-6(低頻度): マウスとEnterキーの同時押下で片方の解除がもう片方の保持状態を無視する

- **file**: `MainWindowViewModel.cs:2053-2056`(`TestModeRelease`、保持カウント無し)
- 同一デバイスをマウスとEnterで同時に押した場合、片方を離すと無条件で`Inputs[device]=false`に
  なり、もう片方がまだ押されたままでも見た目上OFFに戻る。発生条件は狭いが構造的に存在する。

---

## B群: cleanup/efficiency(全てCONFIRMED)

- **B-1**: `DiagramRenderer.cs:974-978`の`on`計算で`energized!`(null許容演算子)と
  `elementConducting is not null`(明示チェック)という異なる流儀が同一三項式内に混在、
  「両者は常に同時に非null」という前提をコードの書き方自体が体現していない。可読性の問題。
- **B-2**: `OnRealtimeTick`(100ms間隔)が`TestSession.Tick`(内部でNetlistBuilder.Build+
  Evaluateを実行、新規`ElementConducting`も計算)を呼ぶが、この結果は`State.Energized`の
  持ち越し以外一切使われず捨てられる。直後の`RedrawCanvas`→`DiagramRenderer.Render`が
  独立にもう一度Build+Evaluateを行う。100msごとに実質同一計算が2回走り、うち1回
  (TestSession側の新規ElementConducting計算)は完全に無駄。既存のE-2(未対応)にコストが
  上乗せされている。
- **B-3(Altitude)**: `DeviceClass`(BOM分類目的のenum、P-020確立)がテストモードの動作分岐
  (TestModePress等)にも流用され、粒度不足(Relay=ContactNO/NC/Coil/ContactorMain3Pを一括り)
  により今回のC-2/C-3のようなバグを引き起こした。対症療法として`IsRealContactElement`ヘルパー
  を追加したが、根本(enumの目的混在)は解消されておらず、将来3件目の呼び出し箇所でも同型の
  バグが再発しうる。
- **B-4(Altitude、軽微)**: 矢印キーcaseのみ`if (!CanEditDiagram) {...; break;}`という
  本体内if/break idiomで、同一コミットの他9箇所は`when ... && CanEditDiagram`というcaseガード
  idiomを踏襲している。1箇所だけの逸脱で一貫性が目視依存になっている。

---

## パターン再発台帳との照合

A-2/A-3/A-5は、いずれもPR-12候補(新規上位モード導入時のマウス/キーボード経路対応漏れ)と
同型——「CanEditDiagram統合」という横展開自体は行ったが、統合対象の洗い出しが不完全
(DeviceNameBox・画像挿入メニューが漏れた)。台帳のPR-12候補は初出T-061(937e99a)のみだったが、
**同一タスクの2周目でも同型の漏れが再発**しており、「新規ゲート導入時は全入力経路(ツールバー・
キーボード・メニュー・テキストボックス編集欄)を機械的に列挙してから適用する」という横展開手順の
必要性を示す実例として台帳へ追記すべきと判断する(家老確認のうえ追記予定)。

## 不明点

なし(全指摘、検証エージェントが行番号付きで裏付け済み)。

## 派生提案

なし。
