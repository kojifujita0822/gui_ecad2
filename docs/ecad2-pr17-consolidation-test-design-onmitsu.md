# `PR-17`複製統合＋`P-169`＋`P-170` テスト設計（隠密）

> 2026-08-06 隠密。家老の采配により、`onmitsu.md`【MUST】「テスト設計の起草」に従い**仕様側から**起こす。
> 対象＝侍の実装設計書（`docs/ecad2-pr17-consolidation-design-samurai.md`、コミット`1139477`＋`4e1a11e`）と
> 忍者の実機観点書（`docs/ecad2-pr17-consolidation-verification-prep-ninja.md`）。**行番号はすべて`a017c34`（＋その後のpush分`2e36638`）時点、侍の設計書と同一基準**。
>
> **侍の実装はこの設計が届いてからにて、まだ着手されておらぬ。**

---

## 0. 前提の確認（一次ソースで裏取り済み）

侍の設計書の数・行番号を`MainWindowViewModel.cs`で直読し、以下を確認した——**食い違いなし**。

| 箇所 | 行 | 件数 |
|---|---|---|
| 基準`NotifySelectedElementChanged()` | `:2675-2692` | 15 |
| `DeleteSelectedElement` | `:2521-2557`（通知は`:2540-2554`） | 15（基準と完全同一） |
| `SelectedCell` setter | `:461-515`（通知は`:496-512`） | 17（基準15＋`SelectedCellDisplay`＋`HasNoPropertySelection`） |
| `ReplaceDocument` | `:3550-3650`（通知は`:3630-3645`） | 16（基準15＋`SelectedCellDisplay`） |
| 既存の呼出6箇所 | `:2575` `:2637` `:3204` `:3285` `:3503` `:3806` | — |

**併せて確認**——`SelectedImage`（`:1289-1300`）・`SelectedFrame`（`:1480-`付近）の両setterは`SetProperty`の戻り値を見ず`HasNoPropertySelection`を無条件通知。`ReplaceDocument`は`SelectedImage=null`（`:3588`）・`SelectedFrame=null`（`:3592`）を必ず通る。**侍の§5-2の記述と一致**。

**`SelectedElement`は`SelectedCell`と`CurrentSheet.Elements`からの算出プロパティ**（`:2137-2138`、専用フィールドを持たぬ）——テスト設定では「要素を`Elements`へ追加し`SelectedCell`をその位置へ合わせる」ことで`HasSelectedElement`を真にする。

---

## 1. 段の切り方の確認——設計書どおり2段、測り方も別

| 段 | 主張 | 測り方 |
|---|---|---|
| **段1** | 3箇所の複製を`NotifySelectedElementChanged()`呼び出しへ統合。**振る舞い不変** | 統合前後で**通知集合が一致**すること |
| **段2** | 基準へ`HasNoPropertySelection`を足す。**通知が意図して増える**（9経路すべて＋1） | 現行コードでは通知されぬことを**RED先行証明**、実装後にGREEN |

以下、段ごとに分ける。**同一コミットへ混ぜぬこと**（侍の設計書§3、家老の采配双方の求め）。

---

## 2. 段1のテスト設計——「通知集合が変わらぬこと」

### 2-1. 検出力の要——**件数ではなく集合を見る（`PR-27`直結）**

**家老の申し付けどおり、件数（`Assert.Equal(n, raised.Count)`）は使わぬ。** 件数一致は、例えば
`SelectedElementLabelDy`が漏れて代わりに`SelectedElementComment`が二重に飛んでも15件のままゆえ検出できぬ
——**まさに`PR-27`が警告する「対称な入れ替わりを件数が隠す」形**にござる。

**ゆえに全テスト、`raised`は`HashSet<string>`（またはそれに準ずる重複排除集合）で捕らえ、
`Assert.Equal(expectedSet, raisedSet)`で**集合そのもの**を比較する。**

### 2-2. 期待集合の組み立て——**二重定義を避ける**

`MenuPlacementToolTests`（T-133増分6）が`ExpectedEntries()`から`SymbolIndices()`を派生させた作法に倣い、
**基準15件を1箇所の`static readonly`配列として持ち、他の期待集合はそこからの`Union`で組み立てる**
（並びの定義を二重に持たぬため）。

```csharp
private static readonly string[] BasisProperties = {
    nameof(MainWindowViewModel.SelectedElement),
    nameof(MainWindowViewModel.HasSelectedElement),
    nameof(MainWindowViewModel.SelectedElementKindDisplay),
    nameof(MainWindowViewModel.SelectedElementDeviceName),
    nameof(MainWindowViewModel.IsSelectedElementSelectSwitch),
    nameof(MainWindowViewModel.SelectedElementNotchPosition),
    nameof(MainWindowViewModel.IsSelectedElementBreaker3P),
    nameof(MainWindowViewModel.SelectedElementBreakerType),
    nameof(MainWindowViewModel.IsSelectedElementLamp),
    nameof(MainWindowViewModel.SelectedElementLampColor),
    nameof(MainWindowViewModel.IsSelectedElementTimerRelated),
    nameof(MainWindowViewModel.SelectedElementSetpoint),
    nameof(MainWindowViewModel.SelectedElementSetpointSliderValue),
    nameof(MainWindowViewModel.SelectedElementLabelDy),
    nameof(MainWindowViewModel.SelectedElementComment),
}; // 15件、基準そのもの
```

`DeleteSelectedElement`の期待＝`BasisProperties`そのまま。
`SelectedCell`setterの期待＝`BasisProperties ∪ { SelectedCellDisplay, HasNoPropertySelection }`（17件）。
`ReplaceDocument`の期待＝`BasisProperties ∪ { SelectedCellDisplay }`（16件、段1時点ではまだ`HasNoPropertySelection`を含まぬ）。

### 2-3. 捕捉時の「家族フィルタ」——無関係な通知に巻き込まれぬための備え

**`ReplaceDocument`は`Document`・`CurrentFilePath`・`CanEditDiagram`等、本統合と無関係な通知も同時に出す**
（`:3623-3629`）。**集合比較をそのまま無フィルタで行うと、これら無関係な通知が増減しただけで
本テストが無関係な理由で壊れる**——結合度が高すぎる設計になる。

**ゆえに`PropertyChanged`購読では、上記`BasisProperties ∪ {SelectedCellDisplay, HasNoPropertySelection}`
に**属する名だけを`raised`へ加える**フィルタを掛ける**（無関係な通知はそもそも集合に入れない）。
これにより「本統合が対象とする15〜17件のみ」を過不足なく検める形になる。

### 2-4. 具体的なテスト（Fact 3件＋境界値1件）

| # | 対象 | 操作（入力） | 期待 |
|---|---|---|---|
| 2-4-a | `DeleteSelectedElement` | 要素を1つ配置し選択、`DeleteSelectedElement()`を呼ぶ | 捕捉集合＝`BasisProperties`と完全一致（15件） |
| 2-4-b | `SelectedCell` setter（変化あり） | 要素の在るセルを選択後、**別のセル**（行・列とも異なる）を選択 | 捕捉集合＝`BasisProperties ∪ {SelectedCellDisplay, HasNoPropertySelection}`と完全一致（17件） |
| 2-4-c | `SelectedCell` setter（**退化入力・境界値**） | **同一セルを再選択**（`vm.SelectedCell = vm.SelectedCell`と同値の代入） | **捕捉集合は空**（0件）——`if (SetProperty(...))`の条件分岐そのものが統合後も保たれているかを見る |
| 2-4-d | `ReplaceDocument` | 要素を選択した状態で`vm.NewDocument()`を呼ぶ（`ReplaceDocument`はprivateゆえ`NewDocument()`/`OpenFile`経由で駆動） | 捕捉集合＝`BasisProperties ∪ {SelectedCellDisplay}`と完全一致（16件、`HasNoPropertySelection`はまだ含まぬ） |

**2-4-cが要**——**PR-27の退化入力チェックそのもの**にござる。「同一値の再代入では17件が一切発火しない」
という**現状の条件分岐（ガード）**は、複製をまとめる際に`if`ごと消してしまう改変（＝常に発火する形へ
崩れる改変）に対して**無防備になりがち**（3箇所を1関数へ寄せる作業で、うっかりガードの外へ出しやすい）。
**この境界値ケースが無ければ、「17件は正しいが常に発火するようになった」という壊れ方を段1のテストは
一切検出できぬ**。

---

## 3. 段2のテスト設計——「`HasNoPropertySelection`が意図どおり増えること」

> **【2026-08-06追記・家老の一次ソース確認（コミット`292cef0`）を受けて全面改訂】**
> **`P-169`の射程は起票の四倍であった**——**削除経路だけでなく、配置2種・行削除の計4経路が欠落**。
> **本節は2種の別物を扱う。混ぜぬこと**（家老の申し付けそのまま）——
> - **§3-1〜3-3＝「今、実際に欠けておるもの」**（4経路の欠落そのもの、`P-169`射程拡大）
> - **§3-4〜3-5＝「今は偶然埋まっておるが、支えが消えたら分かるか」**（`ReplaceDocument`の暗黙の依存）

### 3-0. 【最重要】単体テストで見るべきは「値」ではなく「通知が発火したか」

**`HasNoPropertySelection`はバッキングフィールドを持たぬ算出プロパティ**
（`=> !HasSelectedElement && !HasSelectedImage && !HasSelectedFrame`、`:1308`）。
**ゆえに`vm.HasNoPropertySelection`を直接読めば、通知の有無に関わらず常に「今の正しい値」が返る**
——**バグの正体は値の誤りではなく、`PropertyChanged`が飛ばぬためWPFのバインドが再評価されず、
画面が古い表示のまま取り残されること**（家老の言う「案内文だけが取り残される」）。

**すなわち`Assert.False(vm.HasNoPropertySelection)`のような値の直接比較は、修正前でも修正後でも
常にGREENで通ってしまい、本件の検出力を持たぬ。** **本節のテストはすべて「`HasNoPropertySelection`という
名の`PropertyChanged`が実際に発火したか」を捕らえる形で設計する**（§3-2以降すべて共通の原則）。

### 3-1. 症状は二つの顔を持つ——**空白型と二重型**

家老の整理をそのまま設計の軸に採る。

| 型 | 遷移 | 見え方 | 経路 |
|---|---|---|---|
| **空白型** | 有→無（選択が無くなったのに気づかぬ） | 案内文が出ぬまま右下が空白 | `DeleteSelectedElement`（`P-169`起票）／`DeleteRowAtCommand`（同型） |
| **二重型** | 無→有（選択ができたのに気づかぬ） | **案内文が消えぬまま要素詳細も同時に出る**（忍者実機確認2026-08-06、`scratchpad\pr17-54-confirmed.png`、**横並び**） | `PlaceElementAtSelectedCell`（PartId経路`:3204`／Kind経路`:3285`） |

**二重型は誰も挙げておらなんだ症状**（家老評）——**`HasSelectedElement`は基準15件に含まれ正しく
飛ぶため詳細側は正しく現れるが、`HasNoPropertySelection`だけが基準に無く取り残されるため、
「両方同時に出る」という単独では気づきにくい形になる**。

### 3-2. 発火条件の境界——**`SelectedCell`が変わるか否か（機序は「呼び出し自体が無い」、`SetProperty`早期returnではない）**

> **【2026-08-06訂正】当初、本節は`memory: ecad2_setproperty_early_return_trap`と同軸（値が
> 偶然一致する経路でクリア処理がスキップされる）と書いたが、**家老の訂正・忍者の対照実験C
> （再クリックを一切介さず`{ESC}`のみでも二重型が出る）により機序として誤りと判明した**。
> **正しい機序＝そもそも`HasNoPropertySelection`への通知呼び出しが基準に含まれておらぬ**
> （`SetProperty`を呼んだ上で値が同じゆえスキップされるのではなく、**呼び出し自体が最初から
> 無い**）。**「`SelectedCell`が変わらぬ」という表面の共通点だけを見て機序まで同じと早合点した
> のは家老と同じ誤りにて、ここで訂正する**（一次ソース＝§0の各経路の通知ブロック自体に
> `HasNoPropertySelection`の行が存在せぬことで確認済み。「値は同じだが呼んだ」形の`SetProperty`
> ガードはどこにも介在しない）。

**それでも4経路すべてのテストで`SelectedCell`を操作前後で不変に保つ設定を要る理由は変わらぬ**
——ただし理由は「ガードを回避するため」ではなく、**「`SelectedCell`が変化する経路では
setter自身の17件ブロック（`HasNoPropertySelection`を含む）が別途飛んでしまい、対象の
4経路（欠落した固有の通知）の有無を覆い隠してしまう」ため**である。**同値分割の境界というより、
「他の正しい経路に紛れさせぬための実験条件の分離」**と言うのが正確。

| 条件 | 期待 | 理由 |
|---|---|---|
| `SelectedCell`**不変**（配置直後・削除直後・削除対象行そのものを選択中の行削除） | `HasNoPropertySelection`の通知が**飛ばぬ**側＝4経路固有の欠落が単独で顕在化する | 対象経路自身の通知ブロックにしか`HasNoPropertySelection`が無い以上、これが唯一の観測窓 |
| `SelectedCell`**変化**（別セルへ移る・シート切替） | setterの17件ブロックが飛ぶため通知は届く（が、これは対象4経路とは別の経路由来） | 混ぜるとどちらの経路が発火させたか区別できなくなる＝**対照条件として明示的に避ける** |

### 3-3. 具体的なテスト——4経路それぞれに専用のRED先行証明

**既存6箇所の呼出のうち「値が変わらぬ3経路」（`ReplaceOneDeviceName`／`ReplaceAllDeviceName`／
`EndOrJoinTargetDraft`）は代表1件のテストで足りる**——**この3つは基準関数を呼ぶだけで構造が
均一かつ実害も無いため**（後述§3-5末尾の技法で代表を1件用意すれば足りる）。
**残る4経路（配置2種・行削除・削除）はそれぞれ異なる実バグの手当てゆえ、個別に専用テストを要る**
（構造の均一性と症状の均一性は別物であった、という本采配そのものの教訓）。

| # | 経路 | 型 | 操作（`SelectedCell`を意図的に不変へ保つ） | ①現行（RED期待） | ③段2後（GREEN期待） |
|---|---|---|---|---|---|
| 3-3-a | `DeleteSelectedElement`（`:2521`） | 空白型 | 要素を配置し選択、`SelectedCell`はそのままで`DeleteSelectedElement()` | `HasNoPropertySelection`の発火**0回** | **1回** |
| 3-3-b | `DeleteRowAtCommand`（`:3806`） | 空白型 | `Grid.Rows=10`、要素を行3列1へ配置、`SelectedCell=(3,1)`（削除対象行そのものを選択、`RowInsertDeleteCommandsTests`T-a1と同一境界）、`DeleteRowAtCommand.Execute(3)` | **0回** | **1回** |
| 3-3-c | `PlaceElementAtSelectedCell(string,…)`（PartId経路、`:3204`） | 二重型 | **空セル**を選択（`HasNoPropertySelection`＝真の状態、`SelectedCell`は以後不変）→ 同セルへ`PlaceElementAtSelectedCell("contact-no", "X001", isOr:false)` | **0回** | **1回** |
| 3-3-d | `PlaceElementAtSelectedCell(ElementKind,…)`（Kind経路、`:3285`） | 二重型 | 同上、`PlaceElementAtSelectedCell(ElementKind.ContactNO, null)` | **0回** | **1回** |

**3-3-c・3-3-dが要**——**忍者の実機実測が示した「横並びで両方同時に出る」症状の単体テストでの再現**
にござる。**空セル選択→同セル配置という操作そのものが、忍者の対照実験（別セルを経由せず`{ESC}`のみ）
の単体版**。

### 3-4. 【`ReplaceDocument`固有】家老の問いへの回答——**件数ではなく「回数」で見る**

**家老の問い**＝「`ReplaceDocument`経路の暗黙の依存（`SelectedImage`/`SelectedFrame`setterの無条件通知）が
壊れたことを捕らえるテストが在るか」。

**答え＝在る。ただし`HasNoPropertySelection`が「飛んだか否か」を見るだけでは駄目**にござる。

**理由**——`ReplaceDocument`は**段2の実装が無くとも**、`SelectedImage=null`（`:3588`）・
`SelectedFrame=null`（`:3592`）の2setterが無条件に`HasNoPropertySelection`を通知するため、
**現行コードの時点で既に`HasNoPropertySelection`は2回飛んでおる**。**「飛んだか」を見る形の
テストは、段2を実装せずとも最初からGREENで通ってしまい、段2の寄与を測れておらぬ**——
まさに`onmitsu.md`「検出力は迂回経路が塞がっておることを確かめよ」が指す穴そのもの。

**ゆえに`HasNoPropertySelection`が**「そのイベント内で何回発火したか」を数える****。

| 段 | `ReplaceDocument`内での`HasNoPropertySelection`発火回数 |
|---|---|
| 現行（段1後・段2前） | **2回**（`SelectedImage`setter由来＋`SelectedFrame`setter由来、いずれも暗黙） |
| 段2後 | **3回**（上記2回＋基準`NotifySelectedElementChanged()`経由の明示1回） |

**2→3という回数の増分だけが、段2が「本当に基準へ足されたか」を暗黙の救いから切り離して示す。**
**これは`PR-27`が戒める「件数だけを見る」（＝異なる2つの集合の大きさを比べて中身の入れ替わりを
見逃す）とは別物**——**ここで数えるのは同一プロパティ名の発火"回数"であり、集合比較ではないため
`PR-27`の穴には当たらぬ**（回数を数えて中身が入れ替わる余地はない、対象が単一のプロパティ名ゆえ）。

同じ技法は**`SelectedCell`setterの回帰確認にも使える**——setter自身が持つ既存の
明示`HasNoPropertySelection`呼び出し（現行1回）を、段2で基準へ「移す」際に**消し忘れれば2回に
なる**（二重発火という新種の不具合）。**「増えていないこと」を確かめる意味でも回数が要る。**

### 3-5. `ReplaceDocument`・`SelectedCell`setter回帰の具体的なテスト

| # | 対象 | 操作 | ①現行（段2前） | ③段2後 |
|---|---|---|---|---|
| 3-5-a | `SelectedCell` setter（回帰確認） | 要素の在るセル→別セルへ選択変更 | 1回発火（現行の明示呼び出し） | **依然1回**発火（基準へ移るのみ、増減なし。§3-4末尾の技法） |
| 3-5-b | `ReplaceDocument`（忍者発見・射程拡大分） | 要素を選択した状態で`NewDocument()` | **2回**発火（暗黙のみ） | **3回**発火（暗黙2＋明示1、§3-4の技法） |
| 3-5-c | 既存呼出のうち「値が変わらぬ3経路」の代表1件＝`ReplaceAllDeviceName`（`:2637`） | 要素を選択し`ReplaceAllDeviceName`を呼ぶ（デバイス名一括置換、選択自体は変えぬ操作） | 0回発火 | **1回**発火——**基準関数を経由するだけで恩恵を受けることの代表証明**（§3-3冒頭の判断根拠） |

**3-5-aが要**——段2実装で`SelectedCell`setter自身の既存`HasNoPropertySelection`呼び出しを
消し忘れると2回発火に膨らむ（二重発火という新種の不具合）。**「増えていないこと」を確かめる
意味でも回数が要る。**

**3-5-cを1件のみに絞った理由**——**`ReplaceOneDeviceName`（`:2575`）・`EndOrJoinTargetDraft`
（`:3503`）を含め、この3箇所はいずれも`NotifySelectedElementChanged()`を単に呼ぶだけで、
呼び出し元ごとの条件分岐や個別処理を挟まぬことを確認済み**（配置2種・`DeleteRowAtCommand`とは
異なり、これらの3箇所には`SelectedCell`不変で選択の中身だけ変わるという状況が生じぬため、
実害も無い）。**段2は基準関数の中身を変えるだけゆえ、この3箇所が均一に恩恵を受けることは
構造上保証される——ただし段1のテスト（2章）が「各呼び出し元は今も基準関数を正しく呼んでおるか」
を独立に守っておることが前提**（この前提が崩れれば3-5-cも意味を失う。両章は対で機能する）。

### 3-6. 【射程・訂正】忍者§5-4への回答は3-3-c/dで足りる——値ベースの案は誤りであった

**忍者の観点書§5-4「配置後にプレースホルダが正しく消えるか、①の時点でまだ未確認」への回答は、
本設計の当初案では「配置後の`vm.HasNoPropertySelection`の値を見る」としていたが、
§3-0の原則（値は常に正しく算出されるため検出力が無い）に照らし誤りであった——本節で訂正する。**

**正しい回答＝3-3-c（PartId経路）・3-3-dが既に答えを与えている。** 家老の一次ソース確認により
**①現行の時点で実は「穴が有る」（欠落4経路の2つ）と判明したため、忍者の§5-4「穴の有無を先に
確かめる」という問い自体への回答も「無い」ではなく「有る、二重型として」に変わる**。
**忍者へは「単体で穴の存在と型（二重型）まで確定した。実機はUIの見え方（横並び表示等）を
受け持つ形でよい」と申し送る。**

---

## 4. 対称性・退化性の点検（`PR-27`適用の総括）

| 観点 | 本設計での手当て |
|---|---|
| **退化入力**（同一セル再選択＝無変化） | 2-4-cで明示的に加えた。ガード条件の消失を検出する唯一のケース |
| **件数だけを見る穴** | 2章は**集合**、3章は**単一プロパティの回数**で見る。いずれも`Assert.Equal(n, list.Count)`型を使わぬ |
| **順序依存** | 本設計では順序を主張せぬ（`RowInsertDeleteCommandsTests`のような同一プロパティの複数回発火＋順序依存は、段1・段2いずれの対象範囲にも入らぬため対象外） |
| **対称な入れ替わり**（例：`SelectedElementLabelDy`と`SelectedElementComment`の取り違え） | 2章の集合比較（`HashSet`比較）で捕捉可能——名前が1つでも違えば`Assert.Equal`は集合の差分として落ちる |

---

## 5. 忍者観点書との重複回避

**忍者の観点書は「画面表示」（プレースホルダの可視性・ステータスバー文字列）を対象とし、
本設計は「`PropertyChanged`通知そのもの」を対象とする**——層が違うため素の重複はない。

**唯一の関連＝3-3-c/3-3-d（§3-6参照）**。ここは単体テストで先に「通知の有無」を確定させ、
忍者の§5-4「穴の有無」の事前判定を助ける形にした（実際には忍者の実機実測の方が先に届き、
単体テストの見立てを裏づける形になった、§3-6参照）。**通知の有無・回数という単体テストの領分と、
実際に画面へ描かれるかという忍者の領分は、引き続き別**（本設計は前者のみを扱う）。

---

## 6. 侍への申し送り

1. **段1と段2は別コミットに分ける**（設計書の推奨どおり）。**2章のテストは段1のコミットへ、
   3章のテストは段2のコミットへ**（3-3・3-5各項の①列は段2着手前のRED先行証明として使う）。
2. **2章のテストは「捕捉フィルタ」を伴う**——無条件にすべての`PropertyChanged`を集めると
   `Document`等の無関係な通知に巻き込まれる（§2-3）。**フィルタの対象集合を明示的にコードへ
   残すこと**（`BasisProperties`の配列がそのまま兼ねる）。
3. **3章は「回数」を数える**——`List<string>`で発火を全件記録し`.Count(n => n == nameof(...))`
   で数える形が素直（`Assert.Equal`で件数比較する対象は**単一のプロパティ名に限定**しており、
   2章で戒めた「異種混合の件数比較」には当たらぬ）。
4. **3-5-aは「増えていないこと」を確かめる回帰確認**——段2の実装で`SelectedCell`setter自身の
   既存`HasNoPropertySelection`呼び出しを消し忘れると2回発火に膨らむ。**このケースを見落とさぬこと**。
5. **3-3-c/3-3-d（配置2種の二重型）は、忍者の実機実測（`docs/ecad2-pr17-baseline1-verification-ninja.md`）
   で①現行の時点の穴の実在が既に確定しておる**——**RED先行証明の「RED」は机上の予測ではなく
   実機で裏づけ済みと明記して侍へ渡す**（本設計の当初案が「値を見る」誤りに気づかず「①はGREENの
   見込み」と書いていた経緯があるため、念のため強調する）。

---

## 7. 射程・限界

- **本設計は`PropertyChanged`購読という1つの軸に絞ってある。** バインド経由で値を検証する
  間接的なテスト（`onmitsu.md`§4「本調査の軸は『`PropertyChanged`を購読するテスト』」と同型の
  限定）は対象外——**実機で忍者が担う。**
- **3-5-cで「値が変わらぬ3経路」中1箇所のみを代表としたのは、構造上の均一性（基準関数の中身を
  変えるだけ）を根拠にした判断であり、実測ではない。** 侍が実装時にこの3箇所を目視で再確認し、
  分岐や個別処理が紛れ込んでいないかを一度は当たられたい（該当があれば代表選定の前提が崩れる）。
  **なお当初「9箇所中1箇所で足りる」としていたのは誤りで、実際には4箇所（配置2種・行削除・削除）
  は代表では足りず個別に専用テストを要ることが家老の一次ソース確認で判明した（§3-3参照）。**
- **`P-170`（`SheetReorderInsertionAdorner.cs:13`の未凍結Pen）は本設計の対象外**——家老の申し送り
  どおり実機で見えるものではなく、侍のテスト実行結果（並列スイートでの再現）に委ねる。
