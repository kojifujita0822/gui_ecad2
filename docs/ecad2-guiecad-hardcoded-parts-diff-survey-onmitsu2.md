# GuiEcadハードコード部品とecad2固定部品リストの差分調査

調査者: 隠密2　最終更新: 2026-07-11

殿観察（押釦・タイマー接点がecad2の部品リストに入っていない）を受けた家老采配。GuiEcad・ecad2
両実ソースを実物照合（Grep回避、Read直読中心）。実装・書き込みは行っていない。

**先に結論**：ecad2のCore層（`ElementKind` enum・描画シンボル・シミュレーション分類・機器種別
マッピング）には、GuiEcadと**全く同じ21種類**が既にすべて実装済み（T-007移植のまま1文字も
変わっていない）。欠けているのは「配置パレットへ実際に部品を並べる登録」の部分のみ。しかも
これはecad2固有の欠落ではなく、**GuiEcad自体が最初から「登録済み5種」と「専用ツールボタン
14種＋4種」という2つの別経路を使い分けていた**ことが、ecad2側では片方（専用ツールボタン経路）
がまるごと未着手なまま、という構図。

---

## DoD(1): GuiEcadのハードコード部品定義 全量列挙

GuiEcadの組込み部品は**2つの独立した経路**で提供されている。

### 経路A: `BasicPartTemplates.All()`（自作パーツと同列、部品選択パレット経由）

`C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.Core/Persistence/BasicPartTemplates.cs`
（全文Read済み）。**5種類のみ**：

| 部品名 | ElementKind/Role | Id | 備考 |
|---|---|---|---|
| a接点 | `PartRole.ContactNO` | `basic-contact-no` | |
| b接点 | `PartRole.ContactNC` | `basic-contact-nc` | |
| コイル | `PartRole.Coil` | `basic-coil` | |
| 端子台 | `PartRole.Terminal` | `basic-terminal` | |
| セレクトSW | `PartRole.ContactNO`（電気的にa接点と同一） | `basic-select-switch` | |

`PartFolderStore`が初回起動時に「図形/」フォルダへ展開、以後は自作パーツと同列にユーザーが
呼び出す（`MainPage.Parts.cs`、自作パーツ管理の一部として扱われる）。

### 経路B: 縦ツールパレットの専用配置ボタン（`MainPage.xaml:299-441`、全文Read済み）

`ToolStackPanel`にRadioButton/DropDownButtonとして常設。`OnToolSelected`
（`MainPage.Tools.cs:81-90`）がTagから`Enum.TryParse<ElementKind>`で直接種別を解決する
（テンプレートを介さない汎用パーサ、`ParseSymbolTag`, `MainPage.Tools.cs:63-72`）。

| # | ボタン表示 | Tag(=ElementKind) | 区分 | path:line |
|---|---|---|---|---|
| 1 | 選択 | `select` | ツール | `MainPage.xaml:301-313` |
| 2 | a接点 | `ContactNO` | 常設 | `MainPage.xaml:316-321` |
| 3 | b接点 | `ContactNC` | 常設 | `MainPage.xaml:322-327` |
| 4 | **押釦NO** | `PushButtonNO` | 常設 | `MainPage.xaml:328-333` |
| 5 | **押釦NC** | `PushButtonNC` | 常設 | `MainPage.xaml:334-339` |
| 6 | **タイマNO**（接点） | `TimerContactNO` | 常設 | `MainPage.xaml:340-345` |
| 7 | **タイマNC**（接点） | `TimerContactNC` | 常設 | `MainPage.xaml:346-351` |
| 8 | **瞬時NO** | `TimerInstantContactNO` | 常設 | `MainPage.xaml:352-357` |
| 9 | **瞬時NC** | `TimerInstantContactNC` | 常設 | `MainPage.xaml:358-363` |
| 10 | コイル | `Coil` | 常設 | `MainPage.xaml:366-371` |
| 11 | **表示灯**（ランプ） | `Lamp` | 常設 | `MainPage.xaml:372-377` |
| 12 | 端子 | `Terminal` | 常設 | `MainPage.xaml:378-383` |
| 13 | 分岐（縦コネクタ） | `connector` | ツール | `MainPage.xaml:386-392` |
| 14 | 分断（配線分断） | `wirebreak` | ツール | `MainPage.xaml:393-399` |
| 15 | 枠 | `frame` | ツール | `MainPage.xaml:400-406` |
| 16 | 直線 | `line` | ツール | `MainPage.xaml:407-413` |
| 17 | 点 | `dot` | ツール | `MainPage.xaml:414-420` |
| 18 | 画像挿入 | （別ボタン、`OnInsertImageClick`） | ツール | `MainPage.xaml:421-427` |
| 19 | その他▼→**セレクトSW** | `SelectSwitch` | ドロップダウン | `MainPage.xaml:434` |
| 20 | その他▼→**サーマル(OL)** | `ThermalOverload` | ドロップダウン | `MainPage.xaml:435` |
| 21 | その他▼→**非常停止** | `EmergencyStop` | ドロップダウン | `MainPage.xaml:436` |
| 22 | その他▼→**三相モータ** | `Motor` | ドロップダウン | `MainPage.xaml:437` |

`OnOtherPartSelected`（`MainPage.Tools.cs:93-113`）が「その他▼」の実処理、同じく
`ToolFromTag`/`ParseSymbolTag`を経由。

### 経路にも登録されていない種別（GuiEcadでも配置UIが確認できなかったもの）

`ElementKind`定義（`GuiEcad.Core/Model/Element.cs:17-30`、全文Read済み）は21種あるが、
上記A・Bいずれにも該当しないのは以下：

- **Timer**（タイマコイル本体）
- **Counter**（カウンタコイル）
- **Breaker3P**（配線用遮断器、主回路3極）
- **ContactorMain3P**（電磁接触器主接点、主回路3極）
- **ThermalOverload3P**（サーマルリレー、主回路3極）

`Enum.TryParse<ElementKind>`は任意の種別名を受け付ける汎用実装だが、XAML側にこれらのタグを
持つボタン・メニュー項目が一切見当たらない。**GuiEcad自体、この5種を配置する専用UIを
用意していなかった可能性が高い**（コイルボタンで配置後、機器名を手動でTIM～等にリネームし
Setpoint等のパラメータを直接編集する運用だった可能性——`Element.cs`のコメント「タイマ接点・OL
は初期は手動入力（docs/simulation.md）」がこれを示唆するが断定はできない。主回路3極記号は
「主回路（動力回路）」専用シートで別のツールセットが出る設計の可能性もあるが、本調査では
GuiEcad側の主回路シート専用UIまでは確認していない＝**不明**）。

---

## DoD(2): ecad2側の現状

### `ElementKind` enum（`src/Ecad2.Core/Model/Element.cs:17-30`、全文Read済み）

GuiEcad側と**完全に一致**（21種、1文字違わず）：`ContactNO, ContactNC, Coil, Lamp,
PushButtonNO, PushButtonNC, SelectSwitch, Terminal, Timer, Counter, TimerContactNO,
TimerContactNC, EmergencyStop, ThermalOverload, TimerInstantContactNO, TimerInstantContactNC,
Motor, Breaker3P, ContactorMain3P, ThermalOverload3P`

### `BasicPartTemplates.All()`（`src/Ecad2.Core/Persistence/BasicPartTemplates.cs`、全文Read済み）

GuiEcad側と**完全に一致**（5種のみ：a接点/b接点/コイル/端子台/セレクトSW）。

### ツールバー配置ボタン（`src/Ecad2.App/MainWindow.xaml`、前回棚卸し調査で全量確認済み）

2段目ツールバーの専用ボタンは12項目のみ：選択ツール(Esc)・a接点配置(F5)・OR a接点配置(Shift+F5)・
b接点配置(F6)・OR b接点配置(Shift+F6)・コイル配置(F7)・端子台配置(F8)・自由線(横/縦)記入(F9/Shift+F9)・
縦分岐線記入(Shift+F9)・接続点記入(F10)・配線分断記入(F10)・自作パーツ。

**GuiEcadの経路B（専用ツールボタン14種＋その他4種）に相当するものが、押しボタン・タイマ接点・
瞬時接点・ランプ・サーマル・非常停止・三相モータについて、ecad2には一切存在しない。**

`BuiltinPlaceButton_Click`（`MainWindow.xaml.cs:1167-1178`）→`ActivateBuiltinTool`
（`:1152-1159`）は`_viewModel.PartPalette.Entries`から`Category=="" && Definition.Name==partName`
で検索する実装——**`BasicPartTemplates.All()`に登録されている部品名なら、既存の仕組みで
そのままツールバーボタン化できる**（GuiEcadの`Enum.TryParse`ほど汎用ではないが、名前ベースの
検索という点で同種の仕組み）。

---

## DoD(3): 差分一覧表

| # | 部品 | GuiEcadでの配置経路 | ecad2状態 | 
|---|---|---|---|
| 1 | 押釦NO (PushButtonNO) | 経路B（縦パレット常設） | **無し** |
| 2 | 押釦NC (PushButtonNC) | 経路B（縦パレット常設） | **無し** |
| 3 | タイマNO/接点 (TimerContactNO) | 経路B（縦パレット常設） | **無し** |
| 4 | タイマNC/接点 (TimerContactNC) | 経路B（縦パレット常設） | **無し** |
| 5 | タイマ瞬時NO (TimerInstantContactNO) | 経路B（縦パレット常設） | **無し** |
| 6 | タイマ瞬時NC (TimerInstantContactNC) | 経路B（縦パレット常設） | **無し** |
| 7 | 表示灯/ランプ (Lamp) | 経路B（縦パレット常設） | **無し** |
| 8 | セレクトSW (SelectSwitch) | 経路B（その他▼）＋経路A | **部分あり**（`BasicPartTemplates`に登録済み、自作パーツパレット経由で到達可能。ただし専用ツールボタンは無い） |
| 9 | サーマル(OL) (ThermalOverload) | 経路B（その他▼） | **無し** |
| 10 | 非常停止 (EmergencyStop) | 経路B（その他▼） | **無し** |
| 11 | 三相モータ (Motor) | 経路B（その他▼） | **無し** |
| 12 | タイマコイル本体 (Timer) | **GuiEcadにも配置UI無し（不明）** | 無し（GuiEcad側も同様の欠落の可能性） |
| 13 | カウンタ (Counter) | **GuiEcadにも配置UI無し（不明）** | 無し（同上） |
| 14 | 遮断器/接触器/サーマル3極 (Breaker3P等) | **GuiEcadにも配置UI無し（不明、主回路シート専用の可能性）** | 無し（同上） |

**殿観察の「押釦・タイマー接点」はまさに#1〜4に該当し、本調査で完全に裏付けが取れた。**

## DoD(4): Core層の器の有無（追加コスト判定材料）

### 描画シンボル：`src/Ecad2.Core/Rendering/SymbolGlyphs.cs`（全文Read済み）

`Draw`メソッドのswitch文（:19-53）で、**21種中20種**に専用の描画ロジックが実装済み
（`PushButtonNO`/`PushButtonNC`/`EmergencyStop`/`TimerContactNO`/`TimerContactNC`/
`TimerInstantContactNO`/`NC`（通常接点と同形）/`ThermalOverload`（暫定形状、専用DXF未提供と
コメントあり）/`Coil`/`Timer`（`TimerCoil`）/`Lamp`/`Terminal`/`Motor`/`Breaker3P`/
`ContactorMain3P`/`ThermalOverload3P`まで網羅）。**唯一`Counter`のみswitch文に無く、
default節（矩形＋リード線の汎用シンボル）に落ちる**。

### シミュレーション分類：`src/Ecad2.Core/Model/ElementCatalog.cs`（全文Read済み）

`IsContact`：PushButtonNO/NC・TimerContactNO/NC・TimerInstantContactNO/NC・EmergencyStop・
ThermalOverload まで対応済み。`IsLoad`：Coil/Lamp/Timer/Counter まで対応済み。
`IsInputControlled`：PushButtonNO/NC・SelectSwitch・EmergencyStop・ThermalOverload まで対応済み。
→ **押釦・タイマ接点・非常停止は全てシミュレーション分類済み**。

### 機器種別(DeviceClass)マッピング：`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1450-1463`
（`MapToDeviceClass`、T-045増分B対応、全文確認済み）

PushButtonNO/NC/EmergencyStop→`PushButton`、Timer/TimerContactNO/NC/TimerInstantContactNO/NC→
`Timer`、Lamp→`Lamp`、Counter→`Counter`、ContactorMain3P→`Relay`まで**全て対応済み**（機器表の
「種別」列に正しい日本語ラベルで出る、`DeviceClassToTextConverter`と組み合わせ）。

### 自作パーツRole変換：`src/Ecad2.Core/Model/PartResolver.cs:43-59`（`ComponentKind`）

自作パーツ経由（`PartRole`）ではPushButtonNO/NCまで対応済みだが、Lamp/Timer/Counter等の
`PartRole`は無い（`ContactNO/ContactNC/Coil/Lamp/Terminal/InputNO/InputNC`のみ）。ただし
**`BasicPartTemplates`経由の組込みテンプレートは`ElementInstance.Kind`を直接持つため、この
`PartRole`変換を経由しない**（`PartId`未設定の通常配置では`ComponentKind`は`e.Kind`をそのまま
返す、`PartResolver.cs:43-46`）。→ **本経路の制約はテンプレート追加の障害にならない**。

### 結論：Core層の器は押釦・ランプ・タイマ接点・非常停止・サーマルについて**完全に揃っている**

追加で必要な作業は「配置UIへの登録」のみ。Counter単体のみ描画シンボルが暫定（default矩形）だが、
配置UI自体を新設する優先度が低い（GuiEcadでも配置経路自体が確認できなかった）ため実害は小さい。

## DoD(5): 規模見積と所感

| 対応方針 | 規模 | 根拠 |
|---|---|---|
| `BasicPartTemplates.All()`へPartDefinition追加（自作パーツパレット経由での到達） | **小** | Core層完備。既存5種のPrimitives定義パターン（`SymbolGlyphs`の座標に+0.5オフセット、コメントに明記の変換規則）をなぞるだけで機械的に追加可能。1種あたり数行〜十数行 |
| ツールバーへ専用配置ボタン追加（GuiEcad同等の体験） | **小〜中** | `BuiltinPlaceButton_Click`/`ActivateBuiltinTool`の既存パターン（名前ベース検索）をそのまま複製すればよい。XAML側にButtonをTag付きで追加するだけ、コードビハインドの新規分岐は基本的に不要（上記PartDefinition追加とセットなら） |
| Timer/Counter/主回路3極のUI新設 | **中〜大（優先度低）** | GuiEcadでも配置手段が未確認、参照実装が無いためゼロから設計要。かつ主回路3極記号は非シミュレート・自由配線前提でセル配置とは異なる特殊な扱い（`ElementCatalog.Ports`参照） |

**総合所見**：殿観察の「押釦・タイマー接点」は、両方とも上記表の1〜2番目（規模「小」〜「小〜中」）
で解決できる見込みが高い。Core層（モデル・描画・シミュレーション・機器種別分類）が既に完備して
いるため、**GuiEcadのPartEditorWindowのような大規模実装は不要**——BasicPartTemplatesへの
PartDefinition追加＋（必要なら）ツールバーボタン追加、という比較的軽い作業で殿の要望に応えられる
と考えられる。どの部品をどちらの経路（自作パーツパレット経由 or 専用ツールボタン）で提供するかは
UI/UX判断であり、着手時に殿確認が必要な論点として申し送る。

## 出典一覧

- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs`（Read、全文）
- `src/Ecad2.Core/Model/Element.cs`（Read、全文）
- `src/Ecad2.Core/Model/Sheet.cs`（Read、全文）
- `src/Ecad2.Core/Rendering/SymbolGlyphs.cs`（Read、全文）
- `src/Ecad2.Core/Model/ElementCatalog.cs`（Read、全文）
- `src/Ecad2.Core/Model/PartResolver.cs`（Read、全文）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1450-1483`（`MapToDeviceClass`/`ResolveDeviceClass`）
- `src/Ecad2.App/MainWindow.xaml.cs:1147-1178`（`ActivateBuiltinTool`/`BuiltinPlaceButton_Click`）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.Core/Persistence/BasicPartTemplates.cs`（Read、全文）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.Core/Model/Element.cs`（Read、全文）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.xaml:299-441`（Read、全文）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Tools.cs:1-120`（Read）
- `docs/ecad2-guiecad-unwired-features-survey-onmitsu2.md`（本セッション前回調査、参照）

## 不明点

- GuiEcadでTimer（コイル本体）・Counter・Breaker3P/ContactorMain3P/ThermalOverload3Pをどう配置
  していたか（そもそも配置UIが無かったのか、主回路シート専用の別UIがあったのか）は本調査では
  確認できず。必要であれば追加調査可能。
- `MainCircuit`（主回路）シート専用のツールセットがGuiEcadに存在するかは未確認。

## 派生提案の有無

なし（家老采配の範囲内で完結）。
