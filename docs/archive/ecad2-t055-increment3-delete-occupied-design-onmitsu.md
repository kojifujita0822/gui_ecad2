# T-055増分3 削除対象行への「要素ごと削除」対応 設計調査＋テスト設計（隠密）

殿裁定確定（2026-07-11）：GroupFrame内部削除の到達不能問題（引き継ぎメモ記載）は「要素ごと削除
（GuiEcad同型）」採用で解決。**適用範囲は増分3（`DeleteRowAtCommand`）限定**（増分1
`DeleteRowCommand`・増分2`UpdateSheetSettingsCommand`は現状の拒否のまま、遡及修正なし）。
家老采配（2026-07-11）DoD3点に対応。本書は**設計調査＋テスト設計の起草のみ**、実装はまだ着手しない。

---

## 1. GuiEcad実ソース：5種の「行削除時の要素ごと削除」挙動照合

`C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\ElementCommands.cs:436-473`
（`DeleteRowCommand.Execute`）を実物照合。既存precheck文書（`docs/ecad2-t055-increment3-precheck-onmitsu.md`
§2.3）はGroupFrame部分のみの抜粋だったため、本書で5種全体・実行順序を含め再照合する。

```csharp
public void Execute()
{
    _removedElements = _sheet.Elements.Where(e => e.Pos.Row == _targetRow).ToList();
    foreach (var e in _removedElements) _sheet.Elements.Remove(e);
    _removedConnectors = _sheet.Connectors
        .Where(c => c.TopRow == _targetRow || c.BottomRow == _targetRow).ToList();
    foreach (var c in _removedConnectors) _sheet.Connectors.Remove(c);
    _removedBreaks = _sheet.WireBreaks.Where(b => b.Row == _targetRow).ToList();
    foreach (var b in _removedBreaks) _sheet.WireBreaks.Remove(b);
    _removedComments = _sheet.RungComments.Where(rc => rc.Row == _targetRow).ToList();
    foreach (var rc in _removedComments) _sheet.RungComments.Remove(rc);
    _shrunkFrames = new(); _removedFrames = new();
    foreach (var f in _sheet.Frames.ToList())
    {
        if (f.TopLeft.Row == _targetRow) { _removedFrames.Add(f); _sheet.Frames.Remove(f); }
        else if (f.TopLeft.Row < _targetRow && f.TopLeft.Row + f.Height - 1 >= _targetRow)
            { _shrunkFrames.Add((f, f.Height)); f.Height--; }
    }
    RowOps.ShiftRows(_sheet, _targetRow, -1, inclusive: false);
    foreach (var f in _sheet.Frames)
        if (f.TopLeft.Row > _targetRow) f.TopLeft = f.TopLeft with { Row = f.TopLeft.Row - 1 };
    _sheet.Grid.Rows--;
}
```

### 5種それぞれの削除条件（事実）

| # | 型 | 削除条件 | 削除されない場合の扱い |
|---|---|---|---|
| 1 | `ElementInstance` | `Pos.Row == targetRow` | 対象行以外は`RowOps.ShiftRows`で通常シフト |
| 2 | `VerticalConnector` | `TopRow == targetRow \|\| BottomRow == targetRow`（**端点のみ**。範囲を跨ぐが端点でない場合は削除されない） | 跨ぐだけのコネクタは端点のみ`ShiftRows`で縮む（例: Top=1,Bottom=5でtargetRow=3を削除→Top=1のままBottom=4、削除されず短くなる） |
| 3 | `WireBreak` | `Row == targetRow` | 対象行以外は通常シフト |
| 4 | `RungComment` | `Row == targetRow` | 対象行以外は通常シフト |
| 5 | `GroupFrame` | `TopLeft.Row == targetRow` → **枠ごと削除**。<br>`TopLeft.Row < targetRow && TopLeft.Row + Height - 1 >= targetRow`（内部にかかる）→ **Height--**（枠ごと削除ではない） | 上記いずれでもない場合（`TopLeft.Row > targetRow`）→ 位置のみ-1シフト。`TopLeft.Row < targetRow`かつ終端行も`< targetRow`（枠が対象行より完全に手前）→ 不変 |

### 実行順序（事実、重要）

1. 4種（ElementInstance/VerticalConnector/WireBreak/RungComment）のうち対象行に該当するものを**先に削除**
2. GroupFrameの削除／Height--判定（対象行に対する判定のみ、まだシフトしていない座標系で判定）
3. `RowOps.ShiftRows`で4種の残存要素を対象行より後ろのみ-1シフト
4. GroupFrameの位置シフト（`TopLeft.Row > targetRow`のみ-1、これも未シフト座標系での判定→シフト実行）

**順序が重要な理由**：GroupFrameの削除／Height--判定は「シフト前の元の座標」で行う必要がある
（先にシフトしてしまうと`TopLeft.Row == targetRow`等の判定基準が変わってしまう）。ecad2の実装でも
この順序を保つ必要がある。

---

## 2. ecad2側 `DeleteRowAtCommand`/`IsRowOccupied` 改修設計方針

### 2.1 現状（変更前）

- `MainWindowViewModel.IsRowOccupied`（`MainWindowViewModel.cs:1306-1311`）：5種いずれかが対象行に
  「かかっていれば」true（GroupFrameは`row >= TopLeft.Row && row < TopLeft.Row + Height`で範囲全体）。
- `DeleteRowAtCommand`（同1703-1716）：`TryRejectOccupiedRow`で`IsRowOccupied`がtrueなら削除を拒否し
  StatusMessageへ警告、`return`。
- `RowOps.DeleteRow`（`RowOps.cs:43-58`）：契約「targetRow行に要素なし」を前提とし、GroupFrameの
  「枠ごと削除」「Height--」は未実装（コメントに明記）。

### 2.2 改修方針

**(a) `IsRowOccupied`自体は変更しない**（増分1`DeleteRowCommand`・増分2`UpdateSheetSettingsCommand`は
現状の拒否のまま、殿裁定の適用範囲限定に従う）。

**(b) `DeleteRowAtCommand`から`TryRejectOccupiedRow`呼び出しを撤廃**し、無条件で
「要素ごと削除」実行へ進む（Grid.Rows下限ガード`sheet.Grid.Rows <= GridSpec.MinRows`は維持）。

**(c) `RowOps.DeleteRow`をGuiEcad同型の「要素ごと削除」に拡張**。1節の実行順序（削除→GroupFrame判定
→4種シフト→GroupFrame位置シフト）をそのまま踏襲する。

**(d) 戻り値の追加が必要**：現状`RowOps.DeleteRow`は`void`だが、削除された`ElementInstance`のうち
`DeviceName`が設定されているものは、呼び出し元（App層）で機器表（`Document.Devices.ByName`）の
クリーンアップが必要になる（`DeleteSelectedElement`、`MainWindowViewModel.cs:1259-1285`に既存の
単一削除時クリーンアップの前例あり：削除要素の`DeviceName`が他のどのシートのどの要素からも
参照されなくなれば`Document.Devices.ByName`から該当エントリを除去）。`RowOps`はCore層
（`Ecad2.Core.Model`名前空間）に属し`Document.Devices`（App層が管理する機器表の概念とは別、
`LadderDocument.Devices`はCore層所属だが操作自体は`MainWindowViewModel`に閉じている）を直接
操作すべきではないため、**`RowOps.DeleteRow`は削除された`ElementInstance`のリスト
（`IReadOnlyList<ElementInstance>`）を戻り値として返し、呼び出し元`DeleteRowAtCommand`側で
`DeleteSelectedElement`と同型の機器表クリーンアップを行う**設計を提案する（GuiEcadにはこの種の
機器表管理の記載がなく参考にできないため、ecad2固有の設計判断）。

**(e) `SelectedCell`追随の扱い（新規論点）**：現状の往復1周目修正（`DeleteRowAtCommand`
`MainWindowViewModel.cs:1710-1711`）は`sc.Row > row`のみ-1シフトし、`sc.Row == row`（削除対象行
そのものを選択中）は不変のまま据え置く。占有拒否ガードがあった旧設計では「対象行を選択していても
要素は消えない」ため据え置きで問題なかったが、**要素ごと削除方式では対象行を選択していた場合、
選択中の要素自体が消滅しうる**。据え置き（`sc.Row == row`のまま不変）を維持しても、`SelectedElement`
は`SelectedCell`からの都度算出のため即座にnullになるだけでクラッシュ等はしないと見られるが、
「選択位置は変えないが指す実体は消える」という挙動が使用感として妥当かは**UI/UX分岐に該当しうるため
殿確認を推奨**する（既存規則の自然な延長＝据え置きのままで良い、という解釈も成立するため、家老の
判断で「据え置きのまま実装→忍者実機確認で違和感があれば再検討」という進め方も可能と考える）。

### 2.3 変更が想定される箇所（設計方針、実装コードそのものではない）

- `src/Ecad2.Core/Model/RowOps.cs`：`DeleteRow`の拡張（戻り値追加、削除ロジック追加、実行順序の並べ替え）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`：`DeleteRowAtCommand`（占有拒否撤廃、`RowOps.DeleteRow`
  戻り値を使った機器表クリーンアップ追加、コメント更新）
- `tests/Ecad2.Core.Tests/RowOpsTests.cs`：`DeleteRow_GroupFrame_TargetEqualsFrameStartRow_...`の更新
  （テストコメントで「将来更新すること」と明記済み）、新規ケース追加（3節参照）
- `tests/Ecad2.App.Tests/`：`DeleteRowAtCommand`の占有時の新挙動テスト（機器表クリーンアップ含む）

---

## 3. テスト設計（同値分割・境界値分析・対称性点検・[Theory]活用）

`onmitsu.md`「テスト設計の起草」節の技法適用。既存`RowOpsTests.cs`のパラメタライズド構造
（4種共通`[Theory][InlineData]`＋GroupFrame個別`[Fact]`）を踏襲する。

### 3.1 同値分割

**4種共通（ElementInstance/WireBreak/RungComment、単一Row値を持つ型）**
- 対象行に要素あり（削除対象そのもの）→ **削除される**（新規）
- 対象行より後ろに要素あり → -1シフト（既存カバー済み）
- 対象行より前に要素あり → 不変（既存カバー済み）

**VerticalConnector（2値Top/Bottomを持つ、他3種と非対称）**
- `TopRow == targetRow` または `BottomRow == targetRow`（端点一致）→ **削除される**（新規）
- 範囲が対象行を跨ぐが端点でない（`TopRow < targetRow < BottomRow`）→ 削除されず、対象行より後ろの
  端点のみ-1シフト（新規、GuiEcad同型の非自明な挙動のため明示テストが必要）
- 両端点とも対象行より前 → 不変（既存カバー済み）
- 両端点とも対象行より後ろ → 両方-1シフト（既存カバー済み）

**GroupFrame（開始行＋Height、3値化した状態遷移的構造）**
- 開始行 == 対象行 → **枠ごと削除**（新規、既存テストは「現状無変化」を検証しており更新要）
- 開始行 < 対象行 かつ 終端行（開始行+Height-1）>= 対象行（内部にかかる）→ **Height--**（新規、
  現状のRowOps.DeleteRowには当該分岐自体が無い）
- 開始行 > 対象行 → 位置のみ-1シフト（既存カバー済み）
- 開始行 < 対象行 かつ 終端行 < 対象行（枠が対象行より完全に手前）→ 不変（既存カバー済み）

### 3.2 境界値分析

| # | ケース | 入力 | 期待結果 |
|---|---|---|---|
| B1 | GroupFrame、Height=1（最小）の枠が削除対象そのもの | `TopLeft.Row=3, Height=1`, `targetRow=3` | 枠ごと削除（Height--で0にする経路ではなく削除経路であることを明示的に確認） |
| B2 | GroupFrame、対象行が終端行ちょうど | `TopLeft.Row=3, Height=3`(範囲3-5), `targetRow=5` | `Height=2`（内部詰め、開始行不変） |
| B3 | GroupFrame、対象行が開始行の直後（内部の最初の行） | `TopLeft.Row=3, Height=3`, `targetRow=4` | `Height=2`（内部詰め） |
| B4 | GroupFrame、対象行が開始行の直前 | `TopLeft.Row=3, Height=3`, `targetRow=2` | 位置のみ-1（`TopLeft.Row=2`、Height=3不変） |
| B5 | VerticalConnector、TopRow==targetRow（上端一致） | `TopRow=3, BottomRow=6`, `targetRow=3` | 削除される |
| B6 | VerticalConnector、BottomRow==targetRow（下端一致） | `TopRow=1, BottomRow=3`, `targetRow=3` | 削除される |
| B7 | VerticalConnector、範囲内だが端点でない（跨ぐのみ） | `TopRow=1, BottomRow=5`, `targetRow=3` | 削除されない、`TopRow=1`不変・`BottomRow=4`（-1） |
| B8 | Grid.Rows下限（MinRows=1）到達直前での削除 | `Grid.Rows=2`, 削除実行 | 削除後`Grid.Rows=1`（下限ちょうど、クランプではなく通常減算） |
| B9 | 同一行に5種すべてが同時存在 | 各型を`targetRow`に配置 | 全種が削除される（複合ケース、対称性点検） |

### 3.3 対称性点検

既存4種（ElementInstance/WireBreak/RungComment＋VerticalConnectorの端点判定）は同じ
「対象行==削除」規則を持つため、`[Theory][InlineData]`で1つのテストメソッドに統合できる
（`PlaceElementAt`ヘルパーの拡張で対応可能、VerticalConnectorのみTopRow/BottomRow個別ケースを
追加の`[Theory]`で補う）。GroupFrameは3値構造（開始行・Height・削除/縮小/シフトの分岐）のため
個別`[Fact]`のまま。この構造は既存`RowOpsTests.cs`の書式と一致させること。

### 3.4 パラメタライズド活用（xUnit `[Theory]`+`[InlineData]`）

```
[Theory]
[InlineData("ElementInstance")]
[InlineData("WireBreak")]
[InlineData("RungComment")]
public void DeleteRow_RemovesElementAtTargetRow(string elementType) { ... }
```
のような、既存`DeleteRow_ShiftsElementAfterTargetRow`等と同じ命名規則・同じ`PlaceElementAt`/`GetRow`
ヘルパー活用を推奨（`VerticalConnector`は端点条件の非対称性ゆえ本Theoryの対象から外し、B5/B6/B7を
個別`[Fact]`で扱う）。

### 3.5 状態遷移分析について

`RowOps.DeleteRow`自体は単発の値変更操作であり、複数状態を持つステートマシンではないため、本設計では
状態遷移技法の適用対象外と判断する（誤って技法リストを機械的に当てはめない）。ただし、`DeleteRowAtCommand`
呼び出し前後の`CanExecute`→`Execute`→`StatusMessage`/`SelectedCell`更新という一連の流れは「1操作
1遷移」であり、複雑な遷移表を要する構造ではない。

### 3.6 App層テスト（`DeleteRowAtCommand`）

- 占有行（5種いずれか）を削除実行し、StatusMessageへの拒否文言が**出ないこと**（旧挙動からの転換確認）
- 削除されたElementInstanceの`DeviceName`が他要素から参照されなくなった場合、`Document.Devices.ByName`
  から該当エントリが除去されること（2.2(d)の機器表クリーンアップ、`DeleteSelectedElement`の既存
  テストパターンに倣う）
- `DeviceName`が他シートの別要素からも参照されている場合は機器表エントリが**残ること**（境界値、
  `DeleteSelectedElement`同様の「他参照あり」ケース）

---

## 4. 不明点・要確認事項

- **2.2(e) SelectedCellの扱い**：対象行そのものを選択中に要素ごと削除する場合の据え置き挙動が妥当か。
  UI/UX分岐に該当しうるため殿確認を推奨するか、家老裁量で「据え置きのまま実装→忍者実機確認」で
  進めるかは家老判断に委ねる。
- GuiEcadのコードには機器表（ecad2の`Document.Devices`相当）の管理記載が見当たらず、2.2(d)の
  クリーンアップ設計はecad2独自の判断（`DeleteSelectedElement`の前例踏襲）。GuiEcad側に対応する
  概念が存在するかどうかは本調査スコープ外で未確認。

---

## 出典
- GuiEcad: `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\ElementCommands.cs:389-473`
- ecad2: `src/Ecad2.Core/Model/RowOps.cs`、`src/Ecad2.Core/Model/Element.cs:121-163`、
  `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1259-1285,1300-1323,1703-1716`、
  `tests/Ecad2.Core.Tests/RowOpsTests.cs`
- 背景：`docs/todo.md`T-055節、`docs/ecad2-t055-implementation-plan-samurai.md`、
  `docs/ecad2-t055-increment3-precheck-onmitsu.md`、`docs/ecad2-t055-increment3-round1-review-onmitsu.md`、
  `docs-notes/handover-next-session.md`
