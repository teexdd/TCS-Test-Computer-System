using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Cfg
{
    public const string AppName = "Test Computer System";
    public const string Version = "1.0.0";
    public static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static readonly string InstallDir = Path.Combine(Local, @"Programs\TestComputerSystem");
    public static readonly string DataDir = Path.Combine(Local, "TestComputerSystem");
    public static string ExePath { get { return Path.Combine(InstallDir, AppName + ".exe"); } }
    public static string ConfigFile { get { return Path.Combine(DataDir, "config.ini"); } }
    public static string WarnFile { get { return Path.Combine(DataDir, "warnings.txt"); } }

    static string logDir;
    public static string LogDir
    {
        get
        {
            if (logDir == null)
            {
                string d = @"C:\TestComputerSystem\Logs";
                try { Directory.CreateDirectory(d); string p = Path.Combine(d, ".w"); File.WriteAllText(p, "1"); File.Delete(p); }
                catch { d = Path.Combine(DataDir, "Logs"); Directory.CreateDirectory(d); }
                logDir = d;
            }
            return logDir;
        }
    }

    // which checks
    public static bool Drivers = true, Display = true, Speaker = true, Network = true, Boot = true, Apps = true;
    // privacy
    public static bool Serial = false, UserName = false, NetSpeed = true, Consent = false;
    public static int KeepDays = 90;
    // behaviour
    public static bool AutoStart = true;
    public static int RunMode = 0;        // 0 = every logon, 1 = once a day
    public static int PatternMs = 500;
    public static int AutoCloseSec = 10;
    public static string LastRun = "", ConsentDate = "";
    public static string Language = "en-US";
    public static int ThemePref = 0;               // 0 follow Windows, 1 light, 2 dark
    public static bool Unattended = true, EarlyStart = false;
    public static int Skin = 0;                    // 0 Windows 11, 1 Retro, 2 Hi-Tech terminal
    public static bool BootCheck = false, Monitor = false;
    public static int ActivityDays = 30;
    public static bool AppliedEarly = false, AppliedBoot = false;
    public static string AppliedLang = "";
    public static string SelfPath { get { return System.Reflection.Assembly.GetEntryAssembly().Location; } }
    public static string BootDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TestComputerSystem"); } }
    public static List<string> AutoApps = new List<string>();

    static bool B(string v) { return v == "1"; }
    static int I(string v, int d) { int x; return int.TryParse(v, out x) ? x : d; }

    public static void Load()
    {
        try
        {
            if (!File.Exists(ConfigFile)) return;
            AutoApps.Clear();
            foreach (string line in File.ReadAllLines(ConfigFile, Encoding.UTF8))
            {
                int i = line.IndexOf('='); if (i <= 0) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                switch (k)
                {
                    case "Drivers": Drivers = B(v); break;
                    case "Display": Display = B(v); break;
                    case "Speaker": Speaker = B(v); break;
                    case "Network": Network = B(v); break;
                    case "Boot": Boot = B(v); break;
                    case "Apps": Apps = B(v); break;
                    case "Serial": Serial = B(v); break;
                    case "UserName": UserName = B(v); break;
                    case "NetSpeed": NetSpeed = B(v); break;
                    case "Consent": Consent = B(v); break;
                    case "AutoStart": AutoStart = B(v); break;
                    case "KeepDays": KeepDays = I(v, 90); break;
                    case "RunMode": RunMode = I(v, 0); break;
                    case "PatternMs": PatternMs = Math.Max(200, Math.Min(3000, I(v, 500))); break;
                    case "AutoCloseSec": AutoCloseSec = Math.Max(0, Math.Min(120, I(v, 10))); break;
                    case "Language": Language = v; break;
                    case "ThemePref": ThemePref = I(v, 0); break;
                    case "Unattended": Unattended = B(v); break;
                    case "EarlyStart": EarlyStart = B(v); break;
                    case "Skin": Skin = I(v, 0); break;
                    case "BootCheck": BootCheck = B(v); break;
                    case "Monitor": Monitor = B(v); break;
                    case "ActivityDays": ActivityDays = Math.Max(1, Math.Min(365, I(v, 30))); break;
                    case "AppliedEarly": AppliedEarly = B(v); break;
                    case "AppliedBoot": AppliedBoot = B(v); break;
                    case "AppliedLang": AppliedLang = v; break;
                    case "LastRun": LastRun = v; break;
                    case "ConsentDate": ConsentDate = v; break;
                    case "App": if (v.Length > 0) AutoApps.Add(v); break;
                }
            }
        }
        catch { }
    }

    public static void Save()
    {
        Directory.CreateDirectory(DataDir);
        var s = new StringBuilder();
        Action<string, bool> b = delegate(string k, bool v) { s.AppendLine(k + "=" + (v ? "1" : "0")); };
        Action<string, object> o = delegate(string k, object v) { s.AppendLine(k + "=" + v); };
        b("Drivers", Drivers); b("Display", Display); b("Speaker", Speaker); b("Network", Network); b("Boot", Boot); b("Apps", Apps);
        b("Serial", Serial); b("UserName", UserName); b("NetSpeed", NetSpeed); b("Consent", Consent); b("AutoStart", AutoStart);
        o("KeepDays", KeepDays); o("RunMode", RunMode); o("PatternMs", PatternMs); o("AutoCloseSec", AutoCloseSec);
        o("LastRun", LastRun); o("ConsentDate", ConsentDate);
        o("Language", Language); o("ThemePref", ThemePref); b("Unattended", Unattended); b("EarlyStart", EarlyStart);
        o("Skin", Skin); b("BootCheck", BootCheck); b("Monitor", Monitor); o("ActivityDays", ActivityDays);
        b("AppliedEarly", AppliedEarly); b("AppliedBoot", AppliedBoot); o("AppliedLang", AppliedLang);
        foreach (string a in AutoApps) o("App", a);
        File.WriteAllText(ConfigFile, s.ToString(), new UTF8Encoding(false));
    }

    public static void MarkRun() { LastRun = DateTime.Now.ToString("yyyy-MM-dd"); try { Save(); } catch { } }
}

static class Installer
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string UnKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\TestComputerSystem";

    static string StartMenuLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Cfg.AppName + ".lnk"); } }

    public static bool SamePath(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); } catch { return false; }
    }

    public static void CopyFiles()
    {
        Directory.CreateDirectory(Cfg.InstallDir);
        string src = Application.ExecutablePath;
        if (!SamePath(src, Cfg.ExePath)) File.Copy(src, Cfg.ExePath, true);
    }

    public static void MakeShortcut()
    {
        try
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { StartMenuLink });
            Type lt = lnk.GetType();
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { Cfg.ExePath });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Cfg.InstallDir });
            lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { Cfg.AppName });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(lnk);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(sh);
        }
        catch { }
    }

    public static int RunHidden(string file, string args, bool elevate)
    {
        var psi = new ProcessStartInfo(file, args);
        psi.UseShellExecute = elevate; psi.CreateNoWindow = true; psi.WindowStyle = ProcessWindowStyle.Hidden;
        if (elevate) psi.Verb = "runas";
        using (Process p = Process.Start(psi)) { p.WaitForExit(60000); return p.HasExited ? p.ExitCode : -1; }
    }

    // Logon task / boot task are created by ONE elevated run of this exe ("/admin-apply"), so there is at most one UAC prompt.
    // returns false if approval was declined (the early-start / boot options are then switched off).
    public static bool ApplyStartup()
    {
        bool early = Cfg.AutoStart && Cfg.EarlyStart, boot = Cfg.BootCheck, ok = true;
        bool need = early != Cfg.AppliedEarly || boot != Cfg.AppliedBoot || (boot && Cfg.AppliedLang != L.Code);
        if (need)
        {
            string user = Environment.UserDomainName + "\\" + Environment.UserName;
            string args = "/admin-apply " + (early ? 1 : 0) + " " + (boot ? 1 : 0) + " " + L.Code + " \"" + user + "\" \"" + Cfg.ExePath + "\"";
            int rc = -99;
            try { rc = RunHidden(Cfg.SelfPath, args, true); } catch { }
            ok = rc == 0;
            if (ok) { Cfg.AppliedEarly = early; Cfg.AppliedBoot = boot; Cfg.AppliedLang = L.Code; }
            else { if (early) Cfg.EarlyStart = false; if (boot) Cfg.BootCheck = false; early = false; }
        }
        else early = early && Cfg.AppliedEarly;
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (k != null)
            {
                if (Cfg.AutoStart && !early) k.SetValue("TestComputerSystem", "\"" + Cfg.ExePath + "\" /startup"); else k.DeleteValue("TestComputerSystem", false);
                if (Cfg.Monitor) k.SetValue("TestComputerSystemMonitor", "\"" + Cfg.ExePath + "\" /monitor"); else k.DeleteValue("TestComputerSystemMonitor", false);
            }
        }
        return ok;
    }

    public static void RegisterUninstall()
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UnKey))
        {
            k.SetValue("DisplayName", Cfg.AppName);
            k.SetValue("DisplayVersion", Cfg.Version);
            k.SetValue("Publisher", "Test Computer System");
            k.SetValue("InstallLocation", Cfg.InstallDir);
            k.SetValue("DisplayIcon", Cfg.ExePath);
            k.SetValue("UninstallString", "\"" + Cfg.ExePath + "\" /uninstall");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }

    // Step 1: ask. Step 2 runs from a temp copy so the install folder (incl. the .exe) can be deleted - no cmd.exe tricks.
    public static void StartUninstall()
    {
        DialogResult r = MessageBox.Show(
            L.T("un.ask"),
            Cfg.AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (r == DialogResult.Cancel) return;
        string tmp = Path.Combine(Path.GetTempPath(), "tcs_uninstall_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".exe");
        File.Copy(Cfg.SelfPath, tmp, true);
        Process.Start(new ProcessStartInfo(tmp, "/uninstall2 " + (r == DialogResult.Yes ? "1" : "0")) { UseShellExecute = false });
        Application.Exit();
    }

    public static void FinishUninstall(bool removeData)
    {
        Thread.Sleep(1200);
        try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue("TestComputerSystem", false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UnKey); } catch { }
        AppMonitor.RequestStop();
        try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue("TestComputerSystemMonitor", false); } catch { }
        if (Directory.Exists(Cfg.BootDir) || Cfg.AppliedEarly) { try { RunHidden(Cfg.SelfPath, "/admin-apply 0 0 en-US \"x\" \"x\"", true); } catch { } }
        try { if (File.Exists(StartMenuLink)) File.Delete(StartMenuLink); } catch { }
        for (int i = 0; i < 10; i++)
        {
            try { if (Directory.Exists(Cfg.InstallDir)) Directory.Delete(Cfg.InstallDir, true); break; } catch { Thread.Sleep(500); }
        }
        if (removeData)
        {
            try { if (Directory.Exists(Cfg.DataDir)) Directory.Delete(Cfg.DataDir, true); } catch { }
            try { if (Directory.Exists(@"C:\TestComputerSystem")) Directory.Delete(@"C:\TestComputerSystem", true); } catch { }
        }
        MessageBox.Show(L.T("un.done"), Cfg.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
