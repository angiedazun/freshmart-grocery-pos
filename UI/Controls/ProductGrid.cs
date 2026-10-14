using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Touch-friendly grid of product cards. Drag to scroll, tap a card to add it to the cart.</summary>
    public class ProductGrid : TouchScrollControl
    {
        List<Product> items = new List<Product>();
        int cols = 1, cardW, cardH, plateD, gap, pad;
        int pressed = -1, hover = -1;

        public event Action<Product> ProductTapped;
        public string EmptyText { get; set; } = "No products found";

        public ProductGrid()
        {
            Font = Theme.F(10f);
        }

        public void SetItems(List<Product> list)
        {
            items = list ?? new List<Product>();
            pressed = hover = -1;
            OnRelayout();
            ScrollToTop();
            Invalidate();
        }

        protected override void OnRelayout()
        {
            gap = Theme.S(14);
            pad = Theme.S(4);
            int avail = Math.Max(Theme.S(140), Width - pad * 2 - Theme.S(8));
            int minW = Math.Max(Theme.S(138), Math.Min(Theme.S(190), avail / 5));
            cols = Math.Max(1, (avail + gap) / (minW + gap));
            cardW = (avail - gap * (cols - 1)) / cols;
            plateD = Math.Max(Theme.S(84), Math.Min(Theme.S(138), (int)(cardW * 0.64)));
            cardH = plateD + Theme.S(118);
            int rows = (items.Count + cols - 1) / cols;
            SetContentHeight(rows == 0 ? 0 : pad * 2 + rows * (cardH + gap) - gap + Theme.S(8));
        }

        Rectangle CardRect(int i)
        {
            int c = i % cols, r = i / cols;
            return new Rectangle(pad + c * (cardW + gap), pad + r * (cardH + gap), cardW, cardH);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < items.Count; i++) if (CardRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnPressAt(Point p) { pressed = HitTest(p); Invalidate(); }
        protected override void OnPressCancel() { if (pressed != -1) { pressed = -1; Invalidate(); } }
        protected override void OnHoverAt(Point p)
        {
            int h = HitTest(p);
            if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnTapAt(Point p)
        {
            int i = HitTest(p);
            if (i >= 0 && ProductTapped != null) ProductTapped(items[i]);
        }

        protected override void PaintContent(Graphics g, Rectangle visible)
        {
            if (items.Count == 0)
            {
                var r = new RectangleF(0, visible.Top, Width, Height);
                Gfx.Icon(g, Icons.Search, 30f, Theme.TextFaint, new RectangleF(0, r.Y + Height / 2f - Theme.S(50), Width, Theme.S(50)));
                Gfx.Text(g, EmptyText, Theme.FS(13f), Theme.TextMuted, new RectangleF(0, r.Y + Height / 2f, Width, Theme.S(30)),
                    StringAlignment.Center);
                return;
            }

            int rowH = cardH + gap;
            int first = Math.Max(0, (visible.Top - pad) / rowH) * cols;
            int last = Math.Min(items.Count - 1, ((visible.Bottom - pad) / rowH + 1) * cols - 1);
            for (int i = first; i <= last; i++) DrawCard(g, items[i], CardRect(i), i == pressed, i == hover);
        }

        void DrawCard(Graphics g, Product p, Rectangle r, bool isPressed, bool isHover)
        {
            var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
            if (isPressed) rf.Inflate(-Theme.S(3), -Theme.S(3));
            float rad = Theme.S(22);
            bool out_ = p.IsOut;
            Color cc = Theme.Hex(p.CategoryColor);

            Gfx.Shadow(g, rf, rad, isHover ? 6 : 4, isHover ? 11 : 7, 3);
            Gfx.FillRound(g, Color.White, rf, rad);
            Gfx.StrokeRound(g, isHover && !out_ ? Gfx.Alpha(Theme.Primary, 150) : Theme.Border, rf, rad);

            // soft category-tinted halo + white plate
            float d = plateD * (isPressed ? 0.96f : 1f);
            float cx = rf.X + rf.Width / 2f, cy = rf.Y + Theme.S(14) + plateD / 2f;
            var halo = new RectangleF(cx - d / 2f - Theme.S(5), cy - d / 2f - Theme.S(5), d + Theme.S(10), d + Theme.S(10));
            Gfx.FillCircle(g, Gfx.Lighten(cc, .9f), halo);
            var plate = new RectangleF(cx - d / 2f, cy - d / 2f, d, d);
            Gfx.FillCircle(g, Color.FromArgb(22, 15, 23, 42), new RectangleF(plate.X, plate.Y + Theme.S(3), plate.Width, plate.Height));
            Gfx.FillCircle(g, Color.White, plate);

            var img = ImageCache.Get(p.ImagePath);
            var inner = new RectangleF(plate.X + Theme.S(4), plate.Y + Theme.S(4), plate.Width - Theme.S(8), plate.Height - Theme.S(8));
            if (img != null) Gfx.DrawCover(g, img, inner, inner.Width / 2f);
            else
            {
                using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(inner, Gfx.Lighten(cc, .84f), Gfx.Lighten(cc, .66f), 60f))
                    g.FillEllipse(br, inner);
                if (!string.IsNullOrEmpty(p.Icon))
                {
                    float pt = (float)Math.Round(inner.Width * 0.46f * 0.75f / Theme.Scale * 2f) / 2f;
                    Gfx.Text(g, p.Icon, Theme.Emoji(pt), Gfx.Darken(cc, .12f), inner, StringAlignment.Center, StringAlignment.Center);
                }
                else Gfx.Text(g, Initials(p.Name), Theme.FB(22f), Gfx.Darken(cc, .1f), inner, StringAlignment.Center, StringAlignment.Center);
            }

            // add badge on the plate's edge
            float cs = Theme.S(32);
            var plus = new RectangleF(plate.Right - cs * 0.62f, plate.Bottom - cs * 0.78f, cs, cs);
            Gfx.FillCircle(g, Color.White, new RectangleF(plus.X - 2, plus.Y - 2, plus.Width + 4, plus.Height + 4));
            Gfx.FillCircle(g, out_ ? Theme.Track : Theme.Primary, plus);
            Gfx.Icon(g, Icons.Add, 10f, out_ ? Theme.TextFaint : Color.White, plus);

            if (!out_ && p.IsLow) Badge(g, "Only " + p.StockText + " left", Theme.WarningSoft, Theme.WarningDark, rf.X + Theme.S(10), rf.Y + Theme.S(10));

            // text block (centred)
            float tx = rf.X + Theme.S(8), tw = rf.Width - Theme.S(16);
            float ty = plate.Bottom + Theme.S(16);
            Gfx.Text(g, p.Name, Theme.FS(10.5f), Theme.Text, new RectangleF(tx, ty, tw, Theme.S(22)), StringAlignment.Center);
            Gfx.Text(g, p.IsWeighed ? "Sold by weight" : p.Unit, Theme.F(9f), Theme.TextMuted, new RectangleF(tx, ty + Theme.S(21), tw, Theme.S(18)), StringAlignment.Center);
            Gfx.Text(g, Fmt.Money(p.Price) + (p.IsWeighed ? " /kg" : ""), Theme.FB(p.IsWeighed ? 10.5f : 12f), out_ ? Theme.TextFaint : Theme.PrimaryDark,
                new RectangleF(tx, ty + Theme.S(42), tw, Theme.S(28)), StringAlignment.Center);

            if (out_)
            {
                Gfx.FillRound(g, Color.FromArgb(150, 255, 255, 255), rf, rad);
                Badge(g, "OUT OF STOCK", Theme.DangerSoft, Theme.DangerDark, cx - Theme.S(56), cy - Theme.S(13), Theme.S(112));
            }
        }

        static void Badge(Graphics g, string text, Color bg, Color fg, float x, float y, float width = 0)
        {
            var f = Theme.FB(8f);
            float w = width > 0 ? width : Gfx.Measure(g, text, f).Width + Theme.S(18);
            var r = new RectangleF(x, y, w, Theme.S(26));
            Gfx.FillRound(g, bg, r, Theme.S(13));
            Gfx.Text(g, text, f, fg, r, StringAlignment.Center);
        }

        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1 ? parts[0].Substring(0, 1).ToUpper()
                : (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpper();
        }
    }
}
