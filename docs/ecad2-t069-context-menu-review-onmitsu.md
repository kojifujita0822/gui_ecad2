# T-069 右クリックメニュー(即着手可能部分) 静的コードレビュー(隠密)

- 対象コミット: `4e049c6`(侍実装、src/Ecad2.App/MainWindow.xaml.cs 1ファイル)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: 台帳DoD整合確認+`code-review`スキル併用(軽量既定、新規実装1周目ゆえ5角度に絞って実施)+既知トラップ狙い撃ち(家老指定2点)
- スコープ境界: レビューのみ、書き込みなし

## 結論サマリ

家老指定の既知トラップ2点(ヒットテスト優先順位の境界条件・F2条件の流用整合性)のうち、**F2条件の流用は完全一致で問題なし**。一方**ヒットテスト境界条件の検証から、複数セル幅要素での判定漏れという重大なバグが発覚**。加えてcode-reviewスキルにより、独立に3件の要修正級バグを追加発見した(いずれも複数角度独立検出)。

## 要修正(CONFIRMED、複数角度独立検出)

### 1. CellWidth>1要素の右クリック判定漏れ

**該当**: `MainWindow.xaml.cs:683` `sheet.Elements.FirstOrDefault(el => el.Pos == pos) is not null`

`Motor`(CellWidth=3)・`Breaker3P`/`ContactorMain3P`/`ThermalOverload3P`(CellWidth=2、いずれも主回路シートの主要要素)は複数セルを占有するが、この判定は要素の**左上アンカーセルとの完全一致**のみで、`el.CellWidth`を考慮していない。

**失敗シナリオ**: 3極ブレーカ(左端Pos=(row,1)、列1-2を占有)の右側セル(列2)で右クリック→判定が一致せずnull→縦コネクタも該当なしなら「行の前に行を挿入/末尾に行を追加/行を削除」という無関係なメニューが表示され、「削除/機器名変更/コメント編集」が一切出せない。

**根拠**: 同種の単純Pos一致漏れは`MainWindowViewModel.cs:1525-1529`の`IsOccupied`で既に区間交差判定(`el.Pos.Column <= right && left <= el.Pos.Column + el.CellWidth - 1`)へ修正済み(T-071バグ修正)。本コミットはその修正前パターンを新規ヒットテストに再導入している。

**検出経路**: code-reviewスキルAngle A・Dの2系統独立検出+隠密がElementCatalog.DefaultCellWidth/IsOccupiedを読解し裏取り済み。

### 2. 右クリックでDeviceNameBoxの未確定編集がサイレント消失

**該当**: `MainWindow.xaml.cs:685`(要素分岐の`SelectedCell = pos`)、`690-691`(縦コネクタ分岐の`SelectedCell = null; SelectedConnector = connector`)

新設の右クリック分岐は`CommitDeviceNameEdit()`を一切呼ばずに`SelectedCell`/`SelectedConnector`を直接代入する。

**失敗シナリオ**: 要素Aを選択し`DeviceNameBox`へ新機器名を入力(未確定、`UpdateSourceTrigger=Explicit`)→確定前に別要素Bまたは縦コネクタを右クリック→`SelectedCell`/`SelectedConnector`の再代入で`SelectedElementDeviceName`のPropertyChangedが即発火し、TwoWayバインディングで`DeviceNameBox.Text`がBの機器名へ強制上書きされる→Aへの入力内容はコミットされず消失。

**根拠**: 既存の`DeleteMenuItem_Click`(1169行目)は同種の状態変更の**前に**明示的に`CommitDeviceNameEdit()`を呼んでいる(`grep`で262/283/294/362/1134/1139/1169行目に呼び出し確認)。新設の右クリック分岐だけがこの防御を欠く。

**検出経路**: code-reviewスキルAngle B・Cの2系統独立検出+隠密が`CommitDeviceNameEdit`呼び出し箇所を洗い出し裏取り済み。

### 3. 部品配置モード中の「機器名変更」が無反応

**該当**: `MainWindow.xaml.cs:737` `DeviceNameBox.Focus(); DeviceNameBox.SelectAll();`

Tool.Mode==PlaceElement(部品配置モード、`IsPartSelectionVisible=true`)の間、`DeviceNameBox`を含むプロパティ用DockPanelは`MainWindow.xaml:486`の`InverseBoolToVisibility`によりCollapsedになる。WPFの仕様上、Visibility=Collapsed配下の要素にはフォーカスが乗らない。

**失敗シナリオ**: 左パレットで部品配置モードのまま既存要素を右クリックし「機器名変更」を選択→`Focus()`/`SelectAll()`が無効化され、リネームが無反応(例外もフィードバックも無し)。既存の左クリック選択(631行目)は`Tool.Mode==Select`のガードを持つが、この右クリックハンドラには同等のガードが無い。

**検出経路**: code-reviewスキルAngle A・Bの2系統独立検出+隠密がXAMLのVisibility条件を確認し裏取り済み。

## 要確認(仕様意図が不明、殿/家老の判断を仰ぎたい)

### 4. 右クリックだけで記入中の縦コネクタ・自由線ドラフトが消える

**該当**: `MainWindow.xaml.cs:685`(要素分岐)、`690`(縦コネクタ分岐)の`SelectedCell`代入

`SelectedCell`のsetter(`MainWindowViewModel.cs:263-291`)は値の変化に関わらず常時`ClearConnectorDraftIfAny()`/`ClearFreeLineDraftIfAny()`を実行する既存仕様(T-041由来、「選択状態をクリアする唯一の入口」として意図的に設計)。新設の右クリック分岐はTool.Modeを問わずこれを経由するため、縦コネクタ記入中(F9ドラフト、Tool.Mode==PlaceConnector)に既存要素・縦コネクタを右クリックすると、メニュー項目を選ぶ前の**メニューを開いた時点で**記入内容が警告なく破棄される。

**論点**: 既存の左クリック処理も`Tool.Mode==Select`のガード外で`SelectedCell = ToGridPos(position)`(662行目)を実行しており、記入中に左クリックすればドラフトは消える既存仕様がある。今回の右クリックはこれと一貫した挙動とも解釈できるが、**右クリックは「対象を選択する」意図ではなく「メニューを開く」意図であり、メニューを開いただけで記入内容を失うのはUX上望ましくない可能性が高い**。GuiEcad原本でこの挙動がどうなっていたか調査書に記載が無ければ確認要。

**検出経路**: code-reviewスキルAngle A発見、隠密が`SelectedCell`setter実装を読解し裏取り済み。

## 家老指定の既知トラップ2点の検証結果

- **ヒットテスト優先順位の境界条件**: 上記「要修正1」として重大な問題を発見(境界条件どころか根本的な判定方式の欠陥)。
- **F2条件の流用**: `MainWindow.xaml.cs:742`(右クリック)と`1062-1063`(F2キー)を一字一句照合、`!sheet.MainCircuit && pos.Row >= 0 && pos.Row < DiagramRenderer.TotalRows(sheet)`で完全一致。**問題なし**。

## 経過観察

- **行操作メニューの表示条件変更**(Angle B): 旧実装は行操作3項目(挿入/追加/削除)を無条件表示していたが、新実装はelse節(要素/縦コネクタが無いセルのみ)でしか表示しない。要素のある行では行操作ができなくなった。コミットコメントから意図的な仕様変更と読めるが、殿裁定・調査書に明記が無ければ意図確認を推奨。
- **MenuItem生成パターンの軽微な重複**(Angle D): 「MenuItem生成→Click登録→Items.Add」の3行パターンが`BuildElementContextMenuItems`と縦コネクタ用生成コードで重複。共通ヘルパー化の余地あり(軽微)。

CLAUDE.md規約違反なし(Angle E)。

## 派生提案の有無

なし(全指摘T-069の実装範囲内)。
