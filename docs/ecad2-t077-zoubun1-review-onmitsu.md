# T-077増分1 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `6063714`（「使い方」ウィンドウPoC：非モーダル・F1・FlowDocument変換）
手法: `git show 6063714 -- <path>` で範囲を明示した手動レビュー。effort=low（1周目既定）。

## 結論

**指摘なし。DoD（殿裁定：F1割当・非モーダル・案B「FlowDocument自作変換」）を満たすと判断する。** ビルド・テスト裏取り済み（0警告0エラー、新規テスト8件全合格）。忍者実機確認へ進めてよい。1点、実機確認時の目視ポイントを申し添える（下記(d)参照、指摘ではない）。

## 確認観点と根拠

### (a) DoD整合確認

`docs/todo.md` T-077節654-663行の殿裁定（案B採用・案1ナビゲーション・F1割当）のうち、増分1（PoC）スコープの2点（非モーダル基盤・F1割当・案B技術方式の実現可能性確認）を対象に整合を確認した。「使い方(_G)」メニュー項目新設（`MainWindow.xaml`952-953行付近）、F1キー配線（`MainWindow.xaml.cs`2185行付近`Window_PreviewKeyDown`）、`MarkdownFlowDocumentConverter`新設（案B）、いずれも実装確認。ナビゲーションUI（案1）・全11領域対応は増分2スコープであり、本増分の対象外（コミットメッセージにも明記済み）。整合確認OK。

### (b) design-brief原則1の裏取り（F1常時有効の妥当性）

`docs/ecad2-ui-ux-design-brief.md`45行目：「単一フォーカスマネージャ＋フォーカス依存キーマップ切替（R1/R2/R4）：フォーカスの所在を...単キーショートカットはキャンバスフォーカス時のみ有効、**F1〜F12・修飾キーは常時有効**」。侍所見「F1キーは原則1（キャンバスフォーカス限定）の対象外、汎用機能のため常時有効」は、design-brief本文の記述と完全に一致する。F1はF1〜F12ファンクションキーの範疇であり、そもそも原則1の「単キーショートカット」（キャンバスフォーカス限定の対象）には含まれない。侍所見は正確。

### (c) 非モーダルWindow実装の設計妥当性（プロジェクト初）

`MainWindow.xaml.cs`の`ShowUsageWindow()`：
- `_usageWindow`をMainWindowのインスタンスフィールドとして保持し、`null`ならnewして`Owner = this`設定後`Show()`、既存インスタンスがあれば`Activate()`のみ実行——多重起動防止として妥当。
- `_usageWindow.Closed += (_, _) => _usageWindow = null;`——ウィンドウが閉じられた際に確実に参照をnull化しており、リーク（閉じた後もMainWindow側が古い参照を持ち続ける）を防いでいる。イベントハンドラ自体はUsageWindow側のイベントへの登録であり、UsageWindowがGC対象になれば一緒に解放されるため、ハンドラリークの懸念もない。
- `UsageWindow.xaml.cs`側：コンストラクタ内で埋め込みリソースを`using`ブロック経由で読み込み、変換後は`Stream`/`StreamReader`とも確実に破棄している。リソース解放漏れなし。
- `Owner=this`のみでZ-order制御は特別に行っておらず、WPF標準の非モーダル挙動（Ownerの上に表示、独立してAlt+Tab選択可能）に委ねている。GX Works3的な「並べて参照しながら作業できる」という要件（モーダルではないためMainWindow操作をブロックしない）を満たす設計。

指摘なし。

### (d) `MarkdownFlowDocumentConverter`の変換ロジック・表構文除外判断

実装（行単位ステートマシン）を通読：見出し(H1-H3)・水平線・コードブロック・箇条書き・番号付きリスト・通常段落（空行までの連続行結合）の判定順序、インライン強調・コードの正規表現処理、いずれも設計として妥当。新規テスト8件（見出し/段落結合/箇条書き/番号付きリスト/コードブロック/水平線/インライン強調・コード/未対応構文フォールバック）で該当構文を確認、`dotnet test`で全合格を実測確認済み。

未対応構文（表・リンク`[text](url)`等）は正規表現のいずれにもマッチせず通常段落として素通しされる（プレーンテキストのまま）。これは意図した挙動であり、テスト`Convert_未対応のMarkdown表構文はプレーンテキストの段落として残る`で確認済み。

**申し送り（指摘ではない）**：コミットメッセージは「表の少ない領域を選定する運用」としているが、実際に選定された`ecad2-spec-statusbar.md`にも表構文が1つ（`grep -c '^|'`で7行、27-33行目、項目/バインド先/書式/表示条件の4列×5行）含まれることを確認した。「表がゼロ」ではなく「少ない」という程度の話であり、コミットメッセージの主張と矛盾はしないが、実際にPoCを起動するとこの7行部分は`| 項目 | バインド先 | ... |`という生のMarkdown記法がそのまま崩れて表示される。機能不全ではなくPoCとして許容範囲と判断するが、忍者実機確認時にこの表部分の見た目（崩れて表示されること自体は仕様どおりなので実害ではない）を一応確認事項に含めることを提案する。

### (e) EmbeddedResource設計（単一の真実源）

`Ecad2.App.csproj`：`docs/spec/ecad2-spec-statusbar.md`を相対パス経由で`EmbeddedResource`として直接参照し、`LogicalName`でリソース名を明示。ビルド時にDLLへコンパイルされるため実行時に`docs/spec`自体への依存はなく、発行(publish)時の懸念もない。「`docs/spec`更新が再ビルドで自動反映される」という侍所見は正しい（手動コピー・同期作業が不要という設計上の利点）。

増分1時点では`docs/spec`原文（開発者向け詳細を含む）がそのまま表示される点は、`docs/todo.md`采配文に「増分1（PoC）」と明記されており、増分3（ユーザー向け平易版への変換）で差し替える前提のPoCとして妥当と判断する。将来、参照先パスを平易版ファイルへ切り替えるだけで済む設計であり、先を見た設計判断として整合的。

### (f) ダークモード連動

`MarkdownFlowDocumentConverter`内で`SetResourceReference`を使用する3箇所（`DialogForegroundBrush`＝FlowDocument全体の文字色、`PanelGridLineBrush`＝水平線、`InputBackgroundBrush`＝コードブロック背景）を確認。3キーとも`Theme.Dark.xaml`・`Theme.Light.xaml`両方に既存定義があることを確認した（新規キー追加なし、既存の確立されたテーマ機構を再利用）。`UsageWindow.xaml`側の`Background`/`Foreground`も`DialogBackgroundBrush`/`DialogForegroundBrush`——既存`AboutDialog.xaml`と同型パターンで、確立済みキーの再利用。指摘なし。

### (g) code-reviewスキル併用

marketplace版導入後も起動不可（本セッション冒頭で再確認済み）。`onmitsu.md`既定どおり手動レビューで代替。

### ビルド・テスト裏取り

`dotnet build src/Ecad2.App/Ecad2.App.csproj --no-incremental` → 0警告0エラー。`dotnet build tests/Ecad2.App.Tests/Ecad2.App.Tests.csproj --no-incremental` → 0警告0エラー。続けて`dotnet test --no-build --filter "FullyQualifiedName~MarkdownFlowDocumentConverterTests"` → **8件全合格**。

## 不明点

なし。

## 派生提案（範囲外の気づき）

特になし。

## 出典

- `src/Ecad2.App/MainWindow.xaml`（ヘルプメニュー952-953行付近）
- `src/Ecad2.App/MainWindow.xaml.cs`（`ShowUsageWindow`・`UsageMenuItem_Click`・F1キー配線）
- `src/Ecad2.App/Views/UsageWindow.xaml`・`UsageWindow.xaml.cs`
- `src/Ecad2.App/Views/MarkdownFlowDocumentConverter.cs`
- `src/Ecad2.App/Ecad2.App.csproj`（EmbeddedResource設定）
- `tests/Ecad2.App.Tests/MarkdownFlowDocumentConverterTests.cs`
- `docs/ecad2-ui-ux-design-brief.md`（45行目、原則1）
- `docs/todo.md` T-077節（626-663行、殿裁定・增分1采配）
- `src/Ecad2.App/Themes/Theme.Dark.xaml`・`Theme.Light.xaml`（DynamicResourceキー定義）
