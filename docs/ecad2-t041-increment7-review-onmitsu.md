# T-041増分7第1弾 静的レビュー（隠密）

> 2026-07-08 隠密レビュー。対象コミット`a471260`（`feat(app): T-041増分7(第1弾) VerticalConnector
> のドラッグ移動・端点リサイズ+キーボード等価操作`）。家老指定観点(1)〜(4)＋`code-review`スキル
> （line-by-line diff scan・removed-behavior/cross-file・品質(Reuse/Simplification/Efficiency/
> Altitude/Conventions)角度、3エージェント並行）併用。実測検証（`dotnet test`）も併用した。

---

## 結論：**要修正。最重要はCONFIRMED（2エージェント独立発見）の構造的懸念——ドラッグ状態
(`_draggingConnector`)が既存の「クロスカット選択クリア」パターンの外に置かれている。加えて
外部データ由来のクラッシュ可能性、マウスキャプチャ喪失時の状態不整合、および横展開時の
モグラ叩きが既に進行中であるという重要な警告あり**

家老指定観点(1)(4)は問題なし。観点(2)(3)は通常の対話的操作の範囲では一貫しているが、
`code-review`の複数角度が、既存の`_connectorDraft`/`_freeLineDraft`が確立していた保護パターン
から新設の`_draggingConnector`が漏れているという構造的な穴を独立に発見した。

---

## 家老指定観点の検証

### (1) 実機確認で発見・修正した2件のバグが正しく解消されているか

**①`SelectedEndpointIsStart`のsetter通知漏れ —— 解消を確認**

`MainWindowViewModel.cs:212-224`で`if (SetProperty(...)) OnPropertyChanged(nameof
(SelectedEndpointDisplay));`という形に修正されている。単純な派生プロパティ通知の追加で、
問題は見当たらない。

**②Escキャンセル後のReleaseMouseCaptureタイミング —— 通常経路は解消、ただし外的要因での
キャプチャ喪失には別の穴が残る（下記(3)所見C参照）**

`_connectorDragConsumedByEscape`フラグで、Esc押下時はキャプチャを維持し実際のMouseUpで
無害化してから解放する設計（`MainWindow.xaml.cs:246-260,282-291`）を確認した。通常の
「Esc→そのままマウスを離す」経路では正しく機能する。ただし、この経路はDispatcher非依存で
`ConnectorDragAndResizeTests.cs`の対象外（ViewModel単体テストの方針上、意図的に対象外）の
ため、単体テストでは検証されていない。

### (2) ドラッグ経路とキーボード経路が同じ状態・同じMarkDirty()呼び出しに収束しているか

基本フロー（値が実際に変化した場合のみ`MarkDirty()`）は一貫していることを確認した
（`ConfirmDragConnector`/`MoveSelectedConnector`/`MoveSelectedConnectorColumn`/
`ResizeSelectedConnectorEndpoint`いずれも同型）。ただし下記所見A・Dで指摘する経路間の
競合・不整合が残る。

### (3) Top<Bottom不変条件が全操作経路で一貫して守られているか

通常のアプリ内操作（記入・ドラッグ・キーボード操作）の範囲では、ドラッグ端点リサイズ
（`c.BottomRow - 1`/`c.TopRow + 1`でクランプ）・キーボード端点伸縮
（`ResizeSelectedConnectorEndpoint`、同型のクランプ）・記入確定（`ConfirmConnectorDraft`、
`topRow == bottomRow`ならreturn false）のいずれも一貫して基準（差分最小1）を守っている。
ただし下記所見Bで指摘する外部データ由来のケースは対象外。

### (4) 96件のregression維持 —— 実測で確認

`dotnet test src/Ecad2.sln`実行、Core14件・App82件、計96件合格。侍の報告と一致。

---

## `code-review`スキル併用で判明した所見

### 所見A（CONFIRMED・重大、2エージェント独立発見）: `_draggingConnector`が既存の
「クロスカット選択クリア」パターンの外に置かれている

既存の記入中ドラフト（`_connectorDraft`/`_freeLineDraft`）は、`SelectedCell`のsetter・
`ReplaceDocument`の両方で`ClearConnectorDraftIfAny()`/`ClearFreeLineDraftIfAny()`により
無条件クリアされる「確立済みパターン」を持つ。今回追加された`_draggingConnector`（および
対になる`_draggingConnectorIsEndpoint`等の一時フィールド、Viewの`_connectorDragStarted`/
`_connectorDragConsumedByEscape`）だけがこのパターンの外にあることを、独立した2エージェント
がそれぞれ発見し、自分でもコードトレースで到達可能性を確認した。

**具体的な3経路（コードトレースで到達可能性を確認済み）**：

1. **Delete経由**（`MainWindowViewModel.cs:402`、最も現実的）：縦コネクタをマウスでドラッグ
   中（ボタン押下保持・キャプチャ中）に、もう一方の手でDeleteキーを押すと
   `DeleteSelectedConnector()`が`sheet.Connectors`から実体を削除し`SelectedConnector = null`
   にするが、`_draggingConnector`は削除済みの実体を参照したまま残る。以降の`MouseMove`は
   リスト外に取り残されたオブジェクトを書き換え続け、`MouseUp`の`ConfirmDragConnector()`も
   既に削除済みのオブジェクトに対し`MarkDirty()`判定を行う。Escを押せば`CancelDragConnector()`
   が孤立オブジェクトに無意味に発火しつつEscapeレイヤーを消費し、本来届くべき選択解除層へ
   落ちない。

2. **シート切替経由**（`MainWindowViewModel.cs:126`）：ドラッグ中にShift+Tab
   （`MainWindow.xaml.cs:426-431`の`CyclePanelFocus()`、`IsDraggingConnector`を見ずに常時
   発火することを確認済み）でシートナビゲーションパネルへフォーカス移動し矢印キーでシートを
   切替えると、`CurrentSheetIndex`のsetターが`SelectedConnector = null`にするが
   `_draggingConnector`は旧シートの実体を参照したまま残る。

3. **ReplaceDocument経由**（`MainWindowViewModel.cs:1032-1084`）：ドラッグ中にAlt系メニュー
   アクセスキー等で新規/開くを実行すると、`ReplaceDocument`は`SelectedConnector = null`他を
   明示的にクリアするが（1049行目）、`_draggingConnector`のクリアが無い。新しく開いた
   ドキュメント（`IsDirty = false`で始まるはず）が、直後のMouseUpで理由なく「未保存」表示に
   なる。

いずれも即座のクラッシュやデータ破損には至らないが、「削除済み/切替後/開き直した対象への
無意味な書き込み」「予期しないMarkDirty発火」「Escapeレイヤーの誤消費」という一貫性の
乱れを生む。このプロジェクトでは`_connectorDraft`が同種の問題（増分2隠密レビュー所見E、
シート切替でのクロスリーク）を過去に経験し「setter集約による無条件クリア」で解消した
経緯があり、今回`_draggingConnector`が同じ轍を踏んでいる。

### 所見B（CONFIRMED・中、外部データ起因）: clamp前提の未検証によるクラッシュ可能性

`VerticalConnector`（`Ecad2.Core/Model/Element.cs:122-128`）は`TopRow`/`BottomRow`に
バリデーションを持たない単純なauto propertyであり、`GcadSerializer.Deserialize`
（`Ecad2.Core/Persistence/GcadSerializer.cs:29-37`）も`JsonSerializer.Deserialize`を素通し
するのみで大小関係を検証しない。手編集された`.gcad`ファイルや旧バージョン由来のデータで
`TopRow >= BottomRow`のコネクタが読み込まれた場合、それを選択してドラッグ端点操作や
Tab+Shift+矢印での伸縮を行うと、`Math.Clamp(x, 0, c.BottomRow - 1)`や`Math.Clamp(x,
c.TopRow + 1, Rows - 1)`で`min > max`となり`ArgumentException`が未処理のまま投げられ
クラッシュする。`ConfirmConnectorDraft`（579行目）は`topRow == bottomRow`なら`return false`
とする二重防御を持つが、ドラッグ・キーボード伸縮側にはこの防御がない。

### 所見C（CONFIRMED・中、2エージェント独立発見）: マウスキャプチャ喪失時の状態不整合

`LadderCanvasHost.CaptureMouse()`の戻り値を確認しておらず、対応する`LostMouseCapture`
イベントハンドラも実装されていない。ドラッグ中にAlt+Tab等の外的要因でキャプチャが失われると、
`_draggingConnector`・Viewの`_connectorDragConsumedByEscape`等が中途半端な状態のまま残る。
その後ユーザーが（ドラッグと無関係な）通常クリックをすると、`LadderCanvasHost_
PreviewMouseLeftButtonUp`の`if (_viewModel.IsDraggingConnector)`分岐に誤って入り、直前の
中途半端なドラッグ位置が`ConfirmDragConnector()`によってそのまま確定・`MarkDirty()`されて
しまう。`poc/t041-drag-poc`も同様に`LostMouseCapture`を扱っていないが、PoCは使い捨ての
検証用ウィンドウのため実害が無く、本実装ではその欠落がそのまま持ち越されている。

### 所見D（severity低、参考）: ドラッグ中の矢印キー操作競合

`UpdateDragConnector`はドラッグ開始時のスナップショットからの絶対差分で毎回`TopRow`/
`BottomRow`を再計算する。ドラッグ中（マウスボタン押下保持）に矢印キーで
`MoveSelectedConnector`/`ResizeSelectedConnectorEndpoint`を呼ぶと、次の`MouseMove`
イベント（ボタン押下中の微小な手ぶれでも発火）でキーボード操作の結果が予告なく上書き・
消失する。データ破損はないが、「矢印キーを押した瞬間だけ動いてすぐ戻る」という視覚的な
違和感を生む。

### 所見E（Altitude・重要な警告）: 横展開時のモグラ叩きが既に進行中

`SelectedEndpointIsStart`/`ToggleSelectedEndpoint`/`HasSelectedLinePrimitive`は
`VerticalConnector`/`FreeLine`の両方を意識した設計（`ToggleSelectedEndpoint`は両方を
チェック）だが、実際のドラッグ状態機械（`BeginDragConnector`〜`CancelDragConnector`、
6フィールド+4メソッド）とキーボード等価操作は`VerticalConnector`専用で、共有抽象がない。
コミットメッセージが明言する「残りの横展開（WireBreak/FreeLine/ConnectionDot）は後続
コミットで対応する」を受け、**作業ツリーには本コミット後の未コミット差分として、
WireBreak用に`_wireBreakDragPressPositionDip`/`_wireBreakDragStarted`/
`_wireBreakDragConsumedByEscape`という同型のフィールド・フローが既に追加されつつあることを
`git status`で確認した**。所見A（クロスカットクリア漏れ）を含むVerticalConnector版の設計上
の欠陥が、横展開の過程でそのまま複製されるリスクが高い。**横展開を進める前に、所見A・B・Cを
VerticalConnector版で解消してから複製する方が、二重の手戻りを避けられると考える。**

### 所見F〜H（severity低、Reuse/Simplification/Efficiency）

- **F（Reuse）**：`HitTestConnectorDragMode`（`LadderCanvas.cs:217`）が`HitTestConnector`
  とほぼ同一の「列位置一致判定+行範囲許容誤差判定」ロジックを複製している。
- **G（Simplification）**：ドラッグの一時状態を6個の独立フィールドで保持しており、PoC
  （`poc/t041-drag-poc`）の`DragMode { None, Move, ResizeTop, ResizeBottom }`という4値enum
  設計から後退している。「間隔を保ったままクランプ」ロジックも`MoveSelectedConnector`と
  `UpdateDragConnector`本体移動分岐とで、「Top<Bottomクランプ」も`ResizeSelectedConnector
  Endpoint`と`UpdateDragConnector`端点分岐とで、それぞれ重複している。
- **H（Efficiency）**：`LadderCanvasHost_PreviewMouseMove`はドラッグ中毎回`RedrawCanvas()`
  （`NetlistBuilder.Build`のフル再構築込み）を呼ぶ。PoCは接続点1本だけの空シートで
  「60fps許容枠内」を実測済みだが、要素・配線が多数存在する実際のシートでの再計測は
  行われていない（コミットメッセージは機能面の実機確認のみ言及）。

### 所見I（テストカバレッジ、参考）: Escキャンセルの回帰は単体テスト対象外

`ConnectorDragAndResizeTests.cs`は意図的にView層（マウス/キーボード操作）を対象外とする
方針のため、所見(1)②のEsc→キャプチャ維持→実MouseUpでの無害化という回帰は自動テストで
検知できない。将来この周辺をリファクタリングした際、静かに再発するリスクがある。

### Conventions —— 明確な違反は無し

---

## severity整理

| 所見 | 種別 | severity | 対応要否 |
|---|---|---|---|
| A | 正しさ（構造的） | **重大** | 対応推奨、横展開前の解消が望ましい |
| B | 正しさ（クラッシュ） | 中 | 対応推奨（既存モデル全体の脆弱性、T-041スコープ外の可能性もあり家老判断） |
| C | 正しさ（構造的） | 中 | 対応推奨 |
| D | 正しさ（軽微） | 低 | 任意 |
| E | Altitude | 重要な警告 | 横展開の進め方に関わる、家老判断 |
| F〜H | Reuse/Simplification/Efficiency | 低 | 任意 |
| I | テストカバレッジ | 低 | 対応不要（既知の制約） |

---

## 出典・参照

- 対象コミット`a471260`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`SelectedEndpointIsStart`205-243行目、
  `SelectedConnector`251-266行目、ドラッグ状態機械267-341行目、`DeleteSelectedConnector`
  402-409行目、`CurrentSheetIndex`100-127行目、`ReplaceDocument`1032-1084行目）
- `src/Ecad2.App/MainWindow.xaml.cs`（マウスハンドラ246-310行目、`CyclePanelFocus`
  426-431行目、キーボードハンドラ573-618行目）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`HitTestConnectorDragMode`/`RowAtDip`）
- `src/Ecad2.Core/Model/Element.cs:122-128`（`VerticalConnector`、バリデーション無し）
- `src/Ecad2.Core/Persistence/GcadSerializer.cs`（`Deserialize`、大小関係検証無し）
- `tests/Ecad2.App.Tests/ConnectorDragAndResizeTests.cs`（新規13件）
- `poc/t041-drag-poc/T041DragPoc/DragCanvas.cs`（比較対象PoC、`DragMode` enum・
  `RunRenderBenchmark`）
- `code-review`スキル（line-by-line diff scan・removed-behavior/cross-file・品質角度、
  3エージェント並行、CONFIRMED3件・所見6件）
- `git status`（作業ツリーの未コミット差分、WireBreak横展開WIPの存在確認）
