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

### 実施したが未検証・進行中

- **侍が増分B（P-025 VM層＋P-020マッピング=裁可済み案A）実施中**。完了後は
  隠密レビュー→忍者実機の既定順

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
3. **侍**の増分B完了報告を受けたら→隠密レビュー→忍者実機（配置操作全般の回帰＋機器表「種別」列の
   英語名表示確認）の既定順——増分Bの対応表は殿裁可済み=案A（上記2.参照）、着手時の再確認不要
4. 増分Bクローズ→増分C（View層）、以降Dと計画どおり
5. pending（殿判断待ち、急がない）：P-036〜P-038・P-031/P-032/P-035・P-040（新規、
   Dispatcher直接依存の再発防止アーキテクチャテスト）

---

## 起動時の合図

4ターミナルとも「開始」で起動（1つずつ間を空けて）。役割は`prompts/startup-auto.md`のstep0〜6で
自動決定する。今回は家老・侍・忍者・隠密の4セッション。**ブランチは
`feature/t045-app-layer-refactor`のまま触らないこと（0.参照）。**
