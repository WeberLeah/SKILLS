param(
    [string]$Port = 'COM4',
    [string]$Bin,
    [int]$Baud = 9600,
    [int]$BootCaptureSec = 45
)
# UART BootLoader upgrade for the water sampler APP.
# Flow: reboot -> "firmware upgrade" command -> stream APP.bin (1024B frames)
#       -> wait 12s for commit/jump -> capture boot log (default 45s).
$ErrorActionPreference = 'Stop'
if (-not $Bin) { $Bin = Join-Path (Get-Location).Path 'Template\Project-L431\Temaple\APP.bin' }
if (-not (Test-Path $Bin)) { throw ("BIN not found: " + $Bin) }

$data = [IO.File]::ReadAllBytes($Bin)
$spw  = [BitConverter]::ToUInt32($data, 0)
$rv   = [BitConverter]::ToUInt32($data, 4)
Write-Output ("BIN bytes={0} SP=0x{1:X8} Reset=0x{2:X8}" -f $data.Length, $spw, $rv)
if (($spw -band 0xFFFF0000) -ne 0x20000000) { throw 'SP not in SRAM range (0x2000xxxx)' }
if ($rv -lt 0x08007000 -or $rv -gt 0x08040000) { throw 'Reset vector outside APP range' }

$enc = [Text.Encoding]::GetEncoding(936)
$cmdReboot  = [string]::new([char[]]@(0x91CD, 0x542F))
$cmdUpgrade = [string]::new([char[]]@(0x56FA, 0x4EF6, 0x5347, 0x7EA7))

$sp = New-Object System.IO.Ports.SerialPort $Port, $Baud, None, 8, One
$sp.Encoding = $enc
$sp.ReadTimeout = 100
$sp.WriteTimeout = 10000
$sp.WriteBufferSize = 4096
$sp.Open()

$logPath = Join-Path $env:TEMP ('uart_upgrade_' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$sw = [IO.StreamWriter]::new($logPath, $false, $enc)

function SendS([string]$s) {
    $b = $enc.GetBytes($s)
    $sp.Write($b, 0, $b.Length)
}
function Pump([int]$ms) {
    $until = (Get-Date).AddMilliseconds($ms)
    while ((Get-Date) -lt $until) {
        try { $s = $sp.ReadExisting() } catch { $s = '' }
        if ($s.Length -gt 0) { $sw.Write($s); $sw.Flush() }
        Start-Sleep -Milliseconds 30
    }
}

$sw.WriteLine('=== reboot ==='); $sw.Flush()
SendS $cmdReboot
Start-Sleep -Milliseconds 1200

$sw.WriteLine('=== upgrade cmd ==='); $sw.Flush()
SendS $cmdUpgrade
Pump 2000

$sw.WriteLine('=== stream ' + $data.Length + ' bytes ==='); $sw.Flush()
$t0 = Get-Date
$chunk = 1024
for ($i = 0; $i -lt $data.Length; $i += $chunk) {
    $n = [Math]::Min($chunk, $data.Length - $i)
    $sp.Write($data, $i, $n)
}
$sw.WriteLine('stream done in ' + [int]((Get-Date) - $t0).TotalSeconds + 's'); $sw.Flush()

Pump 12000
$sw.WriteLine('=== boot log ==='); $sw.Flush()
Pump ($BootCaptureSec * 1000)

$sp.Close()
$sw.Close()
Write-Output ('UPGRADE DONE -> ' + $logPath)
