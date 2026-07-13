# T-069 往復4周目相当 テスト設計書(隠密)

- 対象: `docs/ecad2-t069-fix2-review-onmitsu.md`「要修正(新規発見、CONFIRMED)」2件(殿裁可済み、2026-07-13)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: 仕様側(あるべき振る舞い)からのテスト設計起草。テスト設計技法(同値分割・境界値分析・状態遷移・ペア構成の対称性・パラメタライズド活用)を明示適用。
- スコープ境界: 設計のみ、実装は侍。テスト設計と実装の分離原則(`karo.md`)に従い、設計にある観点は侍が勝手に省かない。設計にないテストの追加は自由。
- 対象外: P-072(「表示と実行の整合原則」の左クリック・矢印キー等への横展開)は据え置き(家老指示)。経過観察(HasAnyDraftのフェイルセーフ喪失、`CommitDeviceNameEdit`3箇所複製等)も今回対象外。

## 検証観点1: ツールバーボタンのドラフトクリア漏れ(要修正1)

### あるべき振る舞い

記入中ドラフト(縦コネクタ/自由線/画像挿入)を保持した状態でツールバーの部品配置ボタン(a接点等、`ActivateBuiltinTool`)・自作パーツボタン(`ActivateOpenPartSelection`)を押下した場合、既存のEscape/確定と同じくドラフトは適切にクリアされ、`HasAnyDraft`はfalseに戻るべき。ドラフトを保持したままTool.Modeだけが素通しで切り替わってはならない。

### 状態遷移表

| 遷移前Tool.Mode | ドラフト状態 | 操作 | 遷移後Tool.Mode(期待) | HasAnyDraft(期待) |
|---|---|---|---|---|
| Select | なし | ActivateBuiltinTool | PlaceElement | false(既存動作、回帰確認) |
| PlaceElement(連続配置中、ドラフト無し) | なし | ActivateBuiltinTool(別部品) | PlaceElement | false(既存動作、回帰確認) |
| PlaceConnector | `_connectorDraft`≠null | ActivateBuiltinTool | PlaceElement | **false(現状=true、バグ)** |
| PlaceLine | `_freeLineDraft`≠null | ActivateBuiltinTool | PlaceElement | **false(現状=true、バグ)** |
| PlaceImage | `_imageInsertDraft`≠null | ActivateBuiltinTool | PlaceElement | **false(現状=true、バグ)** |
| PlaceConnector | `_connectorDraft`≠null | ActivateOpenPartSelection | PlaceElement | **false(現状=true、バグ)** |

### 同値分割・境界値

- 有効域: ドラフトを持たないモード(Select/PlaceElement)からの遷移 → 既存動作、回帰させないことを確認する対照ケースとして残す。
- 無効域(バグ域): 3種の記入中モード(PlaceConnector/PlaceLine/PlaceImage)**全て**を網羅する(1種だけでなく3種とも対称性を点検、T-064で画像挿入ドラフトだけ横展開漏れが起きた前例と同型の見落としを防ぐ)。
- ペア構成の対称性: ドラフトクリアが必要な入口は`ActivateBuiltinTool`と`ActivateOpenPartSelection`の2つ。少なくとも1組(表の最終行)は両方の入口で確認する。

### テストケース(Theory活用)

xUnitの`[Theory]`+`[InlineData]`で、記入中モード3種×`ActivateBuiltinTool`相当操作の組を1つのTheoryにまとめる:

- `ToolbarButtonEquivalent_ClearsResidualDraft_BeforeSwitchingMode(モード種別)` — PlaceConnector/PlaceLine/PlaceImageの3ケースを`[InlineData]`で列挙。
- 個別`[Fact]`として、`ActivateOpenPartSelection`相当の操作でも最低1ケース確認する(ペア対称性の確認目的、同一ロジック分岐であればTheory化までは不要)。
- 各テストの手順: 該当ドラフトを開始(`Begin*Draft`) → ツールバーボタン相当の操作を実行 → Assert:
  1. `HasAnyDraft == false`
  2. 該当ドラフトが実際にキャンセルされている(生成されずに終わっている)こと。例: 縦コネクタなら操作後に`sheet.Connectors`へ何も追加されていないこと(単に`HasAnyDraft`のフラグだけでなく、既存の`CancelConnectorDraft`等と同じ効果を持つことを確認する)。
  3. `Tool.Mode == PlaceElement`(切替自体は正しく行われること、退行させない)。

## 検証観点2: 連続配置中の右クリックによる作業起点(SelectedCell)破壊(要修正2)

### あるべき振る舞い

連続配置中(Tool.Mode=PlaceElement、SelectedCell=作業セルP0、T-021分岐A)に、無関係な既存要素上で右クリックしてコンテキストメニューを開いても、それだけ(実行前の段階)では作業起点(SelectedCell)を変更してはならない。メニューを何も選ばずに閉じた場合、SelectedCellはP0のまま維持されるべき。メニュー項目を実際に選択して操作を実行する場合に限り、その操作対象(ヒット要素)への切替が許容される。

### 状態遷移表

| 遷移前SelectedCell | ヒット要素の位置 | 操作 | 遷移後SelectedCell(期待) |
|---|---|---|---|
| P0(連続配置の作業セル) | アンカーセル(`hitElement.Pos`==クリック位置) | 右クリック→メニュー表示→キャンセル | P0のまま(正規化してもP0と一致する退化ケース、回帰確認) |
| P0 | 非アンカーセル(`CellWidth`>1要素の2セル目等) | 右クリック→メニュー表示→キャンセル | **P0のまま(現状=hitElement.Posへ変化、バグ本体)** |
| P0 | 非アンカーセル | 右クリック→メニュー表示→削除メニューを実行 | ヒット要素が削除される(操作自体は正しく機能すべき、キャンセル時の修正で実行系を壊さないことの確認) |
| P0 | ヒットなし(空セル) | 右クリック→行操作メニュー表示→キャンセル | P0のまま(既存、回帰確認) |

### 同値分割・境界値

- 境界値: アンカーセルと非アンカーセル(`CellWidth`>1要素、例: SelectSwitch=3の2/3セル目)の両方を網羅する。アンカーセルのケースは「正規化してもP0と一致するため見た目上バグが顕在化しない」退化ケースとして区別する(境界値分析の下限側)。
- ペア構成の対称性: 「メニューをキャンセルした場合(現状維持を期待)」と「メニューの項目を実行した場合(切替を期待)」は非対称な期待値の組であり、**両方**確認しないと、キャンセル時の復元処理を入れた副作用で実行系が壊れていないかを検出できない。

### テストケース

- `RightClickOnNonAnchorCell_DuringContinuousPlacement_CancelledMenu_PreservesOriginalSelectedCell` — 主眼のバグ再現ケース(非アンカーセル×キャンセル)。
- `RightClickOnAnchorCell_DuringContinuousPlacement_CancelledMenu_SelectedCellUnchanged` — 境界(退化)ケース。
- `RightClickOnNonAnchorCell_DuringContinuousPlacement_DeleteExecuted_TargetsHitElement` — キャンセル時の修正と対になる「実行時は正しく動作する」退行防止確認。

### 実装形態への申し送り(参考、実装方法は侍裁量)

上記は現状View層(`MainWindow.xaml.cs`)のイベントハンドラ内ロジックのため、単体テストで直接検証するには実装上の工夫が要る可能性がある(例: 右クリック時のメニュー対象解決をSelectedCellの書き換えと分離する、あるいはメニューのキャンセル時に復元する等)。具体的な実装方法は侍の裁量とするが、いずれの方法を選んでも上記状態遷移表の最終的な`SelectedCell`の値は満たすこと。View層依存でどうしても単体テスト不可と判断した場合は、RED証明不可の理由を明記の上、実機確認(忍者)へ委ねてよい。

## DoD

- 検証観点1・2それぞれの状態遷移表の全行がテストとして反映されていること(設計書にある観点を勝手に省かない)。
- 回帰テスト全件が既存の合格数から減っていないこと。
- 修正1・2それぞれについてRED先行証明(修正前コードで該当テストがFAILすることを実測)を行うこと。View層依存でRED証明不可の場合はその旨を報告に明記すること。
