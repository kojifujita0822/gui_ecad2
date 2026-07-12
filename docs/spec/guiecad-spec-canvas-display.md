# GuiEcad仕様書：キャンバス表示

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）のキャンバス表示実装をExplore委譲調査で纏め、
`docs/spec/ecad2-spec-canvas-display.md`（ecad2側、T-075起票）と比較可能な形で整理する。

対応するecad2側仕様書：`docs/spec/ecad2-spec-canvas-display.md`

---

## 1. ズーム機能

`CanvasViewport`クラス（`CanvasViewport.cs:9-45`）がUI非依存の状態＋計算クラスとして`Zoom`
（既定1.6）・`PanX`/`PanY`（既定20,20）を保持。

- 範囲：`Math.Clamp(Zoom*factor, 0.2, 12.0)`（32行）——**20%〜1200%**。ecad2の25%〜400%とは
  範囲が大きく異なる。
- Ctrl+ホイール：マウス位置固定のズーム（`ZoomBy(1.1^(delta/120), position)`）、Pan補正式で
  カーソル下の点が画面上で動かないよう調整（`MainPage.xaml.cs:757-771`、`CanvasViewport.cs:29-36`）。
- **刻み幅は乗算方式**（ホイール1ノッチ=`1.1^n`倍、ボタン/キー操作=`1.2`倍）——ecad2の「±0.1固定
  加算」とは方式が異なる。
- Ctrl非押下ホイールは**縦パン**（`PanY += delta/3.0`）——**パン機能自体がecad2に存在しない概念**。
- メニュー/ツールバー/キーボードショートカット（Ctrl++/Ctrl+-/Ctrl+0）が全て実装済み、
  `MainPage.KeyBindings.cs:103-108`でユーザー再割当て可能なキーバインドとして正式登録。
- リセット：`CanvasViewport.Reset()`で固定既定値（Zoom=1.6等）へ戻すのみ、内容に応じた実フィット
  計算ではない。
- シート別ビュー状態記憶：`SaveViewState`/`RestoreViewState`（`MainPage.Sheets.cs:72-90`）で
  シートごとにZoom/Pan/ホバーセルを退避・復元（アプリ内メモリのみ、非永続）。
- **ステータスバーにズーム%表示は存在しない**（grep該当なし、主キャンバス用としては）。

---

## 2. グリッド表示切替

`_showGrid`（`MainPage.xaml.cs:155`）**既定値`false`**（`RenderOptions.ShowGrid`も既定false、
コメント明記）——**ecad2は`IsGridVisible`既定`true`（表示で起動、殿裁定）と真逆**。

メニュークリック（`OnGridToggle`）のみが切替経路——**キーボードショートカット（Ctrl+G等）は
存在しない**（`MainPage.KeyBindings.cs`にも該当ID登場せず）。

描画アルゴリズムは`DiagramRenderer.DrawGrid`（407-420行）：縦線=列境界（行中心±0.5セル短縮）、
横線=行中心のみ——**ecad2の記述と完全一致**するアルゴリズム。色・線幅も既定は`GridGray #D2D2D2`・
0.10mmで一致するが、**GuiEcadはテーマ切替でグリッド色も変わる**点がecad2と異なる（3節参照）。
`_showGrid`は都度インスタンス生成の`RenderOptions`経由で、専用の永続化コードは無い。

---

## 3. ダークモード・配色テーマ（ecad2に相当機能なし、GuiEcad独自の2系統）

GuiEcadには**独立した2系統**のダークモードが存在（コメント「UIクロムのダークモードとは独立に切替」）：

**UIクロムのダークモード**：`OnDarkModeToggle`が`RootGrid.RequestedTheme`を切替、
`%MyDocuments%\GuiEcad\ui-theme.txt`へ永続化、**アプリ再起動をまたいで維持**。

**キャンバス（作図色）のダークモード**：`OnCanvasDarkToggle`が`_drawingTheme`を
`DrawingTheme.Dark`/`Default`へ切替。`Dark`は`Foreground(225,225,225)`・`Background(32,34,38)`・
`GridColor(70,74,80)`。**PDF出力は常に`Default`固定**。`%MyDocuments%\GuiEcad\drawing-theme.txt`
へ別ファイル永続化。通電色・接続済み色・手動強制色は「意味色」としてテーマ間で固定。

**ecad2側にはダークモード・配色テーマ切替の記述が一切ない**（`ecad2-spec-canvas-display.md`にも
`menu-toolbar.md`にも記述なし）。ecad2の「空状態⇔作業領域の配色切替」（`HasProject`による濃紺⇔白）
は状態依存の背景色2値切替であり、GuiEcadの「作図色ダークモード」（ユーザー操作によるテーマ全体
切替、永続化あり）とは目的も実装方式も別物。

---

## 4. 座標変換の仕組み（3段階は一致、ズームの合成位置が異なる）

- グリッド座標→mm：`GridGeometry`（`GridGeometry.cs:1-33`）——`X(boundary)`/`YRow(row)`/逆変換
  `ColAt`/`RowAt`/`BoundaryAt`/`BoundaryAtHalf`——**ecad2側の記述内容と一致するアルゴリズム**。
  実運用値`CellMm=9.0, MarginMm=20.0`。
- mm→DIP：`DipsPerMm=96.0/25.4`——ecad2の`MmToDip`と同一定数。
- **ズームの合成位置がecad2と根本的に異なる**：GuiEcadは`OnDraw`内で
  `Matrix3x2.CreateScale(scale)*Matrix3x2.CreateTranslation(PanX,PanY)`を描画セッションの合成行列へ
  直接積む方式（`MainPage.Drawing.cs:47-48`）。ecad2はズームを外側の`LayoutTransform`のみに適用し
  内部ロジックはズーム非依存（ecad2 spec 4節）。**GuiEcadはヒットテストも`ToWorld`でZoom/Pan除去が
  必須**（`MainPage.xaml.cs:699`）——構造的に異なる。

---

## 5. 選択・ハイライト表示（色の使い分け設計がecad2と異なる）

ecad2は「OrangeRed系で統一」だが、GuiEcadは**主要選択系はBlue系（塗り/枠）**、**検索・DRC・
キーボードフォーカスはオレンジ系**という用途別の使い分け：

| 対象 | 色・方式 |
|---|---|
| 単一要素選択 | `Blue(0,80,220)`枠、線幅0.3mm |
| 複数選択セット | 半透明青`(80,0,120,255)`塗り |
| 検索結果（現在候補／その他） | 濃いオレンジ／薄いオレンジ塗り |
| キーボード配置モードのフォーカスセル | オレンジ枠`(220,255,140,0)` |
| DRCジャンプ先 | 半透明オレンジ帯 |
| 配置プレビュー（ゴースト） | 半透明青紫`(120,0,120,255)` |

選択枠の線幅も0.3〜0.5mmとecad2（2.0〜3.5mm）より**桁違いに細い**。選択種別ごとに描画関数・
パディング計算が個別実装されており、ecad2の「同一`Draw()`内で類似方式に統一」とは粒度が異なる。

---

## 6. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | 出典 |
|---|---|
| キャンバスパン（縦ホイール＋自由ドラッグ） | `CanvasViewport.cs:15-16`、`MainPage.Pointer.cs:224,408,549-552` |
| マウス位置固定のズーム中心点補正 | `CanvasViewport.cs:29-36` |
| シート別ビュー状態記憶(Zoom/Pan/カーソル) | `MainPage.Sheets.cs:72-90` |
| 作図色ダークモード＋UIクロムダークモード（両方永続化） | `MainPage.xaml.cs:260-339` |
| 配置プレビュー（ゴースト表示） | `MainPage.Drawing.cs:144-160` |
| キーボード配置モードのフォーカスセル枠 | `MainPage.Drawing.cs:162-169` |
| 限時タイマ接点の残り時間バッジ表示 | `MainPage.Drawing.cs:193-240` |

### (2) ecad2のみにある機能

| 機能 | ecad2側出典 | GuiEcad側の状況 |
|---|---|---|
| Ctrl+ホイールの±0.1固定刻みズーム | `ecad2-spec-canvas-display.md` | 乗算方式(`1.1^n`/`1.2`倍) |
| ステータスバーへのズーム%常時表示 | 同上 | 相当コントロールなし |
| シート0件時の空状態配色切替 | 同上 | 最終シート削除ガードにより0件状態自体が発生しない |

### (3) 両方にあるが挙動が異なる点

| 項目 | ecad2 | GuiEcad |
|---|---|---|
| ズーム範囲 | 25%〜400% | 20%〜1200% |
| ズーム刻み | ±0.1固定加算 | 乗算方式 |
| ズーム中心 | 制御ロジックなし | マウス位置固定 |
| ズームのキーボード操作 | 未実装(計画のみ) | 実装済み・再割当て可能 |
| グリッド既定表示 | 表示で起動(true) | 非表示で起動(false) |
| グリッド切替キー | Ctrl+G | なし(メニューのみ) |
| グリッド色 | 常時固定 | テーマ依存 |
| ズームの合成位置 | 外側LayoutTransform、ヒットテスト非依存 | 描画合成行列に直接組込み、ヒットテストもZoom/Pan除去必須 |
| 選択ハイライト基調色 | OrangeRed系統一 | Blue系主体＋検索/DRC/フォーカスはオレンジ系 |
| 選択枠線幅 | 2.0〜3.5mm | 0.3〜0.5mm |
| ダークモード | 存在しない | UIクロム/作図色2系統、両方永続化 |

---

## 出典

- GuiEcad: `CanvasViewport.cs:1-45`、`GridGeometry.cs:1-33`、`MainPage.Drawing.cs:45-270`、
  `MainPage.xaml.cs:155,260-339,699,757-771`、`MainPage.Sheets.cs:72-90`、
  `MainPage.KeyBindings.cs:103-108`、`GuiEcad.Core/Rendering/DiagramRenderer.cs:10,15-16,407-420`、
  `DrawingTheme.cs:17-32,54,62,76-86`（Explore委譲調査、行番号は本文各所参照）
- ecad2: `docs/spec/ecad2-spec-canvas-display.md`（比較対象）

## 不明点

- シート0件状態がGuiEcadの他の削除・エラーリカバリ経路まで含めて本当に発生し得ないかは未確認
  （最終シート削除ガードのみ確認、推測含む）。
- `_showGrid`が本当にどこにも永続化されていないか（`ui-theme.txt`/`drawing-theme.txt`以外の全数
  grepは未実施、推測）。
- ウィンドウリサイズ時の再描画挙動は本調査スコープ外のため未確認。
