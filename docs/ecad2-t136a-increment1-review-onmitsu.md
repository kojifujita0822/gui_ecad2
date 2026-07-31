# T-136(A)増分1静的レビュー（1周目・軽量既定）

隠密（key=1785485896132）記す。2026-07-31。対象＝`98ee494`（push済み）。

## 結論：指摘なし。狙い撃ち3観点とも符合を確認

## (a) 台帳DoD整合

`docs/todo.md` T-136節（殿裁定2026-07-31、論点1・2）と実装を突き合わせた。

| 殿裁定 | 実装 |
|---|---|
| シート種別＝3値（Any/ControlOnly/MainCircuitOnly）、既定＝Any | `PartDefinition.cs`の`enum SheetAffinity`、`PartDefinition.SheetAffinity`の初期化子`= SheetAffinity.Any`で確認 |
| 枷に反した時＝メニュー無効化(予防)＋`ValidatePlacement`拒否(防御・サイレント) | 本増分は後者のみ対象（メニュー無効化は増分2）。`ValidatePlacement`は例外・メッセージなしで`false`を返すのみ、呼び出し元は`if (!ValidatePlacement(...)) return;`——サイレント拒否を確認 |
| T-133裁定4（3極記号は主回路シート限定）も同じ箱に乗る | `ElementCatalog.SheetAffinityOf`が`Breaker3P`/`ContactorMain3P`/`ThermalOverload3P`を`MainCircuitOnly`と宣言、`PartResolver.SheetAffinityOf`が`PartDefinition`/`ElementCatalog`の二分岐を一元解決——計画書§2の設計どおり実装されている |

`PartResolver.SheetAffinityOf`/`Ports`/`CreatesComponent`/`ComponentKind`の4メソッドはいずれも`lib?.Get(e.PartId)`のnull判定で同型に分岐しており、計画書が示した「既存3つと同じ形」を確認した。

## (b) 手動の観点確認（新規テスト26件の検出力）

件数の裏取り＝`Core 16`（`PartResolverTests.cs`のTheory/Fact数え上げ：3+4+1+1+1+6=16）／`App 10`（`T136SheetAffinityTests.cs`：6+1+1+1+1=10）。合計26件、commitメッセージの主張と一致（再現手段：diffのTheory `InlineData`個数とFact個数を直接数える）。

`dotnet test`で両ファイルを実行し全件合格を確認（`PartResolverTests`20件全合格＝既存4件＋新規16件、`T136SheetAffinityTests`10件全合格）。

**RED証明4件の机上検算**——実行によらず、各改変が「どの既存アサーションを反転させるか」を論理的に追跡した（共有main上での実注入は`onmitsu.md`【MUST】により行わず、静的推論のみで代替）。

| 改変 | 侍の実測 | 隠密の机上検算 |
|---|---|---|
| A（`IsAllowedOnSheet`の真偽入替え） | 12件RED | `IsAllowedOnSheet_3値と2種のシートの全6組合せ`(Core, 6ケース)と`自作パーツは枷に合うシートにのみ置ける`(App, 6ケース)は同一の6通り(affinity×mainCircuit)を独立に測っており、真偽反転で両方とも全6ケースが逆転する。6+6=**12件で一致** |
| B（`PartResolver.SheetAffinityOf`の二分岐逆転） | 2件RED | `SheetAffinityOf_自作パーツは定義の側から解決する`と`SheetAffinityOf_組込み種別は種別の側から解決する`(いずれもCore・Fact)がまさにこの分岐を狙い撃ちしており、分岐を逆にすれば両方とも失敗する。**2件で一致** |
| C（`ValidatePlacement`から枷の条件を落とす） | 5件RED | 拒否(`expect false`)を測るテストを数えた——App理論値2ケース(ControlOnly×main、MainCircuitOnly×制御)＋`枷に反した配置はUndo履歴を積まない`＋`枷に反する要素は移動できぬ`＋`組込み種別の要素は種別の側の枷で移動が決まる`の前段アサーション(Breaker3P)。2+1+1+1=**5件で一致** |
| D（3極記号の宣言を落とす＝`ElementCatalog.SheetAffinityOf`が常にAnyを返す） | 5件RED | `SheetAffinityOf_主回路3極記号は主回路専用`(Core,3ケース)＋`SheetAffinityOf_組込み種別は種別の側から解決する`(Core,Breaker3P使用)＋`組込み種別の要素は種別の側の枷で移動が決まる`(App,前段アサーション)。3+1+1=**5件で一致** |

4件とも報告値と机上検算が一致し、RED証明とテスト内容の整合に疑義なしと判ずる。

**気づき（軽微・依頼範囲外）**——`ValidatePlacement`の呼び出し元3箇所のうち、移動は2経路（ドラッグ`UpdateDragElement:1698`／矢印キー`MoveSelectedElement:1739`）あるが、新規テストは矢印キー経路のみを測り、ドラッグ経路の専用テストは無い。両経路は同一の`PartResolver.SheetAffinityOf(element, PartLibrary)`呼び出しを共有し分岐が無いため実害は小さいと見るが、`memory: feedback_praise_also_needs_scrutiny`と同種の「同型実装のカバレッジ非対称」に該当しうるため一応記録する。1周目軽量ゆえ要修正とは判じない。

## (c) 家老指定の狙い撃ち観点

**観点1（RED証明とテスト内容の整合）**——上記(b)の表のとおり、4件とも一致を確認。

**観点2（配置経路だけがPartResolverを通らぬ設計の非対称）**——`PlaceElementAtSelectedCell`（`MainWindowViewModel.cs:3024`付近）は`definition?.SheetAffinity ?? SheetAffinity.Any`と直接`PartDefinition`から引いており、`PartResolver.SheetAffinityOf`を呼んでいない。侍のコメントどおり「配置時にはまだ`ElementInstance`が無い」ため（`PartResolver.SheetAffinityOf`のシグネチャは`ElementInstance`必須）。

一致するかを一次ソースで追跡した——`definition`が非nullの場合、`PartResolver.SheetAffinityOf`も内部で同じ`part.SheetAffinity`を返す（同一の値）。`definition`がnull（未解決PartId）の場合、配置経路は`SheetAffinity.Any`に直行するが、この場合に生成される`ElementInstance.Kind`は（T-071の既知の性質どおり）既定値`ContactNO`のままであり、`PartResolver.SheetAffinityOf`が辿るなら`ElementCatalog.SheetAffinityOf(ElementKind.ContactNO)`も同じく`Any`を返す（3極記号でないため）。**両分岐とも結果が一致することを一次ソースの追跡で確認した。侍の「今は実害なし」という見立ては正しいと判ずる。**

**観点3（枷は移動にも効く、実装が申告どおりか）**——`UpdateDragElement`・`MoveSelectedElement`双方が`PartResolver.SheetAffinityOf(element, PartLibrary)`を`ValidatePlacement`へ渡しており、新設の専用テスト`枷に反する要素は移動できぬ`（部品定義を後から`MainCircuitOnly`へ変更→移動不可）・`組込み種別の要素は種別の側の枷で移動が決まる`（Breaker3Pは移動不可、ContactNOへ変えれば移動可）の両方が実測どおりGREENで通ることを確認した。**申告どおりの挙動であり、仕様の当否は殿の領分ゆえ判じない。**

## 出典

`98ee494`の全diff直読（`git show`）、`docs/todo.md` T-136節、`docs/ecad2-t136-implementation-plan-samurai.md`、`dotnet test`実測（`PartResolverTests`20件・`T136SheetAffinityTests`10件、いずれも全合格）。

## 派生提案

なし（上記「気づき」は軽微ゆえ本タスクの修正対象とは判じない。家老の判断を仰ぐ）。
