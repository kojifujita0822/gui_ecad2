# T-152 テスト設計（隠密起草）——サーマルリレーa/b・ソレノイド、クロスリファレンス検査からの除外

殿ご裁可＝案B（`PartDefinition`への真偽値マーカー、文言「クロスリファレンス検査から除外する」、
既定オフ）。着手前調査＝`docs/ecad2-p187-pretask-investigation-onmitsu.md`。本書はその9節相当。

**手当ての形は二層**（調査書3節で示した非対称のまま）——
(A) サーマルリレーa/b＝`elem.PartId`直指定で`DesignRuleCheck`から局所的に除外
(B) ソレノイド等の自作パーツ＝新設マーカーで`DRC-XREF-002`から除外

**マーカー名は本書で`IsExcludedFromCrossReference`と仮称する**（`IsOrEligible`の命名規則に
倣った提案にて、最終名は侍・家老の裁量）。

---

## 0. 前提（一次ソース確認）

### 0-1. `DesignRuleCheck.CheckCrossReference`の機構（`:55-97`）

`lib`を受け取り、`elem.PartId`にも手が届く。機器名(`DeviceName`)ごとに
`isRelayContact`（`IsContact(kind) && !IsInputControlled(kind)`）と
`isRelayCoil`（`kind is Coil or Timer`）を集計し、`hasContact && !hasCoil`→
`DRC-XREF-001`、`hasCoil && !hasContact`→`DRC-XREF-002`。

### 0-2. 既存の電気的ふるまい網は流用できる（家老眼目4への回答）

`tests/Ecad2.Core.Tests/T133Increment7ThermalRelayTests.cs:90-95`
`役割は原本どおりa接点とb接点である()`が**既に**
`RelayNO().Role == PartRole.ContactNO` ／ `RelayNC().Role == PartRole.ContactNC`
を固定しており、ドキュメントコメント自身が「`ThermalOverload`を選んでおらぬことを押さえるのが要」
と明記しておる。**もし実装がa/bのRoleをThermalOverloadへ寄せる誤りを犯せば、この既存テストが
即座にRED化する**——家老が案じた回帰（a接点がb接点へ化ける）は、Role単位で既に押さえられておる。

**新規テストは不要**。T-152のDoDには「本テストが引き続きGREENであること」を明記するに留める。

---

## 1. サーマルリレー側（`DRC-XREF-001`）——眼目1「他の組込み接点を巻き込まぬこと」

### 1-1. 同値分割

| 群 | 代表 | 期待（修正後） |
|---|---|---|
| サーマルリレーa/b | `BuiltinPartIds.ThermalRelayNO`/`NC` | 除外される（コイル無しでも`DRC-XREF-001`が出ぬ） |
| 既存の除外済み（対照） | `サーマル.gcadpart`（`ThermalOverloadId`、Role=ThermalOverload） | 引き続き除外（**二重除外にならず、単に無傷であること**） |
| 通常のリレー接点（対照・巻き込み検出） | `BasicPartTemplates.ContactNOId`/`ContactNCId` | 引き続き警告が出る（**巻き込まれておらぬこと**） |

三群を同じ形（コイル無し・接点のみの機器）で並べて測るのが眼目1の直接の答えになる——
「除外される」だけでなく「除外されてはならぬものが除外されておらぬ」を同じテストの中で対にする
（`memory: feedback_control_experiment_needs_naive_baseline`の型）。

### 1-2. テストケース

```
[Theory]
[InlineData(BasicPartTemplates.ThermalRelayNOId, false)]   // 除外＝警告出ぬ
[InlineData(BasicPartTemplates.ThermalRelayNCId, false)]
[InlineData(BasicPartTemplates.ThermalOverloadId, false)]  // 対照＝元より除外済み、無傷
[InlineData(BasicPartTemplates.ContactNOId, true)]          // 対照＝巻き込まれず警告が出る
[InlineData(BasicPartTemplates.ContactNCId, true)]
CheckCrossReference_単一機器がコイルを伴わぬ時_種別ごとに正しく警告有無が決まる(string partId, bool expectWarning)
```
- コイルの無い機器(`DeviceName="X1"`のみ)を1件配置し`CheckCrossReference`を実行
- `expectWarning`に応じて`ContactWithoutCoil`診断の有無を確認

### 1-3. 境界＝両方が揃った場合（サーマルリレー＋通常接点の混在）

```
[Fact]
CheckCrossReference_サーマルリレーと通常接点が同一機器名で混在する時_通常接点側の警告は消えぬ()
```
- 同一`DeviceName`へサーマルリレーaと通常のContactNOを1件ずつ配置（コイル無し）
- **仕様上の判断が要る境界**——現行の集計は機器名単位ゆえ、サーマルリレー側を`u.RelayContacts`
  から除いても、通常接点側が1件でも入れば`hasContact=true`のまま。**この構成では警告は出続ける
  のが正しい**（サーマルリレーを除いても「本物の接点」が残っておれば駆動元不明のまま）。
  設計時点の想定であり、実装後に侍が異なる解釈を採るなら理由を添えて申し出られたい

---

## 2. ソレノイド側（`DRC-XREF-002`）——眼目2「検出力をまるごと失わぬこと」

**調査書3-2で挙げた危うさをそのまま検証点にする**——マーカーが立っておらぬコイルは、
本物のリレーコイルであろうと自作パーツであろうと、**引き続き警告が出ねばならぬ**。

### 2-1. 同値分割

| 群 | マーカー | 期待 |
|---|---|---|
| 自作Coil・マーカーtrue（ソレノイド想定） | `IsExcludedFromCrossReference=true` | 除外される |
| 自作Coil・マーカーfalse（既定・付け忘れ想定） | `false`（既定） | **引き続き警告が出る**（検出力の生命線） |
| 自作Coil・マーカー未設定（後方互換、旧`.gcadpart`相当） | フィールド自体が無い状態から読み込み | `false`と同じ扱い＝警告が出る（3節で詳述） |
| 組込みCoil（対照） | 該当なし | 従来どおり警告が出る（マーカーの導入で組込み側に波及せぬこと） |

### 2-2. テストケース

```
[Fact]
CheckCrossReference_マーカーtrueの自作コイルは死にリレー警告から除外される()
```
- `IsExcludedFromCrossReference=true`の自作`PartDefinition`（Role=Coil）を1件配置（対応する接点なし）
- `CoilWithoutContact`診断が出ぬことを確認

```
[Fact]
CheckCrossReference_マーカーfalseの自作コイルは死にリレー警告が引き続き出る()
```
- 同条件でマーカーのみ`false`（既定）
- `CoilWithoutContact`診断が出ることを確認——**これが眼目2の核心**。
  もし実装がマーカーの真偽を取り違えて自作Coil全般を除外してしまえば、本テストがそれを検出する

```
[Fact]
CheckCrossReference_組込みコイルはマーカー導入後も従来どおり警告が出る()
```
- `BasicPartTemplates.CoilId`（組込み、マーカーの概念が無い）を1件配置
- 従来どおり`CoilWithoutContact`が出ることを確認（1-1の「対照」と同じ狙いをソレノイド側でも取る）

---

## 3. 後方互換（眼目3）

### 3-1. 既存`.gcadpart`（マーカーフィールドを持たぬJSON）の読込

`PartDefinition.SheetAffinity`が同型の前例を持つ（`PartDefinition.cs:82-86`
「永続化＝JsonSerializerによる自動反映...旧.gcadpartに本フィールドが無ければ既定値のまま読まれる」）。
`bool`の新設プロパティも同じ挙動になる見込み（`System.Text.Json`の既定＝欠落プロパティは
型の既定値、`bool`なら`false`）。

```
[Fact]
IsExcludedFromCrossReference未設定のJSONを読むと既定値falseになる()
```
- マーカーのキー自体を含まぬJSON文字列（実際の旧`.gcadpart`を模す）を
  `PartLibrarySerializer`（または該当するデシリアライズ経路）で読み込む
- `part.IsExcludedFromCrossReference == false`を確認
- **これは`System.Text.Json`の既定動作を確かめる網であり、侍の実装を待たずとも
  フィールド追加のみでGREENになる見込み**（4節参照）

### 3-2. 往復（保存→再読込）でマーカーが保たれること

```
[Fact]
IsExcludedFromCrossReferenceは保存して読み直しても保たれる(bool value)  // [Theory] true/false
```
- 値を設定した`PartDefinition`をシリアライズ→デシリアライズし、値が保たれることを確認
- 既存の`PartLibrarySerializer`往復テスト（`SheetAffinity`等）と同型のパターンを踏襲

---

## 4. RED先行証明の見通しと制約（家老眼目5）

**サーマルリレー側（1節）は新API不要——今すぐRED先行で書ける。**
`BasicPartTemplates.ThermalRelayNOId`等の既存APIのみで構成でき、現行コード（除外ロジック無し）に
対して実行すれば`ContactWithoutCoil`が出てRED、修正後は出ずGREENになる。

**ソレノイド側（2節）は新設フィールド（`PartDefinition.IsExcludedFromCrossReference`）に依存する。**
フィールドが存在せぬ現行コードでは2-2の`true`系テストがそもそもコンパイルできぬ
——`memory: feedback_red_proof_new_api_limitation`と同型の制約。

**段取りの提案**：侍がまず`PartDefinition`へフィールドを追加する一手（それ自体は数行、
リスクなし）を先に打てば、2-2の`true`系テストが「コンパイルは通るがDRC側が未対応ゆえ除外されず
RED」という形になり、RED先行証明が成立する。2-2の`false`系・3節は**フィールド追加のみで
GREENになる見込み**（DRC側の対応を待たず先に緑になる）ため、これらは「RED先行証明の対象外
（回帰網）」と実装報告で区別されたい（T-151の commit message が「後者は...証明対象外」と
明記した書式を踏襲）。

---

## 5. 侍への申し送り（設計外・実装時の判断事項）

- サーマルリレー側の除外条件を`elem.PartId is BuiltinPartIds.ThermalRelayNO or BuiltinPartIds.ThermalRelayNC`
  のような直指定にするか、`lib.Get(elem.PartId)`経由で何らかの共通の目印を見るかは実装者判断。
  1-2のテストはいずれの実装でも通る形にしてある（観測点はPartId・振る舞いのみ、実装経路は問わぬ）
- 1-3（混在時の挙動）は某の想定にて、実装が異なる解釈を採るならテストごと相談されたい
- エディタUI（チェックボックスの配置・文言）は本書の範囲外——UI/UXの実見は忍者・殿の領分

以上、着手前調査（P-187）と本設計をもって、明日8/17の実装・検証がそのまま始められる形に
してあると存ずる。

---

## 6. 追補（2026-08-16・射程拡大）——`DRC-TYPE-001`誤発火（`CheckDeviceTypeConsistency`）

隠密のT-152静的レビューで発見。殿裁可により射程へ含める。

### 6-0. 実害の機序（一次ソース確認）

`DesignRuleCheck.CheckDeviceTypeConsistency`（`:153-209`）は`ElementCatalog.IsInputControlled`で
機器名ごとに接点を`EnergizedControlledRefs`／`InputControlledRefs`へ振り分ける。

- `サーマル.gcadpart`（Role=ThermalOverload）＝`IsInputControlled`が真→`InputControlledRefs`
- `サーマルリレーa/b`（Role=ContactNO/NC）＝`IsInputControlled`が偽→`EnergizedControlledRefs`

**現場の通例＝OL本体と補助接点は同一機器名**（殿ご確認済み）。両者が同一`DeviceName`を持てば
`hasEnergized && hasInput`が成立し、**`DRC-TYPE-001`（`Error`severity）「励磁系接点と入力系接点が
混在しています。シミュレーションが正しく動作しません」が誤って発火する**。孤立利用（OL単体、
サーマルリレー単体）では発火せぬが、通例の組み方でこそ発火する——XREF側と対を成す型の誤りにて、
T-133増分7でサーマルリレーa/bが新設されて初めて組める形になった（新設パーツと既存パーツの
掛け合わせで生じた、いずれか単体の欠陥ではない）。

### 6-1. 手当ての形——【推奨】門を置く（除外）。振り分け改めは退ける

**振り分け改め（`ElementCatalog.IsInputControlled`自体を書き換え、サーマルリレーの`ElementKind`を
input系に寄せる）は退ける。** 理由＝`IsInputControlled`は`DesignRuleCheck`だけでなく
**`Evaluator.cs:172-177`（`IsConducting`）のテストモード導通判定そのもの**が参照しておる
——`InputControlled`なら`s.Inputs[DeviceName]`（手動トグル）のみで導通、そうでなければ
`s.Energized[DeviceName]`（コイル励磁）が主導・手動強制はOR経由の副次的な迂回路にすぎぬ。
`Evaluator.IsConducting`は`Component`（`NetlistBuilder`解決後のランタイム構造）を相手にし、
`PartId`/`PartLibrary`へは手が届かぬ——**振り分けを変えるにはNetlistBuilder→Componentの経路まで
PartId弁別を通す必要があり、DesignRuleCheckだけの局所改修では済まぬ**。2日の期限では負いきれぬ
規模と判ずる。

**【現行のシミュレーション挙動は変えずとも実害は乏しい】** サーマルリレーは元よりコイルを
持たぬ設計ゆえ、テストモードで`s.Energized[DeviceName]`が真になることは自然には起こらぬ。
現行どおり「Energized判定＋手動強制のOR」のままでも、**手動強制（`s.Inputs`）は元から効く**
——実質、現行のままテストモードでの手動操作に支障はない見込み（実測ではなく机上の見立て。
実機での手動強制操作の確認は忍者の領分）。

**ゆえに`CheckDeviceTypeConsistency`側だけに、`CheckCrossReference`と同型の門を置く**——
サーマルリレーa/bを`EnergizedControlledRefs`／`InputControlledRefs`いずれにも算入せず素通しする。
`Evaluator`・`ElementCatalog.IsInputControlled`本体には一切触れぬ。

**【除外(門)と再分類、どちらでも良かったが除外を推す一点】** 実は「サーマルリレーを
`InputControlledRefs`側へ再分類する」（`DesignRuleCheck`内だけのローカルな振り分け変更、
`ElementCatalog.IsInputControlled`は変えぬ）という手も机上では成り立つ——OL+補助接点の
false positiveは同じく消え、かつ「サーマルリレー＋無関係な励磁系接点」の命名衝突は
（再分類なら）引き続き検出できる（6-3で詳述）。**されど除外の方が実装が単純（既存の門と完全に
同型）で、2日の期限には除外を推す**——再分類が拾える追加の検出力（6-3）は極めて稀な命名衝突
一種のみで、実装の単純さと引き換える値打ちに乏しいと判ずる。侍がより慎重を期すなら再分類でも
よく、その場合はテストケースを組み替える要があると申し送る。

### 6-2. 眼目2＝二重除外の無害確認

`サーマル.gcadpart`自体の分類ロジックは本追補で一切変更せぬ——`IsInputControlled`のまま。
対照テストで「サーマル単体は従来どおり」「サーマル＋無関係接点の組合せは従来どおり」を固定する。

### 6-3. 眼目3＝本来鳴るべき混在が消えぬこと、および除外方式の既知の限界

- **回帰**＝サーマルリレーと無関係な、通常のContactNO(Energized)とPushButtonNO(InputControlled)
  の組合せは、従来どおり`DRC-TYPE-001`が出ること（サーマルリレー側の変更が無関係な判定へ
  波及せぬことの確認）
- **【除外方式の既知の限界・明記しておく】** サーマルリレーa/bを門で除外すると、
  「サーマルリレー＋無関係な入力系接点（押釦等）が同一機器名」という**稀な命名衝突**は
  検出できなくなる（除外された側は算入されず、hasEnergiedが立たぬゆえ）。これは6-1で述べた
  トレードオフそのもの。**実装後、この限界を明示するテストを1件（`Assert`で「警告が出ぬ」ことを
  確認し、コメントで限界と明記）残しておくことを勧める**——「検出漏れ」と「仕様どおりの限界」を
  後から区別できるように

### 6-4. RED先行証明

既存API（`BasicPartTemplates.ThermalOverloadId`・`ThermalRelayNOId`/`NCId`）のみで構成できる。
主眼のテスト（サーマル本体＋補助接点が同一機器名→`DRC-TYPE-001`が出ぬこと）は、現行コード
（門なし）に対して実行すればRED（実際に出てしまう）、修正後GREENになる——RED先行証明が成立する。

### 6-5. テストケース一覧（追補）

```
[Theory]
[InlineData(BasicPartTemplates.ThermalRelayNOId)]
[InlineData(BasicPartTemplates.ThermalRelayNCId)]
CheckDeviceTypeConsistency_OL本体と補助接点が同一機器名の時_混在警告が出ぬ(string relayPartId)
```
- device"OL1"へ`ThermalOverloadId`＋`relayPartId`を配置、`TypeConflictEnergizedVsInput`が出ぬこと

```
[Fact]
CheckDeviceTypeConsistency_サーマル単体は従来どおり警告を出さぬ()
[Fact]
CheckDeviceTypeConsistency_サーマルと無関係な励磁系接点の組合せは従来どおり警告が出る()
[Fact]
CheckDeviceTypeConsistency_通常の励磁系接点と入力系接点の混在は引き続き警告が出る()
```
（6-2・6-3前半の回帰網）

```
[Fact]
CheckDeviceTypeConsistency_サーマルリレーと無関係な入力系接点の組合せは警告が出ぬ_既知の限界()
```
（6-3後半、除外方式の限界を明記する記録用テスト）

以上、明日の実装がすぐ着手できる形に整えたと存ずる。
