# T-151 テスト設計（自作パーツ定義を図面へ埋め込む）

起票元＝`P-183`。台帳＝`docs/todo.md`の該当節（`T-151`）。殿ご裁可2026-08-14＝案(a)原本回帰。
本書は仕様側からの起草（隠密、家老采配2026-08-15）。侍はこれをコードへ落とす。設計にないテストの
追加は自由、設計にあるものを勝手に省くのは不可（既定運用どおり）。

太字は用いていない（このプロジェクトの通例どおり[1]で使用可だが、本書は表と箇条書きで足りるため）。

[1] 太字自体を禁ずる規則は`handover-next-session.md`のローカル運用にとどまり、他のdocsファイル
（本書もそれに倣う）には及ばない。以下、通常表記のみで書く。

---

## 0. 前提・一次ソースで確かめた事実

Exploreエージェントへの委譲調査＋自らの直読みで、以下を一次ソースから確認した。

### 0-1. 型定義

- `LadderDocument.Library`（`src/Ecad2.Core/Model/Document.cs:12`）＝`PartLibrary? Library { get; set; }`。
  nullable、初期化子なし（既定null）。コメント「ドキュメント埋め込みの自作パーツライブラリ（null =
  組込み種別のみ）」。
- `PartLibrary`（`PartDefinition.cs:94-99`）＝`Dictionary<string, PartDefinition> ById` と
  `Get(string? id)` のみ。
- `PartDefinition`（`PartDefinition.cs:70-91`）＝`Id`・`Name`・`WidthCells`・`HeightCells`・`Role`・
  `IsOrEligible`・`SheetAffinity`・`Ports`(`List<PortDef>`)・`Primitives`(`List<PartPrimitive>`、
  `[JsonPolymorphic]`で6種の派生型を型判別子付きで多態シリアライズ済み＝`PartLine`/`PartCircle`/
  `PartArc`/`PartRect`/`PartPolyline`/`PartText`)。

### 0-2. 保存側＝要素生成箇所は事実上1箇所へ収束（台帳の見立ては訂正を要す）

台帳`:96-97`は「経路が増えておる見込み」と記したが、これは起票時の隠密の見立てであり、侍の実測
（2026-08-15）で覆った——GuiEcadは5箇所に生成コードが分散していたが、ecad2は唯一の要素生成箇所
（`MainWindowViewModel.cs:3177-3237 PlaceElementAtSelectedCell(string partId, string deviceName,
bool isOr)`）へ、UI側の6本の導線（ツールバー直接配置・その他図形メニュー・部品選択リスト・Enter
キー、いずれも配置バー確定の`PlacementOkButton_Click`（`MainWindow.xaml.cs:4265-4283`）1点へ合流）
のうち5本が収束する構造になっている（隠密が独立に確認、家老経由で共有済み）。

**設計上の意味**＝保存側（DoD1）のテストは、この1箇所（`PlaceElementAtSelectedCell`）を核として
組み立てれば足りる。UI入口ごとに別テストを重ねる必要はない（重ねるとすれば「配置バーへ正しく
Tool.PartIdが渡るか」というUI結線の話であり、本設計の射程外＝`T136PlacementEntryPointsTests.cs`が
既に担う領域）。

なお`ElementInstance.DeepClone()`（`Element.cs:149-159`）は`PartId`をコピーするコードを持つが、
`src`のどこからも呼ばれていない（コピー&ペーストは未実装）。本設計の対象外。

### 0-3. 読込側＝単一の集約点を経由

`MainWindowViewModel.PartLibrary`（`:2919`、現状`{ get; }`でsetterなし）が、レンダリング・DRC・
ネットリスト・プロパティパネル（の一部）・PDF出力のすべてが読む単一の入口になっている（現状は
`PartPalette.Library`をそのまま参照、`:3734`）。

**設計上の意味**＝DoD2（読込側切替）のテストは、この`PartLibrary`プロパティが観測できる値・
そこから導かれる各機能の出力を確かめれば足り、侍がどう配線し直すか（`PartLibrary`をsetter付きに
して`ReplaceDocument`で差し替えるか、内部で都度マージするか等）という実装方式には依存しない形で
設計する。

### 0-4. 侍の下調べで判明した2つの分岐点（家老経由、2026-08-15）

侍が実装着手前に下調べを行い、本設計に影響する2点を先んじて報告した。

**分岐点1＝`ResolveDeviceClass`のCategory依存**（`MainWindowViewModel.cs:3162-3171`）。
`PartPalette.Entries`（ローカルフォルダ由来、`Category`というフォルダ配置由来の属性を持つ）を
`element.PartId`で検索する分岐が先頭にあり、`Category:"", Role:SelectSwitch, IsOrEligible:false`
の3条件が揃った時だけ`DeviceClass.SelectSwitch`を早期returnする。`Category`は`PartDefinition`に
存在しないフィールド（`PartSelectionEntryViewModel`側＝フォルダ配置に属す）ゆえ、`Document.Library`
は`Category`を持ち得ない。

隠密が独立に一次ソースを確かめた結果（`send_message`で家老へ報告済み、2026-08-15）＝

- `PartResolver.ComponentKind`（`PartResolver.cs:102-127`）の`PartRole.SelectSwitch =>
  ElementKind.SelectSwitch`と、`MainWindowViewModel.MapToDeviceClass`（`:3130-3143`）の
  `ElementKind.SelectSwitch => DeviceClass.SelectSwitch`は、いずれも`IsOrEligible`を一切参照しない
  （両関数を全文精読して確認）。すなわち`Role=SelectSwitch`である限り、フォールバック側
  （`CreatesComponent`→`ComponentKind`→`MapToDeviceClass`、`PartLibrary`経由）は`IsOrEligible`の
  値によらず常に`DeviceClass.SelectSwitch`を返す。
- ゆえに`Category`分岐の`IsOrEligible:false`条件は、その組（`Role=SelectSwitch`かつ
  `IsOrEligible=true`）が実在するか否かに関わらず結果へ影響し得ない（実在確認＝
  `BasicPartTemplates.cs`を`grep`、`IsOrEligible=true`は`:115,:134`の2箇所のみでいずれも
  ContactNO/ContactNC。セレクトSW定義`:184-195`に明示代入なく既定false。実在しないことも確認済み
  だが、上の論理がその実在有無に依存しない点がより強い保証）。

**【2026-08-15追記・殿ご裁可により確定】** 家老より、殿が案2（Categoryゲート分岐を削り
`ComponentKind`経由へ一本化）を裁可された旨の連絡を受けた。T-061当時の家老裁定（最小修正ゆえ
分岐は残す）を覆した理由＝当時は「`Category`が引ける」ことが前提であったが、`Document.Library`へ
移せばその前提自体が崩れる。範囲拡大を許したのではなく、前提が変わったゆえ裁定を引き直した形
（家老の弁）。**裁可の根拠は隠密・侍の二重確認が揃って初めて立った**——侍が「`ComponentKind`と
`MapToDeviceClass`の二つの写像が同じ結果へ届く」ことを裏取りし、隠密が「`IsOrEligible`は
`ComponentKind`経由の判定に一切関与しない」ことを全文精読で確認。片方だけでは足りなかった。

**本設計での扱い**＝上記により実装方針は案2に確定した。プロパティパネル用途の主テストは、この
分岐に触れない`IsSelectedElementTimerRelated`（`Role`のみに依存、`Category`不使用）を用いる
（4節）。加えて、案2適用後も「`Role=SelectSwitch`の要素は`IsOrEligible`の値によらず
`DeviceClass.SelectSwitch`に分類される」という不変条件を回帰テストとして固定する（6-4節）。

**【既存テストによる裏付け・2026-08-15追記】** 実は`ResolveDeviceClass`のCategory分岐を直接狙う
既存の回帰テストが既にある——`MainWindowViewModelTests.cs:280-354`
`PlaceElementAtSelectedCell_ClassifiesSelectSwitchByDataFieldsNotFixedId`（T-045増分B由来、
`[Theory]`4ケースA/B/C/D）。隠密が4ケースを机上で再検算した結果、**いずれも案2で結果が変わらない**：

| ケース | 内容 | 案2適用後の経路 | 期待値 |
|---|---|---|---|
| A | 組込みセレクトSW（`Category=""`, `Role=SelectSwitch`, `IsOrEligible=false`） | branch2のみ→`ComponentKind`→`SelectSwitch` | `DeviceClass.SelectSwitch`（一致） |
| B | 自作だが基本図形フォルダ直下に再配置（`Category=""`, `Role=SelectSwitch`, `IsOrEligible=false`） | 同上 | `DeviceClass.SelectSwitch`（一致） |
| C | 純正ContactNO（`Role=ContactNO`） | branch2のみ→`ComponentKind`→`ContactNO` | `DeviceClass.Relay`（一致） |
| D | 自作接点（`Category="自作"`, `Role=ContactNO`, `IsOrEligible=false`） | 同上 | `DeviceClass.Relay`（一致） |

すなわちこの既存`[Theory]`テストは、案2実装後も**無改変のまま**回帰の網として機能する
（侍のDoD5「既存テストを触ったなら件数と理由を明記」に対しては「触らずに済む」旨を報告できる
見込み）。ただし4ケースとも`Document.Library`は`null`（`NewDocument()`直後）のまま検証しており、
`vm.PartLibrary`のフォールバック経路（`Document.Library=null`→ローカル解決、後方互換）が正しく
保たれて初めて成り立つ点に注意——本設計6節の後方互換テストと合わせて初めて全体の安全が立つ。

新規に補うべきは、この既存テストが**扱っていない**`IsOrEligible=true`かつ`Role=SelectSwitch`の
仮想ケース（実在しないが、案2の安全性の論拠そのものを固定する。家老の弁＝「実在せぬことを
知ったうえで両方を測るのは、将来その組が生まれた時の網」）のみ（6-4節）。

**分岐点2＝`PartLibrary`が生成時固定**（`:2919`、setterなし）。侍は`ReplaceDocument`
（新規/開くの単一ゲートウェイ）を仕掛け所と見ている。既存テスト`PartPaletteViewModelCrudTests.cs:99`
の`Assert.Same(libraryBefore, vm.PartLibrary)`は「`PartLibrary`は生成後ずっと同一インスタンス」を
固定した回帰テストであり、`ReplaceDocument`で差し替える設計にすれば文書を開き直した時点で
インスタンスが変わり得るため、この主張自体が成り立たなくなる見込み。

**本設計での扱い**＝このテストの扱い（更新／削除／意味の書き換え）は侍の実装時のDoD5（既存テストを
触ったなら件数と理由を明記）の対象として申し送るのみとし、本書では新規に書き直さない。ただし
「文書を開き直した時、`PartLibrary`の内容が新しい文書のものに切り替わり、旧文書の内容が残留しない」
という状態遷移は、まさにこの仕掛け所（`ReplaceDocument`）を狙うテストとして4-3節に設計する。

---

## 1. 同値分割・境界値分析

### 1-1. `Document.Library`の状態空間

| 区分 | 内容 | 代表例 |
|---|---|---|
| null | 未着手・レガシー | 既存`.gcad`、`NewDocument()`直後 |
| 非null・空 | `ById.Count=0` | 自作パーツを一度も配置していない新規文書（後述の遅延初期化次第では通過しない区分もあり得る） |
| 非null・1件 | 通常ケース | - |
| 非null・複数件（異なるId） | 通常ケース（複数種の自作パーツ） | - |

### 1-2. 要素側`PartId`の状態空間

| 区分 | 内容 | 既存の担保 |
|---|---|---|
| null／空文字列 | ビルトイン記号（`ElementKind`経由）。Library無関係 | `PartResolver.IsUnresolvedPartId`の`!IsNullOrEmpty(e.PartId)`ガード（既存、変更不要） |
| 非空、どちらのLibraryにも無い | 従来のDRC-PART-001ケース | `DesignRuleCheckPartIdTests.cs`で既存担保、回帰確認のみ |
| 非空、ローカルにはあるがDocument.Libraryには無い（後方互換時） | レガシー文書＋既存ローカル定義 | 5節 |
| 非空、Document.Libraryにある | 本タスクの主眼 | 4節 |

---

## 2. 状態遷移（保存側＝配置）

| 開始状態 | 操作 | 終了状態 | 検証観点 |
|---|---|---|---|
| `Document.Library=null` | 自作パーツ"X"を配置（`PlaceElementAtSelectedCell("X", ...)`） | `Library`非null、`ById["X"]`が値としてローカル定義と一致 | 遅延初期化の境界（nullから非nullへの遷移そのもの） |
| `Document.Library={X:...}` | 同じ"X"を再度配置（2個目のX要素） | `ById`は依然`{X}`の1件、例外なし | 冪等性・重複キー処理の頑健性（境界値） |
| `Document.Library={X:...}` | 別の"Y"を配置 | `ById={X, Y}`の2件、Xの内容は変化しない | 複数件の独立性 |
| いずれか | ビルトイン記号（`PartId`なし、`ElementKind`直接指定の3極記号等、`:3315`の経路）のみを配置 | `Document.Library`は操作前と不変（nullならnullのまま） | 対象外パスへの無干渉確認 |

`[Theory]`適用例（冪等性・複数件を1本のパラメタライズドテストで束ねる）：

```
[Theory]
[InlineData(1, 1)]  // 同じXを1回だけ配置 → ById.Count==1
[InlineData(2, 1)]  // 同じXを2回配置      → ById.Count==1のまま(Yを混ぜない)
public void PlaceElementAtSelectedCell_EmbedsDefinitionIdempotently(int placementCount, int expectedCount)
```

---

## 3. 対称性・退化性チェック（PR-27適用）

**罠**＝配置直後にローカル定義と埋め込み定義の値を比べるだけのテストは、両者が値として一致するのが
自明（配置時点でローカル値をそのまま複製するため）ゆえ、「本当に複製（ディープコピー）されたか、
それとも同一オブジェクトの参照を共有しているだけか」を区別できない。参照共有のまま出荷されると、
台帳の検証観点「ローカルでパーツを直しても既存図面が変わらぬこと」（`docs/todo.md`T-151節）という
仕様の核心が満たされない。

**対策**＝検出力を持たせるため、以下の2段構えで設計する。

1. 配置直後＝埋め込み値がローカル値と一致すること（複製できていることの基本確認）
2. 配置後にローカル定義を書き換える（例＝`WidthCells`を変更）＝埋め込み値は書き換え前の値のまま
   （＝ローカルの現在値とは異なる値になる）ことを確認

(2)の方が検出力が高い——(1)だけでは参照共有の穴を検出できない。(2)なら「参照共有か複製か」が
一撃で分かる（ローカルを書き換えた瞬間、参照共有なら埋め込み側も一緒に変わってしまう）。

**【2026-08-15訂正・侍指摘、隠密が独立に裏取り】** 上記(2)の「ローカル定義を書き換える」手段に
`PartPaletteViewModel.SaveEditedPart`を使うと、参照共有の検出力が失われる。一次ソース確認
（`PartPaletteViewModel.cs:74-88 Load()`・`:136-142 SaveEditedPart()`）＝`SaveEditedPart`は
`_store.SaveCustom(part)`でディスクへ書き出した後`Load()`を呼び、`Load()`は`Library.ById.Clear()`
してから`_store.Enumerate()`の結果で**丸ごと作り直す**。すなわちディスク経由の往復で、ローカルの
`PartDefinition`は配置時に参照した古いインスタンスとは**別の新しいオブジェクト**へ差し替わる。
実装が仮に参照共有のまま出荷されていても（配置時に捕まえた古いインスタンス参照を埋め込み側が
保持し続けているだけで）、`SaveEditedPart`後は「ローカルの現在値」自体が新インスタンスへ移って
しまい、埋め込み側（古いインスタンスのまま）とは自然に値が異なる——**参照共有のバグが、この
手段では検出できず素通りしてしまう**（`memory: ecad2_comparison_target_identity_pitfall`と同型。
比較対象の同一性を先に固定しないまま「値が違う」を見て「複製できている」と誤認する）。

**対策＝検出力を持たせる主テストは、ローカルの`PartDefinition`インスタンスを直接書き換える形に
する**（`PartPaletteViewModel.Library.ById["X"].WidthCells = 99`のように、ディスクを経由せず
同一インスタンスのプロパティを直接変更）。これなら「配置時に捕まえたインスタンスと、直接書き換えた
インスタンスが同一か否か」がそのまま参照共有の有無に対応し、確実に検出できる。

```
[Fact]
public void PlaceElementAtSelectedCell_ThenMutateLocalDefinitionInstanceDirectly_EmbeddedValueStaysAtPlacementTime()
{
    // 準備: ローカルにWidthCells=5の"X"を用意し配置
    // 操作: vm.PartPalette.Library.ById["X"].WidthCells = 99 (同一インスタンスを直接書き換え、
    //       SaveEditedPart経由のディスク往復は使わない)
    // 検証: vm.Document.Library.ById["X"].WidthCells は 5 のまま(99ではない)
}

[Fact]
public void PlaceElementAtSelectedCell_ThenSaveEditedPart_EmbeddedValueStaysAtPlacementTime()
{
    // 実操作の導線確認(侍提案、参照共有の検出力は上記が担う。こちらは
    // SaveEditedPart→Load()という実際の編集操作の経路そのものが正しく動くことの確認)
    // 準備: ローカルにWidthCells=5の"X"を用意し配置
    // 操作: vm.PartPalette.SaveEditedPart(新WidthCells=99の定義, oldPath)
    // 検証: vm.Document.Library.ById["X"].WidthCells は 5 のまま(99ではない)
}
```

同じ観点は4節（読込側）の識別用定義データにも適用する——各用途のテストで用いる埋め込み定義の
値（`WidthCells`・`Role`・`Ports`数）は、組込みContactNOの既定値と意図的にずらす。「たまたま
組込み既定へのフォールバックと同じ絵になって見分けがつかない」という迂回経路を塞ぐためである。

**【2026-08-15訂正・侍指摘、隠密が独立に裏取り】** ただし「ずらしたつもり」が実際にずれているかは
個別に一次ソースで確認しないと、本節が戒めているのと同じ罠（比較対象の同定を固定せずに「違う値を
選んだつもり」で安心する）に自分自身がはまる。4節の識別用データは以下の2点を訂正する（詳細は
4-1節）：

- `Ports`＝2点は組込みContactNOの既定と**同数**だった（`ElementCatalog.cs:60-80`の既定分岐が
  `L`(境界0)・`R`(境界width)の2点を返す）。3点以上へ改める
- PDF/クロスリファレンス観点＝`Role=TimerContactNO`は`ElementCatalog.IsContact`
  （`ElementCatalog.cs:191-197`）で`ContactNO`と同じく`true`を返すため、未解決時のContactNO
  フォールバックと区別がつかない。`Role=Coil`（`IsLoad`側、`:200-201`）の部品を別途用意し、
  `Coils`側に入るか`Contacts`側に入るかで弁別する

---

## 4. 読込側の対称確認表（DoD2の核心・5用途 × ローカル在/退避）

`onmitsu.md`の既定作法（横並びで呼ばれる処理群は表を埋めよ）に倣う。5用途は同じ入口
（`MainWindowViewModel.PartLibrary`）を読むが実装は独立しており、1つが直っても他が直っていない
横展開漏れが起こり得る（過去のmemory: 修正の横展開確認と同型のリスク）。ゆえに5用途すべてへ
同一パターンの検証を適用する。

### 4-1. 共通の準備（以下「埋込済み文書」と呼ぶ）

- `ElementInstance{ PartId="custom-x", DeviceName="CR1" }`を持つ`Sheet`を1枚持つ`LadderDocument`
- `Document.Library.ById["custom-x"]` に識別しやすい値を設定する：
  - `WidthCells=7`（`BasicPartTemplates.All()`が返す組込みのいずれとも一致しない値。既定`WidthCells=1`への退化フォールバック
    と取り違えないための境界値）
  - `Role=PartRole.TimerContactNO`（組込みContactNOの既定Roleと明確に異なる値を選び、
    「本当に埋込定義のRoleを見ているか、既定値へ落ちているだけか」を区別できるようにする）
  - `Name="埋込専用テスト部品"`（レンダリングでのテキスト直接確認用）
  - `Ports`を**3点**（`[2026-08-15訂正]`組込みContactNOの既定分岐＝`ElementCatalog.cs:75-79`は
    `L`(境界0)・`R`(境界width)の**2点**を返すため、2点では既定フォールバックと同数になり弁別
    できない。3点以上へ改める）
  - `Primitives`に最低2種の型を含める（例＝`PartLine`と`PartRect`。多態シリアライズの実地確認を
    兼ねる、6-4節参照）
- 加えて`Document.Library.ById["custom-coil"]`（`ElementInstance{ PartId="custom-coil",
  DeviceName="Y1" }`）を1件追加する。`Role=PartRole.Coil`（`[2026-08-15訂正]`PDF/クロス
  リファレンス観点の弁別専用。`ElementCatalog.IsContact`＝`ElementCatalog.cs:191-197`は
  `TimerContactNO`と`ContactNO`の両方に`true`を返すため、`custom-x`（TimerContactNO）だけでは
  未解決時のContactNOフォールバックと絵柄・分類が一致してしまい弁別できない。`Role=Coil`は
  `IsLoad`側（`:200-201`）に属し、フォールバック時の`ContactNO`（`IsContact`側）とは
  `Contacts`/`Coils`の分類そのものが分かれるため、これで初めて弁別できる）
- ローカルの`図形/自作`フォルダは**空**（`ViewModelTestBase`の既定のまま用意すれば足りる——
  テストごとに新規一時フォルダを発行する既存の仕組みが、そのまま「他所からこの文書だけを持って
  きた」状態を模する。これが最も厳しい検証条件——ローカルに同名の定義すら存在しないため、実装が
  `PartPalette.Library`側へフォールバックしてしまう抜け道があれば必ず失敗する）

### 4-2. 用途ごとの検証表

| 用途 | 検証方法（一次ソース裏付け） | 期待結果 |
|---|---|---|
| DRC | `DesignRuleCheck.CheckUnresolvedPartId(doc, vm.PartLibrary)`（`DesignRuleCheck.cs:283-304`） | 診断0件（DRC-PART-001が出ない） |
| レンダリング | 既存の`RecordingRenderer`パターン（`DiagramRendererLabelTests.cs`と同型のテストダブル）で`DiagramRenderer.Render(rec, sheet, vm.PartLibrary)`（`DiagramRenderer.cs:35,262,268`） | 例外なし。かつ`renderer.DrawnTexts`に`"埋込専用テスト部品"`相当のテキストが出現する、または描画プリミティブ数が組込みContactNOの定形と異なる（既定フォールバックへ落ちていないことを直接示す） |
| ネットリスト | `NetlistBuilder.Build(sheet, vm.PartLibrary)`（`NetlistBuilder.cs:18`） | 生成`Netlist`の端子数が`Ports=3点`を反映（`[2026-08-15訂正]`組込みContactNOの既定は2点のため、2点では弁別できない。3点にしたことでフォールバックとの取り違えを検出できる） |
| プロパティパネル | `vm.IsSelectedElementTimerRelated`（`MainWindowViewModel.cs:2412-2415`、`PartResolver.CreatesComponent`/`ComponentKind`のみに依存、`Category`不使用） | `Role=TimerContactNO`を反映し`true`を返す |
| PDF出力 | `PdfExporter.Export(doc, vm.PartLibrary, tempPath)`（`PdfExporter.cs:16,20`）→`CrossReferenceBuilder.Build`の結果 | 例外なく完了。`[2026-08-15訂正]``custom-x`（TimerContactNO）が`"CR1"`として`Contacts`側に、`custom-coil`（Coil）が`"Y1"`として`Coils`側に現れる。`TimerContactNO`単体では`ElementCatalog.IsContact`が未解決時のContactNOフォールバックと同じ`true`を返すため弁別できず、`Coil`（`IsLoad`側）を対にして初めて「フォールバックしていない」ことを示せる |

### 4-3. 状態遷移＝文書を開き直した時の切替（0-4節・分岐点2に対応）

```
[Fact]
public void ReplaceDocument_SwitchesPartLibraryToNewDocumentsEmbeddedLibrary_NoStaleLeak()
{
    // 準備: 文書A(Library={custom-x: WidthCells=7})を開く。vm.PartLibrary.Get("custom-x")が7を返すことを確認
    // 操作: 文書B(Library={custom-x: WidthCells=3, 別内容})または(Library=null)を開き直す(LoadFromFile/NewDocument)
    // 検証: vm.PartLibrary.Get("custom-x")が文書Bの値(3、またはローカル解決/null)を返す。7が残留しない
}
```

**検出力の一言**（`onmitsu.md`「消せば鳴るか」の作法に倣う）——4-2節・4-3節のテストは、もし実装が
`MainWindowViewModel.PartLibrary`の切替を怠り、依然として常に`PartPalette.Library`（空のローカル
フォルダ由来＝空`Dictionary`）を返し続けていたら、5用途すべてが必ず落ちるように値を選んである
（DRCは必ず発火、レンダリングは既定フォールバック、ネットリストは端子数不一致、プロパティパネルは
`false`、PDFはクロスリファレンス行が空になる）。4-3節は「差し替えた後、古い文書の内容が漏れて
残る」という別の穴（`SelectedCell`の暗黙クリア漏れ等、過去に何度も出た「残留」型の不具合と同根）
を狙う。

---

## 5. 後方互換（DoD3・DoD6隣接）

| ケース | 検証方法 | 期待結果 |
|---|---|---|
| `library`キーを含まないレガシーJSON文字列（`GcadCompatibilityTests.cs`の既存パターン＝`cellHeight`欠落テストに倣う） | `GcadSerializer.Deserialize(json)` | `Library`が`null`、例外なし |
| 上記文書をロードし、ローカルに同名パーツが存在する状態 | DRC／レンダリング等 | 従来どおりローカル解決で正しく動作する（`Library=null`でも動作が不変であることの確認。これが「後方互換」の実体） |
| `Document.Library=null`のままDRC／レンダリング／ネットリスト／PDFを呼ぶ | 各関数呼び出し | `NullReferenceException`等が起きない（nullガードの確認、境界値。各関数はいずれも`PartLibrary?`を受け取る設計になっており`lib?.Get(...)`のnull条件演算子で担保されている一次ソース確認済み——`PartResolver.cs`各メソッド） |

---

## 6. 具体的なテストケース一覧・配置先の提案

既存の命名慣習（`T<番号><主題>Tests.cs`）に倣う。侍の判断で分割・統合してよい。

### 6-1. `tests/Ecad2.App.Tests/T151PartLibraryEmbedPlacementTests.cs`（保存側、`ViewModelTestBase`継承）

- `PlaceElementAtSelectedCell_NullLibrary_EmbedsDefinitionAndInitializesLibrary`（2節1行目）
- `PlaceElementAtSelectedCell_EmbedsDefinitionIdempotently`（`[Theory]`、2節）
- `PlaceElementAtSelectedCell_MultipleDistinctParts_EmbedsIndependently`（2節3行目）
- `PlaceElementAtSelectedCell_BuiltinSymbolOnly_DoesNotTouchDocumentLibrary`（2節4行目）
- `PlaceElementAtSelectedCell_ThenMutateLocalDefinition_EmbeddedValueStaysAtPlacementTime`（3節、検出力の核心）

### 6-2. `tests/Ecad2.App.Tests/T151PartLibraryResolutionTests.cs`（読込側、`ViewModelTestBase`継承・ローカル空フォルダ）

- `CheckUnresolvedPartId_EmbeddedDefinitionWithEmptyLocalFolder_NoWarning`
- `Render_EmbeddedDefinitionWithEmptyLocalFolder_DrawsCorrectPrimitives`
- `NetlistBuild_EmbeddedDefinitionWithEmptyLocalFolder_ReflectsPortCount`
- `IsSelectedElementTimerRelated_EmbeddedDefinitionWithEmptyLocalFolder_ReflectsRole`
- `PdfExport_EmbeddedDefinitionWithEmptyLocalFolder_IncludesDeviceNameInCrossReference`
- `ReplaceDocument_SwitchesPartLibraryToNewDocumentsEmbeddedLibrary_NoStaleLeak`（4-3節）

### 6-3. `tests/Ecad2.Core.Tests/T151GcadLibraryCompatibilityTests.cs`（後方互換・シリアライズ往復）

- `Deserialize_LegacyJsonWithoutLibraryKey_LibraryIsNull`（`GcadCompatibilityTests.cs`パターン踏襲）
- `SaveThenLoad_EmbeddedLibraryWithPolymorphicPrimitives_RoundTripsExactly`（`PartLine`と`PartRect`を
  混在させ、`[JsonPolymorphic]`の型判別子が正しく往復することを実測。これはドキュメント内包という
  新しい文脈での初回確認であり、属性自体は既に付与済みだが実際にファイル往復を通す価値がある）
- `CheckUnresolvedPartId_NullLibrary_NoException`（5節3行目、代表1件。他4関数は侍の実装時に同型で
  追加してよい）

### 6-4. `MainWindowViewModelTests.SelectSwitchClassificationCases()`へケースE追加（0-4節分岐点1・案2の固定）

新規ファイルを起こすより、既存の`[Theory]`＋`MemberData`（`MainWindowViewModelTests.cs:280-354`、
6-4節冒頭で確認済みの実証済みハーネス＝`PartFolderStore`の一時フォルダへ`.gcadpart`を書き出し
`store.Enumerate()`経由で反映する方式）へケースを1つ足す方が、既存の枠組みをそのまま使えて
軽い。

```
// E: 自作セレクトSW(自作フォルダ、Role=SelectSwitch・IsOrEligible=true、案2の安全性そのものを
// 固定する仮想ケース。現存はしない組み合わせだが、Category=="自作"のためCategory==""ゲートは
// 元より通らず、案2でComponentKind経由に一本化されても結果は変わらないことを示す)。
yield return new object[]
{
    "custom-select-switch-or-eligible-guid",
    new PartDefinition
    {
        Id = "custom-select-switch-or-eligible-guid", Name = "自作セレクトSW", Role = PartRole.SelectSwitch,
        IsOrEligible = true, Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
    },
    true, // 自作フォルダ(Category=="自作")
    DeviceClass.SelectSwitch,
};
```

ケースA〜Dは0-4節の再検算のとおり案2適用後も無改変のまま通る見込みであり、ケースEのみが
新規の網を張る。侍のDoD5には「既存`[Theory]`のケースを1件追加（A〜Dは無改変）」と記録できる。

---

## 7. スコープ外・射程外の気づき（DoDには含めないが申し送る）

### 7-1. Undo後の孤児Libraryエントリ（`P-176`と同型の懸念）

`PlaceElementAtSelectedCell`は`UndoManager.RecordSnapshot(Document)`（`:3224`）の**後**に
`sheet.Elements.Add(newElement)`（`:3227`）を行っている。もし埋め込み（`Document.Library.ById[...]
= ...`）の挿入位置がこのスナップショットより**前**なら、配置をUndoで取り消しても埋め込み済みの
`PartDefinition`だけが`Document.Library`に孤児として残る（`P-176`＝`PinnedPartStore`の孤児Id問題と
同型。隠密が2026-08-14の引き継ぎで「器は寸分違わず残り、配線だけが丸ごと落ちる型」の二例目として
警戒していた対象がこれに当たる可能性がある）。

DoDに明記が無くスコープ外だが、実装時にどちらの順で書くかで挙動が変わる箇所ゆえ、侍が意識して
選べるよう申し送る。実害は「使われないエントリがJSONに残り少し太る」程度に留まる見込みで、
`P-176`と同じく実害は極小と隠密は見立てる（対処するかどうかは家老・侍の判断）。

### 7-2. `PartPaletteViewModelCrudTests.cs:99`への影響

0-4節・分岐点2のとおり。侍のDoD5（既存テストを触ったなら件数と理由を明記）の対象として申し送る。
本書では新規に書き直さない。

### 7-3. `MainWindowViewModel.PartLibrary`の内部実装方式は問わない

setter追加＋`ReplaceDocument`での差し替え、内部マージ、都度計算プロパティ化など、複数の実装方式が
あり得る。本設計のテストはいずれも`vm.PartLibrary`が返す値・そこから導かれる各機能の出力のみを
観測しており、特定の内部方式を前提にしていない。

---

## 8. 検証パイプライン

台帳どおり＝侍実装 → 隠密の静的レビュー → 忍者の実機確認。RED先行証明（DoD4）は上記各テストを
実装前に書き、現行コード（`Document.Library`未配線）に対して実行してREDになることで示す
（`CheckUnresolvedPartId`系は既存コードでも通る可能性があるため、埋め込み・読込双方が未実装の
現行コードに対して6-1・6-2節のテストが確実にREDになることを侍が確認されたい）。

---

## 9. 案Y'（開封時バックフィル）——文書内の部分的埋め込みと後方互換の交差点（2026-08-15追記、殿ご裁可済み）

侍が実装着手前に見つけ、隠密が独立に原本（GuiEcad一次ソース）を直読みして確認した論点。
本節が解決するまで6-2節の一部・後方互換の一部は着手できないが、**それ以外（2節・3節・6-1節の
保存側テスト全般、4-2節のうち"埋込済み文書のみで完結する"検証、6-3・6-4節）は本節の決着を
待たずに進めてよい**。

### 9-1. 穴の内容

1. レガシー文書（`Library=null`）を開く。自作パーツA・B（ローカルに定義あり）は従来どおり
   ローカル解決で正しく描画される
2. そこへ自作パーツCを新たに配置する→`Document.Library`が`null`から`{C}`へ遅延初期化される
3. もし読込側の切替が「`Document.Library`が非nullになった瞬間、以後は`Document.Library`のみを
   見る（個々のIdごとのローカルへのフォールバックを持たない）」という素朴な二値切替であれば、
   この瞬間A・Bが解決不能になり、`DRC-PART-001`へ黙って化ける——**本タスクが直そうとしている
   症状そのものが、実装の仕方次第で新たに生み出されてしまう**

### 9-2. GuiEcad一次ソースでの確認結果（隠密、2026-08-15）

`C:\Users\kojif\Desktop\生産物\gui_ecad\`を直読み。

- `GuiEcad.Core\Model\PartResolver.cs`（全54行）＝`Ports`/`CreatesComponent`/`ComponentKind`の
  いずれも`lib?.Get(e.PartId)`のみで判定し、ローカルフォルダへのフォールバックを一切持たない
- 呼び出し元（`MainPage.Drawing.cs`/`Drc.cs`/`Properties.cs`/`Menu.cs`/`Pointer.cs`/`Find.cs`/
  `PdfPreviewDialog.xaml.cs`、計18箇所`grep`確認）は例外なく`_document.Library`を直接渡す
- すなわちGuiEcadの読込側は「`Document.Library`が唯一絶対の情報源、個別フォールバック皆無」
  という純粋設計であり、ecad2でこの設計をそのまま持ち込めば9-1節の穴がそのまま再現する
- **なぜGuiEcad自身ではこの穴が表に出ないか＝「出にくい」のではなく「原理的に起こり得ぬ」**。
  `MainPage.Parts.cs`（配置・作成・編集の全5箇所）・`MainPage.Tools.cs`（1箇所）はいずれも
  `_document.Library ??= new PartLibrary(); _document.Library.ById[id] = def;`の対で書かれており、
  `Document.Library`フィールドと埋め込みロジックが不可分の一体機能として最初から実装されている。
  GuiEcad自身の中に「フィールドはあるが配線が落ちた文書」という状態が生じ得ない構造にある。
  **ecad2は逆で、フィールドだけが先にJSONスキーマ上存在し、配線が長らく落ちていた（`P-183`の
  症状の正体）——穴の原因そのものが、本節の移行問題の原因と同じ**。GuiEcadは参考にならず、
  ecad2固有の解が要る

### 9-3. 候補案（殿へ諮る材料、隠密は特定案を推さない）

| 案 | 内容 | 長所 | 短所 |
|---|---|---|---|
| X（読込時フォールバック） | `Document.Library`優先、そこに無いIdだけローカルへフォールバックする恒常的な二段読み | 実装は比較的単純。不意打ちが無い | 案(c)とは優先順位が逆だが「二段構え」という点で類する。他所の環境へ持ち出した際、未埋め込みの旧要素はなお解決できないまま残る（`P-183`の症状が一部残存） |
| Y（侍提案・配置時バックフィル） | `Document.Library`遅延初期化の瞬間に、文書内の全シート・全要素を走査しローカルで解決できるものを一括で埋め込んでから配置分を加える | 完了後はGuiEcadと同じ「埋め込みのみが情報源」という単純な終着点に至る。移植性も完全に回復する | 無関係な1個の新規配置が、既存の全要素のローカル現在値を問答無用で凍結する「不意打ち」がある（ローカルを編集中だった場合、意図せぬ時点の値が固定される） |
| Y'（開封時バックフィル） | Yと同じ走査だが、発火時機を「文書を開いた瞬間」に前倒しする | 不意打ち性が消える（開いた瞬間に一括で完結し、以後の配置操作の結果に左右されない） | 「開いただけで文書の中身が実質書き換わる」（保存すれば形式が進む）という別の性質を持つ。編集していないのに保存を促すダイアログが出る等、UI/UX上の波及があり得る |

### 9-4. 隠密の判断

3案とも「使い手から見える挙動の変化が、いつ・どの操作を引き金に起きるか」を左右する
UI/UXの分岐であり、殿ご裁可済みの「案(a)＝挙動の変化（ローカル変更が既存図面へ反映されない）」
の射程を、レガシー文書の移行局面へどう及ぼすかという未確定の一点にかかる。ゆえに家老裁量では
決めず殿へ諮る筋と判ずる（`memory: feedback_route_design_decisions_to_user`）。侍はテストを
先に書いて待機中——本節の決着を待たずに進められる範囲（本節冒頭）は先に進めてよい。

### 9-5. 【2026-08-15追記・殿ご裁可＝案Y'に確定】

決め手は不意打ち性の解消（家老の弁）。案Xは他所へ持ち出した際に未埋め込みの旧要素がなお解決
できず眼目を部分的に損なうため不採用。

以下、案Y'（文書を開いた瞬間に全走査バックフィルする）を前提に設計を確定する。

#### (a) バックフィルの対象・除外

`LoadFromFile`（＝`ReplaceDocument`）の折、`Document.Library`が`null`または非nullを問わず
（`Document.Library`が既に一部埋め込み済みの場合との重ね書きも起こりうる——0-4節で確認した
`ById[id]=def`の代入は上書き前提の書き方であり冪等）、文書内の全シート・全要素の`PartId`を走査し、
その時点でローカル（`PartPalette.Library`）に解決できるものを`Document.Library`へ埋め込む。
ローカルにも解決できないPartId（従来の`DRC-PART-001`ケース）は埋め込めないため対象外——これは
案Y'でも解消しない（そもそも定義が失われている以上、解消しようがない。9-6節のテストで明示する）。

#### (b) `IsDirty`の扱い（隠密の推奨、要・家老/殿確認）

**推奨＝バックフィル後は`IsDirty=true`とする**。理由＝案Y'の存在意義は「開いた瞬間に一貫した
規則で移行を終える」ことにあるが、`IsDirty=false`のまま（保存を促さない）だと、閲覧・印刷だけで
閉じられた場合に移行がファイルへ一切残らず、次回このファイルを他所で開いても同じ問題が再発する
——**Y'が案Xに対して主張する強み（移植性の完全回復）が、保存されて初めて実現する**ため。

**実装上の注意（既存コードの落とし穴、`:3591-3703 ReplaceDocument`実測）**＝現状`ReplaceDocument`
末尾`:3703`で`IsDirty = false`を明示している（コメント「新規/開く直後は未保存の変更が無い状態から
始まる」）。バックフィル処理をこの行より**前**に置けば、この既定のfalse代入に黙って上書きされ、
上記推奨が意図せず無効化される。バックフィルを行った場合は`:3703`より**後**で改めて`IsDirty=true`
へ変える必要がある——`7-1節`（Undo孤児）と同型の「リセット処理との前後関係」の罠であり、侍が
実装時に意識されたい。

ただし`IsDirty`の真偽自体は使い手から見える保存プロンプトに直結するUXの一部でもあるため、
上記は隠密の推奨に留め、家老・殿の最終確認を仰ぐ。

#### (c) 忍者への申し送り（実機観点、家老が采配文へ組み込まれたい）

`IsDirty`の値そのものは6-2節相当の単体テストで確定できるが、**その値が実機でどう見えるか**
（未保存プロンプトのダイアログが実際に出るか、文言が「何もしていないのに変更あり」という状況に
適切か、タイトルバーの変更マーク等）は静的テストでは確認できず、忍者の実機確認に委ねるべき観点
と判ずる。

観点案（家老の采配文への組み込み用）＝
1. 自作パーツを含むレガシー文書（`Library`なし、9-6節の検体と同型）を開き、**何も操作せず**
   閉じる（またはウィンドウを閉じる操作）。未保存確認ダイアログが出るか
2. 出る場合、文言が「何もしていないのに変更ありと言われて困惑する」ものになっていないか
   （殿・使い手の目線での違和感の有無）
3. 出た上で保存した場合、ファイルの`Library`に実際にA・Bの定義が書き込まれているか
   （JSON直接確認、または再度ローカルを退けて開き直し正しく解決されることの確認）

### 9-6. 案Y'の単体テスト（`tests/Ecad2.App.Tests/T151OpenTimeBackfillTests.cs`、`ViewModelTestBase`継承）

共通の準備＝ローカルに自作パーツ"A"・"B"（互いに識別しやすい値、3節の対称性チェックに倣う）を
用意し、`Library=null`かつ要素A・Bを持つレガシー文書のJSONを直接組み立てる（`GcadSerializer`で
一時ファイルへ書き出すか、`LadderDocument`を直接構築して`GcadSerializer.Save`で保存する）。

| テストケース | 検証内容 |
|---|---|
| `LoadFromFile_LegacyDocWithResolvableParts_BackfillsAllIntoDocumentLibrary` | 開いた直後（配置等の追加操作なし）に`vm.Document.Library.ById`が`{A, B}`の両方を含む |
| `LoadFromFile_LegacyDocWithUnresolvableParts_LeavesThatIdUnembedded_StillReportsUnresolved` | ローカルにも存在しないPartId（従来のDRC-PART-001ケース）はバックフィル後も埋め込まれず、`CheckUnresolvedPartId`が変わらず警告を出す（9-1(a)の除外を明示、5節の後方互換テストと整合） |
| `LoadFromFile_LegacyDocWithResolvableParts_SetsIsDirtyTrue` | 9-5(b)の推奨どおり`vm.IsDirty`が`true`になる（家老・殿の最終確認が`false`側に転じた場合は期待値を反転する） |
| `LoadFromFile_ThenMutateLocalDefinition_BackfilledValueStaysAtOpenTime` | バックフィル後にローカルのAを書き換えても、`vm.Document.Library.ById["A"]`は開いた時点の値のまま（3節のPR-27パターンをY'固有の経路にも適用、参照共有でないことの確認） |
| `NewDocument_NoElements_LibraryStaysNull` | 新規文書（要素0件）はバックフィル対象が無いため`Library`は`null`のまま（境界値、遅延初期化そのものを不必要に起こさないことの確認） |
| `LoadFromFile_AlreadyEmbeddedLegacyMix_MergesWithoutOverwritingExistingEmbeddedValues`（任意、実装次第） | `Document.Library`に一部（例＝Cのみ）既に埋め込まれた文書を開いた場合、バックフィルで新たにA・Bが加わってもCの既存値を壊さない（9-2(a)「重ね書きも起こりうる」の裏付け） |

---

## 9-7. 組込みパーツの扱い（2026-08-15追記・侍実装後の所見、隠密が独立に一次ソース確認）

侍が実装して初めて判明した所見——`BackfillDocumentLibraryFromLocalCatalog`
（`MainWindowViewModel.cs`新設）は`PartPalette.Library.Get(partId)`で解決するため、**組込み（`BasicPartTemplates.All()`の固定集合）も
`PartId`経路である以上バックフィル対象に入る**。帰結＝自作パーツを一切含まぬ図面でも、組込みを
1つでも使っていれば開いた瞬間`IsDirty=true`になる（ほぼ全ての既存図面が該当する見込み）。

### (a) 一次ソースでの確認（隠密、2026-08-15）

- **`SeedBasics()`は起動のたびに無条件で走る**（`PartPaletteViewModel.cs:63-70`のコンストラクタ、
  `store.EnsureFolders(); store.SeedBasics(); Load();`）。`SeedBasics`自体は冪等（既存ファイルは
  上書きしない、`PartFolderStore.cs:172-185`）。**すなわち組込み（`BasicPartTemplates.All()`の固定集合、実数は侍が実装中に機械で数え直し17件と確定）はどの環境でも、ローカルの
  図形フォルダを万一手動で消しても、次回起動で必ず復元され解決できる**——案B（組込みを対象外に
  する）の前提「埋め込まずとも解決できる筈」は構造的に成り立つ
- **ただしローカルの組込みファイルは版が変われば陳腐化しうる**（`PartFolderStore.cs:77-121`に
  実例3件——T-037`IsOrEligible`補正・T-061`SelectSwitch`Role補正・T-143モータ`PortKind`補正。
  いずれも「`SeedBasics`は既存ファイルを上書きしないため、コード側`BasicPartTemplates.All()`が
  直っても展開済みのファイルには届かない」ことへの後追いパッチ）。**すなわち組込み定義の
  「真の最新版」はコード（`BasicPartTemplates.All()`）であり、ローカルファイルはその写しに過ぎず
  陳腐化しうる、という構図が既に3回実現している**
- **GuiEcad原本が組込みも埋め込む理由を確認**——`MainPage.Parts.cs:210`のコメント「組み込み
  パーツ（EmbeddedResource）を配置対象にする」のとおり、GuiEcadの組込みパーツは**アセンブリへ
  コンパイル時に埋め込まれたリソース**であり、フォルダファイルを経由しない。すなわちGuiEcadの
  組込みは常にアプリのバージョンそのものと不可分で、**陳腐化という概念自体が存在しない**
  （ecad2の`SeedBasics`のような「フォルダへ一度書き出し、以後上書きしない」という中間層が無い）。
  **GuiEcadが組込みを埋め込んでも安全なのは、embedding対象が陳腐化し得ない値だからであり、
  ecad2のようにフォルダへ書き出した可変ファイルを埋め込む場合とは前提が異なる**

### (b) 隠密の見立て——案Bを推す（推奨、殿の最終判断を要す）

上記(a)から、単に「IsDirtyの不意打ちを避けるため」だけでなく、**もう一つ独立した理由**で案Bを
推す——**案A（組込みも埋め込む）は、ローカルの組込み定義がその時点で陳腐化していた場合、その
古い（誤りを含みうる）定義をその文書へ永久に固定してしまう**。案B（組込みは埋め込まない）なら、
文書を開くたび常にその時点で最新の組込み定義（`Enumerate()`が起動のたびに適用する既知バグの
後追い補正込み）で解決される——**T-151がまさに今回、組込みではなく自作パーツについて解こうと
している「移植性の分断」を、埋め込む対象を広げることで組込み側に新たに持ち込んでしまう**。

ただし1点、案Bの射程外を申し添える——通常の作成経路（`PartPaletteViewModel.SaveCustom`）は必ず
`図形/自作/`（`Category="自作"`）へ書くが、使い手がExplorer等で`.gcadpart`を直接`図形/`直下
（`Category=""`）へ手動配置した場合、これは実質「自作」でありながらCategory判定では「組込み」
扱いとなり案Bのバックフィル対象から漏れる。正規の作成経路を通らぬ手動操作に限られる稀な経路であり、
`P-183`の主眼（正規に作った自作パーツの移植性）は損なわれない。

### (c) 実装上の含意（案B採用時、侍への申し送り）

`BackfillDocumentLibraryFromLocalCatalog`・配置時の`EmbedPartDefinition`呼び出し（両方）を、
`PartPalette.Library.Get(partId)`ではなく`PartPalette.Entries`（`Category`を持つ）経由の解決へ
差し替え、`Category != ""`（＝自作フォルダ由来）の場合のみ埋め込む形にする必要がある——現状は
配置側・バックフィル側とも`Category`を見ずに一律埋め込んでいるため、両方の対称的な修正が要る
（片方だけ直すと横展開漏れになる、`memory`の修正横展開確認と同型）。
