# T-070 検索・置換機能：着手前設計整理

調査者: 隠密2　最終更新: 2026-07-11

家老采配（殿しばし席を外されるにつき家老裁量で沙汰）。GuiEcad実物の検索・置換操作体系を調査し、
既存DRCジャンプ機構の流用可否、ecad2 App層に必要な新規実装項目、UI/UX分岐論点を整理する。
**実装は行っていない、着手前調査のみ**。

---

## 最重要発見：Core層に検索・置換ロジックが既に完備（App層結線待ちのT-064/T-071と同型パターン）

`src/Ecad2.Core/Simulation/DeviceRenamer.cs`（GuiEcadから移植済み、Read全文）：

- **`Rename(doc, from, to)`**：全シート横断でDeviceNameが`from`に完全一致（大小無視）する要素を
  一括置換し、**`Document.Devices`（機器表）のキー移行まで面倒を見る**（GuiEcadの
  `RenameDeviceCommand`をループで1件ずつ実行する方式より一段進んだ一括処理）。
  **T-070の「全置換」はこれをそのまま呼ぶだけで完結する**。
- **`Find(doc, name)`**：全シート横断でDeviceName完全一致要素を列挙。**コメントに「検索バーの
  ハイライト用」と明記**——検索機能のために用意されていながら、App層のどこからもgrep 0件で
  呼ばれていない未使用メソッドと確認済み。T-064（ImageInsert）・T-071（部品テンプレート）と
  同じ「Core層完備・App層結線待ち」パターンがここでも再現している。

**この発見により、T-070は検索・置換の中核ロジック実装が不要——App層のUI・状態管理・結線のみで
完結する可能性が高く、規模見立て（todo.md記載「中〜大」）は再検討の余地がある。**

---

## DoD(1): GuiEcad実物の検索・置換操作体系

`GuiEcad.App/FindController.cs`（純ロジック、Read全文）・`MainPage.Find.cs`（UI結線、Read全文）・
`MainPage.Drc.cs:97-120`（検索結果パネル、Read）を実物照合。

| 項目 | GuiEcadの実装 |
|---|---|
| トリガー | Ctrl+F／メニュー「検索」→`ToggleFindBar()` |
| 検索対象 | 機器名（`ElementInstance.DeviceName`）の**完全一致**（大小無視）、全シート横断 |
| UI形式 | 作図エリア上部にオーバーレイ表示される横長バー（検索ボックス・前/次ボタン・
  件数ステータス・区切り線・置換後ボックス・置換/全置換ボタン・閉じるボタンを横並び） |
| 検索実行 | `TextChanged`で随時再検索、Enterで次の一致へ |
| 循環ジャンプ | `Next()`/`Prev()`が`(Index+1)%Count`方式で循環（先頭↔末尾を跨いで循環） |
| ジャンプ処理 | 一致要素のシートへ切替＋**`CenterViewOnElement`でビューをパンして中央表示** |
| 置換1件 | 現在ジャンプ中の1要素だけを`RenameDeviceCommand`でリネーム（**機器名としての
  一意性は保証しない設計**——あえて1要素だけ変える） |
| 全置換 | `FindController.Matches`で一致要素を全列挙し、1件ずつ`RenameDeviceCommand`実行 |
| 検索結果パネル | 下部出力パネルの別タブ（**DRCタブと同居**、`SearchResultPanelView`）。
  リスト項目選択で`OnSearchResultItemSelected`→ジャンプ（DRCの`OnDrcItemSelected`と同型構造） |

**GuiEcad自体が検索結果とDRC結果を同一の下部出力パネル構造（タブ切替リスト＋選択でジャンプ）で
実装している**——今回のDoD(2)（ecad2のDRCジャンプ機構の流用可否）への強い示唆になる。

---

## DoD(2): 既存DRCジャンプ機構（`OutputPanelViewModel`）の流用可否

**流用できる、というよりむしろ同じ設計パターンを踏襲するのが自然**（GuiEcad自体が両者を同居させて
いる設計と符合する）。

`src/Ecad2.App/ViewModels/OutputPanelViewModel.cs`（Read全文）の`JumpTo`は、GuiEcadの
`CenterViewOnRow`/`CenterViewOnElement`（**ビューをパンして中央表示**）とは異なり、**「該当セルを
`_owner.SelectedCell`へ選択移動する」という、ecad2で既に確立された軽量パターン**を使っている。
検索結果のジャンプもこのパターンを踏襲すれば、GuiEcadのビューパン処理を新規移植する必要が無い。

`Diagnostics`（`ObservableCollection<Diagnostic>`）＋`SelectedDiagnostic`というプロパティ設計も、
検索結果用に同型で新設できる（型は`Diagnostic`ではなく検索結果専用の型が必要——DRCと検索は
別概念のため無理な転用は避けるべき、DoD3参照）。

ただし以下はDRCジャンプに無い検索固有の要素で、新規追加が必要：
- 循環Next/Prev（DRCリストはクリック選択のみで「次/前」ボタンによる循環移動が無い）
- 件数ステータス表示（「N / M」形式）
- 置換ボックスとの連動

---

## DoD(3): 必要な新規実装項目

1. **検索状態管理**：現在位置Index・循環Next/Prev。ロジック自体は`DeviceRenamer.Find`が既存の
   ため、App層側は薄いラッパ（`FindController`相当、ecad2のUndo非依存の設計に合わせ
   コマンドクラス化は不要）で足りる
2. **検索トリガーUI**：Ctrl+F等のショートカット・メニュー項目（UI/UX論点1）
3. **検索・置換UI本体**：配置場所（UI/UX論点1）
4. **ジャンプ実装**：`OutputPanelViewModel.JumpTo`パターンを踏襲（DoD2参照、新規開発コストは低い）
5. **置換1件**：対象1要素のみのDeviceName変更。`DeviceRenamer.Rename`を使うと一致する全要素を
   巻き込んでしまうため、**「1件だけ置換」を実装するなら`DeviceRenamer`を経由しない単発変更が
   必要**（機器表整合処理は`SelectedElementDeviceNameセッター`と同型のケアが要る）。この挙動自体を
   採用するかはUI/UX論点3
6. **全置換**：`DeviceRenamer.Rename`をそのまま呼ぶだけで完結（新規ロジック不要）
7. **検索結果パネル**：下部出力パネルへの新規タブ（`OutputPanelViewModel`のDRCタブと同居させるか
   別ViewModelにするかは実装設計、UI/UX論点ではない）
8. **Undo対応**：新規コマンドクラス不要——T-064/T-067と同じ結論、既存`UndoManager.RecordSnapshot`
   パターンを置換操作の直前に呼ぶだけで足りる

---

## DoD(4): UI/UX分岐となりうる論点（選択肢化、殿確認要）

### 論点1: UI形式・配置場所

| 案 | 内容 |
|---|---|
| A | GuiEcad同様、作図エリア上部にオーバーレイバー（Ctrl+Fでトグル表示） |
| B | 下部出力パネルの新規タブのみ（検索ボックスもパネル内、上部バーは作らない） |
| C | 独立ダイアログ（GX Works3等、一般的な検索ダイアログスタイル） |
| D | 上部バー＋下部結果パネルの両方（GuiEcad完全踏襲） |

### 論点2: 検索対象範囲

| 案 | 内容 |
|---|---|
| A | GuiEcad同様、機器名（DeviceName）の完全一致のみ |
| B | 部分一致も追加 |
| C | 機器名以外（コメント・ラベル等）も対象に含める——**ただしT-069調査でコメント編集UI自体が
    ecad2に皆無と判明済みのため、対象を広げるとT-069の別課題を前提に持ち込むことになる点に
    注意**（現時点では非推奨だが選択肢として明記） |

### 論点3: 置換1件の挙動（機器名の整合性論点、DoD3の5参照）

| 案 | 内容 |
|---|---|
| A | GuiEcad同様、現在ジャンプ中の1要素だけをリネーム（機器名としての一意性は保証しない） |
| B | 「1件」ボタンを廃し、常に該当機器名の全要素をまとめて置換（`DeviceRenamer.Rename`のみ使用、
    機器名の整合性を常に保つ設計——UXとしては「置換」ボタンが実質「全置換」と同じ意味になる） |

---

## 出典一覧

- `docs/todo.md`（T-070、優先度表のみ・専用節は未起票と確認）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/FindController.cs`（Read全文）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Find.cs`（Read全文）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Drc.cs:1-120`（検索結果パネル・
  DRCパネル、Read）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.xaml:478-514`（FindBar XAML、Read）
- `src/Ecad2.Core/Simulation/DeviceRenamer.cs`（Read全文）
- `src/Ecad2.App/ViewModels/OutputPanelViewModel.cs`（Read全文）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1218-1254`（`SelectedElementDeviceName`、
  機器表整合処理の参考実装、Read）
- `docs/ecad2-t064-image-insert-design-onmitsu2.md`・`docs/ecad2-t067-groupframe-design-onmitsu2.md`
  （本セッション前回調査、Undo基盤の結論を再利用）

## 不明点

- GuiEcadの`OnFindBoxTextChanged`（`TextChanged`ハンドラ本体）は本調査では未読——随時検索の
  デバウンス有無等の細部は確認していない。必要なら追加調査可能（規模判断に影響しない些細事項と判断）。

## 派生提案の有無

なし（家老采配の範囲内で完結）。
