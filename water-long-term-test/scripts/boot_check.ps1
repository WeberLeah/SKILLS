param(
    [string]$Port = 'COM4',
    [int]$Seconds = 45,
    [string]$OutDir = $env:TEMP
)
# 重启设备并抓启动日志，核对 LSE 起振结果。
# 输出一行：BOOT_LSE=OK|FAIL|UNKNOWN  LOG=<文件>
$ErrorActionPreference = 'Stop'
$enc  = [Text.Encoding]::GetEncoding(936)
$cmdInfo  = [string]::new([char[]]@(0x7CFB,0x7EDF,0x4FE1,0x606F))
$cmdReset = [string]::new([char[]]@(0x91CD,0x542F))
$readyMark= [string]::new([char[]]@(0x672C,0x673A,0x4FE1,0x606F))
$lseOk    = [string]::new([char[]]@(0x4C,0x53,0x45,0x8D77,0x632F,0x6210,0x529F))
$lseFail  = [string]::new([char[]]@(0x4C,0x53,0x45,0x8D77,0x632F,0x5931,0x8D25))

$logPath = Join-Path $OutDir ('boot_' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$sp = New-Object System.IO.Ports.SerialPort $Port,9600,None,8,One
$sp.Encoding = $enc; $sp.ReadTimeout = 100; $sp.WriteTimeout = 10000
$sp.Open()
$sw = [IO.StreamWriter]::new($logPath, $false, $enc)

function SendS([string]$s) { $b = $enc.GetBytes($s); $sp.Write($b,0,$b.Length) }
function CaptureFor([int]$ms) {
    $buf = ''
    $u = (Get-Date).AddMilliseconds($ms)
    while ((Get-Date) -lt $u) {
        try { $s = $sp.ReadExisting() } catch { $s = '' }
        if ($s) { $sw.Write($s); $sw.Flush(); $buf += $s }
        Start-Sleep -Milliseconds 30
    }
    return $buf
}

# 先确认设备在线（休眠时靠重发覆盖唤醒窗口）
$ready = $false
for ($i = 0; $i -lt 20; $i++) {
    SendS $cmdInfo
    $r = CaptureFor 2500
    if ($r.Contains($readyMark)) { $ready = $true; break }
}
if (-not $ready) {
    $sw.Close(); $sp.Close()
    Write-Output ('BOOT_LSE=UNKNOWN LOG=' + $logPath + ' (device not responding)')
    exit 1
}

SendS $cmdReset
Start-Sleep -Milliseconds 800
$boot = CaptureFor ($Seconds * 1000)
$sw.Close(); $sp.Close()

$lse = 'UNKNOWN'
if ($boot.Contains($lseFail)) { $lse = 'FAIL' }
elseif ($boot.Contains($lseOk)) { $lse = 'OK' }
Write-Output ('BOOT_LSE=' + $lse + ' LOG=' + $logPath)
