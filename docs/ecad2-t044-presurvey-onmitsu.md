# T-044 事前調査（隠密）

> 2026-07-07 隠密調査。対象タスク：OR自動配線の冗長縦分岐抑止（殿直接要望）。
> 家老指定観点(a)〜(d)。Exploreエージェントによるコード調査（事実と推測を峻別、出典付き）。

---

## 総合判断：**P-003の前提を壊すリスクは低い**

現状のコードは、実はすでに「母線境界（列0）に接する縦コネクタ」を母線接続判定の対象外として扱う設計に
なっている（`DiagramRenderer.LeftTerminator`・`NetlistBuilder.LeftRailReached`いずれも`c.Column > 0`
という既存条件を持つ）。このため、T-044が問題視する「冗長な縦分岐」は、**現状すでに母線への直結横線と
縦コネクタの両方が描かれる「二重接続」状態になっている**ことが原因と判明した。この事実により、
「新規行・基準行が列0にある場合は左側VerticalConnectorそのものを生成しない」という最小変更は、既存の
フォールバック機構に自然に乗るため、母線接続の欠落（浮き）も電気的な分断も起きない。

---

## 観点(a): OR自動生成で縦コネクタを作る箇所

**確認**：縦コネクタ生成は`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:333-373`の
`PlaceElementAtSelectedCell`内、`isOr=true`分岐（354-372行目）に一元化されている（呼び出し先メソッドは
なく、このメソッド自身が生成）。

```csharp
int leftColumn = Math.Min(baseElement.Pos.Column, pos.Column);
int rightColumn = Math.Max(baseElement.Pos.Column, pos.Column) + cellWidth;
sheet.Connectors.Add(new VerticalConnector { Column = leftColumn, TopRow = br, BottomRow = pos.Row });
sheet.Connectors.Add(new VerticalConnector { Column = rightColumn, TopRow = br, BottomRow = pos.Row });
```

現状は`isOr=true`である限り、左右2本の縦コネクタが常に生成される（例外なし）。基本パーツ（a接点/b接点）
は1セル幅・ポートoffset0/1のため、`leftColumn`は基準要素・新規要素いずれかの実際の列位置と一致する。
**新規行が列0（母線直近）に置かれた場合、`leftColumn=0`の縦コネクタが無条件に生成される**ことを確認した。

## 観点(b): 左分岐省略時の`LeftTerminator`前提崩壊の有無

**確認（決定的な事実）**：`DiagramRenderer.cs:373-380`の`LeftTerminator`は`c.Column > 0`という条件を
持つ（コメント「境界0（母線）は対象外」、**P-003修正前から存在する条件**）。つまり**列0の縦コネクタは、
たとえ`BottomRow==row`が成立していても、そもそも母線接続省略の判定対象になっていない**。

これを辿ると、`leftColumn==0`のケースでは：
- `LeftTerminator(sheet, 新規行, lb)`は列0のコネクタを候補にせず`null`を返す
- `DiagramRenderer.cs:332-336`により、`lt is null`分岐（母線から直接横線を描く＝母線直結線）が実行される

**結論**：現状すでに、列0に新規行がある場合は母線への直接横線が描かれている。つまり「新規行は縦コネクタ
経由でのみ母線接続」という前提は、列0の場合には**最初から適用されていない（例外扱い）**。これは同時に、
「冗長な縦コネクタ（母線とほぼ重なる縦線）」と「直結の横線」の**両方が描かれる二重接続状態**を作って
おり、これがT-044の問題（殿の目に見えている「冗長な縦分岐」）の正体と考えられる（推測：T-044の題名・
殿要望の文言とこの二重描画挙動が一致するため）。

**したがって**：`leftColumn==0`の場合に左側VerticalConnectorを生成しないよう変更しても、`LeftTerminator`
のBottomRow判定には影響を与えない（そのコネクタは元々`c.Column>0`で除外されており、生成有無に関わらず
戻り値は`null`のまま）。新規行が「浮く」ことはなく、冗長な縦線が消えるだけ。

（`leftColumn>0`の一般ケースへスコープを広げる場合は、`LeftTerminator`のコネクタ検索フォールバック
機構自体が「コネクタが無ければ直結線を描く」設計のため自然に追従するが、基準行に複数要素がある複雑
ケースは`docs/proposed.md`のP-003エントリで「今回のOR実装スコープ外」と明記されており、改めて検証が
必要——推測混じりの分析、下記「留意点」参照。）

## 観点(c): `NetlistBuilder`の電気的接続性

**確認**：`NetlistBuilder.cs:241-247`の`LeftRailReached`も`c.Column > 0`を持ち、列0のコネクタは母線
接続を妨げる要因として扱われない。よって列0の新規行・基準行は、コネクタの有無に関わらず
`AddHorizontalWireUnions`経由で`leftRail`へ直接unionされる。**左縦分岐を省略しても回路が電気的に
分断されることはない**。

**留意点（発見事項）**：`LeftRailReached`は`(c.TopRow == row || c.BottomRow == row)`という、P-003で
レンダラ側が修正した条件と同じ形をしている。しかし`git show 282f3eb`確認の結果、**P-003は
`DiagramRenderer.cs`のみを修正し`NetlistBuilder.cs`は無修正のまま**。これはバグではなく意図的と考え
られる（推測、根拠あり）：Union-Findは対称的な合併操作のため、「新規行が`AddHorizontalWireUnions`で
直接`leftRail`にunion」される経路と「縦コネクタが`topNode`/`botNode`をunion」する経路のどちらか一方が
成立すれば、推移律により最終的に同一ネットへ帰着する。つまりレンダリング（見た目の線）と電気的接続性
（Netlist）は独立した問題であり、Netlist側は元々壊れていなかった。

右側（合流側）コネクタはT-044仕様(2)により維持されるため、並列接続としての電気的トポロジーは保たれる。

## 観点(d): 「自ノードと左母線の間に何も無い」の実装方針候補

**候補1（最小変更・低リスク、推奨）**：`PlaceElementAtSelectedCell`内で計算済みの`leftColumn`を使い、
`leftColumn == 0`のときだけ左側`VerticalConnector`の`Add`をスキップする。

```csharp
if (leftColumn > 0)
    sheet.Connectors.Add(new VerticalConnector { Column = leftColumn, TopRow = br, BottomRow = pos.Row });
sheet.Connectors.Add(new VerticalConnector { Column = rightColumn, TopRow = br, BottomRow = pos.Row });
```

根拠：`leftColumn=0`は「基準要素または新規要素のいずれかが列0（母線境界）にある」ことと数学的に同値
（`Math.Min`の性質）。`LeftTerminator`／`LeftRailReached`が既に用いる`c.Column>0`という除外条件と完全に
整合するため、両者の前提を壊さない最も安全な実装。殿仕様「配置行で自ノードと左母線の間に何も無い場合」
とも、基本パーツ（1セル幅）の範囲では一致する。

**候補2（より一般的だがスキャンコスト増・スコープ拡大）**：`sheet.Elements`を対象行についてスキャンし、
自要素より左に既存要素が無いかを判定するヘルパーを追加する（`NothingLeftOf(row, column)`）。基準行に
複数要素があるケース（列0の要素が無くても、基準要素の手前に別要素が無い＝実質「間に何もない」ケース）
まで扱いたい場合はこちらが必要になるが、P-003エントリが明示的に「スコープ外」とした複雑ケースに踏み込む
ことになり、検証範囲が広がる。

**推奨**：殿仕様の文言（「配置行で自ノードと左母線の間に何も無い場合」）と現状のOR自動生成が扱う範囲
（基本パーツ・1セル幅、基準行は「近い列の要素」を選ぶのみで先頭要素判定はしていない）を踏まえると、
**候補1で殿仕様の意図する範囲は満たせる**と考える。候補2は将来の拡張候補として記録するに留める。

---

## 留意点（今後の実装・レビューで注意すべき事実）

- `NetlistBuilder.LeftRailReached`は`TopRow`/`BottomRow`を区別しない条件のままであり、P-003のレンダラ
  修正と非対称。今回のColumn=0限定の変更ではこの非対称性は問題にならない（Union-Findの推移律で吸収
  される）が、将来この非対称性自体を「直す」変更が入る場合は改めて電気的接続性への影響検証が必要。
- 候補2（基準行に複数要素がある一般ケース）を採用する場合はP-003のスコープ外領域に踏み込むため、
  リスク評価を改めて行うこと。

---

## 出典・参照

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（333-373行目、`PlaceElementAtSelectedCell`）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs`（332-336/373-380行目、`LeftTerminator`）
- `src/Ecad2.Core/Simulation/NetlistBuilder.cs`（205-218/241-247/260-272行目、`AddHorizontalWireUnions`/
  `LeftRailReached`/`BuildVerticalConnectorUnions`）
- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs`（30-31行目、基本パーツのポート定義）
- `src/Ecad2.Core/Model/Element.cs`（34行目、`GridPos`定義）
- `docs/proposed.md`（P-003エントリ、既存の母線接続バグ修正の経緯・スコープ限定の明記）
- `git show 282f3eb`（P-003修正コミット、`DiagramRenderer.cs`のみ変更を確認）
- Exploreエージェントによるコード調査（事実は「確認」、推測は「推測」と明記して報告）
