# 基本図形(Element)移動機能の調査(隠密、2026-07-14)

対象: 殿指摘「基本図形の移動もGuiEcadにはあったのでUIに未結線なだけでは」の裏付け調査。
GuiEcad原本(`C:\Users\kojif\Desktop\生産物\gui_ecad\`)実装確認＋ecad2 Core層調査。

## 結論(要約)

**殿の指摘のとおり、GuiEcadには基本図形のマウスドラッグ移動機能が確かに存在する。** ただし
ecad2側の状況は「Core完備・App未結線」パターンには**該当しない**——GuiEcad自身もCore層側に
移動専用ロジックを持たず、App層のマウスハンドラ+Undo個別コマンドのみで完結する軽量実装
だった(`ElementInstance.Pos`が単純setterのみだったのが元々の理由)。ecad2の`ElementInstance.
Pos`(`Element.cs:48`)も同型の単純setterのため、**Core層への追加実装はほぼ不要、App層の
新規実装のみで足りる**見立て。

## DoD1: GuiEcad側の実装確認

出典: `MainPage.Pointer.cs`(マウスハンドラ)・`Commands/ElementCommands.cs:122-140`
(`MoveElementCommand`)

- **マウスドラッグのみ**(キーボード操作は無し、`MainPage.KeyBindings.cs`grep確認、矢印キー等の
  移動バインドは0件)。
- 開始(`OnPointerPressed`、301-337行): 作画モード(選択・移動モード)中、要素をクリックすると
  `_moving = hitElem`・`_moveStartPos = hitElem.Pos`を記録。既に範囲選択中でその要素が選択集合に
  含まれる場合は一括移動(`_multiMoveOrigins`等)の起点にもなる。
- 移動中(`OnPointerMoved`、527-546行): マウス位置→グリッド座標変換後、
  `CellEmpty(row, col, _moving)`(占有チェック)を満たす場合のみ`_moving.Pos = new GridPos(row,
  col)`で**即座に位置更新**(リアルタイムプレビュー、Undo登録はまだ行わない)。占有先セルが
  埋まっていれば単に位置更新をスキップ(その場に留まる)。
- 確定(`OnPointerReleased`、744-745行): `_moving.Pos != _moveStartPos`(実際に動いていれば)
  `_history.Execute(new MoveElementCommand(_sheet, _moving, _moveStartPos, _moving.Pos))`で
  Undo/Redo登録。
- 複数選択の一括移動(710-745行)にも対応(要素・縦コネクタ・自由線・枠・接続点を`BatchCommand`で
  まとめてUndo登録)。ただし**ecad2には範囲選択機能自体が無い**(DoD2参照)ため、この部分は
  当面のスコープ外と考えられる。
- `MoveElementCommand`(`ElementCommands.cs:122-140`)自体はGuiEcad.Core層ではなく**App層の
  Commands配下**に存在し、中身は`Execute()`で`_element.Pos = _to`・`Undo()`で`_element.Pos =
  _from`という単純代入のみ。Core層に移動専用のドメインロジックは無い。

## DoD2: ecad2 Core層の確認結果

**該当ロジックは無い(家老軽grepと一致)。「Core完備・App未結線」パターンには該当しない。**

- `Ecad2.Core`内を`MoveElement`/`ElementDrag`/`DragElement`/`MoveSelected`でgrepし0件
  (今回改めて確認)。
- `ElementInstance.Pos`(`src/Ecad2.Core/Model/Element.cs:48`)は`public GridPos Pos { get; set;
  }`という単純setterのみ。GuiEcad側と全く同型で、Core層側に特別な検証・副作用は元々無い。
- 一方、ecad2は**既に画像(`ImageInsert`)・縦コネクタ・自由線・接続点のドラッグ移動機能を
  App層に確立済み**(`MainWindowViewModel.cs`の`IsDraggingImage`/`BeginDragImage`/
  `CancelDragImage`等、縦コネクタ側`IsDraggingConnector`等も同型)。基本図形(Element)だけが
  この確立済みドラッグ機構の対象から外れている状態。
- ecad2には**複数要素の範囲選択機能自体が存在しない**(`MainWindowViewModel.cs`を
  `SelectedSet`/`MultiSelect`/`SelectedElements`でgrepし0件)。GuiEcadの一括移動機能は
  ecad2の現行機能に対応物が無いため、実装するとしても単一要素移動が現実的なスコープ。

## DoD3: 影響範囲・実装方針の見立て

1. **Core層**: 追加実装ほぼ不要。`ElementInstance.Pos`は既にset可能。強いて言えば移動先の
   占有チェック(GuiEcadの`CellEmpty`相当)だが、ecad2には配置時の同種チェック
   (`ValidatePlacement`/`IsOccupied`、`MainWindowViewModel.cs:2167`付近)が既に存在するため、
   これを移動時にも再利用できる見込み。
2. **App層(新規実装)**: 画像ドラッグ(`BeginDragImage`/`IsDraggingImage`/`CancelDragImage`、
   `MainWindowViewModel.cs:1273-1329`付近)と同型のパターンを、要素(Element)向けに横展開する
   形が自然。GuiEcadのUndo個別コマンド(`MoveElementCommand`)方式ではなく、ecad2既存の
   `UndoManager.RecordSnapshot`方式(移動確定時に1回スナップショット)を踏襲すべき
   (T-086調査時と同様の設計差)。
3. **マウスハンドラ**: `LadderCanvasHost_PreviewMouseLeftButtonDown`/`MouseMove`/
   `PreviewMouseLeftButtonUp`(既存の縦コネクタ・画像ドラッグと同じイベント群)への横展開が
   必要。既存のドラッグ状態(コネクタ・配線分断・自由線・接続点・画像)との競合順序(どのドラッグ
   条件を先に判定するか)の整理が要る。
4. **CanEditDiagramガード【MUST横展開】**: 他の全ドラッグ操作と同様、テストモード中は移動禁止
   にすべき(T-061確立の統一ゲート)。
5. **Escapeキャンセル層**: `MainWindow.xaml.cs`のEscape多層処理(1291行〜)に、他のドラッグ系
   (`_connectorDragConsumedByEscape`等)と同型の「要素ドラッグ中のEscでキャンセル」層を追加する
   必要がある(既存6系統と同じ設計)。
6. **範囲選択・一括移動はスコープ外**: ecad2に範囲選択機能自体が無いため、GuiEcadの
   `_multiMoveOrigins`相当は今回の対象に含めるべきでない(範囲選択機能自体の新設は別タスク)。

## 出典

- GuiEcad: `MainPage.Pointer.cs:301-337,515-546,670-745`・
  `Commands/ElementCommands.cs:122-140`(`MoveElementCommand`)
- ecad2: `src/Ecad2.Core/Model/Element.cs:48`(`Pos`)・
  `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`(`IsDraggingImage`等、横展開の参考実装)

## 不明点

- GuiEcadの移動中占有チェック(`CellEmpty`)が、移動対象の要素自身の元位置をどう除外している
  か(自分自身への「移動」を占有扱いしていないか)は、`CellEmpty`メソッド自体を読んでいないため
  未確認(横展開時に要参照)。
- ecad2の`ValidatePlacement`/`IsOccupied`が、要素移動のような「元位置を空けつつ新位置を検証」
  という用途にそのまま使えるか(現状は新規配置専用の可能性)は未検証、実装時に要確認。
