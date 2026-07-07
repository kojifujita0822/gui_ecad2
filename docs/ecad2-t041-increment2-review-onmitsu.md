# T-041増分2 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット`be9c15f`（`feat(app): T-041増分2 - 縦コネクタ手動記入
> (sF9+矢印キー+Enter/Esc)`）。家老指定観点(1)〜(5)＋`code-review`スキル（Reuse/Efficiency/
> Altitude/Conventions角度、1エージェント）併用。

---

## 結論：**要修正（中〜高）。キー操作フロー自体は原案通り正しく実装されているが、「記入中に
シートが切り替わる」経路が未対策で、制御回路限定という不変条件・グリッド範囲の両方を迂回しうる。
加えてマウスクリックとの相互作用に軽微な取りこぼしがある**

家老指定観点(1)(2)は原案・既存Escape多層構造と正しく整合している。観点(3)（制御回路シート限定
ガード）は**記入開始時は正しいが、記入完了（確定）時に再検証されない**という抜け穴をCONFIRMED。
観点(4)（増分1選択モデルとの相互作用）にも1件、マウスクリック絡みの取りこぼしをCONFIRMED。
観点(5)便乗変更なし。`code-review`併用で同じ「シート切替」根本原因に基づく副次的リスク（範囲外
座標での確定）、および増分1から続く軽微なEfficiency/Reuse/Altitude所見を検出した。

---

## 対象差分

`git show be9c15f`で確認。`MainWindow.xaml.cs`+78/-5、`MainWindowViewModel.cs`+77、
`LadderCanvas.cs`+26/-1、新規テスト`ConnectorDraftTests.cs`+154（9件）。

---

## 家老指定観点の検証

### (1) sF9キー操作フローが原案通りか —— **一致を確認**

`docs/ecad2-t041-key-flow-proposal-samurai.md`3節と照合：始点＝`SelectedCell`（行・整数列境界）、
Up/Downで終点行を伸縮、Left/Rightで列境界1.0刻み・Shift+Left/Rightで0.5刻み（原案「セル中央=X.5
境界」と一致）、Enterで確定（範囲0は確定せず案内）、Escで取消・要素非生成——いずれも原案の記述と
実装（`MainWindowViewModel.cs`の`BeginConnectorDraft`/`MoveConnectorDraftRow`/
`MoveConnectorDraftColumn`/`ConfirmConnectorDraft`/`CancelConnectorDraft`、
`MainWindow.xaml.cs`の`AdjustConnectorDraft`）が一致することを確認した。F9（制御回路シートでは
「当面未使用」と原案が明記）に対応するキーバインドが追加されていないことも原案通り。

### (2) 記入中プレビュー・Enter確定/Esc取消（層2'）とEscape多層構造の整合 —— **問題なし**

`MainWindow.xaml.cs`のEscapeハンドラは、層2（`Tool.Mode==PlaceElement`→選択モードへ）と層3
（選択解除）の間に「層2'」として`Tool.Mode==PlaceConnector`の分岐を挿入しており、既存の
if/else-ifチェーンの相互排他性を壊さず自然に組み込まれている。`CancelConnectorDraft()`は
`SelectedCell`に触れず（T-021分岐Aの「ツール保持」流儀と整合）、「1回のEscは1層だけ」の原則も
保たれている。

### (3) 制御回路シート限定のガード —— **開始時は機能するが、CONFIRMED: 確定時に再検証されない**

`TryBeginConnectorDraft`（`MainWindow.xaml.cs`）は`sheet.MainCircuit`を見て記入開始をブロックし、
正しく機能する。しかし**記入開始後、確定（Enter）までの間にシートが切り替わるケースが未対策**
である。

`MainWindowViewModel.CurrentSheetIndex`のsetter（100-117行目）は`SelectedCell`/`SelectedConnector`
のクリアのみを行い、`Tool`・`_connectorDraft`には一切触れない。一方`ConfirmConnectorDraft()`
（`MainWindowViewModel.cs`）は`CurrentSheet`（＝呼び出し時点でアクティブな**どのシートでも**）に
対して無条件に`sheet.Connectors.Add(...)`する。

**再現手順**：制御回路シートでSelectedCellを選びsF9（記入開始、`Tool.Mode=PlaceConnector`）
→Esc/Enterを押さず、左パネルのシートナビゲーション（マウスクリック、キャンバスのフォーカス
スコープ外）またはDRC出力パネルのジャンプで**別シートへ切替**→`Tool.Mode`は`PlaceConnector`の
まま、`_connectorDraft`も旧シート由来の値（AnchorRow/CurrentRow/Column）を保持したまま残留する
→この状態でEnterを押すと、`ConfirmConnectorDraft()`は**新しく表示中のシート**へ
`VerticalConnector`を追加する。

**実害**：(a) 新シートが主回路（`MainCircuit==true`）であっても、確定時に`MainCircuit`の再
チェックが無いため、**「縦分岐線は制御回路シート限定」という設計上の不変条件そのものが破られる**
（主回路シートに`VerticalConnector`が混入する）。(b) `ConfirmConnectorDraft()`は
`sheet.Grid.Rows`/`Columns`へのクランプを一切行わない（クランプは`MoveConnectorDraftRow`/
`MoveConnectorDraftColumn`が呼ばれた時にのみ効く場当たり的な実装で、シート切替後に再度矢印キーを
押さなければ古いシートの行数・列数を前提にした値のまま確定してしまう）——旧シートの方が大きい
グリッドだった場合、新シートのグリッド範囲外の`TopRow`/`BottomRow`/`Column`を持つ
`VerticalConnector`が生成されうる（P-022/P-024で既に経過観察中の「範囲外設置」系の技術的負債と
同系統の新規実例と考える）。

`ConnectorDraftTests.cs`の新規9件にはシート切替を絡めたテストが1件も無く、この経路は現状未検証
のまま埋め込まれている。

### (4) 増分1の選択モデル（SelectedConnector）との相互作用 —— **1件、マウスクリックとの取りこぼしをCONFIRMED（(3)より軽微）**

好ましい相互作用（設計通り機能）：`TryBeginConnectorDraft`は`SelectedCell is null`を要求するため、
`SelectedConnector`が選択中（増分1の排他設計により`SelectedCell`は必ずnull）の状態ではsF9が
「配置するセルを先に選択してください」で弾かれ、記入開始時に`SelectedConnector`との共存は構造的に
起こらない。

**取りこぼし**：`LadderCanvasHost_PreviewMouseLeftButtonUp`（`MainWindow.xaml.cs`222行目付近）の
接続コネクタ選択分岐は`_viewModel.Tool.Mode == ToolMode.Select`の場合のみ有効で、それ以外
（`PlaceConnector`＝記入中を含む）は無条件に`_viewModel.SelectedCell = LadderCanvasHost.ToGridPos
(position); TryPlaceActiveTool();`という「素通し」の分岐へ落ちる。`TryPlaceActiveTool()`は
`Tool.Mode != PlaceElement`なら即returnするため実際に要素が配置されることは無いが、
**`SelectedCell`だけはマウスクリック位置へ書き換わってしまう**。`_connectorDraft`はこの変化と
無関係に元の値を保持し続けるため、記入中に誤って（または好奇心で）キャンバス上をクリックすると、
画面上ではオレンジのセル矩形ハイライトがクリック位置へ移動する一方、記入中の破線プレビューは
元の位置に残ったままになり、続く矢印キー操作は「見た目上どこにも表示されていないはずの」元の
アンカーに対して効き続ける、という視覚的な混乱を生む。データ破損や誤配置には至らないため(3)より
severityは低いが、記入モード中はマウスクリックを無視する（またはEsc相当の取消として扱う）ガードが
無い点は取りこぼしと判断する。

### (5) 便乗変更なし —— **確認済み**

---

## `code-review`スキル併用の追加所見

### 所見C（Efficiency、中確度）: 矢印キー1回ごとにフルネットリスト再構築

`MoveConnectorDraftRow`/`MoveConnectorDraftColumn`は呼ばれるたび
`OnPropertyChanged(nameof(ConnectorDraftPreview))`を発火し、`RedrawCanvas()`→
`DiagramRenderer.Render`→`NetlistBuilder.Build(sheet, library)`（Union-Find全走査）がフル実行
される。`sheet.Elements`/`Connectors`/`WireBreaks`は記入中一切変化していないため、この再構築は
本質的に無駄な計算であり、矢印キーのタイプマティックリピート（数十ms間隔）で連打された場合、
要素数の多いシートで体感遅延が生じるリスクがある（実測は行っておらず、現状のグリッド規模での
断定はできない）。増分1レビューで指摘した「二重RedrawCanvas」と同根の問題で、記入操作の主要な
インタラクション経路（矢印キー連打）に位置するため、増分1の単発クリック時より発生頻度が高い点が
異なる。

### 所見D（Reuse、severity低）: 縦コネクタの線分描画ロジックが増分1と増分2で重複

`LadderCanvas.cs`の選択ハイライト描画（増分1）と記入中プレビュー描画（増分2、今回追加）が、
`geo.X(...)`/`geo.YRow(TopRow/BottomRow)`から`Point`を組み立てる同型のコードをそれぞれ独立に
書いている（Penとゼロ長特例のみ差分）。増分1レビュー所見B（`HitTestConnector`/`ToGridPos`の
DIP→mm変換重複）と同種のパターンが今回also再発しており、共通ヘルパーへの抽出機会と考える。

### 所見E（Altitude、増分1所見Aの継続）: ViewModelの作業中状態が3→4個目に増殖

`Tool`/`SelectedCell`/`SelectedConnector`に続き`_connectorDraft`が4つ目の「記入中/選択中」状態
として追加されたが、各状態の破棄責務は「`CurrentSheetIndex`セッターの手書きクリアリスト」と
「Escapeハンドラのswitch分岐」に個別に散らばっており、統一的な「対話モードをリセットする単一
入口」が無い。増分1では`SelectedCell`/`SelectedConnector`の排他管理漏れが4箇所で実際に発生し、
「`SelectedCell`のsetterを唯一のクリア入口にする」という設計転換で解消した経緯があるが、今回
`_connectorDraft`には同種の対処が適用されておらず、観点(3)のCONFIRMED実害はまさにこの型の
問題が再発した実例である。増分3（`WireBreak`横展開）で`_wireBreakDraft`等が同じパターンで
追加されれば、シート切替時のクリア漏れが今後も個別に発生し続ける懸念がある（増分1レビュー所見Aの
繰り返しであり、severityは高まりつつあると評価する）。

### Conventions —— **明確な違反は無し**

---

## 侍への申し送り（修正方針、参考）

- 観点(3)のCONFIRMEDは、`CurrentSheetIndex`のsetterに`_connectorDraft = null;
  Tool = ToolState.SelectDefault;`（もしくは`CancelConnectorDraft()`相当の呼び出し）を追加する
  ことで、増分1の`SelectedCell`setter集約と同型の対処で解消できると考える。あわせて
  `ConfirmConnectorDraft()`自身にも`sheet.MainCircuit`・`Grid.Rows`/`Columns`範囲の防御的
  再チェックを入れておくと、将来同種の「記入中にシートが変わる」経路が別途生まれても二重に
  守られる。
- 観点(4)のマウスクリック取りこぼしは、`LadderCanvasHost_PreviewMouseLeftButtonUp`で
  `Tool.Mode==PlaceConnector`の場合はクリックを無視する（`e.Handled`相当）か、
  `CancelConnectorDraft()`を呼ぶかのいずれかで解消できると考える（UI/UX的にどちらが自然かは
  軽微な判断のため侍の裁量範囲と考える）。
- 所見E（状態管理の統合）は増分1で申し送り済みの検討課題（増分3着手前）と同じ論点であり、今回の
  観点(3)の実害はその論点の具体的な裏付けになったと考える。増分3着手前の検討時に本レビューも
  合わせて参照されたい。

---

## 出典・参照

- 対象コミット`be9c15f`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（100-117行目`CurrentSheetIndex`、
  `BeginConnectorDraft`/`MoveConnectorDraftRow`/`MoveConnectorDraftColumn`/
  `ConfirmConnectorDraft`/`CancelConnectorDraft`）
- `src/Ecad2.App/MainWindow.xaml.cs`（`TryBeginConnectorDraft`、`AdjustConnectorDraft`、
  Escape層2'、`LadderCanvasHost_PreviewMouseLeftButtonUp`）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`SelectedSheet`→`CurrentSheetIndex`
  経由の切替経路）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`ConnectorDraftPen`、プレビュー描画）
- `tests/Ecad2.App.Tests/ConnectorDraftTests.cs`（新規9件、シート切替絡みのケース無しを確認）
- `docs/ecad2-t041-key-flow-proposal-samurai.md`（3節、キー操作フロー原案）
- `docs/ecad2-t041-increment1-review-onmitsu.md`・`-2.md`（所見A/B、状態管理集約の経緯）
- `code-review`スキル（Reuse/Efficiency/Altitude/Conventions角度、1エージェント、高確度1件・
  中確度2件を検出、うち高確度1件は隠密自身の独立追跡と一致）
