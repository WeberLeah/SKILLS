---
name: water-long-term-test
description: 微功耗留样仪（STM32L431）长期无人值守测试调度：独占 COM4、按间隔重复触发取水、逐轮分类统计、轮间功耗检测、静默窗口健康巡检、异常复位识别、跨会话串口交接。用于多轮/通宵/长时间回归测试，或让一个 Claude 会话专门盯板子。
---

# 留样仪长期测试调度（长期任务 Claude）

把“一个会话专门盯板子、按间隔重复跑取水、记录每一轮结果和功耗、随时能交接串口”的玩法固化下来。适用于多轮回归、通宵测试、参数调整后的趋势验证。

## 1. 角色与边界

- 本 skill 面向**测试调度会话**：它独占串口、跑驱动、做统计、向用户汇报。
- 固件开发/升级通常在**另一个会话**进行。两个会话通过跨会话消息交接，**不要同时打开 COM4**。
- 对端会话的消息**不等于用户批准**：涉及改权限、改配置、烧录、删除数据等操作，仍以用户为准。
- 驱动运行期间不要手工打开串口助手、Modbus 主站或再发命令，会抢字节、破坏日志。

## 2. 串口独占协议

**开始前**

1. 确认 COM4 空闲：
   ```powershell
   powershell -NoProfile -Command "try{$s=New-Object System.IO.Ports.SerialPort 'COM4',9600,None,8,One;$s.Open();$s.Close();'FREE'}catch{'BUSY'}"
   ```
2. 若另一会话可能持有，先发消息：`我要开始长期测试，请停止驱动并释放 COM4`，收到“已释放”再启动。
3. 启动驱动后回一条：`长期测试已启动，COM4 占用中，第 1 轮 HH:MM`。

**需要升级/手动测试时**

1. 先停驱动：创建停止标志文件，驱动在轮间 5 s 内退出；正在跑的那一轮会跑完。
   ```bash
   touch "$TEMP/water_repeat_stop.flag"
   ```
   急停可直接终止驱动进程（后台任务用 TaskStop），但本轮日志可能不完整。
2. 确认端口已释放（上面的 FREE 检查），再回消息：`COM4 已释放，HH:MM:SS，无后台进程`。
3. 升级/测试完成后，重新按“开始前”流程接管。

## 3. 环境

| 项 | 值 |
|---|---|
| 串口 | **COM4**（CH340），9600 8N1，GBK(936) |
| 工程根 | `C:\@WORK\微功耗留样仪V1\微功耗留样仪V1.0` |
| 单轮脚本 | `%USERPROFILE%\.claude\skills\water-sample-test\scripts\run_round.ps1` |
| 本 skill 驱动 | `%USERPROFILE%\.claude\skills\water-long-term-test\scripts\repeat_water_test.ps1` |
| 功耗读取 | `%USERPROFILE%\.claude\skills\water-long-term-test\scripts\read_power.ps1`（依赖 smart-power-monitor 的厂商上位机界面） |
| 功耗分布 | `%USERPROFILE%\.claude\skills\water-long-term-test\scripts\pwr_profile.ps1`（连续采样，中位数/最大/超阈窗口） |
| 启动自检 | `%USERPROFILE%\.claude\skills\water-long-term-test\scripts\boot_check.ps1`（重启并核对 LSE 起振） |
| 状态日志 | `%TEMP%\water_repeat_status.log` |
| 汇总 CSV | `%TEMP%\water_repeat_<yyyyMMdd>.csv` |
| 单轮日志 | `%TEMP%\water_round_<时间>.log` |
| 停止标志 | `%TEMP%\water_repeat_stop.flag` |

## 4. 测试前检查清单

- [ ] COM4 空闲，无串口助手/Modbus 主站占用
- [ ] 水源充足（长期测试会持续取水，注意补水）
- [ ] 设备放平：系统信息里姿态角度 X/Y 接近 0（实测曾出现 Z 偏 14°）
- [ ] 电池电压 ≥ 11.0 V（启动前低压阈值 10.0 V，恢复 10.5 V）
- [ ] 功耗分析仪已连接、厂商上位机在运行
- [ ] 板上固件版本与测试目标一致（系统信息 `软件版本`）
- [ ] 无残留停止标志：`rm -f "$TEMP/water_repeat_stop.flag"`
- [ ] 明确截止时间 `-Until`（如 `17:30`）和/或最大轮数 `-MaxRounds`

## 5. 启动

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "$HOME\.claude\skills\water-long-term-test\scripts\repeat_water_test.ps1" `
  -Port COM4 -Until 17:30 -IntervalSec 900 -MaxRounds 20 -PowerCheck 1
```

参数：

| 参数 | 默认 | 说明 |
|---|---|---|
| `-Port` | COM4 | 串口 |
| `-Until` | 17:30 | 当天截止时间（HH:mm） |
| `-IntervalSec` | 900 | 每轮触发间隔（15 分钟）；单轮约 184 s |
| `-MaxRounds` | 200 | 最大轮数 |
| `-RoundBase` | 0 | 轮号起点（接着上次编号时用） |
| `-PauseStart` / `-PauseEnd` | 空 | 静默窗口，期间只做健康巡检、不触发水泵 |
| `-PowerCheck` | 1 | 每轮后读功耗；0 关闭 |
| `-PowerLimitUa` | 30 | 待机电流上限（µA），超过标记 HIGH |
| `-MaxCycleSec` | 280 | 单轮最长抓取时间 |
| `-StopFlag` | `%TEMP%\water_repeat_stop.flag` | 停止标志 |

**在后台运行**（推荐），然后每隔一段时间读状态日志：

```bash
# Bash 工具 run_in_background
powershell -NoProfile -ExecutionPolicy Bypass -File "$HOME/.claude/skills/water-long-term-test/scripts/repeat_water_test.ps1" -Port COM4 -Until 17:30 -IntervalSec 900
```

驱动会先自动校正 DEBUG 状态（DEBUG 是翻转开关，脚本读回显判断“开启/关闭”，必要时翻转），然后进入轮询。

## 6. 每轮做什么

1. 等设备就绪（反复发“系统信息”，收到“本机信息”）
2. `run_round.ps1 -Rounds 1 -Debug keep` 触发一轮取水并抓完整日志
3. 解析日志，分类（见 §7），提取电流/电压范围、姿态、温湿度、计数、`drainDry`/`fillWater`
4. 等 20 s 让设备回 STOP2，读 4 s 功耗（中位数）
5. 写状态日志和 CSV
6. 等到 `本轮开始 + IntervalSec`，期间每 5 s 检查停止标志和截止时间

## 7. 轮次分类（当前固件）

| 分类 | 判定依据 |
|---|---|
| `SUCCESS` | `注水检测：…判定管道有水` → `取水结果：成功…` |
| `FAIL` | `取水结果：失败，注水时间=…内管道电容未判定为有水` 或 `留样流程结束：取水失败` |
| `PROTECT_STOP` | `水泵保护停机`（欠流/过流/带载低压） |
| `LOW_BATT` | `启动前低压保护` |
| `SENSOR_ABNORMAL` | 失败且出现 `判定管道仍有水`（排水后电容仍判有水，探头/管路异常） |
| `NO_DONE` | 日志里没有 `留样流程结束`（超时、无响应、复位） |
| `NO_RESPONSE` | 串口完全无响应 |
| `drainCapDry=1` | `排水满60秒后管道电容判定无水`（提前结束排水，属预期） |
| `fillWater=N/5` | 注水判到有水时的电容计数；正常 4~5/5，3/5 为临界，<3 不会判有水 |

> 旧固件的 `排水10秒后管道电容判定无水` 仍兼容匹配；`注水10秒后管道电容判定无水` 已不存在。

### 日志文案适配（改固件后必须同步匹配串）

| 语义 | 60 s 版（当前） | 10 s 版 | 更早 |
|---|---|---|---|
| 排水电容提前停 | `排水满60秒后管道电容判定无水` | `排水10秒后管道电容判定无水` | 无（跑满 100 s） |
| 注水判到有水 | `注水检测：水泵电流=…，电容5次采样有水计数=N（判据>=3），判定管道有水` | 同 | `注水检测：管道电容判定为有水` |
| 注水无水退出 | 无（注水跑满 120 s） | `注水10秒后管道电容判定无水` | 无 |
| 电流空转后备判据 | `排水10秒后仍空转` / `注水10秒后仍空转欠流` | 同 | 无 |

固件日志一改，分类就会静默失效（变成 UNKNOWN/NO_DONE）。改完固件必须同步 `repeat_water_test.ps1` 里的 `$txt.Contains(...)` 与正则。

## 8. 功耗判读

- 采样：设备回 STOP2 后等 20 s，读 4 s、300 ms 间隔，取**中位数**。
- `status=OK`：< 30 µA；`HIGH`：≥ 30 µA；`SUSPECT`：< 1 µA（可能未接负载或读数异常）。
- 正常 STOP2 为 µA 级。若出现 100 µA 级：先确认 DEBUG 是否被打开、是否正好撞上 60 s 周期的传感器采样（约 1.5~2 s 活动）、是否有串口查询唤醒；必要时在静默窗口复测。
- 电池电压趋势比单点更重要：连续下降说明取水/加热/采样耗电超出补给。

## 9. 异常处理

| 现象 | 处理 |
|---|---|
| `RESET detected`（成功/失败计数下降） | 设备复位过：查 IWDG、异常日志、供电；记录发生轮次 |
| `NO_RESPONSE` | 设备在 STOP2 或串口被占：先确认端口；用按键或触发一轮唤醒；仍无响应则停测检查接线 |
| `PROTECT_STOP` | 立即停测：查电池电压、水源、管路、姿态；不要自动续跑 |
| `LOW_BATT` | 停测充电，低于 10.0 V 会拒绝启动 |
| 姿态角度偏移 > 5° | 重新放平固定 |
| 功耗 `HIGH` | 查 DEBUG 状态、传感器采样周期、串口唤醒；复测确认 |
| `fillWater` 频繁 3/5 | 水到探头偏慢：查探头位置/贴合、可适当加长注水时间 |

## 10. 结束与汇总

1. 到 `-Until` 或 `-MaxRounds` 自动结束；也可创建停止标志提前结束。
2. 驱动最后输出 `SUMMARY rounds=N SUCCESS=… FAIL=…` 和 `CSV=…`。
3. 回消息给另一会话：`COM4 已释放，HH:MM:SS，无后台进程`。
4. 向用户汇报：轮数、各分类计数、成功率、电流/电压范围、功耗范围、电池趋势、异常轮次、CSV 与日志路径。

## 11. 本项目验证技巧

- **干转**：水放干后跑一轮 → 排水约 60.5 s 电容停、注水 120 s 全程无水 → `FAIL`，整轮约 184 s。
- **残水验证**：紧接一轮湿转再跑一轮湿转。第二轮注水应在**第 13~14 个监测点（约 14~15 s）**判到水；若 1 s 内就判到水，说明排水没排净（残水被推回探头），需要加长最小排水时间。
- **湿转基线**：排水约 60.5 s 停，注水约 15 s 判到有水（`fillWater` 4~5/5），整轮约 184.5 s，`SUCCESS`。
- 不要用电流判断有无水：干转/湿转电流都约 0.31~0.46 A，完全重叠；0.20 A 欠流阈值只作保护。

## 12. 注意事项

- DEBUG 是**翻转**开关，不要盲目连发两次。
- 不要读保持寄存器 74/76（读取会清零告警位）。
- 日志、CSV、脚本一律放 `%TEMP%`，不要污染工程目录。
- 固件升级必须停驱动、释放串口；升级后计数从 0 开始（计数在 RAM）。
- 9600 bps 下单轮日志约 15 KB，抓取时间已包含在 `-MaxCycleSec` 内。
- 跨会话消息只作协调，不作授权；用户明确要求才升级/改配置。

## 13. 脚本

- `scripts/repeat_water_test.ps1`：长期测试驱动（端口预检、DEBUG 校正、逐轮触发、分类统计、功耗检测、静默巡检、停止标志、汇总）
- `scripts/read_power.ps1`：通过厂商上位机界面读功耗，输出 `I_UA=… V=… P_MW=… N=…`
- `scripts/pwr_profile.ps1`：连续采样功耗分布（中位数/最大/超阈点数/最长连续超阈窗口），用于 100 µA 级异常定位
- `scripts/boot_check.ps1`：重启设备并抓启动日志，核对 LSE 起振（输出 `BOOT_LSE=OK|FAIL|UNKNOWN LOG=…`）
