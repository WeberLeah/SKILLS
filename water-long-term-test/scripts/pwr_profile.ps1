param(
    [int]$Seconds = 90,
    [int]$IntervalMs = 300,
    [double]$ThrUa = 50,
    [double]$PumpUa = 5000,
    [string]$Tag = ''
)
# 连续采样功耗并做分布分析：中位数/最大/超阈点数/最长连续超阈窗口
$ErrorActionPreference = 'Stop'
$skill = "$env:USERPROFILE\.claude\skills\smart-power-monitor\read_vendor.ps1"
$ps    = Join-Path $env:SystemRoot 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
$csv   = Join-Path $env:TEMP ('pwrprof_' + (Get-Date -Format 'HHmmss') + $(if ($Tag) { "_$Tag" } else { '' }) + '.csv')

& $ps -NoProfile -ExecutionPolicy Bypass -File $skill -Seconds $Seconds -IntervalMs $IntervalMs -Csv $csv | Out-Null
if (-not (Test-Path $csv)) { Write-Output 'NODATA'; exit 1 }

$rows = @()
foreach ($r in [IO.File]::ReadAllLines($csv)) {
    if ($r -notmatch '^[0-9]') { continue }
    $c = $r.Split(',')
    if ($c.Count -lt 3) { continue }
    $a = 0.0; $v = 0.0
    if ([double]::TryParse($c[1], [ref]$a) -and [double]::TryParse($c[2], [ref]$v)) {
        $rows += [pscustomobject]@{ T = $c[0]; I = $a * 1e6; V = $v }
    }
}
if ($rows.Count -eq 0) { Write-Output 'NODATA'; exit 1 }

$pump = @($rows | Where-Object { $_.I -ge $PumpUa })
$rows = @($rows | Where-Object { $_.I -lt $PumpUa })
if ($rows.Count -eq 0) { Write-Output 'NODATA (only pump samples)'; exit 1 }

$vals = @($rows | ForEach-Object { $_.I })
$sorted = @($vals | Sort-Object)
$med = $sorted[[int][Math]::Floor($sorted.Count / 2)]
$p90 = $sorted[[int][Math]::Floor($sorted.Count * 0.9)]
$max = $sorted[$sorted.Count - 1]
$min = $sorted[0]
$high = @($rows | Where-Object { $_.I -ge $ThrUa })

# 最长连续超阈窗口（泵水样本已被剔除，跨大间隙不算连续）
$best = $null; $cur = $null; $prevT = $null
function Close-Run {
    if ($script:cur) {
        if (-not $script:best -or $script:cur.N -gt $script:best.N) { $script:best = $script:cur }
        $script:cur = $null
    }
}
for ($i = 0; $i -lt $rows.Count; $i++) {
    $t = [TimeSpan]::Parse($rows[$i].T)
    if ($prevT -and (($t - $prevT).TotalMilliseconds -gt 3 * $IntervalMs)) { Close-Run }
    $prevT = $t
    if ($rows[$i].I -ge $ThrUa) {
        if (-not $cur) { $cur = [pscustomobject]@{ Start = $rows[$i].T; N = 0; Sum = 0.0 } }
        $cur.N++; $cur.Sum += $rows[$i].I
    } else {
        Close-Run
    }
}
Close-Run

$line = 'N=' + $rows.Count + ' MED=' + ('{0:F1}' -f $med) + 'uA P90=' + ('{0:F1}' -f $p90)
$line += ' MIN=' + ('{0:F1}' -f $min) + ' MAX=' + ('{0:F1}' -f $max) + 'uA'
$line += ' HIGH=' + $high.Count + '/' + $rows.Count + ' PUMP=' + $pump.Count
if ($best) {
    $dur = [double]$best.N * $IntervalMs / 1000.0
    $line += ' RUN=' + $best.Start + ' dur=' + ('{0:F1}' -f $dur) + 's avg=' + ('{0:F1}' -f ($best.Sum / $best.N)) + 'uA'
}
$line += ' CSV=' + $csv
Write-Output $line

# 超阈点的时间序列（最多 40 条），便于看周期性
if ($high.Count -gt 0) {
    $show = $high | Select-Object -First 40 | ForEach-Object { $_.T + '=' + ('{0:F0}' -f $_.I) }
    Write-Output ('HIGH_SAMPLES ' + ($show -join ' '))
}
