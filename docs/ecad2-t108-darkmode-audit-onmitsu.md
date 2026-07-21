# T-108 ダークモード対応漏れの全点検（隠密）

日付: 2026-07-21

## 1. AddSheetDialog修正（コミット`9c14b8b`）レビュー

### (a) DoD整合確認

`docs/todo.md` T-108節「進め方」1.「今回の具体的箇所（`AddSheetDialog`のRadioButton）修正を侍へ先行采配」と完全に一致。`ControlCircuitRadio`・`MainCircuitRadio`の2箇所へ`Foreground="{DynamicResource DialogForegroundBrush}"`をローカル値で個別指定。ビルド確認済み（0警告0エラー）。

### (b) 侍の原因特定の一次ソース裏取り

`dotnet/wpf` `Aero2.NormalColor.xaml`4513-4578行（隠密のscratchpad保存分を再利用して確認）：`<Style TargetType="{x:Type RadioButton}">`（x:Keyなし、テーマスタイルとして機能）の**Style本体**4517行に`<Setter Property="Foreground" Value="{DynamicResource {x:Static SystemColors.ControlTextBrushKey}}" />`が存在する。`ControlTemplate.Triggers`（4548-4574行）にはForegroundへ言及するトリガーが一切無い（Background/BorderBrush/Fill/Opacityのみ）。侍の主張「Style本体のForeground Setterが原因、ControlTemplate.Triggersではない」は一次ソースで完全に裏付けられる。

### (c) 既存パターン（PR-20/21）との異同整理

PR-20の3パターン（StaticResource固定解決／ItemsHostコンテナ生成前提／ControlTemplate.Triggers優先順位）のいずれにも該当しない。今回は「ecad2側に暗黙的スタイルが存在しない型は、WPFがプロパティ単位でAero2既定のテーマスタイル（同名の名前付きスタイル）へフォールバックし、そのStyle本体の通常Setterが継承より優先される」という第4の型。**新規パターン候補PR-24として`docs-notes/pattern-recurrence-log.md`へ記帳済み**（後述2節のStatusBar発見と合わせ、記帳時点で既に2例判明）。

侍所見「ControlTemplate優先順位問題（PR-20/21型）ではなく、単純な個別指定で解決する型」は妥当。対処法として、この型はControlTemplate差し替えのような大掛かりな対応が不要で、ローカル値の個別指定のみで確実に解決できる点も確認した。

## 2. App層全11XAMLファイルの機械的棚卸し結果

### 手法

`App.xaml`の暗黙的スタイル一覧（`ListBox`/`ListBoxItem`/`DataGrid`/`DataGridRow`/`DataGridColumnHeader`/`DataGridCell`/`Button`/`ToggleButton`/`ComboBox`/`ComboBoxItem`/`ScrollBar`、加えて`MainWindow.xaml`内`TabItem`はAvalonDockペイン専用ローカルスタイルでT-106対応済み）を既対応として除外。全対象ファイルのXMLタグ出現頻度を集計し、対応済み型以外（`RadioButton`/`CheckBox`/`TextBlock`/`TextBox`/`Slider`等）を全箇所確認した。`Window`要素自体の`Background`/`Foreground`指定有無も全7ダイアログ+MainWindowで確認した。

### 発見一覧（優先度順）

| 優先度 | ファイル | 型/箇所 | 状態 | 詳細 |
|---|---|---|---|---|
| **高** | `Views/PdfPreviewDialog.xaml` | `Window`要素自体 | **未対応** | 他の全ダイアログ（AddSheetDialog等6件）は`Window`要素に`Background`/`Foreground`をDynamicResource指定しているが、本ファイルのみ完全に欠落。内部の`TextBlock`（`PageLabel`/`ZoomLabel`）も継承元が無いため既定色（黒文字・白背景相当）のまま。**PDF出力は頻用機能であり、開くたびにダークモード中でも常にライトモード風の画面が表示される** |
| **高** | `MainWindow.xaml`（`StatusBarArea`） | `StatusBar`/`StatusBarItem`（7項目のTextBlock群を含む） | **未対応** | `StatusBar`用の暗黙的スタイルがecad2側に存在せず、Aero2既定テーマスタイル（`Aero2.NormalColor.xaml`5377-5458行）がそのまま適用。Style本体で`Background="#FFF1EDED"`（固定薄灰）・`Foreground={DynamicResource SystemColors.ControlTextBrushKey}`（固定）。RadioButton（1節）と同一メカニズム。**ステータスバー全体（モード/ツール/選択セル/操作対象端点/ズーム/行コメント案内/案内メッセージの7項目）が常時ライトモード風配色のまま**、常時表示される領域だけに影響大 |
| 中（対応済み） | `Views/AddSheetDialog.xaml` | `RadioButton`×2 | **対応済み**（1節参照） | — |
| 低 | `MainWindow.xaml`（プロパティパネル） | `Slider`（設定時間クイック設定） | 未対応（実害小） | Aero2既定`Slider`のStyle本体`Foreground`はトラック/つまみ色のみに影響、テキストラベルを持たないコントロールのため文字が読めない実害は無い。`Background`/`BorderBrush`は既定`Transparent`のため親背景を透過し違和感は小さいと推測 |
| 低 | `MainWindow.xaml`（自作パーツリスト） | `TextBlock`（カテゴリラベル、`Foreground="Gray"`） | ハードコード（実害小） | 中間色のため明暗どちらの背景でも一定の視認性は保たれると推測。実機での目視確認が望ましい |
| 低 | `Views/SheetSettingsDialog.xaml` | `TextBlock`（行数エラーメッセージ、`Foreground="Red"`） | ハードコード（実害小） | 警告色として意図的な固定と考えられ、視認性は保たれると推測 |

### 対応済みと確認できた主要箇所（参考、問題なし）

- 全24件の`TextBox`（プロパティパネル・各ダイアログ・検索バー・配置バー・行コメント/枠ラベルエディタ）：いずれも`Background="{DynamicResource InputBackgroundBrush}"`/`Foreground="{DynamicResource InputForegroundBrush}"`を個別指定済み。
- `CheckBox`（画像プロパティのトレース用下絵チェック、1箇所）：`Foreground="{DynamicResource PanelContentForegroundBrush}"`個別指定済み。
- `TextBlock`66件中、`ToolBarKeyLabelStyle`/`PlacementToolBarKeyLabelStyle`（T-083で対応済み）を参照する23件、`PanelContentForegroundBrush`等を個別指定する多数、`TextElement.Foreground`継承（`FindBar`/`ElementPlacementBar`/`RungCommentEditor`/`FrameLabelEditor`の各`Border`が`TextElement.Foreground`を設定しその子孫へ継承される設計）に依存する箇所は、いずれも継承元の存在を確認済みで問題なし。
- `Views/AboutDialog.xaml`・`Views/UsageWindow.xaml`：`Window`要素にBackground/Foreground指定あり、対応済み。
- `Themes/Theme.Dark.xaml`・`Theme.Light.xaml`：色定義（`SolidColorBrush`）のみでコントロール定義自体が無いため棚卸し対象外。

## 3. パターン再発検知

新規パターン候補PR-24として`docs-notes/pattern-recurrence-log.md`へ記帳済み（RadioButton・StatusBarの2例、記帳時点で確定基準相当）。「ダークモードで特定コントロールだけ色が変わらない」という今後の報告に対し、暗黙的スタイルの有無とAero2既定Style本体のSetterをまず確認する、という調査観点を提起した。

## 不明点

- Slider・カテゴリラベル(Gray)・エラーテキスト(Red)の3件は「実害小」と推測したが、実機（ダークモード）での目視・画素採取による裏取りは行っていない（静的解析の範囲外）。

## 派生提案

特になし（本書内の優先度低3件は指摘に留め、対応要否は家老判断に委ねる）。

## 出典

- `src/Ecad2.App/App.xaml`（暗黙的スタイル一覧）
- `src/Ecad2.App/MainWindow.xaml`（StatusBarArea 1690-1725行、Slider 1551行、TextBlock各所、Window要素冒頭）
- `src/Ecad2.App/Views/*.xaml`全7件
- `src/Ecad2.App/Themes/Theme.Dark.xaml`・`Theme.Light.xaml`
- WPF本体一次ソース`dotnet/wpf`（scratchpad保存済み`Aero2.NormalColor.xaml`、RadioButton4513-4578行・StatusBar/StatusBarItem5377-5458行・Slider5357-5368行）
- `git show 9c14b8b`（AddSheetDialog修正差分）
- `docs/todo.md` T-108節
