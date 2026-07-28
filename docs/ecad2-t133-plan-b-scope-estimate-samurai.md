# T-133/T-131 案B 改修範囲の見立て（侍）

> 2026-07-28 侍。家老の下知に応じ、**案B（`ElementKind` を設定する配置経路の新設）の改修範囲を
> 読み切り、規模を見立てる**。隠密が「案Bの改修範囲は読み切っておらぬ、規模の断定は侍の見立てを
> 待つのが筋」と留保を付した箇所（`docs/ecad2-t133-guiecad-other-shapes-survey-onmitsu.md` §4-4）を埋める。
>
> **見立てのみ。実装には一切入っていない**（家老のスコープ境界指示）。
> 行番号はすべて `b0645c3` 時点。**核心はすべて侍が一次ソースを直読して確かめた。**

---

## 総括——**案Bを推す。隠密の案A評価には勘定漏れがあると見る**

隠密の所見は「**案A＝App層の改修が小さい／短所は `Type` を表現できぬこと**」であった。
**この『改修が小さい』という見立てに、描画を書き直す費えが勘定されておらぬ。**

**理由＝`PartId` を持つ要素は `SymbolGlyphs` を通らぬ。**
`DiagramRenderer.cs:1038` の `SymbolGlyphs.Draw` は **`part is null` の時にのみ呼ばれる**（直読確認）。
すなわち案A（`PartDefinition` 化）を採ると——

- **既に完成しておる `SymbolGlyphs.Breaker3P`（`:288`）・`ContactorMain3P`（`:313`）・
  `ThermalOverload3P`（`:331`）は一度も呼ばれぬ**
- 代わりに**同じ絵を `PartPrimitive`（線・円・弧・矩形の集合）で描き起こす**ことになる。
  **V/H切替（`Pole` struct による軸入替）・ELBのテストボタン分岐も、すべてプリミティブで再現する要がある**
- しかも**`PartDefinition` は `Params` を持たぬ**ゆえ、**`Type`(NFB/MCCB/ELB) を表すには型ごとに別パーツ**
  ——**3種 × V/H 2 × Type 3 ＝ 最大18パーツ**。隠密の見積「6パーツ」は**V/Hのみを数えた数**にござる

**ゆえに案Aは「小さい改修」ではなく、`SymbolGlyphs` の完成品を捨てて描き直す大工事**と見る。

---

## 1. 案Bの改修範囲——触るファイルと中身

### 1-1. 必須（これが無ければ配置できぬ）

| # | ファイル:行 | 中身 | 規模 |
|---|---|---|---|
| 1 | `MainWindowViewModel.cs:2916` `PlaceElementAtSelectedCell` | **`Kind`/`Params` を受けるオーバーロードを足す**。既存シグネチャは温存 | **小（+15行程度）** |
| 2 | 同 `:2921` `cellWidth` の解決 | 現状 `PartLibrary.Get(partId)?.WidthCells ?? 1`。**`Kind` 経路では `ElementCatalog.DefaultCellWidth(kind)` を引く分岐が要る**（3極記号＝2） | **極小（3行）** |
| 3 | `ToolState.cs:22` `Orient` | **宣言のみで未配線**（隠密確認、侍も `src`/`tests` とも参照0件を確認）。**器は在るゆえ繋ぐだけ** | **極小** |
| 4 | `MainWindow.xaml.cs` 配置導線 | メニュー選択→`Tool` 切替→キャンバスクリックで配置。**`TryPlaceActiveTool`（`:2381`）が `Tool.PartId is not string` で早期returnする**ゆえ、**`Kind` 経路の分岐を足す** | **中（40〜60行）** |
| 5 | `MainWindow.xaml` メニュー | 「パーツ(P)→その他図形」＋6エントリ | **小** |

### 1-2. 原本仕様を満たすために要るもの

| # | ファイル | 中身 | 規模 |
|---|---|---|---|
| 6 | `MainWindowViewModel.cs` プロパティパネル | **`Params[Type]` の切替**（NFB/MCCB/ELB）。**既存の `SelectedElementLampColor`（`:2247`）・`SelectedElementNotchPosition`（`:2221`）と同型ゆえ写経で足りる** | **小〜中** |
| 7 | `MainWindow.xaml` プロパティパネル | 上記の ComboBox ＋ 表示条件 | **小** |

**原本の作法**（隠密調査 §2-3、一次ソースのコメントに明記）＝
**向き(V/H)は配置時に確定し以後切替不可／型(NFB/MCCB/ELB)は配置後にプロパティで切替**。
**この非対称は原本の意図ゆえ踏襲する**（`memory: feedback_verify_design_intent_before_quickfix`）。

### 1-3. 触らずに済むもの（**案Bの利**）

- **`src/Ecad2.Core/**` は改修不要**——`ElementKind` 3種・`DefaultCellWidth`=2・ポート空・
  `SymbolGlyphs` 3メソッド・V/H切替・ELB分岐・`DiagramRenderer` の `Type` ラベル描画、**すべて実装済み**
- **`src/Ecad2.Pdf/**` は改修不要**——`ElementKind` を一切知らず、`DiagramRenderer` へ委譲しておる
- **DRC・ネットリスト・シミュレーションは改修不要**——3極記号は `ElementCatalog.CreatesComponent`
  の3述語（`IsContact`/`IsLoad`/`IsPassthrough`）のいずれにも入らぬゆえ、**全チェックから自動的に外れる**

---

## 2. 既存の配置フローへの波及——**最大の分岐は「配置バーを通すか否か」**

現在の配置は**一本道**にござる（侍が直読で確認）——

```
メニュー/ツールバー/F5〜F8/部品リスト/キャンバスクリック/Enter
  → TryPlaceElement(PartFolderEntry, isOr)     MainWindow.xaml.cs:3657
  → 配置バー表示（ComboBox＝PartSelectionEntryViewModel、デバイス名入力）
  → PlacementOkButton_Click                     :3970
  → PlaceElementAtSelectedCell(partId, …)       MainWindowViewModel.cs:2916
  → sheet.Elements.Add                          :2931   ← src内で唯一の追加箇所
```

**`TryPlaceElement` は `PartFolderEntry` を引数に取り（`:3657`）、`initialEntry.Definition.WidthCells`
でプレチェックする（`:3667`）。配置バーの ComboBox も `PartSelectionEntryViewModel`（`Definition` 必須）
に依存する。3極記号は `PartDefinition` を持たぬゆえ、この道には乗らぬ。**

| | (B-1) 配置バーを通さぬ【推奨】 | (B-2) 配置バーを通す |
|---|---|---|
| 形 | メニューで選ぶ→キャンバス単クリックで即配置。デバイス名は後からプロパティパネルで付ける | ComboBox を「`PartDefinition` または `ElementKind`」の双方を載せられる形へ拡張 |
| **原本との一致** | **一致する**（原本は配置バーを持たず単クリック配置＝隠密調査 §2-1） | 原本には無い形 |
| 規模 | **中**。`TryPlaceActiveTool` へ `Kind` 分岐を足すのみ | **大**。`PartSelectionEntryViewModel`・ComboBox・`ResolveEntry`・OK確定の全段に波及 |
| 短所 | **ecad2 の既存要素は全て配置バー経由ゆえ、3極記号だけ作法が違う**（**UI/UX分岐＝殿の裁可を要する**） | 作法は揃うが、器の拡張が大きい |

**侍の推奨＝(B-1)**。理由は規模と原本一致の двух点。
**ただし「3極記号だけ配置バーを通らぬ」という非対称は使用感に関わるゆえ、殿へ諮るべき事項**と心得る。

---

## 3. 回帰の危険どころ——4件

### 3-1. 【最重要・両案に共通】**縦方向の占有判定が存在せぬ**

**`ElementCatalog.cs:11` のコメントは「主回路3極記号は 2×2 セル（sample.png 準拠）」と述べておるが、
`ElementInstance` に高さを表す器が無い**（`Element.cs:44-56` を直読。`CellWidth` のみで `CellHeight` は不在）。

そして——

- **`IsOccupied`（`MainWindowViewModel.cs:2769-2774`）は `el.Pos.Row == pos.Row`＝同一行のみ**
- **`HitTestElement`（`:2779-2784`）も同じく行は完全一致**
- **`IsWithinGridBounds`（`:2735-2737`）は `cellWidth` しか見ず、高さを考慮せぬ**
  （対して `IsFrameWithinGridBounds`（`:2742-2744`）は `height` を持つ＝**GroupFrame だけが高さを扱える**）

**帰結**＝3極記号を配置しても、**下半分に他の要素を重ねて置けてしまう**。
**下半分をクリックしても選択できぬ**（P-077と同型の症状が縦方向に出る）。

**そして `PartDefinition.HeightCells` は救いにならぬ**——**図面の占有判定に一切使われておらぬ**ことを
侍が全使用箇所を列挙して確かめた。使われるのは**部品エディタ内のみ**（`PartEditorCanvas`・
`PartEditorDialog`・`PartShapeGeometry.ClampPort`・`PartOptimizer`）で、
**`MainWindowViewModel`・`DiagramRenderer`・占有判定のいずれからも参照0件**。
`BasicPartTemplates` の15件も**全て `HeightCells = 1`**。

→ **高さの穴は案A・案Bのいずれを採っても残る。ゆえに案の選択には影響せぬが、
別途の判断を要する論点**にござる。**「2×2」を本当に実現するならモデル（`ElementInstance`）と
占有判定の双方に手が要り、規模は本件と別建てになる。**

**【推奨】今回は「幅2・高さ1として扱う」と割り切り、高さは別途起票する**——
描画が2セル分の高さに及ぶか否かは `SymbolGlyphs` を読めば判るが、**占有と描画がずれること自体は
既存の `Motor`（幅3）でも起きておらぬ新しい事態**ゆえ、混ぜると本件の規模が読めなくなる。

### 3-2. `Kind` が既定値でない要素が**初めて生まれる**

**現状、`ElementInstance.Kind` へ非既定値を書く production コードは `src` 全体で 0 件**
（侍が `PlaceElementAtSelectedCell` を直読して確認。`Kind` が動くのは JSON 読込のみ）。
すなわち**配置UIから作られる全要素は例外なく `Kind == ContactNO`**。

案Bはこの前提を**初めて破る**。ゆえに **`e.Kind` を直接参照しておる箇所**が影響を受けうる。

- **`DiagramRenderer.cs:1023` `bool isContact = e.Kind is ContactNO or ContactNC`**
  → 3極記号は `Kind=Breaker3P` 等ゆえ `false` になる。**むしろ正しくなる方向**
- **既存要素の挙動は変わらぬ**（`Kind=ContactNO` のまま）
- **`memory: feedback_type_safe_alternative_scope_check`（型安全な代替提案は範囲確認とセットで）に
  照らし、着手時に `e.Kind` の直接参照を全件洗い直すこと**——**上記1件は侍が確認したが、
  網羅は着手時に改めて数え直す**（**この見立ての時点では網羅を主張せぬ**）

### 3-3. `PlaceElementAtSelectedCell` のシグネチャに**テストが多数依存**

**オーバーロード追加で回避する**（既存シグネチャを変えぬ）。変更にすると回帰が広がる。

### 3-4. 機器表の `DeviceClass` は**先行実装が到達不能**

`MapToDeviceClass:2871` に `ContactorMain3P → DeviceClass.Relay` が既に書かれておるが、
`ResolveDeviceClass:2907` が **`CreatesComponent` ガードで先に `Other` へ落とす**
（3極記号は `CreatesComponent=false`）。**到達させるか否かは仕様判断**——
**非シミュレート記号を機器表へどう載せるかは殿の裁可を要する**と見る。

---

## 4. 規模の見立て（DoD）

| | 案B (B-1) | 案A |
|---|---|---|
| 実装 | **120〜180行**（うちCore 0行） | **数百行**（`PartPrimitive` で3記号×V/H×ELBを描き起こし） |
| 新規パーツ定義 | 0 | **最大18**（3種×V/H 2×Type 3）。**Typeを諦めれば6** |
| テスト | **20〜30件** | 同程度＋プリミティブの形状テスト |
| `SymbolGlyphs` の既存実装 | **活きる** | **死蔵** |
| 原本仕様（Type切替） | **満たせる** | **満たせぬ**（隠密所見に同意） |
| 触るファイル | **実装5・テスト1〜2** | 実装3〜4＋パーツ定義 |

**規模＝「中」**と見立てる。**T-131（P-101＝配置導線、P-100＝Type ComboBox）と同一の穴**ゆえ、
**T-131を実装すればT-133の中核（差分2・3・4）が同時に満たされる**という隠密の見立てに同意する。

---

## 5. 案Aで良しとする道が残るか——**残る。ただし条件付き**

**「`Type` を諦める」なら案Aは成立する**（V/H の6パーツのみ）。その場合——

- **`SymbolGlyphs` の3メソッドをプリミティブで描き起こす費えは残る**（これが案Aの本体）
- **原本仕様からは外れる**（原本は配置後にプロパティで型を切替）
- **`Params` を使わぬゆえ `ToolState.Orient` の配線も不要**になり、App層は確かに小さくなる

**侍の所見＝それでも案Bを推す。** 決め手は**`SymbolGlyphs` の完成品が既に在ること**にござる。
**描く力が既に完成しておるのに、それを捨てて描き直すのは筋が通らぬ。**
案Bの「構造的制約に触れる」という短所は、**実測してみれば `PlaceElementAtSelectedCell` への
オーバーロード追加（+15行）と `cellWidth` の分岐（3行）で足りる**——**隠密が読み切れなんだ
この一点が、案Bの評価を変える。**

---

## 6. 副次所見（本件の範囲外。家老の判断を仰ぐ）

1. **`Element.cs:27` のコメントが実装と食い違う**——「すべて非シミュレート・**3セル幅**・縦流れ」と
   あるが、**`ElementCatalog.cs:12` の実装は幅2**（同ファイル `:11` のコメントも「2×2セル」）。
   **コード内コメントの陳腐化**にござる。**落とし先＝`docs/proposed.md` 起票の候補**
   （直すだけなら極小だが、`src/` への書き込みゆえ采配を要する）
2. **`ToolState.Orient`・`PinnedPartStore`・`LadderDocument.Library` の3つが「器はあるが未配線」**
   （隠密所見と一致。侍も `ToolState.Orient` の参照0件を確認）。
   **移植プロジェクトで「型はあるが結線が無い」を定期的に洗う価値**は侍も同意する。
   **落とし先＝`docs/proposed.md`**

---

## 7. 未確認・留保（断じておらぬ事柄）

1. **`SymbolGlyphs.Breaker3P`（`:288-312`）の本文は読んでおらぬ**——V/H切替とELB分岐が「実装済み」
   であることは隠密調査と subagent 調査の一致で受け取っており、**侍自身は switch の3行
   （`SymbolGlyphs.cs:45-47`）までしか直読しておらぬ**。**描画が縦2セル分に及ぶか否かも未確認**
2. **`e.Kind` の直接参照の網羅は主張せぬ**（3-2に既述）。着手時に数え直す
3. **実装行数の見積は経路の構造から導いた概算**であり、実装して確かめたものではない
4. **(B-1)/(B-2) の選択、および高さの扱いは殿の裁可を要する**と考える

---

## 8. 出典（**侍が直読した箇所**）

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs` — `PlaceElementAtSelectedCell` `:2916-2954`／
  `IsOccupied` `:2769-2774`／`HitTestElement` `:2779-2784`／`IsWithinGridBounds` `:2735-2737`／
  `IsFrameWithinGridBounds` `:2742-2744`／`ResolveDeviceClass` `:2907-2909`
- `src/Ecad2.App/MainWindow.xaml.cs` — `TryPlaceElement` `:3657-3698`／`PlacementOkButton_Click` `:3970-3988`／
  `TryPlaceActiveTool` `:2381-2387`
- `src/Ecad2.Core/Model/Element.cs`（全文）／`ElementCatalog.cs:1-40`／`PartResolver.cs`（全文）
- `HeightCells`・`WidthCells` の全使用箇所（機械的に列挙）

**受け取った調査（二次情報として扱い、核心は上記で裏を取った）**
- `docs/ecad2-t133-guiecad-other-shapes-survey-onmitsu.md`（隠密、原本仕様）
- `docs/ecad2-t133-ecad2-current-state-verification-ninja.md`（忍者、ecad2実機側）
