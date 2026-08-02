# T-143 テスト設計：展開済みモータの`kind`欠落を起動時に補正する（案A）

設計日: 2026-08-02　起草者: 隠密（key=1785663815688）　依頼元: 家老（実装は侍、`karo.md`に基づきテスト設計と実装を分離）

---

## 0. 記帳文の検分（家老依頼）

`docs/todo.md` T-143節（:713-782）を通読した。**一歩踏み込んだ記述は見当たらなかった**。数値・射程・
機序の各記述は隠密・侍双方の調査結果と一致し、「侍が」「隠密が」と主体も明記されている。家老自身の
見立て（「Kindは接続点の配列の中にあり…階層が違う公算」）も「未検証」と明記され適切に留保されている。

---

## 1. 【最重要・実装前に必ず読まれたい】技術的制約——「欠落」と「意図的なPower」はJSON上区別不可能

**DoD2「殿が手で編集なされた内容を壊さぬこと」の実現可能性に関わる限界を先に述べる。**

`JsonOptions.Default`は`kind`フィールドが欠落したJSONを読んでも例外を出さず、`PortDef.Kind`はデシリ
アライズ後に既定値`PortKind.Power`（enum値0）となる（`Element.cs:57`）。**この結果は「JSONに`kind`
フィールドが元々無かった（欠落）」場合と「JSONに`"kind":"power"`と明示的に書かれていた」場合とで
まったく同じ値になり、両者をコード側から区別する手段が無い。**

**前例（T-061の`Role`補正）との構造的な違い**——前例は「元の値が`ContactNO`の場合のみ補正、それ以外
（`ContactNC`等）は殿の意図的な変更とみなし触らない」という設計（`Enumerate_UserCustomizedSelectSwitchRole_NotOverwritten`、
`PartFolderStoreTests.cs:252`）で「欠落と意図」を区別している。**これが成り立つのは`PartRole`が
多値だから**——ContactNO以外の値であれば「意図的な変更」と判別できる。

**しかし`PortKind`は`Power`／`DrcExempt`の2値のみ**（`Element.cs:41-47`）。「欠落→既定値落ち」の
結果と「補正が正しく適用された後の状態」がどちらも同じ2値のいずれかに収まるため、**「殿がモータの
接続点を意図的に`Power`へ戻していた」ケースと「まだ補正されていない（欠落のまま）」ケースは、
前例と同型の判定条件では原理的に区別できない**。

**How to apply（案Aの現実的な落としどころ）**：実務上、モータの接続点を意図的に赤へ戻す動機は
考えにくい（T-136(C)でモータは結線から外れ、赤＝電源接続点という定義が実態と合わなくなったことが
青化の理由そのもの）。ゆえに「固定Id＝`basic-motor`なら、`Kind != DrcExempt`の各ポートを`DrcExempt`
へ補正する」という単純な条件（前例2件と同型の設計）で実務上は足りると判ずるが、**これは「原理的な
保証」ではなく「実務上の割り切り」である**点をDoDの解釈として明記されたい。**この限界そのものを
テストで検出することはできない**（区別できないものは測れない）ため、下記§2ではこの限界を前提とした
設計に留める。

---

## 2. テスト設計（同値分割・境界値・状態遷移・対称性・`[Theory]`）

対象＝`PartFolderStore.Enumerate()`（`PartFolderStore.cs:59-134`）への追記。前例2件
（`:85-89`＝`IsOrEligible`補正、`:96-100`＝`Role`補正）と同じ位置・同じ形（`if`→書き換え→
`try/catch`書き戻し）に揃える。

### 観点A：同値分割（対象Idと初期`Kind`状態）

| # | フィクスチャ（`.gcadpart`のJSON） | 期待結果 | 対応DoD |
|---|---|---|---|
| A1 | `id=basic-motor`、`ports`3件とも`kind`欠落（実運用データそのものの形） | U/V/W3件とも`Kind=DrcExempt`へ補正され、ファイルへ書き戻される | DoD1 |
| A2 | `id=basic-motor`、`ports`3件とも既に`kind="drcExempt"` | 変化なし（値も再書き込みも起きない・冪等性） | DoD4 |
| A3 | `id=basic-motor`以外（例＝`a接点`＝`ContactNOId`）、`kind`欠落 | 変化なし（`Power`のまま。誤爆防止） | DoD3 |
| A4 | `id=basic-motor`、3件中1件のみ`kind`欠落・残り2件は既に`drcExempt`（部分的境界ケース） | 欠落分のみ`DrcExempt`へ補正、既存の`drcExempt`は保持（過剰書き込みの有無を確認） | DoD1・4 |

### 観点B：破壊性の点検（DoD2、家老の言う「最も重い」観点）

**Kind以外のフィールドが一切変わらないことを、個別に測る**（`PartFolderStoreTests.cs`の既存前例には
この形の直接比較テストが無いため、本タスクで新設する意義がある）。

| # | フィクスチャ | 検証内容 |
|---|---|---|
| B1 | `id=basic-motor`、`primitives`を標準のモータ形状と異なる値（殿が手で編集した体）に変更、`kind`欠落 | 補正後も`primitives`が一字一句変わらない（フィールド単位で比較） |
| B2 | 同上、`widthCells`／`heightCells`／`name`を標準と異なる値に変更 | 補正後もこれらのフィールドが変わらない |
| B3 | 同上、`ports`の`name`／`rowOffset`／`boundaryOffset`を標準と異なる値に変更（`kind`のみ欠落） | 補正後も`Kind`以外の`PortDef`フィールド（`Name`／`RowOffset`／`BoundaryOffset`）は変わらない |

### 観点C：状態遷移（冪等性、DoD4）

| # | シナリオ | 検証内容 |
|---|---|---|
| C1 | `Enumerate()`を1回目呼ぶ（A1の欠落フィクスチャ） | ファイルへ書き戻された内容に`"kind":"drcExempt"`相当の文字列が3件現れる |
| C2 | C1の後、同一フォルダで`Enumerate()`を2回目呼ぶ | 1回目の書き戻し後とファイルの内容（バイト列）が完全一致——再書き込みされていないか、書き込まれても値は不変であることのいずれかを、ファイルの最終更新時刻または内容ハッシュで確認 |

### 観点D：対称性点検（`[Theory]`活用、退化を避ける）

**U/V/Wの3ポートを1つだけ確認して安心する穴を防ぐ**——T-136(B)増分5のテスト（`T136Increment5PortKindAssignmentTests`）
が「3端子とも同色」を明示的に検証した先例と同じ形を踏襲する。

| # | 検証内容 |
|---|---|
| D1 | `Assert.All(result.Ports, p => Assert.Equal(PortKind.DrcExempt, p.Kind))`で3件個別に確認（1件だけ見て
     済ませない） |

### 観点E：誤爆防止（前例2件の既存テストと同型）

| # | シナリオ | 検証内容 |
|---|---|---|
| E1 | `id=basic-motor`ではない別の固定Id（例＝`SelectSwitchId`）で`kind`欠落 | 変化なし（`Enumerate_OtherBasicPartWithContactNORole_NotAffected`と同型の誤爆防止テスト） |
| E2 | 書き込み失敗（読み取り専用ファイル） | 例外が外へ伝播せず、メモリ上は補正済みで継続する（`Enumerate_LegacySelectSwitchJsonReadOnly_BackfillsInMemoryWithoutThrowing`と同型） |

---

## 3. RED先行証明の観点

**A1・D1は新APIに依存せず、既存の`PartFolderStore.Enumerate()`のみで書けるテストである**——未実装の
コードでも「モータのKindが赤のまま」という形でFAILする（既存の`GcadCompatibilityTests`が示すとおり、
`kind`欠落→`Power`という挙動自体は現行コードで既に成立しているため、A1は補正処理が無い現状コードでも
コンパイルは通り、実行結果がFAILする＝正しくREDが取れる）。実装投入後は全件GREENへ転じることを
確認されたい。

**C2（冪等性）とB1〜B3（非破壊性）は実装が入って初めて意味を持つ観点**であり、実装前は「補正処理自体が
存在しない」ため恒常的にPASSしてしまう（補正されないのだから当然変化しない）——これは`memory:
feedback_red_proof_new_api_limitation`の言う「新設APIに依存するテストは旧コードでコンパイル可能でも
意味のあるRED/GREENの往復にならない」ケースに近い。**実装者（侍）はB・Cの観点についてはREDの意味が
薄いことを承知のうえで、A・D・Eの観点でRED先行証明の主眼を置かれたい。**

---

## 4. 事実と推測の峻別

**事実（一次ソースで確認）**
- `PartDefinition.Ports`は`List<PortDef>`（`PartDefinition.cs:89`）、`PortDef`は`readonly record struct`
  （`Element.cs:57`）——前例2件（`IsOrEligible`／`Role`）は`PartDefinition`直下のミュータブルな
  プロパティへの直接代入だが、`Kind`補正は`Ports`配列の各要素を`with`式で置き換える必要があり、
  **家老の見立てどおり構造が異なる**
- `PortKind`は`Power`／`DrcExempt`の2値のみ（`Element.cs:41-47`）
- 前例T-061の`Role`補正テスト`Enumerate_UserCustomizedSelectSwitchRole_NotOverwritten`
  （`PartFolderStoreTests.cs:252`）は「元の値が`ContactNO`の場合のみ補正」という条件で
  意図的変更を区別している

**推測**
- 「殿がモータの接続点を意図的に`Power`へ戻す動機は考えにくい」——これは業務的な蓋然性の見立てであり、
  確実な保証ではない（§1で明記済み）

---

## 5. スコープ境界の確認

本書はテスト設計のみ。実装（`PartFolderStore.cs`への追記・`with`式によるPorts補正ロジック）は侍へ
委ねる。「構造そのものの穴（今後どのテンプレートの`Kind`を変えても展開済みマシンへは届かぬ）」は
`docs/todo.md`に記帳済みのとおり本タスクの範囲外であり、再掘りしない。
