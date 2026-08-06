# T-133増分7 着手前調査（組み込みパーツ2件・隠密）

> 2026-08-06 隠密。家老采配——増分7着手前、`karo.md`【MUST】に従い対象を原本一次ソースで固める。
> **既存調査書`docs/ecad2-t133-guiecad-motor-and-builtin-parts-survey-onmitsu.md`§2が
> thermal-relay a/bの完全な定義を既に確認済み**（着手前チェック、重複回避）。本書はその上で
> 家老が新たに問うた2点（サーマル(OL)との関係／17→19の検算）を埋める追補。

---

## 1. 原本の定義——既存調査書で完結済み（再掲）

`GuiEcad.App/Assets/Parts/thermal-relay-{a,b}.gcadpart`（`EmbeddedResource`）。

| 項目 | a | b |
|---|---|---|
| サイズ | 幅1・高さ1 | 幅1・高さ1（同一） |
| ポート | `L`(row0,boundary0)・`R`(row0,boundary1) | 同一 |
| Role | `contactNO` | `contactNC` |
| 図形 | 円2個＋横線1本(y=-0.1875)＋×印(横線の**上**) | 円2個＋横線1本(y=+0.1875)＋×印(横線の**下**) |

**通常の接点記号に、サーマル特有の×印を上下いずれかに足したもの**——電気的な振る舞い（role）は
通常のa/b接点と同一で、図面上の記号だけがサーマルリレー用に異なる。

---

## 2. ecad2側に近いものは既にあるか——**thermal-relay自体は無いが、紛らわしい別物が既にある**

### 2-1. 近いもの＝`ContactNO`/`ContactNC`（役儀と寸法が一致）

`BasicPartTemplates.cs:81-`（`ContactNO`）・`:100-`（`ContactNC`）は幅1・高さ1・
`Ports=TwoPorts()`（L/R水平2端子）——**サイズ・ポート配置ともthermal-relay a/bと完全一致**。
**移植は既存の`ContactNO`/`ContactNC`相当の器へ、図形定義（primitives）を差し替えるだけで
済む見込み**（既存調査書の見立てを再確認、`推測・実装規模の見積りは侍の領分`）。

### 2-2. 【家老の問い・要】既存の「サーマル(OL)」（増分6で移植済み）とは**別の実体**——一次ソースで確定

家老の懸念どおり、名称が紛らわしい実体が既に3つ存在する。**一次ソースを突き合わせ、
別物であることを確定した**。

| 実体 | 原本タグ | 原本描画元 | ecad2の現状 |
|---|---|---|---|
| **①サーマル(OL)**（単独） | `ThermalOverload` | `SymbolGlyphs.cs:172` `Thermal()`（コの字形） | **移植済み**（`basic-thermal-overload`、T-133増分6でメニュー再掲） |
| **②サーマル(OL) 2極** | `ThermalOverload3P#V`/`#H` | `SymbolGlyphs.cs:331` `ThermalOverload3P()` | **移植済み**（3極記号、T-133増分4） |
| **③thermal-relay a/b**（本増分の対象） | ―（`OtherBuiltins`配列に無し） | `.gcadpart`実体（円+線+×印） | **未移植**（増分7の対象） |

`GuiEcad.Core/Rendering/SymbolGlyphs.cs:37`（`case ElementKind.ThermalOverload: Thermal(...)`）・
`:47`（`case ElementKind.ThermalOverload3P: ThermalOverload3P(...)`）を直読し、①②はいずれも
**`ElementKind`経由の組込み描画**（`.gcadpart`を持たぬ）と確認——**③（`.gcadpart`実体）とは
実装形態からして別系統**。①のecad2側実装コメント「専用DXF未提供のため暫定形」（コの字形）も
③（正式な円+線+×印の接点記号）とは似ても似つかぬ図形であり、**混同の実害は無い**。

**申し送り（侍への注記）**——**名称が紛らわしい**。③のパーツIdは`ThermalOverloadId`
（`"basic-thermal-overload"`、①が既に使用中）と衝突せぬよう、原本ファイル名
（`thermal-relay-a`/`thermal-relay-b`）に倣った別Idを採ること。**表示名（`Name`）も
「サーマル(OL)」（①）と区別できる語（「サーマルリレーa」等、原本`name`フィールドの表記ゆれは
正字へ整える——既存調査書の副次所見）を採るのが筋**。

---

## 3. 部品リストが17→19件へ変わる根拠——**検算・正しいと確認**

### 3-1. `BasicPartTemplates.All()`は現在15件（一次ソースで数え直し）

`BasicPartTemplates.cs:61-78`の`All()`を直読——`private static PartDefinition`メソッド15個
（`ContactNO`〜`EmergencyStop`）を配列へ列挙、**過不足なく15件**。

### 3-2. 表示件数17は「15＋OR2」——計画書の記述と一致

`PartPaletteViewModel.cs:75-76`（`Entries.Where(e => e.Category == "" && e.Definition.IsOrEligible)`
から`ORa`/`ORb`論理エントリを追加）。`IsOrEligible=true`は`BasicPartTemplates.cs`内**2箇所のみ**
（`:88`＝`ContactNO`、`:107`＝`ContactNC`）——`grep`で全件確認済み、他に無し。

**すなわち現在の表示件数＝`BasicPartTemplates.All()`15件＋OR論理2件＝17件**。計画書
（`ecad2-t133-implementation-plan-samurai.md:334-337`）・忍者の実機UIA採取と一致。

### 3-3. 増分7後は19件——**単位を揃えて確認**

thermal-relay a/bを`BasicPartTemplates`へ追加すれば`All()`は15→**17件**。**この`17`は
表示件数の現状値`17`（15+OR2）とは別物**（`BasicPartTemplates.All()`自体の件数）——**同じ
数値`17`が二つの異なる単位を指す点に注意**（計画書の記述自体は正しいが、読み違えやすい）。

**表示件数（OR込み）**は`All()`が17件になった上でOR2件が加わり、**17→19件**。
**thermal-relay a/bに`IsOrEligible=true`を付与しない限り**（原本調査でもOR論理は
a接点/b接点専用と確認済み、role=`contactNO`/`contactNC`を持つからといって自動でOR適格には
ならぬ——`IsOrEligible`は明示フラグ）、OR件数は2のまま変わらぬ。

**結論＝「17→19」の予測は正しい。ただし17という数値が「`BasicPartTemplates.All()`の件数
（15→17）」と「表示件数（17→19）」の2つの異なる意味で使われており、忍者が基準値として
使うべきは後者（表示件数17→19）である旨を明記して申し送る。**

---

## 4. 射程・限界

- **実装規模の見積り・図形primitivesの精密な移し替えは侍の領分**——本調査は定義の突合と数の検算のみ
- **`SymbolGlyphs.Thermal()`／`ThermalOverload3P()`の中身（コの字形が業界標準として正確か等）は
  電気図面の専門知識を要し、既存調査書同様に本調査の範囲外**
- **thermal-relay a/bの実機での見え方は未検証**（一次ソース直読のみ、GuiEcad.App.exe起動確認はせず）

---

## 出典

- `docs/ecad2-t133-guiecad-motor-and-builtin-parts-survey-onmitsu.md`§2（thermal-relay a/b定義、既存）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.Core\Rendering\SymbolGlyphs.cs:37,47,172,331`
- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs:61-78,81-,100-,88,107,361-377`（全文直読）
- `src/Ecad2.App/ViewModels/PartPaletteViewModel.cs:75-76`
- `docs/ecad2-t133-implementation-plan-samurai.md:334-337`
