# T-125 App層全体構造調査（隠密、第1報）

> 2026-07-25 隠密調査。殿御下知「基本機能は構想下のものは完成している。隠密調査からapp全体を
> 見直す方針でいきたい」を受けたT-125の第一段。T-045（App層リファクタリング、2026-07-09完全
> Done）の作法を踏襲し、`docs/archive/ecad2-t045-structure-survey-onmitsu.md`を基準に「T-045で
> 何が解決され、何が残ったか・その後どう保たれたか」を測った。調査のみ、実装には一切触れていない。
> 急がずともよいとの仰せにつき、本報は第1報（ホットスポット計測・T-045成果の追跡・見直し候補
> 一次列挙）。増分分割素案は方向性を示すに留め、詳細設計は侍の計画起草を待つのが筋と考える。

---

## 総括

**最重要所見＝T-045完了時点（2026-07-09、1532+1470=3002行）から現在（2026-07-25、
3395+4132=7527行）までの16日間で、`MainWindowViewModel.cs`/`MainWindow.xaml.cs`の合計行数が
約2.5倍に肥大化している。** T-045が「配置前検証サービス抽出」等で解いたはずの負債集中構造は、
その後の急速な機能拡張（GroupFrame・PartEditor・画像挿入・検索置換・テストモード等）に
吸収され、絶対規模としては「振り出しに戻る」どころか**当時より深刻**になっている。

一方、T-045が確立した**設計パターン自体**（`ForceCancelIfAny`外枠共通化・`ConfirmDrag<T>`/
`CancelDrag<T>`ジェネリック・`ValidatePlacement`統合検証・`IDispatcherService`抽象化）は、
後続の新機能実装で驚くほど忠実に踏襲され続けている——「制度は機能したが、制度が対処する
規模そのものが急拡大した」という構図と見受ける。

---

## 1. App層ホットスポット計測

### 1.1 ファイル規模（現在）

| ファイル | 現在行数 | T-045完了時(`5f2ee6e`)行数 | 倍率 |
|---|---|---|---|
| `MainWindowViewModel.cs` | 3395 | 1532 | **2.2倍** |
| `MainWindow.xaml.cs` | 4132 | 1470 | **2.8倍** |
| 合計 | 7527 | 3002 | **2.5倍** |

App層全体14717行のうち、この2ファイルだけで**約51%**を占める（T-045当時の比率は未計測だが、
当時から既に「負債集中」と指摘されていた状態がさらに悪化していることは行数の絶対値から明らか）。

参考：他の主要ファイルは`LadderCanvas.cs`728行・`PartEditorCanvas.cs`734行・
`SheetNavigationViewModel.cs`323行・`PartEditorDialog.xaml.cs`304行と、いずれも
2大ファイルの1/10以下の規模に収まっている——**肥大化は`MainWindow.xaml.cs`/
`MainWindowViewModel.cs`の2ファイルへの一極集中**という構図は変わっていない。

### 1.2 責務の集中度（現在）

- `MainWindowViewModel.cs`：**public 180メンバー・private 103メンバー、計283**（1クラスの
  責務としては過大）
- `MainWindow.xaml.cs`：**private void（主にイベントハンドラ）143個**、`case`文49箇所
- ドラッグ状態機械は、T-045当時の4種（Connector/WireBreak/FreeLine/ConnectionDot）から
  **7種**（+Element/Image/Frame）に拡大（`_dragging*`フィールド計7種を確認）

### 1.3 増分の内訳（推定、T-045完了後に追加された主要機能）

`docs/todo.md`のDoneタスク一覧から、T-045完了（2026-07-09）以降に主要な機能追加があった
タスクを拾うと、T-055（行操作）・T-058（AvalonDockドッキング化）・T-061（テストモード結線）・
T-064（画像挿入）・T-067（GroupFrame）・T-068（自作パーツエディタ、増分0〜3-c）・T-069/T-070
（右クリックメニュー・検索置換）・T-083（ダークモード）・T-089（ボタン押下feedback）・T-099/
T-100/T-103/T-104/T-106（AvalonDock関連の多数の修正）等、**16日间で20件超のタスクが
`MainWindow.xaml.cs`/`MainWindowViewModel.cs`に触れている**（正確な全数は未計測、規模感の
把握が目的のため深追いしていない）。

---

## 2. T-045の成果がどう保たれた／崩れたか

### 2.1 保たれた・むしろ強化された点

1. **`ForceCancelIfAny`外枠共通化（増分D）**：T-045当時4種のみだったが、**その後追加された
   Element/Image/Frameの3種にも一貫して適用**され、現在7種全てが同一パターンに従う
   （`MainWindowViewModel.cs` 595, 799, 925, 1136, 1366, 1489, 1632行目に7つの
   `ForceCancelDragXxxIfAny`を確認、いずれも共通ヘルパー`ForceCancelIfAny`経由）。
2. **`ConfirmDrag<T>`/`CancelDrag<T>`ジェネリック（増分D）**：**これが最も注目すべき点**。
   T-045完了時点の増分Dレビュー（`docs/archive/ecad2-t045-increment-d-review-onmitsu.md`
   DoD(6)）では「ConnectionDotのみのPoC、Connector/FreeLineへの展開は技術的に可能だが
   見送り」という状態だった。**現在のコードでは、Connector・WireBreak・FreeLine・
   ConnectionDot・Image・Frame・Elementの7種すべてが`ConfirmDrag(ref _draggingXxx, ...)`/
   `CancelDrag(ref _draggingXxx, ...)`という同一ジェネリックヘルパー経由に統一されている**
   （`MainWindowViewModel.cs` 665, 671, 830, 835, 1005, 1011, 1190, 1195, 1401, 1419, 1530,
   1682, 1850行目で確認）。隠密のDoD(6)所見「展開見送りを覆しうる技術的材料」が、その後
   実際に採用され、かつ新機能実装のデフォルトパターンとして定着した——理想的な追随例。
3. **`ValidatePlacement`統合検証（増分B）**：`MainWindowViewModel.cs:2797`に現存し、
   `cellWidth`・`exclude`パラメータが拡張され、T-088（要素移動機能）でも`MoveElementTo`系
   （1651, 1692行目）から再利用されている。
4. **`IDispatcherService`抽象化（増分A）**：`Application.Current.Dispatcher`への直接依存は
   `WpfDispatcherService.cs`（正規アダプタ自身）の1箇所のみで、新規箇所での直接依存の再発は
   確認できなかった（P-040のアーキテクチャテストが機能している）。

### 2.2 崩れた・未達のまま残った点

1. **【最重要】ファイル規模の肥大化**（1.1節参照）——T-045の整理効果が後続機能追加で
   相殺されている。
2. **WireBreak/ConnectionDotの配置経路が`ValidatePlacement`を依然として使っていない**：
   `PlaceWireBreakAtSelectedCell`（860行目）・`PlaceConnectionDot`（1221行目）はいずれも
   独自の重複チェックのみで境界ガードを持たない。T-045増分C計画時点（実装計画書107-118行）で
   「本増分に含めるか将来課題とするかは着手時に家老へ確認」とされたまま、**16日経った今も
   未着手**と確認した。
3. **部品種別弁別ロジック（`Category==""&&Role==X&&IsOrEligible==Y`）の重複は解消されていない**：
   T-045増分B修正レビュー所見I（4箇所目の重複と指摘）から変わらず、現在も4ファイル
   （`PartEditorDialog.xaml.cs`・`MainWindow.xaml.cs`・`PartPaletteViewModel.cs`・
   `MainWindowViewModel.cs`）に同型パターンが分散したまま。
4. **`Sheet.Elements`の不変条件保証（データ構造側）は未着手のまま**：P-021の残課題（最小案＝
   呼び出し元でのガードは実装済みだが、モデル層自体の保証は据え置き）。

---

## 3. 家老の観点の種への回答

### 3.1 「setterで個別に副作用を起こす方式」の構造的懸念（本日の隠密自身の指摘）

T-068増分3-c静的レビューで発見した`PartEditorCanvas.WidthCells`/`HeightCells`setterの問題
（複数プロパティが同時に変化する際、片方ずつ独立にsetterが発火し中間状態で不整合が起きる）は、
**`MainWindowViewModel.cs`本体には同型の実例を確認できなかった**（`SelectedCell`・
`CurrentSheetIndex`等の主要setterはいずれも単一プロパティの変更に閉じており、複数プロパティが
連鎖的に個別セットされる経路は見当たらない）。ただし、これは「今回のApp層全体調査で網羅的に
確認した」わけではなく、`PartEditorDialog`/`PartEditorCanvas`固有の`SizeBox_TextChanged`
（2つのTextBoxが同一ハンドラに結線され、Text代入のたびに両プロパティを再セットする設計）で
初めて顕在化した構造である可能性が高いと考える（推測）。**T-068決着後に他のダイアログ・
複数プロパティ連携箇所（`SheetSettingsDialog`等）へも同型の罠がないか横断確認する価値がある**
と考えるが、断定はしない。

### 3.2 P-077・P-050の拾い直し

先の`docs/ecad2-proposed-pending-review-20260725-onmitsu.md`で報告した2件は、いずれも
「App層全体の構造的懸念」として本調査の文脈に直結する：

- **P-077**（左右クリックヒットテスト優先順位ロジックの重複、`MainWindow.xaml.cs`の左クリック
  ハンドラ/右クリックハンドラ2箇所に手書き重複）は、まさに本節が計測した`MainWindow.xaml.cs`
  4132行・49 `case`文という肥大化の一因であり、**構造見直しの一部として自然に取り込める**。
- **P-050**（GroupFrameのVisual*Mm座標オーバーライドがRowOps.InsertRow/DeleteRowに未追随）は
  `MainWindowViewModel.cs`側ではなくCore層（`RowOps.cs`）の話だが、GroupFrame機能
  （92箇所で言及、Frame関連コードの塊）自体が本調査のホットスポットの一部であり、見直しの
  ついでに実害確認する価値がある。

---

## 4. 見直し候補（規模の見立てとともに）

| 候補 | 規模見立て | 根拠 |
|---|---|---|
| WireBreak/ConnectionDot配置経路への`ValidatePlacement`横展開 | 小 | T-045当時から明確な適用先が分かっている、既存パターンの機械的横展開 |
| 部品種別弁別ロジック（4箇所重複）の共通ヘルパー化 | 小〜中 | 対象4ファイルの目的が異なるため単純統合は不可、共通述語の抽出に留める設計判断が要る |
| P-077：左右クリックヒットテスト優先順位の共通化 | 中 | `MainWindow.xaml.cs`の主要な複雑度要因の1つ、要素種別が今後も増える見込みなら効果大 |
| `MainWindow.xaml.cs`のイベントハンドラ群の責務分離 | 大 | 143個のハンドラ・4132行、T-045が対象にしなかった領域。code-behindからの機能移譲は影響範囲が広く要注意 |
| `MainWindowViewModel.cs`の分割（283メンバー） | 大 | 単一クラスとしての責務過多が明確。ドラッグ状態機械7種・配置系・Undo/Redo・機器表連携等、既に独立ViewModelを持つ機能（`SheetNavigationViewModel`等）との切り出し基準の見直しが必要 |
| Sheet.Elements不変条件保証（データ構造側） | 中〜大 | モデル層の設計変更を伴う、P-021からの積み残し |
| GroupFrame座標オーバーライド未追随（P-050）の実害確認 | 小（調査のみ） | 対処自体は小さいと見積もるが、まず実害の有無を確認する調査が先 |

---

## 5. 増分分割の素案（方向性のみ、詳細設計は侍起草を待つ）

T-045が「独立性の高い順」で分割したのに倣い、以下の順を素案として示す（優先順位・要否は
家老・侍の判断に委ねる）：

1. **増分α（小規模・独立性高）**：WireBreak/ConnectionDot境界ガード横展開＋部品種別弁別
   ロジックの共通ヘルパー化——T-045の未達分の後始末、着手障壁が低い
2. **増分β（中規模）**：P-077横展開（左右クリックヒットテスト共通化）——構造改善と
   pending項目解消を同時に狙える
3. **増分γ（大規模、要事前調査）**：`MainWindowViewModel.cs`の責務分割——283メンバーの
   分類・切り出し境界の見極めが前提。着手前に「どの機能群が独立ViewModel化に適するか」の
   追加調査が要ると考える（本報告の範囲外、次段の調査候補）
4. **増分δ（大規模、要事前調査）**：`MainWindow.xaml.cs`のイベントハンドラ群の責務分離——
   View層のためUI Automation実機確認の比重が増える。T-045が「View層は後段」とした判断と同じ
   理由で最後に回すのが妥当と考える

**T-045との違い**：今回はP-016/P-025のような個別の`docs/proposed.md`起票項目からの積み上げ
ではなく、「規模そのものの肥大化」という、より広い構造課題が起点になっている。増分γ・δは
T-045の増分A〜Dより一段大きい規模の作業になる見込みで、着手前に追加の構造調査（クラス内の
機能クラスタリング、既存ViewModelとの責務境界の再定義等）を挟む価値があると考える。

---

## 6. 未確認・次段候補

- App層全体のホットスポット計測はMainWindowViewModel.cs/MainWindow.xaml.csの2ファイルに
  焦点を絞った。他のView（`PartEditorCanvas.cs`734行等）・ViewModel（`FindViewModel.cs`
  242行等）は相対的に小規模だが、個別の設計品質（責務凝集度等）までは未調査。
- `MainWindowViewModel.cs`283メンバーの機能クラスタリング（どの塊が独立ViewModel化に
  適するか）は本報告では行っていない。増分γ着手前の調査候補として次段へ回す。
- P-050の実害確認（GroupFrameドラッグ後の行挿入/削除でVisual*Mm座標がズレるか）は
  未実施（コード読解のみ、実機確認は忍者の領分）。

---

## 出典・参照

- `docs/archive/ecad2-t045-structure-survey-onmitsu.md`（T-045当時の構造調査、本報告の基準）
- `docs/archive/ecad2-t045-implementation-plan-samurai.md`（T-045実装計画）
- `docs/archive/ecad2-t045-increment-b-review-onmitsu.md`・`-increment-b-fix-review-onmitsu.md`・
  `-increment-d-review-onmitsu.md`・`-addendum2-review-onmitsu.md`（T-045各増分レビュー）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（現状3395行、public180/private103メンバー）
- `src/Ecad2.App/MainWindow.xaml.cs`（現状4132行、ハンドラ143個）
- `git show 5f2ee6e:...`（T-045完了時点のファイルスナップショット、行数比較の基準）
- `docs/todo.md`（T-045完了以降のDoneタスク一覧、肥大化要因の推定）
- `docs/ecad2-proposed-pending-review-20260725-onmitsu.md`（本日の棚卸し、P-077/P-050の拾い直し）
