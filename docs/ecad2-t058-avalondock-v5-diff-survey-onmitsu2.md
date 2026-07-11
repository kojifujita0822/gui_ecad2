# T-058関連 AvalonDock 4.74.1 と 5.0.0(プレビュー) 機能差分調査

調査者: 隠密2　最終更新: 2026-07-11

殿の意向（将来5.0へ移行した場合の留意点を今のうちに把握）を受けた家老采配。一次情報源
（GitHubリポジトリ本体、`gh api`直接取得）を優先し、実装・書き込みは行っていない。

**最重要の一次資料**：masterブランチ（v5.0.0プレビュー開発元）に公式移行ガイド
`docs/migration/breaking-changes.md`が存在し、破壊的変更が体系的に文書化されている。本調査は
これを主軸に、実際のコミット履歴・Issue/PRでの実運用者フィードバックまで踏み込んで裏取りした。

---

## DoD(1): 5.0.0の主な新機能・変更点の具体化

`docs/migration/breaking-changes.md`（masterブランチ、`gh api`で直接取得）「New Features
(Non-Breaking)」節より：

| 機能 | パッケージ | 内容 |
|---|---|---|
| `ToggleDockingManager` | `AvalonDock` | VS Code/Rider風、トグルボタン付きサイドバー |
| Arcテーマ | `AvalonDock.Themes.Arc` | Dark/Light変種を持つモダンテーマ（T-058前回調査で既確認） |
| JSONシリアライザ | `AvalonDock.Serializer.Json` | JSON形式のレイアウト永続化（新規） |
| MVVM基底クラス群 | `AvalonDock.Mvvm` | `DockableBase`・`ToolboxBase`・`DockLayoutService`等 |
| MVVM CommunityToolkit統合 | `AvalonDock.Mvvm.CommunityToolkit` | `ObservableDockableBase`・`ObservableToolboxBase`（ソースジェネレータ活用） |
| DI統合 | `AvalonDock.DependencyInjection` | `AddAvalonDock()`拡張メソッド |
| Core抽象化 | `AvalonDock.Core` | `IFactory`・`IDockingManager`・`IAutoHideManager`等のインターフェース群 |
| DTOシリアライズ | `AvalonDock.Core` | シリアライズがDTOレイヤーへリファクタ、`LayoutSerializerBase`でカスタムシリアライザ拡張可能 |

**所見**：前回調査（`docs-notes`前任情報）で「MVVM/DI強化の大型刷新」と見立てた内容が、
具体的なクラス名・パッケージ名レベルで裏付けられた。特に`AvalonDock.Mvvm.CommunityToolkit`は、
ecad2が現状CommunityToolkit.Mvvmを未使用（T-058前回調査で確認済み）なため直ちに関係しないが、
将来ecad2がCommunityToolkit.Mvvmを採用する場合には親和性が高くなる新機能。

## DoD(2): 破壊的変更の有無

同資料「Breaking Changes in v5.0.0」節より（要約、原文は英語）：

### パッケージ構造

- **シリアライザが別パッケージへ分離**（影響度=High、公式表記）：XMLシリアライザは
  `Dirkster.AvalonDock`本体から`Dirkster.AvalonDock.Serializer.Xml`へ移動。
  名前空間も`AvalonDock.Layout.Serialization` → `AvalonDock.Serializer.Xml`に変更。
  修正は「シリアライザパッケージを追加インストールし`using`文を更新するだけ」（公式Fix）。
- 新規`AvalonDock.Core`パッケージが自動参照される（影響度=Low、明示インストール不要）。

### アーキテクチャ

- `ILayoutEngine`インターフェースの新設（影響度=Low〜Medium、カスタムレイアウト計算ロジックを
  内部APIで書いている場合のみ影響。ecad2は現状AvalonDock自体未導入のためこの種のカスタム
  ロジックは存在せず、影響は無いと見込まれる）。

### ターゲットフレームワークの変更（影響度=High、公式表記）

| フレームワーク | 状態 |
|---|---|
| .NET Framework 4.0 | **削除** |
| .NET Framework 4.5.2 | **削除** |
| .NET Core 3.0 / 3.1 | **削除** |
| .NET 5.0 | **削除** |
| .NET 6.0 / 7.0 / 8.0 | **引き続き対象外**（Not targeted、4.74系から変化なし） |

**v5.0.0でサポートされるフレームワークは.NET Framework 4.8・.NET 9.0（-windows TFM）・
.NET 10.0（-windows TFM）のみ**。masterブランチの`source/Directory.Build.props`
（`gh api`で直接取得）でも`ComponentTargetFrameworks = net9.0-windows;net10.0-windows;net48`
と確認済み——**4.74系にあった`net5.0-windows`という緩やかな受け皿が5.0.0では完全に消える**。

**【最重要の懸念点】**：4.74系はecad2（`net8.0-windows`）から見て`net5.0-windows`アセットへの
フォールバック解決が期待できたが（T-058前回調査参照）、5.0.0は`net48`（.NET Framework、フル
フレームワーク）か`net9.0-windows`以上しか選択肢がない。**.NET8プロジェクトが5.0.0を参照する
場合、フォールバック解決先が`net48`（.NET Frameworkアセンブリ）まで下がる可能性が高く**、
.NET Core/.NET系プロジェクトから.NET Frameworkのみのアセンブリを参照する構成は動作面での
制約・非推奨パターンになりやすい。**実質的に「ecad2側を.NET 9以降へ上げない限り、5.0.0への
追随は難しい」という結論になり得る**（推測を含む所見、実機検証はしていない）。

### 挙動変更（公式表記=Low、ただし実運用報告と乖離あり——下記参照）

「下部ドッキングパネルの再スタック挙動」のバグ修正。公式表記は影響度Lowだが、**実際の利用者
フィードバックでは当初「High/Breaking」と評価された**（後述Issue #604参照、最終的には別原因と
判明し収束）。

## DoD(3): レイアウト永続化（Serializer）の互換性

**一次情報として、実運用者からの詳細なインパクト報告とメンテナの迅速な対応が確認できた
（Issue #604、2026-07-03提出、closed）**：

1. 報告者（10モジュール・30〜40件のXMLレイアウトを持つ規模の利用者）が「v4で保存したレイアウトが
   v5で壊れる、手動で全レイアウトを再作成する必要があり5〜10時間かかった」と報告し、公式移行
   ガイドの影響度表記を「Low」から「High/Breaking」へ修正すべきと提起。
2. メンテナ（sharpSteff）が「実例をくれれば直せる」と即応。
3. 報告者が自己分析の結果、**真因は「レイアウトロジックのバグ」ではなく「シリアライズの
   フォーマット非互換」**（XMLエンコーディング宣言の不一致・大文字始まりbool値`"True"/"False"`を
   v5の`XmlSerializer`が受け付けない）と特定、回避策（文字列置換）を共有。
4. メンテナが**5.0.0-preview.96で「xml-v4互換レイヤー」を追加**、報告者が動作確認し解決を確認。
5. 対応PR #607（2026-07-07マージ、`gh api`で本文取得済み）の詳細：
   - DTOスキーマは既にv4のワイヤーフォーマットと**1:1で一致**（要素名・属性名・要素順序等、
     リファクタ前コードとの照合で検証済みと明記）。
   - 唯一の非互換要因は上記2点（エンコーディング宣言・bool大文字小文字）で、これをPR#607が解消。
   - **逆方向（v5で保存→v4で読込）も変更不要**（v4は`bool.Parse`で大文字小文字を区別せず
     読むため、v5の出力もそのまま受け付ける）。
   - ゴールデンファイルテスト（v4実物レイアウトの完全な読込テスト：ネスト要素のxmlns混入・
     フローティングウィンドウ・アンカーグループ・非表示セクション込み）も追加済み。

**結論**：レイアウト永続化のファイルフォーマット自体（XML内容）は**4.74系と5.0系でほぼ完全
互換**（PR#607適用後、最新プレビュー[preview.98、96より新しい]には反映されている見込み）。
ただし**API面（名前空間・パッケージ）は破壊的変更あり**——コード上は`using`文の更新と
シリアライザパッケージの追加インストールが必要（公式表記どおり「10〜20分程度」の軽微な作業）。

## DoD(4): 5.0安定版リリース見込み・開発の活発度

- 現在**5.0.0-preview.98**（2026-07-09時点、NuGet.org確認）。プレビュー番号が98まで進んでいる
  ことから、相当数の反復を経ていると推測される。
- **開発は非常に活発**：masterブランチの直近コミット5件はいずれも2026-07-03〜07-08の1週間に
  集中（`gh api`直接取得で確認）。うち1件のコミットメッセージ「Some Fixes for v5 occured
  during production usage」（2026-07-05）は、**プレビュー段階のv5が既に一部利用者の実運用で
  使われ始めている**ことを示唆。
- 安定版（5.0.0正式リリース）の具体的な時期表明は、本調査で確認した一次資料の範囲では
  **見当たらず、不明**。ただし上記の実運用フィードバック対応の速さ（Issue報告→2プレビュー
  バージョン以内で修正）から、開発体制は活発かつ利用者対応も迅速と評価できる。
- 並行して安定版4.74系も継続保守されている（v4.73.0→v4.74.0→v4.74.1が2026年4月に短期間で
  複数回リリース、T-058前回調査で確認済み）——5.0系の開発と4.74系の保守は並走中。

## DoD(5): 結論 — 4.74.1で今実装した場合、将来5.0移行時に書き直しを要する箇所

| 領域 | 書き直しの要否 | 見積り |
|---|---|---|
| レイアウトファイル形式（XML） | **不要**（PR#607以降ほぼ完全互換） | ゼロ |
| シリアライザのコード（`using`文・パッケージ参照） | **要**（名前空間・パッケージが変わる） | 軽微（公式見積り10〜20分） |
| カスタムレイアウト計算ロジック | 条件付き（`ILayoutEngine`実装が必要になる場合のみ） | ecad2は通常のドッキングUIのみなら影響小と見込む |
| MVVM実装（ViewModel連携） | 不要（強制ではない、新設の`AvalonDock.Mvvm`基底クラスへの
  移行は任意のアップグレードパスであり、4.74時点の自前実装のままでも5.0で動作継続可能と見込まれる） | 移行する場合のみ中程度 |
| **ターゲットフレームワーク（ecad2自体）** | **【最重要】ecad2が`net8.0-windows`のままだと
  5.0.0への追随自体が困難になる可能性が高い**（5.0.0は`net48`/`net9.0-windows`以上のみ対象、
  4.74系にあった`net5.0-windows`という緩衝フォールバックが消えるため） | 大——ecad2自体の
  .NET 9以降への移行が前提になりうる（未検証の推測を含む、実機確認は次段階の課題） |

**総合所見**：4.74.1でAvalonDockを今すぐ導入すること自体のリスクは、レイアウト永続化の観点
では低い（フォーマット互換性は確保されている）。しかし、**「将来5.0へ移行する」という前提を
置くなら、それはAvalonDockのマイナーアップグレードでは済まず、ecad2自身のターゲット
フレームワークを.NET 8から.NET 9以降へ引き上げる意思決定とセットになる可能性が高い**——
これは本調査の一次資料からの推測であり、実機検証はしていない点に留意されたい。

## 出典一覧

- `docs/migration/breaking-changes.md`（`Dirkster99/AvalonDock`リポジトリ、masterブランチ、
  `gh api`で直接取得）
- `source/Directory.Build.props`（同上masterブランチ、TFM確認）
- GitHub Issue #604「Update impact levels in breaking changes documentation」とそのコメント全件
  （`gh api`で本文・コメント直接取得）
- Pull Request #607「Make XML layout deserialization tolerant of legacy formats」（本文・
  マージ日時、`gh api`で直接取得）
- masterブランチの直近コミット履歴5件（`gh api repos/.../commits?sha=master`）
- NuGet.org `Dirkster.AvalonDock`パッケージページ（バージョン5.0.0-preview.98確認、WebFetch）
- `docs/ecad2-t058-avalondock-net8-precheck-onmitsu2.md`（前回調査、本調査の前提として参照）

## 不明点

- 5.0.0安定版の正式リリース時期は一次資料からは確認できず不明。
- `ILayoutEngine`実装がecad2の想定用途（通常のドッキングUI）で実際に必要になるかは、
  導入時の設計次第であり本調査の範囲では未確定。
- 「.NET8プロジェクトから5.0.0がnet48にフォールバックする」という懸念は、TFM構成からの
  論理的推測であり、実機での動作確認（PoC）は行っていない。

## 派生提案の有無

なし（家老采配の範囲内で完結）。
