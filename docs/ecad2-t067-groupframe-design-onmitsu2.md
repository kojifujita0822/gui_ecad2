# T-067 GroupFrame（グループ枠）作成・編集UI：着手前設計整理

調査者: 隠密2　最終更新: 2026-07-11

家老采配（殿しばし席を外されるにつき家老裁量で沙汰、T-069調査で「GroupFrame系はT-067前提」と
指摘した当のタスク）。GuiEcad実物のGroupFrame操作体系を調査し、ecad2 App層に必要な新規実装項目・
P-050対処要否・UI/UX分岐論点を整理する。**実装は行っていない、着手前調査のみ**。

---

## DoD(1): GuiEcad実物のGroupFrame操作体系

`GuiEcad.App/MainPage.Pointer.cs`（作成・移動、Read済）・`MainPage.ContextMenu.cs`（削除・線種、
前回T-069調査でRead済）を実物照合。

| 操作 | GuiEcadの実装 |
|---|---|
| 作成 | 「枠」ツール選択→キャンバス上でマウスドラッグ（**mm連続座標、グリッド非依存の自由配置**）→
  リリースで確定（半セル未満の極小ドラッグは誤操作とみなし無視）。`TopLeft`/`Width`/`Height`
  （グリッド近似値、行シフト計算のフォールバック用）と`VisualXMm/YMm/WidthMm/HeightMm`
  （実表示に使う絶対mm座標）を**両方同時に設定**。`AddFrameCommand` |
| ラベル編集 | 既存枠を**ダブルクリック**→`ShowFrameLabelEditor`（キャンバス座標から算出した位置に
  インラインテキストボックスをオーバーレイ表示）→Enter/Tabで確定・Escでキャンセル→
  `RenameFrameCommand` |
| 移動 | 既存枠をクリック（選択）→ドラッグ→`VisualXMm/YMm`を更新→リリース時に位置変化があれば
  `MoveFrameCommand`確定 |
| 枠線スタイル変更 | 右クリックメニュー「線種」サブメニュー（実線/破線/点線）→
  `SetFrameBorderStyleCommand`（前回T-069調査で確認済み） |
| 削除 | 右クリックメニュー「削除」→`DeleteFrameCommand`（前回T-069調査で確認済み） |

**最大の特徴**：GroupFrameはGuiEcadの全機能中で唯一「グリッドに縛られない自由なmm連続座標での
配置」を行う（他の要素・自由線・接続点はいずれもグリッド/格子点スナップ前提）。

---

## ecad2側の現状（Core層完備の再確認、前回T-069/T-055調査からの追加確認込み）

- `GroupFrame`モデル（`src/Ecad2.Core/Model/Element.cs:143-163`）はGuiEcadと完全一致（Label/TopLeft/
  Width/Height/BorderStyle/`Visual*Mm`4種）。
- `DiagramRenderer.DrawFrames`（`DiagramRenderer.cs:485-519`）も**Visual*Mm優先ロジックまで完全同型**
  で実装済み（`VisualXMm ?? X(TopLeft.Column)`等のフォールバック含む）——追加確認点。
- `RowOps.InsertRow/DeleteRow`（`RowOps.cs`）は`TopLeft.Row`/`Height`のシフト処理まで済み
  （前回T-069/T-055調査で確認済み）だが、**`Visual*Mm`4種は追随しない＝これがP-050の実体**。
- App層：生成・編集・移動・削除、いずれの経路も皆無（P-054記載どおり、変更なし）。

---

## DoD(2)/(3): P-050対処要否は「枠の配置単位」という設計分岐そのものに直結

P-050（`RowOps.InsertRow/DeleteRow`がGroupFrameの`Visual*Mm`を未追随）は、対処要否が独立した
バグ修正ではなく、**GroupFrameの配置単位をどう設計するかという分岐に従属する**：

- **案X（グリッドセル単位限定）**：`Visual*Mm`を一切使わない新規実装にする。この場合
  **P-050は構造的に非該当**になる（そもそも追随すべきVisual座標が存在しない）。ただし枠の位置・
  サイズが必ずセル境界に揃う制約が生じ、GuiEcadより自由度が下がる。
- **案Y（GuiEcad同様mm自由配置）**：`Visual*Mm`を使う。表現力はGuiEcadと同等だが、**P-050の対処
  （`RowOps.InsertRow/DeleteRow`に`Visual*Mm`のシフト処理を追加）が必須になる**。

**この分岐は、後述する作成操作方法の論点（マウスドラッグ vs キーボードステップ）とも自然に対応する**
——キーボードステップ方式は必然的に「セル単位の整数ステップ」に近い設計になりやすく、案Xと
親和性が高い。マウスドラッグ方式はmm連続座標を扱いやすく、案Yと親和性が高い。

---

## DoD(2): App層に必要な新規実装項目

1. **枠作成ツールモード新設**（`ToolMode.PlaceFrame`相当、ecad2の既存`ToolMode`列挙に倣う）
2. **作成操作のUI実装**（マウスドラッグ方式かキーボードステップ方式か、UI/UX論点1）
3. **Undo対応**：新規コマンドクラス不要——T-064調査と同じ結論、ecad2の`UndoManager.RecordSnapshot`
   パターンを操作直前に呼ぶだけで足りる（GuiEcadの`IUndoCommand`方式は不要）
4. **ラベル編集UI**（インラインオーバーレイ方式かプロパティパネル方式か、UI/UX論点2）
5. **選択機構への統合**：`SelectedElement`/`SelectedConnector`等と同様の排他選択プロパティ
   （`SelectedFrame`相当）を`MainWindowViewModel`へ新設。枠のヒットテストも新設
6. **移動操作**（ドラッグかキーボード方向キーか、UI/UX論点3）
7. **枠線スタイル変更UI**：右クリックメニュー（T-069で新設予定のヒットテスト基盤に相乗り）
8. **削除**：右クリックメニュー（T-069のGroupFrame系そのもの——T-069はT-067完了を前提としていた
   ため、着手順序はT-067→T-069GroupFrame系という関係が変わらず確認された）

---

## DoD(4): UI/UX分岐となりうる論点（選択肢化、殿確認要）

### 論点1: 枠の作成操作方法（案X/Yと連動）

| 案 | 内容 | 配置単位との親和性 |
|---|---|---|
| A | GuiEcad同様マウスドラッグ（mm連続座標の自由配置） | 案Y（mm自由配置）と自然に組み合う |
| B | ecad2の既存`FreeLine`と同型のキーボードステップ方式（`BeginFreeLineDraft`/
    `MoveFreeLineDraftEnd`/`ConfirmFreeLineDraft`と同構造——セル選択→ツールモード→矢印キーで
    右下角を段階的に広げる→Enterで確定）。**ecad2の「キーボードファースト」方針・既存の
    自由線実装パターンと整合** | 案X（グリッドセル単位）と自然に組み合う |
| C | 両方併用 | — |

### 論点2: 枠の配置単位（P-050対処要否に直結、DoD3参照）

| 案 | 内容 | P-050対処要否 |
|---|---|---|
| X | グリッドセル単位限定（`Visual*Mm`不使用の新規実装） | **不要**（構造的に非該当） |
| Y | GuiEcad同様mm自由配置（`Visual*Mm`使用） | **必須**（`RowOps`拡張が要る） |

### 論点3: ラベル編集のUI方式・トリガー

| 案 | 内容 |
|---|---|
| A | GuiEcad同様、ダブルクリックでキャンバス上にインラインテキストボックスをオーバーレイ表示 |
| B | ecad2の既存パターン（`DeviceNameBox`等）に倣い、右パネルのプロパティ領域で編集（枠選択中に表示） |
| C | 右クリックメニュー「ラベル編集」経由のみ（T-069のGroupFrame系メニューと統合、トリガーの
    二重化を避ける） |

### 論点4: 移動操作方式

| 案 | 内容 |
|---|---|
| A | マウスドラッグ（GuiEcad方式） |
| B | キーボードの矢印キーで平行移動（既存のFreeLine/縦コネクタの平行移動パターンと統一） |
| C | 両方併用 |

---

## 出典一覧

- `docs/todo.md`（T-067・T-069節）
- `docs/proposed.md`（P-050・P-054、Read全文該当箇所）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.Pointer.cs:154-829`
  （枠作成・移動・ラベル編集、Read）
- `C:/Users/kojif/Desktop/生産物/gui_ecad/src/GuiEcad.App/MainPage.ContextMenu.cs`
  （線種変更・削除、前回T-069調査でRead済、再利用）
- `src/Ecad2.Core/Model/Element.cs:143-163`（GroupFrameモデル、Read）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs:483-519`（DrawFrames、Read）
- `src/Ecad2.Core/Model/RowOps.cs`（前回T-069調査でRead済、再利用）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1095-1168`（FreeLineDraft、キーボード操作の
  既存参考パターン、Read）
- `docs/ecad2-t069-context-menu-design-onmitsu2.md`（本セッション前回調査、GroupFrame系の依存関係）
- `docs/ecad2-t064-image-insert-design-onmitsu2.md`（本セッション前回調査、Undo基盤の結論を再利用）

## 不明点

なし（本調査の範囲内では未解決の疑問点なし。ただし論点1〜4は殿確認が必須の設計判断であり、
不明点ではなく選択待ちの論点として扱う）。

## 派生提案の有無

なし（家老采配の範囲内で完結）。
