# `DRC-XREF-001` が外部接点で消せぬ警告になる件——調査（忍者）

調査日: 2026-08-16（殿ご下問、家老経由。task_id未採番、調査のみ・実装には及ばず）

殿のご下問＝
> 外部接点のパーツは書き込み図面内に接点しか存在せず、DRC判定でコイルなしと判定されてしまう。
> 解消できない警告だが対処方法がないか？

出所＝T-151の修正前症状の実測（`docs/ecad2-t151-before-fix-symptom-ninja.md` 4節）で
忍者が副次症状として観測した `DRC-XREF-001` と同じものと家老が見立て、調査が下った。

---

## 結論（先に述べる）

除外の仕組みは既に在り、新設を要さぬ。実測で確かめた（6節）。

殿がご覧になっておる警告は、T-151 の穴——自作パーツの定義が解決できず
`ElementInstance.Kind` の既定値 `ContactNO` へ静かに落ちること——の副作用にござる。
すなわち T-151 が直れば自ずと消える。

ただし同型の症状が別の出どころに残る見込みがある（7節。サーマルリレーa／b）。

## 1. `DRC-XREF-001` の在り処と条件

在り処＝`src/Ecad2.Core/Simulation/DesignRuleCheck.cs`

| 何 | 場所 |
|---|---|
| コード定数 `ContactWithoutCoil = "DRC-XREF-001"` | `:24-25` 近傍（`:45` の `UnresolvedPartId` と同じ定数群） |
| 判定本体 `CheckCrossReference` | `:55-109` |
| 弁別の一行 | `:68` |
| 警告の生成 | `:89-92` |

弁別の一行（`:68`）＝

```csharp
bool isRelayContact = ElementCatalog.IsContact(kind) && !ElementCatalog.IsInputControlled(kind);
```

`kind` は生の `ElementInstance.Kind` ではなく `PartResolver.ComponentKind(elem, lib)` の戻り値
（`:66`）にて、自作パーツは `PartRole` から写像された値が入る。

鳴る条件＝機器名ごとに集計して「リレー接点が1つ以上あり、かつ駆動コイルが0個」（`:86-92`）。

なお `:65` に `if (!PartResolver.CreatesComponent(elem, lib)) continue;` があり、
`PartRole.NonSimulated` はここで先に落ちる。後述のとおり、これは外部接点とは別の経路にござる。

## 2. 外部接点を弁別する手がかり——既に在る

家老の当て推量（`PartRole` の `inputNO`／`inputNC` で弁別できる筋）は当たっておるが、
新たに作る要はござらぬ。既に配線されておる。

写像（`src/Ecad2.Core/Model/PartResolver.cs:106-126` `ComponentKind`）＝

| `PartRole` | 写像先 `ElementKind` | `IsInputControlled` | `DRC-XREF-001` |
|---|---|---|---|
| `InputNO` | `PushButtonNO` | true | 対象外（鳴らぬ） |
| `InputNC` | `PushButtonNC` | true | 対象外 |
| `SelectSwitch` | `SelectSwitch` | true | 対象外 |
| `EmergencyStop` | `EmergencyStop` | true | 対象外 |
| `ThermalOverload` | `ThermalOverload` | true | 対象外 |
| `ContactNO` | `ContactNO` | false | 対象（鳴る） |
| `ContactNC` | `ContactNC` | false | 対象 |
| `TimerContactNO/NC` | 同名 | false | 対象 |
| `TimerInstantContactNO/NC` | 同名 | false | 対象 |
| `Coil` / `Lamp` / `Terminal` | 同名 | ——（接点でない） | コイル側として集計 |
| `NonSimulated` | ——（`ComponentKind` は例外を投げる） | —— | `:65` で先に除外 |

`IsInputControlled` の定義（`src/Ecad2.Core/Model/ElementCatalog.cs:210-212`）＝
`PushButtonNO` ／ `PushButtonNC` ／ `SelectSwitch` ／ `EmergencyStop` ／ `ThermalOverload` の5種。

すなわち `role: "inputNO"` を持つ外部接点は、ライブラリで解決できてさえおれば
`PushButtonNO` へ写像され、`:68` で `isRelayContact = false` となって警告の対象から外れる。

## 3. ではなぜ殿の環境で鳴っておるのか

解決できておらぬからにござる。

`PartResolver.IsUnresolvedPartId`（`PartResolver.cs:98-99`）が示すとおり、
`PartId` が在っても `lib` で引けねば `ComponentKind` は `ElementInstance.Kind` を返す（`:104`）。
そして `PartId` 経路で置かれた要素の `Kind` は既定値 `ContactNO` のまま
（T-046由来の構造的制約。`ElementCatalog.cs:85-88` に同じ構図の説明がある）。

ゆえに外部接点は `role=inputNO` を持ちながら、解決できぬ限り
「役割の分からぬ、ただのa接点」として扱われ、リレー接点と見なされて警告が鳴る。

忍者の実測（T-151記録 5節）＝`sample/big_sample.gcad` でソレノイド定義をローカルへ置いたところ、
`DRC-XREF-001` が3件から1件へ減った。残る1件は外部接点a（置かなんだもの）にござった。

## 4. 除外側へ動かしてはならぬもの（巻き込みの検分）

`memory: feedback_type_safe_alternative_scope_check`（T-037でRole判定がセレクトSWを巻き込んだ実例）に従い、
`PartRole` の値を持つ部品を実際に全件洗い出した。

ローカル `…\Ecad2\図形\` の `.gcadpart` 22件（`図形` 直下17件＋`自作` 5件。
別途 `モータ.gcadpart.ninja-t133i8-backup-20260806` が在るが拡張子が異なり列挙外）。

| role | 件数 | 部品 | 現状の扱い |
|---|---|---|---|
| `contactNO` | 6 | a接点／サーマルリレーa／T136AFFANY／T136AFFCTRL／T136AFFMAIN／T136UIMAIN | 鳴る |
| `contactNC` | 2 | b接点／サーマルリレーb | 鳴る |
| `timerContactNO` | 1 | タイマ接点NO | 鳴る |
| `timerContactNC` | 1 | タイマ接点NC | 鳴る |
| `timerInstantContactNO` | 1 | タイマ瞬時接点NO | 鳴る |
| `timerInstantContactNC` | 1 | タイマ瞬時接点NC | 鳴る |
| `inputNO` | 1 | 押釦NO | 鳴らぬ |
| `inputNC` | 1 | 押釦NC | 鳴らぬ |
| `selectSwitch` | 1 | セレクトSW | 鳴らぬ |
| `emergencyStop` | 1 | 非常停止 | 鳴らぬ |
| `thermalOverload` | 1 | サーマル | 鳴らぬ |
| `coil` | 1 | コイル | コイル側 |
| `lamp` | 1 | 表示灯 | 接点でない |
| `terminal` | 1 | 端子台 | 接点でない |
| `nonSimulated` | 2 | モータ／T142検体NS | `:65` で除外 |

危うい手＝`PartRole.ContactNO`／`ContactNC` を除外側へ動かすこと。
a接点・b接点という最も基本の部品まで除外され、`DRC-XREF-001` が事実上死ぬ。
T-037の轍そのものにござる。

タイマ接点を動かすのも同断——`ElementCatalog.cs:207-208` のコメントが
「タイマ接点はタイマコイル励磁＋経過時間で制御するため含まない」と設計意図を明言しており、
これは駆動元が図面上に在ることを前提とした正しい扱いにござる。

## 5. 原本GuiEcadの扱い

対応物は在り、しかも一字一句同じにござった。

| | GuiEcad | ecad2 |
|---|---|---|
| `CheckCrossReference` の弁別 | `src/GuiEcad.Core/Simulation/DesignRuleCheck.cs:64` | `DesignRuleCheck.cs:68` |
| `IsInputControlled` | `src/GuiEcad.Core/Model/ElementCatalog.cs:78-80` | `ElementCatalog.cs:210-212` |

いずれも文言・対象5種とも完全に一致。原本にも「外部接点」という特別扱いは無い。

差分は ecad2 側に T-146（殿裁定2026-08-08）で加わった
`if (sheet.MainCircuit) continue;`（主回路シートを診断対象から外す一行）のみ。

すなわち原本で本件が問題にならぬのは、判定が違うからではなく
`Document.Library` が全面的に配線されており、外部接点の `role=inputNO` が
常に解決できるからにござる（T-151の起票理由そのもの）。

## 6. 実測——外部接点そのもので確かめた

当初、本書の結論はコード読解のみに拠っており、外部接点での実測を伴っておらなんだ。

忍者が先に実測しておったのはソレノイド（`role=nonSimulated`）にて、これは `:65` の
`CreatesComponent` が false で早期に落ちる経路にござる。
外部接点（`role=inputNO`）は `CreatesComponent` が true ゆえ `:65` を通り、
`:68` の `IsInputControlled` で外れる——結論は同じでも経路が別にござった。

ゆえに別途測った。侍の手が空いておるのを確かめてから実機を握った。

### 手順

`big_sample.gcad` の `library.byId` にある外部接点a の定義を、そのまま
`.gcadpart` として `図形\自作\` へ一時的に置き、同じファイルを開き直してDRCを1回走らせた。
ソレノイドの定義は置いておらぬ（前回の検証後に退避したまま）。

### 予測（着手前に書いたもの）

`DRC-PART-001` が2件・`DRC-XREF-001` が2件、いずれもソレノイド1・2のみ。計4件。
外れればそれ自体が所見になる。

### 実測

計4件。予測と完全一致。

| コード | 機器名 |
|---|---|
| `DRC-XREF-001` | ソレノイド1 |
| `DRC-XREF-001` | ソレノイド2 |
| `DRC-PART-001` | ソレノイド1 |
| `DRC-PART-001` | ソレノイド2 |

プログラムON（外部接点a）は両方の警告から消えた。
`OutputGrid` の `ScrollPattern` は `Scrollable=False`・`ViewSize=100` にて、取りこぼしは無い。

絵も定義どおりに描かれた——円2つ＋その上の矩形（`t151-10-crop-extcontact.png`）。
未解決時のa接点（縦2本の平行線）とは明らかに別物にござる。

### 何が示されたか

三軸が揃うた。

1. `DRC-PART-001` から消えた＝定義が解決された
2. `DRC-XREF-001` からも消えた＝`InputNO` → `PushButtonNO` → `IsInputControlled` が効いた
3. 絵が定義どおりになった

すなわち 2節の写像表は机上のものではなく、実際にそのとおり動いておる。
除外の仕組みは生きており、届いておらなんだのは role そのものであった。

### なお踏んだ罠（前回と同型）

1度目、メニュー項目 `設計チェック実行(D)` を掴み損ね（`$drc` が null）、DRCが走らぬまま
「0件」という結果を得た。前回の誤読と同じ型にござる。

今回は例外が出たゆえ即座に判ったが、例外が出ておらねば
「外部接点を置いたら全部消えた」と誤って報じておった——0件は
「ソレノイドの分まで消えた」ことを意味し、予測（4件）と食い違うゆえ気づけた筈ではあるが、
都合の良い方向への食い違いは見過ごしやすい。

対処＝メニュー展開と項目取得を最大3回まで繰り返す形にし、
各試行で展開状態と取得可否を出力するようにした（`ExpandCollapseState` と
項目の null 判定を毎回書き出す）。1回目で成った。

## 7. 範囲外検出——サーマルリレーa／b の role が揃うておらぬ

本件の調査中に見つけたもので、殿のご下問の範囲外にござる。
忍者は起票も対処もせず、事実のみ報ずる。

`図形` 直下に、名の似た3件が併存しておる。

| ファイル | role | `DRC-XREF-001` | 作成日時 |
|---|---|---|---|
| `サーマル.gcadpart` | `thermalOverload` | 鳴らぬ | 2026-07-12 |
| `サーマルリレーa.gcadpart` | `contactNO` | 鳴る | 2026-08-06 |
| `サーマルリレーb.gcadpart` | `contactNC` | 鳴る | 2026-08-06 |

サーマルリレーの接点は熱動素子で駆動されるものにて、図面上に駆動コイルは無いのが正常にござる。
にもかかわらず `contactNO`／`contactNC` ゆえ、T-151が直った後も
「消せぬ `DRC-XREF-001`」が残る筋にござる——すなわち殿のご下問と同型の症状が、
外部接点とは別の出どころで残りうる。

後の2件はT-133増分7で追加されたもの（作成日時から）。
role の選定が意図的か否かは忍者には判じかね申さぬ——
`thermalOverload` を選ばなんだ理由が在ったやもしれ申さぬゆえ、
当時の経緯を知る者（隠密・侍）の検分を要する。

【併せて申し添える】この一件は、殿のご下問への答えを「T-151が直れば済む」とだけ申し上げると
取りこぼす類にござる。T-151の修正後に改めてDRCを走らせ、
どの警告が残るかを数える一手が要ると忍者は見る。

---

## 付録・証跡

scratchpad 配下（`…\20b0c6a5-…\scratchpad\`）。

| ファイル | 中身 |
|---|---|
| `t151-09-extcontact-resolved.png` | 外部接点a を解決した状態。DRC 4件 |
| `t151-10-crop-extcontact.png` | 同・5倍拡大（円2つ＋矩形＝定義どおり） |
| `t151-07-crop-before.png` | 比較用。未解決時のa接点の絵 |
| `t151-backup\外部接点a.gcadpart` | 実測に用いた一時パーツ（退避済み） |
| `t151-backup\parts-md5-final.txt` | 実測後のMD5。着手前と差分なしを確認済み |

## 付録・調査に用いた手立て

- 一次ソースの直読＝`DesignRuleCheck.cs`／`PartResolver.cs`／`PartDefinition.cs`／`ElementCatalog.cs`
- 原本との突合＝`gui_ecad` 配下の同名2ファイルを行指定で読み、文言まで照合
- role の洗い出し＝ローカル `.gcadpart` 22件を `ConvertFrom-Json` で全件列挙してから集計
  （`onmitsu.md` の「数を報告する前に個々の項目を列挙してから合計を出す」に倣う。
  列挙を飛ばして `Group-Object` の数だけ出せば、拡張子の異なるバックアップ1件を
  取りこぼしておることに気づけなんだ）
