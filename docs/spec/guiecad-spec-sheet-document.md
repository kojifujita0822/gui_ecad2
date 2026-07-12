# GuiEcad仕様書：シート/ドキュメント管理

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）のシート/ドキュメント管理実装をExplore委譲調査で纏め、
`docs/spec/ecad2-spec-sheet-document.md`（ecad2側、T-075起票）と比較可能な形で整理する。

対応するecad2側仕様書：`docs/spec/ecad2-spec-sheet-document.md`

---

## 1. シート追加/削除/改名

### 追加（`MainPage.Sheets.cs:118-141`）

ボタン「＋」（`OnAddSheetBtn`）。**ダイアログ表示なし**、クリック即座に新規シート追加。
`Grid={Columns=現在シートのColumns, Rows=8}`、`Bus`は`_document.Settings.DefaultBus`をコピー
（123-128行）。名前は空文字のまま（`Sheet.Name`既定`""`）。シート種別（主回路/制御回路）を
**追加時に選択する手段はない**（既定`MainCircuit=false`のまま、種別変更は追加後に別途シート
設定ダイアログで行う）。

### 削除（`MainPage.Sheets.cs:143-167`）

ボタン「－」（`OnDeleteSheetBtn`）。**確認ダイアログなし**。最後の1枚は削除不可
（`if (_document.Sheets.Count <= 1) return;`、145行）——ただし**ボタン自体は常時押下可能で
グレーアウトしない**、クリック時にno-opで戻るだけ（ecad2は`CanExecute`でボタン自体を無効化）。
削除に伴い`_history.RemoveCommandsForSheet(sheet)`でUndo履歴からも該当シート分のコマンドを除去
（152行、`guiecad-spec-undo-redo.md`1節参照）。**ただしシート削除操作自体はUndoスタックに積まれない**。

### 改名（`MainPage.Sheets.cs:169-191`）

ボタン「名前」、またはシートツリーのダブルクリック（`OnNavTreeDoubleTapped`）でも同じダイアログが
開く。`ContentDialog`＋`TextBox`（初期値=現在名、フォーカス+全選択）。OK時は無条件に
`_sheet.Name = box.Text.Trim(); RebuildNavTree(); MarkDirty();`（185-190行）——**空文字/同名でも
`MarkDirty()`が呼ばれる**（ecad2は`RenameCommand`で無変化時は`MarkDirty`しない、差分あり）。

---

## 2. シート種別の区分

`Sheet.MainCircuit`（bool、既定`false`、`Sheet.cs:25-27`）がecad2と同名・同型の唯一の種別
プロパティ。**設定手段はシート設定ダイアログのチェックボックスのみ**（追加ダイアログ自体が
存在しないため、追加時点での種別指定は不可）。

意味論：`MainCircuit=true`のシートは左右母線・自動横配線の描画をスキップし、自由直線・接続点で
手動結線する運用（`DiagramRenderer.cs:73-101,187-193`）——**この構図はecad2にそのまま継承されている**
（`guiecad-spec-wiring.md`5節で詳述）。

**重要な差分**：ツールボタンの活性/非活性はシート種別と連動しない。自由直線・接続点等の
`RadioButton`（`MainPage.xaml:407-420`）に`MainCircuit`絡みの`IsEnabled`バインディングは存在せず、
主回路・制御回路どちらのシートでも4種の記入ツールが常時使用可能（`guiecad-spec-wiring.md`1節参照）。
ecad2のF9/Shift+F9/F10のようなシート種別依存のショートカット・活性切替はGuiEcadに存在しない。

---

## 3. 新規/開く/保存の操作フロー

| 操作 | GuiEcad |
|---|---|
| 新規 | `OnMenuNew`→`ConfirmDiscardIfDirtyAsync()`→`new LadderDocument()`+`CreateEmptySheet()`1枚（`MainPage.Menu.cs:74-80`） |
| 開く | `OnMenuOpen`→確認→`FileOpenPicker`(`.gcad`)→`LoadFileAsync`（`MainPage.Menu.cs:82-125`） |
| 上書き保存 | `OnMenuSave`→`SaveCurrentAsync()`：パスありなら`GcadSerializer.Save`、無ければ`SaveAsAsync()`（148,153-168行） |
| 名前を付けて保存 | `OnMenuSaveAs`→`SaveAsAsync()`：`FileSavePicker`（既定名=`Info.Title`または`"diagram"`）（150,171-192行） |
| ウィンドウクローズ | `OnAppWindowClosing`→`ConfirmDiscardIfDirtyAsync()`（`MainWindow.xaml.cs:33-45`） |

未保存確認：`ContentDialog`、「保存」(Primary)／「破棄」(Secondary)／「キャンセル」(Close)の3択
（`MainPage.Menu.cs:196-215`）——ecad2の`MessageBox.Show(...YesNoCancel)`と選択肢の意味は同等。

GCADファイル形式：`GcadSerializer`が`SchemaVersion`（`CurrentSchemaVersion=1`）を用い、不一致時は
例外（`GcadSerializer.cs:10,15,33-35`）——ecad2の記述（バージョン=1、不一致で例外）と一致。

---

## 4. シート設定の入力範囲・バリデーション

`OnSheetSettings`（`MainPage.Dialogs.cs:160-246`）：

| 項目 | GuiEcadの範囲・挙動 |
|---|---|
| シート名 | プレースホルダ「省略時: シート N」、トリムのみ、空文字許容 |
| 左/右母線名 | 空でなければ上書き（空入力では既定を維持） |
| 電圧（母線間・任意） | プレースホルダ「AC200V」、空なら`null` |
| 「既定母線名にする」チェック | ONで`Settings.DefaultBus`を更新（次回シート追加の既定へ反映） |
| 主回路チェック | `Sheet.MainCircuit`をトグル |
| 列数 | `NumberBox` Min=2, Max=20。範囲外は自動`Math.Clamp`補正、配置済み要素の最右端を下回らないよう自動拡大（拒否ではない） |
| 行数 | `NumberBox` Min=1, Max=60。同様に自動クランプ＋自動拡大 |

**ecad2との明確な違い**：ecad2は行数縮小時に要素が存在すれば**拒否・エラー表示**（確定させない）
だが、GuiEcadは要素の最大占有位置まで**サイレントに自動クランプ**し直す（エラー表示なし、確定は
必ず成功）。また**GuiEcadはシート設定ダイアログで列数も編集可能**（2〜20）だが、ecad2の
`SheetSettingsDialog`は行数・左右母線名の3項目のみで列数編集項目を持たない。

シート設定変更（名前・主回路フラグ・母線名・行数列数）は**Undo対象外**、確定時に明示的に
`MarkDirty()`（243-244行）——ecad2の「行数変更はUndo対象外」という記述と一致する。

**`BusConfig.PowerLabel`の用途判明**：ecad2側仕様書が「不明点」としていたこの項目は、GuiEcad側の
実装（シート設定ダイアログの「電圧（母線間・任意）」欄、`Sheet.cs:42`）から**母線間電圧ラベル**
であると判明した（ecad2側でUI露出するかは別途殿裁定事項）。

---

## 5. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | 出典 | 備考 |
|---|---|---|
| オートセーブ（既定5分間隔、`.autosave.GCAD`、復元確認フロー） | `MainPage.Autosave.cs:9-56`、`MainPage.Menu.cs:98-125` | ecad2側に記載なし |
| テンプレートからの新規作成（ビルトイン2種＋ユーザー保存） | `MainPage.Templates.cs:11-66` | 「動力+制御図面」は主回路シート(母線R/S/T事前配置)+制御回路シートの2枚構成 |
| Undoが要素配置・移動・削除・行挿入削除等を広くカバー | `Commands/ElementCommands.cs:267-470` | ecad2はシート追加削除のみ（Undo対象が事実上逆転、6節参照） |
| `IsDirty`のUndoDepth連動方式（Undo対象外変更は`-1`センチネルで強制ダーティ化） | `MainPage.xaml.cs:473-482` | ecad2は手動`MarkDirty()`直呼びのみ（Undo機構自体がMVP限定のため） |
| シート名ダブルクリックで改名ダイアログ直起動 | `MainPage.Sheets.cs:169-170` | ecad2に対応なし |
| BOM（部品表）エディタ（全シート横断） | `MainPage.Dialogs.cs:250-` | ecad2はT-066でBOM編集（型式のみ）を別途実装済み |
| シート設定ダイアログでの列数編集（2〜20） | `MainPage.Dialogs.cs:166-172` | ecad2の`SheetSettingsDialog`に列数編集項目なし |
| シートツリーのドラッグ並び替え | `MainPage.xaml:464-469`（`NavTree`定義） | 殿実機確認（2026-07-12）で「ドラッグでシート順が動く」ことを確認済み。ただしコード上`CanDragItems`/`CanReorderItems`/`DragItemsCompleted`等の明示設定・ハンドラは一切存在しない（本調査でも`MainPage.xaml`・`MainPage.Sheets.cs`双方をgrepし該当なしと確認）——WinUI3 `TreeView`の既定挙動のみで並び替え操作自体は動いている可能性が高い。**しかしモデル同期処理（`DragItemsCompleted`等）が見当たらず、`RebuildNavTree()`が呼ばれる操作（追加/削除/改名/シート設定確定等）のたびに`Document.Sheets`の実際の順序で再構築される**ため、ドラッグ後に画面上で並びが変わって見えても保存・再表示で元の`Document.Sheets`順に戻る「見た目のみ」の疑いがある（静的読解による推測、実機での保存→再読込往復までは未検証）。ecad2側は**T-082としてモデル同期込みの並び替えを新規実装予定**（本調査時点では未着手） |

### (2) ecad2のみにある機能

| 機能 | ecad2側出典 | GuiEcad側の状況 |
|---|---|---|
| シート追加ダイアログ（名前欄＋種別ラジオボタン、キャンセル可） | `ecad2-spec-sheet-document.md`2節 | ダイアログなし、即時追加（種別は追加後に別途設定） |
| 起動直後0シートの「濃紺の空状態」（`HasProject=false`） | 同上 | GuiEcadは起動直後から常時1シート存在、0シート状態という概念自体がない |
| シート削除の`CanExecute`によるボタングレーアウト | 同上 | GuiEcadはボタン常時押下可能、クリック時no-opで防止 |
| シート種別によるショートカット動的付け替え（F9/Shift+F9/F10） | `ecad2-spec-wiring.md` | GuiEcadのツールボタンはシート種別と無関係に常時活性 |
| 行数縮小時の占有行チェックと拒否（エラー表示） | `ecad2-spec-sheet-document.md`5節 | GuiEcadは自動クランプで拒否しない |

### (3) 両方にあるが挙動が異なる点

| 項目 | ecad2 | GuiEcad |
|---|---|---|
| 起動直後のシート数 | 0件（濃紺空状態） | 1件（`CreateEmptySheet()`、Columns=8,Rows=8,LeftName="R200",RightName="S200"） |
| シート追加のUI | ダイアログ表示 | ダイアログなし即時追加 |
| 改名時のダーティ化条件 | 空文字/同名なら`MarkDirty`しない | 常に`MarkDirty()` |
| Undo/Redoの対象範囲 | シート追加/削除のみ | 要素配置/移動/削除・行操作等（**シート追加/削除/改名/シート設定はUndo対象外**——ecad2とほぼ逆転） |
| `IsDirty`実装方式 | 手動`MarkDirty()`ブールフラグ相当 | Undoスタック深さ比較＋Undo対象外変更は`-1`センチネル |
| 行数縮小時のバリデーション | 拒否・エラー表示 | 自動クランプ・エラー表示なし |

---

## 出典

- GuiEcad: `MainPage.Sheets.cs:118-191`、`MainPage.Dialogs.cs:160-246`、`MainPage.Menu.cs:74-215`、
  `MainPage.Autosave.cs:9-56`、`MainPage.Templates.cs:11-66`、`MainPage.xaml.cs:473-482`、
  `MainWindow.xaml.cs:33-45`、`GuiEcad.Core/Model/Sheet.cs:8,25-27,42`、
  `GuiEcad.Core/Persistence/GcadSerializer.cs:10,15,33-35`、`Commands/ElementCommands.cs:267-470`
  （Explore委譲調査、行番号は本文各所参照）
- ecad2: `docs/spec/ecad2-spec-sheet-document.md`（比較対象）

## 不明点

- GuiEcad側の列数下限2・上限20という数値の設計意図（コメント等の説明が見当たらず）。
- 主回路シートで自由直線・接続点ツールが常時活性である一方、ツールチップの用途ヒント以外に
  実運用上の利用制限があるか（コード上の制約からは判断できず）。
- ecad2側の「列数はシート追加時に20固定」という記述（比較対象spec由来）はecad2ソース自体の
  再確認が必要（本調査はGuiEcad側実物照合が主眼のため未実施）。
- 本調査はソースコード読解のみで、GuiEcad実機起動での操作再現は行っていない。
