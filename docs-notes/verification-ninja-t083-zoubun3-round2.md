# T-083増分3 往復2周目（TextBox白浮き修正）実機検証記録

検証者: 忍者（key=1784187128338） / 検証日: 2026-07-16
対象コミット: 5867a51（新規`InputBackgroundBrush`/`InputForegroundBrush`導入）
検証方法: 全て座標指定の画素採取（`Bitmap.GetPixel`）で判定。

## 結論（先出し）

未確認分（RenameDialog・SheetSettingsDialog・FindBar検索/置換欄・ElementPlacementBarデバイス名
欄・RungCommentEditor）全てで白浮きが解消され、暗色（#3C3C3C）に正しく修正されていることを
確認した。侍殿が確認済みのAddSheetDialog・DocumentInfoDialogと合わせ、観点3のNGは全解消。

## 画素採取結果

| 対象 | 実測値 | 判定 |
|---|---|---|
| RenameDialog（新しいシート名欄） | #3C3C3C | OK（修正確認） |
| SheetSettingsDialog（行数欄） | #3C3C3C | OK |
| SheetSettingsDialog（左母線名欄） | #3C3C3C | OK |
| SheetSettingsDialog（右母線名欄） | #3C3C3C | OK |
| FindBar（検索欄 FindQueryBox） | #3C3C3C | OK |
| FindBar（置換後欄 FindReplaceBox） | #3C3C3C | OK |
| ElementPlacementBar（デバイス名欄 PlacementDeviceNameBox） | #3C3C3C | OK |
| RungCommentEditor（RungCommentBox） | #3C3C3C | OK |

（`t083-3r2-renamedialog-darkon.png`, `t083-3r2-sheetsettings-darkon.png`,
`t083-3r2-findbar-darkon.png`, `t083-3r2-placementbar-darkon.png`,
`t083-3r2-rungcomment-darkon.png`）

**注記**：FindBarの検索/置換欄は目視では依然白っぽく見えたが、画素採取では#3C3C3Cと明確に
暗色化していた。前回の層B誤読と同型の「目視だけでは判定を誤る」ケースであり、今回も画素採取
を用いたことで正しく判定できた。

## 観点2：既存機能への回帰 — OK

検証中、層A（キャンバス色）・シート追加/削除等の基本動作に異常は見られなかった。

## スクリーンショット一覧

`C:\Users\kojif\AppData\Local\Temp\claude\C--ECAD2\94da924d-f90e-4959-a6f5-2c5199b52c6c\scratchpad\`
配下：`t083-3r2-renamedialog-darkon.png`, `t083-3r2-sheetsettings-darkon.png`,
`t083-3r2-findbar-darkon.png`, `t083-3r2-placementbar-darkon.png`,
`t083-3r2-rungcomment-darkon.png`
