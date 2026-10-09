# 使い方の説明図（スクリーンショット＋矢印）を作るための共通関数（殿ご下命2026-10-09）。
# New-UsageShots.ps1 から dot-source して使う。単体では何もせぬ。
#
# 【実機確認スキル(.claude/skills/ecad2-ui-automation/helpers.ps1)を使い回さぬ理由】
# あちらは対象を「Ecad2.App という名のプロセス」で探す。殿がインストール版を起動なされておる間に
# 走らせれば、殿のアプリを掴んで操作してしまう。本スクリプトは自ら起動したプロセスの PID と
# ウィンドウハンドルだけを相手にし、名前では一切探さぬ。
#
# 【殿のレイアウトを書き換えぬための取り決め】
# - 終了は必ず強制終了(Stop-Process -Force)。正規終了は Window_Closing を通り、撮影用に整えた
#   パネル配置が殿の main-layout.xml へ保存されてしまう。
# - それでも念のため、起動前に main-layout.xml を退避し、終了後にハッシュを突き合わせる
#   (New-UsageShots.ps1 が行う)。

Add-Type -AssemblyName System.Drawing, System.Windows.Forms, UIAutomationClient, UIAutomationTypes

if (-not ('UsageShotNative' -as [type])) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public class UsageShotNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
}
"@
}

# 撮影用のアプリを起動し、指定のモニタへ決まった大きさで置く。戻り値は Process。
# 図面は起動引数で開く(ファイルダイアログを操作せずに済む。T-123 の関連付け用引数の転用)。
function Start-UsageShotApp {
    param(
        [Parameter(Mandatory)][string]$ExePath,
        [string]$DocumentPath,
        [int]$Width = 1400,
        [int]$Height = 900
    )
    if (-not (Test-Path $ExePath)) { throw "アプリが見つからぬ: $ExePath （先に dotnet build src/Ecad2.App を）" }

    $startArgs = @{ FilePath = $ExePath; PassThru = $true }
    if ($DocumentPath) { $startArgs.ArgumentList = "`"$DocumentPath`"" }
    $process = Start-Process @startArgs

    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) { throw "アプリが起動直後に終了した(終了コード $($process.ExitCode))" }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 300
    }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw "アプリのウィンドウが30秒以内に現れなんだ" }

    # セカンダリモニタが在ればそちらへ(殿の作業画面を塞がぬ)。無ければプライマリの左上。
    $screen = [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Select-Object -First 1
    if (-not $screen) { $screen = [System.Windows.Forms.Screen]::PrimaryScreen }
    [UsageShotNative]::MoveWindow($process.MainWindowHandle, $screen.Bounds.X + 60, $screen.Bounds.Y + 40, $Width, $Height, $true) | Out-Null
    Start-Sleep -Milliseconds 2500   # 図面の読み込みとレイアウトの落ち着きを待つ

    $dpi = [UsageShotNative]::GetDpiForWindow($process.MainWindowHandle)
    if ($dpi -ne 96) { Write-Warning "ウィンドウのDPIが $dpi (96でない)。絵の大きさと注釈の位置がずれうる。" }
    return $process
}

# 自ら起動した撮影用アプリだけを止める。念のためパスを確かめ、他所のプロセスには手を出さぬ。
function Stop-UsageShotApp {
    param([Parameter(Mandatory)][System.Diagnostics.Process]$Process, [Parameter(Mandatory)][string]$ExePath)
    $Process.Refresh()
    if ($Process.HasExited) { return }
    $actual = (Get-Process -Id $Process.Id -ErrorAction SilentlyContinue).Path
    if ($actual -ne (Resolve-Path $ExePath).Path) { throw "PID $($Process.Id) は撮影用アプリではない($actual)。止めぬ。" }
    Stop-Process -Id $Process.Id -Force
    $Process.WaitForExit(5000) | Out-Null
}

# ウィンドウの「見えておる枠」の矩形(スクリーン座標)。GetWindowRect は見えぬリサイズ枠を含むため、
# そのまま撮ると右と下に黒い帯が付く。
function Get-UsageShotFrame {
    param([Parameter(Mandatory)][IntPtr]$Handle)
    $outer = New-Object UsageShotNative+RECT
    [UsageShotNative]::GetWindowRect($Handle, [ref]$outer) | Out-Null
    $frame = New-Object UsageShotNative+RECT
    $size = [System.Runtime.InteropServices.Marshal]::SizeOf([type][UsageShotNative+RECT])
    if ([UsageShotNative]::DwmGetWindowAttribute($Handle, 9, [ref]$frame, $size) -ne 0) { $frame = $outer }
    [pscustomobject]@{
        OuterLeft = $outer.Left; OuterTop = $outer.Top
        OuterWidth = $outer.Right - $outer.Left; OuterHeight = $outer.Bottom - $outer.Top
        Left = $frame.Left; Top = $frame.Top
        Width = $frame.Right - $frame.Left; Height = $frame.Bottom - $frame.Top
    }
}

# ウィンドウを撮って Bitmap で返す(呼び手が Dispose する)。PrintWindow 方式ゆえ、前面に出さずとも
# 他のウィンドウに隠れておっても撮れる。見えておる枠だけに切り詰める。
function Get-UsageShotBitmap {
    param([Parameter(Mandatory)][IntPtr]$Handle)
    $frame = Get-UsageShotFrame -Handle $Handle
    $full = New-Object System.Drawing.Bitmap $frame.OuterWidth, $frame.OuterHeight
    $graphics = [System.Drawing.Graphics]::FromImage($full)
    $hdc = $graphics.GetHdc()
    $ok = [UsageShotNative]::PrintWindow($Handle, $hdc, 2)   # 2 = PW_RENDERFULLCONTENT
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    if (-not $ok) { $full.Dispose(); throw "PrintWindow が失敗した" }

    $crop = New-Object System.Drawing.Rectangle ($frame.Left - $frame.OuterLeft), ($frame.Top - $frame.OuterTop), $frame.Width, $frame.Height
    $cropped = $full.Clone($crop, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $full.Dispose()
    $cropped.SetResolution(96, 96)
    return $cropped
}

# ウィンドウ配下の UIA 要素を、絵の左上を原点とする矩形つきで列挙する。注釈の的はこれで引く
# ——座標を手で打たずに済み、画面の配置が変わっても撮り直すだけで追従する。
function Get-UsageShotAnchors {
    param([Parameter(Mandatory)][IntPtr]$Handle)
    $frame = Get-UsageShotFrame -Handle $Handle
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($Handle)
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $all) {
        $current = $element.Current
        $rect = $current.BoundingRectangle
        if ($rect.IsEmpty -or $rect.Width -le 0 -or $rect.Height -le 0) { continue }
        if (-not $current.AutomationId -and -not $current.Name) { continue }
        [pscustomobject]@{
            AutomationId = $current.AutomationId
            Name = $current.Name
            ControlType = $current.ControlType.ProgrammaticName -replace '^ControlType\.', ''
            X = [int]($rect.X - $frame.Left); Y = [int]($rect.Y - $frame.Top)
            Width = [int]$rect.Width; Height = [int]$rect.Height
        }
    }
}

# 注釈の的(target)を、絵の上の1点と、囲み枠に使う矩形へ解く。
# 指定の形:
#   { "automationId": "CanvasArea" }            UIA の AutomationId(完全一致)
#   { "name": "a接点配置 (F5)" }                 UIA の Name(完全一致)
#   { "nameLike": "*PB1*" }                      UIA の Name(ワイルドカード)
#   { "xy": [640, 300] }                         絵の上の座標(最後の手段。画面が変われば直す要あり)
# いずれも "at"(center/left/right/top/bottom/topLeft/topRight/bottomLeft/bottomRight) と
# "nudge": [dx, dy] で矩形の中の位置を選べる。複数当たれば "index"(0始まり)で選ぶ。
function Resolve-UsageShotTarget {
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)]$Anchors)
    if ($Target.xy) {
        return [pscustomobject]@{ X = [double]$Target.xy[0]; Y = [double]$Target.xy[1]; Rect = $null }
    }
    $found = @(
        if ($Target.automationId) { $Anchors | Where-Object { $_.AutomationId -eq $Target.automationId } }
        elseif ($Target.name) { $Anchors | Where-Object { $_.Name -eq $Target.name } }
        elseif ($Target.nameLike) { $Anchors | Where-Object { $_.Name -like $Target.nameLike } }
        else { throw "的の指定が解せぬ: $($Target | ConvertTo-Json -Compress)" }
    )
    if ($Target.controlType) { $found = @($found | Where-Object { $_.ControlType -eq $Target.controlType }) }
    if ($found.Count -eq 0) { throw "的が画面に見つからぬ: $($Target | ConvertTo-Json -Compress)" }
    $index = if ($null -ne $Target.index) { [int]$Target.index } else { 0 }
    if ($found.Count -gt 1 -and $null -eq $Target.index) {
        Write-Warning "的が $($found.Count) 件当たった。先頭を使う: $($Target | ConvertTo-Json -Compress)"
    }
    $a = $found[$index]
    $at = if ($Target.at) { [string]$Target.at } else { 'center' }
    $x = switch -Regex ($at) { 'left$|Left$' { $a.X; break } 'right$|Right$' { $a.X + $a.Width; break } default { $a.X + $a.Width / 2 } }
    $y = switch -Regex ($at) { '^top' { $a.Y; break } '^bottom' { $a.Y + $a.Height; break } default { $a.Y + $a.Height / 2 } }
    if ($Target.nudge) { $x += [double]$Target.nudge[0]; $y += [double]$Target.nudge[1] }
    [pscustomobject]@{ X = [double]$x; Y = [double]$y; Rect = (New-Object System.Drawing.RectangleF $a.X, $a.Y, $a.Width, $a.Height) }
}

# 注釈を絵へ焼き込む。注釈1件の形:
#   { "number": 1, "target": {...}, "badgeOffset": [dx, dy], "box": true }
# - 番号の丸(badge)を「的の点＋badgeOffset」に置き、そこから的の点へ矢印を引く。
#   badgeOffset を省くか [0,0] にすれば、的の点に丸だけを置く(矢印なし)。
# - "box": true で、的の要素の外周を囲む(xy 指定の的では囲めぬ)。
# 丸数字の文字(環境依存文字)は使わず、丸を描いて中へ普通の数字を書く。
function Add-UsageShotAnnotations {
    param([Parameter(Mandatory)][System.Drawing.Bitmap]$Bitmap, [Parameter(Mandatory)]$Annotations, [Parameter(Mandatory)]$Anchors)
    $accent = [System.Drawing.Color]::FromArgb(255, 211, 47, 47)
    $halo = [System.Drawing.Color]::FromArgb(235, 255, 255, 255)
    $radius = 13.0

    $g = [System.Drawing.Graphics]::FromImage($Bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $accentBrush = New-Object System.Drawing.SolidBrush $accent
    $haloBrush = New-Object System.Drawing.SolidBrush $halo
    $font = New-Object System.Drawing.Font 'Segoe UI', 11.5, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    try {
        # 囲み枠と矢印を先に、番号の丸を後に描く(丸が線に埋もれぬように)。
        $resolved = foreach ($annotation in $Annotations) {
            $target = Resolve-UsageShotTarget -Target $annotation.target -Anchors $Anchors
            $dx = if ($annotation.badgeOffset) { [double]$annotation.badgeOffset[0] } else { 0.0 }
            $dy = if ($annotation.badgeOffset) { [double]$annotation.badgeOffset[1] } else { 0.0 }
            [pscustomobject]@{ Annotation = $annotation; Target = $target; BadgeX = $target.X + $dx; BadgeY = $target.Y + $dy }
        }

        foreach ($item in $resolved) {
            if ($item.Annotation.box -and $item.Target.Rect) {
                $r = $item.Target.Rect
                $haloPen = New-Object System.Drawing.Pen $halo, 5
                $boxPen = New-Object System.Drawing.Pen $accent, 2.5
                $g.DrawRectangle($haloPen, $r.X - 2, $r.Y - 2, $r.Width + 4, $r.Height + 4)
                $g.DrawRectangle($boxPen, $r.X - 2, $r.Y - 2, $r.Width + 4, $r.Height + 4)
                $haloPen.Dispose(); $boxPen.Dispose()
            }
            $vx = $item.Target.X - $item.BadgeX; $vy = $item.Target.Y - $item.BadgeY
            $length = [Math]::Sqrt($vx * $vx + $vy * $vy)
            if ($length -gt $radius + 8) {
                # 丸の縁から的の点へ。白い縁取りを下に敷き、どんな地の色でも見えるようにする。
                $ux = $vx / $length; $uy = $vy / $length
                $from = New-Object System.Drawing.PointF ($item.BadgeX + $ux * $radius), ($item.BadgeY + $uy * $radius)
                $to = New-Object System.Drawing.PointF $item.Target.X, $item.Target.Y
                foreach ($style in @(@($halo, 6.0), @($accent, 2.5))) {
                    $pen = New-Object System.Drawing.Pen $style[0], $style[1]
                    $pen.CustomEndCap = New-Object System.Drawing.Drawing2D.AdjustableArrowCap 3.2, 4.2, $true
                    $g.DrawLine($pen, $from, $to)
                    $pen.Dispose()
                }
            }
        }

        foreach ($item in $resolved) {
            $g.FillEllipse($haloBrush, $item.BadgeX - $radius - 2, $item.BadgeY - $radius - 2, ($radius + 2) * 2, ($radius + 2) * 2)
            $g.FillEllipse($accentBrush, $item.BadgeX - $radius, $item.BadgeY - $radius, $radius * 2, $radius * 2)
            $box = New-Object System.Drawing.RectangleF ($item.BadgeX - $radius), ($item.BadgeY - $radius + 0.5), ($radius * 2), ($radius * 2)
            $g.DrawString([string]$item.Annotation.number, $font, [System.Drawing.Brushes]::White, $box, $format)
        }
    }
    finally {
        $format.Dispose(); $font.Dispose(); $haloBrush.Dispose(); $accentBrush.Dispose(); $g.Dispose()
    }
}
