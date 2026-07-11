# ecad2 仕様書：キャンバス表示

T-075（殿裁定、2026-07-11起票）体系の第8号、第4弾3件目。実装コード・殿裁定記録
（`docs/todo.md`/`docs/todo-archive.md`/`docs/proposed.md`）・忍者実機検証記録（`docs-notes/`配下）を
突き合わせ、「仕様として確定している挙動」を出典付きで明文化する。

---

## 1. ズーム機能

- プロパティ名は`CanvasScale`（`double`、`MainWindowViewModel.cs:61-68`）。既定値`1.0`、setterで
  `Math.Clamp(value, 0.25, 4.0)`により**最小25%〜最大400%**にクランプ（範囲外は無音でクランプ）。
- **Ctrl+ホイール操作**（`MainWindow.xaml.cs:133-139`）：Ctrl押下時のみ`CanvasScale += e.Delta > 0 ?
  0.1 : -0.1`——**1ホイールノッチにつき±0.1（10%）刻み**。
- **ズーム中心点**：`LayoutTransform`に`ScaleTransform`をバインドするのみで`RenderTransformOrigin`
  の指定はなく（既定0,0）、ズーム中心点を制御するロジックはコード上見当たらない。**マウス位置中心
  ではなく、要素既定の原点（左上）基準の拡大縮小**と読める。
- **キーボードでのズーム操作は未実装**（`MainWindow.xaml:408`コメント「段階8（キーボード規約の
  全体配線）でまとめて対応する」と明記、計画済み未着手）。
- **ズームリセット（100%へ戻す）コマンド・ボタンは存在しない**（不明/未実装）。
- ステータスバー表示：`"ズーム: {0:P0}"`形式。
- **非永続**：`CanvasScale`は`Persistence`層に一切参照なし（保存対象外、アプリ再起動でリセット）。

### 導入経緯（明示的な殿裁定タスクは不在）

ズーム機能自体を新規導入する殿裁定タスクは見当たらない——既に基盤実装済みの前提としてT-021
（パン・座標整合の検証観点）に登場するのみ。最新の棚卸し（隠密2、2026-07-11）でも「A（基盤のみ）」
区分とされ、「メニュー/ツールバーの明示的ボタン、Ctrl++/Ctrl+-キー操作は計画済み・未着手事項」と
明記されている。「全体表示（フィット）」も`CanvasViewport.Reset()`が固定既定値へのリセットのみで
実際のフィット計算ではないとの指摘もある（`docs/ecad2-guiecad-unwired-features-survey-onmitsu2.md`）。

---

## 2. グリッド表示切替

- `IsGridVisible`（既定`true`、非永続——**殿裁定2026-07-11**「アプリ再起動で既定値へ戻る」）。
- View側実体は`LadderCanvas.ShowGrid`：setterで値が変わると`DiagramRenderer`を
  `new DiagramRenderer(options: new RenderOptions { ShowGrid = value })`で**作り直す**
  （インプレース更新ではなく再インスタンス化方式）。
- 反映経路：`ViewModel_PropertyChanged`が`IsGridVisible`変更を検知し`LadderCanvasHost.ShowGrid`
  セット→`RedrawCanvas()`を明示呼び出し（**バインディングだけでは自動再描画されない**）。
- 描画ロジック：`DiagramRenderer.DrawGrid`。縦線=列境界（行中心±0.5セル分に短縮）、横線=行中心のみ、
  色は`DrawingTheme.GridGray`（`#D2D2D2`、線幅0.10mm）。`App.xaml`の`WorkAreaGridBrush`（同色）は
  定義のみで実際の描画には**未使用**。
- Ctrl+G：`Key.G when Ctrl`でトグル。メニュー側は`IsCheckable="True"`表示のみ、実処理はキー入力
  ハンドラが担う。

### 裁定経緯（T-056、殿裁定4点、2026-07-11）

(1)トグルUI=案A（メニュー項目へIsCheckable+Command結線）(2)既定値=表示で起動（現状維持）
(3)ショートカット=Ctrl+G割当（現行未使用・衝突なし）(4)**永続化=今回は非永続**（共通設定機構は
P-052として別途起票）。実装は裁定どおり完全一致。

---

## 3. 空状態⇔作業領域の配色切替

- `HasProject`（算出プロパティ、`Document.Sheets.Count > 0`、setterなし）。
- `EmptyStateBackgroundBrush = #24325A`（濃紺）、`WorkAreaBackgroundBrush = White`。
- 切替はXAML `DataTrigger`（`ScrollViewer.Style`の`Style.Triggers`）による純粋な宣言的実装、
  コードビハインド処理なし。

### 色決定経緯

起源はT-012（GX Works3実機調査）の新発見「状態依存配色」。殿裁定（2026-07-03）「ecad2で新規導入、
GX Works3風の濃紺⇔白切替も踏襲」、実装方式は`HasProject`+DataTrigger。実装完了時の初期色は
**`#1E2A47`**だったが、殿実機確認（2026-07-05）で色見本3案から選択、「ほんの少しだけ明るく」の
裁定で**`#24325A`へ調整**（現行値）。

### 実機確認

起動直後スクリーンショットのピクセル値実測で「R=36 G=50 B=90 = `#24325A`と完全一致」を確認済み
（`docs-notes/ecad2-t019-second-round-verification-ninja.md:52`）。

---

## 4. キャンバスの座標系

3段階の変換：**グリッド座標(Row/列境界) → mm実座標 → DIP → (LayoutTransformでの画面スケール)**。

- mm変換：`GridGeometry`が`X(boundary)=MarginMm+boundary*CellMm`（列0.5刻み対応）、
  `YRow(row)=MarginMm+(row+0.5)*CellMm`（行中心）、逆変換`ColAt`/`RowAt`（Floor）、`BoundaryAt`/
  `BoundaryAtHalf`（Round、縦コネクタ用0.5刻みスナップ）を提供。
- mm→DIP：`MmToDip = 96.0/25.4`。
- `ToGridPos`：クリック位置(DIP)→`GridPos`変換の入口。
- `CellRectDip`：選択ハイライト・UI Automationの`GetBoundingRectangleCore`で共用する1セル分の矩形算出。
- **スケール（ズーム）は上記変換の外側、`LayoutTransform`で最後に適用される**ため、`LadderCanvas`
  内部のヒットテスト・座標変換ロジック自体はズーム非依存（DIPローカル座標のまま）。

---

## 5. 選択セルのハイライト表示

`SelectedCellPen`：`Brushes.OrangeRed`、太さ2.0、塗りつぶし無し（枠線のみ）。`SelectedCell`の変更は
`RedrawCanvas()`経由で反映。他の選択系（縦コネクタ=太さ3.5、WireBreak/ConnectionDot=塗り円、
FreeLine=太線）も同一`Draw()`内で類似方式（いずれも`OrangeRed`系で統一）。

---

## 6. ウィンドウリサイズ時の再描画

`RedrawCanvas()`呼び出しは`MainWindow.xaml.cs`内で**34箇所**、いずれもシート切替・選択状態変更・
要素配置削除・Undo/Redo等の**モデル変更イベント**に紐づく明示呼び出し。

**ウィンドウ/ペインのリサイズ自体をフックするハンドラは存在しない**（`SizeChanged`等は0件）。
`LadderCanvas`は`Draw()`時にmm由来のDIP固定値でサイズをセットする`FrameworkElement`であり、
ホストの`ScrollViewer`がリサイズ時のスクロールバー表示・クリップをWPF標準レイアウトで処理する
（設計意図の明文コメントはなく、構造からの推定）。

---

## 7. 既知の罠・未解決事項

### 未解決（描画座標系そのものに起因する疑い）

- **P-027**：ウィンドウ縦リサイズでキャンバス内の行1描画位置が約56pxオフセットし、旧座標クリック
  で存在しないはずの「行0」が選択され機器表に残存データが発生する事象。断定なし・再現条件未特定、
  **保留継続（殿裁定2026-07-10）**。T-033の原点ズレ系・P-024との類似性が指摘されている。
  シート/ドキュメント管理領域の「リサイズ直後のプロセス消失」とは**別事象**。
- **P-012**：行0要素配置時の母線・シンボル描画異常（`docs/spec/ecad2-spec-placement.md`7節と
  同一事象、原因未特定）。
- **P-022/P-024**：選択範囲（行-1・列-2まで）は仕様だが、**その範囲が描画範囲に含まれてしまう
  点は殿教示によりバグと確認済み**。配置側の下限ガードはT-045で対処済みだが、**描画側の対応は
  未着手のまま残課題**。

### 誤診断からの訂正事例（実機検証固有）

- Ctrl+Gでのグリッド反映確認で「描画反映されず」とNG誤診断→殿の実機操作で正常確認・訂正（原因は
  UIA経由キー送信の検証手順側）。
- ズーム操作直後に選択が未選択に戻るとの観測→殿自身の実機確認で再現せず、UI Automation合成操作
  固有の副作用と訂正（P-007=withdrawn）。

---

## 8. 実機確認記録

- Ctrl+ホイールでのズーム（100%→150%）は正常動作を確認。
- グリッド表示切替は6件一括検証（2026-07-11）で全観点OK（非表示時もグリッド線のみ消え要素・母線等
  は残存）。
- 空状態⇔作業領域の配色切替はピクセル値実測で確認済み（3節参照）。
- ステータスバー「ズーム: 100%」の初期値正常性は複数の実機回帰記録で継続確認。

## 不明点

- ズームリセット（100%へ戻す）機能の有無（コード上見当たらず、未実装と推測）。
- ウィンドウリサイズ時の再描画ハンドラが存在しない設計の意図（明文コメントなし、WPF標準レイアウト
  依存という構造からの推定に留まる）。
