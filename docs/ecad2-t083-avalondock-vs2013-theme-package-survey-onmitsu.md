# T-083向け調査: AvalonDock VS2013テーマパッケージの配信元確認(隠密)

調査日: 2026-07-14　調査者: 隠密　依頼元: 家老（T-083、殿提示URLの使用可否確認）

## 調査題目

殿より提示された以下URLの「Dirkster.AvalonDock.Themes.VS2013」パッケージ（VS2013 Dark/Lightテーマ、
AvalonDockドッキングクローム向け）の使用可否。

> https://github.com/Clear-Bible/AvalonDock/pkgs/nuget/Dirkster.AvalonDock.Themes.VS2013

## 結論（要約）

**殿提示のGitHub Packages版（Clear-Bibleフォーク）は不採用が妥当、nuget.org公式版（Dirkster99本人
所有・4.74.1・侍導入のAvalonDock本体と完全一致）の採用が明確に優位**。殿裁可済み、
`docs/todo.md` T-058節へ記録済み（本調査書はその出典として残す）。

## 事実（出典付き）

### 1. 殿提示のGitHub Packages版（Clear-Bibleフォーク）の素性

1. **配信元は第三者フォーク**: `Clear-Bible/AvalonDock`はAvalonDock本体の開発元
   `Dirkster99/AvalonDock`本人のリポジトリではなく、聖書翻訳支援ソフトウェアを開発する組織
   Clear-Bible（Clear.Bible）による第三者フォーク。GitHub Packagesページに
   "forked from Dirkster99/AvalonDock"と明記。README記載は
   "Our own development branch of the well known WPF document docking library"。
   出典: https://github.com/Clear-Bible/AvalonDock ,
   https://github.com/Clear-Bible/AvalonDock/pkgs/nuget/Dirkster.AvalonDock.Themes.VS2013
2. **最新版4.70.5・約4年前更新停止**: 殿の言及どおり裏取り確認済み。バージョン履歴は
   4.70.2〜4.70.5のみでそこから更新なし。
   出典: 上記GitHub Packagesページ
3. **本体4.74.1（侍導入・Dirkster99本家フォーク）とのバージョン乖離**: 4.70.5は4.74.1と離れており、
   依存関係の明記も確認できず互換性に懸念あり。
4. **GitHub Packages配信固有の認証コスト**: GitHub PackagesのNuGetフィードは公開パッケージであっても
   匿名取得不可で、PAT（`read:packages`スコープ）による認証設定が常に必要。CI環境では
   `GITHUB_TOKEN`で代替可能な場合もあるが、同一Organization内リポジトリに限定される制約もある。
   採用するならビルド環境・開発者PC双方にPAT設定という追加運用コストが発生する。
   出典: https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry

### 2. nuget.org公式版（採用決定版）

隠密自身がWebFetchで一次確認済み（2026-07-14、出典: https://www.nuget.org/packages/Dirkster.AvalonDock.Themes.VS2013）。

- **所有者（Owners）**: Dirkster99本人（AvalonDock本体と同一）
- **最新安定版**: 4.74.1（2026-04-25公開）——侍導入のAvalonDock本体4.74.1と**完全一致**
- **依存関係**: `Dirkster.AvalonDock >= 4.74.1`
- **対応TFM**: `net40`以上、`netcoreapp3.0`以上、`net5.0-windows7.0`、`net9.0-windows7.0`、
  `net10.0-windows7.0`を含む（ecad2のnet10化と合致）
- **配信形態**: nuget.org通常配信のため匿名取得可、GitHub Packagesのような追加認証設定は不要
- 5.0.0-preview系列でも開発継続中（2026年7月時点で活発）
- 姉妹テーマパッケージ（VS2010/Aero/Expression等）も同系列でnuget.org公開されている模様（推測、
  個別の依存関係までは未確認）

## 判断根拠（比較表）

| 観点 | GitHub Packages版(Clear-Bible) | nuget.org公式版(Dirkster99) |
|---|---|---|
| 所有者 | 第三者フォーク | 本体と同一(Dirkster99) |
| 最新版 | 4.70.5(4年前停止) | 4.74.1(2026-04-25、活発) |
| 本体4.74.1との整合 | 不明・乖離懸念 | 完全一致(依存関係で明示) |
| net10.0-windows対応 | 不明 | 確認済み |
| 取得方法 | PAT認証必須 | 匿名取得可 |

## 不明点

- Clear-Bibleフォークが何のためにVS2013テーマを独自パッケージ化したか（本家に無い機能追加等の
  可能性）は未調査。ただし今回の採否判断には影響しない（本家版で要件を満たすため）。
- 姉妹テーマパッケージそれぞれの本体バージョンとの依存関係詳細は未確認（今回のスコープ外）。

## 裁定

殿裁可（2026-07-14）によりnuget.org公式版`Dirkster.AvalonDock.Themes.VS2013`(4.74.1)の採用に
決定。`docs/todo.md` T-058節へ記録済み。
