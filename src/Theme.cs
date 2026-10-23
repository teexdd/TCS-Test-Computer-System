using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

// Light-weight custom UI: everything is vector-drawn with GDI+ (no image files, no extra libraries) to keep RAM low.
static partial class T
{

    static Dictionary<string, Font> fonts = new Dictionary<string, Font>();
    static string fam;
    public static Font F(float pt, bool bold)
    {
        string k = Family + pt.ToString() + (bold ? "b" : "r");
        Font f;
        if (!fonts.TryGetValue(k, out f)) { f = new Font(Family, pt, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point); fonts[k] = f; }
        return f;
    }
    public static Font F(float pt) { return F(pt, false); }
    public static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
    public static Color A(Color c, int a) { return Color.FromArgb(a, c); }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        rad = Rad(rad);
        float d = rad * 2;
        if (d > r.Width) d = r.Width;
        if (d > r.Height) d = r.Height;
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
    public static void FillRound(Graphics g, Brush b, RectangleF r, float rad) { using (var p = Round(r, rad)) g.FillPath(b, p); }
    public static void FillRound(Graphics g, Color c, RectangleF r, float rad)
    {
        using (var b = new SolidBrush(c)) FillRound(g, b, r, rad);
        if (c.A == 255 && r.Width >= 30 && r.Height >= 20) Decor(g, r, c);
    }
    public static void DrawRound(Graphics g, Pen pen, RectangleF r, float rad) { using (var p = Round(r, rad)) g.DrawPath(pen, p); }
    public static void Q(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }
    public static void Txt(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h, StringAlignment v, bool wrap)
    {
        using (var sf = new StringFormat())
        {
            sf.Alignment = h; sf.LineAlignment = v; sf.Trimming = StringTrimming.EllipsisCharacter;
            if (!wrap) sf.FormatFlags = StringFormatFlags.NoWrap;
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
        }
    }
    public static void Txt(Graphics g, string s, Font f, Color c, RectangleF r) { Txt(g, s, f, c, r, StringAlignment.Near, StringAlignment.Near, true); }
    public static void TxtL(Graphics g, string s, Font f, Color c, RectangleF r) { Txt(g, s, f, c, r, StringAlignment.Near, StringAlignment.Center, false); }

    // ---- status icon: 0 pending, 1 running, 2 ok, 3 warn, 4 fail, 5 skipped
    public static void StateIcon(Graphics g, int st, RectangleF r, int spin)
    {
        switch (st)
        {
            case 0: using (var p = new Pen(Line, 2f)) g.DrawEllipse(p, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2); break;
            case 1:
                using (var p = new Pen(A(Accent, 40), 3f)) g.DrawEllipse(p, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
                using (var p = new Pen(Accent, 3f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawArc(p, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4, spin, 100); }
                break;
            case 2:
            case 3:
            case 4:
                using (var b = new SolidBrush(st == 2 ? Ok : st == 3 ? Warn : Bad)) g.FillEllipse(b, r);
                using (var p = new Pen(Color.White, 2.4f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                    float x = r.X, y = r.Y, w = r.Width, h = r.Height;
                    if (st == 2) g.DrawLines(p, new PointF[] { new PointF(x + w * .27f, y + h * .53f), new PointF(x + w * .44f, y + h * .70f), new PointF(x + w * .75f, y + h * .32f) });
                    else if (st == 3) { g.DrawLine(p, x + w * .5f, y + h * .25f, x + w * .5f, y + h * .58f); g.DrawLine(p, x + w * .5f, y + h * .74f, x + w * .5f, y + h * .75f); }
                    else { g.DrawLine(p, x + w * .3f, y + h * .3f, x + w * .7f, y + h * .7f); g.DrawLine(p, x + w * .7f, y + h * .3f, x + w * .3f, y + h * .7f); }
                }
                break;
            default:
                using (var p = new Pen(Muted, 2f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawLine(p, r.X + r.Width * .3f, r.Y + r.Height / 2, r.X + r.Width * .7f, r.Y + r.Height / 2); }
                break;
        }
    }

    // ---------------------------------------------------------------- scenes
    static void Pine(Graphics g, float cx, float by, float h, Color c)
    {
        using (var b = new SolidBrush(c))
        {
            g.FillRectangle(b, cx - h * 0.03f, by - h * 0.12f, h * 0.06f, h * 0.12f);
            for (int i = 0; i < 3; i++)
            {
                float apex = by - h * (1 - i * 0.22f), bas = apex + h * 0.42f, hw = h * (0.17f + i * 0.07f);
                g.FillPolygon(b, new PointF[] { new PointF(cx, apex), new PointF(cx - hw, bas), new PointF(cx + hw, bas) });
            }
        }
    }

    static void Poly(Graphics g, Color c, Rectangle r, float[] xy)
    {
        var pts = new PointF[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(r.X + xy[i * 2] * r.Width, r.Y + xy[i * 2 + 1] * r.Height);
        using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
    }

    // pal 0 = day, 1 = sunset
    public static void Nature(Graphics g, Rectangle r, int pal)
    {
        GraphicsState st = g.Save();
        using (var clip = Round(r, 26)) g.SetClip(clip, CombineMode.Replace);
        Color sky1, sky2, sunC, m1, m2, m3, l1, l2, fg;
        if (pal == 0) { sky1 = C(96, 170, 255); sky2 = C(232, 245, 255); sunC = C(255, 246, 210); m1 = C(160, 190, 232); m2 = C(104, 146, 200); m3 = C(56, 102, 156); l1 = C(130, 184, 234); l2 = C(48, 106, 162); fg = C(22, 60, 70); }
        else { sky1 = C(255, 122, 110); sky2 = C(255, 224, 172); sunC = C(255, 247, 218); m1 = C(220, 140, 166); m2 = C(154, 100, 150); m3 = C(86, 66, 122); l1 = C(255, 178, 142); l2 = C(112, 76, 132); fg = C(38, 30, 62); }
        Rectangle rr = new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
        using (var b = new LinearGradientBrush(rr, sky1, sky2, 90f)) g.FillRectangle(b, rr);
        float sx = r.X + r.Width * 0.68f, sy = r.Y + r.Height * 0.31f, sr = r.Width * 0.07f;
        using (var gp = new GraphicsPath())
        {
            gp.AddEllipse(sx - sr * 3.2f, sy - sr * 3.2f, sr * 6.4f, sr * 6.4f);
            using (var pb = new PathGradientBrush(gp)) { pb.CenterColor = A(sunC, 220); pb.SurroundColors = new Color[] { A(sunC, 0) }; g.FillPath(pb, gp); }
        }
        using (var b = new SolidBrush(sunC)) g.FillEllipse(b, sx - sr, sy - sr, sr * 2, sr * 2);
        using (var b = new SolidBrush(A(Color.White, 150)))
        {
            g.FillEllipse(b, r.X + r.Width * .12f, r.Y + r.Height * .14f, r.Width * .22f, r.Height * .035f);
            g.FillEllipse(b, r.X + r.Width * .20f, r.Y + r.Height * .165f, r.Width * .18f, r.Height * .03f);
            g.FillEllipse(b, r.X + r.Width * .44f, r.Y + r.Height * .26f, r.Width * .2f, r.Height * .03f);
        }
        Poly(g, m1, r, new float[] { 0, .60f, .12f, .46f, .24f, .56f, .38f, .40f, .52f, .57f, .66f, .44f, .82f, .56f, 1, .47f, 1, .62f, 0, .62f });
        Poly(g, m2, r, new float[] { 0, .64f, .18f, .51f, .34f, .62f, .55f, .48f, .75f, .62f, 1, .53f, 1, .66f, 0, .66f });
        Rectangle lake = new Rectangle(r.X - 1, (int)(r.Y + r.Height * .64f), r.Width + 2, (int)(r.Height * .24f));
        using (var b = new LinearGradientBrush(lake, l1, l2, 90f)) g.FillRectangle(b, lake);
        for (int i = 0; i < 7; i++)
        {
            float w = r.Width * (0.20f - i * 0.025f);
            float y = lake.Y + lake.Height * 0.08f + i * lake.Height * 0.11f;
            using (var p = new Pen(A(sunC, 150 - i * 15), 2.5f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawLine(p, sx - w / 2, y, sx + w / 2, y); }
        }
        Poly(g, m3, r, new float[] { 0, .70f, .22f, .64f, .45f, .72f, .7f, .66f, 1, .72f, 1, .80f, 0, .80f });
        Poly(g, fg, r, new float[] { 0, .84f, .30f, .88f, .62f, .93f, 1, .89f, 1, 1, 0, 1 });
        Pine(g, r.X + r.Width * .10f, r.Y + r.Height * .90f, r.Height * .20f, fg);
        Pine(g, r.X + r.Width * .20f, r.Y + r.Height * .92f, r.Height * .14f, fg);
        Pine(g, r.X + r.Width * .90f, r.Y + r.Height * .93f, r.Height * .22f, fg);
        Pine(g, r.X + r.Width * .80f, r.Y + r.Height * .94f, r.Height * .15f, fg);
        g.Restore(st);
    }

    static void SceneBg(Graphics g, Rectangle r, GraphicsPath clip)
    {
        g.SetClip(clip, CombineMode.Replace);
        Rectangle rr = new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
        using (var b = new LinearGradientBrush(rr, C(196, 214, 255), C(244, 247, 255), 90f)) g.FillRectangle(b, rr);
        using (var b = new SolidBrush(A(Color.White, 120)))
        {
            g.FillEllipse(b, r.X - r.Width * .2f, r.Y + r.Height * .55f, r.Width * .7f, r.Width * .7f);
            g.FillEllipse(b, r.X + r.Width * .55f, r.Y - r.Width * .15f, r.Width * .6f, r.Width * .6f);
        }
    }

    public static void Desktop(Graphics g, Rectangle r)
    {
        GraphicsState st = g.Save();
        using (var clip = Round(r, 26)) SceneBg(g, r, clip);
        float W = r.Width, H = r.Height, X = r.X, Y = r.Y;
        FillRound(g, C(226, 208, 188), new RectangleF(X - 4, Y + H * .76f, W + 8, H * .3f), 0);
        // tower
        FillRound(g, C(44, 50, 78), new RectangleF(X + W * .07f, Y + H * .40f, W * .17f, H * .38f), 10);
        FillRound(g, C(70, 78, 112), new RectangleF(X + W * .09f, Y + H * .43f, W * .13f, H * .03f), 3);
        FillRound(g, C(70, 78, 112), new RectangleF(X + W * .09f, Y + H * .48f, W * .13f, H * .03f), 3);
        using (var b = new SolidBrush(Ok)) g.FillEllipse(b, X + W * .145f, Y + H * .70f, W * .02f, W * .02f);
        // monitor
        float mw = W * .60f, mh = H * .34f, mx = X + W * .34f, my = Y + H * .22f;
        FillRound(g, C(32, 36, 58), new RectangleF(mx, my, mw, mh), 14);
        RectangleF sc = new RectangleF(mx + 9, my + 9, mw - 18, mh - 18);
        using (var b = new LinearGradientBrush(sc, Accent, C(150, 90, 255), 40f)) FillRound(g, b, sc, 8);
        FillRound(g, A(Color.White, 215), new RectangleF(sc.X + 16, sc.Y + 16, sc.Width * .42f, sc.Height * .42f), 7);
        FillRound(g, A(Color.White, 120), new RectangleF(sc.X + sc.Width * .52f, sc.Y + 24, sc.Width * .36f, sc.Height * .28f), 7);
        FillRound(g, A(Color.White, 90), new RectangleF(sc.X + 16, sc.Y + sc.Height * .62f, sc.Width * .7f, sc.Height * .10f), 5);
        FillRound(g, C(60, 66, 92), new RectangleF(mx + mw / 2 - 9, my + mh, 18, H * .09f), 3);
        FillRound(g, C(60, 66, 92), new RectangleF(mx + mw / 2 - mw * .18f, my + mh + H * .075f, mw * .36f, H * .028f), 8);
        // keyboard + mouse
        RectangleF kb = new RectangleF(mx + mw * .08f, Y + H * .81f, mw * .64f, H * .075f);
        FillRound(g, Color.White, kb, 8);
        using (var p = new Pen(Line, 1.5f))
        {
            for (int i = 1; i < 12; i++) g.DrawLine(p, kb.X + kb.Width * i / 12f, kb.Y + 6, kb.X + kb.Width * i / 12f, kb.Bottom - 6);
            g.DrawLine(p, kb.X + 8, kb.Y + kb.Height / 2, kb.Right - 8, kb.Y + kb.Height / 2);
        }
        FillRound(g, Color.White, new RectangleF(mx + mw * .80f, Y + H * .805f, W * .05f, H * .085f), 12);
        // plant
        FillRound(g, C(214, 120, 88), new RectangleF(X + W * .88f, Y + H * .66f, W * .07f, H * .10f), 5);
        using (var b = new SolidBrush(C(64, 168, 110)))
        {
            g.FillEllipse(b, X + W * .85f, Y + H * .52f, W * .07f, H * .15f);
            g.FillEllipse(b, X + W * .91f, Y + H * .55f, W * .07f, H * .13f);
        }
        g.Restore(st);
    }

    public static void Laptop(Graphics g, Rectangle r)
    {
        GraphicsState st = g.Save();
        using (var clip = Round(r, 26)) SceneBg(g, r, clip);
        float W = r.Width, H = r.Height, X = r.X, Y = r.Y;
        FillRound(g, C(226, 208, 188), new RectangleF(X - 4, Y + H * .70f, W + 8, H * .4f), 0);
        float lw = W * .66f, lh = H * .36f, lx = X + (W - lw) / 2, ly = Y + H * .26f;
        FillRound(g, C(32, 36, 58), new RectangleF(lx, ly, lw, lh), 12);
        RectangleF sc = new RectangleF(lx + 8, ly + 8, lw - 16, lh - 16);
        using (var b = new LinearGradientBrush(sc, C(255, 140, 120), Accent, 40f)) FillRound(g, b, sc, 6);
        FillRound(g, A(Color.White, 215), new RectangleF(sc.X + 14, sc.Y + 14, sc.Width * .40f, sc.Height * .42f), 6);
        FillRound(g, A(Color.White, 120), new RectangleF(sc.X + sc.Width * .52f, sc.Y + 20, sc.Width * .36f, sc.Height * .26f), 6);
        using (var b = new SolidBrush(C(190, 198, 222)))
        {
            PointF[] bas = { new PointF(lx - W * .05f, ly + lh), new PointF(lx + lw + W * .05f, ly + lh), new PointF(lx + lw + W * .09f, ly + lh + H * .06f), new PointF(lx - W * .09f, ly + lh + H * .06f) };
            g.FillPolygon(b, bas);
        }
        FillRound(g, C(150, 158, 186), new RectangleF(X + W * .5f - W * .06f, ly + lh, W * .12f, H * .014f), 4);
        FillRound(g, Color.White, new RectangleF(X + W * .76f, Y + H * .64f, W * .12f, H * .12f), 6);
        using (var b = new SolidBrush(C(255, 200, 90))) g.FillEllipse(b, X + W * .80f, Y + H * .56f, W * .05f, W * .05f);
        g.Restore(st);
    }
}

