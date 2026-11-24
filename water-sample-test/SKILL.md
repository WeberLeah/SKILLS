---
name: water-sample-test
description: 微功耗留样仪（STM32L431）重复测试流程：AC5 编译 → UART BootLoader 烧录 APP → 上电自检/H2 电压检测 → 打开 DEBUG → 触发取水 → 抓日志解析 → Modbus 回读。用于烧录后验证、回归测试、取水成功/失败排查。
---

# 留样仪重复测试（烧录 + 取水 + 回读）

把“改完代码 → 烧录 → 跑一轮取水 → 看结果”的完整流程固化下来，后续可直接复用。

## 适用场景

- 改完 APP 代码，需要烧录并验证功能
- 回归测试：排水 / 注水 / 电容 / 泵保护 / 计数 / 传感器 / Modbus
- 取水失败排查：区分“水没到探头”和“探头 / 接线 / 极性故障”

## 0. 环境（本机实测）

| 项 | 值 |
|---|---|
| 串口 | **COM4**（CH340；串口助手 / Modbus 主站会占用） |
| 参数 | 9600 8N1，无流控，GBK(936) |
| 工程根 | `C:\@WORK\微功耗留样仪V1\微功耗留样仪V1.0` |
| APP 工程 | `Template\Project-L431\Temaple.uvprojx` |
| Boot 工程 | `BootLoader\Project-L431\Temaple.uvprojx`（一般不动） |
| 固件 | `Template\Project-L431\Temaple\APP.bin` |
| AC5 | `C:\Keil\Keil_v5\ARM\ARMCC`（V5.06 update 6） |
| UV4 | `C:\Keil\Keil_v5\UV4\UV4.exe` |
| Flash | Boot `0x08000000`/28KB，APP `0x08007000`/228KB，VTOR `0x7000` |

> `serial-tool` skill 文档里写的是 COM5，本机 CH340 实际枚举为 **COM4**。用前先确认端口。

## 1. 编译

```bash
cd "C:/@WORK/微功耗留样仪V1/微功耗留样仪V1.0"
"C:/Keil/Keil_v5/UV4/UV4.exe" -b "Template/Project-L431/Temaple.uvprojx" -j0 -o "$TEMP/app_build.log"
iconv -f GBK -t UTF-8 "$TEMP/app_build.log" | tail -5
```

要求：

- `0 Error(s), 0 Warning(s)`
- `APP.bin` 头：SP 在 `0x2000xxxx`，Reset 在 `0x08007xxx`

检查头：

```bash
powershell -NoProfile -Command '$d=[IO.File]::ReadAllBytes("Template/Project-L431/Temaple/APP.bin"); "bytes="+$d.Length; "SP=0x"+([BitConverter]::ToUInt32($d,0)).ToString("X8"); "Reset=0x"+([BitConverter]::ToUInt32($d,4)).ToString("X8")'
```

## 2. 烧录（UART BootLoader，无需断电）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "$HOME\.claude\skills\water-sample-test\scripts\upgrade_uart.ps1" -Port COM4
```

脚本流程：`重启` → 1.2 s → `固件升级` → 2 s → 1024 B/帧连续发送 → 12 s 提交/跳转 → 再抓 45 s 启动日志。

启动日志应依次出现：

- `BootLoader启动：软件版本=V1.41...`
- `固件接收完成：字节数=...，帧间空闲1s判定结束`
- `APP启动页写入：地址=0x08007000...`
- `跳转到应用程序：栈顶地址=... 复位向量=...`
- `系统启动：设备名称=... 软件版本=V1.3...`
- `上电排水前电压检测：电池电压=XX.XXV，启动阈值=10.0V`
- `上电排水开始：前10s持续排水，10s后每秒检测管道电容...最长30s`

异常分支：

- `上电排水跳过：电池电压=... 低于启动阈值=...，不启动水泵`
- `上电排水超时：持续30s管道仍检测到存水，请检查水泵与管路`

上电排水最长 30 s，脚本的 45 s 抓取会覆盖；之后 APP 才进入 OS 循环。

## 3. 跑一轮（自动等就绪、开 DEBUG、触发、等结束）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "$HOME\.claude\skills\water-sample-test\scripts\run_round.ps1" -Port COM4 -Rounds 1 -Debug on
```

说明：

- `-Debug on` 表示“发一次调试信息输出开关”。该命令是**翻转**；刚烧录重启后默认为 0，所以 `on` 就是打开。
- 不确定当前状态时用 `-Debug keep`，先抓 3 s 看有没有明细日志，再决定是否翻转。
- 脚本会先反复发“系统信息”直到收到“本机信息”（最多 60 s），避免在启动排水期间发命令丢失。
- 每轮**约 184 s**（最小排水 60 s 版）：0.8 s 等待 + 0.6 s 电压检测 + 排水 60 s + 稳定 1.5 s + 电容 5 次 + 1 s + 注水 120 s。
  - 排水 60 s 后电容连续 2 批（500 ms）无水计数 ≥3 → 提前结束；一直判不到无水时跑满 `排水反转` 时间（默认 100 s），整轮约 224 s。
- 结束标志：`留样流程结束`。

## 4. 解析日志

日志在 `%TEMP%\water_round_<时间>.log`（GBK）：

```bash
iconv -f GBK -t UTF-8 "$TEMP/water_round_xxx.log" > "$TEMP/round_utf8.log"
grep -nE "启动前电池电压|排水阶段结束|管道电容采样完成|注水阶段开始|注水检测|取水结果|留样流程结束|保护停机" "$TEMP/round_utf8.log"
```

电气范围统计：

```bash
grep -E "排水监测|注水监测" "$TEMP/round_utf8.log" \
| sed -E 's/.*(排水|注水)监测.*电流=([0-9.]+)A.*电池电压=([0-9.]+)V.*/\1 \2 \3/' \
| awk '{ph=$1;i=$2+0;v=$3+0;if(!(ph in ci)){imin[ph]=imax[ph]=i;vmin[ph]=vmax[ph]=v};if(i<imin[ph])imin[ph]=i;if(i>imax[ph])imax[ph]=i;if(v<vmin[ph])vmin[ph]=v;if(v>vmax[ph])vmax[ph]=v;ci[ph]++}END{for(ph in ci)printf "%s: %.2f~%.2f A, %.2f~%.2f V (n=%d)\n",ph,imin[ph],imax[ph],vmin[ph],vmax[ph],ci[ph]}'
```

## 5. 判定标准

| 结果 | 日志特征 |
|---|---|
| 成功 | `注水检测：水泵电流=…（判据>0.20A且<=1.00A），电容5次采样有水计数=N（判据>=3），判定管道有水` → `取水结果：成功，...累计成功取水=N次` → 系统信息 `瓶内储水：有水`、`成功取水：N次`、`最近结果：成功` |
| 失败 | 无“判定管道有水”行 → `取水结果：失败，注水时间=120秒内管道电容未判定为有水，累计失败取水=N次` → `最近结果：失败` |
| 保护停机 | `水泵保护停机：注水阶段空转欠流` / `堵转过流` / `带载电池电压过低`（正常测试不应出现）。**状态 12 路径不打印“取水结果：失败”**，只有 `留样流程结束：取水失败，链路耗时=…`，统计时要认这条 |
| 排水提前结束 | `排水满60秒后管道电容判定无水：连续2批采样无水计数均>=3，提前结束排水`（**属正常行为**，不是异常） |
| 异常复位 | 成功/失败计数中途归零，或日志中出现启动横幅 |

正常电气范围（实测）：排水 **0.32–0.47 A**、注水 **0.31–0.44 A**；带载电压 **11.4–12.0 V**（保护线 10.0 V）。

> **干转与湿转电流完全重叠**（干 0.31–0.44 A / 湿 0.33–0.47 A），电流**不能**用来判断管路有没有水，只能看管道电容。

保护阈值（`swi.c`）：

- 启动前空载电池 `10.0 V`，低压恢复 `10.5 V`
- 带载低压 `10.0 V` 连续 1 s
- 过流 `1.0 A` 连续 150 ms
- 注水欠流/空转 `0.20 A`：注水开始 10 s 后连续 3×50 ms；本泵干湿电流都远高于此，实际不会触发
- 排水：前 60 s 强制排水，之后电容连续 2 批（500 ms）无水计数 ≥3 才提前结束

## 6. 取水失败排查

成功条件（`swi.c:621`）：排水后 `Check_OK=1`，且注水 120 s 内 PB2 至少一次读到 0（有水）。

- **PB1** = 电容电源（流程中为高）
- **PB2** = 电容输入，**1=无水，0=有水**，上拉输入
- 系统信息里的“管道电容：正常”**不是独立自检**：它只是根据排水后读到“无水”推断的，探头断线或卡在 1 也会显示正常

排查顺序：

1. 注水时测 PB1 为高、PB2 干态为高；把探头泡水，看 PB2 是否变 0。
   - 能变 0 → 探头正常，查水源、进水管、泵引水、管路方向、探头位置；
   - 不能变 0 → 探头、接线或极性。
2. 观察泵是否真的出水。**电流不能用来判断有无水**：实测干转 0.31–0.44 A、湿转 0.33–0.47 A，完全重叠；0.20 A 欠流阈值只作保护。
3. 时序参考：正常湿转时水约在注水开始后 **14–15 s** 到探头。若注水 **1 s 内**就判到有水，说明管里有上一轮残水没排净（排水太短），这不是正常成功；紧接湿转再跑一轮即可复现，解决办法是最小排水时长 ≥60 s。
4. 设备放平固定。实测曾出现运行中 Z 轴偏约 14°。
5. 排除“水来得慢”：发 `设置注水正转时间:300`（1–600 s，掉电保存），再跑一轮。

## 7. Modbus 回读（可选）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "$HOME\.claude\skills\water-sample-test\scripts\modbus_read.ps1" -Port COM4 -Slave 5 -Mode input
```

- `-Mode input`：0x04 输入寄存器，PDU 起始 `0x0000`，30 个；电池 / 电流 / 温湿度 / 角度 / 电容 / 计数 / 地址 / 时间。
- `-Mode holding`：0x03 保持寄存器，读 78~83（Modbus 地址在 80）。
- **地址约定**：FreeMODBUS 在 `mbfuncinput.c` / `mbfuncholding.c` 里做了 `usRegAddress++`，PDU 地址 N 对应寄存器 #(N+1)。所以读完整电池浮点必须从 PDU `0x0000` 开始；从 `0x0001` 开始会整体错开一个寄存器。
- 浮点打包：线上 4 字节为 `b3 b2 b1 b0`（大端 IEEE-754）。
- **不要读保持寄存器 74/76**：读取会清零告警位。

> **设备休眠时串口可能不响应**：设备空闲会进入 STOP2。本机启动日志有
> `LSE起振失败：LPUART时钟已切换为HSI16，STOP2下串口起始位唤醒不可用，仅RTC与按键可唤醒`。
> 因此设备休眠后单发一帧 Modbus 很容易丢失。脚本默认跨 12 s 重试（覆盖 RX8025 10 s 唤醒周期）；
> 仍无响应时，先触发一轮取水或按按键把设备唤醒，再立即读取。文本命令（系统信息等）同理。

## 8. 注意事项

- 串口独占：先关串口助手、Modbus 主站等占用 COM4 的程序。
- APP 升级不需要断电，软复位即可（Boot 的 IWDG 喂养已加，但 `boot.bin` 本次未烧，需要 SWD 才能更新）。
- 日志、脚本一律放 `%TEMP%`，不要放进工程目录。
- DEBUG 是翻转开关；抓完可再发一次关闭。
- 烧录/复位后取水计数从 0 开始（计数在 RAM，不写备份寄存器）。

## 9. 脚本

- `scripts/upgrade_uart.ps1`：编译产物烧录 + 启动日志抓取
- `scripts/run_round.ps1`：等就绪、可选翻转 DEBUG、触发 N 轮、抓日志
- `scripts/modbus_read.ps1`：读输入/保持寄存器并解码
- 长期重复测试（定时触发 + 逐轮统计 + 功耗检测 + 启动校验）：见 skill `water-long-term-test`
