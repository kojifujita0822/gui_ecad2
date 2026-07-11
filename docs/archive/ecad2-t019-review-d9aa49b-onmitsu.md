# T-019追加実装(コミットd9aa49b)静的レビュー（隠密）

対象: 殿裁定(2026-07-05)3点の実装——未保存確認フロー(IsDirty/MarkDirty)・新規=1シート生成・
HasProject通知修正。`code-review`スキル(medium、8観点finder→1-vote verify)を併用。

## 家老指定5観点への回答

| # | 観点 | 判定 |
|---|------|------|
| 1 | MarkDirty呼び出し6箇所の網羅性 | 網羅済み(要素配置/削除/デバイス名変更・シート追加/削除/改名)。Grepで他の変更操作箇所も洗い出したが漏れなし。 |
| 2 | IsDirtyリセットのタイミング整合 | SaveToFile成功時(例外時はスキップされ正しくtrue維持)・ReplaceDocument時とも位置は正しい。 |
| 3 | 3択「はい=保存」後、名前を付けて保存キャンセル時の遷移中止 | 正しく動作する。`SaveDocument()`後に`!_viewModel.IsDirty`を見る設計で、キャンセル/保存失敗時はIsDirtyがtrueのまま残り遷移中止。 |
| 4 | 新規1シートの内容(PageNumber・グリッド寸法)が既存流儀と一致するか | 一致(`PageNumber=1, Name="シート1", Grid{Rows=10,Columns=20}`)。ただしAddCommand内の同種コードと重複している(下記#4)。 |
| 5 | 通知発火の位置の正しさ | AddCommand/DeleteCommandとも`Sheets.Add/RemoveAt`の**後**にMarkDirty/NotifyHasProjectChangedを呼んでおり正しい。 |

## code-reviewスキル所見（verify後）

| # | 判定 | 内容 |
|---|------|------|
| 1 | **CONFIRMED（最重要）** | ウィンドウを閉じる操作(右上×ボタン・Alt+F4)に`ConfirmDiscardIfDirty()`が組み込まれていない。`Closing`/`OnClosing`ハンドラがMainWindow.xaml/xaml.cs・App.xaml.csのいずれにも存在せず、新規/開くの入口(NewButton_Click/OpenButton_Click)からのみ呼ばれる。未保存の変更(IsDirty=true)がある状態で閉じると確認なしにデータが失われる。GuiEcadのOnMenuRestart(`docs/ecad2-guiecad-code-survey-onmitsu.md` T-024節、確認処理を経由せず即Exit)と同種の「入口分散」再発にあたる。 |
| 2 | **CONFIRMED（構造リスク）** | 「変更操作の入口で明示的にMarkDirty()を呼ぶ」方式は、GuiEcadの「Undo対象外操作へのMarkDirty呼び忘れ」問題と本質的に同型の「新規変更操作追加時の呼び忘れ」リスクを抱える。実際、本コミット自体が「NotifyHasProjectChanged呼び出しが当初漏れ、後の実機検出で発覚・修正」という前例を含んでおり、リスクの実在性を裏付ける。IsDirty/MarkDirtyの単体テストは0件（`tests/Ecad2.Core.Tests/`該当なし、そもそも対象がEcad2.App層でテスト対象外）。 |
| 3 | CONFIRMED（軽微） | `RenameCommand`はシート名を変更前と同じ文字列にリネームしても`MarkDirty()`を無条件で呼ぶ。同ファイル内の`SelectedElementDeviceName`セッターには`oldName==newName`の同値ガードがあるのに非対称。実害は「不要な未保存確認ダイアログが出る」false-positiveのみ。 |
| 4 | PLAUSIBLE（将来リスク） | `NewDocument()`と`SheetNavigationViewModel.AddCommand`のシート生成コード(`PageNumber`/`Name`命名規則/`Grid{Rows=10,Columns=20}`)が重複。現時点で値は完全一致し実害なし。将来デフォルトグリッドサイズ変更時に片方修正漏れで不整合を招くリスクに留まる。 |
| 5 | REFUTED | 「HasProjectのコメントが空状態到達不能という古い記述のまま」という候補は誤り。実際のコメントは「起動直後はSheets=0のためfalse=濃紺スタート(殿裁定2026-07-05)」と現状を正確に記述済み(起動時ダミー3シート生成は既に廃止されている)。 |
| 6 | REFUTED | 「RenameCommandのIndexOf(sheet)が-1になりModel/UI不整合を招く」という候補は誤り。`SelectedSheet`のgetterは常に`Sheets[index]`(コレクション内の参照そのもの)を返し、Sheetは参照型でEquals未オーバーライドのため`IndexOf`は常に成功、到達不能。

## 隠密所見

要修正候補として家老の判断を仰ぐべきは**#1(閉じる操作の未保存確認漏れ)**。データ喪失に直結するため優先度最高。
**#2(テスト0件)**は今すぐの大規模改修は不要(YAGNI、6箇所規模ではObserver化は過剰)だが、最低限の回帰テスト追加は妥当な投資として提案する。
**#3(Renameの無条件MarkDirty)**は軽微につき経過観察でも良い。**#4**は設計メモ。#5・#6は却下。
