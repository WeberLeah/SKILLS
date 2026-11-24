param(
    [string]$Port = 'COM4',
    [string]$Until = '17:30',
    [int]$IntervalSec = 900,
    [int]$MaxRounds = 200,
    [int]$RoundBase = 0,
    [string]$PauseStart = '',
    [string]$PauseEnd = '',
    [int]$PowerCheck = 1,
    [int]$PowerLimitUa = 30,
    [int]$MaxCycleSec = 280,
    [string]$PowerScript = "$env:USERPROFILE\.claude\skills\water-long-term-test\scripts\read_power.ps1",
    [string]$SkillDir = "$env:USERPROFILE\.claude\skills\water-sample-test",
    [string]$StopFlag = "$env:TEMP\water_repeat_stop.flag"
)
# 微功耗留样仪长期测试驱动：每 IntervalSec 秒触发一轮取水，逐轮分类统计，
# 轮间读功耗，静默窗口做健康巡检，直到 Until / MaxRounds / 停止标志。
$ErrorActionPreference = 'Stop'
$enc    = [Text.Encoding]::GetEncoding(936)
$utf8   = New-Object Text.UTF8Encoding($true)
$rr     = Join-Path $SkillDir 'scripts\run_round.ps1'
$stamp  = Get-Date -Format 'yyyyMMdd'
$csv    = Join-Path $env:TEMP ("water_repeat_$stamp.csv")
$status = Join-Path $env:TEMP 'water_repeat_status.log'
$deadline = [datetime]::Parse((Get-Date).ToString('yyyy-MM-dd') + ' ' + $Until)
$pauseS = $null; $pauseE = $null
if ($PauseStart) { $pauseS = [datetime]::Parse((Get-Date).ToString('yyyy-MM-dd') + ' ' + $PauseStart) }
if ($PauseEnd)   { $pauseE = [datetime]::Parse((Get-Date).ToString('yyyy-MM-dd') + ' ' + $PauseEnd) }

if (-not (Test-Path $rr)) { throw ('run_round.ps1 not found: ' + $rr) }
try {
    $sp0 = New-Object System.IO.Ports.SerialPort $Port,9600,None,8,One
    $sp0.Open(); $sp0.Close()
} catch {
    throw ('PORT BUSY: ' + $Port + ' - 先确认另一会话已释放串口')
}

function Log([string]$line) {
    $t = '[' + (Get-Date -Format 'HH:mm:ss') + '] ' + $line
    [IO.File]::AppendAllText($status, $t + "`r`n", $utf8)
    Write-Output $line
}

# --- 确保 DEBUG 打开（开关命令会回显当前状态，可确定性地校正） ---
function Ensure-DebugOn {
    $cmdInfo  = [string]::new([char[]]@(0x7CFB,0x7EDF,0x4FE1,0x606F))
    $cmdDebug = [string]::new([char[]]@(0x8C03,0x8BD5,0x4FE1,0x606F,0x8F93,0x51FA,0x5F00,0x5173))
    $readyMark= [string]::new([char[]]@(0x672C,0x673A,0x4FE1,0x606F))
    $onMark   = [string]::new([char[]]@(0x5F00,0x542F))
    $offMark  = [string]::new([char[]]@(0x5173,0x95ED))
    $sp = New-Object System.IO.Ports.SerialPort $Port,9600,None,8,One
    $sp.Encoding = $enc; $sp.ReadTimeout = 100; $sp.WriteTimeout = 10000
    $sp.Open()
    $buf = ''
    for ($i = 0; $i -lt 20; $i++) {
        $b = $enc.GetBytes($cmdInfo); $sp.Write($b,0,$b.Length)
        $u = (Get-Date).AddMilliseconds(2500)
        while ((Get-Date) -lt $u) { try { $s = $sp.ReadExisting() } catch { $s = '' }; if ($s) { $buf += $s }; Start-Sleep -Milliseconds 30 }
        if ($buf.Contains($readyMark)) { break }
    }
    $state = 'unknown'
    for ($k = 0; $k -lt 2; $k++) {
        $b = $enc.GetBytes($cmdDebug); $sp.Write($b,0,$b.Length)
        $u = (Get-Date).AddMilliseconds(2500); $r = ''
        while ((Get-Date) -lt $u) { try { $s = $sp.ReadExisting() } catch { $s = '' }; if ($s) { $r += $s }; Start-Sleep -Milliseconds 30 }
        if ($r -match ([regex]::Escape($onMark)))  { $state = 'on';  break }
        if ($r -match ([regex]::Escape($offMark))) { $state = 'off'; continue }
    }
    $sp.Close()
    return $state
}

function Num([string]$v, [double]$def = 0) {
    $x = 0.0
    if ([double]::TryParse($v, [ref]$x)) { return $x }
    return $def
}

# 静默窗口内的健康巡检：只读“系统信息”，不触发水泵
function Poll-Health {
    $cmdInfo  = [string]::new([char[]]@(0x7CFB,0x7EDF,0x4FE1,0x606F))
    $readyMark= [string]::new([char[]]@(0x672C,0x673A,0x4FE1,0x606F))
    $sp = New-Object System.IO.Ports.SerialPort $Port,9600,None,8,One
    $sp.Encoding = $enc; $sp.ReadTimeout = 100; $sp.WriteTimeout = 10000
    $sp.Open()
    $buf = ''
    for ($i = 0; $i -lt 20; $i++) {
        $b = $enc.GetBytes($cmdInfo); $sp.Write($b,0,$b.Length)
        $u = (Get-Date).AddMilliseconds(2500)
        while ((Get-Date) -lt $u) { try { $s = $sp.ReadExisting() } catch { $s = '' }; if ($s) { $buf += $s }; Start-Sleep -Milliseconds 30 }
        if ($buf.Contains($readyMark)) { break }
    }
    $sp.Close()
    return $buf
}

$header = 'round,start,end,category,has_done,success,fail,protect,low_batt,pipe_water,fill_detected,battery_v,drain_i_min,drain_i_max,drain_v_min,drain_v_max,in_i_min,in_i_max,in_v_min,in_v_max,ang_x,ang_y,ang_z,succ_cnt,fail_cnt,temp_c,humi_rh,sleep_ua,sleep_v,log'
if (-not (Test-Path $csv)) { [IO.File]::WriteAllText($csv, $header + "`r`n", $utf8) }

Log ('START until=' + $deadline.ToString('yyyy-MM-dd HH:mm') + ' interval=' + $IntervalSec + 's port=' + $Port + ' pause=' + $PauseStart + '-' + $PauseEnd)
$dbg = Ensure-DebugOn
Log ('DEBUG state=' + $dbg)

$round = $RoundBase
$lastSc = $null; $lastFc = $null
while ($round -lt $MaxRounds -and (Get-Date) -lt $deadline -and -not (Test-Path $StopFlag)) {
    $now = Get-Date
    # 静默窗口：不触发水泵，只做健康巡检
    if ($pauseS -and $pauseE -and $now -ge $pauseS -and $now -lt $pauseE) {
        $h = Poll-Health
        $readyMark2 = [string]::new([char[]]@(0x672C,0x673A,0x4FE1,0x606F))
        $hb = 'NO_RESP'; $hcur = ''; $ht = ''; $hh = ''; $hax = ''; $hay = ''; $haz = ''; $hpipe = '?'; $hsc = ''; $hfc = ''; $hlast = ''
        $hok = $false
        if ($h.Contains($readyMark2)) {
            $hok = $true
            if ($h -match '电池电压：([0-9.]+)V') { $hb = $Matches[1] }
            if ($h -match '水泵电流：([0-9.]+)A') { $hcur = $Matches[1] }
            if ($h -match '主板温度：([0-9.]+)℃') { $ht = $Matches[1] }
            if ($h -match '主板湿度：([0-9.]+)%') { $hh = $Matches[1] }
            if ($h -match '姿态角度：X=(-?[0-9.]+)°\s+Y=(-?[0-9.]+)°\s+Z=(-?[0-9.]+)°') { $hax = $Matches[1]; $hay = $Matches[2]; $haz = $Matches[3] }
            if ($h -match '成功取水：(\d+)次') { $hsc = $Matches[1] }
            if ($h -match '失败取水：(\d+)次') { $hfc = $Matches[1] }
            if ($h -match '最近结果：(\S+)') { $hlast = $Matches[1] }
            if ($h -match '管道电容：正常') { $hpipe = 'normal' } elseif ($h -match '管道电容：异常') { $hpipe = 'abnormal' }
            if ($hsc -ne '' -and $null -ne $lastSc -and ([int]$hsc + [int]$hfc) -lt ($lastSc + $lastFc)) {
                Log ('RESET detected: cnt ' + $lastSc + '/' + $lastFc + ' -> ' + $hsc + '/' + $hfc)
            }
            if ($hsc -ne '') { $lastSc = [int]$hsc; $lastFc = [int]$hfc }
        }
        $iline = 'IDLE batt=' + $hb + 'V cur=' + $hcur + 'A temp=' + $ht + 'C humi=' + $hh + '%RH'
        $iline += ' angle=' + $hax + '/' + $hay + '/' + $haz + ' pipe=' + $hpipe
        $iline += ' succ=' + $hsc + ' fail=' + $hfc + ' last=' + $hlast
        Log $iline
        Start-Sleep -Seconds 240
        continue
    }
    # 避免一轮泵水跨进静默窗口
    if ($pauseS -and $pauseE -and $now -lt $pauseS -and $now.AddSeconds(250) -ge $pauseS) {
        Log ('PAUSE wait: next round would overlap ' + $pauseS.ToString('HH:mm'))
        while ((Get-Date) -lt $pauseS -and -not (Test-Path $StopFlag)) { Start-Sleep -Seconds 5 }
        continue
    }
    $round++
    $t0 = Get-Date
    $cat = 'UNKNOWN'; $logPath = ''; $txt = ''
    $hasDone = $false; $succ = $false; $fail = $false; $protect = $false
    $lowv = $false; $pipeWater = $false; $fill = $false
    $batt = 0.0; $ax = 0.0; $ay = 0.0; $az = 0.0; $sc = 0; $fc = 0; $cntFound = $false; $temp = 0.0; $humi = 0.0
    $drainIdle = $false; $fillIdle = $false; $drainDry = ''; $fillCnt = ''
    $diMin = $null; $diMax = $null; $dvMin = $null; $dvMax = $null
    $iiMin = $null; $iiMax = $null; $ivMin = $null; $ivMax = $null
    $pua = ''; $pv = ''; $pst = ''

    try {
        $out = (& $rr -Port $Port -Rounds 1 -Debug keep -MaxCycleSec $MaxCycleSec 2>&1) -join "`n"
        if ($out -match 'log=(.+?)(\r?\n|$)') { $logPath = $Matches[1].Trim() }
        if ($logPath -and (Test-Path $logPath)) { $txt = [IO.File]::ReadAllText($logPath, $enc) }

        $hasDone  = $txt.Contains('留样流程结束')
        $succ     = $txt.Contains('取水结果：成功')
        $fail     = ($txt.Contains('取水结果：失败') -or $txt.Contains('留样流程结束：取水失败'))
        $protect  = $txt.Contains('水泵保护停机')
        $lowv     = $txt.Contains('启动前低压保护')
        $pipeWater= $txt.Contains('判定管道仍有水')
        $fill     = $txt.Contains('判定管道有水')
        $drainIdle= ($txt.Contains('排水10秒后仍空转') -or $txt.Contains('排水空转停机') -or $txt.Contains('上电排水结束：水泵空转'))
        $fillIdle = $txt.Contains('注水10秒后仍空转欠流')
        $drainCapDry = ($txt.Contains('排水满60秒后管道电容判定无水') -or $txt.Contains('排水10秒后管道电容判定无水'))

        $m = [regex]::Matches($txt, '电池电压：([0-9.]+)V')
        if ($m.Count -gt 0) { $batt = Num $m[$m.Count-1].Groups[1].Value }
        $m = [regex]::Matches($txt, '姿态角度：X=(-?[0-9.]+)°\s+Y=(-?[0-9.]+)°\s+Z=(-?[0-9.]+)°')
        if ($m.Count -gt 0) { $g = $m[$m.Count-1].Groups; $ax = Num $g[1].Value; $ay = Num $g[2].Value; $az = Num $g[3].Value }
        $m = [regex]::Matches($txt, '主板温度：([0-9.]+)℃')
        if ($m.Count -gt 0) { $temp = Num $m[$m.Count-1].Groups[1].Value }
        $m = [regex]::Matches($txt, '主板湿度：([0-9.]+)%')
        if ($m.Count -gt 0) { $humi = Num $m[$m.Count-1].Groups[1].Value }
        $m = [regex]::Matches($txt, '成功取水：(\d+)次'); if ($m.Count -gt 0) { $sc = [int]$m[$m.Count-1].Groups[1].Value; $cntFound = $true }
        $m = [regex]::Matches($txt, '失败取水：(\d+)次'); if ($m.Count -gt 0) { $fc = [int]$m[$m.Count-1].Groups[1].Value }
        $m = [regex]::Matches($txt, '管道电容采样完成：采样5次，无水计数=(\d+)'); if ($m.Count -gt 0) { $drainDry = [int]$m[$m.Count-1].Groups[1].Value }
        $m = [regex]::Matches($txt, '电容5次采样有水计数=(\d+)'); if ($m.Count -gt 0) { $fillCnt = [int]$m[$m.Count-1].Groups[1].Value }

        foreach ($mm in [regex]::Matches($txt, '(排水|注水)监测：水泵电流=([0-9.]+)A，电池电压=([0-9.]+)V')) {
            $ph = $mm.Groups[1].Value; $i = Num $mm.Groups[2].Value; $v = Num $mm.Groups[3].Value
            if ($ph -eq '排水') {
                if ($null -eq $diMin -or $i -lt $diMin) { $diMin = $i }
                if ($null -eq $diMax -or $i -gt $diMax) { $diMax = $i }
                if ($null -eq $dvMin -or $v -lt $dvMin) { $dvMin = $v }
                if ($null -eq $dvMax -or $v -gt $dvMax) { $dvMax = $v }
            } else {
                if ($null -eq $iiMin -or $i -lt $iiMin) { $iiMin = $i }
                if ($null -eq $iiMax -or $i -gt $iiMax) { $iiMax = $i }
                if ($null -eq $ivMin -or $v -lt $ivMin) { $ivMin = $v }
                if ($null -eq $ivMax -or $v -gt $ivMax) { $ivMax = $v }
            }
        }

        if (-not $hasDone)              { $cat = 'NO_DONE' }
        elseif ($protect)               { $cat = 'PROTECT_STOP' }
        elseif ($lowv)                  { $cat = 'LOW_BATT' }
        elseif ($succ)                  { $cat = 'SUCCESS' }
        elseif ($fail -and $pipeWater)  { $cat = 'SENSOR_ABNORMAL' }
        elseif ($fail)                  { $cat = 'FAIL' }
        else                            { $cat = 'UNKNOWN' }
    } catch {
        $cat = 'NO_RESPONSE'
        Log ('ROUND ' + $round + ' ERROR ' + $_.Exception.Message)
    }

    # 等设备回到 STOP2 后读实际功耗（不碰串口，只读厂商上位机界面）
    if ($PowerCheck) {
        Start-Sleep -Seconds 20
        try {
            $pout = (& powershell -NoProfile -ExecutionPolicy Bypass -File $PowerScript -Seconds 4 -IntervalMs 300 2>&1) -join ' '
            if ($pout -match 'I_UA=([0-9.]+)') { $pua = $Matches[1] }
            if ($pout -match 'V=([0-9.]+)')    { $pv  = $Matches[1] }
        } catch { }
        $pst = 'OK'
        if ($pua -eq '')                          { $pst = 'NODATA' }
        elseif ([double]$pua -ge $PowerLimitUa)   { $pst = 'HIGH' }
        elseif ([double]$pua -lt 1)               { $pst = 'SUSPECT' }
        Log ('PWR round=' + $round + ' I=' + $pua + 'uA V=' + $pv + 'V status=' + $pst)
    }

    $fmt = { param($x) if ($null -eq $x) { '' } else { $x.ToString('0.00') } }
    $row = @(
        $round, $t0.ToString('HH:mm:ss'), (Get-Date).ToString('HH:mm:ss'), $cat,
        [int]$hasDone, [int]$succ, [int]$fail, [int]$protect, [int]$lowv, [int]$pipeWater, [int]$fill,
        $batt.ToString('0.00'),
        (& $fmt $diMin), (& $fmt $diMax), (& $fmt $dvMin), (& $fmt $dvMax),
        (& $fmt $iiMin), (& $fmt $iiMax), (& $fmt $ivMin), (& $fmt $ivMax),
        $ax.ToString('0.0'), $ay.ToString('0.0'), $az.ToString('0.0'), $sc, $fc,
        $temp.ToString('0.00'), $humi.ToString('0.00'), $pua, $pv, $logPath
    ) -join ','
    [IO.File]::AppendAllText($csv, $row + "`r`n", $utf8)

    $line = 'ROUND ' + $round + ' ' + $cat + ' batt=' + $batt.ToString('0.00') + 'V'
    $line += ' drain=' + (& $fmt $diMin) + '-' + (& $fmt $diMax) + 'A/' + (& $fmt $dvMin) + '-' + (& $fmt $dvMax) + 'V'
    $line += ' fill=' + (& $fmt $iiMin) + '-' + (& $fmt $iiMax) + 'A/' + (& $fmt $ivMin) + '-' + (& $fmt $ivMax) + 'V'
    $line += ' angle=' + $ax.ToString('0.0') + '/' + $ay.ToString('0.0') + '/' + $az.ToString('0.0')
    $line += ' temp=' + $temp.ToString('0.00') + 'C humi=' + $humi.ToString('0.00') + '%RH'
    $line += ' cnt=' + $sc + '/' + $fc
    if ($drainDry -ne '') { $line += ' drainDry=' + $drainDry + '/5' }
    if ($fillCnt -ne '')  { $line += ' fillWater=' + $fillCnt + '/5' }
    if ($drainIdle) { $line += ' drainIdle=1' }
    if ($fillIdle)  { $line += ' fillIdle=1' }
    if ($drainCapDry) { $line += ' drainCapDry=1' }
    if ($pua -ne '') { $line += ' pwr=' + $pua + 'uA/' + $pst }
    Log $line

    # 若本轮没有任何 LOG_DEBUG 明细，说明设备复位后 DEBUG 回到关闭，重新打开
    if ($hasDone -and -not $txt.Contains('排水阶段开始')) {
        $dbg = Ensure-DebugOn
        Log ('DEBUG recheck -> ' + $dbg)
    }

    # 计数只在 RAM，下降即说明设备复位过
    if ($cntFound) {
        if ($null -ne $lastSc -and ($sc + $fc) -lt ($lastSc + $lastFc)) {
            Log ('RESET detected: cnt ' + $lastSc + '/' + $lastFc + ' -> ' + $sc + '/' + $fc)
        }
        $lastSc = $sc; $lastFc = $fc
    }

    # 等到本轮触发后 IntervalSec 秒；期间每 5s 检查停止标志和截止时间
    $next = $t0.AddSeconds($IntervalSec)
    while ((Get-Date) -lt $next) {
        if (Test-Path $StopFlag) { break }
        if ((Get-Date) -ge $deadline) { break }
        Start-Sleep -Seconds 5
    }
}

# --- 汇总 ---
$rows = @()
if (Test-Path $csv) {
    $lines = [IO.File]::ReadAllLines($csv, $utf8)
    if ($lines.Count -gt 1) { $rows = $lines[1..($lines.Count-1)] | Where-Object { $_ -match '^\d+,' } }
}
$cats = $rows | ForEach-Object { ($_ -split ',')[3] }
$summary = $cats | Group-Object | Sort-Object Count -Descending | ForEach-Object { $_.Name + '=' + $_.Count }
Log ('SUMMARY rounds=' + $rows.Count + ' ' + ($summary -join ' '))
Log ('CSV=' + $csv)
if (Test-Path $StopFlag) { Remove-Item $StopFlag -Force }
