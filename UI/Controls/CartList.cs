using System;
using System.Drawing;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;

namespace GroceryPOS.UI.Controls
{
    /// <summary>The live cart: owner-drawn rows with big +/- steppers and a remove button.</summary>
    public class CartList : TouchScrollControl
    {
        enum Zone { None, Minus, Plus, Qty, Remove }

        Cart cart;
        int rowH, pressedRow = -1;
        Zone pressedZone = Zone.None;

        public event Action<CartLine> PlusClicked, MinusClicked, RemoveClicked, QtyClicked;

        public CartList()
        {
            BackColor = Color.White;
        }

        public Cart Cart
        {
            get { return cart; }
            set
            {
                if (cart != null) cart.Changed -= OnCartChanged;
                cart = value;
                if (cart != null) cart.Changed += OnCartChanged;
                OnRelayout();
            }
        }

        int lastCount;
        void OnCartChanged()
        {
            OnRelayout();
            if (cart.Lines.Count > lastCount) ScrollToBottom();
            lastCount = cart.Lines.Count;
            Invalidate();
        }

        protected override void OnRelayout()
        {
            rowH = Theme.S(92);
            SetContentHeight(cart == null ? 0 : cart.Lines.Count * rowH);
        }

        int ContentW { get { return Width - Theme.S(10); } }
        const int TextX = 62;

        Rectangle MinusRect(int row) { return new Rectangle(Theme.S(TextX), row * rowH + rowH - Theme.S(46), Theme.S(36), Theme.S(36)); }
        Rectangle QtyRect(int row) { return new Rectangle(Theme.S(TextX) + Theme.S(40), row * rowH + rowH - Theme.S(46), Theme.S(66), Theme.S(36)); }
        Rectangle PlusRect(int row) { return new Rectangle(Theme.S(TextX) + Theme.S(110), row * rowH + rowH - Theme.S(46), Theme.S(36), Theme.S(36)); }
        Rectangle RemoveRect(int row) { return new Rectangle(ContentW - Theme.S(34), row * rowH + Theme.S(6), Theme.S(34), Theme.S(34)); }

        Zone HitZone(Point p, out int row)
        {
            row = cart == null ? -1 : p.Y / rowH;
            if (row < 0 || row >= cart.Lines.Count) { row = -1; return Zone.None; }
            if (RemoveRect(row).Contains(p)) return Zone.Remove;
            var minus = MinusRect(row); minus.Inflate(Theme.S(4), Theme.S(4));
            var plus = PlusRect(row); plus.Inflate(Theme.S(4), Theme.S(4));
            if (minus.Contains(p)) return Zone.Minus;
            if (plus.Contains(p)) return Zone.Plus;
            if (QtyRect(row).Contains(p)) return Zone.Qty;
            return Zone.None;
        }

        protected override void OnPressAt(Point p) { pressedZone = HitZone(p, out pressedRow); Invalidate(); }
        protected override void OnPressCancel() { pressedRow = -1; pressedZone = Zone.None; Invalidate(); }

        protected override void OnTapAt(Point p)
        {
            int row;
            var z = HitZone(p, out row);
            if (row < 0) return;
            var line = cart.Lines[row];
            switch (z)
            {
                case Zone.Plus: if (PlusClicked != null) PlusClicked(line); break;
                case Zone.Minus: if (MinusClicked != null) MinusClicked(line); break;
                case Zone.Remove: if (RemoveClicked != null) RemoveClicked(line); break;
                case Zone.Qty: if (QtyClicked != null) QtyClicked(line); break;
            }
        }

        protected override void PaintContent(Graphics g, Rectangle visible)
        {
            if (cart == null || cart.Lines.Count == 0)
            {
                float cy = visible.Top + Height / 2f;
                Gfx.FillCircle(g, Theme.SurfaceAlt, new RectangleF(Width / 2f - Theme.S(44), cy - Theme.S(96), Theme.S(88), Theme.S(88)));
                Gfx.Icon(g, Icons.Cart, 28f, Theme.TextFaint, new RectangleF(Width / 2f - Theme.S(44), cy - Theme.S(96), Theme.S(88), Theme.S(88)));
                Gfx.Text(g, "Cart is empty", Theme.FS(13f), Theme.Text, new RectangleF(0, cy + Theme.S(2), Width, Theme.S(28)), StringAlignment.Center);
                Gfx.Text(g, "Tap a product or scan a barcode", Theme.F(9.5f), Theme.TextMuted,
                    new RectangleF(0, cy + Theme.S(30), Width, Theme.S(24)), StringAlignment.Center);
                Gfx.Text(g, "F2  search      F12  checkout", Theme.F(8.5f), Theme.TextFaint,
                    new RectangleF(0, cy + Theme.S(62), Width, Theme.S(22)), StringAlignment.Center);
                return;
            }

            int first = Math.Max(0, visible.Top / rowH);
            int last = Math.Min(cart.Lines.Count - 1, visible.Bottom / rowH);
            int W = ContentW;
            for (int i = first; i <= last; i++)
            {
                var l = cart.Lines[i];
                int y0 = i * rowH;

                // tile
                float ts = Theme.S(48);
                var tile = new RectangleF(Theme.S(2), y0 + Theme.S(10), ts, ts);
                Color cc = Theme.Hex(l.CategoryColor ?? "#16A34A");
                var img = ImageCache.Get(l.ImagePath);
                if (img != null) Gfx.DrawCover(g, img, tile, Theme.S(12));
                else
                {
                    Gfx.FillRound(g, Gfx.Lighten(cc, .84f), tile, Theme.S(12));
                    if (!string.IsNullOrEmpty(l.Icon)) Gfx.Text(g, l.Icon, Theme.Emoji(19f), Gfx.Darken(cc, .12f), tile, StringAlignment.Center);
                    else Gfx.Text(g, ProductGrid.Initials(l.Name), Theme.FB(14f), Gfx.Darken(cc, .1f), tile, StringAlignment.Center);
                }

                float tx = Theme.S(TextX);
                Gfx.Text(g, l.Name, Theme.FS(10.5f), Theme.Text, new RectangleF(tx, y0 + Theme.S(8), W - tx - Theme.S(38), Theme.S(22)));
                string sub = Fmt.Money(l.UnitPrice) + (l.IsWeighed ? "  /  kg" : string.IsNullOrEmpty(l.Unit) ? "" : "  ·  " + l.Unit);
                Gfx.Text(g, sub, Theme.F(9f), Theme.TextMuted, new RectangleF(tx, y0 + Theme.S(27), W - tx - Theme.S(38), Theme.S(16)));

                // remove
                var rr = RemoveRect(i);
                bool remDown = pressedRow == i && pressedZone == Zone.Remove;
                var rc = new RectangleF(rr.X + Theme.S(4), rr.Y + Theme.S(4), rr.Width - Theme.S(8), rr.Height - Theme.S(8));
                if (remDown) Gfx.FillCircle(g, Theme.DangerSoft, rc);
                Gfx.Icon(g, Icons.Close, 9f, remDown ? Theme.Danger : Theme.TextFaint, rc);

                // stepper
                Stepper(g, MinusRect(i), Icons.Minus, pressedRow == i && pressedZone == Zone.Minus, false);
                var q = QtyRect(i);
                bool qd = pressedRow == i && pressedZone == Zone.Qty;
                Gfx.FillRound(g, qd ? Theme.Track : Theme.SurfaceAlt, q, Theme.S(10));
                Gfx.Text(g, l.QtyText, Theme.FB(l.IsWeighed ? 9.5f : 11.5f), Theme.Text, q, StringAlignment.Center);
                Stepper(g, PlusRect(i), Icons.Add, pressedRow == i && pressedZone == Zone.Plus, true);

                Gfx.Text(g, Fmt.Money(l.LineTotal), Theme.FB(11.5f), Theme.Text,
                    new RectangleF(tx + Theme.S(152), y0 + rowH - Theme.S(46), W - tx - Theme.S(152), Theme.S(36)), StringAlignment.Far);

                if (i < cart.Lines.Count - 1)
                    using (var pen = new Pen(Theme.Border))
                        g.DrawLine(pen, Theme.S(2), y0 + rowH - 1, W, y0 + rowH - 1);
            }
        }

        static void Stepper(Graphics g, Rectangle r, string glyph, bool isDown, bool primary)
        {
            Color bg = primary ? (isDown ? Theme.PrimaryDark : Theme.Primary) : (isDown ? Theme.Track : Color.White);
            Color fg = primary ? Color.White : Theme.Text;
            Gfx.FillRound(g, bg, r, Theme.S(10));
            if (!primary) Gfx.StrokeRound(g, Theme.Border, r, Theme.S(10));
            Gfx.Icon(g, glyph, 10f, fg, r);
        }
    }
}
