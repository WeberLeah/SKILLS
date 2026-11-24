---
name: serial-tool
description: 微功耗留样仪（STM32L431）串口工具：COM5 抓日志/发设备命令，以及经 UART BootLoader 升级 APP.bin。需要读板子输出、打开调试日志、触发一次留样、或烧录固件时使用。
---

# 串口工具（抓日志 + 固件升级）

一个文件涵盖两件事：**读串口日志** 和 **串口升级固件**。设备命令通过纯文本发送，升级走 BootLoader 的串口升级态。

## 1. 硬件与参数

| 项目 | 值 |
|---|---|
| 串口 | `COM5`（CH340） |
| 参数 | 9600 8N1，无流控 |
| 编码 | **GBK（codepage 936）**，收发都按 936 编解码 |
| 固件文件 | `Template\Project-L431\Temaple\APP.bin`（Keil 构建后生成） |
| Flash 布局 | BootLoader `0x08000000`(28KB) / APP `0x08007000`(228KB)，VTOR `0x7000` |
| 日志方式 | LPUART1，`MODBUS_BAUD`=9600，和升级共用 COM5 |

> 串口是独占的：烧录或抓日志前先关掉串口助手、Modbus 主站等占着 COM5 的程序，否则打开报 `Access to the port 'COM5' is denied`。

## 2. 设备命令（不带回车换行）

直接发 GBK 文本，**末尾不要加 `\r\n`**，设备按整串匹配：

| 命令 | 作用 | 备注 |
|---|---|---|
| `重启` | 软件复位 | 复位后 BootLoader 有约 5s 升级窗口；升级时先用它把设备带回 BootLoader |
| `固件升级` | 进入串口升级态 | 进入后 10s 内必须开始发固件数据 |
| `调试信息输出开关` | **翻转** `Debug_Info_Flag` | 默认 0，发一次打开；抓完再发一次关掉，别连发两次 |
| `设置触发留样:1` | 触发一次取水流程 | 半角冒号，全角会匹配失败 |

PowerShell 里用 `[char]` 数组构造命令，避免脚本文件编码影响：

```powershell
$cmdDebug   = [string]::new([char[]]@(0x8C03,0x8BD5,0x4FE1,0x606F,0x8F93,0x51FA,0x5F00,0x5173))  # 调试信息输出开关
$cmdTrigger = [string]::new([char[]]@(0x8BBE,0x7F6E,0x89E6,0x53D1,0x7559,0x6837,0x3A,0x31))        # 设置触发留样:1
```

## 3. 固件升级流程（BootLoader）

1. 发 `重启`，等 **1.2s**；
2. 发 `固件升级`，等 **2s**（同时把回显写进日志）；
3. 把 `APP.bin` 分成 **1024 字节** 帧连续写完，中途不能停；
4. 写完后等 **12s**，让 BootLoader 判定结束、校验、提交并跳转。

关键时序（BootLoader 的约定，改脚本不要动）：

- 进入升级态后 **10s 内** 必须收到第一帧；
- 帧与帧之间空闲超过 **1s** 会被判定为文件结束；
- 9600bps 下约 1.04ms/字节，53KB 固件大约要 **55s**，`Write` 是阻塞的，别设太短的超时。

升级前检查 `APP.bin` 头：`SP` 应在 `0x2000xxxx`，Reset 向量应在 `0x0800xxxx`（当前 APP 基址 `0x08007000`）。脚本会把两者打印出来，异常就别发。

## 4. 抓日志流程

1. （可选）发 `调试信息输出开关`，让 `LOG_DEBUG` 输出——**注意是翻转**，不确定当前状态就先抓 3s 看有没有 DEBUG；
2. （可选）发 `设置触发留样:1` 跑一次完整取水流程；
3. 持续读串口 N 秒，原始字节按 GBK 写文件；上电初期有约 40s 的排水/自检，想抓完整流程就等一会儿再触发；
4. 分析时按 936 编码读文件，过滤空行。

注意：`Log_Detail_Flag=0`（默认）时日志只有业务内容；抓日志期间别用其它程序抢占 COM5。

## 5. 统一脚本（复制到 `%TEMP%` 后直接运行）

脚本内无中文字面量（命令用 `[char]` 构造），存盘编码不敏感；若你自行加了中文注释，请存成 **UTF-8 带 BOM**，否则 PowerShell 5.1 会按 GBK 解析出错。

```powershell
param(
    [ValidateSet('log','upgrade')][string]$Action = 'log',
    [string]$Port = 'COM5',
    [int]$Seconds = 120,          # log: 抓取时长
    [switch]$DebugToggle,         # log: 发送“调试信息输出开关”
    [switch]$Trigger,             # log: 发送“设置触发留样:1”
    [string]$Bin                  # upgrade: APP.bin 路径，缺省用工程内产物
)
$ErrorActionPreference = 'Stop'
$enc = [Text.Encoding]::GetEncoding(936)

$cmdReboot  = [string]::new([char[]]@(0x91CD,0x542F))
$cmdUpgrade = [string]::new([char[]]@(0x56FA,0x4EF6,0x5347,0x7EA7))
$cmdDebug   = [string]::new([char[]]@(0x8C03,0x8BD5,0x4FE1,0x606F,0x8F93,0x51FA,0x5F00,0x5173))
$cmdTrigger = [string]::new([char[]]@(0x8BBE,0x7F6E,0x89E6,0x53D1,0x7559,0x6837,0x3A,0x31))

$sp = New-Object System.IO.Ports.SerialPort $Port,9600,None,8,One
$sp.Encoding       = $enc
$sp.ReadTimeout    = 200
$sp.WriteTimeout   = 8000
$sp.WriteBufferSize = 4096
$sp.Open()

function Send-Text([string]$s) { $b = $enc.GetBytes($s); $sp.Write($b, 0, $b.Length) }
function Pump([int]$ms) {
    $until = (Get-Date).AddMilliseconds($ms)
    while ((Get-Date) -lt $until) {
        try { $s = $sp.ReadExisting() } catch { $s = '' }
        if ($s.Length -gt 0) { $sw.Write($s); $sw.Flush() }
        Start-Sleep -Milliseconds 30
    }
}

if ($Action -eq 'log') {
    $out = Join-Path $env:TEMP 'serial_capture.log'
    $sw  = [IO.StreamWriter]::new($out, $false, $enc)
    if ($DebugToggle) { $sp.DiscardInBuffer(); Send-Text $cmdDebug;  Pump 3000 }
    if ($Trigger)     { $sw.WriteLine('=== trigger ==='); $sw.Flush(); Send-Text $cmdTrigger }
    Pump ($Seconds * 1000)
    $sp.Close(); $sw.Close()
    Write-Output ('LOG DONE -> ' + $out)
}
else {
    if (-not $Bin) { $Bin = Join-Path (Get-Location).Path 'Template\Project-L431\Temaple\APP.bin' }
    $data  = [IO.File]::ReadAllBytes($Bin)
    $spWord = [BitConverter]::ToUInt32($data, 0)
    $rstVec = [BitConverter]::ToUInt32($data, 4)
    Write-Output ('BIN bytes=' + $data.Length + '  SP=0x' + $spWord.ToString('X8') + '  Reset=0x' + $rstVec.ToString('X8'))
    if (($spWord -band 0xFFFF0000) -ne 0x20000000) { throw 'SP not in SRAM range' }
    if ($rstVec -lt 0x08007000)                    { throw 'Reset vector below APP base 0x08007000' }

    $out = Join-Path $env:TEMP 'serial_upgrade.log'
    $sw  = [IO.StreamWriter]::new($out, $false, $enc)

    $sw.WriteLine('=== reboot ==='); $sw.Flush()
    Send-Text $cmdReboot
    Start-Sleep -Milliseconds 1200

    $sw.WriteLine('=== send upgrade cmd ==='); $sw.Flush()
    Send-Text $cmdUpgrade
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
    $sp.Close(); $sw.Close()
    Write-Output ('UPGRADE SEQUENCE DONE -> ' + $out)
}
```

用法：

```powershell
# 打开调试日志并抓 125s（含一次触发留样）
powershell -ExecutionPolicy Bypass -File "$env:TEMP\serial_tool.ps1" -Action log -DebugToggle -Seconds 125 -Trigger

# 只抓日志 60s
powershell -ExecutionPolicy Bypass -File "$env:TEMP\serial_tool.ps1" -Action log -Seconds 60

# 升级工程里最新编译的 APP.bin
powershell -ExecutionPolicy Bypass -File "$env:TEMP\serial_tool.ps1" -Action upgrade
```

## 6. 常见问题

| 现象 | 原因/处理 |
|---|---|
| 打开端口报 Access denied | 其它程序占着 COM5（串口助手、Modbus 主站、BootLoader 工具），先关掉 |
| 发命令没反应 | 结尾别加 `\r\n`；冒号用半角；确认 9600 8N1 |
| 日志乱码 | 按 GBK(936) 读，不是 UTF-8 |
| 升级卡住 / 超时回 BootLoader | 首帧超过 10s、帧间空闲超 1s、或 BIN 头不对；重发 `重启`→`固件升级` 再来 |
| 升级后没日志 | `Debug_Info_Flag` 又翻回去了，重发 `调试信息输出开关` 一次 |
| 烧完设备不跑 | 确认烧的是 `Temaple\APP.bin`（不是 axf/hex），且 Reset 向量在 `0x08007000` 之后 |

## 7. 相关

- 代码改动只编译不烧录，烧录前必须得到用户确认（见 `build-then-ask-before-flash`）。
- 日志内容怎么写、`LOG_INFO/LOG_DEBUG` 的字节预算见 `Template/User:log-print` skill。
