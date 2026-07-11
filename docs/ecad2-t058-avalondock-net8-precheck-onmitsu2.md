# T-058 AvalonDock導入前の事前確認調査（.NET 8対応）

調査者: 隠密2　最終更新: 2026-07-11

前任調査（`docs/ecad2-t058-docking-float-survey-onmitsu.md`）で「.NET 8明示対応が未確認」と
残った論点の深掘り。Web調査（GitHub API・NuGet.orgが一次情報源）、実装・書き込みは行っていない。

---

## DoD(1): .NET 8（net8.0-windows）明示サポートの有無

### 確定事実（ソースコード直接確認）

最新安定版**Dirkster.AvalonDock 4.74.1**（2026-04-25リリース）の`source/Directory.Build.props`
（GitHub API経由でタグ`v4.74.1`時点の実ファイルを直接取得・確認）：

```
ComponentTargetFrameworks = netcoreapp3.0;net5.0-windows;net9.0-windows;net10.0-windows;net40;net48
```

**net6.0-windows・net7.0-windows・net8.0-windowsのいずれも明示的ターゲットに含まれていない**
——net5.0-windowsの次はnet9.0-windowsへ直接飛んでおり、net6〜8は意図的にスキップされている。
NuGet.org上の表示でも同様（net8.0-windows等は「計算済み互換」表記のみで明示サポートなし）。

### 経緯（なぜ.NET8が明示対象にないか）

- Issue #467「.NET 8 Update」（2023-12-16提出、ユーザー要望）に対し、メンテナ本人
  （sharpSteff）が**2026-04-05に「最新プレビューnugetは既に.NET9・.NET10もターゲットにしている」
  とコメントしてクローズ**。→ **.NET8を経由せず直接.NET9/.NET10へ進む意図的な方針**と判明
  （技術的障壁やバグによる断念ではない）。
- Issue #491「Feature: Changed target frameworks」（net6.0/net8.0/net4.6.2への変更を提案するPR、
  2024-10-07クローズ）は**コメント無しでクローズ**——提案どおりには採用されなかった（理由の明記
  はGitHub上に見当たらず、上記の.NET9/10直行方針が実質的な採用ルートになったと推測される）。
- v5.0.0は現在プレビュー中（5.0.0-preview.98、2026-07-09更新）で、MVVM/DI強化等の大型刷新を
  含むが**まだ安定版ではない**。

### .NET 8プロジェクトからの参照時の動作見込み

NuGetの資産選択規則により、`net8.0-windows`プロジェクトが`Dirkster.AvalonDock`を参照すると、
明示ターゲットの中で最も近い下位互換TFMである**`net5.0-windows`アセットに解決される**見込み
（WPFは.NET 5以降API面で大きな破壊的変更がなく、動作する可能性は高いと考えられる）。ただし
**実際に.NET8プロジェクトで動作したという一次報告・実機確認記録は本調査では見つからず、
「未確認」のまま**——前任調査の「導入前に小規模PoCで実機確認すべき」という所見をそのまま追認する。

## DoD(2): .NET 8 WPFでの動作報告・既知Issue

GitHub Issue検索・Web検索の両方で、「.NET 8で（AvalonDockが）クラッシュする」という直接的な
報告は**見当たらなかった**。関連して見つかったのは：

- Issue #510「Binding Errors from Style.Triggers in Theme.xaml」（Open、bugラベル、2025-08-21
  提出）——テーマXAMLのStyle.Triggersに起因するバインディングエラー。.NET8固有かどうかは
  本調査では確認できず（中身未読、タイトルのみ確認）。
- 深刻なクラッシュ・レイアウト崩れの集中報告は見当たらないが、**「見つからなかった」ことは
  「問題が無い」ことの証明にはならない**（不在証明の限界。検索網羅性の限界であり、断定は避ける）。

## DoD(3): テーマパッケージの要否・構成

コアパッケージ（`Dirkster.AvalonDock`）に見た目のテーマは同梱されず、**別途テーマパッケージの
追加が前提の構成**。公式README（v4.74.1時点）確認済みの一覧：

| パッケージ名 | 備考 |
|---|---|
| `Dirkster.AvalonDock.Themes.Arc` | 新テーマ（Dark/Light、READMEで「NEW!」表記、モダンな丸角・コンパクトタブ意匠） |
| `Dirkster.AvalonDock.Themes.VS2013` | Dark/Light/Blueの3種、**最多ダウンロード数**（392,654） |
| `Dirkster.AvalonDock.Themes.VS2010` | VS2010風 |
| `Dirkster.AvalonDock.Themes.Metro` | Metro/WinUI風 |
| `Dirkster.AvalonDock.Themes.Aero` | 旧Windows Aero風 |
| `Dirkster.AvalonDock.Themes.Expression` | Expression Blend風（Dark/Light） |

適用は`DockingManager.Theme`プロパティへ`<Vs2013DarkTheme />`等のインスタンスを設定する方式
（コード/XAMLどちらでも可）。カスタムブラシのみを読み込む軽量な代替手段
（`ResourceDictionary.MergedDictionaries`への直接マージ）も用意されている。README注記：
「これらのテーマ定義はAvalonDock内のコントロールのみを対象とし、標準のButton/TextBlock等は
別途MahApps.Metro等の汎用テーマライブラリで対応する必要がある」——ecad2は現状そうした汎用WPF
テーマライブラリを導入していないため、テーマ適用範囲がAvalonDock内部に閉じる点は留意が必要。

GX Works3踏襲のクラシックな意匠を志向するecad2の既存デザイン方針には、VS2013系（最多実績・
Dark/Light/Blue選択可）が親和性が高いと考えられる（隠密所感、決定は着手時の殿確認事項）。

## DoD(4): 最小導入手順の整理（手順書レベル、PoC実装は侍職分）

1. **NuGet追加**：`Dirkster.AvalonDock`（コア）＋テーマパッケージ1つ（例：
   `Dirkster.AvalonDock.Themes.VS2013`）。
2. **XAML名前空間宣言**：`xmlns:avalonDock="https://github.com/Dirkster99/AvalonDock"`
   （公式サンプル`source/TestApp/MainWindow.xaml`のタグ`v4.74.1`時点を直接確認した正確な値）。
3. **DockingManager配置**：対象領域に`<avalonDock:DockingManager>`を置き、
   `<DockingManager.Theme><Vs2013DarkTheme /></DockingManager.Theme>`でテーマ指定。
4. **レイアウト階層**：`LayoutRoot`→`LayoutPanel`（`Orientation`指定）の下に、ツールウィンドウ側
   は`LayoutAnchorablePane`→`LayoutAnchorable`、ドキュメント側は`LayoutDocumentPaneGroup`→
   `LayoutDocumentPane`→`LayoutDocument`という階層で組む（公式サンプルで実物確認済み）。
5. **MVVM連携**：ecad2は`Ecad2.App.csproj`確認の結果**CommunityToolkit.Mvvmを使用していない**
   （手動実装と見込まれる）ため、`Dirkster.AvalonDock.Mvvm`（無印）で足り、
   `Dirkster.AvalonDock.Mvvm.CommunityToolkit`版は不要と見込まれる。
6. **レイアウト永続化**：前任調査（`docs/ecad2-t058-docking-float-survey-onmitsu.md`5節）が
   課題として挙げていた「アプリ再起動時の配置復元」に対応する公式パッケージ
   `Dirkster.AvalonDock.Serializer.Xml`／`.Json`が存在する——自前実装不要で足りる見込み。

### 【注記】本調査中に遭遇したWebFetch要約の誤り

調査序盤、汎用WebFetchツール（小型要約モデル経由）が「`xmlns:avalonDock=
"http://schemas.xceed.com/wpf/xaml/avalondock"`」「`ToggleDockingManager`」「DI統合パッケージが
README記載」といった情報を提示したが、これは**v5.0.0プレビュー版（未リリース）の新機能または
誤情報が混入したもの**であり、安定版v4.74.1の実物（GitHub API経由でcsproj・README・サンプル
XAMLを直接取得）と食い違っていた。本調査書の数値・コード片は全てGitHub API直接取得（`gh api`
コマンド）またはソースファイル直読で裏取り済みだが、**今後この種の調査で汎用Web要約ツールを
使う際は、バージョン混同・軽微な幻覚のリスクを踏まえ一次ソースでの裏取りを徹底すべき**という
教訓として記録しておく。

## 出典一覧

- `source/Directory.Build.props`（`Dirkster99/AvalonDock`リポジトリ、タグ`v4.74.1`、GitHub API
  `gh api repos/Dirkster99/AvalonDock/contents/...`で直接取得）
- `source/TestApp/MainWindow.xaml`（同上タグ、GitHub API直接取得、xmlns宣言・DockingManager配置
  実例を確認）
- `README.md`（同上タグ、GitHub API直接取得）
- GitHub Issue #467「.NET 8 Update」・#491「Feature: Changed target frameworks」・#510
  （`gh api repos/Dirkster99/AvalonDock/issues/...`）
- GitHub Releases一覧（`gh api repos/Dirkster99/AvalonDock/releases`、最新安定版4.74.1確認）
- NuGet.org `Dirkster.AvalonDock`パッケージページ（TFM一覧・バージョン情報、WebFetch）
- `src/Ecad2.App/Ecad2.App.csproj`（Read、TargetFramework=net8.0-windows・CommunityToolkit.Mvvm
  不使用の確認）
- `docs/ecad2-t058-docking-float-survey-onmitsu.md`（前任調査、Read）

## 不明点

- Issue #510のバインディングエラーが.NET8固有か否かは中身未読のため不明（深掘りは範囲外と判断、
  必要なら追加調査可能）。
- .NET8プロジェクトからの実機動作報告は本調査の検索範囲では発見できず——「問題が無い」ことの
  確認ではなく「見つからなかった」に留まる。**PoCでの実機確認は引き続き必須**。

## 派生提案の有無

なし（家老采配の範囲内で完結）。
