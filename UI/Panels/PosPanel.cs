using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;
using GroceryPOS.UI.Dialogs;

namespace GroceryPOS.UI.Panels
{
    /// <summary>The checkout screen: category pills, product cards, barcode box and the live bill.</summary>
    public class PosPanel : Panel
    {
        readonly Cart cart = new Cart();
        List<Product> all = new List<Product>();
        List<Category> cats = new List<Category>();

        readonly ModernTextBox search = new ModernTextBox { Glyph = Icons.Search, Placeholder = "Scan barcode or search..." };
        readonly PillBar pills = new PillBar();
        readonly ProductGrid grid = new ProductGrid();

        readonly SurfacePanel card = new SurfacePanel { Radius = 22 };
        readonly CartList cartList = new CartList();
        readonly RoundButton recallBtn = new RoundButton { Text = "Held", Glyph = Icons.Pause, Kind = BtnKind.Soft, GlyphSize = 11f, Font = Theme.FS(10f) };
        readonly RoundButton holdBtn = new RoundButton { Text = "Hold", Glyph = Icons.Pause, Kind = BtnKind.Secondary, GlyphSize = 11f, Font = Theme.FS(10f) };
        readonly RoundButton discountBtn = new RoundButton { Text = "Discount", Glyph = Icons.Tag, Kind = BtnKind.Secondary, GlyphSize = 11f, Font = Theme.FS(10f) };
        readonly RoundButton clearBtn = new RoundButton { Text = "Clear", Glyph = Icons.Delete, Kind = BtnKind.DangerSoft, GlyphSize = 11f, Font = Theme.FS(10f) };
        readonly RoundButton checkoutBtn = new RoundButton { Text = "Checkout", Kind = BtnKind.Primary, Font = Theme.FS(14f), Radius = 18 };
        readonly StatChip salesChip = new StatChip { Title = "Today's sales", Glyph = Icons.Chart, Accent = Theme.Primary, Value = "-" };
        readonly StatChip ordersChip = new StatChip { Title = "Orders today", Glyph = Icons.Document, Accent = Theme.Info, Value = "-" };
        int heldCount;

        /// <summary>Raised after stock changes (sale completed) so other screens can refresh badges.</summary>
        public event Action StockChanged;

        public bool HasOpenOrder { get { return cart.Lines.Count > 0; } }

        public PosPanel()
        {
            BackColor = Theme.Bg;
            DoubleBuffered = true;

            cart.Changed += OnCartChanged;
            cartList.Cart = cart;

            search.EnterPressed += (s, e) => OnScan();
            search.TextChanged += (s, e) => ApplyFilter();
            pills.SelectedChanged += (s, e) => ApplyFilter();
            grid.ProductTapped += AddProduct;

            cartList.PlusClicked += l =>
            {
                if (!cart.SetQuantity(l, l.Quantity + (l.IsWeighed ? 100 : 1))) Toast.Show(this, "Only " + Fmt.Qty(l.IsWeighed, l.MaxStock) + " in stock", ToastKind.Warning);
                FocusSearch();
            };
            cartList.MinusClicked += l => { cart.SetQuantity(l, l.Quantity - (l.IsWeighed ? 100 : 1)); FocusSearch(); };
            cartList.RemoveClicked += l => { cart.Remove(l); FocusSearch(); };
            cartList.QtyClicked += EditQuantity;

            recallBtn.Click += (s, e) => Recall();
            holdBtn.Click += (s, e) => Hold();
            discountBtn.Click += (s, e) => EditDiscount();
            clearBtn.Click += (s, e) => ClearOrder();
            checkoutBtn.Click += (s, e) => Checkout();

            card.Paint += PaintCard;
            card.Controls.Add(cartList);
            card.Controls.Add(recallBtn);
            card.Controls.Add(holdBtn);
            card.Controls.Add(discountBtn);
            card.Controls.Add(clearBtn);
            card.Controls.Add(checkoutBtn);

            Controls.Add(card);
            Controls.Add(search);
            Controls.Add(salesChip);
            Controls.Add(ordersChip);
            Controls.Add(pills);
            Controls.Add(grid);
            OnCartChanged();
        }

        // ------------------------------------------------------------------ data
        public void Reload()
        {
            try
            {
                cats = ProductService.GetCategories();
                all = ProductService.GetProducts();
            }
            catch (Exception ex)
            {
                Msg.Info(this, "Could not load products", ex.Message, true);
                return;
            }

            var names = new List<string> { "All Items" };
            names.AddRange(cats.Select(c => c.Name));
            pills.SetItems(names, cats.Select(c => Theme.Hex(c.Color)).ToList());
            DoLayout();
            cart.TaxRate = AppSettings.TaxRate;

            // sync cart lines with fresh stock
            foreach (var l in cart.Lines.ToList())
            {
                var p = all.FirstOrDefault(x => x.ProductId == l.ProductId);
                if (p == null || p.Stock <= 0) { cart.Lines.Remove(l); continue; }
                l.MaxStock = p.Stock;
                if (l.Quantity > p.Stock) l.Quantity = p.Stock;
            }
            cart.NotifyChanged();
            ApplyFilter();
            UpdateHeldCount();
            UpdateStats();
        }

        void UpdateStats()
        {
            try
            {
                var z = SaleService.GetZReport(DateTime.Today, Session.User.IsAdmin ? (int?)null : Session.User.UserId, "");
                salesChip.Value = Fmt.Money(z.NetSales);
                ordersChip.Value = z.Transactions.ToString();
            }
            catch { }
        }

        void ApplyFilter()
        {
            string q = (search.Text ?? "").Trim().ToLowerInvariant();
            string cat = pills.SelectedIndex > 0 ? pills.SelectedItem : null;
            var list = all.Where(p =>
                (cat == null || p.CategoryName == cat) &&
                (q.Length == 0 || p.Name.ToLowerInvariant().Contains(q) || (p.Barcode != null && p.Barcode.ToLowerInvariant().Contains(q))))
                .ToList();
            grid.EmptyText = q.Length > 0 ? "No match for \"" + search.Text.Trim() + "\"" : "No products in this category";
            grid.SetItems(list);
        }

        void UpdateHeldCount()
        {
            try { heldCount = SaleService.HeldCount(); } catch { heldCount = 0; }
            recallBtn.Text = heldCount > 0 ? "Held (" + heldCount + ")" : "Held";
            recallBtn.Kind = heldCount > 0 ? BtnKind.Soft : BtnKind.Ghost;
        }

        // ------------------------------------------------------------------ input
        public void FocusSearch()
        {
            if (!IsHandleCreated) return;
            BeginInvoke((Action)(() => { if (!IsDisposed && Visible) search.Focus(); }));
        }

#if DEBUG
        public void DebugAdd(string productName, int qty)
        {
            var p = all.FirstOrDefault(x => x.Name == productName);
            if (p != null) cart.Add(p, qty);
        }
#endif

        /// <summary>Called by the main form when a key is typed with nothing focused (USB barcode scanners).</summary>
        public void CaptureKey(char c)
        {
            search.Focus();
            search.Inner.AppendText(c.ToString());
        }

        void OnScan()
        {
            string code = (search.Text ?? "").Trim();
            if (code.Length == 0) return;
            var hit = all.FirstOrDefault(p => string.Equals(p.Barcode, code, StringComparison.OrdinalIgnoreCase));
            if (hit == null)
            {
                var matches = all.Where(p => p.Name.IndexOf(code, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (matches.Count == 1) hit = matches[0];
            }
            if (hit == null)
            {
                Toast.Show(this, "No product found for \"" + code + "\"", ToastKind.Error);
                search.SelectAll();
                return;
            }
            AddProduct(hit);
            search.Text = "";
        }

        void AddProduct(Product p)
        {
            int qty = 1;
            if (p.IsWeighed)
            {
                if (p.Stock <= 0) { Toast.Show(this, p.Name + " is out of stock", ToastKind.Error); FocusSearch(); return; }
                using (var wd = new WeightDialog(p.Name, p.Price, p.Stock, 0))
                {
                    if (wd.ShowOver(this) != DialogResult.OK) { FocusSearch(); return; }
                    qty = wd.Grams;
                }
            }
            switch (cart.Add(p, qty))
            {
                case AddResult.OutOfStock: Toast.Show(this, p.Name + " is out of stock", ToastKind.Error); break;
                case AddResult.LimitReached: Toast.Show(this, "Only " + p.StockText + " of " + p.Name + " in stock", ToastKind.Warning); break;
            }
            FocusSearch();
        }

        void EditQuantity(CartLine line)
        {
            if (line.IsWeighed)
            {
                using (var wd = new WeightDialog(line.Name, line.UnitPrice, line.MaxStock, line.Quantity))
                    if (wd.ShowOver(this) == DialogResult.OK) cart.SetQuantity(line, wd.Grams);
            }
            else
            {
                using (var d = new NumberDialog(line.Name, "Quantity", line.Quantity, false, line.MaxStock, "Available in stock: " + line.MaxStock))
                    if (d.ShowOver(this) == DialogResult.OK) cart.SetQuantity(line, (int)d.Value);
            }
            FocusSearch();
        }

        // ------------------------------------------------------------------ order actions
        void Hold()
        {
            if (cart.Lines.Count == 0) { Toast.Show(this, "Nothing to hold - the cart is empty", ToastKind.Info); return; }
            try
            {
                SaleService.HoldOrder(Session.User.UserId, "Order " + DateTime.Now.ToString("HH:mm"), cart);
                cart.Clear();
                UpdateHeldCount();
                Toast.Show(this, "Order held - recall it from the Held button", ToastKind.Success);
            }
            catch (Exception ex) { Msg.Info(this, "Could not hold order", ex.Message, true); }
            FocusSearch();
        }

        void Recall()
        {
            if (heldCount == 0) { Toast.Show(this, "There are no held orders", ToastKind.Info); return; }
            HeldOrder h;
            using (var d = new HeldOrdersDialog())
            {
                var r = d.ShowOver(this);
                UpdateHeldCount();
                if (r != DialogResult.OK) { FocusSearch(); return; }
                h = d.Selected;
            }

            int skipped = 0;
            bool wasEmpty = cart.Lines.Count == 0;
            foreach (var kv in h.Lines)
            {
                var p = all.FirstOrDefault(x => x.ProductId == kv.Key);
                if (p == null || cart.Add(p, Math.Min(kv.Value, Math.Max(1, p.Stock))) != AddResult.Added) skipped++;
            }
            if (wasEmpty && h.DiscountValue > 0) { cart.DiscountIsPercent = h.DiscountIsPercent; cart.DiscountValue = h.DiscountValue; cart.NotifyChanged(); }
            try { SaleService.DeleteHeld(h.HeldId); } catch { }
            UpdateHeldCount();
            Toast.Show(this, skipped == 0 ? "Held order recalled" : "Recalled - " + skipped + " item(s) skipped (out of stock)",
                skipped == 0 ? ToastKind.Success : ToastKind.Warning);
            FocusSearch();
        }

        void EditDiscount()
        {
            if (cart.Lines.Count == 0) { Toast.Show(this, "Add items before applying a discount", ToastKind.Info); return; }
            using (var d = new DiscountDialog(cart.Subtotal, cart.DiscountIsPercent, cart.DiscountValue))
            {
                if (d.ShowOver(this) == DialogResult.OK)
                {
                    cart.DiscountIsPercent = d.IsPercent;
                    cart.DiscountValue = d.Value;
                    cart.NotifyChanged();
                }
            }
            FocusSearch();
        }

        void ClearOrder()
        {
            if (cart.Lines.Count == 0) return;
            if (Msg.Confirm(this, "Clear this order?", "All " + cart.ItemCount + " items will be removed from the cart.", "Clear order", true)) cart.Clear();
            FocusSearch();
        }

        public void Checkout()
        {
            if (cart.Lines.Count == 0) { Toast.Show(this, "The cart is empty", ToastKind.Info); return; }

            PaymentInfo pay;
            using (var d = new PaymentDialog(cart.Total))
            {
                if (d.ShowOver(this) != DialogResult.OK) { FocusSearch(); return; }
                pay = d.Result;
            }

            Sale sale;
            try { sale = SaleService.Checkout(cart, pay, Session.User.UserId); }
            catch (StockException ex)
            {
                Msg.Info(this, "Stock changed", ex.Message, true);
                Reload();
                return;
            }
            catch (Exception ex)
            {
                Msg.Info(this, "Checkout failed", ex.Message, true);
                return;
            }

            bool printed = ReceiptPrinter.AutoPrint(ReceiptPrinter.BuildSale(sale));
            string note = printed ? "Receipt sent to printer"
                : AppSettings.AutoPrint ? "Auto-print skipped - choose a receipt printer in Settings" : null;
            cart.Clear();
            using (var done = new SaleCompleteDialog(sale, note)) done.ShowOver(this);

            Reload();
            if (StockChanged != null) StockChanged();
            FocusSearch();
        }

        // ------------------------------------------------------------------ layout + painting
        void OnCartChanged()
        {
            bool has = cart.Lines.Count > 0;
            checkoutBtn.Enabled = has;
            checkoutBtn.RightText = has ? Fmt.Money(cart.Total) : null;
            holdBtn.Enabled = has;
            discountBtn.Enabled = has;
            clearBtn.Enabled = has;
            discountBtn.Kind = cart.DiscountAmount > 0 ? BtnKind.Soft : BtnKind.Secondary;
            card.Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        int summaryH, rowStep;

        void DoLayout()
        {
            if (Width < 50 || Height < 50) return;
            bool compact = Height < Theme.S(600);
            int gap = Theme.S(18);
            int cartW = Math.Max(Theme.S(340), Math.Min(Theme.S(430), (int)(Width * 0.36)));
            card.SetBounds(Width - cartW, 0, cartW, Height);
            int mainW = Width - cartW - gap;
            int searchH = Theme.S(compact ? 50 : 54);
            pills.PillHeight = Theme.S(compact ? 40 : 44);
            int pillsH = pills.MeasureHeight(mainW);
            bool chips = mainW >= Theme.S(620);
            int cg = Theme.S(12), chipW = Theme.S(168);
            int searchW = chips ? mainW - (chipW + cg) * 2 : mainW;
            search.SetBounds(0, 0, searchW, searchH);
            salesChip.Visible = ordersChip.Visible = chips;
            salesChip.SetBounds(searchW + cg, 0, chipW, searchH);
            ordersChip.SetBounds(searchW + cg * 2 + chipW, 0, chipW, searchH);
            pills.SetBounds(0, searchH + Theme.S(12), mainW, pillsH);
            int gridTop = searchH + Theme.S(12) + pillsH + Theme.S(12);
            grid.SetBounds(0, gridTop, mainW, Height - gridTop);

            int m = Theme.S(compact ? 18 : 22), inner = cartW - m * 2;
            recallBtn.SetBounds(cartW - m - Theme.S(112), Theme.S(14), Theme.S(112), Theme.S(40));

            int checkoutH = Theme.S(compact ? 60 : 68), actionsH = Theme.S(compact ? 44 : 50);
            checkoutBtn.SetBounds(m, Height - Theme.S(compact ? 16 : 22) - checkoutH, inner, checkoutH);
            int ay = checkoutBtn.Top - Theme.S(10) - actionsH;
            int bw = (inner - Theme.S(20)) / 3;
            holdBtn.SetBounds(m, ay, bw, actionsH);
            discountBtn.SetBounds(m + bw + Theme.S(10), ay, bw, actionsH);
            clearBtn.SetBounds(m + (bw + Theme.S(10)) * 2, ay, inner - (bw + Theme.S(10)) * 2, actionsH);

            rowStep = Theme.S(compact ? 22 : 26);
            summaryH = rowStep * 3 + Theme.S(compact ? 38 : 46);
            int sumTop = ay - Theme.S(8) - summaryH;
            int listTop = Theme.S(64);
            cartList.SetBounds(m - Theme.S(4), listTop, inner + Theme.S(8), Math.Max(Theme.S(70), sumTop - listTop - Theme.S(4)));
            card.Invalidate();
        }

        void PaintCard(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            int m = Theme.S(22), w = card.Width - m * 2;
            var cardRect = new RectangleF(0.5f, 0.5f, card.Width - 1.5f, card.Height - 1.5f);
            using (var clip = Gfx.Round(cardRect, Theme.S(22)))
            {
                var oldClip = g.Clip;
                g.SetClip(clip, System.Drawing.Drawing2D.CombineMode.Intersect);
                var band = new Rectangle(0, 0, card.Width, Theme.S(62));
                using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(band, Color.FromArgb(0xE3, 0xFA, 0xEA), Color.White, 0f))
                    g.FillRectangle(br, band);
                g.Clip = oldClip;
            }
            Gfx.StrokeRound(g, Theme.Border, cardRect, Theme.S(22));
            Gfx.Text(g, "Current order", Theme.FB(15f), Theme.Text, new RectangleF(m, Theme.S(10), Theme.S(200), Theme.S(26)));
            Gfx.Text(g, (cart.ItemCount == 1 ? "1 item" : cart.ItemCount + " items") + "  ·  Walk-in customer", Theme.F(9.5f), Theme.TextMuted,
                new RectangleF(m, Theme.S(34), Theme.S(200), Theme.S(22)));
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, m, Theme.S(62), m + w, Theme.S(62));

            int sumTop = holdBtn.Top - Theme.S(8) - summaryH;
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, m, sumTop, m + w, sumTop);
            float y = sumTop + Theme.S(6);
            Row(g, "Subtotal", Fmt.Money(cart.Subtotal), m, w, y, Theme.Text, false);
            string dl = cart.DiscountAmount > 0 && cart.DiscountIsPercent ? "Discount (" + cart.DiscountValue.ToString("0.##") + "%)" : "Discount";
            Row(g, dl, cart.DiscountAmount > 0 ? "-" + Fmt.Money(cart.DiscountAmount) : Fmt.Money(0), m, w, y + rowStep,
                cart.DiscountAmount > 0 ? Theme.DangerDark : Theme.TextMuted, false);
            Row(g, cart.TaxRate > 0 ? "Tax (" + cart.TaxRate.ToString("0.##") + "%)" : "Tax", Fmt.Money(cart.TaxAmount), m, w, y + rowStep * 2, Theme.TextMuted, false);
            float ty = y + rowStep * 3 + Theme.S(2);
            Gfx.Text(g, "Total", Theme.FS(12f), Theme.Text, new RectangleF(m, ty, w, Theme.S(34)));
            Gfx.Text(g, Fmt.Money(cart.Total), Theme.FB(19f), Theme.PrimaryDark, new RectangleF(m, ty, w, Theme.S(34)), StringAlignment.Far);
        }

        static void Row(Graphics g, string label, string value, int x, int w, float y, Color valueColor, bool bold)
        {
            Gfx.Text(g, label, Theme.F(10f), Theme.TextMuted, new RectangleF(x, y, w, Theme.S(24)));
            Gfx.Text(g, value, bold ? Theme.FB(10.5f) : Theme.FS(10.5f), valueColor, new RectangleF(x, y, w, Theme.S(24)), StringAlignment.Far);
        }
    }
}