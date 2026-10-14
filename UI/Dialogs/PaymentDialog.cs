using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Dialogs
{
    /// <summary>Touch payment screen: Cash (with change calculator), Card, or Credit / store account.</summary>
    public class PaymentDialog : DialogBase
    {
        readonly decimal total;
        string buf = "";
        Customer customer;

        readonly SurfacePanel totalCard = new SurfacePanel { Fill = Theme.Primary, Stroke = Color.Empty };
        readonly SegmentedControl seg = new SegmentedControl();
        readonly SurfacePanel summary = new SurfacePanel { Fill = Theme.SurfaceAlt };
        readonly FlowLayoutPanel quick = new FlowLayoutPanel { BackColor = Color.White, WrapContents = true };
        readonly RoundButton payBtn = new RoundButton { Text = "Complete payment", Kind = BtnKind.Primary, Glyph = Icons.Check };

        readonly Panel cashPane = new Panel { BackColor = Color.White };
        readonly Panel cardPane = new Panel { BackColor = Color.White };
        readonly Panel creditPane = new Panel { BackColor = Color.White };
        readonly ValueBox given = new ValueBox();
        readonly NumPad pad = new NumPad();
        readonly ModernTextBox refBox = new ModernTextBox { Placeholder = "Approval code / last 4 digits (optional)" };
        readonly SurfacePanel customerCard = new SurfacePanel { Fill = Theme.SurfaceAlt };

        public PaymentInfo Result { get; private set; }

        int Mode { get { return seg.SelectedIndex; } }   // 0 cash, 1 card, 2 credit

        public PaymentDialog(decimal total) : base("Payment", 960, 700, "Choose how the customer is paying")
        {
            this.total = total;
            int h = Theme.S(700) - Theme.S(86) - Theme.S(8) - Theme.S(26);
            var canvas = AddCanvas();

            // ---- left column ----
            totalCard.SetBounds(0, 0, Theme.S(440), Theme.S(112));
            totalCard.Paint += (s, e) =>
            {
                Gfx.Text(e.Graphics, "AMOUNT DUE", Theme.FS(9.5f), Color.FromArgb(200, 255, 255, 255), new RectangleF(Theme.S(24), Theme.S(14), Theme.S(300), Theme.S(24)));
                Gfx.Text(e.Graphics, Fmt.Money(total), Theme.FB(30f), Color.White, new RectangleF(Theme.S(24), Theme.S(38), totalCard.Width - Theme.S(48), Theme.S(60)));
            };

            seg.Items = new[] { "Cash", "Card", "Credit" };
            seg.SetBounds(0, Theme.S(128), Theme.S(440), Theme.S(54));
            seg.SelectedChanged += (s, e) => ModeChanged();

            summary.SetBounds(0, Theme.S(198), Theme.S(440), Theme.S(120));
            summary.Paint += PaintSummary;

            quick.SetBounds(0, Theme.S(332), Theme.S(440), Theme.S(124));

            payBtn.SetBounds(0, h - Theme.S(66), Theme.S(440), Theme.S(66));
            payBtn.Font = Theme.FS(13f);
            payBtn.GlyphSize = 15f;
            payBtn.Click += (s, e) => Pay();

            // ---- right: cash ----
            cashPane.SetBounds(Theme.S(470), 0, Theme.S(434), h);
            given.Caption = "Amount given by customer";
            given.Prefix = AppSettings.Currency + " ";
            given.SetBounds(0, 0, Theme.S(434), Theme.S(92));
            pad.ShowDecimal = true;
            pad.SetBounds(0, Theme.S(108), Theme.S(434), Math.Min(Theme.S(440), h - Theme.S(108)));
            pad.KeyPressed += k => Press(k);
            cashPane.Controls.Add(given);
            cashPane.Controls.Add(pad);

            // ---- right: card ----
            cardPane.SetBounds(Theme.S(470), 0, Theme.S(434), h);
            var cardArt = new SurfacePanel { Fill = Theme.SurfaceAlt };
            cardArt.SetBounds(0, 0, Theme.S(434), Theme.S(240));
            cardArt.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var ic = new RectangleF((cardArt.Width - Theme.S(96)) / 2f, Theme.S(32), Theme.S(96), Theme.S(96));
                Gfx.FillCircle(g, Theme.InfoSoft, ic);
                Gfx.Icon(g, Icons.Card, 34f, Theme.Info, ic);
                Gfx.Text(g, "Present the card on the terminal", Theme.FS(12.5f), Theme.Text, new RectangleF(0, Theme.S(142), cardArt.Width, Theme.S(30)), StringAlignment.Center);
                Gfx.Text(g, "Tap, insert or swipe, then confirm below\nonce the terminal approves the payment.", Theme.F(10f), Theme.TextMuted,
                    new RectangleF(Theme.S(20), Theme.S(174), cardArt.Width - Theme.S(40), Theme.S(54)), StringAlignment.Center, StringAlignment.Near, true);
            };
            refBox.SetBounds(0, Theme.S(280), Theme.S(434), Theme.S(52));
            var refCap = new Label { Text = "Reference (optional)", Font = Theme.FS(9f), ForeColor = Theme.TextMuted, BackColor = Color.Transparent, AutoSize = false };
            refCap.SetBounds(0, Theme.S(254), Theme.S(300), Theme.S(24));
            cardPane.Controls.Add(cardArt);
            cardPane.Controls.Add(refCap);
            cardPane.Controls.Add(refBox);

            // ---- right: credit ----
            creditPane.SetBounds(Theme.S(470), 0, Theme.S(434), h);
            var pick = new RoundButton { Text = "Select customer account", Kind = BtnKind.Secondary, Glyph = Icons.People };
            pick.SetBounds(0, 0, Theme.S(434), Theme.S(62));
            pick.Click += (s, e) => PickCustomer();
            customerCard.SetBounds(0, Theme.S(80), Theme.S(434), Theme.S(150));
            customerCard.Paint += PaintCustomer;
            creditPane.Controls.Add(pick);
            creditPane.Controls.Add(customerCard);

            canvas.Controls.Add(totalCard);
            canvas.Controls.Add(seg);
            canvas.Controls.Add(summary);
            canvas.Controls.Add(quick);
            canvas.Controls.Add(payBtn);
            canvas.Controls.Add(cashPane);
            canvas.Controls.Add(cardPane);
            canvas.Controls.Add(creditPane);

            BuildQuick();
            ModeChanged();
        }

        decimal Given { get { return NumKeys.Parse(buf); } }

        void BuildQuick()
        {
            quick.Controls.Clear();
            var amounts = new SortedSet<decimal>();
            foreach (var step in new[] { 100m, 500m, 1000m, 2000m, 5000m, 10000m })
            {
                var v = Math.Ceiling(total / step) * step;
                if (v > total) amounts.Add(v);
            }
            AddQuick("Exact", total);
            foreach (var a in amounts.Take(4)) AddQuick(Fmt.Num(a).Replace(".00", ""), a);
        }

        void AddQuick(string text, decimal amount)
        {
            var b = new RoundButton { Text = text, Kind = text == "Exact" ? BtnKind.Soft : BtnKind.Secondary, Width = Theme.S(128), Height = Theme.S(54), Margin = new Padding(0, 0, Theme.S(14), Theme.S(12)) };
            b.Click += (s, e) => { buf = amount.ToString("0.##", CultureInfo.InvariantCulture); CashChanged(); };
            quick.Controls.Add(b);
        }

        void ModeChanged()
        {
            cashPane.Visible = Mode == 0;
            cardPane.Visible = Mode == 1;
            creditPane.Visible = Mode == 2;
            quick.Visible = Mode == 0;
            CashChanged();
        }

        void Press(string k) { buf = NumKeys.Apply(buf, k, true); CashChanged(); }

        void CashChanged()
        {
            given.Value = buf;
            given.Accent = Mode == 0 && buf.Length > 0 && Given < total ? Theme.Warning : Theme.Primary;
            payBtn.Enabled = Mode == 0 ? Given >= total && total >= 0 : Mode == 1 ? true : customer != null;
            summary.Invalidate();
            customerCard.Invalidate();
        }

        void PickCustomer()
        {
            using (var d = new CustomerPickerDialog())
                if (d.ShowOver(this) == DialogResult.OK) { customer = d.Selected; CashChanged(); }
        }

        void PaintSummary(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            float x = Theme.S(22), w = summary.Width - Theme.S(44);
            if (Mode == 0)
            {
                decimal change = Given - total;
                bool short_ = buf.Length > 0 && change < 0;
                Gfx.Text(g, "Amount given", Theme.F(10f), Theme.TextMuted, new RectangleF(x, Theme.S(14), w, Theme.S(24)));
                Gfx.Text(g, Fmt.Money(Given), Theme.FS(12f), Theme.Text, new RectangleF(x, Theme.S(14), w, Theme.S(24)), StringAlignment.Far);
                using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, x, Theme.S(48), x + w, Theme.S(48));
                Gfx.Text(g, short_ ? "Still to pay" : "Change to return", Theme.FS(11f), short_ ? Theme.WarningDark : Theme.Text, new RectangleF(x, Theme.S(58), w, Theme.S(48)));
                Gfx.Text(g, Fmt.Money(short_ ? -change : Math.Max(0, change)), Theme.FB(19f), short_ ? Theme.WarningDark : Theme.PrimaryDark,
                    new RectangleF(x, Theme.S(58), w, Theme.S(48)), StringAlignment.Far);
            }
            else if (Mode == 1)
            {
                Gfx.Text(g, "Payment method", Theme.F(10f), Theme.TextMuted, new RectangleF(x, Theme.S(14), w, Theme.S(24)));
                Gfx.Text(g, "Card terminal", Theme.FS(12f), Theme.Text, new RectangleF(x, Theme.S(14), w, Theme.S(24)), StringAlignment.Far);
                using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, x, Theme.S(48), x + w, Theme.S(48));
                Gfx.Text(g, "Charge exactly " + Fmt.Money(total) + " to the card, then press\nComplete payment.", Theme.F(10f), Theme.TextMuted,
                    new RectangleF(x, Theme.S(56), w, Theme.S(54)), StringAlignment.Near, StringAlignment.Near, true);
            }
            else
            {
                Gfx.Text(g, "Charged to account", Theme.F(10f), Theme.TextMuted, new RectangleF(x, Theme.S(14), w, Theme.S(24)));
                Gfx.Text(g, customer == null ? "-" : customer.Name, Theme.FS(12f), Theme.Text, new RectangleF(x, Theme.S(14), w, Theme.S(24)), StringAlignment.Far);
                using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, x, Theme.S(48), x + w, Theme.S(48));
                Gfx.Text(g, "New balance", Theme.FS(11f), Theme.Text, new RectangleF(x, Theme.S(58), w, Theme.S(48)));
                Gfx.Text(g, customer == null ? "-" : Fmt.Money(customer.Balance + total), Theme.FB(19f), Theme.WarningDark,
                    new RectangleF(x, Theme.S(58), w, Theme.S(48)), StringAlignment.Far);
            }
        }

        void PaintCustomer(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            if (customer == null)
            {
                Gfx.Icon(g, Icons.Person, 24f, Theme.TextFaint, new RectangleF(0, Theme.S(18), customerCard.Width, Theme.S(60)));
                Gfx.Text(g, "No customer selected", Theme.FS(11.5f), Theme.TextMuted, new RectangleF(0, Theme.S(80), customerCard.Width, Theme.S(28)), StringAlignment.Center);
                Gfx.Text(g, "Pick an existing account or add a new one.", Theme.F(9.5f), Theme.TextFaint, new RectangleF(0, Theme.S(106), customerCard.Width, Theme.S(26)), StringAlignment.Center);
                return;
            }
            var av = new RectangleF(Theme.S(22), Theme.S(22), Theme.S(60), Theme.S(60));
            Gfx.FillCircle(g, Theme.InfoSoft, av);
            Gfx.Text(g, ProductGrid.Initials(customer.Name), Theme.FB(15f), Theme.Info, av, StringAlignment.Center);
            Gfx.Text(g, customer.Name, Theme.FS(13f), Theme.Text, new RectangleF(av.Right + Theme.S(16), Theme.S(24), customerCard.Width - Theme.S(120), Theme.S(30)));
            Gfx.Text(g, string.IsNullOrEmpty(customer.Phone) ? "No phone on file" : customer.Phone, Theme.F(10f), Theme.TextMuted,
                new RectangleF(av.Right + Theme.S(16), Theme.S(54), customerCard.Width - Theme.S(120), Theme.S(26)));
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Theme.S(22), Theme.S(98), customerCard.Width - Theme.S(22), Theme.S(98));
            Gfx.Text(g, "Current balance owed", Theme.F(10f), Theme.TextMuted, new RectangleF(Theme.S(22), Theme.S(106), customerCard.Width - Theme.S(44), Theme.S(30)));
            Gfx.Text(g, Fmt.Money(customer.Balance), Theme.FS(11.5f), Theme.Text, new RectangleF(Theme.S(22), Theme.S(106), customerCard.Width - Theme.S(44), Theme.S(30)), StringAlignment.Far);
        }

        void Pay()
        {
            if (!payBtn.Enabled) return;
            var r = new PaymentInfo();
            if (Mode == 0) { r.Method = "Cash"; r.AmountGiven = Given; r.Change = Given - total; }
            else if (Mode == 1) { r.Method = "Card"; r.AmountGiven = total; r.Reference = refBox.Text; }
            else { r.Method = "Credit"; r.CustomerId = customer.CustomerId; r.AmountGiven = 0; }
            Result = r;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool OnNumericKey(string key) { if (Mode != 0) return false; Press(key); return true; }
        protected override void OnEnterKey() { Pay(); }
    }
}
