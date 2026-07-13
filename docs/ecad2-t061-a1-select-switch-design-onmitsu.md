# T-061 A-1 構造対処 設計調査書(隠密起草、2026-07-14)

対象: セレクトSWの電気的導通判定が構造的に機能しない問題(A-1、`docs/archive/ecad2-t061-review-onmitsu.md`)。
殿裁定=構造対処(規模大)採用。本書は「テスト設計と実装の分離」原則に則り、実装前に仕様側から方針を固める。
**本書は設計調査のみ、実装は含まない**(家老采配のスコープ境界どおり)。

---

## 1. 根本原因(コード直読で確定、推測なし)

`PartRole`(`src/Ecad2.Core/Model/PartDefinition.cs:6-13`)というenumには**`SelectSwitch`という値が
そもそも存在しない**(ContactNO/ContactNC/Coil/Lamp/Terminal/NonSimulated/InputNO/InputNC/
TimerContactNO/TimerContactNC/TimerInstantContactNO/TimerInstantContactNC/ThermalOverload/
EmergencyStopの14種のみ)。

`BasicPartTemplates.SelectSwitch()`(`src/Ecad2.Core/Persistence/BasicPartTemplates.cs:151-169`)は
`Role = PartRole.ContactNO`を明示指定している(コメント「セレクトSW: 2端子円＋切替バー（接点として
扱う・セレクトSW-NEW.gcadpart 由来）」、意図的に「電気的にはa接点と同一」という設計だった)。

`PartResolver.ComponentKind`(`src/Ecad2.Core/Model/PartResolver.cs:43-65`)は`part.Role`からの
switch式で、`PartRole.ContactNO => ElementKind.ContactNO`にマッピングされる。**`ElementKind.SelectSwitch`
へ到達するマッピングパスが存在しない**ため、実際に配置されたセレクトSWの`Component.Kind`は常に
`ElementKind.ContactNO`になる。

結果、以下の既存ロジック(いずれもセレクトSWを正しく扱う設計で既に実装済み)が**デッドコード化**している:

- `NetlistBuilder.BuildComponents`(`NetlistBuilder.cs:313-314`)の`kind == ElementKind.SelectSwitch`
  ゲート → `Component.SwitchPosition`が常に0固定
- `Evaluator.IsConducting`(`Evaluator.cs:148-150`)の`c.Kind == ElementKind.SelectSwitch`分岐
  (ノッチ位置一致判定) → 到達不可能、一般のContactNO判定(172-184行)に必ず落ちる

---

## 2. 対処方針: PartRoleへSelectSwitch値を追加しマッピングを完成させる

### 2-1. 変更内容(最小構成、rule of three対応=新規判定ロジックは書かない)

1. **`PartDefinition.cs:8`**: `PartRole`enumへ`SelectSwitch`を追加。
   ```csharp
   public enum PartRole
   {
       ContactNO, ContactNC, Coil, Lamp, Terminal, NonSimulated, InputNO, InputNC,
       TimerContactNO, TimerContactNC, TimerInstantContactNO, TimerInstantContactNC,
       ThermalOverload, EmergencyStop,
       SelectSwitch,   // 新規(A-1構造対処)
   }
   ```
2. **`BasicPartTemplates.cs:157`**: `SelectSwitch()`の`Role = PartRole.ContactNO`を
   `Role = PartRole.SelectSwitch`へ変更。
3. **`PartResolver.cs:47-64`**: `ComponentKind`のswitch式へ`PartRole.SelectSwitch => ElementKind.SelectSwitch,`
   を追加。

この3点だけで、既存の`NetlistBuilder`のSwitchPosition取り込み・`Evaluator.IsConducting`のノッチ判定が
**新規コード無しでそのまま機能する**(両者とも「正しくSelectSwitchとして解決されること」を前提に
既に実装済みだったため)。

### 2-2. 横展開必須の追加修正(見落とすと機器表分類が壊れる)

**`MainWindowViewModel.cs:2127-2136`の`ResolveDeviceClass`**:
```csharp
private DeviceClass ResolveDeviceClass(ElementInstance element)
{
    var entry = PartPalette.Entries.FirstOrDefault(e => e.Definition.Id == element.PartId);
    if (entry is { Category: "", Definition.Role: PartRole.ContactNO, Definition.IsOrEligible: false })
        return DeviceClass.SelectSwitch;
    ...
}
```
現状は「セレクトSWはRole=ContactNOのためComponentKind経由では区別できない」という制約
(コメント2118-2119行に明記)を前提に、Category+Role+IsOrEligibleの3条件パターンマッチで
フォールバック的に識別している。2-1の変更後は`Definition.Role: PartRole.ContactNO`の条件が
セレクトSWにマッチしなくなるため、**この行を`PartRole.SelectSwitch`へ書き換えないと機器表分類が
壊れる**(これがPR-01/PR-05型の「横展開漏れ」再発ポイントになりうる、要注意)。

**シンプル化の機会(実装時に家老/侍判断、本書では提案に留める)**: 2-1の変更後は`ComponentKind`
経由で`ElementKind.SelectSwitch`が正しく解決されるようになるため、`MapToDeviceClass`
(`MainWindowViewModel.cs:2108`に既存の`ElementKind.SelectSwitch => DeviceClass.SelectSwitch`
マッピングあり)が正規経路で機能するようになり、`ResolveDeviceClass`の2130行の特殊フォールバック
分岐自体が理論上不要になる可能性がある。ただし自作パーツの誤判定防止(コメント2124-2126行)との
関係を再検討する必要があり、範囲拡大を避けるため**本書は「最小修正(条件値の書き換えのみ)」を
基本案とし、分岐削除は任意提案とする**。

### 2-3. 影響なし(確認済み、変更不要)

- **`PartThumbnailRenderer.cs:43-44`**: `isOr && definition.IsOrEligible`がゲートであり、
  セレクトSWは`IsOrEligible=false`(既定値、OR接点機能とは無関係)のためこの分岐に到達しない。影響なし。
- **`.gcad`図面ファイルのシリアライズ**: `PartRole`は`ElementInstance`(`Element.cs:44-71`、図面に
  保存される要素)のプロパティには存在せず、`PartId`参照経由で実行時に`PartDefinition`(パーツ定義
  ファイル側)から解決される。図面データ自体への影響は無い。
- **`MainWindowViewModelTests.cs:346-383`の`MapToDeviceClass_...`テスト**: リフレクション経由で
  `MapToDeviceClass(ElementKind)`を直接検証しており`PartRole`/`ComponentKind`経路を通らないため
  無関係。

---

## 3. 既存ユーザー環境への反映(マイグレーション処理、殿裁定=選択肢2採用・恒久対応をA-1スコープに含める)

### 3-0. 【訂正】前版の事実誤認

前版(初稿)は「T-037で確認された同型の反映問題は恒久対応が未実装のまま」と報告したが、これは
**誤りだった**。一次情報(実コード)を再確認したところ、`PartFolderStore.Enumerate()`
(`PartFolderStore.cs:77-89`)内に、**T-037往復3周目で既にマイグレーション処理が実装・テスト済み**
であることを確認した(対応するテストは`tests/Ecad2.Core.Tests/PartFolderStoreTests.cs:154-199`他)。
前版の誤りは、Explore委譲時に参照したのが往復2周目時点の「レビュー指摘」文書
(`docs/archive/ecad2-t037-review-onmitsu-2.md`)のみで、その後の実装コミット(往復3周目)を実コードで
確認していなかったことが原因。ここに訂正する。

### 3-1. 既存の確立パターン(T-037、IsOrEligible補正)

```csharp
// PartFolderStore.cs:77-89(Enumerate()内、Id重複チェックより前)
if (!def.IsOrEligible && (def.Id == BasicPartTemplates.ContactNOId || def.Id == BasicPartTemplates.ContactNCId))
{
    def.IsOrEligible = true;
    try { PartLibrarySerializer.SaveOne(def, file); } catch { /* ベストエフォート */ }
}
```

特徴: `Enumerate()`(パーツ列挙、アプリ起動時に`PartPaletteViewModel`経由で毎回実行)内で、固定Id
限定・単一フィールド限定の差分検出→書き戻しを行う。書き戻し失敗はtry-catchでベストエフォート
(読み取り専用・OneDrive同期中でも起動を止めない、T-039の教訓)。冪等(既に補正済みなら`!def.IsOrEligible`
がfalseで何もしない)。

### 3-2. 採用方式: 上記パターンをそのまま踏襲する(新規メソッド不要)

`SeedBasics()`(新規ファイル生成)とは別に、`Enumerate()`内の同ブロック直後へ、セレクトSW用の
同型マイグレーションを追加するだけで足りる。(a)基本図形限定の起動時マイグレーション処理という
当初案の枠組みと同じだが、**既存の実装場所・パターンを再利用するため新規基盤は不要**、(b)の
SchemaVersion導入は見送る(過剰、KISS)。

```csharp
// A-1構造対処(T-061): 旧版JSON(PartRole.SelectSwitch追加より前)はセレクトSWがRole=ContactNOの
// まま保存されている。固定Id(SelectSwitchId)のときだけRole=SelectSwitchへ補正する
// (上記IsOrEligible補正と同型パターン、T-037踏襲)。ユーザーが意図的に他のRoleへ変更していた
// 場合は尊重し上書きしない(ContactNOのままの場合のみ対象)。
if (def.Id == BasicPartTemplates.SelectSwitchId && def.Role == PartRole.ContactNO)
{
    def.Role = PartRole.SelectSwitch;
    try { PartLibrarySerializer.SaveOne(def, file); } catch { /* ベストエフォート */ }
}
```

配置位置: `PartFolderStore.cs`の既存IsOrEligible補正ブロック(85-89行目)の直後。Id重複チェック
(94行目以降)より前に置く(コピー耐性、T-037と同じ理由=本ファイルが後でコピーにより新Id再採番
されても、書き戻し内容には補正後のRoleが引き継がれる)。

### 3-3. 統合方法・呼び出しタイミング

新規メソッド不要。`Enumerate()`は既に`PartPaletteViewModel`コンストラクタ経由でアプリ起動毎回
呼ばれており(`SeedBasics()`とは別系統の既存呼び出し)、追加ブロックもこの既存経路へ自動的に乗る。
`SeedBasics()`(未展開ファイルの新規作成)と`Enumerate()`内マイグレーション(既存ファイルの是正)は
役割が異なり、両者の実行順序(`PartPaletteViewModel`コンストラクタで`SeedBasics()`→`Enumerate()`
相当の順)を変える必要は無い(未展開ならSeedBasicsが新規作成、既存なら次のEnumerate等でマイグレーション
対象になる)。

### 3-4. 安全策(影響範囲)

1. **自作パーツ・他の基本図形への誤爆防止**: 対象は`def.Id == BasicPartTemplates.SelectSwitchId`
   という固定Id完全一致のみ。IsOrEligible補正(ContactNOId/ContactNCIdの2件限定)と同じ限定方式。
2. **他フィールドの非破壊**: 修正対象はRoleのみ。ユーザーがPrimitives(見た目)やNameを編集していても
   `def`の他プロパティはLoadOne時点のまま保持され、Role変更後にSaveOneで書き戻すため保持される。
3. **ユーザーの意図的な変更の尊重**: `def.Role == PartRole.ContactNO`(旧デフォルト値そのもの)の
   場合のみ対象とする。ユーザーが何らかの理由でRoleを別の値に変更済みなら対象外(上書きしない)。
4. **書き戻し失敗のベストエフォート**: 既存パターンと同じtry-catch。

### 3-5. テスト設計(既存`PartFolderStoreTests.cs`への追加、既存パターンを型として再利用)

既存の`Enumerate_LegacySelectSwitchJsonWithoutIsOrEligible_StaysFalse`(179-199行目、固定Id補正の
対象を限定していることの再混入防止テスト)と同じ固定Id・同じ手法で、以下を追加する:

| テスト名 | 検証内容 |
|---|---|
| `Enumerate_LegacySelectSwitchJsonWithContactNORole_BackfillsToSelectSwitchRoleAndSaves` | 旧版JSON(`"role":"contactNO"`、固定Id=SelectSwitchId)が`Role=SelectSwitch`に補正され、ファイルにも書き戻されること(`...IsOrEligible_BackfillsTrueAndSaves`と同型) |
| `Enumerate_SelectSwitchAlreadyMigrated_NoChangeIdempotent` | 既にRole=SelectSwitchのファイルは再実行しても変化しない(冪等性) |
| `Enumerate_UserCustomizedSelectSwitchRole_NotOverwritten` | ユーザーが意図的に別Role(例:ContactNC)へ変更済みのセレクトSW.gcadpartは上書きされない(安全策3の検証) |
| `Enumerate_OtherBasicPartWithContactNORole_NotAffected` | 固定Id=ContactNOId(通常のa接点)は対象外(Id不一致)でRole=ContactNOのまま変化しない(誤爆防止) |
| `Enumerate_LegacySelectSwitchJsonReadOnly_BackfillsInMemoryWithoutThrowing` | 読み取り専用ファイルでの書き戻し失敗時も例外を伝播させず起動を止めない(既存の同型テストを流用) |

### 3-6. 後方互換性(JSON直列化形式の確認)

`JsonOptions.Default`(`src/Ecad2.Core/Persistence/JsonOptions.cs:18`)は
`Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }`を使用しており、`PartRole`
は文字列直列化(`"contactNO"`等)される。**enum値の追加(`SelectSwitch`)は既存の整数値ズレを起こさず、
既存ファイルの`"role":"contactNO"`はそのまま正しくデシリアライズされる**(T-071設計書の既存知見と
一致、確認済み)。

---

## 4. 影響範囲の総括(file:line)

| 種別 | file:line | 内容 |
|---|---|---|
| 要修正(必須) | `PartDefinition.cs:8` | `PartRole`に`SelectSwitch`追加 |
| 要修正(必須) | `BasicPartTemplates.cs:157` | `Role`値変更 |
| 要修正(必須) | `PartResolver.cs:47-64` | `ComponentKind`switchへケース追加 |
| 要修正(必須・横展開) | `MainWindowViewModel.cs:2130` | `ResolveDeviceClass`条件値変更 |
| 影響なし(確認済み) | `PartThumbnailRenderer.cs:43-44` | `IsOrEligible`ゲートで到達せず |
| 影響なし(確認済み) | `.gcad`図面ファイル形式 | `PartRole`は図面に非保存 |
| 影響なし(確認済み) | `MainWindowViewModelTests.cs:346-383` | Kind直接検証、経路と無関係 |
| 対処済み(3節、殿裁定=選択肢2) | `PartFolderStore.Enumerate()` | マイグレーション処理追加(T-037パターン踏襲、新規メソッド不要) |
| テスト新設要 | `PartFolderStoreTests.cs` | マイグレーション処理の回帰テスト5件(3-5節) |

`switch (part.Role)`という網羅的switch式は`PartResolver.cs:47`の1箇所のみ(Grep確認済み)。同箇所は
`_ => throw new InvalidOperationException(...)`というcatch-allを持つため、`SelectSwitch`ケース追加を
仮に忘れてもコンパイルエラーにはならず、実行時に例外がthrowされる(未対応時に気づきやすい安全設計、
ただし2-1で明示的に追加するため通常は発生しない)。

---

## 5. テストケース設計(体系的技法適用、`onmitsu.md`「テスト設計の起草」節準拠)

### 5-1. 状態遷移(セレクトSW配置→ノッチ切替→シミュレーション評価)

| 現在状態 | 事象 | 遷移先 | 検証すべき副作用 |
|---|---|---|---|
| 未配置 | セレクトSW配置(ノッチ0接点) | 配置済み | `PartResolver.ComponentKind`が`ElementKind.SelectSwitch`を返す |
| 配置済み(ノッチ0/1の2接点、同一デバイス名) | テストモードでノッチ0接点をクリック/Enter | ノッチ位置=1へ | `NetlistBuilder`の`Component.SwitchPosition`が正しく設定され、`Evaluate()`後ノッチ0接点は非導通・ノッチ1接点は導通 |
| ノッチ位置=1 | 再度クリックしてノッチ0へ戻す | ノッチ位置=0 | 逆方向も対称に動作(C-1修正=`CycleSelectSwitch`のUI状態管理と、本件=電気的評価の両方が一致すること) |

### 5-2. 境界値・同値分割

- 同値クラス: (a) ノッチ位置が接点の`SwitchPosition`と一致(導通) (b) 不一致(非導通)
  (c) 同一デバイス名で複数ノッチが同時に配置されている場合の排他性(1つのノッチのみ導通)
- 境界: `SwitchPosition`未設定(`Params[Position]`キー無し→`int.TryParse`失敗→既定値0のまま、
  `NetlistBuilder.cs:314`)の要素が、ノッチ0の接点として扱われることの明示的検証。
- `PartRole.SelectSwitch`が`CreatesComponent`(`PartResolver.cs:29-34`)でtrueを返すこと
  (`Role != NonSimulated`のみを見るため自動的にtrueになるはずだが明示テストで担保)。

### 5-3. ペア対称性(通常のContactNO要素への非干渉)

| 観点 | ContactNO(通常接点) | SelectSwitch |
|---|---|---|
| `PartResolver.ComponentKind` | `ElementKind.ContactNO`のまま(不変) | `ElementKind.SelectSwitch`(新規) |
| `Evaluator.IsConducting` | 一般ロジック(172-184行)、無変更 | セレクトSW専用分岐(148-150行)、到達可能に |
| `ResolveDeviceClass` | `DeviceClass.Relay`(不変) | `DeviceClass.SelectSwitch`(条件値変更後も同じ結果を維持することを回帰確認) |

`BasicPartTemplates.SelectSwitch()`のみを変更し他のRole定義(ContactNO/ContactNC等)には触れないため、
通常接点への影響は無いはずだが、既存の`GcadCompatibilityTests`(ContactNOの`.gcadpart`読み込み)と
`T061ModeFixTests`(`IsRealContactElement_ContactNOOrNC_IsTrue`等)の全件再実行で回帰確認する。

### 5-4. Theory活用(新規追加すべきテスト)

`NetlistBuilder`/`Evaluator`層(Core.Tests)に、UI状態管理層(`T061ModeFixTests`の
`TestModePress_SelectSwitch_CyclesNotchPosition`、これは`CurrentTestSession.State.Positions`のみ検証)
とは別に、**電気的導通そのもの**を検証する新規テストが必要:

```csharp
[Theory]
[InlineData(/*notch=*/0, /*switchPos=*/0, /*expectConducting=*/true)]
[InlineData(/*notch=*/0, /*switchPos=*/1, /*expectConducting=*/false)]
[InlineData(/*notch=*/1, /*switchPos=*/0, /*expectConducting=*/false)]
[InlineData(/*notch=*/1, /*switchPos=*/1, /*expectConducting=*/true)]
public void Evaluate_SelectSwitchNotch_ConductsOnlyMatchingPosition(...)
```
同一デバイス名で2ノッチ分の接点をシートに配置し、`State.Positions[deviceName]=notch`を設定して
`Evaluator.Evaluate`を実行、`EvalResult.ElementConducting[要素Id]`が期待通りになることを検証する
(B群修正=T-061で追加済みの`ElementConducting`をそのまま活用できる、rule of three対応)。

### 5-5. 後方互換

マイグレーション処理そのものの回帰テストは3-5節(`PartFolderStoreTests.cs`)で確定済み。
`GcadCompatibilityTests.cs`(GuiEcad実サンプルとの互換性検証が主目的)側への追加は必須ではないが、
GuiEcad由来の実セレクトSWサンプル(`.gcadpart`)が入手できるなら、`role: "contactNO"`のままの
実サンプルが正しくマイグレーションされることの追加確認として任意で検討してよい(優先度低)。

---

## 6. 不明点

- 2-2節の`ResolveDeviceClass`特殊分岐の削除可否(シンプル化提案)は実装時に家老・侍判断でよいか未確認。

## 7. 派生提案

なし。

## 8. 実装規模の総括

コア対処(2節): 3ファイル3箇所+横展開1箇所。マイグレーション処理(3節): 既存`Enumerate()`への
追加ブロック1箇所(新規メソッド・新規基盤不要)。テスト新設: `PartFolderStoreTests.cs`へ5件、
`Evaluator`/`NetlistBuilder`層へTheory1件。前版時点の「実装規模は当初懸念より小さい」という見立ては、
マイグレーション処理を恒久対応化した後も維持できる見込み(既存の確立パターンを再利用できたため)。
