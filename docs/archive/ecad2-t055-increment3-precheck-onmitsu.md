# T-055 増分3 事前調査（隠密）

家老采配（2026-07-10）を受けての事前調査。計画書 `docs/archive/ecad2-t055-implementation-plan-samurai.md` §1増分3の
着手前調査。**調査のみ、実装は行っていない。**

調査項目は (1) WPF ContextMenu実装パターン、(2) 5種要素の行シフト処理設計案。いずれも Explore エージェントへ
委譲した一次調査結果を、隠密が実物照合のうえ統合した。

---

## 1. WPF ContextMenu 実装パターン

### 事実

- `src/Ecad2.App/` 配下に `ContextMenu` の前例は**皆無**（grep 0件）。ゼロから設計してよい。
- 右クリック関連イベント（`MouseRightButtonDown` 等）も現状**皆無**。左クリックは
  `MainWindow.xaml:420-423` で `PreviewMouseLeftButtonDown/Move/Up` を `LadderCanvasHost` に配線し、
  実処理は `MainWindow.xaml.cs:318`以降・`460`以降。
- 座標→GridPos変換は既存流用可: `Views/LadderCanvas.cs:394`
  `public GridPos ToGridPos(Point localPositionDip) => new GridPos(geo.RowAt(yMm), geo.ColAt(xMm));`
  呼び出し例 `MainWindow.xaml.cs:542`。
- Command基盤: `Commands/RelayCommand.cs:5-32` の `RelayCommand`（`Action`/`Action<object?>` +
  任意の`CanExecute`）。ViewModel側の例 `MainWindowViewModel.cs:1620-1627`
  （`AddRowCommand = new RelayCommand(() => {...}, () => CurrentSheet is Sheet sheet && ...)`）。
- ダイアログ呼び出し流儀（`MainWindow.xaml.cs:162-169`、`RenameDialog`等）:
  View側で`new`・`ShowDialog()`→結果を`Command.Execute(param)`で渡す。**動的な文言・パラメータは
  View（コードビハインド）側で確定してからCommandへ渡す**のが確立パターン。
- メニューHeaderの動的生成（「行{N}の前に...」）の前例は無いが、上記ダイアログパターンと同型で
  「コードビハインド側で状況確定→動的生成」に倣うのが既存流儀に整合する。

### 所見（推奨アプローチ）

`LadderCanvasHost` に `PreviewMouseRightButtonDown`（または`ContextMenuOpening`）ハンドラを追加し、
`ToGridPos`で行番号を確定 → コードビハインドで`ContextMenu`/`MenuItem`を都度`new`し
`Header = $"行{pos.Row + 1}の前に行を挿入"`等をセット → `Command`は新設`RelayCommand`
（例:`InsertRowBeforeCommand`）でMainWindowViewModelへ、パラメータは行番号（int）を渡す設計を推奨。
ダイアログ（`ShowDialog`）は不要、`AddRowCommand`型の直接Command実行パターンに近い。

---

## 2. 5種要素の行シフト処理設計

### 2.1 型定義・保持構造（事実、`src/Ecad2.Core/Model/Element.cs` および `Sheet.cs`）

| 型名 | Row系プロパティ | 保持元（`Sheet`） | setter可変性 |
|---|---|---|---|
| `ElementInstance` | `Pos.Row`（`GridPos`内） | `Elements: List<ElementInstance>` | `Pos`はset可、`Row`単体は不可（`with`式必須） |
| `VerticalConnector` | `TopRow`/`BottomRow` | `Connectors: List<VerticalConnector>` | 各々直接set可（2値独立） |
| `WireBreak` | `Row` | `WireBreaks: List<WireBreak>` | 直接set可 |
| `GroupFrame` | `TopLeft.Row`（`GridPos`内）、`Height`は範囲 | `Frames: List<GroupFrame>` | `TopLeft`はset可、`Row`単体は不可（`with`式必須）、`Height`は直接set可 |
| `RungComment` | `Row` | `RungComments: List<RungComment>` | 直接set可 |

`GridPos`は `readonly record struct GridPos(int Row, int Column)`（Element.cs:34）でイミュータブル。
`Sheet.Lines`（`CircuitLine.Row`）は家老指示によりシフト対象外で確定済み（未使用フィールドの可能性）。

増分1（`MainWindowViewModel.cs:1306-1311`の`IsRowOccupied`）が今回の5種を同一順序で走査済みだが、
増分1レビュー（`docs/archive/ecad2-t055-increment1-review-onmitsu.md:84`）で「増分3で再利用できない一回限りの実装」と
申し送り済み。

### 2.2 GuiEcad参考実装（事実、実物照合済み）

計画書にある「`RowOps.ShiftRows`相当」は仮称ではなく、GuiEcad実ソースに実在するクラス名。
`C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\ElementCommands.cs:389-494` を実物照合した。

```csharp
// 390-407行: 4種（GroupFrame以外）の共通シフト
internal static class RowOps
{
    public static void ShiftRows(Sheet sheet, int threshold, int delta, bool inclusive)
    {
        bool Hit(int row) => inclusive ? row >= threshold : row > threshold;
        foreach (var e in sheet.Elements)
            if (Hit(e.Pos.Row)) e.Pos = e.Pos with { Row = e.Pos.Row + delta };
        foreach (var c in sheet.Connectors)
        {
            if (Hit(c.TopRow)) c.TopRow += delta;
            if (Hit(c.BottomRow)) c.BottomRow += delta;
        }
        foreach (var rc in sheet.RungComments)
            if (Hit(rc.Row)) rc.Row += delta;
        foreach (var wb in sheet.WireBreaks)
            if (Hit(wb.Row)) wb.Row += delta;
    }
}
```

GroupFrameは`RowOps.ShiftRows`の対象**外**で、`InsertRowCommand`/`DeleteRowCommand`側で個別分岐している
（詳細は2.3節）。GuiEcad側はUndo機構（`IUndoCommand`）と同居する`GuiEcad.App.Commands`名前空間に配置。

### 2.3 GroupFrame範囲またぎの扱い（事実＋所見）

GuiEcad実装（挿入・削除とも実物照合済み）:

**挿入**（`targetRow`の前に1行挿入、`InsertRowCommand.Execute`）:
```csharp
foreach (var f in _sheet.Frames)
    if (f.TopLeft.Row >= _targetRow) f.TopLeft = f.TopLeft with { Row = f.TopLeft.Row + 1 };
    else if (f.TopLeft.Row + f.Height > _targetRow) f.Height++;
```
- 枠の開始行が挿入点以降（`TopLeft.Row >= targetRow`）→ 枠ごと下へ1行シフト（位置のみ、Height不変）
- 枠の開始行が挿入点より前だが範囲が挿入点にかかる（`TopLeft.Row + Height > targetRow`）→ `Height++`（内部挿入、位置不変）

**削除**（`targetRow`を削除、`DeleteRowCommand.Execute`）:
```csharp
if (f.TopLeft.Row == _targetRow) { /* 枠ごと削除 */ }
else if (f.TopLeft.Row < _targetRow && f.TopLeft.Row + f.Height - 1 >= _targetRow)
    { f.Height--; }
// ...
foreach (var f in _sheet.Frames)
    if (f.TopLeft.Row > _targetRow) f.TopLeft = f.TopLeft with { Row = f.TopLeft.Row - 1 };
```
- 枠の開始行そのものが削除対象 → 枠ごと削除
- 削除対象行が枠の内部（開始行より下・終端行以上）→ `Height--`（内部詰め、位置不変）
- 枠が削除行より完全に下 → 位置のみ-1シフト

**所見**: 「枠の開始行にかかる／またぐ挿入・削除は位置シフト、内部（開始行より後ろの範囲内）にかかる挿入・削除は
Height伸縮」という一貫した規則になっている。`Height`は「枠が占める行数」という意味的フィールドであり、
内部への挿入で新行も枠に包含されるのは自然な挙動。**この規則をecad2でも同型踏襲することを推奨する。**

計画書 `docs/archive/ecad2-t055-implementation-plan-samurai.md:103` は「`GroupFrame.Height`は行数のため挿入/削除で
調整不要、位置のみシフト」と記載しているが、これは枠の**開始行以降まるごとが挿入/削除点より後ろにある単純ケース**
のみを想定した簡略化であり、**内部挿入・内部削除（Height伸縮が必要なケース）が計画書に未記載**である。
増分3着手前に計画書側の追記が必要。

### 2.4 共通ロジックの置き場所・抽象化（所見）

- **置き場所**: ecad2はUndo未実装（スコープ外）のためCommandクラス化は不要。`Ecad2.Core.Model`配下に
  新規 `RowOps.cs`（`internal static class RowOps`、メソッド名`ShiftRows`はGuiEcadと揃える）を推奨。
  `Sheet.cs`への直接追記は責務混在になるため避ける。
- **`IHasRow`のような共通インターフェース抽象化は不要**と考える。理由:
  - `VerticalConnector`はRow相当が2値（TopRow/BottomRow）で単一プロパティに畳めない
  - `ElementInstance`/`GroupFrame`は`GridPos`内包で`with`式による再構築が必要
  - `WireBreak`/`RungComment`は直接代入
  この非対称性を一枚のインターフェースで吸収するコストが、型ごとに数行のループを書くコストを上回る。
  GuiEcad実装も同様に型別直書きで解決しており、前例と整合する。

---

## 3. 家老へ提起する新規論点（殿確認要否は家老判断に委ねる）

**削除対象行に要素が存在する場合の扱い**——家老指示の「UI/UX確定済み事項」は
「**最終行**に要素があれば削除拒否（警告）」だが、これは**増分1（末尾行の加減算）**の挙動であり、
**増分3（任意位置削除）**の「削除対象行そのものに要素がある場合」の扱いとは別論点。

GuiEcad実装（`DeleteRowCommand.Execute`、上記2.3節コード）を見る限り、GuiEcadは**要素ごと削除**する
（削除を拒否しない）。ElementInstance/VerticalConnector（Top/BottomRow一致）/WireBreak/RungComment/
GroupFrame（開始行一致）のいずれも、削除対象行にかかっていれば消える。

これは計画書 `docs/archive/ecad2-t055-implementation-plan-samurai.md:109` の開かれた論点そのもの
（「ecad2はUndo非対応のため単純に要素ごと削除するか、削除を拒否するかは殿確認要」）であり、
今回の家老采配メッセージの「確定済み事項」には含まれていない。**未確定と判断し、増分3着手前に
殿確認が必要な事項として報告する。**

---

## 4. PoC要否の所見

計画書は増分3を「着手前にリスク検証（PoC）を挟むことを推奨」としていたが、本調査の結果:

- **ContextMenu実装**: WPF標準機能の範囲内。前例はないが、既存の座標変換（`ToGridPos`）・
  Command基盤（`RelayCommand`）・「View側で状況確定→Command実行」の流儀を組み合わせれば見通しが立つ。
  技術的な不確実性は低い。
- **行シフト処理**: GuiEcadに実装例が実在し、型ごとの直書きループ（4種共通＋GroupFrame個別分岐）で
  対応可能なことが具体的なコードで裏付けられた。ecad2側の型定義もGuiEcadとほぼ対応しており、
  移植の障害は見当たらない。

**所見: 独立したPoCフェーズ（別ブランチでの先行検証等）は不要と考える。** GuiEcadの実装が実質的に
「動作実績のある設計」として機能しており、通常の増分実装プロセス（RED先行証明＋5型それぞれのテスト、
計画書§1増分3のテスト観点）で十分にリスクを吸収できる。ただし上記§3の削除挙動の論点、および
計画書のGroupFrame内部挿入/削除の記載更新は、実装着手前に解消しておくべき。

---

## 5. 次の1手

1. 殿確認: §3「削除対象行に要素が存在する場合、要素ごと削除か拒否か」
2. 計画書 `docs/archive/ecad2-t055-implementation-plan-samurai.md` §1増分3へ、GroupFrame内部挿入・削除時の
   Height伸縮規則（§2.3）を追記
3. 上記2点が解消され次第、侍が増分3実装に着手可能（独立PoCフェーズは不要という所見つき）
