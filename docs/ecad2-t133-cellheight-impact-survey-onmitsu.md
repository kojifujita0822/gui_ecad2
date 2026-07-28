# T-133増分2「CellHeight新設」影響範囲調査（隠密、2026-07-28）

家老采配（増分2の前提調査）。**調査のみ。`src/`・`tests/`への書き込みは行っておらぬ。**
対象コミット＝`fec4117`（T-134完了時点、`main`最新）。

---

## 0. 総括

**既存調査書`docs/ecad2-t133-height-and-port-pitch-survey-onmitsu.md`が、家老の想定（原本側調査）に反し
実は既にecad2側の影響範囲（永続化・DeepClone・4メソッドの改修内容）を厚く扱っておる**（§1参照）。
ゆえに本調査は**既出部分を再掲せず、以下2点にのみ的を絞った**——

1. **既存調査書の1箇所の誤り訂正**（`PartThumbnailRenderer.cs:57`はCellWidthを書いておらぬ）
2. **家老DoD3＝4メソッドの呼び出し元列挙**（既存調査書は「メソッド内で何を直すか」は示すが
   「誰が呼んでおるか」は列挙しておらなんだ。これが本調査の主眼）

**併せて、侍の実装計画（`docs/ecad2-t133-implementation-plan-samurai.md`、コミット`3081cab`時点の行番号）は
T-134のコミット（`fec4117`）で行番号がずれておる**ことを確認した（§4）。増分2着手時は行番号を取り直されたい。

---

## 1.【家老宛】既存調査書との重複関係の訂正

家老の采配文は`docs/ecad2-t133-height-and-port-pitch-survey-onmitsu.md`を
「**貴殿の原本側・高さ/ピッチ調査**」と紹介したが、**実際に読み直したところ内容は原本(GuiEcad)側ではなく
ecad2側の一次ソース（`GridGeometry.cs`・`Element.cs`・`MainWindowViewModel.cs`・`GcadSerializer.cs`等）を
直読した調査であった**。原本仕様の調査は別文書`docs/ecad2-t133-guiecad-other-shapes-survey-onmitsu.md`
（侍の実装計画§6「受け取った調査」欄に別掲）が該当する。

この訂正の実務上の意味＝**家老DoD1（永続化）・DoD2の大半（CellWidth書込箇所の洗い出し）は、
既存調査書の§3・§4で既に相当程度回答済み**にござる。本調査ではその内容を再掲せず、以下を参照されたい——

- **永続化（JSON schema・旧ファイル互換）**＝既存調査書§4全体（4-1安全性の根拠、4-2 SchemaVersionを
  上げてはならぬ、4-4回帰テスト未整備、4-5 Undo/Redoへの波及）
- **DeepClone等のコピー経路の総論**＝既存調査書§4-3（3箇所を列挙、ただし1箇所に誤りあり→次節で訂正）
- **回帰の見取り図（層ごとの判定一覧）**＝既存調査書§3-1（表形式で12件）

---

## 2. 訂正：CellWidthへの書込箇所は3箇所、ただし内訳が異なる

既存調査書§4-3は「追加漏れに注意すべき3箇所」として以下を挙げていた——

1. `Element.cs:58-67` `DeepClone()`
2. `MainWindowViewModel.cs:2924` `new ElementInstance { … }`
3. **`PartThumbnailRenderer.cs:57` `new ElementInstance`**

**3番目を一次ソースで直読したところ、誤りであった。** `src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs:57`——

```csharp
var element = new ElementInstance { PartId = definition.Id, Pos = new GridPos(0, 0) };
```

**`CellWidth`を一切設定しておらぬ（既定値1のまま）。** `src`全体を`CellWidth\s*=`で機械的に
再検索しても同ファイルに`CellWidth`は一切出現しない（0件）。

**訂正後の正しい内訳（3箇所、いずれも直読で確認済み）**——

| # | 箇所 | 内容 |
|---|---|---|
| 1 | `src/Ecad2.Core/Model/Element.cs:50` | プロパティ既定値 `public int CellWidth { get; set; } = 1;` |
| 2 | `src/Ecad2.Core/Model/Element.cs:63` | `DeepClone()`内 `CellWidth = CellWidth`（コピー） |
| 3 | `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:2965` | `PlaceElementAtSelectedCell`内 `new ElementInstance { … CellWidth = cellWidth, … }`（行番号は`2924`から`2965`へ移動、T-134分の増分による） |

**再現手段**＝`src/`配下を`CellWidth\s*=`で機械的に検索（`obj/`・`bin/`除外）、ヒット3件を1件ずつ`Read`で
直読して確認。

**含意**＝`PartThumbnailRenderer.cs`のサムネイル描画は**現状でも実際の`CellWidth`を反映しておらぬ**
（常に既定1相当で描く）。これは**CellHeight新設が新たに生む危険ではなく、既存の振る舞い**であるため、
増分2のDoDとしては「対応不要（現状追認）」と整理してよいと考える（**推測。仕様として意図的か
放置かは不明、家老・侍の判断を仰ぐ**）。

---

## 3.【家老DoD3】増分3で触る4メソッドの呼び出し元列挙——回帰範囲の見取り図

**直読で1件ずつ確認した。以下は全て実際に`Read`で中身まで検めた事実であり、grepのヒット数を
そのまま採用してはおらぬ**（`grep`は候補の絞り込みにのみ使用）。

### 3-1. 直接の呼び出し元（4メソッドを直接呼ぶ箇所）

| メソッド | 定義 | 呼び出し元 | 件数 |
|---|---|---|---|
| `IsWithinGridBounds` | `MainWindowViewModel.cs:2763-2765` | `PlaceWireBreakAtSelectedCell`(:883)／`IsSelectedCellWithinGrid`(:2761、ラッパー)／`ValidatePlacement`(:2890) | **3** |
| `IsOccupied` | `MainWindowViewModel.cs:2797-2802` | `IsSelectedCellOccupied`(:2752、ラッパー)／`ValidatePlacement`(:2890) | **2** |
| `HitTestElement` | `MainWindowViewModel.cs:2807-2812`（public） | `TestModePress`(:2823)／`MainWindow.xaml.cs`の右クリックメニュー構築(:2093)／`ShowTestModeContextMenu`(:2189) | **3** |
| `ValidatePlacement` | `MainWindowViewModel.cs:2889-2890`（private） | `UpdateDragElement`(:1685)／`MoveSelectedElement`(:1726)／`PlaceElementAtSelectedCell`(:2950) | **3** |

**注記**＝侍の実装計画に記された定義行番号（`:2735-2737`等）は**T-134コミットで全てずれておる**
（後述§4）。上表は`fec4117`時点で`Read`し直した現在の行番号。

### 3-2. 間接の呼び出し元——ラッパー2件を経由する層（見落としやすい）

`IsWithinGridBounds`と`IsOccupied`はそれぞれ**View層向けの公開ラッパー**を1つずつ持つ。
このラッパー自体もcellWidthを引数に取り透過的に渡しておるため、**高さ対応時はラッパーのシグネチャにも
`cellHeight`を通す要があるはず**（判断は侍・家老に委ねる。ここでは事実のみ報ずる）。

| ラッパー | 定義 | 呼び出し元（すべて`MainWindow.xaml.cs`） | 件数 |
|---|---|---|---|
| `IsSelectedCellWithinGrid(cellWidth=1)` | `MainWindowViewModel.cs:2760-2761` | `TryPlaceWireBreak`(:3465、`cellWidth`省略＝既定1)／`TryPlaceConnectionDot`(:3546、同左)／`TryPlaceElement`(:3668、`cellWidth`明示) | **3** |
| `IsSelectedCellOccupied(cellWidth=1)` | `MainWindowViewModel.cs:2751-2752` | `TryPlaceElement`(:3673、`cellWidth`明示) | **1** |

**`TryPlaceElement`（`MainWindow.xaml.cs:3657`）は両ラッパーを続けて呼ぶ唯一の箇所**（:3668と:3673）——
配置プレチェック（範囲外→占有済みの順で文言を出し分ける）にござる。

### 3-3. 全体の見取り図（重複除去、実際にコードを変更する必要がありうる箇所）

```
IsWithinGridBounds ─┬─ PlaceWireBreakAtSelectedCell (cellWidth固定1、配線分断=高さ概念なし)
                     ├─ ValidatePlacement ─┬─ UpdateDragElement
                     │                     ├─ MoveSelectedElement
                     │                     └─ PlaceElementAtSelectedCell
                     └─ IsSelectedCellWithinGrid ─┬─ TryPlaceWireBreak (MainWindow.xaml.cs)
                                                   ├─ TryPlaceConnectionDot (MainWindow.xaml.cs)
                                                   └─ TryPlaceElement (MainWindow.xaml.cs)

IsOccupied ─┬─ ValidatePlacement (上記と同一ノード)
            └─ IsSelectedCellOccupied ─── TryPlaceElement (MainWindow.xaml.cs)

HitTestElement ─┬─ TestModePress
                ├─ 右クリックメニュー構築 (MainWindow.xaml.cs:2093)
                └─ ShowTestModeContextMenu (MainWindow.xaml.cs:2189)
```

**要素(`ElementInstance`)を扱う箇所は`element.CellWidth`を経由して自然に`element.CellHeight`へ拡張できる
見込み**（`UpdateDragElement`・`MoveSelectedElement`が好例、既に`element.CellWidth`を渡しておる）。
**一方`PlaceWireBreakAtSelectedCell`・`TryPlaceWireBreak`・`TryPlaceConnectionDot`は`WireBreak`／接続点という
`ElementInstance`ではない点系プリミティブを扱っており、`cellWidth: 1`固定で高さの概念自体が無い**——
**これらは増分3の変更対象外と見立てる**（**推測。侍の設計判断に委ねる**）。

**高さを持つ`ElementInstance`が直接絡む箇所に絞ると、実質的な回帰範囲は
`ValidatePlacement`とその3呼び出し元（ドラッグ・矢印移動・新規配置）＋`HitTestElement`の3呼び出し元
（テストモード左クリック・右クリックメニュー×2）に集約される**。

---

## 4.【気づき】侍の実装計画の行番号がT-134コミットでずれておる

`docs/ecad2-t133-implementation-plan-samurai.md`は**コミット`3081cab`時点**の行番号を根拠に書かれておるが
（同文書冒頭に明記）、**その後`fec4117`（T-134、740行挿入）がマージされ、対象メソッドの行番号は
全てずれておる**——

| メソッド | 計画書記載 | `fec4117`時点の実際 |
|---|---|---|
| `IsWithinGridBounds` | `:2735-2737` | `:2763-2765` |
| `IsOccupied` | `:2769-2774` | `:2797-2802` |
| `HitTestElement` | `:2779-2784` | `:2807-2812` |
| `ValidatePlacement` | `:2861-2862` | `:2889-2890` |

**ずれ幅はいずれも+28行で一定**（T-134が該当箇所より前に28行を挿入したため）。
**増分2・3着手時は、この表の右列（`fec4117`時点）を使われたい。旧行番号のまま照合すると
別のメソッドを指す**（現に`:2861`は本調査時点で`IsRealContactElement`の近傍を指しており、
`ValidatePlacement`とは別のメソッドになっていた）。

→ **落とし先＝本調査書。侍が増分2・3へ着手する際にこの表を参照されたい**（家老経由で申し送り）。

---

## 5. 不明点・未確認

- **ラッパー2件（`IsSelectedCellWithinGrid`・`IsSelectedCellOccupied`）が`cellHeight`を要するか**は
  侍の設計判断。本調査は「現在どちらもcellWidthのみを引数に取る」という事実のみを報ずる
- **`PartThumbnailRenderer.cs`が`CellWidth`を無視する挙動が意図的か放置か**は不明（§2）
- **`PlaceWireBreakAtSelectedCell`等の点系プリミティブが増分3の対象外という見立ては推測**——
  設計判断は侍・家老に委ねる

---

## 出典

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:883, 1685, 1726, 2751-2765, 2797-2802, 2807-2823,
  2889-2890, 2944-2969`（全て`Read`で直読）
- `src/Ecad2.App/MainWindow.xaml.cs:2093, 2189, 3445-3474, 3524-3546, 3657-3677`（全て`Read`で直読）
- `src/Ecad2.Core/Model/Element.cs:44-68`（全文）
- `src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs:47-64`
- `src/Ecad2.Core/Persistence/GcadSerializer.cs`（全文47行、`JsonSerializer`によるリフレクション
  ベースの往復であることを確認、既存調査書§4-1の裏付け）
- 呼び出し元候補の一次絞り込みは`Explore`エージェントへ委譲、結果は本人が`Read`で1件ずつ再確認した
  （`PartThumbnailRenderer.cs:57`の誤り検出はこの再確認で発覚）
- 参照（重複再掲せず）：`docs/ecad2-t133-height-and-port-pitch-survey-onmitsu.md`
