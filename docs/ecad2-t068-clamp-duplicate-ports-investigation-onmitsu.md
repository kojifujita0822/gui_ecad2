# T-068 保存時クランプによる接続点重複生成 調査（隠密）

> 2026-07-25 隠密調査。家老委譲。忍者が実機確認で発見した「`ClampPortsToFrame`が重複チェックを
> せず、`RowOffset`違い・`BoundaryOffset`同一の接続点2個を持つ部品の高さを縮めて保存すると、
> 両方が完全同一座標へ収束する」事象について、(1)GuiEcad原本との性質の見極め (2)実害の程度
> (3)対処案の列挙、の3点を調査した。検体`T068増分3c枠外検証部品.gcadpart`はRead専用で確認、
> 書き換えは一切行っていない。**調査のみ、対処の採否は殿判断——`docs/proposed.md`起票は家老に
> 委ねる。**

---

## 検体の確認

```json
{
  "widthCells": 3, "heightCells": 1, "role": "contactNO",
  "ports": [
    { "name": "P1", "rowOffset": 0, "boundaryOffset": 1 },
    { "name": "P2", "rowOffset": 0, "boundaryOffset": 1 }
  ]
}
```
P1・P2が完全に同一座標（row0, boundary1）に収束していることを確認した。

---

## 1. GuiEcad原本との性質の見極め — 【原本に無い新しい穴】と判定する

`docs/ecad2-t068-increment3-design-onmitsu.md`289-294行目に記載済みの**分岐F**（ポート移動時の
重複チェック非対称性）を確認した：「GuiEcad原本の既存の弱点をそのまま踏襲する設計」——これは
`PartEditorCanvas.UpdatePortDrag`のコメント「原本どおり、移動先が既存の接続点と重なっても弾かない
（追加時のみ重複を見る非対称を踏襲）」と同一の話で、**ドラッグ移動によりユーザーが意図的に
2つのポートを重ねる操作をした場合**に生じる、原本由来の既知の弱点である。

今回発見された事象はこれとは性質が異なる：
- **原本はそもそも`OnSizeChanged`/`OnSave`いずれもポートに触れず「クランプ処理」自体を
  持たない**（今回のsetter方式撤回の根拠そのもの、`docs/ecad2-t068-port-reclamp-investigation-onmitsu.md`
  参照）。原本には「クランプによって複数ポートが同一座標へ収束する」という経路自体が存在
  しえない。
- 分岐F（ドラッグ移動）は**ユーザーが明示的に重ねる操作をした場合にのみ**発生する、いわば
  「わざとやれば起きる」弱点。
- 対して今回の事象は、**ユーザーが「基準枠を縮めて保存する」という一見無害な操作をしただけで、
  意図せず2つの異なる座標のポートが同一座標へ収束する**——ユーザーの意図に反した副作用。

**結論：これは「原本から在る穴に新しい入口が増えた」のではなく、`ClampPortsToFrame`（ecad2独自の
新設機能）自体が生んだ、原本に存在しない新しい重複生成経路である。** 分岐Fの延長・同型の弱点として
扱うのは不正確と考える。

---

## 2. 実害の程度 — `NetlistBuilder`一次ソース精読の結果

`NetlistBuilder.BuildElementConnections`（139-158行目、増分2レビューで裏取り済みの箇所）を
再度精読した。

### 縮退のメカニズム

```csharp
var pl = ports[0]; var pr = ports[0];
foreach (var p in ports)
{
    if (p.BoundaryOffset < pl.BoundaryOffset) pl = p;
    if (p.BoundaryOffset > pr.BoundaryOffset) pr = p;
}
leftBoundary[i] = e.Pos.Column + pl.BoundaryOffset;
rightBoundary[i] = e.Pos.Column + pr.BoundaryOffset;
leftNode[i] = Node(e.Pos.Row + pl.RowOffset, leftBoundary[i]);
rightNode[i] = Node(e.Pos.Row + pr.RowOffset, rightBoundary[i]);
```
`<`/`>`という厳密不等号比較のため、**P1・P2が同一`BoundaryOffset`の場合、`pl`も`pr`も
`ports[0]`（=P1）のまま更新されない**。結果、`pl == pr`となり`leftBoundary[i] == rightBoundary[i]`・
`leftNode[i] == rightNode[i]`という、**要素本来の「2つの異なる接続点」が「1つの接続点」へ
縮退する**状態になる。

### 波及先1：Component（`NetlistBuilder.cs:325-334`）

`Component.NetA = Net(leftNode[i])`・`NetB = Net(rightNode[i])`は同一値になる。`Evaluator.cs:139-140`
の`AddEdge(c.NetA, c.NetB)`は自己ループとなり、これ自体は連結性判定上無害（既に同一ノード）。

### 波及先2：横配線接続（`AddHorizontalWireUnions`、205-218行目）— より重大

```csharp
if (LeftRailReached(sheet, row, leftBoundary[idxs[0]]) && !severed(...))
    unions.Add((leftNode[idxs[0]], leftRail));
for (int k = 1; k < idxs.Count; k++)
    if (!severed(...)) unions.Add((rightNode[idxs[k - 1]], leftNode[idxs[k]]));
```
母線・隣接要素との接続処理は、いずれも`leftBoundary[i]`/`rightBoundary[i]`という**縮退済みの
座標**を基準に行われる。本来「要素の左端（Column+0相当）」に対して行われるべき母線・前段要素との
接続が、**実際には縮退後の座標（本来の右端側）に対して行われる**——要素の左右の区別自体が
失われ、母線・前段要素・後段要素のいずれの接続も、この単一の座標点へまとめて`union`される
可能性が高い。

**評価**：これは「断線（片側が浮く）」というより、**「要素を挟んで本来分離されているべき左右の
ネットが、意図せず同一ネットへ統合される」という誤結線（短絡に近い挙動）**と判断する。ただし、
実際にどちらの挙動（断線寄りか短絡寄りか）になるかは、配置される図面上の具体的な文脈
（前後の要素・母線との位置関係、`Severed`＝配線分断の有無）に依存し、**これは静的読解による
推論であり、実際のシミュレーション動作の実測による裏取りはできていない**（実測は忍者の領分と
心得る）。「誤結線防止のためのクランプが、別の誤結線を生みうる」という忍者の所見と、静的読解の
結論は方向性として一致する。

---

## 3. 対処案（列挙のみ、採否は殿判断）

1. **`ClampPortsToFrame`実行後に重複を検出し、保存時バリデーションで拒否する**（既存の
   `PartEditorDialog.OkButton_Click`の「ポート2点未満は拒否」と同じ位置に追加できる。実装は
   小さいが、「クランプの結果ユーザーが意図しない保存拒否に遭う」というUXの荒さが残る）
2. **クランプ時に重複が生じる場合、片方を隣接位置へ自動的にずらす**（「隣接」の定義・ずらす方向
   ・さらに別のポートと衝突した場合の連鎖処理など、設計判断を要する分岐が多い）
3. **現状維持（分岐Fと同様、原本由来の弱点として据え置く）**——ただし本調査の結論（1節）により
   「原本に無い新しい穴」と判定したため、この案を分岐Fと同列に正当化するのは筋が弱いと考える
4. **参考**：忍者の検体`T068増分3c枠外検証部品.gcadpart`のような既存の汚れたファイルが既に
   存在する場合の救済（読込時クランプ等）は、先の調査（`docs/ecad2-t068-port-reclamp-investigation-
   onmitsu.md`）で「殿裁定により編集中は一切クランプしない」と決した経緯があり、対処案1・2を
   採る場合も「保存時のみ」の枠内で完結させる設計が原本準拠の方針と整合すると考える

---

## 不明点

- 実際にこの検体を図面上に配置してシミュレーション実行した際の具体的な挙動（短絡として現れるか、
  断線として現れるか、あるいは他の形か）は未実機確認。
- 忍者が発見した経緯（どのような操作手順でこの検体が生成されたか）の詳細は本調査では確認して
  いない。

---

## 出典・参照

- `C:\Users\kojif\OneDrive\ドキュメント\Ecad2\図形\自作\T068増分3c枠外検証部品.gcadpart`（検体、Read専用）
- `src/Ecad2.Core/Simulation/NetlistBuilder.cs`（139-158行目・205-218行目・294-338行目）
- `src/Ecad2.Core/Simulation/Evaluator.cs`（139-140行目）
- `src/Ecad2.App/Views/PartEditorCanvas.cs`（`UpdatePortDrag`、分岐Fのコメント）
- `docs/ecad2-t068-increment3-design-onmitsu.md`（289-294行目、分岐F・分岐Gの記録）
- `docs/ecad2-t068-port-reclamp-investigation-onmitsu.md`（前回調査、殿裁定の経緯）
