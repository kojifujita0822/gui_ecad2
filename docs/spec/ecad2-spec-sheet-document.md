# ecad2 仕様書：シート/ドキュメント管理

T-075（殿裁定、2026-07-11起票）に基づく体系的仕様書の第1号。実装コード・殿裁定記録
（`docs/todo.md`/`docs/todo-archive.md`）・忍者実機検証記録（`docs-notes/`配下）を突き合わせ、
「仕様として確定している挙動」を出典付きで明文化する。**事実（コード実装）と裁定根拠（殿裁定の
経緯）を区別して記載する。**

---

## 1. シートの初期状態

### 確定仕様

| 状態 | シート数 | 備考 |
|---|---|---|
| アプリ起動直後 | **0件** | `HasProject=false`、濃紺（#24325A）の空状態表示 |
| 「新規」コマンド実行後 | **即1件** | `シート1`、行数10・列数20、制御回路 |

**起動直後とNewDocument()後で仕様が異なる2段階構成**であり、仕様書上もこの区別を明記する。

### 出典（事実）

- `LadderDocument.Sheets`は`= new()`で空リスト初期化（`src/Ecad2.Core/Model/Document.cs:13`）。
- `MainWindowViewModel.Document`はコンストラクタで`= new()`のまま、シート追加処理を経ずに
  Sheets=0件で起動（`MainWindowViewModel.cs:82-91`、コメント「起動直後は空(Sheets=0)、
  HasProject=false=濃紺スタート、殿裁定2026-07-05」）。
- `NewDocument()`は即座に1件のデフォルトシート（`PageNumber=1`, `Name="シート1"`,
  `Grid={Rows=10,Columns=20}`）を生成したドキュメントへ差し替える（`MainWindowViewModel.cs:1582-1595`、
  コメント「殿裁定2026-07-05、GX Works3流儀」）。
- `HasProject => Document.Sheets.Count > 0`（`MainWindowViewModel.cs:191`）。

### 0件時のUI挙動

- キャンバス：`ScrollViewer.Style`の`DataTrigger`が`HasProject=False`時に`EmptyStateBackgroundBrush`
  （濃紺）へ切替（`MainWindow.xaml:414-427`）。
- ツールバー：保存系メニュー・ボタンは`IsEnabled="{Binding HasProject}"`で無効化
  （`MainWindow.xaml:115-116`ほか多数）。「新規」「開く」は常時有効。
- 忍者実機確認（`docs-notes/ecad2-t019-zoku-verification-ninja.md:10-13`）：ツールバー8ボタン全て
  グレーアウト、F5〜F8等のキーでも「シートがありません。新規作成（Ctrl+N）から始めてください」と
  案内表示される。`sheets:[]`の手作りファイル読込でも同じ濃紺ガードが再現。

### 裁定根拠

- T-019（Done, 2026-07-05）：新規作成の初期状態＝即1シート生成（「Sheets=0暫定から変更」）、
  起動直後＝濃紺スタート（ダミー文書生成を廃止）（`docs/todo-archive.md:108`）。
- T-020（Done, 2026-07-03）：空状態⇔作業領域の動的切替をGX Works3風に新規導入。GuiEcadには
  この概念自体が存在しないと隠密が確認済み（`docs/todo-archive.md:109`）。

### 本日T-062検証での確認（実機記録）

`docs-notes/ecad2-t062-main-operations-regression-ninja.md:34-47`：起動直後にa接点配置ボタンが
Invoke失敗→シート0件と判明→退行かと一瞬疑ったが、T-019記録を根拠に**既存仕様（退行ではない）**と
確定。**本仕様書はこの切り分け難航を解消するために起票されたT-075そのものの直接の動機である。**

---

## 2. シート追加/削除/改名

### 追加

- `AddSheetButton_Click`（`MainWindow.xaml.cs:188-194`）が`AddSheetDialog`を表示、OK時に
  `SheetNavigation.AddCommand.Execute((SheetName, IsMainCircuit))`。
- `AddCommand`（`SheetNavigationViewModel.cs:90-142`）：名前が空なら`シート{連番}`へ自動フォール
  バック、`Grid=new GridSpec{Rows=10,Columns=20}`固定、`MainCircuit=isMainCircuit`。追加前に
  `UndoManager.RecordSnapshot`でスナップショット記録。
- `AddSheetDialog`：名前欄＋種別ラジオボタン2択（既定は制御回路）。名前空欄でも自動採番、
  キャンセルで生成されない（`docs/archive/ecad2-t041-addsheetdialog-ninja-verification.md:11-15`実機確認済み）。

### 削除

**削除UIは存在する**（`DeleteSheetButton`、`MainWindow.xaml:389-390`、
`Command="{Binding SheetNavigation.DeleteCommand}"`、確認ダイアログなし）。

- `CanExecute: () => Sheets.Count > 1`——**最後の1枚は削除不可**（「ドキュメントにシートが0枚の
  状態を作らない」、`SheetNavigationViewModel.cs:145-166`）。
- 削除後は`CurrentSheetIndex`を`Math.Min(index, Sheets.Count-1)`へクランプ。
- 忍者実機確認（`docs-notes/ecad2-t050-realmachine-verification-ninja.md:20-39`）：削除境界
  （先頭/中間/末尾/下限）は全4パターンで規則どおり動作。

### 改名

- `RenameSheetButton_Click`（`MainWindow.xaml.cs:177-184`）→`RenameDialog`→
  `RenameCommand.Execute(dialog.NewName)`。
- `RenameCommand`（`SheetNavigationViewModel.cs:174-200`）：空文字/同名は無変更（`MarkDirty`しない）、
  `Sheets.RemoveAt+Insert`でコンテナ再構築（ListBox表示反映のため）。

### 実装上の注意点（既知の罠、実機検証固有・UI Automation経由の観測時のみ該当）

- UI Automationでの`DeleteSheetButton.IsEnabled`表示は`Count=1`でも`True`と誤観測されることがある
  （`CommandManager.RequerySuggested`再評価タイミング問題と推測）。物理クリックでは実際には
  削除がブロックされる（`docs-notes/ecad2-t050-realmachine-verification-ninja.md:63-71`）。
- モーダルダイアログを開くボタンへ`InvokePattern.Invoke()`を連打すると、モーダル制約を無視して
  同一ダイアログが複数枚重なって開く事故が確認されている
  （`docs-notes/ecad2-t051-realmachine-verification-ninja.md:79-98`）。UI Automation経由の検証
  ツール固有の罠であり、アプリの通常操作（マウス/キーボード）では発生しない。

---

## 3. シート種別（主回路/制御回路）と結線ボタンの有効/無効

### 確定仕様

`Sheet.MainCircuit`（bool、既定`false`＝制御回路、`src/Ecad2.Core/Model/Sheet.cs:25-27`）が唯一の
種別プロパティ。二値のみ（他の種別は存在しない）。

| ボタン | ショートカット | 有効条件 |
|---|---|---|
| 自由線(横線)記入 | F9 | 主回路のみ（`IsMainCircuitSheet`） |
| 自由線(縦線)記入 | Shift+F9 | 主回路のみ（`IsMainCircuitSheet`） |
| 接続点記入 | F10 | 主回路のみ（`IsMainCircuitSheet`） |
| 縦分岐線記入 | Shift+F9 | 制御回路のみ（`IsControlCircuitSheet`） |
| 配線分断記入 | F10 | 制御回路のみ（`IsControlCircuitSheet`） |

同じショートカットキー（Shift+F9、F10）がシート種別によって異なる機能に割り当てられる点に注意
（`MainWindow.xaml`、自由線横316-325行/縦306-315行、接続点336-340行、縦分岐線326-335行、
配線分断346-350行）。`IsMainCircuitSheet`/`IsControlCircuitSheet`は`CurrentSheet`がnull
（未プロジェクト時）では両方`false`（`MainWindowViewModel.cs:164-170`）。

### 裁定根拠

- T-041（2026-07-07）：F9/Shift+F9の対象はシート種別で自動切替。主回路シート作成手段が
  当初無かった問題を受け、殿裁定＝案1（AddSheetDialogに種別選択ラジオボタン新設）
  （`docs/todo-archive.md:140`）。
- T-047（完全Done, 2026-07-09）殿裁定3点：(1)5ボタン固定＋非対応シートはグレーアウト
  (2)意匠はT-040様式 (3)既存7ボタン末尾に区切り線で隣接。`IsMainCircuitSheet`/
  `IsControlCircuitSheet`プロパティを新設（`docs/todo-archive.md:196-211`）。
- T-048（完全Done, 2026-07-10）：シート種別によるグリフ意匠の切替も含め、忍者実機回帰で
  「シート種別での活性/グレーアウト逆転」を確認済み（`docs/todo-archive.md:151-168`）。

### 本日T-062検証での確認（実機記録）

`docs-notes/ecad2-t062-main-operations-regression-ninja.md:78-92`：横配線ボタンInvoke失敗を
「認識できないエラーです」で観測→コード確認で`IsMainCircuitSheet`条件と判明→当日作成の既定シート
（制御回路）で横線ボタンが無効なのは**正しい仕様**と確認。`docs/todo.md:222`にも「既存仕様と確認、
退行でない」と明記。

---

## 4. 新規/開く/保存

### 操作フロー

- **新規**：`NewButton_Click`（`MainWindow.xaml.cs:266-270`）→`ConfirmDiscardIfDirty()`→
  `NewDocument()`（1シート付きドキュメントへ差替、1節参照）。
- **開く**：`OpenButton_Click`（275-295行）→`ConfirmDiscardIfDirty()`→`OpenFileDialog`（`*.gcad`）
  →`LoadFromFile(path)`。例外は一般化したメッセージボックスへ変換。
- **上書き保存**：`SaveButton_Click`→`SaveDocument()`（212-229行）：`HasProject`チェック→デバイス名
  編集確定（`CommitDeviceNameEdit()`）→`CurrentFilePath`があれば上書き、なければ`SaveDocumentAs()`。
- **名前を付けて保存**：`SaveAsMenuItem_Click`（240-245行、T-063）：`HasProject`チェック後、パス
  確定済みでも常に`SaveDocumentAs()`へ（`SaveFileDialog`表示）。
- **ウィンドウクローズ**：`Window_Closing`も`ConfirmDiscardIfDirty()`を経由。

### 未保存確認フロー

保存/破棄/キャンセルの3択（`MessageBox.Show`、`MessageBoxButton.YesNoCancel`）。T-019殿裁定
（2026-07-05）で明示的に「入れる」と決定（`docs/todo-archive.md:108`）。

### 状態プロパティ

| プロパティ | 意味 | 更新タイミング |
|---|---|---|
| `HasProject` | `Sheets.Count > 0` | 派生プロパティ（都度算出） |
| `IsDirty` | 未保存の変更有無 | `MarkDirty()`で明示的にtrue化（Undo機構が無いため変更操作の入口ごとに手動呼び出し）。`ReplaceDocument`完了時・`SaveToFile`成功時にfalseへリセット |
| `CurrentFilePath` | 現在開いている`.gcad`パス | 新規/未保存は`null`。`SaveToFile`成功時・`ReplaceDocument`時に更新 |

### GCADファイル形式

- `GcadSerializer`（`src/Ecad2.Core/Persistence/GcadSerializer.cs`）が`LadderDocument`全体
  （`Sheets`含む）をJSONシリアライズ。
- `Save()`は`SchemaVersion=CurrentSchemaVersion(=1)`を設定して書込。`Load()`はスキーマ版不一致で
  `NotSupportedException`。
- .GCAD互換性はGuiEcad実サンプル3件でのテストで実証済み（T-007、`docs/todo-archive.md:100`）。
- ファイル形式（JSON構造・バージョニング）自体を巡る独立の殿裁定は見当たらない（該当記録なし）。

---

## 5. シート設定（行数拡張、母線名）

### 操作フロー

`SheetSettingsButton_Click`（`MainWindow.xaml.cs:198-205`）→`SheetSettingsDialog(sheet.Grid.Rows,
sheet.Bus.LeftName, sheet.Bus.RightName)`→OK時に`UpdateSheetSettingsCommand.Execute(new
SheetSettings(Rows, LeftName, RightName))`。

### 確定仕様

| 項目 | 範囲・既定値 | 検証 |
|---|---|---|
| 行数 | `GridSpec.MinRows=1`〜`MaxRows=60`（既定22、シート追加時は個別に10で上書き） | `int.TryParse`失敗または範囲外はエラー表示、確定せず |
| 左母線名（LeftName） | 既定`"N24"`、空文字許容 | バリデーションなし（殿裁定、GuiEcad踏襲） |
| 右母線名（RightName） | 既定`"P24"`、空文字許容 | 同上 |

- 行数を縮小する場合、縮小対象行（新Rows〜旧Rows-1）に要素・縦コネクタ・分断・枠・行コメントが
  1つでもあれば拒否し`StatusMessage`にエラー表示（`IsRowOccupied`/`TryRejectOccupiedRow`、
  `MainWindowViewModel.cs:1341-1358`）。
- 成功時は`sheet.Grid.Rows`・`sheet.Bus.LeftName/RightName`を更新し`FinishRowCountChange`
  （`SelectedCell`クランプ・`MarkDirty`・`NotifyCurrentSheetChanged`、1364-1371行）。
- `BusConfig.PowerLabel`はnullableで存在するが、`SheetSettingsDialog`のUIには露出していない
  （用途未調査、**不明点**）。

### 裁定根拠

T-055（完全Done、全増分1〜3、2026-07-11、殿直接指示2026-07-10起票）：
- 母線番号入力の正体＝母線名（`LeftName`/`RightName`の文字列手動編集、数値の母線番号はGuiEcadに
  不在と判明）。
- 行追加UIはGuiEcad全系統踏襲（ツールバー行±・Ctrl+Shift+Up/Down・右クリック任意位置挿入・
  シート設定ダイアログ数値入力）。既定行数=10・上限=60で統一（`docs/todo.md:393-399`）。
- 要素の存在する行の削除は拒否（警告、増分1・3共通）。任意位置削除（`DeleteRowAtCommand`）のみ
  「要素ごと削除」（GuiEcad同型）を採用、増分1・2（末尾行の追加/削除）は遡及修正しない
  （`docs/todo.md:403-408, 440-447`）。
- Bus名の空文字入力は許容（増分2着手前裁定、`docs/todo.md:494-497`）。

---

## Undo/Redoとの関係（相互参照）

シート追加・削除のみがUndo/Redo対象（MVP、T-051）。改名・行数変更・母線名変更はUndo対象外。
詳細は次弾の仕様書`docs/ecad2-spec-undo-redo.md`（未作成、第1弾3件目）で扱う。

## 不明点

- `BusConfig.PowerLabel`の実際の用途（コード上存在するが未使用箇所を含め詳細調査は未実施）。
- Undo対象外（改名・行数変更等）の網羅的な検証は次弾のUndo/Redo仕様書で行う。
