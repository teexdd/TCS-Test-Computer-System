using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

class SetupForm : Form
{
    int page = 0;
    Btn back, next;
    ToggleRow[] chk = new ToggleRow[6];
    ToggleRow tgAuto, tgEarly, tgUnatt, tgBoot, tgMon, tgAccept, tgLaunch, tgOpen;
    Rectangle[] skinR = new Rectangle[0];
    int hoverSkin = -1;
    TextBox legal;
    Rectangle leftR, rightR;
    Rectangle[] langR = new Rectangle[0];
    int hoverLang = -1;
    List<Item> steps = new List<Item>();
    int spin, tipIdx, tipTick;
    float prog, progTarget;
    string installError;
    System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer();
    static readonly string[] CK = { "drivers", "display", "speaker", "network", "boot", "apps" };

    public SetupForm()
    {
        Text = Cfg.AppName;
        ClientSize = new Size(1120, 740);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        leftR = new Rectangle(16, 16, 400, ClientSize.Height - 32);
        rightR = new Rectangle(leftR.Right + 44, 44, ClientSize.Width - leftR.Right - 44 - 44, ClientSize.Height - 88);

        back = new Btn(); back.Style = 1; back.Size = new Size(140, 48);
        next = new Btn(); next.Size = new Size(200, 48);
        back.Location = new Point(rightR.Right - 200 - 12 - 140, rightR.Bottom - 48);
        next.Location = new Point(rightR.Right - 200, rightR.Bottom - 48);
        back.Click += delegate { if (page > 0 && page < 4) Go(page - 1); };
        next.Click += OnNext;
        Controls.Add(back); Controls.Add(next);

        bool[] cv = { Cfg.Drivers, Cfg.Display, Cfg.Speaker, Cfg.Network, Cfg.Boot, Cfg.Apps };
        for (int i = 0; i < 6; i++) chk[i] = Row(cv[i], rightR.Y + 120 + i * 66);
        tgAuto = Row(Cfg.AutoStart, rightR.Y + 130);
        tgEarly = Row(Cfg.EarlyStart, rightR.Y + 200);
        tgUnatt = Row(Cfg.Unattended, rightR.Y + 270);
        tgBoot = Row(Cfg.BootCheck, rightR.Y + 340); tgBoot.Height = 68;
        tgMon = Row(Cfg.Monitor, rightR.Y + 418); tgMon.Height = 68;
        legal = new TextBox();
        legal.Multiline = true; legal.ReadOnly = true; legal.ScrollBars = ScrollBars.Vertical; legal.BorderStyle = BorderStyle.None;
        legal.Text = L.Legal();
        legal.SetBounds(rightR.X + 12, rightR.Y + 214, rightR.Width - 24, 250);
        Controls.Add(legal);
        tgAccept = Row(false, rightR.Y + 484);
        tgAccept.Changed += delegate { next.Enabled = tgAccept.Value; };
        tgLaunch = Row(false, rightR.Y + 200);
        tgOpen = Row(true, rightR.Y + 270);

        anim.Interval = 40; anim.Tick += OnTick; anim.Start();
        Go(0);
    }

    ToggleRow Row(bool v, int y)
    {
        ToggleRow r = new ToggleRow(); r.Value = v; r.SetBounds(rightR.X, y, rightR.Width, 58);
        Controls.Add(r); return r;
    }

    void ApplyLang()
    {
        for (int i = 0; i < 6; i++) { chk[i].Title = L.T("c." + CK[i]); chk[i].Sub = L.T("c." + CK[i] + ".d"); chk[i].Invalidate(); }
        tgAuto.Title = L.T("s.auto"); tgAuto.Sub = L.T("s.auto.d");
        tgEarly.Title = L.T("s.early"); tgEarly.Sub = L.T("s.early.d");
        tgUnatt.Title = L.T("s.unatt"); tgUnatt.Sub = L.T("s.unatt.d");
        tgBoot.Title = L.T("s.boot"); tgBoot.Sub = L.T("s.boot.d");
        tgMon.Title = L.T("s.mon"); tgMon.Sub = L.T("s.mon.d");
        tgAccept.Title = L.T("u.accept"); tgAccept.Sub = L.T("u.accept.d");
        tgLaunch.Title = L.T("u.launch"); tgLaunch.Sub = L.T("u.launch.d");
        tgOpen.Title = L.T("u.open"); tgOpen.Sub = L.T("u.open.d");
        legal.Font = T.F(9.5f); legal.BackColor = T.Row; legal.ForeColor = T.Ink;
        back.Text = L.T("b.back"); back.Font = T.F(11, true); next.Font = T.F(11, true);
        foreach (Control c in new Control[] { tgAuto, tgEarly, tgUnatt, tgBoot, tgMon, tgAccept, tgLaunch, tgOpen }) c.Invalidate();
        Text = Cfg.AppName + " - " + L.T("u.title");
        UpdateButtons();
    }

    void UpdateButtons()
    {
        next.Text = page == 3 ? L.T("b.install") : page == 4 ? L.T("b.installing") : page == 5 ? L.T("b.finish") : L.T("b.next");
        back.Visible = page > 0 && page < 4;
        next.Enabled = page == 3 ? tgAccept.Value : page != 4;
        Invalidate();
    }

    void Go(int p)
    {
        page = p;
        foreach (ToggleRow c in chk) c.Visible = p == 1;
        tgAuto.Visible = tgEarly.Visible = tgUnatt.Visible = tgBoot.Visible = tgMon.Visible = p == 2;
        legal.Visible = tgAccept.Visible = p == 3;
        tgLaunch.Visible = tgOpen.Visible = p == 5;
        ApplyLang();
        if (p == 4) StartInstall();
    }

    void OnNext(object s, EventArgs e)
    {
        if (page == 1) { ApplyCfg(); Go(2); }
        else if (page == 2) { ApplyCfg(); Go(3); }
        else if (page == 3) { ApplyCfg(); Cfg.Consent = true; Cfg.ConsentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"); Go(4); }
        else if (page == 5)
        {
            try
            {
                if (Cfg.Monitor) AppMonitor.Start();
                if (tgLaunch.Value) Process.Start(Cfg.ExePath, "/test");
                else if (tgOpen.Value) Process.Start(Cfg.ExePath);
            }
            catch { }
            Close();
        }
        else Go(page + 1);
    }

    void ApplyCfg()
    {
        Cfg.Drivers = chk[0].Value; Cfg.Display = chk[1].Value; Cfg.Speaker = chk[2].Value;
        Cfg.Network = chk[3].Value; Cfg.Boot = chk[4].Value; Cfg.Apps = chk[5].Value;
        Cfg.AutoStart = tgAuto.Value; Cfg.EarlyStart = tgEarly.Value && tgAuto.Value; Cfg.Unattended = tgUnatt.Value;
        Cfg.Language = L.Code; Cfg.BootCheck = tgBoot.Value; Cfg.Monitor = tgMon.Value;
    }

    // ------------------------------------------------------------ install
    void StartInstall()
    {
        steps.Clear();
        for (int i = 1; i <= 5; i++) steps.Add(new Item("i" + i, L.T("u.i" + i)));
        Action[] acts = { Installer.CopyFiles, Cfg.Save, Installer.MakeShortcut, delegate { if (!Installer.ApplyStartup()) { Cfg.Save(); } }, Installer.RegisterUninstall };
        installError = null; prog = progTarget = 0;
        Thread t = new Thread(delegate()
        {
            for (int i = 0; i < acts.Length; i++)
            {
                steps[i].State = 1;
                Thread.Sleep(1100);
                try { acts[i](); steps[i].State = 2; }
                catch (Exception ex) { steps[i].State = 4; installError = ex.Message; break; }
                progTarget = (i + 1) / (float)acts.Length;
            }
            Thread.Sleep(900);
            try { BeginInvoke(new Action(AfterInstall)); } catch { }
        });
        t.IsBackground = true; t.Start();
    }

    void AfterInstall()
    {
        if (installError != null)
        {
            MessageBox.Show(L.F("u.err", installError), Cfg.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Go(3);
        }
        else Go(5);
    }

    void OnTick(object s, EventArgs e)
    {
        if (page != 4) return;
        spin = (spin + 14) % 360; prog += (progTarget - prog) * 0.12f;
        if (++tipTick % 125 == 0) tipIdx = (tipIdx + 1) % 10;
        Invalidate(rightR);
    }

    // ------------------------------------------------------------ language tiles (page 0)
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = -1;
        int hs = -1;
        if (page == 0) { for (int i = 0; i < langR.Length; i++) if (langR[i].Contains(e.Location)) h = i; for (int i = 0; i < skinR.Length; i++) if (skinR[i].Contains(e.Location)) hs = i; }
        if (h != hoverLang || hs != hoverSkin) { hoverLang = h; hoverSkin = hs; Cursor = (h >= 0 || hs >= 0) ? Cursors.Hand : Cursors.Default; Invalidate(rightR); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (page == 0)
            for (int i = 0; i < langR.Length; i++)
                if (langR[i].Contains(e.Location)) { L.Set(L.Langs[i][0]); Cfg.Language = L.Code; ApplyLang(); Invalidate(); }
        if (page == 0)
            for (int i = 0; i < skinR.Length; i++)
                if (skinR[i].Contains(e.Location)) { Cfg.Skin = i; T.Init(Cfg.ThemePref, i); BackColor = T.Card; ApplyLang(); Invalidate(); foreach (Control c in Controls) c.Invalidate(); }
        base.OnMouseDown(e);
    }

    // ------------------------------------------------------------ paint
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; T.Q(g);
        g.Clear(T.Card);
        GraphicsState st = g.Save();
        using (var clip = T.Round(leftR, 30)) g.SetClip(clip, CombineMode.Replace);
        T.Bloom(g, leftR);
        g.Restore(st);
        Color bf = T.ObBgFg, fg = T.ObFg;
        T.FillRound(g, T.A(bf, 40), new RectangleF(leftR.X + 36, leftR.Y + 36, 52, 52), 16);
        T.Icon(g, "check", new RectangleF(leftR.X + 46, leftR.Y + 46, 32, 32), bf, 3f);
        T.Txt(g, Cfg.AppName, T.F(22, true), bf, new RectangleF(leftR.X + 36, leftR.Y + 104, leftR.Width - 60, 40), StringAlignment.Near, StringAlignment.Near, false);
        T.Txt(g, L.T("u.tagline"), T.F(12), T.A(bf, 215), new RectangleF(leftR.X + 36, leftR.Y + 148, leftR.Width - 60, 56));
        for (int i = 0; i < 3; i++)
        {
            RectangleF r = new RectangleF(leftR.X + 28, leftR.Y + 250 + i * 112, leftR.Width - 56, 100);
            T.Glass(g, r, 22, 28);
            using (var b = new SolidBrush(T.Skin == 0 ? Color.White : T.Accent)) g.FillEllipse(b, r.X + 18, r.Y + 28, 40, 40);
            T.Txt(g, (i + 1).ToString(), T.F(13, true), T.Skin == 0 ? T.C(10, 70, 180) : T.OnAccent, new RectangleF(r.X + 18, r.Y + 28, 40, 40), StringAlignment.Center, StringAlignment.Center, false);
            T.Txt(g, L.T("u.step" + (i + 1)), T.F(12, true), fg, new RectangleF(r.X + 70, r.Y + 14, r.Width - 84, 26), StringAlignment.Near, StringAlignment.Near, false);
            T.Txt(g, L.T("u.step" + (i + 1) + "d"), T.F(9.5f), T.A(fg, 215), new RectangleF(r.X + 70, r.Y + 42, r.Width - 84, 50));
        }
        T.Txt(g, L.T("u.nouac"), T.F(9), T.A(bf, 190), new RectangleF(leftR.X + 36, leftR.Bottom - 56, leftR.Width - 60, 40));

        string[] tk = { "u.lang", "u.chk", "u.start", "u.legal", "u.inst", "u.done" };
        string[] sk = { "u.langs", "u.chks", "u.starts", "u.legals", "u.insts", "u.dones" };
        T.Txt(g, L.T(tk[page]), T.F(26, true), T.Ink, new RectangleF(rightR.X, rightR.Y, rightR.Width, 52), StringAlignment.Near, StringAlignment.Near, false);
        T.Txt(g, L.T(sk[page]), T.F(11), T.Muted, new RectangleF(rightR.X, rightR.Y + 56, rightR.Width, 44));

        if (page == 0) { PaintLangs(g); PaintSkins(g); }
        else if (page == 3) T.Txt(g, L.T("u.legal.sum") + "\n" + L.T("u.legal.ver"), T.F(9.5f), T.Ink, new RectangleF(rightR.X, rightR.Y + 100, rightR.Width, 108));
        else if (page == 4) PaintInstall(g);
        else if (page == 5) T.StateIcon(g, 2, new RectangleF(rightR.X, rightR.Y + 116, 60, 60), 0);
        // step dots
        for (int i = 0; i < 6; i++)
            using (var b = new SolidBrush(i == page ? T.Accent : T.Line)) g.FillEllipse(b, rightR.X + i * 18, rightR.Bottom - 20, i == page ? 10 : 8, i == page ? 10 : 8);
    }

    void PaintLangs(Graphics g)
    {
        int n = L.Langs.Length;
        langR = new Rectangle[n];
        int cw = (rightR.Width - 16) / 2;
        for (int i = 0; i < n; i++)
        {
            Rectangle r = new Rectangle(rightR.X + (i % 2) * (cw + 16), rightR.Y + 120 + (i / 2) * 84, cw, 72);
            langR[i] = r;
            bool sel = L.Langs[i][0] == L.Code;
            T.FillRound(g, sel ? T.A(T.Accent, 30) : (hoverLang == i ? T.A(T.Accent, 14) : T.Row), r, 18);
            if (sel) using (var p = new Pen(T.Accent, 2f)) T.DrawRound(g, p, new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), 18);
            using (var f = T.FF(T.FamilyFor(L.Langs[i][0]), 13, true)) T.TxtL(g, L.Langs[i][1], f, T.Ink, new RectangleF(r.X + 22, r.Y, r.Width - 80, r.Height));
            RectangleF dot = new RectangleF(r.Right - 44, r.Y + 24, 24, 24);
            if (sel) T.StateIcon(g, 2, dot, 0); else using (var p = new Pen(T.Line, 2f)) g.DrawEllipse(p, dot.X + 1, dot.Y + 1, 22, 22);
        }
    }

    void PaintSkins(Graphics g)
    {
        string[] n = { L.T("sk.win11"), L.T("sk.retro"), L.T("sk.term") };
        Color[] sw = { T.C(0, 103, 192), T.C(0, 128, 128), T.C(0, 255, 170) };
        int y0 = rightR.Y + 120 + ((L.Langs.Length + 1) / 2) * 84 + 10;
        T.Txt(g, L.T("u.skin"), T.F(13, true), T.Ink, new RectangleF(rightR.X, y0, rightR.Width, 28), StringAlignment.Near, StringAlignment.Near, false);
        skinR = new Rectangle[3];
        int cw = (rightR.Width - 24) / 3;
        for (int i = 0; i < 3; i++)
        {
            Rectangle r = new Rectangle(rightR.X + i * (cw + 12), y0 + 36, cw, 76);
            skinR[i] = r;
            bool sel = Cfg.Skin == i;
            T.FillRound(g, sel ? T.A(T.Accent, 30) : (hoverSkin == i ? T.A(T.Accent, 14) : T.Row), r, 14);
            if (sel) using (var p = new Pen(T.Accent, 2f)) g.DrawRectangle(p, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
            using (var b = new SolidBrush(sw[i])) g.FillRectangle(b, r.X + 14, r.Y + 16, 44, 44);
            if (i == 1) using (var p = new Pen(Color.White)) { g.DrawLine(p, r.X + 14, r.Y + 16, r.X + 57, r.Y + 16); g.DrawLine(p, r.X + 14, r.Y + 16, r.X + 14, r.Y + 59); }
            if (i == 2) T.Txt(g, ">_", T.F(12, true), Color.Black, new RectangleF(r.X + 14, r.Y + 16, 44, 44), StringAlignment.Center, StringAlignment.Center, false);
            T.TxtL(g, n[i], T.F(10.5f, true), T.Ink, new RectangleF(r.X + 68, r.Y, r.Width - 74, r.Height));
        }
    }

    void PaintInstall(Graphics g)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            RectangleF rr = new RectangleF(rightR.X, rightR.Y + 116 + i * 54, rightR.Width, 48);
            T.FillRound(g, T.Row, rr, 14);
            T.StateIcon(g, steps[i].State, new RectangleF(rr.X + 14, rr.Y + 12, 24, 24), spin);
            T.TxtL(g, steps[i].Name, T.F(11, true), steps[i].State == 0 ? T.Muted : T.Ink, new RectangleF(rr.X + 52, rr.Y, rr.Width - 66, rr.Height));
        }
        float py = rightR.Y + 116 + steps.Count * 54 + 10;
        T.FillRound(g, T.Line, new RectangleF(rightR.X, py, rightR.Width, 8), 4);
        if (prog > 0.004f)
        {
            RectangleF pr = new RectangleF(rightR.X, py, Math.Max(10, rightR.Width * Math.Min(prog, 1f)), 8);
            using (var b = new LinearGradientBrush(pr, T.Accent, T.C(150, 90, 255), 0f)) T.FillRound(g, b, pr, 4);
        }
        RectangleF tb = new RectangleF(rightR.X, rightR.Bottom - 170, rightR.Width, 124);
        T.FillRound(g, T.A(T.Accent, 22), tb, 24);
        Color[] pal = { T.C(61, 90, 254), T.C(255, 150, 60), T.C(40, 190, 130), T.C(255, 90, 140), T.C(150, 100, 255) };
        T.FillRound(g, pal[tipIdx % 5], new RectangleF(tb.X + 20, tb.Y + 20, 60, 60), 18);
        T.Icon(g, "bulb", new RectangleF(tb.X + 32, tb.Y + 32, 36, 36), Color.White, 2.4f);
        T.Txt(g, L.T("o.tipchip"), T.F(9.5f, true), T.Accent, new RectangleF(tb.X + 100, tb.Y + 14, tb.Width - 120, 22), StringAlignment.Near, StringAlignment.Near, false);
        T.Txt(g, L.T("tip" + (tipIdx % 10 + 1)), T.F(11), T.Ink, new RectangleF(tb.X + 100, tb.Y + 38, tb.Width - 120, tb.Height - 44));
    }

    protected override void OnFormClosed(FormClosedEventArgs e) { anim.Stop(); anim.Dispose(); base.OnFormClosed(e); }
}
