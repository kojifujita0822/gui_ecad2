# 引き継ぎメモ（忍者、T-061 A-1・T-070実機検証 1周目）

最終更新: 2026-07-14（忍者記す）。

**出力破損の離脱プロトコル発動による打ち切り**（`long-horizon-discipline`スキル§5、
同一セッション内でGrep結果の同種破損=`docs-notes/output-corruption-log.md` #13・#14が
2回連続発生。いずれもRead直読で実ファイルは無傷と確認済み・実害なし。プロトコルに従い
2回目時点で作業続行を打ち切る）。

`long-horizon-discipline`スキル§6の5点セット形式で記す。

---

## 1. 目的とDoD（家老委譲メッセージそのまま）

家老より2タスクの実機確認采配を受けた。

**T-061 A-1**（セレクトSWの電気的導通判定、構造対処、コミット268f6cc）task_id: T-061
1. セレクトSWを配置し、ノッチ切替（クリック/Enter）で正しく導通/非導通が切り替わること
2. 通常のContactNO要素（a接点等）の挙動に変化がないこと
3. 機器表で引き続きセレクトSWとして正しく分類表示されること
4. 【G-1確認・最重要】テストモード突入直後、一度もクリックしない状態でセレクトSWの見た目を
   確認——全ノッチが非導通表示（グレー）になるか否か。もしなるなら、クリックした瞬間から
   正しい表示に切り替わることも確認。詳細は`docs/ecad2-t061-a1-review-onmitsu.md`G-1節
5. 開発機は既にアプリを起動済みでセレクトSW.gcadpartが展開されている可能性が高い——
   マイグレーション処理により旧ファイルが自動補正されることも併せて確認

**T-070**（検索・置換機能、往復4周でコミット6184e29まで決着）task_id: T-070
（`docs/ecad2-t070-review-onmitsu.md`のfailure_scenarioを再現手順として使う）
1. テストモード中、Ctrl+Fで検索バーは開けるが「置換」「全置換」ボタンが無効化されること（A-1核心）
2. 検索結果パネルが左寄せでなく正しく幅いっぱいに展開されること（A-9）
3. 部品配置バーと検索バーが同時表示された状態でEscapeが両方正しく機能すること（A-7）
4. 機器名の大文字小文字違い置換（重複要素あり）でBOM情報が保持され重複エントリが残らないこと（D-1相当）
5. 全置換で既存Deviceへ置換してもBOM情報が保護されること（D-2）
6. 検索でジャンプ後に無関係な編集をUndoし、その状態で「置換」を押しても正しい要素（画面上選択中）が
   置換されること（E-1、最重要）
7. FindBar表示中にF5等を押しても誤配置が起きないこと（A-6）

スコープ境界（家老指示）: 両タスクとも上記観点の実機検証のみ。範囲外の気づきは通常どおり
proposed.md行きとして報告。

---

## 2. 現在の状態（三区分）

### 検証済み（根拠あり）

なし。DoD観点はT-061・T-070とも1件も検証に着手できていない。

### 実施したが未検証・未完了

- ビルド確認: `dotnet build src/Ecad2.App` で警告0・エラー0を確認（根拠: ビルド出力）。
- `Start-Ecad2App`でアプリ起動済み（PID確認済み、MainWindowHandle確立）。**殿指示により
  検証完了後もアプリは起動したままにする**（Stop-Ecad2App未実施、意図的）。
- 起動直後はシートが0件だったため、「＋」ボタン（AddSheetButton）→シート追加ダイアログ→
  OKでシート1（制御回路、既定名）を新規作成。ダイアログ多重化なしを確認済み。
- 「自作パーツ」ボタンで部品選択パネルを開き、「セレクトSW」項目の存在を確認
  （`PartSelectionList`内、DisplayName="セレクトSW"）。
- セレクトSW配置を試行 → **未成功**。詳細は次節参照。
- T-070側は着手前（セレクトSW配置に手間取り時間切れ）。

### 未着手・スキップ

- T-061観点1〜5・G-1すべて未検証。
- T-070観点1〜7すべて未検証。

---

## 3. 試して失敗したアプローチと結果（次セッションへの最重要引き継ぎ）

**セレクトSW配置が成立しない問題**：

1. 部品選択パネルの「セレクトSW」ListBoxItemに対し、UI Automation
   `SelectionItemPattern.Select()`経由で選択（`Invoke-Ecad2Element`ヘルパー使用）。
   ステータスバーの「ツール:」表示は`PlaceElement`に変化した。
2. キャンバス上の複数箇所（列0/列2/列4/列6相当、`Invoke-Ecad2CanvasClick`で座標クリック）を
   試したが、いずれも「選択セル: 行1/列N」という表示のみでElementは配置されない
   （配置バー`ElementPlacementBar`も表示されない）。
3. クリック直後に`Send-Ecad2Keys "{ENTER}"`を同一呼び出し内で送っても変化なし（既知の罠
   「PowerShell呼び出しを分けるとフォーカスロストを誘発する」を踏まえ同一呼び出し内で試行
   済み、それでも不成立）。

**ソース調査で判明した疑い（未検証の仮説、断定不可）**:
`MainWindow.xaml`の`PartSelectionList`（`ListBox`）は`SelectionChanged`バインドを意図的に
持たず、`ListBox.ItemContainerStyle`の`EventSetter Event="PreviewMouseLeftButtonDown"
Handler="PartSelectionItem_Clicked"`のみで選択処理を行う設計（コードコメントに明記あり、
同一行クリックでの再選択を成立させるための意図的パターン、`OutputGridRow_Clicked`と同型）。
`PartSelectionItem_Clicked`（`MainWindow.xaml.cs:2260`）が`TryPlaceElement`を呼び
`Tool.PartId`等の内部状態を設定していると見られるが、**UI Automationの
`SelectionItemPattern.Select()`はこの`PreviewMouseLeftButtonDown`イベントを発火させない**
可能性が高い。ステータスバー表示が`PlaceElement`に見えたのは、`ListBox`自体の
`SelectionChanged`が別経路（`ToolMode`のバインディング等）で部分的に反映された見かけ上の
変化であり、実際に必要な`Tool.PartId`（配置する部品の実体）が正しくセットされていないため、
`TryPlaceActiveTool()`（`MainWindow.xaml.cs:1232`、`Tool.PartId is not string partId`で
早期return）がノーオペしている可能性がある。**ninja.mdの既知の罠「UI Automation経由の
Invoke/Selectがボタンの実処理を迂回する」の新パターン候補**。

対応候補案（次セッションで検討）:
- (a) `helpers.ps1`の`Invoke-Ecad2Element`にListBoxItem専用のフォールバック
  （`RaiseEvent`でMouseButtonEventArgsを合成する等）を追加する
- (b) `Invoke-Ecad2ScreenClick`（実座標クリック、フォーカス占有あり）で
  `PartSelectionList`の該当項目を直接クリックする方式に切り替える
- (c) 殿代行操作に切り替える

---

## 4. スコープ境界

- 実装・ビルド・コミットは行っていない（侍領分、忍者は今回関与せず）。
- 触った範囲: 実行中アプリの操作のみ（シート1新規作成、UI操作の試行）。ドキュメントは
  本引き継ぎファイルと`docs-notes/output-corruption-log.md`への追記（#13・#14）のみ。
- 未追跡ファイル2件（前回引き継ぎ記載のscratchpad系）は今回のセッションで一切手を触れていない。

---

## 5. 次の1手

1. **アプリは起動したまま**（殿指示）。次セッションは新規に`Start-Ecad2App`を呼ばず、
   既存プロセス（`Get-Ecad2Process`で確認）へ接続して継続する。ただしシート1は未保存の
   まま作成済み・要素は1つも配置されていない状態。
2. 上記3節の罠（UI Automation Select()がPreviewMouseLeftButtonDownを発火させない疑い）の
   真偽をまず確認し、(a)〜(c)いずれかの対応でセレクトSW配置を成立させる。
3. セレクトSW配置成立後、T-061観点1〜5・G-1（最重要）を検証。
4. T-070（セレクトSWと無関係、独立着手可）は観点1〜7を`docs/ecad2-t070-review-onmitsu.md`の
   failure_scenarioに沿って検証。
5. 出力破損記録簿（`docs-notes/output-corruption-log.md` #13・#14）を踏まえ、次セッションは
   **冒頭からGrep contentモード（-A/-B/-C含む）を一切使わずRead直読・Glob・files_with_matches/
   countのみで進めること**（今回、直前に警戒を自覚したにも関わらず同種破損が再発した実例）。

---

## 起動時の合図

各ターミナルとも「開始」で起動。役割は`prompts/startup-auto.md`のstep0〜6で自動決定する。
