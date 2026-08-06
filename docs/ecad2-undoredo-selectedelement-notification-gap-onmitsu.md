# Undo/Redo経路の`SelectedElement`系通知欠落 調査（隠密）

> 2026-08-06 隠密。家老の采配により、前任隠密が`/code-review`実測から拾うた気づき
> （「Undo/Redoでセル座標が不変だと`SetProperty`の構造的等価判定で`SelectedElementBreakerType`等の
> 通知が丸ごと飛ばぬ」）を検分。**前任の報告は正確、実害も具体経路で確定。`PR-17`（`P-169`4経路）とは
> 別筋——手当ては別途要る。**

---

## 1. 前任の報告の正確性——**確認済み**

家老が確かめたのは`GridPos`が`readonly record struct`である一点のみ（`Element.cs:44`）。
残りを一次ソースで裏取りした。

### 機序

`SetProperty<T>`（`ViewModelBase.cs:22-31`）——
```csharp
protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
{
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(propertyName);
    return true;
}
```

`GridPos`は`readonly record struct`（`Element.cs:44`、`Row`/`Column`の値で構造的等価）ゆえ、
`EqualityComparer<GridPos?>.Default.Equals(a, b)`は**値が同じなら参照ではなく値で真になる**。

`ApplyUndoRedoSnapshot`（`MainWindowViewModel.cs:3826-3869`、Undo/Redo双方が呼ぶ共通経路、
`:3795`/`:3807`）——

```csharp
var oldSelectedCell = SelectedCell;       // Document差し替え前に捕捉
Document = restored;
...
SelectedCell = ClampSelectedCellToSheetRows(oldSelectedCell, CurrentSheet);
```

`ClampSelectedCellToSheetRows`（`:3817-3820`）は`pos.Row >= sheet.Grid.Rows`の時のみ`Row`を
クランプし、**それ以外（通常ケース）は引数をそのまま返す**——すなわち`oldSelectedCell`と
**値として完全に同一の`GridPos?`**を`SelectedCell`setterへ渡す。

`SelectedCell`setter（`:443-505`）は`if (SetProperty(ref _selectedCell, value))`で
**15＋`SelectedCellDisplay`＋（段2後は`HasNoPropertySelection`）の通知ブロック全体**を包んでおり
（`:494-503`）、**値が変わらねばこのブロックは丸ごと発火せぬ**。

**ゆえに「Undo/Redoで選択セルの座標そのものは動かない」という通常ケース（＝クランプが要らない
大半のケース）では、`SelectedElement`系の通知が一切飛ばぬ。**

### 実害への到達経路（具体例で確認）

`SelectedElementBreakerType`のsetter（`:2349-2366`）は`UndoManager.RecordSnapshot(Document)`を
呼んでからParamsを書き換える——**Undo対象である**。

1. Breaker3P要素を選択、ComboBoxで種別（NFB→MCCB等）を変更（Undo記録される）
2. `Ctrl+Z`（`UndoCommand`、`:3791-3797`）→`ApplyUndoRedoSnapshot`→**選択セルの座標は不変ゆえ
   `SelectedCell`setterの通知ブロックが発火せず**、`SelectedElementBreakerType`の
   `PropertyChanged`も飛ばぬ
3. `SelectedElementBreakerType`の**値自体**（算出プロパティ、`Params`から都度読み直す）は
   Undo後正しく戻っているが、**ComboBoxはWPFバインドの再評価契機（`PropertyChanged`）を
   受け取らぬためUndo前の値を表示し続ける**（見た目の巻き戻り漏れ）

**`NotifyCurrentSheetDependentPropertiesChanged`（`:395-406`、`ApplyUndoRedoSnapshot`内で
`SetCurrentSheetIndexCore`経由のみ・シート跨ぎUndo時のみ間接発火）は`CurrentSheet`／
シート種別系／`PartPalette`のみを対象とし、`SelectedElement`系は一切含まぬ**——他経路での
救済も無いことを確認した。`OnPropertyChanged(nameof(Document), oldDocument)`もWPF側で
`SelectedElementBreakerType`等（`Document`のサブパスではなくVM直下のプロパティ）を
再評価させぬため無関係。

**結論＝前任の報告は正確。実測せずとも一次ソースのみで確定できる静的な欠陥。**

---

## 2. 実害の範囲——**`SelectedElement`系16件全部、かつ二つの顔を持つ**

### 影響範囲

`SelectedCell`setterの通知ブロックは**単位で丸ごと**発火/不発火するため、影響は
`SelectedElementBreakerType`単独ではなく、**基準15件＋`SelectedCellDisplay`（段2後は
`HasNoPropertySelection`も）全て**に及ぶ——`SelectedElementDeviceName`・`SelectedElementComment`・
`SelectedElementLabelDy`・`SelectedElementSetpoint`等、プロパティパネルの表示項目ほぼ全域。

**引き金となる操作は「選択中の要素・そのセルに対して行った、Undo対象の編集を、選択セルを
動かさずにUndo/Redoする」全て**——`SelectedElementBreakerType`に限らず、`SelectedElementComment`
（コメント編集）等も同型で影響しうる（個々のsetterを網羅的に確認してはおらぬ、下記射程参照）。

### `P-169`と同じ「二つの顔」がここにも現れる

`P-169`の空白型／二重型分類（`docs/ecad2-pr17-consolidation-test-design-onmitsu.md`§3-1）と
同じ構造がUndo/Redo経路にも現れる——

| 型 | シナリオ | 見え方 |
|---|---|---|
| **値の巻き戻り漏れ型** | 要素のプロパティ編集（例＝`SelectedElementBreakerType`）をUndo | ComboBox等が編集後の値のまま表示され続ける（Undo前の値に戻らぬ） |
| **二重型（`P-169`と同型）** | 要素の削除をUndo（削除自体は`DeleteSelectedElement`、Undo対象＝`T-134`殿裁定） | 復活した要素が選択済み座標に戻るのに`HasSelectedElement`等が通知されず、プレースホルダ「要素を選択してください」が消えぬまま要素詳細が出ぬ、または旧表示が残る（段2実装後は`HasNoPropertySelection`もこの型に含まれる） |

**ただし2型目（要素削除のUndo）は本調査で機序を推論したのみで実機未確認**——値の巻き戻り漏れ型
（`SelectedElementBreakerType`）ほど直接には確認しておらぬ。

---

## 3. `PR-17`（`P-169`4経路）との関係——**別筋、段2では塞がらぬ**

家老の見立てどおり別筋と確認した。

| | `P-169`（`PR-17`射程） | 本件（Undo/Redo） |
|---|---|---|
| **機序** | 呼び出しそのものが基準（`NotifySelectedElementChanged`）に無い | 呼び出しは`SelectedCell`setterの通知ブロックへ含まれるが、**`SetProperty`の構造的等価判定でブロック全体が発火しない** |
| **影響範囲** | `HasNoPropertySelection`1プロパティのみ | **通知ブロック全体（15＋`SelectedCellDisplay`、段2後は16）** |
| **`PR-17`段2で塞がるか** | 塞がる（射程そのもの） | **塞がらぬ**——段2は基準関数の中身に1件足すだけで、`SelectedCell`setterの`if(SetProperty(...))`ガード自体には触れぬ |

**すなわち`PR-17`の完了後もこの欠陥は残る。別タスクとしての手当てを要する。**

### 参考＝同じsetter内に既に前例がある

`SelectedCell`setter冒頭（`:448-455`のコメント）——**過去に同型の欠陥
（`CurrentSheetIndex`経由の同一シートジャンプで早期returnにより後続処理が丸ごと飛ばされた実例）
が見つかっており、`SelectedConnector`等の排他クリアは`SetProperty`ガードより前へ意図的に
移設済み**。**されど`SelectedElement`系15件＋`SelectedCellDisplay`のブロックはガードの内側に
残ったまま**——過去の教訓が一部にしか適用されておらなんだ形。

`memory: ecad2_setproperty_early_return_trap`とも同根（**今回は取り違えではなく、
文字どおりそのものにござる**——`PR-17`検分時に家老と拙者が誤って引いた同memoryは、
今回のUndo/Redo経路にこそ正しく当てはまる）。

---

## 4. 射程・限界

- **確認済み＝一次ソースのみ**（`SelectedElementBreakerType`を実例に機序を追い切った）。
  **実機・単体テストでの実測はしておらぬ**——「見た目に反映されぬ」という結論は、
  静的な通知欠落から論理的に導いたものであり、忍者の実機確認を要する
- **`SelectedElementBreakerType`以外の個別setter（`SelectedElementComment`・`SelectedElementLabelDy`等）が
  同じくUndo対象かは1件ずつ確認しておらぬ**——通知ブロックが丸ごと発火せぬという機序は
  `SelectedCell`の値に依存する話ゆえ、Undo対象であるどのプロパティ編集にも共通するはずだが、
  個別の裏取りは今回の射程外
- **「二重型」（削除のUndo）は機序の推論のみ**——`DeleteSelectedElement`がUndo対象になった
  時点（`T-134`殿裁定）以降、実機で確かめられておらぬ
- **実装・修正は本調査の範囲外**（隠密の役儀にあらず）。参考までに、`SelectedCell`setter冒頭の
  排他クリア移設と同型の手当て（`ApplyUndoRedoSnapshot`が`SelectedCell`再代入の成否に関わらず
  `NotifySelectedElementChanged()`を無条件で呼ぶ、`PlaceElementAtSelectedCell`・
  `DeleteRowAtCommand`と同型の作法）が構造的には筋が良さそうに見えるが、**これは観察であって
  提案の確定ではない**。設計判断は殿・家老・侍の領分にござる
