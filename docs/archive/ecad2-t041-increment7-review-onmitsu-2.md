# T-041増分7 最終レビュー（隠密、4種全体・所見A/B/C修正後）

> 2026-07-08 隠密レビュー。対象コミット23c18c8（WireBreak横展開、第2弾）・4aa11f4（所見A/B/C
> 修正+FreeLine横展開、第3弾）・4154fb8（ConnectionDot横展開、第4弾）。家老指定観点(1)〜(4)＋
> `code-review`スキル（line-by-line diff scan・cross-file/品質・ConnectionDot専用検証、
> 3エージェント並行）併用。実測検証（`dotnet test`、および独立WPF最小再現プログラムでの
> `ReleaseMouseCapture`/`LostMouseCapture`同期発火の実証）も併用した。

---

## 結論：**要修正（最重要級）。マウスドラッグによる移動・リサイズが4種すべてで機能しない
致命的なリグレッションを実機検証前に発見（実測実証済み）。加えて中〜軽微の指摘複数**

前回（コミットa471260）で指摘した所見A・B・Cの解消方針自体は妥当。しかし所見C対応
（`LostMouseCapture`ハンドラの新規追加）が、既存のマウスドラッグ確定処理
（`ReleaseMouseCapture()` → `ConfirmDrag*()`の順序）と競合し、**マウスでドラッグして指を
離すと必ず開始位置へスナップバックし、`MarkDirty()`も呼ばれない**という、増分7の核心機能
そのものを無効化する致命的なバグを新たに作り込んでいることを、独立エージェントの指摘＋
自前のWPF最小再現プログラムでの実証の両方で確認した。

---

## 家老指定観点の検証

### (1) 所見A修正がsetter集約方式で3経路すべて確実にカバーされているか

**基本方針は正しく実装されている**。`SelectedConnector`/`SelectedWireBreak`/`SelectedFreeLine`
/`SelectedConnectionDot`の4setterすべてで`ForceCancelDrag*IfAny()`が`SetProperty`呼び出しより
前に無条件配置されており、順序は4種間で一貫している（コード比較で確認）。Delete
（各`DeleteSelected*`が最終的にこれらのsetterへ`null`を代入）・シート切替
（`CurrentSheetIndex`のsetter→`SelectedCell = null`→各setterへの連鎃）・文書差し替え
（`ReplaceDocument`が各setterへ`null`を代入）の3経路とも、最終的にこれらのsetterを経由する
ため、コードトレース上はカバーされている。

**ただし新たな抜け穴を発見（所見Y、後述）**：`ForceCancelDrag*IfAny()`は`_dragging*`を
`null`にするだけで、`CancelDrag*()`と異なり**位置を復元しない**。Delete・ReplaceDocument経由
では対象オブジェクト自体が破棄・差し替えられるため実害はないが、**シート切替は生きた
シート・オブジェクトに対して発生するため、ドラッグ中に既に`UpdateDrag*`が適用済みの半端な
位置が`MarkDirty()`もされず黙って確定してしまう**。既存の所見A回帰テストはいずれも
`BeginDrag*`のみで`UpdateDrag*`を呼ばずに検証しており、この状態を検出できていない。

### (2) 所見C/B修正の妥当性

**所見Cは重大な副作用を伴う形で「修正」されてしまっている（下記所見X、最重要）**。
`CaptureMouse()`の戻り値確認自体は正しく実装されている。しかし`LostMouseCapture`ハンドラの
追加が、既存の確定処理の呼び出し順序（`ReleaseMouseCapture()`を`ConfirmDrag*()`より先に
呼ぶ）と組み合わさることで、通常のマウスドラッグ確定を毎回無効化してしまう。

所見Bは、VerticalConnectorに対する事前ガード追加（`Math.Clamp`のmin>max回避）は妥当に実装
されている。WireBreak/FreeLine/ConnectionDotはMath.Clamp不使用（FreeLine/ConnectionDotは
座標を直接加算、WireBreakは独立フィールドのRow/Boundaryに対するClampのため相互反転が
起きない）ため対象外という判断も、確認の結果妥当と判断する（コミットメッセージの「WireBreak
はMath.Clamp不使用」という記述は不正確だが、所見Bの具体的パターン=同一エンティティの
Top/Bottom相互反転には該当しないため実害はない）。

### (3) WireBreak/FreeLineへの横展開が同パターンを正しく踏襲し新たな副作用がないか
（ConnectionDot分も追加確認）

基本パターン（`_dragging*`状態機械・`ForceCancelDrag*IfAny`・`LostMouseCapture`ハンドラ・
Escの`ConsumedByEscape`）は4種間で一貫して踏襲されており、コピペミス（変数の取り違え等）は
無いことを確認した（3エージェント独立検証）。ただし以下の新規指摘がある：

- **所見Z（FreeLine固有、実在確認済み）**：`ResizeSelectedFreeLineEndpoint`が、線の向きと
  逆軸のdelta（常に0）を受け取った場合に無意味な自己代入を行い、ゼロ長ガードを素通りして
  `MarkDirty(); return true;`まで到達する。View側`ResizeSelectedFreeLineByKey`は
  `Key.Up/Down/Left/Right`すべてを無条件でこのメソッドへ渡しており方向フィルタが無い。
  垂直線にLeft/Right、水平線にUp/Downを押すと、何も座標が変わっていないのに未保存マークが
  付く。既存テスト（`ResizeSelectedFreeLineEndpoint_Vertical_IgnoresXDelta`）は`deltaXMm`/
  `deltaYMm`両方を非ゼロにして呼んでおり（実際のView呼び出しは必ず片方が0）、この抜け穴を
  検出しない。WireBreak/ConnectionDotは端点概念が無いため発生しない。

- **所見AA（FreeLine固有、severity低〜中）**：`UpdateDragFreeLine`/`MoveSelectedFreeLine`は
  グリッド・ページ境界へのクランプを一切行わない（VerticalConnector/WireBreakは全ての更新
  経路で`sheet.Grid.Rows`/`Columns`へクランプしている）。Undo機能が無い本アプリで自由線を
  ドラッグ・矢印キーで大きく動かすと、印刷ページ外の任意のmm座標へ飛んでいき見失う恐れが
  ある。

### (4) 130件のregression維持 —— 実測で確認（ただし所見X・Yを検出できないテスト構成）

`dotnet test src/Ecad2.sln`実行、Core14件・App116件、計130件合格。侍の報告と一致。ただし
新規回帰テスト（`ConnectorDragAndResizeTests.cs`/`WireBreakDragTests.cs`/
`FreeLineDragAndResizeTests.cs`/`ConnectionDotDragTests.cs`）はいずれも`MainWindowViewModel`を
直接呼ぶ設計で、`MainWindow.xaml.cs`のマウスイベント配線・WPFキャプチャの同期発火を経由
しないため、**所見X（最重要）を原理的に検出できない**。所見Yも、既存の所見Aテストが
`UpdateDrag*`を呼ばずに強制クリアを検証しているため検出できない。

---

## `code-review`スキル併用で判明した所見

### 所見X（CONFIRMED・最重要、実測実証済み）: マウスドラッグ確定処理の呼び出し順序が
`LostMouseCapture`と競合し、ドラッグ結果が常に巻き戻る

`LadderCanvasHost_PreviewMouseLeftButtonUp`（`MainWindow.xaml.cs`435〜466行、Connector/
WireBreak/FreeLine/ConnectionDotの4ブロック共通）は、いずれも

```csharp
LadderCanvasHost.ReleaseMouseCapture();   // 先
_viewModel.ConfirmDragConnector();         // 後
```

の順で呼んでいる。WPFの`ReleaseMouseCapture()`は、要素が実際にキャプチャを保持していれば
`LostMouseCapture`イベントを**同一コールスタック内で同期発火**する——これは推測ではなく、
独立したWPF最小再現プログラム（スクラッチパッド、`Canvas.CaptureMouse()`→
`Canvas.ReleaseMouseCapture()`を実行し`LostMouseCapture`ハンドラの発火有無を実測）で確認した：

```
=== Step 2: ReleaseMouseCapture (simulating MouseUp handler) ===
[Event] LostMouseCapture fired. IsMouseCaptured=False
After ReleaseMouseCapture() returns: IsMouseCaptured=False
Event log immediately after ReleaseMouseCapture() call: [LostMouseCapture fired]
```

`LadderCanvasHost_LostMouseCapture`（516〜541行）は「まだドラッグ中なら」という
`if (_viewModel.IsDraggingConnector)`ガードで`CancelDragConnector()`を呼ぶが、
`ReleaseMouseCapture()`呼び出し時点では`ConfirmDragConnector()`がまだ実行されていないため
`IsDraggingConnector`は依然`true`。結果、`ReleaseMouseCapture()`の中で`CancelDragConnector()`
が割り込み実行され、モデルの値をドラッグ開始前の位置へ復元し`_draggingConnector = null`に
してしまう。制御が戻った直後に呼ばれる`ConfirmDragConnector()`は`_draggingConnector is
VerticalConnector c`のパターンマッチが失敗し完全な空振りとなる。

**結果**：マウスでドラッグして指を離すと、見かけ上は移動したように動くが、離した瞬間に
元の位置へスナップバックし、`MarkDirty()`も呼ばれない。VerticalConnector・WireBreak・
FreeLine・ConnectionDotの4種すべてで同一構造のため同時に壊れる。

`LadderCanvasHost_LostMouseCapture`のコメントは「ReleaseMouseCapture()を能動的に呼んだ直後の
正常フロー（MouseUp/Escの各分岐）でも本イベントは発火するが、その時点では既に
Confirm/CancelDrag*でIsDragging*=falseになっているため各ガードが素通しし二重処理には
ならない」としているが、これは**MouseUp分岐に関しては誤り**（`ReleaseMouseCapture()`が
`ConfirmDrag*()`より先に呼ばれているため、`ConfirmDrag*()`実行前の時点ではまだ
`IsDragging*=true`）。Esc分岐（`CancelDrag*()`を呼んでから`_*DragConsumedByEscape=true`に
する、`ReleaseMouseCapture()`はまだ呼ばない設計）については、`CancelDrag*()`実行後に
`IsDragging*`が既に`false`になっているため、後続の（実際にマウスを離した際の）
`ReleaseMouseCapture()`呼び出しでの`LostMouseCapture`発火は素通りし、二重処理にならないのは
正しい。**問題はMouseUp分岐の順序のみ**。

**修正方針（参考）**：`ConfirmDrag*()`を`ReleaseMouseCapture()`より先に呼ぶよう単純に順序を
入れ替えれば解消すると考える。`ConfirmDrag*()`実行後は`_dragging* = null`（`IsDragging*
= false`）になっているため、その後`ReleaseMouseCapture()`が同期的に`LostMouseCapture`を
発火しても、`LadderCanvasHost_LostMouseCapture`内の各ガードは素通りする。4箇所（Connector/
WireBreak/FreeLine/ConnectionDot）すべてで同じ入れ替えが必要。

**実機確認との整合性**：侍は各コミットで「実機でドラッグ移動を確認済み」と報告している。
これは所見C対応（`LostMouseCapture`追加、4aa11f4以降）の**前**（a471260時点）の確認、
または確認観点が「クラッシュしないこと」中心だった可能性がある。所見Xは4aa11f4で
`LostMouseCapture`が追加されて以降に混入した可能性が高く、侍・忍者による実機再検証を
強く推奨する。

### 所見Y（CONFIRMED・重大）: `ForceCancelDrag*IfAny()`が位置を復元しないため、シート切替
経由で未確定の編集が無言で確定される

`ForceCancelDragConnectorIfAny()`（および`ForceCancelDragWireBreakIfAny`/
`ForceCancelDragFreeLineIfAny`/`ForceCancelDragConnectionDotIfAny`も同型）は`_dragging*`を
`null`にするだけで、`CancelDrag*()`と異なり位置を復元しない。Delete・ReplaceDocument経由
では対象オブジェクト自体が破棄・差し替えられるため無害だが、**シート切替（`CurrentSheetIndex`
経由）は生きたシート・オブジェクトに対して発生する**ため、ドラッグ中に既に`UpdateDrag*`が
適用済みの半端な位置が`MarkDirty()`もされず黙って確定してしまう。

具体的には：ドラッグでコネクタをTopRow=3→6へ動かした直後（`UpdateDragConnector`実行済み、
まだマウスボタン押下中）、Shift+Tab等でシートナビへフォーカス移動し矢印キーで
`CurrentSheetIndex`を変更すると`SelectedCell = null`経由で`ForceCancelDragConnectorIfAny`が
発火する。`IsDraggingConnector`は`false`になるが、コネクタは元のTopRow=3へ戻らずTopRow=6の
まま残り、`MarkDirty()`もされないため未保存フラグが立たない。その後の保存はDocumentを丸ごと
シリアライズするため、ユーザーが確定していない中途半端な移動が気付かれずに保存される。
既存の所見A回帰テストは`BeginDrag*`のみで`UpdateDrag*`を呼ばずに強制クリアを検証しており、
この状態を検出できていない。

### 所見Z（CONFIRMED・中、FreeLine固有）: `ResizeSelectedFreeLineEndpoint`の偽陽性MarkDirty

上記(3)節に記載。垂直線にLeft/Right、水平線にUp/Downキーを押すと、何も座標が変わって
いないのに`MarkDirty()`が呼ばれ`true`が返る。WireBreak版の`MoveSelectedWireBreak`は
`if (newRow == b.Row && newBoundary == b.Boundary) return false;`という「両方変化なし」の
明示チェックを持ちこの問題が無いことと対照的（比較検証済み）。

### 所見AA（severity低〜中、FreeLine固有）: 境界クランプの欠如

上記(3)節に記載。`UpdateDragFreeLine`/`MoveSelectedFreeLine`にグリッド・ページ境界への
クランプが無い。

### 所見AB（severity低、参考）: コミットメッセージの数値不一致

4154fb8のコミットメッセージは「新規テスト8件」と記載しているが、実際の
`ConnectionDotDragTests.cs`には7件の`[Fact]`のみ存在する（`dotnet test`の実測件数
Core14+App116=130も、親4aa11f4時点の123からの差分7件で辻褄が合う）。機能的な問題ではないが、
今後この種の数値ミスマッチが「本当は書いたはずのテストが漏れている」ことの見落としサインに
なりうるため、報告の数値を鵜呑みにせず実測で裏取りする運用の重要性を示す一例。

### 所見AC（Altitude、継続）: 4種のドラッグ状態機械のコピペ増殖

VerticalConnector（約155行）/WireBreak（約93行）/FreeLine（約160行）/ConnectionDot
（約80行）の4つのドラッグ状態機械が、nullableフィールド+`IsDragging*`算出プロパティ+
`ForceCancelDrag*IfAny`+Begin/Update/Confirm/Cancelという同一の外枠を持つまま横に並んで
いる。今回発見した所見Y（`ForceCancelDrag*IfAny`が復元を伴わない）は、この外枠が4箇所
（実質コピペ）に複製されたことで、同一の設計ミスが独立に4回混入するリスクを体現した実例
である。共通の型（対象参照+スナップショット+復元/確定デリゲートを持つ汎用DragSession）へ
集約すれば、この種の齟齬は1箇所の実装ミスで済み、複数箇所へ同時混入することはなくなると
考える。P-025（配置前検証サービス抽出）と同種の状況証拠が今回も積み上がった形。

---

## severity整理

| 所見 | 種別 | severity | 対応要否 |
|---|---|---|---|
| X | 正しさ（機能不全） | **最重要** | 至急対応要（増分7の核心機能が動作しない） |
| Y | 正しさ（データ整合性） | 重大 | 対応推奨 |
| Z | 正しさ（軽微、FreeLine固有） | 中 | 対応推奨 |
| AA | 正しさ（軽微、FreeLine固有） | 低〜中 | 対応推奨 |
| AB | 報告精度 | 低 | 対応不要（気づきとして記録） |
| AC | Altitude | 継続 | 家老判断（横展開完了後の共通化検討） |

---

## 出典・参照

- 対象コミット23c18c8・4aa11f4・4154fb8（`git show`で全差分確認）、親a471260
- `src/Ecad2.App/MainWindow.xaml.cs`（`LadderCanvasHost_PreviewMouseLeftButtonUp`415〜466
  行目、`LadderCanvasHost_LostMouseCapture`511〜545行目付近）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`ConfirmDragConnector`358〜364行目、
  `ForceCancelDragConnectorIfAny`等、`ResizeSelectedFreeLineEndpoint`731行目付近、
  `UpdateDragFreeLine`/`MoveSelectedFreeLine`649/717行目付近）
- `tests/Ecad2.App.Tests/ConnectorDragAndResizeTests.cs`・`WireBreakDragTests.cs`・
  `FreeLineDragAndResizeTests.cs`・`ConnectionDotDragTests.cs`
- WPF最小再現プログラム（スクラッチパッド、`Canvas.CaptureMouse()`/`ReleaseMouseCapture()`と
  `LostMouseCapture`の同期発火を実測、`src`/`tests`は未変更）
- `docs/archive/ecad2-t041-increment7-review-onmitsu.md`（コミットa471260、初回レビュー、所見A/B/C
  の原本）
- `code-review`スキル（line-by-line diff scan・cross-file/品質・ConnectionDot専用検証、
  3エージェント並行、CONFIRMED2件（所見X・Y、うち1件は隠密自身の実測でも独立に実証）・
  所見複数）
