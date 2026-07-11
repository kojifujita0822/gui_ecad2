# T-033 増分2 再レビュー（隠密、往復2周目=修正上限）

> 2026-07-07 隠密レビュー。対象コミット `5c73b66`（`fix(app): T-033増分2 - 配置バーMargin残留による自己参照汚染を修正(往復2周目)`）。
> 診断ログ一次パス実測（`docs-notes/ecad2-t033-diag-pass1-analysis-ninja.md`、8ケース）で三者
> （侍診断・隠密独立結論・家老検算）完全一致した真因（Measure()呼び出し時のMargin残留による
> 自己参照フィードバックループ）への対処の再レビュー。家老指定観点(1)〜(4)＋`code-review`スキル
> （medium、8角度finder→1-vote verify）併用。**上限消化ゆえ最大限慎重に検証した。**

---

## 結論：**クリーン。忍者の再検証（診断ログ付き数値証明）へ回してよい**

---

## 対象差分

`git show 5c73b66 -- src/Ecad2.App/MainWindow.xaml.cs` で確認。純粋な4行追加（コメント3行＋コード1行）
のみ。`PositionPlacementBar`メソッド内、`ElementPlacementBar.Measure()`呼び出し直前に
`ElementPlacementBar.Margin = new Thickness(0);` を追加。

---

## 家老指定観点の検証

### (1) リセット位置がMeasureより確実に前か・他経路での再汚染の隙 —— **問題なし**

- 追加行(656行目)と`Measure()`呼び出し(657行目)は同一メソッド内で連続する同期文であり、間に他処理は
  一切挟まっていない。確実に前で実行される。
- Grepで`ElementPlacementBar.Margin`の書き込み箇所を全数確認：`PositionPlacementBar`メソッド内の656行目
  （リセット）・669行目（最終位置設定）の**2箇所のみ**。`PositionPlacementBar`自体の呼び出し元も
  `TryPlaceElement`（622行目）の**1箇所のみ**。
- XAML側（`MainWindow.xaml`474-477行目、`ElementPlacementBar`のBorder定義）にMargin属性・Style・
  DataTrigger・PropertyTriggerは一切存在しない（既定値Thickness(0)からスタート）。`Window.Resources`内の
  既存Style（`ToolBarButtonStyle`・`ToolBarKeyLabelStyle`）もTargetTypeがButton/TextBlockでBorderには
  無関係。他経路での再汚染の隙はない。
- `ClosePlacementBar`（730行目付近）はMarginに一切触れず、次回表示時のリセット（656行目）に委ねる設計。
  意図と矛盾なく成立している。

### (2) Margin=0リセットがクランプ計算・最終Margin設定と干渉しないか —— **干渉なし**

- クランプ計算（661-664行目）は`topLeft`・`workAreaOrigin`（いずれもMarginリセットより前の650-651行目で
  確定済みの変数）のみを用いており、Marginの値には数値的に依存しない。
- 669行目の最終Margin設定はクランプ後のx,yで完全に上書きするため、656行目でリセットした0という値が
  混入する余地はない。

### (3) 1行修正に便乗した余計な変更がないか —— **なし**

diff確認済み、追加4行（コメント3行＋コード1行）以外の変更は皆無。

### (4) 実機の実構造前提での理屈の再検証（前回onmitsu-4の反省を踏まえ） —— **確認済み**

前回（onmitsu-4）は最小WPF再現プログラム（RootLayoutGridのRow0/Row1のAuto行測定のみ）で検証し、
ScrollViewer・LayoutTransform（ズーム）・IsEnabled連動という実機の複雑な相互作用を見落として誤判定した。
今回は以下の観点で実機の実構造を踏まえて再検証した：

- 真因自体は診断ログの実機実測（8/8ケース完全一致、`docs-notes/ecad2-t033-diag-pass1-analysis-ninja.md`）
  で既に直接証明されており、理論のみに依拠していない。
- `code-review`スキルのAngle A finderが提起した新規懸念（「`topLeft`/`workAreaOrigin`を求める
  `TranslatePoint`呼び出しがMarginリセットより前に実行されており、もし`ElementPlacementBar`の配置行が
  Auto行ならその高さが子要素のMargin/DesiredSizeに依存し、間接的にTranslatePointの結果が汚染されうる」）
  について、`MainWindow.xaml`65-71行目でRootLayoutGridのRow2（`ElementPlacementBar`の配置行）が実際には
  `Height="*"`（Star行）でありAuto行ではないことを確認した上で、RootLayoutGridと同型のRow構成
  （Auto/Auto/*/Auto/Auto）を再現した最小WPFプログラムで実測検証した。結果：Margin・DesiredSizeを
  (0,0)〜(5000,5000)まで極端に振ってもRow2(Star行)の`ActualHeight`は完全に不変（580.00固定）。
  **REFUTED**（WPF仕様上、Star行の高さは単独配置された子要素のサイズに依存しないため、この間接汚染経路
  は存在しない）。

---

## `code-review`スキル併用の追加所見

8角度finder（line-by-line／removed-behavior／cross-file／Reuse／Simplification／Efficiency／
Altitude／Conventions）を実行し、Reuse・Efficiency・Conventions・cross-file・removed-behaviorの5角度は
該当なし。line-by-line（Angle A）・Altitudeの2角度から候補が出たため、それぞれ1-vote verifyを実施。

### 所見1: Auto行経由の間接汚染説 —— **REFUTED**（上記(4)参照）

### 所見2（技術的負債・現状バグではない）: Marginの位置決め／測定リセット値としての二重役割

**判定: PLAUSIBLE（将来リスク、修正不要・記録のみ）**

`PositionPlacementBar`は`ElementPlacementBar.Margin`を「位置決め」（669行目）と「測定前のリセット値」
（656行目）という2つの役割で使い回している。verifyエージェントの評価：WPFの`Measure()`が
`DesiredSize=コンテンツ+Margin`を返す仕様がある以上、「Marginで絶対位置決めする設計」を採る限り
656行目のリセットは**必要十分な正しい対処**であり、対症療法とは言い切れない。ただし、より根本的な設計
（`Canvas.Left/Top`添付プロパティを使えば、位置決めとMeasure/Arrangeが構造的に完全分離され、この種の
自己参照汚染はクラスごと発生しえない）と比べると、Margin方式という土台自体は残っている。

将来のリスクシナリオ：現状バー表示中のウィンドウリサイズに追随する再クランプ機能（経過観察項目、
`docs/archive/ecad2-t033-review-onmitsu-4.md`所見9）が実装される場合、新しい`SizeChanged`ハンドラ等が独自に
`ElementPlacementBar.Measure()`を呼ぶ際、656行目のリセット手順を複製し忘れると同種のバグが別経路から
再発しうる。現時点で実在するバグではなく、まだ存在しない呼び出し経路を仮定した予見的リスクのため
CONFIRMEDではない。**P-025リファクタ等、Margin/Measureに再度手を入れる機会があれば`Canvas.Left/Top`化を
検討候補として記録する。**

---

## 忍者への申し送り

- 修正5c73b66は静的検証・実測ベースの検証（診断ログ8ケース完全一致＋Grid仕様の最小プログラム実測）の
  両面でクリーン。実機での数値証明（診断ログは家老指示により温存中）による最終確認へ進めて良い。
- 確認観点：行1/6/10・同一セル開き直し複数回・ズーム150%（100%/150%とも）で、診断ログの`barDesiredSize`
  が`barActualSize`（実サイズ、想定540×38）と一致し続けること（前回は前回Margin分だけ乖離していた）。
  一致すれば真因解消の直接証拠となる。
- 診断ログ自体は忍者検証OK後に別コミットで削除予定（侍コミットメッセージ記載通り）。

---

## 出典・参照

- 対象コミット `5c73b66`（`git show`で全差分確認）
- `docs-notes/ecad2-t033-diag-pass1-analysis-ninja.md`（診断ログ実測、8ケース対応表）
- `docs/archive/ecad2-t033-review-onmitsu-4.md`（前回レビュー、往復1周目クリーン→実機NGで再調査に至った経緯）
- `src/Ecad2.App/MainWindow.xaml`（65-71/83-90/474-477行目、RootLayoutGrid行構成・ElementPlacementBar定義）
- `src/Ecad2.App/MainWindow.xaml.cs`（599-670行目、`TryPlaceElement`/`PositionPlacementBar`）
- `code-review`スキル（medium、8角度finder→1-vote verify、REFUTED1・PLAUSIBLE1・該当なし6角度）
- verifyエージェントによる実測検証（RootLayoutGridと同型のGrid構成をdotnet runで実行、Star行測定仕様の
  確認、scratchpad内・src/への書き込みなし）
