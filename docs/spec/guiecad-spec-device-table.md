# GuiEcad仕様書：機器表・BOM

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）の機器表・BOM実装をExplore委譲調査で纏め、
`docs/spec/ecad2-spec-device-table.md`（ecad2側、T-075起票）と比較可能な形で整理する。

対応するecad2側仕様書：`docs/spec/ecad2-spec-device-table.md`

**【重要・気づき】** 本調査で参照した`ecad2-spec-device-table.md`は「T-066（BOM編集）＝Approved・
未着手」と記載するが、`src/Ecad2.App`の現物コード（`MainWindow.xaml.cs:193-204`、
`MainWindow.xaml:465`）およびgit log（`da55395`等）は**型式列編集機能が実装・クローズ済み**である
ことを示しており齟齬がある。本調査ではこの齟齬をそのまま比較材料として扱い、事実（現物コード）を
優先して4節・8節に反映した。ecad2側仕様書自体の改変は本タスクのスコープ外のため行っていない。
別途家老へ報告する。

---

## 1. Deviceの生成・登録・削除

### 生成：BOMエディタOK確定時のみ（要素配置時の即時登録なし）

`Device.cs`（`Name`/`Class`/`Model`/`Maker`/`Quantity`）はecad2と完全同一構造。しかし
`_document.Devices.ByName`への書き込みはリポジトリ全体で**BOMエディタのOK確定処理1箇所のみ**
（`MainPage.Dialogs.cs:339-343`）——**要素配置時にDeviceを自動登録する経路はGuiEcadに存在しない**
（ecad2仕様書1節の`PlaceElementAtSelectedCell`相当処理は見つからず）。

既存Deviceが見つかった場合は`Class`は更新されず、`Model`/`Maker`/`Quantity`のみ上書き。

### 削除：削除ロジックが存在しない（コード解析上の帰結）

`Devices.ByName.Remove`の呼び出しは死コード化した`DeviceRenamer.cs:40`の1箇所のみ。**一度BOM
エディタでOKされ登録された機器は、その後参照要素を全削除しても`Devices.ByName`から消えない**——
ecad2仕様書2節の`RemoveDeviceIfUnreferenced`（参照カウント方式の自動削除）に相当する仕組みは
GuiEcadに存在しない。

### 画面表示は`Devices.ByName`を経由しない別ロジック

常設パネル（3節）・BOMエディタの機器一覧はいずれも毎回シート全要素を走査して動的に組み立て、
`_document.Devices.ByName`は参照しない——「画面上の機器名一覧」（要素走査、常に最新）と
「`Devices.ByName`の永続化データ」（BOMエディタOK時のみ更新）が別物という構造。

---

## 2. デバイス名改名

### `DeviceRenamer`（Core層）は死コード

`DeviceRenamer.Rename`（`GuiEcad.Core/Simulation/DeviceRenamer.cs:18-46`）はecad2の同名クラスと
ほぼ同一実装（大文字小文字無視の全シート一括置換＋`ByName`キー移行）だが、**GuiEcad側では呼び出し
元がsrc全体に0件**——ecad2はセッターから呼ぶが、GuiEcadの実際の改名経路は別の仕組みに置換済み。

### 実際の改名は`RenameDeviceCommand`（単一要素単位、Undo対応）

`ElementCommands.cs:194-212`：`Execute()`は`_element.DeviceName=_to`のみ、**`Devices.ByName`には
一切触れない**。呼び出し元3箇所：プロパティパネル機器名欄コミット、検索・置換バーの単一置換、
全置換。

### 一括改名は「検索・置換」機能として存在（GuiEcad独自）

`OnReplaceAll`（`MainPage.Find.cs:103-112`）：`FindController.Matches`で全シート横断の完全一致
要素を収集し、各要素へ`RenameDeviceCommand`を個別に`_history.Execute`——**ドキュメント全体を横断
する機器名一括改名機能が存在する**（ecad2仕様書に同種機能の言及なし）。

ただし`RenameDeviceCommand`は`Devices.ByName`のキーを移行しないため、**（コード解析上の帰結、
実機未確認）**全置換後も旧名のDeviceエントリが孤立して残り続け、BOM PDFに古い機器名のまま
載り続ける可能性がある。

---

## 3. 機器表(BOM)UI（常設パネルとダイアログの2系統）

### 3-1. 常設パネル「機器表」タブ（`DeviceListView`）

右ドックパネルの`TabView`内（`MainPage.xaml:761-794`）。`RefreshDevicePanel`
（`MainPage.Properties.cs:81-112`）が全シート・全要素を走査し機器名初出のみ採用、Name昇順・
大小無視ソート（ecad2の`DeviceTableViewModel.BuildList()`と同一ソートキー）。

**列構成は単一文字列**（`"[種別]  機器名"`を`Content`に設定するのみ）——ecad2のDataGrid3列
（機器名/種別/型式）のような列分割はなく、Model/Maker/Quantity表示も一切ない。**編集不可**、
選択時は対象要素へジャンプするのみ。種別ラベルは`PartResolver.ComponentKind`をその場で解決する
14種の`DeviceKindLabel`——`Devices.ByName`の`DeviceClass`は参照しない（常に最新反映）。

### 3-2. 「部品リスト(BOM)...」ダイアログ（`OnBomEditor`）

`MainPage.Dialogs.cs:250-353`。機器名/種別/型式/メーカー/数量の**5列**。機器名・種別は表示のみ、
**型式・メーカー・数量の3列が編集可能**。OK確定時に全行を`Devices.ByName`へ書き戻し、未登録名は
`new Device`生成。変更があれば`MarkDirty()`のみ——**`_history.Execute`を呼ばずUndo/Redo対象外**。

---

## 4. BOM表のPDF出力（GuiEcadは到達可能、ecad2は本来到達不能設計）

`DiagramRenderer.RenderBomPage`（`DiagramRenderer.cs:751-796`）が機器名/種別/型式/メーカー/数量の
5列を描画。**PDFプレビュー・実出力の両経路とも、メニュー/ツールバーの`Click`属性が結線済み**で、
`Devices.ByName.Count>0`のとき実際にBOMページが生成される（`PdfPreviewDialog.xaml.cs:55-98,222-225`、
`MainPage.Menu.cs:288-293`）。

**結論**：GuiEcadのBOM表PDF出力はコード上到達可能かつ結線済みであり、ecad2仕様書0節が明記する
「App層からの呼び出し元がなく到達不能」という状態はGuiEcadには当てはまらない。ただし
`Devices.ByName`が空（BOMエディタで一度もOKしていない）場合はBOMページ自体が生成されない点に注意。

---

## 5. デバイス種別決定ロジック

`MapDeviceClass(ElementKind kind)`（`MainPage.Dialogs.cs:355-367`）：`Coil`/`Timer`/`ContactNO`/
`ContactNC`→Relay、`PushButtonNO`/`NC`/`EmergencyStop`→PushButton、`SelectSwitch`→SelectSwitch、
`Lamp`→Lamp、`TimerContact*`→Timer、`Terminal`→Terminal、他→Other。**既存Deviceの場合`Class`は
再計算されない**（ecad2仕様書1節「登録時に一度だけ決定」と同じ設計思想）。

ecad2仕様書1節の`ResolveDeviceClass`にある「`PartPalette.Entries`ベースの`SelectSwitch`固定判定
分岐（`Category==""`かつ`Role=ContactNO`かつ`IsOrEligible=false`）」に相当する**特殊分岐はGuiEcad
側`PartResolver.cs`全文確認の結果、存在しない**。

**種別ラベルの二重管理（GuiEcad固有）**：画面用（`DeviceKindLabel`、ElementKindベース14種、常に
最新反映）とPDF用（`DeviceClassLabel`、DeviceClassベース8種、BOMエディタOK時点のスナップショット）
が**分離した別実装**。ecad2仕様書が言う「画面・PDFで同一表記に統一する設計」（T-053裁定）とは
異なり、GuiEcadは統一されていない。

---

## 6. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | 出典 |
|---|---|
| 機器名の一括置換（検索・置換バー「全置換」、Undo可能） | `MainPage.Find.cs:103-112` |
| BOM表PDF出力の実配線（プレビュー・実出力とも結線済み） | `MainPage.Menu.cs:217-294`、`PdfPreviewDialog.xaml.cs:55-98` |
| BOM編集ダイアログでModel/Maker/Quantity全編集 | `MainPage.Dialogs.cs:250-353` |
| 常設の機器表パネル（開閉トグル・要素ジャンプ付き） | `MainPage.xaml:761-794`、`MainPage.Properties.cs:30-112` |

### (2) ecad2のみにある機能

| 機能 | ecad2側出典 |
|---|---|
| 配置時のDevice即時登録（非上書きポリシー） | 仕様書1節 |
| 参照カウント方式のDevice自動削除 | 仕様書2節 |
| `PartPalette`ベースの`SelectSwitch`固定判定分岐 | 仕様書1節 |
| 画面・PDFで種別ラベル表記を統一する設計 | 仕様書0節、T-053裁定 |

### (3) 両方にあるが挙動が異なる点

| 項目 | GuiEcad | ecad2 |
|---|---|---|
| Deviceの生成タイミング | BOMエディタOK確定時のみ | 要素配置時・プロパティ編集時に即時登録 |
| Deviceの削除 | 削除ロジックなし、残存し続ける | 参照0件で自動削除 |
| 機器名改名の反映範囲 | 要素の`DeviceName`のみ、`Devices.ByName`キー移行なし | 要素と`Devices.ByName`キーの両方を移行 |
| 機器一覧の常設画面表示 | 単一文字列ListView、要素走査から都度構築 | 3列DataGrid、ViewModelスナップショット |
| BOM列の編集可否 | 型式・メーカー・数量の3列編集可 | 型式(Model)のみ編集可（**現物コードで確認、仕様書記載と齟齬あり**） |
| BOM表PDF出力の到達可能性 | 到達可能（結線済み） | 到達不能設計（仕様書0節） |
| 種別ラベルの画面/PDF統一性 | 画面用/PDF用が別実装・別粒度 | 統一設計（T-053裁定） |
| BOM編集内容のUndo対象性 | `MarkDirty()`のみ、対象外 | 同左（`MainWindow.xaml.cs:203`も`MarkDirty()`のみ、一致点） |
| ソートキー | Name昇順・大小無視 | 同左（一致点） |
| Deviceモデル構造 | `Name`/`Class`/`Model`/`Maker`/`Quantity` | 同左（完全一致、一致点） |

---

## 出典

- GuiEcad: `GuiEcad.Core/Model/Device.cs:6-16`、`GuiEcad.Core/Simulation/DeviceRenamer.cs:18-46`、
  `GuiEcad.Core/Model/PartResolver.cs:14,29-53`、`GuiEcad.Core/Rendering/DiagramRenderer.cs:751-796,809-820`、
  `MainPage.Dialogs.cs:250-367`、`MainPage.Properties.cs:30-112,446-524`、`MainPage.Find.cs:92-112`、
  `MainPage.Commands/ElementCommands.cs:194-212`、`MainPage.Menu.cs:217-294`、
  `PdfPreviewDialog.xaml.cs:55-98,222-225`、`MainPage.xaml:130-131,147,234,241,761-794`
  （Explore委譲調査、行番号は本文各所参照）
- ecad2: `docs/spec/ecad2-spec-device-table.md`（比較対象、4節冒頭の齟齬注記も参照）、
  `src/Ecad2.App/MainWindow.xaml.cs:193-204`、`MainWindow.xaml:465`（現物コード確認）

## 不明点

- 全置換による機器名変更後、旧Deviceエントリが孤立しBOM PDFに残り続ける挙動は論理的帰結であり
  実機未確認。
- `Devices.ByName`の孤立エントリを画面から削除する手段が本当に皆無か、非grep対象経路（リフレクション
  等）が無い保証はできない。
- ecad2の`SelectSwitch`固定判定に相当する仕組みがGuiEcadの`PartLibrary`データ自体（テンプレートJSON等）
  にあるかは未確認。
