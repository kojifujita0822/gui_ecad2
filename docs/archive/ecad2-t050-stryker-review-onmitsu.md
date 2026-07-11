# T-050クローズ前 Stryker.NET手動棚卸し（隠密）

> 2026-07-10 隠密調査。往復2周超え案件(T-050)クローズ時の掟に基づく手動棚卸し。
> 家老采配（対象=T-050全4コミット 3190226+e2f44d7+1d6db37+7838956、変更領域5ファイル
> ＋テスト群）。**本書は事実の列挙のみ。補強要否の判断は家老に委ねる。**

---

## 実行概要

- コマンド: `tests/Ecad2.App.Tests` から `dotnet stryker -p "Ecad2.App.csproj" -c 4 --output <scratchpad>/stryker-t050`
- 対象: `Ecad2.App.csproj` プロジェクト全体（`-m`差分限定オプションは過去実測で挙動不安定のため不使用、
  T-046調査書の推奨どおりファイル単位の事後突合で対応）
- 実行時間: 2分16秒（2303 mutants created、618件が実テスト対象）
- **全体mutation score: 23.88%**（Killed 411 / Survived 207 / Timeout 0 / Errors 0）
- レポート: `<scratchpad>/stryker-t050/reports/mutation-report.html`（JSON抽出データは
  `<scratchpad>/stryker-t050-report.json`、隠密が解析用に生成したスクラッチパッド常駐ファイル）
- 事前確認: Ecad2.App.exe不起動を確認済み（MSB3027対策）、侍のビルド専有なしを確認済み

---

## T-050変更領域5ファイルのmutation score

| ファイル | 総mutants | Killed | Survived | NoCoverage | その他 | score |
|---|---|---|---|---|---|---|
| `Diagnostics/TraceLog.cs` | 65 | 1 | 12 | 42 | Ignored10 | — |
| `ViewModels/DeviceTableViewModel.cs` | 6 | 0 | 4 | 0 | Ignored2 | — |
| `ViewModels/MainWindowViewModel.cs` | 793 | 362 | 141 | 84 | Ignored79/CompileError127 | — |
| `ViewModels/SheetNavigationViewModel.cs` | 79 | 36 | 10 | 8 | Ignored8/CompileError17 | — |
| `ViewModels/ViewModelBase.cs` | 21 | 4 | 7 | 3 | Ignored3/CompileError4 | — |

（`score`列は個別算出せずStryker全体値23.88%を参照。ファイル単位scoreはHTML版レポートに詳細あり）

---

## T-050変更行との突合（3190226+e2f44d7+1d6db37+7838956のdiffと現在行番号を照合）

各ファイルの生存ミュータントを「T-050で実際に変更・追加された行」と「ファイル内その他の
既存コード（T-050範囲外）」に仕分けた。突合はdiff直読+現在ファイルのRead直読による。

### T-050変更行に直接該当する生存ミュータント（3件）

1. **`SheetNavigationViewModel.cs:137`** — `if (index >= 0) _owner.SetCurrentSheetIndexCore(index);`
   （AddCommand内、1d6db37でCurrentSheetIndexセッタ経由からSetCurrentSheetIndexCore直呼びへ変更した行）
   → `[Equality mutation] "index > 0"` が Survived。Sheets.IndexOf(sheet)は直前でSheets.Add(sheet)
   済みのため理論上必ず`>=0`だが、この防御的条件自体をindex==0の境界で崩す変異をテストが検知できていない。

2. **`Diagnostics/TraceLog.cs:64`** — `catch (ArgumentException) { return value; }`
   （e2f44d7で新規追加、指摘1=不対サロゲート例外保護）
   → `[Block removal mutation] "{}"` が Survived。RED証明はEdit一時改変（try/catch自体の除去）で
   実測されたが、Strykerの「catchブロックの中身を空にする」変異は別経路であり、これは現行テストで
   検知できていない。

3. **`ViewModelBase.cs:38`** — `PropertyChangedForTest?.Invoke(propertyName, oldValue);`
   （1引数版OnPropertyChanged内、1d6db37でテストフック層として新規追加）
   → `[Statement mutation] ";"` が Survived。**対になる2引数版OnPropertyChanged内の同フック呼び出し
   （現行L47相当）はKilledで、非対称**。3周目までの新規テスト群はSelectedSheetの2引数版通知経路
   （DeleteCommand/AddCommand/ResetSheets等）をPropertyChangedForTest経由で検証しているが、
   1引数版経由（SetProperty経由の通常プロパティ変更全般）のフック発火自体を検証するテストは無い模様。

### 上記以外の各ファイルの生存ミュータント（T-050変更行に非該当、既存コード領域）

- `TraceLog.cs`: 残り11件（L20/26×4/29の文字列定数・L73×5のLogPropertyChangedガード条件）。
  いずれもT-050の3コミットでは変更されていない既存行（diff非該当をコンテキスト行として確認済み）。
- `DeviceTableViewModel.cs`: 4件全て（コンストラクタブロック・Refresh内の`Devices = BuildList();`・
  Rebind内`Refresh();`・BuildList内のOrderBy式）。T-050で追加されたのはコメント+oldDevices捕捉+
  OnPropertyChanged引数のみで、いずれもKilled側。
- `MainWindowViewModel.cs`: 141件全て。T-050変更箇所（CurrentSheetIndexセッタ全体・
  SetCurrentSheetIndexCoreメソッド全体・ReplaceDocument内のoldSelectedSheet捕捉/RefreshSelectedSheet
  呼び出し）はdiff上の行番号(103-144/1502/1541等)と照合した結果、該当Survivedなし＝全てKilled。
  141件は主にドラッグ操作（コネクタ/自由線/接続点）・DRC関連ロジック等、T-050と無関係な既存コード。
- `SheetNavigationViewModel.cs`: 残り9件（L107 GridSpec初期化・L116/120 AddCommand内の既存通知呼出・
  L157 DeleteCommand内NotifyHasProjectChanged・L176/179/182/183 RenameCommand既存ロジック）。
  いずれもdiff非該当（コンテキスト行）を確認済み。
- `ViewModelBase.cs`: 残り6件（L24/27/29/30のSetProperty既存ガード・L37のTraceLog.LogPropertyChanged
  呼出・L47の2引数版TraceLog.LogPropertyChanged呼出）。いずれもT-039由来の既存行。

---

## 出典・注記

- diff照合: `git show 3190226/e2f44d7/1d6db37/7838956 -- <各ファイル>`の直読、現在ファイルは
  `Read`直読（Grep結果は本タスクでは未使用、handover-samurai-t050-round3.mdの示唆に倣いRead主体で実施）
- JSON抽出: HTMLレポート内に埋め込まれたJS変数（`app.report = {...}`）をNode.jsスクリプトで
  括弧バランス解析し抽出・パース（スクラッチパッドのみに作業ファイルを残置、`src`/`tests`は無変更）
- 全件の生の生存ミュータント一覧（行番号・mutatorName・置換後コード）はHTML版レポートに完全収録
