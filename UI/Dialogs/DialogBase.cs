using System;
using System.Drawing;
using System.Windows.Forms;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Dialogs
{
    /// <summary>Borderless, rounded modal dialog that dims the window behind it.</summary>
    public class DialogBase : Form
    {
        protected readonly Panel Body;
        readonly string title, subtitle;
        readonly int headerH;
        Rectangle closeRect;
        bool closeHover;

        public DialogBase(string title, int width, int height, string subtitle = null)
        {
            this.title = title;
            this.subtitle = subtitle;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;
            BackColor = Color.White;
            DoubleBuffered = true;
            headerH = Theme.S(subtitle == null ? 68 : 86);
            ClientSize = new Size(Theme.S(width), Theme.S(height));
            Padding = new Padding(0, headerH, 0, 0);
            Body = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(Theme.S(28), Theme.S(8), Theme.S(28), Theme.S(26))
            };
            Controls.Add(Body);
            Font = Theme.F(10f);
        }

        protected void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (var p = Gfx.Round(new RectangleF(0, 0, Width, Height), Theme.S(22)))
                Region = new Region(p);
        }

        protected override void OnShown(EventArgs e) { ApplyRegion(); base.OnShown(e); }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int s = Theme.S(40);
            closeRect = new Rectangle(Width - s - Theme.S(20), Theme.S(18), s, s);
            ApplyRegion();
            Invalidate();
        }

        /// <summary>Override in keypad dialogs to receive physical keyboard digits. Return true when consumed.</summary>
        protected virtual bool OnNumericKey(string key) { return false; }
        protected virtual void OnEnterKey() { }

        bool TypingInField { get { return ActiveControl is TextBoxBase || ActiveControl is ComboBox; } }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!TypingInField && (char.IsDigit(e.KeyChar) || e.KeyChar == '.') && OnNumericKey(e.KeyChar.ToString())) e.Handled = true;
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.Handled = true; }
            else if (!TypingInField && e.KeyCode == Keys.Back && OnNumericKey("BACK")) e.Handled = true;
            else if (!TypingInField && e.KeyCode == Keys.Enter) { OnEnterKey(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool h = closeRect.Contains(e.Location);
            if (h != closeHover) { closeHover = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(closeRect); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (closeRect.Contains(e.Location)) { DialogResult = DialogResult.Cancel; Close(); }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            Gfx.Text(g, title, Theme.FB(16f), Theme.Text, new RectangleF(Theme.S(28), Theme.S(subtitle == null ? 0 : 12), Width - Theme.S(120), subtitle == null ? headerH : Theme.S(40)));
            if (subtitle != null)
                Gfx.Text(g, subtitle, Theme.F(10f), Theme.TextMuted, new RectangleF(Theme.S(28), Theme.S(50), Width - Theme.S(120), Theme.S(26)));
            Gfx.FillCircle(g, closeHover ? Theme.Track : Theme.SurfaceAlt, closeRect);
            Gfx.Icon(g, Icons.Close, 10f, Theme.TextMuted, closeRect);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, headerH - 1, Width, headerH - 1);
            using (var p = Gfx.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), Theme.S(22)))
            using (var pen = new Pen(Gfx.Alpha(Theme.TextFaint, 120))) g.DrawPath(pen, p);
        }

        /// <summary>Shows the dialog modally over <paramref name="owner"/> with a dimmed backdrop.</summary>
        public DialogResult ShowOver(Control owner)
        {
            Form top = owner == null ? null : owner.FindForm();
            ScrimForm scrim = null;
            try
            {
                if (top != null && top.Visible)
                {
                    scrim = new ScrimForm { Bounds = top.Bounds };
                    scrim.Show(top);
                }
                return ShowDialog((IWin32Window)scrim ?? top);
            }
            finally
            {
                if (scrim != null) { scrim.Close(); scrim.Dispose(); }
                if (top != null && !top.IsDisposed) top.Activate();
            }
        }

        class ScrimForm : Form
        {
            public ScrimForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                BackColor = Color.Black;
                Opacity = 0.5;
            }
            protected override bool ShowWithoutActivation { get { return true; } }
        }

        // ------- small helpers shared by dialogs -------

        /// <summary>A transparent fill panel inside the padded body, for absolutely positioned content.</summary>
        protected Panel AddCanvas()
        {
            var p = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.White };
            Body.Controls.Add(p);
            return p;
        }

        /// <summary>Bottom action row; buttons are given left-to-right and packed against the right edge.</summary>
        protected static Panel ButtonRow(params RoundButton[] buttons)
        {
            var row = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(56), BackColor = Color.White };
            foreach (var b in buttons) row.Controls.Add(b);
            EventHandler layout = (s, e) =>
            {
                int x = row.Width, gap = Theme.S(12);
                for (int i = buttons.Length - 1; i >= 0; i--)
                {
                    buttons[i].Height = row.Height;
                    buttons[i].Top = 0;
                    buttons[i].Left = x - buttons[i].Width;
                    x = buttons[i].Left - gap;
                }
            };
            row.Resize += layout;
            layout(row, EventArgs.Empty);
            return row;
        }

        protected Label MakeLabel(string text, float pt, Color color, bool bold = false, ContentAlignment align = ContentAlignment.TopLeft)
        {
            return new Label
            {
                Text = text, AutoSize = false, ForeColor = color, BackColor = Color.Transparent,
                Font = bold ? Theme.FS(pt) : Theme.F(pt), TextAlign = align
            };
        }

        /// <summary>A caption above an input, stacked in one panel.</summary>
        protected Panel Field(string caption, Control input, int inputHeight = 0)
        {
            int h = inputHeight > 0 ? inputHeight : Theme.S(50);
            var p = new Panel { Height = Theme.S(26) + h + Theme.S(12), Dock = DockStyle.Fill, Margin = new Padding(0, 0, Theme.S(14), 0), BackColor = Color.White };
            var lbl = new Label
            {
                Text = caption, Dock = DockStyle.Top, Height = Theme.S(26), ForeColor = Theme.TextMuted,
                Font = Theme.FS(9f), TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent
            };
            input.Dock = DockStyle.Top;
            input.Height = h;
            p.Controls.Add(input);
            p.Controls.Add(lbl);
            return p;
        }
    }

    public static class Msg
    {
        public static bool Confirm(Control owner, string title, string message, string yes = "Confirm", bool danger = false)
        {
            using (var d = new MessageDialog(title, message, yes, "Cancel", danger ? MessageKind.Danger : MessageKind.Question))
                return d.ShowOver(owner) == DialogResult.OK;
        }

        public static void Info(Control owner, string title, string message, bool error = false)
        {
            using (var d = new MessageDialog(title, message, "OK", null, error ? MessageKind.Danger : MessageKind.Info))
                d.ShowOver(owner);
        }
    }

    public enum MessageKind { Info, Question, Danger }

    public class MessageDialog : DialogBase
    {
        readonly MessageKind kind;

        public MessageDialog(string title, string message, string yes, string no, MessageKind kind)
            : base(title, 480, 300)
        {
            this.kind = kind;
            var lbl = new Label
            {
                Text = message, Dock = DockStyle.Fill, Font = Theme.F(11f), ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent, Padding = new Padding(Theme.S(70), Theme.S(6), 0, 0)
            };
            var ok = new RoundButton { Text = yes, Kind = kind == MessageKind.Danger && no != null ? BtnKind.Danger : BtnKind.Primary, Width = Theme.S(150) };
            ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            RoundButton cancel = null;
            if (no != null)
            {
                cancel = new RoundButton { Text = no, Kind = BtnKind.Secondary, Width = Theme.S(130) };
                cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            }
            var row = cancel == null ? ButtonRow(ok) : ButtonRow(cancel, ok);
            Body.Controls.Add(lbl);
            Body.Controls.Add(row);
            Body.Paint += PaintIcon;
        }

        void PaintIcon(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            Color c = kind == MessageKind.Danger ? Theme.Danger : kind == MessageKind.Question ? Theme.Warning : Theme.Info;
            var ic = new RectangleF(Theme.S(28), Theme.S(10), Theme.S(48), Theme.S(48));
            Gfx.FillCircle(g, Gfx.Lighten(c, .82f), ic);
            Gfx.Icon(g, kind == MessageKind.Info ? Icons.Check : Icons.Warning, 15f, c, ic);
        }
    }
}
