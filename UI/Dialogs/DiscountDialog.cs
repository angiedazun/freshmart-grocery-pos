using System;
using System.Drawing;
using System.Windows.Forms;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Dialogs
{
    /// <summary>Apply a percentage or fixed-amount discount to the current order.</summary>
    public class DiscountDialog : DialogBase
    {
        readonly decimal subtotal;
        readonly SegmentedControl seg = new SegmentedControl();
        readonly ValueBox box = new ValueBox();
        readonly NumPad pad = new NumPad();
        readonly SurfacePanel preview = new SurfacePanel { Fill = Theme.SurfaceAlt };
        readonly RoundButton apply;
        string buf = "";

        public bool IsPercent { get; private set; }
        public decimal Value { get; private set; }

        public DiscountDialog(decimal subtotal, bool isPercent, decimal current)
            : base("Apply discount", 880, 620, "Order subtotal " + Fmt.Money(subtotal))
        {
            this.subtotal = subtotal;
            if (current > 0) buf = current.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            // left column
            var left = new Panel { Dock = DockStyle.Left, Width = Theme.S(400), BackColor = Color.White };
            seg.Items = new[] { "Percentage  %", "Amount  " + AppSettings.Currency };
            seg.SelectedIndex = isPercent ? 0 : 1;
            seg.SetBounds(0, 0, Theme.S(400), Theme.S(54));
            seg.SelectedChanged += (s, e) => { buf = ""; Update_(); BuildQuick(); };

            box.SetBounds(0, Theme.S(72), Theme.S(400), Theme.S(92));

            preview.SetBounds(0, Theme.S(180), Theme.S(400), Theme.S(104));
            preview.Paint += PaintPreview;

            quick.SetBounds(0, Theme.S(300), Theme.S(400), Theme.S(120));
            left.Controls.Add(seg);
            left.Controls.Add(box);
            left.Controls.Add(preview);
            left.Controls.Add(quick);

            // right column
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(Theme.S(30), 0, 0, 0) };
            pad.ShowDecimal = true;
            pad.Dock = DockStyle.Top;
            pad.Height = Theme.S(344);
            pad.KeyPressed += k => Press(k);
            right.Controls.Add(pad);

            apply = new RoundButton { Text = "Apply discount", Kind = BtnKind.Primary, Width = Theme.S(220), Glyph = Icons.Tag };
            apply.Click += (s, e) => Commit(false);
            var remove = new RoundButton { Text = "Remove", Kind = BtnKind.DangerSoft, Width = Theme.S(150) };
            remove.Click += (s, e) => Commit(true);
            var cancel = new RoundButton { Text = "Cancel", Kind = BtnKind.Secondary, Width = Theme.S(130) };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            var row = current > 0 ? ButtonRow(remove, cancel, apply) : ButtonRow(cancel, apply);

            Body.Controls.Add(right);
            Body.Controls.Add(left);
            Body.Controls.Add(row);
            BuildQuick();
            Update_();
        }

        readonly FlowLayoutPanel quick = new FlowLayoutPanel { BackColor = Color.White, WrapContents = true };

        void BuildQuick()
        {
            quick.Controls.Clear();
            string[] vals = seg.SelectedIndex == 0 ? new[] { "5", "10", "15", "20", "25", "50" } : new[] { "50", "100", "200", "500", "1000", "2000" };
            foreach (var v in vals)
            {
                var b = new RoundButton
                {
                    Text = seg.SelectedIndex == 0 ? v + "%" : v, Kind = BtnKind.Soft, Width = Theme.S(112), Height = Theme.S(52),
                    Margin = new Padding(0, 0, Theme.S(14), Theme.S(12))
                };
                string captured = v;
                b.Click += (s, e) => { buf = captured; Update_(); };
                quick.Controls.Add(b);
            }
        }

        decimal Discount
        {
            get
            {
                decimal v = NumKeys.Parse(buf);
                decimal d = seg.SelectedIndex == 0 ? Math.Round(subtotal * Math.Min(v, 100m) / 100m, 2) : v;
                return Math.Min(d, subtotal);
            }
        }

        bool Valid
        {
            get
            {
                decimal v = NumKeys.Parse(buf);
                return buf.Length > 0 && v > 0 && (seg.SelectedIndex == 0 ? v <= 100m : v <= subtotal);
            }
        }

        void Press(string k) { buf = NumKeys.Apply(buf, k, true, 8); Update_(); }

        void Update_()
        {
            box.Caption = seg.SelectedIndex == 0 ? "Discount percentage" : "Discount amount";
            box.Value = buf;
            box.Prefix = seg.SelectedIndex == 1 ? AppSettings.Currency + " " : "";
            box.Suffix = seg.SelectedIndex == 0 ? " %" : "";
            box.Accent = buf.Length > 0 && !Valid ? Theme.Danger : Theme.Primary;
            if (apply != null) apply.Enabled = Valid;
            preview.Invalidate();
        }

        void PaintPreview(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            decimal d = Valid ? Discount : 0m;
            float x = Theme.S(20), w = preview.Width - Theme.S(40);
            Gfx.Text(g, "Discount", Theme.F(10f), Theme.TextMuted, new RectangleF(x, Theme.S(12), w, Theme.S(24)));
            Gfx.Text(g, "-" + Fmt.Money(d), Theme.FS(11f), Theme.DangerDark, new RectangleF(x, Theme.S(12), w, Theme.S(24)), StringAlignment.Far);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, x, Theme.S(48), x + w, Theme.S(48));
            Gfx.Text(g, "New subtotal", Theme.FS(10.5f), Theme.Text, new RectangleF(x, Theme.S(54), w, Theme.S(36)));
            Gfx.Text(g, Fmt.Money(subtotal - d), Theme.FB(14f), Theme.PrimaryDark, new RectangleF(x, Theme.S(54), w, Theme.S(36)), StringAlignment.Far);
        }

        void Commit(bool remove)
        {
            if (!remove && !Valid) return;
            IsPercent = seg.SelectedIndex == 0;
            Value = remove ? 0m : NumKeys.Parse(buf);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool OnNumericKey(string key) { Press(key); return true; }
        protected override void OnEnterKey() { if (apply.Enabled) Commit(false); }
    }
}
