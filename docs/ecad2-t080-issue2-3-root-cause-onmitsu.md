# T-080追加往復 課題2・3 根本原因調査+テスト設計(隠密、独立系統)

- task_id: T-080
- 対象: 課題2(縦コネクタ選択の巻き添えクリア)・課題3(エディタ位置の水平スクロール未考慮)
- 出典: `docs-notes/ecad2-t080-ninja-final-verification.md`観点4・範囲外検出
- 手法: 静的読解のみ。侍の並行修正は参照していない(独立系統)。
- 対象コミット: `88c9522`時点の`src/Ecad2.App/MainWindow.xaml.cs`・`MainWindow.xaml`・
  `Views/LadderCanvas.cs`・`ViewModels/MainWindowViewModel.cs`

---

## 課題2: 縦コネクタ選択の巻き添えクリア

### 結論

**根本原因を確定(高確度)**。家老の示唆どおり、**「ダブルクリックの1発目が通常クリックとして
選択解除を通る」ことが直接原因**。Down側移設(74d2783)とは無関係に、**往復1周目以前から存在する
既存コード**(`c4dd2b1`時点で既に同一箇所を確認、下記出典参照)が真因であり、T-080固有の新規回帰
ではない。

### 根拠

行コメント領域(右母線より右の余白、グリッド外)への単発クリック(ダブルクリックの1発目=
ClickCount==1)は、以下の経路をたどる:

1. **Down側**(`LadderCanvasHost_PreviewMouseLeftButtonDown`、393-411行目): 新設のダブルクリック
   判定は`e.ClickCount==2`が偽のため短絡評価で不成立(`88c9522`修正後)。既存4分岐(縦コネクタ/
   配線分断/自由線/接続点のドラッグ開始判定)も、コメント領域には該当プリミティブが存在しない
   ため全て不成立。Down側は実質何もしない
2. **Up側**(`LadderCanvasHost_PreviewMouseLeftButtonUp`、598-647行目): 同様に615-644行目の
   ヒット判定(`HitTestConnector`/`HitTestWireBreak`/`HitTestFreeLine`/`HitTestConnectionDot`)が
   コメント領域に対していずれもnullを返し不成立。**646-647行目
   `_viewModel.SelectedCell = LadderCanvasHost.ToGridPos(position); TryPlaceActiveTool();`
   に到達する**
3. `SelectedCell`のsetter(`MainWindowViewModel.cs:252-289`)は「値が変化しない場合も含め常時」
   `SelectedConnector`/`SelectedWireBreak`/`SelectedFreeLine`/`SelectedConnectionDot`をnull
   クリアする設計(T-041増分1隠密レビュー指摘、コメントに明記)。よって**1発目のクリックの時点で
   既に、選択中だった縦コネクタの選択状態が無条件にクリアされる**。2発目のクリック(ダブルクリック
   成立)を待つ必要すらない

### この経路がT-080以前から存在したことの裏取り

`git show c4dd2b1:src/Ecad2.App/MainWindow.xaml.cs`(往復1周目時点)を確認したところ、
`_viewModel.SelectedCell = LadderCanvasHost.ToGridPos(position); TryPlaceActiveTool();`は
既に626-627行目に同一の形で存在していた(T-041由来の既存ロジック、T-080固有の変更ではない)。

**なぜ今回初めて表面化したか**: 往復1周目まではダブルクリックでコメントエディタが開くこと自体が
機能していなかった(本体の(a)バグ)。1発目のクリックで選択がクリアされても、2発目でエディタが
開かないため、ユーザーは「クリックしても何も起きない」としか認識できず、選択解除に気づきにくかった
可能性が高い。往復2周目で(a)が修正されダブルクリックが機能するようになって初めて、「エディタが
開いたのに選択状態が消えた」という体験のギャップが表面化した、という説明が整合的。

### 修正の方向性(参考情報、実装は侍マター)

行コメント領域(グリッド外、ヒット領域内)への単発クリックでは、`SelectedCell`のsetterを呼ばない
(選択状態を変更しない)ようにする必要がある。案:
- Up側646行目の直前に「クリック位置が`HitTestRungCommentRow`のヒット領域内(既存コメントの有無を
  問わない)なら、選択状態を変更せず何もしてreturnする」というガードを追加する。ダブルクリックで
  ないため既存コメントは開かないが、選択状態も変更しないという「何もしない」の扱いになる
- より一般化するなら、`ToGridPos`がグリッド範囲外を返す場合は`SelectedCell`の更新自体をスキップ
  する、という汎用修正も考えられる(行コメント固有ではなく、グリッド外クリック全般の設計漏れとして
  扱う方が筋が良い可能性がある。ただし挙動変更の範囲が広がるため、侍・家老での要検討事項とする)

---

## 課題3: エディタ位置の水平スクロール未考慮

### 結論

**根本原因を確定(中〜高確度)**。`PositionRungCommentEditor`(`MainWindow.xaml.cs:1736-1752`)の
画面端クランプ処理が、実際にユーザーの目に見えるキャンバス表示領域(`CanvasArea`という
`ScrollViewer`のビューポート)ではなく、**ナビツリー・キャンバス・機器表パネル等を全て含む
中央ワークエリア全体(`MainWorkAreaGrid`)を基準にしている**ため、水平スクロールされていない状態
では、クランプが実質的に機能せず、ユーザーの可視範囲外(だが`MainWorkAreaGrid`全体の範囲内、
機器表パネルの真下)にエディタが描画されてしまう。

### 根拠

```csharp
private void PositionRungCommentEditor(int row, Ecad2.Model.Sheet sheet)
{
    var inputPoint = LadderCanvasHost.RungCommentAnchorDip(row, sheet);
    var topLeft = LadderCanvasHost.TranslatePoint(inputPoint, RootLayoutGrid);
    var workAreaOrigin = MainWorkAreaGrid.TranslatePoint(new Point(0, 0), RootLayoutGrid);
    ...
    double maxX = Math.Max(workAreaOrigin.X, workAreaOrigin.X + MainWorkAreaGrid.ActualWidth - barSize.Width);
    ...
    double x = Math.Clamp(topLeft.X, workAreaOrigin.X, maxX);
    ...
}
```

- `inputPoint`(`LadderCanvas.RungCommentAnchorDip`、`LadderCanvas.cs:231-237`)のX座標は
  `_renderer.RightBusX(sheet.Grid.Columns) + RungCommentXOffsetMm`——**行に依存しない固定値**
  (右母線位置は全行共通)。忍者実測でY座標は行ごとに正しく変化しX座標だけが固定だった事実
  (`row=0: X=2975, row=1: X=2975`)と整合する
- `MainWindow.xaml:374`で`MainWorkAreaGrid`は`Grid.Row="2"`に配置された単一のGridで、この中に
  ナビツリー(`SheetNavList`)・`GridSplitter`・`CanvasArea`(`ScrollViewer`、413行目)・
  `GridSplitter`・右パネル(機器表等)が全て含まれる構造(XAML確認済み)。つまり
  `MainWorkAreaGrid.ActualWidth`は「中央ワークエリア全体の幅」であり、実際にキャンバスが見える
  範囲(`CanvasArea`のビューポート)より遥かに広い
- `TranslatePoint`はビジュアルツリーの変換を正しく辿るため、水平スクロールされていない状態
  (左端表示)での右母線位置(画面外)の`TranslatePoint`結果は、`RootLayoutGrid`座標系でも大きな
  X値(画面外)を返すのが正しい動作。問題は後続のクランプが、この「画面外の値」を「ユーザーの
  可視範囲」ではなく「`MainWorkAreaGrid`全体の範囲」でしか制限できていない点にある。忍者の
  「機器表パネルの真下」という観測は、`MainWorkAreaGrid`内の右側カラム(機器表パネル)付近に
  クランプされていることと符合する

### 既存の類似実装(PositionPlacementBar)との関係

`PositionRungCommentEditor`のコメントは「PositionPlacementBarと同型のTranslatePoint方式
(RootLayoutGrid座標系への変換・MainWorkAreaGrid基準の画面端クランプ)を流用する」と明記している。
すなわち**この設計(MainWorkAreaGrid基準のクランプ)自体は行コメント固有の新規実装ではなく、
既存の`PositionPlacementBar`から意図的に踏襲されたもの**。`PositionPlacementBar`自体が同種の
問題を抱えている可能性があるが、配置バーは通常グリッド内のセル位置(スクロール済みの可視範囲内)
に表示されることが多く、この問題が顕在化しにくかった可能性がある(未検証、家老・侍の判断で
横展開要否を検討されたい範囲外の気づきとして申し添える)。

### 修正の方向性(参考情報、実装は侍マター)

クランプ基準を`MainWorkAreaGrid`ではなく、実際に可視のキャンバス領域(`CanvasArea`という
`ScrollViewer`のビューポート)に変更する必要がある。`ScrollViewer`は`ViewportWidth`/
`ViewportHeight`プロパティで現在の可視範囲(スクロール後のクリップ後サイズ)を提供するため、
`workAreaOrigin`の取得元・`ActualWidth`/`ActualHeight`の参照元をいずれも`MainWorkAreaGrid`から
`CanvasArea`へ差し替えるのが最小差分の案と見る。ただし`CanvasArea`自体がキャンバス以外の要素
(ナビツリー等)を含まないScrollViewerであることの確認は侍の実装時に要る。

---

## テスト設計(仕様側からの起草)

### 課題2: 単体テスト化の見立て

**部分的に単体テスト化可能**。修正が「クリック位置がコメント領域(ヒット領域内)なら選択状態を
変更しない」という判定として実装されるなら、`ShouldOpenRungCommentEditor`と同型で、判定ロジック
自体(「このクリックで選択状態を変更すべきか」を返す純粋関数)を抽出すれば単体テスト化できる。
`HitTestRungCommentRow`の結果(int?)を入力に取る形にすれば、`MouseButtonEventArgs`の構築(家老
裁定3の制約対象=`ClickCount`)には依存しないため、課題1より制約が緩い可能性がある。

観点(同値分割):
| 状態 | クリック位置 | 期待結果(修正後) |
|---|---|---|
| 縦コネクタ選択中 | コメント領域内(ヒットあり) | 選択状態を維持(クリアしない) |
| 縦コネクタ選択中 | グリッド内の空セル | 選択状態をクリア(既存仕様どおり、変更なし) |
| 縦コネクタ選択中 | 別の縦コネクタ上 | 新しい縦コネクタへ切替(既存仕様どおり、変更なし) |
| 何も選択なし | コメント領域内 | 変化なし(SelectedCellもnullのまま) |

ペア対称性: `SelectedConnector`/`SelectedWireBreak`/`SelectedFreeLine`/`SelectedConnectionDot`の
4種いずれについても同じ表が成立するか点検する(T-041増分7のカバレッジ不整合再発防止、
onmitsu.md該当節に倣う)。

回帰確認: 修正後も「コメント領域外(通常のグリッド内クリック)では従来どおり選択状態がクリアされる」
という既存仕様を壊していないことを確認する必要がある(既存のSelectedCellセッター系テストの
再実行で担保できる可能性が高い、新規テスト不要かもしれない)。

### 課題3: 単体テスト化の見立て

**View層の実レイアウト(ActualWidth・TranslatePoint)に深く依存し、単体テスト化は極めて困難**。
`ActualWidth`等はウィンドウが実際に表示されレイアウトパスが実行されて初めて意味のある値になる
ため、STAスレッド上でLadderCanvas単体をnewするだけの既存パターン(`RungCommentHitTestTests`)では
再現できない(親要素の`ScrollViewer`・`MainWorkAreaGrid`・実ウィンドウのレイアウトツリー全体が
必要)。

部分的に単体テスト化できる範囲: クランプ計算式自体(`Math.Clamp`によるX/Y座標の範囲制限ロジック)
を、`(Point topLeft, Point viewportOrigin, double viewportWidth, double viewportHeight, Size
barSize) => Point`のような純粋関数として抽出すれば、入力を全て数値で与えられるため単体テスト化
可能(境界値: topLeftが範囲内/範囲外(左右上下それぞれ)/ちょうど境界上)。ただし、これは「クランプ
計算そのものの正しさ」を保証するのみで、「実際にCanvasAreaのビューポート値を正しく取得している
か」「TranslatePointが正しい値を返しているか」はレイアウト依存のため、単体テストではカバーでき
ない。

実機観点として列挙(忍者マター):
1. 水平スクロールされていない状態(左端表示)でF2/ダブルクリックし、エディタが画面内
   (`CanvasArea`の可視範囲内)に表示されること
2. 水平スクロールされた状態(右母線側が見えている状態)でも同様に画面内に収まること(修正前の
   往復1〜2周目の検証はすべてこの状態で行われていたため退行しないことの確認を兼ねる)
3. 垂直方向で同様の問題が無いか(Y方向は既存のT-033増分2位置バグ対処=RootLayoutGrid基準の
   TranslatePointで解消済みのはずだが、クランプ基準の変更に伴い再確認を推奨)
4. ズーム率を変更した状態(`CanvasScale`≠1.0)でも正しく画面内に収まるか
5. ウィンドウリサイズ直後の位置計算が正しいか(`MainWorkAreaGrid`→`CanvasArea`への基準変更に
   伴う副作用がないか)

---

## 不明点

- 課題2の修正方針(局所ガード追加 vs グリッド外クリック全般への汎用対応)のどちらを取るかは
  影響範囲の見積もりが必要で、隠密の静的読解だけでは断定できない。侍の実装時の判断、または
  家老の采配を仰ぎたい
- `PositionPlacementBar`が課題3と同種の問題を抱えているかは未検証(範囲外の気づき、対応要否は
  家老采配)
- クランプ基準を`CanvasArea`へ変更した場合、`CanvasArea`自体がキャンバス以外の要素を含まない
  ことの確認(XAML上は`ScrollViewer`直下に`LadderCanvasHost`のみで問題なさそうに見えるが、
  実装時に再確認を推奨)

## 検証の限界

本調査は静的読解のみ(共有mainへの一時注入なし、侍の並行修正は未参照)。課題3は特にレイアウト
依存度が高く、実機確認(忍者マター)による最終裏取りが必須。
