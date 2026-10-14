using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    public enum InputMode { Text, Integer, Decimal }

    /// <summary>Rounded text field with optional leading icon, placeholder and focus ring.</summary>
    public class ModernTextBox : Control
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        const int EM_SETCUEBANNER = 0x1501;

        public readonly TextBox Inner = new TextBox();
        bool focused;
        string glyph, placeholder;

        public event EventHandler EnterPressed;

        public Color FillColor { get; set; } = Color.White;
        public InputMode Mode { get; set; } = InputMode.Text;

        public ModernTextBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.ContainerControl | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.F(11f);
            Height = Theme.S(50);
            Inner.BorderStyle = BorderStyle.None;
            Inner.Font = Font;
            Inner.BackColor = FillColor;
            Inner.ForeColor = Theme.Text;
            Inner.Enter += (s, e) => { focused = true; Invalidate(); };
            Inner.Leave += (s, e) => { focused = false; Invalidate(); };
            Inner.TextChanged += (s, e) => OnTextChanged(e);
            Inner.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && EnterPressed != null) { e.SuppressKeyPress = true; EnterPressed(this, EventArgs.Empty); }
            };
            Inner.KeyPress += (s, e) =>
            {
                if (char.IsControl(e.KeyChar)) return;
                if (Mode == InputMode.Integer && !char.IsDigit(e.KeyChar)) e.Handled = true;
                else if (Mode == InputMode.Decimal)
                {
                    bool ok = char.IsDigit(e.KeyChar) || (e.KeyChar == '.' && Inner.Text.IndexOf('.') < 0);
                    if (!ok) e.Handled = true;
                }
            };
            Inner.HandleCreated += (s, e) => ApplyPlaceholder();
            Controls.Add(Inner);
        }

        public string Glyph { get { return glyph; } set { glyph = value; DoLayout(); Invalidate(); } }
        public string Placeholder { get { return placeholder; } set { placeholder = value; ApplyPlaceholder(); } }
        public bool ReadOnly { get { return Inner.ReadOnly; } set { Inner.ReadOnly = value; } }
        public int MaxLength { get { return Inner.MaxLength; } set { Inner.MaxLength = value; } }
        public bool Password { get { return Inner.UseSystemPasswordChar; } set { Inner.UseSystemPasswordChar = value; } }

        public override string Text { get { return Inner.Text; } set { Inner.Text = value; } }
        public new bool Focus() { return Inner.Focus(); }
        public void SelectAll() { Inner.SelectAll(); }

        void ApplyPlaceholder()
        {
            if (Inner.IsHandleCreated && placeholder != null) SendMessage(Inner.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
        }

        protected override void OnFontChanged(EventArgs e) { Inner.Font = Font; DoLayout(); base.OnFontChanged(e); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); DoLayout(); }

        void DoLayout()
        {
            int left = Theme.S(16) + (string.IsNullOrEmpty(glyph) ? 0 : Theme.S(30));
            Inner.Left = left;
            Inner.Width = Math.Max(10, Width - left - Theme.S(16));
            Inner.Top = Math.Max(0, (Height - Inner.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            Inner.BackColor = FillColor;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Theme.S(14);
            Gfx.FillRound(g, FillColor, r, rad);
            Gfx.StrokeRound(g, focused ? Theme.Primary : Theme.Border, r, rad, focused ? 2f : 1f);
            if (!string.IsNullOrEmpty(glyph))
                Gfx.Icon(g, glyph, 12f, focused ? Theme.Primary : Theme.TextFaint, new RectangleF(Theme.S(12), 0, Theme.S(28), Height));
        }
    }
}
