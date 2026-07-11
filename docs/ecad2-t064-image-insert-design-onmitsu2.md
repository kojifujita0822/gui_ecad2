# T-064 画像挿入機能のUI結線：着手前設計整理

調査者: 隠密2　最終更新: 2026-07-11

家老采配（殿しばし席を外されるにつき家老裁量で沙汰）。GuiEcad実物の画像挿入操作体系を調査し、
ecad2 App層に必要な新規実装項目とUI/UX分岐論点を整理する。**実装は行っていない、着手前調査のみ**。

---

## DoD(1): GuiEcad実物の画像挿入操作体系

`GuiEcad.App/MainPage.Image.cs`（全文Read済）・`MainPage.Pointer.cs`・`MainPage.Properties.cs`・
`Commands/ElementCommands.cs`を実物照合。

| 操作 | GuiEcadの実装 |
|---|---|
| 挿入トリガー | 縦ツールパレット（経路B）の「画像」ボタン（`OnInsertImageClick`） |
| ファイル選択 | `FileOpenPicker`、フィルタ=.bmp/.png、既定フォルダ=PicturesLibrary |
| 配置位置 | マウスホバー中のセル位置（`_hoverCell`）。ホバー外なら余白基準点 |
| 初期サイズ | 画像の実ピクセルサイズをmm換算、長辺が120mm超なら縮小（アスペクト比維持） |
| 選択 | クリックのヒットテスト。**背面固定描画のため他要素より判定優先度が最後**（`HitTestImage`） |
| 移動 | ドラッグ（サブグリッドスナップ、`CellMm/LineSnapDiv`刻み）、`MoveImageCommand` |
| リサイズ | **ドラッグハンドルではなくプロパティパネルの幅/高さNumberBox（数値入力）**、`ResizeImageCommand` |
| トレース用下絵トグル | プロパティパネルのCheckBox（`IsTracingOnly`）、`SetImageTracingOnlyCommand` |
| 削除 | 既存の削除経路（Deleteキー等）、`DeleteImageCommand` |
| 永続化 | ファイルパスの外部参照のみ（.GCADに画像自体は埋め込まない）。ドキュメント読込時にキャッシュへ
  再ロード（`ReloadImageCacheForDocument`） |
| Undo対応 | `IUndoCommand`実装の個別コマンドクラス4種（Add/Delete/Move/Resize/SetTracingOnly） |

**注目点**：リサイズはドラッグハンドルではなく数値入力のみ。GuiEcadはマウス主体UIだが、この点に限れば
キーボード操作と親和性が高い（ecad2のキーボードファースト方針と相性が良い可能性がある）。

---

## DoD(2)前提: ecad2側の現状確認

### Core層（todo.md記載「完備」の裏付け、Read済）

- `ImageInsert`モデル（`src/Ecad2.Core/Model/Element.cs:100-118`）：GuiEcad版とフィールド完全一致
  （Id/FilePath/XMm/YMm/WidthMm/HeightMm/IsTracingOnly）。
- `DiagramRenderer.DrawImages`（`DiagramRenderer.cs:471-481`）：実装済み、トレース用下絵の画面/PDF出し
  分け（`IncludeTracingImages`オプション）も含め完備。
- **画面描画・PDF出力とも実装済み**（前回T-071調査では未確認だった追加確認点）：
  `WpfRenderer.DrawImage`（`src/Ecad2.Rendering.Wpf/WpfRenderer.cs:114-123`、ファイルパスから
  `BitmapImage`を遅延ロード＋キャッシュして描画）・`PdfRenderSurface.DrawImage`
  （`src/Ecad2.Pdf/PdfRenderSurface.cs:220-225`）とも実物あり。**画面・PDFどちらも「渡されれば
  正しく描く」状態で待機中**。

### App層（皆無の確認）

`ImageInsert`・「画像」でsrc/Ecad2.App全体をgrepしても実装コード0件（MainWindow.xamlに1件ヒットした
「画像」は無関係な既存コメント——ツールバーアイコンのStrokeThickness調整コメント、誤ヒット）。
**挿入・選択・移動・リサイズ・プロパティ表示・削除、いずれも皆無**。todo.mdの記述どおり。

### Undo基盤の重要な相違点（GuiEcadとecad2で設計が根本的に異なる）

ecad2のUndo基盤（`src/Ecad2.App/Commands/UndoManager.cs`、T-051）は、GuiEcadの個別コマンドクラス
方式（`IUndoCommand`、操作ごとにAdd/Delete/Move/Resize等のクラスを新設）とは**全く異なる**：
ドキュメント全体をJSONスナップショットとしてUndo/Redo二本のスタックへ積む方式
（`RecordSnapshot(doc)`を操作直前に呼ぶだけ）。**画像操作専用のUndoコマンドクラスを新規に作る
必要は無い**——既存の他操作（要素配置・削除等）と同じパターンで、各画像操作の直前に
`RecordSnapshot`を呼べばUndo/Redo対応が完了する。GuiEcadより実装コストが低い点として明記する。

### プロパティ表示領域の現状

右パネル下段に「プロパティ」領域は存在する（`MainWindow.xaml:467-481`）が、現状は
`HasSelectedElement`の二値切替（未選択時プレースホルダー／選択時`DeviceNameBox`固定1項目）のみの
単純な作りで、GuiEcadのような「選択オブジェクト種別に応じて動的にコントロールを構築する」仕組みは
無い。画像選択時に幅/高さ・トレーストグルを表示するには、この領域の拡張（選択種別分岐）が必要。

---

## DoD(2): App層に必要な新規実装項目

1. **挿入トリガー**：メニュー項目 or ツールバーボタン新設（UI/UX論点1）
2. **ファイルピッカー呼び出し**：WPF `Microsoft.Win32.OpenFileDialog`（bmp/png フィルタ）
3. **配置位置・初期サイズ決定ロジック**：GuiEcad同型を踏襲するか要検討（UI/UX論点2）
4. **選択機構への統合**：`MainWindowViewModel`に`SelectedElement`/`SelectedConnector`等と同様の
   排他選択プロパティ（`SelectedImage`相当）を新設。クリックのヒットテストも新設
5. **移動（ドラッグ）**：マウスドラッグでの位置変更
6. **リサイズ操作**：数値入力方式を踏襲するか、ドラッグハンドル方式を新設するか（UI/UX論点3）
7. **プロパティ表示領域の拡張**：選択種別分岐（画像選択時に幅/高さ・トレーストグルを表示）
8. **削除経路への統合**：T-063で実装済みの「削除」メニュー・Deleteキー経由の`DeleteSelected*`群に
   `DeleteSelectedImage`相当を追加
9. **Undo対応**：新規コマンドクラス不要、既存`RecordSnapshot`パターンを画像操作の各所に適用するのみ

---

## DoD(3): UI/UX分岐となりうる論点（選択肢化、殿確認要）

### 論点1: 挿入トリガーの配置場所

| 案 | 内容 |
|---|---|
| A | メニュー項目のみ新設（「挿入」等の新設メニュー、または既存メニューへ追加） |
| B | ツールバーへ専用ボタン新設（GuiEcadの経路B「画像」ボタンに相当する位置） |
| C | 両方（メニュー＋ツールバー、T-071の自作パーツボタン等と同様の二重導線） |

### 論点2: 配置位置の決定方式（ecad2の「キーボードファースト」方針との整合が論点）

| 案 | 内容 |
|---|---|
| A | GuiEcad同様マウスホバー位置基準 |
| B | ecad2の`SelectedCell`（キーボード選択中セル）基準——キーボード操作のみで完結でき、既存の
    要素配置フロー（`PlaceElementAtSelectedCell`）と統一感がある |
| C | 固定位置（左上余白等）に挿入し、事後にドラッグ/数値入力で移動する前提 |

### 論点3: リサイズ操作方式

| 案 | 内容 |
|---|---|
| A | GuiEcad同様、プロパティパネルの数値入力（NumberBox相当）のみ |
| B | ドラッグハンドルによる直接リサイズ（マウス操作、GuiEcadには無い機能） |
| C | 両方併用 |

### 論点4: 対応ファイル形式

| 案 | 内容 |
|---|---|
| A | GuiEcad同様 bmp/png のみ |
| B | jpg等も追加（WPFの`BitmapImage`はjpg等も読める見込みだが未検証） |

---

## 出典一覧

- `docs/todo.md`（T-064節）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Image.cs`（Read全文）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Pointer.cs`（画像選択/移動箇所、Read）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Properties.cs:310-388`
  （`BuildImageProperties`・リサイズ・トレーストグル、Read）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/Commands/ElementCommands.cs:569-610`
  （Image系Undoコマンド4種、grep確認）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.Core/Model/Element.cs`（ImageInsertモデル、Read）
- `src/Ecad2.Core/Model/Element.cs:100-118`（ImageInsert、Read）
- `src/Ecad2.Core/Model/Sheet.cs`（Read全文）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs:186,469-481`（DrawImages、Read）
- `src/Ecad2.Rendering.Wpf/WpfRenderer.cs:114-130`（DrawImage実装、Read）
- `src/Ecad2.Pdf/PdfRenderSurface.cs:220-225`（DrawImage実装、grep確認）
- `src/Ecad2.Core/Rendering/IRenderer.cs:47`（DrawImageインターフェース定義、grep確認）
- `src/Ecad2.App/Commands/UndoManager.cs`（Read全文）
- `src/Ecad2.App/MainWindow.xaml:369-481`（右パネル・プロパティ領域の現状、Read）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（SelectedElement等の選択機構、grep確認）

## 不明点

- WPFの`BitmapImage`がjpg等bmp/png以外の形式をどこまで正しく扱えるかは未検証（論点4の判断材料、
  必要なら追加調査可能）。
- GuiEcadの「背面固定描画・選択ヒットテスト最後」という設計（画像は常に最背面）を踏襲するかは
  今回は自明として選択肢化しなかったが、他要素との重なり方についてもし異論があれば別途論点となる。

## 派生提案の有無

なし（家老采配の範囲内で完結）。
