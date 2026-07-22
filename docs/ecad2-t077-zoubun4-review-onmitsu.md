# T-077増分4 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `f961c06`（使い方ウィンドウの参照先をdocs/usage平易版へ切替）
手法: `git show f961c06 -- <path>` で範囲を明示した手動レビュー。effort=low（1周目既定）。

## 結論

**軽微な指摘2点、いずれも実害なし・経過観察でよいと判断する。** DoDは満たしている。ビルド・テスト裏取り済み（0警告0エラー、既存15件全合格）。忍者実機確認へ進めてよい。

## 確認観点と根拠

### (a) DoD整合確認

`docs/todo.md`763-765行「`docs/usage`11件はドラフト状態...参照先を`docs/usage`平易版へ切替える実装（増分4）が必要」との整合を確認。`csproj`側`EmbeddedResource`・`UsageWindow.Topics`配列の`ResourceFileName`・既存テストのファイル名参照、いずれもdocs/spec→docs/usageへ一貫して切替済み。DoD整合確認OK。

### (b) csproj側EmbeddedResourceの切替の正しさ

`grep -c "ecad2-spec-" Ecad2.App.csproj` → **0件**（旧docs/spec側の埋め込みが完全に除去されている）。`grep -c "ecad2-usage-" Ecad2.App.csproj` → **22件**（11ファイル×`Include`+`LogicalName`の2箇所=22件、過不足なし）。`dotnet build`で0警告0エラーを確認。切替は正しい。

### (c) DisplayName更新2件の妥当性、および申告漏れ1件の発見

`docs/usage/ecad2-usage-*.md`全11件の実際の見出し（1行目）を`head -1`で確認し、`UsageWindow.Topics`のDisplayNameと突き合わせた。

- undo-redo：「Undo/Redo」→「元に戻す・やり直し（Undo/Redo）」——実見出し`# 元に戻す・やり直し（Undo/Redo）`と完全一致。正しい。
- drc-output：「設計チェック(DRC)・出力パネル」→「設計チェック(DRC)・出力パネル・検索」——実見出し`# 設計チェック(DRC)・出力パネル・検索`と完全一致。正しい。

**申告漏れ1件を発見**：diffを1行ずつ突き合わせたところ、**statusbar**のDisplayNameも「ステータスバー・モード可視化」→「ステータスバー・モード表示」へ変更されていた（実見出し`# ステータスバー・モード表示`と一致、変更内容自体は正しい）。侍のコミットメッセージは「DisplayName更新2件（undo-redo・drc-output）」とのみ記載しており、statusbarの変更が申告から漏れている。他8件（sheet-document/menu-toolbar/placement/wiring/canvas-display/part-management/device-table/pdf-testmode）は変更なしを確認。

**実害はない**（変更内容自体は実際の見出しと正しく一致している）が、コミットメッセージの正確性の観点で軽微な指摘として記録する。

### (d) 既存テスト15件のアサーション緩和が検証意図を損なっていないか

増分2時点の`Assert.StartsWith("# ecad2 仕様書", content)`は、docs/spec全11ファイルが共通接頭辞「# ecad2 仕様書：{領域名}」を持っていたことに依存した検証だった。今回`docs/usage`平易版へ切替わったことで各ファイルの見出しがバラバラ（共通接頭辞なし、(c)節で確認済み）になったため、`Assert.StartsWith("# ", content)`へ緩和されている。

**検証力の低下を確認**：増分2コミットメッセージに明記された本来の設計意図「csproj側LogicalNameのtypo等をテストで検出可能にする」に照らすと、緩和後のテストは「Topics配列のresourceFileNameとcsproj側のLogicalNameが、両方とも実在する**別の**ファイルへ誤って対応してしまう」というクロスコンタミネーションを検出できなくなっている（別ファイルでも「# 」で始まる限りテストが通ってしまうため）。

**ただし完全に無力化したわけではない**：`Topics_ResourceFileNameが重複しない`（Topics内の重複防止）・`Topics_全項目のResourceFileNameが実際に読み込める`（存在確認）の2テストが引き続き機能しており、「対応するリソースが存在しない」typoは従来どおり検出できる。検出できなくなったのは「存在する別のファイルへ取り違える」という、より限定的な誤りのケースのみ。

**改善余地（家老・侍判断、隠密からの強制ではない）**：`[Theory]`の`InlineData`を`(string fileName, string expectedHeadingPrefix)`の2引数にし、各ファイルの実際の見出し文言を期待値として明示すれば、クロスコンタミネーション検出力を維持できたと考えられる。現状のテストでも実害は限定的（11ファイル中2つのtypoが特定の組み合わせで噛み合う必要があり発生確率は低い）と判断し、必須の修正としては提起しない。

### (e) code-reviewスキル併用

marketplace版導入後も起動不可（本セッション冒頭で再確認済み）。`onmitsu.md`既定どおり手動レビューで代替。

### ビルド・テスト裏取り

`dotnet build src/Ecad2.App/Ecad2.App.csproj --no-incremental`・`dotnet build tests/Ecad2.App.Tests/Ecad2.App.Tests.csproj --no-incremental` とも0警告0エラー。`dotnet test --no-build --filter "FullyQualifiedName~UsageWindowTests"` → **15件全合格**。

## 不明点

なし。

## 派生提案（範囲外の気づき）

特になし（(d)の改善余地は指摘内で言及済み、強制ではない）。

## 出典

- `src/Ecad2.App/Ecad2.App.csproj`（EmbeddedResource切替）
- `src/Ecad2.App/Views/UsageWindow.xaml.cs`（Topics配列）
- `tests/Ecad2.App.Tests/UsageWindowTests.cs`（アサーション緩和）
- `docs/usage/ecad2-usage-*.md`全11件（実見出し確認）
- `docs/todo.md`763-765行（増分4采配）
