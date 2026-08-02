# T-132増分2 テスト設計書：`IsColumnOccupied` （隠密）

起草日: 2026-08-02　起草者: 隠密（key=1785673319031より後継、本文中は「隠密」と表記）
依頼元: 家老　対象: `docs/ecad2-t132-columns-powerlabel-plan-samurai.md` §3増分2
探索範囲: `MainWindowViewModel.cs`（`IsRowOccupied`実装・`:2686-2691`）、`Element.cs`（`VerticalConnector`
`:199-205`、`WireBreak``:210-217`、`ElementInstance.ContainsRow``:129`）、`RowCommandsTests.cs`（既存行側テスト
`:363-385`）を`Read`／`Grep`で確認。一次ソース精読のみ、実機・ビルドは行っていない（静的検討）。

---

## 0. 結論（先に書く）

**家老DoD3「行側`IsRowOccupied`が同じ問いをどう解いておるか確かめられたい」への答え＝
行側はこの問いを一度も解いていない。** 境界と列（整数区画）の対応という問題は、列側で初めて生じる
新規の問いであり、行側に倣うべき前例は存在しない。理由は§1で示す。

ゆえに侍の問い（境界`b`が列`floor(b)`と`ceil(b)-1`のいずれ、または両方に掛かるか）は隠密が新たに
設計せねばならぬ。§2で対応案を示し、§3〜6でその案を検証するテストを設計する。

---

## 1. 行側は`Column`／`Boundary`を一切見ていない（非対称の事実）

`IsRowOccupied`（`MainWindowViewModel.cs:2686-2691`）：

```csharp
internal static bool IsRowOccupied(Sheet sheet, int row)
    => sheet.Elements.Any(e => e.ContainsRow(row))
        || sheet.Connectors.Any(c => Math.Min(c.TopRow, c.BottomRow) <= row && row <= Math.Max(c.TopRow, c.BottomRow))
        || sheet.WireBreaks.Any(w => w.Row == row)
        || sheet.Frames.Any(f => row >= f.TopLeft.Row && row < f.TopLeft.Row + f.Height)
        || sheet.RungComments.Any(rc => rc.Row == row);
```

`VerticalConnector`（`Element.cs:199-205`）は`Column`（double、列境界）と`TopRow`/`BottomRow`（int、行範囲）
を別々に持つ。`IsRowOccupied`が判定に使うのは`TopRow`/`BottomRow`のみで、`Column`は一度も参照されない。
同様に`WireBreak`（`:210-217`）は`Boundary`（double、列境界）と`Row`（int、単一行）を持つが、
`IsRowOccupied`が使うのは`Row`のみで`Boundary`は参照されない。

**すなわち縦コネクタ・分断マークは「行方向は範囲または単一行の整数」「列方向は単一点のdouble」という
非対称な構造を最初から持っており、行側の実装はこの非対称性を「列方向を見ない」ことで単純に回避している。**
列側で対称的に実装しようとした瞬間、行側には存在しなかった「単一点の境界をどの整数列区画に属させるか」
という変換問題が初めて生じる。これが侍の問いの正体である。

**Elements・Frames（列方向も整数）は非対称ではない**——`e.Pos.Column`＋`e.CellWidth`、
`f.TopLeft.Column`＋`f.Width`はいずれも列側でも整数のまま扱え、行側の`ContainsRow`（中心対称、
`Pos.Row - RowSpan <= row && row <= Pos.Row + RowSpan`）とは判定方式こそ異なるが
（侍の計画書§3で列側は左上原点の半開区間`[Column, Column+CellWidth)`と明記済み、これは意図的な既存差）、
「境界→列の変換」という新規問題は抱えていない。**新規の問いが生じるのは`Connectors`・`WireBreaks`の2種のみ。**

---

## 2. 対応案【家老裁定2026-08-02＝採用確定】

**家老裁定**：本節の案を採る。片側のみに倒す対案（`floor(b)`のみ等）は「消してはならない列を消せてしまう」
データ損壊方向の事故に届くため、対等な選択肢として殿へ諮ることなく退けられた
（過剰検出寄りは「消せるはずが消せぬ」不便止まりであり、両者は重みが非対称）。
以下は元の提案時の記述。

列`c`が占める区間を閉区間`[c, c+1]`（侍の計画書の定義どおり）としたとき、境界`b`が列`c`に「掛かる」条件を

```csharp
c <= b && b <= c + 1
```

とする（閉区間の両端含む）。この一本の不等式で、侍が挙げた2ケースが自然に導かれる：

| `b`の形 | 判定結果 |
|---|---|
| 整数境界ちょうど（例: `b = 3`） | 列2の判定でも`3 <= 3`成立、列3の判定でも`3 <= 3`成立——**両方に掛かる** |
| セル中央 X.5（例: `b = 3.5`） | 列2の判定は`3.5 <= 3`不成立、列3の判定は`3 <= 3.5 <= 4`成立——**列3のみ一意に掛かる** |

**この案を推す理由**：整数境界を「両方に掛かる」とみなすのは、見落とし方向（漏れ）ではなく
過剰検出方向（誤爆）に倒す設計であり、`memory: feedback_geometric_transform_endpoint_oversight`
（制御点だけ変え端点を変え忘れる＝見落とし型の事故）を構造的に避けられる。増分4での用途は
「列を縮小してよいか」の拒否判定であり、過剰検出は「本当は消せる列を消せないと誤判定する」
不便に留まるが、見落としは「消してはならない列を消してしまう」データ損壊に至る。**双方が
同じ重さの誤りではない以上、安全側（過剰検出寄り）に倒すのが妥当**——ただしこれは隠密の推奨であり、
「両方に掛かる」で本当に良いか（例えば境界ちょうどを片側だけに帰属させたい別の設計思想があるか）は
侍・家老の裁定を要する。

---

## 3. 観点A（同値分割）——4種それぞれの「列内／列外」

行側`IsRowOccupied_ReturnsTrue_WhenElementAtRow`（`RowCommandsTests.cs:363-374`）と対称の形。
対象は`Elements`／`Connectors`／`WireBreaks`／`Frames`の4種（`RungComments`は列座標を持たぬため対象外、
侍の計画書§2-1のとおり）。

```csharp
[Theory]
[InlineData("ElementInstance")]
[InlineData("VerticalConnector")]
[InlineData("WireBreak")]
[InlineData("GroupFrame")]
public void IsColumnOccupied_ReturnsTrue_WhenElementAtColumn(string elementType)

[Fact]
public void IsColumnOccupied_ReturnsFalse_WhenColumnEmpty()
```

`VerticalConnector`／`WireBreak`をこのTheoryに含める際は、§4の境界問題を持ち込まぬよう
**列境界がちょうど整数（＝対象列の左端）となる配置**で置く（もっとも素直に「列内」と判じられる形）。
セル中央・整数境界の作り分けは観点Bで別途検証する。

---

## 4. 観点B（境界値分析）——`Connectors`／`WireBreaks`固有

§2の対応案が正しく働くかを、両種について同型に検証する。

```csharp
// VerticalConnector: 整数境界(b=3)は列2・列3の両方にヒットする
[Theory]
[InlineData(2, true)]
[InlineData(3, true)]
[InlineData(1, false)]
[InlineData(4, false)]
public void IsColumnOccupied_Connector_AtIntegerBoundary_HitsBothAdjacentColumns(int column, bool expected)

// VerticalConnector: セル中央境界(b=3.5)は列3のみ一意にヒットする
[Theory]
[InlineData(2, false)]
[InlineData(3, true)]
[InlineData(4, false)]
public void IsColumnOccupied_Connector_AtHalfBoundary_HitsOnlyOneColumn(int column, bool expected)

// WireBreak: 上記2つと同型（境界の意味はVerticalConnectorと同じ、コメント`:209`参照）
[Theory] ... IsColumnOccupied_WireBreak_AtIntegerBoundary_HitsBothAdjacentColumns
[Theory] ... IsColumnOccupied_WireBreak_AtHalfBoundary_HitsOnlyOneColumn
```

**ここが本増分でもっとも事故りやすい箇所**（家老が引いた`memory: feedback_geometric_transform_endpoint_oversight`
そのもの）。整数境界ケースを`InlineData`で明示し、"両側にヒットする"ことをテストが真に検証していることを
確認する——実装が誤って片側だけ（例えば`floor`のみ）を採ってしまっても、境界がX.5のケースだけでは
検出できない。**両ケースを必ず対にして書くこと。**

---

## 5. 観点C（`Elements`／`Frames`の列幅境界）

侍の計画書の式（`e.Pos.Column <= column && column < e.Pos.Column + e.CellWidth`、
`f.TopLeft.Column <= column && column < f.TopLeft.Column + f.Width`）はいずれも半開区間`[Column, Column+幅)`。
`CellWidth`／`Width`が2以上の場合の始端・終端・外側をTheoryで確認する。

```csharp
// ElementInstance: Pos.Column=3, CellWidth=2 → 占有列は{3,4}、5は含まれない
[Theory]
[InlineData(2, false)]
[InlineData(3, true)]
[InlineData(4, true)]
[InlineData(5, false)]
public void IsColumnOccupied_Element_RespectsCellWidth(int column, bool expected)

// GroupFrame: 同型
[Theory] ... IsColumnOccupied_Frame_RespectsWidth
```

半開区間の終端（`column < Pos.Column + CellWidth`）が誤って閉区間（`<=`）に実装されないかを
このTheoryが検出する。

---

## 6. 観点D（対称性点検）——効く範囲と効かぬ範囲を切り分ける

`onmitsu.md`の対称性点検は、本増分では**部分的にしか成立しない**ことを先に明記する。

- **Elements／Frames**：行版・列版とも整数区間同士の判定ゆえ、同一シートに同型の配置をして
  `IsRowOccupied`と`IsColumnOccupied`が対応する結果を返すことを確認できる（対称性検証が意味を持つ）。
- **Connectors／WireBreaks**：§1のとおり行側はこの問いを一度も解いていない（列方向を見ない）ため、
  「行側と対応する結果になるべきだ」という基準自体が存在しない。**この2種について対称性テストを
  書こうとしてはならない**——書けば「何と比べて対称であるべきか」が定義できず、テストの意図が
  空虚になる。

```csharp
[Fact]
public void IsColumnOccupied_And_IsRowOccupied_AgreeOnElementPlacement()
// Element/Frameのみを対象に、同一シートで両者を呼び結果が対応することを確認
```

---

## 7. 観点E（誤爆防止）

列`c`にのみ要素を置き、隣接列`c-1`／`c+1`では（§4の整数境界ケースを除き）`false`になることを確認。
既存の`IsRowOccupied_ReturnsFalse_WhenRowEmpty`（`RowCommandsTests.cs:377-384`）と対称の形で足りる。
§3のTheoryの`InlineData(false)`側と実質重複するため、独立のテストとしては起こさず、
§3・§4・§5の否定ケースで代替する（無駄な重複を避ける、`onmitsu.md`の簡潔性の規範）。

---

## 8. RED先行証明の可否（DoD4）

`IsColumnOccupied`は侍の計画書どおり新設の純粋関数であり、呼び出し元（増分4）を待たず単体で
呼べる（`IsRowOccupied`と同じ形）。**RED先行証明は可能**——ただし新設メソッドゆえ、
「メソッドが存在しない」段階ではテストコード自体がコンパイルできない
（`memory: feedback_red_proof_new_api_limitation`と同型の制約）。

**手順**：(1) 侍が`IsColumnOccupied`を空実装（例えば常に`false`を返す、あるいは`Elements`のみ見て
`Connectors`/`WireBreaks`/`Frames`を見ない仮実装）で先に置く → (2) 本テスト設計のTheoryを実装し、
真の実装に対する期待値で走らせREDを確認する（境界値テスト・誤爆防止テストが正しく落ちることを
実測で確かめる）→ (3) 真の実装に差し替えGREENへ。**とりわけ§4の整数境界Theoryは、
「両方にヒットする」を要求するテストが空実装（`false`固定）に対して確実にREDになることを
実測で確認されたい**——ここが本増分でもっとも実測の値打ちが高い箇所である。

---

## 9. スコープ境界

**含むもの**：`IsColumnOccupied`単体のテスト設計（観点A〜E）のみ。

**含まないもの**：
- 増分4（`UpdateSheetSettingsCommand`拡張・呼び出し側テスト）——侍の計画書が意図的に別増分とした境界
  （`samurai.md`【MUST】＝述語を切り出したら呼び出し側にもテストを置く、T-125増分αの教訓）を尊重し、
  本設計書はそちらへ踏み込まない
- mm座標要素（`FreeLines`／`ConnectionDots`／`Images`／`Frames.VisualXMm`）の扱い——殿ご裁定（案1）
  により対象外、蒸し返さない
- ~~§2の対応案の採否そのものの決定——隠密は提案のみ、裁定は侍・家老に委ねる~~
  → **家老裁定2026-08-02で採用確定済み（§2参照）**

---

## 10. 事実と推測の峻別

**事実**（一次ソースで確認済み）：
- `IsRowOccupied`の実装内容（§1のコード引用）
- `VerticalConnector`／`WireBreak`のフィールド構成（`Column`/`TopRow`/`BottomRow`、`Boundary`/`Row`）
- 既存の行側テストの形式（`RowCommandsTests.cs:363-385`）

**推測・提案として出したもの**（家老裁定2026-08-02で採用確定済み）：
- §2の対応案（`c <= b && b <= c + 1`、整数境界は両側にヒット）
- 過剰検出寄りに倒すべきという優先順位の判断（データ損壊 > 不便、という重み付け）

**未実施**：実機確認・ビルド確認は行っていない。静的な一次ソース精読とテスト設計のみ。
