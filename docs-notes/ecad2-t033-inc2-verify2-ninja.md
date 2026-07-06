# T-033 増分2 修正（往復2周目、5c73b66）再検証（忍者）

> 対象コミット `5c73b66`（Margin残留自己参照汚染の修正、`ElementPlacementBar.Margin=Thickness(0)`を
> Measure()前に追加）。隠密再レビュー `docs/ecad2-t033-review-onmitsu-5.md`（クリーン）を受けての実機
> 再検証。診断ログ（T-038運用、温存中）による数値証明も併せて実施。
> ログ全文: `docs-notes/ecad2-t033-diag-pass2-ninja.log`

## 結論サマリ：**全観点OK。位置バグ・非決定性ともに解消を確認。**

| 観点 | 判定 |
|---|---|
| (1) 一次パスと同一条件の再現（行1/6/10・開き直し3回・ズーム150%行6/7） | OK |
| (2) ログ数値証明（barDesiredSize=barActualSize一致、開き直しでpostClamp同一値） | OK |
| (3) 目視（セル直下表示） | OK |
| (4) 軽い回帰（配置確定・Esc） | OK |

---

## 観点1・2: 位置の再現＋数値証明

**行1（100%）**: `barDesiredSize=(540.00,38.00)` `barActualSize=(540.00,38.00)`（**一致**）。
`postClamp=(299.59,219.57)`=`translateOut`と同一（クランプ不発動）。セル直下表示、一次パスと同じ結果。

**行6（100%）**: `barDesiredSize=barActualSize=(540.00,38.00)`（**一致、一次パスでは(839.59,257.57)と
乖離していた箇所**）。`postClamp=(299.59,389.65)`=`translateOut`と完全一致。**旧症状「セル真上」は解消、
セル直下に表示**。
証跡: `docs-notes/images/t033-verify2-row6-ninja.png`

**行10（100%）**: `barDesiredSize=barActualSize=(540.00,38.00)`（一致）。`postClamp=(299.59,525.71)`=
`translateOut`と完全一致（クランプ不発動）。**旧症状3（最上部表示）は解消、セル直下に表示**。
証跡: `docs-notes/images/t033-verify2-row10-ninja.png`

**行10、同一セル開き直し3回**: 3回とも`barDesiredSize=barActualSize=(540.00,38.00)`、
`postClamp=(299.59,525.71)`で**完全に一致**（小数点以下含め同一値）。一次パスで観測された
`257.57⇔359.47`のブレは消滅。**症状4（非決定性）は解消**。
証跡: `docs-notes/images/t033-verify2-row10-reopen1/2/3-ninja.png`

**ズーム150%行6**: `barDesiredSize=barActualSize=(540.00,38.00)`、`postClamp=(337.39,529.49)`=
`translateOut`と一致（クランプ不発動）。一次パスで最上部に飛んでいた症状は解消。

**ズーム150%行7**: `barDesiredSize=barActualSize=(540.00,38.00)`（一致）。`preClamp=(337.39,580.51)`に
対し`postClamp=(337.39,541.04)`と**画面下端クランプは正しく発動**（`clampMax.Y=541.04`）。これは
`workAreaOrigin+workAreaSize-barActualSize`による正当な画面端クランプであり、一次パスで見られた
「過大なDesiredSizeによる誤クランプ」とは性質が異なる（実サイズに基づく妥当なクランプ）。目視でも
セル直下・画面端でのわずかな押し上げのみで、セル自体を覆い隠してはいない。
証跡: `docs-notes/images/t033-verify2-zoom150-row6-ninja.png` / `-zoom150-row7-ninja.png`

---

## 観点3: 目視（セル直下表示）

上記スクショ群にて、行1/6/10・ズーム150%行6/7いずれもバーが選択セルの直下に表示されることを確認
（一次パスで崩れていた行6・行10・ズーム150%行6/7も含め全ケースで解消）。

## 観点4: 軽い回帰

- **配置確定（OK）**: ズーム150%行7でa接点+機器名「X1」をOK確定→機器表に「X1 / Other」反映。回帰なし。
  証跡: `t033-verify2-regression-place-ninja.png`
- **Esc**: 行5で配置バー表示中にEscを押下→バーが正しく閉じ、選択セルの枠のみ残る状態に戻ることを
  スクリーンショットで確認（ステータスバー文言だけでは変化が読み取れないため、必ず画面で裏取り）。
  証跡: `t033-verify2-regression-esc-ninja.png`

## 範囲外検出・不明事項

なし。

## 診断ログについて

温存されたままのため、本パスでも記録された（`canvasHostIsArrangeValid` `canvasHostIsMeasureValid`
`barIsMeasureValid`の3項目が追加されている、侍による二次パス向け追加分と思われる）。解析は侍・隠密に
委ねる。忍者としては、全ケースで`barDesiredSize=barActualSize`の一致が確認できたことのみ報告する。
