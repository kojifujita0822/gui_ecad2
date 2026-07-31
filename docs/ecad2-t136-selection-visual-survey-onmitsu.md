# T-136(B)前提調査：パーツエディタ接続点の選択表現を色以外へ移す案

隠密（key=1785485896132）記す。2026-07-31。家老采配より。

## 0. 調査題目・スコープ

**題目**：`PartEditorCanvas.cs:699-700`周辺の接続点(ポート)描画で、赤丸/青丸を「選択状態」ではなく
「接続点の種類」専用の色として使う仕様変更（殿裁定・P-1案採用）に向け、選択状態を色以外で表現する
代替案を調査する。

**スコープ境界**：調査のみ。実装はせぬ（侍へ委譲）。見せ方の最終確定は殿の裁可を要す。

**DoD**：
1. 描画経路の一次ソース確認
2. 色以外で選択を表す案を2〜3、実装コスト・視認性の長短つき
3. 同じ色の使い分けが他に無いか（横展開の要否）

---

## 1. 描画経路の一次ソース確認

`src\Ecad2.App\Views\PartEditorCanvas.cs`の`Draw()`メソッド内、694-701行目：

```csharp
// 接続点は図形の上に重ねて描く（ヒットテストで優先するのと同じ順序）
for (int i = 0; i < _ports.Count; i++)
{
    var c = CellToLocalMm(_ports[i].BoundaryOffset, _ports[i].RowOffset);
    bool selected = i == _selectedPortIndex;
    renderer.FillCircle(c, _geo.CellMm * 0.14,
        selected ? new Ecad2.Rendering.Color(255, 255, 69, 0) : new Ecad2.Rendering.Color(255, 30, 144, 255));
}
```

**事実**：
- 接続点は`FillCircle`（塗りつぶし円のみ、輪郭線なし）1回の呼び出しで描かれる。**選択の有無と色が単一の`Color`引数に統合されており、選択状態と種類の両方を1つの色軸だけで表そうとすると衝突する**（今回の問題の核）。
- 半径は`_geo.CellMm * 0.14`（既定`CellMm=9.0mm`ゆえ約1.26mm）で選択/非選択とも同一。
- 色定数は`new Ecad2.Rendering.Color(255, 255, 69, 0)`＝OrangeRed、`new Ecad2.Rendering.Color(255, 30, 144, 255)`＝DodgerBlueのARGB値そのまま埋め込み（`DrawingTheme`等の定数経由ではない）。
- 同ファイル668-671行目の`selectedStroke`（OrangeRed, 幅0.5）は図形(primitive)選択時の輪郭強調（681行目）およびRotateツールの中心十字マーカー（703-709行目）に使われる。**こちらは接続点の色とは独立した別変数**であり、「選択」を表す用途でのみ使われている（種類等の兼用はない）。

**出典**：`src\Ecad2.App\Views\PartEditorCanvas.cs:668-716`（直読、`Grep`不使用）

---

## 2. 描画APIの確認（代替案の実装可否）

`src\Ecad2.Rendering\IRenderer.cs`および`src\Ecad2.Rendering.Wpf\WpfRenderer.cs`を確認。

`IRenderer`には接続点描画に転用できる以下のプリミティブが**既存**する（新規API追加は不要）：

| メソッド | 内容 | WPF実装 |
|---|---|---|
| `FillCircle(center, radius, color)` | 塗りつぶし円（現状使用中） | `WpfRenderer.cs:63-64` |
| `DrawCircle(center, radius, stroke)` | **輪郭線のみの円**（未使用箇所あり） | `WpfRenderer.cs:60-61` |
| `DrawLine(a, b, stroke)` | 直線（Rotateツールの十字マーカーで使用実績あり） | `WpfRenderer.cs:43-44` |

`StrokeStyle`は`Color`・`Width`(mm)・`LineStyle`(Solid/Dashed/Dotted)を持つ（`IRenderer.cs:15-16`）ため、
リング・破線・太さいずれも既存の型で表現可能。**新規の描画バックエンド機能追加は不要**——三案とも
既存APIの組み合わせで実装できる。

`DrawingTheme.cs`（26-38行目）には、テーマ非依存の「意味色」static定数（`Blue`=接続済み検査・
`Powered`=通電・`ManualForced`=手動強制・`Comment`=機器コメント）が既に整理されている。**赤丸/青丸の
種類色とも、選択リングの色ともぶつからない第三の意味色を新設する余地**がこの枠組みに既にある。

---

## 3. 代替案（色以外で選択を表す）

いずれも「`FillCircle`の色は種類のみを表す」ことを前提とし、選択の有無は**別の描画要素**として重ねる。

### 案A：外周リング（推奨）

選択中のポートにだけ、`FillCircle`の外側へ`DrawCircle`（輪郭線のみ）を重ねて描く。

```csharp
renderer.FillCircle(c, _geo.CellMm * 0.14, typeColor);   // 種類色（赤/青）は選択と無関係に決まる
if (selected)
    renderer.DrawCircle(c, _geo.CellMm * 0.20, new StrokeStyle(ringColor, 0.15));
```

- **実装コスト**：低。既存の`DrawCircle`をそのまま呼ぶだけで、追加コードは2〜3行。
- **視認性**：中〜高。リング半径を塗り円(0.14セル)より一回り大きく(0.20セル程度)取れば、
  種類色を隠さずに選択を明示できる。ズームアウト時はストローク幅0.15mm程度を確保すれば潰れにくい。
- **色の選定が未決**：リング色は赤/青いずれとも衝突せぬ第三色が要る。候補は
  (a) `_theme.Foreground`（既存の`DrawingTheme`、ライト=黒・ダーク=明灰、テーマと自然に馴染む）、
  (b) `DrawingTheme`に新規の意味色（`Powered`等と同枠）を追加。**この色の選定自体はUI/UXの見た目に
  関わる分岐ゆえ、`docs-notes/roles/onmitsu.md`の役儀どおり実装案としてのみ提示し、確定は殿の裁可を要す**。

### 案B：十字マーカー重畳（Rotateツール中心線の意匠を流用）

`PartEditorCanvas.cs:703-709`のRotateツール中心十字マーカーと同じ手法で、選択ポートの上に短い十字線2本を重ねる。

- **実装コスト**：低。既存パターンの転用ゆえ実装は容易（既に同一ファイル内に実装例がある）。
- **視認性**：中。単体では明瞭だが、**接続点が密集する状況**（T-136(B)自体が「高さ2以上・接続点複数」を
  対象にしている）では、隣接ポートの十字と交差・重畳して見づらくなる懸念がある。密集時のオフセット
  調整が別途要る可能性。

### 案C：破線リング（案Aの変種、強調度を上げる）

案Aのリングを実線でなく破線(`LineStyle.Dashed`)にし、選択との違いをさらに際立たせる。

- **実装コスト**：案Aと同等（`StrokeStyle`の`Style`引数を変えるのみ）。
- **視認性**：**セルサイズ(9mm)に対しリング半径が小さい(2mm弱)ため、破線ピッチによっては
  1周に数個しか破線が入らず、実線と見分けが付きにくくなる恐れがある**。案Aより優れるとは言い切れず、
  隠密としては推さぬ（実測（忍者の実機確認）で判断すべき点）。

**隠密の推し**：案A（外周リング、実線）。既存API・既存の`DrawingTheme`意味色枠組みをそのまま使え、
実装コストが最も低く、密集時の破綻リスクも十字案(B)より小さいと見る。ただしリング色の最終選定は
殿裁可を要する。

---

## 4. 横展開の要否（同じ色の使い分けが他に無いか）

`Explore`エージェントへ`src\Ecad2.App\Views\LadderCanvas.cs`のOrangeRed/DodgerBlue使用箇所の
全数調査を委譲した。結果：

- **OrangeRed**：全13箇所（`SelectedCellPen`・`SelectedConnectorPen`・`SelectedWireBreakBrush`・
  `SelectedFreeLinePen`・`SelectedConnectionDotBrush`・`SelectedImagePen`・`SelectedFrameSolidPen`等、
  L83-L145）は**例外なく「選択状態」専用**（命名・コメントとも「選択中の〜」で統一）。
- **DodgerBlue**：全箇所（L92-L167、`ConnectorDraftPen`・`FreeLineDraftPen`・`ImageDraftPen`等）は
  **例外なく「記入中(未確定)・ドラフト・プレビュー」専用**で、選択状態とは無関係。
- 非選択時の既定表示（`DiagramRenderer.DrawDots`のConnectionDot等）はテーマ由来の中立色
  (`_theme.Get(StrokeRole.Wire).Color`)を使い、赤/青とは無関係。

**結論**：`LadderCanvas.cs`では「選択=Orange」「ドラフト=Blue」の2軸が最初から独立しており、
`PartEditorCanvas.cs`の接続点のように**単一の色軸が選択/非選択の二値を兼ねる**構造にはなっていない。
**同種の衝突は他に見当たらず、横展開は不要**と判断する。

**推測（補足、Exploreエージェントの所見）**：将来「ドラフト中の要素を選択できる」仕様に変われば、
OrangeRedとDodgerBlueが同一要素に同時適用される新たな衝突源になりうるが、現状は該当しない。

**出典**：Exploreエージェントによる`LadderCanvas.cs`全数調査（家老へは調査書内の引用として記録、
一次ソース行番号は上記のとおり本人による直読に基づく）

---

## 5. 不明点・派生提案

- **リング色（案A）の最終決定は殿裁可が要る**——`Foreground`流用か新規意味色かの分岐は
  `memory: feedback_route_design_decisions_to_user`（UI/UX分岐は必ず殿へ確認）に該当する
  見た目の分岐であり、隠密の一存では決めない。
- 派生提案なし（本調査の範囲内で完結）。

---

## 6. 報告

家老へ`send_message`で本書のパスを共有し、要旨（結論・推し案・不明点）を伝える。
