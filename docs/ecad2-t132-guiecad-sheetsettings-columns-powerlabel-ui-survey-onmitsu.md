# T-132 シート設定：列数・電源ラベル 原本UI形式調査（隠密）

> 2026-07-27 隠密調査。家老采配「T-132 原本調査」（P-070起票、殿裁可2026-07-27）を受けての
> 静的調査。原本＝`C:\Users\kojif\Desktop\生産物\gui_ecad`。手筋はT-131と同型。
> スコープ境界：**調査のみ、UI案の決定はしない**。

---

## 総括

原本のシート設定ダイアログ（`OnSheetSettings`、`MainPage.Dialogs.cs:160-246`）は、電源ラベル・
列数を含む7項目を単一の`ContentDialog`内に持つ。**最重要のDoD(2)＝列数を減らして既存要素が
はみ出す場合の扱いは、「クランプ」でも「警告して拒否」でもなく、『ユーザー指定値と既存要素が
必要とする最小列数の、大きい方を静かに採用する』という第3の方式**だった——ユーザーへの
警告・エラー表示は一切無い。ecad2側の現行ダイアログ（行数のみ実装済み）は対照的に**入力
検証エラーを明示表示してOKを弾く**方式を採用しており、両者の「はみ出し時の哲学」は既に
食い違っている。この落差は殿裁可の重要な材料になると考える。

---

## 1. 原本のUI形式（DoD(1)）

`MainPage.Dialogs.cs:160-246`（`OnSheetSettings`）全文確認。単一`ContentDialog`
（Title="シート設定"）内、`StackPanel`（縦積み）で以下7項目を上から順に配置：

| # | 項目 | コントロール | 入力形式 | 既定値 |
|---|---|---|---|---|
| 1 | シート名 | `TextBox` | 自由入力（Header="シート名"、PlaceholderText="省略時: シート N"） | `_sheet.Name` |
| 2 | 左母線名 | `TextBox` | 自由入力 | `_sheet.Bus.LeftName` |
| 3 | 右母線名 | `TextBox` | 自由入力 | `_sheet.Bus.RightName` |
| **4** | **電源ラベル（電圧）** | **`TextBox`** | **自由入力**（Header="電圧（母線間・任意）"、PlaceholderText="母線間電圧（例: AC200V）"） | `_sheet.Bus.PowerLabel ?? ""` |
| 5 | 「既定にする」 | `CheckBox` | チェック | 常にoff |
| 6 | 「主回路」 | `CheckBox` | チェック | `_sheet.MainCircuit` |
| **7** | **列数** | **`NumberBox`** | **スピナー付き数値**（`SpinButtonPlacementMode.Inline`、Minimum=2/Maximum=20、Header="列数（2〜20）"） | `_sheet.Grid.Columns` |
| 8 | 行数 | `NumberBox` | 同上（Minimum=1/Maximum=60、Header="行数（1〜60）"） | `_sheet.Grid.Rows` |

**電源ラベル**：シンプルな`TextBox`（プレースホルダのみ、専用の検証UIなし）。空欄ならOK確定時に
`null`へ変換される（`voltageBox.Text.Trim().Length > 0 ? ... : null`、217行）——**空文字許容**
（ecad2既存の母線名バリデーション方針「殿裁定、GuiEcad踏襲」と同型）。

**列数**：`NumberBox`＋インラインスピンボタン（+/-が常に見える形式、`SheetSettingsDialog.xaml.cs`
既存の行数`TextBox`＋手動パースとは異なるコントロール種）。`Minimum`/`Maximum`によるUIレベルの
範囲制限に加え、OK確定時にもう一段`Math.Clamp(newCols, 2, 20)`が入る（230行、二重防御）。
**エラーメッセージ・確認ダイアログは無い**——範囲外を入力しようとしてもNumberBox自体が
UIレベルで弾く設計（WinUI3の`NumberBox`標準機能）。

---

## 2. 列数を減らす方向で既存要素がはみ出す場合の扱い（DoD(2)、最重要）

`MainPage.Dialogs.cs:229-241`：

```csharp
int newCols = double.IsNaN(colBox.Value) ? _sheet.Grid.Columns : (int)colBox.Value;
newCols = Math.Clamp(newCols, 2, 20);
int minCols = _sheet.Elements.Count > 0
    ? _sheet.Elements.Max(el => el.Pos.Column + el.CellWidth)
    : 1;
_sheet.Grid.Columns = Math.Max(newCols, minCols);

int newRows = double.IsNaN(rowBox.Value) ? _sheet.Grid.Rows : (int)rowBox.Value;
newRows = Math.Clamp(newRows, 1, 60);
int minRows = _sheet.Elements.Count > 0
    ? _sheet.Elements.Max(el => el.Pos.Row + 1)
    : 1;
_sheet.Grid.Rows = Math.Max(newRows, minRows);
```

**結論＝「ユーザー入力値」と「既存要素が実際に占有している最大範囲（列＝`Pos.Column +
CellWidth`の最大値、行＝`Pos.Row + 1`の最大値）」の、大きい方を採用する**。行・列とも完全に
同型のロジック。

- ユーザーが「10列にしたい」と入力しても、既存要素が15列目まで使っていれば、**実際には15列の
  ままになる**（黙って上書き、ユーザー入力は無視される）。
- **警告・エラーメッセージは一切表示されない**。ダイアログはOKで正常に閉じ、ユーザーは
  「列数を変更したつもりが変わっていない」ことに、後でグリッド表示を見て気づく形になる。
- 逆にユーザー入力値が既存要素の必要量より大きければ、ユーザー入力値がそのまま採用される
  （通常の拡大ケース）。
- **「クランプ」（強制的にユーザー値へ寄せる）でも「拒否」（ダイアログを閉じさせない）でもない、
  『暗黙の下限保護』という第3の方式**——DoD(2)の選択肢（クランプ/警告/拒否）のいずれにも
  完全には当てはまらない点に注意されたい。

---

## 3. ecad2側`SheetSettingsDialog`の現況（DoD(3)）

`src/Ecad2.App/Views/SheetSettingsDialog.xaml`・`.xaml.cs`（29行＋40行、全文確認）。

**現行の実装項目＝行数・左母線名・右母線名の3項目のみ**（`TextBlock`ラベル＋`TextBox`の
組み合わせ、WPFネイティブコントロール）。**列数・電源ラベルはいずれも未実装**（本タスクの
対象そのもの）。「既定にする」チェック・「主回路」チェックの2項目も未実装だが、これらは
T-132のスコープ外（起票元のP-070は列数・電源ラベルの2点のみを対象とする）。

**入力検証の方式が原本と異なる**：

```csharp
private void OkButton_Click(object sender, RoutedEventArgs e)
{
    if (!int.TryParse(RowsBox.Text, out int rows) || rows < GridSpec.MinRows || rows > GridSpec.MaxRows)
    {
        RowsErrorText.Visibility = Visibility.Visible;   // エラーメッセージ表示、OKを弾く
        return;
    }
    ...
}
```

ecad2の既存「行数」実装は、`TextBox`＋手動`int.TryParse`＋範囲外なら**赤字エラーメッセージ
表示＋OK処理を中断**（`RowsErrorText.Visibility = Visible`）という設計——**原本の「黙って
クランプ/下限保護」方式とは明確に異なる、エラー明示型**。列数を新設する場合、この既存の
行数実装パターン（エラー明示型）を踏襲するか、原本の暗黙的下限保護方式を踏襲するかは、
**本タスク唯一にして最大のUI/UX分岐**になると考える（家老采配文の見立てどおり）。

**Core層の受け皿**：`BusConfig.PowerLabel`（`string?`、`Sheet.cs:47`）は既に存在し、原本と
同型（空文字はnullへ変換して保持、UI側の責務）。一方、**`GridSpec`（`Sheet.cs:31-40`）には
`MinRows`/`MaxRows`（1/60）は定数として存在するが、`MinColumns`/`MaxColumns`に相当する定数は
現状存在しない**——列数の範囲チェックを行数と同型（`GridSpec.MinColumns`等の定数新設）で
実装するか、原本と同じ2〜20をハードコードするかも、実装時の検討事項になる（事実として報告、
提案はしない）。**ecad2の`Columns`既定値は40**（`Sheet.cs:39`）——原本の許容上限（20）を
既に超えている点も、範囲決定の際の考慮材料になりうる（事実のみ報告）。

---

## 不明点

- 原本の`Grid.Columns`の既定値（新規シート作成時）は本調査では確認していない（`OnSheetSettings`
  はダイアログ実装のみを対象としたため）。ecad2既定値40との比較が必要であれば追加調査可。
- 「既定にする」「主回路」チェックの2項目はT-132の対象外だが、原本ダイアログには存在し
  ecad2には無い——将来別タスクの検討材料になりうる（自らタスク化はしない、気づきとして
  記録のみ）。

---

## 出典・参照

- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Dialogs.cs`
  （369行、全文。160-246行＝`OnSheetSettings`全体）
- `src/Ecad2.App/Views/SheetSettingsDialog.xaml`（29行、全文）
- `src/Ecad2.App/Views/SheetSettingsDialog.xaml.cs`（40行、全文）
- `src/Ecad2.Core/Model/Sheet.cs`（62行、全文。31-40行＝`GridSpec`、43-48行＝`BusConfig`）
- `docs/todo.md`（T-132節、98-108行）
