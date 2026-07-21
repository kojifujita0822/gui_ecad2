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
  バーのテキストに矛盾がないか確認すること。
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
  別の限界がある。6節「トラブルシュート」の同名項目参照）

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
# → メニュー項目の取得・実行は6節「ダイアログ・ポップアップの検出」を参照（通常のFindAllでは拾えない）

# AvalonDockペインのドラッグ操作（タブ切り離し・境界リサイズ。0節参照、UIA標準パターンでは代替不可）
# タブ切り離し例: タブのタイトルText要素をつかみ、十分な距離(300px超)をドラッグする
$titleEl = Find-Ecad2Element -Name "シート"   # LayoutAnchorableのTitle文字列
$b = $titleEl.Current.BoundingRectangle
$cx = [int]($b.X + $b.Width / 2); $cy = [int]($b.Y + $b.Height / 2)
Invoke-Ecad2Drag -FromX $cx -FromY $cy -ToX ($cx + 300) -ToY ($cy + 400)
# → 切り離し成立の確認はSave-Ecad2Screenshotではなく6節のEnumWindows手法を使う（フロート化した
#   パネルは別ウィンドウとして生成されるため、メインウィンドウのPrintWindowには写らない）

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

# 検証後のクリーンアップ（通常はここでアプリを終了する）
Stop-Ecad2App
# → 殿より実機確認後に「stay」と指示された場合は、本コマンドを実行せずEcad2.Appを起動したまま
#   維持する（殿が次回自ら起動時の様子を確認したいため、2026-07-16殿指示）
```

## 4. 既知の AutomationId・要素構成（2026-07-03 T-009/T-016実測、変更されうるため都度 FindAll で確認推奨）

| AutomationId / Name | 内容 |
|---|---|
| `StatusBarArea` | ステータスバー全体。配下に `"ツール: X"` `"ズーム: N%"` のText 2件（個別IDなし、Prefixで判別） |
| `PartPaletteList` | 左パーツパレットのListBox。ListItemのNameは `PartFolderEntry { Category = ..., FilePath = ..., Definition = ... }` という完全文字列 |
| `CanvasArea` | 中央キャンバス（Pane、単一ビジュアルとしてUI Automationツリーに現れる。内部の図形要素は個別に走査できない可能性が高い＝GuiEcadのWin2D Canvasと同様の制約） |
| `DeviceTableGrid` | 右パネル機器表（DataGrid、`DataItem`単位で行、列は機器名/種別/型式） |
| ツールバーボタン | `"新規作成 (Ctrl+N)"` `"開く (Ctrl+O)"` `"上書き保存 (Ctrl+S)"` `"元に戻す (Ctrl+Z)"` `"やり直し (Ctrl+Y)"` `"PDF出力 (Ctrl+P)"`（1段目）、`"選択ツール (Esc)"` `"a接点配置 (F5)"` `"b接点配置 (F6)"` `"コイル配置 (F7)"` `"端子台配置 (F8)"`（2段目） |

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

## 6. トラブルシュート

- 要素が見つからない (`throw "... not found"`) → `Get-Ecad2Process` でアプリが起動しているか確認。
  ダイアログ（確認メッセージ等）が前面に出て要素ツリーが変わっている可能性もある。
- クリック・キー送信しても反応がない → `Set-Ecad2Foreground` を挟んでウィンドウをアクティブに
  してから再試行（`Invoke-Ecad2ScreenClick`/`Send-Ecad2Keys` は内部で自動的に行う）。
- ツール状態とツールバーのハイライト表示が食い違う（複数ボタンが同時に選択状態に見える等）→
  実装側のバグの可能性が高い。`Get-Ecad2StatusText` の値とスクリーンショットの両方を証跡として
  残し、実装担当（侍）へ再現手順とともに報告する。
- リサイズ直後にプロセスが消えた → `$env:TEMP\ecad2-ui-automation-std{out,err}.log` と
  `$env:TEMP\ecad2-crash.log`（未処理例外ハンドラが記録）を確認する。
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
  まず仮想化を疑う。
- **プロパティパネルのテキストボックス（`DeviceNameBox`等）への`ValuePattern.SetValue`は、値を
  設定できても背後のモデルへの反映（機器表更新等）がされないことがある**（2026-07-11、T-055増分3
  検証で実証。WPFの`Binding`が`LostFocus`等の実イベントを要求するため、UIA経由の値設定だけでは
  トリガーされないと見られる）。一方、要素配置時のインラインダイアログのテキストボックスは
  `Invoke-Ecad2CanvasClick`での物理クリック＋`Send-Ecad2Keys`実入力なら正しく反映される。モデルへの
  反映有無を確認したい検証では後者を使うこと。
- **`Send-Ecad2Keys`直後にスクリーンショットで見た目を確認すると、実際の物理操作とは異なる結果が
  観測されることがある**（2026-07-11、T-056のCtrl+Gグリッド表示切替検証で発生。UIA経由ではメニューの
  ToggleStateは切り替わるのにキャンバス描画が反映されていないように見えたが、殿の実機操作では
  正常に切り替わった。`Start-Sleep`を600ms挟んでも再現し、原因未特定）。キーボードショートカットの
  見た目検証で疑わしい結果が出た場合は、待機時間を増やす・複数回スクリーンショットを取るなどの
  対照実験を行うか、殿代行操作（0節参照）での再確認を検討すること。
- **モーダルダイアログを開くボタンを`InvokePattern.Invoke()`で連続Invokeすると、モーダル制約を
  無視して背後のボタンが実行され続け、同一ダイアログが複数枚重なって開くことがある**
  （2026-07-11、T-051検証で実証。`AddSheetButton`はClick=コードビハインドで`AddSheetDialog`を
  `ShowDialog()`表示する実装だが、ダイアログが開いた状態を知らずに同じボタン（や他のボタン）へ
  `Invoke()`を重ねたところ、通常のマウスクリックなら受けるはずのモーダルブロックが効かず、
  「シート追加」ダイアログが3枚重なって開いた。この状態では他要素へのInvokeが「認識できない
  エラーです」という例外で断続的に失敗したり、`FindAll`が空を返したりと原因不明の不安定挙動が
  連鎖する。ダイアログの存在自体は上記のEnumWindows手法でしか検出できない（`Save-Ecad2Screenshot`
  はメインウィンドウしか描画しないため写らない）。**対策**：ダイアログを開く可能性のあるボタン
  （Click=コードビハインドで`ShowDialog()`するもの。`Command="{Binding ...}"`のボタンは通常
  対象外）をInvokeした直後は、次の操作に進む前に必ずEnumWindowsでダイアログの出現枚数を確認する
  （1枚のみであることを確認してから、そのダイアログ内の要素を操作する）。原因不明の
  「認識できないエラー」例外や`FindAll`の空振りに遭遇したら、まずこの罠（ダイアログの多重化）を
  疑うこと。
- **PDFプレビューウィンドウのPrintWindow撮影は色情報が信頼できない**（2026-07-12、T-080検証で
  実証。PDFプレビューウィンドウ（メインとは別ウィンドウ、ネイティブPDFレンダラーを内包）を
  PrintWindow方式でスクリーンショット撮影したところ、行コメントの文字色が行によって黒/赤/青に
  ばらついて写ったが、殿の実画面確認では**全て黒色**だった。撮影画像側の異常であり、実描画は
  正常）。PDFプレビューに対する**色・配色系の所見はPrintWindow撮影画像だけを根拠に報告しない**こと。
  色異常らしきものを見つけたら、まず撮影手法起因を疑い、殿の実画面確認（0節の殿代行操作）で
  裏取りしてから判定する（レイアウト・文字内容・ページ構成の確認には引き続き使ってよい）。
- **`Send-Ecad2Keys`でダイアログへ文字列（ファイルパス等）を送るのは避ける**（2026-07-11、
  P-056で実証）。`Set-Ecad2Foreground`は元々モーダル状態を考慮せず無条件にメインウィンドウを
  アクティブ化する実装だったため、`ShowDialog`内部の`EnableWindow(FALSE)`と矛盾する
  「無効化されているのにアクティブ」という状態を作り、フォーカス誤爆→意図しないコマンド
  誘発（実機ではCtrl+O相当の再トリガーによるダイアログ多重化）につながった実例がある
  （殿PCのキーボード入力不通フリーズ＝P-056の契機ともなった）。**対策済み**：
  `Set-Ecad2Foreground`は対象ウィンドウが`IsWindowEnabled`で無効化されている間は
  `SetForegroundWindow`自体をスキップするよう改修済み（モーダル表示中は何もしない）。加えて
  `Send-Ecad2Keys`は21文字以上の文字列を既定で拒否する（`-Force`で明示解除しない限り例外）。
  これらの安全策があっても、**ダイアログへのテキスト入力は`Send-Ecad2Keys`ではなく、最近使った
  ファイル一覧のボタン操作・物理クリック+短いキー入力など別手段を優先すること**。
- **PowerShellツール呼び出し自体がアプリのフォーカスロスト系イベントを発火させる**（2026-07-12、
  T-080検証で2回再現）。F2送信等でフォーカスロスト確定型のUI（行コメントエディタ等）を開いた直後に、
  UIAクエリ等を含む**別の**PowerShell呼び出しを実行すると、その呼び出し自体が原因とみられる
  ウィンドウ非アクティブ化により数百ms後に`LostKeyboardFocus(NewFocus=null)`が自動発火し、
  対象UIが意図せず閉じる。**対策**：キー送信と状態確認を同一PowerShell呼び出し内に収めるか、
  状態確認をBashの`tail`等（PowerShell以外）に限定する。
- **標準WindowsのOpenFileDialogの「ファイル名」欄はAutomationId="1148"のPane内部に埋もれており、
  通常の`FindAll`探索では辿りつけないことがある**（2026-07-15、T-096実機確認で実証）。
  **`Send-Ecad2Keys`で`%n`（Alt+N、ファイル名欄のアクセラレータキー）を送りフォーカス移動させる
  のが確実**。ダイアログへのテキスト入力自体は上記「`Send-Ecad2Keys`でダイアログへ文字列を送るの
  は避ける」の制約に従うこと（フォーカス移動用の単発アクセラレータキーと、長い文字列送信は
  別問題）。
- **部品選択リスト（`PartSelectionList`、ListBoxItem）は`ScrollItemPattern`をサポートしており、
  `GetCurrentPattern([ScrollItemPattern]::Pattern) → ScrollIntoView()`で可視範囲外の項目も
  スクロール後に選択可能**（2026-07-15、T-096実機確認で実証）。DataGridの仮想化トラブル
  シュート（本節上部「DataGridはUI仮想化により...」参照）と同型の制約がListBoxにもあるため、
  リスト項目数が多い場合はまずスクロール状態を疑うこと。
- **殿へ物理操作を依頼した検証は「チャット復帰クリック汚染」に注意**（2026-07-12、T-080で実証。
  殿が操作結果を報告するためチャットウィンドウをクリックする——その動作自体がアプリのウィンドウ
  非アクティブ化を起こし、フォーカスロスト系イベントを発火させる。T-080往復2周目では、この汚染に
  より「窓内クリックでエディタが閉じる」という**真逆の誤った実測確定**を一度生んだ）。**対策**：
  依頼文言に「操作後、**5秒待ってから**チャットへ復帰」を明記し、診断ログのタイムスタンプで
  操作時刻と発火時刻の間隔を突合すれば、操作起因と復帰クリック起因を機械的に判別できる。
  フォーカス・アクティブ状態に関わる検証では、殿の**目視証言**（「クリック後もエディタは開いた
  ままだった」等）を一次情報として重視し、ログ単独で確定しない。
- **中心配置等の高精度な位置測定では、円弧とテキストが重なる画像領域を単純なバウンディング
  ボックス走査で扱うと円弧のピクセルを誤検出する**（2026-07-15、T-097コイル中心配置検証で実証）。
  「中心からの距離が半径-マージン未満のピクセルのみ採用」という円形マスクを適用すると誤検出を
  排除できた。円形記号（コイル等）に重なるテキストの位置測定ではこの手法を用いること。
- **Ctrl+ホイールズームの中心点挙動に一貫性を欠くことがある**（2026-07-15、T-097検証で発生。
  要素選択直後にズームすると意図した位置に留まる場合と、無関係な位置（左上）へ飛ぶ場合があり、
  条件は未特定）。原理不明のため対策は迂回策のみ：`ScrollBar`の`RangeValuePattern.SetValue`で
  直接スクロール位置を調整すること。
- **テーマ・配色（Light/Dark等）の実機検証は、目視でなく座標指定の画素採取（`Bitmap.GetPixel`等）
  を標準手順とする**（2026-07-16、T-083ダークモード検証で実証。忍者・隠密2とも同一のスクリー
  ンショットを目視で「変わっていない」と誤読した実例あり。要因は(1)アクティブキャプション等
  「両テーマ共通で変わらないのが仕様」の配色を誤NG判定しやすい (2)対象領域が細い帯（十数px）で
  目視では変化を見落としやすい、の2点。座標を決めて期待値（16進カラーコード）と実測値を突き合わ
  せる方式なら誤読が構造的に起きない）。
- **`AutomationElement`で`ControlType.MenuBar`を`FindFirst`すると、アプリのメニューバーでなく
  ウィンドウタイトルバーのシステムメニュー（左上アイコン領域）を誤って掴むことがある**
  （2026-07-16、T-083検証で実証。この罠により「メニューの背景色がテーマに反応しない」という
  誤診断が生まれかけた——実際に画素を採取していた座標が、アプリのメニューではなくOSタイトル
  バー領域（画面最上部、y座標にしておよそ0〜31px、テーマと無関係に常時同系色）だった）。
  メニューバー等ウィンドウ最上部に近い要素の座標確認は、目視・勘のy座標決め打ちではなく、
  対象の`AutomationElement`の`BoundingRectangle`を`Get-Ecad2WindowRect`のウィンドウ原点からの
  相対座標に変換して使うこと（UIAの絶対座標とウィンドウ内相対座標の変換を経由すれば、
  タイトルバー領域とアプリ内要素の取り違えを避けられる）。
- **ドロップダウンメニュー（サブメニューのPopup）は、ContextMenu同様メインウィンドウとは別の
  トップレベルウィンドウとして生成される**（2026-07-16、T-083検証で実証）。`Save-Ecad2Screenshot`
  でメインウィンドウを撮っても写らないため、6節の`EnumWindows`手順でPopup自身のウィンドウ
  ハンドルを取得し、そのハンドルへ`PrintWindow`する必要がある。
- **選択ハイライト等、テキストと重なる領域の単発ピクセル座標採取はアンチエイリアシングを拾い
  誤った色を返すことがある**（2026-07-17、T-083増分5 DataGridCell選択色検証で実証。単発座標で
  誤値`#B878D7`を得たが正しくは`#0078D7`だった）。対象領域全体の色出現頻度を集計し、最多出現色
  （支配色）を採用する方式に切り替えると安定する。選択ハイライトやポップアップ背景など、文字や
  枠線が重なりうる領域の画素採取ではこの集計方式を優先すること。
- **半透明（alpha付き）ブラシの実測値と理論値の比較は、ブラシのColor値をそのまま比べても一致
  しない**（2026-07-17、T-083増分7メニューのホバー/選択背景`MenuItemAccentBackgroundBrush`検証で
  実証）。PNGスクリーンショットは背景と合成済みの不透明ピクセルとして保存されるため、理論値側も
  「背景色とブラシのalpha合成計算値」を算出してから実測値と突き合わせること。単純比較は誤NG判定
  を招く。
- **AvalonDock既定のCtrl+Tabナビゲータ（NavigatorWindow）をUIA経由で開くには、事前にDockingManager
  内の何らかの要素へキーボードフォーカスを設定しておく必要がある**（2026-07-17、T-083増分5検証で
  実証）。フォーカス位置が不明な状態で`Ctrl+Tab`を送信してもポップアップが検出できなかった。
  対象パネル内の要素へ`SetFocus()`してから`Ctrl+Tab`を送信し、`EnumWindows`で別ウィンドウとして
  検出する手順を踏むこと。
- **【重要な例外】画素採取（スクリーンショット経由）が万能とは限らない——微細なテクスチャ・
  レンダリングパターン系の視覚アーティファクトは、静止画キャプチャでは視認の限界があり人間の
  目でしか判別できない場合がある**（2026-07-17、T-100ドックタブのハッチング模様調査で殿ご指摘。
  侍の自己目視・build/test上は「改善」と見えたが、殿の実機直接観察では「解消せず」と食い違った）。
  上記の各教訓（単発ピクセル誤読・半透明合成計算等）は「色・配色の誤判定」対策として有効だが、
  **ハッチング等の微細パターン系の不具合は、この限りでない可能性がある**。PrintWindow等の
  キャプチャ方式が特定のWPF描画効果を再現しない可能性が疑われるが未確定。この種の不具合は
  忍者のUIA/画素採取だけで「解消」と判定せず、殿ご自身の実機目視での最終確認を要する。
- **微細テクスチャ系アーティファクトの除去確認には「対象領域のユニーク色数」判定が有効な代替手段
  になりうる**（2026-07-17、T-100のドラッグハンドル模様(`DragHandleTexture`)除去確認で実証）。
  当該`Rectangle`はWPF既定でAutomationPeer非対応（`RawViewWalker`でもヒットしない）ためUIA直接
  探索は不可、また模様自体は上記の「人間の目でしか判別できない」ケースに該当しうる。しかし
  対象領域を画素採取し「出現する色の種類数」を集計する方式なら、模様が残っていれば複数色、
  完全に消えていれば単色（背景色のみ）になるという性質を機械的に判定できる。人間の目で微細な
  模様の有無を判別するより、統計的な色数カウントの方が閾値判定として頑健。同種の「消えたか
  どうか」を確認する場面（模様・テクスチャ除去等）ではこの方式を優先的に検討すること。
  **判定範囲の注意点**（2026-07-17、T-100ダークモード検証で実証）：文字・アイコンの境界に近い
  領域を含めるとアンチエイリアシングにより色数が誤って複数になり誤NG判定を招く（出力パネルで
  実際に遭遇、境界から十分離した領域で再測定し解消）。判定領域は文字・アイコン等の境界から
  離れた、単色であるべき余白部分のみに絞ること。
- **【重大な罠】`PlacementToolBarDockingManager`（配置ツールバー2段目）は、PrintWindow撮影・UIA
  探索(`FindAll`)の両方が内容を正しく捕捉できないことがある**（2026-07-17、T-099/T-100検証で
  発覚）。忍者がダークモード切替で当該パネルが「潰れている」と観測したが、殿の実機目視（人間の
  目）では正常表示と確認——UIA探索でボタン0件・PrintWindow画像でも選択ツールのみ表示と、**両手法
  が一致して「見えない」と誤示した**ため、手法の限界だと気づきにくい特に厄介なケース。T-080の
  PDFプレビュー撮影不正確の教訓と同型。**このパネルに関する「表示されない/潰れている」系の
  観測は、他手法（PrintWindow・UIA）で裏取りできても鵜呑みにせず、可能な限り殿ご自身の実機目視
  での確認を優先すること**（本スキル冒頭の「画素採取が目視に勝る」原則の数少ない例外）。原因
  技術は未解明（このDockingManagerが`AnchorablePaneControlStyle`をローカル値カスタム設定して
  いる点との関連が疑われるが未確定）。
- **ボタン押下中等、「一瞬だけ存在する状態」の視覚効果検証は自動化での確証が難しい**
  （2026-07-17、T-089押下フィードバック検証で実証）。`SetCursorPos`+`mouse_event(LEFTDOWN)`→
  スクリーンショット→`LEFTUP`という手順を試みたが、既存の`IsMouseOver`等他のフィードバックと
  視覚的に紛れる・マウスダウン〜撮影間のタイミング制御が粗く確実な確証が得られなかった。実装
  （XAML構造）自体のコード目視確認と、build/test通過は自動化で担保できるが、**瞬間的な視覚状態の
  実際の見え方は殿ご自身の実機操作でのご確認に委ねる方が確実**。
- **【重大な罠】高速連写でのGetPixel直読とBitmap保存画像が食い違うことがある**（2026-07-18、
  新規発見6=一瞬ライトモードに戻る現象の検証で実証）。要素配置確定操作直後をPrintWindow+GetPixelで
  高速連写したところ、7回中3回で特定フレームがRGB(255,255,255)を検出——しかし該当フレームの
  Bitmapをそのまま画像ファイルに保存し目視すると実際には白くなっておらず通常配色のまま。同一
  Bitmapオブジェクトへの反復GetPixel呼び出しは一貫して255を返す（読取りノイズではない）にも
  かかわらずSave画像には反映されないという食い違いが未解明のまま残った。**高速連写・過渡的状態の
  検証でGetPixel結果とBitmap保存画像を突き合わせ、両者が食い違う場合は機械的判定を鵜呑みにせず、
  検証手法自体（PrintWindowのキャプチャタイミング・GC・スレッド競合等）を疑うこと**。原因技術は
  未解明。このケースでは深追いをやめ、次善策（侍による診断ログ注入、または殿ご自身の実機目視）へ
  切り替えるのが得策と判断した。
- **グリッドセル座標の特定に毎回クリック→ステータスバー確認→微調整の当てずっぽう試行を要し
  非効率だった**（2026-07-18、忍者所見）。キャンバス原点・セル間隔（Cell幅、母線位置等）は
  `DiagramRenderer`/`LadderCanvas`側で定数化されているはずゆえ、UIA操作スクリプト側にも
  既知の原点・セル間隔を定数として持たせておけば、行/列→ピクセル座標の変換を毎回の手探りなしに
  計算できる。次回以降、頻用する座標変換はヘルパー関数として用意しておくことを推奨する。
- **「見た目と実際の状態が食い違う」系の不具合（T-044等）は、目視・画素採取だけでは真因に迫れない
  ことが多い**（2026-07-18、忍者所見）。判定ロジックは正しいのに描画に反映されない、といった
  動的な食い違いは、早めに診断ログ計装（侍への采配）へ切り替える判断を優先すること——本スキルの
  検証手法（画素採取・UIA探索）で粘るより、実測ログの方が核心に迫れる場面が多い。
- **【重大な罠】WPFキャンバス（DrawingVisual/DrawingContext方式で描画される領域、`LadderCanvas`等）
  の描画内容確認にPrintWindow（`PW_RENDERFULLCONTENT`込み）を使うと、実際に描画されている内容が
  欠落して見えることがある**（2026-07-19、T-044「OR自動配線の分岐線バグ」調査で実証）。忍者が
  PrintWindow撮影で「行8接点左側の配線が視覚的に欠落」と複数回報告し、侍の診断ログ計装でも
  `DrawWire`が正しい座標・タイミングで発火していることまで確認できたにもかかわらず矛盾が解けな
  かったが、殿の実機目視では「確実に繋がっている」、忍者のCopyFromScreen方式での再撮影でも
  正しく描画されていることが確認され、**バグの実在ではなくPrintWindow撮影手法の限界と確定**。
  `PW_RENDERFULLCONTENT`フラグは既に使用済み（`Save-Ecad2Screenshot`実装、5節参照）だったため
  「フラグ未指定」という単純な原因ではなく、DirectXベースの合成描画（別スレッド）とGDIベースの
  キャプチャ（UIスレッド起因の`PrintWindow`呼び出し）のタイミング不整合が濃厚（一般に広く文書化
  された既知問題ではないため断定はしない）。既存教訓（386-395行目の`PlacementToolBarDockingManager`
  ・281-287行目のPDFプレビュー色不正確）と同根の「PrintWindow方式の限界」系だが、**キャンバス内の
  実描画内容（配線・図形の有無）そのものが欠落する**という、より重い実例。
  **対策**：キャンバス内の描画正確性（配線・図形の有無、断線等の「無いはずの異常」）を検証する
  場面では、PrintWindow単体の結果を鵜呑みにしない。CopyFromScreen方式（フォアグラウンド化が
  前提、0節参照）でのクロスチェック、またはPDF出力（ネイティブレンダラーで別経路のため独立した
  裏取りになる）で必ず裏取りすること。**この観点に限り、0節「フォーカス非占有優先」の原則より
  正確性を優先し、CopyFromScreen使用（フォアグラウンド化を伴う）を許容する**。
- **【新規のUIA限界】AvalonDock標準のOverlayWindow/DropTarget機構（ドラッグ中に一時的に現れる
  十字型ドロップターゲットUI）は、`SetCursorPos`+`mouse_event`合成によるマウスドラッグでは
  検出できないことがある**（2026-07-19、T-099(c)十字型UI位置ズレ検証で実証）。忍者が3回試行
  したが、`EnumWindows`・`CopyFromScreen`いずれの手法でも十字型UI自体を一度も検出できず、
  副次的にフロートウィンドウのBoundsが不自然な動き（Y座標固定・X座標のみ変化・Width縮小）を
  観測するに留まった。0節「AvalonDockペインのドラッグ操作」で述べた`Invoke-Ecad2Drag`（多段階
  マウス合成）はタブ切り離し・境界リサイズの成立には有効だが、**ドラッグ中にのみ一時的に生成
  されるOverlayWindow自体の検出には別の壁がある**とみられる（原因未特定。合成マウス操作が
  AvalonDock独自の`DragService`にドラッグ開始と認識されず`CreateOverlayWindow()`自体に到達
  していない可能性、OverlayWindowの特殊なウィンドウ属性がEnumWindows等の標準列挙で拾いにくい
  可能性のいずれも排除できない）。本家AvalonDockも実ドラッグの自動テストを行わず内部API直接
  呼び出しで代替している（0節既存記述参照）ことから、構造的な自動化限界の可能性が高い。
  **この種の検証（ドロップターゲットUIの表示位置・タイミング等）は、UIA/EnumWindows合成操作
  で粘らず、早期に殿ご自身の実機目視・操作確認へ切り替えることを検討する**。
- **【新規のUIA限界】キーボードフォーカス系検証で`AutomationElement.FocusedElement`が終始
  「Window」を誤報告し実態を掴めないことがある**（2026-07-20、T-104キーボードナビゲーション
  検証で実証）。AvalonDockのAutoHideサイド領域内をフォーカスが巡回していた実態は、UIA単独では
  一切検出できず、侍が仕込んだ`GotFocus`イベントのクラスハンドラ計装（`%TEMP%`へ同期I/Oで
  記録）によって初めて判明した。**UIA単独でのフォーカス系検証には構造的な限界があり、
  `FocusedElement`が実態と乖離していると疑われる場合は早期に診断ログ計装（侍への采配）へ
  切り替えること**。
- **視覚的なフォーカス破線枠が実際のWPF内部フォーカスと食い違うことがある**（同上検証で実証）。
  対策前、Tab連打してもフォーカス破線枠は終始「基本機能」タブに固定表示のまま変化しなかったが、
  診断ログ実測では実フォーカスはAvalonDock内部コントロール階層を巡回していた。**目視のフォーカス
  表示（破線枠等）も、UIAの`FocusedElement`と同様に鵜呑みにできない場合がある**——両者が一致
  していても実態と異なりうる点に注意する。
- **狙った不整合を人為的に作る検証テクニック**（同上検証、レイアウト読込失敗メッセージの確認で
  使用）：`%AppData%`のレイアウトXML等、永続化ファイルを検証前に意図的に旧構成へ書き換えてから
  アプリを起動し、想定した異常系（読込失敗・非互換検出等）が正しく発火するかを確認する手法。
  検証後は元のファイルをバックアップから復元すること。正常系の実機確認だけでなく、意図的な
  異常系の再現にも応用できる。
- **Disabled状態のボタンへ`InvokePattern.Invoke()`すると「認識できないエラーです」という
  原因不明の例外を返すことがある**（2026-07-21、T-101検証で実証。シートが1枚も無い状態で
  配置ツールボタン群がDisabledのままInvokeを試み時間を要した）。上記269-282行の「モーダル
  ダイアログ多重化」由来の同一エラーメッセージとは別原因。**全ボタンで同一エラーが連発する
  場合は、まずダイアログ多重化を疑い、それで説明がつかなければ次に対象要素のEnabled状態
  （前提操作＝シート追加等が漏れていないか）を疑うこと**。
- **別ウィンドウ（ComboBoxドロップダウンPopup・PDFプレビュー等）のPrintWindow撮影は、
  `Save-Ecad2Screenshot`がメインウィンドウ専用のため毎回インラインでAdd-Typeし直す運用に
  なっている**（2026-07-21、忍者所見・T-106/T-107で複数回発生）。`helpers.ps1`は既に
  `Ecad2Native`クラス（`PrintWindow`/`GetWindowRect`のP/Invokeラッパー）を内部で保持して
  いるため、**独自にAdd-Typeせず`Ecad2Native`をそのまま使えば`System.Drawing.Common`参照
  エラー等を回避できる**（侍もT-106で同エラーに数回つまずいた後、helpers.ps1の既存インフラ
  流用で即解決した実例あり）。任意ウィンドウハンドルを撮影する汎用ヘルパー化は今後の改善
  候補（未実装、着手は各役の判断に委ねる）。
- **`SheetNavList`のScrollBarは`ControlType.ScrollBar`でのUIA探索では検出できない**
  （2026-07-21、T-106検証で実証。座標を目視特定してから画素採取する迂回で対応）。
  シート数を増やしてスクロールバーを実出現させた状態での検証が必要な場合は、UIA探索を
  試みる前提を置かず、まず座標の目視特定＋画素採取の手順に切り替えること。
- **実機確認の前にCore/App層の該当コード（プロパティの持ち方、共有/非共有の設計等）を
  先読みしておくと、殿からの追加確認依頼（仕様面の疑問）にも即座に的確な再現手順を組める**
  （2026-07-21、T-107で実証。`ElementInstance.Comment`が要素インスタンス個別かDeviceクラス
  共有かをコードで確認してから実機再現したことでスムーズに検証できた）。UIA操作の組み立てに
  入る前の一手間として、関連コードの構造を先に把握する価値がある。
- **「名前を付けて保存」ダイアログの既定フォルダは、同一セッション内の直前の別ダイアログ操作
  （画像挿入等）で使ったカレントフォルダを引き継ぐことがある**（2026-07-21、T-098検証で実証。
  T-105検証で画像挿入時に使った`C:\Windows\Web\Screen`が残存し、そのまま保存しようとして
  「アクセス許可がありません」という想定外の確認ダイアログが追加で挟まった）。**対策**：
  保存操作では`%n`でファイル名欄へフォーカス後、ファイル名のみでなく`C:\ECAD2\sample\<name>`
  のようにフルパスを明示的に入力すると、フォルダの引き継ぎ問題を回避できる。
- **標準WPFの`GridSplitter`（Thumbドラッグ）は、AvalonDockペインの境界ドラッグと異なり、
  対象ウィンドウの明示的なフォアグラウンド化が必要**（2026-07-21、T-077増分2検証で実証。
  非モーダル`UsageWindow`内のGridSplitterへ`Invoke-Ecad2Drag`相当の操作を試みたところ、
  フォーカスが無い状態では無反応だったが、`SetForegroundWindow`後は正しく動作した）。
  AvalonDock側のドラッグ操作（0節参照）は内部的にフォーカス状態を問わず動作する実績がある
  ため同一視しがちだが、標準WPFコントロールのThumbドラッグは別物と意識すること。非モーダル
  ウィンドウ内のGridSplitter等をドラッグ検証する際は、操作前に対象ウィンドウを
  フォアグラウンド化する一手間を忘れないこと。
