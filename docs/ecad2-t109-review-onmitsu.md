# T-109 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `2c4d1dc`（ステータスバー「ツール:」表示の日本語化）
手法: `git show 2c4d1dc -- <path>` で範囲を明示した手動レビュー＋実測検証。effort=low（1周目既定）。

## 結論

**指摘なし。DoD（殿裁定「案B」統一感重視）を満たすと判断する。** ビルド・テスト裏取り済み（0警告0エラー、新規9件全合格）。忍者実機確認へ進めてよい。

## 確認観点と根拠

### (a) DoD整合確認

`docs/todo.md`53-87行「殿裁定（2026-07-21）＝案B（統一感重視）採用」との整合を確認。方針「内部実装（enum自体の名称・値）は変更せず、表示専用の変換ロジックを追加」どおり、`ToolModeToTextConverter`新設＋`MainWindow.xaml`のBindingへ`Converter={StaticResource ToolModeToText}`を追加する形で実装されている。DoD整合確認OK。

### (b) `ToolModeToTextConverter`の実装確認（表示専用であること）

コミット差分に`src/Ecad2.App/ViewModels/ToolState.cs`（`ToolMode` enum定義）への変更は含まれていない——`enum`自体の名称・値は完全に維持されている。`ToolModeToTextConverter`は`IValueConverter`実装で、`Convert`メソッドのみが`ToolMode`値→日本語文字列のswitch式変換を担う。`ConvertBack`は`NotSupportedException`を投げる（片方向バインディング専用の設計、Bindingも`Text="{Binding Tool.Mode, Converter=...}"`という一方向表示用途のみで実際に問題ない）。「内部実装は変更しない、表示専用」というDoDに完全に合致する。

`MainWindow.xaml`のBinding：`Text="{Binding Tool.Mode, Converter={StaticResource ToolModeToText}, StringFormat='ツール: {0}'}"`——WPFのBinding処理順序（Converter適用→StringFormat適用）に従い、Converterが先に日本語文字列（例：「選択」）を返し、その後StringFormatで「ツール: 」が前置される。正しい順序。

### (c) 8値全ての訳語の正確性

殿裁定の確定訳語8つと、`ToolModeToTextConverter.Convert`内のswitch式を1行ずつ突き合わせた。

| ToolMode | 殿裁定訳語 | 実装 | 一致 |
|---|---|---|---|
| `Select` | 選択 | `"選択"` | OK |
| `PlaceElement` | 要素配置 | `"要素配置"` | OK |
| `PlaceConnector` | 縦コネクタ記入 | `"縦コネクタ記入"` | OK |
| `PlaceFrame` | グループ枠記入 | `"グループ枠記入"` | OK |
| `PlaceLine` | 自由線記入 | `"自由線記入"` | OK |
| `PlaceDot` | 接続点記入 | `"接続点記入"` | OK |
| `PlaceWireBreak` | 配線分断記入 | `"配線分断記入"` | OK |
| `PlaceImage` | 画像配置 | `"画像配置"` | OK |

8値全て完全一致。`_ => value?.ToString() ?? ""`というフォールバック分岐もあり、将来`ToolMode`に新しい値が追加された場合も未対応値として安全にフォールバックする設計（クラッシュしない）。

### (d) `docs/usage/ecad2-usage-statusbar.md`修正内容の妥当性

「表示される項目」表のツール行：「`ツール: Select`」「英語表記のまま表示されます」という記述を「`ツール: 選択`」「（各値の意味は下記「ツール表示の見方」参照）」へ修正。「ツール表示の見方」節の表も、8値の表示値列を英語（`Select`等）から日本語訳語（選択等）へ全て更新。隠密が増分6で申し送った「殿裁定確定後にusage側の『英語表記のまま』という前提記述も書き換えが必要」という内容がそのとおり反映されている。実際に日本語化された後の表示内容と完全に一致しており、妥当。

### (e) code-reviewスキル併用

marketplace版導入後も起動不可（本セッション冒頭で再確認済み）。`onmitsu.md`既定どおり手動レビューで代替。

### ビルド・テスト裏取り

`dotnet build src/Ecad2.App/Ecad2.App.csproj --no-incremental`・`dotnet build tests/Ecad2.App.Tests/Ecad2.App.Tests.csproj --no-incremental` とも0警告0エラー。`dotnet test --no-build --filter "FullyQualifiedName~ToolModeToTextConverterTests"` → **9件全合格**（8値のTheoryテスト+ConvertBack例外テスト、コミットメッセージの件数と一致）。

## 不明点

なし。

## 派生提案（範囲外の気づき）

特になし。

## 出典

- `src/Ecad2.App/Converters/ToolModeToTextConverter.cs`
- `src/Ecad2.App/MainWindow.xaml`（Binding・Converter登録）
- `tests/Ecad2.App.Tests/ToolModeToTextConverterTests.cs`
- `docs/usage/ecad2-usage-statusbar.md`
- `docs/todo.md`53-87行（殿裁定・T-109采配）
- `src/Ecad2.App/ViewModels/ToolState.cs`（変更なきことの確認）
