# T-051クローズ前 Stryker.NET手動棚卸し 実行障害記録（隠密）

> 2026-07-11 隠密調査。往復3周超え案件（T-051）クローズ時の掟（`docs/ecad2-t050-stryker-review-onmitsu.md`
> と同一制度）に基づき手動棚卸しを試みたが、**Ecad2.App（WPF）プロジェクト側でStrykerの解析自体が
> 失敗し、mutation実行に至らなかった**。本書は事実の記録のみ。対応方針は家老の裁定に委ねる。

---

## 1. 症状

`tests/Ecad2.App.Tests`から`dotnet stryker -p "Ecad2.App.csproj" -c 4 --output <出力先>`を実行すると、
以下のエラーで即座に失敗する（実行時間5秒程度、mutation自体は一切実行されない）：

```
[INF] Analyzing 1 test project(s).
[WRN] Analysis of project ..\..\src\Ecad2.App\Ecad2.App.csproj failed for frameworks net8.0-windows.
[INF] Project C:\ECAD2\src\Ecad2.App\Ecad2.App.csproj analysis failed hence can't be mutated.
Stryker.NET failed to mutate your project.
Failed to analyze project builds. Stryker cannot continue.
```

`--diag`オプションで詳細ログを見ると、根本原因は以下のMSBuildエラー（`NETSDK1022`系）：

```
重複する 'Compile' 個のアイテムが含められました。.NET SDK には、既定でプロジェクト
ディレクトリからのアイテムが 'Compile' 個含まれています。...重複するアイテムは、
'C:\ECAD2\src\Ecad2.App\obj\Debug\net8.0-windows\MainWindow.g.cs';
'C:\ECAD2\src\Ecad2.App\obj\Debug\net8.0-windows\Views\AddSheetDialog.g.cs';
'C:\ECAD2\src\Ecad2.App\obj\Debug\net8.0-windows\Views\RenameDialog.g.cs';
'C:\ECAD2\src\Ecad2.App\obj\Debug\net8.0-windows\Views\SheetSettingsDialog.g.cs';
'C:\ECAD2\src\Ecad2.App\obj\Debug\net8.0-windows\App.g.cs';
'C:\ECAD2\src\Ecad2.App\obj\Debug\net8.0-windows\GeneratedInternalTypeHelper.g.cs' でした。
```

`obj`内に既に存在するWPF XAML生成ファイル（`.g.cs`）が、Stryker内部のBuildalyzerが実行する
DesignTimeBuild（`SkipCompilerExecution=true`等の特殊フラグ付き）の過程で、SDK既定の
`**/*.cs`暗黙Compileグロブと重複認識される。

---

## 2. 試した切り分け・対処（いずれも解消せず）

1. **通常の`dotnet build --no-incremental`は問題なく成功する**（0エラー・0警告）。Strykerの
   DesignTimeBuild特有の経路でのみ発生。
2. **蓄積した一時ビルドファイル（`*_wpftmp*.GlobalUsings.g.cs`、2717個）を削除**→解消せず。
   （これらのファイル自体が今回の重複原因のリストには含まれていなかったが、異常な蓄積量
   だったため念のため削除した）
3. **`src/Ecad2.App/obj`・`bin`を完全削除→再ビルドで正常性確認→Stryker再試行**→解消せず。
   `obj`を空の状態から始めても、Stryker自身のDesignTimeBuild実行中に生成される`.g.cs`が
   同じ解析パス内で重複認識される模様。
4. **2回連続実行**（1回目の失敗で生成された`obj`状態のまま2回目を実行）→解消せず。
5. **Ecad2.Core.Tests側（WPF非依存）で同じ手順を実行→正常動作を確認**（49秒、
   2359 mutants skipped、301 tested、Killed156/Survived142/Timeout3、mutation score 7.43%）。
   **問題はWPFプロジェクト固有**と判明。

---

## 3. Web調査

`Stryker.NET "duplicate 'Compile' items" WPF MainWindow.g.cs Buildalyzer`で検索。

- Stryker.NET公式GitHub Issue #2083（"Failure to build project with modified
  IntermediateOutputPath"）に類似の症状報告あり（ただし本プロジェクトは
  `IntermediateOutputPath`をカスタマイズしていない、`Ecad2.App.csproj`を確認済み標準構成）。
- 一般論として、WPFプロジェクトでSDK既定のCompile/Page/ApplicationDefinitionアイテムと
  `obj`内生成物が重複する`NETSDK1022`系エラーは既知のパターンで、回避策として
  `EnableDefaultPageItems=false`等をcsprojへ明示設定する方法が挙げられている（これは
  **実装ファイル（`.csproj`）への書き込みを要するため、隠密の権限外**——書き込みは侍に一元化
  する運用ルールに従い、本書では記録に留める）。
- Stryker内部のBuildalyzerが、WPFプロジェクトのDesignTimeBuildを扱う際の既知の相性問題である
  可能性が高いと推測する（未確定）。

---

## 4. 過去実績との対比

`docs/ecad2-t046-stryker-survey-onmitsu.md`（2026-07-08）・`docs/ecad2-t050-stryker-review-onmitsu.md`
（2026-07-10）では、**全く同じ実行手順**（`tests/Ecad2.App.Tests`から`dotnet stryker -p
"Ecad2.App.csproj" -c 4 --output <出力先>`）でEcad2.App側のStrykerが正常動作した実績がある
（T-046: 2265 mutants/104秒/19.79%、T-050: 2303 mutants/2分16秒/23.88%）。**今回のみ失敗する**
ことから、プロジェクト構成自体の問題ではなく、直近の環境変化が原因と推測する（原因未特定）。

候補（いずれも未確認の推測）：
- `dotnet --version`で確認した現在のSDKは`10.0.301`。`docs/todo.md`のT-062（.NET 10移行）は
  未着手のまま、ターゲットフレームワークは`net8.0-windows`のまま据え置きという過渡的な状態。
  SDKが自動更新されていた場合、WPFのDesignTimeBuildを担うMSBuildタスクの挙動が変化した
  可能性がある。
- 本セッション内で`dotnet build`を極めて高頻度に実行しており（T-051の4コミット×複数回の
  ビルド・テスト実測）、`obj`フォルダの蓄積状態が過去の棚卸し時と異なっていた可能性がある
  （§2-2で確認した2717個の一時ファイル蓄積が、その状況証拠の一つ）。

---

## 5. 結論・申し送り

Ecad2.App（WPF）側のStryker手動棚卸しは、隠密の権限内での対処（`obj`/`bin`クリーン、再試行）
では解消できなかった。Ecad2.Core側は正常動作を確認済み（mutation score 7.43%、詳細は本書の
本題ではないため割愛、必要なら別途報告する）。

対応方針の選択肢（家老の裁定を仰いだ）：
- A. `Ecad2.App.csproj`へ`EnableDefaultPageItems=false`等の回避策を侍に依頼し、Stryker実行を
  再試行する（恒久対応、ただしcsproj変更の影響範囲を事前に検証する必要がある）。
- B. 今回のT-051クローズではEcad2.App側のStryker棚卸しを見送り、通常のコードレビュー
  （`docs/ecad2-t051-round2〜4-*.md`、code-reviewスキル併用済み）で代替する。
- C. さらに時間をかけて回避策を調査する（Stryker設定ファイル、`--build-plugins`オプション等）。

---

## 6. 家老裁定（2026-07-11）

**T-062（.NET 10移行）完了後へ延期。**

家老仮説：本日殿が導入した.NET 10 SDK（10.0.301）が原因。`global.json`が存在しないため、
Buildalyzer（Stryker内部が使うMSBuildプロジェクト解析ライブラリ）が最新SDK（10.0.301）で
DesignTimeBuildを走らせるようになった。T-050（2026-07-10）までは正常動作し、今日（2026-07-11）
だけ壊れた時期が完全に符合する。§4で隠密が挙げた「SDK自動更新」仮説と一致する。

T-062でTargetFramework=net10へ移行すればSDK経路（プロジェクトのターゲットフレームワークと
実行SDKバージョンの不一致）が正式化され、解消する見込み。**移行後に再試行し、T-051領域の
棚卸しをそこで実施する。** それでも失敗する場合は、`global.json`によるSDKバージョン固定、
または本書§3で触れた`csproj`側の回避策（`EnableDefaultPageItems=false`等）を侍へ依頼する。

本件はT-051クローズの阻害要因とはしない（通常観点のコードレビューで代替、
`docs/ecad2-t051-round2〜4-*.md`参照）。T-051は忍者実機確認へ回された。

---

## 出典
- `docs/ecad2-t046-stryker-survey-onmitsu.md`（過去の正常動作実績、実行手順の出典）
- `docs/ecad2-t050-stryker-review-onmitsu.md`（同上、往復2周超えクローズ時の制度の出典）
- `src/Ecad2.App/Ecad2.App.csproj`（構成確認、`IntermediateOutputPath`等のカスタマイズ無し）
- Web検索結果（Stryker.NET GitHub Issue #2083、NETSDK1022関連の一般情報）
