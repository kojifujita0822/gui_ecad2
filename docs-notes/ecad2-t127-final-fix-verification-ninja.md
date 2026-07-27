# T-127 保存レイアウトファイル書き換え後の最終実機確認（忍者）

日付: 2026-07-27
task_id: T-127（殿裁可、家老采配）

## 実行した変更

`%AppData%\Ecad2\docking-layout\main-layout.xml`（実パス:
`C:\Users\kojif\AppData\Roaming\Ecad2\docking-layout\main-layout.xml`）9行目の1箇所のみ変更。

```diff
-      <LayoutAnchorablePane DockWidth="220">
+      <LayoutAnchorablePane DockWidth="190">
```

他の値（15行目`DockWidth="280"`、4行目`DockHeight="100"`、24行目`DockHeight="160"`、
`FloatingWidth`、`LastActivationTimeStamp`等）には一切触れていない。

## 控えの所在

変更前に`Copy-Item`で複製を作成済み（`rm`未使用、原本は削除していない）。

- 控え先: `C:\ECAD2\sample\t127-layout-backup\main-layout.xml.bak-20260727`

万一の復元が必要な場合は、この控えを`Copy-Item`で
`C:\Users\kojif\AppData\Roaming\Ecad2\docking-layout\main-layout.xml`へ上書きコピーすれば原状復帰可能。

## 検証手法

- `ecad2-ui-automation`スキルに従いセカンダリモニタで起動（リサイズ操作は行っていない）
- シート追加ダイアログのOKボタンはUIA `InvokePattern`経由
- 判定は`BoundingRectangle`・`RangeValuePattern`の数値採取。スクリーンショット（`PrintWindow`方式）
  で整合確認も実施

## 結論（明確化）

**収まった。水平スクロールバーは完全に消滅した。**

## 観点別結果

### 観点1: 水平スクロールバーが消えること

**OK**。`ScrollBar count: 1`（垂直のみ、水平は存在せず）。前回（書き換え前）は2件
（垂直・水平）検出されていたのと対照的。

### 観点2: 描画エリア実幅＝888pxの見込み

**OK、完全一致**。`CanvasArea`実測: `Bounds=2171,202,888,435` → **Width=888px**、
家老の見込みどおり。垂直ScrollBar分（実測17px）を差し引いた実効ビューポート871px ≧
理論必要幅849pxで**余裕22px**（家老試算どおり）。

### 観点3: 左パネル実幅＝182px前後

**OK、ほぼ一致**。`SheetNavList`実測: `Bounds=1978,205,182,399` → **Width=182px**、
家老の見込み「182px前後」と完全一致。190指定がそのまま反映された姿。

### 観点4: ウィンドウ幅1400px

**OK**。`Get-Ecad2WindowRect`実測: `Width=1400px, Height=800px`。

## 追加所見（P-131関連、参考情報）

垂直ScrollBarの`RangeValue.Maximum = 56.2585826771655`——これは**T-127着手前調査時、
モニタ移動を伴う`Resize-Ecad2Window`実行後**に観測した値と完全に一致する。しかし**今回は
リサイズ操作を一切行っておらず、モニタ移動も発生していない**。それにもかかわらず73.26では
なく56.26が出た。

これは前回忍者が報告した「モニタ移動が垂直Maximum変化の原因では」という仮説（P-131）と
整合しない新事実——**モニタ移動を伴わずとも、左パネル幅の縮小（220→182px、描画エリア拡大
738/858→888px）だけで垂直方向の値が変化しうる**ことを示唆する。原因技術は未特定（横幅変更が
なぜ垂直スクロール量に影響するのか不明）。深追いはしていない。P-131の調査時にはこの所見も
併せて検討する価値がある。

## スクリーンショット所見

`t127-verify-after-190.png`を目視したところ、キャンバス下部の水平スクロールバーが消え、
右母線「P24」がキャンバス枠内に完全に収まっていることを確認。数値採取の結果と矛盾せず整合する。

## 家老・殿への報告向け要約

- 保存レイアウトファイルの`DockWidth="220"→"190"`書き換えにより、**水平スクロールバーは完全に
  消滅し、全20列がスクロールなしで表示可能になった**（殿裁定どおりの結果）
- 描画エリア888px・左パネル182px・ウィンドウ幅1400px、いずれも家老の見込みと一致
- 副次的な気づきとして、モニタ移動なしでも垂直スクロールMaximumが変化する事象を新たに観測
  （P-131の追加手がかり、家老・隠密の判断に委ねる）
