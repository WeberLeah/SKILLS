param(
    [int]$Seconds = 4,
    [int]$IntervalMs = 300,
    [string]$SkillScript = "$env:USERPROFILE\.claude\skills\smart-power-monitor\read_vendor.ps1"
)
# 通过厂商上位机界面读功耗，输出单行 ASCII：
#   I_UA=<中位数电流> V=<中位数电压> P_MW=<功率> N=<样本数>
$ErrorActionPreference = 'Stop'
$tmp = Join-Path $env:TEMP ('pwr_' + [guid]::NewGuid().ToString('N') + '.csv')
$ps  = Join-Path $env:SystemRoot 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'

& $ps -NoProfile -ExecutionPolicy Bypass -File $SkillScript -Seconds $Seconds -IntervalMs $IntervalMs -Csv $tmp | Out-Null

if (-not (Test-Path $tmp)) { Write-Output 'I_UA=NA V=NA P_MW=NA N=0'; exit 1 }
$rows = [IO.File]::ReadAllLines($tmp) | Select-Object -Skip 1 | Where-Object { $_ -match '^[0-9]' }
Remove-Item $tmp -Force -ErrorAction SilentlyContinue

$cur = @(); $vol = @()
foreach ($r in $rows) {
    $c = $r.Split(',')
    if ($c.Count -lt 4) { continue }
    $x = 0.0; $y = 0.0
    if ([double]::TryParse($c[1], [ref]$x) -and [double]::TryParse($c[2], [ref]$y)) { $cur += $x; $vol += $y }
}
if ($cur.Count -eq 0) { Write-Output 'I_UA=NA V=NA P_MW=NA N=0'; exit 1 }

$cs = $cur | Sort-Object
$vs = $vol | Sort-Object
$med = $cs[[int][Math]::Floor($cs.Count / 2)]
$medv = $vs[[int][Math]::Floor($vs.Count / 2)]
Write-Output ('I_UA=' + ('{0:F1}' -f ($med * 1e6)) +
              ' V=' + ('{0:F3}' -f $medv) +
              ' P_MW=' + ('{0:F3}' -f ($med * $medv * 1000)) +
              ' N=' + $cur.Count)
