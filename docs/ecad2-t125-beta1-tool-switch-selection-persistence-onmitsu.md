# T-125増分β-1：Tool切替時のSelectedCell/SelectedFrame/SelectedElement保持調査（隠密）

> 2026-07-27 隠密調査。家老采配「侍の疑義（β前段調査のシナリオは記述のままでは成立せぬ疑い濃厚）
> を、Tool切替の観点から再検証せよ」を受けての一次ソース調査。**調査のみ、実装はしていない。**

---

## 総括：侍の疑義は正しい。拙者の前回シナリオ（Select中だけで完結）は成立しない。

**自己訂正を先に述べる。** 拙者の前回調査（`ecad2-t125-beta-p077-investigation-onmitsu.md`3節）で
示した「要素Aを選択→枠Bをダブルクリックして編集起動→編集終了→要素Aをクリック」というシナリオは、
**物理的なダブルクリック操作として実行すると成立しない**。理由は、ダブルクリックが「1回目Down→
1回目Up→2回目Down→2回目Up」という4イベントの連鎖であり、**1回目のUp処理（`MainWindow.xaml.cs:2006-2021`、
Tool.Mode==Select限定の通常クリック選択処理）が、2回目のDown（`OpenFrameLabelEditor`起動）より
先に発火し、そこで`SelectedCell=null; SelectedFrame=frame;`が実行されてしまう**——この時点で
`SelectedElement`（Aの選択）は`SelectedCell=null`経由で失われる。**「要素Aを選択したまま枠Bを
ダブルクリック」という状態は、Select中の通常のマウス操作だけでは作れない。**

**ただし、「Tool.Modeを一時的にSelect以外へ切り替える」経路を挟めば、両立状態は実際に作れる。**
**Tool.Modeの切替（setter・ツールバーボタン処理とも）は`SelectedCell`/`SelectedFrame`/
`SelectedElement`のいずれにも一切触れない**ため、Select以外のツールモード中に両立状態を作り、
Selectへ戻せば、二重掴みは成立する。

**結論：DoD(3)「成立せぬなら明言せよ」に該当するのは拙者の前回シナリオ（Select中で完結）のみ。
家老が予告した新経路（Select以外で両立状態を作る→Selectへ戻す）は成立する。**

---

## 1. DoD(1)：Tool.Mode切替時に選択状態はクリアされるか

**クリアされない。** 一次ソースで確認した3箇所すべてが選択状態に触れない：

### `Tool`プロパティのsetter（`MainWindowViewModel.cs:29-47`）

```csharp
public ToolState Tool
{
    get => _tool;
    set
    {
        if (_appMode == AppMode.Test && value.Mode != ToolMode.Select) return;
        object? oldValue = TraceLog.IsEnabled ? _tool : null;
        _tool = value;
        OnPropertyChanged(nameof(Tool), oldValue);
        OnPropertyChanged(nameof(IsPartSelectionVisible));
        OnPropertyChanged(nameof(ActiveToolTag));
    }
}
```

`_tool`の代入と3件の通知のみ。`SelectedCell`/`SelectedFrame`への言及は皆無。

### `CancelResidualDraftForToolSwitch()`（`MainWindowViewModel.cs:1988-1999`）

ツールバーのツール切替ボタン（`ActivateBuiltinTool`等）が呼ぶ一元クリアヘルパーだが、対象は
**「記入中ドラフト」**（縦コネクタ・自由線・画像挿入・枠・合流先確認の5種）のみ。**選択状態
（`SelectedCell`/`SelectedFrame`/`SelectedElement`）は対象外**——ドラフト（未確定の記入作業）と
選択（既存要素・枠の選択）は別の状態区分であり、本ヘルパーは前者専用。

### ツールバーボタン処理（`ActivateBuiltinTool`、`MainWindow.xaml.cs:3312-3323`）

```csharp
_viewModel.CancelResidualDraftForToolSwitch();
_viewModel.Tool = new ViewModels.ToolState(ViewModels.ToolMode.PlaceElement, PartId: entry.Definition.Id, IsOr: isOr);
```

上記2つの組み合わせのみ。選択状態への言及なし。**Escキーでの`Select`復帰も、ドラッグ中の
`CancelDrag*`呼び出し群（`MainWindow.xaml.cs:2505-2578`付近、`IsDraggingConnector`〜
`IsDraggingFrame`まで7種）が中心で、いずれも「ドラッグ中状態」のキャンセルのみを行い選択状態には
触れない**（このEscape分岐は「ドラッグ中でなければ素通り」という設計のため、選択状態を伴わない
通常のTool復帰では作用しない）。

---

## 2. DoD(3)（先に確定）：拙者の前回シナリオが成立しない理由

### 侍の指摘の正体（一次ソースで裏取り）

「左Up」チェーン（`LadderCanvasHost_PreviewMouseLeftButtonUp`、`MainWindow.xaml.cs:1859〜`）の
通常クリック選択処理は、**`Tool.Mode==Select`でガードされている**（2006行目）：

```csharp
if (_viewModel.Tool.Mode == ViewModels.ToolMode.Select && _viewModel.CurrentSheet is Ecad2.Model.Sheet sheet)
{
    if (LadderCanvasHost.HitTestConnector(position, sheet) is ... connector) { ... }
    if (LadderCanvasHost.HitTestFrame(position, sheet) is Ecad2.Model.GroupFrame frame)
    {
        _viewModel.SelectedCell = null;   // ← ここでSelectedElement(A)が失われる
        _viewModel.SelectedFrame = frame;
        return;
    }
    ...
}
```

**枠Bの境界線をクリックすると、Frame単体選択の分岐（`SelectedCell=null`→`SelectedFrame=frame`の
排他クリア順序）にヒットする。** ダブルクリックの**1回目**のUp（`ClickCount`は常に1、WPFの既知
仕様）は、まさにこの通常クリック処理を経由する。**2回目のDown（`ClickCount==2`）で
`OpenFrameLabelEditor`が起動する前に、1回目のUpで`SelectedElement`は既に失われている。**

### 拙者の前回調査の誤り

前回（β前段調査）は「ダブルクリック」を単一の瞬間的操作として扱い、その内部で発生する
「1回目クリックによる通常選択処理」という中間状態を見落としていた。**`OpenFrameLabelEditor`
自体が`SelectedCell`に触れないこと（正しい）と、その手前で通常クリック処理が`SelectedCell`を
書き換えること（見落とし）を混同していた。**

**結論：「Select中に要素Aを選択したまま枠Bをダブルクリックする」という経路は成立しない。**

---

## 3. DoD(2)：成立する経路（Select以外を経由）

以下の操作手順で、二重掴みが実際に成立する（一次ソースの精読による静的確認、実機未検証）：

1. **Select中**に要素A（複数セル幅、境界線が枠Bと重なる位置）を選択
   （`SelectedCell`=Aの位置、`SelectedElement`=A）
2. **ツールバーボタン（F5等）でTool.ModeをSelect以外（例：`PlaceElement`）へ切替**
   （`ActivateBuiltinTool`、選択状態は上記1節のとおり保持される）
3. **Select以外のモード中**、枠Bをダブルクリックする
   - 1回目Down（`ClickCount=1`）：`MainWindow.xaml.cs:1597`の早期returnで各種ドラッグ判定は
     スキップされるが、その手前（1549-1564行）のRungComment/Frame判定（`ClickCount==2`条件）は
     不成立のため何も起きない
   - 1回目Up：`MainWindow.xaml.cs:2006`の`Tool.Mode==Select`ガードにより**通常クリック選択処理は
     完全にスキップされる**——`SelectedCell`は一切変化しない
   - 2回目Down（`ClickCount=2`）：`1559`行目のFrame判定（**ツールモードを問わず**実行）にヒットし
     `OpenFrameLabelEditor(B)`起動、`SelectedFrame=B`
   - この間、**`SelectedElement`（A）は一度も触れられず保持されたまま**
4. 編集を終了（Enter/Tab/Esc）——`CloseFrameLabelEditor`は`SelectedFrame`をクリアしない
   （2節前回調査で確認済み、変更なし）。`SelectedFrame=B`・`SelectedElement=A`が両立したまま
5. **Escキー等でTool.ModeをSelectへ戻す**（選択状態は保持される、1節で確認済み）
6. Select中に要素Aの位置を左クリック（Down）
   - Element判定（`1693-1705`行目相当）にヒットし`BeginDragElement(A)`実行
   - `return`が無いため直後にFrame判定（`1707`行目以降）も評価される
   - **要素Aの位置が枠Bの境界線近傍とも一致すれば、`BeginDragFrame(B)`も同時に実行されうる**
   - → **二重掴みが成立**

**この経路は「Select以外のツールモードを一時的に経由する」という点で、拙者の前回シナリオより
一手余分だが、通常のUI操作（ツールバーボタンでツールを切り替える、Escで戻す）のみで到達可能であり、
特殊な操作・エラー状態を要しない。**

---

## 4. β-1修正（案A、return追加）との関係

家老の采配によれば、侍の修正（`1690-1705`行目のElement判定末尾に`return`を追加、案A）は
「実害の有無によらず進めている」——排他規約を破る唯一の箇所である事実自体は動かないため。

**この案Aは、本調査が確定した「Select以外経由の二重�, り」シナリオの手順6における実害
（`BeginDragElement`と`BeginDragFrame`の同時発火）を直接防ぐ。** ただし、**両立状態自体
（`SelectedFrame=B`かつ`SelectedElement=A`という選択データの不整合）は、案Aだけでは解消されない**
——returnを追加しても、次に「枠Bの境界線上（要素Aとは重ならない位置）」をクリックすれば
Frame単体のドラッグは正常に成立してしまい、両立状態そのものはそのまま残り続ける。

**多重開始ガード（案B）の要否についての所見**：案Aで実害（同時ドラッグ）は防げるため、案Bを
「同時ドラッグの防止」という目的だけで追加する必要性は薄いと考える。ただし、**両立状態自体を
放置してよいか**（`SelectedFrame`と`SelectedElement`が同時に非nullという、プロパティパネル等の
表示ロジックが想定していないかもしれない状態）は、案A・案Bとは別の論点として残る——これは
DoD範囲外の気づきであり、断定はせず家老へ申し送る。

---

## 未確認・留保

- 手順6「要素Aの位置と枠Bの境界線が完全一致する」具体的な配置（GroupFrame＋幅広要素の座標関係）は
  幾何学的に可能と推測するが、実際にそのような配置をecad2の編集操作で作れるかまでは確認していない
  （静的読解のみ）
- 実機での再現は行っていない（忍者領分）
- 両立状態が`SelectedElement`派生プロパティ（`SelectedElementDeviceName`等13個）や右パネル表示に
  どう影響するかは未調査（β計画書DoD(4)がこれらを「γに残す」領域としており、本調査のスコープ外）

---

## 出典

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（29-47行＝`Tool`プロパティ、1988-1999行＝
  `CancelResidualDraftForToolSwitch()`、415-483行＝`SelectedCell`setter、1446-1456行＝
  `SelectedFrame`setter）
- `src/Ecad2.App/MainWindow.xaml.cs`（1501-1710行＝左Down全文、1859-2040行付近＝左Up全文、
  2006-2021行＝Tool.Mode==Selectガード下のFrame単体選択処理、3312-3323行＝`ActivateBuiltinTool`、
  2489-2578行付近＝Escapeハンドラのドラッグキャンセル群、3912-3963行＝`OpenFrameLabelEditor`/
  `CloseFrameLabelEditor`）
- `docs/ecad2-t125-beta-p077-investigation-onmitsu.md`（拙者の前回調査、3節の結論を本調査で訂正）
