using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

// Language files (lang\*.lang) are embedded in the exe. Add a language: copy en-US.lang, translate, add one line to Langs, rebuild.
static class L
{
    // code, native name, font candidates (first installed wins)
    public static readonly string[][] Langs = {
        new string[] { "en-US", "English (US)", "Segoe UI Variable Text|Segoe UI|Tahoma" },
        new string[] { "th-TH", "ไทย", "Leelawadee UI|Tahoma" },
        new string[] { "zh-CN", "简体中文", "Microsoft YaHei UI|Microsoft YaHei|SimSun" },
        new string[] { "ja-JP", "日本語", "Yu Gothic UI|Meiryo UI|MS UI Gothic" },
        new string[] { "es-ES", "Español", "Segoe UI Variable Text|Segoe UI|Tahoma" }
    };
    static Dictionary<string, string> cur = new Dictionary<string, string>();
    static Dictionary<string, string> en;
    public static string Code = "en-US";

    public static string[] Fonts(string code)
    {
        foreach (string[] l in Langs) if (l[0] == code) return l[2].Split('|');
        return Langs[0][2].Split('|');
    }

    static Dictionary<string, string> Load(string code)
    {
        var d = new Dictionary<string, string>();
        try
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lang." + code + ".lang"))
            {
                if (s == null) return d;
                using (var r = new StreamReader(s, Encoding.UTF8))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line[0] == '#') continue;
                        int i = line.IndexOf('=');
                        if (i <= 0) continue;
                        d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Replace("\\n", "\n");
                    }
                }
            }
        }
        catch { }
        return d;
    }

    public static void Set(string code)
    {
        if (en == null) en = Load("en-US");
        Code = code;
        cur = code == "en-US" ? en : Load(code);
        global::T.ResetFonts();
    }

    public static string T(string key)
    {
        string v;
        if (cur.TryGetValue(key, out v)) return v;
        if (en != null && en.TryGetValue(key, out v)) return v;
        return key;
    }
    public static string F(string key, params object[] a) { try { return string.Format(T(key), a); } catch { return T(key); } }

    public static string Legal()
    {
        try
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("legal.txt"))
            using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd().Replace("\r\n", "\n").Replace("\n", "\r\n");
        }
        catch { return ""; }
    }
}
