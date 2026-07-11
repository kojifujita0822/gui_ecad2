# P-056 SendKeysシステムフリーズ疑い 技術裏付け調査（隠密）

対象: task_id=2（家老采配、2026-07-11）。P-056（殿PCキーボード入力不通フリーズ、直前操作が
`SendKeys::SendWait`によるファイルパス送信だった件）の技術的裏付けを一次情報で調査した。
忍者による実機再現調査とはWチェックの並行関係（本調査はコード・公式ドキュメントのみ、実機操作は行わず）。

**結論：JournalHook方式（SendKeysの既定実装）が「システム全体のキーボード/マウス入力を無効化する」
性質を持つことは公式ドキュメントで確認できた。ただしSendKeys.SendWait自体が実際にフリーズを
起こした直接の既知issueは検索範囲内では発見できず（間接的技術裏付けに留まる）。**

---

## 1. .NET SendKeys(SendWait)の内部実装方式

出典: [SendKeys Class (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.sendkeys?view=windowsdesktop-8.0)、
[Simulate keyboard events (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/input-keyboard/how-to-simulate-events)、
`.NET` 実ソース `SendKeys.cs`（後述）。

- SendKeysには2つの内部実装が存在する。
  - **JournalHook（旧実装）**: Win32 APIの`SetWindowsHookEx(WH_JOURNALPLAYBACK, ...)`でシステムフックを設置する方式。
  - **SendInput（新実装）**: `SendInput` APIで合成キー入力イベントを直接送る方式（Windows Vista以降のUAC環境で
    JournalHookが失敗するケースに対応するため追加された）。
- **既定動作（Default）は「まずJournalHookを試し、失敗したらSendInputにフォールバック」**。
  app.configの`<add key="SendKeys" value="JournalHook"/>`または`"SendInput"`で明示指定も可能。
- `SendWait`は`Send(keys, control, wait: true)`の薄いラッパーであり、JournalHook使用時は追加で`Flush()`
  （`Application.DoEvents()`をイベントキューが空になるまで回すループ）を呼んで完了を待つ。

### 実ソース確認（一次情報、`dotnet/winforms` GitHub）

`https://raw.githubusercontent.com/dotnet/winforms/e83409daa530605da4eb5f847c6740a520325d25/src/System.Windows.Forms/src/System/Windows/Forms/SendKeys.cs`
を取得し、該当箇所を直接確認した（要約ではなく行内容を目視で確認済み）。

```csharp
// InstallHook() 222-239行
s_hhook = User32.SetWindowsHookExW(User32.WH.JOURNALPLAYBACK, s_hook, Kernel32.GetModuleHandleW(null), 0);
if (s_hhook == IntPtr.Zero) { throw new SecurityException(SR.SendKeysHookFailed); }
```

```csharp
// Send() 903-966行（JournalHook経路の要点）
if (s_sendMethod.Value == SendMethodTypes.JournalHook || s_hookSupported!.Value)
{
    ClearKeyboardState();
    InstallHook();          // ← フック設置。try/finallyの保護なし
    SetKeyboardState(oldState);
}
...
if (wait) { Flush(); }       // ← DoEventsループでイベント配信完了を待つ
```

```csharp
// Flush() 988-995行
public static void Flush()
{
    Application.DoEvents();
    while (s_events.Count > 0) { Application.DoEvents(); }
}
```

```csharp
// UninstallJournalingHook() 1000-1010行
private static void UninstallJournalingHook()
{
    if (s_hhook != IntPtr.Zero)
    {
        s_stopHook = false;
        s_events.Clear();
        User32.UnhookWindowsHookEx(s_hhook);
        s_hhook = IntPtr.Zero;
    }
}
```

`UninstallJournalingHook()`の呼び出し元は本ファイル内では`OnThreadExit`（スレッド終了時、try/catchで
例外握り潰し）のみ確認できた。フックコールバック本体（`SendKeysHookProc.Callback`、イベント配信完了時に
自らアンフックする設計と推測される）は別ファイル（`SendKeysHookProc.cs`相当）にあり、**本調査では
その実装までは追えていない（不明点として明記）**。

**推測（事実と峻別）**：`Send()`のJournalHook経路は`InstallHook()`〜`Flush()`の間、明示的な
try/finallyでの保護がない。もし`Flush()`のDoEventsループが（対象ウィンドウがメッセージを処理しない等の
理由で）終わらない状態に陥った場合、フックが解除されないまま処理がブロックし続ける可能性がある
構造には見える。ただしこれは実ソースから読み取れる構造上の懸念であり、実際にそのパスで
フリーズが再現することを示す一次情報（issue等）は発見していない。

## 2. JournalPlaybackフックの公式仕様（システム全体への影響）

出典: [JournalPlaybackProc callback function (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/win32/winmsg/journalplaybackproc)

> As long as a JournalPlaybackProc hook procedure is installed, **regular mouse and keyboard input is disabled**.

**公式ドキュメントに明記**：JournalPlaybackフック（SendKeysのJournalHook実装が使うフックそのもの）が
設置されている間は、通常のマウス・キーボード入力が無効化される、という仕様が存在する。

> if the user presses CTRL+ESC or CTRL+ALT+DEL during journal playback, the system stops the playback,
> unhooks the journal playback procedure, and posts a WM_CANCELJOURNAL message to the journaling application.

脱出手段はCtrl+Esc/Ctrl+Alt+Delのみ、という記述もある。殿の実際の復帰操作（スタートメニュー経由、
マウスは生存）とは経路が異なる（Ctrl+Alt+Del等を明示的に使ったとの記録は引き継ぎメモになし）ため、
**この点は仮説を完全には裏付けない食い違いとして正直に記載する**（マウスが生存していた点も、
公式ドキュメントの「マウス・キーボード両方無効化」という記述とは一部整合しない）。

また同ドキュメントには、フック実装側のバグパターンとして以下の記述もある：

> If code is HC_GETNEXT and the return value is greater than zero, the system sleeps for the number of
> milliseconds... The system will appear to be not responding.

これは自作JournalPlaybackフックの実装注意点であり、.NET SendKeys側の実装がこのパターンに
該当するかどうかは未確認（自身の実装は上記の通りDoEventsループ待機であり、直接this注意点には
該当しない可能性が高いが、断定はできない＝不明点）。

### 傍証：兄弟フック（WH_JOURNALRECORD）の実例報告

出典: [WH_JOURNALRECORD Hook Blocks Mouse Clicks + Keystrokes (Microsoft Q&A)](https://learn.microsoft.com/en-us/answers/questions/167703/wh-journalrecord-hook-blocks-mouse-clicks-keystrok)

WH_JOURNALPLAYBACKと対をなすWH_JOURNALRECORDフックについて、実機で「マウスクリック・キーボード
入力が完全にロックされ、Ctrl+Alt+Delでのみ復帰可能」という実例が公式Q&Aに報告されている
（32/64bit不整合・サードパーティ干渉が主因として挙げられている）。**これはSendKeysが使う
WH_JOURNALPLAYBACKそのものの事例ではないが、同系統のジャーナルフックがシステム全体の入力を
実際にロックしうることの傍証として扱う**（事実と推測の峻別：これは「傍証」であり「直接証拠」ではない）。

### SendKeys.SendWait自体の既知issue検索結果

`dotnet/winforms`・`dotnet/wpf`等のGitHub issue、Web検索で「SendKeys freeze system-wide」
「SendKeys journal hook hang」等を検索したが、**SendKeys.SendWaitそのものがシステム全体を
フリーズさせたことを報告する既知issueは発見できなかった**（検索範囲内。存在しないと断定はできず、
「発見できず」に留める）。CodeProjectのフォーラム記事（"SendKeys.Send make my system hangs"）は
検索結果に存在を確認したが、WebFetchでの内容取得に失敗し（空白応答）、内容確認はできなかった。

## 3. ecad2側のSendKeys使用箇所

出典: `C:\ECAD2\.claude\skills\ecad2-ui-automation\helpers.ps1`、`SKILL.md`（実コードgrep確認済み）。

**呼び出し箇所は1箇所のみ**：

```powershell
# helpers.ps1 301-306行
function Send-Ecad2Keys {
    param([Parameter(Mandatory)][string]$Keys)
    Set-Ecad2Foreground
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds 200
}
```

- `SKILL.md`（0節・3節）における設計意図は「Esc・Ctrl+Tab等の**グローバルショートカット検証**」用途。
  ただし関数自体は任意の文字列を受け付ける汎用実装であり、**技術的にはファイルパス等の長い文字列も
  送信可能**（設計意図と実装の間に制約がない）。
- 引き継ぎメモにあった「開くダイアログへファイルパス+Enterを送る」という具体的手順は、`SKILL.md`・
  `helpers.ps1`のどちらにも定型手順として明記されていない。忍者が汎用の`Send-Ecad2Keys`を使い
  手動でファイルパス文字列を送った可能性が高いと推測されるが、**コード上の直接証拠はなく推測に留まる**。

### リスクの高い呼び出しパターン（推測含む、明示）

1. **長い文字列送信**：ファイルパスのような長い文字列は生成イベント数が多く、`Flush()`の
   DoEventsループが完了するまでの時間・ウィンドウが長くなる。短いショートカットキー（`{ESC}`等）に
   比べ、対象ウィンドウ側で処理待ちが発生するリスクの窓が広がる（推測）。
2. **フォーカス未確定のタイミング競合**：`SKILL.md`のトラブルシュート節（239-252行）に既知の罠として
   記載されている「モーダルダイアログの多重化」「ダイアログ検出の遅延」等、対象ウィンドウが
   期待通りにフォーカス/表示されていない状態で`SendWait`を呼ぶと、送信したキーイベントが
   意図した相手に届かず取りこぼされ、`Flush()`のイベント消化待ちが長引く可能性がある（推測）。
3. **対象アプリのメッセージポンプ停止との組み合わせ**：モーダルダイアログ表示中など、対象アプリの
   UIスレッドが一時的にメッセージを処理しにくい状態と`SendWait`が重なった場合、上記1.のリスクが
   増幅されうる（推測）。

---

## まとめ

| 観点 | 結果 |
|---|---|
| (1) 内部実装方式 | JournalHook（WH_JOURNALPLAYBACKフック）とSendInputの2方式、既定はJournalHook優先。一次情報（公式ドキュメント＋実ソース）で確認済み |
| (2) 既知フリーズ事例 | JournalPlaybackフック自体が「設置中は入力無効化」と公式ドキュメントに明記（一次情報）。SendKeys.SendWait固有のフリーズissueは検索範囲内で未発見。兄弟フック（WH_JOURNALRECORD）の実例報告を傍証として確認 |
| (3) ecad2側の使用箇所 | `Send-Ecad2Keys`（helpers.ps1）1箇所のみ。長文字列送信・フォーカス未確定タイミングがリスクパターン（推測） |

## 不明点

- SendKeysHookProc.Callback（フックコールバック本体）の実装は未確認。正常時にどのタイミングで
  自らアンフックするかの詳細ロジックは追えていない。
- 殿PCで実際に発生した現象（キーボードのみ不通、マウス生存）とJournalPlaybackフックの公式仕様
  （マウス・キーボード両方無効化、Ctrl+Alt+Delでのみ復帰）は完全には一致しない。技術的関連は
  否定できないが、確定的な裏付けには至っていない。
- SendKeys.SendWaitそのものがシステム全体フリーズを起こした直接の既知issueは未発見（存在しないとは断定しない）。

## 派生提案（範囲外の気づき、着手せず）

- `Send-Ecad2Keys`にキー文字列長の上限チェックや、送信前にフォーカス確定を待つガードを設ける等の
  対策は技術的に考えられるが、実装要否・方式は家老・殿の裁定次第（隠密は提案のみ）。

---

## 4. 追記：忍者実機発見によるリスクパターン③の実体解明（2026-07-11、家老共有）

忍者の実機試行・コード側調査により、上記「2. リスクの高い呼び出しパターン」③（フォーカス未確定の
タイミング競合）の具体的な実体が判明した。以下は忍者の調査結果を家老経由で受け取ったものであり、
**隠密自身が実機で再現・検証したものではない**（事実と伝聞の区別を明示）。

### 忍者の実機発見（伝聞、家老共有分）

- アプリ側`OpenButton_Click`の実装（`ShowDialog(this)`）は標準的で不備なし。WPFの`ShowDialog`は
  内部的に親ウィンドウを`EnableWindow(FALSE)`で無効化した上でダイアログをモーダル表示する。
- 検証ヘルパー側`Set-Ecad2Foreground`（`helpers.ps1` 148-153行、隠密が実コードで確認済み）は
  `$proc.MainWindowHandle`（＝アプリのメインウィンドウ、`ShowDialog`表示中は無効化されているはず）
  に対し**無条件で`SetForegroundWindow`を呼ぶ**：

  ```powershell
  function Set-Ecad2Foreground {
      $proc = Get-Ecad2Process
      if (-not $proc) { throw "Ecad2.App process not found." }
      [Ecad2Native]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
      Start-Sleep -Milliseconds 300
  }
  ```

  `Send-Ecad2Keys`（301-306行）は呼び出し冒頭で必ずこの`Set-Ecad2Foreground`を経由する。
- 忍者はこれにより、モーダルダイアログ表示中でも`GetForegroundWindow`でメインウィンドウ側が
  実際にアクティブ化されてしまう現象を実機で確認（家老共有分、隠密未検証）。
- 家老の整理：「無効化されているのにアクティブ」という、通常のマウス操作（ダイアログ表示中は
  メインウィンドウをクリックしてもモーダル制約で無視される）では起こり得ない状態が、検証ヘルパー
  経由の`SetForegroundWindow`によって人為的に作られていた疑い濃厚。

### 原因像の更新

上記「2. リスクの高い呼び出しパターン」③で「フォーカス未確定のタイミング競合」と推測していた
内容は、より具体的には**検証ヘルパー`Set-Ecad2Foreground`が対象を判別せず常にメインウィンドウへ
`SetForegroundWindow`する実装になっている点**に起因する疑いが濃厚となった。この状態で
`SendKeys::SendWait`がキー入力（Enter含む）を送ると、本来ダイアログが受け取るべき入力が
無効化されているはずのメインウィンドウ側へ渡る／Windows内部のフォーカス・有効化状態に不整合が
生じる、という筋道が成り立つ。

**事実と推測の峻別**：
- 事実（隠密がコードで確認済み）：`Set-Ecad2Foreground`が`$proc.MainWindowHandle`に無条件で
  `SetForegroundWindow`を呼ぶ実装であること。
- 伝聞（家老共有、隠密未検証）：モーダルダイアログ表示中でもこの呼び出しによりメインウィンドウが
  実際にアクティブ化される現象が実機で確認されたこと。
- 推測：この状態が1節・2節で述べたJournalHookフックの入力無効化と組み合わさり、フリーズの
  引き金になった可能性。ただしEnter送信実験自体は殿確認待ちで保留中（家老共有分）であり、
  **本フリーズ説はまだ実機再現による最終確認を経ていない**。

**この疑いが正しければ、本事象は「通常のマウス操作では起こり得ない、検証ヘルパー固有の状態」に
起因する可能性が高く、殿の通常操作（アプリを直接マウス・キーボード操作する場合）では再現しない
と推測される**（推測）。ただし1節で述べた通り、JournalHookフック自体がシステム全体の入力を
無効化する性質はSendKeys.SendWaitの通常操作でも共通して存在するため、フリーズの根本的な
可能性がヘルパー固有の問題に完全に還元されるとは断定できない。

---

## 5. 対策プラン検討（task_id=4、家老采配）

原因究明が一区切りしたことを受け、対策プランを複数案で検討する。**本節は提示のみ、実装（コード
修正）は行わない**（スコープ境界どおり）。現行スキルの設計思想（フォーカス非占有優先、
`Invoke-Ecad2Button`等のUI Automationパターン系は非占有・`Send-Ecad2Keys`/座標クリックのみ要注意）
との整合性を各案とも満たす（いずれもSendKeys経路の安全性を高める方向のみで、非占有系には触れない）。

### 5.1 `Set-Ecad2Foreground`の修正案

**案A（推奨）：モーダル無効化状態を検出してスキップ**

`IsWindowEnabled`（新規P/Invoke宣言、`Ecad2Native`へ1行追加）でメインウィンドウが無効化されて
いる（＝モーダルダイアログ表示中と推定できる）場合は、`SetForegroundWindow`の呼び出し自体を
スキップする。

```powershell
function Set-Ecad2Foreground {
    $proc = Get-Ecad2Process
    if (-not $proc) { throw "Ecad2.App process not found." }
    if (-not [Ecad2Native]::IsWindowEnabled($proc.MainWindowHandle)) {
        return   # モーダルダイアログ表示中と推定、メインウィンドウのアクティブ化はしない
    }
    [Ecad2Native]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 300
}
```

- 規模：小（P/Invoke宣言1行＋分岐3〜4行）
- リスク：低。通常時（メインウィンドウが有効）の既存動作には一切影響しない。モーダル時は
  「何もしない」に倒すだけなので新規の副作用を生まない。
- コスト：小。侍実装の見積もりは軽微（他ヘルパーの延長で完結）。
- 留意点：この案は「モーダルダイアログへキー入力したい」という用途そのものを暗黙にブロックする
  効果も持つ。ただし`Send-Ecad2Keys`の設計意図（`SKILL.md`）はもともと「グローバル
  ショートカット検証」であり、ダイアログへの入力送信は想定用途外（P-056はこの想定外運用で
  発生した）。よってこの副作用は安全側の設計として妥当と考える（推測含む判断、家老・殿の
  裁定を仰ぐ）。

**案B：実際に有効な最前面ウィンドウを対象にする**

`SKILL.md` 6節に既存の`EnumWindows`列挙ロジックを転用し、Ecad2プロセスに属し・可視・有効な
最前面ウィンドウ（ダイアログ含む）を判定して、そちらへ`SetForegroundWindow`する。

- 規模：中（既存のEnumWindows列挙を転用できるが判定条件が複雑化）
- リスク：中。ダイアログを正しく検出できればより「意図通り」の動作になるが、検出ロジック自体の
  バグ（複数ダイアログ多重化時の挙動含む）が新たな不具合を生むリスクがある。
- コスト：中〜大。
- 評価：案Aで根本原因（無条件のメインウィンドウアクティブ化）は解消できるため、現時点で案Bまで
  踏み込む必要性は薄いと考える（過剰対応の懸念）。

### 5.2 `Send-Ecad2Keys`側の安全策

**案A（推奨）：長文字列送信のガード**

ショートカットキー表記（`{ESC}`、`^{TAB}`等）は通常十数文字以内に収まる。ファイルパス等の
長文字列送信は設計意図（グローバルショートカット検証）から外れた使い方であるため、文字数上限を
設けて警告的に止める。

```powershell
function Send-Ecad2Keys {
    param([Parameter(Mandatory)][string]$Keys, [switch]$Force)
    if ($Keys.Length -gt 20 -and -not $Force) {
        throw "Send-Ecad2Keys: キー文字列が長すぎます('$Keys', $($Keys.Length)文字)。ショートカットキー検証以外の用途（ファイルパス入力等）が疑われます。ダイアログへの入力は別手段（最近使ったファイル一覧・UIボタン操作等）を優先してください。意図的な場合は -Force を指定。"
    }
    Set-Ecad2Foreground
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds 200
}
```

- 規模：小
- リスク：低。既定の用途（短いショートカットキー）には影響なし。
- コスト：小。

**案B：送信直前のフォアグラウンド/有効状態の再検証**

`Set-Ecad2Foreground`呼び出し後、実際にメインウィンドウがフォアグラウンドかつ有効になっているかを
`GetForegroundWindow`＋`IsWindowEnabled`で再確認し、満たさなければ送信自体を中止する。

```powershell
function Send-Ecad2Keys {
    param([Parameter(Mandatory)][string]$Keys)
    Set-Ecad2Foreground
    $proc = Get-Ecad2Process
    if ([Ecad2Native]::GetForegroundWindow() -ne $proc.MainWindowHandle -or -not [Ecad2Native]::IsWindowEnabled($proc.MainWindowHandle)) {
        throw "Send-Ecad2Keys: メインウィンドウがフォアグラウンド/有効状態ではありません（モーダルダイアログ表示中の疑い）。キー送信を中止しました。"
    }
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds 200
}
```

- 規模：小〜中（`GetForegroundWindow`のP/Invoke宣言が別途必要）
- リスク：低〜中。誤検知で正当な呼び出しがブロックされる可能性はゼロではないが、モーダル
  ダイアログへの送信自体がそもそも想定用途外のため許容範囲と考える。
- コスト：小。
- 5.1案Aと組み合わせると二重の安全網になる（5.1案Aでフォーカス誤爆自体を防ぎ、5.2案Bで
  それでも異常状態のまま送信されることを最終防止する）。

**案C（非推奨・参考）：SendKeys自体を自前のSendInput実装へ置換**

JournalHookフック自体のリスク（1節・2節で述べたシステム全体入力無効化）を根本から避けるため、
`SendKeys.SendWait`を使わず独自にタイムアウト管理付きの`SendInput`実装へ置き換える。

- 規模：大（新規実装）
- リスク：中（新規実装固有のバグリスク）
- コスト：大
- 評価：P-056の実機発見（4節）によれば根本原因はフォーカス誤爆であり、JournalHook自体の
  リスクは今回のフリーズの直接要因と確定したわけではない。現時点でここまでの対応は過剰と考える
  （参考として提示するのみ、非推奨）。

### 5.3 `SKILL.md`トラブルシュート節への反映内容案

以下を6節（トラブルシュート）へ既知の罠として追記する案：

> **`Send-Ecad2Keys`でダイアログへ文字列（ファイルパス等）を送るのは避ける**（2026-07-11、
> P-056で実証）。`Set-Ecad2Foreground`はモーダル状態を考慮せず無条件にメインウィンドウを
> アクティブ化する実装のため、`ShowDialog`内部の`EnableWindow(FALSE)`と矛盾する
> 「無効化されているのにアクティブ」という状態を作り、フォーカス誤爆→意図しないコマンド
> 誘発（実機ではCtrl+O相当の再トリガーによるダイアログ多重化）につながった実例がある。
> ダイアログへのテキスト入力は`Send-Ecad2Keys`ではなく、最近使ったファイル一覧のボタン操作・
> 物理クリック+短いキー入力など別手段を優先すること。

加えて0節（前提・既知の制約）の`Send-Ecad2Keys`に関する既存記述へ「モーダルダイアログ表示中の
使用は特に注意」の一文を追加する案も併せて提示する。

- 規模：小（ドキュメント追記のみ）
- リスク：なし
- コスト：小

### 5.4 推奨する組み合わせ

**5.1案A＋5.2案A＋5.3（ドキュメント反映）** を推奨する。いずれも小規模・低リスクで相互補完的
（5.1案Aが根本原因を塞ぎ、5.2案Aが誤用そのものを未然に防ぎ、5.3が再発防止の知識を残す）。
5.2案Bは5.1案Aとやや安全網が重複するため必須ではないが、コストが小さいため併用しても害はない
（家老・殿の裁定次第）。5.1案B・5.2案Cは現時点では過剰対応と考え非推奨。

実装コスト目安（隠密の見積もり、侍による実測ではない）：5.1案A 約30分、5.2案A 約15分、
5.2案B 約20〜30分、5.3 約15〜20分。いずれも1時間以内に収まる小規模な変更と見積もる。
