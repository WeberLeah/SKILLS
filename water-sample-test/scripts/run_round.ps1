param(
    [string]$Port = 'COM4',
    [int]$Baud = 9600,
    [int]$Rounds = 1,
    [int]$MaxCycleSec = 260,
    [ValidateSet('on', 'off', 'keep')][string]$Debug = 'on',
    [int]$ReadyTimeoutSec = 60
)
# Wait until APP is responsive, optionally toggle DEBUG, trigger N water
# sample rounds, and capture logs to %TEMP%\water_round_<ts>.log (GBK).
#
# -Debug on   : send the debug toggle once. The command is a TOGGLE; after a
#               fresh boot the flag is 0, so "on" turns DEBUG output on.
# -Debug off  : send the toggle once (assumes it is currently on).
# -Debug keep : do not send it.
$ErrorActionPreference = 'Stop'
$enc = [Text.Encoding]::GetEncoding(936)

$cmdInfo    = [string]::new([char[]]@(0x7CFB, 0x7EDF, 0x4FE1, 0x606F))
$cmdDebug   = [string]::new([char[]]@(0x8C03, 0x8BD5, 0x4FE1, 0x606F, 0x8F93, 0x51FA, 0x5F00, 0x5173))
$cmdTrigger = [string]::new([char[]]@(0x8BBE, 0x7F6E, 0x89E6, 0x53D1, 0x7559, 0x6837, 0x3A, 0x31))
$readyMark  = [string]::new([char[]]@(0x672C, 0x673A, 0x4FE1, 0x606F))
$doneMark   = [string]::new([char[]]@(0x7559, 0x6837, 0x6D41, 0x7A0B, 0x7ED3, 0x675F))
$succMark   = [string]::new([char[]]@(0x53D6, 0x6C34, 0x7ED3, 0x679C, 0xFF1A, 0x6210, 0x529F))
$failMark   = [string]::new([char[]]@(0x53D6, 0x6C34, 0x7ED3, 0x679C, 0xFF1A, 0x5931, 0x8D25))

$sp = New-Object System.IO.Ports.SerialPort $Port, $Baud, None, 8, One
$sp.Encoding = $enc
$sp.ReadTimeout = 100
$sp.WriteTimeout = 10000
$sp.WriteBufferSize = 4096
$sp.Open()

$logPath = Join-Path $env:TEMP ('water_round_' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$sw = [IO.StreamWriter]::new($logPath, $false, $enc)

function SendS([string]$s) {
    $b = $enc.GetBytes($s)
    $sp.Write($b, 0, $b.Length)
}
function CaptureFor([int]$ms) {
    $buf = ''
    $until = (Get-Date).AddMilliseconds($ms)
    while ((Get-Date) -lt $until) {
        try { $s = $sp.ReadExisting() } catch { $s = '' }
        if ($s.Length -gt 0) { $sw.Write($s); $sw.Flush(); $buf += $s }
        Start-Sleep -Milliseconds 30
    }
    return $buf
}

# 1) Wait for the APP to answer "system info" (boot drain can take ~30s).
$ready = $false
for ($t = 0; $t -le $ReadyTimeoutSec; $t += 3) {
    SendS $cmdInfo
    $r = CaptureFor 3000
    if ($r.Contains($readyMark)) { $ready = $true; break }
}
if (-not $ready) {
    $sw.Close(); $sp.Close()
    throw ('APP not responding on ' + $Port + ' within ' + $ReadyTimeoutSec + 's')
}
Write-Output ('APP ready, log=' + $logPath)

# 2) Optional DEBUG toggle.
if ($Debug -ne 'keep') {
    SendS $cmdDebug
    [void](CaptureFor 2500)
    Write-Output ('DEBUG toggle sent (-Debug ' + $Debug + ')')
}

# 3) Trigger rounds.
for ($r = 1; $r -le $Rounds; $r++) {
    $sw.WriteLine('### round ' + $r + ' trigger ###'); $sw.Flush()
    SendS $cmdTrigger
    [void](CaptureFor 500)

    $buf = ''
    $ok = $false
    $until = (Get-Date).AddSeconds($MaxCycleSec)
    while ((Get-Date) -lt $until) {
        try { $s = $sp.ReadExisting() } catch { $s = '' }
        if ($s.Length -gt 0) {
            $sw.Write($s); $sw.Flush(); $buf += $s
            if ($buf.Length -gt 6000) { $buf = $buf.Substring($buf.Length - 3000) }
        }
        if ($buf.Contains($doneMark)) { $ok = $true; break }
        Start-Sleep -Milliseconds 30
    }
    [void](CaptureFor 1500)
    SendS $cmdInfo
    [void](CaptureFor 2500)
    Write-Output ('round ' + $r + ': done=' + $ok)
}

$sp.Close()
$sw.Close()

$txt = [IO.File]::ReadAllText($logPath, $enc)
$succ = [regex]::Matches($txt, [regex]::Escape($succMark)).Count
$fail = [regex]::Matches($txt, [regex]::Escape($failMark)).Count
Write-Output ('SUMMARY success=' + $succ + ' fail=' + $fail + ' log=' + $logPath)
