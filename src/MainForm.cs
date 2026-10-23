using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// "Smart-home style" dashboard: score ring + device illustration, bento cards per check (toggle + test), activity and history.
class Dash : Control
{
    class Hit { public Rectangle R; public Action A; }
    [StructLayout(LayoutKind.Sequential)]
    class MEMSTAT
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMSTAT)); public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In, Out] MEMSTAT s);

    static readonly string[] Keys = { "drivers", "display", "speaker", "network", "boot", "apps" };
    static readonly string[] Icons = { "chip", "monitor", "speaker", "wifi", "power", "apps" };
    static readonly Color[] Cols = { Color.FromArgb(150, 100, 255), Color.FromArgb(61, 140, 255), Color.FromArgb(255, 150, 60), Color.FromArgb(40, 190, 130), Color.FromArgb(255, 90, 140), Color.FromArgb(30, 180, 200) };

    List<Hit> hits = new List<Hit>();
    LastRun last = new LastRun();
    List<int> hist = new List<int>();
    List<string[]> acts = new List<string[]>();
    double memPct, memGb, diskFree, diskTotal;
    public Action<string> RunTest;

    public Dash()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 860;
    }

    public void Reload()
    {
        last = LastRun.Load();
        hist.Clear(); acts.Clear();
        try
        {
            string hf = Path.Combine(Cfg.DataDir, "history.txt");
            if (File.Exists(hf))
                foreach (string l in File.ReadAllLines(hf).Reverse().Take(10).Reverse())
                { string[] p = l.Split('|'); int s; if (p.Length > 1 && int.TryParse(p[1], out s) && s >= 0) hist.Add(s); }
            if (File.Exists(Cfg.WarnFile))
                foreach (string l in File.ReadAllLines(Cfg.WarnFile, Encoding.UTF8).Reverse().Take(3)) acts.Add(l.Split(new char[] { '|' }, 3));
        }
        catch { }
        try { var m = new MEMSTAT(); if (GlobalMemoryStatusEx(m)) { memPct = m.dwMemoryLoad; memGb = m.ullTotalPhys / 1073741824.0; } } catch { }
        try { var d = new DriveInfo("C"); diskFree = d.AvailableFreeSpace / 1073741824.0; diskTotal = d.TotalSize / 1073741824.0; } catch { }
        Invalidate();
    }

    static Color StateCol(int st) { return st == 2 ? T.Ok : st == 3 ? T.Warn : st == 4 ? T.Bad : T.Muted; }

    void Pill(Graphics g, Rectangle r, string text, bool primary, Action a)
    {
        T.Push(g, r, text, T.F(10.5f, true), primary, false, false, true, false);
        hits.Add(new Hit { R = r, A = a });
    }

    void Switch(Graphics g, Rectangle r, bool on) { T.DrawSwitch(g, r, on); }

    bool CfgGet(string k) { switch (k) { case "drivers": return Cfg.Drivers; case "display": return Cfg.Display; case "speaker": return Cfg.Speaker; case "network": return Cfg.Network; case "boot": return Cfg.Boot; default: return Cfg.Apps; } }
    void CfgSet(string k, bool v) { switch (k) { case "drivers": Cfg.Drivers = v; break; case "display": Cfg.Display = v; break; case "speaker": Cfg.Speaker = v; break; case "network": Cfg.Network = v; break; case "boot": Cfg.Boot = v; break; default: Cfg.Apps = v; break; } }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; T.Q(g); g.Clear(T.Bg); hits.Clear();
        int W = Width, M = 32, CW = W - M * 2;
        bool nb = last.Type == "Notebook";
        string dev = L.T(nb ? "t.nb" : "t.pc");

        // header
        int hr = DateTime.Now.Hour;
        T.Txt(g, L.T(hr < 12 ? "d.morning" : hr < 18 ? "d.afternoon" : "d.evening"), T.F(24, true), T.Ink, new RectangleF(M, 22, CW - 220, 44), StringAlignment.Near, StringAlignment.Near, false);
        T.Txt(g, (last.Model.Length > 0 ? last.Model + "   |   " : "") + DateTime.Now.ToString("D"), T.F(10.5f), T.Muted, new RectangleF(M, 62, CW - 220, 24), StringAlignment.Near, StringAlignment.Near, false);
        Pill(g, new Rectangle(W - M - 190, 26, 190, 48), L.T("b.runfull"), true, delegate { if (RunTest != null) RunTest(null); });

        // hero
        int hy = 104, hh = 236, hw = (int)(CW * 0.62);
        Color bf = T.ObBgFg;
        RectangleF hero = new RectangleF(M, hy, hw, hh);
        if (T.Skin == 2) { T.FillRound(g, T.Card, hero, 0); using (var p = new Pen(T.Accent, 1.5f)) g.DrawRectangle(p, hero.X, hero.Y, hero.Width - 1, hero.Height - 1); }
        else using (var b = new LinearGradientBrush(hero, T.Skin == 1 ? T.C(0, 0, 128) : T.C(0, 103, 192), T.Skin == 1 ? T.C(128, 0, 128) : T.C(120, 80, 230), 25f)) T.FillRound(g, b, hero, 30);
        int score = last.Score;
        RectangleF ring = new RectangleF(M + 30, hy + 30, 130, 130);
        T.Ring(g, ring, score < 0 ? 0 : score / 100f, T.A(bf, 60), bf, 12f);
        T.Txt(g, score < 0 ? "--" : score.ToString(), T.F(30, true), bf, new RectangleF(ring.X, ring.Y + 36, ring.Width, 46), StringAlignment.Center, StringAlignment.Near, false);
        T.Txt(g, L.T("d.health"), T.F(8.5f), T.A(bf, 210), new RectangleF(ring.X, ring.Y + 82, ring.Width, 22), StringAlignment.Center, StringAlignment.Near, false);
        int ok = 0, wn = 0, fl = 0;
        foreach (var kv in last.R) { if (kv.Key == "sys") continue; if (kv.Value.State == 2) ok++; else if (kv.Value.State == 3) wn++; else if (kv.Value.State == 4) fl++; }
        string head = score < 0 ? L.T("d.never") : (wn + fl == 0 ? L.F("d.great", dev) : score >= 70 ? L.F("d.good", dev, wn + fl) : L.F("d.bad", dev, wn + fl));
        T.Txt(g, head, T.F(15, true), bf, new RectangleF(M + 180, hy + 34, hw * 0.50f - 150, 90));
        if (last.Time != DateTime.MinValue) T.Txt(g, L.F("d.last", last.Time.ToString("g")), T.F(9.5f), T.A(bf, 210), new RectangleF(M + 180, hy + 128, hw * 0.50f - 150, 22), StringAlignment.Near, StringAlignment.Near, false);
        T.Glass(g, new RectangleF(M + 30, hy + hh - 56, hw * 0.50f - 20, 36), 18, 40);
        T.Txt(g, L.F("d.counts", ok, wn, fl), T.F(9.5f, true), bf, new RectangleF(M + 30, hy + hh - 56, hw * 0.50f - 20, 36), StringAlignment.Center, StringAlignment.Center, false);
        Rectangle ill = new Rectangle((int)(M + hw * 0.54), hy + 16, (int)(hw * 0.43), hh - 32);
        if (T.Skin == 2)
        {
            string art = nb ? "  .------------.\n  | > SYSTEM  |\n  | > ONLINE_ |\n  '------------'\n /______________\\" : "  .------------.\n  | > SYSTEM  |\n  | > ONLINE_ |\n  '------------'\n      _||_\n     /____\\";
            T.Txt(g, art, T.F(12, true), T.Accent, new RectangleF(ill.X, ill.Y + 20, ill.Width, ill.Height - 20));
        }
        else if (nb) T.Laptop(g, ill); else T.Desktop(g, ill);

        // memory + disk
        int rx = M + hw + 16, rw = CW - hw - 16;
        RectangleF c1 = new RectangleF(rx, hy, rw, 110), c2 = new RectangleF(rx, hy + 126, rw, 110);
        T.FillRound(g, T.Card, c1, 26); T.FillRound(g, T.Card, c2, 26);
        T.Ring(g, new RectangleF(rx + 20, hy + 20, 70, 70), (float)(memPct / 100), T.Line, T.Accent, 9f);
        T.Txt(g, ((int)memPct) + "%", T.F(11, true), T.Ink, new RectangleF(rx + 20, hy + 20, 70, 70), StringAlignment.Center, StringAlignment.Center, false);
        T.Txt(g, L.T("d.mem"), T.F(11.5f, true), T.Ink, new RectangleF(rx + 106, hy + 22, rw - 120, 26), StringAlignment.Near, StringAlignment.Near, false);
        T.Txt(g, L.F("d.memv", (int)memPct, memGb.ToString("0.#")), T.F(9.5f), T.Muted, new RectangleF(rx + 106, hy + 50, rw - 120, 44));
        T.Icon(g, "disk", new RectangleF(rx + 20, hy + 126 + 18, 34, 34), T.Accent, 2f);
        T.Txt(g, L.T("d.disk"), T.F(11.5f, true), T.Ink, new RectangleF(rx + 66, hy + 126 + 20, rw - 80, 26), StringAlignment.Near, StringAlignment.Near, false);
        double used = diskTotal > 0 ? 1 - diskFree / diskTotal : 0;
        T.FillRound(g, T.Line, new RectangleF(rx + 20, hy + 126 + 66, rw - 40, 10), 5);
        T.FillRound(g, used > 0.9 ? T.Bad : T.Accent, new RectangleF(rx + 20, hy + 126 + 66, Math.Max(8, (float)((rw - 40) * used)), 10), 5);
        T.Txt(g, L.F("d.diskv", diskFree.ToString("0"), diskTotal.ToString("0")), T.F(9), T.Muted, new RectangleF(rx + 20, hy + 126 + 80, rw - 40, 22), StringAlignment.Near, StringAlignment.Near, false);

        // check cards
        int cy = hy + hh + 24;
        T.Txt(g, L.T("d.checks"), T.F(14, true), T.Ink, new RectangleF(M, cy, 300, 30), StringAlignment.Near, StringAlignment.Near, false);
        cy += 38;
        int cw = (CW - 28) / 3, ch = 112;
        for (int i = 0; i < 6; i++)
        {
            string k = Keys[i];
            Rectangle r = new Rectangle(M + (i % 3) * (cw + 14), cy + (i / 3) * (ch + 14), cw, ch);
            T.FillRound(g, T.Card, r, 24);
            Color col = Cols[i];
            T.FillRound(g, T.A(col, 36), new RectangleF(r.X + 18, r.Y + 18, 48, 48), 16);
            T.Icon(g, Icons[i], new RectangleF(r.X + 27, r.Y + 27, 30, 30), col, 2f);
            T.Txt(g, L.T("c." + k), T.F(11.5f, true), T.Ink, new RectangleF(r.X + 78, r.Y + 16, cw - 78 - 84, 26), StringAlignment.Near, StringAlignment.Near, false);
            Res rs; bool has = last.R.TryGetValue(k, out rs);
            int st = has ? rs.State : 0;
            using (var b = new SolidBrush(StateCol(st))) g.FillEllipse(b, r.X + 78, r.Y + 52, 8, 8);
            T.Txt(g, has ? rs.Detail : L.T("st.none"), T.F(9), T.Muted, new RectangleF(r.X + 92, r.Y + 44, cw - 92 - 16, 40), StringAlignment.Near, StringAlignment.Near, true);
            bool on = CfgGet(k);
            Rectangle sw = new Rectangle(r.Right - 66, r.Y + 20, 46, 26);
            Switch(g, sw, on);
            hits.Add(new Hit { R = sw, A = delegate { CfgSet(k, !CfgGet(k)); try { Cfg.Save(); } catch { } Invalidate(); } });
            Rectangle tl = new Rectangle(r.Right - 110, r.Bottom - 34, 96, 26);
            T.Txt(g, L.T("b.test"), T.F(9.5f, true), T.Accent, tl, StringAlignment.Far, StringAlignment.Center, false);
            hits.Add(new Hit { R = tl, A = delegate { if (RunTest != null) RunTest(k); } });
        }

        // activity + history
        int ay = cy + 2 * (ch + 14) + 14, ah = 170, aw = (int)(CW * 0.60);
        RectangleF a1 = new RectangleF(M, ay, aw, ah), a2 = new RectangleF(M + aw + 16, ay, CW - aw - 16, ah);
        T.FillRound(g, T.Card, a1, 26); T.FillRound(g, T.Card, a2, 26);
        T.Txt(g, L.T("d.activity"), T.F(11.5f, true), T.Ink, new RectangleF(M + 22, ay + 16, aw - 44, 24), StringAlignment.Near, StringAlignment.Near, false);
        if (acts.Count == 0)
        {
            T.StateIcon(g, 2, new RectangleF(M + 22, ay + 62, 30, 30), 0);
            T.TxtL(g, L.T("d.noact"), T.F(10.5f), T.Muted, new RectangleF(M + 64, ay + 62, aw - 90, 30));
        }
        for (int i = 0; i < acts.Count; i++)
        {
            string[] p = acts[i]; string sev = p.Length > 1 ? p[1] : "warn", msg = p.Length > 2 ? p[2] : "";
            float yy = ay + 48 + i * 38;
            using (var b = new SolidBrush(sev == "fail" ? T.Bad : T.Warn)) g.FillEllipse(b, M + 24, yy + 11, 10, 10);
            T.TxtL(g, msg, T.F(9.5f), T.Ink, new RectangleF(M + 44, yy, aw - 200, 32));
            T.Txt(g, p[0], T.F(8.5f), T.Muted, new RectangleF(M + aw - 150, yy, 130, 32), StringAlignment.Far, StringAlignment.Center, false);
        }
        T.Txt(g, L.T("d.hist"), T.F(11.5f, true), T.Ink, new RectangleF(a2.X + 22, ay + 16, a2.Width - 44, 24), StringAlignment.Near, StringAlignment.Near, false);
        if (hist.Count == 0) T.Txt(g, L.T("d.nohist"), T.F(10), T.Muted, new RectangleF(a2.X + 22, ay + 70, a2.Width - 44, 24), StringAlignment.Near, StringAlignment.Near, false);
        else
        {
            float bw = (a2.Width - 44 - (hist.Count - 1) * 8f) / hist.Count, baseY = ay + ah - 22, maxH = ah - 80;
            for (int i = 0; i < hist.Count; i++)
            {
                float hgt = Math.Max(6, maxH * hist[i] / 100f);
                Color bc = hist[i] >= 90 ? T.Ok : hist[i] >= 70 ? T.Warn : T.Bad;
                T.FillRound(g, i == hist.Count - 1 ? bc : T.A(bc, 120), new RectangleF(a2.X + 22 + i * (bw + 8), baseY - hgt, bw, hgt), Math.Min(8, bw / 2));
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool over = false;
        foreach (Hit h in hits) if (h.R.Contains(e.Location)) over = true;
        Cursor = over ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        foreach (Hit h in hits.ToArray()) if (h.R.Contains(e.Location)) { h.A(); break; }
        base.OnMouseClick(e);
    }
}

class MainForm : Form
{
    public bool Restart;
    NavBtn[] nav = new NavBtn[5];
    Panel host, pgDash, pgSet, pgWarn, pgLog, pgAct, bar;
    ListBox lbAct; ToggleRow tgMonA;
    Dash dash;
    Label toast;
    ToggleRow[] chk = new ToggleRow[6];
    ToggleRow tgAuto, tgEarly, tgUnatt, tgBoot, tgMon, tgSerial, tgUser, tgNet;
    ComboBox cbMode, cbLang, cbTheme, cbSkin;
    NumericUpDown numKeep, numMs, numClose, numAct;
    ListBox lbApps, lbWarn, lbLogs;
    Label lblNoWarn;
    TextBox txtLog;
    int cur = -1;
    static readonly string[] CK = { "drivers", "display", "speaker", "network", "boot", "apps" };

    public MainForm()
    {
        Text = Cfg.AppName;
        ClientSize = new Size(1240, 800);
        MinimumSize = new Size(1040, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = T.Bg;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        string[] kinds = { "home", "apps", "gear", "warn", "doc" };
        string[] names = { L.T("nav.dash"), L.T("nav.activity"), L.T("nav.settings"), L.T("nav.warnings"), L.T("nav.logs") };
        for (int i = 0; i < 5; i++)
        {
            int k = i;
            nav[i] = new NavBtn(); nav[i].Kind = kinds[i]; nav[i].Text = names[i]; nav[i].Location = new Point(8, 120 + i * 80);
            nav[i].Click += delegate { Show(k); };
            Controls.Add(nav[i]);
        }
        host = new Panel(); host.BackColor = T.Bg; host.Location = new Point(92, 0); host.Size = new Size(ClientSize.Width - 92, ClientSize.Height);
        host.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(host);
        BuildDash(); BuildActivity(); BuildSettings(); BuildWarnings(); BuildLogs();
        Activated += delegate { if (cur == 0) dash.Reload(); else if (cur == 1) LoadActivity(); };
        Show(0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; T.Q(g);
        g.Clear(T.Bg);
        g.FillRectangle(new SolidBrush(T.Card), 0, 0, 92, ClientSize.Height);
        T.FillRound(g, T.Accent, new RectangleF(20, 28, 52, 52), 16);
        T.Icon(g, "check", new RectangleF(30, 38, 32, 32), Color.White, 3f);
        T.Txt(g, "v" + Cfg.Version, T.F(8), T.Muted, new RectangleF(0, ClientSize.Height - 30, 92, 20), StringAlignment.Center, StringAlignment.Near, false);
    }

    void Show(int i)
    {
        cur = i;
        for (int k = 0; k < 5; k++) { nav[k].Active = k == i; nav[k].Invalidate(); }
        pgDash.Visible = i == 0; pgAct.Visible = i == 1; pgSet.Visible = i == 2; pgWarn.Visible = i == 3; pgLog.Visible = i == 4;
        if (i == 0) dash.Reload();
        if (i == 1) LoadActivity();
        if (i == 3) LoadWarnings();
        if (i == 4) LoadLogs();
    }

    void LaunchTest(string only)
    {
        try { Process.Start(Application.ExecutablePath, "/test" + (only != null ? " /only=" + only : "")); } catch { }
    }

    // ------------------------------------------------------------ helpers
    Label Head(Control p, string t, int x, int y, float size)
    {
        Label l = new Label(); l.Text = t; l.Font = T.F(size, true); l.ForeColor = T.Ink; l.AutoSize = true; l.Location = new Point(x, y); l.BackColor = Color.Transparent;
        p.Controls.Add(l); return l;
    }
    Label Note(Control p, string t, int x, int y, int w)
    {
        Label l = new Label(); l.Text = t; l.Font = T.F(9.5f); l.ForeColor = T.Muted; l.Location = new Point(x, y); l.Size = new Size(w, 26); l.BackColor = Color.Transparent;
        l.TextAlign = ContentAlignment.MiddleLeft; p.Controls.Add(l); return l;
    }
    ToggleRow Tg(Control p, string t, string s, bool v, int y, int w)
    {
        ToggleRow r = new ToggleRow(); r.Title = t; r.Sub = s; r.Value = v; r.Back = T.Bg; r.SetBounds(0, y, w, 58); p.Controls.Add(r); return r;
    }
    Btn B(Control p, string t, int style, int x, int y, int w, Action a)
    {
        Btn b = new Btn(); b.Text = t; b.Style = style; b.Back = p.BackColor; b.SetBounds(x, y, w, 44); b.Font = T.F(10, true);
        b.Click += delegate { a(); }; p.Controls.Add(b); return b;
    }
    NumericUpDown Num(Control p, int x, int y, int min, int max, int inc, int val)
    {
        NumericUpDown n = new NumericUpDown(); n.Minimum = min; n.Maximum = max; n.Increment = inc; n.Value = Math.Max(min, Math.Min(max, val));
        n.Font = T.F(10); n.SetBounds(x, y, 100, 30); p.Controls.Add(n); return n;
    }

    // ------------------------------------------------------------ DASHBOARD
    void BuildDash()
    {
        pgDash = new Panel(); pgDash.Dock = DockStyle.Fill; pgDash.AutoScroll = true; pgDash.BackColor = T.Bg; host.Controls.Add(pgDash);
        dash = new Dash(); dash.Location = new Point(0, 0); dash.Width = pgDash.ClientSize.Width; dash.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        dash.RunTest = LaunchTest;
        pgDash.Controls.Add(dash);
        pgDash.Resize += delegate { dash.Width = Math.Max(760, pgDash.ClientSize.Width); };
    }

    // ------------------------------------------------------------ SETTINGS
    void BuildSettings()
    {
        pgSet = new Panel(); pgSet.Dock = DockStyle.Fill; pgSet.BackColor = T.Bg; host.Controls.Add(pgSet);
        bar = new Panel(); bar.Dock = DockStyle.Bottom; bar.Height = 78; bar.BackColor = T.Card; pgSet.Controls.Add(bar);
        Panel sc = new Panel(); sc.Dock = DockStyle.Fill; sc.AutoScroll = true; sc.BackColor = T.Bg; pgSet.Controls.Add(sc);
        sc.BringToFront();
        Panel c = new Panel(); c.BackColor = T.Bg; c.Location = new Point(36, 28); c.Size = new Size(740, 1840); sc.Controls.Add(c);
        int W = 740, y = 0;
        Head(c, L.T("s.title"), 0, y, 24); y += 62;

        Head(c, L.T("s.checks"), 0, y, 13); y += 40;
        bool[] cv = { Cfg.Drivers, Cfg.Display, Cfg.Speaker, Cfg.Network, Cfg.Boot, Cfg.Apps };
        for (int i = 0; i < 6; i++) { chk[i] = Tg(c, L.T("c." + CK[i]), L.T("c." + CK[i] + ".d"), cv[i], y, W); y += 64; }
        y += 14;

        Head(c, L.T("s.start"), 0, y, 13); y += 40;
        tgAuto = Tg(c, L.T("s.auto"), L.T("s.auto.d"), Cfg.AutoStart, y, W); y += 64;
        tgEarly = Tg(c, L.T("s.early"), L.T("s.early.d"), Cfg.EarlyStart, y, W); y += 64;
        tgUnatt = Tg(c, L.T("s.unatt"), L.T("s.unatt.d"), Cfg.Unattended, y, W); y += 64;
        tgBoot = Tg(c, L.T("s.boot"), L.T("s.boot.d"), Cfg.BootCheck, y, W); tgBoot.Height = 66; y += 72;
        tgMon = Tg(c, L.T("s.mon"), L.T("s.mon.d"), Cfg.Monitor, y, W); tgMon.Height = 66; y += 72;
        Note(c, L.T("s.actdays"), 0, y + 4, 480); numAct = Num(c, 490, y + 2, 1, 365, 5, Cfg.ActivityDays); y += 44;
        Note(c, L.T("s.freq"), 0, y + 4, 160);
        cbMode = new ComboBox(); cbMode.DropDownStyle = ComboBoxStyle.DropDownList; cbMode.Font = T.F(10);
        cbMode.Items.AddRange(new object[] { L.T("s.f0"), L.T("s.f1") }); cbMode.SelectedIndex = Cfg.RunMode == 1 ? 1 : 0;
        cbMode.SetBounds(170, y + 2, 280, 30); c.Controls.Add(cbMode); y += 44;
        Note(c, L.T("s.ms"), 0, y + 4, 420); numMs = Num(c, 430, y + 2, 200, 3000, 100, Cfg.PatternMs); y += 44;
        Note(c, L.T("s.close"), 0, y + 4, 520); numClose = Num(c, 530, y + 2, 0, 120, 1, Cfg.AutoCloseSec); y += 60;

        Head(c, L.T("s.apps"), 0, y, 13); y += 40;
        lbApps = new ListBox(); lbApps.Font = T.F(10); lbApps.SetBounds(0, y, W, 120); lbApps.BorderStyle = BorderStyle.FixedSingle;
        foreach (string a in Cfg.AutoApps) lbApps.Items.Add(a);
        c.Controls.Add(lbApps); y += 130;
        B(c, L.T("b.add"), 0, 0, y, 170, AddApp);
        B(c, L.T("b.remove"), 1, 182, y, 190, delegate { if (lbApps.SelectedIndex >= 0) lbApps.Items.RemoveAt(lbApps.SelectedIndex); }); y += 72;

        Head(c, L.T("s.priv"), 0, y, 13); y += 40;
        tgSerial = Tg(c, L.T("s.ser"), "", Cfg.Serial, y, W); y += 64;
        tgUser = Tg(c, L.T("s.usr"), "", Cfg.UserName, y, W); y += 64;
        tgNet = Tg(c, L.T("s.spd"), "", Cfg.NetSpeed, y, W); y += 64;
        Note(c, L.T("s.keep"), 0, y + 4, 480); numKeep = Num(c, 490, y + 2, 0, 3650, 30, Cfg.KeepDays); y += 44;
        Note(c, L.T("s.local"), 0, y, W); y += 54;

        Head(c, L.T("s.look"), 0, y, 13); y += 40;
        Note(c, L.T("s.lang"), 0, y + 4, 160);
        cbLang = new ComboBox(); cbLang.DropDownStyle = ComboBoxStyle.DropDownList; cbLang.Font = T.F(10);
        int li = 0; for (int i = 0; i < L.Langs.Length; i++) { cbLang.Items.Add(L.Langs[i][1]); if (L.Langs[i][0] == Cfg.Language) li = i; }
        cbLang.SelectedIndex = li; cbLang.SetBounds(170, y + 2, 280, 30); c.Controls.Add(cbLang); y += 44;
        Note(c, L.T("s.theme"), 0, y + 4, 160);
        cbTheme = new ComboBox(); cbTheme.DropDownStyle = ComboBoxStyle.DropDownList; cbTheme.Font = T.F(10);
        cbTheme.Items.AddRange(new object[] { L.T("s.th0"), L.T("s.th1"), L.T("s.th2") }); cbTheme.SelectedIndex = Math.Max(0, Math.Min(2, Cfg.ThemePref));
        cbTheme.SetBounds(170, y + 2, 280, 30); c.Controls.Add(cbTheme); y += 44;
        Note(c, L.T("s.skin"), 0, y + 4, 160);
        cbSkin = new ComboBox(); cbSkin.DropDownStyle = ComboBoxStyle.DropDownList; cbSkin.Font = T.F(10);
        cbSkin.Items.AddRange(new object[] { L.T("sk.win11"), L.T("sk.retro"), L.T("sk.term") }); cbSkin.SelectedIndex = Math.Max(0, Math.Min(2, Cfg.Skin));
        cbSkin.SetBounds(170, y + 2, 280, 30); c.Controls.Add(cbSkin);

        toast = new Label(); toast.AutoSize = false; toast.Font = T.F(10, true); toast.ForeColor = T.Ok; toast.BackColor = Color.Transparent; toast.SetBounds(24, 26, 330, 28);
        bar.Controls.Add(toast);
        B(bar, L.T("b.save"), 0, 370, 17, 150, SaveSettings);
        B(bar, L.T("b.runfull"), 1, 532, 17, 200, delegate { SaveSettings(); LaunchTest(null); });
        B(bar, L.T("b.uninstall"), 2, 744, 17, 160, delegate { Installer.StartUninstall(); });
        foreach (Control x in bar.Controls) { Btn bb = x as Btn; if (bb != null) bb.Back = T.Card; }
    }

    void AddApp()
    {
        using (var d = new OpenFileDialog())
        {
            d.Filter = "Applications (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*";
            if (d.ShowDialog(this) == DialogResult.OK && !lbApps.Items.Contains(d.FileName)) lbApps.Items.Add(d.FileName);
        }
    }

    void SaveSettings()
    {
        string oldLang = Cfg.Language; int oldTheme = Cfg.ThemePref, oldSkin = Cfg.Skin; bool oldMon = Cfg.Monitor;
        Cfg.Drivers = chk[0].Value; Cfg.Display = chk[1].Value; Cfg.Speaker = chk[2].Value;
        Cfg.Network = chk[3].Value; Cfg.Boot = chk[4].Value; Cfg.Apps = chk[5].Value;
        Cfg.AutoStart = tgAuto.Value; Cfg.EarlyStart = tgEarly.Value; Cfg.Unattended = tgUnatt.Value; Cfg.RunMode = cbMode.SelectedIndex;
        Cfg.PatternMs = (int)numMs.Value; Cfg.AutoCloseSec = (int)numClose.Value;
        Cfg.Serial = tgSerial.Value; Cfg.UserName = tgUser.Value; Cfg.NetSpeed = tgNet.Value; Cfg.KeepDays = (int)numKeep.Value;
        Cfg.Language = L.Langs[Math.Max(0, cbLang.SelectedIndex)][0]; Cfg.ThemePref = cbTheme.SelectedIndex; Cfg.Skin = cbSkin.SelectedIndex;
        Cfg.BootCheck = tgBoot.Value; Cfg.Monitor = tgMon.Value; Cfg.ActivityDays = (int)numAct.Value;
        Cfg.AutoApps.Clear();
        foreach (object o in lbApps.Items) Cfg.AutoApps.Add(o.ToString());
        try
        {
            bool ok = Installer.ApplyStartup();
            Cfg.Save();
            if (!ok) { tgEarly.Value = false; tgBoot.Value = false; toast.ForeColor = T.Warn; toast.Text = L.T("s.adminfail"); MessageBox.Show(L.T("s.adminfail"), Cfg.AppName); }
            else { toast.ForeColor = T.Ok; toast.Text = L.T("s.saved"); }
        }
        catch (Exception ex) { toast.ForeColor = T.Bad; toast.Text = L.F("s.fail", ex.Message); return; }
        if (Cfg.Monitor && !oldMon) AppMonitor.Start(); else if (!Cfg.Monitor && oldMon) AppMonitor.RequestStop();
        if (oldLang != Cfg.Language || oldTheme != Cfg.ThemePref || oldSkin != Cfg.Skin) { Restart = true; Close(); }
    }

    // ------------------------------------------------------------ ACTIVITY (app names + open/close times only)
    void BuildActivity()
    {
        pgAct = new Panel(); pgAct.Dock = DockStyle.Fill; pgAct.BackColor = T.Bg; host.Controls.Add(pgAct);
        Head(pgAct, L.T("a.title"), 36, 28, 24);
        Note(pgAct, L.T("a.sub"), 36, 82, 700);
        tgMonA = new ToggleRow(); tgMonA.Title = L.T("s.mon"); tgMonA.Sub = L.T("s.mon.d"); tgMonA.Value = Cfg.Monitor; tgMonA.Back = T.Bg;
        tgMonA.SetBounds(36, 116, 820, 66); pgAct.Controls.Add(tgMonA);
        tgMonA.Changed += delegate
        {
            bool was = Cfg.Monitor; Cfg.Monitor = tgMonA.Value;
            try { Cfg.Save(); Installer.ApplyStartup(); } catch { }
            if (Cfg.Monitor && !was) AppMonitor.Start(); else if (!Cfg.Monitor && was) AppMonitor.RequestStop();
            tgMon.Value = Cfg.Monitor;
        };
        B(pgAct, L.T("a.clear"), 1, 36, 196, 170, delegate
        {
            if (MessageBox.Show(L.T("a.confirm"), Cfg.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { AppMonitor.Clear(); LoadActivity(); }
        });
        lbAct = new ListBox(); lbAct.DrawMode = DrawMode.OwnerDrawFixed; lbAct.ItemHeight = 62; lbAct.BorderStyle = BorderStyle.None; lbAct.BackColor = T.Bg;
        lbAct.SetBounds(36, 252, 820, 500); lbAct.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lbAct.DrawItem += DrawAct; pgAct.Controls.Add(lbAct);
    }

    List<AppMonitor.Sess> sess = new List<AppMonitor.Sess>();
    void LoadActivity()
    {
        sess = AppMonitor.Sessions(300);
        tgMonA.Value = Cfg.Monitor;
        lbAct.Items.Clear();
        foreach (var x in sess) lbAct.Items.Add(x.Name);
        if (sess.Count == 0) lbAct.Items.Add(L.T("a.none"));
    }

    void DrawAct(object s, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        Graphics g = e.Graphics; T.Q(g);
        using (var b = new SolidBrush(T.Bg)) g.FillRectangle(b, e.Bounds);
        RectangleF r = new RectangleF(e.Bounds.X, e.Bounds.Y + 3, e.Bounds.Width - 6, e.Bounds.Height - 8);
        if (sess.Count == 0) { T.Txt(g, lbAct.Items[e.Index].ToString(), T.F(11), T.Muted, r, StringAlignment.Near, StringAlignment.Center, false); return; }
        var x = sess[e.Index];
        T.FillRound(g, T.Card, r, 16);
        T.FillRound(g, T.A(x.Closed ? T.Muted : T.Ok, 40), new RectangleF(r.X + 14, r.Y + 9, 40, 40), 12);
        T.Icon(g, "apps", new RectangleF(r.X + 21, r.Y + 16, 26, 26), x.Closed ? T.Muted : T.Ok, 2f);
        T.TxtL(g, x.Name, T.F(11, true), T.Ink, new RectangleF(r.X + 68, r.Y, r.Width * 0.42f, r.Height));
        string t = L.T("a.opened") + " " + x.Open.ToString("g") + (x.Closed ? "   |   " + L.T("a.closed") + " " + x.Close.ToString("g") : "   |   " + L.T("a.running"));
        T.Txt(g, t, T.F(9), T.Muted, new RectangleF(r.X + r.Width * 0.46f, r.Y, r.Width * 0.54f - 14, r.Height), StringAlignment.Far, StringAlignment.Center, false);
    }

    // ------------------------------------------------------------ WARNINGS
    void BuildWarnings()
    {
        pgWarn = new Panel(); pgWarn.Dock = DockStyle.Fill; pgWarn.BackColor = T.Bg; host.Controls.Add(pgWarn);
        Head(pgWarn, L.T("w.title"), 36, 28, 24);
        Note(pgWarn, L.T("w.sub"), 36, 82, 700);
        B(pgWarn, L.T("b.rerun"), 0, 36, 118, 170, delegate { LaunchTest(null); });
        B(pgWarn, L.T("b.clear"), 1, 218, 118, 150, delegate { try { if (File.Exists(Cfg.WarnFile)) File.Delete(Cfg.WarnFile); } catch { } LoadWarnings(); });
        lbWarn = new ListBox(); lbWarn.DrawMode = DrawMode.OwnerDrawFixed; lbWarn.ItemHeight = 80; lbWarn.BorderStyle = BorderStyle.None; lbWarn.BackColor = T.Bg;
        lbWarn.SetBounds(36, 180, 820, 560); lbWarn.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lbWarn.DrawItem += DrawWarn; pgWarn.Controls.Add(lbWarn);
        lblNoWarn = new Label(); lblNoWarn.Text = L.T("w.none"); lblNoWarn.Font = T.F(13, true); lblNoWarn.ForeColor = T.Ok; lblNoWarn.AutoSize = true; lblNoWarn.Location = new Point(40, 200); lblNoWarn.BackColor = Color.Transparent;
        pgWarn.Controls.Add(lblNoWarn); lblNoWarn.BringToFront();
    }

    void LoadWarnings()
    {
        lbWarn.Items.Clear();
        try { if (File.Exists(Cfg.WarnFile)) foreach (string l in File.ReadAllLines(Cfg.WarnFile, Encoding.UTF8).Reverse()) lbWarn.Items.Add(l); } catch { }
        lblNoWarn.Visible = lbWarn.Items.Count == 0;
    }

    void DrawWarn(object s, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        Graphics g = e.Graphics; T.Q(g);
        using (var b = new SolidBrush(T.Bg)) g.FillRectangle(b, e.Bounds);
        string[] p = lbWarn.Items[e.Index].ToString().Split(new char[] { '|' }, 3);
        string time = p.Length > 0 ? p[0] : "", sev = p.Length > 1 ? p[1] : "warn", msg = p.Length > 2 ? p[2] : "";
        Color c = sev == "fail" ? T.Bad : T.Warn;
        RectangleF r = new RectangleF(e.Bounds.X, e.Bounds.Y + 5, e.Bounds.Width - 6, e.Bounds.Height - 12);
        T.FillRound(g, T.Card, r, 20);
        T.FillRound(g, T.A(c, 40), new RectangleF(r.X + 16, r.Y + (r.Height - 46) / 2, 46, 46), 14);
        T.Icon(g, "warn", new RectangleF(r.X + 25, r.Y + (r.Height - 46) / 2 + 9, 28, 28), c, 2f);
        T.Txt(g, msg, T.F(10.5f, true), T.Ink, new RectangleF(r.X + 78, r.Y + 8, r.Width - 96, r.Height - 32));
        T.Txt(g, time + "   |   " + L.T(sev == "fail" ? "w.sev.fail" : "w.sev.warn"), T.F(8.5f), T.Muted, new RectangleF(r.X + 78, r.Bottom - 26, r.Width - 96, 20), StringAlignment.Near, StringAlignment.Near, false);
    }

    // ------------------------------------------------------------ LOGS
    void BuildLogs()
    {
        pgLog = new Panel(); pgLog.Dock = DockStyle.Fill; pgLog.BackColor = T.Bg; host.Controls.Add(pgLog);
        Head(pgLog, L.T("l.title"), 36, 28, 24);
        Note(pgLog, L.T("l.sub"), 36, 82, 700);
        lbLogs = new ListBox(); lbLogs.Font = T.F(9.5f); lbLogs.BorderStyle = BorderStyle.None; lbLogs.BackColor = T.Card; lbLogs.ForeColor = T.Ink;
        lbLogs.SetBounds(36, 122, 290, 560); lbLogs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        lbLogs.SelectedIndexChanged += delegate { ShowLog(); }; pgLog.Controls.Add(lbLogs);
        txtLog = new TextBox(); txtLog.Multiline = true; txtLog.ReadOnly = true; txtLog.ScrollBars = ScrollBars.Both; txtLog.WordWrap = false;
        txtLog.Font = new Font("Consolas", 9f); txtLog.BackColor = T.Card; txtLog.ForeColor = T.Ink; txtLog.BorderStyle = BorderStyle.None;
        txtLog.SetBounds(342, 122, 560, 560); txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pgLog.Controls.Add(txtLog);
        Action<Btn> anc = delegate(Btn bb) { bb.Anchor = AnchorStyles.Bottom | AnchorStyles.Left; };
        anc(B(pgLog, L.T("b.openfolder"), 0, 36, 698, 170, delegate { try { Process.Start("explorer.exe", "\"" + Cfg.LogDir + "\""); } catch { } }));
        anc(B(pgLog, L.T("b.del"), 1, 218, 698, 170, DeleteLog));
        anc(B(pgLog, L.T("b.delall"), 1, 400, 698, 150, DeleteAllLogs));
    }

    void LoadLogs()
    {
        lbLogs.Items.Clear(); txtLog.Text = "";
        try { foreach (string f in Directory.GetFiles(Cfg.LogDir, "Report_*.txt").OrderByDescending(x => File.GetLastWriteTime(x))) lbLogs.Items.Add(Path.GetFileName(f)); } catch { }
        if (lbLogs.Items.Count > 0) lbLogs.SelectedIndex = 0; else txtLog.Text = L.T("l.none");
    }

    void ShowLog()
    {
        if (lbLogs.SelectedItem == null) return;
        try
        {
            string t = File.ReadAllText(Path.Combine(Cfg.LogDir, lbLogs.SelectedItem.ToString()), Encoding.UTF8);
            if (t.Length > 300000) t = t.Substring(0, 300000) + "\r\n... (truncated)";
            txtLog.Text = t;
        }
        catch (Exception ex) { txtLog.Text = ex.Message; }
    }

    void DeleteLog()
    {
        if (lbLogs.SelectedItem == null) return;
        try { File.Delete(Path.Combine(Cfg.LogDir, lbLogs.SelectedItem.ToString())); } catch { }
        LoadLogs();
    }

    void DeleteAllLogs()
    {
        if (MessageBox.Show(L.T("l.confirm"), Cfg.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { foreach (string f in Directory.GetFiles(Cfg.LogDir, "Report_*.txt")) File.Delete(f); } catch { }
        LoadLogs();
    }
}
