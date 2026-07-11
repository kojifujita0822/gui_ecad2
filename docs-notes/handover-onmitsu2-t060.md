# 引き継ぎメモ（隠密2 T-060調査、異常離脱）

最終更新: 2026-07-11（隠密2記す、出力破損2回検知による離脱）

`long-horizon-discipline`スキル§6の5点セット形式で記す。

---

## 0. 離脱理由

Grepツール結果の破損を同セッション内で2回検知（§5基準＝2回目で離脱）。
詳細は`docs-notes/output-corruption-log.md`記録#8参照。実ファイルはいずれもRead直読で
無傷確認済み（tool-result生成段階の破損、ファイル内容自体は無事）。

---

## 1. 目的とDoD（委譲時の文面）

task_id=T-060 PDF出力機能のUI結線調査（調査のみ、実装不可）

DoD：
1. Ecad2.Pdf層のAPI面の棚卸し（エントリポイント・入力[Document/Sheet]・出力[ファイルパス]・
   複数ページ対応状況[P-001=1ページ打ち切り疑いの現状も確認]）
2. GuiEcad側でのPDF出力の呼び出し方（UI→Pdf層の結線パターン）の実物照合
3. ecad2で結線に必要な変更点の洗い出し（保存ダイアログパターン踏襲可否、SaveFileDialog既存実装の有無）
4. 開かれた論点（出力範囲=全シートか現在シートか等、UI/UX分岐になりうる点）の列挙

調査書は`docs/ecad2-t060-pdf-ui-wiring-survey-onmitsu2.md`へ（**未作成、正式報告書はまだ**）。

---

## 2. 現在の状態（三区分）

### 検証済み（根拠あり）

- **Ecad2.Pdf層のAPI面**：`src/Ecad2.Pdf/PdfRenderSurface.cs`のみ存在（Glob`src/Ecad2.Pdf/**/*.cs`で確認、
  objビルド成果物除く）。`PdfRenderSurface : IRenderSurface`（`IDisposable`）——コンストラクタ
  `PdfRenderSurface(string path)`、`BeginPage(Size2D pageSizeMm) -> IRenderer`、`EndPage()`、
  `Dispose()`で`_doc.Save(_path)`。日本語フォント対応`WindowsFontResolver`同梱。**Document/Sheetを
  跨ぐページ走査オーケストレーションを担うクラスは存在しない**——呼び出し側(App層)がループを書く必要あり。
- **Core層`DiagramRenderer`（`src/Ecad2.Core/Rendering/DiagramRenderer.cs`）は複数ページ機能を
  既に完全実装済み**：`RenderPageCount(sheet)`（枠あり時の行分割ページ数）・`Render(..., pageRowStart,
  pageRowCount, pageNumber, totalPages, enableBorder)`・`RenderCrossRefPage`（クロスリファレンス専用
  ページ）・`RenderBomPage`（部品表専用ページ）・`CrossRefPageCount`。全文Read済み。
- **P-001「PdfExporterが1ページで打ち切られる仕様」は現行Core層の問題ではなくT-002 PoC限定と判明**。
  該当`PdfExporter`クラスは`poc/wpf-focus-poc/WpfFocusPoc/PdfExporter.cs`のみに存在し、単純な
  `if (y > page.Height.Point - spacing) break;`で打ち切る簡易実装（本番のEcad2.Pdf/DiagramRendererとは
  無関係の独立スパイク）。本実装（`Ecad2.Core.DiagramRenderer`）は最初から複数ページ設計であり、
  P-001の懸念は現行アーキテクチャには当てはまらない（=既に解消済みとして報告書に明記できる）。
  根拠：全リポジトリGrep「PdfExporter」→3件（docs/proposed.md記載元、poc配下2件のみ、src配下0件）。
- **GuiEcad側の実物結線パターン**（`C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Menu.cs:217-298`、Read済み）：
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
  - `PdfPreviewDialog.xaml.cs`が存在（`internal sealed partial class PdfPreviewDialog : ContentDialog`）
    ——**中身は未読了**（次セッションで要確認）。
- **ecad2側の既存SaveFileDialogパターン**（`src/Ecad2.App/MainWindow.xaml.cs:216-221`、Read済み）：
  `Microsoft.Win32.SaveFileDialog { Filter = GcadFileFilter, DefaultExt = ".gcad" }` →
  `ShowDialog(this) == true` → `TrySaveToFile`。WPF標準の同期的ダイアログ（GuiEcadの非同期WinUI3
  FileSavePickerと異なりawait不要）。PDF出力もこの同型パターン（Filter="PDF ファイル|*.pdf",
  DefaultExt=".pdf"）を踏襲すれば足りる見込み。
- **ecad2のツールバー/メニュー結線状況**（`src/Ecad2.App/MainWindow.xaml:114-183`）：メニュー
  「PDF出力(_P)」(116行目)・ツールバーボタン(181-183行目)ともにXAML要素は存在するが、いずれも
  `Click`属性・`Command`バインドが未設定（他の項目と比較し欠落を確認）。todo.md記載どおり。

### 未確認（DoD未達、次セッションで要確認）

- Ecad2.Core層にGuiEcadの`CrossReferenceBuilder`・`DeviceTable`（BOM）相当のクラスが実在するか、
  APIが一致するかは**未検証**。Grep「CircuitNumberer|CrossReferenceBuilder|DeviceTable|EnableBorder」が
  ヒットした7ファイル（`DesignRuleCheck.cs`, `DiagramRenderer.cs`, `CrossReference.cs`,
  `CircuitNumberer.cs`, `DeviceRenamer.cs`, `Document.cs`, `Device.cs`）は把握したが、どの語が
  どのファイルにマッチしたか・`CrossReferenceBuilder`という完全一致クラス名の存在は未確認
  （`CrossReference.cs`に類似機能がある可能性が高いが中身未読）。
- `Document.Settings.EnableBorder`相当の設定がecad2側に存在するか未確認（枠あり/枠なし出力の切替設定）。
- GuiEcadの`PdfPreviewDialog`の中身未読（プレビューの実装方式・XAML構成）。
- ecad2で結線に必要な変更点の具体的コード差分は未確定（DoD(3)未達、上記未確認事項が前提）。
- 開かれた論点（DoD(4)）は下記「判明しつつある論点候補」までで、網羅的列挙としては未完。

### 判明しつつある論点候補（未確定、次セッションで深掘り・殿確認要）

- **出力範囲**：GuiEcadは「全シート常に」決め打ち。ecad2で同じにするか、現在シートのみ選択肢を
  設けるかはUI/UX分岐→着手時に殿確認【MUST】（task-implementationスキルの既定方針どおり）。
- **枠あり/枠なし切替**（EnableBorder相当）の有無・デフォルト値。
- **プレビュー機能**（PdfPreviewDialog相当）を最初から実装するか、v1は「直接出力のみ」でスコープを
  絞るか——GuiEcadは実装済みだが、ecad2側の実装コスト次第では後回しの余地あり（要殿確認）。

---

## 3. 試して失敗したアプローチと結果

なし。作業自体は順調に進んでいたが、ツール結果破損の2回目検知により§5規定で打ち切り。
調査アプローチ自体の失敗ではない。

---

## 4. スコープ境界

- 隠密は読み取り専用。実装・書き込みは侍へ委譲（今回は該当作業なし）。
- src/tests一切未変更（元々書き込み対象外）。
- `docs/ecad2-t060-pdf-ui-wiring-survey-onmitsu2.md`は**未作成**（調査未完のため正式報告書はまだ）。
- `docs-notes/output-corruption-log.md`への1行追記（#8）のみ実施。本ファイル作成も本離脱プロトコルの一部。

---

## 5. 次の1手（再起動後の隠密/隠密2がやること）

1. 本ファイルを読み、**Grep使用を避けRead直読中心**で調査を再開する
   （既知知見：Grep -A/-Cで広めに読む操作自体が本件破損型の誘因）。
2. 未確認事項の優先順位で潰す：
   (a) `src/Ecad2.Core/Simulation/CrossReference.cs`を直接ReadしてGuiEcadの
       `CrossReferenceBuilder`/`DeviceTable`相当の存在・API差異を確認
   (b) `src/Ecad2.Core/Model/Document.cs`を直接ReadしてSettings/EnableBorder相当の有無を確認
   (c) GuiEcad `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/PdfPreviewDialog.xaml.cs`を
       直接Readしてプレビュー方式を把握
3. 上記が揃ったら、`docs/ecad2-t060-pdf-ui-wiring-survey-onmitsu2.md`を正式に書き起こす
   （DoD(1)〜(4)を満たす形、本ファイルの「検証済み」区分をベースに再利用可）。
4. 家老へ調査完了報告（本セッションでは異常検知のみ報告済み、正式な調査完了報告はまだ）。
5. T-056（グリッド線表示切替の事前案起草）は家老から追加采配済み、T-060完了後に着手する約束をしている
   （`docs/ecad2-t056-grid-toggle-proposals-onmitsu2.md`へ、`docs/ecad2-uiux-proposals-p017-p020-p023-onmitsu.md`が書式参考）。

---

## 起動時の合図

通常どおり「開始」で起動。役割は`prompts/startup-auto.md`のstep0〜6で自動決定する
（隠密が空いていれば隠密、埋まっていれば隠密2）。ブランチはmain、作業ツリーはこのセッションでは
無変更（docs-notes 2ファイルの追記のみ）。
