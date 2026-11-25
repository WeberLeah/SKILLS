# TestNotify — 局域网测试结果通知

一个**免安装**的小工具：一台电脑当"接收端"（窗口程序，可缩到托盘），其他电脑/测试脚本把 PASS / FAIL 结果发过来，接收端实时显示、统计，并弹出 **Windows 原生通知**；同一份数据还可以在网页大屏上看，所有结果追加到 `results.csv`。

不依赖 Python、不需要联网、不需要装任何运行库（用的是 Windows 自带的 .NET Framework），也不用管理员权限。

**默认安装目录**：`D:\TestNotify`。整个文件夹可以随意拷贝/移动到其他电脑或盘符，程序都按自身所在目录读写 `results.csv`、`dashboard.html`、`sender.ini`、`receiver.ini`。

---

## 文件说明

| 文件 | 用途 |
| --- | --- |
| `TestNotify.exe` | 接收端窗口程序（双击运行，关窗后留在托盘） |
| `dashboard.html` | 大屏界面，必须和 exe 放在同一目录；想改界面就改它 |
| `Sender.exe` | **发送端窗口程序**（测试机/操作员用，双击运行） |
| `Send-TestResult.ps1` | 发送脚本（无界面，适合接进测试程序） |
| `send.bat` | 极简发送入口，内部调用上面的 ps1 |
| `results.csv` | 运行后自动生成的完整日志，Excel 可直接打开 |
| `Receiver.cs` / `Sender.cs` | 接收端 / 发送端源码 |
| `build.bat` | 修改源码后重新编译两个程序 |
| `allow-firewall.bat` | 放行 TCP 8080 防火墙规则（右键"以管理员身份运行"） |

---

## 快速开始（3 步）

**1. 接收端电脑**：双击 `TestNotify.exe`

- 打开的是普通窗口（不是 cmd 黑框），顶部显示监听地址，例如 `http://192.168.1.100:8080/`。
- **默认不会自动打开浏览器**——接收端窗口里就能看结果；想看网页大屏时点右上角"打开大屏"，或用浏览器访问这个地址。
- 首次运行 Windows 会弹防火墙提示，勾选"专用网络"并允许。错过了就点托盘菜单"放行防火墙（管理员）"，或管理员运行一次 `allow-firewall.bat`。
- **关闭窗口不会退出程序**：它会缩到右下角托盘继续监听，双击托盘图标即可重新打开。要真正退出，点窗口里的"退出"或托盘右键 →"退出"。

**2. 测试机**：发送结果

操作员直接双击 `Sender.exe` 点按钮就行（见下文"发送端窗口程序"）；也可以用命令：

```powershell
# 最简单（假设接收端是 192.168.1.100）
curl "http://192.168.1.100:8080/?result=PASS"
```

```powershell
# 带工位、序列号、备注（中文请用这个脚本，编码不会乱）
.\Send-TestResult.ps1 -Server 192.168.1.100 -Result FAIL -Station A1 -SN SN2026001 -Message "电压超限 3.2V"
```

```bat
rem 或者改好 send.bat 里的 SERVER 后用
send.bat FAIL A1 SN2026001 "电压超限"
```

**3. 看结果**：接收端窗口中间的大字立刻变成 PASS（绿）或 FAIL（红），同时右下角弹出 Windows 通知；网页大屏每 0.8 秒刷新，FAIL 时还会响三声并红框闪烁。

---

## 接收端窗口与通知

窗口从上到下：

- 顶部：监听地址 + "复制地址" / "打开大屏"按钮
- 中间：最近一次结果的大字，右侧是 PASS / FAIL / 总数
- 列表：最近 500 条记录（PASS 绿色、FAIL 红色），`results.csv` 里保留全部历史
- 底部：**结果通知**下拉（`全部结果` / `仅 FAIL` / `关闭`），选择会保存到 `receiver.ini`，下次启动沿用

关于通知：

- 用的是 Windows 自带通知（右下角弹出，也能在操作中心找到），标题是 PASS 合格 / FAIL 不合格，正文带工位、SN、备注；
- 点一下通知会恢复接收端窗口；
- 托盘图标颜色跟着最近结果走：灰色=还没有结果，绿色=PASS，红色=FAIL；
- 如果通知没弹出，检查 Windows"设置 → 系统 → 通知"里是否允许了 TestNotify，以及是否开了"专注助手"。

托盘右键菜单：显示主界面、打开大屏、清空统计、放行防火墙（管理员）、退出。最小化/关闭窗口都只是收进托盘，HTTP 监听一直在跑。

---

## 发送端窗口程序（操作员用）

`Sender.exe` 是给测试机上的人用的图形界面，不需要命令行。

**第一次使用**

1. 把 `Sender.exe` 拷到测试机，双击打开；
2. 顶部填接收端 IP 和端口，点 **检测**，右侧变绿显示 `● 已连接` 就通了；
3. 填好工位（例如 `A1`）——IP、端口、工位会保存到同目录的 `sender.ini`，下次自动带出来。

**日常操作**

- 扫描枪扫产品 SN（或手动输入），**按回车** → 自动发送 **PASS**，SN 框自动清空，直接扫下一件；
- 不合格时点红色 **FAIL** 按钮；在"备注"里写原因会一起发出去（备注不会被自动清空，方便连续记录同类问题）；
- 下方日志实时显示每条发送结果，绿色 PASS、红色 FAIL；发送失败会显示原因并提示"连接失败"；
- 左下角日志区上方会显示当前目标地址，发送成功 SN 自动清空。

> 提醒：回车发送只对 SN 输入框生效，在备注框里回车是换行。

**命令行模式**（`Sender.exe` 也支持无界面调用，方便接进测试脚本）

```bat
Sender.exe --send PASS --server 192.168.1.100 --port 8080 --station A1 --sn SN001
Sender.exe --send FAIL --server 192.168.1.100 --station A1 --sn SN001 --msg "电压超限"
```

成功返回退出码 `0`，发送失败返回 `1`。

---

## 上报接口

接收端只认一个地址：

```
POST/GET  http://<接收端IP>:8080/report
```

也兼容 `http://<接收端IP>:8080/`（带参数时）和 `/?result=PASS` 这种写法。

### 字段

| 字段 | 别名（都可省略） | 说明 |
| --- | --- | --- |
| `result` | `status`、`state`、`res`、`结果`、`测试结果` | **必填**。`PASS/OK/合格/通过/成功` → PASS；`FAIL/NG/不合格/失败` → FAIL；其他值 → INFO（蓝色，不计入合格率） |
| `station` | `name`、`bench`、`工位`、`站点` | 工位/设备名 |
| `sn` | `serial`、`serialno`、`序列号`、`条码` | 产品序列号 |
| `msg` | `message`、`note`、`remark`、`detail`、`备注` | 备注、失败原因 |

时间由接收端自己记录（不信任测试机的时钟）。

### 三种发送方式都支持

```bash
# 1) 查询串
curl "http://192.168.1.100:8080/report?result=PASS&station=A1&sn=SN001"

# 2) 表单
curl -X POST -d "result=FAIL&station=A1&sn=SN001&msg=电压超限" http://192.168.1.100:8080/report

# 3) JSON（UTF-8）
curl -X POST -H "Content-Type: application/json" \
     -d '{"result":"PASS","station":"A1","sn":"SN001"}' \
     http://192.168.1.100:8080/report

# 4) 裸文本
curl -X POST --data-binary "PASS" http://192.168.1.100:8080/report
```

Python（如果测试机上有 Python）：

```python
import requests
requests.post("http://192.168.1.100:8080/report",
              json={"result": "FAIL", "station": "A1", "sn": "SN001", "msg": "电压超限"},
              timeout=3)
```

C#：

```csharp
using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
await http.PostAsync("http://192.168.1.100:8080/report",
    new System.Net.Http.StringContent(
        "{\"result\":\"PASS\",\"station\":\"A1\"}",
        System.Text.Encoding.UTF8, "application/json"));
```

返回：`{"ok":true,"result":"PASS"}`。连不上就说明 IP/端口/防火墙有问题。

### 辅助接口

| 地址 | 作用 |
| --- | --- |
| `GET /api/state` | 当前统计和最近记录（JSON），大屏就是轮询它 |
| `GET /api/clear` | 清空统计（不删 CSV） |
| `GET /api/ping` | 返回 `pong`，排查连通性用 |

---

## 大屏（可选）

接收端窗口已经能看实时结果；网页大屏是给"另一台电脑 / 大电视 / 手机"看的可选项，在窗口点"打开大屏"或浏览器访问 `http://<接收端IP>:8080/`。

- 顶部：PASS / FAIL / 总数
- 中间：最近一次结果的大字显示（绿色 PASS、红色 FAIL）
- 下方：最近记录表格（内存里保留最近 500 条，页面显示 200 条）
- 按钮：声音开关、只看 FAIL、清空、全屏

FAIL 声音需要浏览器允许播放（点一下页面即可解锁），有"声音：开/关"按钮随时关掉。

---

## 命令行参数

```
TestNotify.exe [--port 8080] [--open] [--tray]

  -p, --port <端口>   换端口（默认 8080，被占用时用这个）
  -o, --open          启动后自动打开网页大屏（默认不打开）
      --tray          启动后直接缩到托盘（配合开机自启用）
```

---

## 常见问题

**打不开大屏 / 连不上**
1. 两台电脑是否在同一网段（`ipconfig` 看前三段是否一致）；
2. 接收端窗口顶部显示的地址对不对，端口后面要带 `/`；
3. 防火墙：第一次运行要在弹窗里允许"专用网络"，或管理员运行 `allow-firewall.bat`；
4. 在接收端本机自测：`curl http://127.0.0.1:8080/api/ping`，能返回 `pong` 说明服务正常。

**端口 8080 被占用**：`TestNotify.exe --port 8090`，发送端地址同步改成 8090。

**中文乱码**
- 用 `Sender.exe`、`Send-TestResult.ps1` 或 JSON（UTF-8）发送，不要用 cmd 里的 `curl "…msg=中文"`（cmd 会用 GBK 编码，服务端按 UTF-8 解码）。
- `results.csv` 是带 BOM 的 UTF-8，Excel 直接双击打开不会乱码。

**关了窗口程序还在？**
这是设计行为：关闭/最小化窗口只是收进托盘，监听不中断，避免测试中途误关。真正退出请点窗口里的"退出"按钮，或右键托盘图标 →"退出"。任务管理器里结束进程也可以。

**开机自启**：把 `TestNotify.exe` 的快捷方式放到
`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`，并在快捷方式"目标"末尾加 ` --tray`，开机后直接蹲在托盘里。

**修改界面**：直接编辑 `dashboard.html`，保存后刷新页面就生效（exe 会检测文件变化，不用重启）。

**修改功能/重新编译**：编辑 `Receiver.cs` 或 `Sender.cs`，运行 `build.bat` 一次编译两个程序。编译用的是 Windows 自带的 `csc.exe`，无需安装 Visual Studio。

**注意**：`results.csv` 和 `dashboard.html` 必须和 exe 在同一目录（日志按 exe 所在目录生成）。
