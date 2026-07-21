# T-106 配置ツールバーのタブ/インラインバー/ScrollBarダークモード対応 — 静的レビュー（隠密）

**対象コミット**: `48008f5`（第1弾、タブ文字色・非選択背景）+ `0c3a7d4`（第2弾、ComboBox/未定アイコン/ScrollBar）
**レビュー日**: 2026-07-21
**effort**: low相当（1周目、ただしスコープ2回拡大・303insertions/3deletionsのため丁寧に確認）
**手法**: `code-review`スキルは恒久的にSkillツールから起動不可（`onmitsu.md`既定事象）につき、`git show`範囲明示の手動レビュー。加えて家老指定の重点観点（AvalonDock/VS2013テーマとの競合、既存コントロールへの影響）確認のため、GitHub一次ソース（Dirkster99/AvalonDock）をcurl直接取得し照合した。

## 結論

**両コミットとも問題なし。** 家老指定の重点観点2点はいずれも一次ソース確認で解消。忍者実機確認で1点、確認を追加してほしい事項がある。

## 重点観点(1): AvalonDock/VS2013テーマとの競合・二重定義

`AvalonDock`本体（`source/Components/AvalonDock/Themes/generic.xaml`、1716行、curl取得）と`AvalonDock.Themes.VS2013`（`source/Components/AvalonDock.Themes.VS2013/Themes/Generic.xaml`、2933行、curl取得）の両方をGrepで確認した結果、**`ScrollBar`型・`ComboBox`型の明示的スタイル定義は一切存在しない**（VS2013側の"ScrollBar"ヒットはコンテキストメニュー内`ScrollViewer`の属性値1件のみで、型スタイルではない）。よって今回App.xamlへ新設した暗黙的スタイル（`TargetType={x:Type ScrollBar}`/`{x:Type ComboBox}`/`{x:Type ComboBoxItem}`/`{x:Type Thumb}`/`{x:Type RepeatButton}`）は、AvalonDock/VS2013テーマ側のいかなる定義とも競合しない。

## 重点観点(2): 既存コントロールへの予期せぬ影響

- **ComboBox**：ecad2コードベース全体でComboBoxを使用しているのは`PlacementPartComboBox`（MainWindow.xaml 1768行）1箇所のみ（grep確認）。他画面（ダイアログ等）への影響なし。
- **ComboBoxItem（ItemContainerStyle）**：既存の明示的Style（`ToolTip`/`AutomationProperties.Name`の2 Setterのみ）へ`BasedOn="{StaticResource {x:Type ComboBoxItem}}"`を追加しただけ。既存Setterへの変更なし、修正は正確。
- **ScrollBar**：暗黙的スタイルのためアプリ全体（ListBox/DataGrid内部のスクロールバー含む）に波及するが、これはT-106(7)のスコープ拡大の目的（「スクロールバー全般」）そのもの。App.xaml内で`{x:Type ScrollBar}`等の暗黙的キーが重複定義されていないこともgrepで確認済み（各型1回のみ）。
- **未定アイコンボタン2個**：`Style="{x:Null}"`のまま維持（T-089時の「押下フィードバック対象外」という意図的設計を継続）、`Background`属性のみ追加。既存の押下フィードバック無し扱いに変更なし。

## 新規DynamicResourceキーの網羅性（隠密独自裏取り）

侍の機械チェック（新規キー24件、Light/Dark両テーマに漏れなく存在）に加え、隠密でも新規3件（`ScrollBarThumbBrush`/`ScrollBarThumbHoverBrush`/`ScrollBarButtonHoverBrush`）と、新設スタイルが参照する既存キー（`InputBackgroundBrush`/`InputForegroundBrush`/`DialogBorderBrush`）がLight/Dark両テーマに存在することをgrepで個別確認した。T-089型UnsetValueクラッシュのリスクなし。

## 実機確認で追加してほしい点

侍の起動確認は「シート追加→a接点配置→ComboBoxドロップダウン展開」を**ライトモードのみ**実施。以下2点は未確認のため、忍者実機確認で追加してほしい：
1. **ダークモードでのComboBoxドロップダウン展開**（見た目・クラッシュ有無とも）
2. **ScrollBarの実際の出現ケース**（シート数を増やしてSheetNavList等をオーバーフローさせ、ダーク/ライト両モードでThumb・矢印ボタンの視認性を確認）——今回のスコープ拡大の主目的である「スクロールバー全般」の実地確認がまだ行われていない。

## 派生提案

なし。
