# 使い方の説明図を、定義ファイル(shots.json)から一括で作り直す（殿ご下命2026-10-09）。
#
#   pwsh tools/usage-screenshots/New-UsageShots.ps1                 # 全部作り直す
#   pwsh tools/usage-screenshots/New-UsageShots.ps1 -Only overview  # 名を指定して1枚だけ
#   pwsh tools/usage-screenshots/New-UsageShots.ps1 -Only overview -ListAnchors
#                                                                   # 注釈の的に使える要素を一覧する(絵は作らぬ)
#
# 出力は docs/usage/images/<名>.png。アプリへは再ビルドで埋め込まれる。
# 撮るのは「今ビルドされておる Debug 版」ゆえ、先に dotnet build src/Ecad2.App を済ませること。
#
# 【殿の作業を妨げぬ範囲】撮影(PrintWindow)と UIA のボタン操作は、ウィンドウを前面に出さず
# マウスも動かさぬ。セカンダリモニタに撮影用のウィンドウが数秒現れるのみ。
# キー送出や実マウスが要る絵(メニューを開いた姿、ドラッグ中の姿など)は定義に "intrusive": true を
# 付け、-AllowIntrusive を明示した時だけ作る——その間は殿の画面とマウスを借りる。

[CmdletBinding()]
param(
    [string[]]$Only,
    [switch]$ListAnchors,
    [switch]$AllowIntrusive
)

$ErrorActionPreference = 'Stop'
$toolDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $toolDir '..\..')).Path
. (Join-Path $toolDir 'UsageShots.ps1')

$exePath = Join-Path $repoRoot 'src\Ecad2.App\bin\Debug\net10.0-windows\Ecad2.App.exe'
$outputDir = Join-Path $repoRoot 'docs\usage\images'
$definition = Get-Content (Join-Path $toolDir 'shots.json') -Raw -Encoding utf8 | ConvertFrom-Json
New-Item -ItemType Directory -Force $outputDir | Out-Null

# 殿のレイアウトの退避(UsageShots.ps1 冒頭の取り決め)。強制終了しか使わぬゆえ書き換わらぬ筈だが、
# 書き換わっても画面上は何も起きず気づけぬ。ハッシュで確かめ、変わっておれば戻す。
$layoutPath = Join-Path $env:APPDATA 'Ecad2\docking-layout\main-layout.xml'
$layoutBackup = $null
$layoutHash = $null
if (Test-Path $layoutPath) {
    $layoutBackup = Join-Path ([System.IO.Path]::GetTempPath()) "ecad2-usage-shots-main-layout-$PID.xml"
    Copy-Item $layoutPath $layoutBackup -Force
    $layoutHash = (Get-FileHash $layoutPath -Algorithm SHA256).Hash
}

function Invoke-UsageShotStep {
    param([Parameter(Mandatory)]$Step, [Parameter(Mandatory)][IntPtr]$Handle)
    switch ($Step.action) {
        'invoke' {
            # UIA の InvokePattern。前面化もマウス移動も伴わぬ。
            $root = [System.Windows.Automation.AutomationElement]::FromHandle($Handle)
            $property = if ($Step.automationId) { [System.Windows.Automation.AutomationElement]::AutomationIdProperty } else { [System.Windows.Automation.AutomationElement]::NameProperty }
            $value = if ($Step.automationId) { $Step.automationId } else { $Step.name }
            $condition = New-Object System.Windows.Automation.PropertyCondition $property, $value
            $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if (-not $element) { throw "操作の相手が見つからぬ: $value" }
            $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        }
        'wait' { }
        default { throw "未知の操作: $($Step.action)" }
    }
    Start-Sleep -Milliseconds $(if ($Step.waitMs) { [int]$Step.waitMs } else { 600 })
}

$made = @()
try {
    foreach ($shot in $definition.shots) {
        if ($Only -and $Only -notcontains $shot.name) { continue }
        if ($shot.intrusive -and -not $AllowIntrusive) {
            Write-Host "skip  $($shot.name)  (画面とマウスを借りる絵。-AllowIntrusive で作る)"
            continue
        }

        $document = if ($shot.document) { Join-Path $toolDir $shot.document } else { $null }
        $width = if ($shot.window.width) { [int]$shot.window.width } else { [int]$definition.window.width }
        $height = if ($shot.window.height) { [int]$shot.window.height } else { [int]$definition.window.height }
        $process = Start-UsageShotApp -ExePath $exePath -DocumentPath $document -Width $width -Height $height
        try {
            $handle = $process.MainWindowHandle
            foreach ($step in @($shot.steps)) { if ($step) { Invoke-UsageShotStep -Step $step -Handle $handle } }

            $anchors = @(Get-UsageShotAnchors -Handle $handle)
            if ($ListAnchors) {
                $anchors | Sort-Object Y, X | Format-Table ControlType, AutomationId, Name, X, Y, Width, Height -AutoSize | Out-String -Width 220 | Write-Host
                continue
            }

            $bitmap = Get-UsageShotBitmap -Handle $handle
            try {
                if ($shot.annotations) { Add-UsageShotAnnotations -Bitmap $bitmap -Annotations $shot.annotations -Anchors $anchors }
                $final = $bitmap
                if ($shot.crop) {
                    # 絵の一部だけを載せる場合。注釈を焼いた後で切るゆえ、注釈の座標は切る前の絵のまま書ける。
                    $area = Resolve-UsageShotTarget -Target $shot.crop.target -Anchors $anchors
                    if (-not $area.Rect) { throw "$($shot.name): crop の的は要素で指定すること" }
                    $pad = if ($null -ne $shot.crop.padding) { [int]$shot.crop.padding } else { 12 }
                    $left = [Math]::Max(0, [int]$area.Rect.X - $pad); $top = [Math]::Max(0, [int]$area.Rect.Y - $pad)
                    $right = [Math]::Min($bitmap.Width, [int]($area.Rect.X + $area.Rect.Width) + $pad)
                    $bottom = [Math]::Min($bitmap.Height, [int]($area.Rect.Y + $area.Rect.Height) + $pad)
                    $final = $bitmap.Clone((New-Object System.Drawing.Rectangle $left, $top, ($right - $left), ($bottom - $top)), $bitmap.PixelFormat)
                    $final.SetResolution(96, 96)
                }
                $path = Join-Path $outputDir "$($shot.name).png"
                $final.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Host ("made  {0}  {1}x{2}  {3:N0} bytes" -f $shot.name, $final.Width, $final.Height, (Get-Item $path).Length)
                $made += $path
                if (-not [object]::ReferenceEquals($final, $bitmap)) { $final.Dispose() }
            }
            finally { $bitmap.Dispose() }
        }
        finally {
            Stop-UsageShotApp -Process $process -ExePath $exePath
        }
    }
}
finally {
    if ($layoutBackup) {
        $after = if (Test-Path $layoutPath) { (Get-FileHash $layoutPath -Algorithm SHA256).Hash } else { '' }
        if ($after -ne $layoutHash) {
            Copy-Item $layoutBackup $layoutPath -Force
            Write-Warning 'main-layout.xml が撮影中に書き換わっておった。退避から戻した。'
        }
        Remove-Item $layoutBackup -Force -ErrorAction SilentlyContinue
    }
}
