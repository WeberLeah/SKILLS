---
name: clean-workspace
description: 微功耗留样仪工程的目录卫生规范。生成备份、补丁脚本、构建日志、串口抓包或新 skill 之前必读：所有辅助产物放工程外的独立文件夹，工程根目录只保留源码和工程文件。
---

# 工程目录卫生（STM32L431 微功耗留样仪）

**核心原则：工程目录 = 源码 + Keil 工程文件。其余一切辅助产物都放到工程外的独立文件夹。**

本文所有路径都**相对工程根目录**描述，不写死盘符：`<工程>` = 本工程根目录，`<工程名>` = 该目录的文件夹名。

## 1. 允许出现在工程里的东西

| 路径 | 说明 |
|---|---|
| `Template\` | APP 源码与 Keil 工程（`Project-L431\Temaple.uvprojx` 等） |
| `BootLoader\` | BootLoader 源码与工程 |
| `.git\`、`.gitignore` | 版本库 |
| `Template\Project-L431\Temaple\` | Keil 编译输出（`APP.bin`/`.axf` 等），属于工程产物，保留 |

**除此以外，任何新增文件（包括所有备份、快照、临时副本、补丁脚本、日志）都不得出现在工程目录的任何位置——不只是根目录，`Template\`、`BootLoader\` 的每一层子目录同样不允许。**

## 2. 辅助产物的固定去处

辅助产物根目录：**与工程目录同级的兄弟目录 `..\<工程名>_Debug\`**（本项目即 `..\微功耗留样仪V1.0_Debug\`，由工程根向上一级定位，不写死盘符）。

| 产物 | 位置（相对 `<工程>`） | 说明 |
|---|---|---|
| 改源码前的单项备份 | `..\<工程名>_Debug\backups\<工程内相对路径>\<文件名>.<yyyyMMdd-HHmmss>.bak` | **工程外**；每个文件最多保留 **3 份**，见第 3 节 |
| 批量重构前的整树快照 | `..\<工程名>_Debug\<年-月>_backups\<工程内相对路径>` | **工程外**；例：`2026-11_backups\Template\User\sys_init.c.bak_lp` |
| 一次性 PowerShell 补丁脚本 | `%TEMP%` | 不写进工程，也不长期保留 |
| 构建日志 / 烧录日志 | `%TEMP%` 或 `..\<工程名>_Debug\logs\` | 不要用工程里的 `_build\`（已废弃） |
| 串口抓包 / 硬件调试日志 | `%TEMP%` | 命名带日期，如 `serial_capture_20261120.log` |
| 新写的 skill | `..\<工程名>_Debug\skills\<name>\SKILL.md` 或全局 `C:\Users\<用户>\.claude\skills\<name>\` | **默认不放进工程**，由用户自行安装 |
| 批量改名/重构的中间备份 | `..\<工程名>_Debug\<年-月>_backups\` | 不要建 `_build\` 之类的目录 |

## 3. 修改前备份与轮换（强制）

**每次修改任何工程文件之前，先备份它的当前版本；同一个文件最多保留 3 份备份，超出删最旧的。**

> **备份一律存放在工程目录之外**：位置为工程根的同级目录 `..\<工程名>_Debug\backups\`（相对工程根定位，不写死盘符）。
> 子目录名虽然照抄原文件在工程里的位置，但**只是为了让备份能对应回原文件，绝不是把备份放回工程**。
> 工程目录（`Template\`、`BootLoader\` 及其下所有子目录）内严禁出现任何 `.bak`、`.bak_*`、快照或临时副本。

规则：

1. 备份位置：`..\<工程名>_Debug\backups\<工程内相对路径>\<原文件名>.<yyyyMMdd-HHmmss>.bak`
   例：改 `Template\User\sys_init.c` → `..\微功耗留样仪V1.0_Debug\backups\Template\User\sys_init.c.20261120-153012.bak`
2. **同一轮任务里对同一文件连续改多次，只备份第一次动手前的原版**，不要每改一行就存一份。
3. 备份后立即轮换：按修改时间排序，只保留最新 3 份，第 4 份及更早的删除。
4. 批量改多个文件时逐个备份，各自独立轮换。
5. 整目录/批量改名这类大改动，除单文件备份外，另做一份整树快照到 `..\<工程名>_Debug\<年-月>_backups\`。
6. 备份只是安全网，**不是版本管理**：确认改动稳定后可以主动清理，但至少保留最近一次。

标准操作（改文件前先执行）：

```powershell
function Backup-ProjectFile([string]$rel) {
    # 在工程根目录下执行；备份目录由工程位置推导：<父目录>\<工程名>_Debug\backups
    $root    = (Resolve-Path '.').Path
    $bakBase = Join-Path (Split-Path $root -Parent) ((Split-Path $root -Leaf) + '_Debug\backups')
    $src = Join-Path $root $rel
    if (-not (Test-Path $src)) { return }
    $dir = Join-Path $bakBase (Split-Path $rel -Parent)
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $name = Split-Path $rel -Leaf
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    Copy-Item -LiteralPath $src -Destination (Join-Path $dir "$name.$stamp.bak") -Force
    Get-ChildItem $dir -Filter "$name.*.bak" | Sort-Object LastWriteTime -Descending |
        Select-Object -Skip 3 | ForEach-Object { Remove-Item $_.FullName -Force }
}

# 用法：动手改之前
Backup-ProjectFile 'Template\User\sys_init.c'
Backup-ProjectFile 'Template\Project-L431\ADC\bsp_adc.c'
```

## 4. 操作规矩

1. **建任何新文件/目录前**，先判断它是不是"工程的一部分"：不是就放到第 2 节的去处。
2. **修改前先按第 3 节备份**，备份文件一律放工程外的 `..\<工程名>_Debug\backups\`，不要在源码目录里堆 `.bak_*`。
3. **用完即清**：补丁脚本、抓包文件、中间日志确认不需要后删除，或至少不留在工程里。
4. **删除前先确认**：删文件/目录前先列清单并得到用户确认（用户说"删掉"时按他指的范围删，不擅自扩大）。
5. **skill 不进工程**：工程内不要出现 `.claude\skills\` 目录；生成后告诉用户路径，由用户决定装到全局还是工程级。
6. **历史教训**（2026-11-20 清理）：
   - `_build\`（159 个文件：L431/L422/L433 编译日志、补丁脚本、改名备份）已整体删除；
   - 源码目录里上百个 `.bak_*` 已迁到 `2026-11_backups\`，2023/2025 年的老 `.bak_fix*` 直接删除；
   - `log-print`、`serial-tool` 两个 skill 已移出工程。

## 5. 收尾自检清单

任务结束时对照一遍：

- [ ] 每个改过的文件，动手前都已在 `..\<工程名>_Debug\backups\` 留下备份，且同名备份不超过 3 份
- [ ] 工程根目录只有 `.git`、`.gitignore`、`Template`、`BootLoader`
- [ ] `Template\` / `BootLoader\` 下没有新增 `.bak*`、`.rej`、`.log`、`.ps1`、临时 `.txt`
- [ ] 没有新建 `_build\`、`_tmp\`、`build\` 之类的目录
- [ ] 所有临时脚本和日志都在 `%TEMP%` 或 `..\<工程名>_Debug\`
- [ ] 新 skill 在 `..\<工程名>_Debug\skills\` 或全局 skills 目录，不在工程里

发现不合规的产物，主动提出迁移或删除，不要留在原地。
