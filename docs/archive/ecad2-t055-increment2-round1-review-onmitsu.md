# T-055増分2往復1周目 再レビュー（隠密）

> 2026-07-10 隠密調査。家老采配、対象=commit accaf6a（MainWindowViewModel.cs ±54行、テスト+52行）。
> 4観点で検証。code-reviewスキルはeffort=medium【必須】で実施
> （8フィンダー角度×最大6候補→重複排除6候補→1票検証、CONFIRMED1/PLAUSIBLE2/REFUTED3）。

---

## 総合判定

**要修正候補1件（CONFIRMED、UX正しさ観点）あり**。他は妥当または申し送り事項。

---

## 観点別判定

### (1) IsRowOccupied占有チェックの拒否位置・判定範囲・境界値Theory — 機能面は妥当、**文言に要修正指摘（CONFIRMED）**

**機能面**: `for (int row = settings.Rows; row < sheet.Grid.Rows; row++)`（縮小範囲=新Rows〜旧Rows-1の全行）は、DeleteRowCommandの単一行判定を正しく拡張したもの。ループはRows代入前の旧`sheet.Grid.Rows`を参照するため境界（off-by-one）は無く、拡大方向（`settings.Rows >= sheet.Grid.Rows`）では自動的にno-opになることも確認した。境界値Theory（InlineData 5/7/9=縮小範囲の先頭/中間/末尾）も妥当な境界値分析。

**要修正（code-reviewスキル、Angle A・B・Simplification三重発見、1票検証でCONFIRMED）**:
`UpdateSheetSettingsCommand`の占有時拒否メッセージが、DeleteRowCommand由来の
「最終行に要素があるため削除できません」をそのまま流用している（`MainWindowViewModel.cs:1657`）。
DeleteRowCommandは常に最終行のみが対象なのでこの文言は正確だが、UpdateSheetSettingsCommandは
縮小範囲内の任意行（先頭・中間・末尾）で拒否しうる。10行→5行縮小で要素が行7（中間行）にのみ
存在する場合でも「最終行に要素がある」と表示され、ユーザーは実際の元凶（行7）ではなく旧最終行
（行9）付近を確認しに行き、原因探しに無駄な手間を要する。テスト
`Execute_WhenShrinkRangeHasElement_SetsWarningMessage`が要素をrow=7に置いた上でこの誤った文言を
Assertしており、**誤った挙動そのものがGREEN条件として固定されている**（テスト側の見直しも必要）。

### (2) FinishRowCountChange抽出後の3コマンド既存挙動 — 概ね妥当、軽微な挙動変化1件（PLAUSIBLE）

コードの構造比較により、3コマンド（Add/Delete/Update）とも「Rows代入→（Bus名代入、Updateのみ）→
FinishRowCountChange呼び出し」の順序・内容が旧来のインライン実装と完全に一致しており、cleanup分の
回帰は無いと判断した（コミットメッセージの「全321件合格」とも整合）。

**申し送り（code-reviewスキル、1票検証でPLAUSIBLE）**: AddRowCommandは元々SelectedCellクランプを
持たなかったが、`FinishRowCountChange`抽出により新たにクランプ処理を継承した。検証エージェントが
`OutputPanelViewModel.JumpTo`（DRC出力パネルのジャンプ、行削除等でGrid.Rowsが縮小された後に
再計算されない古いDRC診断結果を経由）で理論上到達可能な経路を特定したが、**この変化は正味では
有益な調和化**（他2コマンドと同じ安全網をAddRowCommandも持つようになっただけ）であり、新たな
危険を持ち込むものではないとの判定。対応不要、記録のみ。

### (3) RED証明の整合 — 妥当

範囲チェックループ除去→要素があっても縮小できてしまいAssert失敗（RED）という論理を実装コードと
突合し、コミット記載の実測と整合することを確認した。

### (4) 便乗拡大なし — 妥当

変更はMainWindowViewModel.cs・SheetSettingsCommandTests.csの2ファイルのみ、前回指摘範囲
（IsRowOccupiedチェック追加・トリプレット重複解消）にピンポイントで対応。

---

## code-review（effort=medium）まとめ

| 候補 | 判定 |
|---|---|
| 占有時拒否メッセージが「最終行」固定文言で実際の占有行位置と乖離（3フィンダー角度が独立発見） | **CONFIRMED**（実装・テスト双方の見直しが必要） |
| AddRowCommandがFinishRowCountChange経由で新たにSelectedCellクランプを継承 | PLAUSIBLE（DRCジャンプ経由で理論上到達可能だが、正味は有益な調和化） |
| FinishRowCountChangeがtail-truncation限定で増分3のシフト意味論に対応できない | PLAUSIBLE（メソッド名・docコメント・増分3計画書とも「行数変更」専用と自己限定済み、今回の欠陥ではない） |
| DeleteRowCommand/UpdateSheetSettingsCommandの占有チェック形状が不統一 | REFUTED（2箇所は本プロジェクト自身の抽出閾値=3箇所未達、既に往復1周目で審議・裁定済みの再提起） |
| StatusMessage無条件クリアが将来の情報メッセージ表示と衝突しうる | REFUTED（増分3という未着手の仮定に基づく先読み、CLAUDE.md「要求解釈の鉄則」のYAGNI違反） |
| for-loopをLINQ Any()化すべき | REFUTED（提案コードは行拡大時に`Enumerable.Range`が負数countで例外を投げるリグレッションを持ち込む） |

---

## 出典

- 差分: `git show accaf6a`直読
- 増分2初回レビュー: `docs/ecad2-t055-increment2-review-onmitsu.md`
- code-reviewスキル実行記録: 本セッション内、Agent(finder)×8＋Agent(verifier)×6を並列実行
