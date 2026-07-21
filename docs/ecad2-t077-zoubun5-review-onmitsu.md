# T-077增分5 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `725dbd8`（MarkdownFlowDocumentConverterに表構文対応を追加）
手法: `git show 725dbd8 -- <path>` で範囲を明示した手動レビュー＋実測検証。effort=medium（無限ループバグの重点確認のため通常の1周目より深め）。

## 結論

**指摘なし。DoD（殿裁定：表構文のFlowDocument変換対応追加）を満たすと判断する。** 重点確認事項（無限ループバグ修正）は論理的に妥当かつ実測でも裏付けを取った。ビルド・テスト裏取り済み（0警告0エラー、Converter系13件・UsageWindow系22件いずれも全合格）。忍者実機確認へ進めてよい。

## 確認観点と根拠

### (a) DoD整合確認

`docs/todo.md`775-779行「殿裁定＝増分5として表構文のFlowDocument変換対応を追加する」（対象6領域：menu-toolbar・pdf-testmode・placement・sheet-document・statusbar・wiring）との整合を確認。ヘッダー行+区切り線+データ行のMarkdown表をWPF `Table`へ変換する機能が実装され、対象6領域の実データ変換テストも追加されている。DoD整合確認OK。

### (b) 【重点】無限ループバグの修正内容・論理的妥当性

**根本原因の分析**：段落結合ブロックへ到達する行は、外側のif-else連鎖（見出し/水平線/コードブロック/表/箇条書き/番号付きリストの順に判定）のいずれにもマッチしなかった行に限られる。表構文の判定条件のみが「`|`で始まる **かつ** 次の行が区切り線である」という複合条件であるため、「`|`で始まるが次の行が区切り線でない」行は表判定をすり抜けて段落結合ブロックへ到達しうる。この行が持つ「`|`始まり」という性質は、段落結合ループ内の新規除外条件`!lines[i].TrimStart().StartsWith("|")`に該当するため、ループ本体が1度も実行されず`paragraphLines`が空のまま`i`が進まない——外側の大きな`while (i < lines.Length)`ループも同じ行を無限に再処理することになる。この分析は`MarkdownFlowDocumentConverter.cs`のコード構造と整合しており、論理的に妥当と判断する。

**修正内容**：`paragraphLines`の初期化を`{ lines[i] }`（1行目を無条件で取り込み）とし、直後に`i++`してから、2行目以降のみ除外条件を適用する構造に変更。これにより外側ループは行数が0であっても必ず最低1行分前進するため、無限ループは構造的に発生しなくなる。他の除外条件（見出し・水平線・コードブロック・箇条書き・番号付きリスト）はいずれも外側のif-else連鎖で単一条件判定済みのため、段落結合ブロック到達時点で該当しないことが保証されており、同型の無限ループリスクは表構文以外には存在しないと判断した。

**RED証明・回帰テストの実測確認**：バグシナリオを直接再現するテスト`Convert_区切り線が続かないパイプ始まり行は通常段落として処理し無限ループしない`（入力`"| これは表ではない行 |\n通常の続き。"`）を確認。`dotnet test`をタイムアウト付き（30秒）で実測実行し、**Converter系13件が525msで全合格**（ハングなし）することを確認した。侍報告「10秒タイムアウトで強制終了を実測」の主張と整合する結果が得られた。

### (c) Markdown表構文の変換ロジック妥当性

- `IsTableSeparatorRow`（正規表現`^:?-+:?$`）：単純な`-`区切り線、および将来拡張余地のある位置指定記法（`:---:`等）の形にもマッチする形だが、実際の変換では位置指定は無視される（コメントで明記、docs/usage全11領域の実測範囲で不要と確認済みとの申告）。
- `SplitTableRow`：先頭・末尾の`|`除去後に`|`分割という標準的なMarkdown表パース方式。妥当。
- `CreateTable`：データ行の列数がヘッダーより**少ない**場合は空セルで埋める仕様を確認・テストで裏付け済み。列数が**多い**場合は超過列が無言で切り捨てられる実装だが、`docs/usage`6領域の表行の列数を`awk`で実測したところ、いずれの領域も全行が単一の列数に統一されており（列数不一致は存在しない）、現状のデータでは問題化しないことを確認した。

### (d) 全11領域での実データ変換確認の裏取り

`UsageWindowTests.cs`：殿指摘6領域（menu-toolbar/pdf-testmode/placement/sheet-document/statusbar/wiring）を対象に`LoadEmbeddedMarkdown_表を含む6領域は実際にTableへ変換される`（`[Theory]`6件、実際に`Table`型のBlockが含まれることを確認）、および全11領域を対象に`Topics_全項目のコンテンツがMarkdownFlowDocumentConverterで例外なく変換できる`（1件、ループで全件変換し例外なしを確認）。`dotnet test`実測で**UsageWindowTests系22件全合格**（既存15件+新規7件、コミットメッセージの件数と一致）。侍申告どおり裏取りできた。

### (e) ダークモード対応の確認

`CreateTableCell`内で使用する`PanelHeaderBackgroundBrush`・`PanelHeaderForegroundBrush`・`PanelGridLineBrush`の3キーについて、両テーマファイル（`Theme.Dark.xaml`・`Theme.Light.xaml`）に既存定義があることを確認。さらに`PanelHeaderBackgroundBrush`/`PanelHeaderForegroundBrush`は`MainWindow.xaml`251-252行・`App.xaml`187-188行で既にAvalonDockペインヘッダー等に使われている確立されたキーであり、今回の表ヘッダーセルへの適用は既存のヘッダー系配色を再利用した設計として妥当。新規キー追加なし。

### (f) code-reviewスキル併用

marketplace版導入後も起動不可（本セッション冒頭で再確認済み）。`onmitsu.md`既定どおり手動レビューで代替。

### ビルド・テスト裏取り

`dotnet build tests/Ecad2.App.Tests/Ecad2.App.Tests.csproj --no-incremental` → 0警告0エラー。`dotnet test --no-build --filter "FullyQualifiedName~MarkdownFlowDocumentConverterTests"`（30秒タイムアウト付き） → **13件全合格（525ms）**。同`--filter "FullyQualifiedName~UsageWindowTests"` → **22件全合格（165ms）**。いずれもハング・タイムアウトなし。

## 不明点

なし。

## 派生提案（範囲外の気づき）

特になし（列数超過時の無言切り捨ては(c)節で言及済み、現状データでは問題化しないため指摘に留めず提起もしない）。

## 出典

- `src/Ecad2.App/Views/MarkdownFlowDocumentConverter.cs`（表構文対応・無限ループ修正）
- `tests/Ecad2.App.Tests/MarkdownFlowDocumentConverterTests.cs`（新規6件+既存修正1件）
- `tests/Ecad2.App.Tests/UsageWindowTests.cs`（新規7件）
- `src/Ecad2.App/Themes/Theme.Dark.xaml`・`Theme.Light.xaml`（DynamicResourceキー定義）
- `src/Ecad2.App/MainWindow.xaml`・`App.xaml`（既存PanelHeaderキー使用箇所）
- `docs/usage/ecad2-usage-*.md`（6領域の表行列数実測）
- `docs/todo.md`775-779行（殿裁定・増分5采配）
