# T-151 ラベル重なり修正 テスト設計（隠密起草）

殿ご裁可＝案(1)「自作パーツには既定値を適用せぬ」。role=Coil であっても自作パーツなら
コイル用オフセットを用いず、既定値を適用しない位置に置く。

家老の采配メッセージにあった眼目三つ（(a)弁別箇所 (b)組込み非波及 (c)role=Coil以外への影響）に加え、
侍の所見二件（家老 `Read` 確認済み、`karo→onmitsu` メッセージ 02:59）を前提として織り込む。
本書は着手前の設計であり、実装（侍）はこれを土台にコードへ落とす。

---

## 0. 前提確認（家老DoD3・侍の断り「描画側の適用箇所は追っておらぬ」への回答）

一次ソースを自ら開いて確かめた。侍の所見二件（未確認としていた分）は家老が別途裏取り済みで、
本書の記述と一致する。

### 0-1. `ElementCatalog.DefaultLabelDy` の実呼び出しは二箇所（対称消費者）

| 箇所 | 役割 |
|---|---|
| `DiagramRenderer.cs:1254`（`DrawElementLabel`内） | 描画そのもの。`Params["LabelDy"]`（個別値）優先、無ければ `ElementCatalog.DefaultLabelDy(labelKind, orient)` |
| `MainWindowViewModel.cs:2486-2489`（`DefaultLabelDyOf`） | プロパティパネルの相対値表示（`SelectedElementLabelDy`）の基準点 |

`MainWindowViewModel.cs:2474-2485` のdocコメントが明記するとおり、T-145はこの二箇所を意図的に
一元化した——「片方だけ直せば、プロパティパネルの相対値と実際の描画位置が食い違う（入力欄に0と
出ておるのに絵は別の場所）」。**本修正も両方を同時に直さねば同じ食い違いを再生する。**
9-4節に、この食い違いを直接突く検証点を立てる。

`DefaultLabelDx` は `MainWindowViewModel` 側に対称の消費者（`SelectedElementLabelDx`）が存在しない
（grep 0件）。後述0-3のとおり自作パーツの `DefaultLabelDx` は本修正前から常に0であり、
この非対称は実害を持たない。

### 0-2. 「自作か組込みか」を弁別する手段は現状App層にしかない（眼目(a)の核心）

- `MainWindowViewModel.BuiltinPartIds`（`:2973-2974`）＝`BasicPartTemplates.All().Select(p=>p.Id)`
  の固定Id集合。これが**唯一**の弁別手段（家老裁定2026-08-15、Categoryに依らぬ方式として確定済み）
- `MainWindowViewModel.PartLibrary`（算出プロパティ、`:2948-2961`）は組込みと自作を**混ぜて**返す。
  `DiagramRenderer._lib`／`PartResolver` に渡るのはこの統合済みライブラリであり、
  `lib.Get(e.PartId) is not null` だけでは出自（自作か組込みか）を判じられぬ
  ——**統合の時点で出自の情報が失われる**
- Core層（`Ecad2.Core.Model.PartResolver`／`Ecad2.Core.Model.ElementCatalog`／`Ecad2.Core.Rendering.DiagramRenderer`）
  からは `BuiltinPartIds` が見えぬ。ただし `BasicPartTemplates`（`Ecad2.Persistence`）自体は Core
  プロジェクト内に在り、Core側で同じ集合を独自に作ることは可能
- **しかし Model／Rendering 層から Persistence 層を直接参照すれば層の向きが逆になる**——
  `ElementCatalog.cs:82-96`（`MotorPartId`）が同じ理由でPersistence側の定数を複製して回避した先例が
  既にある。複製すれば「二つの綴りが食い違えば黙って迂回が効かなくなる」という同型のリスクを負う
  （T-145のモータ迂回が現に踏んだ罠）

**【不変条件として記す・実装方式は指示しない】** ラベルオフセットの解決点（0-1の二箇所）が
「自作か組込みか」を判じる時、**Category（フォルダの物理配置）に依ってはならぬ**（家老裁定
2026-08-15の射程内）。手動で「図形/」直下へ置かれた実質自作パーツ（`Category=""`）も、
組込みをコピーして再採番されたパーツ（T-035）も、**`BuiltinPartIds` と同じ固定Id集合との照合**で
自作側へ正しく倒れねばならぬ。弁別をどこに置くか（Core層に複製するか、Appから何らかの形で
届けるか）は実装者の判断に委ねる——9-2節のテストはこの外部観測可能な弁別結果のみを固定し、
内部の配線方式には依存しない形にしてある。

### 0-3. `DefaultLabelDx` は自作パーツでは既に常に0（対応不要の確認）

`ElementCatalog.DefaultLabelDx` が非0を返すのは `ElementKind.Motor` のみ。`PartResolver.LabelKind`
がモータへ迂回するのは `lib?.Get(e.PartId) is not null && e.PartId == ElementCatalog.MotorPartId`
の場合に限られ、`MotorPartId` は組込み固定Idゆえ自作パーツが一致することはあり得ない。
すなわち**自作パーツの `labelKind` が `ElementKind.Motor` になる経路は存在せず、`DefaultLabelDx`
は自作パーツについて修正前から常に0**。本修正は `DefaultLabelDy` のみが対象でよい
（`DefaultLabelDx` へ同型の対処を加える必要はない）。

---

## 1. 弁別の同値分割——「自作か組込みか」の境界

| ケース | 分類 | 根拠 |
|---|---|---|
| `PartId` が `BuiltinPartIds` に含まれる | 組込み | そのまま |
| `PartId` が自作フォルダ（`図形/自作`）由来、`BuiltinPartIds`に無い | 自作（通常経路） | 0-2 |
| `PartId` が「図形/」直下へ手動配置、`Category=""`だが`BuiltinPartIds`に無い | 自作（稀な経路） | 0-2、Categoryに依らぬ設計の直接の適用対象 |
| 組込みをコピーして作った自作パーツ（T-035で再採番済み） | 自作 | 新Idが`BuiltinPartIds`外 |
| `PartId` が未解決（ライブラリで引けぬ） | **対象外・現状維持** | `DRC-PART-001`が拾う既存経路。`e.Kind`（構造的既定値ContactNO）へ静かに落ちる。本修正が新たに触れる筋ではない |
| `PartId` が null／空（Kind経路、3極記号等） | **対象外・現状維持** | 自作/組込みの概念が無い。従来どおり`e.Kind`をそのまま用いる |

**「未解決」「Kind経路」を対象外に明記する理由**：案(1)の射程は「自作パーツ」であり、
「自作と見紛う」だけの状態（未解決PartId）まで巻き込むと、`DRC-PART-001`が警告している最中の
要素の見た目まで変えてしまう。ここは9-3節の回帰テストで現状維持を固定する。

---

## 2. Role別の影響同値分割（眼目(c)）

`ElementCatalog.DefaultLabelDy` の既定値表を「自作パーツで到達しうる値が非0か0か」で二分する
（`PartResolver.ComponentKind`のRole→Kind写像を経由）。

| Role | 写像先Kind | 現行の`DefaultLabelDy` | 修正で変わるか |
|---|---|---|---|
| ContactNO | ContactNO | -1.5 | **変わる**（→修正後の値、9節参照） |
| ContactNC | ContactNC | -1.5 | 変わる |
| Coil | Coil | **-5.72** | **変わる（本タスクの主眼＝ソレノイド）** |
| Lamp | Lamp | -1.5 | 変わる |
| Terminal | Terminal | -2.0 | 変わる（既定値の中で最大の移動幅） |
| InputNO | PushButtonNO | -0.5 | 変わる |
| InputNC | PushButtonNC | -1.5 | 変わる |
| NonSimulated | （`CreatesComponent=false`ゆえ`e.Kind`へ、常にContactNO） | -1.5 | 変わる（Roleでなく構造的既定値経由だが同じく影響を受ける） |
| TimerContactNO/NC | TimerContactNO/NC | 0.0（catch-all） | **変わらぬ**（既に0） |
| TimerInstantContactNO/NC | 同左 | 0.0 | 変わらぬ |
| ThermalOverload | ThermalOverload | 0.0 | 変わらぬ |
| EmergencyStop | EmergencyStop | 0.0 | 変わらぬ |
| SelectSwitch | SelectSwitch | 0.0 | 変わらぬ |

**「変わる」7種＋NonSimulatedの計8種が本修正の実質的な影響範囲**——役儀md「role=Coilであっても」
という家老の言い回しは例示であり、殿裁可「自作パーツには既定値を適用せぬ」自体は全Role共通の
規則と読む（9-6節に確認事項として明記）。**「変わらぬ」6種は、そもそも今から通常位置と同じ値で
あり、退行しないことをそのまま回帰網に加える**（対称性チェック、9-3節）。

---

## 3. 修正後の期待値（数値の確定・要確認事項）

殿裁可の文言「通常のラベル位置に置く」を、家老経由の伝聞（「役儀書」相当の一次文言ではなく
家老の言い換え）としてそのまま数値化するには曖昧さが残る。二つの読みを立て、根拠とともに示す。

- **読みA（本書の既定案）＝ `dy=0.0`**：`ElementCatalog.DefaultLabelDy`の個別Kind分岐を一切
  適用しない（catch-all `_ => 0.0` と同じ帰結）。「既定値を適用せぬ」を字義どおり
  「`DefaultLabelDy`の表引きそのものを行わない」と読む。2節「変わらぬ」6種が既に置かれている
  位置と完全に揃う——「特別な調整をしていない要素と同じ場所」という意味で最も「通常」に近い
- **読みB＝ `dy=-1.5`（`e.Kind`＝ContactNOの値をそのまま使う）**：PartId経由の要素は`e.Kind`が
  構造的に常にContactNOである（T-046由来）ことを踏まえ、「Role由来の特別扱いだけをやめ、
  構造的な既定＝ContactNO用の値に委ねる」と読む

**本書は読みAを既定として9節のテストを起草する**（catch-all実装の方が変更点が単純で、
「既定値表を自作パーツには一切適用しない」という文言との適合度が高いと判じたため）。
**ただし数値は読みA/Bいずれでも1行で差し替えられるよう、期待値をテスト内の定数
`ExpectedCustomPartLabelDy`に一本化してある**（9-1節）。**この解釈は殿の一次のお言葉を
確認したものではない**——着手前に家老経由で一言確認いただくのが穏当（9-7節「確認依頼」参照）。

---

## 4. RED先行証明の見通し（家老DoD4）

**新API依存ではない。既存APIのみでRED→GREENが成立する。**

- 自作パーツの作成・配置は`T151PartLibraryEmbedPlacementTests.cs`が既に使っている
  `vm.PartPalette.SaveNewPart(part)` → `vm.NewDocument()` → `vm.PlaceElementAtSelectedCell(id, name, isOr:false)`
  の3手順のみで足りる。いずれも現行コードに実在する
- ラベル位置の観測も`vm.SelectedElementLabelDy`（プロパティパネル側）と
  `DiagramRenderer.Render`+`RecordingRenderer`（描画側、`T145MotorLabelOffsetTests`と同型）の
  いずれも現行APIで足りる
- **RED＝「修正前は自作Role=Coilが-5.72のまま」を先に固定する**。9-1節のテストは、
  現在の実装に対して実行すればRED（自作なのに-5.72のまま）、修正後はGREEN（0.0または-1.5)
  になる形で書ける——新設APIの実装を待たずに書き下ろせる

---

## 5. テストケース設計

### 9-1. 自作Role=Coilの主眼ケース（本タスクの発端）

```
[Fact]
Placement_CustomPartWithCoilRole_DoesNotUseCoilLabelOffset()
```
- 自作パーツ（Id="t151-label-custom-coil"、`WidthCells=1`、`Role=PartRole.Coil`、
  組込みコイルとは異なる図形＝折れ線）を`SaveNewPart`→配置
- `vm.SelectedElementLabelDy`（相対値）を読み、絶対値へ変換（＝`defaultDy`そのもの、
  相対値0なら絶対値は`defaultDy`と一致するテストの前提を利用——`T097LabelDyTests`と同じ手法）
- `Assert.NotEqual(-5.72, 絶対dy)`（コイル用オフセットを引きずっていないこと）
- `Assert.Equal(ExpectedCustomPartLabelDy, 絶対dy)`（3節の数値、既定案0.0）

### 9-2. 弁別の境界（眼目(a)の外部観測固定）

| # | ケース | 期待 |
|---|---|---|
| 1 | 組込みPartId（`BasicPartTemplates.CoilId`） | 従来どおり-5.72（変化なし） |
| 2 | 自作、通常経路（`SaveNewPart`で`図形/自作`へ保存） | `ExpectedCustomPartLabelDy` |
| 3 | 自作、`図形/`直下へ手動配置（`Category=""`）を模した経路 | `ExpectedCustomPartLabelDy`（Categoryで判定しておらぬことの直接固定） |
| 4 | 未解決PartId（ライブラリに存在しないId） | 従来どおり-1.5（`e.Kind`=ContactNOの値、現状維持） |

**#3の実装**：`PartFolderStore`の`Category`判定に依らず自作扱いされることを検証するため、
`vm.PartPalette.SaveNewPart`ではなく`PartFolderStore`相当の直接配置（`Category=""`固定）で
定義を仕込む。侍実装時、`PartPaletteViewModelCrudTests`等の既存手法を踏襲されたい
（隠密は経路のみ指定し、具体的なヘルパ実装は実装者の判断に委ねる——0-2節の不変条件どおり）。

### 9-3. 組込み非波及・対称性の回帰網（眼目(b)、2節の「変わらぬ」6種）

```
[Theory]
[InlineData(PartRole.TimerContactNO)]
[InlineData(PartRole.ThermalOverload)]
[InlineData(PartRole.SelectSwitch)]
Placement_CustomPartWithAlreadyZeroOffsetRole_RemainsZero(PartRole role)
```
- 2節「変わらぬ」6種のうち3種を代表として選ぶ（PR-27に照らし、退化・対称を避けた代表選定——
  Timer系・熱動系・切替系という異なる`ComponentKind`写像先を横断する3種）
- 自作パーツで配置し、絶対dyが0のまま（修正の前後で不変）であることを固定

```
[Theory]
[InlineData(BasicPartTemplates.CoilId, -5.72)]
[InlineData(BasicPartTemplates.ContactNOId, -1.5)]
[InlineData(BasicPartTemplates.TerminalId, -2.0)]
Placement_BuiltinPart_LabelOffsetUnaffectedByFix(string builtinId, double expected)
```
- **組込み側の回帰網**。2節「変わる」8種のうち組込み固定Idを持つ3種（Coil・ContactNO・Terminal＝
  最大移動幅を含む）を代表とし、自作向けの修正が組込みへ波及しておらぬことを直接固定する
- 既存の`DiagramRendererLabelTests.Render_PartIdPlacedCoilWithoutLabelDy_UsesCoilDefaultLabelDy_MatchingDirectKindElement`
  と`T097LabelDyTests`の複数件が、これと同じ性質を既に部分的に固定している——**本修正のPRでは
  これら既存テストがGREENのままであることも合否条件に含める**（新規テストを足すだけでなく、
  既存網が壊れていないことを確認）

### 9-4. 二消費者の食い違い検証（0-1節、侍が挙げた眼目）

```
[Fact]
CustomCoilPart_RenderedLabelPosition_MatchesPropertyPanelDefault()
```
- 自作Role=Coilパーツを配置
- `DiagramRenderer.Render`+`RecordingRenderer`で実際に描かれた機器名ラベルのY座標を取得
- `vm.SelectedElementLabelDy`（相対値、未設定なら"0"のはず）と`Params["LabelDy"]`未設定を確認
- 「描画側が実際に使ったdy」と「プロパティパネルが基準とするdefaultDy」を突き合わせ、
  **描画側だけを直してプロパティパネル側を直し忘れた実装では本テストがREDになる**ことを設計上の
  ねらいとして明記（0-1節の警告どおりの穴を機械的に検出する）

### 9-5. Role横断の対称性チェック（眼目(c)、2節「変わる」8種の網羅性）

```
[Theory]
[InlineData(PartRole.ContactNO)]
[InlineData(PartRole.Coil)]
[InlineData(PartRole.Terminal)]
[InlineData(PartRole.NonSimulated)]
Placement_CustomPartWithAffectedRole_UsesExpectedOffset(PartRole role)
```
- 2節「変わる」8種のうち4種（-1.5系の代表ContactNO・最大値のCoil・次点のTerminal・
  構造的既定値経由で異質なNonSimulated）を代表選定
- いずれも`ExpectedCustomPartLabelDy`（3節）に揃うことを確認——**「role=Coilだけを特別扱いした
  実装」（侍の設計書230行目の懸念そのもの）を検出する網**。Coil以外の1種でもズレていれば、
  Coil限定の場当たり実装だったことが判る

### 9-6. 個別値`Params["LabelDy"]`優先の回帰（既存契約の保持）

```
[Fact]
CustomCoilPart_WithExplicitLabelDy_OverridesDefaultRegardlessOfFix()
```
- 自作Role=Coilパーツを配置し、`vm.SelectedElementLabelDy`へ明示値を設定
- 描画側のY座標が明示値どおりになること（`ExpectedCustomPartLabelDy`の変更に一切影響されぬこと）を確認
- 「既定値を適用せぬ」の射程は"既定"のみであり、使い手の個別指定（密集回避の手動調整、既存機能）
  を殺してはならぬという不変条件を固定する

---

## 6. RED実測の段取り（侍実装前の確認手順）

1. 9-1節を先に実装し、**現行コード**（案(1)適用前）に対して実行する
   → RED（絶対dy=-5.72のまま）を確認・スクリーンショットまたはテスト出力を残す
2. 9-3節「組込み非波及」のTheoryは現行コードでも既にGREEN（このコミット時点では自作/組込み
   区別が無いためではなく、組込みIdをそのまま使えば元より-5.72等が出るため）——**修正前から
   GREENなテストは回帰網としての価値はあるが、RED先行証明の対象ではない**ことを実装者は
   区別されたい
3. 9-2節#3（Category=""の稀経路）は、現行コード（弁別手段が無い）に対しては#2と同じく
   REDになるはず——もしGREENであれば、それは9-2の設計意図と異なる偶然の一致であり、
   実装前に隠密へ再確認されたい

---

## 7. 確認依頼（家老経由で殿へ、または家老裁量の範囲）

1. **3節の数値**（読みA=0.0 か 読みB=-1.5 か）——殿の一次のお言葉を確認できておらぬため、
   着手前に一言いただきたい。**UI/UXの見え方に直結する数値ゆえ、家老裁量では決めずお諮り
   されたい**（`memory: feedback_route_design_decisions_to_user`の射程と判ずる）
2. **2節「変わる」8種すべてに手当てを及ぼすか**——殿裁可の文言は「自作パーツには既定値を
   適用せぬ」と全Role共通の言い回しであった旨、家老の伝聞から読み取ったが、実害（重なり）が
   実測されたのはRole=Coil（ソレノイド）のみ。他Role（自作ContactNO等）は現状ラベル位置に
   問題が無い可能性があり、**「直さなくてよいものまで動かす」変化点として殿の視野に入れて
   おいていただきたい**（対処自体は変えずともよいが、変化の射程は認識を揃えたい）

以上二点は実装のブロッカーではない——9節のテストはいずれも定数一本（3節）または対象Role一覧
（2節の表）を差し替えるだけで追随できる形にしてある。侍は本書のテストケース一覧を土台に、
まず0-2節の不変条件（Categoryに依らぬ弁別）を満たす実装を組まれたい。
