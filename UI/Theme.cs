using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace GroceryPOS.UI
{
    /// <summary>Design tokens: colours, fonts, DPI scale.</summary>
    public static class Theme
    {
        public static float Scale = 1f;
        public static int S(int v) { return (int)Math.Round(v * Scale); }
        public static float S(float v) { return v * Scale; }

        public static Color Hex(string h)
        {
            try { return ColorTranslator.FromHtml(h); } catch { return Primary; }
        }

        // Surfaces
        public static readonly Color Bg = Color.FromArgb(0xF1, 0xF4, 0xF9);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceAlt = Color.FromArgb(0xF7, 0xF9, 0xFC);
        public static readonly Color Border = Color.FromArgb(0xE3, 0xE8, 0xEF);
        public static readonly Color Track = Color.FromArgb(0xE6, 0xEA, 0xF1);

        // Text
        public static readonly Color Text = Color.FromArgb(0x0F, 0x17, 0x2A);
        public static readonly Color TextMuted = Color.FromArgb(0x64, 0x74, 0x8B);
        public static readonly Color TextFaint = Color.FromArgb(0x94, 0xA3, 0xB8);

        // Brand
        public static readonly Color Primary = Color.FromArgb(0x16, 0xA3, 0x4A);
        public static readonly Color PrimaryDark = Color.FromArgb(0x15, 0x80, 0x3D);
        public static readonly Color PrimarySoft = Color.FromArgb(0xDC, 0xFC, 0xE7);

        public static readonly Color Danger = Color.FromArgb(0xEF, 0x44, 0x44);
        public static readonly Color DangerDark = Color.FromArgb(0xDC, 0x26, 0x26);
        public static readonly Color DangerSoft = Color.FromArgb(0xFE, 0xE2, 0xE2);
        public static readonly Color Warning = Color.FromArgb(0xF5, 0x9E, 0x0B);
        public static readonly Color WarningDark = Color.FromArgb(0xB4, 0x53, 0x09);
        public static readonly Color WarningSoft = Color.FromArgb(0xFE, 0xF3, 0xC7);
        public static readonly Color Info = Color.FromArgb(0x3B, 0x82, 0xF6);
        public static readonly Color InfoSoft = Color.FromArgb(0xDB, 0xEA, 0xFE);

        // Sidebar
        public static readonly Color Sidebar = Color.FromArgb(0x0B, 0x12, 0x20);
        public static readonly Color SidebarHover = Color.FromArgb(0x15, 0x1E, 0x30);
        public static readonly Color SidebarText = Color.FromArgb(0x9A, 0xA7, 0xBD);

        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

        static Font Make(string family, float pt, FontStyle style)
        {
            string k = family + "|" + pt + "|" + (int)style;
            Font f;
            if (!cache.TryGetValue(k, out f)) { f = new Font(family, pt, style, GraphicsUnit.Point); cache[k] = f; }
            return f;
        }

        /// <summary>Regular UI font.</summary>
        public static Font F(float pt) { return Make("Segoe UI", pt, FontStyle.Regular); }
        /// <summary>Semi-bold UI font.</summary>
        public static Font FS(float pt) { return Make("Segoe UI Semibold", pt, FontStyle.Regular); }
        /// <summary>Bold UI font.</summary>
        public static Font FB(float pt) { return Make("Segoe UI", pt, FontStyle.Bold); }
        public static Font Mono(float pt) { return Make("Consolas", pt, FontStyle.Regular); }
        public static Font Emoji(float pt) { return Make("Segoe UI Emoji", pt, FontStyle.Regular); }

        static string iconFamily;
        public static Font Ico(float pt)
        {
            if (iconFamily == null)
            {
                iconFamily = "Segoe MDL2 Assets";
                using (var ifc = new InstalledFontCollection())
                {
                    bool has = false, fluent = false;
                    foreach (var fam in ifc.Families)
                    {
                        if (fam.Name == "Segoe MDL2 Assets") has = true;
                        if (fam.Name == "Segoe Fluent Icons") fluent = true;
                    }
                    if (!has && fluent) iconFamily = "Segoe Fluent Icons";
                }
            }
            return Make(iconFamily, pt, FontStyle.Regular);
        }

        public static void InitScale()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
        }
    }

    /// <summary>Segoe MDL2 icon glyphs.</summary>
    public static class Icons
    {
        public const string Cart = "\uE7BF";
        public const string Box = "\uE7B8";
        public const string Warning = "\uE7BA";
        public const string Chart = "\uE9D9";
        public const string Report = "\uE9F9";
        public const string Settings = "\uE713";
        public const string Lock = "\uE72E";
        public const string Search = "\uE721";
        public const string Add = "\uE710";
        public const string Close = "\uE711";
        public const string Delete = "\uE74D";
        public const string Edit = "\uE70F";
        public const string Pause = "\uE769";
        public const string Tag = "\uE8EC";
        public const string Print = "\uE749";
        public const string Check = "\uE73E";
        public const string Back = "\uE72B";
        public const string Backspace = "\uE750";
        public const string Refresh = "\uE72C";
        public const string Person = "\uE77B";
        public const string People = "\uE716";
        public const string Calendar = "\uE787";
        public const string Card = "\uE8C7";
        public const string History = "\uE81C";
        public const string Power = "\uE7E8";
        public const string Save = "\uE74E";
        public const string Key = "\uE8D7";
        public const string Cash = "\uE825";
        public const string Library = "\uE8F1";
        public const string Document = "\uE8A5";
        public const string Minus = "\uE738";
        public const string Home = "\uE80F";
    }

    /// <summary>GDI+ drawing helpers.</summary>
    public static class Gfx
    {
        public static void SmoothOn(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void FillRound(Graphics g, Brush b, RectangleF r, float radius)
        {
            using (var p = Round(r, radius)) g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Color c, RectangleF r, float radius, float width = 1f)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(c, width)) g.DrawPath(pen, p);
        }

        public static void FillCircle(Graphics g, Color c, RectangleF r)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, r);
        }

        /// <summary>Soft layered drop shadow (cheap approximation of a blur).</summary>
        public static void Shadow(Graphics g, RectangleF r, float radius, int layers = 5, int alpha = 9, float offsetY = 3f)
        {
            for (int i = layers; i >= 1; i--)
            {
                var rr = new RectangleF(r.X - i, r.Y - i + offsetY, r.Width + i * 2, r.Height + i * 2);
                FillRound(g, Color.FromArgb(alpha, 15, 23, 42), rr, radius + i);
            }
        }

        public static Color Alpha(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c); }

        public static Color Blend(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Darken(Color c, float t) { return Blend(c, Color.Black, t); }
        public static Color Lighten(Color c, float t) { return Blend(c, Color.White, t); }

        static StringFormat MakeFormat(StringAlignment h, StringAlignment v, bool wrap)
        {
            var sf = new StringFormat(StringFormat.GenericTypographic);
            sf.Alignment = h;
            sf.LineAlignment = v;
            sf.Trimming = wrap ? StringTrimming.EllipsisWord : StringTrimming.EllipsisCharacter;
            sf.FormatFlags = wrap ? StringFormatFlags.LineLimit : StringFormatFlags.NoWrap;
            return sf;
        }

        public static void Text(Graphics g, string s, Font f, Color c, RectangleF r,
            StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center, bool wrap = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (var sf = MakeFormat(h, v, wrap))
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
        }

        public static SizeF Measure(Graphics g, string s, Font f)
        {
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                return g.MeasureString(s ?? "", f, 10000, sf);
            }
        }

        public static void Icon(Graphics g, string glyph, float pt, Color c, RectangleF r)
        {
            Text(g, glyph, Theme.Ico(pt), c, r, StringAlignment.Center, StringAlignment.Center);
        }

        /// <summary>Draws an image "cover" fitted inside a rounded rectangle.</summary>
        public static void DrawCover(Graphics g, Image img, RectangleF target, float radius)
        {
            float sr = (float)img.Width / img.Height, tr = target.Width / target.Height;
            RectangleF src;
            if (sr > tr) { float w = img.Height * tr; src = new RectangleF((img.Width - w) / 2f, 0, w, img.Height); }
            else { float h = img.Width / tr; src = new RectangleF(0, (img.Height - h) / 2f, img.Width, h); }
            var old = g.Clip;
            using (var p = Round(target, radius))
            {
                g.SetClip(p, CombineMode.Intersect);
                g.DrawImage(img, Rectangle.Round(target), src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel);
                g.Clip = old;
            }
        }
    }
}
