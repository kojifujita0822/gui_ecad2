# 引き継ぎメモ（次回セッションへ）

最終更新: 2026-07-04（**全セッション再起動**のための引き継ぎ。侍のコンテキスト申告を受けた殿裁定。T-021増分(vi)完了・push済み時点）

## 【最優先・即確認】

1. **ブランチ**: 共有ワークツリー（`C:\ECAD2`、4セッション共通）は `feature/t021-enter-placement` のまま。**mainではない。** 起動時 `git branch --show-current` で確認。T-021全増分完了→mainマージ（殿確認のうえ）までこのブランチで作業する。
2. **T-021増分(vi)は完了・push済み**（`origin/feature/t021-enter-placement`、コミット0b5a20bまで、忍者最終検証で全項目OK）。**次の作業は増分(iv) Esc多段階4層**（前セッションの侍は未着手のまま再起動。着手手順は下記「侍への申し送り」(2)参照）。家老は起動後、侍へ増分(iv)を采配せよ。
3. 作業ツリーはクリーンで引き継ぐ（未コミットなし）。`git status` で確認。

## 【MUST】UI/UX・使用感に関わる分岐は必ず殿へ確認（維持）

従来どおり。詳細は `docs-notes/roles/karo.md` 同名節と memory（feedback_route_design_decisions_to_user）参照。
実装途中に発覚する細かい挙動の分岐は「家老が既定案を選び実装→忍者検証→実挙動を殿に提示して確認」のサイクルで可。構造的・後戻りしにくい分岐は実装前に選択肢提示。

## 現況（2026-07-04 再起動時点）

- **T-021**: 増分1＋増分(vi)完了。Enter配置＋フォーカス設計集約（Click温存＋キーボード由来照合方式）＋Space/Enter対称＋UIA Invoke互換＋GridSplitter 3箇所Tab除外、すべて実機検証通過。**5往復の詳細経緯は `docs/todo.md` T-021行に完全記録**（次の類似バグ時に必読）。
- **残作業**: 増分(iv) Esc多段階4層 → 増分(v) 矢印追従スクロール（両者独立、並行可） → 全増分完了時に **mainへのマージを殿確認のうえ実施** → その後T-015のスコープ再定義へ（殿へ選択肢提示が必要、todo.md参照）。
- **経過観察2件**（修正しない合意済み）: (1)Space押下中の強制フォーカス移動→記録残留→同一ボタンUIA Invokeで判定1回ズレ (2)Alt+Tab系の同種残留。台帳T-021備考参照。
- 他の未着手: T-019（ドキュメント管理、T-020濃紺最終確認の前提）・T-033・T-028・T-029・T-022・T-013・T-032・P-005。詳細は `docs/todo.md`。

## 増分(vi)の技術教訓（次の類似作業の核心、send_message上の知見を保全）

1. **【最重要】Clickイベント温存の原則**: WPFボタンでマウス/キーボード経路を分けたいとき、Clickを廃してPreviewMouseLeftButtonUp/PreviewKeyDownへ分離してはならない（UIA Invoke無反応・マウスキャプチャ意味論喪失・キーリピート誤爆・Spaceキャンセル猶予喪失の4点を同時に壊した実績）。**ButtonBase標準のClick発火を温存し、PreviewKeyDownは「意図の記録」のみ、実処理はClickで行う。**
2. **実測主義**: WPFフォーカス内部動作の謎は、理論（一次ソース読解）2巡でも外した。**コミットしない診断ログ**（File.AppendAllTextで%TEMP%へ、DIAG BUILDマーカーで古バイナリ排除、git restoreで撤収）を仕込み実測してから直す手順が決定打だった。
3. **WPFフォーカスの一次情報知見**（隠密調査、独立docs化の可否は殿に諮ること）:
   - ButtonBaseのEnter/Space処理は非対称。Enter=OnKeyDownで即OnClick。Space=OnKeyDownでCaptureMouse→OnKeyUpでIsSpaceKeyDown=false後にReleaseMouseCapture→OnLostMouseCapture経由でKeyboard.Focus(null)を呼びうる（＝Click直前にボタンが一瞬フォーカスを失う）。
   - WindowはControl由来でFocusable=true、かつKeyboard.IsFocusableはルートVisual（InternalVisualParent==null）を無条件true扱いする例外規定がある。「ウィンドウは非フォーカス可能」という直感は誤り。
   - `Keyboard.Focus(null)`は「フォーカスなし」にならず、**主フォーカススコープの復元先（直前の具体的要素）まで遷移する**。LostKeyboardFocusのNewFocus値で「一時喪失か真のキャンセルか」を区別する設計は原理的に不成立。

## 各役への申し送り

### 侍へ（前任侍より）

**(1) フォーカス制御の流儀（コードだけでは伝わりにくい点）**
- `FocusCanvas()`＝LadderCanvasHostへの唯一の正規手段。CanvasAreaは独立FocusScope（T-016既知の罠）のため、FocusManager.SetFocusedElement→Keyboard.Focusの2段方式が必須。
- `ConsumeToolButtonFocusRestore(sender)`が「FocusCanvasするかボタンへFocus()し直すか」の唯一の判定点。`_toolButtonKeyboardClickSource`（object?参照型）との一致でキーボード由来判定。記録は`ToolButtonPreviewKeyDown`（Enter/Space時、3ボタン共通単一ハンドラ）。クリア経路はConsume消費とWindow_PreviewMouseLeftButtonDownの2つ**のみ**（LostKeyboardFocusでのクリアは案A失敗により撤去済み、復活させないこと）。
- GridSplitterは**3箇所**（Grid.Column=1・3に加えGrid.Row=1が右パネル機器表⇔プロパティ間）。「N箇所」という記述を鵜呑みにせずgrepで実数確認する癖を。
- テストのベースラインは3件合格（App層のみの変更では不変）。

**(2) 増分(iv) Esc多段階4層の入り口**
- 一次資料: `docs/ecad2-t021-implementation-plan-samurai.md` 増分(iv)節＋`docs/ecad2-t021-keyboard-spec.md` 論点3（4層: 1.編集中→編集キャンセル 2.配置モード→選択モードへ 3.要素選択中→選択解除 4.何もなし→無視）。この2文書の再読で足り、追加調査は不要の見込み。
- 現状Window_PreviewKeyDownのEscapeケースは「ActivateSelectDefault()+FocusCanvas()+Handled」を無条件一括実行→層ごとの段階判定へ分解する。
- 層1はElementPlacementDialog（モーダル別Window）側の責務。まず**IsCancel="True"のボタンが既にあるか確認**（WPF標準のEsc→クローズ規約が既に効いている可能性）。
- 層2〜4はEscapeケース内で「Tool.Mode==PlaceElementか」「SelectedCell!=nullか」を順に見て1回のEscで1層だけ戻す。
- FocusCanvas()は設計集約プラン根拠3表（Escはグローバルショートカットゆえ常時実行）を踏襲し、まず全層で無条件呼び出しのままでよい。
- StatusMessage残留クリアが各層で必要になる可能性（T-017由来の既知の教訓）。
- 増分(v)は(iv)と独立、並行/後着手可。

### 忍者へ（前任忍者より）

- **モーダルダイアログ対応ヘルパーの昇格候補**: 既存`ecad2-ui-automation`スキルのSend-Ecad2Keys/Set-Ecad2Foregroundはメインウィンドウ固定のため、**モーダルダイアログ表示中はキー入力が裏へ漏れる**（二重ダイアログ等の混乱実績あり）。前任忍者が全トップレベルウィンドウ列挙→ダイアログ有無判定→フォアグラウンド切替の`Send-KeysSafe`を作成し全検証で活用した。実物は前任セッションのscratchpad: `C:\Users\kojif\AppData\Local\Temp\claude\C--ECAD2\740cf328-a3db-4b85-8da0-d9598d19450d\scratchpad\ecad2-dialog-helpers.ps1`（消えていたら上記機能要件から再作成可能）。スキル`helpers.ps1`への正式追加は**台帳外の新規作業ゆえ殿に諮ること**。
- **Ctrl+Tabで段（FocusScope）を跨げる**: 2段ツールバー構成でTabは段内循環するがCtrl+Tabで段間移動できる。キーボードナビ検証の必須操作。
- **Tab連打洗い出し**: Tabを1回ずつ送り毎回FocusedElementを記録する手法で、Tabオーダー系の不具合（GridSplitter迷い込み等）を機械的に発見できる。
- UIA Invoke/Select偽結果対策はninja.md反映済み（物理操作で再検証）。

### 隠密へ（前任隠密より）

- 調査文書は`docs/ecad2-t021-enter-placement-survey-onmitsu.md`・`docs/ecad2-t021-focus-design-consolidation-plan-onmitsu.md`参照（docs反映済み）。
- **code-reviewスキル運用の機微**: 8角度finder+verifyは1角度数分〜十数分かかる。効率面ではTaskOutput(block=true, timeout長め)での直接待機も検討。**lowは純削除・小差分の確認には十分だが、設計上の穴（attribution swap等）の検出にはmedium以上が必要**（実績: 3周目レビューの穴2件はmediumで発見）。

## 運用メモ（本セッションの出来事・学び）

1. **複数ウィンドウ二重指揮事故（o4ss6mk3の顛末）**: 殿が別のClaude Codeウィンドウにも承認を入力し、そのセッションが「家老」として善意で並行指揮した（指示内容が偶然一致し実害なし）。学び: (a)**殿への依頼: 指示は1ウィンドウにのみ入力し、使わないウィンドウは閉じる** (b)同役を名乗る別IDを見たら即、全役へ照会し「役名義はID照合で防御」する（今回この運用で収束。各役は「確認済みの家老IDのみ有効」とする運用を確立済み——ただしIDは起動毎に変わるため、新セッションでは名乗り→key照合の正規フローで再確立する）。
2. **summary無しの謎peerの正体**: apm1o5sm はpeer-mcpサーバー自身のプロセス（bun server.ts、PID 5676）だった。幽霊セッションと早合点せず、`Get-CimInstance Win32_Process`でプロセス実体を確認するとよい。
3. **git push のask解除が有効**（`~/.claude/settings.json`から`Bash(git push *)`を削除済み、rm/sudoは維持）。殿委任によるもの。**元に戻す時期は殿に確認すること。**
4. 検証プロセスの実績: 検証パイプライン（侍→隠密→忍者）・Wチェック並行方式（侍診断＋隠密理論の突合が真因確定の決め手）・往復2周上限＋殿承認条項（案A不成立時の案B切替を事前承認得ておく方式が機動力を生んだ）、いずれも有効に機能した。

## 次回セッションの起動手順

1. 4ターミナルとも `prompts/startup-auto.md` の同一プロンプトで起動（起動順に家老→侍→忍者→隠密が自動で埋まる。1つずつ間を空けて起動推奨）。
2. 家老は起動後、本ファイルの【最優先・即確認】を実施→侍へ増分(iv)を采配（上記「侍への申し送り」(2)を添えるとよい）。
3. `docs/todo.md`・`karo.md`の権限線引きを確認してから采配すること。
