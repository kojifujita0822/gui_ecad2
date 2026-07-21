# ecad2 仕様書：結線操作

T-075（殿裁定、2026-07-11起票）体系の第2号。実装コード・殿裁定記録（`docs/todo.md`/
`docs/todo-archive.md`）・忍者実機検証記録（`docs-notes/`・`docs/`配下のT-041検証書群）を突き合わせ、
「仕様として確定している挙動」を出典付きで明文化する。シート種別（主回路/制御回路）によるボタン
有効/無効の対応表は`docs/ecad2-spec-sheet-document.md`3節を参照。

---

## 0. 4つの結線プリミティブと5つのツールバーボタン

**実装クラスは4種**（`src/Ecad2.Core/Model/Element.cs`）。ツールバーボタンは5個（横線・縦線・
縦分岐線・接続点・配線分断）だが、これはシート種別で切り替わる**同一2ショートカット
（F9・Shift+F9）+専用1ショートカット（F10、シート種別で機能が変わる）**に対応するボタン露出の
数であり、モデルクラスの種類数（4）とは一致しない点に注意。

| クラス | 座標系 | 対応するボタン（シート種別） |
|---|---|---|
| `VerticalConnector`（縦コネクタ） | グリッド座標（`Column`/`TopRow`/`BottomRow`） | 縦分岐線記入(Shift+F9)＝制御回路限定 |
| `WireBreak`（配線分断） | グリッド座標（`Boundary`/`Row`） | 配線分断記入(F10)＝制御回路限定 |
| `FreeLine`（自由線） | **mm実座標**（`X1Mm/Y1Mm/X2Mm/Y2Mm`、水平・垂直のみ、斜め線はスコープ外） | 横線記入(F9)・縦線記入(Shift+F9)＝主回路限定 |
| `ConnectionDot`（接続点） | **mm実座標**（`XMm/YMm`） | 接続点記入(F10)＝主回路限定 |

`VerticalConnector`のみ`DeepClone`メソッドが未定義（他3クラスとの非対称、**不明点**：意図的な設計か
実装漏れかは未調査）。

### キー割当の裁定経緯

殿裁定（2026-07-07）：**案A＝線系はF9/Shift+F9、点系は専用キーF10に統一**（当初は別案だったが
「F10統一でいいよ」と訂正）。F9無修飾は常に`FreeLine`横線（`MainWindow.xaml.cs:808-813`、制御回路
シートでは「自動横配線があるため対応する手動記入は無い」とコメント明記）。Shift+F9とF10は
`sheet.MainCircuit`で分岐（815-838行）。

---

## 1. 記入フロー

### ツール状態との関係

- `VerticalConnector`記入中：`Tool.Mode = ToolMode.PlaceConnector`（`MainWindowViewModel.cs:1026`）。
  矢印キーでドラフトを伸縮→Enter確定（`ConfirmConnectorDraft`, 1057行）／Esc取消。
- `FreeLine`記入中：`Tool.Mode = ToolMode.PlaceLine`（1123行）。同様に矢印キー伸縮→Enter確定
  （`ConfirmFreeLineDraft`, 1144行）／Esc取消。
- **`WireBreak`/`ConnectionDot`は即時記入（確認フェーズなし）**——`ToolState.cs`に`PlaceDot`/
  `PlaceWireBreak`のenum値は定義されているが、`MainWindowViewModel.cs`/`MainWindow.xaml.cs`の
  いずれにも代入箇所がなく**未使用（デッドコード）**と判明。ツールバーボタン・F10キーいずれも
  共通の`Try系`メソッド（`TryBeginConnectorDraft`/`TryBeginFreeLineDraft`/`TryPlaceConnectionDot`/
  `TryPlaceWireBreak`）を呼ぶ共通実装（T-047、`MainWindow.xaml.cs:1431-1434`）。

### F10のWPF既知の落とし穴（実装バグ・修正済み）

F10単体キーは`WM_SYSKEYDOWN`扱いになるというWPF既知仕様のため、通常の`KeyDown`ではなく
`e.SystemKey`で拾う必要がある（`MainWindow.xaml.cs:827-833`コメント）。**忍者実機検証で、この
対応前のビルドではF10押下時に`WireBreak`が記入されずWPF標準メニューへフォーカスが奪われる
重大バグを発見**（`docs/archive/ecad2-t041-increment123-ninja-verification.md:25-30`）、`abddba3`で修正
済み。

### 記入時の制約

- `FreeLine`は水平・垂直のみ（斜め線はスコープ外、殿裁定2026-07-07）。
- `WireBreak`は同一箇所への重複記入を「既に配線分断があります」で防止
  （実機確認済み、`docs/archive/ecad2-t041-increment123-ninja-verification.md:25-30`）。
- 記入中のシート切替は自動的にSelectツールへ戻り、誤生成なし（同ファイル`:19-24`）。

### 記入中（ドラフト中）の他操作ブロック（2026-07-21追記、T-091・T-092反映）

- **T-091（2026-07-14）**：F5〜F10のグローバルショートカット（配置系）は、縦コネクタ/自由線/
  画像挿入のドラフト記入中は無反応になる（`HasAnyDraft`ガード、F5〜F10全9箇所へ適用）。ドラフト
  中に配置バーが誤って開き記入内容が宙に浮くバグの修正。
- **T-092（2026-07-15）**：ドラフト記入中はAddRow/DeleteRow（行の追加/削除）・Undo/Redoも同様に
  無効化される（`!HasAnyDraft`ガード、ブロック方式。詳細は`docs/spec/ecad2-spec-undo-redo.md`
  4節参照）。ドラフトが指す行・シートの前提が崩れることによる無警告ズレ確定を防ぐ。

---

## 2. ドラッグ操作

4種すべてに本体移動を実装。線系2種（`VerticalConnector`/`FreeLine`）はさらに端点リサイズを実装
（点系2種は端点概念がないため本体移動のみ）。

- **`VerticalConnector`**（`MainWindowViewModel.cs:330-516`）：本体移動はTop/BottomRowの間隔を
  保ったままクランプ、列位置も追従（P-039殿裁定で列ドラッグ対応）。端点リサイズは`TopRow<BottomRow`
  不変条件を維持。矢印キー等価操作あり（`MoveSelectedConnector`/`MoveSelectedConnectorColumn`/
  `ResizeSelectedConnectorEndpoint`、Tab+Shift+矢印で対象端点切替）。
- **`WireBreak`**（554-620行）：点系のため本体移動のみ。
- **`FreeLine`**（669-855行）：mm実座標のためdouble精度。端点は水平/垂直判定してX/Yいずれかのみ
  動かし、最小長`FreeLineMinLengthMm=1.0`を保証。
- **`ConnectionDot`**（857-980行）：点系・本体移動のみ、mm精度。

ドラッグ確定/キャンセルは4種共通のジェネリックヘルパー`ConfirmDrag<T>`/`CancelDrag<T>`（939-952行、
T-045増分D）に集約。ヒットテストは`LadderCanvas.cs`側（ViewModelは幾何を知らない設計）。

### ヒットテスト方式の使い分け（殿裁定、増分4）

- `VerticalConnector`：「先頭一致」（既存据え置き）
- `FreeLine`/`ConnectionDot`：「nearest-wins」（増分4裁定）

複数種が重なる場合の一般的な優先度ルールについては、上記ヒットテスト方式の違い以上の明文裁定は
見当たらない（**該当記録なし**）。

### 実装バグ（発見・修正済み、参考記録）

- 列ドラッグ（水平方向）無反応 → `86bf96e`で修正（`docs/archive/ecad2-t041-increment7-ninja-verification.md:21-24`
  発見、`docs/archive/ecad2-p039-ninja-verification.md`で再検証OK）。
- 境界クランプに`min>max`ガードが無く`ArgumentException`実測クラッシュ → `767325b`で修正
  （所見AB、`docs/proposed.md`P-034該当）。
- 逆軸キー操作での偽陽性`MarkDirty` → 修正済み確認（所見Z）。
- **複合バグ**：「自由線(横線)記入」ボタンをTab+Enterで起動した直後、矢印キーがツールバーの標準
  ナビゲーションに奪われ隣接「接続点記入」ボタンへフォーカス移動→Enterで意図しない
  `ConnectionDot`が即時確定配置され、Escでも取消不可（既確定データのため）
  （`docs/archive/ecad2-t047-ninja-verification.md:79-104`、総合判定「要修正」——**修正済みか要確認、
  本調査では追跡できず不明点として記録**）。
- **未解決/両論併記**：境界外ドラッグ+Ctrl+Zで復元されない現象（P-042、
  `docs/archive/ecad2-t045-increment-d-ninja-verification.md:83-103`、未断定）。

---

## 3. 削除ロジック

4メソッド（`DeleteSelectedConnector`/`DeleteSelectedWireBreak`/`DeleteSelectedFreeLine`/
`DeleteSelectedConnectionDot`）は同一構造：選択ありガード→`List.Remove()`成功確認→`MarkDirty()`→
選択クリア→`true`返却。

### Deleteキーでの優先順位（殿裁定、増分1）

`MainWindow.xaml.cs:898-900`の短絡OR連鎖：
```
DeleteSelectedElement() || DeleteSelectedConnector() || DeleteSelectedWireBreak()
    || DeleteSelectedFreeLine() || DeleteSelectedConnectionDot()
```
**「部品優先→配線プリミティブ」**の順位で確定（増分1裁定）。選択は排他的（同時に1種のみ）なので
実質1つだけが実行される。「削除」メニュー（T-063）も同じ連鎖を流用（`docs/ecad2-spec-*`他領域と
共通、`MainWindow.xaml.cs:986-991`）。

---

## 4. 自動配線（制御回路シート限定）

「自動横配線」とは、**要素をグリッドに配置するだけで、その行内の要素同士・母線との横方向配線を
自動生成・自動描画する機能**を指す（`DiagramRenderer.cs`の`DrawRungWires`、297-365行、各行の
`Elements`を列順ソートし隣接ペアごとに座標計算）。

- `Render`メソッド（189-199行）：`if (!sheet.MainCircuit) DrawRungWires(...)`——**制御回路シートのみ**
  自動横配線を描画。母線描画（`DrawRails`）・母線名（`DrawBusLabels`）も同様に制御回路限定。
- 主回路シート（`MainCircuit=true`）ではこれらの呼び出し自体がスキップされ、代わりに
  `DrawFreeLines`（436-456行）でユーザーが手動配置した`FreeLine`を単純描画するのみ。

### 裁定根拠

T-041起票時の殿裁定（2026-07-06）：「制御回路グリッド内は自動配線があるため対象外」＝**自動配線は
制御回路シート限定・主回路シートは手動結線のみ**という構図が確立（`docs/todo-archive.md:140`）。
T-044「OR自動配線の冗長縦分岐抑止」は低ズームで断線して見える描画欠陥が発覚し、殿裁定（2026-07-07）
で一旦保留、手動配線基盤T-041が本命化された経緯がある（`docs/todo.md:713-718`）。

---

## 5. シミュレーション（ConnectivityChecker/NetlistBuilder）との関係

- `ConnectivityChecker.cs`（27-46行）は`Netlist`（構築済みノード/ネット集合）を受け取り、各ネットの
  次数(degree)を見て`degree<=1`を未結線(Dangling)と判定するのみで、結線プリミティブを直接参照しない。
- `NetlistBuilder.cs`（Union-Find方式）が`sheet.Connectors`（`VerticalConnector`）・
  `sheet.WireBreaks`（`WireBreak`）をネット構築時に消費する。
- **`FreeLine`/`ConnectionDot`はNetlistBuilder内で検出されない**——主回路シートはシミュレーション
  対象外という設計（`Element.cs`のBreaker3P等のコメントと整合、推測含む解釈）。

---

## 6. 既知の罠（実機検証固有、UI Automation経由の検証時のみ該当・仕様そのものではない）

- ボタンInvoke後にフォーカスが残留し、次のEnter送信で意図せず同ボタンが再起動する
  （実装バグではなくUI Automation検証手法固有の罠）。
- マウスドラッグ合成にはLEFTDOWN+段階的SetCursorPos（60ms間隔、8〜15回）+LEFTUPが必須、瞬間移動
  では無反応（検証手法上の制約）。
- UI AutomationのInvoke/SelectはClickハンドラ等を迂回し内部状態を不安定化させることがある
  （`docs-notes/roles/ninja.md:71,94`）、疑わしい結果は物理マウスクリックで再検証が必須という
  役割ルール。
- 行挿入/削除で`VerticalConnector`/`WireBreak`の位置が自動シフトする挙動あり
  （`docs-notes/ecad2-t055-increment3-realmachine-verification-ninja.md:31-36`、T-055仕様書側と
  重複領域）。

---

## 7. 本日T-062検証での確認（実機記録）

`docs-notes/ecad2-t062-main-operations-regression-ninja.md:78-92`：試行1で「自由線(横線)記入」
ボタンInvokeが「認識できないエラーです」で失敗（`IsEnabled=False`ボタンInvoke時の既知症状）→
シート種別依存のIsEnabled仕様と判明→検証方針を組み替え（制御回路シートで先に縦分岐線・配線分断を
検証、主回路シートを追加して横線・縦線・接続点を検証）て全項目OKに帰着。**実装バグではなく
既存仕様と確認済み。**

## 不明点

- `VerticalConnector`のみ`DeepClone`未定義の理由（意図的か実装漏れか）。
- 5つ目のツールバーボタンに対応する「もう1種のモデルクラス」は存在しない（4クラスで5ボタンを
  シート種別によって出し分けている構造）——今後の仕様理解の混同を避けるため明記。
- 「自由線起動直後の矢印キーがツールバーナビゲーションに奪われConnectionDot誤配置」バグ
  （`docs/archive/ecad2-t047-ninja-verification.md:79-104`）が最終的に修正済みか、本調査では追跡できず
  未確認。
