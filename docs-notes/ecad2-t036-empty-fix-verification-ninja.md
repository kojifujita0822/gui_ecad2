# T-036空文字修正 再確認記録（忍者）

対象: f985490（デバイス名空文字確定時の機器表孤立残存を修正）。隠密レビュー合格済み。
前回検証(`docs-notes/ecad2-t036-fix-verification-ninja.md`)で拙者が発見した観点3の問題への対応。

## 観点別結果

| # | 観点 | 判定 | 所見 |
|---|------|------|------|
| 1 | 空文字確定(Enter/Tab/欄外クリック各経路)で機器表から旧名が消える(参照なし) | OK | 3経路とも個別に検証。M1(Enter)/TABTEST(Tab)/OUTTEST(欄外クリック)いずれも空文字確定後、機器表から正しく消去(0件)。 |
| 2 | 同名2要素共有→片方だけ空文字確定→機器表に名前が残る(参照保持) | OK | a接点・コイル両方へ"SHARED"設定→a接点側のみ空文字確定→コイル側はSHAREDのまま表示継続、機器表にもSHARED 1件残存(参照保持の正しい動作)。 |
| 3 | 通常リネーム(既存名→別名)・空文字からの再入力の回帰なし | OK | SHARED→M2の通常リネームで機器表も正しく更新(重複なし)。空文字状態からREENTRYへの新規再入力も機器表・キャンバスへ正しく反映(M2・REENTRYの2件)。 |

## 総括

前回検証で発見した「空文字確定時の機器表孤立残存」は解消を確認。同名共有時の参照カウント
（`DeleteSelectedElement`と同一ポリシー）も正しく機能しており、範囲外の気づきなし。
殿の実操作確認へ進めて差し支えないと判断する。

## スクリーンショット

`%TEMP%\claude\C--ECAD2\c62bb213-5b11-418b-80e8-5942a95caa6e\scratchpad\` 配下:
`t036fix2_enter_empty.png` / `t036fix2_tab_empty.png` / `t036fix2_outclick_empty.png` /
`t036fix2_shared_setup.png` / `t036fix2_shared_partial_clear.png` / `t036fix2_rename_normal.png` /
`t036fix2_reentry.png`
