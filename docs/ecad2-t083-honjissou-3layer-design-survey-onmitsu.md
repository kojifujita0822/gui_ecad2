# T-083本実装向け調査: 3層連動設計案・増分分割案(隠密)

調査日: 2026-07-16　調査者: 隠密　依頼元: 家老（T-083本実装・着手前調査）

## 前提

殿裁定（2026-07-16）：起動時既定=ライトモード／OS追従=なし／切替反映=即時。本調査はこの3点を
前提とする。

着手前確認【MUST】：`docs/`配下の既存T-083調査書2件
（`ecad2-t083-fluent-thememode-survey-onmitsu.md`、`ecad2-t083-avalondock-vs2013-theme-package-survey-onmitsu.md`）
を確認済み。本調査はこの2件の結論を踏まえた続編（3層連動設計・増分分割）に絞る。

---

## 結論1: Fluent ThemeMode採用可否

**不採用を維持する（既存調査を踏襲、再結論）。**

既存調査書（2026-07-14初回、2026-07-15独立検証、いずれも隠密）で「.NET 10時点でも
`ThemeMode`は`[Experimental("WPF0001")]`のまま解除されず、MessageBox非対応・フラッシュ
不具合未解決・カスタムスタイル併用で見た目崩壊の懸念あり」と結論済み。2026-07-15時点の
API公式リファレンス確認（最終更新2026-07-01）でも実験的機能の位置づけは変わらず。本調査
時点（2026-07-16）で状況を覆す新情報は無し（再Web調査は既存結論と1日しか離れておらず
省略、既存結論の鮮度は十分と判断）。

**帰結**：UIクローム（メニュー・ツールバー・ダイアログ）のダークモードはFluent任せに
できないため、下記「層C」は自前のResourceDictionary切替方式（WPF標準機能のみ、新規外部
依存なし）で実現する。

---

## 結論2: 3層連動設計案

家老事前検分どおり3層構成。各層の現状（Explore調査、2026-07-16実施）と対応方針を示す。

### 層A: 作図キャンバス色（Core `DrawingTheme.cs`）

**現状**：PoC完了済み（コミット8a24318）。`DrawingTheme.Default`/`Dark`の2パレット
（`Foreground`/`Background`/`GridColor`/`TableHeaderFill`＋テーマ非依存の意味色`Blue`/
`Powered`/`ManualForced`/`NonEnergizedGray`）をCore側に完備。App層は
`MainWindowViewModel.IsDarkMode`（bool、`IsGridVisible`と同型）→
`MainWindow.xaml.cs`の`PropertyChanged`ハンドラで`LadderCanvasHost.Theme`をセットし
`RedrawCanvas()`を呼ぶ構造で結線済み。

**本実装で追加すべき点**：
- 永続化は**不要と推測**——殿裁定「起動時既定=ライトモード」は、アプリ再起動のたびに
  ライトモードへ戻す仕様と解釈できる（起動時に前回値を復元する仕様なら「既定」という
  表現にならないはず）。ただし明示的な裁定文言ではないため、着手前に家老経由で殿へ
  再確認することを推奨する（不明点として下記にも記載）。
- `LadderCanvas.cs`内の選択色系（`Brushes.OrangeRed`×6箇所、`Brushes.DodgerBlue`×2箇所、
  `Brushes.White`×1箇所、詳細は下記「App層直接色指定一覧」）は意味色（選択強調）として
  テーマ非依存で妥当だが、暗背景（`DrawingTheme.Dark.Background`=RGB(32,34,38)）上での
  視認性は未検証。既存の意味色（`Powered`=RGB(230,60,0)等）と同様「テーマ間固定」の
  設計方針に揃えるなら現状維持でよいが、暗背景での視認性は実機確認が必要。

### 層B: AvalonDockドッキングクローム

**現状**：`Ecad2.App.csproj`には`Dirkster.AvalonDock`(4.74.1)のみ参照、テーマパッケージ
（`Dirkster.AvalonDock.Themes.VS2013`）は未導入。4つの独立`DockingManager`
（`PlacementToolBarDockingManager`/`LeftPaletteDockingManager`/`RightPanelDockingManager`/
`OutputPanelDockingManager`）に`.Theme`設定なし＝AvalonDock既定テーマ（Aero系）のまま。

**対応方針**：
- 新規依存として`Dirkster.AvalonDock.Themes.VS2013`(4.74.1、nuget.org公式版)を追加する
  （既存調査書で採用決定済み、`docs/todo.md` T-058節に記録済み。バージョンは本体
  `Dirkster.AvalonDock`4.74.1と完全一致）。
- `IsDarkMode`変更時に4つの`DockingManager`それぞれへ`Theme = IsDarkMode ? new Vs2013DarkTheme() : new Vs2013LightTheme()`
  相当を設定する（クラス名はパッケージAPI確認要、着手時に侍が実装しながら確定）。
- 4箇所への同型適用が必要なため、PR-17（横展開漏れ）と同種の再発パターンに注意。共通
  ヘルパーメソッド化を推奨。

### 層C: UIクローム（メニュー・ツールバー・ダイアログ）

**現状**：`App.xaml`は4ブラシのみの薄いリソース辞書（後述）。`ThemeMode`属性・Fluent
明示指定なし。メニュー・ツールバー・大半のダイアログはWPF既定のシステム色
（`SystemColors`系）依存。一部のパネル（`FindBar`・`ElementPlacementBar`・
`RungCommentEditor`）のみ`Background="White"`固定。

**対応方針（Fluent不採用ゆえ自前方式）**：
- `Theme.Light.xaml`/`Theme.Dark.xaml`の2つの`ResourceDictionary`を新設し、UIクローム用の
  背景色・前景色ブラシ（メニュー背景、ツールバー背景、ダイアログ背景、文字色等）を
  `DynamicResource`キーとして定義する。
- `App.xaml`既存の4ブラシ（`EmptyStateBackgroundBrush`/`WorkAreaBackgroundBrush`/
  `WorkAreaGridBrush`/`TestModeActiveBrush`）もこの方式へ統合する（下記「シート0件時
  バグ」の解消と直結）。
- `IsDarkMode`変更時に`Application.Current.Resources.MergedDictionaries`を差し替える
  （WPF標準の伝統的テーマ切替手法、新規外部依存なし）。
- `MainWindow.xaml`のメニュー(`<Menu>`)・`ToolBarTray`/`ToolBar`・各種ダイアログ
  （`AddSheetDialog`等）・固定色パネル（`FindBar`/`ElementPlacementBar`/
  `RungCommentEditor`）を`DynamicResource`参照へ書き換える。
- `PdfPreviewCanvas.cs`(45行、`Brushes.White`)と`PdfPreviewDialog.xaml`(27行、
  `#FF6E6E6E`)は**対象外（現状維持）**——`DrawingTheme.cs`の設計コメント「PDFは常に
  Default（提出図面は白地黒線）」と整合する意図的な固定値のため。

### シート0件時のキャンバス色固定バグ（範囲外の気づき）の解消方針

**原因（推測、コード調査から）**：シートが0件の時は`LadderCanvas`自体が非表示になり、
代わりに`EmptyStateBackgroundBrush`（`App.xaml`固定値`#24325A`濃紺）を使う別パネルが
表示される構造と見られる（`MainWindow.xaml`/`MainWindowViewModel.cs`でこのブラシキーを
参照）。このブラシは`DrawingTheme`非依存の固定`StaticResource`のため、`IsDarkMode`の
On/Offに関わらず常に同じ色になっている。

**解消方針**：上記「層C」の対応に含める——`EmptyStateBackgroundBrush`を
`Theme.Light.xaml`/`Theme.Dark.xaml`側の`DynamicResource`に統合すれば、他のUIクローム色と
同じ仕組みで自動的にテーマ追従する。独立対応は不要、層Cの増分内で一緒に解消できる。

---

## App層直接色指定 一覧（Explore調査、テーマ整合確認用）

| ファイル | 箇所 | 色 | 用途 | テーマ対応要否 |
|---|---|---|---|---|
| `Views/LadderCanvas.cs` | 83,87,112,120,126,129行 | `Brushes.OrangeRed` | 選択強調(枠線/コネクタ/配線/フリーライン/接続点/画像枠) | 意味色、現状維持が既定線だが暗背景視認性は要実機確認 |
| `Views/LadderCanvas.cs` | 96行 | `Brushes.DodgerBlue` | 検索ハイライト点線 | 同上 |
| `Views/LadderCanvas.cs` | 249行 | `Brushes.White` | 選択画像の背景塗り | 要検討(暗背景で浮く可能性) |
| `Views/LadderCanvas.cs` | 182行 | `_theme.Background` | キャンバス背景 | 対応済み(PoC) |
| `Converters/DiagnosticSeverityToBrushConverter.cs` | 16-19行 | Firebrick/DarkOrange/Gray/Black | 診断重大度表示 | 意味色、現状維持が妥当(推測) |
| `Views/PdfPreviewCanvas.cs` | 45行 | `Brushes.White` | PDF用紙背景 | 対象外(PDFは常時白地) |
| `Views/PdfPreviewDialog.xaml` | 27行 | `#FF6E6E6E` | プレビュー外周グレー | 対象外(同上) |
| `Views/SheetReorderInsertionAdorner.cs` | 13行 | `Brushes.DodgerBlue` | シート並べ替え挿入線 | 意味色、現状維持が妥当(推測) |
| `App.xaml` | 13-18行 | `EmptyStateBackgroundBrush`/`WorkAreaBackgroundBrush`/`WorkAreaGridBrush`/`TestModeActiveBrush` | 空状態背景/作業領域背景/グリッド/テストモードON表示 | 層Cへ統合(シート0件バグ解消含む) |
| `MainWindow.xaml` | 558,629,689,699,840,855,936行 | 固定`White`/`Gray`等 | `FindBar`/`ElementPlacementBar`/`RungCommentEditor`等の固定背景パネル | 層Cへ統合 |

---

## 結論3: 増分分割案（侍向け、各増分ごとに忍者検証を挟む前提）

1. **増分1: AvalonDockテーマ連動（層B）**
   `Dirkster.AvalonDock.Themes.VS2013`導入＋4つの`DockingManager`への`IsDarkMode`連動
   Theme設定。既存PoCのIsDarkMode配線に相乗りできるため比較的独立・低リスク。

2. **増分2: UIクロームResourceDictionary基盤構築（層C基盤）**
   `Theme.Light.xaml`/`Theme.Dark.xaml`新設、`App.xaml`既存4ブラシの統合、
   `MergedDictionaries`差し替えロジック実装、`MainWindow.xaml`のメニュー・ツールバー本体を
   `DynamicResource`化。**シート0件時バグの解消もこの増分に含む**
   （`EmptyStateBackgroundBrush`統合のため自然に解消）。

3. **増分3: UIクローム残箇所の対応（層C残り）**
   各種ダイアログ（`AddSheetDialog`等）・固定色パネル（`FindBar`/`ElementPlacementBar`/
   `RungCommentEditor`）の`DynamicResource`化。増分2の基盤を前提とするため増分2の後に
   実施。

4. **増分4: 意味色の暗背景視認性確認・要調整箇所の色調整**
   `LadderCanvas.cs`の選択色系（`OrangeRed`/`DodgerBlue`/`White`）を`DrawingTheme.Dark`
   背景上で実機確認し、視認性不足があれば調整（テーマ非依存を維持したまま値のみ調整するか、
   テーマ別に持たせるかはUI/UX判断ゆえ、視認性不足が実際に見つかった場合のみ殿確認）。

各増分は独立してビルド・動作確認可能な粒度とした。増分1と増分2は依存関係が薄いため
並行着手も可能（侍1名運用のため順次でよいが、順序を入れ替えても支障はない）。

---

## 不明点

- **永続化要否**：起動時既定=ライトモードから「永続化不要」と推測したが、明示裁定では
  ない。着手前に家老経由で殿へ再確認を推奨する。
- **`LadderCanvas.cs`選択色系の暗背景視認性**：未検証（増分4で忍者確認予定）。
- **AvalonDock `Vs2013DarkTheme`/`Vs2013LightTheme`のクラス名・APIの正確な形**：
  パッケージの型定義未確認（Web一次情報のみ、実コードでのAPI確認は増分1着手時に侍が
  NuGetパッケージのメタデータを直接参照して確定させる想定）。

## 派生提案の有無

範囲外の新規作業提案なし（シート0件時バグは家老指定の検証観点内のため、既存タスク
T-083の一部として扱う）。

---

## 出典

- 既存調査書2件（本文冒頭記載）
- Exploreエージェントによるコード実態調査（2026-07-16、`DrawingTheme.cs`全文、
  コミット8a24318差分、`src/Ecad2.App/`配下の直接色指定grep、AvalonDock設定grep、
  `App.xaml`/`MainWindow.xaml`リソース定義）
- `docs/todo.md` T-083節・T-058節
