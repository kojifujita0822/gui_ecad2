# T-105 着手前調査：GroupFrame矢印キー移動対応

侍記す。2026-07-21、家老采配（task_id=T-105）に基づく着手前調査。実装は未着手、本書は調査結果と実装方針案・キー割当叩き台の提示のみ。

## 1. GroupFrameの座標系・選択状態

- `GroupFrame`（`src/Ecad2.Core/Model/Element.cs`）は`GridPos TopLeft`（int Row/Column）＋`int Width/Height`のGrid座標系が主系統。`Visual*Mm`（double, mm自由座標）は旧ファイル読込互換専用で、現行の新規作成は常にnull（殿裁定「配置単位はグリッドセル単位」）。
- 選択状態は`SelectedFrame`（`GroupFrame?`、`MainWindowViewModel.cs`）。`SelectedElement`のようなSelectedCellからの自動算出プロパティではなく、`SelectedImage`と同型の**独立フィールド・単一選択・排他制御**。
- `SelectedCell`のsetterが無条件で`SelectedFrame = null`を実行し、他の選択種別（Connector/WireBreak/FreeLine/ConnectionDot/Image）と同じ排他グループに属する。Esc・Deleteとも既存経路で連携済み、矢印キー機能追加による選択状態管理への副作用はない。

## 2. 既存MoveSelectedXxxパターン比較

| メソッド | 引数型 | 境界方式 | Undo(RecordSnapshot) |
|---|---|---|---|
| MoveSelectedElement | int deltaRow/Column | 全否定（ValidatePlacement不成立ならfalse） | あり |
| MoveSelectedImage | double deltaXMm/YMm | クランプ(Math.Clamp) | あり |
| MoveSelectedConnector/WireBreak/FreeLine/ConnectionDot | int/double delta | クランプ | なし（点系・線系はUndo対象外が既存仕様） |

GroupFrameはGrid座標系（int）である点でMoveSelectedElementに、Undo対象である点（GroupFrameの他操作＝Delete/Rename/BorderStyle/ConfirmDragFrame等は全てRecordSnapshot対応）でもMoveSelectedElementに整合する。境界判定は既存のprivate staticヘルパー`IsFrameWithinGridBounds`（MainWindowViewModel.cs）がそのまま再利用可能（GroupFrameは他要素との重複を許容するため占有判定は不要）。

## 3. 実装方針案

- 新設メソッド`MoveSelectedFrame(int deltaRow, int deltaColumn)`をMoveSelectedElement型（移動前にRecordSnapshot→IsFrameWithinGridBoundsで全否定判定→TopLeft更新→MarkDirty）で実装。
- ドラッグ移動（BeginDragFrame/UpdateDragFrame/ConfirmDragFrame）とはメソッドを分離し、境界判定ヘルパーのみ共有する（Element/ElementDragの既存分離パターンに整合）。
- 呼び出し元は`MainWindow.xaml.cs`の無修飾矢印キーif-elseチェーン（Connector→WireBreak→FreeLine→ConnectionDot→Image→Cellの順）に、**Image判定の直後・最終else(Cellフォールバック)の直前**へ`else if (SelectedFrame is not null) MoveSelectedFrameByKey(e.Key);`を追加。既存の追加順（実装順）に倣うだけで優先順位の矛盾は生じない。
- ラッパー`MoveSelectedFrameByKey`は既存の各`MoveSelectedXxxByKey`と同型（key switchでViewModelのMoveSelectedFrame呼び出し、true時にRedrawCanvas）。

（参考・範囲外情報）矢印キー分岐チェーンには`ToolMode.PlaceFrame`（枠新規作成のドラフト中）のcaseが存在せず、`AdjustFrameDraft`はマウスドラッグ経由のみで矢印キーステップ非対応。これはT-105の主題（既存枠の移動）とは別の欠落であり、対応不要なら参考記載のみ。

## 4. キー割当案の叩き台（殿確認要・UI/UX分岐）

既存の一貫性の実態：

- **無修飾矢印キー**：Connector/WireBreak/FreeLine/ConnectionDot/Image（いずれも独立選択状態、SelectedCellと排他）
- **Ctrl+矢印キー**：Element専用（SelectedCellから自動算出される特殊プロパティのため、通常矢印キーとの衝突回避のために意図的に別モディファイヤへ分離）
- **Shift+矢印キー**：FreeLineの端点伸縮（移動ではなく形状変更の意味で使用中）

GroupFrameの`SelectedFrame`はConnector/WireBreak/FreeLine/ConnectionDot/Imageと同型（SelectedCellからの自動算出ではない独立フィールド）である。

- 案A（無修飾矢印キー、他の独立選択状態と同一の割当）：一貫性が最も高い。SelectedFrame選択中は無修飾矢印キーがCell移動でなくFrame移動に専有される（他要素選択時も同様の挙動のため対称）。
- 案B（Shift+矢印キー）：FreeLineの伸縮と同じキーだが意味が異なる（移動 vs 伸縮）。将来GroupFrameのサイズ変更を矢印キーで実装したくなった場合、キーの意味が競合する余地がある。
- 案C（Ctrl+矢印キー）：Element専用に予約された特殊モディファイヤの流用となり、Element用に設けた「衝突回避」という設計意図とは無関係にGroupFrameへ転用する理由が薄い。

侍所見：案Aが既存の独立選択状態群（Connector等）との一貫性が最も高く、技術的な障壁もない。ただし最終判断は殿確認【MUST】。

## 5. 調査結果の詳細出所

Exploreエージェントによる一次ソース確認（path:line根拠付き）。詳細な行番号根拠は本書作成時の会話ログ参照。
