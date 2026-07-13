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

## 3. 【要殿確認】既存ユーザー環境への反映問題(T-037既知パターンの再発懸念)

`PartFolderStore.SeedBasics()`(`PartFolderStore.cs:141-153`)は「冪等：既存ファイルは上書きしない」
設計(`if (File.Exists(path)) continue;`)。つまり**既に一度でもアプリを起動し「図形/」フォルダへ
`セレクトSW.gcadpart`が展開済みの環境では、`BasicPartTemplates`側のRole変更(2-1)が自動的には
反映されない**。

これはT-037(`IsOrEligible`フィールド追加時)で既に実機確認込みでCONFIRMEDされた既知パターンの再発
(`docs/archive/ecad2-t037-review-onmitsu-2.md`)。当時提示された恒久対応案は(a)基本図形(Id=`basic-*`)
限定の起動時マイグレーション処理、(b)`.gcadpart`へのSchemaVersion導入、いずれも**未実装のまま**。

**本件はA-1構造対処そのものとは別軸の技術的負債であり、以下いずれかの方針を殿・家老に確認されたし**:

- **選択肢1**: 今回はマイグレーション未対応のまま進め、開発機の該当ファイル(`図形/セレクトSW.gcadpart`)
  を手動削除して再展開させる運用回避で済ませる(リリース済み環境が無い/少数なら現実的)。
- **選択肢2**: 恒久対応(マイグレーション処理新設)をA-1のスコープに含め、規模をさらに拡大する。
- **選択肢3**: マイグレーション処理を別タスク化し、`docs/proposed.md`等へ切り出す(A-1は構造対処のみ
  完了させ、反映は別問題として扱う)。

`docs/ecad2-release-procedure`(過去メモリ)によればv0.2リリース手順が既に確立されており、既存の
公開・配布状況次第でこの論点の緊急度が変わる。**隠密の判断範囲外のため、殿確認を仰ぐ**。

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
| 要検討(規模別軸) | `PartFolderStore.SeedBasics()` | 既存環境への反映問題、殿確認要(3節) |
| テスト新設要 | `GcadCompatibilityTests.cs` | SelectSwitch用の後方互換テスト無し |

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

### 5-5. 後方互換(GcadCompatibilityTests新設)

セレクトSWの`.gcadpart`を(旧版=Role未定義または`ContactNO`のまま保存されたファイルを想定した)
デシリアライズテストを追加し、後方互換の扱いを明示する。JSON上の`"role"`キーが存在しない/旧値の
場合にどう振る舞うべきか(既定値へのフォールバック挙動)を、3節の移行方針(選択肢1〜3)と合わせて
確定させる必要がある。

---

## 6. 不明点

- 3節の移行方針(選択肢1〜3のいずれを取るか)は隠密の判断範囲外、殿確認要。
- 2-2節の`ResolveDeviceClass`特殊分岐の削除可否(シンプル化提案)は実装時に家老・侍判断でよいか未確認。

## 7. 派生提案

なし(3節の移行問題は「派生」ではなくA-1本体に不可分の論点として本書に含めた)。
