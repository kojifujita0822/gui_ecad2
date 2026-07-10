# T-055 GuiEcad行数拡張・母線番号入力方式 調査書（隠密）

殿直接指示2026-07-10「10行ではまったく足りないのでgui_ecadの方式を参考にして欲しい。なお母線番号入力も同じ仕様にしたい」を受けた第一段調査。GuiEcad実ソース（`C:/Users/kojif/Desktop/生産物/gui_ecad`）とecad2側受け入れ地形（`C:/ECAD2`）を調査した。

---

## 【最重要・要確認】前提の齟齬：GuiEcadに「母線番号」概念は存在しない

GuiEcadのソースコード・テストコードを全域調査したが、**「母線番号」という数値概念は存在しない**。母線（左右の縦電源線）に関するデータは以下の2種のみ：

- **母線名（`Sheet.Bus.LeftName`/`RightName`）**：文字列（例："N24"/"P24"、既定値）、シート設定ダイアログで手動編集する自由記述ラベル。数値ではない。
- **母線間電圧（`PowerLabel`）**：同じくシート設定ダイアログの文字列フィールド。

一方、GuiEcadには**「回路番号」（`CircuitLine.CircuitNumber`）**という別概念が存在する。これは母線ではなく**行（横の回路線）**に付く連番で、要素が存在する行にのみ`CircuitNumberer`が自動採番する（図面全体通し、1から開始、シートPageNumber順）。ユーザーによる手動編集UIは調査範囲では見つからず、恐らく完全自動採番（未確証）。描画は図面キャンバス上（母線脇）への直接表示は見つからず、クロスリファレンス表内でのみ`"{PageNumber}-{CircuitNumber}"`形式で使われている。

**殿の「母線番号入力」がどちらを指すか（あるいは第三の概念か）、着手前に必ず確認が必要**。特に「入力」という語感からは、ユーザーが値を書き込む操作を想定していると読めるため、自動採番の「回路番号」よりも、GX Works3等の一般的なラダーCADで見られる「行番号を母線脇に手動または自動で表示する」機能（ecad2に近似機能=後述の`DrawRowNumbers`）を指している可能性もある。

---

## 1. GuiEcad行追加・管理方式

### UI操作（3系統＋ダイアログ数値入力、すべて同一コマンドへ集約）
- **ツールバーボタン**：「行＋」/「行－」（`MainPage.xaml:230-231`）
- **キーボードショートカット**：既定`Ctrl+Shift+Up`=末尾行追加、`Ctrl+Shift+Down`=末尾行削除。カスタマイズ可能（`MainPage.KeyBindings.cs:75-90`）
- **右クリックコンテキストメニュー**：「行{N}の前に行を挿入」（任意位置挿入）・「末尾に行を追加」・「行{N}を削除」（`MainPage.ContextMenu.cs:107-124`）
- **シート設定ダイアログでの直接数値入力**：`MainPage.Dialogs.cs:173-179`

### データモデル
`GridSpec`クラス（`GuiEcad.Core/Model/Sheet.cs:30-35`）：
```csharp
public sealed class GridSpec {
    public int Rows { get; set; } = 22;
    public int Columns { get; set; } = 40;
}
```
`int`型可変プロパティ。行挿入・削除はUndo/Redoコマンド化（`InsertLastRowCommand`/`DeleteLastRowCommand`/`InsertRowCommand`/`DeleteRowCommand`、`Commands/ElementCommands.cs:267-494`）。任意位置挿入・削除は`RowOps.ShiftRows`で要素・縦コネクタ・行コメント・分断マーク・枠(GroupFrame)の行番号を一括シフト。

### 既定行数（エントリポイントによりバラバラ、要注意）
| 経路 | 既定Rows |
|---|---|
| `GridSpec`クラス自体の初期値 | 22（実質未使用のフォールバック値） |
| 新規ドキュメント作成 | 8 |
| 新規シート追加 | 8（列数は直前シート継承） |
| テンプレートの一部 | 10 |

### 上限・下限
- **上限**：シート設定ダイアログのみ`Maximum=60`でクランプ（`Dialogs.cs:176,237`）。**ツールバー/ショートカット/右クリック経由の行追加コマンドには上限チェックが一切ない**（ダイアログとコマンド実装が不整合、GuiEcad側の既存の粗さとして両論併記）。
- **下限**：1行。UI層のガードのみ（`Grid.Rows<=1`で無効化、コマンド自体に下限チェックなし）。最終行は消せない。

### 描画・ページ・スクロールへの波及
- 画面キャンバスのサイズ計算（`PageSize`, `enableBorder=false`）は**`Grid.Rowsを見ず**、実際に配置された要素の`maxRow`のみから算出（`DiagramRenderer.cs:121-140`）。画面キャンバス自体はPan/Zoom変換による仮想無限キャンバスで、行数変更時の明示的な再サイズ処理は不要。
- PDF/印刷（`enableBorder=true`）はA4/A3等の固定用紙サイズに対し`RowsPerPage`（用紙に収まる行数、A4縦=28行）で機械的にページ分割（`PageCount`/`RenderPageCount`, `DiagramRenderer.cs:59-102`）。

### 行削除
末尾行削除・任意行削除の2系統。削除行上の要素等はUndo可能な形で削除、枠(GroupFrame)は高さ縮小または削除、後続行は`RowOps.ShiftRows`で繰り上げ。

---

## 2. GuiEcad「母線名」「回路番号」（母線番号候補、詳細）

### 母線名・母線間電圧
- **入力UI**：シート設定ダイアログ（`MainPage.Dialogs.cs:160-227`）、`TextBox`自由入力。「この母線名を新規シートの既定にする」チェックボックスで`LadderDocument.Settings.DefaultBus`に既定値として保存（新規シート作成時の自動初期値＋既存シートは個別手動編集の併用）。
- **データ保持**：`Sheet.Bus`（型`BusConfig`、`Sheet.cs:10,37-43`）。`LeftName`/`RightName`は`string`、シートごとに1組。
- **描画**：`DiagramRenderer.DrawBusLabels`（`DiagramRenderer.cs:234-264`）。左右母線の**上端**に`FontSizeMm=2.4, Bold=true, HAlign=Center`で表示。`PowerLabel`設定時は左右母線を結ぶ両矢印付きで中央上部にも表示。
- **保存形式**：`GcadSerializer`は汎用JSON化のみ（`BusConfig`専用処理なし）、POCOとして自動永続化。

### 回路番号
- **入力**：ユーザー手動編集UIは見つからず、`CircuitNumberer.Number(_document)`呼び出しで自動採番（DRC実行時・テストモード評価更新時・PDF出力時にオンデマンド再計算）。
- **データ保持**：`Sheet.Lines`（`List<CircuitLine>`、`{ int Row; int CircuitNumber; }`、`Sheet.cs:16,52-57`）。要素が存在する行にのみエントリ（空行は採番されない）。
- **描画**：図面キャンバス上（母線脇）への直接描画は見つからず。クロスリファレンス表内でのみ使用。
- **行番号との関係**：`GridPos.Row`（内部座標）とは**一致しない独立の連番**（`docs/data-model.md:30`に「行番号（ステップ番号）は不採用」と明記）。

---

## 3. ecad2側の受け入れ地形

### `GridSpec.Rows`定義
`src/Ecad2.Core/Model/Sheet.cs:31-35`。GuiEcadと同一構造：`int`型可変プロパティ、既定値`Rows=22, Columns=40`（ただし実際の生成箇所ではこの既定値は使われていない）。

### `new GridSpec`の全網羅（2箇所のみ、両方Rows=10ハードコード）
- `SheetNavigationViewModel.cs:106`（`AddCommand`）
- `MainWindowViewModel.cs:1472`（`NewDocument()`）

いずれも`new GridSpec { Rows = 10, Columns = 20 }`。追加のファクトリ等は存在しない。

### 境界ガード（T-045）
共通ロジック`IsWithinGridBounds(GridPos, Sheet)`（`MainWindowViewModel.cs:1315-1317`）を`IsSelectedCellWithinGrid()`（View層事前検知）と`ValidatePlacement()`（配置直前の最終検証）が共有。`Grid.Rows-1`クランプは同ファイル内10箇所以上に点在（縦コネクタ・分断マーク・OR配線ドラフトのドラッグ/移動処理、行386,387,393,429,470,471,567,590,1011,1037,1038,1110）。キーボード移動（`MainWindow.xaml.cs:896`）も同様。

### 描画側`TotalRows`（拡張の受け皿として既に整っている）
`DiagramRenderer.cs:62-67`の`TotalRows(sheet) = Math.Max(sheet.Grid.Rows, maxRow+1)`は**Grid.Rowsの変更に自動追従する設計**（P-002過去バグ修正済み、コメントに経緯記載）。`PageSize()`・`RowsPerPage`・`PageCount`もこれを経由するため連動。画面側`LadderCanvas.Draw`も`PageSize(sheet)`経由でGrid.Rows拡張が自動的にキャンバスサイズ（スクロール領域）へ反映される。**Rows可変化自体の描画側の受け入れ態勢はGuiEcad同様に良好**と見込まれる。

### 【追加所見】PDF出力機能がUIに未結線の可能性
`RenderPageCount`/`PageCount`/`PdfRenderSurface`のsrc内呼び出し元・`MainWindow.xaml`の「PDF出力」メニューのClickハンドラ配線が確認できなかった。行数拡張とは直接関係しないが、T-055着手前後で「行数を増やしたら複数ページPDFがどう出るか」を実機確認する際に影響しうるため、家老・侍への申し送り事項として記録。**要追加確認**。

### 母線番号相当の既存機能
ソースコード中に「母線番号」概念自体は存在しない（`docs/todo.md`のT-055起票文にのみ出現）。近似機能は`DiagramRenderer.DrawRowNumbers`（既定`ShowRowNumbers=true`）——`GridPos.Row+1`を**描画時のみ自動算出**する視覚ガイド、ユーザー編集不可・永続化なし（`Element.cs`コメントに明記）。ユーザー入力データとしては`RungComment`（右母線右側コメント）・将来実装予定なしの`CircuitLine`相当は存在しない。

---

## 4. 移植時の設計論点（殿確認が必要）

1. **「母線番号」の定義**（最重要、上記参照）：GuiEcadの「母線名」（文字列、手動編集）か「回路番号」（自動採番、行単位）か、あるいはecad2の既存`DrawRowNumbers`相当（自動表示のみ、編集不可）を拡張した「編集可能な行番号」を新設するか。
2. **既定行数**：GuiEcadはエントリポイントにより8/10/22とバラバラ（統一されていない）。ecad2でどの値を採用するか、あるいは殿の「10行では足りない」という評から、より大きい既定値（20〜30程度）を検討するか。
3. **行数上限**：GuiEcadはダイアログのみ60でクランプ、コマンド経路は無制限という内部不整合を抱えている。ecad2で踏襲するか、それとも一貫した上限を設けるか。
4. **行追加のUI経路**：GuiEcadの3系統（ツールバー・キーボードショートカット・右クリックコンテキストメニュー）のうち、ecad2のキーボードファースト方針でどこまで揃えるか（ecad2は現状ツールバー＋キーボードショートカットのみが確立、右クリックコンテキストメニューは今回調査した範囲では見当たらず新規UI要素になる可能性）。
5. **PDF出力の未結線疑い**：上記「追加所見」参照。行数拡張の実機確認に影響しうる。

---

## 不明点

- GuiEcad `CircuitLine.CircuitNumber`がユーザー手動編集可能かどうか（編集UIが見つからなかったため恐らく完全自動、確証なし）
- GuiEcadでマウスクリックによる`Grid.Rows`範囲外への直接要素配置がクランプされるか（キーボード配置モードでは上限チェックなしを確認済みだが、マウス経路は未確認）
- GuiEcad `GridGeometry.cs`（`RowAt`/`YRow`等）自体の実装詳細（`DiagramRenderer.cs`経由の呼び出しのみ確認、直接は未読）
- ecad2側`GcadSerializer`が旧ファイル（Grid欠落）読込時に`GridSpec`既定値（Rows=22）へフォールバックする実際の挙動（推測、未検証）
- ecad2側PDF出力機能がUIに結線されているか（上記「追加所見」参照）

## 派生提案の有無

あり——PDF出力機能のUI未結線疑いは、T-055範囲外の気づきとして`docs/proposed.md`への起票を提案する（本調査書内に留め、自ら着手はしない）。

---

## 出典

- GuiEcad: `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.{xaml,Menu,KeyBindings,ContextMenu,Dialogs,Sheets,Pointer,Templates}.cs`、`src/GuiEcad.Core/Model/Sheet.cs`、`src/GuiEcad.Core/Rendering/DiagramRenderer.cs`、`src/GuiEcad.Core/Simulation/CircuitNumberer.cs`、`src/GuiEcad.Core/Commands/ElementCommands.cs`、`tests/GuiEcad.Tests/NumberingTests.cs`、`docs/data-model.md`（GuiEcad側）
- ecad2: `src/Ecad2.Core/Model/Sheet.cs`、`src/Ecad2.App/ViewModels/{SheetNavigationViewModel,MainWindowViewModel}.cs`、`src/Ecad2.App/MainWindow.xaml.cs`、`src/Ecad2.Core/Rendering/DiagramRenderer.cs`
