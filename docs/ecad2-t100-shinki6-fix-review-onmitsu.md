# T-100新規発見6 実装レビュー（隠密）

日付: 2026-07-21
対象コミット: `9f26852`（ListBoxの配置バー表示中パネル白化を修正、方式1=ControlTemplate差し替え）
手法: `git show 9f26852 -- <path>` で範囲を明示した手動レビュー、一次ソース（`dotnet/wpf` Aero2.NormalColor.xaml、隠密の根本原因調査時に取得済み）との突合。effort=low（1周目既定）。

## 結論

**指摘なし。DoD（殿裁定「方式1」）を満たすと判断する。** ビルド確認済み（0警告0エラー）。忍者実機確認へ進めてよい。念のため1点、実機確認時の目視ポイントを申し添える（下記「不明点」参照、指摘ではない）。

## 確認観点と根拠

### (a) DoD整合確認

`docs/todo.md` T-100節578-580行「殿裁定＝方式1（ControlTemplate差し替え、T-106確立パターン踏襲）。App.xamlのListBox暗黙的スタイルへTemplateを明示指定し、Trigger内の白固定色をDynamicResource化する」との整合を確認。`App.xaml`のListBox暗黙的スタイルに`Template`を明示指定（一次ソースのBorder+ScrollViewer+ItemsPresenter構造を踏襲）し、`ControlTemplate.Triggers`内`IsEnabled=false`時の`Background`/`BorderBrush`を`{StaticResource ...}`（白固定）から`{DynamicResource PanelContentBackgroundBrush}`/`{DynamicResource PanelGridLineBrush}`へ差し替え済み。DoDどおり。

### (b) PR-21トラップ狙い撃ち（Style本体既定値Setter移植漏れ）

一次ソース`Aero2.NormalColor.xaml`2524-2565行（隠密の根本原因調査時にscratchpadへcurl取得済み）と、侍実装のStyle本体・ControlTemplateを1行ずつ突合した。

**Style本体既定値Setter**：一次ソース側の`Background`/`BorderBrush`/`BorderThickness`/`Foreground`/`ScrollViewer.HorizontalScrollBarVisibility`/`ScrollViewer.VerticalScrollBarVisibility`/`ScrollViewer.CanContentScroll`/`ScrollViewer.PanningMode`/`Stylus.IsFlicksEnabled`/`VerticalContentAlignment`の10項目全て、侍実装側にも対応するSetterが存在することを確認（`Background`/`Foreground`はecad2既存のDynamicResource、`BorderBrush`は一次ソースの固定色からPanelGridLineBrushへ意図的差し替え、他は値も一致）。欠落なし。

**ControlTemplate本体**：`Border`(x:Name="Bd")+`ScrollViewer`+`ItemsPresenter`の構造、`ControlTemplate.Triggers`内の`IsGrouping`判定`MultiTrigger`（`ScrollViewer.CanContentScroll=false`）も含め完全一致。意図的な差分は`IsEnabled=false`Triggerの`Background`/`BorderBrush`のみ（StaticResource白固定→DynamicResource）。

PR-21のトラップ（ControlTemplateの骨格のみ移植しStyle本体の既定値Setterを移植し忘れる）には該当しない。

### (c) 新規DynamicResourceキー追加なしの裏取り

`PanelContentBackgroundBrush`・`PanelContentForegroundBrush`・`PanelGridLineBrush`の3キーとも`Theme.Dark.xaml`・`Theme.Light.xaml`両方に既存定義があることを確認（`grep`で両ファイルにヒット）。新規キー追加なしとの侍申告どおり。

### (d) ListBox使用箇所の棚卸し

`src/Ecad2.App`配下で`<ListBox`をgrepした結果、XAMLファイルは`MainWindow.xaml`のみ（4件ヒットのうち実際の要素タグは1353行`SheetNavList`・1574行`PartSelectionList`の2箇所、残り2件は`<ListBox.ItemTemplate>`/`<ListBox.ItemContainerStyle>`という子プロパティ要素構文で新規インスタンスではない）。Views配下の6ダイアログ（PdfPreviewDialog/AboutDialog/RenameDialog/AddSheetDialog/SheetSettingsDialog/DocumentInfoDialog）にはListBox使用箇所なし。侍申告「MainWindow.xaml内2箇所のみ、他XAMLファイルになし」は正確。

### (e) code-reviewスキル併用

marketplace版導入後も起動不可（本セッション冒頭で再確認済み）。`onmitsu.md`既定どおり手動レビューで代替。

### ビルド確認

`dotnet build src/Ecad2.App/Ecad2.App.csproj -c Debug` を実行、0警告0エラーで成功を確認（忍者へ使用状況を確認し競合なしと判断のうえ実施）。

## 不明点（指摘ではなく実機確認時の目視ポイント）

Style本体へ新規追加された`BorderThickness="1"`・`BorderBrush="{DynamicResource PanelGridLineBrush}"`について、修正前（Template未指定状態）でもこれらのプロパティがAero2テーマスタイル（`Control`クラスのテーマスタイルへの2段階フォールバック機構、WPF一般仕様）から暗黙的に継承されていた可能性が高く、その場合は今回の追加は「既存の暗黙的な値の明示化」に過ぎず新たな視覚変化は生じないと考えられる。ただし、この2段階フォールバック機構が実際にBorderThickness等の非Template系プロパティにも及ぶかは完全な確証を持てなかった（WPF内部実装の推測を含む）。忍者の実機確認時、通常表示（配置バー非表示中）のSheetNavList/PartSelectionListの枠線の見た目が修正前後で変化していないか、念のため一目確認いただくことを提案する（もし新たに薄い枠線が付いていても、意図的なテーマ色を使っているため実害は無いと考えられ、必須の確認事項ではない）。

## 出典

- `src/Ecad2.App/App.xaml`（ListBox暗黙的スタイル、コミット9f26852差分）
- `src/Ecad2.App/Themes/Theme.Dark.xaml`・`Theme.Light.xaml`（DynamicResourceキー定義）
- `src/Ecad2.App/MainWindow.xaml`（ListBox使用箇所棚卸し）
- WPF本体一次ソース `dotnet/wpf` `Aero2.NormalColor.xaml`（隠密根本原因調査時にscratchpadへcurl取得、2524-2565行）
- `docs/todo.md` T-100節（DoD、殿裁定578-580行）
- `docs-notes/pattern-recurrence-log.md`（PR-21節）
