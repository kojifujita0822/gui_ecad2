# T-136(A)前提調査：シート種別の枷（制御回路／主回路）

隠密（key=1785485896132）記す。2026-07-31。家老采配より。

## 0. 調査題目・スコープ

**題目**：T-136(A)＝自作部品への「制御シート用」「主回路用」設定新設に向けた前提調査。

**DoD**：
1. シート種別が現在どう表現されておるか（データモデル・永続化に在るか無いか）——引き継ぎ書T-133§4-6の裏取り
2. 配置の関門の所在（`ValidatePlacement`等）と、枷を足す際の勘所
3. 【最重点】T-133裁定4（3極記号は主回路シート限定）とT-136(A)の関係——「対象が違う・粒度も違う」という前任の見立てを一次ソースで固める

**スコープ境界**：調査のみ。実装せぬ（侍へ委譲）。

---

## 1. シート種別の現状表現

### 1-1. データモデルには**既に在る**

`src\Ecad2.Core\Model\Sheet.cs:27`：

```csharp
/// <summary>主回路（動力回路）モード: 左右母線・母線名・自動横配線を描かず、自由直線で結線する。
/// 旧ファイルは false（=従来の制御回路）で互換。</summary>
public bool MainCircuit { get; set; }
```

`bool`型のフラグとして**既に存在し、永続化されている**（コメント「旧ファイルはfalse(=従来の制御回路)で互換」＝JSON永続化を通じた後方互換を明示）。UI側は`AddSheetDialog.xaml:22,25`のラジオボタン（`GroupName="SheetType"`、「制御回路」／「主回路」）で新規シート作成時に選ばせ、`SheetNavigationViewModel.cs:138,153`で`Sheet.MainCircuit`へ渡す。

### 1-2. 参照箇所は**描画・ページ分割のみ**（配置系ガードは0件）

`Grep "MainCircuit" src/`で全34件を洗い出した。用途別内訳：

| 用途 | 代表箇所 | 件数の傾向 |
|---|---|---|
| 描画（右母線を描かない等） | `DiagramRenderer.cs:135,142,215,318,324`／`LadderCanvas.cs:324` | 大半 |
| ページ分割（仮想行数） | `DiagramRenderer.cs:92,105,111` | 同上 |
| UI表示・分岐（現在シートが主回路か等のプロパティ） | `MainWindowViewModel.cs:378,380,382,386` | ボタンIsEnabled等 |
| 縦コネクタ・自由線の作図可否 | `MainWindowViewModel.cs:1965,2102` | 主回路では縦コネクタ作図不可等 |
| **配置(`ValidatePlacement`)のガード** | **0件** | **無し** |
| **Simulation層(`NetlistBuilder`等)** | **0件** | **無し（`Grep "MainCircuit" src/Ecad2.Core/Simulation/` → 0件）** |

**結論**：シート種別（`Sheet.MainCircuit`）はデータモデル・永続化・UI選択・描画分岐としては**既に完備**しているが、「このシートにはこの部品/種別しか置けない」という**配置制約としては一切使われていない**。

### 1-3. 引き継ぎ書T-133§4-6の裏取り＝**正しい。既存調査書で確定済み**

引き継ぎ書（`handover-next-session.md`§2）が「T-133§4-6＝主回路シートの前提が実装に無い」と記す件は、**既存の`docs/ecad2-t133-rowmatch-spec-questions-onmitsu.md`（采配4節、122-201行目）で既に一次ソース確認済み**であった（重複調査を避けるため転記でなく参照）。同書の結論：

> 「主回路シートに右母線の概念はない。DRCも接続診断も含まれていない」という殿の前提は、**実装上どこにも分岐が無く、結果的にそう見えているだけ**（DRCは全シートを回す`OutputPanelViewModel.cs:73`、`NetlistBuilder`は`MainCircuit`を一切参照しない等）。

**本調査で新たに確認したのは「配置(`ValidatePlacement`)にもガードが無い」という点**（同書は主にDRC・ネットリストを検分しており、配置導線そのものは対象外だった）。この点は本調査で追加確認した事実である。

---

## 2. 配置の関門の所在と、枷を足す際の勘所

### 2-1. 現在の唯一の関門＝`ValidatePlacement`

`src\Ecad2.App\ViewModels\MainWindowViewModel.cs:2943-2944`：

```csharp
private bool ValidatePlacement(GridPos pos, int cellWidth, int cellHeight, Sheet sheet, ElementInstance? exclude = null)
    => IsWithinGridBounds(pos, cellWidth, cellHeight, sheet) && !IsOccupied(pos, cellWidth, cellHeight, sheet, exclude);
```

現状は**境界内チェック＋占有チェックの2つのみ**。シート種別・部品種別のいずれも見ていない。呼び出し元は3箇所（`:1698`移動時・`:1739`ドラッグ移動時・`:3012`新規配置時）。

### 2-2. 枷を足す勘所（代表例、網羅は侍の判断）

- **新規配置時**（`PlaceElementAtSelectedCell`、`:2998-3012`）は既に`partId`から`PartDefinition`を`PartLibrary.Get(partId)`で解決済み（`:3003`）——**この時点で`definition`に新設フィールド（例:`SheetAffinity`）を持たせれば、`ValidatePlacement`呼び出しの前後に1行足すだけで済む**。シグネチャ変更（`ValidatePlacement`自体に`sheet.MainCircuit`条件を足す/呼び出し元で先に弾く）はどちらでも実装できるが、**既存(f)の設計（`ecad2-t133-increment4-design-samurai.md:101-102`）が「`ValidatePlacement`へ`sheet.MainCircuit`の条件を足す。シグネチャ変更は不要（既に`sheet`を受けており…）」という前例を残しており、これに倣うのが自然**。
- **移動時**（`:1698,1739`）も同じ`ValidatePlacement`を経由するため、新規配置と移動の両方に自動的に効く（既存部品を誤ってシート間ドラッグ移動しても弾かれる）。
- **メニュー/パレット側の予防的無効化**——(f)と同様「メニュー無効化（予防）＋`ValidatePlacement`拒否（防御・サイレント）」の両建てが既存パターン。パレット側は`PartPaletteViewModel`／`PartSelectionEntryViewModel`（`Entry.Definition`を保持）経由で描画されるため、`IsEnabled`をシート種別×`Definition`の新設フィールドで束ねるバインディングを追加する形になる見込み。`CanPlaceOnMainCircuit`（`MainWindowViewModel.cs:386`）が既に同型の前例（シート切替の通知2経路`:395-402`・`:418-424`に接続済み）。

---

## 3. 【最重点】T-133裁定4とT-136(A)の関係

### 3-1. 両者は**別の裁定**である（番号の偶然の一致にすぎぬ）

**T-133裁定4**の出典＝`docs/ecad2-t133-implementation-plan-samurai.md:23-30`「殿裁定の一覧（本計画の前提。すべて`docs/todo.md` T-133節より）」という**T-133専用の番号付きリスト（1〜9）**の4番目：

> 4 | **3極記号は主回路シート限定**＝メニュー無効化（予防）＋`ValidatePlacement`拒否（防御・サイレント、家老補強）

**T-136(A)の裁定**の出典＝引き継ぎ書§3「殿の御裁定（本日分・T-136の前提）」という**2026-07-28に新たに賜った、T-136専用の番号付きリスト（1〜5）**の4番目：

> 4. 【仕様変更】自作部品に「制御シート用」「主回路用」の設定を新設

**両者は出所となる番号付きリストが別文書・別日付・別題目であり、たまたま「4番目」が重なっているだけ**である。同じ「裁定4」という呼び名が、二つの独立した意思決定を指してしまっている——**この呼称の衝突が「二重になる」という誤認を招いた疑いがある**（下記3-3）。

### 3-2. 対象・経路とも構造的に分岐しており、二重にならぬ

**対象の違い**：

| | T-133裁定4（3極記号限定） | T-136(A)（新設） |
|---|---|---|
| 対象 | **`ElementKind`の固定3種**（`Breaker3P`/`ContactorMain3P`/`ThermalOverload3P`） | **`PartDefinition`**（自作パーツ全般、任意個） |
| 配置経路 | **Kind経路**（増分4で新設予定の`PlaceElementAtSelectedCell(ElementKind, ...)`オーバーロード、`ecad2-t133-increment4-design-samurai.md:97-98`） | **PartId経路**（既存の`PlaceElementAtSelectedCell(string partId, ...)`） |
| ポート | **0個**（`ElementCatalog.cs:47-48`「主回路3極記号は自由配線(FreeLine)で結線するため接続点を持たない」） | 任意（`PartDefinition.Ports`、`PartDefinition.cs:61`） |
| 現状の実装状態 | **未実装**（`ecad2-t133-increment4-design-samurai.md:15`「【保留】新規タスクの裁定待ち」） | 未着手（本タスクが起票） |

**決定的な一次ソース**＝`PartDefinition`（`PartDefinition.cs:48-63`）と`ElementKind`の3極記号3種は**モデル上まったく別系統**であり、橋渡しが無い：

- `PartResolver.Ports/CreatesComponent/ComponentKind`（`PartResolver.cs:10-14, 29-34, 43-68`）はいずれも`lib?.Get(e.PartId)`の`null`判定で**PartId経路(`PartDefinition`)とKind経路(`ElementCatalog`)へ完全に分岐**しており、3極記号（Kind経路専用、`PartId`を持たない）は**`PartDefinition`を一切経由しない**。
- ゆえに、T-136(A)で`PartDefinition`へ新設する「シート種別」フィールドは、**3極記号（`ElementKind`直接指定、`PartDefinition`不在）の配置には原理的に届かない**。3極記号を主回路限定にするには、**`PartDefinition`側の新設フィールドとは別に、`ElementKind`側（`ValidatePlacement`が`ElementInstance.Kind`を見る等）の独立した判定が要る**。

**→ 前任の見立て「対象が違う（`ElementKind`への制約／`PartDefinition`への制約）」は一次ソースで固まる。両者は同一の実装で片方がもう片方を兼ねることができず、T-136(A)を実装しても(f)（3極記号の主回路限定）は依然として別途必要**——**二重にはならない**。

**【前任の見立てへの補足・訂正】** 前任は併せて「粒度も違う（ポート単位／部品単位）」と述べていたが、本調査で一次ソースを確認した限り、より正確な言い方は**「対象の粒度が違う（`ElementKind`という固定3種の型レベル制約／`PartDefinition`という個々の自作パーツごとの設定レベル制約）」**である。「ポート単位」という表現の直接の裏付けは本調査では見つからず、伝聞・要約の過程で言葉が変化した可能性がある（`memory: feedback_preliminary_survey_not_final`と同種の転記劣化の疑い、事実として指摘するに留め、断定はしない）。

### 3-3. 「二重になる」という判断の発生源（事実の記録）

`docs/ecad2-t133-increment4-design-samurai.md:22`（侍記、2026-07-28）：

> **(1)は本書(f)の「主回路限定」と二重になる。今(f)を作れば、同じものを二度作ることになる**

この一文が「二重」判断の一次発生源である。**上記3-2のとおり、対象(`ElementKind`固定3種／`PartDefinition`任意)・経路(Kind経路／PartId経路)がいずれも構造的に分岐しており、一次ソース上は「同じもの」ではない。** ただし、両者に共通する点はある——**いずれも「`sheet.MainCircuit`を`ValidatePlacement`（またはその周辺）で見る」という同一の設計パターンを踏襲する**点であり、**実装の「型」は似るが「対象」は別**、というのが本調査の結論である。

---

## 4. 不明点・派生提案

- **(f)（T-133裁定4＝3極記号の主回路限定）を、T-136(A)の裁可を待たずに独立して先行実装してよいか**は家老・侍の裁量／殿の裁可を要する。本調査の結論（二重にならぬ）が正しければ、**(f)はT-136(A)の完了を待つ理由がなくなる**が、実装順序・段取りの判断は隠密の領分ではない。
- **T-136(A)のシート種別フィールドを、将来`ElementKind`側（3極記号等の組込み種別）にも拡張すべきか**は殿・家老の設計判断。本調査は「現状は届かない」という事実のみを述べる。
- 派生提案なし（本調査の範囲内で完結）。

---

## 5. 報告

家老へ`send_message`で本書のパスと要旨を共有する。
