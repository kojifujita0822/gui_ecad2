# T-068 増分3-b2 静的レビュー（隠密、1周目）

対象コミット：`a9d972e`（`feat(app): T-068増分3-b2 - 形状編集キャンバス本体を移植`）
対象ファイル：`src/Ecad2.App/Views/PartEditorCanvas.cs`（新設590行）・
`PartEditorDialog.xaml`（82行差分）・`PartEditorDialog.xaml.cs`（149行差分）

## 結論：要修正なし

最重点観点（座標変換の合成）は手計算で完全一致を確認した。`Primitives`コピー化・Undo/Redo設計・
実装判断4件（うち3件を確認、1件は原本裏取り）いずれも妥当と判断する。既存テストへの影響なし
（Core.Tests204・App.Tests808とも本コミットでは不変、diff対象はApp層3ファイルのみ）。

## 1. 【最重点】座標変換の合成（2段`PushTransform`）の静的検算

`WpfRenderer.PushTransform`（`src/Ecad2.Rendering.Wpf/WpfRenderer.cs`28-34行）を一次ソースで確認：
```csharp
public void PushTransform(double translateX, double translateY, double scale = 1.0)
{
    var group = new TransformGroup();
    if (scale != 1.0) group.Children.Add(new ScaleTransform(scale, scale));
    group.Children.Add(new TranslateTransform(translateX * K, translateY * K));
    _dc.PushTransform(group);
}
```
（`K = 96.0/25.4`、mm→DIP換算）。`TransformGroup.Children`はWPF仕様上先頭要素から順に適用されるため、
本メソッドは「まずscale、次にtranslate（`translateX*K`は既にDIP換算済み）」という変換を意味する。

`Draw()`（`PartEditorCanvas.cs`544-545行）の2段呼び出し：
```csharp
renderer.PushTransform(_panMm.X, _panMm.Y, _zoom);            // 外側: パン・ズーム
renderer.PushTransform(_geo.MarginMm, _geo.MarginMm, 1.0);    // 内側: 原点余白
```
`PartDrawing.DrawPrimitive`が渡す座標は「セル×`CellMm`」というローカルmm値（`WpfRenderer.P()`内部で
`×K`されるためDIP相当値`P = cellCoord*CellMm*K`）。これに内側変換（`+MarginMm*K`、scaleなし）→
外側変換（scale`×zoom`→`+panMm*K`）を順に適用すると：

```
内側適用後: cellCoord*CellMm*K + MarginMm*K
外側scale後: (cellCoord*CellMm*K + MarginMm*K) * zoom
外側translate後: (cellCoord*CellMm*K + MarginMm*K)*zoom + panMm*K
= cellCoord*CellMm*K*zoom + MarginMm*K*zoom + panMm*K
```

一方`CellToDip`（183-188行）を展開：
```
worldMmX = MarginMm + cellCoord*CellMm
DipX = (worldMmX*zoom + panMm) * K = MarginMm*zoom*K + cellCoord*CellMm*zoom*K + panMm*K
```

**両式は項の順序が違うのみで数学的に完全一致**。マウス操作系が使う`DipToCell`（190-195行）も
`CellToDip`の正しい逆関数であることを式変形で確認した（`CellToDip`の式を`cellCoord`について解くと
`DipToCell`の実装と一致）。**合成は正しい**、崩れは見当たらない。

## 2. `Primitives`コピー化がキャンセル時のデータ破壊を防いでいるか

`LoadPrimitives`（153-161行）：`_primitives = primitives.ToList();`——引数（呼び出し元の
`edit.Primitives`）とは別インスタンスの新規リストを作る。呼び出し元（`PartEditorDialog.xaml.cs`
コンストラクタ）：`ShapeCanvas.LoadPrimitives(edit?.Primitives ?? Enumerable.Empty<PartPrimitive>());`
——この時点で`edit.Primitives`（`PartLibrary`内の実体と同一参照）とキャンバス内部の`_primitives`は
完全に切り離される。

`PartPrimitive`の6派生型（`PartDefinition.cs`33-42行、既存T-068増分2レビューで確認済み）はすべて
`sealed record`——キャンバス側の編集操作（`Translate`/`Rotate`等、`PartShapeGeometry`が全て`with`式で
新インスタンスを返す設計、往復1・2周目で確認済み）は元の要素を書き換えない。**「リストのシャロー
コピーで足りる」という前提（全プリミティブがイミュータブル）は本実装でも成立している**。

OK確定時（`OkButton_Click`）も`ShapeCanvas.Primitives.ToList()`で再度コピーしてから`Result`へ格納
しており、キャンバス内部リストとの二重の切り離しがある。**キャンセル時、`edit`（呼び出し元の
`PartDefinition`）は一切触れられないため破壊されない**——設計どおり機能する。

## 3. Undo/Redo設計（設計書§3.4との突合）

`Snapshot`（458行）は`(List<PartPrimitive> Primitives, PartEditorExternalState? External)`の2項目
だが、`PartEditorExternalState`（20行）が`(Ports, WidthCells, HeightCells, Role)`の4項目を持つため、
実質的にGuiEcad原本`EditorSnapshot`の5項目（Prims/Ports/W/H/Role）と情報として一致する（ネスト構造は
異なるが意味は同一）。`CaptureExternalState`/`RestoreExternalState`はダイアログ側への委譲
（デリゲート）で実現しており、設計書§3.4「幅・高さ・役割を編集中に変更可能なUIを持たせる設計と
連動する」という想定どおりの実装。

**`_dragChanged`判定**（PoC所見1の修正）を検算：
- `UpdateMoveDrag`（384-390行）：`if (dx != 0 || dy != 0) _dragChanged = true;`——`||`で正しい
  （`&&`だと片軸のみの移動を見逃す、まさにPR-27型の罠になり得たところだが実装は正しい）
- `UpdateRotateDrag`（421-427行）：`if (deltaDeg != 0) _dragChanged = true;`——単一条件で取り違えの
  余地なし
- `BeginSelectOrMove`/`BeginRotate`でドラッグ開始時に`_dragChanged = false`を確実にリセットしている
  （378・416行）
- `CommitMove`/`CommitRotate`は`if (_dragChanged) PushUndoSnapshot(...)`——GuiEcad原本の
  `CommitDragUndo()`（`_dragChanged`かつ保留スナップショット非nullの場合のみpush）と同型

**設計書§3.4と一致、正しく実装されている。**

## 4. 実装判断4件の確認

**(b) Escは作図中のみ消費**（340-351行）：`case Key.Escape when HasDraft:`——`HasDraft`が偽の場合
このcaseにマッチせず`e.Handled`は`false`のまま、WPF標準の`IsCancel`ボタンへ伝播する。XAML側で
`Button Content="キャンセル" ... IsCancel="True"`を確認、妥当な設計。

**(c) `ArcRyBox`のEnterで`Handled=true`**：XAML側`OkButton`が`IsDefault="True"`のまま維持されている
ことを確認（`PartEditorDialog.xaml`差分末尾）。`ArcRyBox_KeyDown`は通常の`KeyDown`（バブリング）で
`ArcRyBox`自身に直接バインドされており、Enter時に`e.Handled=true`とすることでIsDefaultボタンへの
伝播を止める。妥当。

**(d) Undo/やり直し/削除ボタンの`IsEnabled`制御**：`UpdateShapeStatus()`内で`ShapeCanvas.CanUndo`/
`CanRedo`/`SelectedIndex>=0`を反映。妥当な追加。

**(a) ステータス文言のユーザー向け改訂**：§5で扱う（原本裏取り完了）。

## 5. GuiEcad原本のステータステキストの実態（依頼分）

`PartEditorWindow.xaml.cs`858-866行（`UpdateStatus()`）を確認：
```csharp
StatusText.Text = $"図形 {_prims.Count} / 接続点 {_ports.Count}    ツール: {ToolLabel(_tool)}"
    + (_tool == "polyline" ? "（右クリックで確定）"
       : _tool == "arc" ? "（外接矩形をドラッグ：横=幅・縦=高さで扁平率、上下方向で弧の向き）"
       : _tool == "rotate" ? "（図形をドラッグして15度単位で回転）" : "");
```

原本は「**図形数 / 接続点数 / 現在のツール名 + ツール別の動的操作ガイド**」という構成——単なる
開発者向け情報ではなく、**選択中ツールに応じて操作方法の説明文が切り替わる、ユーザー向けガイダンス**
だった（侍が想定した「PoCの開発者向け」という前提認識はGuiEcad原本の実態とは異なる）。

3-b2の実装：`"図形: {Count}個 / 表示倍率: {Zoom}倍 / Ctrl+ホイールで拡大縮小、中ボタンのドラッグで移動"`
——ズーム・パンの操作案内は含むが、**原本にあった「現在のツール名」表示と「ツール別の動的ガイド
（折れ線=右クリック確定・弧=扁平率説明・回転=15度単位）」に相当する情報が無い**。

ただし完全な欠落ではなく、XAML側で折れ線・弧・回転のRadioButtonに`ToolTip`（例：「クリックで頂点を
追加し、右クリックで確定します」）が個別に付与されており、**情報自体はホバー時のToolTipという別の
手段で提供されている**。原本の「常時ステータスバーに表示」というUXとは発見しやすさが異なるが、
情報が完全に失われているわけではない。

**判断**：致命的な後退ではないが、原本と3-b2とでは「操作ガイダンスの提示方法」という設計方針に
差異がある。UI/UXの使用感に関わる差異のため、**要否は殿確認が望ましい**が、3-b1完了時点での
殿事前裁可（「殿御不在中はGuiEcad踏襲で家老が仮決定」）の範囲内で家老判断とすることもできる水準と
考える。深追いはせず、事実関係のみ報告する。

## 6. 既知トラップ・既存テストへの影響

- **SetProperty早期return罠（PR-03）**：該当なし。`WidthCells`/`HeightCells`/`Tool`/`Zoom`のsetterは
  いずれも早期return判定はあるが、対応する副作用（`Draw()`/`Notify()`）は正しく変更時のみ実行される
  設計で問題なし
- **PR-13型（CanEditDiagramガード漏れ）**：該当なし。本ダイアログはモーダルであり独立したCanEditDiagram
  概念を持たない（既存増分2・3-aレビューと同じ結論）
- **PR-27（本日制度化、対称性・退化入力による検出力消失）**：本コミットは新規テストを含まない
  （diff対象はApp層3ファイルのみ、`Core.Tests`204・`App.Tests`808とも不変）ため直接の対象外。実装
  ロジック自体（`_dragChanged`の`||`判定等）にPR-27型の取り違えが無いかも確認したが、該当なし（§3参照）
- **既存テストへの影響**：`git diff f537ead..a9d972e -- tests/`相当の変更なし（本コミットはテスト
  ファイルに触れていない）、build/test exit 0の申告どおり回帰なしと判断

## 不明点

なし。

## 派生提案

§5のステータス文言の使用感差異について、忍者実機確認時に「ツール切替時のガイダンスの分かりやすさ」を
観点として加えることを推奨する（家老判断に委ねる、隠密からの提案に留める）。

---

## 往復2周目 再レビュー（コミット`6f46ab2`、ステータス文言をGuiEcad原本準拠へ修正）

対象：`PartEditorDialog.xaml.cs`のみ（+24/-2）。§5で指摘した差異への対処。

### 1. 原本準拠の文言確認（折れ線・弧・回転の3ツール）

原本（`PartEditorWindow.xaml.cs`861-864行、§5既掲）と本修正の文言を突き合わせた。

- **折れ線**：原本「（右クリックで確定）」→本修正「クリックで頂点を追加し、右クリックで確定します」。
  確定方法（右クリック）という核心情報は保持、頂点追加の操作説明を追加しており原本の趣旨を損なわない
- **弧**：原本「外接矩形をドラッグ：横=幅・縦=高さで扁平率、上下方向で弧の向き」→本修正「外接する
  矩形をドラッグします。描いた後は下の欄で縦半径を変えられます」。**文言はコピーではなく実装内容に
  即した意訳**——`PartShapeGeometry.BuildArc`は`StartDeg:180・SweepDeg:180・Rot:0`固定（殿裁定=
  半楕円弧のみ、往復1・2周目で確認済み）で、原本にあった「上下方向で弧の向き」を変える機能は
  ecad2に存在しない。原本の文言をそのまま踏襲すると実装に無い機能を案内する誤ったガイダンスに
  なるところを、実態（ドラッグ後にArcRyBoxで縦半径を事後調整）に即して正しく書き換えている。
  **これは原本の逐語的コピーより優れた判断**
- **回転**：原本「図形をドラッグして15度単位で回転」→本修正「図形をドラッグすると15度きざみで
  回ります」。表現差のみで内容完全一致

**結論：3ツールとも原本の趣旨と合致、弧は実装実態に即した適切な意訳。**

### 2. ツール別分岐の網羅性（PR-27の目）

`PartEditTool` enum（`PartEditorCanvas.cs`11行）は7種（Select/Line/Polyline/Rect/Circle/Arc/Rotate）。
`ToolLabel`・`ToolGuide`とも6ケース明示＋`_`（デフォルト、Select相当）で構成されており、**7種全てが
両switch式で漏れなくカバーされている**ことを確認した（3+4=7、6+1=7）。分岐漏れなし。

### 3. 既存機能への影響

`UpdateShapeStatus()`全体（170-212行）を確認。`ArcRyBox`表示切替・`Undo/Redo/Delete`ボタンの
`IsEnabled`制御・`StateChanged`イベント経由の呼び出し経路（`Zoom`セッタ→`Notify()`→
`StateChanged`→`UpdateShapeStatus()`）はいずれも本コミットで変更されていない
（`PartEditorCanvas.cs`は今回のdiff対象外）。ズーム表示の追従を含め既存機能に影響なし。

### 結論（往復2周目）

要修正なし。忍者実機確認へ回してよいと判断する。
