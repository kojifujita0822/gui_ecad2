# ecad2 仕様書：配置操作

T-075（殿裁定、2026-07-11起票）体系の第5号、第3弾。実装コード・殿裁定記録
（`docs/todo.md`/`docs/todo-archive.md`/`docs/proposed.md`）・忍者実機検証記録（`docs-notes/`配下）を
突き合わせ、「仕様として確定している挙動」を出典付きで明文化する。

---

## 0. 要点：組込み品と自作パーツはデータ経路として統一されている

**F5〜F8で配置する「組込み」要素（a接点/b接点/コイル/端子台）も、実装上は`PartId`経由で配置される**
——`ElementInstance.Kind`（既定値`ContactNO`）は配置時に一度も設定されず、事実上未使用フィールドと
化している（`MainWindowViewModel.PlaceElementAtSelectedCell`、`MainWindowViewModel.cs:1492-1555`）。

組込み品は`BasicPartTemplates.cs`（`src/Ecad2.Core/Persistence/`）が`PartDefinition`として定義し、
`PartFolderStore.SeedBasics()`が初回起動時に「図形/」フォルダ直下（`Category=""`）へ`.gcadpart`として
書き出す。`PartResolver`が「`PartId`で`PartLibrary`から解決できればそちらを優先、できなければ
`ElementCatalog`（`Kind`ベース）へフォールバック」という統一解決を行うため、**組込み品も自作パーツも
同一の`SelectionEntries`リストに混在し、同一の配置経路（後述）を通る**。

**不明点**：`ElementInstance.Kind`が配置時に設定されない理由（設計意図か歴史的経緯か）はコード上
明記されていない。

---

## 1. 要素配置のモデル

| クラス | 役割 |
|---|---|
| `ElementInstance`（`Element.cs:44-71`） | `Id`/`Kind`(既定値のみ)/`Pos`/`CellWidth`(常に1)/`DeviceName`/`PartId`/`Params` |
| `PartDefinition`（`PartDefinition.cs:36-51`） | `Id`/`Name`/`WidthCells`/`HeightCells`/`Role`(PartRole)/`IsOrEligible`/`Ports`/`Primitives` |
| `ElementCatalog` | `Kind`ベースの既定値表。`PartId`未設定の特殊種別（Motor/Breaker3P等）専用 |
| `PartResolver` | `PartId`優先、フォールバックで`ElementCatalog`という統一解決ロジック |

---

## 2. 3つの配置経路と`TryPlaceElement`への収束

配置操作には3つの入口があり、いずれも最終的に**同一の`TryPlaceElement`→
`PlaceElementAtSelectedCell`**に収束する（`MainWindow.xaml.cs:1178-1181`コメントに明記）。

| 経路 | 起点 | 挙動 |
|---|---|---|
| ① F5〜F8キー | `Window_PreviewKeyDown`→`TryPlaceBuiltin` | `SelectedCell`が**既に選択済み前提**、即座に配置バー表示 |
| ② ツールバーボタン | `BuiltinPlaceButton_Click`→`ActivateBuiltinTool` | `Tool=PlaceElement`にするのみ、キャンバスクリックでセル確定後に配置バー表示 |
| ③ 部品選択パレット | 「自作パーツ」ボタン→リストクリック | `Tool=PlaceElement(PartId=null)`→右パネルが部品選択リストに切替→クリックで確定 |

- ①はキー1発で完結する「一発配置」、②③は「ツール選択→クリックで確定」という段階を踏む。
- `TryPlaceElement`（`MainWindow.xaml.cs:1488-1525`）：`SelectedCell`未選択／境界外／占有済みのいずれも
  エラー表示で拒否。通れば配置バー（`ElementPlacementBar`）を表示。
- 確定は`PlacementOkButton_Click`が`PlaceElementAtSelectedCell(partId, deviceName, isOr)`を呼ぶ。

---

## 3. OR配置（Shift+F5/F6）

`isOr`パラメータが`ToolState`〜`PlaceElementAtSelectedCell`まで一貫して流れる。`isOr=false`なら
通常配置と同一処理で終了。`isOr=true`の場合（`MainWindowViewModel.cs:1515-1554`）：

1. 新要素より上（`Pos.Row < pos.Row`）にある既存要素の最大行を`baseRow`として探索（無ければ何もしない）。
2. `baseRow`行内で列位置が最も近い要素を`baseElement`とする。
3. `leftColumn=min(...)`、`rightColumn=max(...)+cellWidth`を算出。
4. **右側（合流側）の縦コネクタは常時生成**。左側は`NothingBetweenRailAndColumn`
   （配置行・基準行の双方で母線からの間に既存要素・縦コネクタが無い）がtrueなら省略
   （T-044、電気的トポロジー等価性を保証できる場合のみの最適化）。

OR対象エントリは専用データを持たず、`PartPaletteViewModel`が`IsOrEligible==true`
（a接点/b接点のみ）のエントリを`IsOr=true`でラップした論理エントリとして`SelectionEntries`に
注入する（`ResolveEntry(partId,isOr)`でPartId+IsOr完全一致優先→PartId一致のみへフォールバック）。

### 裁定経緯

T-037殿裁定（2026-07-06）＝案A採用：リストへORa/ORb追加＋サムネイル右下にORバッジ合成。実装後
「ORセレクトSWも誤って混入」する不具合が発覚し、殿裁定で「ORa/ORbのみに絞る」と修正。T-033増分5
殿裁定（2026-07-07）で「ドロップダウンは選択どおりの動作」に統一——**旧来の暗黙OR保持ルール
（接点系同士切替でisOr保持）はこの裁定で廃止**。

---

## 4. 配置バー（ElementPlacementBar）

`MainWindow.xaml:591-654`。`IsPlacementBarVisible`（`MainWindowViewModel.cs:49-59`）が単一の真実源。

- **表示中はグローバルショートカット無効化**（殿裁定2026-07-07、グレーアウト方式）。隠密レビューで
  マウス経路6系統の素通し（DRC行クリック→占有セルジャンプ→二重生成の実データ不整合）を
  CONFIRMEDし、殿裁定で恒久遮断された経緯あり（T-033増分1）。
- **位置決め**：`PositionPlacementBar(cell)`が選択セルの真下に配置、`MainWorkAreaGrid`の右端・下端で
  クランプ（殿注文3点、2026-07-05）。
- **Enter確定**：OKボタンが`IsDefault="True"`のためEnterキーで発火。選んだComboBoxアイテムの
  `IsOr`がそのまま採用される。
- **キャンセル**：`ClosePlacementBar()`が`IsPlacementBarVisible=false`＋キャンバスへフォーカス復帰。
  要素は「OK確定時のみ生成」構造のため、**Escでの中断は孤立要素を残さない**（原子的取消）。
- 確定後も`Tool`/`SelectedCell`はリセットされない（T-021裁定＝連続配置のため）。
- `PlacementDeviceNameBox`（配置バー内）と`DeviceNameBox`（プロパティパネル、既配置要素のリネーム用）
  は別物として実装上区別されている。

### 裁定経緯（T-033、全5増分、完全Done 2026-07-07）

T-021隠密調査（2026-07-04）で「配置後入力が別Windowのモーダル」という齟齬が判明し分離。殿注文3点
（2026-07-05）：(1)表示位置=選択セルの真下 (2)横長化、接点選択と機器名入力を横並び (3)OK/キャンセル
見切れ修正。増分2で位置バグ（`Margin`残留の自己参照フィードバックループ）が判明・修正。増分4で
ComboBoxを文字表記からグリフ表示へ変更（殿の新注文）。

---

## 5. 部品選択パレットとの連動

`PartPaletteViewModel`が`PartFolderStore`から`Entries`を読み込み`SelectionEntries`を構築。
組込み品（`Category=""`）も自作パーツ（`Category="自作"`）も**区別なく同一リストに混在**する。

「自作パーツ」ボタン→`ActivateOpenPartSelection()`は`Tool=PlaceElement(PartId=null)`にするのみ。
これにより`IsPartSelectionVisible`（`Tool.Mode==PlaceElement`）がtrueとなり、右パネル下段が
「プロパティ」から「部品選択」へ切替わる（鶏卵問題の回避）。リストアイテムクリックは
`PartSelectionItem_Clicked`が`TryPlaceElement(entry.Entry, entry.IsOr)`を呼ぶ——**F5〜F8と全く同じ
メソッドに合流**。

---

## 6. 配置時のバリデーション・制約

| 制約 | 判定箇所 | 拒否時の挙動 |
|---|---|---|
| 重複配置 | `IsSelectedCellOccupied()`（バー表示前）＋`ValidatePlacement`（確定直前）の二重ガード | エラー表示 |
| 境界外 | `IsSelectedCellWithinGrid()` | 「選択したセルはグリッド範囲外です」 |
| 未選択セル | — | 「配置するセルを先に選択してください」 |
| プロジェクト未作成 | `HasProject`ガード | 「シートがありません。新規作成（Ctrl+N）から始めてください」 |

重複判定は`Pos`の完全一致のみで、`CellWidth`（複数セル占有）を考慮した範囲重複チェックではない
（F5〜F8対象は全て1セル幅のため実害なしと推測されるが、コード上は多セル要素の重複を防がない、
**不明点**）。

### 重要な非対称性：F5〜F8にはシート種別チェックが一切ない

**a接点/b接点/コイル/端子台（OR含む）は主回路シートでも制御回路シートでも同じく配置可能**——
`TryPlaceBuiltin`/`ActivateBuiltinTool`/`PlaceElementAtSelectedCell`のいずれにも`sheet.MainCircuit`
判定は存在しない。ツールバーの該当ボタンも`IsEnabled="{Binding HasProject}"`のみ。

これに対し、F9/Shift+F9/F10（自由線・縦コネクタ・接続点・配線分断）は`sheet.MainCircuit`を明示
チェックし、対応しないシート種別では拒否メッセージを返す（`docs/spec/ecad2-spec-wiring.md`3節参照）。
**「配置操作」と「結線操作」でシート種別依存の扱いが根本的に異なる**点は仕様理解上の重要な注意点。

---

## 7. 既知の罠・実装バグ

### 修正確認済み

- T-033配置バー位置座標系不整合（増分2でNG判定→修正確認OK）。
- T-036デバイス名編集未反映（修正後Enter/Tab/欄外クリックの3経路OK。ただし空文字確定時の機器表
  孤立エントリは別バグとして申告のみ・未修正、参考記録）。
- T-044連鎖OR配置での縦コネクタ見落とし（修正後OK）。
- P-020種別誤分類・P-024境界セル再発（T-045増分Bで修正確認、「配置下限0」殿裁定2026-07-09）。

### 未解決

- **P-012：行0（最上段）への配置で母線・シンボルが描画されない現象**。T-015検証時に初出、複数タスクで
  再現するが原因未特定のまま。忍者が6条件で再現試行したが「不明」で報告した経緯もある
  （`docs-notes/ecad2-p012-reproduction-attempt-ninja.md`）。後日T-044検証で具体的再現手順
  （行0/列0選択→F5→配置バー→OK→シンボル未描画+要素は内部的に存在）を特定したが、原因は
  未解明のまま。
- P-021：`Sheet.Elements`に重複Pos防止機構の不変条件保証は将来課題としてpending（最小案はT-045
  増分Bで対処済み）。
- P-041：種別切替直後（約300ms以内）のキャンバスクリックで旧種別のまま配置される疑い（UI Automation
  起因の可能性が高いが未確定、保留継続）。
- P-045：あらかじめ選択済みの空セルへF5配置すると、プロパティパネルの表示が更新されない
  （SetProperty早期returnトラップと同型の疑い、pending）。

### UI Automation検証固有の罠（実装バグではない）

モーダル表示中の`Send-Ecad2Keys`未達（ダイアログ自身のNativeWindowHandleへ直接
`SetForegroundWindow`する回避策を確立済み）、UIA Invoke/SelectがClickハンドラを迂回し偽の結果を
生む、部品選択リストの選択ハイライトが視覚的に残らない（対照指標に使うと誤読しやすい）。

---

## 8. 関連タスク（自作パーツ管理系）

- T-015（図形ビジュアルプレビュー）：Done。部品選択リストへサムネイル追加（案1採用）。
- T-035（.gcadpart読込時のID重複再採番）：Done。「再採番パーツのみ配置失敗」という疑いは操作手順の
  誤観測と確定（実装バグなし）。
- T-037（部品選択リストへORa/ORb追加）：Done（3節参照）。
- T-043（ORa/ORbサムネイルのシンボル統一）：Done。
- **T-068（自作パーツ管理・編集UI）：未着手（Approved、gated）**。`PartFolderStore`等Core層は完備
  だが、自作パーツの作成・編集UI自体（GuiEcadのパーツエディタ相当）が皆無。規模大、ロードマップ
  最後尾。
- **T-071（経路B部品10種の専用グリフ、本日新規采配）**：殿裁定3点（GuiEcad専用パレットUI再現は不要／
  「部品」から呼べればよい／既存7種と重複するものは追加不要）。着手前調査で配置バーの簡易アイコンが
  新規10種に対応しないと判明、殿裁定＝全10種分の専用グリフを新規作成（T-048の「意匠プレビュー→
  殿承認制」を踏襲）。現在侍へ実装采配済み・未着手。

---

## 9. 実機確認記録

- F5〜F8の4種配置・自動配線・機器表反映は正常動作、退行なし（本日T-062検証含む複数回確認）。
- OR配置（Shift+F5/F6）：7種ドロップダウンでのORa/ORb選択、縦コネクタ自動生成、家老指定4観点
  （母線際OR配置・連鎖3階層OR・遮る要素ありケース・右合流）すべてOK確認済み。
- シート0件時、F5〜F8全キー無反応＋案内メッセージ表示を確認。
- 本日T-062検証で「要素配置はUndo対象外が元々の仕様」と判明（`docs/spec/ecad2-spec-undo-redo.md`
  0節と一致）。

## 不明点

- `ElementInstance.Kind`が配置時に設定されない理由（設計意図か歴史的経緯か）。
- 重複配置判定が`CellWidth`を考慮しない点の実害有無（F5〜F8対象が全て1セル幅のため現状は顕在化
  しないと推測されるが未検証）。
