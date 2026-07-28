# 采配2 — `PartResolver.BoundarySpan` の害の有無と、同軸の漏れ（T-133増分3の網から漏れた箇所）

作成: 2026-07-28（隠密 key=1785225739418）
発端: 新しい侍が増分4の下調べで `BoundarySpan` が高さを見ぬことを発見、家老が調査を采配
行番号は本調査時点（`b05c59d`）のもの。**本日だけで複数回シフトしておるゆえ、引く際は都度取り直されたい。**

---

## 結論（先に述べる）

| DoD | 結論 |
|---|---|
| 1. `BoundarySpan` が高さを見ぬ害 | **害は無い。** しかも「小さい」ではなく**「分岐自体が到達不能」**とより強く言える |
| 2. 呼び出し元 | **2件のみ**（定義1＋呼び出し1）。**高さを要する経路は無し** |
| 3.【裏の的】同軸の漏れ | **あった。しかも `BoundarySpan` より重い**——**確定3件＋要判断3群** |

**そして家老が想定された軸（`Ports` 経由・`CellWidth` 参照）では、その3件は掛かり申さぬ。**
**効いた軸は `Pos.Row ==`（行の一致比較）にござった。**

---

## DoD1・DoD2 — `BoundarySpan` に害は無い

### 呼び出し元の全数

**再現手段＝`Grep "BoundarySpan" src/` → 2件**

| # | 箇所 | 中身 |
|---|---|---|
| 1 | `PartResolver.cs:18` | 定義 |
| 2 | `DiagramRenderer.cs:1097` | **唯一の呼び出し**（`DrawTimerCountdowns` 内） |

### 害が無いと判ずる3つの理由

**理由1＝そもそも列だけを返す関数である。**
戻り値が `(int Left, int Right)`＝**列境界**にて、docコメントも
「要素が占める**列境界**の左端・右端」と明示しておる（`:17`）。**行は返さぬ設計。**

**理由2＝唯一の呼び出し元が、水平中心の算出にしか使っておらぬ。**
`DiagramRenderer.cs:1097-1099`——
```csharp
var (l, right) = PartResolver.BoundarySpan(e, _lib);
double cx = (X(l) + X(right)) / 2;          // ← BoundarySpan は cx だけに効く
double cy = YRow(e.Pos.Row) - Cell * 1.15;  // ← 垂直位置は e.Pos.Row から直接
```
**垂直位置 `cy` は `e.Pos.Row` から直に算出しており、`BoundarySpan` を経ておらぬ。**

**理由3＝侍が案じられた `ports.Count == 0` 分岐（3極記号を名指しする分岐、`:21-22`）は、
この呼び出し元からは到達不能である。**

`DrawTimerCountdowns` は `:1089` で
`kind is not (ElementKind.TimerContactNO or ElementKind.TimerContactNC)` を `continue` で弾く。
**タイマ接点は `ElementCatalog.Ports`（`:38-54`）の既定分岐 `_` に落ち、必ず2ポート（L/R）を持つ**——
ポート0になるのは `Breaker3P` / `ContactorMain3P` / `ThermalOverload3P` の3極記号のみ（`:47-48`）で、
**それらはタイマ接点ではないゆえ `:1089` で弾かれる。**

**【留保】自作パーツ側は断定を控える。** `PartRole.TimerContactNO/NC` のパーツが `Ports` を空に持てば
理論上は到達するが、**パーツエディタが接続点2つ以上を要求する**（引き継ぎ書§6の忍者の観察）ゆえ
実際には作れぬ見込みにござる。**ただしこれは二次情報にて、隠密は一次で確かめておらぬ。**

### 侍の見立てとの異同

**侍の見立て＝「用途は配線・境界計算にて、3極記号は自由配線で結ぶ設計ゆえ実害は小さい」。**

**結論は当たっておるが、理由が違い申す。**
**実際の唯一の用途は「タイマ残り時間の小窓の水平中心」であり、配線・境界計算ではござらぬ。**
そして**「実害は小さい」ではなく「分岐自体が到達不能」**と、より強く言える。

**なお侍が「本人の見立てにすぎ申さぬ」と自ら区切られたのは正しい所作**にござった——
**区切っておられたゆえ、こちらは理由の側を疑って読めた。**

---

## DoD3【裏の的】— 同軸の漏れは、あった

### 効いた軸は `Pos.Row ==`（行の一致比較）

**再現手段＝`Grep "Pos\.Row ==|\.Row == .*Pos\.Row" src/` → 9件**
（うち1件は増分3が残したコメント `MainWindowViewModel.cs:2857` ゆえ、実コードは8件）

**家老が想定された軸（`Ports` 経由・`CellWidth` 参照）では、下記3件は1件も掛かり申さぬ。**
いずれも `Ports` を経ず、`CellWidth` すら見ておらぬ箇所ゆえにござる。

### 確定の漏れ3件

#### 漏れ1【重い】`MainWindowViewModel.cs:2675` — `IsRowOccupied`（行削除の拒否判定）

```csharp
internal static bool IsRowOccupied(Sheet sheet, int row)
    => sheet.Elements.Any(e => e.Pos.Row == row)                                          // ← 一致比較
        || sheet.Connectors.Any(c => Math.Min(c.TopRow, c.BottomRow) <= row && ...)        // ← 範囲
        || sheet.WireBreaks.Any(w => w.Row == row)
        || sheet.Frames.Any(f => row >= f.TopLeft.Row && row < f.TopLeft.Row + f.Height)   // ← 範囲
        || sheet.RungComments.Any(rc => rc.Row == row);
```

**同じ関数の中で、`Connectors` と `Frames` は範囲で見ておるのに、`Elements` だけが一致比較にござる。**
（`WireBreaks`・`RungComments` は点系プリミティブゆえ一致比較で正しい）

**実害（増分4以降）**＝高さ2の要素の**上下の占有行が「空き」と判定され、行削除が通る**。

**なお `IsOccupied`（`:2835`、増分3で高さ対応済み）と名前も役目も酷似しておるのに、
増分3は後者だけを直しておる。**

#### 漏れ2【最も重い】`RowOps.cs:50` — `DeleteRow`（行削除の実行）

```csharp
var removedElements = sheet.Elements.Where(e => e.Pos.Row == targetRow).ToList();
```

**同メソッドの docコメント（`:42-45`）は、他種別については範囲の扱いを事細かに定めておる**——
「`VerticalConnector` は端点が一致する場合のみ削除。**範囲が跨ぐだけなら削除されず端点のみシフト**」
「`GroupFrame` は開始行が一致すれば枠ごと削除、**跨ぐなら内部詰め（`Height--`）**」。
**`ElementInstance` だけが、その扱いを持たぬ。**

**実害（増分4以降）**＝高さ2の要素の**上下占有行を削除しても要素は消えず、
かつ後続の行シフトで周囲だけがずれる**。**要素の占有していた3行が2行になるのに、要素の高さは2のまま**——
**表示と占有の食い違いが残る。**

**漏れ1と連鎖する**——`IsRowOccupied` が上下行を「空き」と判ずるゆえ、**この経路へ到達しうる。**

#### 漏れ3 `MainWindow.xaml.cs:1666` — 要素ドラッグの掴み判定

```csharp
if (elemHitPos.Row == dragElem.Pos.Row                                    // ← 行は一致比較
    && elemHitPos.Column >= dragElem.Pos.Column
    && elemHitPos.Column <= dragElem.Pos.Column + dragElem.CellWidth - 1) // ← 列は区間包含
```

**列は `CellWidth` の区間包含で `HitTestElement` と同型に判定しておるのに、行だけが取り残されておる。**

**実害（増分4以降）**＝高さ2の要素を選択し、**その上下の占有行を押下してもドラッグが始まらぬ**。
**ヒットテストでは選択できるのに掴めぬ**という食い違いになる。

**本件は `HitTestElement` を呼ばず、同じ判定を手書きで重複させておる**——
**台帳 PR-22候補「左右クリックのヒットテスト優先順位ロジックの手書き重複
（single source of truth 不在）」に該当する疑い**がござる。

### 要判断の3群（隠密は漏れと断定せぬ）

| # | 箇所 | 中身 | なぜ断定せぬか |
|---|---|---|---|
| 4 | `MainWindowViewModel.cs:3078, 3083` | `BuildOrJoinCandidates`（OR合流先候補） | **高さ2の要素が上下行でも「合流先候補」になるべきかは設計判断**にて、実装の穴とは限らぬ |
| 5 | `MainWindowViewModel.cs:3113` | `NothingBetweenRailAndColumn`（母線間の要素有無） | **高さ2の要素が上下行を塞ぐとみなすべきかは同じく設計判断** |
| 6 | `OutputPanelViewModel.cs:102, 104, 106` | DRC結果の行番号から要素を引く | **アンカー行で足りる公算が高いが、一次で確かめておらぬ** |

---

## 本件が示すもの——スコープをメソッド名で列挙した穴

**増分3のスコープ定義は
「`IsWithinGridBounds`・`IsOccupied`・`HitTestElement`・`ValidatePlacement` に高さを通す」
（`ecad2-t133-implementation-plan-samurai.md:236`）とメソッド名で列挙されておった。**

**名指しされた4つは正しく直り、実装にも瑕疵は無い**（本日の静的レビューで確定済み、
`docs/ecad2-t133-increment3-p148-review-onmitsu.md`）。
**漏れたのは、同じ判定を別の名で・別の場所に持っておった箇所にござる。**

**計画書§増分0の留保「軸が違えば網も違う」が、ここでも当たった**——
**増分0は `.Kind` 直接参照16箇所を軸に列挙し、`Ports` 経由の `BoundarySpan` を逃した。**
**増分3はメソッド名を軸に列挙し、`Pos.Row ==` の手書き判定3件を逃した。**
**同じ形の穴が、軸を変えて二度出ておる。**

**具申（事実と分けて述べる）**——次に「高さを通す」類の作業を計画する際は、
**メソッド名でなく「行を判定しておる式」を軸に洗い出す**のが確実にござる
（本調査の再現手段がそのまま使える）。

---

## 判断の材料（(a)(b)(c) の別）

**隠密は事実として下記を述べ、推奨は最後に分けて記す。**

**事実**——
- 漏れ1・2（行削除の拒否と実行）は**対になっており、片方だけ直すと不整合が残る**
- 漏れ3（ドラッグ掴み）は**`HitTestElement` と同じ判定の手書き重複**であり、
  **高さを通すだけでなく `HitTestElement` へ寄せる選択肢もある**
- **3件とも、実害の顕在化は増分4以降**（現時点で高さ2の要素は1つも置けぬ）
- 要判断3群は**設計判断を含むゆえ、実装前に方針を定める要がある**

**推奨（隠密の見立て）**——
- **漏れ1・2は増分4の範囲に含めるのが筋**にござる。**増分4で高さ2の記号が置けるようになった瞬間、
  行削除で要素が壊れる**ゆえ、**同じ増分で塞がねば壊れた状態が世に出る**
- **漏れ3は別増分でも差し支えなし**——**壊れるのでなく「掴めぬ」だけ**ゆえ、実害の質が違う
- **要判断3群は殿へ諮る筋**（合流先候補・母線判定に高さをどう効かせるかは使い勝手の話にて、
  `memory: feedback_route_design_decisions_to_user`）

**採否は家老の裁きに委ね申す。**
