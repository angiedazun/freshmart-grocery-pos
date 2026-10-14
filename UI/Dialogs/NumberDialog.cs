using System;
using System.Drawing;
using System.Windows.Forms;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Dialogs
{
    /// <summary>Keypad dialog for entering a quantity or any whole/decimal number.</summary>
    public class NumberDialog : DialogBase
    {
        readonly ValueBox box = new ValueBox();
        readonly NumPad pad = new NumPad();
        readonly RoundButton ok;
        readonly bool allowDecimal;
        readonly decimal max;
        string buf = "";

        public decimal Value { get; private set; }

        public NumberDialog(string title, string caption, decimal initial = 0, bool allowDecimal = false, decimal max = 999999, string subtitle = null)
            : base(title, 420, 640, subtitle)
        {
            this.allowDecimal = allowDecimal;
            this.max = max;
            if (initial > 0) buf = initial.ToString(allowDecimal ? "0.##" : "0", System.Globalization.CultureInfo.InvariantCulture);

            box.Caption = caption;
            box.Dock = DockStyle.Top;
            pad.ShowDecimal = allowDecimal;
            pad.Dock = DockStyle.Top;
            pad.Height = Theme.S(340);
            pad.KeyPressed += k => Press(k);

            ok = new RoundButton { Text = "Confirm", Kind = BtnKind.Primary, Width = Theme.S(364) };
            ok.Click += (s, e) => Commit();
            var row = ButtonRow(ok);

            Body.Controls.Add(row);
            Body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = Theme.S(14), BackColor = Color.White });
            Body.Controls.Add(pad);
            Body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = Theme.S(14), BackColor = Color.White });
            Body.Controls.Add(box);
            Body.Controls.SetChildIndex(row, 0);
            Refresh_();
        }

        void Press(string k)
        {
            buf = NumKeys.Apply(buf, k, allowDecimal);
            Refresh_();
        }

        void Refresh_()
        {
            box.Value = buf;
            decimal v = NumKeys.Parse(buf);
            box.Accent = v > max ? Theme.Danger : Theme.Primary;
            ok.Enabled = buf.Length > 0 && v <= max;
        }

        void Commit()
        {
            if (!ok.Enabled) return;
            Value = NumKeys.Parse(buf);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool OnNumericKey(string key) { Press(key); return true; }
        protected override void OnEnterKey() { Commit(); }
    }
}
