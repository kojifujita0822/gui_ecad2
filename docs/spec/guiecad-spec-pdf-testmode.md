# GuiEcad仕様書：PDF出力・テストモード

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）のPDF出力・テストモード実装を実物照合で調査し、
`docs/spec/ecad2-spec-pdf-testmode.md`（ecad2側、T-075起票）と比較可能な形で纏める。

**P-060（枠ありPDF出力で用紙幅超過）関連の緊急照会に伴い、PDF出力節を優先して先行報告済み**
（家老宛peerメッセージ2026-07-12）。本ファイルはその正式収録版。

対応するecad2側仕様書：`docs/spec/ecad2-spec-pdf-testmode.md`

---

## 1. PDF出力

### 1-1. 用紙サイズ・枠あり出力の仕組み

`src/GuiEcad.Core/Model/Document.cs:42`：

```csharp
/// <summary>PDF/枠出力の用紙サイズ（縦固定）。</summary>
public enum PaperSize { A4, A3 }
```

コメントで明示されているとおり**縦固定**——横置き(Landscape)の選択肢はコード上どこにも存在しない
（`DiagramRenderer.cs:107-109`のPageW/PageH算出も`PaperSize`の2値切替のみで回転指定なし）。

### 1-2. ページ分割は「行」方向のみ（列方向の分割機構は存在しない）

`src/GuiEcad.Core/Rendering/DiagramRenderer.cs`：

- `RowsPerPage`（59行）：`(PageH - 2*MarginMm) / Cell`で1ページあたり収容可能な行数を算出。
- `PageCount`（69-71行）・`RenderPageCount`（99-103行）：総行数がRowsPerPageを超える場合、
  複数ページへ**縦方向にのみ**分割する。
- **列(横)方向の相当ロジックは存在しない**——`ColumnsPerPage`のような算出プロパティ、または
  横方向のページ分割処理はファイル全体（同ファイル全域を確認）に見当たらない。

### 1-3. 【重要】自動縮小フィット機能は"未配線の死んだ引数"として存在するのみ

`src/GuiEcad.Core/Rendering/IRenderer.cs:29`：

```csharp
void PushTransform(double translateX, double translateY, double scale = 1.0);
```

`scale`引数自体はインターフェースに存在し、`PdfRenderSurface.cs`（`PdfRenderer.PushTransform`
165-170行）でも`XGraphics.ScaleTransform`へ渡す実装まで揃っている。しかし**リポジトリ全体を
grepしても、`PushTransform`の呼び出し箇所（`DiagramRenderer.cs:851,900`の2箇所のみ）はいずれも
`scale`引数を省略し既定値1.0のまま**——自動縮小フィットの「器」は用意されているが、実際に
グリッド幅を用紙幅に合わせて縮小する呼び出しコードは一切存在しない。

### 1-4. 【P-060関連・核心の発見】既定の列数では用紙幅を大幅超過する

`RightBusX(int columns) => X(columns) + BusPad`（`DiagramRenderer.cs:105`）、
`X(boundary) = MarginMm + boundary * CellMm`（`GridGeometry.cs:18`）、既定値
`CellMm=9.0`・`MarginMm=20.0`（`DiagramRenderer.cs:9-10`）、既定列数`Columns=40`
（`GuiEcad.Core/Model/Sheet.cs:34`）から実際に計算：

```
RightBusX(40) = (20 + 40*9) + 9*0.5 = 380 + 4.5 = 384.5mm
```

A4縦=210mm、A3縦=420mm(横は297mm)——**A4はもちろんA3の縦(420mm)ぎりぎり未満というだけで、
A3の横297mmは大幅に超過する**。GuiEcad自身のソースコード上、既定列数のシートを枠あり(A4/A3縦)
でPDF化すればグリッド右側が用紙外へはみ出す計算になる。

**ecad2側も同一の既定値**（`Sheet.cs:39`：`Columns=40`、`CellMm`はecad2側`DiagramRenderer`で
未確認だが同型設計）——**同型の超過が起きる構造は移植時からそのまま持ち越されている**。

### 1-5. 結論：「GuiEcadの収め方を踏襲する」という選択肢は不成立

1-1〜1-4の調査により、**GuiEcadには「グリッドを用紙に収める」ための機構（自動縮小・横置き・
列方向ページ分割のいずれも）が実装されていない**ことを実物照合で確認した。ソースコード上、
既定列数のシートを枠あり出力すれば同型の幅超過が理論上起きる計算になるが、**GuiEcad側で実際に
この状況が実機テスト・ユーザー運用で発生していたか、発生時にどう回避されていたか（例：実運用では
列数40をフルに使うシートが少なかった、枠あり出力自体があまり使われなかった等）は本調査の範囲外
であり不明**（推測はしない）。

P-060の対処方針（縮小フィット/水平ページ分割/用紙設定/列数見直し）は、**GuiEcadに前例がない以上、
ecad2独自の新規設計判断となる**。

### 1-6. プレビュー→エクスポートの2段階UI（従来調査どおり、再確認）

`src/GuiEcad.App/MainPage.Menu.cs`：

- `OnMenuPreviewPdf`（217行〜）：プレビューダイアログ表示、OK押下で`OnMenuExportPdf`を呼ぶ（228行）。
- `OnMenuExportPdf`（231行〜）：`foreach (var sheet in _document.Sheets)`（261行）で全シート走査、
  枠ありページ分割＋クロスリファレンス専用ページ＋BOM専用ページを`PdfRenderSurface`へ書き出す。
- ecad2は「PDF出力(_P)」1エントリのみでプレビュー経由に一本化する裁定済み
  （P-059、`docs/proposed.md:67`、殿裁定2026-07-12=現行実装追認）——GuiEcadの「プレビュー」
  「PDF出力」2メニュー構成とは異なる。

---

## 2. テストモード

### 2-1. 状態管理：単純boolフラグ（`ToolMode` enum等の一元化はなし）

`src/GuiEcad.App/MainPage.xaml.cs:72`：`private bool _testMode;`——GuiEcadの配置ツール状態と
同様、複数箇所の条件分岐（`MainPage.Drawing.cs:147,196`／`MainPage.ContextMenu.cs:32,152,198-199`／
`MainPage.KeyboardMode.cs:80,145`／`MainPage.KeyBindings.cs:69,78,86,92,94`等、少なくとも10箇所超）
で`_testMode`を直接参照する分岐が散在。design-brief（3節#1）が指摘する「フラグの取りこぼしバグの
温床」という設計上の教訓がテストモードにもそのまま該当する。

### 2-2. モード切替：`ToggleButton`、シートごとセッション辞書は**切替の都度**全クリア

`MainPage.xaml.cs:342-356`（`OnTestModeToggle`）：

```csharp
_testSessions.Clear();                                    // 345行
_testSession = _testMode ? GetOrCreateTestSession(_sheet) : null;  // 346行
...
ToolbarBorder.Background = (Brush)...["AppTestModeBrush"]; // 356行（ON時のみ配色変更）
```

**訂正記録**：`docs/spec/ecad2-spec-pdf-testmode.md`2節は「テストモードOFFで全シート分クリア」と
記述するが、実物照合の結果`_testSessions.Clear()`（345行）は`OnTestModeToggle`ハンドラの冒頭で
無条件に呼ばれており、**ON時・OFF時のどちらの切替でも毎回全シート分クリアされる**（OFF時限定では
ない）。実害のある誤りではない（結果的に「ONにする瞬間も必ずクリアされる」という事実は変わらず、
既存記述の趣旨とも矛盾しない）が、より正確な記述として訂正する。

### 2-3. 配色：`AppTestModeBrush`リソース（ライト/ダーク2テーマ分定義）

`App.xaml:29,44`：ライトテーマ`#FFFFEBC8`（淡いオレンジ）、ダークテーマ`#FF2D1A00`（濃い茶）。
ツールバー背景色のみの変化であり、通電中ネット・励磁機器そのものの配色（LDmicro式赤色等）は
別実装箇所にある可能性が高い（本調査では未特定、`ecad2-spec-pdf-testmode.md`もこの点は「未確認」
としている）。

### 2-4. `TestSession`辞書とタイマパネル

`MainPage.xaml.cs:100`：`Dictionary<Sheet, TestSession> _testSessions`——シートごとに独立した
セッションを保持。`GetOrCreateTestSession`（407-413行）は未登録シートに対して新規`TestSession`を
生成し辞書へ登録。`TimerTickPanel.Visibility`（351行）がテストモードON/OFFと連動して表示切替。

---

## 3. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | GuiEcad実装箇所 | 備考 |
|---|---|---|
| プレビュー単独メニュー（エクスポートと分離） | `MainPage.Menu.cs:217`（`OnMenuPreviewPdf`） | ecad2は1エントリに統合済み（P-059裁定） |
| `IRenderer.PushTransform`のscale引数（自動縮小の"器"） | `IRenderer.cs:29` | 未使用の死んだ引数、実質どちらにも「機能」としては存在しない |

### (2) ecad2のみにある機能

該当なし（PDF出力・テストモードともecad2は実装未着手、GuiEcad実装範囲の部分移植すら未完了の段階）。

### (3) 両方にあるが挙動が異なる点／両方に共通する未解決課題

| 観点 | GuiEcad | ecad2 |
|---|---|---|
| **用紙幅超過への対処（P-060関連）** | **対処機構なし**（横置き・自動縮小・列分割いずれも未実装、1-5節） | 未着手、対処方針は新規検討中（P-060、殿裁定待ち） |
| PDFメニュー構成 | プレビュー／エクスポートの2メニュー | 「PDF出力」1メニューにプレビューを統合（P-059裁定） |
| テストモード状態管理 | 単純`bool`フラグ、参照箇所10箇所超に分散 | 未着手（開かれた論点2、上位enum新設が既定方針候補） |
| セッションクリアのタイミング | ON/OFF切替の**両方**で毎回クリア（2-2節、訂正記録） | 未着手（開かれた論点5） |
| テストモード切替のキー割当 | なし（マウス専用`ToggleButton`） | メニュー表記のみ存在しF5と重複表示（機能競合はなし、開かれた論点1） |

---

## 出典

- GuiEcad: `src/GuiEcad.Core/Model/Document.cs:42`、`src/GuiEcad.Core/Rendering/DiagramRenderer.cs:9-10,59,69-71,99-109`、
  `src/GuiEcad.Core/Rendering/GridGeometry.cs:18`、`src/GuiEcad.Core/Rendering/IRenderer.cs:29`、
  `src/GuiEcad.Core/Model/Sheet.cs:34`、`src/GuiEcad.Pdf/PdfRenderSurface.cs:165-170`、
  `src/GuiEcad.App/MainPage.Menu.cs:217,228,231,261`、`src/GuiEcad.App/MainPage.xaml.cs:72,100,342-356,407-413`、
  `src/GuiEcad.App/MainPage.Drawing.cs:147,196`、`src/GuiEcad.App/MainPage.ContextMenu.cs:32,152,198-199`、
  `src/GuiEcad.App/MainPage.KeyboardMode.cs:80,145`、`src/GuiEcad.App/MainPage.KeyBindings.cs:69,78,86,92,94`、
  `src/GuiEcad.App/App.xaml:29,44`
- ecad2: `docs/spec/ecad2-spec-pdf-testmode.md`（比較対象）、`src/Ecad2.Core/Model/Sheet.cs:39`
- 関連: `docs/proposed.md:67-68`（P-059・P-060）

## 不明点

- GuiEcad側で実際に幅超過が実機で発生・観測されていたか（コード上の論理帰結の確認に留まる）。
- 通電中ネット・励磁機器本体の配色実装箇所（`AppTestModeBrush`はツールバー背景のみ、本体配色は未特定）。
- `CommandHistory.Clear()`同様、PDF出力関連の呼び出し元の網羅確認は本調査範囲外。
