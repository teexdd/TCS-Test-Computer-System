using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

// State: 0 pending, 1 running, 2 ok, 3 warning, 4 fail, 5 skipped
class Res
{
    public int State; public string Detail;
    public Res(int s, string d) { State = s; Detail = d; }
}

static class Checks
{
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr h, IntPtr min, IntPtr max);

    static readonly StringBuilder R = new StringBuilder();
    static readonly List<string> warns = new List<string>();
    static readonly List<string> summary = new List<string>();
    public static string DeviceType = "?";
    public static bool Notebook;
    public static bool Saved;
    public static string ReportPath = "";
    public static DateTime Started = DateTime.Now;
    static long peakMb;

    public static void Log(string s) { lock (R) R.AppendLine(s); }
    static void Section(string t) { Log(""); Log("=== " + t + " ==="); }
    public static void Warn(string sev, string msg) { lock (warns) warns.Add(sev + "|" + msg); Log("  ! [" + sev + "] " + msg); }
    public static readonly Dictionary<string, Res> Results = new Dictionary<string, Res>();
    public static string Model = "";
    public static double LastMbps = -1;
    public static void Summ(string key, Res r)
    {
        string[] t = { "WAIT", "RUN", "OK", "WARN", "FAIL", "SKIP" };
        lock (summary) summary.Add(string.Format("[{0,-4}] {1}: {2}", t[r.State], key, r.Detail));
        lock (Results) Results[key] = r;
    }
    public static int WarnCount { get { lock (warns) return warns.Count; } }
    public static List<string> WarnList() { lock (warns) return new List<string>(warns); }
    public static string Dump() { lock (R) return R.ToString(); }

    // identifies the current Windows boot (used to match the results the boot-time task saved before sign-in)
    public static string BootId()
    {
        string id = "";
        Wmi("SELECT LastBootUpTime FROM Win32_OperatingSystem", o => id = Str(o, "LastBootUpTime"));
        return id;
    }

    // Results produced by the boot task (runs as SYSTEM before sign-in). Returns null unless they belong to THIS boot and language.
    public static Dictionary<string, Res> LoadBoot()
    {
        try
        {
            string f = Path.Combine(Cfg.BootDir, "data", "boot.txt");
            if (!File.Exists(f)) return null;
            var d = new Dictionary<string, Res>(); var ws = new List<string>();
            string boot = "", lang = "", type = "", model = "";
            foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
            {
                int i = line.IndexOf('='); if (i <= 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                if (k == "boot") boot = v; else if (k == "lang") lang = v; else if (k == "type") type = v; else if (k == "model") model = v;
                else if (k == "warn") ws.Add(v);
                else if (k == "res") { string[] p = v.Split(new char[] { '|' }, 3); int st; if (p.Length == 3 && int.TryParse(p[1], out st)) d[p[0]] = new Res(st, p[2]); }
            }
            if (boot.Length == 0 || boot != BootId() || lang != L.Code || d.Count == 0) return null;
            DeviceType = type; Notebook = type == "Notebook"; Model = model;
            foreach (string w in ws) { int j = w.IndexOf('|'); if (j > 0) Warn(w.Substring(0, j), w.Substring(j + 1)); }
            string rep = Path.Combine(Cfg.BootDir, "data", "boot_report.txt");
            if (File.Exists(rep)) { Log("--- checks done at boot, before sign-in ---"); Log(File.ReadAllText(rep, Encoding.UTF8).TrimEnd()); }
            return d;
        }
        catch { return null; }
    }

    public static double MemMb()
    {
        using (var p = Process.GetCurrentProcess())
        {
            p.Refresh();
            long pk = p.PeakWorkingSet64 / 1048576;
            if (pk > peakMb) peakMb = pk;
            return p.WorkingSet64 / 1048576.0;
        }
    }
    public static long PeakMb { get { MemMb(); return peakMb; } }

    // free everything & shrink working set after each stage
    public static void Trim()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        try { using (var p = Process.GetCurrentProcess()) SetProcessWorkingSetSize(p.Handle, (IntPtr)(-1), (IntPtr)(-1)); } catch { }
    }

    static void Wmi(string query, Action<ManagementBaseObject> f)
    {
        using (var s = new ManagementObjectSearcher(query))
        using (var c = s.Get())
            foreach (ManagementBaseObject o in c)
                using (o) f(o);
    }
    static string Str(ManagementBaseObject o, string p)
    {
        try { object v = o[p]; return v == null ? "" : v.ToString().Trim(); } catch { return ""; }
    }

    // ------------------------------------------------------------------ system info + PC/Notebook
    public static Res SystemInfo()
    {
        Log(Cfg.AppName + " v" + Cfg.Version);
        Log("Started   : " + Started.ToString("yyyy-MM-dd HH:mm:ss"));
        Log("Computer  : " + Environment.MachineName + (Cfg.UserName ? "   User: " + Environment.UserName : ""));
        Section("SYSTEM INFO");
        var chassis = new List<int>();
        int pcst = 0; bool battery = false; string maker = "", model = "";
        Wmi("SELECT Manufacturer, Model, PCSystemType, TotalPhysicalMemory FROM Win32_ComputerSystem", o =>
        {
            maker = Str(o, "Manufacturer"); model = Str(o, "Model");
            int.TryParse(Str(o, "PCSystemType"), out pcst);
            long ram; long.TryParse(Str(o, "TotalPhysicalMemory"), out ram);
            Log("Maker/Model: " + maker + " " + model);
            Log("RAM        : " + (ram / 1073741824.0).ToString("0.0") + " GB");
        });
        Wmi("SELECT Caption, Version FROM Win32_OperatingSystem", o => Log("OS         : " + Str(o, "Caption") + " (" + Str(o, "Version") + ") " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")));
        Wmi("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor", o => Log("CPU        : " + Str(o, "Name") + " (" + Str(o, "NumberOfCores") + "C/" + Str(o, "NumberOfLogicalProcessors") + "T)"));
        Wmi("SELECT Name FROM Win32_VideoController", o => Log("GPU        : " + Str(o, "Name")));
        Wmi("SELECT Model, Size FROM Win32_DiskDrive", o => { long sz; long.TryParse(Str(o, "Size"), out sz); Log("Disk       : " + Str(o, "Model") + " " + (sz / 1000000000) + " GB"); });
        Wmi("SELECT SMBIOSBIOSVersion, SerialNumber FROM Win32_BIOS", o => Log("BIOS       : " + Str(o, "SMBIOSBIOSVersion") + (Cfg.Serial ? "   Serial: " + Str(o, "SerialNumber") : "")));
        Wmi("SELECT ChassisTypes FROM Win32_SystemEnclosure", o => { var a = o["ChassisTypes"] as ushort[]; if (a != null) foreach (ushort c in a) chassis.Add(c); });
        Wmi("SELECT Name FROM Win32_Battery", o => battery = true);

        int[] portable = { 8, 9, 10, 14, 30, 31, 32 };
        int[] desktop = { 3, 4, 5, 6, 7, 13, 15, 16, 23, 24, 34, 35, 36 };
        string ch = string.Join(",", chassis.Select(c => c.ToString()).ToArray());
        string type, why;
        if (chassis.Any(c => portable.Contains(c))) { type = "Notebook"; why = "chassis " + ch; }
        else if (pcst == 2) { type = "Notebook"; why = "PCSystemType=Mobile"; }
        else if (chassis.Any(c => desktop.Contains(c))) { type = "PC"; why = "chassis " + ch; }
        else if (battery) { type = "Notebook"; why = "battery present"; }
        else { type = "PC"; why = "no battery"; }
        DeviceType = type; Notebook = type == "Notebook";
        Log("Device type: " + type + "  [" + why + "; battery=" + (battery ? "yes" : "no") + "]");

        int st = 2;
        try
        {
            var d = new DriveInfo("C");
            double free = d.AvailableFreeSpace * 100.0 / d.TotalSize;
            Log(string.Format("Drive C:    : {0:0.0} GB free of {1:0.0} GB ({2:0}% free)", d.AvailableFreeSpace / 1073741824.0, d.TotalSize / 1073741824.0, free));
            if (free < 10) { st = 3; Warn("warn", L.F("w.disk", (int)free)); }
        }
        catch { }
        Model = (maker + " " + model).Trim();
        string detail = L.T(Notebook ? "t.nb" : "t.pc") + (Model.Length > 0 ? " | " + Model : "");
        return new Res(st, detail);
    }

    // ------------------------------------------------------------------ drivers
    static string ErrDesc(int code)
    {
        switch (code)
        {
            case 1: return "not configured correctly";
            case 10: return "device cannot start";
            case 12: return "resource conflict";
            case 22: return "device is disabled";
            case 28: return "DRIVER NOT INSTALLED";
            case 31: return "device not working properly";
            case 39: return "driver corrupted / missing";
            case 43: return "device reported a problem";
            case 52: return "driver signature cannot be verified";
            default: return "problem code " + code;
        }
    }

    public static Res Drivers()
    {
        Section("DRIVERS");
        string[] want = { "DISPLAY", "NET", "MEDIA", "BLUETOOTH", "CAMERA", "IMAGE", "USB", "KEYBOARD", "MOUSE", "DISKDRIVE", "BATTERY" };
        var found = new Dictionary<string, List<string>>();
        foreach (string w in want) found[w] = new List<string>();
        bool generic = false;
        Wmi("SELECT DeviceName, DeviceClass, DriverVersion, DriverDate, DriverProviderName FROM Win32_PnPSignedDriver", o =>
        {
            string cls = Str(o, "DeviceClass").ToUpperInvariant(), name = Str(o, "DeviceName");
            if (name == "" || !found.ContainsKey(cls)) return;
            string date = "";
            try { date = ManagementDateTimeConverter.ToDateTime(Str(o, "DriverDate")).ToString("yyyy-MM-dd"); } catch { }
            string line = string.Format("{0}  v{1}  {2}  ({3})", name, Str(o, "DriverVersion"), date, Str(o, "DriverProviderName"));
            if (!found[cls].Contains(line)) found[cls].Add(line);
            if (cls == "DISPLAY" && (name.IndexOf("Basic Display", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Basic Render", StringComparison.OrdinalIgnoreCase) >= 0)) generic = true;
        });
        foreach (string w in want)
        {
            if (w == "IMAGE") continue;
            var lst = new List<string>(found[w]);
            if (w == "CAMERA") lst.AddRange(found["IMAGE"]);
            Log(string.Format("[{0}] {1} driver(s)", w, lst.Count));
            foreach (string l in lst.Take(12)) Log("    " + l);
        }
        var problems = new List<string>(); bool severe = false;
        Wmi("SELECT Name, PNPClass, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0", o =>
        {
            int code; int.TryParse(Str(o, "ConfigManagerErrorCode"), out code);
            string n = Str(o, "Name"); if (n == "") n = "(unknown device)";
            if (code == 28 || code == 39 || code == 43) severe = true;
            problems.Add(string.Format("{0} [{1}] code {2}: {3}", n, Str(o, "PNPClass"), code, ErrDesc(code)));
        });
        Log("Devices with problems: " + problems.Count);
        foreach (string p in problems) Warn("fail", L.F("w.drv", p));
        if (generic) { severe = true; Warn("fail", L.T("w.gpu")); }
        if (found["DISPLAY"].Count == 0) { severe = true; Warn("fail", L.T("w.nodisp")); }
        if (found["NET"].Count == 0) { severe = true; Warn("fail", L.T("w.nonet")); }
        int issues = problems.Count + (generic ? 1 : 0);
        if (issues == 0 && !severe) return new Res(2, L.T("r.drv.ok"));
        return new Res(severe ? 4 : 3, L.F("r.drv.bad", Math.Max(issues, 1)));
    }

    // ------------------------------------------------------------------ boot / shutdown history
    public static Res Boot()
    {
        Section("POWER ON / OFF HISTORY (last 30 days)");
        string last = "";
        Wmi("SELECT LastBootUpTime FROM Win32_OperatingSystem", o =>
        {
            DateTime b = ManagementDateTimeConverter.ToDateTime(Str(o, "LastBootUpTime"));
            TimeSpan up = DateTime.Now - b;
            last = b.ToString("yyyy-MM-dd HH:mm");
            Log("Last boot : " + b.ToString("yyyy-MM-dd HH:mm:ss"));
            Log(string.Format("Uptime    : {0}d {1}h {2}m", up.Days, up.Hours, up.Minutes));
        });
        int bad = 0, total = 0;
        string q = "*[System[(EventID=6005 or EventID=6006 or EventID=6008 or EventID=41 or EventID=1074) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]";
        try
        {
            var query = new EventLogQuery("System", PathType.LogName, q);
            query.ReverseDirection = true;
            using (var rd = new EventLogReader(query))
            {
                EventRecord e;
                while (total < 80 && (e = rd.ReadEvent()) != null)
                    using (e)
                    {
                        string what;
                        switch (e.Id)
                        {
                            case 6005: what = "POWER ON  (event log started)"; break;
                            case 6006: what = "POWER OFF (clean shutdown)"; break;
                            case 6008: what = "UNEXPECTED shutdown"; bad++; break;
                            case 41: what = "Kernel-Power: reboot without clean shutdown"; bad++; break;
                            case 1074:
                                what = "Shutdown/restart requested";
                                try { what += " by " + e.Properties[0].Value + " (" + e.Properties[4].Value + ")"; } catch { }
                                break;
                            default: what = "event " + e.Id; break;
                        }
                        Log(string.Format("{0:yyyy-MM-dd HH:mm:ss}  {1}", e.TimeCreated, what));
                        total++;
                    }
            }
        }
        catch (Exception ex) { Log("Cannot read System event log: " + ex.Message); return new Res(3, L.T("r.boot.noread")); }
        if (bad > 0) { Warn("warn", L.F("w.unexp", bad)); return new Res(3, L.F("r.boot.bad", bad)); }
        return new Res(2, L.F("r.boot.last", last));
    }

    // ------------------------------------------------------------------ applications
    static string Rot13(string s)
    {
        var c = s.ToCharArray();
        for (int i = 0; i < c.Length; i++)
        {
            char ch = c[i];
            if (ch >= 'a' && ch <= 'z') c[i] = (char)((ch - 'a' + 13) % 26 + 'a');
            else if (ch >= 'A' && ch <= 'Z') c[i] = (char)((ch - 'A' + 13) % 26 + 'A');
        }
        return new string(c);
    }
    class AppRec { public string Name; public int Count; public DateTime Last; }

    public static Res Apps()
    {
        Section("APPLICATIONS");
        Log("-- Open now (with a window) --");
        int me = Process.GetCurrentProcess().Id, open = 0;
        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == me || p.MainWindowHandle == IntPtr.Zero || p.MainWindowTitle.Length == 0) continue;
                string start = ""; try { start = p.StartTime.ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
                Log(string.Format("{0}.exe  \"{1}\"  started {2}  RAM {3} MB", p.ProcessName, p.MainWindowTitle, start, p.WorkingSet64 / 1048576));
                open++;
            }
            catch { }
            finally { p.Dispose(); }
        }
        Log("");
        Log("-- Recently launched (Windows UserAssist, current user, newest first) --");
        var list = new List<AppRec>();
        string[] guids = { "{CEBFF5CD-ACE2-4F4F-9178-9926F41749EA}", "{F4E57C4B-2036-45F0-A9AB-443BCFE33D9F}" };
        foreach (string g in guids)
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist\" + g + @"\Count"))
            {
                if (k == null) continue;
                foreach (string vn in k.GetValueNames())
                {
                    var data = k.GetValue(vn) as byte[];
                    if (data == null || data.Length < 68) continue;
                    long ft = BitConverter.ToInt64(data, 60);
                    if (ft <= 0) continue;
                    DateTime last; try { last = DateTime.FromFileTime(ft); } catch { continue; }
                    list.Add(new AppRec { Name = Rot13(vn), Count = BitConverter.ToInt32(data, 4), Last = last });
                }
            }
        foreach (AppRec a in list.OrderByDescending(x => x.Last).Take(40))
            Log(string.Format("{0:yyyy-MM-dd HH:mm:ss}  runs:{1,-4} {2}", a.Last, a.Count, a.Name));
        Log("Note: Windows keeps no full launch history by default; this covers GUI apps started by this user.");
        return new Res(2, L.F("r.apps", open, list.Count));
    }

    // ------------------------------------------------------------------ network
    static string PingTest(string host, int count, out bool ok)
    {
        int good = 0; long sum = 0;
        using (var p = new Ping())
            for (int i = 0; i < count; i++)
                try { var r = p.Send(host, 2000); if (r.Status == IPStatus.Success) { good++; sum += r.RoundtripTime; } } catch { }
        ok = good > 0;
        return string.Format("Ping {0}: {1}/{2} replies, avg {3}", host, good, count, good > 0 ? (sum / good) + " ms" : "-");
    }

    public static Res Network()
    {
        Section("NETWORK");
        string gateway = null;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
            var ip = ni.GetIPProperties();
            string addr = string.Join(",", ip.UnicastAddresses.Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.Address.ToString()).ToArray());
            string gw = string.Join(",", ip.GatewayAddresses.Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.Address.ToString()).ToArray());
            Log(string.Format("Adapter: {0} [{1}] {2} Mbps  IP {3}  GW {4}", ni.Name, ni.NetworkInterfaceType, ni.Speed / 1000000, addr, gw));
            if (gateway == null && gw != "") gateway = gw.Split(',')[0];
        }
        bool ok, pingOk = false, dnsOk = false, httpOk = false;
        if (gateway != null) Log(PingTest(gateway, 3, out ok));
        Log(PingTest("8.8.8.8", 3, out ok)); pingOk |= ok;
        Log(PingTest("1.1.1.1", 3, out ok)); pingOk |= ok;
        try
        {
            var sw = Stopwatch.StartNew();
            var a = Dns.GetHostAddresses("www.microsoft.com");
            Log(string.Format("DNS www.microsoft.com -> {0} ({1} ms)", a.Length > 0 ? a[0].ToString() : "-", sw.ElapsedMilliseconds));
            dnsOk = a.Length > 0;
        }
        catch (Exception e) { Log("DNS FAILED: " + e.Message); }
        try
        {
            var req = (HttpWebRequest)WebRequest.Create("http://www.msftconnecttest.com/connecttest.txt");
            req.Timeout = 8000; req.UserAgent = Cfg.AppName;
            using (var resp = (HttpWebResponse)req.GetResponse()) { Log("HTTP internet check: " + (int)resp.StatusCode); httpOk = (int)resp.StatusCode == 200; }
        }
        catch (Exception e) { Log("HTTP check FAILED: " + e.Message); }

        double mbps = -1;
        if (Cfg.NetSpeed && httpOk)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://speed.cloudflare.com/__down?bytes=8000000");
                req.Timeout = 10000; req.ReadWriteTimeout = 10000; req.UserAgent = Cfg.AppName;
                var sw = Stopwatch.StartNew(); long total = 0;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var st = resp.GetResponseStream())
                {
                    var buf = new byte[65536]; int n;
                    while ((n = st.Read(buf, 0, buf.Length)) > 0) { total += n; if (sw.ElapsedMilliseconds > 10000) break; }
                }
                mbps = total * 8.0 / 1000000.0 / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
                Log(string.Format("Download speed: {0:0.0} Mbps", mbps));
            }
            catch (Exception e) { Log("Speed test FAILED: " + e.Message); }
        }
        if (!(pingOk && dnsOk && httpOk))
        {
            Func<bool, string> yn = delegate(bool b) { return L.T(b ? "st.ok" : "st.fail"); };
            Warn("fail", L.F("w.net", yn(pingOk), yn(dnsOk), yn(httpOk)));
            return new Res(4, L.T("r.net.fail"));
        }
        LastMbps = mbps;
        if (mbps >= 0 && mbps < 3) { Warn("warn", L.F("w.slow", mbps.ToString("0.0"))); return new Res(3, L.F("r.net.slow", mbps.ToString("0.0"))); }
        return new Res(2, mbps >= 0 ? L.F("r.net.speed", mbps.ToString("0.0")) : L.T("r.net.ok"));
    }

    // ------------------------------------------------------------------ speaker
    public static bool HasAudio()
    {
        int n = 0;
        Section("SPEAKER");
        Wmi("SELECT Name, Status FROM Win32_SoundDevice", o => { n++; Log("Audio device: " + Str(o, "Name") + " [" + Str(o, "Status") + "]"); });
        if (n == 0) Warn("fail", L.T("w.noaudio"));
        return n > 0;
    }

    // ch: 0 left, 1 right, 2 both
    static byte[] Wav(int ch, double hz, double sec)
    {
        int sr = 44100, n = (int)(sr * sec);
        using (var ms = new MemoryStream(44 + n * 4))
        using (var w = new BinaryWriter(ms))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 4);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(sr); w.Write(sr * 4); w.Write((short)4); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 4);
            for (int i = 0; i < n; i++)
            {
                double env = Math.Min(1.0, Math.Min(i, n - i) / (sr * 0.03));
                short v = (short)(Math.Sin(2 * Math.PI * hz * i / sr) * env * 0.30 * 32767);
                w.Write((short)(ch == 1 ? 0 : v)); w.Write((short)(ch == 0 ? 0 : v));
            }
            w.Flush();
            return ms.ToArray();
        }
    }
    public static void PlayTone(int ch)
    {
        byte[] wav = Wav(ch, ch == 0 ? 440 : ch == 1 ? 660 : 550, 0.9);
        using (var ms = new MemoryStream(wav))
        using (var sp = new System.Media.SoundPlayer(ms)) { sp.Load(); sp.PlaySync(); }
    }

    static void SaveLast()
    {
        Directory.CreateDirectory(Cfg.DataDir);
        LastRun l = LastRun.Load();
        lock (Results) foreach (var kv in Results) l.R[kv.Key] = kv.Value;
        int score = LastRun.ScoreOf(l.R);
        var sb = new StringBuilder();
        sb.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        sb.AppendLine("type=" + DeviceType);
        sb.AppendLine("model=" + (Model.Length > 0 ? Model : l.Model));
        foreach (var kv in l.R) sb.AppendLine("res=" + kv.Key + "|" + kv.Value.State + "|" + kv.Value.Detail.Replace("\r", " ").Replace("\n", " "));
        File.WriteAllText(LastRun.FilePath, sb.ToString(), new UTF8Encoding(false));
        File.AppendAllText(Path.Combine(Cfg.DataDir, "history.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "|" + score + "|" + LastMbps.ToString("0.0") + "\r\n", new UTF8Encoding(false));
    }

    // ------------------------------------------------------------------ save report
    public static string Save()
    {
        lock (R) { if (Saved) return ReportPath; Saved = true; }
        string dir = Cfg.LogDir;
        string file = Path.Combine(dir, string.Format("Report_{0}_{1:yyyyMMdd_HHmmss}.txt", Environment.MachineName, Started));
        var sb = new StringBuilder();
        lock (R) sb.Append(R.ToString());
        sb.AppendLine(); sb.AppendLine("=== SUMMARY ===");
        lock (summary) foreach (string s in summary) sb.AppendLine(s);
        sb.AppendLine(string.Format("This program: peak RAM {0} MB, run time {1:0} s", PeakMb, (DateTime.Now - Started).TotalSeconds));
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
        ReportPath = file;

        try
        {
            Directory.CreateDirectory(Cfg.DataDir);
            var all = new List<string>();
            if (File.Exists(Cfg.WarnFile)) all.AddRange(File.ReadAllLines(Cfg.WarnFile, Encoding.UTF8));
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            lock (warns) foreach (string w in warns) all.Add(now + "|" + w);
            if (all.Count > 300) all.RemoveRange(0, all.Count - 300);
            File.WriteAllLines(Cfg.WarnFile, all.ToArray(), new UTF8Encoding(false));
        }
        catch { }
        try
        {
            string csv = Path.Combine(dir, "history.csv");
            var c = new StringBuilder();
            if (!File.Exists(csv)) c.AppendLine("DateTime,Computer,Type,Warnings,Report");
            c.AppendLine(string.Format("{0:yyyy-MM-dd HH:mm:ss},\"{1}\",\"{2}\",{3},\"{4}\"", Started, Environment.MachineName, DeviceType, WarnCount, file));
            File.AppendAllText(csv, c.ToString(), new UTF8Encoding(true));
        }
        catch { }
        try { SaveLast(); } catch { }
        if (Cfg.KeepDays > 0)
            try
            {
                foreach (string f in Directory.GetFiles(dir, "Report_*.txt"))
                    if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-Cfg.KeepDays)) File.Delete(f);
            }
            catch { }
        return file;
    }
}

// ----------------------------------------------------------------------------- last results (for the dashboard)
class LastRun
{
    public DateTime Time = DateTime.MinValue; public string Type = "", Model = ""; public int Score = -1;
    public Dictionary<string, Res> R = new Dictionary<string, Res>();
    public static string FilePath { get { return Path.Combine(Cfg.DataDir, "last.txt"); } }

    public static int ScoreOf(Dictionary<string, Res> d)
    {
        int s = 100; bool any = false;
        foreach (var kv in d) { if (kv.Value.State == 4) { s -= 18; any = true; } else if (kv.Value.State == 3) { s -= 8; any = true; } else if (kv.Value.State == 2) any = true; }
        return any ? Math.Max(0, s) : -1;
    }

    public static LastRun Load()
    {
        var l = new LastRun();
        try
        {
            if (!File.Exists(FilePath)) return l;
            foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                int i = line.IndexOf('='); if (i <= 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                if (k == "time") { DateTime t; if (DateTime.TryParse(v, out t)) l.Time = t; }
                else if (k == "type") l.Type = v;
                else if (k == "model") l.Model = v;
                else if (k == "res")
                {
                    string[] p = v.Split(new char[] { '|' }, 3);
                    int st; if (p.Length == 3 && int.TryParse(p[1], out st)) l.R[p[0]] = new Res(st, p[2]);
                }
            }
            l.Score = ScoreOf(l.R);
        }
        catch { }
        return l;
    }
}

// ----------------------------------------------------------------------------- display patterns
static class Patterns
{
    public const int Count = 14;
    public static readonly string[] Names = { "Black", "White", "Red", "Green", "Blue", "Gray 25%", "Gray 50%", "Gray 75%", "Gray gradient", "Colour bars", "Checkerboard", "Grid", "Text sharpness", "Border/geometry" };

    public static void Draw(Graphics g, int idx, int w, int h)
    {
        g.SmoothingMode = SmoothingMode.None;
        switch (idx)
        {
            case 0: g.Clear(Color.Black); break;
            case 1: g.Clear(Color.White); break;
            case 2: g.Clear(Color.Red); break;
            case 3: g.Clear(Color.Lime); break;
            case 4: g.Clear(Color.Blue); break;
            case 5: g.Clear(Color.FromArgb(64, 64, 64)); break;
            case 6: g.Clear(Color.FromArgb(128, 128, 128)); break;
            case 7: g.Clear(Color.FromArgb(192, 192, 192)); break;
            case 8: using (var b = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.Black, Color.White, 0f)) g.FillRectangle(b, 0, 0, w, h); break;
            case 9:
                Color[] bars = { Color.White, Color.Yellow, Color.Cyan, Color.Lime, Color.Magenta, Color.Red, Color.Blue, Color.Black };
                for (int i = 0; i < bars.Length; i++) using (var b = new SolidBrush(bars[i])) g.FillRectangle(b, i * w / bars.Length, 0, w / bars.Length + 1, h);
                break;
            case 10:
                g.Clear(Color.Black);
                for (int y = 0; y < h; y += 32) for (int x = 0; x < w; x += 32) if (((x / 32) + (y / 32)) % 2 == 0) g.FillRectangle(Brushes.White, x, y, 32, 32);
                break;
            case 11:
                g.Clear(Color.Black);
                for (int x = 0; x < w; x += 50) g.DrawLine(Pens.White, x, 0, x, h);
                for (int y = 0; y < h; y += 50) g.DrawLine(Pens.White, 0, y, w, y);
                break;
            case 12:
                g.Clear(Color.White);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                float yy = 20;
                foreach (int sz in new int[] { 8, 10, 12, 16, 24, 36 })
                    using (var f = new Font("Arial", sz)) { g.DrawString("Text sharpness test 0123456789 The quick brown fox jumps over the lazy dog", f, Brushes.Black, 20, yy); yy += f.Height + 10; }
                break;
            default:
                g.Clear(Color.Black);
                g.DrawRectangle(Pens.White, 0, 0, w - 1, h - 1);
                g.DrawLine(Pens.White, 0, 0, w - 1, h - 1);
                g.DrawLine(Pens.White, 0, h - 1, w - 1, 0);
                break;
        }
    }
}
