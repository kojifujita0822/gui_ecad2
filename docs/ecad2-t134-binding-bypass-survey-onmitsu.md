# T-134 付帯調査：ViewModelを迂回してモデルを直接書き換えるBindingの洗い出し

隠密、2026-07-28。家老采配（隠密の具申を受けたもの）＝
「T-134の実装前に、計装の置き場所を誤らぬため`MarkDirty`すら経ぬ経路の全体像を出す」。
**調査のみ。`src/`への書き込みは行っておらぬ。**

**本調査は`docs/ecad2-t134-undo-coverage-survey-onmitsu.md`の3-2節で拙者が「未確認」と
区切った点を埋めるもの**にござる。

---

## 0. 結論——**同型は他に無い。機器表の型式列が唯一**

**編集可能なモデル直結Bindingは`MainWindow.xaml:1500`（機器表の「型式」列）ただ1つ。**
**すなわちT-134の計装は、ViewModel層に置けば型式列を除く全経路を捕まえられる。**

**拙者が「同型が他にあるかは未確認」と残した射程は、これで閉じ申した。**

---

## 1.【DoD 1】XAMLでモデルのプロパティへ直接束ねている箇所

### 1-1. 絞り込みの筋道

XAML全体で`Binding`は**296件**。うち編集経路になりうるものへ、次の順で絞った。

| 段 | 条件 | 残り |
|---|---|---|
| 1 | `Mode=TwoWay`の明示 | 8件 |
| 2 | `ItemsSource`の指定（＝行のDataContextが項目型に変わる箇所） | 5件 |
| 3 | `DataGrid`の列定義 | 3グリッド・14列 |
| 4 | コードビハインドの動的Binding（`SetBinding`／`new Binding(`） | **0件** |

**段1と段2を分けたのが肝にござる**——**`Mode=TwoWay`の明示だけを追っては足りぬ**。
`TextBox.Text`・`CheckBox.IsChecked`・`DataGridTextColumn.Binding`は**既定でTwoWay**ゆえ、
明示が無くとも書き戻る。

### 1-2. `Mode=TwoWay` 明示の8件——**すべてViewModel経由。モデル直結は無し**

| 箇所 | バインド先 | 判定 |
|---|---|---|
| `App.xaml:458, 476` | （テーマのControlTemplate内） | UI状態 |
| `MainWindow.xaml:319` | `IsSelected`（AvalonDockのTabItem） | UI状態 |
| `:926` `IsGridVisible`／`:929` `IsDarkMode`／`:965` `IsTestMode` | ViewModelプロパティ | **文書を変えぬ表示設定** |
| `:1579` `SelectedElementSetpointSliderValue` | ViewModelプロパティ | **計装済み**（setter内で`RecordSnapshot`） |
| `:1600` `SelectedImageIsTracingOnly` | ViewModelプロパティ | **計装済み** |

### 1-3.【本命】`ItemsSource`の5件——行のDataContextが何になるか

| # | 箇所 | `ItemsSource` | 項目の型 | 編集可否 |
|---|---|---|---|---|
| 1 | `:1374` `SheetNavList` | `SheetNavigation.Sheets` | `SheetListItem`（**VM側のラッパー**） | **表示のみ**（`DisplayMemberPath="Name"`。改名は`RenameCommand`経由） |
| 2 | **`:1493` `DeviceTableGrid`** | **`DeviceTable.Devices`** | **`Device`（モデル）** | **型式列のみ編集可** |
| 3 | `:1603` `PartSelectionList` | `PartPalette.SelectionEntries` | VM側の項目 | 表示のみ（`Image`／`TextBlock`） |
| 4 | `:1651` `FindResultsGrid` | `Find.Matches` | 検索結果 | **`IsReadOnly="True"`**（`:1653`） |
| 5 | `:1666` `OutputGrid` | `OutputPanel.Diagnostics` | 診断結果 | **`IsReadOnly="True"`**（`:1669`） |

**#2の裏取り**＝`DeviceTableViewModel.cs:13`＝`public IReadOnlyList<Device> Devices { get; private set; }`。
**`Device`はモデル（`Ecad2.Model`）そのもの**にござる。

### 1-4. 機器表の3列——**型式列だけが編集可能**

`MainWindow.xaml:1497-1501`——

| 列 | Binding | `IsReadOnly` |
|---|---|---|
| 機器名 | `{Binding Name}` | **True** |
| 種別 | `{Binding Class, Converter=...}` | **True** |
| **型式** | **`{Binding Model}`** | **指定なし＝編集可** |

**これが唯一の「ViewModelを経ずにモデルを書き換える」経路にござる。**

### 1-5. ダイアログ9件——**Bindingは全0件**

`src/Ecad2.App/Views/*.xaml`（AboutDialog／AddSheetDialog／DocumentInfoDialog／PartEditorDialog／
PartTextInputDialog／PdfPreviewDialog／RenameDialog／SheetSettingsDialog／UsageWindow）は
**いずれも`{Binding`が1件も無い**。**すべてコードビハインドで値を出し入れする方式**ゆえ、
**Binding経由でモデルを書き換える経路は存在せぬ。**

---

## 2.【DoD 2】型式列は`MarkDirty`・`RecordSnapshot`のどちらを経るか

| | 有無 | 出典 |
|---|---|---|
| `MarkDirty` | **経る** | `MainWindow.xaml.cs:1208`（`DeviceTableGrid_CellEditEnding`内） |
| `RecordSnapshot` | **経らぬ** | View層に`RecordSnapshot`は**0件**（前調査で確認済み） |

**ゆえに型式列の編集は「未保存」とは記録されるが、Undoでは戻せぬ。**

### 2-1. 計装を足す位置——**この経路には救いがある**

`CellEditEnding`は**Bindingが確定する前に発火する**とコメントに明記
（`MainWindow.xaml.cs:1199-1201`＝「まだBindingが確定する前のタイミングで発火するため、
編集要素(TextBox)の新値と旧値(Device.Model)を比較し、実際に変化した場合のみMarkDirty()する」）。

**ゆえにこの位置で`RecordSnapshot`を積めば、正しく「変更前」の状態を積める。**
**既存の同値ガード（`:1207`）の直後が置き場所として自然**にござる。

**【隠密は設計を決めぬ】** 上記は「置ける」という事実の指摘にとどめる。採否は侍・家老の領分。

---

## 3.【DoD 3】型式列と同型は他に何件あるか——**0件**

**編集可能なモデル直結Bindingは型式列が唯一。** 根拠は1-2〜1-5の全件確認にござる。

**すなわちT-134の計装は、ViewModel層（`MarkDirty`を呼ぶ50箇所）に置けば足りる**——
**ただし型式列1件だけは、View層（`MainWindow.xaml.cs:1208`）に置かねば捕まらぬ。**

---

## 4.【DoD 4】再現手段

```powershell
$x = Get-ChildItem -Recurse -Path C:\ECAD2\src -Filter *.xaml

# (1) Binding 総数（=296）
($x | Select-String -Pattern 'Binding').Count

# (2) Mode=TwoWay の明示（=8件）
$x | Select-String -Pattern 'Mode=TwoWay' | Select-Object @{n='F';e={$_.Filename}}, LineNumber

# (3) ItemsSource の指定（=5件。行のDataContextが変わる箇所）
$x | Select-String -Pattern 'ItemsSource="\{' | Select-Object @{n='F';e={$_.Filename}}, LineNumber

# (4) コードビハインドの動的Binding（=0件）
Get-ChildItem -Recurse -Path C:\ECAD2\src -Filter *.cs | Select-String -Pattern 'SetBinding|new Binding\('

# (5) ダイアログのBinding（=各0件）
Get-ChildItem -Recurse -Path C:\ECAD2\src\Ecad2.App\Views -Filter *.xaml |
  ForEach-Object { @((Get-Content $_.FullName) | Select-String -Pattern '\{Binding').Count }
```

### 4-1.【スクリプトの限界を自ら記す】列単位の`IsReadOnly`だけ見ると誤る

**列定義の行に`IsReadOnly="True"`が無いものを機械的に拾うと「編集可12件」と出るが、これは誤り**にござる
——**`FindResultsGrid`・`OutputGrid`は`DataGrid`本体に`IsReadOnly="True"`が付いており**
（`:1653`／`:1669`）、**本体の指定が列に及ぶ**。**実際に編集可能なのは型式列1件のみ。**

**機械的集計は当たりを付けるのみ。1-3・1-4の判定はすべて一次ソースを直読して確かめた。**

---

## 5. 気づきと落とし先

1. **`Mode=TwoWay`の明示を追うだけでは、書き戻る経路を尽くせぬ**——
   `TextBox.Text`・`CheckBox.IsChecked`・`DataGridTextColumn.Binding`は**既定でTwoWay**。
   **「明示されているもの」を数えると、既定で有効なものを丸ごと見落とす。**
   → **落とし先＝`onmitsu.md`調査ワークフロー2項の候補**（**家老のご判断を仰ぐ**）
2. **「読み取り専用か」の指定は、列と本体の2階層にある**（4-1）。**下位だけ見ると誤る。**
   **`memory: feedback_investigation_layer_and_scope`（調査の層違い）と同根**にござる。
   → **落とし先＝既に`memory`にあり。実例としてのみ記す**
3. **ダイアログが全てコードビハインド方式であったのは、本件では幸いした**——
   **Binding経由の抜け道が9ファイル分まとめて消えた。** 意図的な設計か時系列の産物かは**不明**。
   → **落とし先＝なし（事実の記録のみ）**

---

## 6. 不明点

- **ダイアログがBindingを使わぬのが意図か偶然か**——コードからは判じられぬ
- **本調査の射程**＝XAMLとコードビハインドの`SetBinding`まで。
  **`Device`等のモデルを他のコードが直接書き換える経路（Binding以外）は対象外**にござる。
  それらは`MarkDirty`起点の前調査で拾っておる

---

## 出典

- `src/Ecad2.App/MainWindow.xaml:319, 926, 929, 965, 1374-1381, 1491-1502, 1579, 1600, 1603, 1651-1694`
- `src/Ecad2.App/MainWindow.xaml.cs:1198-1209`
- `src/Ecad2.App/ViewModels/DeviceTableViewModel.cs:13`
- `src/Ecad2.App/Views/*.xaml`（9ファイル、`{Binding`は全0件）
- `src/Ecad2.App/App.xaml:458, 476`
- 前調査＝`docs/ecad2-t134-undo-coverage-survey-onmitsu.md`（3-2節が本調査の起点）
