using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    public enum BtnKind { Primary, Secondary, Danger, DangerSoft, Soft, Dark, Ghost }

    /// <summary>Owner-drawn, touch-sized rounded button.</summary>
    public class RoundButton : Control
    {
        BtnKind kind = BtnKind.Secondary;
        string glyph, rightText;
        bool hover, down;
        float radius = 14;

        public BtnKind Kind { get { return kind; } set { kind = value; Invalidate(); } }
        public string Glyph { get { return glyph; } set { glyph = value; Invalidate(); } }
        public string RightText { get { return rightText; } set { rightText = value; Invalidate(); } }
        public float Radius { get { return radius; } set { radius = value; Invalidate(); } }
        public float GlyphSize { get; set; } = 13f;

        public RoundButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.FS(11f);
            Size = new Size(Theme.S(140), Theme.S(52));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        void Colors(out Color bg, out Color fg, out Color border)
        {
            border = Color.Empty;
            switch (kind)
            {
                case BtnKind.Primary: bg = hover ? Theme.PrimaryDark : Theme.Primary; fg = Color.White; break;
                case BtnKind.Danger: bg = hover ? Theme.DangerDark : Theme.Danger; fg = Color.White; break;
                case BtnKind.DangerSoft: bg = hover ? Gfx.Darken(Theme.DangerSoft, .04f) : Theme.DangerSoft; fg = Theme.DangerDark; break;
                case BtnKind.Soft: bg = hover ? Gfx.Darken(Theme.PrimarySoft, .04f) : Theme.PrimarySoft; fg = Theme.PrimaryDark; break;
                case BtnKind.Dark: bg = hover ? Color.FromArgb(0x1E, 0x29, 0x3B) : Color.FromArgb(0x0F, 0x17, 0x2A); fg = Color.White; break;
                case BtnKind.Ghost: bg = hover ? Theme.Track : Color.Transparent; fg = Theme.TextMuted; break;
                default: bg = hover ? Theme.SurfaceAlt : Color.White; fg = Theme.Text; border = Theme.Border; break;
            }
            if (down && kind != BtnKind.Ghost) bg = Gfx.Darken(bg, .08f);
            if (!Enabled)
            {
                bg = bg == Color.Transparent ? bg : Gfx.Blend(bg, Color.White, .6f);
                fg = Gfx.Blend(fg, Color.White, .55f);
                if (border != Color.Empty) border = Gfx.Blend(border, Color.White, .3f);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            Color bg, fg, border;
            Colors(out bg, out fg, out border);

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            if (down) r.Inflate(-1f, -1f);
            float rad = Theme.S(radius);
            if (bg != Color.Transparent) Gfx.FillRound(g, bg, r, rad);
            if (border != Color.Empty) Gfx.StrokeRound(g, border, r, rad);

            float pad = Theme.S(16);
            var gf = Theme.Ico(GlyphSize);
            bool hasGlyph = !string.IsNullOrEmpty(glyph);
            bool hasText = !string.IsNullOrEmpty(Text);
            float gw = hasGlyph ? Gfx.Measure(g, glyph, gf).Width : 0;
            float gap = hasGlyph && hasText ? Theme.S(10) : 0;

            if (!string.IsNullOrEmpty(rightText))
            {
                float x = r.X + pad;
                if (hasGlyph) { Gfx.Text(g, glyph, gf, fg, new RectangleF(x, r.Y, gw + 2, r.Height)); x += gw + gap; }
                Gfx.Text(g, Text, Font, fg, new RectangleF(x, r.Y, r.Width - pad * 2, r.Height));
                Gfx.Text(g, rightText, Theme.FB(Font.SizeInPoints + 1f), fg, new RectangleF(r.X, r.Y, r.Width - pad, r.Height), StringAlignment.Far);
                return;
            }

            float tw = hasText ? Gfx.Measure(g, Text, Font).Width : 0;
            float total = gw + gap + tw;
            float sx = r.X + (r.Width - total) / 2f;
            if (sx < r.X + Theme.S(6)) sx = r.X + Theme.S(6);
            if (hasGlyph) { Gfx.Text(g, glyph, gf, fg, new RectangleF(sx, r.Y, gw + 2, r.Height)); sx += gw + gap; }
            if (hasText) Gfx.Text(g, Text, Font, fg, new RectangleF(sx, r.Y, Math.Max(10, r.Right - sx - Theme.S(6)), r.Height));
        }
    }
}
