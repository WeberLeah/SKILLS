param(
    [string]$Port = 'COM4',
    [int]$Baud = 9600,
    [byte]$Slave = 5,
    [ValidateSet('input', 'holding')][string]$Mode = 'input',
    [int]$WakeRetrySec = 12
)
# Modbus RTU read for the water sampler.
#   input   : 0x04, PDU start 0x0000, 30 registers (full map, battery first)
#   holding : 0x03, PDU start 0x004E, 6 registers (78..83, Modbus addr at 80)
# NOTE: FreeMODBUS increments the PDU address, so PDU address N maps to
#       register #(N+1). Reading input from PDU 0x0001 would shift by one.
$ErrorActionPreference = 'Stop'

function CRC([byte[]]$b) {
    $c = 0xFFFF
    foreach ($x in $b) {
        $c = $c -bxor $x
        for ($i = 0; $i -lt 8; $i++) {
            if ($c -band 1) { $c = ($c -shr 1) -bxor 0xA001 } else { $c = $c -shr 1 }
        }
    }
    return $c
}
function F32([byte[]]$f, [int]$o) {
    $x = [byte[]]@($f[$o + 3], $f[$o + 2], $f[$o + 1], $f[$o])
    return [BitConverter]::ToSingle($x, 0)
}
function U16([byte[]]$f, [int]$o) {
    return ([int]$f[$o] -shl 8) -bor [int]$f[$o + 1]
}
function U32([byte[]]$f, [int]$o) {
    return ([uint32]$f[$o] -shl 24) -bor ([uint32]$f[$o + 1] -shl 16) -bor ([uint32]$f[$o + 2] -shl 8) -bor [uint32]$f[$o + 3]
}

if ($Mode -eq 'input') {
    $req = [byte[]]($Slave, 0x04, 0x00, 0x00, 0x00, 0x1E, 0, 0)
    $byteCount = 0x3C
} else {
    $req = [byte[]]($Slave, 0x03, 0x00, 0x4E, 0x00, 0x06, 0, 0)
    $byteCount = 0x0C
}
$c = CRC $req[0..5]
$req[6] = $c -band 0xFF
$req[7] = ($c -shr 8) -band 0xFF

$sp = New-Object System.IO.Ports.SerialPort $Port, $Baud, None, 8, One
$sp.ReadTimeout = 200
$sp.WriteTimeout = 5000
$sp.Open()

# The device sleeps in STOP2 when idle. On this board LSE does not start, so
# the LPUART start-bit wake is unavailable and a single request can be lost.
# Retry across at least one RX8025 wake cycle (10s) to catch an awake window.
$all = New-Object System.Collections.Generic.List[byte]
$expected = 3 + $byteCount + 2
$deadline = (Get-Date).AddSeconds($WakeRetrySec)
do {
    $sp.Write($req, 0, 8)
    Start-Sleep -Milliseconds 250
    while ($sp.BytesToRead -gt 0) {
        $n = $sp.BytesToRead
        $b = New-Object byte[] $n
        $r = $sp.Read($b, 0, $n)
        if ($r -gt 0) { $all.AddRange([byte[]]$b[0..($r - 1)]) }
    }
    if ($all.Count -ge $expected) { break }
    Start-Sleep -Milliseconds 150
} while ((Get-Date) -lt $deadline)
$sp.Close()

Write-Output ('RX ' + $all.Count + ' bytes: ' + (($all | ForEach-Object { $_.ToString('X2') }) -join ' '))

$found = $false
for ($i = 0; $i -le $all.Count - $expected; $i++) {
    if ($all[$i] -eq $Slave -and $all[$i + 1] -eq $req[1] -and $all[$i + 2] -eq $byteCount) {
        $f = [byte[]]$all.GetRange($i, $expected).ToArray()
        $cc = CRC $f[0..($expected - 3)]
        if (($cc -band 0xFF) -eq $f[$expected - 2] -and (($cc -shr 8) -band 0xFF) -eq $f[$expected - 1]) {
            $found = $true
            Write-Output ('CRC OK @' + $i)
            if ($Mode -eq 'input') {
                Write-Output ('battery      = {0:F2} V'   -f (F32 $f 3))
                Write-Output ('current      = {0:F3} A'   -f (F32 $f 7))
                Write-Output ('temperature  = {0:F2} C'   -f (F32 $f 11))
                Write-Output ('humidity     = {0:F2} %RH' -f (F32 $f 15))
                Write-Output ('angle X/Y/Z  = {0:F2} / {1:F2} / {2:F2} deg' -f (F32 $f 19), (F32 $f 23), (F32 $f 27))
                Write-Output ('cap raw={0} (1=dry,0=water) check={1} pipe={2} bottle={3} lastResult={4}' -f (U16 $f 31), (U16 $f 33), (U16 $f 35), (U16 $f 37), (U16 $f 39))
                Write-Output ('success={0} fail={1} trigger={2}' -f (U32 $f 41), (U32 $f 45), (U16 $f 49))
                Write-Output ('modbusAddr={0} deviceAddr={1}' -f (U16 $f 51), (U32 $f 53))
                Write-Output ('outTime={0}s inTime={1}s sht20Comm={2}' -f (U16 $f 57), (U16 $f 59), (U16 $f 61))
            } else {
                Write-Output ('hold78={0} hold79={1} modbusAddr={2} hold81={3} trigger={4} hold83={5}' -f (U16 $f 3), (U16 $f 5), (U16 $f 7), (U16 $f 9), (U16 $f 11), (U16 $f 13))
            }
            break
        } else {
            Write-Output ('frame @' + $i + ' CRC bad')
        }
    }
}
if (-not $found) { Write-Output 'NO VALID RESPONSE' }
