# P-040（Dispatcher直接依存の再発防止アーキテクチャテスト）静的レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`511254f`（`test(app): P-040 Dispatcher直接依存の
> 再発防止アーキテクチャテストを新設`、main上、新規テストファイル1件のみ）。`code-review`スキル
> （2角度=検出ロジック正しさ／再利用・簡潔性・効率・CLAUDE.md準拠、2エージェントへ統合し並行
> 実行、1-vote検証を含む）をmedium effortで併用。実測検証（`dotnet test`単体実行・grep横断
> 確認）も併用した。

---

## 結論：**クリーン（機能バグなし）。将来リスクの経過観察事項3件を記録**

対象は`tests/Ecad2.App.Tests/DispatcherDependencyArchitectureTests.cs`（62行、新規）のみで、
既存コードへの変更はない。現時点でテストは正しくGREEN・RED両方を実測でき、家老指定の観点
(1)(2)(3)いずれも問題は見当たらない。ただし検出ロジックの性質上、将来のコード変更で
false positive／false negativeが生じうる構造的な穴が3件あり、記録として残す（いずれも
現時点で実害なし＝家老仕分け対象）。

---

## 家老指定観点の検証結果

### (1) 検出ロジックの正しさ（走査範囲・パターン検出・除外の妥当性）

- 走査範囲：`GetViewModelsDirectory()`（`[CallerFilePath]`経由でテストソースの絶対パスから
  `tests/Ecad2.App.Tests` → `tests` → repo root → `src/Ecad2.App/ViewModels`を解決）を実際の
  ディレクトリ構造と照合し、一致することを確認済み（`find`実行結果、ViewModels配下は
  現状フラット構成10ファイルのみ）。
- 検出パターン：`git grep -n "Application.Current.Dispatcher\|Dispatcher.CurrentDispatcher"
  src/Ecad2.App/ViewModels/`を実行し、現状ヒットは`WpfDispatcherService.cs:11`
  （唯一の正規アダプタ）のみと確認。`AllowedFileNames`による除外は正しく機能している。
- 誤検出・見逃しの**将来リスク**は下記「経過観察事項」参照。

### (2) RED証明報告と実テスト内容の整合

侍報告（両パターンをSheetNavigationViewModel.csへ一時注入しRED実測→復元GREEN、229件全合格）を
以下で裏取りした：
- 現状：`dotnet test --filter FullyQualifiedName~DispatcherDependencyArchitectureTests`を
  単体実行しGREEN（1件合格）を確認。
- 全体：`dotnet test`をCore.Tests／App.Tests双方で実行し、Core14+App215＝**229件全合格**、
  侍報告の件数と一致。
- ロジック整合：本テストは`ForbiddenPatterns`の2文字列いずれかを`content.Contains(pattern)`で
  単純検出する構造であり、両パターンを注入すれば`violations`が非空になり
  `Assert.True(violations.Count == 0, ...)`が確実に失敗する。RED再現は検証者エージェントが
  実測でも確認済み（コメント注入によるfalse positive検証の副産物として、パターン文字列を
  含めれば必ず失敗することを実証。詳細は下記所見1参照）。報告内容と実装は整合している。

### (3) code-reviewスキル併用

2角度（検出ロジック正しさ、再利用・簡潔性・効率・CLAUDE.md準拠）を1-vote検証込みで実施。
結果は下記のとおり。

---

## code-review所見（経過観察、3件）

いずれも**現時点で実害なし**（現在のViewModels配下10ファイルには抵触しない）。将来のコード
変更で顕在化しうる構造的な穴として記録し、要修正／経過観察の仕分けは家老に委ねる。

### 所見1（CONFIRMED）：コメント・XMLドキュメントコメントでのfalse positive

`DispatcherDependencyArchitectureTests.cs:41,44`の`content.Contains(pattern)`はファイル全文
（コード・コメント・XMLドキュメントコメント・文字列リテラルを区別しない）を検索する。

**実測**（検証者エージェントが一時的にSheetNavigationViewModel.csのXMLドキュメントコメントへ
「修正前は誤って`Application.Current.Dispatcher`を直接参照していた」という一文を追記→
`dotnet test`実行→RED実測→即削除・`git status --porcelain`で復元確認済み）：実コードは無変更
にもかかわらずテストが失敗した。

→ 将来、当該フォルダのいずれかのファイルに再発防止コメント等でこの文字列を書くと、実依存が
無くてもテストが落ちる。皮肉にも「なぜこのパターンが禁止か」を説明するコメントを書くこと
自体がテストを壊しうる。

### 所見2（CONFIRMED）：検出パターンの範囲が狭く、他の書き方はすり抜ける

`ForbiddenPatterns`は2つの完全一致文字列のみを検査する。以下はいずれも文字列一致せず検出を
すり抜ける（検証者エージェントが技術的に実在するAPI・書き方であることを確認）：
- `System.Windows.Threading.Dispatcher.FromThread(...)`
- `var app = Application.Current; app.Dispatcher.BeginInvoke(...)`（変数分割記述）
- 改行・空白を挟んだ記述（`Application.Current\n    .Dispatcher`）

所見10・P-016の意図（VM層のWPF Dispatcher型への直接依存を`IDispatcherService`経由に強制する）
に照らすと、本テストは特定2文字列の機械的grepに過ぎず、同じ実害を招く別の書き方は通してしまう。
是正するなら`Roslyn`（`Microsoft.CodeAnalysis.CSharp`）でシンボル解決し
`System.Windows.Threading.Dispatcher`型への参照そのものを検出する方式が本質的だが、
現状のテキストマッチ方式に比べ実装コストは相応に上がる。

### 所見3（PLAUSIBLE）：`AllowedFileNames`除外がファイル名のみでパス階層を無視

`AllowedFileNames.Contains(Path.GetFileName(file))`（36行目）はディレクトリ情報を捨てて
ファイル名のみで除外判定する。将来ViewModels配下にサブディレクトリが新設され、そこに偶然
`WpfDispatcherService.cs`という同名ファイルが置かれた場合、無条件に除外される。

ただし現状（T-009〜T-045、28コミットの履歴確認）ViewModels配下はもちろん`src/Ecad2.App`
配下の兄弟ディレクトリ（Commands/Converters/Diagnostics/Views）も一貫してフラット構造であり、
サブディレクトリ導入自体がこのコードベースの慣行から外れる。発生確率は低い将来リスく＝
PLAUSIBLE。

---

## 再利用・簡潔性・効率・CLAUDE.md観点

指摘なし。同種のディレクトリ走査型アーキテクチャテストは他に存在せず（重複なし）、
`File.ReadAllText`全ファイル走査もホットパスでなくファイル数も少数（10件）ゆえ許容範囲。
CLAUDE.mdの明確な規約違反も見当たらない。

---

## 忍者実機検証について

家老判断どおり、ロジック変更なし（新規テストファイルのみ）につき**実機検証は不要**。

---

## pushについて

レビュークリーン確定。push実施は家老に委ねる。
