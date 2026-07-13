# T-064 静的調査: 矢印キー(右)で画像選択解除+セル移動が発生する原因

- 対象: 忍者実機確認`docs-notes/ecad2-t069-t064-verification-ninja.md`「T-064観点4」新規所見
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: コード読解(矢印キーハンドラ・MoveSelectedCell・SelectedCellのsetter)
- スコープ境界: 調査のみ、書き込みなし。修正要否は家老が仕分け。

## 結論サマリ

画像選択中(`SelectedImage`≠null)に矢印キーを押すと、矢印キーハンドラが`SelectedImage`の状態を一切考慮しておらず既定のセルナビゲーション(`MoveSelectedCell`)へフォールスルーする。`MoveSelectedCell`は`SelectedCell`が`null`(画像選択中は往復1周目修正4により常にnull)の場合`GridPos(0,0)`を起点として計算するため、右矢印1回で`GridPos(0,1)`(表示上「行1/列1」)へ移動し、`SelectedCell`のsetターが無条件で`SelectedImage`をクリアする。**新規選択可能状態(`Selected*`)の横展開漏れ(パターン台帳PR-01型)である可能性が高い。**

観点1(リサイズ最小サイズ制約+境界クランプ両立)は、既存の単体テスト4件で十分に担保されている(全て過去のレビューで手計算検証済み)。忍者が保留とした理由は座標クリック手法(UIA)の精度問題であり、実装自体は単体テストレベルで検証済み。

## 観点1: 矢印キー(右)で画像選択解除+セル移動が発生する原因

### 発火の全経路(コード読解、事実)

**該当**: `MainWindow.xaml.cs:1280-1313`(矢印キーのswitch case)、`:1478-1496`(`MoveSelectedCell`)

矢印キー処理の分岐は以下の順:

```csharp
if (Tool.Mode == PlaceConnector) AdjustConnectorDraft(...)
else if (Tool.Mode == PlaceLine) AdjustFreeLineDraft(...)
else if (Tool.Mode == PlaceImage) { /* 無視(往復1周目修正2) */ }
else if (SelectedConnector is not null) MoveSelectedConnectorByKey(...)
else if (SelectedWireBreak is not null) MoveSelectedWireBreakByKey(...)
else if (SelectedFreeLine is not null) MoveSelectedFreeLineByKey(...)
else if (SelectedConnectionDot is not null) MoveSelectedConnectionDotByKey(...)
else MoveSelectedCell(e.Key)   // ← SelectedImageのチェックが無く、ここへフォールスルーする
```

**`SelectedImage`をチェックする分岐が存在しない**。画像選択中(確定済み、Tool.Mode=Select)は、上記いずれの条件にも一致せず(選択排他制御により`SelectedConnector`等は全てnullのため)、最終`else`の`MoveSelectedCell(e.Key)`へ落ちる。

`MoveSelectedCell`(1478-1496行目)は:
```csharp
var current = _viewModel.SelectedCell ?? new Ecad2.Model.GridPos(0, 0);
...
case Key.Right: column = Math.Min(grid.Columns, column + 1); break;
...
_viewModel.SelectedCell = newCell;
```

画像選択確定時(`ConfirmImageInsertDraft`、往復1周目修正4)は`SelectedCell = null; SelectedImage = image;`の順で設定されるため、画像選択中は常に`SelectedCell`が`null`。よって`current`は`GridPos(0,0)`にフォールバックし、右矢印1回で`column = Min(grid.Columns, 0+1) = 1`(通常`grid.Columns>1`のため)、`newCell = GridPos(Row=0, Column=1)`となる(内部0-indexed、表示上「行1/列1」に対応、忍者報告と一致)。

最後に`_viewModel.SelectedCell = newCell;`が実行されると、`SelectedCell`のsetter(263-308行目)が無条件で`SelectedImage = null;`(283行目)を実行するため、**画像選択が解除される**。これが忍者観察の「画像選択が解除され、セル『行1/列1』が選択される」という挙動の完全な原因。

### パターン台帳との照合

台帳PR-01(新規選択可能状態の横展開漏れ——「矢印キー等、記入中ドラフト中の入力に対する分岐」がチェックリスト項目3番として既に制度化済み)と根は同じだが、対象が「記入中ドラフト」ではなく「**選択中**の状態(`SelectedImage`)に対する矢印キー平行移動」である点が異なる。`SelectedConnector`/`SelectedWireBreak`/`SelectedFreeLine`/`SelectedConnectionDot`の4種は全て矢印キーでの平行移動(`MoveSelected*ByKey`)に対応しているのに対し、`SelectedImage`だけこの横展開から漏れている。**「パターンの疑いあり」として申し送る**(既存PR-01そのものではなく、同型・別の切り口の再発)。

### 意図的な仕様である可能性の検討

もし「画像は矢印キーで移動できない」こと自体が意図的な仕様だとしても、その場合はTool.Mode==PlaceImage分岐(1290-1297行目)で採用されている「何もしない」という対応が妥当なはずである。現状の「画像選択解除+無関係なセルへジャンプ」という副作用は、いずれの意図とも整合しない意図しない動作と判断する(事実+推測を峻別: 「横展開漏れによる意図しない副作用」は事実に基づく推論、「意図的仕様の可能性」は明示的に排除できないため推測として併記)。

## 観点2: リサイズ最小サイズ制約+境界クランプ両立の単体テスト担保状況

**既存テスト4件で十分に担保されている**(いずれも過去のレビューで手計算検証済み、`tests/Ecad2.App.Tests/ImageInsertTests.cs`):

| テスト名 | 検証内容 |
|---|---|
| `ResizeImage_BottomRightHandle_NearLeftBoundary_DoesNotExceedPageBoundary`(400行目) | アンカーが左境界近く、対角ハンドルを大きく逆方向へ→境界外にはみ出さず最小サイズ確保 |
| `ResizeImage_TopLeftHandle_NearRightBoundary_DoesNotExceedPageBoundary`(420行目) | アンカーが右下境界近く、同様の逆方向ケース |
| `ResizeImage_DragPastAnchor_FlipsAxisAndFollowsMouse`(477行目) | 境界から離れた位置でアンカー反対側へ超えると軸反転してマウス追従 |
| `ResizeImage_DragPastAnchor_FlipsAxisButStillClampsToPageBoundary`(498行目) | 軸反転しつつ境界近くでは境界優先クランプ |

4件とも`ClampResizeTarget`(`MainWindowViewModel.cs`)の主要な分岐(通常ケース・境界近くでの最小サイズ優先フォールバック・反転追従)を網羅しており、忍者が保留とした「ページ境界を大きく超える位置へドラッグしても境界外にはみ出さない」という観点は単体テストレベルで実測済み。忍者確認が保留に終わった理由は座標クリック(UIA)によるハンドルの当たり判定の精度問題であり、実装のロジック自体は単体テストで担保されている。

## 派生提案の有無

あり(上記「新規選択可能状態(SelectedImage)の矢印キー横展開漏れ」、CONFIRMEDレベルの原因特定、修正要否は家老判断)。自らは着手せず家老へ報告のみ。
