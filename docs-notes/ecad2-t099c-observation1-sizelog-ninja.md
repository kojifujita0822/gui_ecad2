# T-099(c) 観点1 サイズ推移ログ実測（忍者、2026-07-19）

侍計装（`PlacementToolBarDockingManager.SizeChanged`、`%TEMP%\ecad2-diag.log`へ
`ActualWidth`/`ActualHeight`/`MinWidth`記録）を用い、フロート化→再ドッキングを再現しながら
生ログを採取した（加工・要約せず原文のまま記載）。

## 生ログ（タイムスタンプ順）

### 起動直後（初期ドッキング状態、正常時の基準値）
```
20:05:57.858 PlacementToolBarDockingManager_SizeChanged: ActualWidth=100, ActualHeight=0, MinWidth=100
20:05:58.062 PlacementToolBarDockingManager_SizeChanged: ActualWidth=100, ActualHeight=18, MinWidth=100
20:05:58.073 PlacementToolBarDockingManager_SizeChanged: ActualWidth=581, ActualHeight=103.96000000000001, MinWidth=100
20:05:58.080 PlacementToolBarDockingManager_SizeChanged: ActualWidth=581, ActualHeight=93, MinWidth=100
```
→ 最終収束値: **ActualWidth=581, ActualHeight=93**（MinWidth=100は初期の測定過程でのみ関与、
最終的にはコンテンツ幅581が優越）。

### フロート化直後（低速ドラッグ、80ステップ/30ms間隔、終点500ms静止で実行）
```
20:06:20.215 PlacementToolBarDockingManager_SizeChanged: ActualWidth=100, ActualHeight=18, MinWidth=100
```
→ **ActualWidth=100（MinWidthちょうど、下限保証は機能している）、ActualHeight=18まで縮小**。
横方向はMinWidthで下限が守られるが、**縦方向には制約が無く大きく縮小する**。
（`EnumWindows`で可視ウィンドウ数2を確認、フロート化成立）

### 再ドッキング試行後（低速ドラッグ＋終点1秒静止、極小領域中心=絶対(2024,153)を狙う）
```
20:07:05.877 PlacementToolBarDockingManager_SizeChanged: ActualWidth=100, ActualHeight=68, MinWidth=100
20:07:05.882 PlacementToolBarDockingManager_SizeChanged: ActualWidth=581, ActualHeight=68, MinWidth=100
```
→ **可視ウィンドウ数は1に戻り再ドッキングは技術的に成立**。しかし最終値は
**ActualWidth=581, ActualHeight=68**——初期状態の`ActualHeight=93`と比べ**25px不足**している。
視覚的にも配置ツールバー全体が高さ約20〜25px程度の細い水平帯となり、**ボタン列がほとんど
見えない異常なレイアウト**になった（スクリーンショット`t099c-obs1-after-redock.png`、UIA実測
`ControlType.Tab Bounds=1974,144,569,25`＝高さ25px、初期状態の同要素は高さ81pxだった）。

## 所見（忍者の推測、一次確認は侍・隠密に委ねる）

1. **MinWidth=100は横方向の下限保証としては機能している**（フロート化直後、ActualWidthが
   ちょうど100で下げ止まる）。
2. **縦方向（高さ）には対応する下限制約が存在しない**ため、フロート化直後にActualHeightが
   18まで縮小する。UIA実測（前回報告、高さ6px）と多少差はあるが「大きく縮小する」傾向は一致。
3. 再ドッキング後の最終値（ActualHeight=68）が初期状態（93）より25px少ない点は、観点3で
   実測した「上段Opacity=0領域の高さ約25px」との一致が気になるが、これが直接の因果かは
   忍者の実機観測だけでは判断できない（コード上の内訳確認が必要）。
4. **再ドッキング自体は複数回試行すると成立するが、その都度異なる異常レイアウト（前回=縦長
   別タブ化、今回=横長の潰れた帯）になっており、再現するたびに結果が変わる不安定さ**が
   見られる。ドロップ座標・タイミングへの感度が高いことを示唆する。

## 証跡ファイル
- `%TEMP%\ecad2-diag.log`（該当区間: `20:05:57`〜`20:07:05`）
- `%TEMP%\claude\ecad2\t099c-obs1-after-redock.png`：再ドッキング後の潰れたレイアウト
