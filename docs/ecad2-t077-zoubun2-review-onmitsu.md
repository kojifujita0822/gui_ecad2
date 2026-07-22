# T-077増分2 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `66a730e`（使い方ウィンドウのナビゲーションUI：左目次+右コンテンツ、全11領域対応）
手法: `git show 66a730e -- <path>` で範囲を明示した手動レビュー。effort=low（1周目既定）。

## 結論

**指摘なし。DoD（殿裁定「案1」+全11領域対応）を満たすと判断する。** ビルド・テスト裏取り済み（0警告0エラー、新規テスト15件全合格）。忍者実機確認へ進めてよい。

## 確認観点と根拠

### (a) DoD整合確認

`docs/todo.md` T-077節には増分2の番号付きDoDリストは無く「次は増分2（ナビゲーションUI・全11領域対応）」（681行）という記述のみだが、これは殿裁定「案1（左目次+右コンテンツ）」（654-658行）と隠密プラン（`docs/ecad2-t077-plan-onmitsu.md`）の増分計画叩き台と一致する。実装は`UsageWindow.xaml`を左`ListBox`（11領域目次）+`GridSplitter`+右`FlowDocumentScrollViewer`のレイアウトへ拡張し、`docs/spec/ecad2-spec-*.md`全11領域を`EmbeddedResource`として同梱（`csproj`側`grep -c`で11件確認）。目次選択で増分1確立済みの`MarkdownFlowDocumentConverter`を再利用してコンテンツ切替。DoD整合確認OK。

### (b) T-100修正内容との整合裏取り（左目次ListBoxへの暗黙的スタイル適用）

`App.xaml`36行目、ListBox暗黙的スタイル（`<Style TargetType="{x:Type ListBox}">`、`x:Key`なし）は`Application.Resources`内に定義されている。WPFのリソース解決規則上、`Application.Resources`内の暗黙的スタイルはウィンドウ境界を越えてアプリ全体の該当型コントロールへ自動適用される。`UsageWindow.xaml`内の`TopicList`（`ListBox`）にはローカル`Style`指定が無いため、T-100で修正済みの暗黙的スタイル（`ControlTemplate`明示・`IsEnabled=false`トリガーの`DynamicResource`化含む）がそのまま適用される。侍が新規スタイルを起こさず既存の暗黙的スタイルに委ねた設計判断は正しい。指摘なし。

### (c) GridSplitterのダークモード対応

`UsageWindow.xaml`：`<GridSplitter ... Background="{DynamicResource PanelGridLineBrush}"/>`。`PanelGridLineBrush`は前回（T-100・T-077増分1レビュー）で両テーマファイル（`Theme.Dark.xaml`/`Theme.Light.xaml`）に既存定義済みと確認済みのキーであり、新規追加なし。`GridSplitter`は`Control`派生で`Background`依存関係プロパティを持つため、`DynamicResource`バインドで正しく反映される。指摘なし。

### (d) 回帰テスト15件の網羅性確認

`UsageWindowTests.cs`：
- `Topics_11領域全て定義されている`（1件）
- `Topics_DisplayNameが重複しない`（1件）
- `Topics_ResourceFileNameが重複しない`（1件）
- `LoadEmbeddedMarkdown_全11領域の埋め込みリソースが実際に読み込める`（`[Theory]`+`[InlineData]`11件、ファイル名をハードコード列挙）
- `Topics_全項目のResourceFileNameが実際に読み込める`（1件、`Topics`配列からループで動的検証）

合計15件（1+1+1+11+1）、家老依頼の件数と一致。Theory版（ハードコード列挙）とループ版（`Topics`配列から動的導出）の二重チェックは、コメントに明記されたとおり「`InlineData`の列挙漏れがあってもループ版で拾える」という設計意図があり、`csproj`側`LogicalName`のtypo等の対応漏れを機械的に検出できる構成として妥当。`dotnet test`で15件全合格を実測確認済み。

### (e) 初期選択領域・並び順の技術的妥当性

`Topics`配列11件のResourceFileNameと、`docs/spec/ecad2-spec-*.md`実ファイル11件を突き合わせ、過不足なく1対1対応することを確認した（`csproj`側`EmbeddedResource`11件・`Topics`配列11件とも一致）。並び順（基本操作→編集→表示→部品/機器管理→検査・出力というコメントの主張どおりの流れ）に技術的な矛盾・欠落・重複は無い。並び順自体はUI/UX上の軽微な主観的判断であり、家老指示のとおり技術面（対応漏れ・重複）のみ確認し、見た目の妥当性判断は実機確認時の一目確認に委ねるのが適切と考える。

### (f) code-reviewスキル併用

marketplace版導入後も起動不可（本セッション冒頭で再確認済み）。`onmitsu.md`既定どおり手動レビューで代替。

### ビルド・テスト裏取り

`dotnet build src/Ecad2.App/Ecad2.App.csproj --no-incremental`・`dotnet build tests/Ecad2.App.Tests/Ecad2.App.Tests.csproj --no-incremental` とも0警告0エラー。`dotnet test --no-build --filter "FullyQualifiedName~UsageWindowTests"` → **15件全合格**。

## 不明点

なし。

## 派生提案（範囲外の気づき）

特になし。

## 出典

- `src/Ecad2.App/Ecad2.App.csproj`（`EmbeddedResource`11件）
- `src/Ecad2.App/Views/UsageWindow.xaml`・`UsageWindow.xaml.cs`（`Topics`配列・`TopicList_SelectionChanged`）
- `src/Ecad2.App/App.xaml`（ListBox暗黙的スタイル36行目）
- `tests/Ecad2.App.Tests/UsageWindowTests.cs`
- `docs/todo.md` T-077節（654-681行、殿裁定・増分1/2経緯）
- `docs/ecad2-t077-plan-onmitsu.md`（隠密プラン、増分計画叩き台）
