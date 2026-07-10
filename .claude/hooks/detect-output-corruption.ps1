# Stop hook: 出力破損の機械検知（殿裁定2026-07-10、「異常を知る」仕組み・案3）
# ターン終了時にtranscriptの現在ターンassistantテキストを走査し、破損パターンを検知したら
# decision:block で警告をモデル自身の文脈へ返す（モデルは自分の出力を見ていないため、
# 自覚に頼らず機械で「異常を知らせる」のが本hookの狙い）。
# 検知パターンの追加はこのファイルの $findings 判定群へ追記する。
# 失敗時は常に静かに exit 0（ハーネスを壊さない fail-open 設計）。
$ErrorActionPreference = 'SilentlyContinue'
try {
    $stdin = [Console]::In.ReadToEnd()
    if (-not $stdin) { exit 0 }
    $hookInput = $null
    try { $hookInput = $stdin | ConvertFrom-Json } catch { exit 0 }
    if (-not $hookInput) { exit 0 }
    # 無限ループ防止: 本hookのblockで継続した後の再Stopでは再走査しない
    if ($hookInput.stop_hook_active) { exit 0 }
    $tp = $hookInput.transcript_path
    if (-not $tp -or -not (Test-Path -LiteralPath $tp)) { exit 0 }

    # 現在ターンのassistantテキストのみを対象にする:
    # 最後の「素のユーザープロンプト」(tool_resultを含まないuserエントリ)以降を収集
    $lines = Get-Content -LiteralPath $tp -Tail 800 -Encoding UTF8
    $buffer = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if (-not $line) { continue }
        $e = $null
        try { $e = $line | ConvertFrom-Json } catch { continue }
        if (-not $e -or -not $e.type) { continue }
        if ($e.type -eq 'user') {
            $c = $e.message.content
            $isRealUser = $false
            if ($c -is [string]) { $isRealUser = $true }
            elseif ($c) {
                $hasToolResult = $false
                foreach ($b in $c) { if ($b.type -eq 'tool_result') { $hasToolResult = $true; break } }
                if (-not $hasToolResult) { $isRealUser = $true }
            }
            if ($isRealUser) { $buffer.Clear() }
        }
        elseif ($e.type -eq 'assistant') {
            $c = $e.message.content
            if ($c) {
                foreach ($b in $c) {
                    if ($b.type -eq 'text' -and $b.text) { [void]$buffer.Add($b.text) }
                }
            }
        }
    }
    if ($buffer.Count -eq 0) { exit 0 }
    $joined = [string]::Join("`n", $buffer)

    $findings = New-Object System.Collections.Generic.List[string]
    # #1型: 地の文へのcourse/court混入（直後が日本語句読点＝英文の正当なcourseと区別）
    if ($joined -cmatch '(?m)(^|[\s>》」）)])(course|court)[、。]') {
        [void]$findings.Add('地の文への course/court 混入(#1型)')
    }
    # raw invoke構文のテキスト漏出
    if ($joined -match '<invoke\s+name=' -or $joined -match '</invoke>') {
        [void]$findings.Add('raw invoke構文のテキスト表示')
    }
    if ($joined -match '<') {
        [void]$findings.Add('raw antmlタグのテキスト表示')
    }
    # #2型: 偽truncation注記
    if ($joined -match 'truncated to 0\.5\s*MB') {
        [void]$findings.Add('偽truncation注記(#2型)')
    }

    if ($findings.Count -gt 0) {
        $reason = '【出力破損検知(Stop hook機械検知)】このターンのそなたの出力に破損パターンを検知した: ' `
            + ($findings -join '／') `
            + '。(1) docs-notes/output-corruption-log.md へ発生記録を1行追記せよ(同一系列の再発か否かも明記) ' `
            + '(2) 破損した発言を正しい形で言い直せ (3) 同一セッション2回目以降の発生なら ' `
            + 'long-horizon-discipline スキル§5の離脱プロトコルに従え。'
        $out = @{ decision = 'block'; reason = $reason } | ConvertTo-Json -Compress
        Write-Output $out
    }
    exit 0
}
catch { exit 0 }
