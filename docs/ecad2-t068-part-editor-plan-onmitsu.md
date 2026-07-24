# T-068 自作パーツ管理・編集UI 実装計画（隠密プラン起草）

殿裁可済み大物。T-110（AvalonDock統合）と同じ進め方（隠密プラン起草→殿裁可→増分実施）で進める。

## 1. 現状調査

### 1.1 Core層（サポート済み）

| 機能 | API | 場所 |
|---|---|---|
| 単体パーツ保存/読込 | `PartLibrarySerializer.SaveOne`/`LoadOne` | `Persistence/PartLibrarySerializer.cs:54,61` |
| ライブラリ一括保存/読込 | `Save`/`Load`（`.gcadparts`、`SchemaVersion`検査あり） | 同上:23-49 |
| 新規作成相当 | `PartFolderStore.SaveCustom(PartDefinition)` | `Persistence/PartFolderStore.cs:137-143` |
| 一覧列挙 | `PartFolderStore.Enumerate()` | 同上:59-134 |
| 削除 | `PartFolderStore.Delete(path)` | 同上:146-149 |
| 端子・電気役割解決 | `PartResolver.Ports/BoundarySpan/...` | `Model/PartResolver.cs:10-68` |
| 直線マージ最適化 | `PartOptimizer.MergeCollinearLines` | `Model/PartOptimizer.cs:11-38` |
| プレビュー描画 | `PartDrawing.Draw(...)` | `Rendering/PartDrawing.cs:11-66`（編集UIのプレビューにそのまま流用可） |

保存形式は`.gcadpart`（1図形1ファイル、`PartFolderStore.PartExtension`）。

### 1.2 Core層（欠落）

- プロパティ/端子/形状の「編集」専用API・バリデーションが無い（`PartDefinition`は素のPOCOなので呼び出し側で直接書き換え可能だが、境界整合性チェック等は皆無）
- 複製（Duplicate/Clone）APIが無い
- リネーム時のファイル移動/整合性APIが無い（`SaveCustom`は`part.Name`から都度ファイル名生成、名前変更時に旧ファイルが残る）
- `SaveCustom`/`Delete`はApp層のどこからも呼ばれておらず実質未使用（＝T-068で新規に配線する対象）

### 1.3 App層（現状）

**欠落の確認結果：YES（新規作成・編集・削除・複製・保存のUI導線は皆無）**

- `PartPaletteViewModel`/`PartSelectionEntryViewModel`はいずれも表示・選択・配置専用（追加/変更/削除メソッド無し）
- 既存UI（`PartSelectionList`、ツールバー「自作パーツ (F11)」ボタン、配置ドロップダウン）は全て「既存パーツを選んでキャンバスに置く」機能のみ
- 参考にできる既存ダイアログパターン：
  - `Views/AddSheetDialog.xaml`（新規作成系の雛形、`TextBox`名入力+`RadioButton`種別選択+OK/キャンセル）
  - `Views/SheetSettingsDialog.xaml`/`Views/RenameDialog.xaml`（既存データ編集の定型、コンストラクタに既存値を渡し`ShowDialog()==true`でコミット）

### 1.4 GuiEcad原本

**該当実装：YES**（`C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\PartEditorWindow.xaml`＋`.xaml.cs`、132行+956行）

画面構成：
- 上段：名前・幅/高さ（セル単位1-12）・役割（`PartRole` 8種）・組込み図形読み込みドロップダウン
- 中段：描画ツールバー（選択/線/折れ線/矩形/円/弧/回転/文字/接続点のRadioButton）+ Undo/Redo/選択削除 + ズーム
- キャンバス：Win2D `CanvasControl`によるポインタ操作の直接描画
- 下段：弧の扁平率入力、開く/保存/閉じるボタン

対応操作：新規作成/編集（コンストラクタ`edit`引数の有無で分岐）・保存（名前必須・ポート2点以上必須のバリデーション+`MergeCollinearLines`適用）・開く（`.gcadpart`読込で置換）・削除（確認ダイアログ経由、呼び出し元`MainPage.Parts.cs`）・インポート/エクスポート（`.gcadparts`/`.gcadpart`）。**複製専用機能は無い**（別名保存で代替）。

`PartDefinition`のデータモデルはecad2とGuiEcadでほぼ完全同一（`PartPrimitive`多態含む）。ecad2側の拡張差分は`PartRole`追加6種+`SelectSwitch`、`IsOrEligible`フィールドのみ。**Core層・永続化層はT-007で移植済みだが、GUI層（WinUI3/Win2Dのキャンバス編集UI）は未移植** —— T-068はこのGUI層をWPFで新規実装するタスクに相当する。

## 2. 規模の見立てと増分分割案

GuiEcad原本（XAML 132行+コードビハインド956行）が直接の参考になるが、キャンバス描画がWin2D `CanvasControl`（ポインタイベント直接処理）のため、WPFへは**移植ではなく再設計**が必要（`LadderCanvas.cs`/`PartDrawing.cs`の実装知見は流用できる）。

シンボル編集（形状定義）とプロパティ定義（名前/幅高さ/役割/端子）は、`PartDefinition`が単一クラスながら3つの独立したコレクション/値を持つ構造のため、**設計上分離しやすい**（UI側で「プロパティタブ」「端子タブ」「形状タブ」に分けて編集し最後に同一インスタンスへマージする方式が自然）。この分離しやすさを活かし、リスクの低い部分から積み上げる増分分割を提案する。

| 増分 | 内容 | リスク |
|---|---|---|
| 増分0 | PoC：キャンバス上でのプリミティブ直接編集（線・矩形等のドラッグ操作感）を最小限で検証 | 高（後述） |
| 増分1 | プロパティ編集（名前/幅高さ/役割）+ 新規作成/保存/削除/UI導線配線。`AddSheetDialog`等の既存パターン踏襲、GuiEcadのバリデーション（名前必須）も踏襲 | 低 |
| 増分2 | 端子（Port）編集（リスト形式でRowOffset/BoundaryOffset込みの追加・削除）。GuiEcadのバリデーション（ポート2点以上必須）も踏襲 | 低〜中 |
| 増分3 | 形状（Primitive）編集キャンバス本体（線/折れ線/矩形/円/弧/文字/接続点の描画ツール、Undo/Redo、ズーム） | 高 |
| 増分4 | インポート/エクスポート（`.gcadparts`一括） | 低（優先度も低め、後回し可） |

複製（Duplicate）はGuiEcadに専用機能が無かった点を踏まえ、UI/UX判断分岐点として殿確認へ回す（3節参照）。

## 3. リスク評価

**高リスク領域：増分3（形状編集キャンバス）**

- GuiEcad実装はWinUI3/Win2D固有API（`CanvasControl`、ポインタイベント）であり、直接移植不可。ecad2の`LadderCanvas.cs`はグリッドセルへの要素配置・固定図形描画が主体で、「任意形状の対話的編集（ドラッグで頂点移動、円弧のハンドル操作等）」は前例が無い新規UI・新規データ編集フロー
- Undo/Redoについては、App層に汎用`Commands/UndoManager.cs`が既に存在することを確認済み（詳細な流用可否は増分3設計時に詳しく調べる）
- 高リスク領域のPoC先行を推奨（`docs/README.md`が定める高リスク領域の扱い方針＝実装速度より検証優先、プラン→リスク検証(PoC)→増分実装＋各増分で忍者検証、に従う）

**低リスク領域：増分1・2**

- CRUD的な性質で、既存ダイアログパターン（`AddSheetDialog`/`SheetSettingsDialog`/`RenameDialog`）の延長で実装可能

## 4. UI/UX判断分岐点（実装未着手、殿確認へ）

1. **形状編集のツールセット**：GuiEcad同等9種（選択/線/折れ線/矩形/円/弧/回転/文字/接続点）をそのまま踏襲するか、絞るか
2. **複製機能の要否**：GuiEcadには無かった「別名で保存」代替に対し、ecad2で正式な複製ボタンを新設するか
3. **パーツエディタの画面形態**：GuiEcad方式（モーダルダイアログ）を踏襲するか、AvalonDockペイン化するか（ecad2のUI/UX方針＝区画分け維持との整合を要検討）
4. **端子位置編集の入力方式**：キャンバス上でのドラッグにするか、数値入力フォーム（RowOffset/BoundaryOffset直接入力）にするか

## 5. 次の一手

増分0（PoC）の詳細設計、または殿裁可を経た増分1からの着手を、侍への采配として家老にご検討いただきたい。
