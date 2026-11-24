---
name: log-print
description: 微功耗留样仪固件的串口日志打印规范。新增或修改任何 printf/日志输出、调整打印排版、写 LOG_INFO/LOG_DEBUG 时使用。
---

# 日志打印规范（STM32L431 微功耗留样仪）

本项目所有对外输出都走统一日志接口，**不要再直接调用 `printf`**（上电诊断除外，见 §6）。

## 1. 接口

实现：`Template/User/LPUART/bsp_lpuart.c` 末尾；声明与宏：`Template/User/sys.h`。

```c
LOG_INFO (module, "业务日志内容", ...);   // 始终输出
LOG_DEBUG(module, "业务日志内容", ...);   // 仅 Debug_Info_Flag 非 0 时输出
```

宏内部展开为 `Log_Printf(module, __FUNCTION__, ...)`，`方法名` 由 `__FUNCTION__` 自动填充，调用点不要手写。

| 全局变量 | 位置 | 含义 |
|---|---|---|
| `Log_Detail_Flag` | `bsp_lpuart_data.c` | 格式总开关，默认 **0**：只打印业务日志内容；=1 时带完整七段前缀 |
| `Debug_Info_Flag` | `bsp_lpuart_data.c` | 级别开关，默认 0：`LOG_DEBUG` 不输出 |
| `Log_Tick_10ms` | `bsp_lpuart.c` | TIM6 每次 10ms 中断累加，STOP2 期间暂停 |
| `Log_Chain_Id` | `bsp_lpuart.c` | 当前业务链路 ID，0 = 流程外 |

链路函数：`Log_Chain_New()` 在流程起点开新链路；`Log_Chain_Cost_Ms()` 返回链路已耗时。

## 2. 输出格式

`Log_Detail_Flag = 1` 时的完整格式（一行）：

```
【调用时间】2019-01-01 00:02:21【链路ID】0009【线程名】SENSOR【类名】TEMP【方法名】Sensor_Sample_Task【调用耗时】490ms【业务日志】水泵电流=0.017A（0.05Ω采样电阻）
```

- 调用时间：RX8025 实时时间且校验合法才用；否则退化为 `T+123.4s`（上电运行时间）。
- 线程名：调度器任务名，注册于 `main.c` 的 `OS_Add_Task(func, "NAME")`。
- 调用耗时：本次任务被调度开始到本条日志的毫秒数（10ms 分辨率）。

`Log_Detail_Flag = 0`（默认）时只输出 `业务日志` 部分，前后各带一个 `\r\n`。

## 3. 类名（模块名）取值

`CMD`（RS485 命令回调）、`SWI`（水路/留样）、`TEMP`（传感器采样）、`KEY`（按键）、
`PWR`（低功耗）、`RTC`（RX8025）、`SHT20`、`LIS3DH`、`LPUART`、`SYS`（系统启动）。

## 4. 内容写法

一条日志说清四件事：**谁 + 做了什么/发生了什么 + 关键数值（带单位）+ 结果或后续动作**。

- 用中文全角冒号和逗号分隔字段；数值一律带单位（`V`、`A`、`℃`、`%`、`秒`、`次`、`ms`）。
- 参数类日志带上阈值/范围，便于现场判断：
  `启动前低压保护：电池电压=11.60V，启动阈值=11.8V，恢复阈值=12.2V，拒绝抽水`
- 失败类日志写清失败原因与参数是否被修改。
- 不要出现 `ok`、`错误!`、`~~` 这类口语化或带感叹号的措辞。

## 5. 多字段回复的排版（分节格式）

一次回复里有多个字段时**不要挤在一行**，用分节标题 + 每项一行：

```c
LOG_INFO("CMD", "---- 本机信息 ----\r\n"
                 "设备名称：%s\r\n"
                 "设备编号：%u\r\n"
                 "软件版本：%s",
         GATEWAY_NAME_INFO, (unsigned int)LowPower_Task_Status.Device_Addr,
         SOFTWARE_VERSION_INFO);
```

规则：

- 标题固定 `---- 中文标题 ----`，独占一行；节内字段 `名称：值`。
- 多行内容写在一个 `LOG_INFO` 的格式串里，行间用 `"\r\n"`（`Log_Printf` 自带首尾换行，节与节之间自然空一行）。
- 一次回复分多节时，每节一个 `LOG_INFO` 调用。
- 失败结果统一结构：`结果 / 原因 / 输入 / 允许范围 / 参数状态`。
- 命令列表这类罗列内容：标题行 + `序号. 名称` 逐行，用 `sprintf(Help_Buff + strlen(Help_Buff), ...)` 追加，注意缓冲区余量。

参考实现：`RS485_System_Param_Callback`、`SWI_System_Param`、`RS485_Help_Callback`。

## 6. 级别选择

- `LOG_INFO`：上电/初始化诊断、动作开始与结束、取水结果、保护停机、参数修改结果、故障。**上电提示必须每次打印，不能被任何开关屏蔽。**
- `LOG_DEBUG`：阶段性细节（排水/注水阶段、电容采样计数、按键去抖、电池电压中间值）。默认关闭，内容要足够定位问题。
- 循环内、10ms 级高频路径不允许打印任何日志。

## 7. 字节预算（重要）

- 9600bps 阻塞发送约 **1.04ms/字节**，一条典型业务日志约 150~250 字节。
- TIM6 软件看门狗在计数 ≥195（**1.95s**）时复位，所以单次任务调度内的输出总量必须 **< 1s（约 900 字节）**，建议控制在 600 字节以内。
- 分节排版后估算字节数：中文按 2 字节、ASCII 按 1 字节、`\r\n` 按 2 字节。超预算就合并字段或降为 `LOG_DEBUG`。

## 8. 修改本工程源码的注意事项

- 源文件是 **GBK(codepage 936)**，`Edit`/`Write` 工具会拒绝，改用 PowerShell：
  `[IO.File]::ReadAllText($p,[Text.Encoding]::GetEncoding(936))` / `WriteAllText`。
- 临时 `.ps1` 辅助脚本含中文时必须保存为 **UTF-8 带 BOM**：`[Text.UTF8Encoding]::new($true)`。
- 函数名不要用 `R`、`RD`、`WR` 等，会和 PowerShell 内置别名冲突。
- 部分文件是 LF 行尾，做整段匹配时先按 `\r\n`/`\n` 各试一次。

## 9. 完成后

- 只编译：`UV4.exe -b <uvprojx> -j0 -o <log>`，确认 `0 Error(s), 0 Warning(s)`。
- **烧录前必须得到用户确认**，不要自行调用 ST-LINK_CLI。
