# 無条件通知プロパティのガード化調査（隠密、采配2）

> 2026-08-06 隠密。家老の采配2＝「`HasSelectedImage`等の他の無条件通知プロパティが、同値再代入に
> 依存する箇所が他に無いか」＋「`ReplaceDocument`の依存は段2で"消えた"のか"別の支えが増えた"のか」。
> **調査のみ。実装は殿のお戻り待ち。**

---

## 1. 全数調査——`SetProperty(ref ...)`呼出27箇所を精読

`Ecad2.App`内の全`SetProperty(ref _xxx, value)`呼出（27箇所）を`grep`で洗い、
**`if`で囲まれず（＝戻り値を捨てて呼ぶ）、かつ直後に別プロパティへの`OnPropertyChanged`が
続くもの**（＝「同値でも常に別プロパティを通知する」危険な形）を絞り込んだ。

**該当＝2件のみ**——

| # | 発生源setter | 通知先（無条件） | 消費先 |
|---|---|---|---|
| 1 | `SelectedImage`（`:1301`）・`SelectedFrame`（`:1496`） | `HasSelectedImage`／`HasSelectedFrame`／**`HasNoPropertySelection`** | `MainWindow.xaml:1573-1574`のプレースホルダ`TextBlock` |
| 2 | **【新発見】`SelectedConnector`（`:580-581`）・`SelectedFreeLine`（`:921-922`）** | **`HasSelectedLinePrimitive`** | `MainWindow.xaml:1853/1856`のステータスバー項目・区切り線 |

**残る23箇所**（`SelectedWireBreak`・`SelectedConnectionDot`・`StatusMessage`・`ReplaceWith`・
`Matches`・`Thumbnail`・`IsPlaceable`・`Entries`・`SelectionEntries`等）は、いずれも
`SetProperty`単体の呼出のみで後続の無条件通知を持たぬ——**危険な形はこれ以上見当たらぬ**。

### `HasSelectedLinePrimitive`の構造

```csharp
public bool HasSelectedLinePrimitive => SelectedConnector is not null || SelectedFreeLine is not null;
```

`SelectedConnector`・`SelectedFreeLine`の両setterとも`SetProperty`の戻り値を見ず、
`OnPropertyChanged(nameof(HasSelectedLinePrimitive))`を無条件に呼ぶ——**`HasNoPropertySelection`と
寸分違わぬ構造**。

**`SelectedCell`setter冒頭でも無条件クリアされ**（`:456`/`:460`、`SelectedImage`/`SelectedFrame`と
同じ並びの中）、**`ReplaceDocument`でも直に呼ばれる**（`:3593`/`:3597`）——**`P-169`調査で見つけた
「暗黙の依存」と全く同じパターンが、別のプロパティ・別のUI要素で並行して存在する**。

---

## 2. `ReplaceDocument`の依存は「消えた」か「別の支えが増えた」か——**プロパティごとに答えが違う**

### `HasNoPropertySelection`——**「別の支えが増えた」（依存は残るが冗長化した）**

`ReplaceDocument`は段2の前後を問わず`SelectedImage=null`／`SelectedFrame=null`（`:3567`/`:3571`）を
直に呼び続けており、**このコード自体は段2で一切変わっておらぬ**。段2が足したのは
`NotifySelectedElementChanged()`の末尾への1行であり、`ReplaceDocument`はこの関数を
**無条件で**（`if`に包まれず）呼んでおる。

**すなわち段2後の`ReplaceDocument`は`HasNoPropertySelection`を独立な2経路から受け取る**——
(a) `SelectedImage`/`SelectedFrame`経由の暗黙2回、(b) `NotifySelectedElementChanged()`経由の
明示1回（合計3回、`docs/ecad2-pr17-consolidation-test-design-onmitsu.md`§3-5-b実測）。

**(a)を無条件通知からガード化（`if(SetProperty(...))`）しても、(b)が独立に生き残るため
`ReplaceDocument`の`HasNoPropertySelection`通知は失われぬ**——**この一点に限れば、
ガード化は安全と判ずる。**

### `HasSelectedLinePrimitive`——**依存はそもそも「載っておらぬ」（`P-169`型の穴が無い）**

`HasNoPropertySelection`が段2前に抱えておった穴（`DeleteSelectedElement`等、**`SelectedCell`の
`if(SetProperty(...))`ガード内に押し込められ、値が変わらねば丸ごと不発**という構造）を、
`HasSelectedLinePrimitive`は**そもそも持っておらぬ**。

**理由**——`HasSelectedLinePrimitive`の無条件通知は`SelectedConnector`／`SelectedFreeLine`**自身の
setter**に直に書かれておる。**これらのsetterが呼ばれる経路（`SelectedCell`setterの冒頭・
`ReplaceDocument`・削除メソッド自身）はいずれも`if`という外側のガードに包まれておらぬ**
——`SelectedCell`のように「複数プロパティをまとめて1つの`if`で条件づける」という構造そのものが
`HasSelectedLinePrimitive`には存在せぬ。

**ゆえにガード化しても、値が実際に変わる場面（非null→null等）では通知は変わらず正しく飛ぶ。
値が変わらぬ場面（null→null）では現状「意味の無い空撃ち」が飛んでおるだけで、
その空撃ちに依存する消費先（`HasSelectedLinePrimitive`自身の値は不変ゆえバインドの再評価結果も
不変）は無い——ガード化しても実害は生じぬと判じた。**

---

## 3. 結論——**両プロパティともガード化は安全と見立てる。ただし実測はしておらぬ**

| プロパティ | ガード化の安全性 | 理由 |
|---|---|---|
| `HasNoPropertySelection` | **安全**（見立て） | 段2が独立な明示経路を追加済み。暗黙経路が消えても明示経路が残る |
| `HasSelectedLinePrimitive` | **安全**（見立て） | もとより`if`ガードに包まれた依存構造を持たぬ。「値が変わる時は変わらず飛ぶ、変わらぬ時は元より不要」という単純な形 |

**併せて――`HasSelectedLinePrimitive`自体に`P-169`型の穴（ある`if`ガードの内側に押し込められ、
条件が満たされねば丸ごと不発する構造）は無いと確認した。** `SelectedConnector`／`SelectedFreeLine`
自体は各々独立したプロパティで、`SelectedCell`のような「複数プロパティを束ねて1つの条件で
まとめて発火させる」構造の対象にはなっておらぬ。

---

## 4. 射程・限界

- **本調査は「ガード化しても通知が失われぬか」という一点に絞ってある。** 実際に`if(SetProperty(...))`
  を追加してビルド・全体スイートを走らせる実測はしておらぬ——**机上の構造解析のみ**
- **【2026-08-06訂正・取り消し】上記の初版で「`ApplyUndoRedoSnapshot`は`SelectedConnector`等の
  旧参照を残留させる懸念がある」と書いたが、これは誤りであった。** `ApplyUndoRedoSnapshot`は
  自身の関数本体でこそ`SelectedConnector`等に触れぬが、**`SelectedCell`setterを2回通す**
  （`SetCurrentSheetIndexCore`内の無条件`SelectedCell=null`、および末尾の
  `SelectedCell = ClampSelectedCellToSheetRows(...)`）。**`SelectedCell`setter冒頭
  （`:456-468`）は`SelectedConnector`・`SelectedWireBreak`・`SelectedFreeLine`・
  `SelectedConnectionDot`・`SelectedImage`・`SelectedFrame`全てを`if`ガードより前で
  無条件にnullクリアする設計**（T-041増分1「値が変化しない場合も含め常時クリアする」）ゆえ、
  **旧参照は必ず落ちる。懸念は実在せぬ。** 過ちの所在＝`ApplyUndoRedoSnapshot`の関数本体のみを
  見て、それが呼ぶ`SelectedCell`setterの副作用まで追わなんだこと（呼び出し先の副作用追跡漏れ）
- **`SelectedWireBreak`／`SelectedConnectionDot`は無条件通知パターンを持たぬと確認済みだが、
  これらが依存する別種の穴（P-169型）が無いかまでは踏み込んでおらぬ**——今回の軸は
  「無条件通知」であり、それ以外の通知欠落パターンの網羅調査ではない
