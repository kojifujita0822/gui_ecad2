# T-133 GuiEcad「その他図形」原本調査（隠密）

> 2026-07-27 着手・**2026-07-28 完了**（隠密）。原本＝`C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App`。
> DoD(1)〜(4) すべて確認済み。**前セッションの未了分（(1)後半・(2)・(3)・(4)）を本セッションで埋めた。**

---

## 0. 【最重要】「その他図形」は作図図形ではない——前提そのものの訂正

**原本の「その他図形」とは、矩形・円・直線といった作図図形ではなく、
「ツールバーに常設せぬ電気記号・パーツの寄せ集めメニュー」である。**

一次ソースに明記されておる——

- `MainPage.Parts.cs:63-64`
  ```csharp
  // その他図形 = 組込みのその他記号 ＋ 自作図形（左パネル「その他▼」と同じ並び）
  var other = new MenuFlyoutSubItem { Text = "その他図形" };
  ```
- `MainPage.Tools.cs:237`
  ```csharp
  // 「その他図形」の組込み記号（基本記号 a接点〜端子台 は含めない）。上メニューと左パネルで共有。
  ```

**含意**：忍者の実機調査（`docs/ecad2-t133-ecad2-current-state-verification-ninja.md`）は
「矩形・円・楕円・直線・文字・折れ線が ecad2 に無い」ことを8経路で尽くして確定させたが、
**原本の「その他図形」にも、それらは元より含まれておらぬ**。忍者の検証結果は正しく、
**探していた対象の方が実態とずれていた**——`docs/todo.md:398` の「現況＝いずれも未調査」の
段階で立てた見立てが、原本の実装と食い違っていた形にござる。

**ゆえに移植すべき中身は「作図機能」ではなく「記号メニューとその中身」である。**

---

## 1. DoD(1) 「その他図形」の中身——個々に列挙してから数を出す

### 1-1. メニュー階層（`RebuildShapeMenu()`＝`MainPage.Parts.cs:56-130`、全文精読）

```
図形(G)                                    ← メニューバー項目（Title＝"図形(G)"、:58）
├─ その他図形（MenuFlyoutSubItem、:64）
│    ├─ [A] 組込みのその他記号   AddOtherBuiltins(other.Items)          :65
│    ├─ [B] 組み込みパーツ       foreach (_builtinParts)                :68-73
│    ├─ （区切り）※ピン留めが1件以上ある時のみ                          :81
│    ├─ [C] ピン留め済み自作図形 foreach (pinnedEntries)                :83-87
│    ├─ （区切り）                                                      :90
│    └─ [D] 自作図形（サブメニュー）BuildCustomShapesSubItem()          :91
├─ （区切り）                                                            :94
├─ 自作図形を作成...                                                     :96-98
├─ 自作図形を読み込んで編集...                                            :100-102
├─ （区切り）                                                            :104
├─ 自作パーツをエクスポート (.gcadparts)...                               :106-112
└─ 自作パーツをインポート (.gcadparts / .gcadpart)...                     :114-116
```

**左パネル「その他部品▼」ボタン（`RebuildOtherPartMenu()`＝`MainPage.Tools.cs:170-215`）も
同じ [A][B][C][D] を共有する**（`AddOtherBuiltins` を双方が呼ぶ＝`Parts.cs:65` と `Tools.cs:175`）。
**上メニューと左パレットの2経路から同一の中身へ到達できる**という構造にござる。

### 1-2. [A] 組込みのその他記号＝`OtherBuiltins`（`MainPage.Tools.cs:238-252`）——**全10件**

**配列を全文読み、1件ずつ列挙してから数えた**（前回調査の6件は T-131 調査書からの抜粋であり、
前半4件が欠けておった。**転記ではなく一次ソースで再現した結果が下記**）。

| # | 表示ラベル | タグ | ElementKind | 向き |
|---|---|---|---|---|
| 1 | セレクトSW | `SelectSwitch` | SelectSwitch | 無 |
| 2 | サーマル(OL) | `ThermalOverload` | ThermalOverload | 無 |
| 3 | 非常停止 | `EmergencyStop` | EmergencyStop | 無 |
| 4 | 三相モータ | `Motor` | Motor | 無 |
| 5 | ブレーカ(NFB/MCCB/ELB) 縦 | `Breaker3P#V` | Breaker3P | V |
| 6 | ブレーカ(NFB/MCCB/ELB) 横 | `Breaker3P#H` | Breaker3P | H |
| 7 | 電磁接触器 主接点 縦 | `ContactorMain3P#V` | ContactorMain3P | V |
| 8 | 電磁接触器 主接点 横 | `ContactorMain3P#H` | ContactorMain3P | H |
| 9 | サーマル(OL) 2極 縦 | `ThermalOverload3P#V` | ThermalOverload3P | V |
| 10 | サーマル(OL) 2極 横 | `ThermalOverload3P#H` | ThermalOverload3P | H |

**合計10件。ElementKind としては7種**（1〜4 が各1件、5〜10 が3種×向き2）。

### 1-3. [B] 組み込みパーツ＝`_builtinParts`（`MainPage.xaml.cs:49-68`）——**2件**

`LoadBuiltinParts()` を関数丸ごと読み、埋め込みリソース名を1件ずつ列挙した（`:54-58`）：

1. `GuiEcad.App.thermal-relay-a.gcadpart`
2. `GuiEcad.App.thermal-relay-b.gcadpart`

**合計2件**（アセンブリ埋め込み。`GetManifestResourceStream` が null なら黙って skip＝`:63`）。

### 1-4. [C] ピン留め済み自作図形——**可変（ユーザー次第。0件なら区切りごと出ない）**

`_pinnedIds`（`PinnedPartStore` が保存）に含まれる自作図形のみ直下へ平坦に並ぶ（`Parts.cs:76-88`）。
ピン留めのトグルは自作図形サブメニュー内の項目（`Parts.cs:168-171`、
ラベルは「その他図形にピン留め」／「その他図形のピン留めを解除」）。

### 1-5. [D] 自作図形サブメニュー——**可変（ディスクの「図形/自作/」配下）**

`BuildCustomShapesSubItem()`（`Parts.cs:133-148`）。カテゴリが `"自作"` または `"自作/"` 始まりの
エントリのみ、名前順。**0件なら `(なし)`（Disabled）を1件置く**（`:143`）。
各図形は「配置／編集.../削除／ピン留め」の4項目サブメニュー（`BuildShapeSubMenu`＝`:151-174`）。

**なお原本では基本図形の実フォルダへのシードは廃止されておる**（`Parts.cs:39` コメント
「基本図形の実フォルダへのシードは廃止（配置は自作図形のみ・たたき台はコードの組込み図形から）」）。
**ecad2 は逆にシードが有効**（後述 3-2）——ここが両者の構造上の分岐点にござる。

---

## 2. DoD(2) 配置操作とパラメータの持ち方

### 2-1. 配置操作＝「メニューで選ぶ→ツール状態を切替→キャンバスを単クリックで配置」

`OnOtherPartSelected`（`Tools.cs:93-113`）が受け、`_tool` を `ToolMode.PlaceElement` へ差し替える。
併せて走る副作用を1つずつ挙げる（`:99-102`）：

1. `_frameStartMm = null`（枠の作画途中座標を解除）
2. `_lineStartMm = null`（直線の作画途中座標を解除）
3. `_connStartRow = null`（縦渡りの作画途中を解除）
4. `ClearToolRadios()`（左パレットのラジオ選択を全解除＝`Tools.cs:75-79`）
5. `OtherPartButton.Content = item.Text`（左パネルのボタン表示を選択名へ書き換え＝`:111`）

**副作用1〜4はコメントに理由が明記されておる**（`:97-98`「直前に『直線』等を選んでいた場合
クリックがそちらに吸われ記号を配置できなくなるため」）。

配置操作そのものは**単クリック**——`UpdateHintText()`（`Parts.cs:431`）が
`ToolMode.PlaceElement` に対し `"クリック: 配置 | 右クリック: メニュー | Esc: 選択に戻る"` を出す。
**ドラッグではない**（枠＝`PlaceFrame` のみ `"ドラッグ: 枠描画"`、`:433`）。

### 2-2. タグ形式は**4種**であり、`Kind#Orient` は共通形式ではない

前回調査の未確認事項「`Kind#Orient` 形式が全種共通か」への回答＝**共通ではない**。

| # | 形式 | 例 | 解釈箇所 |
|---|---|---|---|
| 1 | `Kind` 単独 | `SelectSwitch` | `ParseSymbolTag`（`Tools.cs:63-72`）。`#` 無しなら Orient=null |
| 2 | `Kind#Orient` | `Breaker3P#V` | 同上。`#` で分割し後半を Orient へ（`:68`） |
| 3 | `part:<PartId>` | `part:xxxx` | `OnOtherPartSelected`（`Tools.cs:105-107`） |
| 4 | `builtin-part:<Id>` | `builtin-part:thermal-relay-a` | `OnPlaceBuiltinPart`（`Parts.cs:211-230`） |

**加えてピン留め項目は Tag に生の Id を直接置く**（`Parts.cs:84`、`OnPlacePinnedPart`＝`:177-193`）。
すなわち**形式は実質5通り**であり、`ToolFromTag` の固定タグ（`connector`/`wirebreak`/`frame`/
`line`/`dot`/`select`＝`Tools.cs:52-61`）とも別系統にござる。

### 2-3. パラメータの持ち方

- **向き（V/H）＝タグに埋め込む**。`ToolState.Orient`（`Tools.cs:41`）へ載り、
  **配置時に確定して以後切替不可**（`Tools.cs:244-245` コメント「タグ "Kind#V/#H" で配置時に
  向きを確定（切替不可）」）
- **ブレーカの型（NFB/MCCB/ELB）＝配置後にプロパティパネルで切替**（同コメント
  「型(NFB/MCCB/ELB)はブレーカ配置後にプロパティパネルで切替」）。**タグには載らぬ**
- **PartId 指定時は Kind が無視され、既定値 `ElementKind.ContactNO` が置かれる**——
  この扱いは4箇所すべてで同一（`Tools.cs:106`／`Parts.cs:190`／`:227`／`:244`。
  うち`:226`と`:243`にコメントで明記）

---

## 3. DoD(3) ecad2側の照合——層ごとに判定

### 3-1. Core/Model・Core/Rendering・Pdf は**移植済み**

**`OtherBuiltins` 10件が指す ElementKind 7種は、すべて ecad2 に存在する**
（`src/Ecad2.Core/Model/Element.cs:17-30`。SelectSwitch・ThermalOverload・EmergencyStop・Motor・
Breaker3P・ContactorMain3P・ThermalOverload3P を1件ずつ照合）。

3極記号3種について層別に確認した結果（サブエージェント調査＋出典突合）：

| 層 | 判定 | 主な出典 |
|---|---|---|
| Model（enum・セル幅・ポート） | **有り** | `Element.cs:28-29`／`ElementCatalog.cs:12`（3種とも幅2）／`:31-32`（ポート空＝自由配線） |
| パラメータキー | **有り** | `Element.cs:10`（`Type`）・`:12`（`Orient`） |
| Rendering（描画本体） | **有り** | `SymbolGlyphs.cs:45,46,47`（switch3件）／`:288`Breaker3P・`:313`ContactorMain3P・`:331`ThermalOverload3P（専用メソッド3件） |
| Rendering（V/H出し分け） | **有り** | `SymbolGlyphs.cs:16`(H判定)・`:246-247`(極配列)・`:256`(XY入替)・`:292`/`:317`/`:335`(各記号の軸切替) |
| Rendering（Type出し分け） | **一部有り** | `SymbolGlyphs.cs:307-309`＝**ELBのみ形状分岐**（NFBとMCCBは同形）／`DiagramRenderer.cs:1044-1057`＝文字ラベルは3値とも表示 |
| 描画の結線 | **有り** | `DiagramRenderer.cs:1034,1038-1039`（本描画）／`:1119,1131-1132`（プレビュー・サムネイル） |
| PDF出力 | **有り（共有）** | `PdfExporter.cs:24,40-44` が同じ `DiagramRenderer` を通す。Pdf配下に3種を名指しする箇所は0件 |

**すなわち「描く力」は既に完成しておる。**

### 3-2. App層は**欠落**——値を書き込む経路が存在せぬ

- **`ParamKeys.Orient` の参照はリポジトリ全体で2件、いずれも読み取り**
  （`DiagramRenderer.cs:1034` と `:1132`）。**書き込み0件**
- **`ParamKeys.Type` の参照は3件、いずれも読み取り**
  （`DiagramRenderer.cs:1039,1046,1132`）。**書き込み0件**
- **`src/Ecad2.App/` 内で3極記号に触れるのは2箇所のみ、いずれも機器表分類であって配置ではない**
  （`MainWindowViewModel.cs:2871` の `MapToDeviceClass`、および `:2807-2815`/`:2868` のコメント）
- **`ToolState.Orient`（`src/Ecad2.App/ViewModels/ToolState.cs:22`）は宣言のみで未配線**——
  `Orient:` 名前付き引数の使用も `.Orient` の読み出しも `src/`・`tests/` とも0件。
  **原本 `ToolState`（`Tools.cs:37-41`）の器だけが移植され、中身が繋がっておらぬ形**

### 3-3. 【構造上の要点】配置フローが `ElementKind` を設定せぬ

ecad2 の配置は**`PartDefinition`（`PartId`）ベース一本**であり、
**`PlaceElementAtSelectedCell` は `ElementInstance.Kind` を設定せず、常に既定値 `ContactNO` のまま
固定される**（T-046由来の既知の構造的制約。`MainWindowViewModel.cs:2826-2830` および `:2839-2844`
のコメントに明記。電気的種別は `PartResolver.ComponentKind` が `PartDefinition.Role` から解決する）。

**3極記号は `PartDefinition` を持たぬ**（`BasicPartTemplates.cs` に3極記号のヒット0件）。
**ゆえに既存の配置フローにそのままでは乗らぬ**——ここが移植の要にござる。

### 3-4. 部品リスト17件の内訳が突合できた

`BasicPartTemplates.All()`（`src/Ecad2.Core/Persistence/BasicPartTemplates.cs:55-72`）を
1件ずつ列挙して数えた＝**15件**（a接点／b接点／コイル／端子台／セレクトSW／押釦NO／押釦NC／
表示灯／モータ／タイマ接点NO／タイマ接点NC／タイマ瞬時接点NO／タイマ瞬時接点NC／サーマル／非常停止）。

これに `IsOrEligible` の論理エントリ2件（ORa接点・ORb接点、`PartPaletteViewModel.cs:75-76`）を
加えて **15＋2＝17件**。**忍者が実機UIAで採取した17件と寸分違わず一致する。**

**ecad2 は原本と異なりシードが有効**（`PartPaletteViewModel.cs:47` が `SeedBasics()` を呼ぶ／
`PartFolderStore.cs:152-164`）。**部品リストの実体はディスクの「図形」フォルダ**にござる。

### 3-5. 「その他図形」の残る構成要素 [B][C][D] の照合

| 原本の要素 | ecad2の対応 | 判定 |
|---|---|---|
| [B] 組み込みパーツ2件（thermal-relay a/b、EmbeddedResource） | **無し**。`_builtinParts`・`thermal-relay` とも `src/` にヒット0件。`.csproj` の EmbeddedResource は `docs/usage/*.md` 11件のみで、パーツ定義は同梱されておらぬ | **欠落** |
| [C] ピン留め | `PinnedPartStore` は**Core層に実装済みだが孤立**（`src/Ecad2.App` からの参照0件・`tests/`も0件）。保存先は `マイドキュメント\Ecad2\pinned-parts.json` | **機構有り・UI結線のみ欠落** |
| [D] 自作図形サブメニュー | `パーツ(P)→自作パーツ(C)` として**実装済み**（忍者確認、0件時は `(なし)`）。保存先は `マイドキュメント\Ecad2\図形\自作\<名前>.gcadpart` | **有り（形は異なる）** |

**参考**：`LadderDocument.Library`（ドキュメント埋め込み）も型はあるが**代入コード0件で未使用**。
原本の「.GCAD 自己完結」（`Parts.cs:238` コメント）は ecad2 では機能しておらぬ。
**本件の範囲外だが、`docs/proposed.md` 起票の候補として記す。**

---

## 4. DoD(4) 移植の規模見積——**「ゼロから作る」ではなく「導線を繋ぐ」**

### 4-1. 結論

**Core・Rendering・Pdf は完成しており、欠けているのは App 層の導線のみ。**
描画実装（`SymbolGlyphs` の3メソッド＋V/H＋ELB分岐）は**そのまま活かせる**。

### 4-2. 埋めるべき差分（個々に列挙）

1. **メニュー階層「パーツ(P)→その他図形」の新設** — 現在 `パーツ(P)` は2件のみ（忍者確認）
2. **3極記号6エントリの配置導線**（Breaker3P・ContactorMain3P・ThermalOverload3P × V/H）
   ——**本件最大の山**
3. **`Orient` の書き込み経路**（配置時に確定・以後切替不可、原本準拠）
4. **`Type` の書き込み経路**（配置後にプロパティパネルで切替、原本準拠）
5. **組み込みパーツ2件（thermal-relay a/b）の移植** — 要否は判断待ち（後述）
6. **ピン留めのUI結線** — `PinnedPartStore` は実装済みゆえ結線のみ
7. **既存4件（セレクトSW／サーマル／非常停止／モータ）をメニューへ再掲するか** — **UI/UX分岐**
   （既に部品リスト17件に在るため、再掲すると同じ記号が二経路に並ぶ）

### 4-3. 【最重要】T-131 と同じ穴である

**上記2・3・4は、T-131（主回路系パラメータUI）が対象とする穴とまったく同一にござる。**

- T-131 の忍者確認＝**「Breaker3P等3極記号3種を配置する手段そのものが存在せぬ」**
  （`docs/todo.md:326-329`。App層の `ParamKeys` 参照0件も同じ根拠）
- T-131 の実装順序＝**P-101（配置前選択UI）が先、P-100（種別ComboBox）が後**（殿裁可済み）
- **P-101 ＝ 差分2・3（配置導線と向き指定）／P-100 ＝ 差分4（Typeの切替）** に対応する

**すなわち T-131 を実装すれば、T-133 の中核部分（差分2・3・4）はそれで満たされる公算が高い。**
T-133 に固有で残るのは**差分1（メニュー階層）・5（組み込みパーツ2件）・6（ピン留め結線）・
7（再掲の是非）**にござる。

**【推測と明示する】**両タスクの重複度合いをどう捌くか（T-131へ寄せるか、T-133を親として
T-131を内包させるか）は**采配の領分**ゆえ、隠密は判断せず家老へ委ねる。

### 4-4. 配置導線の設計分岐（2案。**是非は殿の裁可を要する**）

3極記号は `PartDefinition` を持たぬため、既存の配置フローに乗せる形が2通りある。

| | 案A：`PartDefinition` 化 | 案B：`ElementKind` 経路の新設 |
|---|---|---|
| 中身 | 3極記号を `BasicPartTemplates` へ追加し、部品リスト17件の仲間にする | `ElementKind` を設定する配置経路を設け、`SymbolGlyphs` の描画を使う |
| 長所 | 既存の配置フロー・サムネイル・選択UIにそのまま乗る。App層の改修が小さい | **既存の描画実装（`SymbolGlyphs` 3メソッド＋V/H＋ELB）がそのまま活きる**。`Orient`・`Type` を `Params` で持てる |
| 短所 | **V/Hを別パーツとして2件ずつ登録が要る**（3種×2＝6パーツ）。`Type`(NFB/MCCB/ELB)は `PartDefinition` では表現できぬ。**`SymbolGlyphs` の既存実装が死蔵になる** | `PlaceElementAtSelectedCell` が Kind を設定せぬという **T-046由来の構造的制約に触れる**。改修範囲が広い |
| 隠密の所見 | 短所の「Type を表現できぬ」が致命的——**原本仕様（配置後にプロパティで切替）を満たせぬ** | **原本仕様を満たせるのはこちら**。ただし構造改修を伴う |

**【留保】**案Bの改修範囲は `PlaceElementAtSelectedCell` の周辺を読み切っておらぬゆえ**未確定**。
**規模の断定は侍の見立てを待つのが筋**と心得る。

### 4-5. 判断待ち（隠密は決めぬ）

1. **組み込みパーツ2件（thermal-relay a/b）を移植するか** — ecad2 の `basic-thermal-overload`
   （サーマル）と用途が近いが、原本はa接点版・b接点版の2つを別途持つ。**同梱方式も要決定**
   （原本は EmbeddedResource、ecad2 は C# コード定義の `BasicPartTemplates`）
2. **既存4件をメニューへ再掲するか**（差分7）— **UI/UX分岐**
3. **ピン留めを今回の範囲に含めるか**（差分6）
4. **配置導線は案A・案Bのいずれか**（4-4）

---

## 5. 出典

**原本（GuiEcad）**
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Parts.cs`（全文441行、精読）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Tools.cs`（全文266行、精読）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.xaml.cs`（40-80行、`_builtinParts`／`LoadBuiltinParts` 全文）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.Core\Persistence\PartFolderStore.cs`（全文106行）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.Core\Persistence\BasicPartTemplates.cs`（全文125行）

**ecad2**
- `src/Ecad2.Core/Model/Element.cs`／`ElementCatalog.cs`／`PartDefinition.cs`
- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs`／`PartFolderStore.cs`／`PinnedPartStore.cs`
- `src/Ecad2.Core/Rendering/SymbolGlyphs.cs`／`DiagramRenderer.cs`
- `src/Ecad2.App/ViewModels/PartPaletteViewModel.cs`／`MainWindowViewModel.cs`／`ToolState.cs`
- `src/Ecad2.Pdf/PdfExporter.cs`／`PdfRenderSurface.cs`

**既存調査書**
- `docs/ecad2-t133-ecad2-current-state-verification-ninja.md`（忍者、ecad2実機側）
- `docs/ecad2-t131-guiecad-breaker-type-orient-ui-survey-onmitsu.md`（前段、T-131原本調査）
- `docs/todo.md:326-329`（T-131の忍者確認結果）

---

## 6. 気づきと落とし先

1. **【最重要】調査の前提そのものが実態とずれていた** — 家老の采配文・忍者のDoD・台帳のいずれも
   「その他図形＝矩形・円等の作図図形」という読みで組まれていたが、**原本のコメント2行を読めば
   即座に判る食い違い**であった（`Parts.cs:63`／`Tools.cs:237`）。忍者は8経路を尽くして
   「無い」を正しく証明したが、**探す対象の定義が先に固まっておらなんだ**。
   **「無いことを尽くす」前に「何を探すのかを一次ソースで定義する」工程が要る。**
   → **落とし先＝`onmitsu.md` 調査ワークフロー節（1. 題目・範囲確認）へ一項**。
   忍者側にも効くゆえ **`ninja.md`「『無い』と報ずるには経路を先に列挙してから尽くす」節への
   補足**としても機能する（**どちらへ落とすかは家老の判断を仰ぐ**）
2. **数え上げの誤りが本件でも起きていた** — 前回の自分の調査書が「6件」と書いた `OtherBuiltins` は
   **実は10件**で、前半4件が欠けておった。**T-131調査書からの転記**が発端にござる
   （`onmitsu.md`【MUST】「転記と再現は別物」の実例が、また1件増えた形）。
   **今回は配列を全文読んでから数えたゆえ確定できた。**
   → **落とし先＝既に `onmitsu.md` に制度化済み。追加の落とし先は不要**（実例としてのみ記す）
3. **「器はあるが繋がっておらぬ」箇所が3つ揃って見つかった** — `ToolState.Orient`（宣言のみ）・
   `PinnedPartStore`（孤立クラス）・`LadderDocument.Library`（代入0件）。
   **いずれも移植途上の残置と見られる**（**推測と明示する**）。
   **移植プロジェクトでは「型はあるが結線が無い」を定期的に洗う価値がある**やもしれぬ。
   → **落とし先＝`docs/proposed.md` 起票の候補**（**家老の判断を仰ぐ。隠密は起票せぬ**）
