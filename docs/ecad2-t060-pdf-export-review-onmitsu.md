# T-060 PDF出力機能 静的レビュー報告（隠密）

- 対象コミット: `364cb0f`（増分1・PdfExporter）／`923ee29`（増分2/3・プレビュー+結線）、未push
- 手法: 手動確認8観点＋GuiEcad原本忠実性比較＋PDF固有ドメイン調査（独自3件）＋`code-review`
  スキル（Skill tool、high、8角度finder→verify）。計11件の一次調査＋9件のverifyを実施。
- GuiEcad原本参照: `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Menu.cs:217-298`、
  `PdfPreviewDialog.xaml(.cs)`

## 殿裁定3点との整合性

- ①出力範囲＝全シート常に → **実装済み確認**（`PdfExporter.Export`・`PdfPreviewDialog.BuildPageList`
  とも`document.Sheets`全走査、現在シートのみの分岐は存在しない）
- ②プレビュー機能＝今回実装（2段階UI） → **実装済み確認**。ただし下記F参照（未文書化のスコープ差分あり）
- ③枠あり/枠なし切替＝切替UIなし・常に枠あり → **実装済み確認**（`document.Settings.EnableBorder`の
  値をそのまま使用、UIからの変更手段なし）

## 結論（仕分け）

### 要修正・重大（5件、全てCONFIRMED）

**A. プレビューと実出力PDFで表題欄の総ページ数表示が食い違う（WYSIWYG違反）**
- 箇所: `src/Ecad2.Pdf/PdfExporter.cs:30`
- `PdfExporter.Export`の`totalPages`はシート（図面）ページのみを数え、後から追加される
  クロスリファレンスページ・BOMページを含まない。一方`PdfPreviewDialog.BuildPageList`は
  `sheetPages + crPages + (hasBom?1:0)`で総ページ数を計算しており、両者が構造的に異なる。
  `DiagramRenderer`は`totalPages`を表題欄に「pageNumber / totalPages」として実際に印字する
  （`DiagramRenderer.cs:1028`）。
- verify: CONFIRMED。既存テストフィクスチャ（`PdfExporterTests`1件目）で数値まで確定——
  同一文書の同一ページが、プレビューでは「1/3」、実際にエクスポートされたPDFでは「1/1」と
  異なる総ページ数を表示する。
- 根本原因: `PdfExporter.Export`と`PdfPreviewDialog.BuildPageList`がページ構成ロジック
  （シート走査・枠ページ分割・CrossRef/BOM有無判定）を独立に2箇所実装しており、この乖離は
  氷山の一角（H参照）。対処には、ページ構成を1箇所（例:`Ecad2.Pdf`側の共有ヘルパー）に集約し
  両者が同じ計算結果を参照する設計が望ましい。

**B. Ctrl+Pショートカットが表示のみで機能しない**
- 箇所: `src/Ecad2.App/MainWindow.xaml:117,188` ＋ `MainWindow.xaml.cs`（`Window_PreviewKeyDown`）
- メニュー・ツールバーとも`InputGestureText="Ctrl+P"`を表示しているが、`Window_PreviewKeyDown`
  （Ctrl+S/O/N/Z/Y/Gは全て対応するcaseあり）に`Key.P`のcaseが存在しない。KeyBinding等の代替経路も無し。
- verify: CONFIRMED（grep`Key\.P\b`で全体0件確認）。
- CLAUDE.md記載「**キーボードファースト**（マウス操作に頼らない操作性）を主眼に据える」という
  プロジェクト方針に反する（Conventions角度でも同一指摘、独立2経路で検出）。

**C. Enterキーのデフォルト動作が「安全な閉じる」から「PDF出力（実行）」へ反転している**
- 箇所: `src/Ecad2.App/Views/PdfPreviewDialog.xaml:34-35`
- GuiEcad原本は`ContentDialog`に`DefaultButton="Close"`を明示し、Enterキーは常に安全な
  クローズ動作になるよう設計されていた（`PdfPreviewDialog.xaml:13`）。ecad2移植版は
  「PDF出力」ボタンに`IsDefault="True"`を設定し、「閉じる」ボタンには`IsCancel="True"`
  （Escで反応、Enterとは無関係）のみ。
- verify: CONFIRMED。プレビューを閲覧中（キャンバス領域やPrev/Next等にフォーカスがある状態）に
  習慣でEnterを押すと、安全に閉じるのではなく実際にPDF出力（保存ダイアログ経由のエクスポート）が
  走ってしまう。

**D. ズームの意味がGuiEcadと別物になっており、大きい用紙でプレビューが画面からはみ出す**
- 箇所: `src/Ecad2.App/Views/PdfPreviewCanvas.cs:36-37`（`DrawPage`内`widthDip`計算）
- GuiEcad原本の`zoom`はダイアログ幅に対する相対フィット値（`scale = zoom * (availableWidth-40) / pageWidthMm`、
  `OnCanvasSizeChanged`でウィンドウリサイズ時にも再計算、zoom=1.0＝常に全幅表示）。ecad2は
  `widthDip = pageSizeMm.Width * MmToDip(固定96/25.4) * zoom`という絶対物理サイズ変換で、
  ウィンドウサイズを一切参照しない（`SizeChanged`ハンドラ自体が存在しない）。
- verify: CONFIRMED（GuiEcad原本コード引用込みで確認、実測: A3用紙幅297mm×3.78≈1122.5 DIP　>
  ダイアログ幅900 DIP、はみ出し確定。A4は210mm×3.78≈793.7 DIPでほぼ収まるがギリギリ）。
- 副作用として「等倍」ボタン（旧GuiEcadでは「幅に合わせる」の意）が名称と機能ともズレており、
  GuiEcadにあった「ウィンドウ幅にフィットさせる」手段自体がecad2には存在しない。

**E. PDF出力エラー時に生の例外メッセージ(ex.Message)をそのまま表示（既知パターンの再発）**
- 箇所: `src/Ecad2.App/Views/PdfPreviewDialog.xaml.cs:170`付近（`ExportButton_Click`のcatch節）
- `MainWindow.xaml.cs`には`TrySaveToFile`・`OpenButton_Click`で「隠密調査T-024節推奨」
  「忍者実機検出」とコメントで明記された、同種欠陥（`ex.Message`の生の技術文面表示）を
  **2度**修正した確立パターン（固定日本語文面＋対象パスのみ表示）が存在する。今回の実装は
  このパターンに反し、`$"PDF出力に失敗しました。\n{ex.Message}"`を直接表示する。
- verify: CONFIRMED。`PdfRenderSurface.Dispose()`の`_doc.Save(_path)`はtry/catch外の生I/O、
  ファイルロック中（PDFビューアで開いたまま等、実運用でありがちなケース）等で技術的な英語例外
  文面がそのまま表示されうる、実害のあるリスクと確認。

### 要判断・未文書化スコープ差分（1件、CONFIRMED＝要殿確認の意）

**F. GuiEcadにあった「プレビューなし直接エクスポート」の経路がecad2に存在しない**
- GuiEcad原本は「PDFプレビュー...」（2段階UI）と「PDF出力...」（直接1段階）の2つの独立
  メニューを持つが、ecad2は「PDF出力(_P)」1エントリのみで常にプレビューを経由する。
- verify: CONFIRMED（要殿確認の意）。`docs/todo.md`殿裁定②の文言「プレビュー機能＝今回実装」は
  プレビュー追加の可否を裁定したのみで、GuiEcadにあった直接出力経路の廃止は明示・文脈とも
  裁定されていない。事前調査書（隠密2、DoD(4)論点2）でも「直接エクスポートのみ v1」
  vs「プレビュー追加」の二択のみが提示され、「両方併存（GuiEcad同様）」の第三選択肢は
  一度も殿に提示されていなかった。実害は「再エクスポート時に毎回プレビュー確認の1手間が
  必ず挟まる」というUXコストのみ（バグではない）。意図的な簡素化として追認するか、
  直接出力ショートカットも追加するかは殿判断が必要。

### 経過観察（軽微、5件）

**G. BOM-skipガード（`&& document.Sheets.Count > 0`）のテストカバレッジ不足**
- `PdfExporter.cs:63-69`。`Devices.ByName`はSheetsと無関係に独立して構築可能なモデルであり、
  「機器あり・シート0件」の組み合わせは未テスト。ガード自体はIndexOutOfRange防止に必須で
  正しく機能しているが、将来のリファクタでガードが薄まっても検知できない。verify: CONFIRMED
  （4件目テストケース追加を推奨）。

**H. xref/CircuitNumberer.Numberの二重計算、ページ構成ロジックの重複実装**
- `PdfExportMenuItem_Click`でプレビュー用に1回、`PdfPreviewDialog.ExportButton_Click`→
  `PdfExporter.Export`内部で実出力用にもう1回、`CircuitNumberer.Number`・
  `CrossReferenceBuilder.Build`が独立に実行される。現状は両関数とも決定的（副作用なし・
  純粋関数的）かつダイアログがモーダルのため実害なし。ただし「単一の真実の源」が2箇所に
  分散した設計であり、Aの根本原因そのもの。将来どちらかだけ変更されれば再発する。

**I. `RenderPageCount`が同一シートに対し合計計算とループ内の2回呼ばれる（PdfExporter/BuildPageList双方）**
- 主回路シートでは`RenderPageCount`が`FreeLines`/`ConnectionDots`/`Frames`の線形走査を伴うため
  無駄な二重計算。現状規模では軽微。

**J. `PreviewPage`レコードの設計・`GetPageSize`のデフォルトアーム到達不能**
- `PageNumber`/`TotalPages`は`_pages`のインデックスから導出可能なのに独立フィールド化されている。
  `GetPageSize`の`_ => _dr.CrossRefPageSize()`はPageKind（3値private enum）に対し到達不能で、
  将来4つ目のPageKindが追加された際にサイレントに誤ったサイズを返すリスクを内包する
  （コンパイルエラーで気づけない設計）。

**K. `PdfPreviewCanvas`が`LadderCanvas`とVisualCollection定型コード・MmToDip定数を重複**
- 機能的には正しい（LadderCanvasの重い編集機能を持ち込まなかった判断は妥当）が、
  DrawingVisualホスティングの共通骨格（~15行）が別々にメンテされる状態。

**L. 複数ページ（`RenderPageCount>1`）になる大きいシートを検証するテストが無い**
- ページ行ウィンドウの境界計算自体は手計算で正しさを確認済み（off-by-oneなし）だが、
  回帰テストが無い。

### REFUTED（指摘却下、1件）

- **`PdfPreviewCanvas.DrawPage`の`PushTransform`/`Pop`不整合で例外がマスクされる** →
  verifierが実際に最小WPFアプリを構築し実測、`DrawingContext.Close()`は不均衡なPush
  （Pop忘れ）を許容し例外を投げないことを実証。指摘の技術的前提（Push超過でCloseが例外を
  投げる）が誤りと判明、却下。

## 総括

要修正5件（うちA・B・C・Dは実機で即座に体感される品質問題、Eはエラー時のみ顕在化）は
いずれも独立した複数の調査経路（GuiEcad原本比較・機械的code-review・手動ドメイン確認）が
一致して検出しており確度が高い。F（直接出力経路の欠落）はバグではなくスコープ判断事項として
殿確認を推奨。G〜Lは経過観察で、次の増分または着手時に拾えば足りる規模。
