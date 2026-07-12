# GuiEcad仕様書：結線操作

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）の結線操作実装をExplore委譲調査で纏め、
`docs/spec/ecad2-spec-wiring.md`（ecad2側、T-075起票）と比較可能な形で整理する。先行調査
`docs/archive/ecad2-t041-manual-wiring-survey-onmitsu.md`（2026-07-07）を土台に、対象ファイルは
最終コミット`b0d4ee7f`のまま変更なしと確認（実物再照合）の上で記述する。

対応するecad2側仕様書：`docs/spec/ecad2-spec-wiring.md`

---

## 1. 記入操作（キー割当・マウス操作）

**キーボード経路は4種すべて皆無**（実物再照合）。`MainPage.KeyBindings.cs:48-113`の`CommandDefs`に
F5〜F8（a接点/b接点/コイル/押しボタン）は登録されているが、縦コネクタ・自由線・接続点・配線分断に
対応するコマンド定義はない。ecad2のF9/Shift+F9/F10相当のキー割当はGuiEcadに一切存在しない。

操作はすべて**ツールバーのラジオボタン選択＋マウス**（`MainPage.xaml:386-419`、`GroupName="Tools"`）：

| 要素 | タグ | マウス操作 | 記入コマンド | 出典 |
|---|---|---|---|---|
| 縦コネクタ | `"connector"` | 列境界を上→下ドラッグ（セル中央0.5スナップ） | `AddConnectorCommand` | `MainPage.Pointer.cs:288-299,644-654` |
| 自由直線 | `"line"` | 2点ドラッグ（格子点スナップ、角度制約なし＝斜め線も可） | `PlaceFreeLineCommand` | `MainPage.Pointer.cs:232-243,599-608` |
| 接続点(●) | `"dot"` | 単クリック（格子点スナップ、即実行） | `PlaceDotCommand` | `MainPage.Pointer.cs:246-255` |
| 配線分断 | `"wirebreak"` | クリックでトグル（既存があれば削除） | `AddWireBreakCommand`/`DeleteWireBreakCommand` | `MainPage.Pointer.cs:259-272` |

**重要な差分**：シート種別（`Sheet.MainCircuit`）による記入ツールの切替はGuiEcadに存在しない
（実物再照合、`MainPage.xaml`全体で`MainCircuit`関連の`IsEnabled`バインディング0件）。主回路・
制御回路どちらのシートでも4種の記入ツールが常時同一に使える。ecad2は「F9=横線・接続点は主回路
限定、Shift+F9=縦分岐線・配線分断は制御回路限定」というシート種別切替を新設しており、これは
**GuiEcadに前例のない構造**。

---

## 2. 記入中の操作（確定/取消）

**矢印キーによるドラフト伸縮は存在しない**（実物再照合）。マウスドラッグの連続座標に追従するのみ。

- **確定**：マウスボタンを離した時点（`OnPointerReleased`）。縦コネクタは開始/終端行が異なる場合
  のみ実行、自由線は始点終点差0.01mm超のみ実行。接続点・配線分断はクリック即時実行。
- **取消**：専用の「Esc取消」処理はないが、結果として同等の挙動になる——`HandleEscape`
  （`MainPage.KeyboardMode.cs:120-131`）が「ツールが`Select`以外なら`ActivateTool("select")`」
  へ分岐、`ActivateTool`（`MainPage.Parts.cs:392-407`）が先頭で`ResetDragState()`を呼びドラフトを
  破棄する（「ツール切替の副作用」としての取消、ecad2のような専用実装ではない）。
- Undo/Redo：`IUndoCommand`パターンで全操作対応（`guiecad-spec-undo-redo.md`参照）。

---

## 3. ドラッグ移動・リサイズ

**4種とも「本体の平行移動」のみで、端点個別のリサイズという概念自体が存在しない**（実物再照合、
ecad2との明確な差分）：

| 要素 | ドラッグ挙動 | 出典 |
|---|---|---|
| 縦コネクタ | `Column`（水平位置）のみ変更、`TopRow`/`BottomRow`不変 | `MainPage.Pointer.cs:460-470,666-668` |
| 自由直線 | 始点終点を同一dx/dyで平行移動、長さ・角度不変 | `MainPage.Pointer.cs:472-482,679-686` |
| 接続点 | mm座標を平行移動のみ | `MainPage.Pointer.cs:485-493,689-695` |
| 配線分断 | ドラッグ移動自体が未実装（トグルのみ） | `MainPage.Pointer.cs:259-272` |

ヒットテストは選択モード中に**要素→縦コネクタ→枠→接続点→自由直線→画像**の優先順位で判定
（`MainPage.xaml.cs:581-697`、先行調査は実装箇所を`MainPage.Pointer.cs`と誤記していたため本調査で
訂正）。当たり判定は接続点/縦コネクタが`CellMm*0.25`、自由直線が`CellMm*0.3`＋点-線分距離計算。
接続点・自由直線はnearest-wins、縦コネクタは先頭一致——**ecad2の増分4裁定（縦コネクタ=先頭一致、
自由線/接続点=nearest-wins）と偶然にも同じ使い分けパターン**。

ecad2は端点個別のリサイズ操作（縦コネクタの`TopRow`/`BottomRow`個別変更、自由線の片端移動＋
`FreeLineMinLengthMm=1.0`保証）を実装しているが、**これはGuiEcadに前例のない新規機能**。

---

## 4. 削除の優先順位

専用消去ツールはなく、部品削除と同一の「選択＋Deleteキー」に統合（先行調査結論を再照合確認）。

- キー割当：`Delete`（`MainPage.KeyBindings.cs:56`）に加え**`Backspace`も同じ`DeleteSelected()`を
  呼ぶ**（`MainPage.KeyboardMode.cs:135-140`、固定キー扱い）——GuiEcad独自の2キー統合。
- 優先順位（`DeleteSelected()`、`MainPage.xaml.cs:513-571`）：範囲選択時は`BatchCommand`で
  要素→縦コネクタ→自由線→枠→接続点の順に一括削除。単一選択時は要素→縦コネクタ→枠→自由線→
  接続点→画像の排他分岐。
- **配線分断だけは`DeleteSelected()`経路に含まれない**——「選択」概念自体がなく、記入時のクリック
  トグルでのみ削除される。ecad2は`WireBreak`も他3種と同じDelete短絡OR連鎖に含まれる点で異なる。
- 右クリックメニューには要素の削除・コメント編集・機器名変更・縦コネクタ削除のみ。**自由線・
  接続点・配線分断には右クリック削除メニューがない**。

---

## 5. 自動配線（GuiEcadに前例あり、ecad2はこれを継承）

**GuiEcadにも自動横配線が存在し、制御回路シート限定という構図もecad2と同一**（実物再照合、
重要な発見）。`DiagramRenderer.Render`186-196行：`if (!sheet.MainCircuit)`で`DrawRails`・
`DrawBusLabels`・`DrawRungWires`を呼ぶ——主回路シートではこれらをスキップし`DrawFreeLines`
（ユーザー手動配置分）のみ描画。

この構造はecad2側`DiagramRenderer.cs`（`Render`189-199行・`DrawRungWires`297-365行・
`DrawFreeLines`436-456行）とほぼ同一の関数名・条件分岐であり、**ecad2は「自動横配線・制御回路
シート限定」という設計自体をGuiEcadから直接継承している**——ecad2独自の新設ではない。
`ecad2-spec-wiring.md`4節の「T-041起票時の殿裁定」はこの継承関係に触れていない点、留意されたい。

---

## 6. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | 出典 |
|---|---|
| Undo/Redo（コマンドパターン、記入・移動・削除すべて対応） | `Commands/ElementCommands.cs`全体 |
| 自由線の斜め線（角度制約なし） | `MainPage.Pointer.cs:232-243,599-608` |
| Backspaceキーも削除に割当 | `MainPage.KeyboardMode.cs:135-140` |
| 縦コネクタ削除の右クリックメニュー | `MainPage.ContextMenu.cs:67-75` |

### (2) ecad2のみにある機能

| 機能 | ecad2側出典 |
|---|---|
| F9/Shift+F9/F10のキーボード記入 | `ecad2-spec-wiring.md`1節 |
| 記入中の矢印キードラフト伸縮＋Enter確定/Esc取消 | 同0-1節 |
| 既存配線の端点個別リサイズ（縦コネクタ`TopRow`/`BottomRow`、自由線片端移動） | 同2節 |
| シート種別によるツール切替（F9/Shift+F9/F10の対象クラス変化） | 同0節 |
| Delete優先順位に`WireBreak`を含む4種統一 | 同3節 |

### (3) 両方にあるが挙動が異なる点

| 項目 | GuiEcad | ecad2 |
|---|---|---|
| 記入操作の入口 | マウスのみ | キーボード(F9/Shift+F9/F10)＋ツールバー併存 |
| 記入の確定方式 | マウスボタンリリース時（連続座標） | 矢印キー段階伸縮→Enter明示確定 |
| 既存配線ドラッグ | 平行移動のみ | 平行移動＋端点個別リサイズ |
| WireBreakの削除経路 | 記入トグルのみ、選択概念なし | 他3種と同じDelete短絡OR連鎖に含む |
| 自動配線の主体設計 | GuiEcadで先に確立 | 同一構造を継承（関数名・行構成がほぼ一致） |

---

## 出典

- GuiEcad: `MainPage.KeyBindings.cs:48-113`、`MainPage.Pointer.cs:232-299,437-504,599-654,666-695`、
  `MainPage.xaml.cs:513-571,581-697`、`MainPage.KeyboardMode.cs:120-140`、`MainPage.Parts.cs:392-407`、
  `MainPage.ContextMenu.cs:55-75`、`GuiEcad.Core/Rendering/DiagramRenderer.cs:73-101,186-196,295,428`、
  `GuiEcad.Core/Model/Sheet.cs:27`（Explore委譲調査、行番号は本文各所参照）
- ecad2: `docs/spec/ecad2-spec-wiring.md`（比較対象）
- 先行調査：`docs/archive/ecad2-t041-manual-wiring-survey-onmitsu.md`（土台とし、対象ファイル無変更を
  実物再照合、一部行番号誤記を訂正）

## 不明点

- `DrawRungWires`内部ロジックのGuiEcad/ecad2間での描画結果完全一致は未確認（関数構造・条件分岐の
  一致のみ確認済み）。
- ecad2仕様書側`MainWindowViewModel.cs`引用行番号に十数行のズレを検出（仕様書執筆後の加筆による
  シフトの可能性、ecad2側の再監査は本調査範囲外）。
- `WireBreak`に「選択」概念が本当に皆無か（`MainPage.xaml.cs`全体の網羅確認までは未実施）。
- `Simulation/`ディレクトリ内容は未調査、ecad2の「主回路シートはシミュレーション対象外」設計が
  GuiEcadから継承されたものかは不明（推測に留める）。
