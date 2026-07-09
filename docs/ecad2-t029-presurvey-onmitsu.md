# T-029（ツールバーボタン配置時のゴースト表示）先行調査（隠密）

> 2026-07-09 隠密調査。着手承認は殿裁定2026-07-09（P-040完了後の次タスク）。調査のみ・
> src/tests書き込みなし。目的：実装方式の比較とUI/UX分岐点の整理を殿へ提示し、裁定を仰ぐ。

---

## 結論（先出し）

- ecad2には既に**`DiagramRenderer.DrawPreview`**（半透明色で1要素描画）が実在し、GuiEcadの
  ゴースト表示もこのメソッドの前身をそのまま使っていた（GuiEcad由来・T-007移植済み）。
  **実装方式は「既存Rendererメソッドの活用＋既存DrawingContext内での追加呼び出し」が
  最小コスト・最小リスク**（詳細は2節）。
- ただし「マウス位置に追従する」という文言どおりの実装（GuiEcad方式＝常時ホバー追跡）と、
  「選択中セル（SelectedCell）に表示する」という低コスト代替案とで、**キーボードファースト
  理念との整合性が変わる**。これが最大のUI/UX分岐点（4節）であり、殿の判断を仰ぎたい。

---

## 1. 現状把握

### 1-1. 簡易版（ステータスバー表示）の実装箇所

`src/Ecad2.App/MainWindow.xaml.cs:1054-1066`の`ActivateBuiltinTool`直前のコメント（1058行目）に
明記されている：

```csharp
// ゴースト(プレビュー)表示は簡易版としてステータスバー表示のみに留める(視覚プレビューはT-029)。
private void ActivateBuiltinTool(string partName, bool isOr)
{
    ...
    _viewModel.Tool = new ViewModels.ToolState(ViewModels.ToolMode.PlaceElement, PartId: entry.Definition.Id, IsOr: isOr);
    _viewModel.StatusMessage = $"配置ツール: {partName}{(isOr ? "(OR)" : "")} - キャンバスをクリックして配置位置を指定してください";
}
```

`ToolState`（`src/Ecad2.App/ViewModels/ToolState.cs:12-23`）は`ToolMode`enum＋
`record struct ToolState(Mode, Kind?, PartId?, Orient?, IsOr)`の単一状態集約
（GuiEcad時代のbool束乱立バグの反省、同ファイルコメント6-11行目）。

### 1-2. 配置フロー全体（呼び出し順）

1. ツールバーボタンClick → `BuiltinPlaceButton_Click`（MainWindow.xaml.cs:1074）→
   `ActivateBuiltinTool`（1059）→ `Tool = new ToolState(PlaceElement, PartId, IsOr)`。
2. キャンバスクリック → `LadderCanvasHost_PreviewMouseLeftButtonUp`（366行目台）→
   `SelectedCell = LadderCanvasHost.ToGridPos(position)`（521）→ 配置確定処理へ分岐。
3. `TryPlaceElement`（1320付近）が未選択/範囲外/占有チェック後、非モーダルの浮動インライン
   バー（`ElementPlacementBar`）を選択セル直下に表示。
4. OK確定で`PlaceElementAtSelectedCell`（MainWindowViewModel.cs:1349）が実データ配置。

**重要な事実**：マウスの**ホバー移動**でSelectedCellを追従更新する処理は現状**存在しない**。
`LadderCanvasHost_PreviewMouseMove`（366行目）は`IsMouseCaptured`時（＝コネクタ/自由線の
ドラッグ中）のみ処理し、通常のホバーでは何もしない。SelectedCellは**クリック（MouseUp）時のみ**
更新される。つまり文言通りの「マウス位置追従」を実装するには、新規の常時有効なマウス移動
ハンドラを追加してグリッド座標を継続的に取得する処理が必要になる。

### 1-3. キャンバス描画パイプライン

- `LadderCanvas`（`src/Ecad2.App/Views/LadderCanvas.cs`）は`FrameworkElement`+
  `VisualCollection`によるDrawingVisualホスト方式（`OnRender`オーバーライドではない）。
  `Draw()`が毎回`DrawingVisual.RenderOpen()`でDrawingContextを開き、`WpfRenderer`経由で
  `DiagramRenderer.Render()`を呼んで確定図形を描いた後、選択ハイライト（`SelectedCellPen`＝
  `Brushes.OrangeRed`、46行目）や「記入中プレビュー」（コネクタ・自由線の破線ペン、
  非半透明）を**同じDrawingContext上に追加描画**している。
- 既にT-017/T-027で、選択中セル（`SelectedCell`）には`SelectedCellPen`によるオレンジ枠
  ハイライトが常時描画される（129行目`dc.DrawRectangle(null, SelectedCellPen, CellRectDip(cell))`）。
  これは矢印キー移動でもクリックでも同一の`SelectedCell`を経由するため、**ecad2は既に
  「キーボード操作でもマウス操作でも同じ“現在セル”表示機構」を持っている**（GuiEcadの
  `_hoverCell`/`_focusCell`の二重管理とは異なる設計）。
- `DiagramRenderer`（`src/Ecad2.Core/Rendering/DiagramRenderer.cs:900-926`）には
  **`DrawPreview(IRenderer r, ElementInstance e, Color color, PartLibrary? library = null)`**
  が既に存在（docstring:「配置プレビュー用に1要素を指定色（半透明可）で描く。Renderの後に
  呼ぶこと」）。`color`にアルファ値を含めれば半透明描画がそのまま可能。ただし`_lib`の一時
  差し替えは「UIスレッド単一使用が前提・スレッドセーフでない」との注記あり（923-926行目）。
  WPFはUIスレッド単一前提のため実害はない。
- ドラッグ系操作（コネクタ・自由線）では既に`PreviewMouseMove`のたびに`RedrawCanvas()`→
  `Draw()`を呼ぶパターンが実運用実績としてある。マウス移動毎の高頻度再描画自体は
  新規リスクではなく、既存踏襲で足りる。

### 1-4. T-015（サムネイル機能）のDrawPreview利用との関係

`DrawPreview`の唯一の現行呼び出し元は`src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs:56`。
オフスクリーンの`DrawingVisual`へ描き`RenderTargetBitmap`へラスタライズする**一発生成**の
仕組みで、部品選択リストのサムネイルとして一度だけ生成・キャッシュされる（`PartSelectionEntryViewModel.cs`）。

**T-029への転用**：`DrawPreview`**メソッド自体**はそのまま再利用できる（シグネチャがアルファ
込みのColorを受け付ける）。一方、T-015の「RenderTargetBitmap化」という実装パターン自体は
T-029には不向きと考える（推測）。T-015は静的一発描画・キャッシュ用途だが、T-029はマウス移動
毎に高頻度再描画する必要があり、`LadderCanvas.Draw()`が既に持つ「同一DrawingContext上に
確定図形＋各種プレビューを追記する」方式（コネクタ破線ペン等と同型）に`DrawPreview`呼び出しを
1行足す形の方が自然かつ低コスト。

---

## 2. GuiEcadの前例

**結論：前例が「ある」。** GuiEcad実ソース（`C:\Users\kojif\Desktop\生産物\gui_ecad\src`、
T-007移植元）に、まさに同種の機能が実装されていた。

`GuiEcad.App\MainPage.Drawing.cs:144-159`：

```csharp
// 記号配置ツール選択中：マウス位置（キーボード配置モード中はフォーカスセル）に
// 半透明の配置プレビュー（ゴースト）を描く。配置可能なセル範囲（列内）にいるときだけ表示する。
var previewCell = _keyboardModeActive ? _focusCell : _hoverCell;
if (!_testMode && PlaceKind is ElementKind pk
    && previewCell.Row >= 0 && previewCell.Column >= 0 && previewCell.Column < _sheet.Grid.Columns)
{
    var ghost = new ElementInstance { Kind = pk, Pos = previewCell, PartId = PlacePartId, ... };
    if (PlaceOrient is not null) ghost.Params[ParamKeys.Orient] = PlaceOrient;
    dr.DrawPreview(renderer, ghost, new Color(120, 0, 120, 255));   // 半透明の青紫
}
```

- `_hoverCell`は`MainPage.Pointer.cs:418-425`の`OnPointerMoved`ハンドラで継続更新される
  （マウス移動→セル変換→ゴースト位置反映という一連のロジック）。
- **重要な事実**：GuiEcadは「マウスホバー中」と「キーボード配置モード中」を**別変数
  （`_hoverCell`/`_focusCell`）で二重管理**し、`_keyboardModeActive`フラグで切り替えていた。
  これはGuiEcad全体のbool束乱立バグ（design-brief 3節#1、`ToolState.cs`コメント参照）と
  同根の設計であり、ecad2がT-016で単一`SelectedCell`に統合した経緯（1-3節参照）とは
  対照的である。
- 範囲チェックは**列のみ**（`Column < _sheet.Grid.Columns`）で行の上限チェックが無い
  （GuiEcad側の実装の特徴であり、ecad2のT-045増分B境界ガード＝行0〜Rows-1・列0〜Columns-1
  両方チェックとは異なる。そのままは移植しない方がよいと考える＝推測）。
- また、GuiEcadはキーボード配置モード中、配置ツール未選択でも「フォーカスセルの矩形強調表示」
  （オレンジ枠、159-163行目付近）を別途行っていた。ecad2ではこれに相当する機構が
  `SelectedCellPen`によるハイライト（1-3節）として既に**ツール選択に関係なく常時**実装済み。

---

## 3. WPF実装方式の候補比較

一般的なWPF実装知識ベースの比較（既存コード事実と組み合わせて評価）。

| 方式 | 概要 | 実装コスト | リスク | 既存パイプラインとの整合性 |
|------|------|-----------|--------|--------------------------|
| **1. Adorner（AdornerLayer）** | `AdornerLayer`にプレビュー専用Adornerを追加 | 中（新規クラス・Add/Remove管理・`InvalidateVisual`） | 中（`AdornerDecorator`祖先要・パン/ズーム変換の二重管理になりやすい） | 低（既存はDrawingVisual+VisualCollectionホスト方式、OnRenderオーバーライドでないため別の描画経路・別のvisual tree位置を持ち込む） |
| **2. DrawingVisual重畳** | 既存`VisualCollection`にプレビュー専用の別Visualを追加 | 低〜中 | 低（技術基盤は既存と同一） | 高（同じDrawingVisualベースだが「オーバーレイ層を1つ増やす」判断が伴う） |
| **3. 既存Renderer拡張（DrawPreview活用）** | `LadderCanvas.Draw()`内の既存DrawingContext上で`DiagramRenderer.DrawPreview`を追加呼出し | **低**（新規クラス・新規Visual・新規レイヤー不要、既存の「記入中プレビュー」に1行足す程度） | **低**（マウス移動毎の全体再描画は既存ドラッグ系操作で実績あり＝新規リスクでない） | **最高**（既存API・既存パイプライン・既存の再描画パターン全てに合致） |
| 4. Popup/別Window（参考） | マウス座標追従の別ウィンドウ | 中〜高 | 中〜高（論理/画面座標の二重変換、DPI・マルチモニタ、パン/ズーム同期） | 低（CAD系では座標変換の煩雑さゆえ採用例は少ない、一般論） |

**推奨（一般論＋既存事実の組み合わせ）**：方式3（既存`Draw()`内で`DrawPreview`を追加呼出し）が
最小コスト・最小リスクで既存パイプラインとの整合性も最も高い。新規レイヤーやAdornerを
増やさない分、パン/ズーム変換の二重管理も発生しない。GuiEcadも実質的に同型の方式
（Rendererの`DrawPreview`を既存描画サイクル内で呼ぶ）を採っていた。

---

## 4. UI/UX分岐点（殿への選択肢提示用）

### 分岐点A：何に追従させるか【最重要】

- **案A-1（GuiEcad方式・忠実再現）**：マウスホバーを常時追跡する新規ハンドラを追加し、
  ホバー中セルにゴーストを表示。キーボード配置モード（矢印キーでSelectedCell移動）中は
  別途SelectedCellへゴーストを表示する分岐が必要（GuiEcadの`_keyboardModeActive`分岐相当）。
  文言「マウス位置追従」に最も忠実だが、実装コストはA-2より高い（新規ホバー追跡機構が要る）。
- **案A-2（SelectedCell表示・低コスト代替）**：新規のホバー追跡は行わず、既に存在する
  `SelectedCell`（クリックまたは矢印キーで更新される、既存のオレンジ枠ハイライトと同じ対象）
  にツール選択中はゴーストも重ねて表示する。新規ハンドラ不要、既存基盤の完全再利用。
  ただし「クリックする前にマウスを動かしただけでは何も表示されない」（クリック確定後や
  矢印キー移動後にのみゴーストが動く）という点で、文言通りの「マウス追従」ではなくなる。

**キーボードファースト理念（CLAUDE.md「マウス操作に頼らない操作性」）との関係**：
案A-2は追加実装なしにキーボード操作でも同じ視覚効果が得られる（SelectedCellは矢印キーでも
更新されるため）。案A-1はGuiEcad同様キーボード分岐を別途実装すれば同等の効果を持たせられるが、
その分実装コストと分岐条件が増える。**マウス専用機能に閉じるか、キーボードでも同等の価値を
持たせるかは、既存のCLAUDE.mdキーボードファースト原則に照らして案A-2または「案A-1＋キーボード
分岐」を推奨したいが、最終的な使用感の好みは殿の裁定を仰ぐべき論点**と考える（推測）。

### 分岐点B：半透明の見た目

- 色（GuiEcadは`Color(120,0,120,255)`＝半透明の青紫）をそのまま踏襲するか、ecad2の
  既存配色（`SelectedCellPen`のオレンジ系、コネクタ破線ペンの配色等）と統一感を持たせるか。
- 輪郭のみか塗りも含むか（`DrawPreview`は`PartDrawing.Draw`/`SymbolGlyphs.Draw`をそのまま
  呼ぶため、通常の要素描画と同じ線種・塗りになる。色を変えるだけか、線幅や破線化も加えるか）。

### 分岐点C：表示条件（範囲外・占有セルの扱い）

T-045増分B（2026-07-09裁定）で配置可能範囲は「行0〜Rows-1・列0〜Columns-1」に確定済み
（負マージンは選択可・配置不可）。この既存仕様との整合が必要：
- 範囲外セル（選択は可能・配置は不可）にカーソルがある場合、ゴーストを非表示にするか、
  警告色（配置不可を示す赤系等）で表示するか。
- 既に要素がある占有セルの場合、ゴーストを重ねて表示するか、非表示にするか、警告色にするか。

### 分岐点D：表示タイミング

`ToolMode==PlaceElement`（配置ツール選択中）の間ずっと表示するか、特定の条件
（例：一定時間ホバーした場合のみ）を付けるか（後者はGuiEcadに前例なし、複雑化の懸念あり
＝推測）。

---

## 5. 見つかった気づき（範囲外・タスク化しない）

- GuiEcadのゴースト表示は行の範囲チェックが無い（列のみ）。ecad2で移植する際は
  T-045増分Bの境界仕様（行・列両方チェック）に合わせて実装すべき（分岐点Cで既に言及、
  実装時の注意点として記録のみ）。

---

## まとめ

- 実装方式は**方式3（既存`DiagramRenderer.DrawPreview`をLadderCanvas.Draw()内で追加呼出し）**
  が既存資産・既存実績の両面で最も筋が良いと考える（推測含むが根拠は明示済み）。
- UI/UX分岐点は4件（A:追従基準【最重要・キーボードファースト理念に直結】／B:見た目／
  C:表示条件／D:表示タイミング）。特に分岐点Aは実装コスト・キーボード操作との整合性の
  両方を左右するため、殿の裁定を最優先で仰ぎたい。
