# Ecad2 UI Automation ヘルパー
# 使い方: 各 PowerShell 呼び出しの冒頭で dot-source する。
#   . "C:\ECAD2\.claude\skills\ecad2-ui-automation\helpers.ps1"
# PowerShellツールはコマンド間でシェル状態が持続しないため、呼び出しごとに再読込が必要。

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not ("Ecad2Native" -as [type])) {
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Ecad2Native {
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsWindowEnabled(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    public const uint PW_RENDERFULLCONTENT = 0x2;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    public const uint MOUSEEVENTF_LEFTUP = 0x04;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    public const uint MOUSEEVENTF_RIGHTUP = 0x10;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
    public const byte VK_CONTROL = 0x11;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(150);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(80);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(150);
    }
    public static void RightClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(150);
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(80);
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(150);
    }
    public static void Scroll(int x, int y, int delta) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(150);
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
        System.Threading.Thread.Sleep(150);
    }
    // Ctrl+ホイールはWPFのInputManagerがCtrl押下状態をキーボードAPI経由でしか検知しないため、
    // mouse_event単体のScroll()とは別にCtrl押下を明示的に挟む合成が要る（2026-07-05、T-021ズーム検証で実証）。
    public static void CtrlScroll(int x, int y, int clicks, int delayMs) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(100);
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        int direction = clicks >= 0 ? 1 : -1;
        int count = clicks >= 0 ? clicks : -clicks;
        for (int i = 0; i < count; i++) {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)(direction * 120), UIntPtr.Zero);
            System.Threading.Thread.Sleep(delayMs);
        }
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
"@
}

function Get-Ecad2Process {
    Get-Process -Name "Ecad2.App" -ErrorAction SilentlyContinue
}

# dotnet run はラッパープロセス(dotnet)＋実プロセス(Ecad2.App)の2つが立つ。
# 検証操作の対象は必ず Ecad2.App 側（MainWindowHandle を持つ方）。
# 既定（-Screen 'Auto'）はセカンダリモニタが在ればそちらへ自動移動する【MUST・殿指示2026-07-07】。
# ユーザーが作業中のプライマリモニタを検証ウィンドウで占有しないため。'None' での打ち消しは
# 正当な理由（プライマリでしか再現しない事象の検証等）があり殿の了承を得た場合に限る。
function Start-Ecad2App {
    param(
        [string]$OutLog = "$env:TEMP\ecad2-ui-automation-stdout.log",
        [string]$ErrLog = "$env:TEMP\ecad2-ui-automation-stderr.log",
        [ValidateSet('Auto', 'Primary', 'Secondary', 'None')][string]$Screen = 'Auto'
    )
    if (Get-Ecad2Process) { throw "Ecad2.App is already running. Call Stop-Ecad2App first." }
    Start-Process -FilePath "dotnet" -ArgumentList "run --project src/Ecad2.App" -WorkingDirectory "C:\ECAD2" `
        -RedirectStandardOutput $OutLog -RedirectStandardError $ErrLog -WindowStyle Hidden | Out-Null
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        $p = Get-Ecad2Process
        if ($p -and $p.MainWindowHandle -ne 0) {
            if ($Screen -eq 'Auto') {
                Add-Type -AssemblyName System.Windows.Forms
                $hasSecondary = [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Select-Object -First 1
                if ($hasSecondary) { Move-Ecad2WindowToScreen -Screen Secondary }
            } elseif ($Screen -ne 'None') {
                Move-Ecad2WindowToScreen -Screen $Screen
            }
            return $p
        }
    }
    throw "Ecad2.App did not start within 10 seconds"
}

# ウィンドウを指定モニタへ移動する（サイズは維持、モニタ左上から40,40オフセットに配置）。
# モニタが1台しかない環境で 'Secondary' を指定した場合は例外を投げる。
function Move-Ecad2WindowToScreen {
    param([Parameter(Mandatory)][ValidateSet('Primary', 'Secondary')][string]$Screen)
    Add-Type -AssemblyName System.Windows.Forms
    $screens = [System.Windows.Forms.Screen]::AllScreens
    $target = if ($Screen -eq 'Primary') {
        $screens | Where-Object { $_.Primary } | Select-Object -First 1
    } else {
        $screens | Where-Object { -not $_.Primary } | Select-Object -First 1
    }
    if (-not $target) { throw "指定モニタ('$Screen')が見つかりません（接続モニタ数: $($screens.Count)）" }
    $proc = Get-Ecad2Process
    if (-not $proc) { throw "Ecad2.App process not found." }
    $rect = Get-Ecad2WindowRect
    $newX = $target.Bounds.X + 40
    $newY = $target.Bounds.Y + 40
    [Ecad2Native]::MoveWindow($proc.MainWindowHandle, $newX, $newY, $rect.Width, $rect.Height, $true) | Out-Null
    Start-Sleep -Milliseconds 400
    $proc.Refresh()
    if ($proc.HasExited) { throw "Ecad2.App process exited after moving to screen '$Screen'." }
}

function Get-Ecad2Root {
    $proc = Get-Ecad2Process
    if (-not $proc) { throw "Ecad2.App process not found. Launch it first: Start-Ecad2App" }
    [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
}

# 【フォーカス強奪注意】呼び出すと殿が操作中の他ウィンドウからフォーカスを奪う。Send-Ecad2Keys/
# Invoke-Ecad2ScreenClick系（グローバル入力が本質的に必要な操作）が内部で必要時にのみ呼ぶ設計
# のため、通常はこの関数を直接呼ぶ必要はない（2026-07-10、殿指示によりフォーカス非占有化を
# 優先する運用へ変更。Invoke-Ecad2Button等のUI Automationパターン系はフォーカス不要）。
function Set-Ecad2Foreground {
    $proc = Get-Ecad2Process
    if (-not $proc) { throw "Ecad2.App process not found." }
    if (-not [Ecad2Native]::IsWindowEnabled($proc.MainWindowHandle)) {
        return   # モーダルダイアログ表示中と推定、メインウィンドウのアクティブ化はしない(P-056対策)
    }
    [Ecad2Native]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 300
}

# Name / AutomationId / ControlType の任意組み合わせで要素を検索する。
# 例: Find-Ecad2Element -Name "a接点配置 (F5)" -ControlType ([System.Windows.Automation.ControlType]::Button)
function Find-Ecad2Element {
    param(
        [string]$Name,
        [string]$AutomationId,
        [System.Windows.Automation.ControlType]$ControlType,
        [switch]$All
    )
    $root = Get-Ecad2Root
    $conditions = New-Object System.Collections.Generic.List[System.Windows.Automation.Condition]
    if ($Name) { $conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name))) }
    if ($AutomationId) { $conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId))) }
    if ($ControlType) { $conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType))) }
    if ($conditions.Count -eq 0) { throw "Specify at least one of -Name / -AutomationId / -ControlType" }
    $cond = $conditions[0]
    for ($i = 1; $i -lt $conditions.Count; $i++) {
        $cond = New-Object System.Windows.Automation.AndCondition($cond, $conditions[$i])
    }
    if ($All) { return $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond) }
    $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

# InvokePattern（ボタン等）→ 失敗時 SelectionItemPattern（ListItem/TreeItem等）→ TogglePattern の順で試す。
# いずれもUI Automationのプログラム的パターン呼び出しのみで完結し、SetForegroundWindow・
# SetCursorPos等のグローバル入力を一切使わない（2026-07-10実証）。ゆえにフォアグラウンド化・
# マウスカーソル移動を伴わず、殿が他ウィンドウを操作中でも安全に呼べる。ボタン押下・リスト
# 選択・テキスト入力(ValuePattern.SetValue)で完結する検証は極力この経路を使い、
# Send-Ecad2Keys/Invoke-Ecad2ScreenClick系（フォーカス・カーソルを強制的に奪う）は
# キーボードショートカット自体の検証・キャンバス内座標操作等、代替手段が無い場面に限定すること。
function Invoke-Ecad2Element {
    param([Parameter(Mandatory)]$Element)
    $ip = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$ip)) {
        $ip.Invoke(); return $true
    }
    $sip = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$sip)) {
        $sip.Select(); return $true
    }
    $tp = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$tp)) {
        $tp.Toggle(); return $true
    }
    return $false
}

# ツールバー/パレットのボタンを Name で直接押す（座標クリック不要・最も安定）
# Ecad2のツールバーボタンは "a接点配置 (F5)" のように AutomationProperties.Name が付与済み（GuiEcadと異なる）。
function Invoke-Ecad2Button {
    param([Parameter(Mandatory)][string]$Name)
    $el = Find-Ecad2Element -Name $Name -ControlType ([System.Windows.Automation.ControlType]::Button)
    if (-not $el) { $el = Find-Ecad2Element -Name $Name -ControlType ([System.Windows.Automation.ControlType]::ListItem) }
    if (-not $el) { throw "Button/ListItem '$Name' not found" }
    Invoke-Ecad2Element -Element $el | Out-Null
}

# ステータスバー(AutomationId=StatusBarArea)配下の全Textを取得する。
# Ecad2はGuiEcadと違い個別AutomationIdを持たない（"ツール: Select" / "ズーム: 100%" のような文字列で並ぶ）。
# -Prefix を指定すると前方一致する最初の1件を返す（例: "ツール:"）。省略時は全件を返す。
function Get-Ecad2StatusText {
    param([string]$Prefix)
    $bar = Find-Ecad2Element -AutomationId "StatusBarArea"
    if (-not $bar) { return $null }
    $texts = $bar.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
    $names = @($texts | ForEach-Object { $_.Current.Name })
    if ($Prefix) { return ($names | Where-Object { $_.StartsWith($Prefix) } | Select-Object -First 1) }
    return $names
}

# 要素の画面上の正確な中心座標を取得（キャンバス内クリック等、座標が必要な場面用）
function Get-Ecad2ElementCenter {
    param([Parameter(Mandatory)][string]$Name, [System.Windows.Automation.ControlType]$ControlType)
    $el = if ($ControlType) { Find-Ecad2Element -Name $Name -ControlType $ControlType } else { Find-Ecad2Element -Name $Name }
    if (-not $el) { throw "Element '$Name' not found" }
    $r = $el.Current.BoundingRectangle
    [PSCustomObject]@{ X = [int]($r.X + $r.Width / 2); Y = [int]($r.Y + $r.Height / 2) }
}

# ウィンドウの現在位置・サイズ（マルチモニタ環境では起動位置が2240,116のような値になることもある。
# 座標計算は必ずこれを基準にし、絶対座標のハードコードはしない）
function Get-Ecad2WindowRect {
    $proc = Get-Ecad2Process
    if (-not $proc) { throw "Ecad2.App process not found." }
    $rect = New-Object Ecad2Native+RECT
    [Ecad2Native]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    [PSCustomObject]@{ Left = $rect.Left; Top = $rect.Top; Right = $rect.Right; Bottom = $rect.Bottom; Width = $rect.Right - $rect.Left; Height = $rect.Bottom - $rect.Top }
}

# 【フォーカス・カーソル強奪注意】SetCursorPos+mouse_eventでグローバルなマウスカーソルを
# 動かすため、呼び出し中は殿の実カーソル操作と衝突する。キャンバス上のセルクリック等、UI
# Automationツリーで要素を辿れず座標指定が本質的に必要な操作専用（固定UIには使わない、
# Invoke-Ecad2Buttonを優先すること）。
function Invoke-Ecad2ScreenClick {
    param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y)
    Set-Ecad2Foreground
    [Ecad2Native]::Click($X, $Y)
}

# ウィンドウ左上を原点とした相対座標でクリックする（ウィンドウ位置が起動のたびに変わっても安定）
function Invoke-Ecad2CanvasClick {
    param([Parameter(Mandatory)][int]$RelativeX, [Parameter(Mandatory)][int]$RelativeY)
    $rect = Get-Ecad2WindowRect
    Invoke-Ecad2ScreenClick -X ($rect.Left + $RelativeX) -Y ($rect.Top + $RelativeY)
}

# ウィンドウ左上を原点とした相対座標で右クリックする（T-055増分3のコンテキストメニュー検証用に新設。
# 【フォーカス・カーソル強奪注意】Invoke-Ecad2ScreenClickと同様、SetCursorPos+mouse_eventで
# グローバルなマウスカーソルを動かすため、殿の実カーソル操作と衝突する。右クリックでのみ
# 開くContextMenu（PreviewMouseRightButtonDownハンドラ）はUI Automationパターン経由で代替できない
# ため座標右クリックが本質的に必要な唯一のケース）
function Invoke-Ecad2CanvasRightClick {
    param([Parameter(Mandatory)][int]$RelativeX, [Parameter(Mandatory)][int]$RelativeY)
    $rect = Get-Ecad2WindowRect
    Set-Ecad2Foreground
    [Ecad2Native]::RightClick($rect.Left + $RelativeX, $rect.Top + $RelativeY)
}

function Invoke-Ecad2Scroll {
    param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [int]$Delta = -120, [int]$Times = 1)
    Set-Ecad2Foreground
    for ($i = 0; $i -lt $Times; $i++) { [Ecad2Native]::Scroll($X, $Y, $Delta) }
}

# Ctrl+ホイールでのズーム操作を合成する（Ctrl+マウスホイールでのズームUI検証用）。
# 通常のホイールイベント(mouse_event)だけではCtrl修飾が反映されないため、keybd_eventでの
# 明示的なCtrl押下/解放を挟む。$Clicks正=ズームイン(上方向)、負=ズームアウト(下方向)。
# 座標はスクリーン絶対座標（Get-Ecad2ElementCenter等で対象要素の中心を求めて渡すこと）。
function Invoke-Ecad2CtrlScroll {
    param(
        [Parameter(Mandatory)][int]$ScreenX,
        [Parameter(Mandatory)][int]$ScreenY,
        [Parameter(Mandatory)][int]$Clicks,
        [int]$DelayMs = 80
    )
    Set-Ecad2Foreground
    [Ecad2Native]::CtrlScroll($ScreenX, $ScreenY, $Clicks, $DelayMs)
}

# 【フォーカス強奪注意】SendKeysはWindowsのフォーカスウィンドウにしか届かないグローバル
# キー入力のため、呼び出し中は殿のキーボード入力と衝突する。キーボードショートカット自体の
# 検証（対応するボタンが無い/ショートカット固有の挙動を見たい場合）に限定し、単に「そのコマンドを
# 実行したいだけ」ならボタンのInvoke-Ecad2Button経由（フォーカス不要）を優先すること。
# WPFアプリのためSendKeysが機能する（GuiEcad=WinUI3では届かなかったのと対照的）。
# 例: Send-Ecad2Keys "{ESC}" / Send-Ecad2Keys "^{TAB}"（Ctrl+Tab）/ Send-Ecad2Keys "{F5}"
function Send-Ecad2Keys {
    param([Parameter(Mandatory)][string]$Keys, [switch]$Force)
    if ($Keys.Length -gt 20 -and -not $Force) {
        throw "Send-Ecad2Keys: キー文字列が長すぎます('$Keys', $($Keys.Length)文字)。ショートカットキー検証以外の用途(ファイルパス入力等)が疑われます。ダイアログへの入力は別手段(最近使ったファイル一覧・UIボタン操作等)を優先してください。意図的な場合は -Force を指定。"
    }
    Set-Ecad2Foreground
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds 200
}

# PrintWindow(user32.dll、PW_RENDERFULLCONTENT)で対象ウィンドウの内容を直接ビットマップへ
# 描画する。CopyFromScreen(画面表示のキャプチャ)と異なり、対象ウィンドウがフォアグラウンドで
# なくても・他ウィンドウに重なっていても正しく撮れる（2026-07-10、殿裁定によるフォーカス
# 非占有化の一環。旧実装はCopyFromScreen専用でフォアグラウンド化が前提のため、他ウィンドウが
# 前面にある状態で撮影すると誤ってそちらが写り込む事故があった＝実際に発生済み）。
# PrintWindowが失敗した場合（環境依存、極めて稀）のみCopyFromScreen+フォアグラウンド化へ
# フォールバックする。
function Save-Ecad2Screenshot {
    param([Parameter(Mandatory)][string]$Path)
    $proc = Get-Ecad2Process
    if (-not $proc) { throw "Ecad2.App process not found." }
    $rect = Get-Ecad2WindowRect
    $bmp = New-Object System.Drawing.Bitmap $rect.Width, $rect.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [Ecad2Native]::PrintWindow($proc.MainWindowHandle, $hdc, [Ecad2Native]::PW_RENDERFULLCONTENT)
    $g.ReleaseHdc($hdc)
    if (-not $ok) {
        Set-Ecad2Foreground
        $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    }
    $dir = Split-Path $Path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return $Path
}

# リサイズは実装によっては不安定な場合がある（2026-07-03実測: リサイズ直後にプロセスが消失する事象を
# 2回確認、ただし直後の再テストでは再現せず原因未特定）。使用後は必ずプロセス生存を確認すること。
function Resize-Ecad2Window {
    param([Parameter(Mandatory)][int]$Width, [Parameter(Mandatory)][int]$Height, [int]$X = 100, [int]$Y = 80)
    $proc = Get-Ecad2Process
    if (-not $proc) { throw "Ecad2.App process not found." }
    [Ecad2Native]::MoveWindow($proc.MainWindowHandle, $X, $Y, $Width, $Height, $true) | Out-Null
    Start-Sleep -Milliseconds 600
    $proc.Refresh()
    if ($proc.HasExited) { throw "Ecad2.App process exited after resize to ${Width}x${Height}. Possible resize-related crash." }
}

function Stop-Ecad2App {
    Get-Process -Name "Ecad2.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
        (Get-CimInstance Win32_Process -Filter "ProcessId = $($_.Id)" -ErrorAction SilentlyContinue).CommandLine -like "*Ecad2.App*"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
    if (Get-Ecad2Process) { Write-Output "WARNING: Ecad2.App still running after Stop-Ecad2App" }
    else { Write-Output "Ecad2.App stopped" }
}
