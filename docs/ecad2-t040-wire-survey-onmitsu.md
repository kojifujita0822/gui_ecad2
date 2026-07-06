# T-040追加要望：横配線・縦分岐線 手動記入機能 実装規模調査（隠密）

> 2026-07-06 隠密調査。家老依頼（殿要望「横配線と縦分岐線の記入ができるようにしたい」＝
> GXツールバーF9(横線)/Shift+F9(縦線)相当、参照画像 `docs/images/t040-gx-ladder-toolbar-reference.png`）。
> 実装規模の見積もり材料として、(1)ecad2 App層 (2)ecad2 Core/Rendering層 (3)移植元GuiEcad
> の3点をExploreエージェント委譲により調査した。

---

## 結論

**現状ecad2にF9/sF9相当の手動記入コマンドは未実装（キーバインド・ツールバーボタンとも無し）。
ただし移植元GuiEcadには相当機能が実際に存在しており、そのCore層データモデル（`VerticalConnector`・
`FreeLine`）と描画ロジックは既にecad2へ移植済みで生きている（OR縦コネクタ自動生成が現に
これを使用中）。欠けているのは「ユーザー操作（ドラッグ）→手動生成コマンド→キーバインド」という
UI層の配線のみ。ゼロからの新規設計ではなく、GuiEcadのUI層実装パターンを移植する作業に近い。**

- 実装規模: **中程度**（Core/Renderingのデータモデル・描画は流用可能、新規実装はApp層のポインタ操作
  ＋コマンド＋キーバインドに限定される。ただし横線の制御回路グリッド内表現は新規データ構造が必要）
- キーボードファースト方針との差分: GuiEcadでも横線・縦線にキーバインドは無く、パレットの
  ラジオボタン選択＋マウスドラッグのみだった。F9/sF9の新規キーバインド追加はecad2で新たに設計が必要。

---

## 根拠

### (1) ecad2 App層（`src/Ecad2.App`）— 現状「なし」

- `ToolState.cs:12` の `ToolMode` enum に `PlaceConnector`/`PlaceLine`/`PlaceWireBreak`/`PlaceDot` が
  **定義済みだが未使用**（`MainWindowViewModel.cs:43`、`MainWindow.xaml.cs` 各所で参照されるのは
  `PlaceElement`/`Select` のみ）。
- ツールバー（`MainWindow.xaml:86-222`）・キーボードハンドラ（`MainWindow.xaml.cs:246-350`、
  `Window_PreviewKeyDown`）とも横線・縦線・配線に対応するボタン/キーは無い。`F9` はリポジトリ全体で0件。
- 専用コマンド・RoutedCommand・KeyBinding（XAML `InputBinding` 含む）も無し。

→ **未使用のenum値は、GuiEcad移植（T-007）時に列挙型の名前だけ引き継がれ、UI層の中身が
実装されなかった名残と推測される**（下記(3)のGuiEcad実装と値名が一致することから推測。断定はできない）。

### (2) ecad2 Core/Rendering層 — データモデルは既存・流用可能

- **OR縦コネクタ自動生成**: `MainWindowViewModel.PlaceElementAtSelectedCell`
  (`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:320-360`)。358-359行目で
  `sheet.Connectors.Add(new VerticalConnector { Column, TopRow, BottomRow })` を2本生成。
- **`VerticalConnector`モデル**（`src/Ecad2.Core/Model/Element.cs:122-128`）: `Column`(0.5刻み)・
  `TopRow`・`BottomRow` のグリッド座標。`Sheet.Connectors`に保持。
  `NetlistBuilder.BuildVerticalConnectorUnions`(`Simulation/NetlistBuilder.cs:260-272`)でシミュレーション
  ネットに直結（＝電気的に意味を持つ実結線データ）。描画は`DiagramRenderer.DrawConnectors`(395-410行)。
  → **縦線はこの構造をそのまま「ユーザー指定版」に転用しやすい**（座標を渡してインスタンス生成する
  既存パターンがある）。
- **横線**: 制御回路グリッド内の横配線は`DiagramRenderer.DrawRungSegment`(521-579行)による自動計算のみで、
  手動追加用のデータクラスは無い。`WireBreak`(`Element.cs:133-140`)は自動横配線を「切る」マークのみ。
  → **横線の手動追加には新規データ構造が必要**（グリッド内表現の場合）。
- **`FreeLine`モデル**（`Element.cs:77-89`、`Sheet.FreeLines`）: mm実座標(`X1,Y1,X2,Y2`)＋`LineStyle`、
  横線・縦線どちらも表現可能な汎用自由線。ただし**Netlist/シミュレーションに一切参加せず、見た目専用**
  （主回路＝非シミュレート前提の設計）。描画は`DiagramRenderer.DrawFreeLines`(436-456行)、
  `IRenderer.DrawLine`(`Rendering/IRenderer.cs:35`)→WPF実装は`WpfRenderer.DrawLine`
  (`src/Ecad2.Rendering.Wpf/WpfRenderer.cs:43-44`、`DrawingContext.DrawLine`直接描画）。

### (3) 移植元GuiEcad（`C:\Users\kojif\Desktop\生産物\gui_ecad\src`）— 相当機能は実在していた

- **F9/Shift+F9のキーバインドは存在しない**（`grep -rn "F9"`全ソース0件）。
  `MainPage.KeyBindings.cs:95-101`のショートカットはF5=a接点/F6=b接点/F7=コイル/F8=押しボタンのみ。
- **しかし手動記入ツール自体は存在**（パレットのラジオボタン選択＋マウスドラッグ、キー割当なし）:
  1. **「分岐」ツール（縦線・sF9相当）**: `MainPage.xaml:386-390`（Tag="connector"、ツールチップ
     「縦コネクタ（並列分岐）：列の交点を上から下へドラッグ」）。ドラッグ処理
     `MainPage.Pointer.cs:288-299`→確定`643-654`行で`AddConnectorCommand`実行、`VerticalConnector`生成。
     **ecad2のOR自動生成が使うデータモデルと同一**。
  2. **「直線」ツール（横線含む自由線・F9相当に近いが向き固定ではない）**: `MainPage.xaml:407-411`
     （Tag="line"、ツールチップ「自由直線（主回路の母線・結線。格子点に吸着して2点ドラッグ）」）。
     ドラッグ処理`MainPage.Pointer.cs:232-243`→確定`598-608`行で`PlaceFreeLineCommand`実行、
     `FreeLine`生成。主に主回路（動力回路）モード用途（推測：ツールチップ文言・主回路モード排他仕様より）。
  3. **標準ラダー部の横配線は自動生成**（`DrawRungWires`、`NetlistBuilder.cs:52`他）、手動記入ではない。
     ユーザーができるのは「配線分断」ツール（`PlaceWireBreak`、`Pointer.cs:257-272`）で**切断のみ**。

---

## 不明点

- 殿要望「横配線」が (a) 制御回路グリッド内の自動横配線に対する手動上書き・追加を指すのか、
  (b) 主回路的な自由配置の線（GuiEcadの`FreeLine`相当）を指すのか、要件確認が必要
  （設計・実装規模がここで大きく変わる）。
- ecad2の`ToolMode`未使用enum値がGuiEcad移植時の意図的な先行定義か単なる列挙型丸ごと移植の副産物かは
  推測の域を出ない（コミット履歴未確認）。

## 派生提案（範囲外の気づき）

- 上記(a)/(b)いずれの場合も、GuiEcadの`MainPage.Pointer.cs`のドラッグ確定ロジック
  （`AddConnectorCommand`/`PlaceFreeLineCommand`）が実装パターンの直接参考になる。侍への実装依頼時に
  参照箇所として渡すと往復削減に有効と考えられる（家老采配事項、隠密からは提案のみ）。
