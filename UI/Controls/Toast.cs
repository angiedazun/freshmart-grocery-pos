using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    public enum ToastKind { Info, Success, Warning, Error }

    /// <summary>Small transient message pill shown at the top of the window.</summary>
    public static class Toast
    {
        public static void Show(Control host, string message, ToastKind kind = ToastKind.Info)
        {
            var form = host == null ? null : host.FindForm();
            if (form == null) return;

            foreach (Control c in form.Controls)
                if (c is ToastControl) { var old = c; form.Controls.Remove(old); old.Dispose(); break; }

            var t = new ToastControl(message, kind);
            int y = Theme.S(88);
            t.Location = new Point((form.ClientSize.Width - t.Width) / 2, y);
            form.Controls.Add(t);
            t.BringToFront();
        }

        class ToastControl : Control
        {
            readonly string msg;
            readonly ToastKind kind;
            readonly Timer timer = new Timer { Interval = 2400 };

            public ToastControl(string message, ToastKind k)
            {
                msg = message; kind = k;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Font = Theme.FS(10.5f);
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    var sz = Gfx.Measure(g, message, Font);
                    Size = new Size((int)sz.Width + Theme.S(92), Theme.S(54));
                }
                timer.Tick += (s, e) => { timer.Stop(); if (Parent != null) Parent.Controls.Remove(this); Dispose(); };
                timer.Start();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) timer.Dispose();
                base.Dispose(disposing);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Gfx.SmoothOn(g);
                Color accent = kind == ToastKind.Success ? Theme.Primary : kind == ToastKind.Error ? Theme.Danger
                             : kind == ToastKind.Warning ? Theme.Warning : Theme.Info;
                string glyph = kind == ToastKind.Success ? Icons.Check : kind == ToastKind.Info ? Icons.Tag : Icons.Warning;

                var r = new RectangleF(4, 2, Width - 8, Height - 8);
                Gfx.Shadow(g, r, r.Height / 2f, 5, 14, 3);
                Gfx.FillRound(g, Color.FromArgb(0x0F, 0x17, 0x2A), r, r.Height / 2f);
                var ic = new RectangleF(r.X + Theme.S(10), r.Y + (r.Height - Theme.S(30)) / 2f, Theme.S(30), Theme.S(30));
                Gfx.FillCircle(g, accent, ic);
                Gfx.Icon(g, glyph, 10f, Color.White, ic);
                Gfx.Text(g, msg, Font, Color.White, new RectangleF(ic.Right + Theme.S(12), r.Y, r.Width - Theme.S(60), r.Height));
            }
        }
    }
}
