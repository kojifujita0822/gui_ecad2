# T-068 増分3-b3 静的レビュー（隠密、1周目）

対象コミット：`4f6121b`（`feat(app): T-068増分3-b3 - 文字ツール・保存経路・テーマ連動`）
対象ファイル：`MainWindow.xaml.cs`（29行差分）・`PartEditorCanvas.cs`（39行差分）・
`PartEditorDialog.xaml`（2行追加）・`PartEditorDialog.xaml.cs`（26行差分）・
`PartTextInputDialog.xaml`（新設34行）・`PartTextInputDialog.xaml.cs`（新設27行）

## 結論：要修正なし

DoD5点いずれも妥当な実装と判断する。既定値付き引数（観点5）について軽微な指摘を1件申し送る。

## 1. 再描画トリガの経路網羅

`PartPaletteViewModel`の`SaveNewPart`/`SaveEditedPart`/`DeletePart`（増分2レビューで既読、いずれも
内部で`Load()`を呼ぶ）の呼び出し元を`grep`で洗い出したところ、`MainWindow.xaml.cs`の3箇所
（`CreatePartMenuItem_Click`470行・`EditPartMenuItem_Click`517行・`DeletePartMenuItem_Click`531行付近）
のみであることを確認した。**3経路すべてに`RedrawCanvas()`が追加されており、呼び出し元との1対1対応が
取れている。経路の漏れなし**。

**削除経路への追加判断**（「削除したパーツが配置済みの場合、PartIdが解決できなくなり見た目が変わる
ため再描画する」というコメント）は、`PartResolver.IsUnresolvedPartId`（増分2レビューで確認済み、
未解決`PartId`は`ComponentKind`が静かに`ElementInstance.Kind`＝既定値`ContactNO`へフォールバックする
機構）と整合しており、**判断は正しい**。`RedrawCanvas()`自体（951行）も`_viewModel.PartLibrary`を
渡して再描画する既存メソッドで、`Load()`後の最新`PartLibrary`を正しく反映する。

## 2. `MergeCollinearLines`の適用箇所

`OkButton_Click`内：`var primitives = PartOptimizer.MergeCollinearLines(ShapeCanvas.Primitives);`
——OK確定処理内のみで呼ばれる。`PartOptimizer.MergeCollinearLines`（既存実装、`prims.ToList()`から
開始し新しい`List<PartPrimitive>`を返す設計、往復1周目レビューで確認済み）は引数のリストを直接変更
しないため、`ShapeCanvas.Primitives`（キャンバス内部の`_primitives`）自体はこの呼び出しの前後で
不変。**「保存直前にのみ適用・編集中のプリミティブは不変」という原本`OnSave`の性質は保たれている**。
キャンセル時はもちろん、OK確定後もキャンバス側の並びに影響しないというコメントの説明も正確。

## 3. 文字ツールの設計判断

`RequestText`（`Func<string?>?`、`PartEditorCanvas.cs`89-92行）というデリゲートプロパティを新設し、
`PlaceText`（256-263行）は`RequestText?.Invoke()`を呼ぶのみで、実際のダイアログ表示は行わない。
ダイアログ側が`AskShapeText`（`PartEditorDialog.xaml.cs`168-173行）で`PartTextInputDialog`を生成・
表示し、結果を返す。

この委譲パターンは、既存の`CaptureExternalState`/`RestoreExternalState`（Undo/Redoの外部状態委譲、
往復1周目レビューで確認済み）と**同型で一貫した設計方針**——キャンバス（View部品）自身がモーダル
ダイアログの生成・`Owner`ウィンドウ参照等のView階層知識を持たずに済み、単体テスト・単体利用可能性を
保つ。**筋が通っている**。

`PartTextInputDialog`は`RenameDialog`/`AddSheetDialog`と同型の最小モーダル（新設ファイルを確認、
`InputText`プロパティ・空文字ガード・`IsDefault`/`IsCancel`ボタン構成いずれも既存パターンに準拠）。
`RenameDialog`が流用できない理由（タイトル差し替え不可）も実装を見る限り筋が通る。

`PlaceText`の実装（`if (string.IsNullOrEmpty(text)) return;`→`CommitAdd`→`Notify()`）は、
キャンセル・空入力時に`CommitAdd`（`PushUndo()`を含む）が呼ばれないため、**余計なUndoエントリは
積まれない**（PoC所見1の修正方針と整合）。

## 4. テーマ連動

`Theme`プロパティ（`PartEditorCanvas.cs`101-114行）：
```csharp
public DrawingTheme Theme
{
    get => _theme;
    set { if (ReferenceEquals(_theme, value)) return; _theme = value; Draw(); }
}
```
`_theme`を`readonly`から可変フィールドへ変更。`DrawingTheme.Default`/`.Dark`は`static readonly`
プロパティで単一インスタンスのため、`ReferenceEquals`による早期return判定は正しく機能する
（値の内容比較ではなく参照比較だが、渡されるのは常にこの2つの固定インスタンスのみなので実害なし）。

`PartEditorDialog`側：`ShapeCanvas.Theme = isDarkMode ? DrawingTheme.Dark : DrawingTheme.Default;`
——メイン側`MainWindow.xaml.cs`812-814行の`LadderCanvasHost.Theme = _viewModel.IsDarkMode ?
DrawingTheme.Dark : DrawingTheme.Default;`と**プロパティ名・三項演算子による選択方式とも完全に
一致する形で踏襲されている**。「モーダルゆえ表示中のテーマ切替は起こりえない」という前提も、
メインウィンドウの操作がダイアログ表示中はブロックされる（テーマ切替メニューへアクセス不可）ため
妥当。**メインパターンを正しく踏襲している**。

## 5. `isDarkMode`既定値付き引数の妥当性

`public PartEditorDialog(PartDefinition? edit, bool isDarkMode = false)`——呼び出し元を`grep`で
確認したところ現状2箇所（`MainWindow.xaml.cs`470・517行）のみで、**いずれも`_viewModel.IsDarkMode`を
明示的に渡しており、既定値`false`が実際に使われている箇所は無い**。

**家老の懸念（将来の「渡し忘れ」）は妥当な指摘と考える**。既定値付き引数は、型システムによる強制が
効かず、将来の新規呼び出し元（テストコード・別画面からの呼び出し等）が既定値`false`（ライトモード）
のまま省略した場合、ダークモード時に白背景が再発する余地を構造的に残す。これは`onmitsu.md`
「型強制不足の観点確認」節（PR-06）と近い性質——「正しい呼び出し」が型システムでなく人的注意力に
依存する。

一方で実害は限定的——(1)本ダイアログの呼び出し元は「自作パーツ編集」という限られた2導線のみで
頻繁に増える性質のコードではない、(2)見た目が浮くだけで機能的な破壊（クラッシュ・データ損失）には
至らない。**要修正とまでは言わないが、軽微な設計上の弱点として申し送る**。必須引数化（既定値を外す）
も選択肢だが、可読性・呼び出し簡便性とのトレードオフであり家老・侍の裁量に委ねる。

## 6. 既知トラップ・PR-27の目

- **SetProperty早期return罠（PR-03）**：`Theme`セッタの`ReferenceEquals`早期returnは、値が偶然一致
  してクリア処理がスキップされるPR-03の典型パターンとは逆方向（参照が違えば必ず実行される安全側の
  設計）。該当なし
- **PR-13型（CanEditDiagramガード漏れ）**：文字ツールはモーダルダイアログ内のツールでCanEditDiagram
  概念の対象外（既存増分2・3-a・3-b1/b2レビューと同じ結論）。該当なし
- **PR-27（対称性・退化入力）**：本コミットは新規テストを含まない（`MergeCollinearLines`のテストは
  別途侍へ采配済みとのこと、追ってレビュー）。実装ロジック自体（`PlaceText`の座標計算等）も既存の
  座標変換・Undo記録ロジックの再利用のみで、新規の対称性の罠が入り込む余地は見当たらない

## 不明点

なし。

## 派生提案

観点5（既定値付き引数）は経過観察として申し送るのみ、自らタスク化しない。

---

## `MergeCollinearLines`回帰テストの検算（コミット`64b1ac0`、テスト17件・実装無改変）

対象：`tests/Ecad2.Core.Tests/PartOptimizerTests.cs`（新設213行）。3-b1と同型の構図
（侍の見立てが実際のテストで塞がれているか）で検算した。

### 1. 侍が塞いだ2件（片軸のみ一致）の手計算裏取り

`PartOptimizer.Near`の実装は`Math.Abs(x1-x2) < eps && Math.Abs(y1-y2) < eps`（正しい`&&`）。
`MergeCollinearLines_OnlyOneAxisMatches_KeepsBoth`の2ケースを検算：

- **ケース1（X座標のみ一致）**：`a=Line(1,1,3,2)`（終点`(3,2)`）、`b=Line(3,8,5,9)`。
  `Near(a.X2,a.Y2,b.X1,b.Y1)=Near(3,2,3,8)`——X差`0<eps`（真）、Y差`6>eps`（偽）。**正しい`&&`実装では
  偽**（他3分岐も両軸とも`eps`超で偽、`found=false`→2本のまま、期待値と一致）。**`||`に取り違えると
  この分岐が真になり誤マージされ`result.Count=1`となって確実にRED**——手計算で裏取り完了
- **ケース2（Y座標のみ一致）**：`a`終点`(3,2)`、`b=Line(9,2,11,3)`。`Near(3,2,9,2)`——X差`6>eps`
  （偽）、Y差`0<eps`（真）。同様に`&&`なら偽（2本のまま）、`||`なら誤マージされ確実にRED

**2件とも侍の実測どおり検出力を持つことを確認した。**

### 2. 残る穴の検討（家老指摘3点）

**(a) 外積による平行判定・向きが逆のケース**：実装は`Math.Abs(adx*bdy - ady*bdx) > eps`——絶対値判定
のため、`b`の方向ベクトルが逆向き（`-1`倍）でも外積の絶対値は不変（`adx*(-bdy)-ady*(-bdx) =
-(adx*bdy-ady*bdx)`、絶対値同一）。**符号反転に対して原理的に頑健な実装**。既存の
`MergeCollinearLines_AnyEndpointPairing_KeepsOuterEnds`（Theory4ケース）が順方向・逆方向の全4組合せ
（コメントに明記）をカバーしており、向きに関する懸念は既存テストで解消されている。追加の穴は
見当たらない

**(b) 許容誤差の境界値**：`WithinTolerance`（ズレ`1e-6`、`eps=1e-5`より内側）・`BeyondTolerance`
（X差`1e-3`・Y差`5e-4`、両方とも`eps`より外側）の2点を検算し期待値と一致することを確認した。
**軽微な指摘**：この2点は「十分内側」「十分外側」であり、境界そのもの（ちょうど`eps`）や「片方の軸
だけが境界をわずかに超える」という組み合わせのテストは無い。ただし後者相当の検出力は`OnlyOneAxisMatches`
（片軸のみ大きく離れる）で別途確保されており、実害は低いと判断する。修正必須ではない

**(c) `NonLinePrimitives`混在時の扱い**：`MergeCollinearLines_NonLinePrimitives_ArePassedThrough`を
実装ループ（`if (list[ii] is not PartLine a) continue;`）と突き合わせ、Line以外の要素（`PartCircle`・
`PartText`）は変更されず`continue`でスキップされることを確認。`Assert.Same`による参照同一性の検証も
適切。問題なし

### 3. 繰り返し走査（whileループ）の検算

`ThreeSegmentChain_BecomesOne`（3本連鎖）を実装のwhileループに沿って手計算でトレースした——
1周目で`(1,1,3,2)`+`(3,2,5,3)`→`(1,1,5,3)`（1本消え2本に）、2周目で`(1,1,5,3)`+`(5,3,7,4)`→
`(1,1,7,4)`（1本に）、3周目は候補なくwhile終了。期待値`X1=1,Y1=1,X2=7,Y2=4`と一致し、繰り返し走査が
正しく機能することを確認した。

### 4. テスト名・アサーションの整合、重複確認

17件（Fact 11件＋Theory 2件×計6ケース）の名前とアサーションを一つずつ突き合わせ、いずれも内容と
整合していることを確認した。重複は見当たらない——各テストが異なる観点（端点一致・平行判定・許容
誤差・非Line素通し・位置保持・繰り返し走査・不変性）を分担している。

### 結論

要修正なし。侍が自ら発見・補強した2件は手計算で検出力を確認でき、家老指摘3点（外積の向き・許容
誤差境界・NonLinePrimitives）もいずれも既存テストで十分カバーされている（許容誤差の厳密境界値
未検証という軽微な点のみ、実害低く修正不要と判断）。3-b3、これにて決着でよいと存ずる。
