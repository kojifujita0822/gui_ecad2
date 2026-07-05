# 引き継ぎメモ（次回セッションへ）

最終更新: 2026-07-05（第2セッション終了時点。本セッションはT-039のみ処理（殿裁定）。実装+隠密レビューまで完了、要修正対応・実機確認は次回へ）

## 【最優先・即確認】

1. **ブランチ**: main。作業ツリーはクリーン・push済みで引き継ぐ。
2. **次の仕事はT-039（操作トレースログ基盤）の要修正対応**。経緯：隠密2案比較（`docs/ecad2-t039-design-comparison-onmitsu.md`）→**殿裁定=案B採用**（推奨形：(a)ViewModelBase.OnPropertyChanged一括フック (b)RegisterClassHandlerでGotKeyboardFocus/LostKeyboardFocus横断捕捉 (c)ButtonBase.Clickクラスハンドラ。**旧値記録は侍実装時判断に委任**）→侍実装済み（コミット562a0ad、push済み）→**隠密静的レビューで要修正**（`docs/ecad2-t039-implementation-review-onmitsu.md`、全12件・出典付き）。手順：**侍がCRITICAL筆頭に修正（往復1周目）→隠密再レビュー→忍者実機確認**。
   - **[CRITICAL]** TraceLog.Write（File.AppendAllText）とLogPropertyChangedのリフレクション取得に例外処理が皆無。クラスハンドラ→インスタンスハンドラの実行順（WPF仕様、Microsoft Learn確認済み）により、ログ書込失敗（複数インスタンス同時起動のファイル共有違反等）が本来のPropertyChanged発火・**T-036修正（DeviceNameBox_LostKeyboardFocus）**・Click/Command実行を丸ごとスキップさせる。隠密推奨=**TraceLog内部のtry/catch隔離**（これでHIGHの大半も構造的に解消見込み）。
   - **[HIGH]** TraceLog初期化がDispatcherUnhandledException購読より前で安全網に空白。
   - **[MEDIUM-HIGH]** カスタムsetter系（SelectedElementDeviceName/Tool/ReplaceDocument＝最も追跡したい変更群）で旧値が常にnull。
   - **[MEDIUM]** 環境変数判定`env != "0"`が"false"/全角「０」等を無効化として扱えない。
   - ログ名ecad2-trace.log（T-038のecad2-diag.logと別名）は妥当（隠密所見、侍の確認依頼へ回答済み）。
3. **T-039完了後の着手順（殿裁定済み・従来どおり）**: T-034（App層テスト新設）→ T-035（.gcadpart ID重複）→ T-037（ORa/ORb固定7種化。サムネイルOR/非OR同形の課題は実装時に殿相談）→ T-033（非モーダル浮動インライン化＋殿注文3点。殿の構想図=`docs/images/t033-gxworks3-inline-input-reference.png`）。
4. **pending（殿判断待ち・変わらず）**: P-012（行0へ配置すると母線が描画されない、データは正常・描画のみ）／P-013（デバイス名編集中のCtrl+S等で未確定入力がサイレント消失、仕様判断含む）。

## 現況（2026-07-05 第2セッション終了時点）

- **T-039実装内容（562a0ad）**: Ecad2.App/Diagnostics/TraceLog.cs新設（%TEMP%\ecad2-trace.log、key=value、既定OFF、--trace-log引数/ECAD2_TRACE_LOG環境変数、セッション区切り行、高頻度除外=CanvasScale）＋ViewModelBase一括フック（SetProperty経由のみ旧値捕捉）＋App.xaml.csでクラスハンドラ3種登録（sender==e.OriginalSourceでバブリング重複除去、要素識別はAutomationProperties.Name→x:Name→型名）。ビルド0警告0エラー・既存テスト3件合格・侍スモークOK（有効時記録良好・旧値正確／OFF時ログファイル無生成）。
- 起動スイッチはアプリ本体のみ実装。**ecad2-ui-automationスキルのStart-Ecad2App拡張は範囲外として未着手**（忍者実機確認時は環境変数セットで起動すれば現状でも検証可能）。
- T-034は着手撤回済み（殿裁定「今回はT-039だけ」による。侍は調査読込のみで実装未着手・ツリー影響なし）。

## 技術教訓（本日分）

1. **クラスハンドラ（EventManager.RegisterClassHandler）は本来のインスタンスハンドラより必ず先に実行される**（WPF仕様）。診断・トレース系のフックをこの経路に置く場合、フック内部の例外を完全隔離しないと検証対象の動作自体を壊す（「診断ツールが検証対象の信頼性を損なう」本末転倒に注意）。
2. **役決めprotocolの実運用知見**: 忍者の二重名乗りが発生。確定済みセッションは後着の名乗り通知を見落とすことがあるが、家老がkey比較（小さい方が保持）を両者へ通達すれば決定論どおり収束する（今回実績、数十秒で解消）。名無しpeer（「開始」未投入の窓）が余分に居ても役決めに支障なし。

## 運用ルール（変更なし・有効なもの）

1. **pushは家老経由**（侍はコミットまで。家老はpush前に必ず`git log origin/main..HEAD`で未push分を検め、他役のコミット混入を確認してからpush）。
2. push ask解除は4セッション体制運用中は継続（殿裁定済み）。
3. usage申告・peerメッセージ要約・検証パイプライン既定順（侍→隠密静的→忍者実機）・往復2周上限・Wチェック並行方式は従来どおり。
4. サブエージェントへのパス渡しはスラッシュ区切り/引用符で（バックスラッシュはBashに食われる）。

## 各役への申し送り

### 侍へ
- 初仕事は**T-039レビュー指摘の修正**（全12件=`docs/ecad2-t039-implementation-review-onmitsu.md`、CRITICAL筆頭）。隠密推奨のTraceLog内部try/catch隔離が本命。旧値null問題（MEDIUM-HIGH）はカスタムsetter側の明示記録が要るか費用対効果で判断（旧値記録は殿委任事項ゆえ技術判断でよい）。
- TextBox確定契機のExplicit化標準パターン、ReplaceDocument単一責務点、CurrentSheetIndex意味論重複等、前セッションからの申し送りは従来どおり有効（台帳T-019/T-036備考参照）。

### 忍者へ
- **T-039実機確認が持ち越し**。観点案：起動スイッチ2系統（--trace-log引数/ECAD2_TRACE_LOG環境変数）→フォーカス遷移・Binding更新（旧値含む）・Click・ツール切替の操作とログ突合／既定OFF時の無生成・無副作用／複数インスタンス同時起動（CRITICALの再現経路、修正後の頑健性確認に使える）。
- ninja.md「診断ログ連携【MUST】」節・ズームヘルパー（Invoke-Ecad2CtrlScroll）・モーダル中Send-Ecad2Keys禁止は従来どおり。

### 隠密へ
- 初仕事は**侍修正後の再レビュー**。前任のレビュー原本（全12件・出典付き）が追跡表としてそのまま使える。CRITICALの検証観点=「ログ書込を故意に失敗させても本来処理が走るか」。

## 次回セッションの起動手順

1. 4ターミナルとも `claudepeers` で起動（起動順に家老→侍→忍者→隠密が自動で埋まる。1つずつ間を空けて推奨）。
2. 家老は起動後、**侍へT-039修正を采配**（レビューファイルのパスを添える）→隠密再レビュー→忍者実機確認の順で回す。P-012/P-013の判断は殿の折を見て諮る。
3. `docs/todo.md`・`karo.md`の権限線引きを確認してから采配すること。
