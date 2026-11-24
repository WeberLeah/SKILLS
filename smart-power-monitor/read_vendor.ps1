param(
    [int]$IntervalMs = 500,
    [int]$Seconds = 0,
    [string]$Csv = '',
    [string]$ProcessName = 'Smart_power_monitor'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "未找到进程 $ProcessName，请先打开厂商软件"; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Output "未找到 $ProcessName 的主窗口"; exit 1 }

$allCond = [System.Windows.Automation.Condition]::TrueCondition

# 显示区是三个「数值 + 单位」行（V / mA / mW）。
# 控件 AutomationId 每次启动都会变，所以按「同一行的单位 + 它左边的数值」来配对。
function Read-Display {
    $all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $allCond)
    $units = New-Object System.Collections.ArrayList
    $nums  = New-Object System.Collections.ArrayList
    foreach ($e in $all) {
        try {
            $n = $e.Current.Name
            if ([string]::IsNullOrWhiteSpace($n)) { continue }
            $r = $e.Current.BoundingRectangle
            if ($r.Width -le 0 -or $r.Height -le 0) { continue }
            $midY = $r.Y + ($r.Height / 2.0)
            if ($n -match '^(n|µ|u|m)?(A|V|W)$') {
                [void]$units.Add([pscustomobject]@{ U = $n; X = $r.X; Y = $midY })
            } elseif ($n -match '^-?[0-9]+(\.[0-9]+)?$') {
                [void]$nums.Add([pscustomobject]@{ N = [double]$n; X = $r.X; Y = $midY })
            }
        } catch { }
    }

    $res = @{ V = $null; I = $null; P = $null; Vu = ''; Iu = ''; Pu = '' }
    foreach ($u in $units) {
        $cand = $nums | Where-Object { [Math]::Abs($_.Y - $u.Y) -le 8 -and $_.X -lt $u.X } |
                Sort-Object X -Descending | Select-Object -First 1
        if (-not $cand) { continue }
        if ($u.U -match 'A$' -and $null -eq $res.I) { $res.I = $cand.N; $res.Iu = $u.U }
        elseif ($u.U -match 'V$' -and $null -eq $res.V) { $res.V = $cand.N; $res.Vu = $u.U }
        elseif ($u.U -match 'W$' -and $null -eq $res.P) { $res.P = $cand.N; $res.Pu = $u.U }
    }
    return $res
}

function Scale([double]$x, [string]$u) {
    switch -Regex ($u) {
        '^n' { return $x * 1e-9 }
        '^(µ|u)' { return $x * 1e-6 }
        '^m' { return $x * 1e-3 }
        default { return $x }
    }
}

function FmtA([double]$a) {
    $s = [Math]::Abs($a)
    if ($s -eq 0) { return '0 A' }
    if ($s -lt 1e-6) { return ('{0:F2} nA' -f ($a * 1e9)) }
    if ($s -lt 1e-3) { return ('{0:F2} µA' -f ($a * 1e6)) }
    if ($s -lt 1)    { return ('{0:F4} mA' -f ($a * 1e3)) }
    return ('{0:F4} A' -f $a)
}

if ($Csv) {
    $dir = Split-Path -Parent $Csv
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    Set-Content -Path $Csv -Value 'time,Current_A,Voltage_V,Power_W' -Encoding UTF8
}

Write-Output ("设备: " + $win.Current.Name + "   (PID " + $proc.Id + ")")
Write-Output 'Ctrl+C 结束'
Write-Output ''
Write-Output ('{0,-13} {1,14} {2,12} {3,12}' -f '时间', '电流', '电压', '功率')

$t0 = Get-Date
$n = 0
while ($true) {
    $d = Read-Display
    $i = if ($null -ne $d.I) { Scale $d.I $d.Iu } else { 0.0 }
    $v = if ($null -ne $d.V) { Scale $d.V $d.Vu } else { 0.0 }
    $p = if ($null -ne $d.P) { Scale $d.P $d.Pu } else { 0.0 }
    $now = Get-Date
    Write-Output ('{0,-13} {1,14} {2,12} {3,12}' -f $now.ToString('HH:mm:ss.fff'), (FmtA $i), ('{0:F4} V' -f $v), ('{0:F4} mW' -f ($p * 1000)))
    if ($Csv) { Add-Content -Path $Csv -Value ('{0},{1},{2},{3}' -f $now.ToString('HH:mm:ss.fff'), $i, $v, $p) -Encoding UTF8 }
    $n++
    if ($Seconds -gt 0 -and ((Get-Date) - $t0).TotalSeconds -ge $Seconds) { break }
    Start-Sleep -Milliseconds $IntervalMs
}
Write-Output ("采样 {0} 次，用时 {1:F1}s" -f $n, ((Get-Date) - $t0).TotalSeconds)
