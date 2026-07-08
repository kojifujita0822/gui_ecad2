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
- **T-045増分A（P-016 Dispatcher分離）実装完了**：コミット`eb3e9b0`（featureブランチ）。
  IDispatcherService新設・SheetNavigationViewModelの直接依存2箇所置換・MainWindowViewModelに
  3本目コンストラクタ（T-042同型の後方互換）・ImmediateDispatcherService注入で既存4テストの
  try/catch除去と直接検証化。RED証明実測済み（WpfDispatcherService一時注入で4件NRE RED→復元
  GREEN）。179件全合格

### 実施したが未検証・進行中

- **増分Aの隠密レビュー＝中間所見まで（再起動でここが中断点）**。隠密の再起動直前報告：
  ①IDispatcherService設計=T-042と完全同型で**妥当確認済み** ②挙動保存=WpfDispatcherServiceは
  元コードと**完全等価確認済み** ⑤179件regression**実測確認済み** ④書き直し4テスト=後退では
  なく不揃い（改善余地あるが必須でない）と暫定判断 ③ImmediateDispatcherServiceと本番の
  セマンティクス差=隠密の暫定分析では「ContextIdle優先度の本来の意味（ListBoxコンテナ生成
  完了待ち）はViewModel単体テストでは原理的に検証不可能な領域＝単体テストの限界であって
  ImmediateDispatcherServiceの欠陥ではない」との整理だが、**finderエージェントの独立検証結果を
  受け取る前に中断**。→再起動後の隠密へ「観点③の独立検証を仕上げて最終判断」のみ采配すれば
  よい（①②④⑤はやり直し不要）
- 増分Aクリーン後は忍者へ回帰確認（シート追加(＋ボタン)・シートリネームの選択ハイライト追従が
  実機で従来どおり機能すること）

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
2. **隠密**：増分A（`eb3e9b0`）レビューを上記2.の5観点で采配（再起動前に完了報告があった場合は
   その結果に従い次へ）
3. クリーンなら**忍者**へ増分A回帰確認（シート追加・リネームのハイライト追従）
4. 増分Aクローズ→**侍**へ増分B（P-025 VM層）采配、以降C/Dと計画どおり
5. pending（殿判断待ち、急がない）：P-036〜P-038・P-031/P-032/P-035

---

## 起動時の合図

4ターミナルとも「開始」で起動（1つずつ間を空けて）。役割は`prompts/startup-auto.md`のstep0〜6で
自動決定する。今回は家老・侍・忍者・隠密の4セッション。**ブランチは
`feature/t045-app-layer-refactor`のまま触らないこと（0.参照）。**
