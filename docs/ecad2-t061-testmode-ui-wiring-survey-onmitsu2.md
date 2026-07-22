# T-061 テストモード機能のUI結線 設計調査

調査者: 隠密2　最終更新: 2026-07-11

殿裁定2026-07-11によりApproved（gated、P-051起票）。着手順序・スコープは未着手（キュー整理待ち）、
本調査は着手前の設計叩き台。コード変更は伴わない調査のみ。

---

## DoD(1): TestSession/NetlistのAPI面の棚卸し（GuiEcad実物照合込み）

`src/Ecad2.Core/Simulation/TestSession.cs`・`Netlist.cs`をGuiEcad対応ファイル
（`GuiEcad.Core/Simulation/TestSession.cs`・`Netlist.cs`）と直読比較した結果、**両ファイルとも
namespace以外は全文一致**（T-007移植そのまま、T-060調査時のCrossReference.cs/Document.csと
同じパターン）。

### 公開API（そのまま利用可能）

- `TestSession(Sheet sheet, PartLibrary? lib = null)` — シート単位でセッションを生成
- `Evaluate()` — 現在の入力＋持ち越した励磁状態で再評価（内部で`NetlistBuilder.Build`→`Evaluator`）
- `SetInput(string device, bool on)` — 入力設定＋即時評価
- `ToggleInput(string device)` — 入力トグル＋即時評価
- `SetPosition(string device, int notch)` — セレクトSWノッチ位置設定＋即時評価
- `Tick(double dt)` — 実時間`dt`秒経過、励磁中タイマの経過時間を加算・消磁されたタイマをリセット
  → 評価
- `IsEnergized(string device)` / `IsOscillating` / `IsCyclic` — 評価結果の参照
- `State`（`SimState`：`Inputs`/`Energized`/`Positions`/`TimerElapsed`の4辞書）
- `Result`（`EvalResult?`：直近評価結果、`Status`に`Diverging`/`Cyclic`等）

### 描画連携（Core層で既に対応済み）

`src/Ecad2.Core/Rendering/DiagramRenderer.cs:151-167`の`Render(...)`は`SimState? sim = null`引数を
既に持ち、`sim`が非nullなら`Evaluator`で評価して`powered`（通電ネット集合）・`energized`
（機器励磁状態）を算出し描画に反映する経路が**既に実装済み**。具体的な色分けの値（何色をどう使うか）
は本調査では未確認（DoDの主眼=結線可否の確認に留めたため。実装着手時に確認要）。

**結論：Core層は完全に移植済みで、App層からの呼び出しを新規に書くだけでよい。Core層の追加実装は
不要**（T-060のPDF出力調査と同型の結論）。

## DoD(2): GuiEcadのテストモードUI実物照合

`C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.xaml.cs`・`MainPage.Pointer.cs`・
`MainPage.ContextMenu.cs`を直読。

### モード切替UI

- ツールバー上の`ToggleButton`（`TestModeBtn`、`MainPage.xaml:179-180`）の`Click`イベント
  `OnTestModeToggle`（`MainPage.xaml.cs:342-360`）が中心。**キーボードショートカットの割当は
  一切ない**（`MainPage.KeyboardMode.cs`にF5等のテストモード切替キーは存在せず、マウスクリック
  専用の操作面と判明）。
- 切替時の処理：`_testSessions`（シートごとのセッション辞書）をクリアして現在シート分のみ再生成、
  進行中のドラッグ/パン状態とポインタキャプチャを破棄、タイマパネル（`TimerTickPanel`）の
  表示/非表示切替、実時間タイマの開始/停止、ステータスバー更新、**ツールバー背景色を
  `AppTestModeBrush`リソースに変更**（作画モードへ戻すと`ClearValue`で既定に戻す）。
- シートをまたいでも状態が保持される設計（`Dictionary<Sheet, TestSession> _testSessions`）だが、
  **テストモード自体をOFFにすると全シート分クリアされる**（再度ONにすると全シートまっさらに戻る）。

### 通電表示・入力操作

- 押しボタン（`PushButtonNO`/`PushButtonNC`）：左クリック押下で`SetInput(dev, true)`、
  ボタンを離す（PointerReleased/PointerCaptureLost）で`SetInput(dev, false)`——モーメンタリ動作
  （押している間だけON）を再現。
- セレクトスイッチ：クリックで`CycleSelectSwitch`（ノッチを順送り、詳細未読）。
- その他の機器：クリックで`ToggleInput`（保持型トグル）。
- 空欄クリックは作画モードと同様にパン開始（テストモードでもキャンバス移動は可能）。

### 右クリック分岐（接点の手動強制）

`MainPage.ContextMenu.cs:150-200`（`ShowContactContextMenu`）：**接点（ContactNO/ContactNC）限定**
の右クリックメニューで、「手動でON（強制閉路）」「手動を解除（シミュレーション依存に戻す）」の
2択のみ。実体は`TestSession.SetInput(dev, true/false)`の呼び出し（新規APIは不要、既存メソッドの
コンテキストメニュー経由呼び出しに過ぎない）。作画モード用の右クリックメニュー（`ShowDrawingContextMenu`）
とはモードで完全に切り替え。

### タイマ実時間計時パネル

`DispatcherTimer`（100ms間隔）＋`Stopwatch`で実時間を計測し、`TestSession.Tick(dt)`へ流し込む
（`StartRealtimeTimer`/`StopRealtimeTimer`/`OnRealtimeTick`）。一時停止ボタン（`TimerPauseBtn`）で
`DispatcherTimer`を止める/再開する（再開時は経過起点をリセットし時間飛びを防止）。

### ステータス表示との連携

`UpdateTestStatus()`は、モード表示文言の切替に加え、**テストモード中は毎回DRC（設計チェック）も
再実行**して短絡・クロスリファレンス・型不整合・縦交差・負荷到達可能性・直列コイル等の警告を
ステータスバーに反映する（作画モードのDRC実行契機とは別に、テストモード中は評価更新のたびに
自動実行される設計）。

## DoD(3): ecad2で結線に必要な新規実装の洗い出し

### design-brief・他社調査との突合

`docs/ecad2-ui-ux-design-brief.md:63`は「作画/テストモードの明確な視覚的フィードバック（色変化・
右クリックメニュー分岐）」を**GuiEcadから残すべき良い操作パターン**として明記——今回確認した
ツールバー背景色変化・接点右クリック強制ON/OFFのメニューがまさにこれに該当し、**踏襲が既定方針と
一致する**。

`docs/ecad2-ladder-reference-systems-survey-onmitsu.md`3節（他社通電表示調査）との突合：
LDmicro（通電=明るい赤/非通電=グレー）・CODESYS（TRUE=青太線/FALSE=黒太線）・OpenPLC
（通電=緑ハイライト）・CLICK PLC（true=青ハイライト）と、業界で色分けの流儀は割れている
（統一基準なし）。ecad2独自の配色を決める必要があり、これはUI/UX分岐として殿確認が要る
（下記「開かれた論点」参照）。

### 【重要な設計指針の既存先例】ToolMode enumによる状態一元化

`src/Ecad2.App/ViewModels/ToolState.cs`のコメントに明記：GuiEcadは配置ツールの状態を
「複数のboolフラグの束（`_placeKind`/`_placePartId`/`_placeOrient`/...）」として実装し、
「フラグの取りこぼし（クリア漏れ・排他崩れ）バグの温床になった」（design-brief 3節#1
「状態管理の分散」）。ecad2はこれを教訓に`ToolMode` enum + `ToolState` record に**最初から
一元化**する設計を既に採用している。

**GuiEcadのテストモードも`private bool _testMode`という単純フラグ**（`MainPage.xaml.cs:72`）
であり、ToolMode一元化以前の設計思想のまま。ecad2で結線する際、「作画モード/テストモード」の
上位区分を`_testMode`相当のbool一発で実装するか、ToolStateとは独立の上位enum
（例：`AppMode { Drawing, Test }`）としてMainWindowViewModelに一元管理させるかは、design-brief
の既存原則（状態管理の一元化を最優先課題とする）に照らすと**後者が既定方針と整合的**と考えられる
（隠密所感、断定ではない。設計判断は着手時に確定させる必要がある）。

### 新規実装が必要な箇所（App層）

1. **モード切替の状態管理**——上記の一元化方針を踏まえたenum/プロパティの新設。
2. **モード切替UIの結線**——メニュー「テストモード(_M)」・対応するツールバーボタン
   （現状の配置と要否は着手時に検討、GuiEcadはツールバーの`ToggleButton`のみでキー割当なし）。
3. **通電表示の色分け設計**——Core層の`sim`引数受け渡しは対応済みだが、具体的な配色は
   ecad2独自に決める必要がある（DrawingTheme拡張が必要になる可能性）。
4. **入力操作の結線**——押しボタンのモーメンタリ動作（Press/Release）・セレクトSWのノッチ送り・
   その他機器のトグル、いずれもキャンバスのポインタイベントハンドラへの新規分岐追加。
5. **右クリックメニュー（接点の手動強制）**——WPFの`ContextMenu`で同等機能を新設
   （`TestSession.SetInput`の呼び出しのみ、Core側API変更は不要）。
6. **実時間タイマパネル**——`DispatcherTimer`は.NET標準機構でWPFでもそのまま使える
   （Win2D/WinUI3固有の要素はなし、移植コストは低い見込み）。
7. **ステータスバー連携**——モード表示文言・DRC自動再実行との統合（既存のDRC実行経路
   `OutputPanel.RunDrcCommand`との関係整理が必要）。

## DoD(4): 段階導入の叩き台（最小の第一歩）

「テストモードに入れる」ことだけを最小達成するなら、以下の順で段階導入が可能と考える：

1. **第一歩**：モード切替の状態（上記enum/プロパティ）＋メニュー/ツールバーのCommand結線のみ。
   モードに入ると`TestSession`を生成し`Evaluate()`を1回呼ぶが、**通電表示・入力操作・右クリックは
   まだ未実装**でも「モードに入って抜けられる」ことは確認できる（DRC再評価・ステータス表示は
   既存の仕組みに乗せやすいので同時実装でも大きな追加コストにはならない）。
2. **第二歩**：通電表示の色分け（DrawingTheme拡張＋`Render`呼び出し時に`sim`を渡す配線）。
3. **第三歩**：入力操作（押しボタン・トグル・セレクトSW）の結線。
4. **第四歩**：右クリック強制ON/OFFメニュー。
5. **第五歩**：実時間タイマパネル（タイマ命令を使う回路がなければ後回しにしても実害は小さい）。

この段階分けは隠密の所感であり、実際の着手順は家老・侍判断に委ねる。

## DoD(5): 開かれた論点（UI/UX分岐、着手時に殿確認が必要）

1. **【重要・既存の表示矛盾】F5キーの重複**：`src/Ecad2.App/MainWindow.xaml:134`のメニュー
   「テストモード(_M)」には`InputGestureText="F5"`と表示されているが、**F5は既にツールバーの
   「a接点配置」（`src/Ecad2.App/MainWindow.xaml.cs:760`付近、`Key.F5 when noModifier`）で
   実利用中**。テストモード切替に実際にF5を結線すると衝突する。GuiEcad自体はテストモード切替に
   キーボードショートカットを持たない（マウス専用）ため、ecad2で新規にキーを割り当てる場合は
   F5以外の候補（例：Ctrl+F5、Ctrl+T等）を検討する必要がある。**これは実装着手前に必ず解消すべき
   表示上の矛盾**（メニュー文言修正だけで済むか、実際にキー割当を新設するかも含め殿確認要）。
2. **モード管理の設計方式**：GuiEcad同型の単純bool踏襲か、design-briefの状態一元化方針に沿った
   上位enum新設か（上記「新規実装の洗い出し」参照）——UI/UX直結ではないが実装方針の分岐点。
3. **通電表示の配色**：他社実装がLDmicro（赤/グレー）・CODESYS（青/黒）・OpenPLC（緑）等で割れて
   おり、ecad2独自の配色を決める必要がある。既存のOrangeRet系選択ハイライトとの区別も要考慮。
4. **モード切替の場所・見た目**：GuiEcadはツールバーの`ToggleButton`＋背景色変化。ecad2で同型に
   するか、他の見せ方（例：ステータスバー常時表示との連携強化）を検討するかは着手時に殿確認。
5. **シートまたぎの状態保持方針**：GuiEcadは「モードOFF→全シート分クリア」。この挙動を踏襲するか、
   モードOFF後も状態を保持し次回ON時に復元するかは仕様判断（GuiEcadの挙動を継承するのが自然だが
   明示確認が望ましい）。
6. **タイマパネルの要否・実装時期**：タイマ命令（Timer要素）を使わない回路では不要な機能のため、
   段階導入の後回し候補になりうる（要殿確認）。

## 出典一覧

- `src/Ecad2.Core/Simulation/TestSession.cs`・`Netlist.cs`（Read）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs:151-167`（Read）
- `src/Ecad2.App/ViewModels/ToolState.cs`（Read）
- `src/Ecad2.App/MainWindow.xaml:134`（前セッション含め既読範囲）
- `src/Ecad2.App/MainWindow.xaml.cs:632-806`付近（Grep行番号確認、F5キー処理の実在確認）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.Core/Simulation/TestSession.cs`・`Netlist.cs`（Read）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.xaml.cs:85-471`（Read）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Pointer.cs:140-229`（Read）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.ContextMenu.cs:148-200`（Read）
- `docs/ecad2-ui-ux-design-brief.md:50-74`（Read）
- `docs/ecad2-ladder-reference-systems-survey-onmitsu.md:183-272`（Read）
- `docs/todo.md` T-061節（Read）

## 派生提案の有無

- **気づき（範囲外）**：`MainWindow.xaml:134`のF5表示矛盾は、T-061着手を待たず**今すぐ気づく人が
  誤操作しうる表示バグ**（メニューを見た人がF5を押すとテストモードでなくa接点配置が起きる）。
  軽微だが実害があるため、`docs/proposed.md`起票の要否は家老判断に委ねる。

## 不明点

- `CycleSelectSwitch`の詳細実装（セレクトSWのノッチ送りロジック）は未読了。
- Core層`DiagramRenderer`の通電表示の具体的な色定義（`powered`/`energized`をどう描画に反映するか
  の詳細）は本調査では未確認——結線調査の主眼（呼べば動くかの確認）は満たしているが、実装着手時に
  改めて詳細確認が必要。
- GuiEcadの`ShowDrawingContextMenu`（作画モード用右クリックメニュー）の中身は未読了（テストモードの
  スコープ外のため意図的に割愛）。
