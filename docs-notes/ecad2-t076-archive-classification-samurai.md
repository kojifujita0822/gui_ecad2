# T-076 段階1: docs/直下Markdown整理 — アーカイブ対象・残留対象・対象外 一覧（侍作成）

前提確認：現時点で `docs/` 直下は175件（`todo.md`・`todo-archive.md`を除く分類対象は173件）。
`docs/todo.md`本文の「完全Done/完了」明記＋索引の[x]、`docs/todo-archive.md`のチェックリスト＋追記節から
Done/Rejected済みタスク番号57件、進行中・未着手タスク番号19件（T-013,022,028,032,044,046,058,060,061,
064〜071,075,076）を確定。ファイル名は`ecad2-t{番号}-...`の厳密パターン（先頭一致）で機械抽出し、
番号なしファイルは`docs/README.md`・`docs/proposed.md`・`CLAUDE.md`・関連メモリ記録を突合して個別判定。

## アーカイブ対象（136件）

| ファイル名 | 対応タスク番号 | 判定根拠 |
|---|---|---|
| archive/ecad2-t015-implementation-plan-samurai.md | T-015 | Done |
| archive/ecad2-t015-review-onmitsu.md | T-015 | Done |
| archive/ecad2-t015-scope-redefinition-options-onmitsu.md | T-015 | Done |
| archive/ecad2-t019-implementation-plan-samurai.md | T-019 | Done |
| archive/ecad2-t019-review-b7844d2-onmitsu.md | T-019 | Done |
| archive/ecad2-t019-review-d9aa49b-onmitsu.md | T-019 | Done |
| archive/ecad2-t019-review-onmitsu.md | T-019 | Done |
| archive/ecad2-t021-enter-placement-survey-onmitsu.md | T-021 | Done |
| archive/ecad2-t021-focus-design-consolidation-plan-onmitsu.md | T-021 | Done |
| archive/ecad2-t021-implementation-plan-samurai.md | T-021 | Done |
| archive/ecad2-t021-increment-v-review-340f53d-onmitsu.md | T-021 | Done |
| archive/ecad2-t021-keyboard-spec.md | T-021 | Done |
| archive/ecad2-t029-presurvey-onmitsu.md | T-029 | Rejected（取り止め、殿裁定2026-07-09）。取り止めも整理対象 |
| archive/ecad2-t033-implementation-plan-samurai.md | T-033 | Done |
| archive/ecad2-t033-poc-result-samurai.md | T-033 | Done |
| archive/ecad2-t033-review-onmitsu.md | T-033 | Done |
| archive/ecad2-t033-review-onmitsu-2.md | T-033 | Done |
| archive/ecad2-t033-review-onmitsu-3.md | T-033 | Done |
| archive/ecad2-t033-review-onmitsu-4.md | T-033 | Done |
| archive/ecad2-t033-review-onmitsu-5.md | T-033 | Done |
| archive/ecad2-t033-review-onmitsu-6.md | T-033 | Done |
| archive/ecad2-t033-review-onmitsu-7.md | T-033 | Done |
| archive/ecad2-t033-review-onmitsu-8.md | T-033 | Done |
| archive/ecad2-t033-ui-automation-impact-survey-onmitsu.md | T-033 | Done |
| archive/ecad2-t034-review-onmitsu.md | T-034 | Done |
| archive/ecad2-t035-review-2-onmitsu.md | T-035 | Done |
| archive/ecad2-t035-review-onmitsu.md | T-035 | Done |
| archive/ecad2-t036-fix-review-onmitsu.md | T-036 | Done |
| archive/ecad2-t036-observation6-onmitsu.md | T-036 | Done |
| archive/ecad2-t037-or-ui-proposals.md | T-037 | Done |
| archive/ecad2-t037-review-onmitsu.md | T-037 | Done |
| archive/ecad2-t037-review-onmitsu-2.md | T-037 | Done |
| archive/ecad2-t037-review-onmitsu-3.md | T-037 | Done |
| archive/ecad2-t039-design-comparison-onmitsu.md | T-039 | Done |
| archive/ecad2-t039-implementation-review-onmitsu.md | T-039 | Done |
| archive/ecad2-t039-re-review-2-onmitsu.md | T-039 | Done |
| archive/ecad2-t039-re-review-onmitsu.md | T-039 | Done |
| archive/ecad2-t040-wire-survey-onmitsu.md | T-040 | Done |
| archive/ecad2-t041-addsheetdialog-ninja-verification.md | T-041 | Done（全7増分完全Done） |
| archive/ecad2-t041-f10-investigation-onmitsu.md | T-041 | Done |
| archive/ecad2-t041-implementation-plan-samurai.md | T-041 | Done |
| archive/ecad2-t041-increment123-ninja-verification.md | T-041 | Done |
| archive/ecad2-t041-increment1-review-onmitsu.md | T-041 | Done |
| archive/ecad2-t041-increment1-review-onmitsu-2.md | T-041 | Done |
| archive/ecad2-t041-increment2-review-onmitsu.md | T-041 | Done |
| archive/ecad2-t041-increment2-review-onmitsu-2.md | T-041 | Done |
| archive/ecad2-t041-increment3-review-onmitsu.md | T-041 | Done |
| archive/ecad2-t041-increment5-ninja-verification.md | T-041 | Done |
| archive/ecad2-t041-increment5-review-onmitsu.md | T-041 | Done |
| archive/ecad2-t041-increment5-review-onmitsu-2.md | T-041 | Done |
| archive/ecad2-t041-increment5-review-onmitsu-3.md | T-041 | Done |
| archive/ecad2-t041-increment5-review-onmitsu-4.md | T-041 | Done |
| archive/ecad2-t041-increment7-key-operation-proposal-samurai.md | T-041 | Done |
| archive/ecad2-t041-increment7-ninja-verification.md | T-041 | Done |
| archive/ecad2-t041-increment7-review-onmitsu.md | T-041 | Done |
| archive/ecad2-t041-increment7-review-onmitsu-2.md | T-041 | Done |
| archive/ecad2-t041-increment7-review-onmitsu-3.md | T-041 | Done |
| archive/ecad2-t041-increment7-review-onmitsu-4.md | T-041 | Done |
| archive/ecad2-t041-increment7-test-review-ninja.md | T-041 | Done |
| archive/ecad2-t041-key-flow-proposal-samurai.md | T-041 | Done |
| archive/ecad2-t041-mainCircuit-dialog-review-onmitsu.md | T-041 | Done |
| archive/ecad2-t041-mainCircuit-sheet-creation-gap-samurai.md | T-041 | Done |
| archive/ecad2-t041-manual-wiring-survey-onmitsu.md | T-041 | Done |
| archive/ecad2-t043-review-onmitsu.md | T-043 | Done |
| archive/ecad2-t043-review-onmitsu-2.md | T-043 | Done |
| archive/ecad2-t043-review-onmitsu-3.md | T-043 | Done |
| archive/ecad2-t045-addendum2-review-onmitsu.md | T-045 | Done |
| archive/ecad2-t045-implementation-plan-samurai.md | T-045 | Done |
| archive/ecad2-t045-increment-a-ninja-verification.md | T-045 | Done |
| archive/ecad2-t045-increment-a-review-onmitsu.md | T-045 | Done |
| archive/ecad2-t045-increment-b-fix-review-onmitsu.md | T-045 | Done |
| archive/ecad2-t045-increment-b-fix-test-design-onmitsu.md | T-045 | Done |
| archive/ecad2-t045-increment-b-ninja-verification.md | T-045 | Done |
| archive/ecad2-t045-increment-b-review-onmitsu.md | T-045 | Done |
| archive/ecad2-t045-increment-c-ninja-verification.md | T-045 | Done |
| archive/ecad2-t045-increment-c-review-onmitsu.md | T-045 | Done |
| archive/ecad2-t045-increment-d-ninja-verification.md | T-045 | Done |
| archive/ecad2-t045-increment-d-review-onmitsu.md | T-045 | Done |
| archive/ecad2-t045-increment-d-second-wave-review-onmitsu.md | T-045 | Done |
| archive/ecad2-t045-structure-survey-onmitsu.md | T-045 | Done |
| archive/ecad2-t047-fix-ninja-verification.md | T-047 | Done |
| archive/ecad2-t047-fix-review-onmitsu.md | T-047 | Done |
| archive/ecad2-t047-fix-test-design-onmitsu.md | T-047 | Done |
| archive/ecad2-t047-ninja-verification.md | T-047 | Done |
| archive/ecad2-t047-presurvey-onmitsu.md | T-047 | Done |
| archive/ecad2-t047-review-onmitsu2.md | T-047 | Done |
| archive/ecad2-t048-verification-ninja.md | T-048 | Done |
| archive/ecad2-t049-review-onmitsu.md | T-049 | Done |
| archive/ecad2-t049-verification-ninja.md | T-049 | Done |
| archive/ecad2-t050-fix2-review-onmitsu.md | T-050 | Done |
| archive/ecad2-t050-fix2-test-design-onmitsu.md | T-050 | Done |
| archive/ecad2-t050-fix-review-onmitsu.md | T-050 | Done |
| archive/ecad2-t050-fix-test-design-onmitsu.md | T-050 | Done |
| archive/ecad2-t050-review-onmitsu.md | T-050 | Done |
| archive/ecad2-t050-stryker-review-onmitsu.md | T-050 | Done |
| archive/ecad2-t051-bugfix-test-design-onmitsu.md | T-051 | Done |
| archive/ecad2-t051-implementation-plan-samurai.md | T-051 | Done |
| archive/ecad2-t051-precheck-undo-verification-ninja.md | T-051 | Done |
| archive/ecad2-t051-review-onmitsu.md | T-051 | Done |
| archive/ecad2-t051-round2-review-onmitsu.md | T-051 | Done |
| archive/ecad2-t051-round3-review-onmitsu.md | T-051 | Done |
| archive/ecad2-t051-round4-final-review-onmitsu.md | T-051 | Done |
| archive/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md | T-051 | Done |
| archive/ecad2-t051-selectedcell-clamp-test-design-onmitsu.md | T-051 | Done |
| archive/ecad2-t051-stryker-analysis-blocker-onmitsu.md | T-051 | Done |
| archive/ecad2-t051-undo-redo-design-survey-onmitsu.md | T-051 | Done |
| archive/ecad2-t052-review-onmitsu.md | T-052 | Done |
| archive/ecad2-t055-guiecad-row-busnumber-survey-onmitsu.md | T-055 | Done（全増分1〜3完全Done） |
| archive/ecad2-t055-implementation-plan-samurai.md | T-055 | Done |
| archive/ecad2-t055-increment1-review-onmitsu.md | T-055 | Done |
| archive/ecad2-t055-increment1-round2-test-design-onmitsu.md | T-055 | Done |
| archive/ecad2-t055-increment2-review-onmitsu.md | T-055 | Done |
| archive/ecad2-t055-increment2-round1-review-onmitsu.md | T-055 | Done |
| archive/ecad2-t055-increment3-delete-occupied-design-onmitsu.md | T-055 | Done |
| archive/ecad2-t055-increment3-precheck-onmitsu.md | T-055 | Done |
| archive/ecad2-t055-increment3-round1-review-onmitsu.md | T-055 | Done |
| archive/ecad2-t055-increment3-round2-review-onmitsu.md | T-055 | Done |
| archive/ecad2-t055-increment3-selectedcell-bugfix-test-design-onmitsu.md | T-055 | Done |
| archive/ecad2-t056-grid-toggle-proposals-onmitsu2.md | T-056 | Done |
| archive/ecad2-t062-migration-review-onmitsu.md | T-062 | Done |
| archive/ecad2-t063-menu-review-onmitsu.md | T-063 | Done |
| archive/ecad2-t073-p056-fix-review-onmitsu.md | T-073 | Done |
| archive/ecad2-t074-about-dialog-review-onmitsu.md | T-074 | Done |
| archive/ecad2-guiecad-code-survey-onmitsu.md | T-024 | todo-archive.md 119行目「参照」明記、T-024はDone |
| archive/ecad2-wpf-nontechnical-survey-onmitsu.md | T-005 | todo-archive.md 127行目「参照」明記、T-005はDone |
| archive/ecad2-mnemonic-naming-survey-onmitsu.md | T-031 | 「完了」明記のT-031(Done)の成果物（T-032将来構想の基礎資料としても言及されるがT-032自体は未着手） |
| archive/ecad2-preimplementation-survey-onmitsu.md | T-015/018/020/021/023 | 「調査完了グループ」一括事前調査書、参照先5タスクは全てDone |
| archive/ecad2-uiux-proposals-p017-p020-p023-onmitsu.md | T-052/053/054 | P-017/020/023の提案書、対応するT-052・T-053・T-054は全てDone |
| archive/ecad2-ui-ux-inventory.md | T-008 | 冒頭「家老依頼（T-008）」明記、T-008はDone |
| archive/ecad2-uiux-patterns-survey-onmitsu.md | T-008 | 冒頭「T-008・隠密」明記、T-008はDone |
| archive/ecad2-p010-or-fixed-parts-investigation-onmitsu.md | T-037 | proposed.md P-010=「approved → T-037として起票」、T-037はDone |
| archive/ecad2-p039-ninja-verification.md | T-041 | proposed.md P-039=T-041増分7の一環として完了 |
| archive/ecad2-p039-review-onmitsu.md | T-041 | 同上 |
| archive/ecad2-p039-test-design-onmitsu.md | T-041 | 同上 |
| archive/ecad2-p040-review-onmitsu.md | T-045 | proposed.md P-040=T-045増分Aレビュー由来、完了・push済み |
| archive/ecad2-p056-sendkeys-freeze-onmitsu.md | T-073 | todo.md T-073節「技術裏付け」明記、T-073はDone |

## 残留対象（18件）

| ファイル名 | 対応タスク番号 | 判定根拠 |
|---|---|---|
| ecad2-t044-disconnect-investigation-onmitsu.md | T-044 | Blocked（一旦保留、未Done） |
| ecad2-t044-disconnect-repro-ninja.md | T-044 | Blocked |
| ecad2-t044-guiecad-diff-survey-onmitsu.md | T-044 | Blocked |
| ecad2-t044-ninja-verification.md | T-044 | Blocked |
| ecad2-t044-presurvey-onmitsu.md | T-044 | Blocked |
| ecad2-t044-review-onmitsu.md | T-044 | Blocked |
| ecad2-t044-review-onmitsu-2.md | T-044 | Blocked |
| ecad2-t046-stryker-survey-onmitsu.md | T-046 | In-progress（制度運用中、未Done） |
| ecad2-t046-stryker-t045-close-survey-onmitsu.md | T-046 | In-progress |
| ecad2-t058-avalondock-net8-precheck-onmitsu2.md | T-058 | Approved（gated）、PoC未着手 |
| ecad2-t058-avalondock-v5-diff-survey-onmitsu2.md | T-058 | Approved（gated）、PoC未着手 |
| ecad2-t058-docking-float-survey-onmitsu.md | T-058 | Approved（gated）、PoC未着手 |
| ecad2-t060-pdf-ui-wiring-survey-onmitsu2.md | T-060 | Approved（gated）、着手待ち |
| ecad2-t061-testmode-ui-wiring-survey-onmitsu2.md | T-061 | Approved（gated）、着手待ち |
| ecad2-t065-t066-pre-investigation-onmitsu.md | T-065/T-066 | 両タスクともApproved（gated）、未着手 |
| ecad2-t075-spec-plan-onmitsu.md | T-075 | Approved（gated）、隠密が現在進行中 |
| ecad2-guiecad-hardcoded-parts-diff-survey-onmitsu2.md | T-071 | todo.md T-071節「起票元」明記、着手中 |
| ecad2-guiecad-unwired-features-survey-onmitsu2.md | T-064/065/066/067/068/069/071 | 棚卸し起票元、参照先タスク群は全て未Done |

## 対象外（現状維持・判定不能）（19件）

| ファイル名 | 対応タスク番号 | 判定根拠 |
|---|---|---|
| ecad2-t047-gx-icon-survey-onmitsu.md | T-047（例外） | T-047自体はDoneだが`docs/README.md`「殿指示2026-07-09で恒久保存」明記の4件の1つ。殿の恒久保存指示がタスク完了ステータスに優先 |
| ecad2-gxworks3-uiux-survey-onmitsu.md | なし | README.md恒久保存4件の1つ |
| ecad2-gxworks3-uiux-survey-onmitsu-part2.md | なし | README.md恒久保存4件の1つ |
| ecad2-ladder-reference-systems-survey-onmitsu.md | なし | README.md恒久保存4件の「本命」 |
| ecad2-framework-survey-onmitsu.md | なし | README.md「各family調査」節に恒久掲載 |
| ecad2-keyboard-requirements.md | なし | README.md「各family調査」節に恒久掲載 |
| ecad2-stack-web-crossplatform.md | なし | README.md「各family調査」節に恒久掲載 |
| ecad2-stack-native-lightweight.md | なし | README.md「各family調査」節に恒久掲載 |
| ecad2-stack-decision-brief.md | なし | README.md「現況（最重要）」・CLAUDE.mdから直接参照 |
| ecad2-ui-ux-design-brief.md | なし | CLAUDE.mdから直接参照される恒久UI/UX方針書 |
| ecad2-spec-sheet-document.md | T-075 | T-075新設中の恒久文書化対象、進行状況に関わらず据え置き |
| ecad2-spec-undo-redo.md | T-075 | 同上（本調査中に新規追加を確認） |
| ecad2-spec-wiring.md | T-075 | 同上 |
| observations.md | なし | 恒久運用文書 |
| proposed.md | なし | 恒久運用文書（P-番号提案管理台帳） |
| README.md | なし | docs索引文書、常に直下 |
| ecad2-p012-investigation-onmitsu.md | なし | proposed.md P-012=「保留継続」、特定タスクに紐付かない継続保留。**要家老確認** |
| gxworks3-contact-instructions-survey-onmitsu.md | なし | 特定タスク番号への直接紐付け記載なし。**要家老確認** |
| proposed-ninja-onmitsu-sop-plan.md | なし | SOP改訂提案文書、紐付け記載なし。**要家老確認** |

## 検算

アーカイブ対象136件 + 残留対象18件 + 対象外19件 = 合計173件
（`docs/todo.md`・`docs/todo-archive.md`の2件は指示どおり両表から除外。直下総ファイル数175件と一致）

## 特記事項（家老確認推奨点）

1. **`ecad2-t047-gx-icon-survey-onmitsu.md`はT番号パターンだけならT-047(Done)配下だが、README.mdの殿指示（2026-07-09恒久保存）により対象外へ例外的に振り分けた。**
2. `archive/ecad2-mnemonic-naming-survey-onmitsu.md`はT-031(Done)の成果物だが、T-032の基礎資料として引き続き参照される旨が台帳に明記。アーカイブ後もパス変更のみで内容は保持されるため実務上問題ないと判断。
3. `ecad2-p012-investigation-onmitsu.md`・`gxworks3-contact-instructions-survey-onmitsu.md`・`proposed-ninja-onmitsu-sop-plan.md`の3件は台帳上の紐付けが薄く「要家老確認」とした。
4. 調査中に`ecad2-spec-undo-redo.md`が新規追加されたことを確認（T-075隠密作業と符合、他セッション並行稼働の実態）。実移動時は直前に再度`ls docs/*.md`で差分確認を推奨。

段階1（一覧提示）は以上。実際の`git mv`によるアーカイブ移動・参照リンク更新は家老確認後の段階2で実施。
