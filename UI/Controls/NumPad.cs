using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>On-screen numeric keypad. Raises KeyPressed with "0".."9", ".", "00", "BACK".</summary>
    public class NumPad : Control
    {
        static readonly string[] Keys = { "7", "8", "9", "4", "5", "6", "1", "2", "3", null, "0", "BACK" };
        int pressed = -1;

        public bool ShowDecimal { get; set; } = true;
        public event Action<string> KeyPressed;

        public NumPad()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Size = new Size(Theme.S(300), Theme.S(340));
        }

        string KeyAt(int i)
        {
            if (Keys[i] != null) return Keys[i];
            return ShowDecimal ? "." : "00";
        }

        RectangleF CellRect(int i)
        {
            float gap = Theme.S(10);
            float w = (Width - gap * 2) / 3f, h = (Height - gap * 3) / 4f;
            int c = i % 3, r = i / 3;
            return new RectangleF(c * (w + gap), r * (h + gap), w, h);
        }

        int Hit(Point p)
        {
            for (int i = 0; i < 12; i++) if (CellRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            for (int i = 0; i < 12; i++)
            {
                var r = CellRect(i);
                string k = KeyAt(i);
                bool back = k == "BACK";
                bool isDown = i == pressed;
                Color bg = back ? (isDown ? Gfx.Darken(Theme.DangerSoft, .06f) : Theme.DangerSoft)
                                : (isDown ? Theme.PrimarySoft : Color.White);
                Gfx.FillRound(g, bg, r, Theme.S(14));
                if (!back) Gfx.StrokeRound(g, isDown ? Theme.Primary : Theme.Border, r, Theme.S(14));
                if (back) Gfx.Icon(g, Icons.Backspace, 15f, Theme.DangerDark, r);
                else Gfx.Text(g, k, Theme.FS(k.Length > 1 ? 17f : 20f), Theme.Text, r, StringAlignment.Center);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = Hit(e.Location); Invalidate(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int hit = Hit(e.Location);
            int was = pressed;
            pressed = -1;
            Invalidate();
            if (hit >= 0 && hit == was && KeyPressed != null) KeyPressed(KeyAt(hit));
        }

        protected override void OnMouseLeave(EventArgs e) { pressed = -1; Invalidate(); base.OnMouseLeave(e); }
    }
}
