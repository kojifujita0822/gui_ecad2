# AvalonDock v4.74.1 一次ソース参照用ローカルキャッシュ

## 目的
ecad2 のドッキングUIは AvalonDock（NuGetパッケージ `Dirkster.AvalonDock` /
`Dirkster.AvalonDock.Themes.VS2013`）を使用している。NuGetパッケージにはコンパイル済み
DLL（BAML埋め込み）しか含まれず、平文の XAML/C# ソースは含まれていない。そのため
AvalonDock内部実装の調査（docs/配下の調査書31件超）のたびに GitHub
（`Dirkster99/AvalonDock`）へ都度アクセスして取得する運用になっていた。

本ディレクトリは、調査で頻出参照するファイルをローカルにキャッシュし、都度の
GitHub取得の重複を避けるためのもの。殿指示（2026-07-22）により作成。

## 対象バージョン
`v4.74.1`

`src/Ecad2.App/Ecad2.App.csproj` でピン留め中の NuGet パッケージバージョンと一致
（`Dirkster.AvalonDock` / `Dirkster.AvalonDock.Themes.VS2013` とも `Version="4.74.1"`）。

## 保存ファイル一覧

リポジトリ `Dirkster99/AvalonDock` のディレクトリ構造をそのまま保持している。

| 相対パス | 内容 |
|---|---|
| `source/Components/AvalonDock.Themes.VS2013/Themes/Generic.xaml` | VS2013テーマの既定スタイル集。AnchorablePaneControlStyle・LayoutAnchorableControl既定スタイル・LayoutAutoHideWindowControlスタイル等を含む、過去調査で最頻出のファイル |
| `source/Components/AvalonDock.Themes.VS2013/LightBrushs.xaml` | VS2013ライトテーマのブラシリソース定義 |
| `source/Components/AvalonDock.Themes.VS2013/DarkBrushs.xaml` | VS2013ダークテーマのブラシリソース定義 |
| `source/Components/AvalonDock/Layout/LayoutAnchorable.cs` | AvalonDock本体。LayoutAnchorableモデルクラス（CanDock等のプロパティを含む） |
| `source/Components/AvalonDock/Controls/LayoutAutoHideWindowControl.cs` | AvalonDock本体。オートハイド（ピン留め）ウィンドウの制御ロジック |
| `source/Components/AvalonDock/Controls/LayoutAnchorableItem.cs` | AvalonDock本体。LayoutAnchorableItem（LayoutAnchorableに対応するUI項目） |
| `source/Components/AvalonDock/DockingManager.cs` | AvalonDock本体。DockingManager本体（ドッキング全体の管理クラス） |
| `source/Components/AvalonDock/Themes/generic.xaml` | AvalonDock本体自身の既定テーマ（VS2013テーマ側のGeneric.xamlとは別ファイル。ファイル名の大文字小文字もリポジトリ実物どおり小文字`generic.xaml`） |
| `source/Components/AvalonDock/Controls/LayoutAnchorableTabItem.cs` | AvalonDock本体。アンカラブルのタブ（TabItem）のマウス処理（`_draggingItem` staticフィールド・タブ並び替え・アクティブ化）。T-110所見C調査で追加（2026-07-22） |
| `source/Components/AvalonDock/Controls/AnchorablePaneTabPanel.cs` | AvalonDock本体。タブストリップのパネル。タブのドラッグアウト→フロート化開始（`OnMouseLeave`）はここ。同上 |
| `source/Components/AvalonDock/Controls/LayoutAnchorablePaneControl.cs` | AvalonDock本体。アンカラブルペインのTabControl派生コントロール（ドラッグ関連処理を持たないことの確認用）。同上 |
| `source/Components/AvalonDock.Themes.Aero/Theme.xaml` | AeroThemeのタブ用ControlTemplate（128-211行=LayoutDocumentTabItem用、278-334行=LayoutAnchorableTabItem用）。T-119（配置ツールバータブのAeroテーマ風形状変更）で追加（2026-07-22）。**注記**：ecad2はAeroThemeパッケージ自体は使用していない（VS2013テーマのみ）、デザイン参考として`master`ブランチから取得（v4.74.1タグとの厳密な一致は未確認） |
| `source/Components/AvalonDock.Themes.Aero/Brushes.xaml` | AeroThemeのBaseColor1-33実RGB値。同上 |
| `source/Components/AvalonDock.Themes.Aero/AeroColors.cs` | AeroThemeのBaseColorキー定義（`ComponentResourceKey`）。同上 |
| `source/Components/AvalonDock.Themes.Aero/Controls/SplineBorder.cs` | タブ左20px幅の曲線を描く専用コントロール（`OnRender`で2本の`QuadraticBezierSegment`）。T-119の核心部分、124行の小さなクラス。同上 |

各ファイルの先頭には、取得元URL・取得日・対象バージョンを記載したヘッダーコメントを
追記済み（`.xaml`は`<!-- -->`、`.cs`は`//`）。ヘッダー以外は無改変で保存している。

## 陳腐化ポリシー
- 本ディレクトリの内容は取得時点（2026-07-22、v4.74.1）のスナップショットであり、
  自動更新されない。
- `src/Ecad2.App/Ecad2.App.csproj` の `Dirkster.AvalonDock` / `Dirkster.AvalonDock.Themes.VS2013`
  のバージョンを上げた場合は、本ディレクトリの再取得要否をその都度判断すること
  （調査対象箇所が変更されていなければ据え置きでもよい）。
- 各ファイルの内容はGitHub取得のまま無改変で保存している（ヘッダー追記のみ）。

## 使い方（Grep出力破損対策・行範囲索引）

**Grepのcontentモード（`-A`/`-B`/-C含む）は使わないこと【MUST、`feedback_avoid_grep_content_mode`】**。
本ディレクトリのファイルは大きい（最大2900行超）ため、内容を見たくなった際にcontentモードへ
逃げたくなるが、出力破損の既知原因と確定している。代わりに:
1. `Grep`は`output_mode: "files_with_matches"`または`"count"`のみで使い、該当有無・行番号の
   当たりを付ける（`-n`のみは可）。
2. 該当箇所が分かったら`Read`に`offset`/`limit`を指定して直接読む。下表の索引があれば
   Grep自体を省略してよい。

過去の調査書で引用された主な行範囲（新規調査で追加したら本表に追記されたい）:

| ファイル | 行範囲 | 内容 | 出典 |
|---|---|---|---|
| `AvalonDock.Themes.VS2013/Themes/Generic.xaml` | 404-560 | `AnchorablePaneControlStyle`（`ContentTemplate`は553-） | t110設計書 |
| 同上 | 716-720 | 既定`LayoutAnchorableControl`スタイルのIsActiveトリガー（Foreground/Background切替） | t110所見AB調査書 |
| 同上 | 1280-1285 | `AvalonDockThemeVs2013AnchorableTitleTemplate`（既定タイトルテンプレート、色指定なし） | t110所見AB調査書 |
| 同上 | 1712-1800 | `LayoutAnchorableControl`既定スタイル本体（Header:1726-1728、トリガー5本:1749-1795） | t110設計書 |
| 同上 | 2465-2475 | `LayoutAutoHideWindowControl`スタイル（`AnchorableStyle` Setter） | t110設計書 |
| `AvalonDock.Themes.VS2013/LightBrushs.xaml` | 231-234 | `ToolWindowCaptionActiveText`(#FFFFFF)/`ToolWindowCaptionActiveBackground`(#007ACC) | t110所見AB調査書 |
| `AvalonDock.Themes.VS2013/DarkBrushs.xaml` | 234-237 | 同上キー、Lightと同値であることの確認元 | t110所見AB調査書 |
| `AvalonDock/Layout/LayoutAnchorable.cs` | 123-130 | `CanAutoHide`プロパティ | t110設計書 |
| 同上 | 161 | `IsAutoHidden`計算プロパティ | t110設計書 |
| 同上 | 249-258 | `WriteXml`（シリアライズ対象確認） | t110設計書 |
| 同上 | 429 | `ToggleAutoHide()`（public） | t110設計書 |
| `AvalonDock/Controls/LayoutAutoHideWindowControl.cs` | 63-75 | `AnchorableStyle`依存関係プロパティ（既定null） | t110設計書 |
| 同上 | 278 | `_internalHost`生成（明示Style付与、暗黙スタイル非適用の根拠） | t110設計書 |
| `AvalonDock/Controls/LayoutAnchorableItem.cs` | 79-112 | `AutoHideCommand`（CanExecute:105-110・Execute:112） | t110設計書 |
| `AvalonDock/DockingManager.cs` | 1523 | `GetLayoutItemFromModel`（public） | t110設計書 |
| 同上 | 1943 | `ExecuteAutoHideCommand`（internal、中身は`ToggleAutoHide()`1行のみ） | t110設計書 |
| `AvalonDock/Themes/generic.xaml`（本体既定テーマ、上記VS2013版とは別ファイル） | 863-927 | `LayoutAnchorableControl`のThemeStyle（Header:877-879、トリガー:900-923） | t110設計書 |
| `AvalonDock/Controls/LayoutAnchorableTabItem.cs` | 89-102 | `OnMouseLeftButtonDown`（`CanMove`ガード・`_draggingItem`セット） | t110所見C調査書 |
| 同上 | 120-125 | `OnMouseLeftButtonUp`（`Model.IsActive=true`＝タブ上MouseUpでアクティブ化。`_draggingItem`はリセットしない） | t110所見C調査書 |
| 同上 | 128-140 | `OnMouseLeave`（押下状態ならドラッグ候補化） | t110所見C調査書 |
| 同上 | 143-158 | `OnMouseEnter`（押下状態で別タブ進入→`MoveChild`タブ並び替え） | t110所見C調査書 |
| `AvalonDock/Controls/AnchorablePaneTabPanel.cs` | 84-97 | `OnMouseLeave`→`StartDraggingFloatingWindowForContent`（タブからのフロート化開始の正規経路。キャプチャ無し・`e.LeftButton`状態依存） | t110所見C調査書 |
| `AvalonDock/DockingManager.cs` | 1701-1712 | `StartDraggingFloatingWindowForContent`（冒頭に`CanFloat`ガード） | t110所見C調査書 |

「t110設計書」= `docs/ecad2-t110-increment3-titlebar-hide-and-autohide-ui-design-onmitsu.md`、
「t110所見AB調査書」= `docs/ecad2-t110-increment2-findings-ab-investigation-onmitsu.md`、
「t110所見C調査書」= `docs/ecad2-t110-increment2-finding-c-investigation-onmitsu.md`。

**行番号の基準に注意**: 本表の行番号は調査書が引用したGitHubオリジナルの行番号。ローカル保存版は
先頭のヘッダーコメント5行分だけ後ろへズレる（例: オリジナル1701行→ローカル1706行）。Readする際は
offsetを5行手前から広めに取るとよい。

## 追加ファイルの取得手順
今後、別のファイルが調査で必要になった場合は以下の手順で追加する。

1. リポジトリ内パスを特定する:
   ```
   gh api repos/Dirkster99/AvalonDock/git/trees/v4.74.1?recursive=true
   ```
   を取得し、対象ファイル名でパスを検索する（モノレポにつき同名ファイルが
   複数存在しうる。AvalonDock本体 / AvalonDock.Themes.VS2013 / AvalonDock.Themes.Arc等の
   取り違えに注意）。
2. 特定したパスから raw取得する:
   ```
   curl -s -o <出力先> https://raw.githubusercontent.com/Dirkster99/AvalonDock/v4.74.1/<リポジトリ内パス>
   ```
3. ファイル先頭に本READMEと同形式のヘッダー（取得元URL・取得日・対象バージョン・
   陳腐化注意）を追記してから、`docs-notes/vendor-reference/avalondock-v4.74.1/<リポジトリ内パスと同じ相対パス>`
   へ保存する（ディレクトリ構造を保つ）。
4. 保存後、該当ファイルに設計書で引用されている構造（クラス名・スタイルキー等）が
   実際に含まれているかサニティチェックする。
