# P-187 着手前調査（隠密起草）——サーマルリレーa/b role・DRC-XREF-002合流

8/17の枠。殿ご裁可＝「サーマルリレーa/bのrole」＋「役割を正した結果に残る消せぬ警告全般
（`DRC-XREF-002`死にリレー含む）」。手隙ゆえ明日の段取りを厚くする目的で先組みする。

**結論を先に書く**：`proposed.md`起票時の見積り「roleを書き換えるだけなら極小」は、
**サーマルリレーa/bについては成り立たぬ**（1節）。加えて**サーマルリレーa/b側とソレノイド側は
形の違う問題**であり、同じ手当てでは塞がらぬ（3節）。

---

## 1. サーマルリレーa/bの「roleを書き換える」案は電気的に成り立たぬ

`Evaluator.cs:179-184`（`IsConducting`相当の分岐）を一次ソースで確認した。

```
ElementKind.ContactNO or ElementKind.PushButtonNO => state,
ElementKind.ContactNC or ElementKind.PushButtonNC
    or ElementKind.EmergencyStop or ElementKind.ThermalOverload => !state,
```

`ElementKind.ThermalOverload`は**常に`!state`（NC＝b接点相当）一種類のみ**——既存の
`サーマル.gcadpart`（Role=ThermalOverload）はこの一種類しか持たぬ。

対して`サーマルリレーa`は現在`Role=ContactNO`（→`state`、a接点として正しい）、
`サーマルリレーb`は`Role=ContactNC`（→`!state`、b接点として正しい）——**両者ともElement
Catalog.cs `BasicPartTemplates.cs:485/504`のコメントどおり「電気的な振る舞いは通常の接点と
同一」で、現状の実装は正しい**。

もし起票時の見立てどおり両者を`Role=ThermalOverload`へ書き換えれば、**a接点(サーマルリレーa)が
NC(`!state`)へ化ける**——DRC警告は消えるが、シミュレーション結果が逆転する重大な回帰になる。
**「role書き換え」路線はこの一点で採れぬ。**

---

## 2. DRCの機構（一次ソース、`DesignRuleCheck.cs:55-97`）

`CheckCrossReference(doc, lib)`は`lib`を受け取る（`PartResolver.ComponentKind(elem, lib)`で
使用済み）——**`elem.PartId`個別の情報にも手が届く状態**にある、という点が3節の鍵。

- `isRelayContact = IsContact(kind) && !IsInputControlled(kind)`——**`kind`は`ComponentKind`
  経由でRoleから写した`ElementKind`のみを見る。個々のPartIdは見ていない**
- `isRelayCoil = kind is Coil or Timer`
- 機器名ごとに集計し、`hasContact && !hasCoil`→`DRC-XREF-001`、`hasCoil && !hasContact`→
  `DRC-XREF-002`

`IsInputControlled`（`ElementCatalog.cs:216-218`）は`PushButtonNO/NC・SelectSwitch・
EmergencyStop・ThermalOverload`の5種のみ。**サーマルリレーa/bは`ComponentKind`経由で
素の`ElementKind.ContactNO/NC`に写ってしまい、この5種のいずれとも一致しない**——これが
`DRC-XREF-001`が消せぬ根。

---

## 3. サーマルリレー側とソレノイド側は形の違う問題——同じ手当てでは塞がらぬ

### 3-1. サーマルリレーa/b（`DRC-XREF-001`側）＝固定Idを持つ組込み

`BuiltinPartIds.ThermalRelayNO`/`ThermalRelayNC`という**閉じた固定Id**が既に在る
（T-151期に新設、`BuiltinPartIds.cs`参照）。`DesignRuleCheck`に`lib`が届いておる以上、
**`elem.PartId`がこの2値かどうかを直接見て`isRelayContact`から除外する**——という局所的な
分岐一つで塞げる。ElementKind・PartRole・Evaluatorのいずれにも触れずに済み、**起票時の
「極小」という見立ての規模感には、この形なら合致する**。

### 3-2. ソレノイド（`DRC-XREF-002`側）＝自作パーツゆえ固定Idが無い

ソレノイドは自作パーツ（`Role=Coil`）。**サーマルリレーと違い、閉じたId集合が存在しない**——
PartId直指定の分岐は書けぬ。かつ「`Role=Coil`の自作パーツは`DRC-XREF-002`から一律免除する」と
すれば、**サーマルリレーとは非対称な別の問題を新たに生む**——ユーザーが「本物のリレーコイル」の
つもりで自作パーツを作り、接点を追加し忘れた場合の検出力を、まるごと失う。ソレノイドのような
「駆動対象であって、対応する接点が図面上に存在しない設計のコイル」と、「接点を追加し忘れた
本物のリレーコイル」を、**現状のデータモデル（`PartDefinition`）は区別する手段を持たぬ**——
`Role=Coil`という1ビットしか無い。

**すなわちソレノイド側を塞ぐには、`PartDefinition`へ新しいマーカー（例＝
「クロスリファレンス検査の対象外とする」真偽値、既定`false`）を足す形の、**スキーマ変更が要る。
これは(a)自作パーツエディタへの新設UIが伴う（UI/UXの分岐ゆえ殿への確認が要る）、
(b)`DesignRuleCheck`側の対応する分岐、の二段になる——サーマルリレー側（3-1）より一段重い。

### 3-3. 二つの型を一つの表で示す

| | サーマルリレーa/b | ソレノイド |
|---|---|---|
| 由来 | 組込み（固定Id） | 自作（Idは可変） |
| 弁別の手段 | `elem.PartId`直接一致で足りる | 新設マーカーが要る（Idでは弁別不可） |
| 変更の範囲 | `DesignRuleCheck.cs`1箇所 | `PartDefinition`＋エディタUI＋`DesignRuleCheck.cs` |
| UI/UXへの波及 | 無し | 有り（殿確認が要る） |
| 規模 | 起票時の見立て（極小）に合致 | 見立てより一段重い |

---

## 4. 家老・殿への申し送り（提案であって決定ではない）

1. **8/17の枠を割る案**——サーマルリレーa/b側（3-1、PartId直指定で局所対処）は起票時の
   見立てどおり小規模で片付く見込み。ソレノイド側（3-2）はスキーマ変更とUI確認を伴う分、
   同日で両方は重かろう。分けて進める、または後者を`P-187`から切り出し別途起票するかは
   殿・家老の裁量にて、某の判断ではない
2. **もしソレノイド側も8/17に含めるなら**、実装前にUI/UXの分岐（新設マーカーをエディタへ
   どう出すか、既定値・文言等）を殿へ諮る一手が要る（`memory:
   feedback_route_design_decisions_to_user`の射程と判ずる）
3. **暫定の次善策（もし時間が足りねば）**——ソレノイド側は今回見送り、`DRC-XREF-002`が
   ソレノイドで鳴り続ける状態を許容する案もあり得る（実害は「殿が目視で無視する」程度、
   `P-187`起票時の忍者所見に同じ）。これも殿の御裁可を要する

本書は着手前調査にて、実装はいずれの案でも侍が受け持つ。某はここまでで区切る。
