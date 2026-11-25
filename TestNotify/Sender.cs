// TestNotify Sender - 测试结果发送端（Windows 窗口程序）
// 编译: build.bat  (csc.exe /target:winexe /codepage:65001)
//
// 界面使用: 双击 Sender.exe，填接收端 IP/端口/工位，点 PASS / FAIL
// 命令行:   Sender.exe --send PASS --server 192.168.1.100 --port 8080 --station A1 --sn SN001
//
// 不依赖第三方库，Windows 自带 .NET Framework 即可运行。

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static void Main(string[] args)
    {
        // 命令行模式（给测试脚本调用，不弹窗口）
        if (args != null && args.Length > 0 && (HasArg(args, "--send") || HasArg(args, "--result")))
        {
            Environment.Exit(CommandLineSend(args));
            return;
        }

        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SenderForm());
    }

    private static bool HasArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    internal static string ArgValue(string[] args, string name, string def)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return i + 1 < args.Length ? args[i + 1] : def;
            string prefix = name + "=";
            if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return args[i].Substring(prefix.Length);
        }
        return def;
    }

    private static int CommandLineSend(string[] args)
    {
        string result = ArgValue(args, "--send", ArgValue(args, "--result", "PASS"));
        string server = ArgValue(args, "--server", "192.168.1.100");
        int port = 8080;
        int.TryParse(ArgValue(args, "--port", "8080"), out port);
        string station = ArgValue(args, "--station", Environment.MachineName);
        string sn = ArgValue(args, "--sn", "");
        string msg = ArgValue(args, "--msg", "");

        string url = Net.BuildBase(server, port) + "/report";
        string json = Net.BuildJson(result, station, sn, msg);
        string reply, error;
        return Net.PostJson(url, json, out reply, out error) ? 0 : 1;
    }
}

// ---------------- 网络 ----------------

internal static class Net
{
    public static string BuildBase(string server, int port)
    {
        string s = (server ?? "").Trim();
        if (s.Length == 0) s = "127.0.0.1";
        if (s.IndexOf("://", StringComparison.Ordinal) < 0) s = "http://" + s;

        int scheme = s.IndexOf("://", StringComparison.Ordinal) + 3;
        int colon = s.IndexOf(':', scheme);
        int slash = s.IndexOf('/', scheme);
        if (colon < 0 || (slash >= 0 && colon > slash))
        {
            string portText = ":" + port.ToString(CultureInfo.InvariantCulture);
            s = slash < 0 ? s + portText : s.Substring(0, slash) + portText + s.Substring(slash);
        }
        return s.TrimEnd('/');
    }

    public static string BuildJson(string result, string station, string sn, string msg)
    {
        return "{\"result\":\"" + JsonEscape(result) + "\""
             + ",\"station\":\"" + JsonEscape(station) + "\""
             + ",\"sn\":\"" + JsonEscape(sn) + "\""
             + ",\"msg\":\"" + JsonEscape(msg) + "\"}";
    }

    public static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder sb = new StringBuilder(s.Length + 8);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    public static bool PostJson(string url, string json, out string reply, out string error)
    {
        reply = "";
        error = "";
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Timeout = 4000;
            req.ReadWriteTimeout = 4000;
            req.Proxy = null;
            req.KeepAlive = false;

            byte[] data = Encoding.UTF8.GetBytes(json);
            req.ContentLength = data.Length;
            using (Stream s = req.GetRequestStream())
            {
                s.Write(data, 0, data.Length);
            }
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                reply = sr.ReadToEnd();
                return true;
            }
        }
        catch (WebException we)
        {
            error = we.Message;
            if (we.Response != null)
            {
                try
                {
                    using (StreamReader sr = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8))
                    {
                        string body = sr.ReadToEnd();
                        if (body.Length > 0) error = body;
                    }
                }
                catch { }
            }
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool GetText(string url, int timeoutMs, out string text)
    {
        text = "";
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.Proxy = null;
            req.KeepAlive = false;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                text = sr.ReadToEnd();
                return true;
            }
        }
        catch (Exception ex)
        {
            text = ex.Message;
            return false;
        }
    }
}

// ---------------- 界面 ----------------

public sealed class SenderForm : Form
{
    private const int EM_SETCUEBANNER = 0x1501;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    private readonly TextBox txtServer = new TextBox();
    private readonly TextBox txtPort = new TextBox();
    private readonly TextBox txtStation = new TextBox();
    private readonly TextBox txtSn = new TextBox();
    private readonly TextBox txtMsg = new TextBox();
    private readonly Button btnPing = new Button();
    private readonly Button btnPass = new Button();
    private readonly Button btnFail = new Button();
    private readonly Label lblStatus = new Label();
    private readonly RichTextBox log = new RichTextBox();

    private readonly object sendLock = new object();
    private DateTime lastSend = DateTime.MinValue;

    private static readonly Color Green = Color.FromArgb(22, 163, 74);
    private static readonly Color Red = Color.FromArgb(220, 38, 38);
    private static readonly Color Gray = Color.FromArgb(100, 116, 139);

    public SenderForm()
    {
        Text = "测试结果发送端 - TestNotify";
        ClientSize = new Size(760, 640);
        MinimumSize = new Size(680, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        BackColor = Color.FromArgb(244, 246, 249);

        TableLayoutPanel root = new TableLayoutPanel();
        root.Dock = DockStyle.Fill;
        root.Padding = new Padding(14, 12, 14, 12);
        root.ColumnCount = 1;
        root.RowCount = 5;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(root);

        // 第 0 行: 接收端 IP / 端口 / 检测 / 状态
        FlowLayoutPanel row0 = NewRow();
        row0.Controls.Add(NewLabel("接收端"));
        txtServer.Width = 170;
        txtServer.Margin = new Padding(0, 6, 6, 0);
        Cue(txtServer, "192.168.1.100");
        row0.Controls.Add(txtServer);
        txtPort.Width = 70;
        txtPort.Margin = new Padding(0, 6, 6, 0);
        Cue(txtPort, "8080");
        row0.Controls.Add(txtPort);
        btnPing.Text = "检测";
        btnPing.Width = 68;
        btnPing.Height = 30;
        btnPing.Margin = new Padding(2, 5, 0, 0);
        btnPing.Click += delegate { PingServer(false); };
        row0.Controls.Add(btnPing);
        lblStatus.AutoSize = true;
        lblStatus.Text = "● 未检测";
        lblStatus.ForeColor = Gray;
        lblStatus.Margin = new Padding(14, 9, 0, 0);
        row0.Controls.Add(lblStatus);
        root.Controls.Add(row0, 0, 0);

        // 第 1 行: 工位 / SN
        FlowLayoutPanel row1 = NewRow();
        row1.Controls.Add(NewLabel("工位"));
        txtStation.Width = 130;
        txtStation.Margin = new Padding(0, 6, 18, 0);
        Cue(txtStation, "例如 A1");
        row1.Controls.Add(txtStation);
        row1.Controls.Add(NewLabel("SN"));
        txtSn.Width = 240;
        txtSn.Font = new Font("Microsoft YaHei UI", 12f);
        txtSn.Margin = new Padding(0, 3, 12, 0);
        Cue(txtSn, "扫描或输入序列号");
        txtSn.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Send("PASS");
            }
        };
        row1.Controls.Add(txtSn);
        Label hint = new Label();
        hint.AutoSize = true;
        hint.Text = "扫入 SN 后按回车 = 发送 PASS";
        hint.ForeColor = Gray;
        hint.Margin = new Padding(0, 9, 0, 0);
        row1.Controls.Add(hint);
        root.Controls.Add(row1, 0, 1);

        // 第 2 行: 备注
        TableLayoutPanel p2 = new TableLayoutPanel();
        p2.Dock = DockStyle.Fill;
        p2.Margin = new Padding(0);
        p2.ColumnCount = 1;
        p2.RowCount = 2;
        p2.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
        p2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Label lblMsg = new Label();
        lblMsg.Text = "备注（FAIL 时填写原因，可不填）";
        lblMsg.AutoSize = true;
        lblMsg.ForeColor = Color.FromArgb(51, 65, 85);
        lblMsg.Margin = new Padding(0, 0, 0, 2);
        p2.Controls.Add(lblMsg, 0, 0);
        txtMsg.Multiline = true;
        txtMsg.Dock = DockStyle.Fill;
        txtMsg.ScrollBars = ScrollBars.Vertical;
        txtMsg.Margin = new Padding(0, 0, 0, 8);
        p2.Controls.Add(txtMsg, 0, 1);
        root.Controls.Add(p2, 0, 2);

        // 第 3 行: PASS / FAIL
        TableLayoutPanel p3 = new TableLayoutPanel();
        p3.Dock = DockStyle.Fill;
        p3.Margin = new Padding(0);
        p3.ColumnCount = 2;
        p3.RowCount = 1;
        p3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        p3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        StyleBigButton(btnPass, "PASS  合格", Green);
        btnPass.Margin = new Padding(0, 0, 6, 8);
        btnPass.Click += delegate { Send("PASS"); };
        p3.Controls.Add(btnPass, 0, 0);

        StyleBigButton(btnFail, "FAIL  不合格", Red);
        btnFail.Margin = new Padding(6, 0, 0, 8);
        btnFail.Click += delegate { Send("FAIL"); };
        p3.Controls.Add(btnFail, 1, 0);

        root.Controls.Add(p3, 0, 3);

        // 第 4 行: 日志
        log.Dock = DockStyle.Fill;
        log.ReadOnly = true;
        log.BackColor = Color.White;
        log.BorderStyle = BorderStyle.FixedSingle;
        log.Font = new Font("Microsoft YaHei UI", 9f);
        log.Margin = new Padding(0);
        log.TabStop = false;
        root.Controls.Add(log, 0, 4);

        LoadConfig();
        FormClosing += delegate { SaveConfig(); };

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        timer.Interval = 15000;
        timer.Tick += delegate { PingServer(true); };
        timer.Start();

        txtServer.TabIndex = 0;
        txtPort.TabIndex = 1;
        txtStation.TabIndex = 2;
        txtSn.TabIndex = 3;
        txtMsg.TabIndex = 4;
        btnPass.TabIndex = 5;
        btnFail.TabIndex = 6;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Log("接收端: " + Net.BuildBase(txtServer.Text, ParsePort()), Gray);
        Log("提示: 扫描 SN 后按回车发 PASS；不合格点 FAIL 并填写备注", Gray);
        txtSn.Focus();
        PingServer(true);
    }

    // ---------------- 控件构造 ----------------

    private static FlowLayoutPanel NewRow()
    {
        FlowLayoutPanel p = new FlowLayoutPanel();
        p.Dock = DockStyle.Fill;
        p.WrapContents = false;
        p.Margin = new Padding(0);
        return p;
    }

    private static Label NewLabel(string text)
    {
        Label l = new Label();
        l.Text = text;
        l.AutoSize = true;
        l.ForeColor = Color.FromArgb(51, 65, 85);
        l.Margin = new Padding(0, 9, 6, 0);
        return l;
    }

    private static void StyleBigButton(Button b, string text, Color back)
    {
        b.Text = text;
        b.Dock = DockStyle.Fill;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(back, 0.08f);
        b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.2f);
        b.BackColor = back;
        b.ForeColor = Color.White;
        b.Font = new Font("Microsoft YaHei UI", 21f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
    }

    private static void Cue(TextBox box, string text)
    {
        box.HandleCreated += delegate
        {
            try { SendMessage(box.Handle, EM_SETCUEBANNER, (IntPtr)1, text); } catch { }
        };
    }

    // ---------------- 业务 ----------------

    private int ParsePort()
    {
        int port;
        if (!int.TryParse(txtPort.Text.Trim(), out port) || port <= 0 || port > 65535) return 8080;
        return port;
    }

    private void Send(string result)
    {
        string server = txtServer.Text.Trim();
        if (server.Length == 0)
        {
            Log("请先填写接收端 IP", Red);
            txtServer.Focus();
            return;
        }

        lock (sendLock)
        {
            if ((DateTime.Now - lastSend).TotalMilliseconds < 250) return;
            lastSend = DateTime.Now;
        }

        int port = ParsePort();
        string station = txtStation.Text.Trim();
        string sn = txtSn.Text.Trim();
        string msg = txtMsg.Text.Trim();
        string url = Net.BuildBase(server, port) + "/report";
        string json = Net.BuildJson(result, station, sn, msg);
        SaveConfig();

        ThreadPool.QueueUserWorkItem(delegate
        {
            string reply, error;
            bool ok = Net.PostJson(url, json, out reply, out error);
            try
            {
                BeginInvoke(new Action(delegate { OnSent(ok, result, station, sn, msg, error); }));
            }
            catch { }
        });
    }

    private void OnSent(bool ok, string result, string station, string sn, string msg, string error)
    {
        if (ok)
        {
            SetStatus(1);
            string line = result + "  已发送";
            if (station.Length > 0) line += "  工位 " + station;
            if (sn.Length > 0) line += "  SN=" + sn;
            if (msg.Length > 0) line += "  " + msg;
            Log(line, result == "PASS" ? Green : Red);

            if (result == "PASS")
            {
                txtSn.Clear();
                txtMsg.Clear();
            }
            txtSn.Focus();
            txtSn.SelectAll();
        }
        else
        {
            SetStatus(2);
            Log("发送失败: " + error, Red);
            txtSn.Focus();
        }
    }

    private void PingServer(bool silent)
    {
        if (!silent) SetStatus(0);
        int port = ParsePort();
        string url = Net.BuildBase(txtServer.Text, port) + "/api/ping";

        ThreadPool.QueueUserWorkItem(delegate
        {
            string text;
            bool ok = Net.GetText(url, 2500, out text);
            try
            {
                BeginInvoke(new Action(delegate { SetStatus(ok ? 1 : 2); }));
            }
            catch { }
        });
    }

    private void SetStatus(int state)
    {
        if (state == 1)
        {
            lblStatus.Text = "● 已连接";
            lblStatus.ForeColor = Green;
        }
        else if (state == 2)
        {
            lblStatus.Text = "● 连接失败";
            lblStatus.ForeColor = Red;
        }
        else
        {
            lblStatus.Text = "● 检测中…";
            lblStatus.ForeColor = Gray;
        }
    }

    private void Log(string text, Color color)
    {
        if (log.InvokeRequired)
        {
            log.BeginInvoke(new Action(delegate { Log(text, color); }));
            return;
        }
        log.SelectionStart = log.TextLength;
        log.SelectionLength = 0;
        log.SelectionColor = color;
        log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
    }

    // ---------------- 配置 ----------------

    private static string ConfigPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sender.ini");
    }

    private void LoadConfig()
    {
        string server = "", port = "", station = "";
        try
        {
            string path = ConfigPath();
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    int eq = lines[i].IndexOf('=');
                    if (eq <= 0) continue;
                    string k = lines[i].Substring(0, eq).Trim().ToLowerInvariant();
                    string v = lines[i].Substring(eq + 1).Trim();
                    if (k == "server") server = v;
                    else if (k == "port") port = v;
                    else if (k == "station") station = v;
                }
            }
        }
        catch { }

        txtServer.Text = server.Length > 0 ? server : "192.168.1.100";
        txtPort.Text = port.Length > 0 ? port : "8080";
        txtStation.Text = station.Length > 0 ? station : Environment.MachineName;
    }

    private void SaveConfig()
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("server=" + txtServer.Text.Trim());
            sb.AppendLine("port=" + txtPort.Text.Trim());
            sb.AppendLine("station=" + txtStation.Text.Trim());
            File.WriteAllText(ConfigPath(), sb.ToString(), new UTF8Encoding(true));
        }
        catch { }
    }
}
