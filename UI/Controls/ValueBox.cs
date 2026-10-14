using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Large read-only value display used with the on-screen keypad.</summary>
    public class ValueBox : Control
    {
        string caption = "", value = "", prefix = "", suffix = "";
        Color accent = Theme.Primary;

        public ValueBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Height = Theme.S(92);
        }

        public string Caption { get { return caption; } set { caption = value; Invalidate(); } }
        public string Value { get { return value; } set { this.value = value; Invalidate(); } }
        public string Prefix { get { return prefix; } set { prefix = value; Invalidate(); } }
        public string Suffix { get { return suffix; } set { suffix = value; Invalidate(); } }
        public Color Accent { get { return accent; } set { accent = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            Gfx.FillRound(g, Theme.SurfaceAlt, r, Theme.S(16));
            Gfx.StrokeRound(g, accent, r, Theme.S(16), 2f);
            Gfx.Text(g, caption, Theme.FS(9f), Theme.TextMuted, new RectangleF(r.X + Theme.S(18), r.Y + Theme.S(10), r.Width - Theme.S(36), Theme.S(20)));
            string shown = string.IsNullOrEmpty(value) ? "0" : value;
            Color c = string.IsNullOrEmpty(value) ? Theme.TextFaint : Theme.Text;
            Gfx.Text(g, prefix + shown + suffix, Theme.FB(24f), c,
                new RectangleF(r.X + Theme.S(18), r.Y + Theme.S(30), r.Width - Theme.S(36), r.Height - Theme.S(36)), StringAlignment.Far);
        }
    }

    /// <summary>Keypad / keyboard input editing for numeric entry strings.</summary>
    public static class NumKeys
    {
        public static string Apply(string cur, string key, bool allowDecimal, int maxLen = 9, int maxDecimals = 2)
        {
            cur = cur ?? "";
            if (key == "BACK") return cur.Length > 0 ? cur.Substring(0, cur.Length - 1) : cur;
            if (key == ".")
            {
                if (!allowDecimal || cur.Contains(".")) return cur;
                return (cur.Length == 0 ? "0" : cur) + ".";
            }
            if (key == "00" && (cur.Length == 0 || cur == "0")) return cur;
            string n = (cur == "0" && key != ".") ? key : cur + key;
            if (n.Length > maxLen) return cur;
            int dot = n.IndexOf('.');
            if (dot >= 0 && n.Length - dot - 1 > maxDecimals) return cur;
            return n;
        }

        public static decimal Parse(string s)
        {
            decimal v;
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out v) ? v : 0m;
        }
    }
}
