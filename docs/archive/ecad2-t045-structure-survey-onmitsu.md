# T-045 App層リファクタリング構造調査（隠密）

> 2026-07-08 隠密調査。T-041完全クローズ後の采配。対象＝①P-025「配置経路の検証散在」
> ホットスポット計測 ②P-016のDispatcher直接依存箇所全列挙 ③T-041で確定した「4種ドラッグ
> 状態機械のコピペ構造」の共通化候補整理 ④T-045増分分割の材料になる依存関係。
> ①②は`Explore`エージェント2件（並行）、③は隠密自身のコードトレース（T-041往復レビューで
> 蓄積した知見）に基づく。事実と推測を峻別し、推測は明記する。

---

## 1. P-025「配置経路の検証散在」ホットスポット計測

### 1.1 配置系メソッドの責務マップ（実在確認済み）

| メソッド | ファイル:行 | 責務 |
|---|---|---|
| セル選択（`ToGridPos`代入部） | `MainWindow.xaml.cs:521` | セル選択（**検証なし**） |
| `TryPlaceActiveTool` | `MainWindow.xaml.cs:565-571` | ツールバー起点→`TryPlaceElement`委譲 |
| `TryPlaceBuiltin` | `MainWindow.xaml.cs:1143-1152` | F5-F8起点、`HasProject`検証→委譲 |
| `TryPlaceElement` | `MainWindow.xaml.cs:1320-1354` | 検証（`SelectedCell`null／`IsSelectedCellOccupied()`）→配置バーUI表示 |
| `PlacementOkButton_Click` | `MainWindow.xaml.cs:1398-1412` | バーOK確定→`PlaceElementAtSelectedCell`+再描画 |
| `PlaceElementAtSelectedCell`(VM) | `MainWindowViewModel.cs:1292-1353` | **モデル登録**（検証なし＝境界・占有とも未実施） |
| `IsSelectedCellOccupied` | `MainWindowViewModel.cs:1284-1285` | 占有検証（Elements専用） |
| `TryPlaceWireBreak`/`PlaceWireBreakAtSelectedCell` | `MainWindow.xaml.cs:1192-1213`／`MainWindowViewModel.cs:587-595` | WireBreak版の同型3段ゲート＋独自dedup |
| `TryPlaceConnectionDot`/`PlaceConnectionDot` | `MainWindow.xaml.cs:1259-1281`／`MainWindowViewModel.cs:946-953` | ConnectionDot版の同型3段ゲート＋独自dedup |
| `MoveSelectedCell` | `MainWindow.xaml.cs:873-889` | キーボード移動時のみ境界クランプ実施 |

**重複の実態**：`TryPlaceWireBreak`/`TryPlaceConnectionDot`/`TryBeginConnectorDraft`/
`TryBeginFreeLineDraft`は「`HasProject`→シート種別→`SelectedCell`null」の3段ゲートを
ほぼコピペで4回独立実装。占有チェックもElements/WireBreaks/ConnectionDotsで3回別々に
inline実装され共通化されていない。境界クランプ（`Math.Clamp`）も`UpdateDragConnector`等
15箇所前後で個別実装。P-025が指摘する「検証散在」は実データで裏付けられる。

### 1.2 P-020該当箇所（種別マッピング未実装）

現状の行番号は**1195・1310**（提案時点の224/339からT-041以降の増分で押し出された、推測）。

- `SelectedElementDeviceName`セッター内：`MainWindowViewModel.cs:1195`
- `PlaceElementAtSelectedCell`内：`MainWindowViewModel.cs:1310`

両箇所とも`PartResolver.ComponentKind(e, PartLibrary)`（`Ecad2.Core/Model/PartResolver.cs:37`）
経由で`ElementKind`は既に解決可能。`DeviceClass`enum（`Ecad2.Core/Model/Device.cs:3`）は
`Relay/PushButton/SelectSwitch/Lamp/Timer/Counter/Terminal/Other`を持ち、`ElementCatalog`の
`IsContact`/`IsLoad`分類（`ElementCatalog.cs:59-69`）と対応が付けやすい。**新規
`ElementKind→DeviceClass`マッピング関数を1つ作り、1195と1310の両方から呼ぶのが最小手数**
（推測）。

### 1.3 P-021/P-022/P-024該当箇所と検証サービスの呼び出し元候補

- **P-021（占有再チェック欠如）**：`TryPlaceElement`（1327）で占有確認後、実際の
  `Add`は`PlacementOkButton_Click`（1398）経由で`PlaceElementAtSelectedCell`
  （VM:1303 `sheet.Elements.Add`）が行うが、この間に再チェックがない。
  `Sheet.Elements`は単なる`List<ElementInstance>`で不変条件を持たない。
- **P-022/P-024（境界ガード欠如）**：マウス経路`ToGridPos`（`Views/LadderCanvas.cs:402-407`）
  →`GridGeometry.RowAt/ColAt`（`Ecad2.Core/Rendering/GridGeometry.cs:23,26`）は
  `Math.Floor`のみでクランプなし。`MainWindow.xaml.cs:521`でそのまま`SelectedCell`に代入され、
  `SelectedCell`セッター（`MainWindowViewModel.cs:165-203`）側にも境界検証がない。

**共通検証関数の呼び出し元候補**（優先順）：
1. `PlaceElementAtSelectedCell`冒頭（1294）— 最重要、実際のモデル変更点
2. `TryPlaceElement`（1327）— UX即時フィードバック用
3. `SelectedCell`セッター（165）— 境界ガードの単一関門化（既に他選択状態クリアの単一入口
   として機能している設計と整合）
4. `PlaceWireBreakAtSelectedCell`（587）・`PlaceConnectionDot`（946）

### 1.4 着手順の所見（推測含む）

`MainWindowViewModelTests.cs`に`PlaceElementAtSelectedCell`の単体テストが既存（WPF起動不要）。
対して`MainWindow.xaml.cs`側はUI Automation実機確認が要る。**まず`IsSelectedCellOccupied`
（1284）の隣に境界+占有を束ねた新関数を追加し`PlaceElementAtSelectedCell`から呼ぶのが
最小リスク**（呼び出し元1箇所、既存テストで回帰検知可能）。次に`TryPlaceElement`からも
同関数を呼ぶよう置換。**`SelectedCell`セッター自体の境界ガード化とマウス経路の修正は
後回しが安全**（`ToGridPos`は他プリミティブのヒットテストとも座標変換を共有し影響範囲が
広いため、推測）。WireBreak/ConnectionDot側は独立実装のため後続で横展開可能。

---

## 2. P-016「Dispatcher直接依存」箇所の全列挙

### 2.1 直接依存箇所

| ファイル:行 | 内容 |
|---|---|
| `SheetNavigationViewModel.cs:97-99` | `AddCommand`内`Application.Current.Dispatcher.BeginInvoke(ContextIdle, () => SelectedSheet = sheet)` |
| `SheetNavigationViewModel.cs:147-149` | `RenameCommand`内`Application.Current.Dispatcher.BeginInvoke(ContextIdle, RefreshSelectedSheet)` |
| `MainWindow.xaml.cs:1353` | `Dispatcher.BeginInvoke(() => PlacementDeviceNameBox.Focus(), Loaded)` |

P-016で問題視されているのは前2件（ViewModel層、テスト対象）。`MainWindow.xaml.cs:1353`は
View（code-behind）内の`this.Dispatcher`呼び出しで、Windowが生成されテストで単体駆動される
対象ではないため、P-016の射程外と判断（推測）。

### 2.2 BeginInvokeが必要だった理由

いずれも**「WPFのレイアウト/コンテナ生成パスの完了を待つための遅延実行」**が共通理由：
- `AddCommand`：`ObservableCollection.Add`直後はListBoxが新規アイテムのコンテナを生成し
  終えておらず、同期的に`SelectedSheet`を設定しても選択ハイライトが追従しない（T-026実機
  確認で発見）。
- `RenameCommand`：`RemoveAt+Insert`でコンテナを再構築した直後の選択ハイライト再同期も
  同様の理由。
- `MainWindow.xaml.cs:1353`：`Visibility.Collapsed→Visible`直後はMeasure/Arrange未完了の
  ため`Focus()`の同期呼び出しが失敗しうる。

### 2.3 テストへの影響

`tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`の4テストが影響を受ける：
`AddCommand_MarksDirty`・`AddCommand_WithMainCircuitTrue_CreatesMainCircuitSheet`・
`AddCommand_WithBlankName_FallsBackToAutoNumberedName`・`RenameCommand_MarksDirty`。
いずれもtry/catchでNREを握りつぶし、`MarkDirty`検証のみ行っている——**「BeginInvokeが
実際にディスパッチされ選択ハイライトが正しく同期するか」という経路は一切検証できていない**
（xUnitプロセスにWPF Applicationが存在しないため到達前に例外）。`DeleteCommand`関連は
Dispatcher非依存のため影響なし。

### 2.4 DI差し込み箇所の所見（推測含む、既存パターンとの整合あり）

**本リポジトリには既に類似の先例がある**：`MainWindowViewModel`は`PartFolderStore`について
「無引数コンストラクタ=本番用（`CreateDefault()`委譲）」「引数版=テスト注入用」という
**2本立てコンストラクタパターン**をT-042（P-019対応）で導入済み
（`tests/Ecad2.App.Tests/ViewModelTestBase.cs:24`が`new(new PartFolderStore(_tempDir))`で
注入）。**P-016でも同型のパターンが自然**（推測）：`IDispatcherService`
（`BeginInvoke(DispatcherPriority, Action)`程度の最小インターフェース）を新規定義し、
本番実装は`Application.Current.Dispatcher`をラップ、テスト用フェイクは同期的に`Action`を
即時実行する。`SheetNavigationViewModel`のコンストラクタに追加引数として持たせれば、
既存のP-019/T-042の解決パターンと設計の一貫性が保てる。現状`IDispatcherService`等の
抽象化は存在しない（grep未検出、新規作成が必要）。

---

## 3. 4種ドラッグ状態機械の共通化候補整理（隠密、T-041往復レビューの知見）

### 3.1 現状の構成（`MainWindowViewModel.cs`）

| 種別 | 状態フィールド | Begin/Update/Confirm/Cancel/ForceCancel |
|---|---|---|
| VerticalConnector | `_draggingConnector`＋isEndpoint/isTop＋TopRow/BottomRow/Column origin+start（8個） | 306-395行付近 |
| WireBreak | `_draggingWireBreak`＋Row/Boundary origin+start（5個） | 518-565行付近 |
| FreeLine | `_draggingFreeLine`＋isEndpoint/isStart＋X1/Y1/X2/Y2 origin+start+maxXY（10個） | 652-750行付近 |
| ConnectionDot | `_draggingConnectionDot`＋X/Y origin+start+maxXY（5個） | 871-920行付近 |

### 3.2 括れる部分（共通化候補、リスク低）

以下は4種で**完全に同一パターン**であることをコードトレースで確認した：

- `IsDragging*` ⇒ `_dragging* is not null`
- `ForceCancelDrag*IfAny()` ⇒ `if (_dragging* is null) return; CancelDrag*(); OnPropertyChanged(nameof(IsDragging*));`
  （所見Y対応で4種とも統一済み、T-041増分7往復2周目レビューで確認済み）
- `ConfirmDrag*()`の骨格 ⇒ `if (_dragging* is T t && (変化条件)) MarkDirty(); _dragging* = null;`
  （「変化条件」の中身は型ごとの軸数に依存するが、骨格自体は同一）
- `CancelDrag*()`の骨格 ⇒ `if (_dragging* is T t) { 元の値を復元 } _dragging* = null;`

### 3.3 括れない部分（種別固有、共通化リスク中〜高）

- `BeginDrag*()`のスナップショット取得対象（型ごとにフィールド数・軸数が異なる：
  整数2軸+実数1軸／実数2軸／実数4軸／実数2軸）
- `UpdateDrag*()`の実クランプロジック——**これが最も型ごとに異なる**：
  - VerticalConnector本体移動：span保持クランプ（TopRow/BottomRow間隔を保つ）＋Column単純
    クランプ
  - VerticalConnector端点リサイズ：Top<Bottomゼロ長禁止＋min>maxガード（所見B）
  - WireBreak：Row/Boundary独立クランプ（単純）
  - FreeLine本体移動：X/Y独立クランプ＋min>maxガード（所見AB）
  - FreeLine端点リサイズ：向き保持（水平/垂直判定）＋ゼロ長禁止＋境界クランプ（所見AC）
  - ConnectionDot：X/Y単純クランプ（所見AD）
- 端点リサイズの有無（VerticalConnector/FreeLineのみ持つ、WireBreak/ConnectionDotは本体
  移動のみ）

### 3.4 共通化の設計案（3段階、severity順）

1. **【最小リスク】`ForceCancelDrag*IfAny`パターンの共通ヘルパー化**：4箇所とも文字通り
   同一の3行（null チェック→Cancel呼び出し→OnPropertyChanged）。ただしC#の`ref`フィールド
   制約により、単純な関数抽出では対応しづらく、delegateベース（
   `ForceCancelIfAny(Func<bool> isActive, Action cancel, Action notify)`）にするか、
   個別に残すかはトレードオフ（可読性 vs 重複除去）。
2. **【中リスク】`ConfirmDrag*`/`CancelDrag*`の骨格共通化**：スナップショット比較・復元を
   汎用化するには、型ごとのプロパティ群を1つの「スナップショット構造体」として扱う設計が
   前提になる（例: `record ConnectorSnapshot(int TopRow, int BottomRow, double Column)`）。
3. **【高リスク、非推奨】`UpdateDrag*`のクランプロジック共通化**：4種で計算式が本質的に
   異なるため（span保持/独立/向き保持+ゼロ長/単純）、無理に共通化すると分岐だらけの
   汎用関数になり、かえって可読性を損なう可能性が高い。**ここは共通化しない方が良いと
   考える**（推測ではなく、4種の実装を横断的に読んだ上での判断）。

**総合所見**：`DragSession<T>`のような汎用型に完全収束させるのは過度な抽象化のリスクが
あり、1.（外枠のみ）にとどめるのが費用対効果が良いと考える。今回のP-039往復で判明した
「ForceCancelが位置復元を伴わない」（所見Y）・「境界クランプの一部漏れ」（所見AB/AC/AD）
は、いずれも外枠パターンの一部（1.）が型ごとに個別実装されていたために発生した見落としで
あり、1.の共通化はこの種の再発を構造的に防ぐ効果が期待できる。

---

## 4. T-045増分分割の材料になる依存関係（統合所見）

### 4.1 依存関係の整理

- **P-025系（配置前検証サービス）**：`PlaceElementAtSelectedCell`（VM層、既存テストで
  回帰検知可能）から着手するのが最も安全。`TryPlaceElement`（View層code-behind）は
  UI Automation実機確認が必要なため後段。`SelectedCell`セッター・マウス経路
  （`ToGridPos`/`GridGeometry`）は他プリミティブのヒットテストと座標変換を共有し影響範囲が
  広いため最後（推測）。
- **P-016系（Dispatcher抽象化）**：`SheetNavigationViewModel`単体の変更で完結し、
  `MainWindowViewModel`本体への影響は「コンストラクタへの引数追加」程度に限定できる
  （P-019/T-042の先例と同型の設計のため、影響範囲の見積もりがしやすい）。
- **ドラッグ状態機械の外枠共通化**：P-025・P-016とは独立した関心事（配置ではなく既存要素の
  移動）。他の2系統と依存関係がないため、並行して着手可能。ただし、既に安定稼働している
  T-041のコードに手を入れる作業なので、**回帰リスクを避けるため既存122件超のドラッグ関連
  テストが通り続けることを都度確認する体制が前提**（推測ではなく、T-041往復レビューで
  実際にこの種の回帰を複数回発見した実績に基づく判断）。

### 4.2 増分分割案（severity・依存の少なさ順、あくまで案）

1. 増分A：P-016（Dispatcher抽象化）— 影響範囲が最も狭く、独立性が高い
2. 増分B：P-025のVM層（`PlaceElementAtSelectedCell`への検証関数追加）— 既存テストで
   回帰検知可能
3. 増分C：P-025のView層（`TryPlaceElement`等への同関数適用）— UI Automation実機確認が必要
4. 増分D：ドラッグ状態機械の外枠共通化（3.4節の1.のみ）— 他系統と独立、ただし回帰リスク
   管理が前提
5. （検討課題）P-025のSelectedCellセッター・マウス経路の境界ガード化 — 影響範囲が広いため
   最後、または今回のスコープ外として据え置く判断もありうる

この分割案・優先順位は隠密の分析に基づく一案であり、最終的な増分設計・順序は侍の計画起草・
家老の裁可に委ねる。

---

## 出典・参照

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（配置系・ドラッグ系メソッド全般）
- `src/Ecad2.App/MainWindow.xaml.cs`（View層の配置・ドラッグハンドラ）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（Dispatcher依存箇所）
- `src/Ecad2.Core/Model/PartResolver.cs`・`Device.cs`・`ElementCatalog.cs`（P-020マッピング
  材料）
- `src/Ecad2.Core/Rendering/GridGeometry.cs`（境界クランプ欠如箇所）
- `tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`（P-016影響テスト）
- `tests/Ecad2.App.Tests/ViewModelTestBase.cs`（P-019/T-042の2本立てコンストラクタ先例）
- `docs/proposed.md`（P-016/P-020/P-021/P-022/P-024/P-025原案）
- `docs/archive/ecad2-t041-increment7-review-onmitsu.md`〜`-4.md`（4種ドラッグ状態機械の往復レビュー、
  所見A/Y/AB/AC/ADの原本）
- `Explore`エージェント2件（並行、P-025ホットスポット計測・P-016依存箇所列挙）
