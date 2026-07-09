# T-046 Stryker.NET導入可否調査（隠密）

> 2026-07-08 隠密調査。目的＝T-041で「旧実装でも新実装でも通る＝回帰を検出できないテスト」
> が複数残存していた実例を受け、ミューテーションテスティングのツール導入可否を判断する
> 材料集め。実際にグローバルツールとしてインストールし、Ecad2.Core/Ecad2.App双方に対して
> 実行して実測した（`src`/`tests`は未変更、出力はスクラッチパッドへ）。

---

## 結論：**導入推奨（ただし段階的に。まず手動実行での棚卸しから）**

Stryker.NET（バージョン4.16.0）は本プロジェクト構成（.NET 8、WPFプロジェクト含む、
Core+App 2対象プロジェクト×2テストプロジェクト）で問題なく動作することを実測確認した。
実行時間も全体で3分強と、CI常時実行には重いがローカル・定期実行には十分現実的な水準。
一方、現状のmutation score（Core 3.76%・App 19.79%）が非常に低く、いきなりCI必須化・
閾値設定を行うと即座に失敗が頻発する。**まず手動実行でテストの弱点を可視化する運用から
始め、スコアが一定水準に改善してからCI組み込みを検討する段階的導入を推奨する**。

---

## 調査観点①：WPFプロジェクトとの相性・既知の制約

**問題なく動作することを実測確認した**。`Ecad2.App.csproj`（`TargetFramework=net8.0-windows`、
`UseWPF=true`）を対象に`dotnet stryker -p "Ecad2.App.csproj"`を実行：

```
[INF] Found project C:\ECAD2\src\Ecad2.App\Ecad2.App.csproj to mutate.
[INF] 2265 mutants created
Killed:   333
Survived: 233
[INF] Time Elapsed 00:01:43.7990197
[INF] The final mutation score is 19.79 %
```

WPF特有のエラーは一切発生しなかった。発生したコンパイルエラー（325件）は
`MainWindowViewModel.cs`/`MainWindow.xaml.cs`内の通常のロジック（未割り当てローカル変数を
参照するミュータント）由来で、WPF機構（XAML・ビジュアルツリー等）とは無関係。Web一次情報
（Stryker.NET公式ドキュメント・GitHub Issues）を検索した限り、WPF固有の既知の制約を報告する
Issueは見当たらなかった（.NET 8対応・ターゲットフレームワーク検出に関する一般的な既知
問題はあるが、いずれも本プロジェクトでは再現しなかった）。

**推測**：Stryker.NETはMSBuildベースでプロジェクトをビルドしてからCecil/Roslynでミュータント
を注入する方式のため、UIフレームワークの種類（WPF/WinForms/コンソール等）に依存しない設計に
なっていると考えられる。

---

## 調査観点②：実行時間の見込み

実測値（本プロジェクトの現在の規模、157〜179件のApp.Testsを含む状態で計測）：

| 対象プロジェクト | ミュータント数 | 実行時間 | mutation score |
|---|---|---|---|
| Ecad2.Core（14件のテスト） | 2557 | 約63秒 | 3.76%（Killed74/Survived127/NoCoverage1842） |
| Ecad2.App（165件のテスト、WPF） | 2265 | 約104秒 | 19.79%（Killed333/Survived233/NoCoverage1117） |
| **合計** | **4822** | **約167秒（3分弱）** | — |

`concurrency`（並列ワーカー数、デフォルトは論理コア数）に強く依存する。今回はデフォルト
設定（Core実行）および`-c 4`明示指定（App実行）で計測。実行環境のCPUコア数が変われば
比例して変動する（推測）。

**mutation scoreの低さが示すもの**：Core・Appともに「NoCoverage」（テストで一切カバーされて
いない）の割合が非常に高い（Core 72%、App 49%）。これはT-041往復レビューで発見した
「テストが実装の呼び出しパターンと一致していない」「旧実装でも新実装でも通るテスト」問題
とは別の観点だが、**テストカバレッジ自体の薄さを定量的に可視化する**という意味で、
T-046の新制度（テスト設計技法の必須化）の必要性を裏付ける材料になると考える。

---

## 調査観点③：導入形態の選択肢と推奨

| 選択肢 | 評価 |
|---|---|
| 全体CI的に回す（毎コミット/毎PR） | 実行時間（3分強）自体は許容範囲だが、**現状のmutation scoreの低さでは閾値設定が形骸化する**（`--threshold-low`のデフォルト60%を大きく下回るため、現実的な閾値がほぼ「0%以上」になってしまう）。時期尚早と判断する。 |
| 変更ファイル限定（`-m`オプションで差分のみ） | `-m`オプション自体は存在するが、**今回の試行で挙動に不明点があった**（`Element.cs`単体を指定した際、期待した絞り込みにならず「0 total mutants will be tested」という結果になった、原因未特定）。採用する場合は事前に構文・挙動の追加検証が必要（推測ではなく実際に遭遇した未解決の疑問点として明記する）。 |
| 手動実行のみ（往復修正案件のクローズ時・定期棚卸し） | **現状最も現実的**。T-041のような往復の多い修正案件がクローズした際に、対象範囲を絞って実行し「テストで本当に検出できるか」を事後確認する運用に馴染む。 |

**推奨**：手動実行から始める段階的導入。具体的には、往復修正案件（バグ修正・新制度対象案件）
のクローズ時に、変更のあったViewModelクラス単位で手動実行し、mutation scoreとSurvived
ミュータントの一覧を確認する運用を提案する。CI組み込みは、主要なViewModelのmutation scoreが
一定水準（例えば60%程度）に達してから再検討するのが現実的と考える。

---

## 調査観点④：Stryker.NET以外の代替

Web検索の結果、**Stryker.NETが.NETエコシステムにおける事実上唯一の実質的な選択肢**である
ことを確認した。学術文献（Brazilian Symposium on Software Quality等）ではNester・
NinjaTurtles・VisualMutator・PexMutator・CREAMといった名前が挙がるが、いずれも現在の
メンテナンス状況・.NET 8対応状況を示す一次情報は見当たらず、実運用に耐えるかは不明
（推測：学術研究目的で作られた古いツールが多く、活発な開発は行われていない可能性が高い）。
商用ツール（NCrunch等）にも類似機能はあるが、用途・ライセンス体系が異なるため、無償OSSの
Stryker.NET以外に有力な代替は無いと判断する。

---

## 実行時の注意点（2026-07-09追記、T-045クローズ前棚卸しで実際に遭遇）

**実行ディレクトリを誤ると失敗する**：`src/Ecad2.App`（対象プロジェクト自体のディレクトリ）
から`dotnet stryker`を実行すると、StrykerがEcad2.App.csproj自体をテストプロジェクトと誤認識し
（ログに`Using ...\Ecad2.App.csproj as test project`と出る）、`can't be mutated because no
test project references it`という警告のあと`Failed to analyze project builds. Stryker cannot
continue.`で失敗する。

**必ず`tests/Ecad2.App.Tests`ディレクトリ（テストプロジェクト側）から`-p "Ecad2.App.csproj"`
を指定して実行すること**：

```
cd tests/Ecad2.App.Tests
dotnet stryker -p "Ecad2.App.csproj" -c 4 --output <出力先>
```

（Ecad2.Core側も同様に`tests/Ecad2.Core.Tests`から実行する）

---

## 出典・参照

- 実測：グローバルツール`dotnet-stryker`（バージョン4.16.0）を実際にインストールし、
  `Ecad2.Core.Tests`/`Ecad2.App.Tests`から実行（出力先はスクラッチパッド、`src`/`tests`は
  未変更）
- [Stryker.NET GitHub](https://github.com/stryker-mutator/stryker-net)
- [Stryker.NET公式ドキュメント（Introduction/Configuration）](https://stryker-mutator.io/docs/stryker-net/introduction/)
- [Microsoft Learn: Mutation testing - .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/mutation-testing)
- `src/Ecad2.App/Ecad2.App.csproj`（`UseWPF=true`、`net8.0-windows`の構成確認）
- `docs/ecad2-t045-structure-survey-onmitsu.md`（並行して実施したT-045調査、Stryker実測に
  用いたビルド環境の前提を共有）
