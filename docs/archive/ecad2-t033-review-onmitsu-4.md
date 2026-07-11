# T-033 増分2 再レビュー（隠密、往復1周目）

> 2026-07-07 隠密レビュー。対象コミット `bfa8994`（`fix(app): T-033増分2 - 配置バーの座標系不整合を修正(往復1周目)`）。
> 前回の正式レビュー `docs/archive/ecad2-t033-review-onmitsu-3.md`（要修正・重大2件）に対する修正の再レビュー。
> 家老指定観点(1)〜(5)＋経過観察の再評価＋`code-review`スキル（medium、8角度finder→1-vote verify）併用。

---

## 結論：**要修正なし。忍者の実機確認へ進めて差し支えない**

前回CONFIRMEDの重大2件（(a)座標系不整合・(f)Tab迷子）はいずれも解消を確認した。新たに検出した論点は
「現状は実害なしだが将来再発しうる潜在的脆弱性」1件と「既知論点（右母線列の余白矩形問題）が今回も
未対応のまま残存」1件のみで、いずれも忍者の実機確認をブロックする重大度ではない。

---

## 対象差分

`git show bfa8994 -- src/Ecad2.App/MainWindow.xaml src/Ecad2.App/MainWindow.xaml.cs` で全文確認済み。

- (a) `PositionPlacementBar`の`TranslatePoint`変換先を`MainWorkAreaGrid`→ルートGrid（新設`x:Name="RootLayoutGrid"`）へ変更。クランプ基準も`workAreaOrigin`（`MainWorkAreaGrid`原点をRootLayoutGrid座標系へ変換した値）経由に変更。
- (f) プレースホルダアイコン2個（未定アイコン1/2）へ`KeyboardNavigation.IsTabStop="False"`付与。

---

## 家老指定観点の再評価

### (1) 座標系不整合の解消が正しいか —— **解消を確認**

`topLeft`（`LadderCanvasHost.TranslatePoint(point, RootLayoutGrid)`）と`ElementPlacementBar.Margin`は、
ともにRootLayoutGrid基準の値に揃った。`ElementPlacementBar`はRootLayoutGrid直下のGrid.Row="2"（列定義なし
＝全幅セル）に配置されており、現状のXAML構造では**Row0（メニュー）・Row1（ツールバー）を単独専有する
子要素が存在しないため、両行の実測高さは0**（下記「実測検証」参照）。この条件下ではRow2セルの左上が
RootLayoutGrid原点(0,0)と一致するため、`topLeft`をそのまま`Margin`へ適用する現在の実装は正しく動作する。

ただし、この解消は「Row0+Row1=0」という**現状のレイアウト状態に依存した設計**であり、`Margin`計算が
「RootLayoutGridの絶対座標」と「ElementPlacementBar自身のセル左上を基準にした相対座標」を明示的に
区別していない。この点は下記「新規所見1」として技術的負債に記録する。

### (2) クランプ基準のworkAreaOrigin変換の正しさ —— **妥当**

`maxX/maxY`は`Math.Max(workAreaOrigin, workAreaOrigin + ActualWidth/Height - barSize)`の形で、常に
`workAreaOrigin`以上になるよう構成されており、`Math.Clamp`の`min<=max`不変条件は常に成立する（例外なし）。
右端・下端でMainWorkAreaGridの外へはみ出さないという非重なり保証は、workAreaOrigin基準へ座標変換した
上でも維持されている。

verifyエージェントによる数学的検証：`MainWorkAreaGrid`と`RootLayoutGrid`の間にスケール変換
（LayoutTransform/RenderTransform）は存在せず純粋な平行移動のみ（ズーム用`ScaleTransform`は
`LadderCanvasHost`側、`MainWorkAreaGrid`よりさらに下流に付与）であるため、クランプをどちらの座標系で
行っても最終結果は数学的に同一。現状コードのままで問題ない。

### (3) RootLayoutGrid新設による副作用・退行 —— **なし**

「新設」ではなく既存の無名ルートGridへの`x:Name`命名付与のみ。構造・Row/Column定義に変更はなく、
コードビハインドでの識別子衝突（Grep確認済み）もない。退行なし。

### (4) IsTabStop=False付与がGridSplitter既存パターン踏襲か —— **踏襲されている**

既存の`GridSplitter`（XAML 300/332/357行目）と同じ`KeyboardNavigation.IsTabStop="False"`を採用。
`Focusable`はTrueのままだがTabオーダーからの除外という前回指摘の目的（Tab迷子防止）には合致しており、
既存パターンとの一貫性も保たれている。

### (5) 増分1設計の堅持 —— **崩れていない**

`IsPlacementBarVisible`単一情報源・`ClosePlacementBar`のフォーカス集約構造への介入なし。個別ガードの
追加もない。座標計算ロジックのみの変更であり、増分1で確立した設計は維持されている。

---

## 経過観察項目の再評価（前回レビューより）

- **症状4（同一セル開き直しで表示位置が非決定）**: 今回の修正は恒常的な座標系オフセットの解消であり、
  非決定性そのものの原因（Measure呼び出しタイミング等）には対応していない。座標系不整合の解消により
  非決定性も併せて解消されるのか、別要因が残るのかは**実機での再検証が必要**（前回申し送り通り）。
- **(b) クランプがセル自体を覆い隠す可能性**: 変更なし。`Math.Clamp`はMainWorkAreaGrid外周との非重なり
  のみ保証し、選択セル自体との非重なりは今回も保証されていない。
- **ウィンドウリサイズ非追随**: 変更なし。`SizeChanged`等の再配置フックは追加されていない。

---

## 新規所見（`code-review`スキル、8角度finder→1-vote verify）

### 所見1（技術的負債・現状未発火）: Margin計算が絶対座標とセル相対座標を区別していない

**判定: CONFIRMED（潜在的、ただし現状のXAML構造では未発火）**

`PositionPlacementBar`（`MainWindow.xaml.cs:649-659`）は`topLeft`・`workAreaOrigin`をいずれも
`RootLayoutGrid`の**絶対座標系**として算出し、そのまま`ElementPlacementBar.Margin`（自身の割当セルの
左上隅からの**相対値**）へ代入している。両者が一致するのは「Row2セルの左上＝RootLayoutGrid原点(0,0)」
の場合に限られ、これは「Row0+Row1の実測高さ=0」という条件と等価である。

verifyエージェントが実測（最小WPFプログラムでの`Measure`/`Arrange`直接実行、scratchpad内・src/へは
書き込みなし）で確認：
- ウィンドウ極端縮小によりAuto行(Row0/1/3)へ予算が回るシナリオ（Angle A finder提起）は**REFUTED**。
  WPFのGrid測定アルゴリズムはAuto行のサイズをRowSpan要素の必要量と無関係に決定するため、Row0/Row1を
  単独専有する子要素が存在しない限り、ウィンドウサイズによらず常に0のまま。
- 将来RootLayoutGridのRow0またはRow1に（MainContentAreaの外で）新しい子要素が単独追加されるシナリオは
  **CONFIRMED（潜在）**。その時点でRow0/Row1の実測高さが非ゼロになり、配置バーはその分だけ下にずれて
  再発する（今回のバグの発生原理と同じ「絶対座標とセル相対座標の取り違え」）。

現状のXAMLにはそのような子要素は存在しないため**現時点で顕在化している問題ではない**。修正必須では
ないが、より頑健な実装（`ElementPlacementBar`自身のセルオフセットも`TranslatePoint`で明示的に取得し
差し引く等）を将来の増分やP-025リファクタの折に検討する価値がある技術的負債として記録する。

### 所見2（既知論点の再確認）: 右母線列の余白矩形問題は今回も未対応

**判定: CONFIRMED（今回のコミット範囲外・未対応のまま残存）**

前回レビューのfinder所見2（`docs/archive/ecad2-t033-review-onmitsu-3.md:132`、PLAUSIBLE）で指摘した
「右母線列（`Column==grid.Columns`）選択時、`CellRectDip`が右母線外側の余白矩形を返し、配置バーの
表示位置と選択セルの視覚的対応が崩れる」問題は、今回の座標系修正（`TranslatePoint`変換先の変更のみ）
では一切対応されていない。`LadderCanvasHost.CellRectDip(cell)`の呼び出し自体（648行目）は不変であり、
`Key.Right`によるキーボード操作で右母線列への選択移動・配置操作は到達可能（ガードなし）と確認した。
新規バグではなく既知論点の残存であり、実機での見え方（PLAUSUBLE止まり）は前回から変化していない。

### 検討したが不採用の候補

- **Simplification提案**（`workAreaOrigin`を先に計算せずローカル座標でクランプ→最後に1回変換する代案）:
  verifyの結果、数学的には同値だが「明確に優れている」とは言えない（可読性・呼び出し回数とも実質的な
  差なし、現状コードは変換し忘れのリスクがより低い構成）。**REFUTED**、修正提案としては採用しない。
- Reuse／Efficiency／Conventions／Cross-file（呼び出し元・命名衝突）の各角度は該当なし。

---

## 忍者への申し送り（前回からの更新）

- **座標系不整合(a)・Tab迷子(f)の解消は静的観点からは確認済み**。実機での位置再検証（行1/6/10・
  同一セル開き直し複数回・ズーム150%・画面端クランプ）を進めてよい。
- 症状4（非決定性）の原因切り分けは前回申し送り通り、診断ログ（`PositionPlacementBar`呼び出し時点の
  `MainWorkAreaGrid.ActualWidth/Height`・`ElementPlacementBar.DesiredSize`・スクロールオフセット）が
  依然有効な手段。
- 右母線列（最右列）選択中の配置操作も可能であれば見た目を確認願いたい（新規指摘ではなく前回からの
  既知論点の確認、severityは低）。

---

## 出典・参照

- 対象コミット `bfa8994`（`git show`で全差分確認）
- `docs/archive/ecad2-t033-review-onmitsu-3.md`（前回正式レビュー、CONFIRMED2件）
- `src/Ecad2.App/MainWindow.xaml`（全文確認、64-90/272/328-331/401/474-489行目付近）
- `src/Ecad2.App/MainWindow.xaml.cs`（599-661行目、`TryPlaceElement`/`PositionPlacementBar`）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`CellRectDip`119-126行目）
- `src/Ecad2.Core/Rendering/GridGeometry.cs`（`X(boundary)`定義）
- `code-review`スキル（medium、8角度finder→1-vote verify、CONFIRMED2・REFUTED2・該当なし4角度）
- verifyエージェントによる実測検証（WPF Grid測定アルゴリズム、scratchpad内の最小プロジェクトで実行、
  `src/`への書き込みなし）
