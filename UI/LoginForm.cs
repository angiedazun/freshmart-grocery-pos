using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI
{
    /// <summary>Full-screen touch login: pick a profile, tap in a PIN.</summary>
    public class LoginForm : Form
    {
        readonly bool lockMode;
        readonly List<UserAccount> users;
        readonly UserList userList = new UserList();
        readonly NumPad pad = new NumPad { ShowDecimal = false };
        readonly RoundButton signIn = new RoundButton { Text = "Sign in", Kind = BtnKind.Primary, Glyph = Icons.Check, Font = Theme.FS(13f), GlyphSize = 13f, Radius = 16 };
        readonly RoundButton exit = new RoundButton { Text = "Exit", Glyph = Icons.Power, Kind = BtnKind.Ghost, GlyphSize = 12f, Font = Theme.FS(10.5f) };
        readonly BufferedPanel card = new BufferedPanel { BackColor = Color.Transparent };
        readonly Timer clock = new Timer { Interval = 1000 };
        readonly Timer shake = new Timer { Interval = 25 };
        string pin = "";
        string error;
        int shakeStep;
        int selected = -1;

        const int MaxPin = 6;

        public LoginForm(bool lockMode)
        {
            this.lockMode = lockMode;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            ShowInTaskbar = lockMode ? false : true;
            Text = AppSettings.StoreName + " - Sign in";
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Theme.Sidebar;

            users = AuthService.GetUsers(true);
            userList.SetUsers(users);
            userList.SelectedChanged += i => { selected = i; pin = ""; error = null; card.Invalidate(); };

            pad.KeyPressed += k => Press(k);
            signIn.Click += (s, e) => TrySignIn();
            exit.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            exit.Visible = !lockMode;

            card.Paint += PaintCard;
            card.Controls.Add(userList);
            card.Controls.Add(pad);
            card.Controls.Add(signIn);
            Controls.Add(card);
            Controls.Add(exit);

            clock.Tick += (s, e) => card.Invalidate(new Rectangle(0, card.Height - Theme.S(110), Theme.S(420), Theme.S(110)));
            clock.Start();
            shake.Tick += (s, e) =>
            {
                shakeStep++;
                card.Left = baseLeft + (int)(Math.Sin(shakeStep * 1.4) * Theme.S(10) * (1 - shakeStep / 14.0));
                if (shakeStep >= 14) { shake.Stop(); card.Left = baseLeft; }
            };

            if (users.Count > 0) { selected = 0; userList.Selected = 0; }
        }

        int baseLeft;

        protected override void Dispose(bool disposing)
        {
            if (disposing) { clock.Dispose(); shake.Dispose(); }
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int w = Math.Min(Theme.S(1020), Width - Theme.S(60)), h = Math.Min(Theme.S(660), Height - Theme.S(60));
            baseLeft = (Width - w) / 2;
            card.SetBounds(baseLeft, (Height - h) / 2, w, h);
            exit.SetBounds(Width - Theme.S(130), Theme.S(24), Theme.S(106), Theme.S(46));

            int left = Theme.S(400);
            int rx = left + Theme.S(36);
            int rw = w - rx - Theme.S(40);
            int colUsers = Theme.S(250);
            userList.SetBounds(rx, Theme.S(120), colUsers, h - Theme.S(120) - Theme.S(40));
            int pinX = rx + colUsers + Theme.S(34);
            int pinW = w - pinX - Theme.S(40);
            pad.SetBounds(pinX, Theme.S(236), pinW, h - Theme.S(236) - Theme.S(40) - Theme.S(78));
            signIn.SetBounds(pinX, h - Theme.S(40) - Theme.S(62), pinW, Theme.S(62));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            using (var br = new LinearGradientBrush(ClientRectangle, Color.FromArgb(0x0B, 0x12, 0x20), Color.FromArgb(0x0F, 0x3D, 0x26), 35f))
                g.FillRectangle(br, ClientRectangle);
            Gfx.FillCircle(g, Color.FromArgb(24, 34, 197, 94), new RectangleF(-Theme.S(160), Height - Theme.S(340), Theme.S(520), Theme.S(520)));
            Gfx.FillCircle(g, Color.FromArgb(18, 34, 197, 94), new RectangleF(Width - Theme.S(380), -Theme.S(200), Theme.S(560), Theme.S(560)));
        }

        // ------------------------------------------------------------------ input
        void Press(string k)
        {
            if (k == "BACK") { if (pin.Length > 0) pin = pin.Substring(0, pin.Length - 1); }
            else if (k.Length == 1 && char.IsDigit(k[0]) && pin.Length < MaxPin) pin += k;
            error = null;
            card.Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (char.IsDigit(e.KeyChar)) { Press(e.KeyChar.ToString()); e.Handled = true; }
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back) { Press("BACK"); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { TrySignIn(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape && !lockMode) { DialogResult = DialogResult.Cancel; Close(); }
            base.OnKeyDown(e);
        }

        void TrySignIn()
        {
            if (selected < 0 || selected >= users.Count) { error = "Choose your profile first"; card.Invalidate(); return; }
            if (pin.Length < 4) { error = "Enter your 4-6 digit PIN"; card.Invalidate(); return; }
            UserAccount u = null;
            try { u = AuthService.Login(users[selected].UserId, pin); }
            catch (Exception ex) { error = "Database error: " + ex.Message; card.Invalidate(); return; }

            if (u == null)
            {
                error = "Incorrect PIN - try again";
                pin = "";
                shakeStep = 0;
                shake.Start();
                card.Invalidate();
                return;
            }
            Session.User = u;
            DialogResult = DialogResult.OK;
            Close();
        }

        // ------------------------------------------------------------------ painting
        void PaintCard(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            int w = card.Width, h = card.Height;
            var all = new RectangleF(0, 0, w - 1, h - 1);
            Gfx.FillRound(g, Color.White, all, Theme.S(28));

            // brand panel (left)
            int bw = Theme.S(400);
            var brand = new RectangleF(0, 0, bw, h - 1);
            var old = g.Clip;
            using (var p = Gfx.Round(all, Theme.S(28))) g.SetClip(p, CombineMode.Intersect);
            using (var br = new LinearGradientBrush(brand, Color.FromArgb(0x16, 0xA3, 0x4A), Color.FromArgb(0x06, 0x4E, 0x3B), 60f))
                g.FillRectangle(br, brand);
            Gfx.FillCircle(g, Color.FromArgb(28, 255, 255, 255), new RectangleF(-Theme.S(90), h - Theme.S(250), Theme.S(380), Theme.S(380)));
            Gfx.FillCircle(g, Color.FromArgb(22, 255, 255, 255), new RectangleF(bw - Theme.S(170), -Theme.S(110), Theme.S(300), Theme.S(300)));
            g.Clip = old;

            var logo = new RectangleF(Theme.S(44), Theme.S(48), Theme.S(70), Theme.S(70));
            Gfx.FillRound(g, Color.FromArgb(46, 255, 255, 255), logo, Theme.S(22));
            Gfx.Icon(g, Icons.Cart, 24f, Color.White, logo);
            Gfx.Text(g, AppSettings.StoreName, Theme.FB(26f), Color.White, new RectangleF(Theme.S(44), Theme.S(150), bw - Theme.S(80), Theme.S(110)), StringAlignment.Near, StringAlignment.Near, true);
            Gfx.Text(g, "Fast, friendly checkout.\nBuilt for the touch screen.", Theme.F(12f), Color.FromArgb(215, 255, 255, 255),
                new RectangleF(Theme.S(44), Theme.S(270), bw - Theme.S(80), Theme.S(70)), StringAlignment.Near, StringAlignment.Near, true);

            var now = DateTime.Now;
            Gfx.Text(g, now.ToString("HH:mm"), Theme.FB(34f), Color.White, new RectangleF(Theme.S(44), h - Theme.S(122), bw - Theme.S(80), Theme.S(56)));
            Gfx.Text(g, now.ToString("dddd, d MMMM yyyy"), Theme.F(11f), Color.FromArgb(210, 255, 255, 255), new RectangleF(Theme.S(44), h - Theme.S(68), bw - Theme.S(80), Theme.S(28)));

            // right side
            int rx = bw + Theme.S(36);
            Gfx.Text(g, lockMode ? "Screen locked" : "Welcome back", Theme.FB(22f), Theme.Text, new RectangleF(rx, Theme.S(34), w - rx - Theme.S(40), Theme.S(44)));
            Gfx.Text(g, "Choose your profile, then enter your PIN", Theme.F(10.5f), Theme.TextMuted, new RectangleF(rx, Theme.S(76), w - rx - Theme.S(40), Theme.S(28)));

            int pinX = rx + Theme.S(250) + Theme.S(34);
            int pinW = w - pinX - Theme.S(40);
            Gfx.Text(g, "PIN", Theme.FS(9.5f), Theme.TextMuted, new RectangleF(pinX, Theme.S(116), pinW, Theme.S(24)));

            // dots
            float dot = Theme.S(18), gap = Theme.S(16);
            float total = MaxPin * dot + (MaxPin - 1) * gap;
            float dx = pinX + (pinW - total) / 2f, dy = Theme.S(150);
            for (int i = 0; i < MaxPin; i++)
            {
                var r = new RectangleF(dx + i * (dot + gap), dy, dot, dot);
                if (i < pin.Length) Gfx.FillCircle(g, Theme.Primary, r);
                else { Gfx.FillCircle(g, Theme.Track, r); }
            }
            if (!string.IsNullOrEmpty(error))
                Gfx.Text(g, error, Theme.FS(9.5f), Theme.DangerDark, new RectangleF(pinX, Theme.S(186), pinW, Theme.S(30)), StringAlignment.Center);
            else if (selected >= 0 && selected < users.Count)
                Gfx.Text(g, "Signing in as " + users[selected].FullName, Theme.F(9.5f), Theme.TextMuted, new RectangleF(pinX, Theme.S(186), pinW, Theme.S(30)), StringAlignment.Center);
        }

        // ================================================================== user picker
        class UserList : TouchScrollControl
        {
            List<UserAccount> list = new List<UserAccount>();
            int sel = -1, rowH;
            public event Action<int> SelectedChanged;

            public UserList() { BackColor = Color.White; }

            public int Selected { get { return sel; } set { sel = value; Invalidate(); } }

            public void SetUsers(List<UserAccount> users) { list = users; OnRelayout(); Invalidate(); }

            protected override void OnRelayout()
            {
                rowH = Theme.S(88);
                SetContentHeight(list.Count * rowH);
            }

            protected override void OnTapAt(Point p)
            {
                int i = p.Y / rowH;
                if (i >= 0 && i < list.Count) { sel = i; Invalidate(); if (SelectedChanged != null) SelectedChanged(i); }
            }

            protected override void PaintContent(Graphics g, Rectangle visible)
            {
                for (int i = Math.Max(0, visible.Top / rowH); i < list.Count && i * rowH < visible.Bottom; i++)
                {
                    var u = list[i];
                    var r = new RectangleF(2, i * rowH + 3, Width - 14, rowH - 8);
                    bool on = i == sel;
                    Gfx.FillRound(g, on ? Theme.PrimarySoft : Theme.SurfaceAlt, r, Theme.S(18));
                    Gfx.StrokeRound(g, on ? Theme.Primary : Theme.Border, r, Theme.S(18), on ? 2f : 1f);
                    var av = new RectangleF(r.X + Theme.S(14), r.Y + (r.Height - Theme.S(52)) / 2f, Theme.S(52), Theme.S(52));
                    Gfx.FillCircle(g, on ? Theme.Primary : (u.IsAdmin ? Theme.InfoSoft : Theme.WarningSoft), av);
                    Gfx.Text(g, u.Initials, Theme.FB(12f), on ? Color.White : (u.IsAdmin ? Theme.Info : Theme.WarningDark), av, StringAlignment.Center);
                    Gfx.Text(g, u.FullName, Theme.FS(10.5f), Theme.Text, new RectangleF(av.Right + Theme.S(12), r.Y + Theme.S(14), r.Width - Theme.S(90), Theme.S(26)));
                    Gfx.Text(g, u.Role, Theme.F(9f), Theme.TextMuted, new RectangleF(av.Right + Theme.S(12), r.Y + Theme.S(40), r.Width - Theme.S(90), Theme.S(22)));
                }
            }
        }
    }
}
