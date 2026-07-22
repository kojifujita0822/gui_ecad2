# T-110 増分2 緊急調査書（隠密）：合成ドラッグ後のシート内容消失・機器表空欄・Undo不可

調査日: 2026-07-22　調査担当: 隠密　委任元: 家老（最優先指定、実ユーザーデータ消失に繋がりうる事象）
事象: 「配置ツール」タブ中心（相対110,166）から真下300pxのUIA合成ドラッグ（steps=40/delay=40ms、終点=シートパネル領域）直後、シート内容（配置済み要素・コメント）全消失・機器表空欄化・「元に戻す」Disabled。フロート化・タブ切替は不成立。フロートウィンドウは現存しない（忍者EnumWindows確認済み）。

---

## 0. 結論（中間、静的調査の到達点）

1. **症状3点はReplaceDocument実行後の状態と完全に一致する**（§1）。UndoManager.Clear()の呼出はReplaceDocument内の1箇所のみで、「Undo Disabled」＋「シート内容空」＋「機器表空」を単一の操作で説明できるのは本経路だけ。
2. **しかしReplaceDocumentへマウスDown-Move-Up列だけで到達する経路は静的に存在しない**（§2）。全トリガー（メニュー/ツールバーボタン/Ctrl+N・O）はボタン上のDown+Upまたはキーボードを要する。忍者の合成ドラッグ実装も純マウス合成（keybd_event不使用）と確認済み。
3. **副産物として実在バグを1件検出**: SheetNavListのD&D並び替え用状態`_sheetDragSource`に**MouseUpでのクリアが無く残留し続ける**（§3）。合成ドラッグがSheetNavList上を通過すると`DoDragDrop`が誤発火する経路が現実に存在する。ただしその到達点（MoveSheetCommand）は表示中シートの実体を変えず、Undo対象外のため**本事象の症状を説明しない**（別件の要修正として報告）。
4. 増分1（統合トポロジ）固有の影響は認められない（§4）。
5. 仮説の判別マトリクスと忍者への追加確認事項（保全状態のまま読み取りのみ）を§5に示す。**この確認で原因が一意に絞れる見込み**。

---

## 1. 症状とReplaceDocumentの一致（一次ソース）

`ReplaceDocument`（`MainWindowViewModel.cs:2950-3062`、新規/開く共通の単一ゲートウェイ）は以下を実行する:

| ReplaceDocumentの処理 | 忍者観測との対応 |
|---|---|
| `SheetNavigation.ResetSheets()`＋Document差し替え | 新Documentが空/1シートなら「シート内容全消失」に見える |
| `DeviceTable.Rebind(newDocument.Devices)` | 新DocumentのDevicesが空なら「機器表空欄化」 |
| `UndoManager.Clear()`（3059行。**Clear呼出は全コードでここ1箇所のみ**、`UndoManager.cs:47-51`） | 「元に戻すDisabled」（`CanUndo => _undoStack.Count > 0`） |
| `Tool = ToolState.SelectDefault`・`IsDirty = false` | （未観測、§5確認事項） |

呼出元は2つのみ:
- `LoadFromFile(path)`（2926-2930行、「開く」）——ファイルダイアログを伴う。ダイアログは観測されていないため可能性低。
- `NewDocument()`（2935-2945行、「新規」）——「シート1」1枚・要素ゼロ・Devicesゼロの新規文書へ差し替え。**発火していれば観測と完全一致**。

Undo実行（`ApplyUndoRedoSnapshot`経路、ReplaceDocumentとは別）も「配置済み要素の消失+スタック枯渇でUndo Disabled」を説明しうるが、実行後はRedoスタックに積まれ**「やり直し」がEnabledになるはず**（§5の判別点）。

## 2. ReplaceDocumentへの到達経路（全列挙、静的にはマウス列から到達不能）

| トリガー | 定義 | マウスDown-Move-Up列からの発火可否 |
|---|---|---|
| メニュー「ファイル>新規/開く」 | `MainWindow.xaml:677-678` | 不可（メニュー展開操作が必要） |
| ツールバー「新規作成/開く」ボタン | `MainWindow.xaml:788-789`ほか（**基本機能タブ内**） | 不可（Click発火にはボタン上でのDown+Upが必要。今回のDownはタブ上・Upはシートパネル上） |
| Ctrl+N / Ctrl+O | `MainWindow.xaml.cs:2614-2627`（KeyDownハンドラ） | 不可（キーボード専用） |
| （参考）Undo: Ctrl+Z/メニュー/ボタン | 2635-2644行ほか | 同上、マウス列からは不可 |

- `NewButton_Click`は`ConfirmDiscardIfDirty()`ガード付き（1125-1129行）——IsDirty=trueなら確認ダイアログが出る。ダイアログが観測されていない以上、発火していたとすれば**IsDirty=falseの状態だった**ことになる（矛盾はしない。検証セッションの操作履歴次第）。
- 忍者のドラッグ実装`Invoke-Ecad2Drag`→`Ecad2Native.DragDrop`（`helpers.ps1:86-99`）は`SetCursorPos`+`mouse_event(LEFTDOWN/LEFTUP)`のみで**keybd_eventを一切呼ばない**（キーボード経路の混入なし）。
- 残る外部要因: 殿環境の常駐マウスツール（`memory: env_mouseassistant_click_conflict`、合成クリックと競合する既知の実績）が合成移動へ反応して追加入力を注入した可能性は**静的には排除できない**（§5-6）。

## 3. 【実在バグ検出・別件】SheetNavListのD&D状態残留と誤発火経路

`MainWindow.xaml.cs:3744-3770`（T-082のシートD&D並り替え）:

1. `SheetNavList_PreviewMouseLeftButtonDown`が`_sheetDragSource`（掴んだシート）と`_sheetDragStartPoint`を記録する。
2. クリア経路は`SheetNavList_PreviewMouseMove`内のD&D開始時（3762-3763行）**のみ**。XAML購読（`MainWindow.xaml:1084-1088`）にMouseUp系ハンドラは無い。
3. ゆえに**通常のシートクリック（閾値未満で完結）の後、`_sheetDragSource`は次のD&D開始まで残留し続ける**。
4. 残留状態で「左ボタン押下のままSheetNavList上を通過するマウス移動」（今回の合成ドラッグの後半がまさに該当）が来ると、`_sheetDragSource is null`ガード（3754行）を素通りし、閾値（前回クリック位置との距離）も超えるため**`DoDragDrop`が誤発火する**。
5. 到達点は`SheetNavList_Drop`→`MoveSheetCommand`（シート並び替え）。ただし同コマンドは「選択中シートの実体を変えない」設計（`SetCurrentSheetIndexWithoutCrossCut`、`SheetNavigationViewModel.cs:271-280`）かつUndo対象外・DRC結果破棄のみで、**シート内容消失・機器表空欄・Undoクリアのいずれも起こさない**——本事象の主因ではないと判断する（ただし§5-1のシート順確認で発火有無が判る）。

**パターン照合【MUST】**: 「開始時にセットした状態フィールドの対のクリア漏れ」であり、所見C調査で確認したAvalonDock側`LayoutAnchorableTabItem._draggingItem`残留（MouseUpでリセットしない）と**同型の問題がecad2自前コードにも存在した**形。型としてはPR-06（対の処理・不変条件が型システムで強制されず人的手段のみ）の系譜。修正は侍領分（`PreviewMouseLeftButtonUp`での`_sheetDragSource=null`クリア追加が最小対処と見立てる）。台帳記帳は家老の裁定を仰ぐ。

## 4. 増分1（統合トポロジ）の影響（依頼(3)への回答）

- SheetNavListのD&D実装・イベント購読・New/Open経路は増分1で**無変更**（`git diff a78b802~1..HEAD`で該当部にヒット無し、所見A調査で実測済みの差分範囲と同じ）。
- 幾何学的にも「上段ツールバー（タブ下部）の直下に左パレット（シートパネル）」の配置は増分1前後で同一であり、「タブから真下へのドラッグがシートパネルへ入る」状況は前から存在した。
- 増分1固有の変化はタブドラッグ側（CanDock="False"でOverlayWindow非生成→ドロップターゲット不表示）だが、これは§3の誤発火経路とも症状とも独立。
- **結論: 増分1が本事象の座標系・イベントルーティングへ影響した形跡は無い**。

## 5. 仮説判別マトリクスと忍者への追加確認依頼（保全状態のまま、読み取りのみ）

| # | 確認事項（UIA読取/目視） | NewDocument説 | Undo実行説 | 描画消失説（環境GPU） |
|---|---|---|---|---|
| 1 | SheetNavListの項目数・シート名一覧 | **「シート1」1枚のみ**（決定打） | 元の枚数のまま | 元の枚数のまま（UIAで読める） |
| 2 | ウィンドウタイトル/現在ファイルパス表示 | 無題（filePath=null）へ変化 | 変化なし | 変化なし |
| 3 | 「やり直し(Redo)」のIsEnabled | Disabled（Clearで両スタック空） | **Enabled**（決定打） | 変化なし |
| 4 | UIAで機器表（DeviceTableGrid）の行数・キャンバス関連要素 | 論理的にも空 | 過去の状態 | **論理は残存**（描画のみ欠落、決定打） |
| 5 | 保存確認ダイアログの表示履歴 | 出ていない=IsDirty=falseだった | — | — |
| 6 | 事象発生時刻前後の操作ログ（他プロセスの入力注入・常駐マウスツールの稼働有無） | — | — | —（№2の外部要因切り分け） |
| 7 | シート項目の**順序**（並び替えの有無） | —（§3誤発火の発生有無が判る、症状とは独立） | 同左 | 同左 |

追加で、判別後の深掘りが必要になった場合は**診断ログ注入**（`NewDocument`/`LoadFromFile`/`UndoCommand.Execute`の呼出時刻+スタックトレースを記録、侍領分）が最短路（`memory: feedback_diagnostic_log_escalation`）。再現試行は現状保全の後、殿裁可を得てから。

---

# 最終結論（2026-07-22 追補、忍者の6項目確認+対照再現試験を受けて）

## 決着: 環境競合が引き金、実行体はecad2の正規機能（Undo）という複合。実装回帰は無し

忍者の追加観測（シート**0枚**・Redo Disabled・機器表論理0件・タイトル不変・MouseAssistant稼働中）と対照再現試験（MouseAssistant終了後は同一ドラッグ2回とも無害。稼働時は1回で即発生）を突合した結果、以下の機序で全観測が無矛盾に説明できる:

**MouseAssistantが合成ドラッグ（左ボタン押下の直線移動）に反応してUndo系入力（Ctrl+Z等）を連発注入し、スナップショット履歴を遡って「起動直後のシート0枚状態」まで復元された。**

### 裏付け（一次ソース）

1. **起動直後=Sheets=0は正式仕様**: `Document { get; private set; } = new();`+「起動直後は空(Sheets=0、HasProject=false)」（`MainWindowViewModel.cs:237-242`）。`HasProject => Document.Sheets.Count > 0`（381行）。0枚は「HasProject=false・画面濃紺のempty state」として設計上存在する状態。
2. **Undoで0枚へ戻るのは設計想定内**: `ApplyUndoRedoSnapshot`のコメントが「Undo/Redoが『シート0枚⇔1枚以上』の境界を跨ぐ可能性がある(最後の1枚のAdd/Deleteを取り消す場合)」と明記（3245行、`NotifyHasProjectChanged()`呼出あり）。最初のシート追加の`RecordSnapshot`は0枚状態を記録しており（`SheetNavigationViewModel.cs:125`が追加**直前**に記録）、そこまでUndoすれば0枚が復元される。
3. **観測「Undo/Redo両方Disabled」との整合（本調査最大の難所の解消）**: UndoCommand/RedoCommandのCanExecuteはいずれも`CanEditDiagram && !HasAnyDraft && UndoManager.CanUndo/CanRedo`（3191/3199行）で、`CanEditDiagram => HasProject && Mode == AppMode.Drawing`（127行）。**0枚（HasProject=false）ではRedoスタックにデータが残っていてもDisabled表示になる**——「Redo Disabled=Undo実行説の否定材料」という中間判定は誤りで、むしろUndo連打後の0枚状態はRedo Disabled表示になるのが正しい挙動だった。
4. タイトル不変: `ApplyUndoRedoSnapshot`は`CurrentFilePath`に触れない（ReplaceDocumentとは別実装、3212-3215行）。機器表0件: 同メソッドが`DeviceTable.Rebind(restored.Devices)`で0枚時代の空Devicesを復元（3247行）。ダイアログなし: Undoに確認ダイアログは無い。
5. §1で最有力としたReplaceDocument説は「シート0枚」（NewDocumentなら1枚残る）で棄却。§2のとおりマウス列からの到達経路も無かった。Undo経路はマウス列から直接到達しないが、**外部ツールのキー注入**という橋が対照実験で裏付けられた。注入された正確な入力の特定はMouseAssistant設定に依存し本調査の範囲外（機序確定をしたい場合のみTraceLog有効での再現が決定打になる、任意）。

### 【重大・上申】設計上の罠を1件検出: 0枚到達でRedo復旧不能の袋小路

「Undoで0枚へ戻れる（設計想定内）」×「0枚ではHasProjectゲートによりRedoがDisabled（T-061修正A-3のCanEditDiagram統合による）」の組み合わせは、**Undoが0枚へ達した瞬間、Redoスタックに直前データが生きているのにUIから復旧できない詰み状態**を作る。さらにこの状態でシートを追加すると`RecordSnapshot`→`_redoStack.Clear()`（`UndoManager.cs:25`）で復旧の道が完全に閉じる。今回は検証データのため実害無しだが、実ユーザーデータなら重大。対処案（RedoのCanExecuteからHasProject条件を外す＝Redoは「編集」でなく「復元」と位置づける／Undoの底を1枚状態で止める等）はUI/UX分岐のため殿裁定事項として`docs/proposed.md`経由を推奨。

### 付随成果・再発防止

- **別件バグ（§3）**: `_sheetDragSource`のMouseUpクリア漏れ（D&D誤発火経路）は本事象と独立に要修正として残る。
- **運用**: UIA合成ドラッグを伴う検証時はMouseAssistantを終了する（`memory: env_mouseassistant_click_conflict`の適用場面拡大——クリック競合だけでなく**ジェスチャー的な合成移動への反応**も起きる）。
- 保全インスタンス: 復旧不能（上記の袋小路）のため、殿裁定のうえ保全解除でよい。

---

## 出典

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`: 2910-2945（SaveToFile/LoadFromFile/NewDocument）・2950-3062（ReplaceDocument、UndoManager.Clear=3059）・279-343（CurrentSheetIndex/CurrentSheet）・127/237-242/381（CanEditDiagram/Document初期値=空/HasProject）・3185-3199（Undo/RedoCommandのCanExecute）・3212-3254（ApplyUndoRedoSnapshot、0枚⇔1枚境界の明記=3245）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`: 95-199（AddCommand=追加直前RecordSnapshot・DeleteCommand=最終1枚ガードCanExecute:199）
- `src/Ecad2.App/Diagnostics/TraceLog.cs`: 全読（既定OFF・ECAD2_TRACE_LOG/--trace-logで有効化・%TEMP%\ecad2-trace.logへClick/Focus/PropertyChanged記録）
- 忍者の6項目確認・対照再現試験（MouseAssistant有無、家老peer経由2026-07-22）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`: 236-293（MoveSheetCommand、Undo対象外・SetCurrentSheetIndexWithoutCrossCut）
- `src/Ecad2.App/Commands/UndoManager.cs`: 12-51（CanUndo/CanRedo/Clear）
- `src/Ecad2.App/MainWindow.xaml.cs`: 100-105（_sheetDragSourceフィールド）・1124-1137（New/OpenのIsDirtyガード）・2609-2627（Ctrl+S/O/P/Nキーハンドラ）・3738-3821（SheetNavList D&D一式）
- `src/Ecad2.App/MainWindow.xaml`: 677-678/788-789（新規トリガー）・1081-1088（SheetNavListイベント購読5種、MouseUp無し）
- `.claude/skills/ecad2-ui-automation/helpers.ps1`: 86-99（DragDrop=純マウス合成）・331-338（Invoke-Ecad2Drag）
- 忍者peer報告（再現条件・EnumWindows確認）・`docs/ecad2-t110-increment2-finding-c-investigation-onmitsu.md`（_draggingItem残留の同型指摘）
- memory: `env_mouseassistant_click_conflict`・`feedback_diagnostic_log_escalation`
