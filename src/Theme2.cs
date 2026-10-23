using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

// Three UI skins share one set of drawing helpers:  0 = Windows 11 (Fluent)   1 = Retro (90s desktop)   2 = Hi-Tech terminal.
// Everything is vector/GDI+ drawn (no image files) to keep RAM low.
static partial class T
{
    public static bool Dark;
    public static int Skin;   // 0 Windows 11, 1 Retro, 2 Terminal
    public static void Init(int pref, int skin)   // pref: 0 follow Windows, 1 light, 2 dark
    {
        Skin = skin < 0 || skin > 2 ? 0 : skin;
        if (pref == 1) Dark = false;
        else if (pref == 2) Dark = true;
        else
        {
            Dark = false;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    if (k != null) { object v = k.GetValue("AppsUseLightTheme"); if (v is int && (int)v == 0) Dark = true; }
            }
            catch { }
        }
        ResetFonts();
    }
    public static void ResetFonts() { fonts.Clear(); fam = null; }
    static string Pick(string[] names)
    {
        foreach (string n in names) { try { using (var f = new FontFamily(n)) return n; } catch { } }
        return "Tahoma";
    }
    static string[] SkinFonts()
    {
        if (Skin == 2) return new string[] { "Cascadia Mono", "Consolas", "Courier New" };
        if (Skin == 1) return new string[] { "Tahoma", "Segoe UI", "Arial" };
        return new string[] { "Segoe UI Variable Text", "Segoe UI", "Tahoma" };
    }
    public static string Family
    {
        get
        {
            if (fam == null) { bool latin = L.Code == "en-US" || L.Code == "es-ES"; fam = Pick(latin ? SkinFonts() : L.Fonts(L.Code)); }
            return fam;
        }
    }
    public static string FamilyFor(string code) { return Pick(L.Fonts(code)); }
    public static Font FF(string family, float pt, bool bold) { return new Font(family, pt, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point); }

    static Color Pal(Color w11l, Color w11d, Color rl, Color rd, Color term)
    {
        if (Skin == 1) return Dark ? rd : rl;
        if (Skin == 2) return term;
        return Dark ? w11d : w11l;
    }
    public static Color Accent { get { return Pal(C(0, 103, 192), C(96, 205, 255), C(0, 0, 168), C(255, 64, 160), C(0, 255, 170)); } }
    public static Color AccentDark { get { return Pal(C(0, 80, 150), C(70, 170, 220), C(0, 0, 120), C(200, 40, 130), C(0, 200, 130)); } }
    public static Color OnAccent { get { return Skin == 2 || (Dark && Skin != 1) ? Color.Black : (Skin == 1 && Dark ? Color.Black : Color.White); } }
    public static Color Ink { get { return Pal(C(26, 26, 26), C(255, 255, 255), Color.Black, C(255, 236, 170), C(110, 255, 170)); } }
    public static Color Muted { get { return Pal(C(96, 96, 96), C(170, 170, 170), C(72, 72, 72), C(186, 166, 226), C(50, 170, 110)); } }
    public static Color Line { get { return Pal(C(222, 222, 222), C(66, 66, 66), C(128, 128, 128), C(130, 104, 180), C(24, 96, 66)); } }
    public static Color Bg { get { return Pal(C(243, 243, 243), C(32, 32, 32), C(0, 128, 128), C(30, 14, 58), C(0, 6, 3)); } }
    public static Color Card { get { return Pal(Color.White, C(45, 45, 45), C(192, 192, 192), C(66, 48, 110), C(4, 18, 11)); } }
    public static Color Row { get { return Pal(C(248, 248, 248), C(56, 56, 56), C(224, 224, 224), C(50, 36, 90), C(8, 30, 19)); } }
    public static Color Ok { get { return Skin == 2 ? C(0, 255, 120) : C(34, 181, 115); } }
    public static Color Warn { get { return Skin == 2 ? C(255, 200, 0) : C(245, 166, 35); } }
    public static Color Bad { get { return Skin == 2 ? C(255, 70, 70) : C(235, 87, 87); } }
    // text colours for the full-screen check screen (on cards / directly on the background)
    public static Color ObFg { get { return Skin == 0 ? Color.White : Ink; } }
    public static Color ObBgFg { get { return Skin == 2 ? Ink : Color.White; } }

    public static float Rad(float r) { return Skin == 0 ? Math.Min(r, 12f) : 0f; }
    public static GraphicsPath RoundRaw(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    public static void FillPill(Graphics g, Color c, RectangleF r)
    {
        using (var p = RoundRaw(r, r.Height / 2f)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
    }

    // ---- retro bevels
    public static void Bevel(Graphics g, RectangleF rf, bool raised)
    {
        SmoothingMode sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None;
        int x = (int)rf.X, y = (int)rf.Y, x2 = (int)rf.Right - 1, y2 = (int)rf.Bottom - 1;
        Color hi = Dark ? C(160, 140, 210) : Color.White, lo = Dark ? C(20, 10, 40) : C(64, 64, 64), mid = Dark ? C(100, 80, 150) : C(128, 128, 128);
        Color a = raised ? hi : lo, b = raised ? lo : hi, m = raised ? mid : mid;
        using (var pa = new Pen(a)) using (var pb = new Pen(b)) using (var pm = new Pen(m))
        {
            g.DrawLine(pa, x, y, x2 - 1, y); g.DrawLine(pa, x, y, x, y2 - 1);
            g.DrawLine(pb, x, y2, x2, y2); g.DrawLine(pb, x2, y, x2, y2);
            g.DrawLine(pm, x + 1, y2 - 1, x2 - 1, y2 - 1); g.DrawLine(pm, x2 - 1, y + 1, x2 - 1, y2 - 1);
        }
        g.SmoothingMode = sm;
    }
    // decoration drawn on opaque filled panels
    public static void Decor(Graphics g, RectangleF r, Color c)
    {
        if (Skin == 1) Bevel(g, r, true);
        else if (Skin == 2) using (var p = new Pen(A(Accent, 90), 1f)) g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        else if (c.ToArgb() == Card.ToArgb()) using (var p = new Pen(Line, 1f)) DrawRound(g, p, new RectangleF(r.X + .5f, r.Y + .5f, r.Width - 1, r.Height - 1), 8);
    }

    // ---- panels on the full-screen check screen
    public static void Glass(Graphics g, RectangleF r, float rad, int a)
    {
        if (Skin == 1) { FillRound(g, Card, r, 0); return; }
        if (Skin == 2)
        {
            using (var b = new SolidBrush(A(C(0, 12, 6), 215))) g.FillRectangle(b, r);
            using (var p = new Pen(A(Accent, 140), 1f)) g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
            using (var p = new Pen(Accent, 2f))
            {
                float L2 = 12;
                g.DrawLine(p, r.X, r.Y, r.X + L2, r.Y); g.DrawLine(p, r.X, r.Y, r.X, r.Y + L2);
                g.DrawLine(p, r.Right - 1, r.Bottom - 1, r.Right - 1 - L2, r.Bottom - 1); g.DrawLine(p, r.Right - 1, r.Bottom - 1, r.Right - 1, r.Bottom - 1 - L2);
            }
            return;
        }
        FillRound(g, A(Color.White, a), r, rad);
        using (var p = new Pen(A(Color.White, Math.Min(255, a + 45)), 1f)) DrawRound(g, p, r, rad);
    }

    // title drawn ABOVE a panel (retro: title bar, terminal: caption on the border, Win11: nothing)
    public static void PanelTitle(Graphics g, RectangleF r, string title)
    {
        if (Skin == 1)
        {
            RectangleF tb = new RectangleF(r.X, r.Y - 28, r.Width, 28);
            FillRound(g, Card, tb, 0);
            RectangleF ib = new RectangleF(tb.X + 3, tb.Y + 3, tb.Width - 6, tb.Height - 6);
            using (var br = new LinearGradientBrush(ib, Dark ? C(120, 40, 160) : C(0, 0, 128), Dark ? C(255, 64, 160) : C(16, 132, 208), 0f)) g.FillRectangle(br, ib);
            Txt(g, title, F(9.5f, true), Color.White, new RectangleF(ib.X + 8, ib.Y, ib.Width - 60, ib.Height), StringAlignment.Near, StringAlignment.Center, false);
            for (int i = 0; i < 3; i++) { RectangleF bx = new RectangleF(ib.Right - 18 * (3 - i) - 2, ib.Y + 2, 16, ib.Height - 4); FillRound(g, Card, bx, 0); }
        }
        else if (Skin == 2)
        {
            string t = " " + title.ToUpperInvariant() + " ";
            SizeF sz = g.MeasureString(t, F(9, true));
            using (var b = new SolidBrush(Bg)) g.FillRectangle(b, r.X + 16, r.Y - 9, sz.Width, 18);
            Txt(g, t, F(9, true), Accent, new RectangleF(r.X + 16, r.Y - 9, sz.Width + 4, 18), StringAlignment.Near, StringAlignment.Center, false);
        }
    }

    // push button used everywhere (Btn control and the custom-painted check screen)
    public static void Push(Graphics g, Rectangle r, string text, Font f, bool primary, bool hover, bool down, bool enabled, bool onDark)
    {
        RectangleF rf = new RectangleF(r.X, r.Y, r.Width - 1, r.Height - 1);
        if (Skin == 1)
        {
            using (var b = new SolidBrush(Card)) g.FillRectangle(b, rf);
            Bevel(g, rf, !down);
            if (primary && enabled) { SmoothingMode sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None; g.DrawRectangle(Pens.Black, r.X - 1, r.Y - 1, r.Width + 1, r.Height + 1); g.SmoothingMode = sm; }
            Txt(g, text, f, enabled ? Ink : Muted, new RectangleF(r.X + (down ? 1 : 0), r.Y + (down ? 1 : 0), r.Width, r.Height), StringAlignment.Center, StringAlignment.Center, false);
        }
        else if (Skin == 2)
        {
            Color ac = enabled ? (primary ? Accent : Ink) : Line;
            if (hover || down) using (var b = new SolidBrush(A(ac, 55))) g.FillRectangle(b, rf);
            else if (primary && enabled) using (var b = new SolidBrush(A(ac, 24))) g.FillRectangle(b, rf);
            using (var p = new Pen(A(ac, hover ? 255 : 170), 1f)) g.DrawRectangle(p, rf.X, rf.Y, rf.Width, rf.Height);
            Txt(g, "[ " + text + " ]", f, ac, new RectangleF(r.X, r.Y, r.Width, r.Height), StringAlignment.Center, StringAlignment.Center, false);
        }
        else if (onDark)
        {
            FillRound(g, primary ? A(Color.White, hover ? 255 : 238) : A(Color.White, hover ? 70 : 40), rf, 8);
            Txt(g, text, f, primary ? C(10, 70, 180) : Color.White, new RectangleF(r.X, r.Y, r.Width, r.Height), StringAlignment.Center, StringAlignment.Center, false);
        }
        else if (primary)
        {
            Color c = !enabled ? Line : down ? AccentDark : hover ? Pal(C(26, 120, 205), C(130, 215, 255), Accent, Accent, Accent) : Accent;
            FillRound(g, c, rf, 8);
            Txt(g, text, f, enabled ? OnAccent : Muted, new RectangleF(r.X, r.Y, r.Width, r.Height), StringAlignment.Center, StringAlignment.Center, false);
        }
        else
        {
            FillRound(g, hover ? Pal(C(238, 238, 238), C(66, 66, 66), Row, Row, Row) : Row, rf, 8);
            using (var p = new Pen(Line, 1f)) DrawRound(g, p, rf, 8);
            Txt(g, text, f, enabled ? Ink : Muted, new RectangleF(r.X, r.Y, r.Width, r.Height), StringAlignment.Center, StringAlignment.Center, false);
        }
    }

    public static void DrawSwitch(Graphics g, Rectangle r, bool on)
    {
        if (Skin == 1)
        {
            Rectangle cb = new Rectangle(r.X + r.Width - 26, r.Y + (r.Height - 22) / 2, 22, 22);
            using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, cb);
            Bevel(g, cb, false);
            if (on) using (var p = new Pen(Color.Black, 2.4f)) { g.DrawLine(p, cb.X + 5, cb.Y + 11, cb.X + 9, cb.Y + 16); g.DrawLine(p, cb.X + 9, cb.Y + 16, cb.X + 17, cb.Y + 6); }
        }
        else if (Skin == 2) Txt(g, on ? "[ ON ]" : "[ OFF ]", F(10, true), on ? Accent : Muted, new RectangleF(r.X - 20, r.Y, r.Width + 20, r.Height), StringAlignment.Far, StringAlignment.Center, false);
        else
        {
            FillPill(g, on ? Accent : Pal(C(140, 140, 140), C(120, 120, 120), Line, Line, Line), r);
            using (var b = new SolidBrush(on ? OnAccent : Pal(Color.White, Color.White, Color.White, Color.White, Color.White))) g.FillEllipse(b, on ? r.Right - r.Height + 4 : r.X + 4, r.Y + 4, r.Height - 8, r.Height - 8);
        }
    }

    public static void Ring(Graphics g, RectangleF r, float pct, Color track, Color fill, float th)
    {
        using (var p = new Pen(track, th)) g.DrawEllipse(p, r);
        if (pct > 0.001f)
            using (var p = new Pen(fill, th)) { p.StartCap = LineCap.Flat; p.EndCap = LineCap.Flat; if (Skin == 0) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; } g.DrawArc(p, r, -90, Math.Min(359.5f, 360f * pct)); }
    }

    static void Blob(Graphics g, float cx, float cy, float rad, Color c, int a)
    {
        using (var gp = new GraphicsPath())
        {
            gp.AddEllipse(cx - rad, cy - rad, rad * 2, rad * 2);
            using (var pb = new PathGradientBrush(gp)) { pb.CenterColor = A(c, a); pb.SurroundColors = new Color[] { A(c, 0) }; g.FillPath(pb, gp); }
        }
    }

    // full-screen background of the check screen / setup side panel
    public static void Bloom(Graphics g, Rectangle r)
    {
        Rectangle rr = new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
        if (Skin == 1)
        {
            using (var b = new SolidBrush(Bg)) g.FillRectangle(b, rr);
            using (var h = new HatchBrush(HatchStyle.Percent10, A(Color.White, 40), Color.Transparent)) g.FillRectangle(h, rr);
            return;
        }
        if (Skin == 2)
        {
            using (var b = new SolidBrush(Bg)) g.FillRectangle(b, rr);
            RectangleF cb = g.ClipBounds;
            using (var p = new Pen(A(Accent, 16), 1f))
            {
                for (int x = (int)(cb.X / 48) * 48; x < cb.Right; x += 48) g.DrawLine(p, x, cb.Y, x, cb.Bottom);
                for (int y = (int)(cb.Y / 48) * 48; y < cb.Bottom; y += 48) g.DrawLine(p, cb.X, y, cb.Right, y);
            }
            using (var p = new Pen(A(Color.Black, 70), 1f))
                for (int y = (int)(cb.Y / 3) * 3; y < cb.Bottom; y += 3) g.DrawLine(p, cb.X, y, cb.Right, y);
            return;
        }
        using (var b = new LinearGradientBrush(rr, C(2, 14, 52), C(8, 78, 190), 42f)) g.FillRectangle(b, rr);
        float W = r.Width, H = r.Height;
        Blob(g, r.X + W * .12f, r.Y + H * .18f, W * .55f, C(70, 160, 255), 80);
        Blob(g, r.X + W * .90f, r.Y + H * .12f, W * .42f, C(150, 100, 255), 70);
        Blob(g, r.X + W * .70f, r.Y + H * 1.0f, W * .60f, C(0, 190, 255), 70);
        Blob(g, r.X + W * .05f, r.Y + H * 1.0f, W * .40f, C(90, 120, 255), 60);
        for (int i = 0; i < 4; i++)
            using (var p = new Pen(A(Color.White, 14 + i * 4), 2f + i))
                g.DrawBezier(p, r.X - 20, r.Y + H * (.78f + i * .05f), r.X + W * .3f, r.Y + H * (.45f + i * .06f), r.X + W * .65f, r.Y + H * (1.05f - i * .05f), r.X + W + 20, r.Y + H * (.55f + i * .06f));
    }

    public static void StateIconD(Graphics g, int st, RectangleF r, int spin)   // for the check screen
    {
        Color fg = ObFg;
        if (st == 0) { using (var p = new Pen(A(fg, 100), 2f)) g.DrawEllipse(p, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2); }
        else if (st == 1)
        {
            using (var p = new Pen(A(fg, 50), 3f)) g.DrawEllipse(p, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
            using (var p = new Pen(fg, 3f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4, spin, 100); }
        }
        else StateIcon(g, st, r, spin);
    }

    public static void Icon(Graphics g, string kind, RectangleF r, Color col, float sw)
    {
        float x = r.X, y = r.Y, w = r.Width, h = r.Height;
        Func<float, float, PointF> P = delegate(float fx, float fy) { return new PointF(x + fx * w, y + fy * h); };
        Func<float, float, float, float, RectangleF> R = delegate(float fx, float fy, float fw, float fh) { return new RectangleF(x + fx * w, y + fy * h, fw * w, fh * h); };
        using (var p = new Pen(col, sw))
        {
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
            switch (kind)
            {
                case "monitor": DrawRound(g, p, R(.12f, .18f, .76f, .5f), w * .07f); g.DrawLine(p, P(.5f, .68f), P(.5f, .82f)); g.DrawLine(p, P(.32f, .84f), P(.68f, .84f)); break;
                case "laptop": DrawRound(g, p, R(.2f, .2f, .6f, .46f), w * .05f); g.DrawLine(p, P(.08f, .78f), P(.92f, .78f)); break;
                case "speaker":
                    g.DrawPolygon(p, new PointF[] { P(.14f, .4f), P(.3f, .4f), P(.52f, .2f), P(.52f, .8f), P(.3f, .6f), P(.14f, .6f) });
                    g.DrawArc(p, R(.42f, .32f, .3f, .36f), -50, 100); g.DrawArc(p, R(.36f, .2f, .5f, .6f), -50, 100); break;
                case "wifi":
                    {
                        PointF c = P(.5f, .78f);
                        for (int i = 1; i <= 3; i++) { float rr = w * .17f * i; g.DrawArc(p, c.X - rr, c.Y - rr, rr * 2, rr * 2, 225, 90); }
                        using (var b = new SolidBrush(col)) g.FillEllipse(b, c.X - sw, c.Y - sw, sw * 2, sw * 2);
                        break;
                    }
                case "chip":
                    DrawRound(g, p, R(.27f, .27f, .46f, .46f), w * .06f);
                    for (int i = 0; i < 3; i++)
                    {
                        float t = .38f + i * .12f;
                        g.DrawLine(p, P(t, .14f), P(t, .27f)); g.DrawLine(p, P(t, .73f), P(t, .86f));
                        g.DrawLine(p, P(.14f, t), P(.27f, t)); g.DrawLine(p, P(.73f, t), P(.86f, t));
                    }
                    break;
                case "power": g.DrawArc(p, R(.2f, .22f, .6f, .6f), -55, 290); g.DrawLine(p, P(.5f, .12f), P(.5f, .5f)); break;
                case "apps":
                    DrawRound(g, p, R(.2f, .2f, .26f, .26f), w * .05f); DrawRound(g, p, R(.54f, .2f, .26f, .26f), w * .05f);
                    DrawRound(g, p, R(.2f, .54f, .26f, .26f), w * .05f); DrawRound(g, p, R(.54f, .54f, .26f, .26f), w * .05f); break;
                case "gear":
                    g.DrawEllipse(p, R(.32f, .32f, .36f, .36f));
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(p, (float)(x + w * (.5 + .27 * Math.Cos(a))), (float)(y + h * (.5 + .27 * Math.Sin(a))), (float)(x + w * (.5 + .4 * Math.Cos(a))), (float)(y + h * (.5 + .4 * Math.Sin(a))));
                    }
                    break;
                case "warn": g.DrawPolygon(p, new PointF[] { P(.5f, .14f), P(.9f, .82f), P(.1f, .82f) }); g.DrawLine(p, P(.5f, .4f), P(.5f, .6f)); g.DrawLine(p, P(.5f, .71f), P(.5f, .715f)); break;
                case "doc":
                    DrawRound(g, p, R(.24f, .12f, .52f, .76f), w * .06f);
                    g.DrawLine(p, P(.36f, .36f), P(.64f, .36f)); g.DrawLine(p, P(.36f, .52f), P(.64f, .52f)); g.DrawLine(p, P(.36f, .68f), P(.54f, .68f)); break;
                case "home":
                    g.DrawLines(p, new PointF[] { P(.14f, .5f), P(.5f, .17f), P(.86f, .5f) });
                    g.DrawLines(p, new PointF[] { P(.25f, .44f), P(.25f, .84f), P(.75f, .84f), P(.75f, .44f) }); break;
                case "globe": g.DrawEllipse(p, R(.14f, .14f, .72f, .72f)); g.DrawEllipse(p, R(.34f, .14f, .32f, .72f)); g.DrawLine(p, P(.14f, .5f), P(.86f, .5f)); break;
                case "disk": DrawRound(g, p, R(.14f, .3f, .72f, .4f), w * .08f); g.DrawEllipse(p, R(.66f, .46f, .06f, .08f)); break;
                case "ram":
                    DrawRound(g, p, R(.12f, .28f, .76f, .38f), w * .05f);
                    for (int i = 0; i < 4; i++) g.DrawLine(p, P(.24f + i * .17f, .66f), P(.24f + i * .17f, .8f)); break;
                case "check": g.DrawLines(p, new PointF[] { P(.22f, .54f), P(.42f, .72f), P(.78f, .3f) }); break;
                case "bulb": g.DrawEllipse(p, R(.25f, .12f, .5f, .5f)); g.DrawLine(p, P(.38f, .74f), P(.62f, .74f)); g.DrawLine(p, P(.42f, .86f), P(.58f, .86f)); break;
            }
        }
    }
}

// Icon rail button for the main window
class NavBtn : Control
{
    public string Kind = "home";
    public bool Active;
    bool hover;
    public NavBtn()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand; Size = new Size(76, 72);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.Clear(T.Card); T.Q(g);
        RectangleF rc = new RectangleF(4, 2, Width - 8, Height - 4);
        if (T.Skin == 1) { if (Active) T.Bevel(g, rc, false); else if (hover) T.Bevel(g, rc, true); }
        else if (Active) T.FillRound(g, T.A(T.Accent, 34), rc, 12);
        else if (hover) T.FillRound(g, T.A(T.Accent, 14), rc, 12);
        Color c = Active && T.Skin != 1 ? T.Accent : (T.Skin == 1 ? T.Ink : T.Muted);
        T.Icon(g, Kind, new RectangleF(Width / 2f - 14, 12, 28, 28), c, 2f);
        T.Txt(g, Text, T.F(8, true), c, new RectangleF(0, 44, Width, 24), StringAlignment.Center, StringAlignment.Near, false);
    }
}

class Btn : Control
{
    public int Style; // 0 primary, 1 outline, 2 link, 3 text button
    public Color Back = Color.Empty;
    bool hover, down;
    public Btn()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand; Font = T.F(11, true); Size = new Size(170, 46);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.Clear(Back.IsEmpty ? T.Card : Back); T.Q(g);
        if (Style == 2 || Style == 3)
        {
            Color c = hover ? T.Accent : T.Muted;
            T.Txt(g, Text, Font, c, new RectangleF(0, 0, Width, Height), StringAlignment.Center, StringAlignment.Center, false);
            return;
        }
        T.Push(g, new Rectangle(1, 1, Width - 2, Height - 2), Text, Font, Style == 0, hover, down, Enabled, false);
    }
}

class ToggleRow : Control
{
    public string Title = "", Sub = "";
    public Color Back = Color.Empty;
    bool val;
    public event EventHandler Changed;
    public bool Value { get { return val; } set { if (val != value) { val = value; Invalidate(); } } }
    public ToggleRow()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand; Size = new Size(400, 56);
    }
    protected override void OnClick(EventArgs e)
    {
        val = !val; Invalidate();
        if (Changed != null) Changed(this, EventArgs.Empty);
        base.OnClick(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.Clear(Back.IsEmpty ? T.Card : Back); T.Q(g);
        RectangleF rc = new RectangleF(1, 1, Width - 3, Height - 3);
        if (T.Skin == 1) { using (var b = new SolidBrush(T.Row)) g.FillRectangle(b, rc); T.Bevel(g, rc, false); }
        else if (T.Skin == 2) { using (var b = new SolidBrush(T.Row)) g.FillRectangle(b, rc); using (var p = new Pen(T.A(T.Accent, 70), 1f)) g.DrawRectangle(p, rc.X, rc.Y, rc.Width, rc.Height); }
        else T.FillRound(g, T.Row, rc, 10);
        float tw = Width - 120;
        if (Sub.Length > 0)
        {
            T.TxtL(g, Title, T.F(10.5f, true), T.Ink, new RectangleF(18, 6, tw, Height / 2f - 4));
            T.TxtL(g, Sub, T.F(8.5f), T.Muted, new RectangleF(18, Height / 2f - 3, tw, Height / 2f - 2));
        }
        else T.TxtL(g, Title, T.F(10.5f, true), T.Ink, new RectangleF(18, 0, tw, Height));
        T.DrawSwitch(g, new Rectangle(Width - 74, (Height - 28) / 2, 52, 28), val);
    }
}
