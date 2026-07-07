# T-041 手動配線基盤（記入＋消去）調査（隠密）

> 2026-07-07 隠密調査。家老采配（殿裁定＝手動配線基盤を本命化、T-041前倒し・スコープに「消去」を
> 追加）。観点(1)〜(5)を静的コード調査＋Explore委譲（GuiEcad移植元）で検証した。前回の
> `docs/ecad2-t040-wire-survey-onmitsu.md`（F9/sF9「記入」のみの規模調査）を土台に、今回は
> 「消去」を含めた統合設計の材料を厚くする。急がずとも良いとの指示のため、判断材料の列挙を優先し、
> 実装方式の断定は避けた。

---

## 総括

- **ecad2側の現状**：`VerticalConnector`/`FreeLine`/`ConnectionDot`/`WireBreak`のいずれについても、
  「記入」操作は前回調査（T-040）通り皆無。今回新たに確認した**「消去」操作も皆無**——既存の
  `DeleteSelectedElement()`（T-017、Deleteキー）は`sheet.Elements`（配置要素）専用で、この4種の
  手動配線プリミティブには一切触れない。加えて、これら4種には**「選択」という概念自体がApp層に
  存在しない**（`SelectedCell`は`ElementInstance`探索にのみ使われる）。
- **GuiEcad移植元の設計**：記入はマウスドラッグ（縦コネクタ・自由線）または単クリック（接続点）が
  基本、キーボード経路は無し。消去は**専用消去ツールではなく、共通Selectツール＋ヒットテストに
  よる選択＋Deleteキー**という「部品削除と同型」の設計（複数選択・矩形選択にも対応）。唯一
  `WireBreak`だけは「記入と消去が同一クリックのトグル」という例外的設計。
- **Core層データモデル**：4クラスとも単純な値保持クラス（Id無し）で、`List<T>.Remove(参照)`による
  削除自体に技術的障壁は無い（GuiEcadもコマンドオブジェクトの中身は結局同種の削除）。ただし
  **ecad2にはUndo/Redo基盤が存在しない**（設計方針として意図的に不採用、後述）ため、GuiEcadの
  コマンドパターン（`IUndoCommand`）をそのまま移植するのは既存方針と不整合。「記入・消去とも
  直接List操作＋MarkDirty()」という、ecad2の既存OR自動生成・要素削除と同じ流儀に揃えるのが自然。
- **設計上の本当の空白**：技術的な障壁は小さいが、「連続する自由線群からどの1本を選ぶか」という
  **ヒットテスト（当たり判定）の新規設計**と、「記入と消去をどう1つの操作系にまとめるか」という
  **UI/UX判断**が主な検討課題である。(5)のキー操作案は必ず複数案として家老・殿へ委ねる。

---

## 観点(1): ecad2現状に縦コネクタ・横線の削除手段があるか —— **無し（確認済み）**

- `MainWindowViewModel.DeleteSelectedElement()`（`MainWindowViewModel.cs:268-294`、T-017）は
  `SelectedCell`位置の`ElementInstance`を`sheet.Elements`から削除するのみ。`sheet.Connectors`・
  `sheet.FreeLines`・`sheet.ConnectionDots`・`sheet.WireBreaks`への言及は無い（grep確認：これら
  4フィールドへの`.Remove(`呼び出しはリポジトリ全体で0件）。
- `MainWindow.xaml.cs:333-338`の`Key.Delete`ハンドラも`DeleteSelectedElement()`を呼ぶのみで、
  縦コネクタ等の削除分岐は存在しない。
- `ToolMode`（`ToolState.cs:12`）に`PlaceConnector`/`PlaceLine`/`PlaceDot`/`PlaceWireBreak`は
  **列挙値として定義済みだが未使用**（前回T-040調査で確認済み、今回`grep`で再確認——
  `MainWindow.xaml`・`MainWindow.xaml.cs`のいずれにもこれらの値を参照する実装コードは無い）。
- **選択モデルの空白**：`SelectedCell`（`MainWindowViewModel.cs:150-165`）は`GridPos`単位の選択で、
  `SelectedElement`（171-172行目）は`sheet.Elements.FirstOrDefault(el.Pos == pos)`により導出される。
  `VerticalConnector`等の4クラス（`Element.cs:77-140`）はいずれも`Id`フィールドを持たず、
  「今どの縦コネクタ／自由線が選択されているか」を表現するプロパティはViewModelのどこにも無い。
  → **削除機能を作る前に、まず「選択」という概念自体を新設する必要がある**（これが実装規模の
  主要因）。

---

## 観点(2): GuiEcad移植元の手動配線（記入・消去）の実装・操作方式 —— **Explore委譲調査で確認**

Exploreエージェントが`C:\Users\kojif\Desktop\生産物\gui_ecad\src`を調査した結果（要約、詳細は
エージェント報告のファイル:行番号を参照）：

### 記入（4種類、いずれも共通ツール切替方式）

| 要素 | ツールモード | 操作方式 | 確定コマンド |
|---|---|---|---|
| 縦コネクタ | `PlaceConnector`（タグ`"connector"`） | マウスドラッグ（列境界スナップ、セル中央にもスナップ可） | `AddConnectorCommand`（`MainPage.Pointer.cs:643-653`） |
| 自由直線 | `PlaceLine`（タグ`"line"`） | マウス2点ドラッグ（格子点スナップ`SnapLine`） | `PlaceFreeLineCommand`（`Pointer.cs:598-608`） |
| 接続点(●) | `PlaceDot`（タグ`"dot"`） | **単クリック**（格子点スナップ後即実行） | `PlaceDotCommand`（`Pointer.cs:245-255`） |
| 配線分断 | `PlaceWireBreak`（タグ`"wirebreak"`） | **クリックでトグル**（既存があれば削除、無ければ追加） | `AddWireBreakCommand`/`DeleteWireBreakCommand`（`Pointer.cs:257-271`） |

**キーボード経路は4種とも無し**（F9/Shift+F9のキーバインドはGuiEcad全ソースで0件、ツールバーの
ラジオボタン選択＋マウス操作のみ）。ecad2で殿要望のF9/sF9キーバインドを実現するには、GuiEcadに
前例のない**新規のキーボード駆動フロー**を設計する必要がある（前回T-040調査でも既出の論点）。

### 消去 —— **専用消去ツールは無く、部品削除と同型の「選択＋Deleteキー」方式**

- `WireBreak`（記入と同一クリックでトグル）を**除く**3種（縦コネクタ・自由線・接続点）は、
  共通のSelectツール中に**ヒットテスト関数**（`HitTestConnector`/`HitTestFreeLine`/`HitTestDot`、
  `Pointer.cs:339,354,381`）でクリック選択し、単一選択なら`DeleteConnectorCommand`/
  `DeleteFreeLineCommand`/`DeleteDotCommand`、矩形範囲選択で複数選択済みなら`BatchCommand`で
  一括削除（`MainPage.xaml.cs:511-571`、キーバインドは`VirtualKey.Delete`）。
- 「なぞって消す」操作は無い（WireBreak以外）。
- **重要**：GuiEcadの削除操作は「部品(Element)削除と同じDeleteキー・同じSelectツール」に統合
  されている。専用の「消去ツール／消去キー」は存在しない。今回の家老采配「消去を追加」を実現する
  際、GuiEcadに倣うなら**既存の部品削除（Deleteキー）と統合する方向**が移植元の設計思想に忠実。

### データモデル（前回T-040調査から再確認、変更なし）

`FreeLine`（mm実座標`X1,Y1,X2,Y2`+`LineStyle`）・`ConnectionDot`（mm実座標`X,Y`）・
`VerticalConnector`（グリッド座標`Column`0.5刻み+`TopRow`+`BottomRow`）・`WireBreak`
（グリッド座標`Boundary`セル中央+`Row`）。いずれも`Id`フィールドは無い。

### Undo/Redo —— **GuiEcadはコマンドパターン、ecad2は不採用（既存の設計判断）**

GuiEcadは`IUndoCommand`（`Commands/IUndoCommand.cs`）+`CommandHistory`によるコマンドパターンで、
Add/Delete/Move各操作が専用コマンドクラス（`ElementCommands.cs`の`AddConnectorCommand`/
`DeleteConnectorCommand`等）として実装されUndo/Redoに対応する。**ecad2はUndo機能自体を意図的に
不採用**としている（`MainWindowViewModel.cs:81-85`のコメント「ecad2はUndo機能自体が未実装なため、
変更操作の入口で明示的にMarkDirty()を呼ぶ方式を採る」）。これはGuiEcadのUndo深度依存Dirty判定が
構造的欠陥だった反省（`docs/ecad2-guiecad-code-survey-onmitsu.md` T-024節）を踏まえた設計方針。
→ **T-041の記入・消去は、GuiEcadのコマンドオブジェクトをそのまま移植するのではなく、ecad2の
既存OR自動生成・要素削除と同じ「直接List操作＋MarkDirty()」の流儀に揃えるのが整合的**と考える。

---

## 観点(3): T-041当初スコープ（F9/sF9記入）と新規「消去」の統合案 —— **3案を提示**

GuiEcadの前例（観点2）を踏まえ、記入と消去の統合方式には少なくとも3つの設計軸がある。いずれも
UI/UX判断を伴うため、家老が殿へ諮る際の材料として並記する（優劣の断定はしない）。

### 案A：部品削除（既存Deleteキー、T-017）と統合する —— GuiEcad方式に忠実

Selectモード中に縦コネクタ・自由線・接続点をクリックで選択（新規ヒットテスト要）→既存の
`Key.Delete`ハンドラ（`MainWindow.xaml.cs:333`）を拡張し、「選択中の部品が無ければ、選択中の
配線プリミティブを見て分岐削除」とする。長所：キー操作の一貫性が高い（部品も配線も同じDelete）。
短所：配線プリミティブ用の「選択」概念（クリック選択・ハイライト表示）を新規に設計する必要がある
（実装コストの主要因、GuiEcadの`HitTestConnector`等に相当するものが必要）。

### 案B：記入ツール自体にトグル（消去）機能を持たせる —— GuiEcadのWireBreak方式を全種へ拡張

F9(横線)/sF9(縦分岐線)ツール選択中、既存の線・コネクタをクリックすると削除、空白をクリック/
ドラッグすると新規追加、という「記入と消去が同一ツール・同一操作」の設計。長所：新規の「消去
専用モード」も「配線プリミティブの選択状態」も不要（ヒットテストは記入ツール内部で完結）。
短所：GuiEcad自身も縦コネクタ・自由線・接続点の3種にはこの方式を採用しておらず（WireBreak限定）、
「同じクリックで記入にも消去にもなる」操作は誤操作（消したいだけなのに新規線を引いてしまう等）の
リスクをどう抑えるか検討が要る。

### 案C：専用の消去ツール／キーを新設する

F9/sF9とは別に「消去」専用のツールモード・キー（例：GX Works3ツールバー参考画像
`docs/images/t040-gx-ladder-toolbar-reference.png`の末尾付近に、記入系アイコン群とは別の
アイコン（削除系と推測されるが今回未検証、要確認）が存在するように見受けられる）を新設し、
これを選択中はクリックした配線プリミティブが即削除される、という設計。長所：記入と消去の操作が
明確に分離され誤操作が起きにくい。短所：キーバインド・ツールボタンが1種類増える（配置の
一貫性・覚えやすさとのトレードオフ）。

**いずれの案でも、ヒットテスト（クリック位置→どの縦コネクタ／自由線／接続点か）の新規設計は
共通して必要**（案Bも記入ツール内部でヒットテストが要る点は同じ）。

---

## 観点(4): Core層データモデルが手動記入・消去に耐えるか —— **技術的障壁は小さい。設計課題はヒットテストと選択モデル**

- **記入**：`sheet.Connectors.Add(new VerticalConnector{...})`は既にOR自動生成（T-044）で使われて
  いる既存パターンそのもの。`FreeLine`/`ConnectionDot`/`WireBreak`も同型の単純な`List.Add`で足りる
  （`Sheet.cs:12,14,20,22`に既にリストとして保持済み）。**新規データ構造は不要**。
- **消去**：4クラスとも`Id`フィールドが無いが、これは削除の障害にはならない——ヒットテストで
  `sheet.Connectors`等を走査して「一致したインスタンスの参照」を直接得れば、`List<T>.Remove(参照)`
  は曖昧さ無く機能する（C#のクラスは既定で参照同一性、GuiEcadの各`DeleteXxxCommand`も突き詰めれば
  同種の削除である）。**Idを新設する必然性は無い**（将来、他データからの相互参照が必要になれば
  別途検討）。
- **本当の技術課題はヒットテスト**：`VerticalConnector`/`WireBreak`はグリッド座標（Row/Column、
  0.5刻み含む）のため、既存の`LadderCanvas.ToGridPos`（`LadderCanvas.cs:109-115`、クリック位置→
  `GridPos`）の仕組みを拡張し「列境界への近さ」を判定する形で流用しやすい。一方`FreeLine`/
  `ConnectionDot`は**mm実座標**（グリッド非依存）のため、現行の「クリック→グリッドセル」変換
  だけでは対応できず、**点と線分の距離計算（幾何ヒットテスト）という、ecad2に現状存在しない種類の
  当たり判定ロジックを新規に実装する必要がある**（GuiEcadの`HitTestFreeLine`相当、Explore報告では
  具体的な距離しきい値等の実装詳細までは確認できていない・追加調査の余地あり）。
- **主回路(`Sheet.MainCircuit`)モードとの関係**：`Sheet.cs:25-27`のコメント「主回路モードは
  左右母線・自動横配線を描かず自由直線で結線する」から、**横配線・縦分岐線のうち少なくとも一部
  （主回路向け）は`FreeLine`で表現される設計が既に敷かれている**。todo.mdのT-041項目タイトルが
  「主回路用の横配線・縦分岐線」であることから、殿要望は主回路モード（`MainCircuit=true`）の
  `FreeLine`ベースの記入・消去を主眼としている可能性が高い（推測、確定は殿裁定を要する）。
  この場合`VerticalConnector`（制御回路グリッド専用、OR自動配線が使用中）とは**別データ経路**
  になり、T-044のOR自動配線ロジックとは干渉しない（推測、影響範囲は限定的と考えられる）。

---

## 観点(5): キー操作方式の複数案 —— **必ずUI/UX分岐として殿へ諮る前提で列挙**

前回T-040調査時点の指摘通り、GuiEcadにも記入用キーバインドは存在せず、ecad2のF9/sF9は完全新規
設計となる。「消去」を含めた案を以下に列挙する（観点3の案A/B/Cと組み合わせ可能）：

1. **F9(記入)/sF9(記入・別種)＋既存Deleteキー拡張（案Aと組み合わせ）**：記入は新規キー、消去は
   部品と共通のDeleteキー。キー体系がシンプルだが、配線プリミティブの選択操作（クリック選択・
   矢印キーでの選択移動等）を別途設計する必要がある。
2. **F9(横線)/sF9(縦分岐線)ツール自体をトグル方式に（案Bと組み合わせ）**：ツール選択中は記入・
   消去とも同じF9/sF9キーで完結。新規キー数は最小だが誤操作リスク（観点3参照）。
3. **F9/sF9(記入)＋Shift+Delete等の専用消去キー、または専用消去ツールボタン（案Cと組み合わせ）**：
   記入・消去が明確に別キー・別ツールで分離。誤操作は起きにくいが覚えるキーが増える。
4. **右クリックメニューを消去の補助手段にする**：`docs/ecad2-ui-ux-design-brief.md:103`
   「右クリックは補助的、ツールバー/キーボードが中心」という既定方針に沿い、上記1〜3のいずれか
   を主手段としつつ、右クリック→「この配線を削除」を補助的に併設する（キーボードファースト方針
   との整合は保てる）。

**キーボードファースト方針（design-brief）との整合**は4案とも保てるが、「新規に覚えるキーの数」
「誤操作のしやすさ」「GuiEcad前例との乖離度」のトレードオフが異なるため、断定はせず家老経由で
殿へ複数提示することを推奨する。

---

## 忍者・家老への申し送り

- 本調査は静的コード調査のみ（実機確認なし）。ヒットテスト（観点4、特にFreeLineの幾何当たり判定）
  の実装可否・使用感は、侍のプロトタイプ実装後に忍者実機確認で検証するのが妥当と考える。
- 観点(3)(5)はいずれもUI/UX判断（複数案の分岐）を含むため、家老の裁量では決めず殿へ諮られたい
  （`feedback_route_design_decisions_to_user`の既定方針通り）。
- 観点(4)で触れた「T-041の主眼は主回路(FreeLine)か制御回路(VerticalConnector)か」という前提も
  未確定（推測に留まる）。これも殿確認が先に必要な論点と考える。

---

## 出典・参照

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（150-172行目`SelectedCell`/`SelectedElement`、
  268-294行目`DeleteSelectedElement`、81-85行目Undo不採用の設計コメント）
- `src/Ecad2.App/MainWindow.xaml.cs`（333-338行目`Key.Delete`ハンドラ）
- `src/Ecad2.App/ViewModels/ToolState.cs`（12行目`ToolMode` enum、未使用値）
- `src/Ecad2.App/Views/LadderCanvas.cs`（107-115行目`ToGridPos`）
- `src/Ecad2.Core/Model/Element.cs`（77-140行目`FreeLine`/`ConnectionDot`/`VerticalConnector`/
  `WireBreak`）・`src/Ecad2.Core/Model/Sheet.cs`（4-28行目、各リスト保持・`MainCircuit`）
- `docs/ecad2-t040-wire-survey-onmitsu.md`（前回調査、記入のみの規模調査）
- `docs/ecad2-ui-ux-design-brief.md`（103行目、右クリック補助方針）
- `docs/todo.md`（T-041項目、F9/sF9・主回路用の記述）
- `docs/images/t040-gx-ladder-toolbar-reference.png`（GX Works3ツールバー参考画像、末尾アイコンの
  用途は今回未検証）
- Explore委譲調査（GuiEcad `C:\Users\kojif\Desktop\生産物\gui_ecad\src`）：`MainPage.Tools.cs`・
  `MainPage.Pointer.cs`・`MainPage.xaml.cs`・`MainPage.KeyBindings.cs`・`Commands/ElementCommands.cs`・
  `Commands/IUndoCommand.cs`・`Commands/CommandHistory.cs`・`GuiEcad.Core/Model/Element.cs`・
  `docs/drawing-spec.md`・`docs/data-model.md`
