using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// Optional app-activity helper (OFF by default). Records ONLY: time, opened/closed, program name of apps that have a visible window.
// No window titles, no content. A single small background process; stopped by an event handle.
static class AppMonitor
{
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr h, IntPtr min, IntPtr max);
    const string StopName = "TestComputerSystem.MonitorStop";
    public static string LogFile { get { return Path.Combine(Cfg.DataDir, "activity.log"); } }

    static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "explorer", "ApplicationFrameHost", "TextInputHost", "SearchHost", "SearchApp", "ShellExperienceHost", "StartMenuExperienceHost",
        "dwm", "taskhostw", "RuntimeBroker", "sihost", "ctfmon", "LockApp", "Test Computer System", "LogonUI", "fontdrvhost", "conhost",
        "SecurityHealthSystray", "Widgets", "WidgetService", "PhoneExperienceHost", "CompPkgSrv", "msedgewebview2" };

    static Dictionary<string, string> friendly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    static string Friendly(Process p, string name)
    {
        string f;
        if (friendly.TryGetValue(name, out f)) return f;
        f = name;
        try { string d = p.MainModule.FileVersionInfo.FileDescription; if (!string.IsNullOrWhiteSpace(d)) f = d.Trim(); } catch { }
        friendly[name] = f; return f;
    }

    public static void RequestStop()
    {
        try { using (var h = EventWaitHandle.OpenExisting(StopName)) h.Set(); } catch { }
    }

    public static void Start()
    {
        try { Process.Start(Cfg.SelfPath, "/monitor"); } catch { }
    }

    static void Prune()
    {
        try
        {
            if (!File.Exists(LogFile)) return;
            DateTime cut = DateTime.Now.AddDays(-Cfg.ActivityDays);
            var keep = new List<string>();
            foreach (string l in File.ReadAllLines(LogFile, Encoding.UTF8)) { DateTime t; if (l.Length > 19 && DateTime.TryParse(l.Substring(0, 19), out t) && t >= cut) keep.Add(l); }
            File.WriteAllLines(LogFile, keep.ToArray(), new UTF8Encoding(false));
        }
        catch { }
    }

    public static int Run()
    {
        bool created;
        using (var m = new Mutex(true, "TestComputerSystem.Monitor", out created))
        {
            if (!created) return 0;
            using (var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopName))
            {
                Directory.CreateDirectory(Cfg.DataDir);
                Prune();
                var cur = new Dictionary<string, string>();
                int loop = 0;
                while (true)
                {
                    var seen = new Dictionary<string, string>();
                    foreach (Process p in Process.GetProcesses())
                    {
                        try
                        {
                            if (p.MainWindowHandle == IntPtr.Zero) continue;
                            string n = p.ProcessName;
                            if (Skip.Contains(n) || p.MainWindowTitle.Length == 0) continue;   // title is only tested for "is a real window", never stored
                            DateTime st; try { st = p.StartTime; } catch { st = DateTime.MinValue; }
                            seen[p.Id + ":" + st.Ticks] = Friendly(p, n);
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                    DateTime now = DateTime.Now; var sb = new StringBuilder();
                    foreach (var kv in seen) if (!cur.ContainsKey(kv.Key)) sb.AppendLine(now.ToString("yyyy-MM-dd HH:mm:ss") + "|open|" + kv.Value);
                    foreach (var kv in cur) if (!seen.ContainsKey(kv.Key)) sb.AppendLine(now.ToString("yyyy-MM-dd HH:mm:ss") + "|close|" + kv.Value);
                    if (sb.Length > 0) try { File.AppendAllText(LogFile, sb.ToString(), new UTF8Encoding(false)); } catch { }
                    cur = seen;
                    if (++loop % 20 == 0) { GC.Collect(); try { using (var p = Process.GetCurrentProcess()) SetProcessWorkingSetSize(p.Handle, (IntPtr)(-1), (IntPtr)(-1)); } catch { } }
                    if (stop.WaitOne(3000)) break;
                }
            }
        }
        return 0;
    }

    public class Sess { public string Name; public DateTime Open; public bool Closed; public DateTime Close; }

    public static List<Sess> Sessions(int max)
    {
        var all = new List<Sess>();
        try
        {
            if (File.Exists(LogFile))
                foreach (string l in File.ReadAllLines(LogFile, Encoding.UTF8))
                {
                    string[] p = l.Split(new char[] { '|' }, 3);
                    DateTime t; if (p.Length < 3 || !DateTime.TryParse(p[0], out t)) continue;
                    if (p[1] == "open") all.Add(new Sess { Name = p[2], Open = t });
                    else if (p[1] == "close")
                        for (int i = all.Count - 1; i >= 0; i--) if (!all[i].Closed && all[i].Name == p[2]) { all[i].Closed = true; all[i].Close = t; break; }
                }
        }
        catch { }
        all.Reverse();
        return all.Take(max).ToList();
    }

    public static void Clear() { try { if (File.Exists(LogFile)) File.Delete(LogFile); } catch { } }
}
