# T-041 主回路シート作成ダイアログ 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット`fa66efd`（`feat(app): T-041 主回路シート作成ダイアログ
> (殿裁定「案1」)`）。家老指定観点(1)〜(5)＋`code-review`スキル（line-by-line/removed-behavior/
> cross-file/Conventions統合角度、1エージェント）併用。

---

## 結論：**クリーン。忍者実機確認へ回してよい。ただし1点、ダイアログの高さ不足による見切れの
可能性があり、実機での見た目確認を推奨する**

家老指定観点(1)〜(5)いずれも問題なし。`code-review`併用で検出した唯一の要確認事項は
`AddSheetDialog`のウィンドウ高さがコンテンツ量に対しやや不足気味である点（推測、実機確認で
数分で白黒つく）。新規テスト2件を含む`SheetNavigationViewModelTests.cs`を実際に`dotnet test`で
実行し合格を実測した。

---

## 対象差分

`git show fa66efd`で確認。`MainWindow.xaml`+1/-1、`MainWindow.xaml.cs`+10、
`SheetNavigationViewModel.cs`+11/-2、新規`AddSheetDialog.xaml`(+20)・`AddSheetDialog.xaml.cs`
(+30)、`SheetNavigationViewModelTests.cs`+29/-1（新規2件、既存1件を新規約へ追従）。

---

## 家老指定観点の検証

### (1) 名前なしでも自動採番名にフォールバックし失敗しない挙動 —— **維持を確認**

`SheetNavigationViewModel.cs`の`AddCommand`ハンドラ：`string name = rawName.Trim(); if (name.Length
== 0) name = $"シート{pageNumber}";`——ダイアログの`NameBox`を全消去（空文字）・空白のみのいずれ
でも`Trim()`後に空となり正しくフォールバックする。ダイアログ側のデフォルト表示（
`AddSheetButton_Click`が`$"シート{pageNumber}"`を初期テキストとして渡す）とViewModel側で独立に
`pageNumber`を再計算する箇所が2つ存在するが、`ShowDialog()`はモーダルのため間に他のシート追加が
挟まる余地がなく、両者は常に一致する（`code-review`のline-by-line角度でも境界条件として検討した
が、現時点で不整合を起こす経路は無いと判断した）。

**実測検証**：`AddCommand_WithBlankName_FallsBackToAutoNumberedName`テストを含む新規2件を
`dotnet test --filter FullyQualifiedName~SheetNavigationViewModelTests`で実行し、**7件全て合格**
を確認した（実行時間37ms）。

### (2) ダイアログのモーダル設計とRenameDialogパターンとの整合 —— **完全に同型、問題なし**

`AddSheetDialog.xaml.cs`は`RenameDialog.xaml.cs`とフィールド構造・`Loaded`時の
`Focus()`+`SelectAll()`・`OkButton_Click`でのプロパティ確定+`DialogResult=true`という流れが
一字一句同じパターンで実装されている。`MainWindow.xaml.cs`の`AddSheetButton_Click`も
`RenameSheetButton_Click`と同型（`dialog.ShowDialog() == true`のガードでキャンセル
時（`IsCancel="True"`ボタン・Alt+F4等いずれもDialogResultがtrueにならない）は
`AddCommand.Execute`が呼ばれないことを確認した。design-brief 4節#4「非ネスト方針、単一階層のみ」
にも合致する（`MainWindow`から直接1階層のみで開かれ、他のモーダルの中から呼ばれる入れ子構造は
無い）。

### (3) AddCommand呼び出し規約変更の影響範囲 —— **波及漏れなし**

`grep`でリポジトリ全体を確認し、`AddCommand`の呼び出し（`.Execute(...)`）は新設の
`MainWindow.xaml.cs:108`の1箇所のみであることを確認した。旧`Command="{Binding
SheetNavigation.AddCommand}"`のXAMLバインディングは完全に削除され、`CommandParameter`等の
古い呼び出し経路も残っていない。テスト（`SheetNavigationViewModelTests.cs`）も新しいタプル
規約（`("シート2", false)`等）へ正しく追従している。

**副次確認（`code-review`のremoved-behavior角度）**：削除された旧`Command=`バインディングが
持っていた暗黙の挙動（`ICommand.CanExecute`によるボタンの`IsEnabled`自動連動）は、
`AddCommand`のコンストラクタが変更前後を通じて`canExecute`パラメータを一度も渡していない
（`RelayCommand.CanExecute`は`_canExecute?.Invoke(parameter) ?? true`で常に`true`）ため、
**そもそも失われる挙動が存在しなかった**ことを確認した（対比：`DeleteCommand`は
`() => Sheets.Count > 1`という実際のCanExecuteを持つが、今回変更されていないため無関係）。

### (4) MainCircuitフラグが正しくSheetへ反映されるか —— **確認済み**

`AddSheetDialog.OkButton_Click`で`IsMainCircuit = MainCircuitRadio.IsChecked == true;`を確定し、
`AddCommand`ハンドラで`MainCircuit = isMainCircuit,`としてSheetコンストラクタへそのまま渡す。
`ControlCircuitRadio`が`IsChecked="True"`（既定選択）、`MainCircuitRadio`が同一`GroupName=
"SheetType"`で排他選択されるため、ユーザーが何も操作しなければ`false`（制御回路）になる。

**実測検証**：`AddCommand_WithMainCircuitTrue_CreatesMainCircuitSheet`テストで
`addedSheet.MainCircuit`が`true`になることを確認済み（合格）。

**`code-review`のline-by-line角度で確認した懸念（実害なしと判定）**：`RadioButton`の
`GroupName="SheetType"`のスコープは、WPFではNameScope単位（既定でWindowルート）で判定される
ため、`AddSheetDialog`は独立したWindowインスタンスとして自身のNameScopeを持ち、`MainWindow`や
他のダイアログ内に同名`GroupName`が存在しても衝突しない。リポジトリ全体grepでも他に
`"SheetType"`という`GroupName`は存在しないことを確認した。

### (5) 便乗変更なし —— **確認済み**

---

## `code-review`スキル併用の追加所見

### 所見F（推測、要実機確認）: `AddSheetDialog`のウィンドウ高さがコンテンツ量に対しやや不足気味

`AddSheetDialog.xaml`（`Height="210"`、`ResizeMode="NoResize"`、`SizeToContent`未指定）は、
`RenameDialog`（`Height="140"`、見出し+TextBox+ボタン列のみ）に対し「種別:」見出し＋
RadioButton×2を追加したにもかかわらずHeightの増分は+70pxのみ。手計算でおおよそのコンテンツ
高（マージン・各行の既定フォントサイズから概算）を積み上げると、ウィンドウの非クライアント領域
（タイトルバー分）を差し引いた実表示可能領域に対し、OK/キャンセルボタン列が下端でわずかに
見切れる可能性がある。`ResizeMode="NoResize"`のためユーザー側でのリサイズ回避もできない。
断定はできず（フォントメトリクスの実測やDPI設定次第で変わりうる）、忍者の実機起動確認で
数分あれば白黒つく事項のため、確認項目として申し送る。

### 所見G（推測、severity低）: UIA経由の忍者検証手順への影響（新規の注意喚起）

`AddSheetButton`のCommand→Click変更自体はAutomationPeer（`ButtonAutomationPeer`、
`InvokePattern`対応）に変化を与えない。ただし、今後「シート追加ボタンをUIA経由でInvokeすれば
即座にシートが追加される」という前提で書かれる忍者の検証手順・スクリプトがあれば、T-041以降は
モーダルダイアログ（名前入力・種別選択・OKボタンInvoke）の追加ステップが必須になる点は要周知。
現時点で既存のUIAスクリプト・`ecad2-ui-automation`スキル内にこのボタンを直接参照する記述は
見当たらず、実害は未確認。

### 所見H（推測、severity低）: キャンセル時の非実行パス自体はコードビハインドのため単体テスト未到達

新規テスト2件は`AddCommand.Execute`への入力（正常系・空名フォールバック）のみを検証しており、
`AddSheetButton_Click`内の`dialog.ShowDialog() == true`ガード自体（View側コードビハインドの
ロジック）は既存のテスト体制（ViewModelレベル）では検証手段が無い。P-016（Dispatcher依存の
テスト容易性）と同種の構造的制約であり、今回新たに生じた問題ではない。忍者の実機確認で
「キャンセルボタンを押すとシートが追加されないこと」を一項目として確認いただければ十分と考える。

---

## 忍者への申し送り

- 所見F（ダイアログの高さ不足の可能性）：ダイアログを実際に開き、OK/キャンセルボタンが見切れて
  いないか確認されたい。
- 所見H：キャンセルボタン押下でシートが追加されないことを確認されたい。
- 既存観点（案1裁定の趣旨通り、主回路シートが実際に作成でき、`MainCircuit=true`のシートで
  期待通りFreeLine系の描画・挙動になること）も併せて確認されたい。

---

## 出典・参照

- 対象コミット`fa66efd`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`AddCommand`）
- `src/Ecad2.App/MainWindow.xaml.cs`（`AddSheetButton_Click`、`RenameSheetButton_Click`との比較）
- `src/Ecad2.App/Views/AddSheetDialog.xaml`・`.xaml.cs`（新設、`RenameDialog`との比較）
- `src/Ecad2.App/Commands/RelayCommand.cs`（`CanExecute`の既定挙動）
- `tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`（新規2件、実行して合格を実測）
- `docs/ecad2-ui-ux-design-brief.md`（4節#4、非ネスト方針）
- `code-review`スキル（line-by-line/removed-behavior/cross-file/Conventions統合角度、
  1エージェント、推測3件・確認済み5件）
