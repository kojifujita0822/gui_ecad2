# T-041増分5 最終レビュー（隠密、往復3周目）

> 2026-07-08 隠密レビュー。対象コミット`914a2c7`（`fix(app): T-041増分5修正往復3周目 -
> CurrentSheetIndexは無条件へ回帰、所見L真因はRenameCommand側で対処`、殿裁定P-030）。
> 家老指定観点(1)〜(4)を確認。`code-review`スキル（line-by-line/removed-behavior/
> cross-file角度統合、1エージェント）併用。実測検証（`dotnet test`）併用。

---

## 結論：**クリーン。忍者へ増分5全体の実機確認を采配してよい**（ただし所見Lの検証は
単体テスト不可のため、忍者実機確認観点への明記を推奨）

---

## 家老指定観点の検証

### (1) 往復2周目で再発した症状1（削除時の再描画漏れ）が今度こそ解消されているか
—— **解消を確認**

`MainWindowViewModel.cs:100-127`の`CurrentSheetIndex`のsetterが、往復1周目（`4264220`）と
完全に同一のロジック（`SetProperty`戻り値無視→`OnPropertyChanged(nameof(CurrentSheet))`常時
→`SelectedCell = null`常時→`SheetNavigation.RefreshSelectedSheet()`常時、の順序）へ回帰して
いることをコードトレースおよび`code-review`エージェントの独立検証で確認した（コメントのみ
書き替え、実体ロジックは往復1周目と一致）。

新規テスト`DeleteCommand_WhenIndexNumberStaysSameAndSelectedCellAlreadyNull_RaisesCurrentSheetChanged`
（`SheetNavigationViewModelTests.cs:175-200`）は、隠密が往復2周目レビューで指摘したテスト
カバレッジの隙間（削除前に`SelectedCell`が既に`null`のケース）を直接埋めており、
`PropertyChanged`イベントを購読して`CurrentSheet`の発火を直接アサートする形で症状1の再発を
確実に検知できる内容になっている。実測（`dotnet test`）で合格を確認。

### (2) 所見L（改名時ドラフト破棄）が引き続き解消されているか —— **コードトレース上は
解消。ただし単体テストでは検証不可、実機確認が必須**

`SheetNavigationViewModel.RenameCommand`（125-149行目）の遅延コールバックが`SelectedSheet =
sheet`から`RefreshSelectedSheet()`（引数無しメソッド）へ変更され、改名操作が`CurrentSheetIndex`
のsetterを一切経由しなくなったことを確認した。これにより改名は`SelectedCell = null`等の
クロスカットクリアを発火させず、記入中の縦コネクタ・自由線ドラフトは改名によって影響を
受けない。`SelectedSheet`のgetter（25-31行目）は`CurrentSheetIndex`経由で`Sheets[index]`を
返す実装のため、改名前後で`index`が不変（`RemoveAt`+`Insert`で同じ位置に戻す）である以上、
`RefreshSelectedSheet()`のみで左パレットの選択ハイライトは改名後の参照へ正しく追従する。

**検証面の制約**：`code-review`エージェントが指摘したとおり、この経路は
`System.Windows.Application.Current.Dispatcher.BeginInvoke`に依存しており、xUnit単体テスト
環境では`Application.Current`が`null`のため到達前に`NullReferenceException`となる
（既存の`RenameCommand_MarksDirty`テストがこれをtry/catchで握りつぶしている、P-016まで既知の
制約）。**今回、所見Lの真因対処自体を直接検証する単体テストは追加されておらず、追加も原理的に
困難**。忍者の実機確認観点に「記入中の縦コネクタ／自由線ドラフトがある状態でシート改名を
行っても、ドラフトが保持されること」を明記して依頼するのが妥当と考える。

### (3) 新規副作用がないか —— **重大な副作用なし。軽微な指摘2件（対応不要〜任意）**

`code-review`エージェント（1エージェント、line-by-line/removed-behavior/cross-file角度統合）
を併用し、`RenameSheetButton_Click`・`AddCommand`の遅延`SelectedSheet = sheet`・`DeleteCommand`
・`OutputPanelViewModel.JumpTo`との整合性も含め確認した。重大な副作用は確認されなかった。
以下2件はいずれもseverity低、対応必須ではない：

**所見Q（Efficiency、severity低、対応不要）**：`SheetNavigationViewModel.AddCommand`の
`wasEmpty`（Sheets 0→1遷移）経路は、同期的な`NotifyCurrentSheetChanged()`（既存のワーク
アラウンド）に加え、`CurrentSheetIndex`が往復1周目・3周目の「常時無条件」形に戻ったことで、
遅延`SelectedSheet = sheet`（同値0代入）経由でも`OnPropertyChanged(nameof(CurrentSheet))`が
再度発火し、初回シート追加時に`RedrawCanvas()`が2回走る。実害はない（可視の不具合なし、
往復2周目レビューで指摘した同種のEfficiency所見と同根）。

**所見R（ドキュメンテーション、severity低、対応任意）**：
`tests/Ecad2.App.Tests/SelectedConnectorExclusivityTests.cs:51-54`
（`SelectedCellAssignment_ClearsSelectedConnector_EvenWhenCurrentSheetIndexUnchanged`）の
コメントが「クロスカット的クリアはSetPropertyより前に無条件配置...T-041増分5隠密再レビュー
所見M、SelectedCellのsetterと同じ粒度」という**往復2周目時点で撤回された設計**を根拠として
引用したまま残っている。往復3周目で`OnPropertyChanged(nameof(CurrentSheet))`自体も無条件化に
戻ったため、「プロパティ自身の変更通知のみ値変化時限定」という所見Mの粒度分けはもう現行コード
に存在しない。テスト結果自体は（クロスカットクリアが無条件実行されるという結論は変わらず真の
ため）引き続きGREENだが、コメントの論拠が実装と乖離しており、将来の保守者が誤った前提で
調査を始める種になりうる。

### (4) 83件のregression維持 —— **実測で確認**

`dotnet test src/Ecad2.sln`実行、Core14件・App69件（新規1件込み）、計83件合格。侍の報告と
一致。`code-review`エージェントも独立に同結果を再現確認済み。

---

## 総合判断

観点(1)(4)は明確に解消・維持を確認。観点(2)はコードトレース上解消と判断できるが検証面の制約
（Dispatcher依存で単体テスト不可）が残るため、忍者の実機確認でカバーすべき事項として明記を
推奨する。観点(3)の新規副作用はいずれもseverity低で対応不要〜任意。**往復3周目でクリーンと
判定し、忍者へ増分5全体の実機確認を采配してよい**と考える。

---

## 忍者への申し送り事項（推奨、実機確認観点への追加）

- （所見L関連、新規）記入中の縦コネクタ／自由線ドラフトがある状態でシート名変更（改名）を
  行っても、ドラフトが破棄されず保持されること
- 既存申し送り（`docs-notes/handover-next-session.md`）のF9/sF9/F10記入・選択・削除、シート
  削除時のクロスリーク再現確認、回帰スモークは従来どおり

---

## 出典・参照

- 対象コミット`914a2c7`（`git show`で全差分確認）、比較対象`4264220`・`1c23b5d`
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`CurrentSheetIndex`100-127行目）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`RenameCommand`125-149行目、
  `AddCommand`66-100行目）
- `tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`（新規1件、175-200行目）
- `tests/Ecad2.App.Tests/SelectedConnectorExclusivityTests.cs`（51-54行目コメント）
- `docs/archive/ecad2-t041-increment5-review-onmitsu.md`・`-2.md`・`-3.md`（往復1〜2周目の経緯）
- `code-review`スキル（line-by-line/removed-behavior/cross-file角度統合、1エージェント、
  所見3件、うち重大0件）
