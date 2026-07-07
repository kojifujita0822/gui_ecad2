# 引き継ぎメモ（次回セッションへ）

最終更新: 2026-07-07（家老記す、殿指示によりT-041作業中で本日終了）

`long-horizon-discipline`スキル§6の5点セット形式で記す。

---

## 1. 目的とDoD

**T-041＝手動配線基盤（設置＋消去＋修正）**。殿発意「ロジックだけでは処理できない、縦線・横線を
記入・消去する方法を確立する必要がある」を受け、T-044（OR自動配線の冗長縦分岐抑止）から本命を
乗り換えた。

- 対象＝4種すべて（制御回路`VerticalConnector`・`WireBreak`＋主回路`FreeLine`・`ConnectionDot`）
- 機能＝設置（記入）＋消去＋修正（ドラッグ移動・リサイズ、案X＝キーボード等価操作併設のハイブリッド）
- 記入操作＝F9(横線/主回路限定)・sF9(縦線/主回路限定または縦コネクタ/制御回路限定、シート種別で
  自動切替)・F10(点系＝WireBreak/ConnectionDot、シート種別で自動切替)
- 消去操作＝既存の部品削除（Deleteキー、T-017）と同型の「選択→Delete」に統合（案A）
- DoD（増分5まで）＝各観点の実機OK・回帰なし（`docs/ecad2-t041-implementation-plan-samurai.md`
  5節「忍者検証観点」参照）

---

## 2. 現在の状態（三区分）

### 検証済み（根拠あり）

- **増分1**（VerticalConnector選択モデル+Delete統合）：侍実装592131c→隠密レビューCONFIRMED4件
  →修正1edf36c（SelectedCellのsetterへ選択排他クリアを集約、単一の真実源方式）→隠密再レビュー
  クリーン→忍者実機確認全観点OK
- **増分2**（VerticalConnector手動記入、sF9）：侍実装be9c15f→隠密レビューCONFIRMED（シート切替
  中の状態リーク）→修正f5cbde8（_connectorDraftもSelectedCellのsetter経由で一括クリア）→隠密
  再レビュークリーン→忍者実機確認全観点OK
- **増分3**（WireBreak記入、F10）：侍実装9cc5b32→隠密レビュークリーン→忍者実機確認で**F10が
  WPF標準のWM_SYSKEYDOWN仕様(e.SystemKey側)によりメインメニューへ食われ無反応というバグ発見**
  →Wチェック（侍実装確認＋隠密独立調査、一次情報で原因一致）→修正abddba3
  （`case Key.System when e.SystemKey==Key.F10`）→忍者再検証OKでクローズ
- **増分1〜3まとめて忍者最終確認**：全観点OK・回帰なし（T-044/T-017とも異常なし）。
  `docs/ecad2-t041-increment123-ninja-verification.md`
- **主回路シート作成ダイアログ**（増分1〜3の忍者確認で発覚した「UIから主回路シートを作る手段が
  無い」という範囲外検出への対応）：3案提示→殿裁定＝案1（シート追加時に種別選択ダイアログ、
  既存RenameDialogと同型）→侍実装fa66efd→隠密レビュークリーン→忍者実機確認全観点OK
  （`docs/ecad2-t041-addsheetdialog-ninja-verification.md`）
- **増分4**（FreeLine/ConnectionDot幾何ヒットテストPoC）：`poc/t041-freeline-hittest-poc/`、
  9/9 PASS（コミットf11d478）。nearest-wins方式を採用、ズーム安全性はWPFのLayoutTransform仕様に
  より構造的に保証されるためUI付きPoC不要と判断
- **増分5**（FreeLine/ConnectionDot記入・消去・選択ハイライト）：侍実装d6f1747→隠密レビュー
  CONFIRMED重大所見（下記「未検証・保留」参照）→修正4264220（regression proof実施済み、
  68件全合格）

### 実施したが未検証

- **増分5の隠密再レビュー未実施**（本日はここで終了）。修正4264220が隠密の推奨方針
  （`CurrentSheetIndex`のsetterで早期returnの前にクリア処理を無条件実行）通りに解消できているか
  の確認が次回の最優先事項
- 増分5全体の忍者実機確認（F9/sF9/F10の記入・選択・削除、シート削除時のクロスリーク再現確認）
  は隠密再レビュークリーン後に実施
- 増分2所見C（矢印キー連打時の体感遅延）は「余裕があれば」の優先度低のまま未実施
- 増分5所見I/J/K（Efficiency/Reuse/Altitude、いずれも侍・隠密とも対応不要または増分6以降の
  検討課題と整理済み）

### 未着手・スキップ

- **増分6**（ドラッグUI基盤PoC、移動・リサイズの技術検証。ecad2に前例のないマウスキャプチャ
  実装、最大リスク）
- **増分7**（移動・リサイズ本実装、4種すべて。案X＝ドラッグ主手段＋矢印キー等のキーボード等価
  操作を併設、殿裁定確定済み）

---

## 3. 試して失敗したアプローチと結果（同じ失敗の再試行を防ぐ）

- **選択プリミティブのクリアを個別箇所へ機械的に追加する方式**（隠密の当初修正提案）→侍が
  「SelectedCell/CurrentSheetIndexのsetter自体を単一の真実源にする」方式へ設計転換し、より
  堅牢に解消。以後の増分（WireBreak・FreeLine・ConnectionDot）もこのsetter集約パターンを踏襲
  している。**個別箇所への後追いクリアではなく、setter集約が正解**（T-021モグラ叩きの教訓と
  同根）
- **CurrentSheetIndexのsetter集約は「値が変わる場合」のみクリアする設計だった**→シート削除で
  index数値がたまたま維持されるケースでクリアが素通りする穴が増分5で顕在化。**setterの排他クリア
  パターンは「値が変わるかどうかに関わらず無条件で実行する」設計でなければならない**という教訓
  （SelectedCellのsetterは元々無条件パターンだったため無事だったが、CurrentSheetIndexは
  SetPropertyの早期returnに依存していたため脆弱だった）
- F10キーを単体で使う設計は、WPFのWM_SYSKEYDOWN仕様（Alt系統のシステムキー扱い）により通常の
  `Key.F10`ハンドラでは拾えない。**F10を新規キーとして採用する際は`case Key.System when
  e.SystemKey==Key.F10`の分岐が必須**（他のF5〜F9は通常キーのため無関係）

---

## 4. スコープ境界

- 担当パス（本実装）は`CLAUDE.md`記載どおり：`src/Ecad2.Core/`・`src/Ecad2.Rendering.Wpf/`・
  `src/Ecad2.Pdf/`・`src/Ecad2.App/`・`tests/Ecad2.Core.Tests/`・`tests/Ecad2.App.Tests/`。
  書き込みは侍に一元化
- `poc/t041-freeline-hittest-poc/`は実験場、本実装とは別に維持（増分6のドラッグUI基盤PoCも
  同様にpoc/配下で先行検証する想定）
- T-044（OR自動配線の冗長縦分岐抑止）は**一旦保留**。電気的には正常（描画のみの断線、忍者が
  WireNumber一致で実測確定済み）ゆえ緊急性なし。T-041完了後に再検討するか、そのまま据え置くかは
  未定
- 増分6（ドラッグUI基盤）はecad2に前例のないマウスキャプチャ実装のため、必ずPoC先行

---

## 5. 次の1手

1. **隠密**：`docs/ecad2-t041-increment5-review-onmitsu.md`の重大所見に対する侍の修正
   （コミット`4264220`）の再レビューを実施。観点＝`CurrentSheetIndex`のsetterが早期returnの
   前に無条件でクリア処理を実行する設計へ正しく改まっているか、新規テスト2件が実際に意図した
   経路（index数値が維持されるシート削除ケース）を再現できているか
2. クリーンなら**忍者**へ増分5全体の実機確認を采配（F9横線/sF9縦線/F10接続点の記入・選択・削除、
   シート削除時のクロスリーク再現確認、回帰スモーク）
3. 増分5クローズ後は**増分6**（ドラッグUI基盤PoC）へ。侍の実装プラン
   （`docs/ecad2-t041-implementation-plan-samurai.md`4.2節）のPoC確認項目（マウスキャプチャ中の
   再描画性能・ドラッグ開始判定・本体移動/端点リサイズの区別・Escキャンセル）を参照
4. `docs/todo.md` T-041行・`docs/proposed.md`（P-028等）は随時最新化済み、次回起動時に確認すれば
   全体像がつかめる

---

## 起動時の合図

4ターミナルとも「開始」で起動（1つずつ間を空けて）。役割は`prompts/startup-auto.md`のstep0〜6で
自動決定する。今回は家老・侍・忍者・隠密の4セッションが揃っていた。
