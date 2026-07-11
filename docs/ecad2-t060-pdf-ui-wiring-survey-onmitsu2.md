# T-060 PDF出力機能のUI結線調査

調査者: 隠密2　最終更新: 2026-07-11

前任セッション（出力破損#8により§5離脱、`docs-notes/handover-onmitsu2-t060.md`）のDoD(1)(2)を
引き継ぎ、DoD(3)(4)を本セッションで完了。調査は読み取りのみ、実装・書き込みは行っていない。

---

## DoD(1): Ecad2.Pdf層のAPI面の棚卸し

- `src/Ecad2.Pdf/PdfRenderSurface.cs`のみ存在。`PdfRenderSurface : IRenderSurface, IDisposable`
  — コンストラクタ`PdfRenderSurface(string path)`、`BeginPage(Size2D pageSizeMm) -> IRenderer`、
  `EndPage()`、`Dispose()`で`_doc.Save(_path)`。日本語フォント対応`WindowsFontResolver`同梱。
  **Document/Sheetを跨ぐページ走査オーケストレーションを担うクラスは存在しない**——呼び出し側
  （App層）がループを書く必要がある。
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs`は複数ページ機能を既に完全実装済み：
  `RenderPageCount(sheet)`（枠あり時の行分割ページ数）・`Render(..., pageRowStart, pageRowCount,
  pageNumber, totalPages, enableBorder)`・`RenderCrossRefPage`（クロスリファレンス専用ページ）・
  `RenderBomPage`（部品表専用ページ）・`CrossRefPageCount`。
- **P-001「PdfExporterが1ページで打ち切られる仕様」は現行Core層の問題ではなくT-002 PoC限定と判明**。
  該当`PdfExporter`クラスは`poc/wpf-focus-poc/WpfFocusPoc/PdfExporter.cs`のみに存在する簡易実装
  （`if (y > page.Height.Point - spacing) break;`で打ち切り）で、本番の`Ecad2.Pdf`/`DiagramRenderer`
  とは無関係の独立スパイク。全リポジトリでの「PdfExporter」該当は3件（`docs/proposed.md`記載元・
  poc配下2件のみ、src配下0件）。**P-001の懸念は現行アーキテクチャには当てはまらず実質解消済み**。

## DoD(2): GuiEcad側の実物結線パターン照合

`C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Menu.cs:217-298`を全読。

- **プレビュー→エクスポートの2段階UI**：`OnMenuPreviewPdf`（217行目）が`CircuitNumberer.Number`→
  `CrossReferenceBuilder.Build`→`PdfPreviewDialog`表示→ダイアログ「OK」（`ContentDialogResult.Primary`）
  で`OnMenuExportPdf`を呼ぶ。
- `OnMenuExportPdf`（231行目）：`FileSavePicker`（WinUI3、SuggestedFileName=Document.Info.Title、
  フィルタ".pdf"）→`CircuitNumberer.Number`→`CrossReferenceBuilder.Build`→`DiagramRenderer`生成
  （`PaperSize`・`IncludeTracingImages=false`指定）→`PdfRenderSurface`生成→**全シートを
  `foreach (var sheet in _document.Sheets)`で走査**、`enableBorder`時は`dr.RenderPageCount(sheet)`分
  ページ分割、`BeginPage`/`Render`/`EndPage`ループ→クロスリファレンス専用ページ追加
  （`CrossRefPageCount`分）→機器表(BOM)が1件以上あれば専用ページ追加。例外は`ShowErrorAsync`。
- **出力範囲は「全シート常に」**——現在シートのみという選択肢はGuiEcadに存在しない（都度確認なし、決め打ち）。
- `PdfPreviewDialog.xaml.cs`（`GuiEcad.App`、全読済み）：`ContentDialog`＋`Microsoft.Graphics.Canvas`
  の`CanvasControl`（Win2D）でページを1枚ずつ自前描画。`PageKind{Sheet,CrossRef,Bom}`の列挙で
  ページ種別を管理し、`BuildPageList()`が`DiagramRenderer.RenderPageCount`/`CrossRefPageCount`/
  BOM有無から総ページ数と各ページのメタ情報を事前構築。`OnCanvasDraw`で`Win2DRenderer`を介し
  `DiagramRenderer.Render`/`RenderCrossRefPage`/`RenderBomPage`をそのまま呼び出して描画。
  ページ送り（Prev/Next）・ズーム（0.25〜4.0倍、0.25刻み）・幅フィットの操作を持つ。

## DoD(3): ecad2で結線に必要な変更点の洗い出し

### Core層（Ecad2.Core）：追加実装は不要と見込まれる

`src/Ecad2.Core/Simulation/CrossReference.cs`・`src/Ecad2.Core/Model/Document.cs`をGuiEcad対応ファイル
（`GuiEcad.Core/Simulation/CrossReference.cs`・`GuiEcad.Core/Model/Document.cs`）と直読比較した結果、
**両ファイルとも全文一致**（T-007移植時にそのまま移植された模様）。具体的に確認できた対応：

| GuiEcad | ecad2 | 状態 |
|---|---|---|
| `CrossReferenceBuilder.Build(doc, lib)` | 同シグネチャで存在 | 完全一致 |
| `DeviceTable`（`Dictionary<string,Device> ByName`） | `src/Ecad2.Core/Model/Device.cs`に同一定義で存在 | 完全一致 |
| `DocumentSettings.EnableBorder`（既定true） | 同名・同既定値で存在 | 完全一致 |
| `DocumentSettings.PaperSize`（A4/A3 enum） | 同名・同enumで存在 | 完全一致 |

→ GuiEcadの`OnMenuExportPdf`ロジック（CircuitNumberer→CrossReferenceBuilder→DiagramRenderer→
PdfRenderSurfaceのループ）は、Core層API面ではecad2にそのまま移植可能。**App層の新規コードのみで足りる**。

### App層（Ecad2.App）：新規実装が必要な箇所

1. **PDFエクスポートのオーケストレーションコード新設**——GuiEcadの`OnMenuExportPdf`相当
   （全シート走査＋枠ありページ分割＋クロスリファレンスページ＋BOMページ→`PdfRenderSurface`書き出し）
   をApp層（ViewModelまたはコードビハインド）に新規実装する。Core層API自体は流用可能。
2. **保存ダイアログ**——既存パターンを踏襲すれば足りる見込み。
   `src/Ecad2.App/MainWindow.xaml.cs:216-221`に既存実装あり：
   `Microsoft.Win32.SaveFileDialog { Filter = GcadFileFilter, DefaultExt = ".gcad" }` →
   `ShowDialog(this) == true` → `TrySaveToFile`。WPF標準の同期的ダイアログ（GuiEcadの非同期WinUI3
   `FileSavePicker`と異なりawait不要、コードは単純化できる見込み）。PDF出力も同型パターン
   （`Filter="PDF ファイル|*.pdf"`, `DefaultExt=".pdf"`）を踏襲すれば足りる。
3. **メニュー/ツールバーへのCommand結線**——`src/Ecad2.App/MainWindow.xaml:114-183`に
   メニュー「PDF出力(_P)」（116行目）・ツールバーボタン（181-183行目）ともにXAML要素は存在するが、
   いずれも`Click`属性・`Command`バインドが未設定（他の結線済み項目と比較し欠落を確認済み）。
   ViewModelにCommand実装＋バインド追加が必要。
4. **プレビュー機能（GuiEcadの`PdfPreviewDialog`相当）**——**ecad2側に相当する実装は皆無**。
   GuiEcadはWinUI3の`ContentDialog`＋Win2D `CanvasControl`で自前ページ描画・ズーム・ページ送りを
   実装しているが、WPFにはWin2Dが存在しない。WPF側で同等機能を作る場合、`DrawingVisual`＋
   `Image`コントロール、または`InkCanvas`等の代替描画手段を新規に設計する必要があり、単純な
   移植では済まない（下記「開かれた論点」参照）。

## DoD(4): 開かれた論点（UI/UX分岐、着手時に殿確認が必要）

1. **出力範囲**：GuiEcadは「全シート常に」決め打ち（現在シートのみの選択肢は存在しない）。
   ecad2で同じ挙動にするか、現在シートのみ出力する選択肢を設けるかは着手時に殿確認が必要。
2. **プレビュー機能の要否・実装時期**：GuiEcadは「プレビュー→確認→エクスポート」の2段階UIだが、
   WPF側はWin2Dに相当する描画基盤がなく実装コストが増す。v1は「直接エクスポートのみ」でスコープを
   絞り、プレビューは後続タスクに回す余地がある（要殿確認）。
3. **枠あり/枠なし切替のUI**：`DocumentSettings.EnableBorder`はモデル層に既に存在するが、これを
   ユーザーがUIから切り替える手段（トグル・メニュー項目等）の要否・見せ方は未確定（要殿確認）。
4. **エラー時の見せ方**：GuiEcadは例外時`ShowErrorAsync`（WinUI3ダイアログ）。ecad2側の標準的な
   エラー表示パターン踏襲で足りると見込むが、既存パターンの有無は本調査の範囲外（実装着手時に
   侍が確認すべき事項として申し送る）。

## 出典一覧

- `src/Ecad2.Pdf/PdfRenderSurface.cs`（Read）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs`（前任セッションでRead済み、本セッションでは再読せず）
- `src/Ecad2.Core/Simulation/CrossReference.cs`（Read、本セッション）
- `src/Ecad2.Core/Model/Document.cs`（Read、本セッション）
- `src/Ecad2.Core/Model/Device.cs`（Read、本セッション、`DeviceTable`定義確認）
- `src/Ecad2.App/MainWindow.xaml.cs:216-221`（前任セッションでRead済み）
- `src/Ecad2.App/MainWindow.xaml:114-183`（前任セッションでRead済み）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Menu.cs:217-298`（前任セッションでRead済み）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/PdfPreviewDialog.xaml.cs`（Read、本セッション）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.Core/Simulation/CrossReference.cs`（Read、本セッション）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.Core/Model/Document.cs`（Read、本セッション）
- `docs/todo.md` T-060節（Read、本セッション）
- `poc/wpf-focus-poc/WpfFocusPoc/PdfExporter.cs`（前任セッションでGrep確認、実体は未読——存在と
  独立性の確認のみ。中身の再確認は不要と判断）

不明点なし。範囲外の新規気づきなし。
