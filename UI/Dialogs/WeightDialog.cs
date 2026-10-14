using System;
using System.Drawing;
using System.Windows.Forms;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Dialogs
{
    /// <summary>Weigh-in screen for items sold by weight: type grams or kilograms, see the price live.</summary>
    public class WeightDialog : DialogBase
    {
        readonly decimal pricePerKg;
        readonly int maxGrams;
        readonly SegmentedControl unit = new SegmentedControl();
        readonly ValueBox box = new ValueBox();
        readonly NumPad pad = new NumPad();
        readonly SurfacePanel preview = new SurfacePanel { Fill = Theme.SurfaceAlt };
        readonly FlowLayoutPanel quick = new FlowLayoutPanel { BackColor = Color.White, WrapContents = true };
        readonly RoundButton add;
        string buf = "";

        /// <summary>The confirmed weight in grams.</summary>
        public int Grams { get; private set; }

        bool Kg { get { return unit.SelectedIndex == 1; } }

        public WeightDialog(string name, decimal pricePerKg, int maxGrams, int initialGrams)
            : base("Weigh  -  " + name, 880, 640, Fmt.Money(pricePerKg) + " per kg   ·   " + Fmt.Weight(maxGrams) + " available")
        {
            this.pricePerKg = pricePerKg;
            this.maxGrams = maxGrams;
            if (initialGrams > 0) buf = initialGrams.ToString();

            var left = new Panel { Dock = DockStyle.Left, Width = Theme.S(400), BackColor = Color.White };
            unit.Items = new[] { "Grams  (g)", "Kilograms  (kg)" };
            unit.SetBounds(0, 0, Theme.S(400), Theme.S(54));
            unit.SelectedChanged += (s, e) => UnitChanged();

            box.SetBounds(0, Theme.S(70), Theme.S(400), Theme.S(92));
            box.Caption = "Weight on the scale";

            quick.SetBounds(0, Theme.S(176), Theme.S(400), Theme.S(124));
            string[] labels = { "100 g", "250 g", "500 g", "750 g", "1 kg", "2 kg" };
            int[] grams = { 100, 250, 500, 750, 1000, 2000 };
            for (int i = 0; i < labels.Length; i++)
            {
                int g = grams[i];
                var b = new RoundButton { Text = labels[i], Kind = BtnKind.Soft, Width = Theme.S(120), Height = Theme.S(52), Margin = new Padding(0, 0, Theme.S(10), Theme.S(10)) };
                b.Click += (s, e) => SetGrams(g);
                quick.Controls.Add(b);
            }

            preview.SetBounds(0, Theme.S(312), Theme.S(400), Theme.S(124));
            preview.Paint += PaintPreview;

            left.Controls.Add(unit);
            left.Controls.Add(box);
            left.Controls.Add(quick);
            left.Controls.Add(preview);

            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(Theme.S(30), 0, 0, 0) };
            pad.Dock = DockStyle.Top;
            pad.Height = Theme.S(360);
            pad.KeyPressed += k => Press(k);
            right.Controls.Add(pad);

            add = new RoundButton { Text = "Add to bill", Kind = BtnKind.Primary, Glyph = Icons.Cart, Width = Theme.S(220), Enabled = false };
            add.Click += (s, e) => Commit();
            var cancel = new RoundButton { Text = "Cancel", Kind = BtnKind.Secondary, Width = Theme.S(130) };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Body.Controls.Add(right);
            Body.Controls.Add(left);
            Body.Controls.Add(ButtonRow(cancel, add));
            pad.ShowDecimal = false;
            box.Suffix = " g";
            Refresh_();
        }

        int CurrentGrams
        {
            get
            {
                decimal v = NumKeys.Parse(buf);
                return (int)Math.Round(Kg ? v * 1000m : v);
            }
        }

        void SetGrams(int g)
        {
            buf = Kg ? (g / 1000m).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : g.ToString();
            Refresh_();
        }

        void UnitChanged()
        {
            // keep the same physical weight when switching units
            int g = 0;
            if (buf.Length > 0) { decimal v = NumKeys.Parse(buf); g = (int)Math.Round(Kg ? v : v * 1000m); }
            // (buf still holds the old unit's text; Kg already reflects the NEW unit, so reinterpret accordingly)
            pad.ShowDecimal = Kg;
            pad.Invalidate();
            box.Suffix = Kg ? " kg" : " g";
            buf = g <= 0 ? "" : (Kg ? (g / 1000m).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : g.ToString());
            Refresh_();
        }

        void Press(string k)
        {
            buf = NumKeys.Apply(buf, k, Kg, Kg ? 7 : 6, 3);
            Refresh_();
        }

        bool Valid { get { int g = CurrentGrams; return g > 0 && g <= maxGrams; } }

        void Refresh_()
        {
            box.Value = buf;
            box.Accent = buf.Length > 0 && !Valid ? Theme.Danger : Theme.Primary;
            if (add != null) add.Enabled = Valid;
            preview.Invalidate();
        }

        void PaintPreview(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            int grams = CurrentGrams;
            bool over = grams > maxGrams;
            decimal amount = Math.Round(pricePerKg * grams / 1000m, 2);
            float x = Theme.S(22), w = preview.Width - Theme.S(44);

            Gfx.Text(g, "Weight", Theme.F(10f), Theme.TextMuted, new RectangleF(x, Theme.S(14), w, Theme.S(24)));
            Gfx.Text(g, grams > 0 ? Fmt.Weight(grams) : "-", Theme.FS(11.5f), over ? Theme.DangerDark : Theme.Text, new RectangleF(x, Theme.S(14), w, Theme.S(24)), StringAlignment.Far);
            Gfx.Text(g, "Price per kg", Theme.F(10f), Theme.TextMuted, new RectangleF(x, Theme.S(40), w, Theme.S(24)));
            Gfx.Text(g, Fmt.Money(pricePerKg), Theme.FS(11.5f), Theme.Text, new RectangleF(x, Theme.S(40), w, Theme.S(24)), StringAlignment.Far);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, x, Theme.S(70), x + w, Theme.S(70));

            if (over)
                Gfx.Text(g, "Only " + Fmt.Weight(maxGrams) + " in stock", Theme.FS(11f), Theme.DangerDark, new RectangleF(x, Theme.S(78), w, Theme.S(36)));
            else
            {
                Gfx.Text(g, "Amount", Theme.FS(11.5f), Theme.Text, new RectangleF(x, Theme.S(78), w, Theme.S(40)));
                Gfx.Text(g, Fmt.Money(amount), Theme.FB(19f), Theme.PrimaryDark, new RectangleF(x, Theme.S(78), w, Theme.S(40)), StringAlignment.Far);
            }
        }

        void Commit()
        {
            if (!Valid) return;
            Grams = CurrentGrams;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool OnNumericKey(string key) { Press(key); return true; }
        protected override void OnEnterKey() { Commit(); }
    }
}
