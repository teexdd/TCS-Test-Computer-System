using System;
using System.IO;
using System.Linq;
using System.Threading;

static class Program
{
    // Headless modes are dispatched first so they never load WinForms (keeps the background helper tiny).
    [STAThread]
    static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        if (mode == "/monitor") { Cfg.Load(); return AppMonitor.Run(); }
        if (mode == "/boot") return BootMode.Run();
        if (mode == "/admin-apply" && args.Length >= 6) return AdminApply.Run(args);
        return RunUi(args, mode);
    }

    static int RunUi(string[] args, string mode)
    {
        Cfg.Load();
        L.Set(Cfg.Language); T.Init(Cfg.ThemePref, Cfg.Skin);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        try { System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072; } catch { }

        if (mode == "/uninstall2") { Installer.FinishUninstall(args.Length > 1 && args[1] == "1"); return 0; }
        if (mode == "/uninstall") { Installer.StartUninstall(); return 0; }

        if (mode == "/startup" || mode == "/test")
        {
            if (!Cfg.Consent) return 0;   // never run before the user accepted the terms
            if (mode == "/startup" && Cfg.RunMode == 1 && Cfg.LastRun == DateTime.Now.ToString("yyyy-MM-dd")) return 0;
            string only = null;
            foreach (string a in args) if (a.StartsWith("/only=", StringComparison.OrdinalIgnoreCase)) only = a.Substring(6).ToLowerInvariant();
            bool auto = mode == "/startup" ? Cfg.Unattended : args.Any(a => a.Equals("/auto", StringComparison.OrdinalIgnoreCase));
            bool created;
            using (var m = new Mutex(true, "TestComputerSystem.Onboarding", out created))
            {
                if (!created) return 0;
                System.Windows.Forms.Application.Run(new OnboardForm(only, auto));
            }
            return 0;
        }

        bool installed = Installer.SamePath(Path.GetDirectoryName(Cfg.SelfPath), Cfg.InstallDir);
        if (installed && Cfg.Consent)
        {
            MainForm f;
            do { L.Set(Cfg.Language); T.Init(Cfg.ThemePref, Cfg.Skin); f = new MainForm(); System.Windows.Forms.Application.Run(f); } while (f.Restart);
        }
        else System.Windows.Forms.Application.Run(new SetupForm());
        return 0;
    }
}
