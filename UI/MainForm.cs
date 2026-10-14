using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;
using GroceryPOS.UI.Dialogs;
using GroceryPOS.UI.Panels;

namespace GroceryPOS.UI
{
    public interface IRefreshable { void RefreshData(); }

    /// <summary>Application shell: slim icon sidebar, header bar and the active screen.</summary>
    public class MainForm : Form
    {
        class NavItem
        {
            public string Title, Subtitle, Glyph, Nav;
            public bool AdminOnly, Allowed = true;
            public NavButton Button;
            public Control Panel;
            public Func<Control> Factory;
        }

        readonly List<NavItem> items = new List<NavItem>();
        readonly NavItem dashItem, posItem, lowItem;
        readonly BufferedPanel sidebar = new BufferedPanel { BackColor = Color.White };
        readonly BufferedPanel header = new BufferedPanel { BackColor = Color.White };
        readonly Panel content = new Panel { BackColor = Theme.Bg };
        readonly RoundButton lockBtn = new RoundButton { Text = "Lock", Glyph = Icons.Lock, Kind = BtnKind.Secondary, GlyphSize = 11f, Font = Theme.FS(10f) };
        readonly NavButton signOutBtn = new NavButton { Text = "Sign out", Glyph = Icons.Power };
        readonly Timer clock = new Timer { Interval = 1000 };
        NavItem current;

        public bool SignOutRequested { get; private set; }
        PosPanel Pos { get { return (PosPanel)posItem.Panel; } }

        public MainForm()
        {
            Text = AppSettings.StoreName + " - POS";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(Theme.S(1100), Theme.S(680));
            BackColor = Theme.Bg;
            Font = Theme.F(10f);
            KeyPreview = true;
            DoubleBuffered = true;

            dashItem = Add("Dashboard", "Dashboard", "Overview of today's trading and what needs attention", Icons.Home, false, MakeDashboard);
            posItem = Add("POS Terminal", "POS", "Ring up sales - tap a product or scan a barcode", Icons.Cart, false, MakePos);
            Add("Product Inventory", "Inventory", "Manage your catalogue, prices and stock levels", Icons.Box, true, () => new InventoryPanel());
            lowItem = Add("Low Stock Alerts", "Low Stock", "Items at or below their reorder level", Icons.Warning, false, () => new LowStockPanel());
            Add("Sales & Reports", "Reports", "Transaction history and the end-of-shift Z-report", Icons.Chart, false, () => new ReportsPanel());
            Add("System Settings", "Settings", "Store details, staff accounts and categories", Icons.Settings, true, () => new SettingsPanel());

            sidebar.Controls.Add(signOutBtn);
            signOutBtn.BackColor = Color.White;
            signOutBtn.Click += (s, e) => SignOut();
            sidebar.Paint += PaintSidebar;

            header.Controls.Add(lockBtn);
            lockBtn.Click += (s, e) => LockScreen();
            header.Paint += PaintHeader;

            Controls.Add(content);
            Controls.Add(header);
            Controls.Add(sidebar);

            clock.Tick += (s, e) => header.Invalidate(new Rectangle(header.Width - Theme.S(700), 0, Theme.S(700), header.Height));
            clock.Start();

            ApplyRole();
            DoLayout();
            Navigate(Session.User != null && Session.User.IsAdmin ? dashItem : posItem);
        }

        NavItem Add(string title, string nav, string subtitle, string glyph, bool adminOnly, Func<Control> factory)
        {
            var item = new NavItem { Title = title, Nav = nav, Subtitle = subtitle, Glyph = glyph, AdminOnly = adminOnly, Factory = factory };
            item.Button = new NavButton { Text = nav, Glyph = glyph };
            item.Button.Click += (s, e) => Navigate(item);
            sidebar.Controls.Add(item.Button);
            items.Add(item);
            return item;
        }

        Control MakePos()
        {
            var p = new PosPanel();
            p.StockChanged += RefreshBadge;
            return p;
        }

        Control MakeDashboard()
        {
            var d = new DashboardPanel();
            d.NewSaleRequested += () => Navigate(posItem);
            d.ViewLowStockRequested += () => Navigate(lowItem);
            return d;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (current == posItem) Pos.FocusSearch();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) clock.Dispose();
            base.Dispose(disposing);
        }

        // ------------------------------------------------------------------ navigation
        void ApplyRole()
        {
            bool admin = Session.User != null && Session.User.IsAdmin;
            foreach (var it in items) { it.Allowed = !it.AdminOnly || admin; it.Button.Visible = it.Allowed; }
            DoLayout();
            if (current != null && !current.Allowed) Navigate(posItem);
            else if (current != null && current.Panel is DashboardPanel) ((IRefreshable)current.Panel).RefreshData();
            header.Invalidate();
            sidebar.Invalidate();
        }

        public void Navigate(int index) { Navigate(items[index]); }

        void Navigate(NavItem item)
        {
            if (item.Panel == null)
            {
                item.Panel = item.Factory();
                item.Panel.Dock = DockStyle.Fill;
                item.Panel.Visible = false;
                content.Controls.Add(item.Panel);
            }
            foreach (var it in items)
            {
                it.Button.Active = it == item;
                if (it.Panel != null && it != item) it.Panel.Visible = false;
            }
            current = item;
            item.Panel.Visible = true;
            item.Panel.BringToFront();

            var posPanel = item.Panel as PosPanel;
            if (posPanel != null) { posPanel.Reload(); posPanel.FocusSearch(); }
            else
            {
                var r = item.Panel as IRefreshable;
                if (r != null) r.RefreshData();
            }
            RefreshBadge();
            header.Invalidate();
        }

        public void RefreshBadge()
        {
            try { lowItem.Button.Badge = ProductService.LowStockCount(); } catch { }
        }

        // ------------------------------------------------------------------ session
        void LockScreen()
        {
            using (var login = new LoginForm(true))
            {
                if (login.ShowDialog(this) == DialogResult.OK)
                {
                    ApplyRole();
                    Toast.Show(this, "Welcome back, " + Session.User.FullName, ToastKind.Success);
                }
            }
        }

        void SignOut()
        {
            if (posItem.Panel != null && Pos.HasOpenOrder &&
                !Msg.Confirm(this, "Sign out?", "There is an order in progress. Signing out will discard the open cart.", "Sign out", true))
                return;
            SignOutRequested = true;
            Close();
        }

        // ------------------------------------------------------------------ keyboard
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            // A USB barcode scanner "types" digits: route them to the POS search box when nothing else is focused.
            if (current == posItem && !(ActiveControl is TextBoxBase) && !char.IsControl(e.KeyChar))
            {
                Pos.CaptureKey(e.KeyChar);
                e.Handled = true;
            }
            base.OnKeyPress(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (current == posItem)
            {
                if (keyData == Keys.F2) { Pos.FocusSearch(); return true; }
                if (keyData == Keys.F12) { Pos.Checkout(); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ layout + paint
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        void DoLayout()
        {
            int sw = Theme.S(96), hh = Theme.S(78), pad = Theme.S(22);
            sidebar.SetBounds(0, 0, sw, ClientSize.Height);
            header.SetBounds(sw, 0, ClientSize.Width - sw, hh);
            content.SetBounds(sw, hh, ClientSize.Width - sw, ClientSize.Height - hh);
            content.Padding = new Padding(pad, pad - Theme.S(4), pad, pad);

            int y = Theme.S(96), nh = Theme.S(72), m = Theme.S(10);
            foreach (var it in items)
            {
                if (!it.Allowed) continue;
                it.Button.SetBounds(m, y, sw - m * 2, nh);
                y += nh + Theme.S(4);
            }
            signOutBtn.SetBounds(m, sidebar.Height - Theme.S(86), sw - m * 2, Theme.S(72));
            lockBtn.SetBounds(header.Width - Theme.S(24) - Theme.S(104), (hh - Theme.S(46)) / 2, Theme.S(104), Theme.S(46));
        }

        void PaintSidebar(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            var logo = new RectangleF((sidebar.Width - Theme.S(52)) / 2f, Theme.S(18), Theme.S(52), Theme.S(52));
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(logo, Color.FromArgb(0x22, 0xC5, 0x5E), Color.FromArgb(0x15, 0x80, 0x3D), 45f))
                Gfx.FillRound(g, br, logo, Theme.S(16));
            Gfx.Icon(g, Icons.Cart, 17f, Color.White, logo);
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);

            int pad = Theme.S(28);
            float ty = (header.Height - Theme.S(60)) / 2f;
            if (current != null)
            {
                Gfx.Text(g, current.Title, Theme.FB(17f), Theme.Text, new RectangleF(pad, ty, header.Width * 0.4f, Theme.S(34)));
                Gfx.Text(g, current.Subtitle, Theme.F(9.5f), Theme.TextMuted, new RectangleF(pad, ty + Theme.S(34), header.Width * 0.4f, Theme.S(24)));
            }

            float rx = lockBtn.Left - Theme.S(18);
            var u = Session.User;
            if (u != null)
            {
                float nameW = Math.Max(Gfx.Measure(g, u.FullName, Theme.FS(10f)).Width, Gfx.Measure(g, u.Role, Theme.F(8.5f)).Width);
                float w = nameW + Theme.S(70);
                var chip = new RectangleF(rx - w, (header.Height - Theme.S(52)) / 2f, w, Theme.S(52));
                Gfx.FillRound(g, Theme.SurfaceAlt, chip, Theme.S(26));
                Gfx.StrokeRound(g, Theme.Border, chip, Theme.S(26));
                var av = new RectangleF(chip.X + Theme.S(6), chip.Y + Theme.S(6), Theme.S(40), Theme.S(40));
                Gfx.FillCircle(g, Theme.Primary, av);
                Gfx.Text(g, u.Initials, Theme.FB(9.5f), Color.White, av, StringAlignment.Center);
                Gfx.Text(g, u.FullName, Theme.FS(10f), Theme.Text, new RectangleF(av.Right + Theme.S(10), chip.Y + Theme.S(7), nameW + 8, Theme.S(22)));
                Gfx.Text(g, u.Role, Theme.F(8.5f), Theme.TextMuted, new RectangleF(av.Right + Theme.S(10), chip.Y + Theme.S(27), nameW + 8, Theme.S(18)));
                rx = chip.X - Theme.S(24);
            }

            var now = DateTime.Now;
            Gfx.Text(g, now.ToString("HH:mm"), Theme.FB(17f), Theme.Text, new RectangleF(rx - Theme.S(180), ty, Theme.S(180), Theme.S(34)), StringAlignment.Far);
            Gfx.Text(g, now.ToString("ddd, d MMM yyyy"), Theme.F(9.5f), Theme.TextMuted, new RectangleF(rx - Theme.S(180), ty + Theme.S(34), Theme.S(180), Theme.S(24)), StringAlignment.Far);
        }
    }
}
