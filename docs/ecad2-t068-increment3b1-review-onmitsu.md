# T-068 増分3-b1 静的レビュー（隠密、effort中程度以上・実機確認なしの一発勝負）

対象コミット：`f537ead`（`feat(core): T-068増分3-b1 - 形状編集キャンバスの土台をCore層に整える`）
対象ファイル：`src/Ecad2.Core/Model/PartShapeGeometry.cs`（新設236行）・
`src/Ecad2.Core/Rendering/PartDrawing.cs`（101行差分）・
`tests/Ecad2.Core.Tests/PartShapeGeometryTests.cs`（新設387行、テスト65件）
参照：`docs/todo.md` T-068節（1977-2051行、家老仮決定・侍申告）

## 結論：要修正なし（実装）、テスト強化を1件推奨

実装ロジック自体にバグは発見できなかった。`DiagramRenderer.cs:1130`は無改変、`Draw`→`DrawPrimitive`
切り出しは数学的に完全な等価変換と確認した。全幾何関数をPoC実装と1つずつ突き合わせ、ロジックの相違は
無いことを確認した（`TranslatePoints`のガード強化を除く）。

**最重点観点（侍が「自明ゆえ実測を省いた」と申告したテスト群の検算）で、重要な検出力の穴を1件発見**
した——`RotatePoint`関数のY成分（`dy`）の符号を直接検証できるテストが、`RotatePoint_*`という名前の
テスト2件には存在しない。侍が発見した`DistanceToPrimitive_RotatedRect`の符号反転（矩形の中心対称性が
原因）と同型の罠であり、家老の懸念（「似た対称性・退化の罠が未実測群に潜んでおらぬか」）が的中した。

## 1. 【最重点】検出力の検算

### 1.1 発見：`RotatePoint`のY成分符号を直接検証するテストが無い

`PartShapeGeometry.RotatePoint`の実装：
```csharp
public static (double X, double Y) RotatePoint(double x, double y, double centerX, double centerY, double deg)
{
    double rad = deg * Math.PI / 180.0;
    double dx = x - centerX, dy = y - centerY;
    double cos = Math.Cos(rad), sin = Math.Sin(rad);
    return (centerX + dx * cos - dy * sin, centerY + dx * sin + dy * cos);
}
```

`RotatePoint_*`という名前を持つ2テスト（316-336行）を検算：
- `RotatePoint_AboutOrigin_TurnsClockwiseInScreenCoords`：`RotatePoint(1, 0, 0, 0, deg)`——常に`dy=0`
- `RotatePoint_AboutNonOriginCenter_KeepsCenterFixed`：`RotatePoint(5, 5, 5, 5, 123.4)`——`dx=dy=0`
  （点と中心が完全一致、回転式そのものが意味を持たない「中心固定」の確認のみ）

**`dy=0`の入力では、`X`式の`- dy * sin`という項が常に0のまま消える**。つまり、実装がもし
`X = centerX + dx*cos + dy*sin`（符号が逆）というバグを持っていても、この2テストはいずれも
**GREENのまま検出できない**。

`Rotate_Line`・`Rotate_Polyline`等の間接テストも検算したが、いずれも回転対象の頂点が
`dy=0`（Y座標が回転中心と同じ）のケースのみで、同様に符号を検出できない。

### 1.2 唯一の例外＝`DistanceToPrimitive_RotatedRect_UndoesRotationInCorrectDirection`（間接テスト）

`Rect(-2,-1,4,2,Rot=45)`・点`(3,1)`という非対称な配置により、`RotatePoint`内部の符号を実際に検出
できることを検算で確認した（局所座標を手計算すると符号が正しければ`(2.828,-1.414)`＝距離`0.926`、
符号が逆なら別の値になり、テストの期待値`0.926`と一致しない）。**このテスト1件のみが、たまたま
`RotatePoint`のY成分符号バグを検出できる状態になっている**。

一方`DistanceToPrimitive_RotatedRect_MeasuresInRotatedFrame`（Rot=90、点`(0,2)`・`(0,3)`）は、
テスト自身のコメント（156-157行）が明記するとおり矩形の中心対称性により符号を検出できない
（実際に手計算で検証：符号が逆でも局所座標の絶対値が一致し同じ距離になる）。

### 1.3 リスク評価と推奨

実装自体は標準的な回転行列の式であり正しいと確認できた（バグは無い）。しかし、`RotatePoint`という
**関数名を冠したテストが、その関数の核心的な計算（Y成分の符号）を直接検証できていない**のは、
テストスイートの構造として脆弱——もし将来`DistanceToPrimitive_RotatedRect_UndoesRotationInCorrectDirection`
が変更・削除されたら、`RotatePoint`の符号バグを検出する手段がテストスイート全体から失われる。

**推奨**：`RotatePoint_*`に、`dx=0・dy≠0`という直接的なケースを追加する。例：
`RotatePoint(0, 1, 0, 0, 90)` → 期待値`(-1, 0)`（`X = 0*cos90 - 1*sin90 = -1`、`Y = 0*sin90 + 1*cos90 = 0`）。
これで`- dy * sin`の符号を`RotatePoint`単体で直接検出できるようになる。

## 2. その他の未実測群の検算（家老指示分）

### 2.1 `BuildCircle`・`BuildArc`

`BuildCircle_RadiusIsDistanceFromCenterToEdgePoint`は中心座標に`(0,0)`というゼロ値を使っている。
`Distance`関数自体は`(x1,y1)`と`(x2,y2)`に対して数学的に完全対称（減算を二乗するため符号非依存）
なので、引数順序の取り違えは半径の値では原理的に検出不可能——ただし中心座標`Cx/Cy`のアサーション
（期待値`0.0`）で引数取り違えは別途検出できるため、実害は低いと判断した。**軽微な指摘に留める**
（テスト入力を非ゼロ値にする方がベタープラクティスではあるが、修正必須ではない）。

`BuildArc_FromBoundingBox_IsHalfEllipse`はX幅4・Y幅2という非対称な入力で、`R`と`Ry`の取り違えを
確実に検出できる設計になっている。**「自明」の判断は妥当**。

### 2.2 `Translate_*`（折れ線以外）

`Translate_Line_MovesBothEndpoints`・`Translate_Rect_MovesOriginKeepsSize`とも、X/Y方向で異なる値
（非対称な移動量・非対称な元座標）を使っており、成分の取り違えを検出できる設計。**「自明」の判断は
妥当**。

**付記（家老指示の範囲外・念のため確認）**：家老指示の対象外である`Translate_Polyline_MovesEveryVertex`
（238-243行、「折れ線」ゆえ実測済み側とされる）を検算したところ、入力`Polyline(0,0,2,2)`・移動量
`(dx=1,dy=1)`が対角線上・対称な値のため、`TranslatePoints`内で`result[i]=pts[i]+dx`と
`result[i+1]=pts[i+1]+dy`が入れ替わっても結果が変わらない（`dx=dy=1`ゆえ）。実測済みとされる側にも
同種の対称性の罠が残っている可能性がある。深追いはスコープ外のため申し送るのみ。

### 2.3 `CenterOf`

全5テストとも非対称なX/Y値を使っており、成分取り違えを確実に検出できる設計。**「自明」の判断は
妥当**、指摘なし。

### 2.4 距離計算（円・折れ線・弧・文字）

- **円・文字**：`Distance`関数がX/Yに対して完全対称な実装のため、取り違えバグが原理的に発生し得ない。
  「自明」の判断は妥当。
- **折れ線**：2セグメントを走査するテストがあり「最も近いセグメントを選ぶ」ロジックは検証できている。
  3セグメント以上での境界条件（`i+3<pl.Points.Length`）は明示的に未検証だが、同一パターンの
  ループ継続に過ぎずリグレッションリスクは低いと判断（軽微、指摘のみ）。
- **弧**：近似計算ゆえ`Assert.True`による範囲チェックに留まる設計は妥当（厳密値の計算が困難な近似
  ロジックの性質上やむを得ない）。

## 3. `DiagramRenderer.cs:1130`の無改変確認・`Draw`→`DrawPrimitive`の等価性

`DiagramRenderer.cs:1130`は`PartDrawing.Draw(r, _theme, part, Cell, stroke)`のまま無改変と確認した
（呼び出しコード自体を読解）。

`PartDrawing.Draw`は`foreach (var p in part.Primitives) DrawPrimitive(r, theme, p, cell, s);`という
単純委譲になり、`DrawPrimitive`の中身（switch文9ケース：Line/Circle/Arc×3分岐/Rect×2分岐/Polyline/
Text）を旧`Draw`のswitch文と1ケースずつ突き合わせたところ、**インデントが1段減った以外は一字一句
同一**（数式・分岐条件・呼び出し順序いずれも相違なし）。`internal`→`public`はアクセス修飾子の緩和のみ
で、既存呼び出し元への影響はあり得ない。**描画結果は数学的に不変と判定**（ビルド成功は「呼べる」証拠
にすぎないという家老の指摘は正しいが、本件はコードの完全な等価変換であり実行結果の同一性を静的に
保証できるケース）。

## 4. RED証明の代替が当該経路を突いているか

侍の4ラウンド実測結果（`docs/todo.md`2024-2037行）を確認した。各ラウンドでガードを壊した箇所
（Snapの丸め・`DegenerateEps`・`BuildRect`の正規化・`HitTest`の走査順・`DistanceToRect`の内側分岐・
線分クランプ・`Rotate`の`Rot`加算・`AverageAt`の成分分離・`RotateLine`の終点回転・`RotatePolyline`の
頂点回転）と、対応するテストの実装コード上のコメント（`// ===== Snap（丸めを素通しにするとRED）=====`
等）を突き合わせ、記述された経路とテスト内容が論理的に整合していることを確認した。

`DistanceToPrimitive_RotatedRect`の検出力不足発見・補強の経緯（§1.2参照）は、侍の実測プロセスが
実際に機能していたことの証左であり、RED証明代替の運用自体は妥当と判断する。

## 5. 設計判断2件の確認

**(a) `BuildPrimitive`をCoreへ持ち込まず`BuildRect`/`BuildCircle`/`BuildArc`の3分割**：
`PartShapeGeometry.cs`を確認し、`EditTool`列挙型（App層のUI概念）への依存は一切無いことを確認した。
Core層にUI状態を持ち込まない設計原則に忠実で、3-b2でApp層がツール種別ごとに個別呼び出しを行う
想定と整合する。**妥当な判断**。

**(b) `TranslatePoints`のガード強化**（`i + 1 < pts.Length`、PoCの`i < pts.Length`から変更）：
偶数長配列（`PartPolyline.Points`は常にx,yペアで偶数長という前提、生成箇所を確認済み）では
両者は完全に同一の結果を返す。奇数長という異常データの場合のみ挙動が変わり、旧版は
`IndexOutOfRangeException`、新版は最後の不完全なペアを静かにスキップする。**実害なし、安全側の
変更として妥当**。

## 6. 既知トラップ・規約整合の確認

- **SetProperty早期return罠（PR-03）**：該当なし。Core層の静的純粋関数・パターンマッチのみで
  SetPropertyパターンは一切使用されていない
- **PR-13型（CanEditDiagramガード漏れ）**：該当なし。Core層にUIゲート概念自体が存在しない
- **既存規約（`PartOptimizer.cs`）との整合**：ファイルスコープ`namespace`・`public static class`・
  XMLドキュメントコメント・private静的ヘルパーの書き方いずれも`PartOptimizer.cs`と一致。
  `PartShapeGeometry`は加えて`public const`定数（GuiEcad原本の既定値、スナップ刻み・ヒット許容・
  回転スナップ）を外部公開しており、3-b2での再利用を見据えた丁寧な設計と評価できる

## 7. スコープ境界の確認

- View層（`src/Ecad2.App/`）への変更は皆無（diff対象は`src/Ecad2.Core/`と`tests/Ecad2.Core.Tests/`
  のみ）
- 「幾何演算のみ」という家老の追加裁可条件（DoD列挙外の`Translate`/`CenterOf`/距離計算下請け）も、
  全てUI状態（ツール種別・選択状態・Undo/Redo）を含まない純粋関数であることを確認、条件を遵守

## 不明点

なし。

## 派生提案

- §1.3の`RotatePoint_*`テスト強化（`dx=0・dy≠0`ケースの追加）を侍への修正依頼として推奨
- §2.2の`Translate_Polyline`対称性の懸念は、深追いせず申し送りのみ（家老・侍判断に委ねる）

---

## 往復2周目 再レビュー（コミット`6add148`、テスト補強5件・実装無改変）

対象：`tests/Ecad2.Core.Tests/PartShapeGeometryTests.cs`のみ（+108/-50）。`git diff f537ead..6add148 --
src/`が空であることを確認し、**実装側（`PartShapeGeometry.cs`・`PartDrawing.cs`）の一時改変残存
ゼロ**を裏取りした。

### 補強5件の検出力を手計算で検算

1. **【隠密指摘】`RotatePoint`のY成分符号**：新設`RotatePoint_FromYAxis_AppliesCorrectSignToDy`
   （`RotatePoint(0,1,0,0,deg)`、90/180/270度）を検算——90度は`X=0*cos90-1*sin90=-1, Y=0*sin90+1*cos90=0`
   →`(-1,0)`、期待値と一致。符号を反転させれば`X=1`になり期待値`-1`と矛盾するため確実に検出できる。
   `RotatePoint_ObliquePoint_RotatesBothComponents`（`dx=3,dy=1`）・`RotatePoint_AboutNonOriginCenter_
   RotatesAroundThatCenter`（中心`(2,3)`・点`(4,4)`）も手計算で期待値と一致確認、いずれも成分取り違え・
   符号誤り・非原点中心のいずれも検出できる設計。**§1.3で指摘した穴は正しく塞がれた**
2. **【隠密付記→家老が拾った件】`Translate_Polyline`**：`Polyline(0,1,2,5)`・`dx=10,dy=3`を検算——
   頂点`(0,1)→(10,4)`・`(2,5)→(12,8)`で期待値`[10,4,12,8]`と一致。`dx≠dy`かつ頂点も非対角のため
   X/Y取り違えを確実に検出できる
3. **【侍自主発見】`Rotate_Line`/`Rotate_Polyline`の軸上の点**：`Line(1,2,3,1)`を90度回転を検算——
   `(1,2)→(-2,1)`・`(3,1)→(-1,3)`、いずれも期待値と一致。両端点とも軸外（`dx,dy`いずれも非ゼロ）で
   §1.1で指摘した同型の穴を正しく塞ぐ
4. **【侍自主発見】`IsDegenerate`の`&&`/`||`取り違え**：`IsDegenerate_Line_RequiresBothAxesToCollapse`
   のTheory4ケースを確認——縦線`(X差0,Y差1)`・横線`(X差1,Y差0)`のいずれも、`&&`から`||`への取り違えが
   あれば`true`になり期待値`false`と矛盾するため検出できる設計。適切
5. **【侍自主発見】原点起点・正方形入力の解消**：`BuildCircle`・`BuildArc`・距離計算系を非原点・
   非正方形の値へ置き換え。手計算で新期待値と実装の一致を確認（例：`BuildArc(1,3,5,5)`→
   `Cx=3,Cy=4,R=2,Ry=1`）。円・テキストの距離計算はもともとX/Y対称な実装（§2.1参照）ゆえ検出力の
   本質的向上ではないが、テスト衛生としては妥当な改善

5件とも意図通りの検出力を持つと確認した。

### 6件目の懸念（1件、軽微）

テストファイル全体を見直したところ、`Rotate_Arc_AccumulatesRotFieldOnly`（300-317行台）が
`Arc(1, 1, 2, 180, 180, Ry: 1, Rot: 10)`という**`Cx=Cy=1`の対称値**を使っており、かつ**`Cy`の
アサーション自体が無い**（`Rotate_Rect_AccumulatesRotFieldOnly`はX・Y両方アサートしているのと
非対称）。実装は`a with { Rot = a.Rot + deg }`という1行でCx/Cyには一切触れないため、Cx/Cyが
意図せず変化する余地は構造的になく**実害はほぼ皆無**と判断する。ただしテストの完全性としては、
`Rotate_Rect`同様に非対称な`Cx≠Cy`の入力へ変更し`Cy`もアサートする方が望ましい。**軽微な指摘、
修正必須ではない**。

### テスト名・アサーションの整合、重複確認

`IsDegenerate_ZeroLengthLine_ReturnsTrue`/`IsDegenerate_NormalLine_ReturnsFalse`の2`[Fact]`が
`IsDegenerate_Line_RequiresBothAxesToCollapse`という`[Theory]`4ケースへ統合されており、元の2ケースの
内容（同一点→true、通常の線→false相当）は新Theory内に含まれている。重複・矛盾なし、適切な統合。
`RotatePoint_AboutOrigin_...`→`RotatePoint_FromXAxis_...`のリネームも内容と整合。

### 結論（往復2周目）

要修正なし。§6の軽微な指摘（`Rotate_Arc`のCy）は経過観察でよく、3-b1決着として問題ないと判断する。
