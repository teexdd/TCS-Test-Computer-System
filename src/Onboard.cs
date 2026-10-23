using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

class Item
{
    public string Key, Name; public int State; public string Detail = "";
    public Item(string key, string name) { Key = key; Name = name; }
}

// Full-screen "installer style" check shown at sign-in: 1 Checking -> 2 Testing -> 3 Summary -> desktop + auto-start apps.
// Esc / "Skip to desktop" always works. In unattended mode nothing is asked. The process exits when finished.
class OnboardForm : Form
{
    readonly string only;
    readonly bool auto;
    List<Item> items = new List<Item>(), all = new List<Item>();
    int step = 1, spin, tick, countdown = -1, lit, tipIdx, hover;
    string title = "", sub = "", question, curName = "";
    bool spk, pat, finished, showGo;
    int patIdx;
    volatile bool patAbort, closing;
    float prog, progTarget;
    int totalSteps = 1, doneSteps;
    string[] qOpts = new string[0];
    Rectangle rcStep, rcLeft, rcRight, rcTip, rcQ, rcSkip, rcGo, rcBot;
    Rectangle[] qR = new Rectangle[0];
    System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer();
    Thread worker;
    ManualResetEvent ans = new ManualResetEvent(false);
    int answer;
    double memShown;

    public OnboardForm(string only, bool auto)
    {
        this.only = only; this.auto = auto;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen.Bounds;
        TopMost = true; ShowInTaskbar = false; KeyPreview = true;
        BackColor = T.C(6, 40, 110); Text = Cfg.AppName;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        anim.Interval = 40; anim.Tick += OnTick;
        Shown += delegate { Activate(); ComputeLayout(); anim.Start(); worker = new Thread(Flow); worker.IsBackground = true; worker.Start(); };
        KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { if (pat) patAbort = true; else Finish(); } };
        Resize += delegate { ComputeLayout(); };
    }

    // ------------------------------------------------------------ layout
    void ComputeLayout()
    {
        int W = ClientSize.Width, H = ClientSize.Height;
        rcStep = new Rectangle((W - (3 * 210 + 28)) / 2, (int)(H * 0.07), 3 * 210 + 28, 46);
        rcLeft = new Rectangle((int)(W * 0.09), (int)(H * 0.20), (int)(W * 0.38), (int)(H * 0.50));
        int rows = Math.Max(items.Count, 3);
        int rh = Math.Min((int)(H * 0.52), rows * 56 + 36);
        rcRight = new Rectangle((int)(W * 0.53), (int)(H * 0.20), (int)(W * 0.38), rh);
        int tw = Math.Min(940, (int)(W * 0.82));
        rcTip = new Rectangle((W - tw) / 2, H - 212, tw, 112);
        rcBot = new Rectangle((int)(W * 0.06), H - 60, (int)(W * 0.88), 30);
        rcSkip = new Rectangle(rcBot.Right - 300, rcBot.Y, 300, 30);
        rcGo = new Rectangle(rcLeft.X, rcLeft.Bottom - 60, 280, 56);
        // question box: measure the buttons so they always fit inside the box (monospace/CJK fonts are wider)
        int n = qOpts.Length;
        int[] bw = new int[n]; int sum = 0;
        if (n > 0)
        {
            Font bfnt = T.F(11.5f, true);
            using (Graphics mg = CreateGraphics())
                for (int i = 0; i < n; i++)
                {
                    string t = T.Skin == 2 ? "[ " + qOpts[i] + " ]" : qOpts[i];
                    bw[i] = Math.Max(150, (int)mg.MeasureString(t, bfnt).Width + 56); sum += bw[i];
                }
        }
        int qw = Math.Max(700, sum + 14 * Math.Max(n - 1, 0) + 64);
        qw = Math.Min(qw, W - 60);
        if (n > 0 && sum + 14 * (n - 1) + 64 > qw)
        {
            double f = (qw - 64 - 14 * (n - 1)) / (double)sum;
            for (int i = 0; i < n; i++) bw[i] = (int)(bw[i] * f);
        }
        rcQ = new Rectangle((W - qw) / 2, (H - 260) / 2, qw, 260);
        qR = new Rectangle[n];
        int bx = rcQ.X + 32;
        for (int i = 0; i < n; i++) { qR[i] = new Rectangle(bx, rcQ.Bottom - 84, bw[i], 52); bx += bw[i] + 14; }
        Invalidate();
    }

    // ------------------------------------------------------------ painting (colours/shapes come from the current skin)
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        if (pat) { Patterns.Draw(g, patIdx, ClientSize.Width, ClientSize.Height); return; }
        T.Q(g);
        T.Bloom(g, ClientRectangle);
        PaintStepper(g); PaintLeft(g); PaintRight(g); PaintTip(g); PaintBottom(g);
        if (question != null) PaintQuestion(g);
    }

    void PaintStepper(Graphics g)
    {
        string[] n = { L.T("o.step1"), L.T("o.step2"), L.T("o.step3") };
        Color fg = T.ObFg;
        for (int i = 0; i < 3; i++)
        {
            Rectangle pr = new Rectangle(rcStep.X + i * 224, rcStep.Y, 210, 46);
            bool cur = i + 1 == step, done = i + 1 < step;
            T.Glass(g, pr, 23, cur ? 70 : done ? 38 : 16);
            RectangleF c = new RectangleF(pr.X + 8, pr.Y + 8, 30, 30);
            if (done) T.StateIcon(g, 2, c, 0);
            else if (cur)
            {
                using (var b = new SolidBrush(T.Skin == 0 ? Color.White : T.Accent)) g.FillEllipse(b, c);
                T.Txt(g, (i + 1).ToString(), T.F(11, true), T.Skin == 0 ? T.C(10, 70, 180) : T.OnAccent, c, StringAlignment.Center, StringAlignment.Center, false);
            }
            else { using (var p = new Pen(T.A(fg, 110), 2f)) g.DrawEllipse(p, c.X + 1, c.Y + 1, 28, 28); T.Txt(g, (i + 1).ToString(), T.F(11, true), T.A(fg, 150), c, StringAlignment.Center, StringAlignment.Center, false); }
            T.TxtL(g, n[i], T.F(11, true), T.A(fg, cur ? 255 : 190), new RectangleF(pr.X + 48, pr.Y, pr.Width - 56, pr.Height));
        }
    }

    void PaintLeft(Graphics g)
    {
        Color bf = T.ObBgFg, fg = T.ObFg;
        RectangleF ic = new RectangleF(rcLeft.X, rcLeft.Y, 68, 68);
        T.Glass(g, ic, 22, 46);
        T.Icon(g, step == 1 ? "chip" : Checks.Notebook ? "laptop" : "monitor", new RectangleF(ic.X + 14, ic.Y + 14, 40, 40), fg, 2.4f);
        T.Txt(g, title, T.F(30, true), bf, new RectangleF(rcLeft.X, rcLeft.Y + 88, rcLeft.Width, 52), StringAlignment.Near, StringAlignment.Near, false);
        T.Txt(g, sub, T.F(12), T.A(bf, 215), new RectangleF(rcLeft.X, rcLeft.Y + 146, rcLeft.Width, 64));
        int pct = (int)(Math.Min(prog, 1f) * 100);
        T.Txt(g, pct + "%", T.F(54, false), bf, new RectangleF(rcLeft.X, rcLeft.Y + 214, rcLeft.Width, 84), StringAlignment.Near, StringAlignment.Near, false);
        float by = rcLeft.Y + 308;
        if (T.Skin == 2)
        {
            int cells = 32, on = (int)(Math.Min(prog, 1f) * cells);
            T.Txt(g, "[" + new string('#', on) + new string('-', cells - on) + "]", T.F(13, true), bf, new RectangleF(rcLeft.X, by - 6, rcLeft.Width, 26), StringAlignment.Near, StringAlignment.Near, false);
        }
        else
        {
            T.FillRound(g, T.A(bf, 50), new RectangleF(rcLeft.X, by, rcLeft.Width, 8), 4);
            if (prog > 0.004f) T.FillRound(g, bf, new RectangleF(rcLeft.X, by, Math.Max(10, rcLeft.Width * Math.Min(prog, 1f)), 8), 4);
        }
        T.Txt(g, (T.Skin == 2 ? "> " : "") + curName, T.F(10.5f), T.A(bf, 200), new RectangleF(rcLeft.X, by + 20, rcLeft.Width, 26), StringAlignment.Near, StringAlignment.Near, false);
        if (showGo)
            T.Push(g, rcGo, countdown > 0 ? L.F("b.desktopn", countdown) : L.T("b.desktop"), T.F(12, true), true, hover == 2, false, true, true);
    }

    void PaintRight(Graphics g)
    {
        Color fg = T.ObFg;
        T.Glass(g, rcRight, 28, 30);
        T.PanelTitle(g, rcRight, L.T("o.step" + step));
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            RectangleF rr = new RectangleF(rcRight.X + 18, rcRight.Y + 18 + i * 56, rcRight.Width - 36, 50);
            if (it.State == 1) T.FillRound(g, T.A(T.Skin == 0 ? Color.White : T.Accent, T.Skin == 0 ? 34 : 40), rr, 16);
            T.StateIconD(g, it.State, new RectangleF(rr.X + 12, rr.Y + 13, 24, 24), spin);
            T.TxtL(g, it.Name, T.F(11.5f, true), T.A(fg, it.State == 0 ? 150 : 255), new RectangleF(rr.X + 50, rr.Y, rr.Width * .50f, rr.Height));
            Color dc = it.State == 3 ? (T.Skin == 0 ? T.C(255, 214, 140) : T.Warn) : it.State == 4 ? (T.Skin == 0 ? T.C(255, 160, 160) : T.Bad) : T.A(fg, 190);
            T.Txt(g, it.Detail, T.F(9.5f), dc, new RectangleF(rr.X + rr.Width * .52f, rr.Y, rr.Width * .48f - 12, rr.Height), StringAlignment.Far, StringAlignment.Center, false);
        }
    }

    void PaintTip(Graphics g)
    {
        Color fg = T.ObFg, bf = T.ObBgFg;
        T.Glass(g, rcTip, 26, 30);
        T.PanelTitle(g, rcTip, L.T("o.tipchip"));
        Color[] pal = { T.C(120, 148, 255), T.C(255, 150, 60), T.C(40, 190, 130), T.C(255, 90, 140), T.C(150, 100, 255) };
        string[] ik = { "ram", "monitor", "disk", "chip", "power", "power", "monitor", "wifi", "disk", "bulb" };
        RectangleF tile = new RectangleF(rcTip.X + 22, rcTip.Y + 22, 68, 68);
        T.FillRound(g, T.Skin == 2 ? T.A(T.Accent, 40) : pal[tipIdx % pal.Length], tile, 20);
        T.Icon(g, ik[tipIdx % ik.Length], new RectangleF(tile.X + 14, tile.Y + 14, 40, 40), T.Skin == 2 ? T.Accent : Color.White, 2.4f);
        if (T.Skin == 0) T.Txt(g, L.T("o.tipchip"), T.F(9.5f, true), T.A(fg, 190), new RectangleF(rcTip.X + 110, rcTip.Y + 16, rcTip.Width - 140, 22), StringAlignment.Near, StringAlignment.Near, false);
        T.Txt(g, L.T("tip" + (tipIdx % 10 + 1)), T.F(12), fg, new RectangleF(rcTip.X + 110, rcTip.Y + (T.Skin == 0 ? 40 : 22), rcTip.Width - 140, rcTip.Height - 30));
        for (int i = 0; i < 10; i++)
            using (var b = new SolidBrush(T.A(bf, i == tipIdx % 10 ? 240 : 80))) g.FillEllipse(b, rcTip.X + rcTip.Width / 2f - 55 + i * 11, rcTip.Bottom + 12, 6, 6);
    }

    void PaintBottom(Graphics g)
    {
        Color bf = T.ObBgFg;
        string left = L.T(auto ? "o.mode.auto" : "o.mode.ask") + "   |   " + L.F("o.ram", memShown.ToString("0"));
        T.Txt(g, left, T.F(9.5f), T.A(bf, 170), new RectangleF(rcBot.X, rcBot.Y, rcBot.Width - 320, rcBot.Height), StringAlignment.Near, StringAlignment.Center, false);
        T.Txt(g, L.T("b.skip") + "  (Esc)", T.F(10.5f, true), T.A(bf, hover == 1 ? 255 : 190), rcSkip, StringAlignment.Far, StringAlignment.Center, false);
    }

    void PaintQuestion(Graphics g)
    {
        using (var b = new SolidBrush(T.A(Color.Black, 120))) g.FillRectangle(b, ClientRectangle);
        Color fg = T.ObFg;
        if (T.Skin == 0)
        {
            T.FillRound(g, T.C(14, 40, 100), rcQ, 14);
            using (var p = new Pen(T.A(Color.White, 90), 1.5f)) T.DrawRound(g, p, rcQ, 14);
        }
        else { T.Glass(g, rcQ, 0, 255); T.PanelTitle(g, rcQ, Cfg.AppName); }
        float tw = rcQ.Width - 64 - (spk ? 150 : 0);
        T.Txt(g, question, T.F(13), fg, new RectangleF(rcQ.X + 32, rcQ.Y + 28, tw, rcQ.Height - 120));
        if (spk)
            for (int i = 0; i < 2; i++)
            {
                bool on = lit == 3 || lit == i + 1;
                RectangleF r = new RectangleF(rcQ.Right - 150 + i * 66, rcQ.Y + 32, 56, 56);
                T.FillRound(g, on ? T.Accent : T.A(fg, 50), r, 12);
                T.Txt(g, i == 0 ? "L" : "R", T.F(16, true), on ? T.OnAccent : fg, r, StringAlignment.Center, StringAlignment.Center, false);
                if (on) using (var p = new Pen(T.A(fg, 140), 3f)) g.DrawArc(p, r.X - 8, r.Y - 8, r.Width + 16, r.Height + 16, i == 0 ? 120 : -60, 120);
            }
        for (int i = 0; i < qR.Length; i++)
            T.Push(g, qR[i], qOpts[i], T.F(11.5f, true), i == 0, hover == 10 + i, false, true, true);
    }

    // ------------------------------------------------------------ input / timer
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = 0;
        if (question != null) { for (int i = 0; i < qR.Length; i++) if (qR[i].Contains(e.Location)) h = 10 + i; }
        else if (rcSkip.Contains(e.Location)) h = 1;
        else if (showGo && rcGo.Contains(e.Location)) h = 2;
        if (h != hover) { hover = h; Cursor = h != 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (pat) { patAbort = true; return; }
        if (question != null) { for (int i = 0; i < qR.Length; i++) if (qR[i].Contains(e.Location)) { answer = i; ans.Set(); } return; }
        if (rcSkip.Contains(e.Location)) Finish();
        else if (showGo && rcGo.Contains(e.Location)) Finish();
        base.OnMouseDown(e);
    }

    void OnTick(object s, EventArgs e)
    {
        if (pat) return;
        tick++; spin = (spin + 14) % 360;
        if (tick % 25 == 1) { memShown = Checks.MemMb(); Invalidate(rcBot); }
        float old = prog; prog += (progTarget - prog) * 0.12f;
        if (tick % 150 == 0) { tipIdx = (tipIdx + 1) % 10; Invalidate(Rectangle.Inflate(rcTip, 10, 30)); }
        if (countdown >= 0 && tick % 25 == 0) { countdown--; if (countdown <= 0) { Finish(); return; } Invalidate(rcGo); }
        Invalidate(Rectangle.Inflate(rcRight, 6, 6));
        if (Math.Abs(prog - old) > 0.001f) Invalidate(Rectangle.Inflate(rcLeft, 6, 6));
    }

    // ------------------------------------------------------------ worker helpers
    void UI(Action a) { if (closing) return; try { BeginInvoke(a); } catch { } }
    void UIWait(Action a) { if (closing) return; try { Invoke(a); } catch { } }
    void SetItem(Item it, int st, string detail) { it.State = st; it.Detail = detail; UI(delegate { Invalidate(Rectangle.Inflate(rcRight, 6, 6)); }); }

    void GoStep(int n, string t, string s, List<Item> list)
    {
        UIWait(delegate { step = n; title = t; sub = s; items = list; question = null; spk = false; qOpts = new string[0]; ComputeLayout(); });
    }

    void SetQuestion(string q, bool speaker)
    {
        UIWait(delegate { question = q; spk = speaker; qOpts = new string[0]; ComputeLayout(); });
    }

    int Ask(string q, bool speaker, params string[] opts)
    {
        ans.Reset(); answer = -1;
        UIWait(delegate { question = q; spk = speaker; qOpts = opts; ComputeLayout(); });
        ans.WaitOne();
        UIWait(delegate { question = null; spk = false; qOpts = new string[0]; ComputeLayout(); });
        return answer;
    }

    // ------------------------------------------------------------ the flow (worker thread)
    Item Add(List<Item> l, string key) { Item i = new Item(key, L.T("c." + key)); l.Add(i); all.Add(i); return i; }

    void Run(Item it, Func<Res> f) { Run(it, f, null); }

    void Run(Item it, Func<Res> f, Dictionary<string, Res> boot)
    {
        UIWait(delegate { curName = it.Name + "..."; });
        SetItem(it, 1, "");
        Res r;
        try { Res b; if (boot != null && boot.TryGetValue(it.Key, out b)) r = b; else r = f(); }
        catch (Exception ex) { r = new Res(3, L.F("r.err", ex.Message)); Checks.Log("ERROR in " + it.Key + ": " + ex); }
        Checks.Summ(it.Key, r);
        SetItem(it, r.State, r.Detail);
        doneSteps++; progTarget = Math.Min(1f, doneSteps / (float)totalSteps);
        Thread.Sleep(auto ? 150 : 250);
    }

    void Flow()
    {
        try
        {
            Func<string, bool, bool> On = delegate(string k, bool cfg) { return only != null ? only == k : cfg; };
            bool dDrv = On("drivers", Cfg.Drivers), dBoot = On("boot", Cfg.Boot), dApp = On("apps", Cfg.Apps);
            bool dDsp = On("display", Cfg.Display), dSpk = On("speaker", Cfg.Speaker), dNet = On("network", Cfg.Network);
            totalSteps = 1 + (dDrv ? 1 : 0) + (dBoot ? 1 : 0) + (dApp ? 1 : 0) + (dDsp ? 1 : 0) + (dSpk ? 1 : 0) + (dNet ? 1 : 0);

            // results the boot task already produced before sign-in (matched to this boot) are shown instantly
            Dictionary<string, Res> bootRes = only == null ? Checks.LoadBoot() : null;
            var l1 = new List<Item>();
            Item iSys = Add(l1, "sys");
            Item iDrv = dDrv ? Add(l1, "drivers") : null;
            Item iBoot = dBoot ? Add(l1, "boot") : null;
            Item iApp = dApp ? Add(l1, "apps") : null;
            GoStep(1, L.T("o.t1"), L.T("o.s1"), l1);
            Run(iSys, Checks.SystemInfo, bootRes);
            if (iDrv != null) Run(iDrv, Checks.Drivers, bootRes);
            if (iBoot != null) Run(iBoot, Checks.Boot, bootRes);
            if (iApp != null) Run(iApp, Checks.Apps);
            Checks.Trim();
            if (closing) return;

            if (dDsp || dSpk || dNet)
            {
                var l2 = new List<Item>();
                Item iDsp = dDsp ? Add(l2, "display") : null;
                Item iSpk = dSpk ? Add(l2, "speaker") : null;
                Item iNet = dNet ? Add(l2, "network") : null;
                GoStep(2, L.T("o.t2"), L.T(auto ? "o.s2.auto" : "o.s2.ask"), l2);
                if (iDsp != null) Run(iDsp, RunDisplay);
                if (iSpk != null) Run(iSpk, RunSpeaker);
                if (iNet != null) Run(iNet, Checks.Network);
                Checks.Trim();
                if (closing) return;
            }

            try { Checks.Save(); } catch { }
            var l3 = new List<Item>();
            Item t = new Item("type", L.T("o.sumtype")); t.State = 2; t.Detail = L.T(Checks.Notebook ? "t.nb" : "t.pc"); l3.Add(t);
            int bad = 0;
            foreach (Item it in all) { if (it == iSys) continue; l3.Add(it); if (it.State == 3 || it.State == 4) bad++; }
            if (iSys.State == 3 || iSys.State == 4) bad++;
            Checks.Trim();
            Item m = new Item("ram", L.T("o.sumram")); m.State = 2; m.Detail = L.F("o.sumram.v", Checks.MemMb().ToString("0")); l3.Add(m);
            int cd = Cfg.AutoCloseSec;
            if (auto && cd <= 0) cd = 8;
            if (!auto && bad > 0) cd = 0;
            UIWait(delegate
            {
                progTarget = 1f; step = 3; curName = "";
                title = bad == 0 ? L.T("o.t3ok") : L.F("o.t3w", bad);
                sub = bad == 0 ? L.T("o.s3ok") : L.T("o.s3w");
                items = l3; question = null; showGo = true;
                if (cd > 0) countdown = cd;
                ComputeLayout();
            });
        }
        catch (Exception ex) { try { Checks.Log("FLOW ERROR: " + ex); } catch { } UI(delegate { Finish(); }); }
    }

    // ---- display test
    Res RunDisplay()
    {
        if (!auto)
        {
            int a = Ask(L.F("o.qd", Cfg.PatternMs), false, L.T("o.qd.start"), L.T("o.qd.skip"));
            if (a != 0) return new Res(5, L.T("r.skip"));
        }
        UIWait(delegate { pat = true; patIdx = 0; patAbort = false; Invalidate(); });
        bool aborted = false;
        for (int i = 0; i < Patterns.Count && !closing; i++)
        {
            int k = i;
            UIWait(delegate { patIdx = k; Invalidate(); });
            for (int ms = 0; ms < Cfg.PatternMs && !patAbort; ms += 25) Thread.Sleep(25);
            if (patAbort) { aborted = true; break; }
        }
        UIWait(delegate { pat = false; ComputeLayout(); });
        Checks.Log("Display patterns: " + string.Join(", ", Patterns.Names) + (aborted ? " (aborted by user)" : ""));
        if (aborted) return new Res(5, L.T("r.dsp.stop"));
        if (auto) return new Res(2, L.T("r.dsp.auto"));
        int r = Ask(L.T("o.qd.conf"), false, L.T("o.qd.fine"), L.T("o.qd.bad"));
        if (r == 1) { Checks.Warn("fail", L.T("w.udisp")); return new Res(4, L.T("r.dsp.bad")); }
        return new Res(2, L.T("r.dsp.ok"));
    }

    // ---- speaker test
    Res RunSpeaker()
    {
        if (!Checks.HasAudio()) return new Res(4, L.T("r.noaudio"));
        while (!closing)
        {
            SetQuestion(L.T("o.qs.play"), true);
            try
            {
                UIWait(delegate { lit = 1; Invalidate(rcQ); }); Checks.PlayTone(0);
                UIWait(delegate { lit = 2; Invalidate(rcQ); }); Checks.PlayTone(1);
                UIWait(delegate { lit = 3; Invalidate(rcQ); }); Checks.PlayTone(2);
            }
            catch (Exception ex) { UIWait(delegate { question = null; spk = false; ComputeLayout(); }); return new Res(4, L.F("r.err", ex.Message)); }
            UIWait(delegate { lit = 0; });
            if (auto) { UIWait(delegate { question = null; spk = false; ComputeLayout(); }); return new Res(2, L.T("r.spk.auto")); }
            int r = Ask(L.T("o.qs.conf"), true, L.T("o.qs.fine"), L.T("o.qs.bad"), L.T("o.qs.again"));
            if (r == 2) continue;
            if (r == 1) { Checks.Warn("fail", L.T("w.uspk")); return new Res(4, L.T("r.spk.bad")); }
            return new Res(2, L.T("r.spk.ok"));
        }
        return new Res(5, L.T("r.skip"));
    }

    // ------------------------------------------------------------ finish: desktop + auto-start apps
    void Finish()
    {
        if (finished) return;
        finished = true; closing = true; patAbort = true; ans.Set(); anim.Stop();
        try { Checks.Save(); } catch { }
        Cfg.MarkRun();
        Hide();
        foreach (string app in Cfg.AutoApps)
        {
            try { if (File.Exists(app)) Process.Start(new ProcessStartInfo(app) { UseShellExecute = true }); } catch { }
            Thread.Sleep(300);
        }
        Application.Exit();
    }
}
