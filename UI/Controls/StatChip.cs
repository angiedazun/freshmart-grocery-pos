using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Compact KPI pill: tinted icon circle, small label and a bold value.</summary>
    public class StatChip : Control
    {
        string title, value, glyph;
        Color accent = Theme.Primary;

        public StatChip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Size = new Size(Theme.S(176), Theme.S(54));
        }

        public string Title { get { return title; } set { title = value; Invalidate(); } }
        public string Value { get { return value; } set { this.value = value; Invalidate(); } }
        public string Glyph { get { return glyph; } set { glyph = value; Invalidate(); } }
        public Color Accent { get { return accent; } set { accent = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Gfx.FillRound(g, Color.White, r, Theme.S(16));
            Gfx.StrokeRound(g, Theme.Border, r, Theme.S(16));

            float ic = Math.Min(Theme.S(36), r.Height - Theme.S(12));
            var c = new RectangleF(r.X + Theme.S(9), r.Y + (r.Height - ic) / 2f, ic, ic);
            Gfx.FillCircle(g, Gfx.Lighten(accent, .84f), c);
            Gfx.Icon(g, glyph, 11f, accent, c);

            float tx = c.Right + Theme.S(10), w = r.Right - tx - Theme.S(6);
            Gfx.Text(g, title, Theme.F(8.5f), Theme.TextMuted, new RectangleF(tx, r.Y + r.Height * 0.12f, w, r.Height * 0.38f));
            Gfx.Text(g, value, Theme.FB(11f), Theme.Text, new RectangleF(tx, r.Y + r.Height * 0.42f, w, r.Height * 0.48f));
        }
    }
}
