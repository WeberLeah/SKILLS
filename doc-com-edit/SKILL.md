---
name: doc-com-edit
description: 在 Windows 上用 PowerShell + Office COM 批量修改 Word(.doc/.docx) 与 Excel(.xlsx) 文档（逐段/逐表/逐单元格查找替换、增删段落与表格行、标题编号修复、目录与页脚域更新）。当用户要求批量修订生产作业指导书、检验规范、问题清单等 Office 文档，或需要在保留格式的前提下做大量精确文本替换时使用。
---

# Office 文档批量修改（Windows + COM）

用 Word/Excel COM 在**保留原有格式**的前提下改文档。本 skill 来自 IUS2-100K 生产作业指导书（37 页、716 段、9 表、70+ 内嵌图）的实际修订，坑都踩过。

## 铁律

1. **脚本必须存成 UTF-8 with BOM**。Write 工具写出的是无 BOM UTF-8，PowerShell 5.1 会按 ANSI 解析，中文路径/字面量全部乱码。每次写完脚本先转码：
   ```powershell
   $p='...ps1'; $t=[IO.File]::ReadAllText($p,[Text.Encoding]::UTF8)
   [IO.File]::WriteAllText($p,$t,(New-Object Text.UTF8Encoding($true)))
   ```
2. **中文参数不要走命令行**。`-File` 传参时 `|`、`,` 等会被改写；查找词、替换文本一律写进 UTF-8 文本文件，脚本里按行读。
3. **保存后必须校验文件时间戳**。`$doc.Save()` 返回不代表落盘；COM 调用可能静默失败。用 `(Get-Item $docPath).LastWriteTime` 前后对比，并看 `$doc.Saved`（打开即 True = 没改动）。
4. **不要 Save 后立刻 `Stop-Process WINWORD`**。23MB 文档落盘要几秒，强杀会丢数据（实测丢过 3 次替换）。让 `$word.Quit()` 正常退出，或在脚本**开头**清理残留进程。
5. **改前先备份**，备份放 `待修改\备份\` 之类独立目录，命名带日期。
6. **目录域/页脚域最后统一更新**。每次改动页码都会变，中途更新等于白做。

## 推荐工作流

1. 备份原件。
2. 探测现状：段数/表数/图片数、关键字符串所在段落号（`scripts/find.ps1`）。
3. 逐条与用户确认改法（生产文档涉及工艺判定，不要替用户拍板数值）。
4. 用 `scripts/apply.ps1` + ops 文件批量执行，每条打印 `OK/FAIL`。
5. 改完立刻回读验证（`scripts/dump.ps1` 打印段落号/样式/编号/文本）。
6. 全部改完再更新目录与页脚域。

## ops 文件格式（制表符分隔，UTF-8）

```
FIND	旧文本	新文本	预期出现次数
PARA	唯一关键片段	整段新文本
HEAD	旧标题文本	新标题文本
STYLE	段落文本	样式ID(-3=标题2)	yes|no(清除编号)
PIDX	段落号	新文本
PDEL	段落号          # 只删空段落
PDELX	段落号          # 删除任意段落
```

- `FIND` 先按段落文本计数校验，再调用 11 参 `Find.Execute` 全量替换，替换后回读确认残留为 0。
- `PARA/HEAD` 要求关键片段在全文**唯一**，否则 FAIL 中止（防止误改）。
- `PIDX/PDEL/PDELX` 用绝对段落号，**删除务必从大号往小号删**。

## 已知坑与解法

| 现象 | 原因 | 解法 |
|---|---|---|
| 循环 `Find.Execute()` 统计命中数时卡死 | 用 `SetRange($rng.End, Content.End)` 推进的循环会死循环 | 不要循环 Find；计数改为遍历 `Paragraphs` 的文本做 `IndexOf` |
| `$doc.Save()` 报成功但文件没变 | 文档没被判定为“已修改”，Save 是空操作；或替换根本没生效 | 看 `$doc.Saved`；替换用 11 参 `Execute`，别用 `$null` 占位 |
| 设置段落 `Style` 后文字消失、后续标题编号从 1 重排 | 列表段落被拆成“空标题 + 原段落”，并给后续标题挂上直接编号（numId） | 不要对列表段落直接改 Style。修复见 `scripts/renumber-headings.ps1`：对每个被挂 numId 的标题执行 `RemoveNumbers()` + `ApplyListTemplateWithLevel(模板, $true, 2, 2)` |
| 表格单元格写值后单元格合并/报错 | 单元格 Range 含结束标记 `\a` | `$r=$cell.Range; $r.MoveEnd(1,-1); $r.Text='...'` |
| 加表格行后格式不对 | — | `$tbl.Rows.Add()` 后逐格赋值；删除用 `$tbl.Rows.Item(n).Delete()` |
| Excel 读 `.Text` 抛 “Specified cast is not valid” | 合并单元格/类型转换 | 一律 `[string]$cell.Value2`，数字写回用 `[double]$v` |
| 中文弯引号 “ ” 让脚本解析失败 | PowerShell 5.1 把弯引号当字符串定界符 | 这类文案放外部 UTF-8 文本文件，脚本里读 |

## 验证清单

- [ ] 每条 ops 都输出 `OK`，`FAIL=0`
- [ ] 文件 `LastWriteTime` 已更新
- [ ] 回读目标段落，确认文本/样式/自动编号正确
- [ ] 标题编号连续（1..N、x.y），无重排
- [ ] 无残留 `WINWORD.EXE`、无 `~$` 锁文件
- [ ] 最后更新目录域与页脚域，核对总页数
