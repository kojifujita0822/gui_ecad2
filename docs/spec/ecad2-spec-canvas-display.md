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
  色は`DrawingTheme.GridGray`（`#D2D2D2`、線幅0.10mm、**ライトモード＝`DrawingTheme.Default`時**）。
  `App.xaml`の`WorkAreaGridBrush`（同色）は定義のみで実際の描画には**未使用**。
  **（2026-07-21追記、T-083反映）ダークモード（`DrawingTheme.Dark`、`src/Ecad2.Core/Rendering/
  DrawingTheme.cs`91-98行）ではグリッド色は`#464A50`相当（`GridColor = new(255,70,74,80)`）へ
  切り替わる**（DrawingTheme全体がダーク/ライトの2テーマを持つ設計、キャンバス描画色は
  `IsDarkMode`に連動して`DrawingTheme.Default`/`Dark`を切り替えて選択される）。
- Ctrl+G：`Key.G when Ctrl`でトグル。メニュー側は`IsCheckable="True"`表示のみ、実処理はキー入力
  ハンドラが担う。

### 裁定経緯（T-056、殿裁定4点、2026-07-11）

(1)トグルUI=案A（メニュー項目へIsCheckable+Command結線）(2)既定値=表示で起動（現状維持）
(3)ショートカット=Ctrl+G割当（現行未使用・衝突なし）(4)**永続化=今回は非永続**（共通設定機構は
P-052として別途起票）。実装は裁定どおり完全一致。

---

## 3. 空状態⇔作業領域の配色切替

- `HasProject`（算出プロパティ、`Document.Sheets.Count > 0`、setterなし）。
- `EmptyStateBackgroundBrush`・`WorkAreaBackgroundBrush`とも`DynamicResource`（**2026-07-21追記、
  T-083反映**、`src/Ecad2.App/Themes/Theme.Light.xaml`・`Theme.Dark.xaml`）。ライトモードは旧版
  記載どおり`EmptyStateBackgroundBrush=#24325A`（濃紺）・`WorkAreaBackgroundBrush=White`。
  **ダークモードは両方とも`#FF202224`（同一色）**——空状態と作業領域の背景色に視覚的な区別が
  無い状態になっている（意図的な設計判断か見落としかは本調査では確認できず、**気づきとして
  記録**、下記「気づき」節参照）。
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

## 8. 行コメントエディタのクローズ経路

- **確定仕様（殿裁定2026-07-12）**: 行コメントエディタ（右母線右側ダブルクリック、またはF2キーで
  開く編集ボックス）のクローズ経路は**Enter/Tab確定・Esc取消のみ**。ecad2ウィンドウ**内側**の
  クリックでは閉じない（GuiEcad由来の「フォーカスロストで確定」という当初想定は成立せず、
  `MainContentArea.IsEnabled`（`IsRungCommentEditorVisible`連動）によりエディタ表示中はキャンバス・
  メニュー・ツールバー等が無効化されるため、無効化された要素は`LostKeyboardFocus`を発生させない
  ——**誤操作防止の無効化を優先する仕様として確定**）。
- **ウィンドウ非アクティブ化時のみ**：ecad2ウィンドウ自体が非アクティブ化される操作（他アプリへの
  切替等）では`RungCommentBox_LostKeyboardFocus(NewFocus=null)`が発火し、`CommitRungCommentEditor
  (restoreFocus: false)`で確定・クローズする。
- **経緯**: T-080往復2周目の実機検証で「窓内クリックで閉じない」が一度NG（回帰）と誤診断され
  （`docs-notes/ecad2-t080-ninja-verification-round1.md`）、その後「無効化下でもWPFはフォーカスを
  正しく外す」という反証実測が出たが（`docs-notes/ecad2-t080-ninja-verification-round2.md`）、
  これは殿の操作対象がアプリ内キャンバスではなくアプリ外（ウィンドウ非アクティブ化）だったことに
  よる誤認識と判明し撤回。最終実機確認（`docs-notes/ecad2-t080-ninja-final-verification.md`観点6）
  で「窓内クリックだけでは確定せず、窓非アクティブ化で確定する」ことが確定し、隠密調査書
  （`docs/ecad2-t080-rung-comment-mouse-bug-investigation-onmitsu.md`）の当初主仮説が正しかったと
  確定した。往復3回にわたる実測の往還を経ており、一次情報（実機）を都度優先し訂正を重ねた結果の
  最終形である。
- **修正しない**：上記は仕様として確定し、コード修正は行わない（殿裁定2026-07-12）。

### フレームラベルエディタも同一仕様（T-067(4)、2026-07-19確認）

- T-067(4)（フレームラベルインライン編集、行ラベルダブルクリックで開く編集ボックス）で、殿より
  「編集確定の挙動が確定しない」との報告を受け調査した結果、上記と**完全に同一のクローズ経路**
  （Enter/Tab確定・Esc取消のみ、ウィンドウ内側クリックでは確定しない、ウィンドウ非アクティブ化
  時のみ`FrameLabelBox_LostKeyboardFocus`→`CommitFrameLabelEditor`→`CloseFrameLabelEditor`が
  発火）であることを確認した。`MainContentArea.IsEnabled`（`IsFrameLabelEditorVisible`を含む
  OR条件）による無効化構造が行コメントエディタと同型であることが根本原因（隠密の一次ソース調査、
  `docs-notes/ecad2-t067-4-focusloss-verification-ninja.md`）。忍者実機検証でウィンドウ非アクティブ化
  操作（Alt+Esc）時の確定発火を確認し裏付けた（`docs-notes/ecad2-t067-4-nonactivate-verification-ninja.md`）。
- **修正しない**：上記8節と同一仕様として確定し、コード修正は行わない（殿裁定2026-07-19）。

---

## 9. 実機確認記録

- Ctrl+ホイールでのズーム（100%→150%）は正常動作を確認。
- グリッド表示切替は6件一括検証（2026-07-11）で全観点OK（非表示時もグリッド線のみ消え要素・母線等
  は残存）。
- 空状態⇔作業領域の配色切替はピクセル値実測で確認済み（3節参照）。
- ステータスバー「ズーム: 100%」の初期値正常性は複数の実機回帰記録で継続確認。

## 不明点

- ズームリセット（100%へ戻す）機能の有無（コード上見当たらず、未実装と推測）。
- ウィンドウリサイズ時の再描画ハンドラが存在しない設計の意図（明文コメントなし、WPF標準レイアウト
  依存という構造からの推定に留まる）。

## 気づき（範囲外、着手せず、2026-07-21追記）

- ダークモード（T-083）で`EmptyStateBackgroundBrush`・`WorkAreaBackgroundBrush`が同一色
  （`#FF202224`）になっており、空状態⇔作業領域の視覚的区別が消えている（3節参照）。意図的な
  設計判断（ダークモード全体の暗い配色に合わせた）か見落としかは本調査（陳腐化点検）の範囲では
  判定できない。派生の気づきとして`docs/proposed.md`経由で家老へ報告する。
