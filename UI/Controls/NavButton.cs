using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Slim-sidebar item: icon above a small label, soft green tile when active, optional red badge.</summary>
    public class NavButton : Control
    {
        bool active, hover;
        int badge;
        public string Glyph { get; set; }

        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Font = Theme.FS(8.5f);
            Height = Theme.S(72);
            BackColor = Color.White;
        }

        public bool Active { get { return active; } set { active = value; Invalidate(); } }
        public int Badge { get { return badge; } set { badge = value; Invalidate(); } }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Gfx.SmoothOn(g);
            var r = new RectangleF(1, 2, Width - 3, Height - 4);
            if (active) Gfx.FillRound(g, Theme.PrimarySoft, r, Theme.S(16));
            else if (hover) Gfx.FillRound(g, Theme.SurfaceAlt, r, Theme.S(16));

            Color fg = active ? Theme.PrimaryDark : (hover ? Theme.Text : Theme.TextMuted);
            Gfx.Icon(g, Glyph, 16f, fg, new RectangleF(r.X, r.Y + Theme.S(8), r.Width, Theme.S(32)));
            Gfx.Text(g, Text, active ? Theme.FS(8.5f) : Theme.F(8.5f), fg, new RectangleF(r.X, r.Y + Theme.S(40), r.Width, Theme.S(22)), StringAlignment.Center);

            if (badge > 0)
            {
                string t = badge > 99 ? "99+" : badge.ToString();
                float w = Math.Max(Theme.S(20), Gfx.Measure(g, t, Theme.FB(7.5f)).Width + Theme.S(10));
                var b = new RectangleF(r.Right - w - Theme.S(10), r.Y + Theme.S(4), w, Theme.S(20));
                Gfx.FillRound(g, Color.White, new RectangleF(b.X - 2, b.Y - 2, b.Width + 4, b.Height + 4), Theme.S(12));
                Gfx.FillRound(g, Theme.Danger, b, Theme.S(10));
                Gfx.Text(g, t, Theme.FB(7.5f), Color.White, b, StringAlignment.Center);
            }
        }
    }
}
