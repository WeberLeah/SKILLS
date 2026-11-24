---
name: smart-power-monitor
description: 读取 Smart Power Monitor 功耗分析仪（smartxbw，USB HID，SN P118-F000623）的实时电流/电压/功率。厂商上位机 Smart_power_monitor 独占设备时用 UIA 读它界面上的数值；需要直接控制输出电压/限流或做过流保护时才走 SmartPower.dll（32 位）。当用户提到功耗仪、功耗分析仪、Smart Power Monitor、读电流、测功耗、待机电流、nA 级电流、smartxbw、P118 时触发。
---

# 功耗仪（Smart Power Monitor）读取

nA 级电流功耗分析仪，厂商 www.smartxbw.com。**核心约束：一个 HID 设备同时只能被一个进程独占打开**，所以先确定走哪条路线。

## 1. 硬件

| 项目 | 值 |
|---|---|
| 型号 | Smart Power Monitor（SDK V1.3） |
| USB | HID `VID_0483&PID_5911`，SN `P118-F000623` |
| 特征 | 软件启动有**软启动**，`OutputOn` 后约 0.5 s 电压才爬到设定值 |
| 测量范围 | 电流可到 nA 级，显示 4 位有效数字 |

## 2. 两条路线，先选路线

| | 路线 A：读厂商上位机界面 | 路线 B：直接调 SDK DLL |
|---|---|---|
| 占用设备 | 由厂商软件占用 | 本进程独占 |
| 能否控制输出 | 只能在厂商软件里点 | 可以（`OutputOn/OutputOff`） |
| 数据精度 | 界面显示值（4 位有效数字） | 原始读数（每次 1 ms 左右） |
| 与厂商软件共存 | 必须共存 | **互斥，同时开会把设备搞挂** |
| 推荐场景 | 观察、记录、分析 | 需要程序化控制/过流保护 |

> ⚠️ 路线 B 崩溃时不会释放 HID 句柄，会让厂商软件报 `eLog:Err: #109`，只能**拔插 USB** 恢复。所以除非必要，优先用路线 A。

## 3. 路线 A：读厂商上位机（推荐）

厂商软件：`D:\Smart_power功耗分析仪资料\smart_power_monitor\Smart_power_monitor.exe`（V2.8，进程名 `Smart_power_monitor`）。

脚本：`read_vendor.ps1`（本 skill 目录内）

```powershell
# 持续读，500ms 一个点
& "$env:SystemRoot\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -File read_vendor.ps1

# 读 60 秒并同时存 CSV
... -File read_vendor.ps1 -Seconds 60 -Csv D:\current.csv

# 最快约 300ms 一次
... -File read_vendor.ps1 -IntervalMs 300
```

输出：

```
时间                    电流           电压           功率
11:29:40.873       2.1110 mA    10.9680 V   23.1500 mW
```

### 原理（改脚本前必读）

厂商软件的控件 **AutomationId 每次启动都会变**，不能写死。脚本按几何关系配对：

- 显示区是三行「数值 + 单位」（`V` / `mA` / `mW`）；
- 找单位文本（正则 `^(n|µ|u|m)?(A|V|W)$`），再找**同一行（Y 差 ≤ 8px）且在其左侧、X 最接近**的纯数字控件；
- 单位前缀决定量级（n / µ / m）。

界面改版导致读不到时，用 `dump_ui.ps1` 把控件树和坐标打出来重新对。

### 限制

- 厂商窗口**不能最小化**（最小化后 BoundingRectangle 全为 0，读不到）；
- 厂商软件必须处于**采样状态**，否则读到的就是它显示的 `0.0`；
- 读到的是显示值，1 µA 分辨率，不是 32 kSa/s 的原始数据。

## 4. 路线 B：直接调 SDK（仅在需要控制输出时）

SDK 目录：`C:\Users\weiji\Downloads\Smart_power_monitor_SDK_V1.3`，`dll_lib\SmartPower.dll` 是 **x86 32 位**。

必须用 32 位进程：`C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe`，或 csc `/platform:x86` 编译。

| 函数 | 说明 |
|---|---|
| `SmartPower_DeviceNums()` | 枚举设备数 |
| `SmartPower_DeviceOpen(0)` / `DeviceClose()` | 打开/关闭 |
| `SmartPower_GetStatus(double[4])` | 返回 **V、I、Bnc、Reserve** |
| `SmartPower_GetStatus_Ext(double[4], int[4])` | 额外返回 **OV、OC、OT、DC20V** 状态位 |
| `SmartPower_OutputOn(V, A)` | 上电（有约 0.5 s 软启动） |
| `SmartPower_OutputOff(0=HiZ, 1=20K对地)` | 断电；电容约 20 µF，**约 400 ms 泄放到 0** |
| `SmartPower_OutputOn_Ext(V, A, OC_Time, OV_AddV, OV_Time)` | 带过流/过压时间参数 |

### 路线 B 的坑（都踩过）

1. **HID 句柄非线程安全**：多线程同时调 `OutputOn` 和 `GetStatus` 会打乱命令，必须加锁串行化；
2. **`Thread.Sleep(1)` 默认是 15.6 ms**：要先 `timeBeginPeriod(1)`，否则轮询只有 60 Hz；
3. **`DeviceClose` 不会关输出**，关输出只能 `OutputOff`；
4. 程序崩溃不释放句柄 → 厂商软件报 `#109` → 拔插 USB；
5. 电源是**软启动**，上电后校验/保护布防都要留 0.5 s 以上，否则误判"输出异常"。

### 过流保护的经验参数

- 硬件限流设成你要的上限（如 1 A），软件在 `I ≥ 0.98 × 限流` 时 `OutputOff`；
- 判定用**持续时间**而不是采样次数（采样率会变）；
- 上电后 1 s 内不布防，避开软启动和容性浪涌；
- 短路另判：电压 < 50% 设定值 且 电流 ≥ 50% 限流，持续 50 ms。

## 5. 故障排查

| 现象 | 原因 / 处理 |
|---|---|
| 厂商软件报 `eLog:Err: #109` | 设备被别的进程占用过且未正常释放 → **拔插 USB**，只开厂商软件 |
| 读到的全是 0 | 厂商软件没在采样；或窗口被最小化 |
| 脚本报语法错 | 脚本含中文，必须存成 **UTF-8 带 BOM**（PS 5.1 会按 GBK 解析无 BOM 文件） |
| `DeviceNums` 报 `BadImageFormatException` | 用了 64 位进程调 32 位 DLL，换 SysWOW64 的 powershell |
| `Open` 失败 | 厂商软件或其他程序正占着设备 |
| 硬盘被占满 | 厂商软件持续写 `HistoryData\~DataLog.pd`（见过 4 GB），在软件里关掉自动保存或定期清理 |

## 6. 相关

- 设备信息与历史结论见 memory：`smartpower-power-monitor`
- 同机还有 `serial-tool` skill（COM5 / STM32L431 留样仪），注意别和本设备混淆
