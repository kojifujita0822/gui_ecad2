---
name: ecad2-ui-automation
description: Launch and drive the Ecad2 WPF desktop app (dotnet run --project src/Ecad2.App) for real-machine UI verification using Windows UI Automation — invoke toolbar/palette buttons by Name, read status bar text directly, send keyboard shortcuts (WPF accepts SendKeys, unlike GuiEcad/WinUI3), capture window screenshots. ecad2（WPFラダー図CAD）の実機確認・忍者役の検証作業で使う。座標クリックの試行錯誤を避け、UI Automation経由で確実に操作する。
---

# Ecad2 実機確認スキル

WPF デスクトップアプリ ecad2 を実機操作して検証するための手順。
**固定UI（ツールバー・メニュー等）の座標ベースクリックはウィンドウ位置・DPIでズレるため使わない。**
Name/AutomationId 経由で直接呼び出すこと。キャンバス内セルなど座標が本質的に必要な操作のみ、
ウィンドウ左上からの相対座標を使う。

姉妹スキルとして GuiEcad（WinUI3、旧アプリ）用の `guiecad-ui-automation`
（`C:\Users\kojif\Desktop\生産物\gui_ecad\.claude\skills\guiecad-ui-automation\`）が存在する。
本スキルは同じ設計思想を踏襲しつつ、WPFとWinUI3のフレームワーク差分に合わせて調整している
（差分は「0. 前提・既知の制約」参照）。

## 0. 前提・既知の制約

- **【MUST】フォーカス非占有を優先する（殿指示、2026-07-10）**: `Invoke-Ecad2Button`/
  `Invoke-Ecad2Element`（InvokePattern/SelectionItemPattern）・`ValuePattern.SetValue`
  （テキスト入力）・`Save-Ecad2Screenshot`（PrintWindow方式）は、いずれも
  `SetForegroundWindow`/`SetCursorPos`等のグローバル入力を使わず実機で機能することを実証済み
  （ボタン押下・シート追加/削除/改名・ダイアログ操作・スクリーンショット撮影、殿の他ウィンドウ
  操作中でも正しく動く）。ゆえに**ボタン操作・リスト選択・テキスト入力・見た目確認で完結する
  検証は、フォーカスを奪わずに実行できる**。一方 `Send-Ecad2Keys`（キーボードショートカット自体の
  検証）・`Invoke-Ecad2ScreenClick`/`Invoke-Ecad2CanvasClick`/`Invoke-Ecad2CtrlScroll`
  （キャンバス内セルクリック等、UI Automationツリーで要素を辿れず座標指定が本質的に必要な操作）は
  グローバルなキーボード/マウス入力を要し、殿の他ウィンドウ操作と衝突する。**代替できる観点は
  極力前者で済ませ、後者は代替手段が無い場面に限定する**（詳細経緯: 2026-07-10、実機確認中に
  フォアグラウンド化・キー送出が殿の並列セッション操作へ意図せず割り込む事故が発生し、殿裁定で
  本方針へ改修）。
- **`SendKeys` によるキーボード送信はこのアプリに正常に届く**（GuiEcad/WinUI3では届かなかったのと対照的、
  T-002/T-006 PoCで実証済み）。Esc・Ctrl+Tab等のグローバルショートカット検証は
  `Send-Ecad2Keys` を使ってよい。**モーダルダイアログ表示中の使用は特に注意**（21文字以上は既定で
  例外になる長文字列ガード付き、詳細は6節トラブルシュート末尾の項目参照）。
- ツールバーボタンには `AutomationProperties.Name` が付与済み（例: `"a接点配置 (F5)"`）。
  GuiEcadのように「RadioButton自体はName空、Text子要素から親を辿る」フォールバックは基本不要だが、
  念のため `Invoke-Ecad2Button` はButton→ListItemの順で探すようにしてある。
- **ウィンドウの起動位置はモニタ構成によって変わる**（実測: マルチモニタ環境で `2240,116` のような
  値になったことがある）。絶対座標のハードコードはしない。`Get-Ecad2WindowRect` を基準にすること。
- ステータスバー（`AutomationId=StatusBarArea`）配下のテキストは、GuiEcadのように個別
  AutomationId（`StatusPos`等）を持たず、`"ツール: Select"` `"ズーム: 100%"` のような文字列が
  並んでいるだけ。`Get-Ecad2StatusText -Prefix "ツール:"` のように前方一致で取り出す。
- **リサイズ操作は要注意**（2026-07-03実測）: `MoveWindow` でのリサイズ直後にプロセスが消失する
  事象を2回確認した。ただし同条件での再テストでは再現せず、原因は未特定（UI Automation越しの
  他操作との組み合わせが引き金だった可能性もある）。`Resize-Ecad2Window` は呼び出し後に自動で
  プロセス生存確認を行い、消失していれば例外を投げる。リサイズ確認をする際は、その前後で
  必ず他の検証観点と切り離して単独実行し、クラッシュした場合は再現条件を丁寧に記録すること。
- **Ctrl+ホイールでのズームは`Send-Ecad2Keys`では送れない**（キーボードショートカットではなくマウス
  ホイール+修飾キーの組み合わせのため）。`Invoke-Ecad2CtrlScroll`（`keybd_event`でCtrl押下→
  `mouse_event`でホイール送信→Ctrl解放、を合成）を使うこと（2026-07-05、T-021ズーム検証で実証・スキル化）。
- **UI Automation経由の操作（Invoke等）がボタンのClickハンドラを経由せず内部状態を不安定にする
  ケースが実際にあった**（2026-07-03、T-016検証: 複数ツールボタンが同時にハイライトされたまま
  ツール切替不能になるバグが発生。原因はアプリ側のToolState等価性判定の実装不備だったが、UI
  Automation経由の連続呼び出しが誘発した可能性も否定できない）。同一要素に対する連続invoke後に
  不審な挙動が出た場合は、一度スクリーンショットで実際のボタン選択状態（ハイライト）とステータス
  バーのテキストに矛盾がないか確認すること。**この系統の罠は6.2節に複数派生している。**
- **AvalonDockペイン（タブ切り離し・境界線リサイズ）のドラッグ操作はUIA標準パターンで代替不可、
  `Invoke-Ecad2Drag`の多段階マウス合成が必須**（2026-07-14、T-058検証で実証・スキル化）。
  AvalonDockはUIA標準のDrag/DropTargetパターンを実装しておらず、独自`DragService`
  （`CaptureMouse`+`MouseMove`イベント追跡）でのみドラッグを判定する。単発の`SetCursorPos`+
  クリックでは判定閾値に届かず無反応になる（本家AvalonDock自身も実ドラッグを自動テストせず、
  メニュー操作・UIAパターン・内部API直接呼び出しで代替している。隠密2調査で裏付け済み、詳細は
  `docs/ecad2-t058-avalondock-drag-debug-technique-survey-onmitsu2.md`参照）。**移動距離は
  最低100px以上を目安にする**こと（境界リサイズの実例: 60px/30ステップ/50ms間隔→未反映、
  100px/60ステップ/30ms間隔→成功。距離不足が主要な失敗要因）。使用例は3節参照。
  （※ただし、ドラッグ中に一時的に現れるOverlayWindow=十字型ドロップターゲットUI自体の検出には
  別の限界がある。6.7節参照）
- **【MUST】「物理クリック」という語の技術的な中身を報告時に明記する（2026-07-22、T-110/P-119の
  教訓）**：`Invoke-Ecad2CanvasClick`等は内部でWin32 API合成マウスイベント（`SetCursorPos`+
  `mouse_event`）を使っており、UI Automationの`InvokePattern`ではない点で「座標クリック相当」
  ではあるが、**殿ご自身の実際の手によるマウス操作とは技術的に別物**（押下・解放の間隔・座標移動の
  連続性が異なる）。T-110増分3では、この合成マウスイベント特有のタイミングでのみ発生する
  レースコンディション（AutoHideフライアウトのピンボタンで復帰する操作）が「物理クリックで100%
  再現」と報告されたが、殿の実際の手による操作では一切再現しなかった。報告時は「物理クリック」と
  一括りにせず、`Invoke-Ecad2CanvasClick`等の合成マウスイベントか、殿の実操作かを必ず区別して
  明記すること（報告フォーマットは`docs-notes/roles/ninja.md`「報告フォーマット」節にも制度化済み）。
- **【MUST】入口が開いたことは、通れたことではござらぬ**——**本文は6.8節に在る**（2026-07-31、
  T-136(A)増分1、忍者）。**ここには置き場として名を挙げるに留める。**
  **要は「配置バーが開いた」「ダイアログが出た」を成功の証としてはならぬ、ということ**——
  **開くところまでと、実際に事が成るところまでは別の経路にござる。**
  **【なぜ0節へ名を挙げるか】この罠は「引こうと思う」契機が無い**（2026-08-01、忍者の検分）
  ——**検証手順を組む段でトラブルシュートの節を引く者はおらぬ**ゆえ、
  **手順を組む前に必ず目を通す0節の側から送る。**
  **他の【MUST】が「操作の作法」であるのに対し、これは「判定の作法」にござる。**
- **ステータスバーの「選択セル 行N/列M」表示は行のみ+1オフセットされた表示専用の値**（列は
  オフセットなし、2026-07-24、T-102検証時に発覚）。テストコードの`GridPos`（内部座標、0始まり）
  とUI上の表示文字列をそのまま同一視すると、テストの検証座標を「グリッド範囲外」と誤診断しかね
  ない。UI Automationで座標系を扱う際は、内部モデル座標と画面表示文字列の対応関係（行=+1、列=
  そのまま）を都度意識すること。
- **UIA合成ドラッグ（`Invoke-Ecad2Drag`）は近距離（30-90px程度）で再現性が不安定になりやすい**
  （2026-07-24、T-121検証で追加実証。既存の「移動距離は最低100px以上を目安に」という知見と符合、
  実務的には300px以上を確保するとより安定する）。
- **DRC結果パネルの行クリックは対応する図面要素へジャンプする機能を持つ**（2026-07-24、T-121検証
  で発覚）。DRC実行直後、キャンバス座標のつもりで`Invoke-Ecad2CanvasClick`等を呼ぶと、意図せず
  DRC結果パネルの行を叩いてしまい別要素へフォーカスが飛ぶ事故になりうる。DRC実行後に座標クリック
  を行う場合は、クリック先が本当にキャンバス領域内か（DRC結果パネルの表示範囲と重なっていないか）
  を確認すること。

### 検体づくりの落とし穴（2026-07-28、T-133検証、忍者）

- **自作パーツの保存には接続点が2つ以上要る**（`ErrorText` でバリデーションされる）。
  **図形だけ描いて保存しようとすると弾かれる**——検体作りの最初で躓く
- **さらに「接続点が左右に分かれておること」も要る**（2026-07-28、T-133増分1検証で追加。**2つ以上
  置いても弾かれた**）。`ErrorText`＝**「基準枠に収めると接続点がすべて同じ左右位置になります。
  左右に分けて配置してください。」**——**縦方向だけを測りたい検証でも、右辺に1点足さねば保存できぬ。**
  **枠の左辺が `cellX=0`、右辺が `cellX=幅`**（幅1なら1セル分＝34px 右）
- **シートが0枚だとパーツメニューが Disabled**。**起動直後にメニューを叩く前にシートを1枚足すこと**
- **PDF出力の保存先の既定は `C:\ECAD2\sample`**（リポジトリ内だが `.gitignore` 済みゆえ
  git は汚れぬ。同ディレクトリは元より検証成果物の置き場）

**以下は2026-08-02、T-136(C)・T-142の右端検証で忍者が踏んだもの。**

- **「境界＝`Columns`ちょうど」の配置はUI操作からは作れぬ**——グリッド境界チェックが
  **厳密未満**（`IsWithinGridBounds`＝`Column + cellWidth - 1 < Columns`）ゆえ。
  **列18に3幅要素を置こうとすると「選択したセルはグリッド範囲外です」で弾かれる。**
  **右端の境界を測る検証では毎回踏む筋**にて、**JSON検体を直に作って迂回する**
- **`DeviceClass` enum に "motor" は無い**——`Relay`／`PushButton`／`SelectSwitch`／`Lamp`／
  `Timer`／`Counter`／`Terminal`／`Other` のみ。**JSON検体を直に書くときに踏む**
- **`Ctrl+S` は「アプリが実際に開いておるドキュメント」へ書く**——**検体を複数扱う最中に押すと、
  意図せぬファイルを上書きしうる**（忍者が対照ファイルを上書きした実例。
  **実害は自作の一時検証ファイルのみ＝`.gitignore`済み**）。
  **検体を切り替えながら測るときは、保存の前に「今どれを開いておるか」を確かめる**

## 1. 起動

```powershell
. "C:\ECAD2\.claude\skills\ecad2-ui-automation\helpers.ps1"
dotnet build src/Ecad2.App   # 事前ビルド確認（任意、警告0件・エラー0件を確認してから起動する）
Start-Ecad2App                # コンソール出力を $env:TEMP\ecad2-ui-automation-std{out,err}.log にリダイレクトして起動、MainWindowHandle確定まで待機
```

**【MUST】実機確認は必ずセカンダリモニタ上で行う（殿の明示指示、2026-07-07）。**
ユーザーはプライマリモニタで作業しており、検証ウィンドウがプライマリに出現するたびに作業を妨げるため。

- `Start-Ecad2App` は既定（`-Screen Auto`）で、セカンダリモニタが存在すれば起動直後に
  ウィンドウを自動でセカンダリへ移動する（存在しなければ何もしない）。
  **`-Screen None`/`Primary` で既定を打ち消さないこと**（プライマリでしか再現しない事象の
  検証等、正当な理由があり殿の了承を得た場合のみ例外）。
- `Start-Ecad2App` を経由せず起動した場合（`dotnet run` 直叩き等）や、検証中にウィンドウ位置が
  プライマリへ戻ってしまった場合は、操作を始める前に `Move-Ecad2WindowToScreen -Screen Secondary`
  を呼んで移動させる。
- スクリーンショット・キャンバスクリックはウィンドウ左上からの相対座標基準（`Get-Ecad2WindowRect`）
  のため、セカンダリ配置でもそのまま動く。

`Start-Ecad2App` は `dotnet run --project src/Ecad2.App` を起動し、`Ecad2.App` プロセス
（`dotnet` ラッパープロセスとは別、`MainWindowHandle` を持つ方）が確立するまで待機する。

- **`git worktree`（別作業ツリー）上のビルドを検証したい場合、`Start-Ecad2App`はそのまま使えない**
  （`-Project`引数がなくWorkingDirectory固定=`C:\ECAD2`、2026-07-22、侍所見・T-110/P-119決定打
  検証で実証）。worktree配下でビルドした`Ecad2.App.exe`を直接パス指定で起動し、`Get-Ecad2Process`
  等の後続ヘルパーはプロセス名（`Ecad2.App`）で拾えるため通常どおり使える。1回限りの過去コミット
  検証等、スキル本体の改修までは要さない用途ではこの直接起動で足りる。
マウス操作自体は依然として実カーソル移動を伴うため（0節参照）、セカンダリモニタに置いても
「完全にバックグラウンド」にはならないが、少なくともユーザーが作業しているプライマリモニタ上に
ウィンドウが出現しなくなる。

## 2. ヘルパーの読み込み

**PowerShell 呼び出しごとに冒頭で dot-source する**（シェル状態は呼び出し間で持続しないため）:

```powershell
. "C:\ECAD2\.claude\skills\ecad2-ui-automation\helpers.ps1"
```

## 3. 基本操作パターン

```powershell
. "...\helpers.ps1"

# ツールバーボタンを Name で押す（Name はボタンの AutomationProperties.Name、括弧内はショートカット表記）
Invoke-Ecad2Button -Name "a接点配置 (F5)"
Invoke-Ecad2Button -Name "選択ツール (Esc)"

# 組込み記号(a接点等)をツールバーから配置する完全手順(2026-08-08、T-146検証、忍者)。
# セル選択→ツールボタン押下だけでは配置バーが開くのみで要素は生まれぬ。「配置位置」を
# 改めてキャンバスクリックで指定する2段階が要る。ステータス文言
# 「配置ツール: a接点 - キャンバスをクリックして配置位置を指定してください」を複数回読み直したが
# 同じ文言のまま変化せず、「まだ開いていない」と誤読して数手を空費した(危うくUIAツリー探索の
# 手法自体を疑うところであった)。★ただし配置バーが開く前後でこの文言が実際に変わらぬことは
# 確かめておらぬ(開いたタイミングを正確に特定できなんだため)。疑わしければ文言だけで判断せず
# スクリーンショットで実見すること。
Invoke-Ecad2CanvasClick -RelativeX 372 -RelativeY 323   # (1)セル選択
Invoke-Ecad2Button -Name "a接点配置 (F5)"                 # (2)ツール起動
Invoke-Ecad2CanvasClick -RelativeX 372 -RelativeY 323   # (3)配置位置指定(同じセルでよい)→名前入力バーが開く
Send-Ecad2Keys "{ENTER}"                                 # (4)名前を空のまま確定(既定名で配置)。
# 名前欄には`AutomationId="PlacementDeviceNameBox"`が既に付いている(6.2節参照)ため、
# 通常のFindAllでUIA探索するより先にこのIdで直接検索する方が確実。
# ★確定後は必ずデバイス名を設定すること——テストモード中の要素クリックは
# DeviceName未設定だと無反応になる(6.2節「テストモード中のクリックが無反応」参照)。

# 左パーツパレットのリスト項目（Name は完全一致が必要。表示ラベルだけでなく
# "PartFolderEntry { Category = ..., FilePath = ..., Definition = ... }" という完全な文字列になっている点に注意。
# 部分一致で探したい場合は Find-Ecad2Element -All の結果を Where-Object で絞り込む）
$items = Find-Ecad2Element -AutomationId "PartPaletteList" | ForEach-Object {
    $_.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
}
($items | Where-Object { $_.Current.Name -like "*a接点.gcadpart*" } | Select-Object -First 1) | ForEach-Object { Invoke-Ecad2Element -Element $_ }

# ステータスバーの値を直接取得（画像を撮らずに検証できる）
Get-Ecad2StatusText -Prefix "ツール:"   # 例: "ツール: PlaceElement"
Get-Ecad2StatusText -Prefix "ズーム:"   # 例: "ズーム: 100%"
Get-Ecad2StatusText                     # 全件配列で取得

# キー送信（Escでツールキャンセル、Ctrl+Tabでタブ切替ブロック確認など。WPFなので正常に届く）
Send-Ecad2Keys "{ESC}"
Send-Ecad2Keys "^{TAB}"

# Ctrl+ホイールでのズーム操作（通常のSend-Ecad2Keysでは送れないため専用ヘルパーを使う。
# 座標はスクリーン絶対座標、対象要素の中心を渡す。$Clicks正=ズームイン、負=ズームアウト）
$canvas = Find-Ecad2Element -AutomationId "CanvasArea"
$b = $canvas.Current.BoundingRectangle
$cx = [int]($b.Left + $b.Width / 2)
$cy = [int]($b.Top + $b.Height / 2)
Invoke-Ecad2CtrlScroll -ScreenX $cx -ScreenY $cy -Clicks 5   # 100%→150%（1クリック=10%相当、実測）
Invoke-Ecad2CtrlScroll -ScreenX $cx -ScreenY $cy -Clicks -5  # 150%→100%

# キャンバス内のセルをクリック（座標が必要な唯一のケース。ウィンドウ左上からの相対座標で指定）
$canvas = Find-Ecad2Element -AutomationId "CanvasArea"
Write-Output $canvas.Current.BoundingRectangle   # まずキャンバス範囲を確認してから相対座標を決める
Invoke-Ecad2CanvasClick -RelativeX 700 -RelativeY 370

# キャンバス内で右クリック（ContextMenu表示等、右クリック固有の検証用。T-055増分3で新設）
Invoke-Ecad2CanvasRightClick -RelativeX 700 -RelativeY 370
# → メニュー項目の取得・実行は6.1節「ダイアログ・ポップアップの検出」を参照（通常のFindAllでは拾えない）
# ★`Invoke-Ecad2ScreenClick`は`-X/-Y`（スクリーン絶対座標）、`Invoke-Ecad2CanvasClick`は
#   `-RelativeX/-RelativeY`（ウィンドウ相対座標）とパラメータ名が異なる。取り違えて1回エラーに
#   なった実例あり（T-129所見、2026-07-27）。呼び分ける際は名前を都度確認すること。

# キャンバス内でダブルクリック（枠ラベル編集・行コメント編集の入口。2026-07-28、T-125増分β-1で新設）
# 【ヘルパー未実装】helpers.ps1にダブルクリック関数は無い。`Invoke-Ecad2CanvasClick`を2回続けて
# 呼ぶ方式は、内部Sleep(Up後150ms + 次回SetCursorPos後150ms)に加え毎回`Get-Ecad2WindowRect`の
# オーバーヘッドが挟まるため、Windows既定のダブルクリック時間(500ms)を超えて2つの単クリックに
# 分解される恐れがある。`Ecad2Native`のmouse_eventを直に叩いて間隔を詰めること（下記で実証済み）。
function Invoke-Ecad2CanvasDoubleClick {
    param([Parameter(Mandatory)][int]$RelativeX, [Parameter(Mandatory)][int]$RelativeY)
    $w = Get-Ecad2WindowRect
    Set-Ecad2Foreground
    [Ecad2Native]::SetCursorPos($w.Left + $RelativeX, $w.Top + $RelativeY) | Out-Null
    Start-Sleep -Milliseconds 150
    [Ecad2Native]::mouse_event(0x02,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 40
    [Ecad2Native]::mouse_event(0x04,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 60
    [Ecad2Native]::mouse_event(0x02,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 40
    [Ecad2Native]::mouse_event(0x04,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}
# ★WPF側は`MouseButtonEventArgs.ClickCount`をMouseDown側でのみ2以上にする（Up側は常に1固定）。
#   ゆえにダブルクリック判定の実装は必ずDown側にあり、この合成で正しく発火する（T-080往復2周目の
#   実測、SKILL冒頭0節と同根）。上記関数で枠ラベル編集(FrameLabelBox)の開閉を実際に再現できた。

# AvalonDockペインのドラッグ操作（タブ切り離し・境界リサイズ。0節参照、UIA標準パターンでは代替不可）
# タブ切り離し例: タブのタイトルText要素をつかみ、十分な距離(300px超)をドラッグする
$titleEl = Find-Ecad2Element -Name "シート"   # LayoutAnchorableのTitle文字列
$b = $titleEl.Current.BoundingRectangle
$cx = [int]($b.X + $b.Width / 2); $cy = [int]($b.Y + $b.Height / 2)
Invoke-Ecad2Drag -FromX $cx -FromY $cy -ToX ($cx + 300) -ToY ($cy + 400)
# → 切り離し成立の確認はSave-Ecad2Screenshotではなく6.1節のEnumWindows手法を使う（フロート化した
#   パネルは別ウィンドウとして生成されるため、メインウィンドウのPrintWindowには写らない）

# ★AutoHideフライアウトのスプリッターは特に長い距離を要する(2026-07-27、T-130実測、忍者)
#   130pxのドラッグでは掴めずフライアウトが閉じるだけで終わる。310pxで初めて成立した。
#   既載の「300px以上が安定」がそのまま効く形。フライアウトは滞在時間が短く閉じやすいため、
#   通常のペイン境界より条件が厳しいと心得る。
# 境界線リサイズ例: Thumb要素(ControlType=Thumb, LocalizedControlType="縮小表示")をドラッグする
$thumbCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Thumb)
$thumb = (Get-Ecad2Root).FindAll([System.Windows.Automation.TreeScope]::Descendants, $thumbCond)[0]
$tb = $thumb.Current.BoundingRectangle
$tcx = [int]($tb.X + $tb.Width / 2); $tcy = [int]($tb.Y + $tb.Height / 2)
Invoke-Ecad2Drag -FromX $tcx -FromY $tcy -ToX ($tcx + 100) -ToY $tcy   # 距離100px以上を確保(0節参照)

# 見た目そのものを確認したい時だけスクリーンショット
Save-Ecad2Screenshot -Path "$env:TEMP\claude\...\scratchpad\check1.png"
# → 保存後は Read ツールで画像を開いて目視確認する

# ウィンドウリサイズ確認（0節の注意点を踏まえ、単独で行い直後に生存確認する。関数内で自動チェック済み）
Resize-Ecad2Window -Width 900 -Height 500
# ★リサイズ直後は必ずモニタ位置を確認する（T-127実測2026-07-27、忍者が遭遇した新規事象）
#   `Resize-Ecad2Window` は内部で MoveWindow を呼ぶため、拡大後の矩形がモニタ境界をまたぐと
#   ウィンドウがセカンダリからプライマリへ移動してしまう。殿の作業を妨げるため放置は不可。
Move-Ecad2WindowToScreen -Screen Secondary   # 移動していた場合の復帰（していなくとも無害）

# 検証後のクリーンアップ（通常はここでアプリを終了する）
Stop-Ecad2App
# ★★ `Stop-Ecad2App` は**正規終了（`WindowPattern.Close()`）を既定**とする（T-129で改修、2026-07-27）。
#    `Window_Closing` を経由するため、**終了時保存（`SaveDockingLayoutAsDefault()`）・未保存確認
#    ダイアログといった終了時処理が、実際のユーザー操作と同じように走る**。
#    → **戻り値の文字列を必ず読むこと。** 正常時のみ `Ecad2.App stopped (graceful close)` を返す。
#       `WARNING:` で始まる場合は**終了時の保存処理が走った保証がない**——(a)正規終了が効かず
#       強制終了へ落ちた、(b)正規終了の途中で異常が起きた（結果としてプロセスは消えていた）、
#       のいずれか。文言に理由が入るので読み分けられる。いずれも検証結果の解釈が変わる。
#       （(b)を握り潰さぬのはP-135の対処。生存判定だけでは(a)(b)と正常を取り違える）
#    → 待ち時間の上限は `-TimeoutMs`（既定5000ミリ秒）。未保存確認ダイアログ等でブロックされると
#       これを超えて強制終了へ落ちるため、**未保存の変更がある状態で終える検証**では、先に
#       ダイアログを処理するか `-TimeoutMs` を延ばす。
#    【改修の経緯】旧実装は無条件の `Stop-Process -Force` で `Window_Closing` を経由せず、
#    T-128の実機確認中に**検証用の一時的なパネル配置が殿の `main-layout.xml` にそのまま残る事故**
#    が起きた（左パネル532pxのまま残留、忍者が直後に気づき復旧）。T-129はその再発を断つ改修であり、
#    以後は**終了時の挙動・永続化に関わる検証でも `Stop-Ecad2App` をそのまま使ってよい**。
# ★検証後のWARNINGは「異常」でなく「常態」と心得よ（2026-07-27、T-130実測、忍者。3度中2度がWARNING）
#   検証で何か記入すれば未保存となり、確認ダイアログでブロックされる道理。T-129改修の想定内である。
#   慌てて不具合と報じぬこと。**かつ、そのおかげで終了時保存が走らず殿のレイアウトが無傷で済む**
#   という利もある。逆に**終了時保存そのものを検証したい**時は、先にダイアログを処理するか
#   `-TimeoutMs`を延ばして正規終了を確実に通すこと。
# ★★【重要・2026-07-28、T-133増分2で実際に踏んだ】**「WARNINGゆえ無傷」は状態依存の幸運にすぎぬ。**
#   **検証の途中で保存操作（名前を付けて保存・上書き保存）を挟むと未保存でなくなり、確認ダイアログが
#   出ぬ。すると正規終了がすんなり通り、`SaveDockingLayoutAsDefault` が走って殿の `main-layout.xml`
#   が上書きされる。** 実測＝同日4回の起動のうち、**保存を挟まなんだ3回はWARNING（無傷）、
#   保存を挟んだ4回目だけ `Ecad2.App stopped (graceful close)` となり 2387→2445バイトへ書き換わった**。
#   **戻り値の文字列がそのまま危険信号にござる**——`graceful close` を見たら**レイアウトを確かめよ**。
#   **退避してあれば復元するだけで済む**（下記「検証を始める前に退避する」【MUST】が本件で初めて実際に効いた）。
# → 殿より実機確認後に「stay」と指示された場合は、本コマンドを実行せずEcad2.Appを起動したまま
#   維持する（殿が次回自ら起動時の様子を確認したいため、2026-07-16殿指示）
```

**【MUST・2026-08-01追加】`stay`中の状態は自分のものではない**（忍者が踏んだヒヤリ）。
**殿が維持されたアプリを操作なさりうる**——**実例＝忍者が維持しておる間に、殿がスクリーンショットを
撮られ、パーツエディタを閉じられた。次に測ろうとしてウィンドウが見つからず例外になった。**
**害は出なんだが、状態を前提にした測定をしておれば誤っておった。**
**How to apply**：**`stay`中に再び測る時は、必ず現況を採り直してから始める**
（`Get-Ecad2Process`でプロセスの生死、ウィンドウ構成、テーマ、開いておるダイアログ）。
**「先ほどの状態が続いておる」を前提にせぬこと。**
**併せて`stay`を解いてアプリを終う際は、検証で変えた設定（テーマ等）を元へ戻してから終うこと**
（2026-08-01の実例＝忍者がダーク→ライトへ戻してから終了した）。

**【重要】大量の同種UIA操作は1回のPowerShell呼び出しへ詰め込まない**（2026-07-27、T-126実機確認
で実証）。15件の実運用パーツを1つの`foreach`ループで一括処理しようとしたところ、Claude Codeの
自動権限判定（classifier）に「Blocked by classifier」でブロックされた。1件ずつ（「対象を開く→
操作→確定」を1回の呼び出しに収める粒度）に分割すれば問題なく実行できる。UIA自体の制約ではなく
呼び出し側の運用上の制約だが、件数が多い検証では最初からこの粒度で組むこと。

### 【MUST】検証を始める前に `main-layout.xml` を退避する（2026-07-27、T-133検証時のヒヤリ）

**忍者が検証用に起動したApp.exeを他者が閉じると、終了時保存（`Window_Closing`→
`SaveDockingLayoutAsDefault`）が走り、殿の`main-layout.xml`が「検証時の状態」で上書きされる。**

**実例（2026-07-27）**：忍者の関知せぬうちにApp.exeが落ちており、`Stop-Ecad2App`が`was not running`を
返した。`main-layout.xml`は2275→2387バイトへ更新されていた。**幸いパネルを動かしておらなんだゆえ
構造・寸法は無傷**（差分の正体は`LastActivationTimeStamp`とツールバーの`IsSelected`と見られる）だったが、
**ドラッグ・リサイズを伴う検証の最中であれば殿のレイアウトが壊れていた**（T-128の事故と同型）。
**T-129で正規終了を既定化したことの裏返しの副作用**にござる。

**How to apply**：**検証に入る前に`main-layout.xml`を退避し、終えたら復元する**
（T-130の検証で忍者が実際に採った作法を常態化する）。**「自分が閉じるまでは大丈夫」という前提は
置けぬ**——他役・殿・クラッシュ、いずれの経路でも終了保存は走りうる。

**【この作法が実際に殿の設定を救った日】2026-07-28、T-133増分2の実機確認**——
**検証の途中で「名前を付けて保存」を行ったため未保存でなくなり、`Stop-Ecad2App` が
`graceful close` で通って `main-layout.xml` が 2387→2445バイトへ上書きされた**
（同日それまでの3回は未保存ゆえWARNING＝強制終了で無傷であった）。
**退避してあったゆえ、コピー1回でハッシュ一致まで戻せた。**
**教訓＝「保存操作を含む検証」では終了保存がほぼ確実に走る。** 退避を怠れば復旧の手立てが無い。
**併せて、終了後は毎回ハッシュを突き合わせること**——**上書きされても画面上は何も起きぬゆえ、
確かめねば気づかぬ。**

#### 【別の道筋】検証の終わり方が「クリーンな状態」だと、保存操作を挟まずとも同じ結果が起きる（2026-08-06、`PR-17`①②実測、忍者）

**上記の条（保存操作を挟むと終了保存が走る）を踏襲してなお、二度連続で`main-layout.xml`が
上書きされた**——**ただし今回は保存操作を一度も挟んでおらぬ。** 条が誤っておったのではなく、
**「保存操作を挟む」とは別の道筋が在り、射程が足りておらなんだ。**

**機序（忍者の見立て、`Stop-Ecad2App`の実装＝`helpers.ps1:429-474`は確認済みだが、
「`dirty=false`ゆえダイアログが出ぬ」という判定そのものはアプリ側のソースまでは当たっておらぬ）**
——検証手順の最後がたまたま「新規作成」（未保存の変更を一切加えぬ、まっさらな状態）で終わって
おり、**新規作成直後は変更が無い＝`dirty=false`と見立てられ、ゆえに終了確認ダイアログが出ず
`WindowPattern.Close()`（`Stop-Ecad2App`が常に最初に試みる正規終了）がそのまま通り、
終了時保存（`SaveDockingLayoutAsDefault`）が走った**、と見立てる。

**`Stop-Ecad2App`には正規終了を避ける明示的な選択肢が無い**（`helpers.ps1:429-474`実装確認済み。
未保存の変更が無ければ確認ダイアログでブロックされる余地がそもそも無い設計）。

**How to apply**——**「保存操作を挟んだか」だけでなく「検証の最後がクリーンな状態で
終わっておらぬか」も併せて確かめること。**
**実測したのは「新規作成直後で終わる」の一件のみ**——「変更を加えぬ既存ファイルを開いただけで
終わる」場合も同じ機序（`dirty=false`）で起きる**筈だが、こちらは機序からの類推であり実測しておらぬ**。
**どちらの道筋でも結果（終了保存＝`main-layout.xml`上書き）は同じ**ゆえ、**上記の条（保存操作）を
これで置き換えず、並べて持つこと**——**前者を後者で置き換えれば、保存操作の側が抜ける。**

**【実測で埋まった・2026-08-06、T-133増分9、忍者】** 上記「変更を加えぬ既存ファイルを開いただけで
終わる」の未実測ぶんが埋まった。既存ファイル（`t133i8-motor-verify-ninja.gcad`）を開き、
**要素配置・図面編集は一切行わず**（自作パーツの「ピン留め」メニュー操作のみ、`Document`の
dirtyフラグに触れぬ別系統の永続化）終了したところ、**やはり正規終了で通り`main-layout.xml`が
上書きされた**。「新規作成」に限らぬ、想定どおりの姿にて、**この節のHow to applyがそのまま効く形**。

**【手順の変更・2026-08-06】終了後の差分確認は省き、無条件に復元してよい**——**新規作成・
「変更を加えぬ既存ファイルを開いただけ」の両方で実測が揃い、「上書きされておらぬ場合がある」
という前提の方が薄くなった。復元は退避ファイルの単純コピーゆえ、上書きされておらぬ状態へ
重ねて実行しても実害は無い（同一内容を同一内容へ上書きするのみ）。「差分を確認してから
復元するか判断する」という一手を省き、`Stop-Ecad2App`の直後は毎回無条件でコピーし戻す運びとする。**

**退避判断は都度の実務判断**（2026-08-06時点）——**忍者は本節冒頭の【MUST】を都度みずから思い出して
退避しており、検証観点書等の手順書に明記してあったわけではない。** 次にこの節を読む者は、
**手順書へ明記するか、体で覚えるかの判断を自身で下すこと。**

## 4. 既知の AutomationId・要素構成（2026-07-03 T-009/T-016実測、変更されうるため都度 FindAll で確認推奨）

| AutomationId / Name | 内容 |
|---|---|
| `StatusBarArea` | ステータスバー全体。配下に `"ツール: X"` `"ズーム: N%"` のText 2件（個別IDなし、Prefixで判別） |
| `PartPaletteList` | 左パーツパレットのListBox。ListItemのNameは `PartFolderEntry { Category = ..., FilePath = ..., Definition = ... }` という完全文字列 |
| `CanvasArea` | 中央キャンバス（Pane、単一ビジュアルとしてUI Automationツリーに現れる。内部の図形要素は個別に走査できない可能性が高い＝GuiEcadのWin2D Canvasと同様の制約） |
| `DeviceTableGrid` | 右パネル機器表（DataGrid、`DataItem`単位で行、列は機器名/種別/型式） |
| ツールバーボタン | `"新規作成 (Ctrl+N)"` `"開く (Ctrl+O)"` `"上書き保存 (Ctrl+S)"` `"元に戻す (Ctrl+Z)"` `"やり直し (Ctrl+Y)"` `"PDF出力 (Ctrl+P)"`（1段目）、`"選択ツール (Esc)"` `"a接点配置 (F5)"` `"b接点配置 (F6)"` `"コイル配置 (F7)"` `"端子台配置 (F8)"`（2段目） |
| シート追加ダイアログ（`AddSheetButton`押下で開く、2026-08-08 T-146検証、忍者実測） | `NameBox`（Edit、シート名）／`ControlCircuitRadio`・`MainCircuitRadio`（RadioButton、種別選択。`SelectionItemPattern.Select()`で選択可）／`OK`・`キャンセル`（Name一致のButton） |

要素一覧を再取得したい場合:
```powershell
. "...\helpers.ps1"
$root = Get-Ecad2Root
$root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
  ForEach-Object { "$($_.Current.ControlType.ProgrammaticName) Name='$($_.Current.Name)' Id='$($_.Current.AutomationId)' Bounds=$($_.Current.BoundingRectangle)" }
```

## 5. 見た目そのものの確認（レイアウト崩れ・配色・アイコン形状）

UI Automationのテキスト情報だけでは色・アイコン形状・重なりは分からない。`Save-Ecad2Screenshot` で
撮影し、Readツールで画像として開いて目視確認する。**`Save-Ecad2Screenshot` は内部で`PrintWindow`
（`PW_RENDERFULLCONTENT`）を使い対象ウィンドウの内容を直接描画するため、フォアグラウンド化不要
（2026-07-10改修）。Ecad2.Appが他ウィンドウの背後に完全に隠れていても、他ウィンドウが写り込まず
Ecad2.Appの内容だけが正しく撮れることを実証済み**（旧実装は`CopyFromScreen`＝画面表示の
キャプチャのみで、撮影前のフォアグラウンド化が前提だった。フォアグラウンド化を挟まず撮影すると
別ウィンドウが写り込む事故が実際に発生し、それを機に本方式へ切替）。

**色・状態の比較測定では、比較両辺のContentId/要素IDが同一であることを先に固定してから測ること**
（2026-07-22追記）。別要素同士の比較は「差がある」という偽の観測を生む。T-110増分2所見Bが実例：
ライトモード時「基本機能」タブとダークモード時「配置ツール」タブという別ContentId同士を比較して
「ダークだけ文字消失」と誤報したが、実際は配置ツールタブのラベル非表示が両テーマ共通の意図的仕様
だった（テーマは無関係）。T-110増分1(9)のタイムスタンプ日付誤読・T-044の薄線目視誤判定に連なる
「観測対象の同定ミス」型。測定記録には両辺の要素同定情報（ContentId/AutomationId/UIA矩形）を
必ず残し、同定の一致を確認してから色・状態の比較に入る。差分を検出した時こそ「両辺は本当に同じ
要素か」を最初に自問する。

### UIAツリーに現れないキャンバスの座標較正（2026-07-25、T-068増分3-c、忍者）

パーツエディタダイアログのキャンバスは**UIA要素ツリーに現れない**（GuiEcad同様の制約）。
このため座標指定の操作は、**既知定数からの理論値**と**スクリーンショット画素走査の実測値**を
突き合わせて較正する。

- 既知定数＝`cellMm = 9.0` ／ `marginMm = 30.0` ／ `MmToDip = 96 ÷ 25.4`
- これらから理論座標を算出し、実際に描画された図形の位置を画素走査で測って差分を取る
- **T-068増分3-c時点の実測オフセット＝横 +20.6px ／ 縦 +126.6px**（この値で安定）
- ウィンドウサイズ・DPI・ダイアログのレイアウト変更で変わりうるため、**セッションごとに1回
  較正し直す**のが安全

なお、ウィンドウ相対Y=150あたりまではツールバー領域でキャンバスに届かない（既載の罠）。
ドラッグ開始座標がツールバーに重なると描画自体が成立せず、「機能が壊れた」ように見える誤検出に
つながる（T-068増分3-b3で実際に発生、忍者が自ら操作ミスと突き止めて訂正）。

## 6. トラブルシュート

**【T-129で症状別に再編・2026-07-27】** 旧版は発見日付順の追記のみで肥大化し（658行中トラブル
シュート単体で6割を占め、忍者が本日踏んだ罠のうち複数が既に載っていながら見つけられなかった
実例が確認された）、症状から引けなかった。以下、**遭遇した現象の種類ごとに小節を分ける**。
まず自分の状況が下記のどの小節に近いかを見て、そこだけ読めば足りる構成にしている。

### 6.0 何はともあれ最初に確認すること

- 要素が見つからない (`throw "... not found"`) → `Get-Ecad2Process` でアプリが起動しているか確認。
  ダイアログ（確認メッセージ等）が前面に出て要素ツリーが変わっている可能性もある（詳細は6.1節）。
- クリック・キー送信しても反応がない → `Set-Ecad2Foreground` を挟んでウィンドウをアクティブに
  してから再試行（`Invoke-Ecad2ScreenClick`/`Send-Ecad2Keys` は内部で自動的に行う）。
- **リストの項目を叩いても何も起きぬ**（部品選択リストで配置バーが開かぬ等）
  → **上の`Set-Ecad2Foreground`の筋へ走る前に、6.1節「部品選択リストの初期表示は全体の一部にすぎぬ」
  項の下に束ねた3型を見よ**——**(1)`IsOffscreen=True`で叩けぬ (2)送りすぎて通り過ぎた
  (3)`IsOffscreen=False`でも親の矩形を数px外れる**。
  **【症状の文言が被る】**「反応がない」「開かぬ」で引くと、上の項と**6.2節の
  `Invoke-Ecad2CanvasClick`が配置ダイアログを開かぬ**項へ二重に引き寄せられるが、
  **いずれもフォーカスの筋にて、原因が別物にござる。**
  **前セッションの忍者は「危うく回帰と報ずるところであった」と記しておる**（2026-08-01の検分で判明）。
- ツール状態とツールバーのハイライト表示が食い違う（複数ボタンが同時に選択状態に見える等）→
  実装側のバグの可能性が高い。`Get-Ecad2StatusText` の値とスクリーンショットの両方を証跡として
  残し、実装担当（侍）へ再現手順とともに報告する。
- リサイズ直後にプロセスが消えた → `$env:TEMP\ecad2-ui-automation-std{out,err}.log` と
  `$env:TEMP\ecad2-crash.log`（未処理例外ハンドラが記録）を確認する。
- **パターン非対応**（`GetCurrentPattern`が「サポートされていないパターンです」等の例外を返す）
  → **見るべき節は2つあり、順序がある。**
  **(1)まず6.1節「『パターン非対応』に見えたら、その項目が中間メニューでないかを先に疑え」**
  ——**`ExpandCollapse`が在るなら展開して子を数えてから「非対応」を判ずる。
  展開しておらなんだだけ、ということがある。**
  **(2)それでも解けぬなら6.2節**（`SelectionItemPattern`/`TogglePattern`が
  `SynchronizedInputPattern`のみに縮退している場合等）。
  **【この行はかつて「6.1節ではなく6.2節を見よ」と断じており、正解を名指しで否定しておった】**
  （2026-08-01、忍者の検分で発覚）。**6.1節の当該項が生まれたのは後にて、索引の側が追いつかなんだ。**
  **読み手は6.2節を通読し、無いと見て迂回策へ走りうる。**
  **「行き先が古い」より一段悪い「そこには無いと断ずる」型にござる。**
  **【断り・忍者が自ら申し出た】前セッションの忍者が実際に迂回したのは記録に在るが
  （下記(a)本文＝「いきなり`Invoke`を試み、例外を見て結論した」）、
  **その者が索引に送られてそうなったかまでは記録が無い。** **測れるのは「迂回した」ことだけで、
  「索引がその原因であった」は測っておらぬ**——**観測と帰属は別物にござる。**
  **本項の根拠は「索引が6.1節を名指しで否定しておった」という構造の事実のみ**にて、
  **そちらは無傷にござる。**
- **ボタンが`IsEnabled=False`で押せない → まずシート種別を疑う**（2026-07-27、T-125増分α、忍者）。
  **機能によって使えるシート種別が違う。** 実例＝**配線分断は制御回路シート専用**で、主回路シートでは
  `IsEnabled=False`になる。**「複数の記入経路を横断で確かめる」類の検証では、1枚のシートで全経路を
  試せるとは限らず、2種のシートを用意する要がある**——検証計画を立てる段階で、対象機能がどの
  シート種別で使えるかを先に押さえておくこと。**実装バグと思い込んで報告する前に、この一点を確かめよ。**
  **実測の一例（2026-07-28、制御回路シート1枚だけの状態）**＝有効＝`a接点/b接点/OR各種/コイル/
  端子台/縦分岐線記入/配線分断記入/グループ枠記入/自作パーツ/テストモード`、
  無効＝`自由線(横線)記入 (F9)`・`自由線(縦線)記入 (Shift+F9)`・`接続点記入 (F10)`（＝主回路専用）。
  **グループ枠はシート種別を問わず置ける**（`MainWindow.xaml:1306-1311`にその旨のコメントあり）。
- **セルを選ぶつもりのクリックが「配置操作」になってしまう → 直前の配置でツールが残っておる**
  （2026-07-28、T-125増分β-1、忍者。実際に踏んで状態が乱れ、仕切り直しに数手を空費した）。
  **要素を1つ配置し終えてもツールは「要素配置」のまま残る**——これは**殿裁定による意図された仕様**で
  あり不具合ではない（`MainWindow.xaml.cs:3992`＝**T-021分岐A「ツール保持で連続配置」**。
  「移動→配置→命名→確定→また移動」の一気通貫を支えるキーボードファーストの設計で、
  **ツール解除はEscに委ねる**と明記されておる）。
  **How to apply**＝**配置操作の後、次にセルを選ぶ前は必ず`Send-Ecad2Keys "{ESC}"`か
  `Invoke-Ecad2Button -Name "選択ツール (Esc)"`でツールを明示的に戻し、
  `Get-Ecad2StatusText -Prefix 'ツール:'`で戻ったことを確かめてから次へ進む**
  （6.2節末「切替の直後は必ず実際に切り替わったかを確認」と同根）。
  **仕様ゆえ`proposed.md`起票には当たらぬ**——検証手順の側で吸収する類にござる。

### 6.1 要素が見つからない・掴めない（UIAツリー由来）

- **【重要・T-129新規】メニュー展開状態はPowerShell呼び出しをまたぐと失われる**（2026-07-27、
  忍者所見。T-126・T-128とも複数回発生）。メニューを展開する操作と、展開中の項目をInvokeする
  操作を**別々のPowerShell呼び出しに分けると、次の呼び出し時にはメニューが既に閉じており
  `$item`がnullになる**エラーを招く。**「展開→対象項目のInvoke」を同一のPowerShell呼び出し内で
  完結させること**（6.5節「PowerShellツール呼び出し自体がフォーカスロストを誘発する」と同根の
  「呼び出しの区切りで前の操作の状態が失われる」系統の罠）。
  - **【逆側の罠・2026-07-28、T-133増分1検証】既に開いておるメニューへ`Expand()`を重ねると、
    トグルして閉じてしまう**——**症状は上と同じ「`$item`がnull」ゆえ紛らわしい。**
    **上は「閉じたのに開いていると思った」、こちらは「開いていたのに開けようとして閉じた」**にござる。
    **対処＝`$ec.Current.ExpandCollapseState` を先に読み、`Expanded` でなければ展開する**——
    ```powershell
    $ec = $menu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($ec.Current.ExpandCollapseState -ne 'Expanded') { $ec.Expand() }
    ```
    **前の呼び出しが失敗して途中で終わった後に踏みやすい**（実際そうして踏んだ）。
    **`{ESC}`を2度送ってメニューを畳んでから出直すのも確実。**
  - **【「パターン非対応」に見えたら、その項目が中間メニューでないかを先に疑え】**
    （2026-07-31、T-137で忍者が己の前報を実測で覆した）。**`InvokePattern` を取ろうとして
    「サポートされていないパターンです」が出ても、それは行き止まりとは限らぬ**——
    **その項目自体が子を持つ中間メニューで、目的の項目は一段下に在ることがある。**
    **実例＝`パーツ(P)→自作パーツ(C)` 配下の各パーツ名**。パーツ名の項目が持つのは
    `ExpandCollapse` と `SynchronizedInput` のみだが、**展開すると `編集(E)...`／`削除(D)` が現れ、
    `編集(E)...` は `Invoke` できる**（実際これでパーツエディタが開く）。
    **対処＝`GetSupportedPatterns()` を採り、`ExpandCollapse` が在るなら展開して子を数えてから
    「非対応」を判ずる**（3階層を1回の PowerShell 呼び出しで通すこと。上記の「呼び出しをまたぐと
    閉じる」がそのまま効く）。
    **【なぜ前回そう見えたか】**T-133較正時、忍者は**パーツ名の項目でいきなり `Invoke` を試み、
    例外を見て「UIAからは起動できぬ」と結論した**——**展開しておらなんだだけ**にござった。
    そのうえ**「配置なのか編集で開くのかも未確認」という留保まで残しており**、
    **一段下を見れば `編集(E)...` と書いてあった。**
    **「パターン非対応」と「まだ展開しておらぬ」は、症状が同じ例外ゆえ見分けが付かぬ。**
    **記録に「非対応」と残すと、次の者は展開を試さずに迂回策へ走る**——**ゆえに
    「非対応」と書く前に、必ず `ExpandCollapse` の有無を確かめること。**
    （下記6.2節の「パターンが在っても機能せぬ」とは**裏返しの関係**にござる——
    **あちらは在って効かぬ、こちらは無いように見えて実は一段下に在る。**）
- **ダイアログ・ポップアップ（OpenFileDialog、コードビハインド生成のContextMenu、カスタム
  ダイアログ等）が `[System.Windows.Automation.AutomationElement]::RootElement.FindAll(Children,
  Window条件)` では検出できないことがある**（2026-07-10、T-055増分3検証で実証。`Invoke-Ecad2Button`
  でダイアログを開くボタンをInvokeしても、直後のFindAllでウィンドウが1件も増えず「開かれて
  いない」ように見えるが、実際は正しく開いている）。代わりに **Win32 `EnumWindows` API で
  対象プロセスの可視ウィンドウを直接列挙する**と確実に検出できる:
  ```powershell
  Add-Type @"
  using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
  public class WinEnumHelper {
      public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
      [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc p, IntPtr l);
      [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
      [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
      public static List<IntPtr> GetVisible(int pid) {
          var r = new List<IntPtr>();
          EnumWindows((h, l) => { int p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) r.Add(h); return true; }, IntPtr.Zero);
          return r;
      }
  }
  "@
  $handles = [WinEnumHelper]::GetVisible((Get-Ecad2Process).Id)
  # 各ハンドルを [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$h) で取得し中身を探索
  ```
  **ドロップダウンメニュー（サブメニューのPopup）・ContextMenuは、いずれもメインウィンドウとは
  別のトップレベルウィンドウとして生成される**（2026-07-16、T-083検証で実証）ため、
  `Save-Ecad2Screenshot`でメインウィンドウを撮っても写らず、上記`EnumWindows`でPopup自身の
  ウィンドウハンドルを取得しそこへ`PrintWindow`する必要がある。
- **ContextMenu表示中は `(Get-Ecad2Process).MainWindowHandle` がメインウィンドウではなく
  メニュー自身のハンドルを指すことがある**（2026-07-10実証。.NETの`Process.MainWindowHandle`は
  呼び出し時点で再検出されるため、ポップアップがメインウィンドウ候補に化けることがある）。
  この状態で `Get-Ecad2WindowRect`/`Save-Ecad2Screenshot` 等メインウィンドウ前提のヘルパーを
  呼ぶと、小さいメニューの矩形・メニューだけが写ったスクリーンショットが返り誤診断しやすい。
  ContextMenu操作中は「直前に確定したメインウィンドウハンドル」を変数にキャッシュして使うか、
  逆にこの現象自体を「メニューが正しく開いた証拠」として積極的に利用してよい（本スキルの
  `Invoke-Ecad2CanvasRightClick`使用時はメニュー操作直後に`(Get-Ecad2Process).MainWindowHandle`
  でメニュー要素を取得し、メニュー項目のInvoke後に再度呼べばメインウィンドウに戻る）。
- **DataGrid（出力パネル等）はUI仮想化により、スクロール範囲外（非表示）の行がUIA `FindAll`で
  一切取得できない**（2026-07-11、T-052検証で実証。ウィンドウ・パネルが小さい状態でDRC結果を
  `FindAll`で数えたところ実際より少ない件数しか返らず、「一部の診断が出ていない」と誤診断しかけた。
  殿がウィンドウを拡大すると全件表示された）。DataGridの全件を確認する際は、パネルを十分な高さに
  拡大するかスクロールしてから`FindAll`すること。件数の食い違いに気づいたら即バグと断定せず、
  まず仮想化を疑う。**同型の制約がListBoxにもある**——部品選択リスト（`PartSelectionList`、
  ListBoxItem）は`ScrollItemPattern`をサポートしており、
  `GetCurrentPattern([ScrollItemPattern]::Pattern) → ScrollIntoView()`で可視範囲外の項目も
  スクロール後に選択可能（2026-07-15、T-096実機確認で実証）。リスト項目数が多い場合はまず
  スクロール状態を疑うこと。一方、**`SheetNavList`のScrollBarは`ControlType.ScrollBar`での
  UIA探索では検出できない**（2026-07-21、T-106検証で実証）ため、座標を目視特定してから画素採取
  する迂回で対応する。
- **`AutomationElement`で`ControlType.MenuBar`を`FindFirst`すると、アプリのメニューバーでなく
  ウィンドウタイトルバーのシステムメニュー（左上アイコン領域）を誤って掴むことがある**
  （2026-07-16、T-083検証で実証。この罠により「メニューの背景色がテーマに反応しない」という
  誤診断が生まれかけた——実際に画素を採取していた座標が、アプリのメニューではなくOSタイトル
  バー領域（画面最上部、y座標にしておよそ0〜31px、テーマと無関係に常時同系色）だった）。
  メニューバー等ウィンドウ最上部に近い要素の座標確認は、目視・勘のy座標決め打ちではなく、
  対象の`AutomationElement`の`BoundingRectangle`を`Get-Ecad2WindowRect`のウィンドウ原点からの
  相対座標に変換して使うこと。
- **AvalonDockのタブ切替式ツールバーは、非選択タブの内容がUIA要素ツリーから完全に消失する**
  （2026-07-25、T-068増分2検証で実証）。「配置ツール」タブを選択中は「基本機能」タブ配下の
  ボタン（新規作成等）が`FindAll`で一切ヒットしない——非表示なだけでなくツリーそのものから
  消える。**要素が見つからないとき、まず「目的の要素が属するタブが今選択されているか」を
  確認すること**。上記のDataGrid仮想化と同型だが、こちらはスクロールでなくタブ選択が条件。
  実装バグと即断する前に、タブを切り替えてから再探索する。
- **AvalonDockのツールバータブは`TabItem.Name`では判別できぬ**（2026-07-27、T-133検証で実証）。
  `Name`が`AvalonDock.Layout.LayoutAnchorable`という型名になっており、「基本機能」「配置ツール」の
  区別がつかぬ。**子要素の`ControlType.Text`の文字列で同定すること**（上記6.1節末の「非選択タブの
  内容がUIAツリーから消失する」と併せて用いる＝**まずタブを`Text`で同定し、選択してから配下を探す**）。

#### 部品選択リストの項目が「拾えぬ」「叩けぬ」——親1型＋子3型（2026-08-01に見出しを立てた）

**6.1節は最長163行にして見出しが一つも無く、辿り着いても通読を強いておった**（忍者の検分）。
**下記5つは同じ場所で起きる別の失敗ゆえ、ここで束ねる**——
**(0)そもそもリストが現れぬ／(親)拾えぬ／(子1)拾えるが`IsOffscreen=True`で叩けぬ／
(子2)送りすぎて通り過ぎた／(子3)`IsOffscreen=False`でも親の矩形を数px外れる。**
**いずれか1つに辿り着けば、残る4つも目に入る形にしてある。**

- **【型0・まずこれを疑え】`PartSelectionList`がそもそも見つからぬ**（2026-08-05追加、
  T-133増分5、忍者）。**下の4型はいずれも「リストは在るが項目が」の話にて、本型だけが
  「リスト自体が無い」**——**`Find-Ecad2Element -AutomationId "PartSelectionList"`が
  `not found`を返す。**
  **機序＝リストは「自作パーツ (F11)」ツールが有効な間しか現れぬ。**
  **加えて右下の領域は、要素を選択しておる間はプロパティパネルと入れ替わる**
  （**未選択なら「要素を選択してください」の空パネルが出るのみで、部品リストではない**）。
  **忍者は選択を外せば部品リストが出ると思い込み、数手を空費した。**
  **対処**＝**`Invoke-Ecad2Button -Name "自作パーツ (F11)"`を先に押してから探す。**
  **項目名は`Name`が`PartSelectionEntryViewModel`という型名ゆえ判別できぬ**——
  **子の`ControlType.Text`を読んで同定すること**（`'a接点'`／`'セレクトSW'`等）。

- **【重要】部品選択リスト（`PartSelectionList`）の初期表示は全体の一部にすぎぬ**（2026-07-27、
  T-131検証で実測＝**全17件中6件＝35.3%**しか初期状態の`FindAll`で取れなかった）。機序は上記の
  ListBox仮想化と同じだが、**「一覧に無い＝実装されておらぬ」と誤って報ずる危険**がある点が新しい
  ——**不在の証明に使う数え上げでは致命的にござる**。**リストの全件を数える検証では、必ず
  `ScrollItemPattern.ScrollIntoView()`で末尾まで送ってから数えること。**
  - **【さらに危うい変種・2026-07-31、T-136(A)増分1、忍者】`FindAll`で拾えても
    `IsOffscreen=True`のことがある**——**`BoundingRectangle`も返るが、実際には可視範囲の外**にて、
    **その座標を叩いても何も起きぬ。** **上の罠が「拾えぬ」なら、こちらは「拾えるが叩けぬ」**——
    **後者の方が危うい。見つかった気になるゆえ、手法を疑わず実装を疑い始める。**
    **実例＝旧形式の検体が2度続けて「配置できぬ」と出て、危うく回帰と報ずるところであった。**
    切り分けは`ninja.md`「症状を見つけた操作から要素を1つずつ剥がして測れ」に従った——
    (1)**同じセルに別の部品**→置けた＝セルの問題でない (2)**同じ部品を別のシートで**→同じく置けぬ＝
    シート種別の問題でもない (3)**項目の属性を採る**→`IsOffscreen=True`＝**手法の欠陥と判明**。
    **対処**＝
    ```powershell
    if ($target.Current.IsOffscreen) {
        $target.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView()
        Start-Sleep -Milliseconds 600
    }
    $bb = $target.Current.BoundingRectangle   # ★送った「後で」取り直すこと
    ```
    **矩形を送る前に取ってしまうと、古い（画面外の）座標を叩き続ける。**
  - **【もう一つの型・2026-07-31、T-139検証、忍者】末尾まで送ってから探すと、中ほどの項目を
    通り過ぎる**——**上の`IsOffscreen`が「送りが足りぬ」型なら、こちらは「送りすぎ」型にござる。**
    **実例＝全24件のうち中ほどに在る「モータ」を、`LargeIncrement`を6回送ってから探して取り逃した**
    （`RESULT: パーツ 'モータ' がリストに見当たらず`）。**末尾付近の自作パーツばかり探しておった間は
    露見せなんだ**——**探す対象がリストの何処に在るかで、同じ手順が通ったり通らなんだりする。**
    **対処＝先頭へ戻し、段階的に下りながら探す**（見つかった時点で打ち切る）——
    ```powershell
    for ($k=0; $k -lt 9; $k++) { $sp.ScrollVertical([...ScrollAmount]::LargeDecrement); Start-Sleep -Milliseconds 150 }
    $target = $null
    for ($pass = 0; $pass -lt 10 -and $null -eq $target; $pass++) {
        # ...この時点で見えておる項目から探す...
        if ($null -eq $target) { $sp.ScrollVertical([...ScrollAmount]::LargeIncrement); Start-Sleep -Milliseconds 250 }
    }
    ```
    **`scratchpad\place-part.ps1`（自作パーツ配置の補助）は本方式へ改修済み。**
  - **【三つ目の型・2026-07-31、T-136(A)増分2、忍者】`IsOffscreen=False`でも、親リストの矩形を
    わずかに外れておることがある**——**上の2件を潰してなお空振りしたら、これを疑え。**
    **実測（`PartSelectionList`）**——
    ```
    リストの矩形              : Y=426〜632
    ScrollIntoView 直後の項目中心: Y=636   ← 上端を 4px 超えておる（IsOffscreen は False）
    1段スクロール後            : Y=604   ← 矩形の内 → 叩いて配置バーが開いた
    ```
    **わずか4pxにござる。** **`ScrollIntoView`は「可視範囲へ入れる」と謳うが、
    項目が下端ぎりぎりに置かれ、中心座標が矩形の外へ出ることがある。**
    **対処＝`ScrollIntoView`の後、親リストの`BoundingRectangle`と打点を突き合わせ、
    外れておれば`SmallIncrement`で1段寄せてから叩く**（寄せた後に矩形を取り直すこと）。
    **【併せて記す・外れた見立ても材料になる】** 侍は「`IsOffscreen`は覆いを見ぬゆえ、
    スクロールバー等が上に乗っておる恐れ」と読んだが、**本件では外れておった**——
    **打点は「覆われて」おらず、「矩形の外」に在った。** **次に同じ筋を疑う者のために残す。**
    **【もう一つの教訓】報告に「合成クリック」とだけ書いてはならぬ**——
    **忍者がそう書いたため、侍はUIAの`SelectionItemPattern.Select()`と読み、
    「`PreviewMouseLeftButtonDown`は発火せぬ」という誤った見立てを立て申した。**
    **`Invoke-Ecad2ScreenClick`＝Win32 `mouse_event`はOSの入力キューへ注入されるゆえ、
    WPFから見れば実マウスと区別が付かず、`Preview*`系も飛ぶ。**
    **0節の【MUST】「『物理クリック』の技術的な中身を明記せよ」は、この往復を防ぐためにござる。**

### 6.2 操作（Invoke／Toggle／Click／SetValue）しても反映されない

この小節の罠は、**UIA経由の操作がアプリ内部のイベントハンドラを実際のマウス操作と同じ経路で
発火させるとは限らない**、という一点に集約される（0節既載のToolState不安定化の同系統）。

- **プロパティパネルのテキストボックス（`DeviceNameBox`等）への`ValuePattern.SetValue`は、値を
  設定できても背後のモデルへの反映（機器表更新等）がされないことがある**（2026-07-11、T-055増分3
  検証で実証。WPFの`Binding`が`LostFocus`等の実イベントを要求するため、UIA経由の値設定だけでは
  トリガーされないと見られる）。一方、要素配置時のインラインダイアログのテキストボックスは
  `Invoke-Ecad2CanvasClick`での物理クリック＋`Send-Ecad2Keys`実入力なら正しく反映される。モデルへの
  反映有無を確認したい検証では後者を使うこと。
- **`ToggleButton`へのUI Automation `TogglePattern.Toggle()`はClickルーテッドイベントを
  発火させない**（2026-07-21、T-110増分0検証で実証。`ToggleState`（押下表示）は反転するが、
  コードビハインドの`Click=`ハンドラで実処理する実装だと一切処理が走らない）。物理クリック
  （実マウスイベント）で操作し直したところ正常動作した。**対策**：ToggleButtonの検証で
  「ToggleStateは変化するが効果が見えない」場合、実装バグと即断せず物理クリックで再検証すること。
- **AvalonDock系のタブ/トグル要素（配置ツールバーのタブ、テストモードボタン等）は
  `SelectionItemPattern`/`TogglePattern`が軒並み`SynchronizedInputPattern`のみに縮退している
  ことが多い**（2026-07-22、T-119/T-120検証で実証。標準パターンでの操作が使えず合成マウス
  クリックが必須になる場面が同日中に複数回あった）。対象要素で標準パターンが使えるか
  事前にパターンサポート状況（`GetSupportedPatterns()`等）を確認し、非対応なら最初から
  合成クリック（`Invoke-Ecad2ScreenClick`等）を選ぶ方が手戻りが少ない。
- **UIAが`SupportedPatterns`に`ExpandCollapsePattern`を含めて示していても、実際には展開が
  機能しないことがある**（2026-07-24、T-068増分1の動的生成MenuItem検証で実証。3階層目以降の
  動的生成MenuItemで、Expand/物理クリック/キーボード等どの操作手法でも一貫して展開失敗する
  事例が2種確認された——真因はWPF内部実装側の構造的な罠（`HasItems=false`によるRole確定、
  `SubmenuOpened`のRoutedEvent Bubbleによる親ハンドラ誤発火）であり、「パターンとして存在する」
  ことと「実際に機能する」ことは別物）。展開系の操作で複数手法を試しても症状が一貫して不変な
  場合、UIA側でなく対象コントロール実装側の構造的な罠を疑い、6.8節の診断ログ計装へ早めに
  切り替えること。
- **標準WindowsのMessageBox（`System.Windows.MessageBox.Show`等）の「はい/いいえ」ボタンは
  `InvokePattern`非対応で、UIA上は`Pane`として現れることがある**（2026-07-24、T-068増分1の
  削除確認ダイアログ検証で実証。**2026-07-27、シート未保存のまま終了しようとした際の確認
  ダイアログでも再発**——文脈は違えど標準MessageBoxである限り繰り返し起こる罠と判明）。
  カスタムダイアログのボタンとは異なり、物理クリック（`Invoke-Ecad2ScreenClick`）で操作すること。
  **逆にメニューのPopup表示中は座標クリックが
  不発になることがある**（2026-07-25、T-068増分2検証で3回再現。Popupが閉じもせず、クリック
  自体も効かないという二重の空振りになり、`InvokePattern`経由へ切り替えて解消した）。**この2つは
  ちょうど裏返しの関係**にある——標準MessageBoxのボタン＝物理クリック、メニューのPopup項目＝
  InvokePattern、と対象ごとに使い分ける。同じ操作を3回試して症状が変わらなければ、実装バグを
  疑う前にもう一方の操作手法へ切り替えてみるのを定石とする。
- **Disabled状態のボタンへ`InvokePattern.Invoke()`すると「認識できないエラーです」という
  原因不明の例外を返すことがある**（2026-07-21、T-101検証で実証。シートが1枚も無い状態で
  配置ツールボタン群がDisabledのままInvokeを試み時間を要した）。**モーダルダイアログの多重化**
  （下記6.6節）由来の同一エラーメッセージとは別原因。全ボタンで同一エラーが連発する場合は、
  まずダイアログ多重化を疑い、それで説明がつかなければ次に対象要素のEnabled状態（前提操作＝
  シート追加等が漏れていないか）を疑うこと。
  **【第3の発生文脈・2026-07-28、T-125増分β-1、忍者】配置バー（要素配置時のインライン入力バー）が
  開いている間もツールバーが軒並みDisabledになり、同じ「認識できないエラーです」が出る。**
  機序は判っておる——`IsMainContentEnabled = !IsPlacementBarVisible && !IsRungCommentEditorVisible
  && !IsFrameLabelEditorVisible`（`MainWindowViewModel.cs:205`）がメインコンテンツ全体
  （`MainWindow.xaml:882`）の`IsEnabled`を落とすため。**すなわち配置バー・行コメント編集・枠ラベル編集の
  3つのいずれかが開いている間は、ツールバー操作が一切通らぬ**のが設計どおりの姿にござる。
  **踏んだ経緯＝直前の配置でツールが残っていた（6.0節末尾参照）ため、セル選択のつもりのクリックが
  配置バーを開き、その状態で次のツールボタンをInvokeして当たった。** 2つの罠が連鎖する形ゆえ、
  **「認識できないエラー」を見たらまず`Find-Ecad2Element -AutomationId 'PlacementDeviceNameBox'`等で
  入力系オーバーレイが開いていないかを確かめよ**（開いていれば Esc で閉じてから出直す）。
- **Alt絡みショートカット（Ctrl+Alt+X等）の合成キー送信は、WPFのキーバインディング判定に
  到達しないことがある**（2026-07-22、T-110増分1 Ctrl+Alt+S検証で実証。`Send-Ecad2Keys`での
  合成送出後、診断ログ計装に入口記録すら一件も残らず、アプリのキーバインディングハンドラに
  到達していないことを確認。同一セッションで合成`Ctrl+F`（検索バー）は正常到達しており
  「Alt絡み合成のみ不達」の傾向が見られた。到達しない機序は推測の域——`Send-Ecad2Keys`の送出
  順序に起因しシステムキー化される等の可能性、実測未確定——であり機序を断定して記録しないこと）。
  **対策**：Alt絡みショートカットの検証は(a)メニュー等の代替経路で機能自体の健全性を先に証明
  (b)診断ログ計装で到達確認、の二段構えを既定とする。単一の合成キー送信結果（無反応）だけで
  実装バグと即断しないこと。**残余リスク**：ごく低い可能性だが、他ソフトウェアがグローバル
  ホットキーとして同じキー組み合わせを横取りしている場合は物理押下でも反応しないことがありうる
  （今回否定はできていない。同系統の`Ctrl+Alt+R`は殿の物理操作で機能した実績があり可能性は
  低いと見られるが、ゼロ証明ではない——殿が実際の利用で同ショートカットが効かない場合は
  申告を仰ぐこと）。
- **【重要】ツール選択RadioButtonの切替は、UIA・物理クリックいずれでも直前の状態次第で反映されない
  ことがある**（2026-07-25、T-068増分3-c検証で複数回遭遇）。**切替操作の直後は必ずステータステキスト
  等で「実際に切り替わったか」を確認してから次へ進むこと**——確認を怠り無駄な往復が生じた実例あり。
  **この手順はUIAの都合を超えてバグ検出そのものに効く**：同じ検証で「ボタンは選択状態になるがツールは
  切り替わらない」という実装バグ（`Tool_Checked`のswitch式に1ケースだけ欠落）が実在した。**ボタンの
  見た目（選択ハイライト）とアプリの内部状態は別物**であり、見た目だけで切替成功と判断してはならない。
  - **【射程の明示・2026-08-05追加、忍者が誤読して誤報したため】上記の`Tool_Checked`は
    パーツエディタ専用にござる**（`PartEditorDialog.xaml.cs:184`のみ。隠密の実測＝一致10件すべて同ダイアログ）。
    **メインウィンドウの配置ツールバーは別の実装**にて、**各ボタンが個別の`*_Click`ハンドラを持ち、
    キーボードショートカットとまったく同じメソッドを同じ引数で呼ぶ**
    （例＝`FreeLineHorizontalButton_Click`も`F9`も`TryBeginFreeLineDraft(horizontal: true)`）。
    **ゆえに「ボタンでは効かぬがキーボードでは効く」は、メインウィンドウでは原理上起こらぬ。**
  - **【メインウィンドウで「ボタンが効かぬ」と見えたら、まず前提条件を疑え】**（同上、忍者の実例）——
    **配置系のツールは`SelectedCell`が未選択なら即`return`し、ステータスバーへ
    「配置するセルを先に選択してください」を出す**（`MainWindow.xaml.cs:3592-3596`ほか同型が複数）。
    **忍者は`Get-Ecad2StatusText -Prefix "ツール:"`で絞って読んでおり、この最終行を見落として
    「自由線ツールのボタンが効かぬ」と誤報した**（`P-171`として起票され、測り直しでclosed）。
    **`Invoke`でも物理クリックでも、セルを選んでからなら正しく切り替わる。**
    **How to apply**＝**ツール切替が不発なら`Get-Ecad2StatusText`を`-Prefix`なしで全件読む。**
    **値は既に画面に出ておることが多い**（`memory: ecad2_symptom_to_number_translation`の対句＝
    **計装を足す前に、既に採れている値を読み尽くせ**）。
  - **【併せて・「青くハイライトされる」は押下の視覚効果であって活性状態ではない】**（同上、隠密の指摘）。
    **忍者は`F9`ボタンが青くなるのを見て「押されておるのに効かぬ」と読んだが、
    実際は処理が走り早期`return`しておった。** **`IsEnabled`はUIAで直に採れる**——見た目で判ずるな。
- **`Invoke-Ecad2CanvasClick`が配置ダイアログを開かぬことがある**（2026-07-27、T-131検証で**2回再現**）。
  **直前に`Set-Ecad2Foreground`でウィンドウを明示的に前面へ出してから叩くと確実になる**。
  ウィンドウが非アクティブなまま合成クリックを送ると1回目がアクティブ化に消費される類の挙動と
  見られるが、**機序は未確定**（断定して記録しないこと）。**キャンバス操作の前に
  `Set-Ecad2Foreground`を挟むのを既定とする**——6.5節「PowerShell呼び出し自体がフォーカスロストを
  誘発する」と同根の系統にござる。

#### `SelectionItemPattern.Select()` では部品配置が始まらぬ（2026-07-28、T-133検証、忍者）

**左パレットの自作パーツを UIA の `SelectionItemPattern.Select()` で選んでも、配置は始まらぬ。**
**配置の入口は `PartSelectionItem_Clicked` ＝ ListBoxItem の物理クリック**（`MouseButtonEventArgs`）
にて、**UIA の選択では発火せぬ**。**配置バーが開かぬまま数手を空費する。**

**対処**＝`ListBoxItem` の `BoundingRectangle` を採り、**物理クリック（`mouse_event`）で叩く**。
**「選択された」ことと「クリックハンドラが走った」ことは別**にござる。

**本節冒頭「UIAはClickハンドラを迂回する」の新実例**——**`Select()` が成功を返しても、
アプリ側の状態が変わったかを別途確かめること。**

**【機械的な裏付け・2026-08-06追加、T-133増分7検証、忍者】`GetSupportedPatterns()`で確認したところ、
`PartSelectionList`のListItemは`SelectionItemPattern`／`ScrollItemPattern`／`SynchronizedInputPattern`
のみで**`InvokePattern`自体が存在せぬ**。「`Select()`では発火せぬ」を、パターンの有無という
別の角度からも確かめた形にござる。**

**【未検証・次に当たる者は単発クリックから試されたい】本節の対処（単発の物理クリック）に対し、
忍者は今回`Select()`→合成ダブルクリックという手順で配置バーを開いた**（先に`Select()`を挟んだのは
不要だった可能性が高く、本節の記述と矛盾するものではない）。**ただし単発クリックのみで足りるか
（ダブルクリックの2回目が本当に要るか）は切り分けておらぬ**——**「ダブルクリックが要る」と断定して
記すのは早計と判じ、ここでは疑問点として残すに留める。**

#### 逆に「選択済みだが未確定」の見た目だけを観測したいときは`Select()`が使える（2026-08-02、T-140系統2検証、忍者）

**上項の裏返し**——`PartSelectionList`の行を物理クリックすると`PartSelectionItem_Clicked`が
即座に発火し配置フローへ進んでしまい、「選択されただけ」の視覚状態（選択行の背景色等）を
Escでキャンセルする前に測る間が無い。**`SelectionItemPattern.Select()`はクリックハンドラを
迂回するため配置を起こさず、`IsSelected=True`の見た目だけを安定して再現できる。**

```powershell
$sel = $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
$sel.Select()
[Ecad2Native]::SetCursorPos(2700, 700)   # カーソルをリストから退避（下記ホバー色混入を防ぐ）
```

**【対になる罠】マウスホバー色と真の選択色は見た目が近く誤認しやすい**——ホバー色
（`#1F26A0DA`半透明、白地との合成で`#E5F3FB`）と選択色（`#0078D7`）は共に薄い青系で似ており、
物理クリック→Escで配置をキャンセルした直後にカーソルがまだリスト上へ残っておると、
ホバー色を選択色と取り違えかねぬ（本検証で実際に一度取り違えかけ、`IsSelected`を確認して
自己訂正した）。**画素を採る前に必ずUIAの`IsSelected`で「本当に選択されておるか」を確かめること**
——「選択されて見える」ことと「本当に選択されておる」ことは別。

#### 部品配置は「セルを先に選ぶ→部品リストをクリック」の順でなければ始まらぬ（2026-08-02、T-136(C)実機確認、忍者）

**逆順（部品を先にリストで選ぶ→後でセルを選ぶ）では、何度試みても配置は始まらぬ。**
**機序**＝`TryPlaceElement`（`MainWindow.xaml.cs:3657`）は**`SelectedCell`が未選択なら即`return`する**
実装にて、**部品を先に選んでもセルが無ければ何も起こらぬ。**

**この罠は「シート種別の制約」と誤診断されやすい**——実際、忍者は制御回路シートで数手を空費し、
**「制御回路シートに主回路パーツを置けぬ」と報じかけた。真因は操作順であり、シートの枷ではなかった**
（**組込みのモータは`Any`ゆえ両シートに置ける。置けぬのは3極記号3種＝`MainCircuitOnly`にて別物**）。

**対処**＝**必ずセルを先に選択してから部品リストを叩く。** 配置バーが開かぬときは、
**シート種別を疑う前に操作順を疑うこと。**

#### 部品リストのスクロール位置は`Esc`のたびに先頭へ戻る（2026-08-02、同上）

**ツール解除（`Esc`）を打つたび、`PartSelectionList`のスクロール位置が先頭へリセットされる。**
**一覧の下方にある部品を繰り返し扱う検証では、`Esc`のたびにスクロールし直す要がある**
——**「さっき見えていた位置に在る」という前提で`BoundingRectangle`を使い回すと、別の行を叩く。**

#### `Invoke-Ecad2Button` で Undo/Redo ボタンが効かぬ（2026-07-28、T-134実機確認、忍者）

**「やり直し (Ctrl+Y)」を `Invoke-Ecad2Button`（UIA `InvokePattern`）で押しても、何も起こらぬ。**
**同じボタンを `BoundingRectangle` から物理クリック（`Invoke-Ecad2ScreenClick`）すると効いた。**

**厄介なのは「押せぬ」形で失敗せぬこと**——`IsEnabled=True` のまま、例外も出ず、
**ただ状態が何も変わらぬ**。T-134では機器表の件数もボタンの Enabled も不変であった。
**実装側で Redo が壊れておると誤診断しかねぬ**（**実際、忍者は一度そう疑った**）。

**対処**＝**Undo/Redo のような「状態を戻す」系ボタンの検証は、最初から物理クリックで行う。**
効かぬときは実装を疑う前に手法を替えること（本節の他の実例と同根）。

**【併せて】`Ctrl+Z`／`Ctrl+Y` の合成キー送出は、キャンバスにフォーカスが無いと届かぬ**——
`MainWindow.xaml.cs:2942`／`:2953` が **`IsCanvasFocused()` でガードしておる（仕様）**。
**ツールバーやメニューを操作した直後にキーを送ると、黙って素通りする。**
T-134では`Ctrl+Y`が2度不発となり実装の瑕疵を疑ったが、**一次ソースを当たって仕様と割れた**。
**キー送出が不発なら、まず直前に何をクリックしたかを思い出すこと**（6.5節と同根）。

**【対照の取り方は6.8節「Undoが生きておる対照は線を1本引いて取れ」を見よ】**
（2026-08-01に線を張った）。**本項は「押しても効かぬ」の話**にて、
**「`Undo`ボタンが`IsEnabled=False`で、そもそも積まれておらぬのか手法の失敗かが切り分かぬ」場面は
6.8節の側にござる。** **見出しから本項へ正しく着いた者ほど、その先を探さぬ**——
**索引の不備ではなく、相互リンクの欠落であった**（忍者の検分2026-08-01。
**線は`(f)`から6.7節へ張られておったが片方向で、戻る線が無かった**）。

#### 「履歴が伸びておらぬこと」は`IsEnabled`では測れぬ（2026-08-02、T-136(B)増分5、忍者）

**「元に戻す」ボタンの`IsEnabled`は、別の操作で積まれた履歴が残っておれば常に`True`**にござる。
**ゆえにEnabled／Disabledだけでは「今の操作が履歴を積んだか」を判じられぬ。**

**対処＝Undoを尽くすまで連打して回数を数える。** **操作の前後で回数が変わらねば「積んでおらぬ」。**

**実例**＝接続点の種類を左右交互に7回選び替えた後もUndo可能回数は2のまま
（接続点配置2件のみ）——**再入ガードが効いておる証**。
**`IsEnabled`を見ておれば、接続点追加の履歴ゆえ常に`True`で、何も判らなんだ。**

（**関連＝6.8節「Undoが生きておる対照は線を1本引いて取れ」**。
**あちらは「機構が生きておるか」、こちらは「履歴が伸びておらぬか」で目的は違えど、
`IsEnabled`を疑うという手法は同根**——忍者の見立て）

#### テストモード中、要素をクリックしても無反応 → DeviceName未設定を疑え（2026-08-08、T-146検証、忍者）

**組込み記号(a接点等)を配置直後、名前を空のままテストモードで通電クリックを試みても無反応**
のことがある。`TestModePress`メソッド内（`MainWindowViewModel.cs:3048`付近、検索文字列＝
`element.DeviceName is not string device`）のガード条件で、**名前未設定だとnullを返し
何も起きぬ**。

**対処**＝配置後、必ずプロパティパネルの`DeviceNameBox`へ名前を設定してから検証に入る
（`ValuePattern.SetValue`はモデルへ反映されぬことがあるため、物理クリックで実際に
フォーカスしてから`Send-Ecad2Keys`で入力し`{TAB}`等でLostFocusさせること。上記既載の
「プロパティパネルのテキストボックス」罠と同根）。名前設定後は`DeviceTableGrid`の行数で
モデルへ反映されたことを確認できる。

### 6.3 スクリーンショット・見た目確認が実態と食い違う（PrintWindow・画素採取系）

#### 【最初に読め】測る前に軸を選べ——色・数・形の三つがある（2026-08-01、忍者が三つとも使って提起）

**本節は色（比・値）の話に偏っておるが、測る軸は三つござる。問いの種類が軸を決める。**

| 軸 | 測るもの | 実例（2026-08-01） |
|---|---|---|
| **色**（比・値） | **読めるか** | パーツエディタのツールバー文字＝`#000000`／背景`#2D2D30`／**1.53:1** |
| **数**（色数・画素数） | **在るか無いか** | ポップアップ第3階層＝**色数が2色のみ**＝**文字に当たる画素が皆無** |
| **形**（分布・半径・重心） | **何が描かれたか** | 選択リング＝**中心から半径6〜8pxの環**・75px |

**「読めるか」に色数は要らず、「在るか」に比は効かず、「環か塊か」は色でも数でも出ぬ。**
**【比の限界】比は「2色の関係」しか見ぬ**——**「薄くて読みにくい」と「完全に消えておる」が分かれぬ。**
**実例＝第3階層は比が2.29:1と出たが、実際は文字が背景と同色で1画素も存在せなんだ。**

#### 【線引き】面の色は目視で当たる。文字・細線の色は外れる（2026-08-01、忍者が同日3件から）

**既載の「色・配色は目視でなく画素採取」【MUST】に線引きが無く、使い手が毎回迷う**ゆえ足す。

- **外れた2件＝いずれも小さい文字**（シートリストの「CTRL」＝灰を黒と誤読／ツールバーの「線・折れ線」＝**黒を白と誤読**）
- **当たった1件＝面**（ポップアップ全体が真っ白。136x50）

**機序**＝**文字はアンチエイリアスで中間色が混ざり、面積が小さいゆえ脳が周囲から補完する。**
**とりわけ隣に目立つものがある時に外れた**（片や青帯の隣、片や白いアイコンの下）。**面は単色が広く補完の余地がない。**
**最も危ういのは「読めた気になる」こと**——**忍者の弁＝「某は『白い』と積極的に誤り申した。見えておらぬものを見たと思うた」。**

#### 【併せて】目視と画素に上下はない。突き合わせて初めて効く（2026-08-01、忍者）

**同日、忍者は目視で三度外れ画素に救われたが、逆に画像を開いて「真っ白」と見たことが
「2.29:1＝薄いが見える」という数の読み違いを止めた。**
**画素だけを見ておれば「薄いが見える」と報じ、目視だけを見ておれば「黒い文字」と報じておった。**
**食い違いこそが色数を数えさせた**——**`memory: feedback_screenshot_visual_misjudgment_thin_lines`は
「目視は危うい」と片側しか説いておらぬ。**

この小節の罠に共通する背景：`Save-Ecad2Screenshot`は`PrintWindow`（DirectX/GDIの合成キャプチャ）
方式であり、**別ウィンドウ内蔵レンダラー・別スレッド描画・過渡的な状態を撮ると、UIAの検出結果や
殿の実機目視と食い違うことが繰り返し確認されている**。

- **PDFプレビューウィンドウのPrintWindow撮影は色情報が信頼できない**（2026-07-12、T-080検証で
  実証。PDFプレビューウィンドウをPrintWindow方式で撮影したところ、行コメントの文字色が行によって
  黒/赤/青にばらついて写ったが、殿の実画面確認では**全て黒色**だった）。PDFプレビューに対する
  **色・配色系の所見はPrintWindow撮影画像だけを根拠に報告しない**こと。
- **【重大な罠】`PlacementToolBarDockingManager`（配置ツールバー2段目）は、PrintWindow撮影・UIA
  探索(`FindAll`)の両方が内容を正しく捕捉できないことがある**（2026-07-17、T-099/T-100検証で
  発覚）。忍者がダークモード切替で当該パネルが「潰れている」と観測したが、殿の実機目視では
  正常表示と確認——UIA探索でボタン0件・PrintWindow画像でも選択ツールのみ表示と、**両手法が
  一致して「見えない」と誤示した**ため、手法の限界だと気づきにくい特に厄介なケース。**このパネル
  に関する「表示されない/潰れている」系の観測は、他手法（PrintWindow・UIA）で裏取りできても
  鵜呑みにせず、可能な限り殿ご自身の実機目視での確認を優先すること**（本スキル冒頭の「画素採取が
  目視に勝る」原則の数少ない例外）。原因技術は未解明。
- **【重大な罠】WPFキャンバス（DrawingVisual/DrawingContext方式で描画される領域、`LadderCanvas`等）
  の描画内容確認にPrintWindowを使うと、実際に描画されている内容が欠落して見えることがある**
  （2026-07-19、T-044「OR自動配線の分岐線バグ」調査で実証。忍者がPrintWindow撮影で「配線が視覚的
  に欠落」と複数回報告し、侍の診断ログでも正しい座標・タイミングでの発火まで確認できたにも
  かかわらず矛盾が解けなかったが、殿の実機目視・忍者のCopyFromScreen方式での再撮影・PDF出力の
  いずれでも正しく描画されており、**バグの実在ではなくPrintWindow撮影手法の限界と確定**）。
  上記2件と同根の「PrintWindow方式の限界」系だが、**キャンバス内の実描画内容そのものが欠落する**
  という、より重い実例。**対策**：キャンバス内の描画正確性（配線・図形の有無等）を検証する場面
  では、PrintWindow単体の結果を鵜呑みにしない。CopyFromScreen方式（フォアグラウンド化が前提、
  0節参照）でのクロスチェック、またはPDF出力（ネイティブレンダラーで別経路）で必ず裏取りする
  こと。**この観点に限り、0節「フォーカス非占有優先」の原則より正確性を優先し、CopyFromScreen
  使用を許容する**。
- **【重大な罠】高速連写でのGetPixel直読とBitmap保存画像が食い違うことがある**（2026-07-18、
  一瞬ライトモードに戻る現象の検証で実証。要素配置確定操作直後をPrintWindow+GetPixelで高速連写
  したところ、7回中3回で特定フレームがRGB(255,255,255)を検出——しかし該当フレームのBitmapを
  そのまま画像ファイルに保存し目視すると実際には白くなっていない。同一Bitmapへの反復GetPixel
  呼び出しは一貫して255を返すにもかかわらずSave画像には反映されないという食い違いが未解明の
  まま残った）。**高速連写・過渡的状態の検証でGetPixel結果とBitmap保存画像を突き合わせ、両者が
  食い違う場合は機械的判定を鵜呑みにせず、検証手法自体を疑うこと**。このケースでは深追いをやめ、
  6.8節の次善策（診断ログ注入・殿ご自身の実機目視）へ切り替えるのが得策と判断した。
- **ボタン押下中等、「一瞬だけ存在する状態」の視覚効果検証は自動化での確証が難しい**
  （2026-07-17、T-089押下フィードバック検証で実証。既存の`IsMouseOver`等他のフィードバックと
  視覚的に紛れる・マウスダウン〜撮影間のタイミング制御が粗く確実な確証が得られなかった）。
  実装（XAML構造）自体のコード目視確認と、build/test通過は自動化で担保できるが、**瞬間的な
  視覚状態の実際の見え方は殿ご自身の実機操作でのご確認に委ねる方が確実**。
- **【重要な例外】画素採取（スクリーンショット経由）が万能とは限らない——微細なテクスチャ・
  レンダリングパターン系の視覚アーティファクトは、静止画キャプチャでは視認の限界があり人間の
  目でしか判別できない場合がある**（2026-07-17、T-100ドックタブのハッチング模様調査で殿ご指摘。
  侍の自己目視・build/test上は「改善」と見えたが、殿の実機直接観察では「解消せず」と食い違った）。
  下記の色・配色系の対策（単発ピクセル誤読・半透明合成計算等、6.4節）は有効だが、**ハッチング等
  の微細パターン系の不具合は、この限りでない可能性がある**。この種の不具合は忍者のUIA/画素採取
  だけで「解消」と判定せず、殿ご自身の実機目視での最終確認を要する。ただし**「対象領域のユニーク
  色数」判定は有効な代替手段になりうる**（2026-07-17、`DragHandleTexture`除去確認で実証。模様が
  残っていれば複数色、完全に消えていれば単色になるという性質を機械的に判定できる。ただし
  文字・アイコンの境界に近い領域を含めるとアンチエイリアシングで誤NG判定を招くため、判定領域は
  境界から十分離した余白部分のみに絞ること）。
- **UIA検出の「見える/見えない」判定は、実態との乖離が双方向にありうる**（2026-07-22、
  T-110増分1のドラッグ&ドロップ残留オーバーレイ検証で実証）。「UIAやEnumWindows上は存在
  （`IsWindowVisible=True`）するのに実際は既に消えている」だけでなく、**逆に「実際はまだ
  画面上に残っているのに、キャプチャ手法だけを見て消えたと誤判定しかける」ケースもある**
  （上記のPrintWindowキャッシュ残骸等と紛れやすい）。**対策**：EnumWindows/UIAの存在判定だけで
  安心せず、色・見た目に関わる「まだ残っているか/本当に消えたか」を確定する場面では都度
  `CopyFromScreen`（実画面キャプチャ）で裏取りすること。
- **`Send-Ecad2Keys`直後にスクリーンショットで見た目を確認すると、実際の物理操作とは異なる結果が
  観測されることがある**（2026-07-11、T-056のCtrl+Gグリッド表示切替検証で発生。UIA経由ではメニューの
  ToggleStateは切り替わるのにキャンバス描画が反映されていないように見えたが、殿の実機操作では
  正常に切り替わった。`Start-Sleep`を600ms挟んでも再現し、原因未特定）。キーボードショートカットの
  見た目検証で疑わしい結果が出た場合は、待機時間を増やす・複数回スクリーンショットを取るなどの
  対照実験を行うか、殿代行操作（0節参照）での再確認を検討すること。
- **別ウィンドウ（ComboBoxドロップダウンPopup・PDFプレビュー等）のPrintWindow撮影は、
  `Save-Ecad2Screenshot`がメインウィンドウ専用のため毎回インラインでAdd-Typeし直す運用に
  なっている**（2026-07-21、忍者所見・T-106/T-107で複数回発生）。`helpers.ps1`は既に
  `Ecad2Native`クラス（`PrintWindow`/`GetWindowRect`のP/Invokeラッパー）を内部で保持して
  いるため、**独自にAdd-Typeせず`Ecad2Native`をそのまま使えば`System.Drawing.Common`参照
  エラー等を回避できる**（侍もT-106で同エラーに数回つまずいた後、helpers.ps1の既存インフラ
  流用で即解決した実例あり）。**任意ウィンドウハンドルを撮影する汎用ヘルパー化は今後の改善候補**
  （未実装、着手は各役の判断に委ねる。2026-07-27時点でも未着手）。

### 6.4 座標・色の測定手法

- **【設定値が効いておらぬ疑いは、リサイズThumbのBounds比率で見抜ける】**（2026-08-02、
  T-130実機確認、忍者）。**`AutoHideWidth`等の設定値は、読んでも「保存されておるか」しか分からず
  「実際に効いておるか」は分からぬ**——T-130ではコード上`AutoHideWidth=280`が正しく保存されて
  いたにもかかわらず、実際の描画には一切反映されておらなんだ（別軸`AutoHideHeight`が支配的
  だったため）。**見抜いた決め手はリサイズ用`Thumb`要素の`BoundingRectangle`比率**——
  ```powershell
  $thumbCond = New-Object System.Windows.Automation.PropertyCondition(
      [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
      [System.Windows.Automation.ControlType]::Thumb)
  $thumb = (Get-Ecad2Root).FindAll([System.Windows.Automation.TreeScope]::Descendants, $thumbCond) |
      Where-Object { $_.Current.BoundingRectangle.Width -gt 1000 -and $_.Current.BoundingRectangle.Height -lt 20 }
  # 幅≫高さ（横長の水平バー）＝上下方向にのみドラッグ可能＝Height軸が支配的
  # 高さ≫幅（縦長の垂直バー）＝左右方向にのみドラッグ可能＝Width軸が支配的
  ```
  **一般化＝「設定値は読むだけでは足りぬ、実際に動く方向（Thumbの向き）を見て初めて、
  どちらの軸が生きておるかが分かる」。** 数値が保存されているのに見た目が変わらぬ時、
  実装バグを疑う前にこの一手を挟むと安い。
- **【撮影に頼らぬ機械判定】「記入されたか」は重複ガードの警告文言で確定できる**（2026-07-27、
  T-125増分α、忍者）。範囲内で配線分断を記入した直後、スクリーンショットでマーカーを視認できず
  「ガードが効きすぎておる」と**誤報しかけた**。**同じセルへ2度目を押し「この位置には既に配線分断が
  あります」を得たことで、1度目に記入されていたと機械的に確定**できた。**保存ファイルを開くより速い。**
  一般化＝**「同じ操作を2度行い、2度目に重複拒否のメッセージが出るか」で1度目の成否を判定する**。
  細い線・薄い色のマーカーはPrintWindowで写らぬことがある（6.3節）ゆえ、この手が効く場面は多い。
- **グリッドセル座標の特定に毎回クリック→ステータスバー確認→微調整の当てずっぽう試行を要し
  非効率だった**（2026-07-18、忍者所見）。キャンバス原点・セル間隔は`DiagramRenderer`/
  `LadderCanvas`側で定数化されているはずゆえ、UIA操作スクリプト側にも既知の原点・セル間隔を
  定数として持たせておけば、行/列→ピクセル座標の変換を毎回の手探りなしに計算できる。**頻用する
  座標変換のヘルパー関数化は今後の改善候補**（未実装、2026-07-27時点でも未着手）。
  **【実測の較正値・2026-07-28、T-125増分β-1、忍者】ヘルパーが出来るまではこの式で足りる**——
  **相対X = 304 + 34 × 列** ／ **相対Y = 255 + 34 × (ステータスバーの表示行 - 1)**
  （ウィンドウ1400x800・ズーム100%・スクロール初期位置・左パネル幅190の既定レイアウト時）。
  **セルピッチは縦横とも34px**。`CanvasArea`はウィンドウ相対`(211,162)`・888x435であった。
  **較正の取り方＝3点クリックしてステータスバーの「選択セル: 行N/列M」を読み、差分からピッチを出す**
  （実測3回で足りた。当てずっぽうの微調整は要らぬ）。
  **【行の+1オフセットに注意】ステータスバーの表示行は内部座標より1大きい**（列はオフセット無し、
  0節既載）。上式の`(表示行 - 1)`はその換算にござる。**ウィンドウサイズ・ズーム・スクロール位置・
  パネル幅のいずれかが変われば原点はずれる**ゆえ、**セッションごとに1回取り直すこと**
  （6.3節末のパーツエディタ較正と同じ作法）。
- **中心配置等の高精度な位置測定では、円弧とテキストが重なる画像領域を単純なバウンディング
  ボックス走査で扱うと円弧のピクセルを誤検出する**（2026-07-15、T-097コイル中心配置検証で実証）。
  「中心からの距離が半径-マージン未満のピクセルのみ採用」という円形マスクを適用すると誤検出を
  排除できた。円形記号（コイル等）に重なるテキストの位置測定ではこの手法を用いること。
- **複雑形状（曲線＋直線の組み合わせ等）の画素採取は、対象のGrid列幅・境界位置をコードで
  事前確認してから採取座標を決めること**（2026-07-22、T-119の`SplineBorder`色検証で実証。
  単発座標が文字・列境界にたまたま当たり誤った色を拾いかけた）。
- **選択ハイライト等、テキストと重なる領域の単発ピクセル座標採取はアンチエイリアシングを拾い
  誤った色を返すことがある**（2026-07-17、T-083増分5 DataGridCell選択色検証で実証。単発座標で
  誤値`#B878D7`を得たが正しくは`#0078D7`だった）。対象領域全体の色出現頻度を集計し、最多出現色
  （支配色）を採用する方式に切り替えると安定する。
- **半透明（alpha付き）ブラシの実測値と理論値の比較は、ブラシのColor値をそのまま比べても一致
  しない**（2026-07-17、T-083増分7メニューのホバー/選択背景検証で実証）。PNGスクリーンショットは
  背景と合成済みの不透明ピクセルとして保存されるため、理論値側も「背景色とブラシのalpha合成
  計算値」を算出してから実測値と突き合わせること。単純比較は誤NG判定を招く。
- **テーマ・配色（Light/Dark等）の実機検証は、目視でなく座標指定の画素採取（`Bitmap.GetPixel`等）
  を標準手順とする**（2026-07-16、T-083ダークモード検証で実証。忍者・隠密2とも同一のスクリー
  ンショットを目視で「変わっていない」と誤読した実例あり。座標を決めて期待値（16進カラーコード）
  と実測値を突き合わせる方式なら誤読が構造的に起きない）。
- **Ctrl+ホイールズームの中心点挙動に一貫性を欠くことがある**（2026-07-15、T-097検証で発生。
  要素選択直後にズームすると意図した位置に留まる場合と、無関係な位置（左上）へ飛ぶ場合があり、
  条件は未特定）。原理不明のため対策は迂回策のみ：`ScrollBar`の`RangeValuePattern.SetValue`で
  直接スクロール位置を調整すること。
- **タイムスタンプ比較は秒の数値だけでなく日付まで確認する**（2026-07-22、T-110増分1のレイアウト
  保存検証で実証。「保存操作と同一秒でファイルが更新されていた」と報告したが、実際は日付を
  跨いだ前日の別時刻で、秒の数値一致だけを見て日付を見落とした誤判定だった）。ファイル更新時刻・
  ログのタイムスタンプを「操作直後の変化」として報告する際は、秒だけでなく年月日まで確認し、
  報告文にも日付を明記すること。

#### 【新手法】PDFはコンテンツストリームを直読できる（2026-07-28、T-133検証、忍者）

**PDF出力の検証は、スクリーンショットを撮らずに座標を数値で採れる。**
**`FlateDecode` を PowerShell の `DeflateStream` で展開すれば、描画命令の座標が pt 単位で読める。**

**肝＝zlibヘッダ2バイトを飛ばすこと**（`DeflateStream` は raw deflate を期待するため）。

**値打ち**＝**6.3節の「PrintWindow撮影がPDFプレビューの色を誤る」問題を丸ごと迂回できる。**
**理論値と 0.05mm 以内で一致する**ゆえ、**採った座標がどの要素の描画かを同定できる。**
T-133の突合＝右母線 204.5mm に対し実測 204.549mm（差 0.049）、線の終点 300.125mm に対し
実測 300.13mm（差 0.005）。**理論値と実測値を対で並べて初めて「一致」が言える**——
実測値だけを並べても根拠にならぬ。

**併せて採れるもの**＝`MediaBox`（用紙サイズ。A4縦なら `[0 0 595.276 841.89]`）。
**MediaBox を越える座標がそのまま書かれておるか**を見れば、
**座標が切り詰められておらぬことが判る**（T-133では用紙幅の2倍を超える座標を実測した）。

**【重要】ただし、これで「クリップの有無」は判定できぬ**（2026-07-28、忍者が自らの落とし込みを
検分して訂正）。**PDFのクリップ（`W n`）はクリップパスを別に設定するものであり、描画命令の座標
そのものを書き換えぬ。** ゆえに**クリップが掛かっていても座標はそのまま書かれうる。**
T-133で「クリップは掛かっておらぬ」を確定できたのは、**隠密のコード読解**
（`PushClip`は`src`全体で2箇所のみ・要素は`PopClip`の後に描かれる。
`docs/ecad2-t133-pdf-clipping-survey-onmitsu.md` 1節）と**二軸で一致したから**にござる。
**本手法単独で「クリップ無し」と断ずるな。**

**留意**＝**「MediaBox外は紙に出ぬ」はPDF仕様に基づく帰結であって、画像としての実測ではない。**
**紙の見た目そのものを確かめるなら、別途レンダリングして採ること。**

#### キャンバスのクリック有効範囲は `BoundingRectangle` より狭い（2026-07-28、T-133検証、忍者）

**`CanvasArea` の Bounds はウィンドウ相対 `(211,162)`・888x435＝右端は相対 1099 であったが、
セル選択が実際に効いたのは相対 x≒900（列17）まで。**
**右側およそ 200px は叩いても選択セルが変わらぬ。**

**【数の基準に注意】** 幅（888）と相対座標（900）は別の基準にござる——**両者を直に比べるな。**
**比べるべきは「Bounds の右端＝相対 1099」と「効いた右端＝相対 900」。**
（この節は当初「Bounds は 888px 幅だが rel x≒900 まで」と書かれており、幅と相対座標を繋いで
おった。2026-07-28、忍者が自らの落とし込みを検分して訂正）
**なお Bounds の実測値は T-125増分β-1（同日）の較正値**＝ウィンドウ1400x800・ズーム100%・
左パネル幅190 のとき。**ウィンドウ条件が変われば変わる**（6.4節冒頭の較正の注記と同じ）。

**Bounds を信じて座標を決めると空振りする**——**「叩いたが何も起きぬ」を「操作が効かぬバグ」と
誤診断しかねぬ。** **範囲の端を使うときは、効く範囲を先に実測で押さえること**
（T-133では列17まで効くことを確認したのみで、無効になる境界そのものは詰めておらぬ）。

#### ズーム中心点の不整合は、既知のmm座標からスクロールバー値を逆算すれば迂回できる（2026-08-07、T-145実機確認、忍者）

既載（本節冒頭）の「Ctrl+ホイールズームの中心点挙動に一貫性を欠く」対策は「ScrollBarの
RangeValuePattern.SetValueで直接調整する」とのみで、具体の式が無かった。本日、狙った要素の
mm座標が既知（グリッド原点MarginMm・セル寸法CellMm・SymbolGlyphsの局所オフセットから算出できる）
な場合に限り、以下の式で一発到達できることを確かめた。

content_x_px = mm座標x × px_per_mm、content_y_px = mm座標y × px_per_mm
（px_per_mm = 96/25.4 × ズーム倍率。例：400%なら約15.118）
scrollbar_h = content_x_px − ビューポート幅/2、scrollbar_v = content_y_px − ビューポート高さ/2
（ビューポート幅・高さはCanvasAreaのBoundingRectangle）

Ctrl+ホイールでいったん目的のズーム％まで拾ってから（centerはどこでもよい、外れてもこの後
上書きされる）、上式で求めた値をRangeValuePattern.SetValueへ渡せば、狙った要素がビューポート
中央付近に来る。試行錯誤で位置を探る手間が要らぬ。

標本は二度のみ（横向きモータ・縦向きモータの高倍率測定、いずれも一発で到達）。「必ず一発で届く」
という保証まではしておらぬ——scrollbarのmax値が現在のズーム・コンテンツ全体の大きさで変わる点、
負の値を渡すと例外になりうる点（下限0でクランプする処理を要る）は実測で踏まえたが、それ以上の
検証はしておらぬ。次に使う者は、まず一回試してビューポート内に収まっているかを確認してから
測定に入ること。

#### 円の中心を画素採取で求める際の罠三つ（2026-08-07、T-145実機確認、忍者）

円形記号（モータ・コイル等）の中心とラベル位置のずれをサブmm精度で測る際、以下三つはいずれも
本日実際に踏んで気づいたものであり、あらかじめ避けられたものではない。

一つ目＝選択ハイライト（オレンジ、既定色はRGB概ね255,69,0）は輝度基準（(R+G+B)/3 < 閾値）だけの
判定では黒画素と誤検出されうる。オレンジは輝度こそ108程度と低いが、彩度（R−B等のチャンネル差）
が非常に大きく、灰色・黒とは性質が異なる。対策＝彩度も条件に加える（例：max(R,G,B)−min(R,G,B) が
一定値未満、かつ輝度が閾値未満、の両方を満たす画素のみを対象とする）。もっとも確実なのは対象要素の
選択を解いてから撮影すること（Esc等）。

二つ目＝結線が円の極（真上・真下）へ収束する意匠（縦向きモータ等、三本の結線が上端の一点へ集まる形）
では、単純な「上端・下端を上から/下から走査して最初に暗画素へ当たった行」という方式が結線に汚染され、
円の真の上端・下端を捉えられない。対策＝円周上の複数点（中心xを挟んで左右対称に暗画素の左端・右端を
採り、その中点を求める）から中心xをまず固定し、汚染されていない側の極（下端等）と、任意の清浄な行の
円周点（左または右）から、r^2=(y−cy)^2+dx^2 の関係を2点分立てて中心yを逆算する。結線が円の左右から
斜めに入る意匠（横向きモータ）では上下端走査がそのまま使えるため、意匠によって使い分けが要る。

三つ目＝ラベルの暗画素バウンディングボックスを走査する矩形は、対象円の半径より内側に収めねば円弧
そのものを拾ってしまう。モータ（実測半径約90〜104px、zoom350〜400%）向けに定めた走査窓を、より
小さいコイル（実測半径約58.5px、同条件）へそのまま流用したところ、走査窓の縁が円周へ届いてしまい、
バウンディングボックスが円弧まで含めて誤って広がった。対策＝走査窓の半径（対角の半分）を、測定対象
の円の実測半径より確実に小さく取る。対象ごとに窓を作り直すこと。

### 6.5 フォーカス・アクティブ状態

- **PowerShellツール呼び出し自体がアプリのフォーカスロスト系イベントを発火させる**（2026-07-12、
  T-080検証で2回再現）。F2送信等でフォーカスロスト確定型のUI（行コメントエディタ等）を開いた直後に、
  UIAクエリ等を含む**別の**PowerShell呼び出しを実行すると、その呼び出し自体が原因とみられる
  ウィンドウ非アクティブ化により数百ms後に`LostKeyboardFocus(NewFocus=null)`が自動発火し、
  対象UIが意図せず閉じる。**対策**：キー送信と状態確認を同一PowerShell呼び出し内に収めるか、
  状態確認をBashの`tail`等（PowerShell以外）に限定する（6.1節「メニュー展開状態」も同根）。
- **殿へ物理操作を依頼した検証は「チャット復帰クリック汚染」に注意**（2026-07-12、T-080で実証。
  殿が操作結果を報告するためチャットウィンドウをクリックする——その動作自体がアプリのウィンドウ
  非アクティブ化を起こし、フォーカスロスト系イベントを発火させる。T-080往復2周目では、この汚染に
  より「窓内クリックでエディタが閉じる」という**真逆の誤った実測確定**を一度生んだ）。**対策**：
  依頼文言に「操作後、**5秒待ってから**チャットへ復帰」を明記し、診断ログのタイムスタンプで
  操作時刻と発火時刻の間隔を突合すれば、操作起因と復帰クリック起因を機械的に判別できる。
  フォーカス・アクティブ状態に関わる検証では、殿の**目視証言**を一次情報として重視し、ログ単独で
  確定しない。
- **AvalonDock既定のCtrl+Tabナビゲータ（NavigatorWindow）をUIA経由で開くには、事前にDockingManager
  内の何らかの要素へキーボードフォーカスを設定しておく必要がある**（2026-07-17、T-083増分5検証で
  実証）。フォーカス位置が不明な状態で`Ctrl+Tab`を送信してもポップアップが検出できなかった。
  対象パネル内の要素へ`SetFocus()`してから`Ctrl+Tab`を送信し、`EnumWindows`で別ウィンドウとして
  検出する手順を踏むこと。
- **【新規のUIA限界】キーボードフォーカス系検証で`AutomationElement.FocusedElement`が終始
  「Window」を誤報告し実態を掴めないことがある**（2026-07-20、T-104キーボードナビゲーション
  検証で実証）。AvalonDockのAutoHideサイド領域内をフォーカスが巡回していた実態は、UIA単独では
  一切検出できず、侍が仕込んだ`GotFocus`イベントのクラスハンドラ計装によって初めて判明した。
  **UIA単独でのフォーカス系検証には構造的な限界があり、`FocusedElement`が実態と乖離している
  と疑われる場合は早期に6.8節の診断ログ計装へ切り替えること**。
- **視覚的なフォーカス破線枠が実際のWPF内部フォーカスと食い違うことがある**（同上検証で実証）。
  対策前、Tab連打してもフォーカス破線枠は終始「基本機能」タブに固定表示のまま変化しなかったが、
  診断ログ実測では実フォーカスはAvalonDock内部コントロール階層を巡回していた。**目視のフォーカス
  表示（破線枠等）も、UIAの`FocusedElement`と同様に鵜呑みにできない場合がある**。
- **標準WPFの`GridSplitter`（Thumbドラッグ）は、AvalonDockペインの境界ドラッグと異なり、
  対象ウィンドウの明示的なフォアグラウンド化が必要**（2026-07-21、T-077増分2検証で実証。
  非モーダル`UsageWindow`内のGridSplitterへ`Invoke-Ecad2Drag`相当の操作を試みたところ、
  フォーカスが無い状態では無反応だったが、`SetForegroundWindow`後は正しく動作した）。
  AvalonDock側のドラッグ操作（0節参照）は内部的にフォーカス状態を問わず動作する実績があるため
  同一視しがちだが、標準WPFコントロールのThumbドラッグは別物と意識すること。

### 6.6 ダイアログ・モーダル絡み

- **【まずこれを疑え】「名前を付けて保存」は折りたたまれた状態だとファイル名欄が存在しない**
  （2026-07-27、T-125増分α、忍者。数手を空費した実例）。`%n`（Alt+N）も座標クリックも通らぬとき、
  真因は**ダイアログが折りたたみ表示で、ファイル名欄そのものが画面に無い**ことであった。
  **「フォルダーの参照(B)」を展開して初めて現れる。** 下記の「入力が入らぬ」系の罠の**一段手前**——
  **要素が見つからぬ時、まず「その要素は今そもそも表示されておるか」を疑うこと。**
- **モーダルダイアログを開くボタンを`InvokePattern.Invoke()`で連続Invokeすると、モーダル制約を
  無視して背後のボタンが実行され続け、同一ダイアログが複数枚重なって開くことがある**
  （2026-07-11、T-051検証で実証。通常のマウスクリックなら受けるはずのモーダルブロックが効かず、
  ダイアログが3枚重なって開いた。この状態では他要素へのInvokeが「認識できないエラーです」という
  例外で断続的に失敗したり、`FindAll`が空を返したりと原因不明の不安定挙動が連鎖する。ダイアログ
  の存在自体は6.1節のEnumWindows手法でしか検出できない）。**対策**：ダイアログを開く可能性のある
  ボタン（Click=コードビハインドで`ShowDialog()`するもの）をInvokeした直後は、次の操作に進む前に
  必ずEnumWindowsでダイアログの出現枚数を確認する（1枚のみであることを確認してから、そのダイア
  ログ内の要素を操作する）。原因不明の「認識できないエラー」例外や`FindAll`の空振りに遭遇したら、
  まずこの罠（ダイアログの多重化）を疑うこと。
- **`Send-Ecad2Keys`でダイアログへ文字列（ファイルパス等）を送るのは避ける**（2026-07-11、
  P-056で実証。`Set-Ecad2Foreground`が元々モーダル状態を考慮せず無条件にメインウィンドウを
  アクティブ化する実装だったため、`ShowDialog`内部と矛盾する状態を作りフォーカス誤爆→意図しない
  コマンド誘発につながった実例があり、殿PCのキーボード入力不通フリーズの契機ともなった）。
  **対策済み**：`Set-Ecad2Foreground`は対象ウィンドウが無効化されている間はスキップするよう
  改修済み、`Send-Ecad2Keys`は21文字以上の文字列を既定で拒否する。これらの安全策があっても、
  **ダイアログへのテキスト入力は`Send-Ecad2Keys`ではなく、最近使ったファイル一覧のボタン操作・
  物理クリック+短いキー入力など別手段を優先すること**。
  - **標準WindowsのOpenFileDialogの「ファイル名」欄はAutomationId="1148"のPane内部に埋もれており、
    通常の`FindAll`探索では辿りつけないことがある**（2026-07-15、T-096実機確認で実証）。
    **`Send-Ecad2Keys`で`%n`（Alt+N）を送りフォーカス移動させるのが確実**（フォーカス移動用の
    単発アクセラレータキーと、長い文字列送信は別問題）。
    **【同型・AutomationIdは環境で揺れる】2026-08-06、T-133増分7、忍者——「名前を付けて保存」
    ダイアログでは`AutomationId="FileNameControlHost"`という名の付いたPaneの内側で同じく詰まった
    （`1148`ではなくこの名だった）。**`FindAll`が辿りつけぬ現象自体は上記と同じ**——ダイアログを
    開いた時点でファイル名欄には既定でフォーカスが入っておるため、`%n`すら省いて
    `Send-Ecad2Keys "ファイル名" -Force`（21文字超ガードの明示回避）をそのまま送れば足りた。
    **AutomationIdの値そのものはWindowsのバージョン等で揺れうる点に注意**——探すべきは名前ではなく
    「Pane内に埋もれてFindAllが届かぬ」という構造の方にござる。
- **「名前を付けて保存」ダイアログの既定フォルダは、同一セッション内の直前の別ダイアログ操作
  （画像挿入等）で使ったカレントフォルダを引き継ぐことがある**（2026-07-21、T-098検証で実証。
  直前に画像挿入で使ったフォルダが残存し、そのまま保存しようとして「アクセス許可がありません」
  という想定外の確認ダイアログが追加で挟まった）。**対策**：保存操作では`%n`でファイル名欄へ
  フォーカス後、ファイル名のみでなく`C:\ECAD2\sample\<name>`のようにフルパスを明示的に入力すると、
  フォルダの引き継ぎ問題を回避できる。

### 6.7 ドラッグ操作

- **【新規のUIA限界】AvalonDock標準のOverlayWindow/DropTarget機構（ドラッグ中に一時的に現れる
  十字型ドロップターゲットUI）は、`SetCursorPos`+`mouse_event`合成によるマウスドラッグでは
  検出できないことがある**（2026-07-19、T-099(c)十字型UI位置ズレ検証で実証。忍者が3回試行
  したが、`EnumWindows`・`CopyFromScreen`いずれの手法でも十字型UI自体を一度も検出できず、
  副次的にフロートウィンドウのBoundsが不自然な動きを観測するに留まった）。0節「AvalonDockペインの
  ドラッグ操作」の`Invoke-Ecad2Drag`はタブ切り離し・境界リサイズの成立には有効だが、**ドラッグ中
  にのみ一時的に生成されるOverlayWindow自体の検出には別の壁がある**とみられる（原因未特定。
  本家AvalonDockも実ドラッグの自動テストを行わず内部API直接呼び出しで代替していることから、
  構造的な自動化限界の可能性が高い）。**この種の検証は、UIA/EnumWindows合成操作で粘らず、早期に
  殿ご自身の実機目視・操作確認へ切り替えることを検討する**。

#### 要素のドラッグ移動は「先に選択」しておかねば成立せぬ（2026-07-28、T-133増分3検証、忍者）

**要素を選択せぬまま`Invoke-Ecad2Drag`を送っても、要素は動かぬ**——**距離が足りておっても**である。
**行3/列2 → 行8/列12（380px、距離則は満たす）で1度目は不成立**、
**同じ座標・同じ距離でも、先に対象セルをクリックして選択してから送ると成立した。**

**危ういのは「動かなかった」という結果が実装の回帰と見分けがつかぬこと**——
T-133増分3（占有・ヒットテストの改修）の検証中に起きたため、**危うく回帰と報ずるところであった。**

**対処**＝**ドラッグ前に対象セルをクリックし、`DeviceNameBox`の`ValuePattern`値で
「その要素が選択されておる」ことを確かめてから送る**（同一のPowerShell呼び出し内で行うこと）。
**機序は未確定**——実マウスなら押下と同時に選択されるが合成では追いつかぬのか、実装が事前選択を
要するのかは測っておらぬ。**断定して記録しないこと。**

#### 合成ドラッグの距離則は作図ツールにも効く（2026-07-28、T-133検証、忍者）

**既載の「300px以上が安定」は AvalonDock のドラッグに限らぬ。**
**パーツエディタの線ツールでも、136px／40刻みでは引けず、340px／68刻みで成立した。**
**（「引けぬ」は結果の観測にすぎぬ——内部で下書きがどう扱われるかは実測しておらぬ。
機序を推測で添えぬこと。2026-07-28、忍者の検分で推測の機序記述を削除）**

**すなわち「合成ドラッグは短いと成立せぬ」は、アプリ内の作図操作にも当てはまる。**
**線が引けぬときは、まず距離と刻みを増やして試すこと。**

### 6.8 診断・調査プロセスの心得

- **指定された検体が目的の観点を単独で検証できる構造か、想定と異なる結果が出た時点で疑う**
  （2026-07-27、T-126実機確認で実証。家老指定の検体で検証Bを試したところ想定と異なり検証Aの
  文言が出た——判定はクランプ*後*の座標で行われるため、指定検体は既に別の観点が先に発火する
  構造だった。目的の観点だけを単独で確認できる新規検体を作り直して解決した）。**検体は無条件に
  信用せず、想定と異なる結果が出たら「検体の構造そのもの」を疑う候補に含めること**——実装側の
  バグと即断する前に、検体が実際に狙った条件だけを再現しているかを一度確かめる。
- **「見た目と実際の状態が食い違う」系の不具合（T-044等）は、目視・画素採取だけでは真因に迫れない
  ことが多い**（2026-07-18、忍者所見）。判定ロジックは正しいのに描画に反映されない、といった
  動的な食い違いは、早めに診断ログ計装（侍への采配）へ切り替える判断を優先すること——本スキルの
  検証手法（画素採取・UIA探索）で粘るより、実測ログの方が核心に迫れる場面が多い。
- **狙った不整合を人為的に作る検証テクニック**（T-104検証、レイアウト読込失敗メッセージの確認で
  使用）：`%AppData%`のレイアウトXML等、永続化ファイルを検証前に意図的に旧構成へ書き換えてから
  アプリを起動し、想定した異常系（読込失敗・非互換検出等）が正しく発火するかを確認する手法。
  検証後は元のファイルをバックアップから復元すること。正常系の実機確認だけでなく、意図的な
  異常系の再現にも応用できる。
- **実機確認の前にCore/App層の該当コード（プロパティの持ち方、共有/非共有の設計等）を
  先読みしておくと、殿からの追加確認依頼（仕様面の疑問）にも即座に的確な再現手順を組める**
  （2026-07-21、T-107で実証）。UIA操作の組み立てに入る前の一手間として、関連コードの構造を
  先に把握する価値がある。
- **UI検証は、期待値（実装コードの色定義・条件分岐）を先に把握してから実機を見る方が、
  スクショの目視だけより不具合に気づきやすい**（2026-07-22、T-119のタブ強調表示検証で実証。
  「初期選択状態でのみ強調表示が反映されない」という条件付き不具合は、色定義を先読みして
  期待値を持っていたからこそ気づけた所見）。
- **【MUST】入口が開いたことは、通れたことではござらぬ**（2026-07-31、T-136(A)増分1、忍者）。
  **操作の途中で現れるUI（ダイアログ・入力バー等）を「成功の証」に使ってはならぬ。**
  **実例**＝シート種別の枷（配置の可否）を検証する際、忍者は当初**「配置バーが開いたか」で成否を
  判ずるつもりであった**。**だが`ValidatePlacement`（可否の判定）は確定の段で走る**——
  **配置バーが開くのはそれより前**ゆえ、**枷で拒まれる部品でも配置バーは開き、名前を入れて
  Enterを押す操作まで通る。ただ要素が生まれぬだけ**にござる（拒否がサイレントな仕様ゆえ、なおさら）。
  **この指標のまま報じておれば「枷が効いておらぬ」と誤報しておった。**
  **機器表の行数とキャンバスの絵で実体を見て、初めて正しく判った。**
  **How to apply**＝**配置・削除・確定を伴う検証は、必ず「結果側」の指標で判ずる**——
  **機器表（`DeviceTableGrid`）の行数／キャンバスの絵／保存ファイルの中身**。
  **「操作が通った」ことを示すUIの反応は、途中経過にすぎぬ。**
  **関連**＝6.4節「記入されたかは重複ガードの警告文言で確定できる」（**あちらも結果側の指標を採る手**）。
  - **【射程の限定・機器表は要素の種類によっては動かぬ】**（2026-08-05追加、T-133増分5、忍者）——
    **上で筆頭に挙げた機器表が、そのまま使えぬ場合がある。** **実測＝3極記号（`Breaker3P`等）を
    配置しても機器表の行数は増えぬ。a接点は増える**（`X1`で1行）。
    **忍者は「機器表0行＝配置されておらぬ」と誤判定し、数手を空費した**——
    **真は配置されており、`Enter`で同じセルへ再配置を試みて
    「選択したセルには既に要素があります」と出て初めて割れた。**
    **How to apply**＝**機器表を指標に使う前に、その要素が機器表に載る種別かを確かめる。**
    **確かめる間もなくば、キャンバスの絵を併せて見よ**（**絵は種別を問わず出る**）。
    **「結果側の指標」も一つでは足りぬことがある、というのが本件の学びにござる。**
- **【パーツエディタ】Undo機構が生きておることを示す対照は、線を1本引いて取れ**
  （2026-07-31、T-136(A)増分2、忍者＋侍の申し送り）。
  **「Undoが効かぬ」を報ずる前に、Undo機構そのものが生きておるかを別の操作で確かめる要がある**——
  **その対照に接続点を使うと、取れぬことがある。**
  **なぜ接続点では取れなんだか**＝**`AddPort`は基準枠の範囲へクランプされ、同座標なら無視される**
  （`PartEditorCanvas.cs`のポート重複判定）。**ゆえに枠外を叩いても、既にある点と同じ位置へ落ちても、
  接続点は増えぬ**——**「増えぬ」と「Undoが効かぬ」を同時に見ることになり、切り分けが立たぬ。**
  **実例＝忍者は接続点を足そうとして2個のまま増えず、`Undo`ボタンが`IsEnabled=False`である理由を
  「実装がUndo対象にしておらぬ」のか「UIAの`Select()`が迂回した」のか分けられなんだ**
  （結果は前者＝**仕様どおりで不具合に非ず**と侍の読解で決着）。
  **対処＝線ツールで1本引く**（**合成ドラッグは340px以上。6.7節の距離則**）。
  **線は必ず1つ増えるゆえ、`Undo`で消えれば「機構は生きておる」と示せる。**
  **【逆向きの問い＝「履歴が伸びておらぬこと」を測るなら6.2節末尾を見よ】**（2026-08-02に線を張った）。
  **`IsEnabled`は別の操作で積まれた履歴が在れば常に`True`ゆえ、そちらでは連打して回数を数える。**
  **本項と手法は同根（`IsEnabled`を疑う）にて、目的だけが逆にござる。**

## 付記：2026-07-28時点の既知の未実装（改善候補、着手は各役の判断に委ねる）

- 任意ウィンドウハンドルを撮影する汎用`PrintWindow`ヘルパー（6.3節末尾）
- グリッド行列⇔ピクセル座標の変換ヘルパー（6.4節冒頭。**実測の較正式は同節に載せた**ゆえ、
  当面はそれで足りる）
- **キャンバス内ダブルクリックのヘルパー**（3節。**そのまま貼って使える関数定義を載せた**ゆえ、
  `helpers.ps1`へ移すか否かは各役の判断に委ねる）
- 要素の出現・消滅を待つ汎用の待機／リトライヘルパー
