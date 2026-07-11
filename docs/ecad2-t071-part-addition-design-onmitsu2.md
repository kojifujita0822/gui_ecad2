# T-071 経路B部品追加：着手前設計整理

調査者: 隠密2　最終更新: 2026-07-11

家老采配（殿裁定3点確定済み：経路BのUI再現不要／部品選択リストから呼べればよい／コイル等重複分は
追加不要）を受け、追加対象一覧の確定と`BasicPartTemplates`への追加方針（PartDefinition構造・
サムネイル対応方式）を整理する。**実装は行っていない、着手前調査のみ**。

前提資料：`docs/ecad2-guiecad-hardcoded-parts-diff-survey-onmitsu2.md`（本セッション前回調査、
以下「前回調査書」）。実ソース照合はRead直読中心。

---

## 1. 追加対象一覧の確定

`docs/todo.md` T-071節記載の「約10種」と前回調査書DoD(3)差分表を突合した結果、**完全一致**を確認：

| # | 部品 | ElementKind |
|---|---|---|
| 1 | 押釦NO | `PushButtonNO` |
| 2 | 押釦NC | `PushButtonNC` |
| 3 | タイマNO(接点) | `TimerContactNO` |
| 4 | タイマNC(接点) | `TimerContactNC` |
| 5 | タイマ瞬時NO | `TimerInstantContactNO` |
| 6 | タイマ瞬時NC | `TimerInstantContactNC` |
| 7 | 表示灯(ランプ) | `Lamp` |
| 8 | サーマル(OL) | `ThermalOverload` |
| 9 | 非常停止 | `EmergencyStop` |
| 10 | 三相モータ | `Motor` |

セレクトSWは前回調査書DoD(3)#8のとおり`BasicPartTemplates`に既登録（`basic-select-switch`）のため
対象外——todo.md記載の除外理由と一致。**対象一覧に齟齬なし、確定でよい**。

---

## 2. PartDefinition追加方針（3グループに分類）

`BasicPartTemplates`経由で配置される要素は全て`ElementInstance.PartId`を持ち、シミュレーション時の
種別解決は`PartResolver.ComponentKind()`（`src/Ecad2.Core/Model/PartResolver.cs:43-59`）が
`PartDefinition.Role`（`PartRole`enum、`PartDefinition.cs:6`）から`ElementKind`へ変換する経路を通る
（`ElementInstance.Kind`フィールド自体は配置時に設定されず既定値のまま使われない）。**この変換層の
カバー範囲が対象10種に対してどこまで足りているか**が、前回調査書では未確認だった論点。

現状の`PartRole`は8種のみ：`ContactNO, ContactNC, Coil, Lamp, Terminal, NonSimulated, InputNO, InputNC`。
`ComponentKind()`のswitchはこのうち7種のみ`ElementKind`へマッピングし（`NonSimulated`は
`CreatesComponent()=false`でそもそも呼ばれない設計）、対応表は以下：

| PartRole | → ElementKind |
|---|---|
| InputNO | PushButtonNO |
| InputNC | PushButtonNC |
| Lamp | Lamp |
| ContactNO/NC | ContactNO/NC |
| Coil | Coil |
| Terminal | Terminal |

これを対象10種に照らすと3グループに分かれる。

### グループ1：既存Roleで対応可（規模「小」）— 押釦NO/NC・表示灯（3種）

`PartRole.InputNO`/`InputNC`/`Lamp`が既に`ComponentKind()`で正しい`ElementKind`へ写像される。
**Core層・変換層とも変更不要**。`SymbolGlyphs.cs`の座標（+0.5オフセット変換規則、前回調査書記載）を
なぞって`PartDefinition.Primitives`を新規作成するだけで済む。既存5種と同じパターン。

### グループ2：既存Role流用可・ポート定義のみ別途必要（規模「小」）— モータ（1種）

`ElementCatalog.CreatesComponent(Motor)`は`IsContact`/`IsLoad`/`IsPassthrough`いずれもfalseのため
`false`（非シミュレート記号）。`PartResolver.CreatesComponent()`は`Role != NonSimulated`のみを見るため、
**`PartRole.NonSimulated`をそのまま流用可能**（`ComponentKind()`自体が呼ばれない設計）。ただし
モータは3端子(U/V/W)のためTwoPorts()は使えず、`ElementCatalog.Ports(Motor,...)`
（`ElementCatalog.cs:24-29`）と同型の専用3ポート定義が必要。`WidthCells`も`ElementCatalog.
DefaultCellWidth(Motor)=3`に倣い3を設定する。

### グループ3：PartRole拡張が必要（規模「小〜中」）— タイマNO/NC・瞬時NO/NC・サーマル・非常停止（6種）

対応する`PartRole`が存在しない。**単純に既存Role（例：ContactNO/NC）へ丸めることは電気的挙動・
機器分類の両面で劣化を招くため推奨しない**：

- タイマ接点2種・瞬時接点2種：`ElementCatalog.IsInputControlled()`のコメントに明記の通り「タイマ
  コイル励磁＋経過時間で制御」される特殊接点で、通常のContactNO/NCとは評価ロジックが異なる
  （Timer本体との連動）。ContactNO/NCへ丸めると即時接点になり遅延評価が失われる。
- サーマル・非常停止：`IsContact=true`かつ`IsInputControlled=true`（外部入力扱い）だが、
  `MapToDeviceClass()`（`MainWindowViewModel.cs:1450-1463`）では非常停止→`PushButton`、
  サーマル→`Other`と、押釦とも接点とも異なる分類。ContactNO/NCへ丸めるとDeviceClass分類が
  `Relay`に化けてしまう（機器表の「種別」列が誤表示になる）。

**方針**：`PartRole`enumへ6種を追加し、`PartResolver.ComponentKind()`のswitchへ対応ケースを追加する
（機械的な1対1追加）。追加後は`MapToDeviceClass()`が既にElementKindベースで全種対応済み
（Timer系→Timer、EmergencyStop→PushButton、ThermalOverload→Other）のため、**DeviceClass分類側の
追加実装は不要**——PartResolver層さえ拡張すれば機器表分類は自動的に正しく動く。

**後方互換性**：`PartRole`は`JsonStringEnumConverter`で文字列直列化（`JsonOptions.cs:18`、
例："contactNo"）のため、enum値の追加は既存保存済みドキュメントに影響しない（確認済み）。

---

## 3. サムネイル対応方式（追記2026-07-11：殿ご指摘によりGuiEcad実装を追加調査、当初評価を訂正）

部品の見た目表示は独立した3箇所があり、対応状況が異なる。**当初「配置バー全体で専用アイコンが
出ない」と報告したが、実際に自動対応外なのはコンボの小アイコンのみと判明した（訂正）**。

### 経路(a) 部品選択パレット本体のサムネイル＋配置バーの選択中部品大サムネイル — `PartThumbnailRenderer`

`DiagramRenderer.DrawPreview`経由で`PartDefinition.Primitives`をそのまま描画する汎用実装
（ORa/ORb特殊グリフの例外を除く）。部品選択リスト本体（`PartPaletteViewModel.cs:51,61`）と、
配置バーの`SelectedPartThumbnail`（`MainWindow.xaml:643`、`PartSelectionEntryViewModel.Thumbnail`
経由、同じ`PartThumbnailRenderer.Render()`を共有）の**両方がこの経路**。**Primitivesさえ書けば
新規10種すべて自動的に正しく表示される。追加対応は不要**（訂正前の評価どおり、ここは変更なし）。

### 経路(b) 配置バーComboBoxの小アイコン（開閉時・ドロップダウン各項目） — `PartEntryToGlyphGeometryConverter`

`MainWindow.xaml:621-630`の`PlacementPartComboBox`専用。個別Geometryが用意されているのは
`ContactNO/NC`・`Coil`・`Terminal`・SelectSwitch特殊ケースの5パターンのみ、それ以外は「Custom」
（自作パーツと同じ汎用フォルダアイコン）にフォールバックする。**自動対応外なのはこの経路のみ**。

### GuiEcad実装との比較（殿ご指摘を受けた追加調査）

殿よりGuiEcadの経路B専用パレット（画像参照）に専用グリフがある旨ご指摘があり、実体を確認した。
GuiEcadのツールボタンアイコン（`IconContactNO`等11種、`MainPage.xaml:318`等）は個別の手作り画像
ではなく、**実体の記号描画ロジックをそのままSVG化して流用**している：

- `MainPage.Palette.cs:271-292`（`LoadToolIconsAsync`）が`SvgRenderer.GenerateSymbolSvg(kind, ...)`
  を呼び、`Image.Source`へ設定。
- `SvgRenderer.cs:110-127`（`GenerateSymbolSvg`）は内部で`SymbolGlyphs.Draw(renderer, stroke, kind,
  cell, cell)`——**GuiEcad本体の記号描画ロジックをそのままアイコン化**。個別グリフを手で描いて
  いるわけではない。

ecad2にも同型の`SymbolGlyphs.Draw`（`src/Ecad2.Core/Rendering/SymbolGlyphs.cs`）が大部分の種別で
既に完備（前回調査書のDoD(4)記載どおり）。ただし、ecad2の経路(b)（`PartEntryToGlyphGeometryConverter`）
は`MainWindow.xaml:611-620`のコメントにある通り、GuiEcadとは異なり**「ツールバー2段目(T-040)と
統一されたGX様式の簡略化アイコンを意図的に手作りする」設計**であり、実体記号をそのまま流用する
方式ではない（既存5種の統一感を優先した設計判断とみられる）。

新規10種の経路(b)対応には2方式が考えられる：

| 方式 | 内容 | コスト | トレードオフ |
|---|---|---|---|
| A. GX様式で新規手作り | 既存5種と同じ意匠でPath Data(Geometry)を10種分新規に描く | 小〜中（10種分の手作業） | 既存アイコン群との統一感を維持 |
| B. 実体記号を流用（GuiEcad方式） | `Path`→`Image`へ差し替え、`PartThumbnailRenderer`をコンボの小アイコンにも流用 | 小（描画ロジック新規追加は不要、XAML構造の変更のみ） | 経路(a)の精密な記号と同じ絵になり、既存5種のGX簡略化アイコンとは意匠が混在する可能性 |

機能的な支障はないが見た目上の判断が伴う——**方式選定はUI/UX判断につき、着手時に殿確認が必要な
論点**として申し送る（経路(a)は自動対応につき確認不要、経路(b)のみが論点）。

---

## 4. 前回調査書の結論に対する補正

前回調査書DoD(4)は「Core層の器は…完全に揃っている」としたが、これは`ElementCatalog`
（`ElementKind`ベースの分類：描画・シミュレーション分類・DeviceClass写像）の確認に基づくもので、
**実際の配置経路である`PartResolver`/`PartRole`層（BasicPartTemplates経由で配置する場合に必須）は
未確認だった**。本調査で、この変換層は対象10種のうち7種（押釦NO/NC・表示灯・モータ）のみ対応済みで、
残り6種（タイマNO/NC・瞬時NO/NC・サーマル・非常停止）は`PartRole`enum拡張が要ることが判明した。

とはいえ拡張自体はenum値6つ＋switch6ケースの機械的追加であり、**総合規模の見立て（「小〜中」）は
前回調査書から大きくは変わらない**。変更が及ぶファイルが`BasicPartTemplates.cs`だけでなく
`PartDefinition.cs`（enum定義）・`PartResolver.cs`（switch拡張）にも及ぶ、という点が前回想定より
一段具体的になった、というのが本調査の実質的な追加分。

---

## 5. 着手時に殿確認が必要な論点（UI/UX、実装はまだ行わない）

1. 配置バー簡易アイコン（経路(b)）：新規10種に専用グリフを用意するか、Customフォールバックのままで
   よいか。
2. （設計方針として妥当と考えるが一応明記）タイマ接点・瞬時接点・サーマル・非常停止は`PartRole`
   enum拡張で対応する方針でよいか（丸め処理での妥協は非推奨、上記2節参照）。

---

## 出典一覧

- `docs/todo.md`（T-071節）
- `docs/ecad2-guiecad-hardcoded-parts-diff-survey-onmitsu2.md`（前回調査書、Read全文）
- `src/Ecad2.Core/Model/PartDefinition.cs`（Read全文）
- `src/Ecad2.Core/Model/PartResolver.cs`（Read全文）
- `src/Ecad2.Core/Model/ElementCatalog.cs`（Read全文）
- `src/Ecad2.Core/Model/Device.cs`（DeviceClass enum定義）
- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs`（Read全文）
- `src/Ecad2.Core/Persistence/JsonOptions.cs`（PartRole直列化方式）
- `src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs`（Read全文）
- `src/Ecad2.App/Converters/PartEntryToGlyphGeometryConverter.cs`（Read全文）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1440-1510`（`PlaceElementAtSelectedCell`・
  `MapToDeviceClass`・`ResolveDeviceClass`）
- `src/Ecad2.App/MainWindow.xaml:605-644`（配置バーComboBox・選択中部品サムネイル、追記時調査）
- `src/Ecad2.App/ViewModels/PartSelectionEntryViewModel.cs`（Thumbnail生成元、追記時調査）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Palette.cs:269-309`
  （`LoadToolIconsAsync`、追記時調査）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.Core/Rendering/SvgRenderer.cs`（Read全文、追記時調査）

## 不明点

なし（本調査の範囲内では未解決の疑問点なし）。

## 派生提案の有無

なし（家老采配の範囲内で完結）。
