# 引き継ぎメモ（次回セッションへ）

最終更新: 2026-07-08（家老記す、コンテキスト逼迫による全セッション再起動の直前更新）

`long-horizon-discipline`スキル§6の5点セット形式で記す。

---

## 0. 【最重要】作業ツリーの状態

**現在のブランチは `feature/t045-app-layer-refactor`**（mainではない）。T-045専用ブランチ
（殿指示=mainを汚さずリスク回避）。T-045完了後にmainへマージする。mainは`a982b32`まで
push済み・クリーン。**再起動後の各役はこのブランチ上で作業を続けること。**

---

## 1. 目的とDoD

**T-045＝App層リファクタリング**（実装計画は殿裁可済み2026-07-08、
`docs/ecad2-t045-implementation-plan-samurai.md`）。

- 増分A=P-016 Dispatcher分離（IDispatcherService）→ B=P-025 VM層（配置前検証一本化+P-020
  種別マッピング）→ C=View層（TryPlaceElement等+RowAtDip死にコード除去）→ D=ドラッグ外枠
  共通化（ForceCancelDrag*IfAny骨格のみ。UpdateDrag*の実クランプは型ごと維持=隠密判断）
- 中断基準：往復3周超／回帰10件超崩れ／UI-UX判断ブロック → mainへ戻り殿上申
- 検証=増分ごとに隠密レビュー→（UI挙動に触れる増分は）忍者実機確認。新制度適用（下記4.）

**T-041は完全Done**（2026-07-08、全7増分+P-039。忍者実機確認全観点OK・テスト179件全合格）。

---

## 2. 現在の状態（三区分）

### 検証済み（根拠あり）

- **T-041完全クローズ**：詳細は`docs/todo.md`T-041行と`docs/ecad2-p039-ninja-verification.md`。
  mainへpush済み
- **T-045増分B マッピング対応表=殿裁可済み（2026-07-09、案A=第一案+家老補完）**：
  ContactNO/NC・Coil→Relay、Lamp→Lamp、PushButtonNO/NC・EmergencyStop→PushButton、
  SelectSwitch→SelectSwitch、Terminal→Terminal、Timer・TimerContactNO/NC・
  TimerInstantContactNO/NC→Timer、Counter→Counter、ContactorMain3P→Relay（MCコイルと
  同一機器名参照ゆえ配置順による種別揺れ防止）、ThermalOverload/ThermalOverload3P・Motor・
  Breaker3P→Other（該当クラス無し）。**種別列は英語表示のまま**（日本語化はP-020副次所見として
  proposed.md据え置き、殿確認済み）
- **T-045増分A（P-016 Dispatcher分離）実装完了**：コミット`eb3e9b0`（featureブランチ）。
  IDispatcherService新設・SheetNavigationViewModelの直接依存2箇所置換・MainWindowViewModelに
  3本目コンストラクタ（T-042同型の後方互換）・ImmediateDispatcherService注入で既存4テストの
  try/catch除去と直接検証化。RED証明実測済み（WpfDispatcherService一時注入で4件NRE RED→復元
  GREEN）。179件全合格

- **増分B境界ガード下限=殿裁定（2026-07-09）「下限0」**：配置は行0〜Rows-1・列0〜Columns-1のみ
  許容。負マージン（行-1・列-2）は選択のみ可・**配置不可**（選択仕様=殿教示2026-07-07には
  不干渉）。P-024の不可視データ設置・P-022のはみ出し描画を配置経路から根絶する趣旨。
  描画範囲の扱い（負マージンが描画範囲に含まれる件）は殿修正予定の別論点として残る
- **T-045増分Aクローズ確定（2026-07-09）**：パイプライン全段通過＝実装✔（`eb3e9b0`）→
  隠密レビュー✔（クリーン維持・機能バグなし、docs収蔵済み
  `docs/ecad2-t045-increment-a-review-onmitsu.md`）→忍者実機回帰✔（シート追加ハイライト追従・
  リネーム維持・スモーク全OK、`docs/ecad2-t045-increment-a-ninja-verification.md`）
- **隠密code-review新知見の家老仕分け（2026-07-09）**：CONFIRMED4件/PLAUSIBLE6件（全て機能バグ
  でなくテスト盲点・簡潔性）。所見1・2＋軽微指摘＋所見4・9=侍へ「増分A補遺」采配（テスト補強＋
  コメント整備、RED=ミューテーション2種＝ContextIdle別値化・MarkDirtyのlambda内移動の実証を
  DoD化）。所見3（YAGNI）・5〜8=経過観察。所見10=**P-040**としてproposed.md送り（pending）

- **増分A補遺クローズ（2026-07-09）**：侍実装`aba8c51`（新規4テスト=priority検証2＋timing検証2、
  RED証明2種実測→復元GREEN）→隠密レビュークリーン（所見1・2の盲点解消・RED証明整合・コメント
  整備を出典付き確認、`dotnet test`実測**183件全合格**=Core14+App169・失敗0スキップ0。
  ロジック変更なしのため実機不要=家老判断）

- **殿裁定5件（2026-07-09、離席前の先回り確認＋追伸）**：(1)増分C UX=既存様式に揃える（配置バーを
  開かずStatusMessageでabort・新規UIなし） (2)T-045全増分クローズ後のmainマージ=条件（全増分の
  パイプライン全段通過・全テスト合格実測・忍者最終回帰OK）を満たす場合のみ**家老裁量で
  `--no-ff`マージ→push→事後報告** (3)P-040=**承認**、T-045完了後の小タスクとして侍へ采配
  (4)不在中の想定外分岐=保守的既定を家老が選んで進め、帰還時に事後報告 (5)**修正往復2周超も
  例外的に許可**（不在中の停滞回避。同一アプローチ2連敗で前提を崩す一般則・回帰10件超等の
  他の中断基準は維持=家老運用）
- **増分B＝修正往復1周目→クローズ確定（2026-07-09）**：侍実装`e45c2d3`（ValidatePlacement下限0・案A対応表・RED2種・197件
  全合格）→隠密レビューで**要修正**（`docs/ecad2-t045-increment-b-review-onmitsu.md`）。
  CONFIRMED＝`ResolveDeviceClass`の固定Id判定がExplorerコピー再採番後のセレクトSWに外れ
  Relayへ誤分類（T-043で駆逐済み同型パターンの再導入）。家老裁定（2026-07-09）：
  制度（テスト設計と実装の分離）適用＝隠密がテスト設計起草→侍が修正（T-043パターン=Role/弁別
  フィールド判定へ置換）＋所見C（private化）同梱＋RED先行証明→隠密再レビュー→忍者実機。
  所見B（TryPlaceElement未追随=増分C計画済みギャップ）＝増分C先送りせず、忍者観点へ
  「境界外配置の無反応は既知、データ非設置確認で代替」と明記して対処。所見D・E＝経過観察、
  所見F＝P-021残論点として記録済み。
  →**修正完了`3c9dd5a`**（設計書`docs/ecad2-t045-increment-b-fix-test-design-onmitsu.md`どおり
  [Theory]4ケース実装・RED証明=ケースBのみRED/A・C・D GREEN・199件全合格。PartPalette.Entries
  動的検索+T-043同型タプル判定へ置換、所見C=private化同梱）
  →**隠密再レビュークリーン**（DoD5点全通過、`docs/ecad2-t045-increment-b-fix-review-onmitsu.md`。
  新規所見G=基本図形読込失敗という稀有な異常環境でセレクトSWがRelayへ静かに退行しうる新規
  リスク→**家老裁定2026-07-09:経過観察=トレードオフ受容**[頻発コピーバグ解消との交換、防御線
  追加は固定Idパターン再導入ゆえKISS優先で見送り]。PLAUSIBLE3件も経過観察）
  →**忍者実機4観点全OK＝増分Bクローズ（2026-07-09）**（`docs/ecad2-t045-increment-b-ninja-verification.md`：
  セレクトSW=SelectSwitch表示・境界外はデータ非設置=P-024再発なし。実挙動注記=境界外でも配置
  ダイアログは開きOK後サイレント非設置→増分Cで解消予定。検証制約=押しボタン/ランプ/タイマは
  実機素材[.gcadpart/標準リスト]が無く実機確認不可・ユニットテストで担保。往復1周ゆえStryker
  棚卸し対象[2周以上]には非該当=家老判断）

- **増分C（View層）＝侍実装完了`bf0e2c2`→隠密レビュー中**：TryPlaceElement冒頭の境界チェック
  （殿裁定どおり既存様式=StatusMessage「選択したセルはグリッド範囲外です」+return、配置バー
  表示前に弾く=所見B解消）・IsSelectedCellWithinGrid新設（private検証関数のView公開はVM
  ラッパー方式を侍が選択）・境界判定DRY化・RowAtDip死にコード除去・**横展開は含めない=家老
  判断**（計画書「着手時に家老へ確認」への回答、将来課題として残置）・208件全合格
  （Core14+App194）。→**隠密レビュークリーン（指摘ゼロ、`docs/ecad2-t045-increment-c-review-onmitsu.md`）**
  →**忍者実機必須4観点全OK＝増分Cクローズ（2026-07-09）**（`docs/ecad2-t045-increment-c-ninja-verification.md`：
  境界外は配置バー開かずメッセージ・データ非設置、増分Bの「ダイアログ開きOK後サイレント非設置」
  解消確認。範囲外検出=種別切替直後の高速クリックで旧種別配置→**P-041**記録[両論併記=UIA擬似
  事象/実レース、pending]）

### 実施したが未検証・進行中

- **増分D（ドラッグ外枠共通化）＝実装中**（フェーズ1完了→家老判断2026-07-09：
  **(a)案1=delegateベースForceCancelIfAny共通ヘルパー採用**［計画書DoD「所見Y型の構造的再発
  抑止」に適合するのは案1のみ、案2=個別維持はDoD未達と判断］・**(b)ConnectionDotをConfirm/
  Cancel骨格PoC対象に承認**［4種中最もフィールド単純。他3種への展開は実装せずPoC所見のみ
  報告→家老が別途判断］→侍へ実装を正式采配）。DoD=純リファクタ挙動不変・全テスト合格実測。
  UpdateDrag*実クランプは型ごと維持=不可侵。完了後は隠密レビュー→忍者実機（4種ドラッグ回帰
  一巡）の既定順。
  →**実装完了`10b350c`**（ForceCancelIfAny共通ヘルパー+4箇所置換・ConnectionDotのみ
  ConfirmDrag<T>/CancelDrag<T>試作・純リファクタ挙動不変・208件全合格+ドラッグ関連96件個別
  実行全合格。PoC所見=WireBreak展開容易/Connector・FreeLineはdelegate複雑化で要慎重）
  →家老判断（2026-07-09）：他3種への骨格展開はいったん見送り→**隠密レビューで撤回・展開采配**
  （`docs/ecad2-t045-increment-d-review-onmitsu.md`：機能バグ0件。ただしDoD(6)実査=侍PoC所見は
  誤認でConnector/FreeLineのConfirm/Cancelに分岐なし[分岐はUpdateDrag*のみ、混同]、DoD(2)=
  ForceCancelIfAnyは順序強制のみで計画書文言「復元が構造的に効く」はCancelDrag<T>側でのみ達成
  →**展開すればDoD(2)実効化+パターン混在解消**と家老裁定、一次情報に基づく自己訂正）
  →**第2波実装完了`9225c3a`**（3種展開・4種同一パターン統一・純リファクタ挙動不変・208件+
  ドラッグ96件全合格。侍がPoC誤認を訂正=教訓「骨格対象の確認はConfirm/Cancel側とUpdate側を
  明確に分けて読む」）→**隠密差分再レビュークリーン（機能バグ0件、
  `docs/ecad2-t045-increment-d-second-wave-review-onmitsu.md`）**：既存4種の復元連鎖
  （ForceCancelDragXxxIfAny→CancelDragXxx→CancelDrag<T>）実効化=計画書DoD実質達成と家老裁定。
  **正直な限界（T-045総括に記す）**=将来第5型追加時にCancelDrag<T>経由を強制する型制約は無く
  人的レビュー依存（隠密CONFIRMED・実装コメントにも明記）
  →**忍者実機検証＝環境異常で中断（2026-07-09）**：Ecad2.App起動後ウィンドウ内容が白紙描画
  （タイトルバー・枠のみ。UIA経由は全操作正常・プロセス生存・crashログ空、デスクトップ/他
  ウィジェットは正常描画）。移動/最小化復元/再起動の3試行で改善せず=T-038基準で打ち切り。
  **切り分け完了＝環境要因（GPU/DWM等）確定（2026-07-09）**：別WPFバイナリ
  （poc/t041-drag-poc）でも同様の白紙描画を忍者が確認＋隠密のdiff裏取り（増分D全差分は
  MainWindowViewModel.csのみ・描画パイプライン非接触）。増分Dのコード要因は否定。
  **増分D実機検証は殿帰還（PC再起動）待ちで保留、復旧後に忍者が同じ采配（4種ドラッグ回帰
  一巡＋軽い全体スモーク）を再開する**。
  **mainマージは忍者最終回帰OKが揃うまで保留**（殿裁定条件の未達につき）。
  待機中の有効活用＝**Stryker手動棚卸し完了（2026-07-09、
  `docs/ecad2-t046-stryker-t045-close-survey-onmitsu.md`）**：App全体score 22.15%（前回19.79%
  から改善）、T-045変更領域の検出率65〜84%=平均を大幅超、**重大な穴なし**。生存6件の家老仕分け
  ＝(1)ForceCancelIfAnyのnotify()発火未検証＋(3)MapToDeviceClassのPushButton/Timer系未検証4件
  →**侍へ「T-045補遺2」采配済み**（テストのみ・ヘッドレス完結：PropertyChanged発火アサーション
  4種分＋対応表全20値のTheory化。RED=代表2種のミューテーション手動適用で実証）／(2)isActive
  ガード=見送り。手順申し送り（Strykerはtests/Ecad2.App.Testsから実行必須）は隠密が自著docへ
  追記中。補遺2完了後は隠密差分レビュー（ヘッドレス）のみ

### 未着手・スキップ

- T-045増分B/C/D（増分Aクローズ後に順次）
- T-044（OR自動配線）は保留継続

---

## 3. 試して失敗したアプローチと結果（同じ失敗の再試行を防ぐ）

- T-041増分7の教訓：4種コピペ構造が「1種直すと別種で漏れる」を4回再生産（所見X/Y/AB/AC/AD）。
  →T-045増分Dの外枠共通化で根本対処する
- `ReleaseMouseCapture()`は`LostMouseCapture`を同期発火（WPF仕様）。Confirm→Releaseの順が必須
- 「必ず通過するテスト」の3失敗形（①Update無しでBegin→強制クリアのみ検証 ②実呼び出しと
  異なる引数 ③境界の片側のみ）→新制度（下記4.）で構造対処済み
- 詳細はmain側の過去引き継ぎ・`docs/ecad2-t041-increment7-review-onmitsu*.md`参照

---

## 4. スコープ境界・制度（すべて殿裁定済み、以後全タスクに適用）

- **RED先行証明【MUST】**（`samurai.md`）：バグ修正の回帰テストは修正前コードでREDを実測証明
- **テスト設計と実装の分離【MUST】**（`onmitsu.md`「テスト設計の起草」・`samurai.md`・`karo.md`）：
  バグ修正・往復案件は隠密が技法適用（同値分割・境界値・状態遷移・対称性・[Theory]）の設計書を
  先に起草、侍はコード化に徹する
- **テストコード静的レビュー**（`karo.md`）：往復2周以上のタスクで観点に含める（遡及はしない）
- **Stryker.NET手動棚卸し**（2026-07-08殿裁定、`karo.md`・`onmitsu.md`）：往復2周以上の案件
  クローズ時、隠密が手動実行しテストの穴を棚卸し。CI化はscore改善後（現状Core 3.76%/App 19.79%、
  `docs/ecad2-t046-stryker-survey-onmitsu.md`）
- 担当パス・poc/隔離は従来どおり（`CLAUDE.md`）。書き込みは侍一元化・pushは家老経由

---

## 5. 次の1手（再起動後の家老がやること）

1. **役割自動決定**（`prompts/startup-auto.md`）後、**采配の全量再送**（再起動でpeerメッセージ
   队列は消える。アイドルpeerは采配を拾わない教訓あり）
2. ~~増分A補遺~~ **クローズ済み（2026-07-09、上記2.参照）**
3. **侍の増分C完了報告を受けたら**：隠密レビュー→忍者実機（**必須**：境界外・占有セルへの
   配置試行がUI上でも弾かれること＋通常配置の回帰なし）の既定順
4. 増分Cクローズ→増分D（ドラッグ外枠共通化）。**着手時に侍から具体案（delegateベース共通化か
   個別維持か）の提示を受けて家老が判断**（計画書明記の手順）。ConfirmDrag*/CancelDrag*骨格は
   PoC的に1種試行→他3種展開可否の慎重手順
5. pending（殿判断待ち、急がない）：P-036〜P-038・P-031/P-032/P-035・P-040（新規、
   Dispatcher直接依存の再発防止アーキテクチャテスト）

---

## 起動時の合図

4ターミナルとも「開始」で起動（1つずつ間を空けて）。役割は`prompts/startup-auto.md`のstep0〜6で
自動決定する。今回は家老・侍・忍者・隠密の4セッション。**ブランチは
`feature/t045-app-layer-refactor`のまま触らないこと（0.参照）。**
