using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

// Headless (no UI) modes:
//   /boot         runs as SYSTEM from a protected copy in Program Files shortly after Windows starts - BEFORE anyone signs in.
//                 Writes boot.txt into the protected data folder; the sign-in screen then shows the results instantly.
//   /admin-apply  one elevated run (UAC) that creates/removes the logon task and the boot task.
static class BootMode
{
    public static int Run()
    {
        try
        {
            string dir = Path.Combine(Path.GetDirectoryName(Cfg.SelfPath), "data");
            string lang = "en-US";
            try { foreach (string l in File.ReadAllLines(Path.Combine(dir, "boot.ini"))) if (l.StartsWith("lang=")) lang = l.Substring(5).Trim(); } catch { }
            L.Set(lang);
            var res = new Dictionary<string, Res>();
            res["sys"] = Checks.SystemInfo();
            res["drivers"] = Checks.Drivers();
            res["boot"] = Checks.Boot();
            var sb = new StringBuilder();
            sb.AppendLine("boot=" + Checks.BootId());
            sb.AppendLine("lang=" + lang);
            sb.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("type=" + Checks.DeviceType);
            sb.AppendLine("model=" + Checks.Model);
            foreach (var kv in res) sb.AppendLine("res=" + kv.Key + "|" + kv.Value.State + "|" + kv.Value.Detail.Replace("\r", " ").Replace("\n", " "));
            foreach (string w in Checks.WarnList()) sb.AppendLine("warn=" + w.Replace("\r", " ").Replace("\n", " "));
            File.WriteAllText(Path.Combine(dir, "boot_report.txt"), Checks.Dump(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, "boot.txt"), sb.ToString(), new UTF8Encoding(false));
            return 0;
        }
        catch { return 1; }
    }
}

static class AdminApply
{
    const string LogonTask = "TestComputerSystem", BootTask = "TestComputerSystem-Boot";

    static int Sch(string args)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args) { UseShellExecute = false, CreateNoWindow = true };
        using (Process p = Process.Start(psi)) { p.WaitForExit(30000); return p.HasExited ? p.ExitCode : -1; }
    }

    static int Create(string name, string xml)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "tcs_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xml");
        try { File.WriteAllText(tmp, xml, Encoding.Unicode); return Sch("/Create /TN " + name + " /XML \"" + tmp + "\" /F"); }
        finally { try { File.Delete(tmp); } catch { } }
    }

    static string Esc(string s) { return SecurityElement.Escape(s); }

    static string Head(string trigger, string principal, string exe, string args, string limit)
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
            "<RegistrationInfo><Description>Test Computer System</Description></RegistrationInfo>\r\n<Triggers>" + trigger + "</Triggers>\r\n" +
            "<Principals><Principal id=\"Author\">" + principal + "</Principal></Principals>\r\n" +
            "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
            "<AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><ExecutionTimeLimit>" + limit + "</ExecutionTimeLimit><Priority>4</Priority></Settings>\r\n" +
            "<Actions Context=\"Author\"><Exec><Command>" + Esc(exe) + "</Command><Arguments>" + args + "</Arguments></Exec></Actions>\r\n</Task>";
    }

    // args: /admin-apply <early 0|1> <boot 0|1> <lang> <DOMAIN\user> <path to the user's installed exe>
    public static int Run(string[] a)
    {
        try
        {
            bool early = a[1] == "1", boot = a[2] == "1";
            string lang = a[3], user = a[4], userExe = a[5];
            int rc = 0;

            if (early)
                rc |= Create(LogonTask, Head("<LogonTrigger><Enabled>true</Enabled><UserId>" + Esc(user) + "</UserId></LogonTrigger>",
                    "<UserId>" + Esc(user) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel>", userExe, "/startup", "PT30M"));
            else Sch("/Delete /TN " + LogonTask + " /F");

            string dir = Cfg.BootDir, exe = Path.Combine(dir, Cfg.AppName + ".exe");
            if (boot)
            {
                // a protected copy: SYSTEM must never run code from a folder that a normal user can modify
                Directory.CreateDirectory(Path.Combine(dir, "data"));
                File.Copy(Cfg.SelfPath, exe, true);
                File.WriteAllText(Path.Combine(dir, "data", "boot.ini"), "lang=" + lang + "\r\n");
                rc |= Create(BootTask, Head("<BootTrigger><Enabled>true</Enabled><Delay>PT20S</Delay></BootTrigger>",
                    "<UserId>S-1-5-18</UserId><LogonType>ServiceAccount</LogonType><RunLevel>HighestAvailable</RunLevel>", exe, "/boot", "PT15M"));
            }
            else
            {
                Sch("/Delete /TN " + BootTask + " /F");
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
            return rc == 0 ? 0 : 1;
        }
        catch { return 1; }
    }
}
