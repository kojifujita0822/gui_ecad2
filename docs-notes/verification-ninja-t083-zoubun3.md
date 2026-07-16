# T-083増分3（ダイアログ・固定色パネルのUIクローム対応）実機検証記録

検証者: 忍者（key=1784187128338） / 検証日: 2026-07-16
対象コミット: 3efca6b（`src/Ecad2.App/`配下）
検証方法: 前回の教訓（目視誤読）を踏まえ、色判定は全て座標指定の画素採取（`Bitmap.GetPixel`）で実施。

## 結論（先出し）

観点1（ダイアログ背景・ラベル文字）・観点2（3固定色パネル本体）・観点4（既存機能回帰）はOK。
**観点3（隠密指摘のTextBox入力欄）は明確にNG**——確認した全てのダイアログ・パネルで、入力欄
だけが白背景（#FFFFFF）のまま残っており、ダーク背景の中で浮いて見える。

## 観点別 OK/NG

### 観点1：5ダイアログの背景・文字色のLight/Dark連動 — OK（ただし観点3のNGを内包）

いずれもダイアログ背景は#2D2D30系へ正しく暗色化、ラベル文字も視認可能な明色へ切り替わっていた。

| ダイアログ | 背景・ラベル | 入力欄(TextBox) |
|---|---|---|
| AboutDialog（バージョン情報） | OK（入力欄なし） | 該当なし |
| AddSheetDialog（シート追加） | OK | **NG（白のまま、画素実測#FFFFFF）** |
| DocumentInfoDialog（ドキュメント情報） | OK | **NG（8欄全て白のまま）** |
| RenameDialog（シート名の変更） | OK | **NG（白のまま）** |
| SheetSettingsDialog（シート設定） | OK | **NG（3欄全て白のまま）** |

（`t083-3-aboutdialog-darkon.png`, `t083-3-addsheetdialog-darkon.png`,
`t083-3-documentinfo-darkon.png`, `t083-3-renamedialog-darkon.png`,
`t083-3-sheetsettings-darkon.png`）

**補足（範囲外の気づき）**：DocumentInfoDialogは検証開始時`IsEnabled=False`で一度Invokeが
「認識できないエラーです」で失敗した。シートが0件（前段の検証でキャンセルしていたため）だと
無効化される仕様と見られ、シート追加後は正常に開けた。バグではなく前提条件不足だった。

### 観点2：3固定色パネルの暗色連動 — OK（パネル本体）、観点3は同様にNG

- **FindBar**（Ctrl+Fで開く）：パネル背景#2D2D30で正しく暗色化。ただし検索欄
  （FindQueryBox）・置換後欄（FindReplaceBox）は共に白のまま（#FFFFFF）。
  （`t083-3-findbar-darkon.png`）
- **ElementPlacementBar**（要素配置時のインラインバー、a接点で確認）：ラベル部分の背景は
  #2D2D30で正しく暗色化。デバイス名入力欄（PlacementDeviceNameBox）は白のまま（#FFFFFF）。
  （`t083-3-placementbar-darkon.png`）
- **RungCommentEditor**（F2キーで開く行コメント編集）：入力欄（RungCommentBox）自体が白の
  まま（#FFFFFF）。パネル自体が「テキストボックス1個」の構成のため、パネル本体≒入力欄の状態。
  （`t083-3-rungcomment-darkon.png`）

侍殿が「キー送信不安定で実機確認未了」と報告していたFindBar（Ctrl+F）・RungCommentEditor
（F2）は、いずれも今回問題なく開閉でき、キー送信自体の不安定さは再現しなかった。

### 観点3：TextBox入力欄の白浮き【隠密指摘】 — **NG（確定）**

上記の通り、確認した全てのダイアログ・パネルで入力欄が白背景（#FFFFFF）のまま残っている。
隠密殿の懸念（Background/Foreground未指定でWPF既定の白背景のまま）が実機でも裏付けられた。
ダーク背景（#2D2D30）の中で入力欄だけが白く浮いて見え、視認性・統一感の観点で対応が要る。

### 観点4：既存機能への回帰 — OK

- 層A（キャンバス色）：黒背景・グレーグリッド線で正しく暗色化、回帰なし
- 増分1層B（AvalonDockドッキングクローム）：非アクティブ#2D2D30、アクティブ#007ACC(不変)と
  前回の再検証結果通りで一致、回帰なし
- 増分2（ツールバー本体・メニューバー）：いずれも暗色化維持、回帰なし

## スクリーンショット一覧

`C:\Users\kojif\AppData\Local\Temp\claude\C--ECAD2\94da924d-f90e-4959-a6f5-2c5199b52c6c\scratchpad\`
配下：`t083-3-aboutdialog-darkon.png`, `t083-3-addsheetdialog-darkon.png`,
`t083-3-documentinfo-darkon.png`, `t083-3-renamedialog-darkon.png`,
`t083-3-sheetsettings-darkon.png`, `t083-3-findbar-darkon.png`,
`t083-3-placementbar-darkon.png`, `t083-3-rungcomment-darkon.png`
（一時ディレクトリのため恒久保存が要る場合は家老の指示で`docs-notes/`等へ移す）
