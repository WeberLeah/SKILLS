// TestNotify - 局域网测试结果通知接收端（窗口 + 托盘）
// 编译: build.bat  (csc.exe /target:winexe /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll)
// 运行: TestNotify.exe [--port 8080] [--open] [--tray]
//
// 不再有控制台窗口：主界面是普通窗口，关闭窗口 = 退到右下角托盘继续监听，
// 托盘菜单里可以退出。收到结果时弹 Windows 原生通知（托盘气泡/操作中心通知）。
// 默认不打开网页大屏，需要时点窗口上的“打开大屏”。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

internal sealed class TestRecord
{
    public string Time;
    public string Result;
    public string Station;
    public string Sn;
    public string Msg;
    public string Ip;
}

// ---------------- 数据 ----------------

internal sealed class ResultStore
{
    private const int MaxRecords = 500;

    private readonly object sync = new object();
    private readonly object csvSync = new object();
    private readonly List<TestRecord> records = new List<TestRecord>();

    private int pass;
    private int fail;
    private int info;
    private string latest = "";

    public string CsvPath;

    public event Action<TestRecord> Added;
    public event Action Cleared;

    public int Pass { get { lock (sync) return pass; } }
    public int Fail { get { lock (sync) return fail; } }
    public int Info { get { lock (sync) return info; } }
    public string Latest { get { lock (sync) return latest; } }

    public ResultStore(string csvPath)
    {
        CsvPath = csvPath;
        EnsureCsv();
    }

    public TestRecord Add(Dictionary<string, string> d, string ip)
    {
        TestRecord r = new TestRecord();
        r.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string raw = GetParam(d, "result", "status", "state", "res", "结果", "测试结果");
        r.Result = NormalizeResult(raw);
        r.Station = GetParam(d, "station", "name", "bench", "工位", "站点").Trim();
        r.Sn = GetParam(d, "sn", "serial", "serialno", "serialnumber", "序列号", "条码").Trim();
        r.Msg = GetParam(d, "msg", "message", "note", "remark", "detail", "备注", "说明", "信息").Trim();
        if (r.Result == "INFO" && r.Msg.Length == 0 && raw.Trim().Length > 0) r.Msg = "结果=" + raw.Trim();
        r.Ip = ip ?? "";

        lock (sync)
        {
            records.Insert(0, r);
            while (records.Count > MaxRecords) records.RemoveAt(records.Count - 1);
            if (r.Result == "PASS") pass++;
            else if (r.Result == "FAIL") fail++;
            else info++;
            latest = r.Result;
        }

        AppendCsv(r);

        Action<TestRecord> handler = Added;
        if (handler != null) handler(r);
        return r;
    }

    public void Clear()
    {
        lock (sync)
        {
            records.Clear();
            pass = 0;
            fail = 0;
            info = 0;
            latest = "";
        }
        Action handler = Cleared;
        if (handler != null) handler();
    }

    public string BuildStateJson(int port, string startTime)
    {
        StringBuilder sb = new StringBuilder(4096);
        lock (sync)
        {
            sb.Append("{\"port\":").Append(port);
            sb.Append(",\"pass\":").Append(pass);
            sb.Append(",\"fail\":").Append(fail);
            sb.Append(",\"info\":").Append(info);
            sb.Append(",\"total\":").Append(pass + fail + info);
            sb.Append(",\"latest\":\"").Append(Js(latest)).Append('"');
            sb.Append(",\"start\":\"").Append(startTime).Append('"');
            sb.Append(",\"now\":\"").Append(DateTime.Now.ToString("HH:mm:ss")).Append('"');
            sb.Append(",\"records\":[");
            for (int i = 0; i < records.Count; i++)
            {
                if (i > 0) sb.Append(',');
                TestRecord r = records[i];
                sb.Append("{\"time\":\"").Append(Js(r.Time));
                sb.Append("\",\"result\":\"").Append(Js(r.Result));
                sb.Append("\",\"station\":\"").Append(Js(r.Station));
                sb.Append("\",\"sn\":\"").Append(Js(r.Sn));
                sb.Append("\",\"msg\":\"").Append(Js(r.Msg));
                sb.Append("\",\"ip\":\"").Append(Js(r.Ip)).Append("\"}");
            }
            sb.Append("]}");
        }
        return sb.ToString();
    }

    // ---- 工具 ----

    public static string NormalizeResult(string v)
    {
        if (string.IsNullOrEmpty(v)) return "INFO";
        string s = v.Trim();
        string u = s.ToUpperInvariant();
        if (u == "PASS" || u == "OK" || u == "GOOD" || u == "P" || s == "合格" || s == "通过" || s == "成功")
            return "PASS";
        if (u == "FAIL" || u == "NG" || u == "BAD" || u == "F" || s == "不合格" || s == "失败")
            return "FAIL";
        return "INFO";
    }

    public static string GetParam(Dictionary<string, string> d, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            string v;
            if (d.TryGetValue(keys[i], out v)) return v;
        }
        return "";
    }

    public static string Js(string s)
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
                    if (c < 32 || c == '<' || c == '>')
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private void EnsureCsv()
    {
        try
        {
            if (!File.Exists(CsvPath))
                File.WriteAllText(CsvPath, "时间,结果,工位,序列号,备注,来源IP\r\n", new UTF8Encoding(true));
        }
        catch { }
    }

    private void AppendCsv(TestRecord r)
    {
        try
        {
            string line = Csv(r.Time) + "," + Csv(r.Result) + "," + Csv(r.Station) + ","
                        + Csv(r.Sn) + "," + Csv(r.Msg) + "," + Csv(r.Ip) + "\r\n";
            lock (csvSync) File.AppendAllText(CsvPath, line, new UTF8Encoding(false));
        }
        catch { }
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new char[] { ',', '"', '\r', '\n' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}

// ---------------- HTTP 服务 ----------------

internal sealed class HttpServer
{
    private readonly ResultStore store;
    private readonly string htmlPath;

    private TcpListener listener;
    private Thread acceptThread;
    private volatile bool running;
    private int port;
    private string startTime = "";

    private string htmlCache;
    private DateTime htmlStamp = DateTime.MinValue;

    public HttpServer(ResultStore store, string baseDir)
    {
        this.store = store;
        htmlPath = Path.Combine(baseDir, "dashboard.html");
    }

    public bool Start(int portNumber, out string error)
    {
        error = "";
        try
        {
            port = portNumber;
            startTime = DateTime.Now.ToString("HH:mm:ss");
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start(200);
            running = true;
            acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Name = "TestNotifyAccept";
            acceptThread.Start();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Stop()
    {
        running = false;
        try { if (listener != null) listener.Stop(); } catch { }
    }

    private void AcceptLoop()
    {
        while (running)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch
            {
                if (!running) return;
                continue;
            }
            ThreadPool.QueueUserWorkItem(HandleClient, client);
        }
    }

    private void HandleClient(object state)
    {
        TcpClient client = (TcpClient)state;
        string ip = "";
        try
        {
            IPEndPoint ep = client.Client.RemoteEndPoint as IPEndPoint;
            if (ep != null) ip = ep.Address.ToString();
            client.NoDelay = true;
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;

            using (NetworkStream ns = client.GetStream())
            {
                ns.ReadTimeout = 5000;
                ns.WriteTimeout = 5000;

                byte[] buf = new byte[65536];
                int total = 0;
                int headerEnd = -1;
                while (total < buf.Length)
                {
                    int n = ns.Read(buf, total, buf.Length - total);
                    if (n <= 0) return;
                    total += n;
                    headerEnd = IndexOfHeaderEnd(buf, total);
                    if (headerEnd >= 0) break;
                }
                if (headerEnd < 0) return;

                string head = Encoding.UTF8.GetString(buf, 0, headerEnd);
                string[] lines = head.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                string[] requestLine = lines[0].Split(' ');
                if (requestLine.Length < 2) return;

                string method = requestLine[0].ToUpperInvariant();
                string target = requestLine[1];

                int contentLength = 0;
                string contentType = "";
                for (int i = 1; i < lines.Length; i++)
                {
                    int c = lines[i].IndexOf(':');
                    if (c <= 0) continue;
                    string key = lines[i].Substring(0, c).Trim().ToLowerInvariant();
                    string val = lines[i].Substring(c + 1).Trim();
                    if (key == "content-length") int.TryParse(val, out contentLength);
                    else if (key == "content-type") contentType = val;
                }
                if (contentLength < 0) contentLength = 0;
                if (contentLength > 65536) contentLength = 65536;

                int bodyStart = headerEnd + 4;
                int have = total - bodyStart;
                if (have < 0) have = 0;
                if (have > contentLength) have = contentLength;

                byte[] bodyBytes = new byte[contentLength];
                if (have > 0) Buffer.BlockCopy(buf, bodyStart, bodyBytes, 0, have);
                int got = have;
                while (got < contentLength)
                {
                    int n = ns.Read(bodyBytes, got, contentLength - got);
                    if (n <= 0) break;
                    got += n;
                }
                string body = got > 0 ? Encoding.UTF8.GetString(bodyBytes, 0, got) : "";

                Route(method, target, body, contentType, ip, ns);
            }
        }
        catch
        {
        }
        finally
        {
            try { client.Close(); } catch { }
        }
    }

    private static int IndexOfHeaderEnd(byte[] b, int len)
    {
        for (int i = 0; i + 3 < len; i++)
        {
            if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10) return i;
        }
        return -1;
    }

    private void Route(string method, string target, string body, string contentType, string ip, NetworkStream ns)
    {
        if (method == "OPTIONS")
        {
            Respond(ns, 204, "text/plain", null);
            return;
        }

        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            int scheme = target.IndexOf("//", StringComparison.Ordinal);
            int slash = scheme >= 0 ? target.IndexOf('/', scheme + 2) : -1;
            target = slash >= 0 ? target.Substring(slash) : "/";
        }

        string path = target;
        string query = "";
        int q = target.IndexOf('?');
        if (q >= 0)
        {
            path = target.Substring(0, q);
            query = target.Substring(q + 1);
        }
        path = SafeUnescape(path);
        if (path.Length > 1 && path.EndsWith("/")) path = path.Substring(0, path.Length - 1);

        if (path == "/" || path == "/index.html")
        {
            if (method == "POST" || query.Length > 0)
            {
                HandleReport(query, body, contentType, ip, ns);
                return;
            }
            Respond(ns, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(GetDashboard()));
            return;
        }

        if (path == "/report" || path == "/r" || path == "/result" || path == "/api/report")
        {
            HandleReport(query, body, contentType, ip, ns);
            return;
        }

        if (path == "/api/state" || path == "/state")
        {
            Respond(ns, 200, "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(store.BuildStateJson(port, startTime)));
            return;
        }

        if (path == "/api/clear" || path == "/clear")
        {
            store.Clear();
            Respond(ns, 200, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("OK cleared"));
            return;
        }

        if (path == "/api/ping" || path == "/ping")
        {
            Respond(ns, 200, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("pong"));
            return;
        }

        if (path == "/favicon.ico")
        {
            Respond(ns, 204, "text/plain", null);
            return;
        }

        Respond(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not Found"));
    }

    private void HandleReport(string query, string body, string contentType, string ip, NetworkStream ns)
    {
        Dictionary<string, string> p = ParseQuery(query);

        if (!string.IsNullOrEmpty(body))
        {
            string ct = (contentType ?? "").ToLowerInvariant();
            if (ct.IndexOf("json", StringComparison.Ordinal) >= 0)
            {
                ParseJsonInto(body, p);
            }
            else if (body.IndexOf('=') >= 0)
            {
                foreach (KeyValuePair<string, string> kv in ParseQuery(body)) p[kv.Key] = kv.Value;
            }
            else if (!p.ContainsKey("result"))
            {
                p["result"] = body.Trim();
            }
        }

        TestRecord r = store.Add(p, ip);
        Respond(ns, 200, "application/json; charset=utf-8",
            Encoding.UTF8.GetBytes("{\"ok\":true,\"result\":\"" + ResultStore.Js(r.Result) + "\"}"));
    }

    private static void Respond(NetworkStream ns, int code, string contentType, byte[] body)
    {
        string status;
        switch (code)
        {
            case 200: status = "200 OK"; break;
            case 204: status = "204 No Content"; break;
            case 400: status = "400 Bad Request"; break;
            case 404: status = "404 Not Found"; break;
            default: status = code.ToString(CultureInfo.InvariantCulture) + " OK"; break;
        }

        int len = body == null ? 0 : body.Length;
        StringBuilder sb = new StringBuilder();
        sb.Append("HTTP/1.1 ").Append(status).Append("\r\n");
        if (len > 0) sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
        sb.Append("Content-Length: ").Append(len).Append("\r\n");
        sb.Append("Cache-Control: no-store, no-cache, must-revalidate\r\n");
        sb.Append("Access-Control-Allow-Origin: *\r\n");
        sb.Append("Access-Control-Allow-Headers: *\r\n");
        sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
        sb.Append("Connection: close\r\n\r\n");

        byte[] header = Encoding.ASCII.GetBytes(sb.ToString());
        ns.Write(header, 0, header.Length);
        if (len > 0) ns.Write(body, 0, len);
        ns.Flush();
    }

    // ---- 参数解析 ----

    private static string SafeUnescape(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('%') < 0) return s;
        try { return Uri.UnescapeDataString(s); }
        catch { return s; }
    }

    private static Dictionary<string, string> ParseQuery(string q)
    {
        Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(q)) return d;
        string[] parts = q.Split('&');
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0) continue;
            int eq = part.IndexOf('=');
            string key = eq < 0 ? part : part.Substring(0, eq);
            string val = eq < 0 ? "" : part.Substring(eq + 1);
            key = SafeUnescape(key.Replace("+", " ")).Trim();
            val = SafeUnescape(val.Replace("+", " "));
            if (key.Length > 0) d[key] = val;
        }
        return d;
    }

    private const string JsonFieldPattern =
        @"""([^""\\]+)""\s*:\s*(?:""((?:[^""\\]|\\.)*)""|([-+0-9.eE]+)|(true|false|null))";

    private static void ParseJsonInto(string body, Dictionary<string, string> d)
    {
        MatchCollection ms = Regex.Matches(body, JsonFieldPattern);
        foreach (Match m in ms)
        {
            string key = JsonUnescape(m.Groups[1].Value);
            string val = "";
            if (m.Groups[2].Success) val = JsonUnescape(m.Groups[2].Value);
            else if (m.Groups[3].Success) val = m.Groups[3].Value;
            else if (m.Groups[4].Success) val = m.Groups[4].Value;
            if (val == "null") val = "";
            d[key] = val;
        }
    }

    private static string JsonUnescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\' || i + 1 >= s.Length)
            {
                sb.Append(c);
                continue;
            }
            char n = s[++i];
            switch (n)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    if (i + 4 < s.Length)
                    {
                        int code;
                        if (int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                    }
                    break;
                default:
                    sb.Append(n);
                    break;
            }
        }
        return sb.ToString();
    }

    private string GetDashboard()
    {
        try
        {
            if (File.Exists(htmlPath))
            {
                DateTime t = File.GetLastWriteTimeUtc(htmlPath);
                if (htmlCache == null || t != htmlStamp)
                {
                    htmlCache = File.ReadAllText(htmlPath, Encoding.UTF8);
                    htmlStamp = t;
                }
                return htmlCache;
            }
        }
        catch { }
        return "<!DOCTYPE html><meta charset=\"utf-8\"><h1>TestNotify</h1>" +
               "<p>找不到 dashboard.html，请把它放在 TestNotify.exe 同一个目录。</p>";
    }
}

// ---------------- 入口 ----------------

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static void Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }

        MainForm.Dbg("=== start ===");
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ue)
        {
            MainForm.Dbg("UNHANDLED: " + ue.ExceptionObject);
        };
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs te)
        {
            MainForm.Dbg("THREAD EXCEPTION: " + te.Exception);
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        int port = 8080;
        bool openBrowser = false;
        bool startInTray = false;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if ((a == "-p" || a == "--port") && i + 1 < args.Length)
            {
                int p;
                if (int.TryParse(args[++i], out p) && p > 0 && p < 65536) port = p;
            }
            else if (a == "-o" || a == "--open") openBrowser = true;
            else if (a == "-n" || a == "--noopen") openBrowser = false;
            else if (a == "--tray" || a == "--minimized") startInTray = true;
            else if (a == "-h" || a == "--help" || a == "/?")
            {
                MessageBox.Show(
                    "TestNotify - 局域网测试结果通知接收端\r\n\r\n" +
                    "用法: TestNotify.exe [选项]\r\n\r\n" +
                    "  -p, --port <端口>   监听端口，默认 8080\r\n" +
                    "  -o, --open          启动后自动打开网页大屏（默认不打开）\r\n" +
                    "      --tray          启动后直接最小化到托盘\r\n\r\n" +
                    "关闭窗口不会退出，程序留在托盘继续监听；\r\n" +
                    "从托盘菜单选择“退出”才真正结束。",
                    "TestNotify", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(port, openBrowser, startInTray));
    }
}

// ---------------- 主窗口 ----------------

public sealed class MainForm : Form
{
    private static readonly Color Green = Color.FromArgb(22, 163, 74);
    private static readonly Color Red = Color.FromArgb(220, 38, 38);
    private static readonly Color Gray = Color.FromArgb(100, 116, 139);
    private static readonly Color BgIdle = Color.FromArgb(241, 245, 249);
    private static readonly Color BgPass = Color.FromArgb(220, 252, 231);
    private static readonly Color BgFail = Color.FromArgb(254, 226, 226);

    private readonly int startPort;
    private readonly bool openBrowserOnStart;
    private readonly bool startInTray;

    private readonly ResultStore store;
    private readonly HttpServer server;

    private readonly Label lblAddr = new Label();
    private readonly Label lblHero = new Label();
    private readonly Label lblMeta = new Label();
    private readonly Label lblPass = new Label();
    private readonly Label lblFail = new Label();
    private readonly Label lblTotal = new Label();
    private readonly Panel heroPanel = new Panel();
    private readonly ListView list = new ListView();
    private readonly ComboBox cboNotify = new ComboBox();
    private readonly Button btnOpen = new Button();
    private readonly Button btnCopy = new Button();
    private readonly Button btnClear = new Button();
    private readonly Button btnExit = new Button();
    private readonly NotifyIcon tray = new NotifyIcon();
    private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();

    private readonly Icon iconIdle;
    private readonly Icon iconPass;
    private readonly Icon iconFail;

    private string listenUrl = "";
    private bool exiting;
    private bool trayHintShown;

    public MainForm(int port, bool openBrowser, bool startInTray)
    {
        startPort = port;
        openBrowserOnStart = openBrowser;
        this.startInTray = startInTray;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        store = new ResultStore(Path.Combine(baseDir, "results.csv"));
        server = new HttpServer(store, baseDir);
        store.Added += OnRecordAdded;
        store.Cleared += OnCleared;

        iconIdle = MakeIcon(Gray, false);
        iconPass = MakeIcon(Green, true);
        iconFail = MakeIcon(Red, false);

        BuildUi();
        LoadSettings();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        string error;
        if (!server.Start(startPort, out error))
        {
            MessageBox.Show("监听端口 " + startPort + " 启动失败：\r\n" + error +
                            "\r\n\r\n可能被其他程序占用，可换端口启动：TestNotify.exe --port 8090",
                            "TestNotify", MessageBoxButtons.OK, MessageBoxIcon.Error);
            exiting = true;
            Close();
            return;
        }

        string lan = FindLanIp();
        this.listenUrl = "http://" + lan + ":" + startPort + "/";
        lblAddr.Text = "● 监听中   " + listenUrl + "    (本机 http://127.0.0.1:" + startPort + "/)";
        lblAddr.ForeColor = Green;
        Text = "测试结果通知 - 监听中 :" + startPort;
        tray.Text = "测试结果通知 · 监听 " + startPort;

        if (startInTray)
        {
            BeginInvoke(new Action(HideToTray));
        }
        else if (openBrowserOnStart)
        {
            try { Process.Start(listenUrl); } catch { }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 注意：从外部关闭窗口(Task Manager / 脚本)时 CloseReason 是 TaskManagerClosing，
        // 只有点窗口右上角 X 才是 UserClosing，两种都收进托盘。
        if (!exiting && e.CloseReason != CloseReason.WindowsShutDown)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        SaveSettings();
        server.Stop();
        tray.Visible = false;
        base.OnFormClosing(e);
    }

    internal static void Dbg(string msg)
    {
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "testnotify_debug.log"),
                DateTime.Now.ToString("HH:mm:ss.fff ") + msg + "\r\n");
        }
        catch { }
    }

    // ---------------- 界面 ----------------

    private void BuildUi()
    {
        Text = "测试结果通知";
        ClientSize = new Size(900, 660);
        MinimumSize = new Size(760, 540);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        BackColor = Color.FromArgb(248, 250, 252);
        Icon = iconPass;

        TableLayoutPanel root = new TableLayoutPanel();
        root.Dock = DockStyle.Fill;
        root.Padding = new Padding(14, 12, 14, 12);
        root.ColumnCount = 1;
        root.RowCount = 4;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
        Controls.Add(root);

        // ---- 顶部: 标题 / 地址 / 按钮 ----
        TableLayoutPanel top = new TableLayoutPanel();
        top.Dock = DockStyle.Fill;
        top.Margin = new Padding(0);
        top.ColumnCount = 2;
        top.RowCount = 1;
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        TableLayoutPanel titleBox = new TableLayoutPanel();
        titleBox.Dock = DockStyle.Fill;
        titleBox.Margin = new Padding(0);
        titleBox.ColumnCount = 1;
        titleBox.RowCount = 2;
        titleBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        titleBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));

        Label lblTitle = new Label();
        lblTitle.Text = "测试结果通知";
        lblTitle.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
        lblTitle.AutoSize = true;
        lblTitle.Margin = new Padding(0, 0, 0, 0);
        titleBox.Controls.Add(lblTitle, 0, 0);

        lblAddr.AutoSize = true;
        lblAddr.Text = "● 正在启动…";
        lblAddr.ForeColor = Gray;
        lblAddr.Font = new Font("Microsoft YaHei UI", 10f);
        lblAddr.Margin = new Padding(0);
        titleBox.Controls.Add(lblAddr, 0, 1);
        top.Controls.Add(titleBox, 0, 0);

        FlowLayoutPanel topButtons = new FlowLayoutPanel();
        topButtons.FlowDirection = FlowDirection.RightToLeft;
        topButtons.WrapContents = false;
        topButtons.AutoSize = true;
        topButtons.Margin = new Padding(0);
        topButtons.Dock = DockStyle.Top;

        btnOpen.Text = "打开大屏";
        btnOpen.Width = 96;
        btnOpen.Height = 30;
        btnOpen.Margin = new Padding(6, 2, 0, 0);
        btnOpen.Click += delegate { OpenDashboard(); };

        btnCopy.Text = "复制地址";
        btnCopy.Width = 88;
        btnCopy.Height = 30;
        btnCopy.Margin = new Padding(6, 2, 0, 0);
        btnCopy.Click += delegate { CopyAddress(); };

        topButtons.Controls.Add(btnOpen);
        topButtons.Controls.Add(btnCopy);
        top.Controls.Add(topButtons, 1, 0);
        root.Controls.Add(top, 0, 0);

        // ---- 最近结果 ----
        heroPanel.Dock = DockStyle.Fill;
        heroPanel.Margin = new Padding(0, 0, 0, 10);
        heroPanel.BackColor = BgIdle;
        heroPanel.Padding = new Padding(18, 8, 18, 8);
        root.Controls.Add(heroPanel, 0, 1);

        TableLayoutPanel hero = new TableLayoutPanel();
        hero.Dock = DockStyle.Fill;
        hero.Margin = new Padding(0);
        hero.ColumnCount = 3;
        hero.RowCount = 1;
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250f));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
        heroPanel.Controls.Add(hero);

        lblHero.Text = "等待结果";
        lblHero.Font = new Font("Microsoft YaHei UI", 34f, FontStyle.Bold);
        lblHero.ForeColor = Gray;
        lblHero.Dock = DockStyle.Fill;
        lblHero.TextAlign = ContentAlignment.MiddleLeft;
        lblHero.Margin = new Padding(0);
        hero.Controls.Add(lblHero, 0, 0);

        lblMeta.Text = "还没有收到测试结果";
        lblMeta.Font = new Font("Microsoft YaHei UI", 10.5f);
        lblMeta.ForeColor = Color.FromArgb(71, 85, 105);
        lblMeta.Dock = DockStyle.Fill;
        lblMeta.TextAlign = ContentAlignment.MiddleLeft;
        lblMeta.Margin = new Padding(10, 0, 0, 0);
        hero.Controls.Add(lblMeta, 1, 0);

        TableLayoutPanel stats = new TableLayoutPanel();
        stats.Dock = DockStyle.Fill;
        stats.Margin = new Padding(0);
        stats.ColumnCount = 1;
        stats.RowCount = 3;
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 33.4f));

        Label[] statLabels = new Label[] { lblPass, lblFail, lblTotal };
        for (int i = 0; i < statLabels.Length; i++)
        {
            Label l = statLabels[i];
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleRight;
            l.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            l.Margin = new Padding(0);
            stats.Controls.Add(l, 0, i);
        }
        lblPass.ForeColor = Green;
        lblFail.ForeColor = Red;
        lblTotal.ForeColor = Gray;
        UpdateStats();
        hero.Controls.Add(stats, 2, 0);

        // ---- 列表 ----
        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.FullRowSelect = true;
        list.GridLines = false;
        list.HideSelection = false;
        list.BorderStyle = BorderStyle.FixedSingle;
        list.Font = new Font("Microsoft YaHei UI", 9f);
        list.Columns.Add("时间", 150);
        list.Columns.Add("结果", 72);
        list.Columns.Add("工位", 110);
        list.Columns.Add("序列号", 170);
        list.Columns.Add("备注", 250);
        list.Columns.Add("来源", 120);
        list.Resize += delegate { FitNoteColumn(); };
        root.Controls.Add(list, 0, 2);

        // ---- 底部 ----
        TableLayoutPanel bottom = new TableLayoutPanel();
        bottom.Dock = DockStyle.Fill;
        bottom.Margin = new Padding(0);
        bottom.ColumnCount = 4;
        bottom.RowCount = 1;
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        FlowLayoutPanel notifyBox = new FlowLayoutPanel();
        notifyBox.AutoSize = true;
        notifyBox.WrapContents = false;
        notifyBox.Margin = new Padding(0, 8, 0, 0);

        Label lblNotify = new Label();
        lblNotify.Text = "结果通知：";
        lblNotify.AutoSize = true;
        lblNotify.Margin = new Padding(0, 6, 4, 0);
        notifyBox.Controls.Add(lblNotify);

        cboNotify.DropDownStyle = ComboBoxStyle.DropDownList;
        cboNotify.Width = 110;
        cboNotify.Items.Add("全部结果");
        cboNotify.Items.Add("仅 FAIL");
        cboNotify.Items.Add("关闭");
        cboNotify.SelectedIndex = 0;
        cboNotify.Margin = new Padding(0, 4, 0, 0);
        notifyBox.Controls.Add(cboNotify);
        bottom.Controls.Add(notifyBox, 0, 0);

        btnClear.Text = "清空统计";
        btnClear.Width = 92;
        btnClear.Height = 30;
        btnClear.Margin = new Padding(0, 8, 8, 0);
        btnClear.Click += delegate
        {
            if (MessageBox.Show("确定清空统计和历史记录吗？(results.csv 不会被删除)",
                    "TestNotify", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                store.Clear();
            }
        };
        bottom.Controls.Add(btnClear, 2, 0);

        btnExit.Text = "退出";
        btnExit.Width = 76;
        btnExit.Height = 30;
        btnExit.Margin = new Padding(0, 8, 0, 0);
        btnExit.Click += delegate { ExitApp(); };
        bottom.Controls.Add(btnExit, 3, 0);

        root.Controls.Add(bottom, 0, 3);

        // ---- 托盘 ----
        BuildTrayMenu();
        tray.Icon = iconIdle;
        tray.Text = "测试结果通知";
        tray.Visible = true;
        tray.DoubleClick += delegate { RestoreFromTray(); };
        tray.BalloonTipClicked += delegate { RestoreFromTray(); };
    }

    private void FitNoteColumn()
    {
        if (list.Columns.Count < 6) return;
        int fixedWidth = 150 + 72 + 110 + 170 + 120;
        int w = list.ClientSize.Width - fixedWidth - 24;
        list.Columns[4].Width = Math.Max(120, w);
    }

    private void BuildTrayMenu()
    {
        ToolStripMenuItem miShow = new ToolStripMenuItem("显示主界面");
        miShow.Click += delegate { RestoreFromTray(); };

        ToolStripMenuItem miOpen = new ToolStripMenuItem("打开大屏");
        miOpen.Click += delegate { OpenDashboard(); };

        ToolStripMenuItem miClear = new ToolStripMenuItem("清空统计");
        miClear.Click += delegate { store.Clear(); };

        ToolStripMenuItem miFirewall = new ToolStripMenuItem("放行防火墙（管理员）");
        miFirewall.Click += delegate { AllowFirewall(); };

        ToolStripMenuItem miExit = new ToolStripMenuItem("退出");
        miExit.Click += delegate { ExitApp(); };

        trayMenu.Items.Add(miShow);
        trayMenu.Items.Add(miOpen);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(miClear);
        trayMenu.Items.Add(miFirewall);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(miExit);
        tray.ContextMenuStrip = trayMenu;
    }

    // ---------------- 结果处理 ----------------

    private void OnRecordAdded(TestRecord r)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<TestRecord>(OnRecordAdded), r); } catch { }
            return;
        }

        ShowLatest(r);
        UpdateStats();
        AddRow(r);
        UpdateTrayIcon(r);
        NotifyResult(r);
    }

    private void ShowLatest(TestRecord r)
    {
        if (r.Result == "PASS")
        {
            heroPanel.BackColor = BgPass;
            lblHero.ForeColor = Green;
            lblHero.Text = "PASS";
        }
        else if (r.Result == "FAIL")
        {
            heroPanel.BackColor = BgFail;
            lblHero.ForeColor = Red;
            lblHero.Text = "FAIL";
        }
        else
        {
            heroPanel.BackColor = BgIdle;
            lblHero.ForeColor = Gray;
            lblHero.Text = "INFO";
        }

        StringBuilder sb = new StringBuilder();
        if (r.Station.Length > 0) sb.Append("工位 ").Append(r.Station).Append("    ");
        if (r.Sn.Length > 0) sb.Append("SN ").Append(r.Sn).Append("    ");
        if (r.Msg.Length > 0) sb.Append(r.Msg).Append("    ");
        sb.Append(r.Time.Substring(11)).Append("    来自 ").Append(r.Ip);
        lblMeta.Text = sb.ToString();
    }

    private void UpdateStats()
    {
        lblPass.Text = "PASS  " + store.Pass;
        lblFail.Text = "FAIL  " + store.Fail;
        lblTotal.Text = "总计  " + (store.Pass + store.Fail + store.Info);
    }

    private void AddRow(TestRecord r)
    {
        string time = r.Time.Length >= 19 ? r.Time.Substring(5) : r.Time;
        ListViewItem li = new ListViewItem(new string[]
        {
            time, r.Result, r.Station, r.Sn, r.Msg, r.Ip
        });
        li.UseItemStyleForSubItems = true;
        li.ForeColor = r.Result == "PASS" ? Green : r.Result == "FAIL" ? Red : Gray;
        list.Items.Insert(0, li);
        while (list.Items.Count > 500) list.Items.RemoveAt(list.Items.Count - 1);
    }

    private void OnCleared()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(OnCleared)); } catch { }
            return;
        }
        list.Items.Clear();
        heroPanel.BackColor = BgIdle;
        lblHero.ForeColor = Gray;
        lblHero.Text = "等待结果";
        lblMeta.Text = "还没有收到测试结果";
        UpdateStats();
        tray.Icon = iconIdle;
        tray.Text = "测试结果通知 · 监听 " + startPort;
    }

    private void UpdateTrayIcon(TestRecord r)
    {
        tray.Icon = r.Result == "PASS" ? iconPass : r.Result == "FAIL" ? iconFail : iconIdle;
        tray.Text = "测试结果通知 · 最近 " + r.Result + (r.Station.Length > 0 ? " · 工位 " + r.Station : "");
    }

    private void NotifyResult(TestRecord r)
    {
        if (cboNotify.SelectedIndex == 2) return;
        if (cboNotify.SelectedIndex == 1 && r.Result != "FAIL") return;

        string title;
        if (r.Result == "PASS") title = "PASS 合格";
        else if (r.Result == "FAIL") title = "FAIL 不合格";
        else title = r.Result.Length > 0 ? r.Result : "测试结果";

        StringBuilder sb = new StringBuilder();
        if (r.Station.Length > 0) sb.Append("工位 ").Append(r.Station);
        if (r.Sn.Length > 0)
        {
            if (sb.Length > 0) sb.Append("    ");
            sb.Append("SN ").Append(r.Sn);
        }
        if (r.Msg.Length > 0)
        {
            if (sb.Length > 0) sb.Append("\r\n");
            sb.Append(r.Msg);
        }
        if (sb.Length == 0) sb.Append("来自 ").Append(r.Ip);

        ToolTipIcon icon = r.Result == "FAIL" ? ToolTipIcon.Error
                         : r.Result == "PASS" ? ToolTipIcon.Info
                         : ToolTipIcon.Warning;
        try { tray.ShowBalloonTip(5000, title, sb.ToString(), icon); } catch { }
    }

    // ---------------- 托盘 / 窗口 ----------------

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        if (!trayHintShown)
        {
            trayHintShown = true;
            try
            {
                tray.ShowBalloonTip(3000, "仍在后台监听",
                    "窗口已关闭，程序继续在托盘运行。双击托盘图标可重新打开。",
                    ToolTipIcon.Info);
            }
            catch { }
        }
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized) HideToTray();
    }

    private void ExitApp()
    {
        exiting = true;
        Close();
    }

    // ---------------- 动作 ----------------

    private void OpenDashboard()
    {
        try { Process.Start(listenUrl.Length > 0 ? listenUrl : "http://127.0.0.1:" + startPort + "/"); }
        catch { }
    }

    private void CopyAddress()
    {
        try
        {
            Clipboard.SetText(listenUrl.Length > 0 ? listenUrl : "http://127.0.0.1:" + startPort + "/");
            tray.ShowBalloonTip(1500, "已复制", "接收地址已复制到剪贴板", ToolTipIcon.Info);
        }
        catch { }
    }

    private void AllowFirewall()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                "/c netsh advfirewall firewall delete rule name=\"TestNotify " + startPort + "\" >nul 2>&1 & " +
                "netsh advfirewall firewall add rule name=\"TestNotify " + startPort +
                "\" dir=in action=allow protocol=TCP localport=" + startPort + " profile=private,domain");
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
        }
        catch
        {
            MessageBox.Show("需要管理员权限才能修改防火墙规则。", "TestNotify",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string FindLanIp()
    {
        try
        {
            IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());
            for (int i = 0; i < ips.Length; i++)
            {
                if (ips[i].AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ips[i]))
                    return ips[i].ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    // ---------------- 图标 ----------------

    private static Icon MakeIcon(Color color, bool check)
    {
        Bitmap bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (Brush b = new SolidBrush(color))
                g.FillEllipse(b, 1, 1, 30, 30);

            using (Pen p = new Pen(Color.White, 3.4f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                if (check)
                {
                    g.DrawLines(p, new PointF[]
                    {
                        new PointF(8.5f, 16.5f),
                        new PointF(13.5f, 21.5f),
                        new PointF(23.5f, 10.5f)
                    });
                }
                else
                {
                    g.DrawLine(p, 10.5f, 10.5f, 21.5f, 21.5f);
                    g.DrawLine(p, 21.5f, 10.5f, 10.5f, 21.5f);
                }
            }
        }
        IntPtr handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    // ---------------- 设置 ----------------

    private static string SettingsPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "receiver.ini");
    }

    private void LoadSettings()
    {
        try
        {
            string path = SettingsPath();
            if (!File.Exists(path)) return;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq <= 0) continue;
                string k = lines[i].Substring(0, eq).Trim().ToLowerInvariant();
                string v = lines[i].Substring(eq + 1).Trim().ToLowerInvariant();
                if (k == "notify")
                {
                    if (v == "fail") cboNotify.SelectedIndex = 1;
                    else if (v == "off") cboNotify.SelectedIndex = 2;
                    else cboNotify.SelectedIndex = 0;
                }
            }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            string notify = cboNotify.SelectedIndex == 1 ? "fail" : cboNotify.SelectedIndex == 2 ? "off" : "all";
            File.WriteAllText(SettingsPath(), "notify=" + notify + "\r\n", new UTF8Encoding(true));
        }
        catch { }
    }
}
