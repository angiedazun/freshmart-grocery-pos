using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Dialogs
{
    /// <summary>Lists parked orders so the cashier can recall or discard them.</summary>
    public class HeldOrdersDialog : DialogBase
    {
        readonly ListBox list = new ListBox();
        readonly RoundButton recall, delete;

        public HeldOrder Selected { get; private set; }

        public HeldOrdersDialog() : base("Held orders", 620, 620, "Recall a parked order to continue serving it")
        {
            list.BorderStyle = BorderStyle.None;
            list.Dock = DockStyle.Fill;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = Theme.S(78);
            list.IntegralHeight = false;
            list.BackColor = Color.White;
            list.DrawItem += DrawRow;
            list.DoubleClick += (s, e) => DoRecall();
            list.SelectedIndexChanged += (s, e) => { recall.Enabled = delete.Enabled = list.SelectedItem != null; };

            recall = new RoundButton { Text = "Recall order", Kind = BtnKind.Primary, Width = Theme.S(190), Enabled = false };
            recall.Click += (s, e) => DoRecall();
            delete = new RoundButton { Text = "Delete", Kind = BtnKind.DangerSoft, Glyph = Icons.Delete, GlyphSize = 11f, Width = Theme.S(140), Enabled = false };
            delete.Click += (s, e) => DoDelete();

            Body.Controls.Add(list);
            Body.Controls.Add(ButtonRow(delete, recall));
            LoadList();
        }

        void LoadList()
        {
            list.Items.Clear();
            foreach (var h in SaleService.GetHeldOrders()) list.Items.Add(h);
            recall.Enabled = delete.Enabled = false;
        }

        void DrawRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var h = (HeldOrder)list.Items[e.Index];
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            g.FillRectangle(Brushes.White, e.Bounds);
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var r = new RectangleF(e.Bounds.X + 2, e.Bounds.Y + 4, e.Bounds.Width - 6, e.Bounds.Height - 8);
            Gfx.FillRound(g, sel ? Theme.PrimarySoft : Theme.SurfaceAlt, r, Theme.S(14));
            if (sel) Gfx.StrokeRound(g, Theme.Primary, r, Theme.S(14));

            var ic = new RectangleF(r.X + Theme.S(14), r.Y + (r.Height - Theme.S(46)) / 2f, Theme.S(46), Theme.S(46));
            Gfx.FillRound(g, Theme.WarningSoft, ic, Theme.S(13));
            Gfx.Icon(g, Icons.Pause, 13f, Theme.WarningDark, ic);
            float tx = ic.Right + Theme.S(14);
            Gfx.Text(g, h.Label, Theme.FS(11f), Theme.Text, new RectangleF(tx, r.Y + Theme.S(8), r.Width - Theme.S(200), Theme.S(26)));
            Gfx.Text(g, h.ItemCount + " items  ·  " + h.HeldAt.ToString("HH:mm"), Theme.F(9.5f), Theme.TextMuted,
                new RectangleF(tx, r.Y + Theme.S(34), r.Width - Theme.S(200), Theme.S(24)));
            Gfx.Text(g, Fmt.Money(h.Total), Theme.FB(11.5f), Theme.PrimaryDark, new RectangleF(r.X, r.Y, r.Width - Theme.S(16), r.Height), StringAlignment.Far);
        }

        void DoRecall()
        {
            if (list.SelectedItem == null) return;
            Selected = SaleService.LoadHeldLines((HeldOrder)list.SelectedItem);
            DialogResult = DialogResult.OK;
            Close();
        }

        void DoDelete()
        {
            var h = list.SelectedItem as HeldOrder;
            if (h == null) return;
            if (!Msg.Confirm(this, "Delete held order?", "\"" + h.Label + "\" will be removed permanently.", "Delete", true)) return;
            SaleService.DeleteHeld(h.HeldId);
            LoadList();
        }
    }

    /// <summary>Success screen after checkout: change due, print and start a new sale.</summary>
    public class SaleCompleteDialog : DialogBase
    {
        readonly Sale sale;
        readonly string printNote;

        public SaleCompleteDialog(Sale sale, string printNote) : base("Sale complete", 520, 560)
        {
            this.sale = sale;
            this.printNote = printNote;

            var print = new RoundButton { Text = "Print receipt", Kind = BtnKind.Secondary, Glyph = Icons.Print, GlyphSize = 12f, Width = Theme.S(200) };
            print.Click += (s, e) =>
            {
                string err;
                if (ReceiptPrinter.Print(ReceiptPrinter.BuildSale(sale), AppSettings.Printer, out err))
                    Toast.Show(this, "Receipt sent to printer", ToastKind.Success);
                else
                    Toast.Show(this, "Printing failed: " + err, ToastKind.Error);
            };
            var done = new RoundButton { Text = "New sale", Kind = BtnKind.Primary, Glyph = Icons.Cart, Width = Theme.S(210) };
            done.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            Body.Controls.Add(ButtonRow(print, done));
            Body.Paint += PaintBody;
        }

        protected override void OnEnterKey() { DialogResult = DialogResult.OK; Close(); }

        void PaintBody(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            int Width = Body.Width;
            float cx = Width / 2f, y = Theme.S(10);
            Gfx.FillCircle(g, Theme.PrimarySoft, new RectangleF(cx - Theme.S(48), y, Theme.S(96), Theme.S(96)));
            Gfx.FillCircle(g, Theme.Primary, new RectangleF(cx - Theme.S(34), y + Theme.S(14), Theme.S(68), Theme.S(68)));
            Gfx.Icon(g, Icons.Check, 26f, Color.White, new RectangleF(cx - Theme.S(34), y + Theme.S(14), Theme.S(68), Theme.S(68)));

            y += Theme.S(110);
            Gfx.Text(g, "Payment successful", Theme.FB(17f), Theme.Text, new RectangleF(0, y, Width, Theme.S(36)), StringAlignment.Center);
            Gfx.Text(g, sale.InvoiceNo + "  ·  " + sale.PaymentMethod + "  ·  " + sale.ItemCount + " items", Theme.F(10f), Theme.TextMuted,
                new RectangleF(0, y + Theme.S(34), Width, Theme.S(26)), StringAlignment.Center);

            y += Theme.S(76);
            var box = new RectangleF(Theme.S(36), y, Width - Theme.S(72), Theme.S(110));
            Gfx.FillRound(g, Theme.SurfaceAlt, box, Theme.S(16));
            Gfx.Text(g, "Total paid", Theme.F(10.5f), Theme.TextMuted, new RectangleF(box.X + Theme.S(22), box.Y + Theme.S(12), box.Width - Theme.S(44), Theme.S(28)));
            Gfx.Text(g, Fmt.Money(sale.Total), Theme.FS(12.5f), Theme.Text, new RectangleF(box.X + Theme.S(22), box.Y + Theme.S(12), box.Width - Theme.S(44), Theme.S(28)), StringAlignment.Far);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, box.X + Theme.S(22), box.Y + Theme.S(50), box.Right - Theme.S(22), box.Y + Theme.S(50));

            string label = sale.PaymentMethod == "Cash" ? "Change due" : sale.PaymentMethod == "Credit" ? "Charged to" : "Paid by";
            string value = sale.PaymentMethod == "Cash" ? Fmt.Money(sale.ChangeAmount) : sale.PaymentMethod == "Credit" ? (sale.CustomerName ?? "account") : "Card";
            Gfx.Text(g, label, Theme.FS(12f), Theme.Text, new RectangleF(box.X + Theme.S(22), box.Y + Theme.S(56), box.Width - Theme.S(44), Theme.S(44)));
            Gfx.Text(g, value, Theme.FB(sale.PaymentMethod == "Cash" ? 20f : 14f), Theme.PrimaryDark,
                new RectangleF(box.X + Theme.S(22), box.Y + Theme.S(56), box.Width - Theme.S(44), Theme.S(44)), StringAlignment.Far);

            if (!string.IsNullOrEmpty(printNote))
                Gfx.Text(g, printNote, Theme.F(9f), Theme.TextFaint, new RectangleF(0, box.Bottom + Theme.S(8), Width, Theme.S(24)), StringAlignment.Center);
        }
    }

    /// <summary>Paper-style preview of a receipt with reprint / void actions.</summary>
    public class ReceiptDialog : DialogBase
    {
        readonly Sale sale;
        public bool Voided { get; private set; }

        class Paper : Control
        {
            public List<string> Lines = new List<string>();
            public Paper() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Theme.Bg; }
            public int LineH { get { return Theme.S(21); } }
            public int PaperW { get { return Theme.S(420); } }
            public int ContentH { get { return Lines.Count * LineH + Theme.S(60); } }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);
                Gfx.SmoothOn(g);
                var r = new RectangleF((Width - PaperW) / 2f, Theme.S(8), PaperW, ContentH - Theme.S(16));
                Gfx.Shadow(g, r, Theme.S(6), 4, 10, 2);
                Gfx.FillRound(g, Color.White, r, Theme.S(6));
                var f = Theme.Mono(10f);
                for (int i = 0; i < Lines.Count; i++)
                    Gfx.Text(g, Lines[i], f, Theme.Text, new RectangleF(r.X + Theme.S(20), r.Y + Theme.S(18) + i * LineH, r.Width - Theme.S(40), LineH));
            }
        }

        public ReceiptDialog(Sale sale, bool canVoid) : base("Invoice " + sale.InvoiceNo, 620, 780,
            sale.SaleDate.ToString("dddd, d MMM yyyy  HH:mm") + "  ·  " + sale.Status)
        {
            this.sale = sale;
            var paper = new Paper { Lines = ReceiptPrinter.BuildSale(sale, true) };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
            paper.Height = paper.ContentH;
            paper.Dock = DockStyle.Top;
            scroll.Controls.Add(paper);

            var print = new RoundButton { Text = "Reprint", Kind = BtnKind.Secondary, Glyph = Icons.Print, GlyphSize = 12f, Width = Theme.S(160) };
            print.Click += (s, e) =>
            {
                string err;
                if (ReceiptPrinter.Print(ReceiptPrinter.BuildSale(sale, true), AppSettings.Printer, out err))
                    Toast.Show(this, "Receipt sent to printer", ToastKind.Success);
                else Toast.Show(this, "Printing failed: " + err, ToastKind.Error);
            };
            var close = new RoundButton { Text = "Close", Kind = BtnKind.Primary, Width = Theme.S(130) };
            close.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            var buttons = new List<RoundButton>();
            if (canVoid && sale.Status == "Completed")
            {
                var v = new RoundButton { Text = "Void sale", Kind = BtnKind.DangerSoft, Glyph = Icons.Delete, GlyphSize = 11f, Width = Theme.S(150) };
                v.Click += (s, e) => DoVoid();
                buttons.Add(v);
            }
            buttons.Add(print);
            buttons.Add(close);

            Body.Controls.Add(scroll);
            Body.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = Theme.S(14), BackColor = Color.White });
            Body.Controls.Add(ButtonRow(buttons.ToArray()));
        }

        void DoVoid()
        {
            if (!Msg.Confirm(this, "Void this sale?", "Items will be returned to stock and the sale removed from today's totals. This cannot be undone.", "Void sale", true)) return;
            try
            {
                SaleService.VoidSale(sale.SaleId, Session.User.UserId);
                Voided = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { Msg.Info(this, "Could not void sale", ex.Message, true); }
        }
    }
}
