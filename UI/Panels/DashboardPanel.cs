using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Panels
{
    /// <summary>Home screen: KPIs, sales trends, payment mix, categories, best sellers, stock health and staff performance.</summary>
    public class DashboardPanel : Panel, IRefreshable
    {
        readonly SurfacePanel banner = new SurfacePanel { Fill = Theme.Primary, Stroke = Color.Empty, Radius = 22 };
        readonly RoundButton newSale = new RoundButton { Text = "New sale", Glyph = Icons.Cart, Kind = BtnKind.Secondary, GlyphSize = 13f, Font = Theme.FS(11.5f), Radius = 16 };
        readonly StatCard[] kpi =
        {
            new StatCard { Title = "Today's sales", Glyph = Icons.Chart, Accent = Theme.Primary },
            new StatCard { Title = "Orders", Glyph = Icons.Document, Accent = Theme.Info },
            new StatCard { Title = "Items sold", Glyph = Icons.Box, Accent = Color.FromArgb(0x8B, 0x5C, 0xF6) },
            new StatCard { Title = "Average basket", Glyph = Icons.Cart, Accent = Theme.Warning },
            new StatCard { Title = "Gross profit (est.)", Glyph = Icons.Cash, Accent = Color.FromArgb(0x14, 0xB8, 0xA6) },
            new StatCard { Title = "Stock value (cost)", Glyph = Icons.Library, Accent = Color.FromArgb(0x0E, 0xA5, 0xE9) },
            new StatCard { Title = "Credit owed", Glyph = Icons.People, Accent = Color.FromArgb(0xEC, 0x48, 0x99) },
            new StatCard { Title = "Needs restocking", Glyph = Icons.Warning, Accent = Theme.Danger }
        };
        readonly SurfacePanel chartCard = new SurfacePanel();
        readonly SurfacePanel donutCard = new SurfacePanel();
        readonly SurfacePanel hourlyCard = new SurfacePanel();
        readonly SurfacePanel catCard = new SurfacePanel();
        readonly SurfacePanel topCard = new SurfacePanel();
        readonly SurfacePanel lowCard = new SurfacePanel();
        readonly SurfacePanel recentCard = new SurfacePanel();
        readonly SurfacePanel staffCard = new SurfacePanel();
        readonly SurfacePanel stockCard = new SurfacePanel();

        List<DayPoint> daily = new List<DayPoint>();
        decimal[] mix = new decimal[3];
        decimal[] hourly = new decimal[24];
        List<CategorySale> cats = new List<CategorySale>();
        List<TopProduct> top = new List<TopProduct>();
        List<Product> low = new List<Product>();
        List<RecentSale> recent = new List<RecentSale>();
        List<StaffSale> staff = new List<StaffSale>();
        int lowTotal, outTotal, inStock, lowOnly, productCount;
        decimal costValue, retailValue;
        string greeting = "", summary = "";
        RectangleF viewAllRect;
        readonly Color[] kpiColors = new Color[8];

        public event Action NewSaleRequested, ViewLowStockRequested;

        static readonly Color CashC = Theme.Primary, CardC = Theme.Info, CreditC = Theme.Warning;

        bool IsAdmin { get { return Session.User != null && Session.User.IsAdmin; } }

        public DashboardPanel()
        {
            BackColor = Theme.Bg;
            AutoScroll = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
            MouseEnter += (s, e) => Focus();

            banner.Paint += PaintBanner;
            newSale.Click += (s, e) => { if (NewSaleRequested != null) NewSaleRequested(); };
            banner.Controls.Add(newSale);
            Controls.Add(banner);
            foreach (var k in kpi) Controls.Add(k);

            chartCard.Paint += PaintChart;
            donutCard.Paint += PaintDonut;
            hourlyCard.Paint += PaintHourly;
            catCard.Paint += PaintCategories;
            topCard.Paint += PaintTop;
            lowCard.Paint += PaintLow;
            recentCard.Paint += PaintRecent;
            staffCard.Paint += PaintStaff;
            stockCard.Paint += PaintStock;
            lowCard.MouseMove += (s, e) => lowCard.Cursor = viewAllRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            lowCard.MouseUp += (s, e) => { if (viewAllRect.Contains(e.Location) && ViewLowStockRequested != null) ViewLowStockRequested(); };
            foreach (var c in new[] { chartCard, donutCard, hourlyCard, catCard, topCard, lowCard, recentCard, staffCard, stockCard }) Controls.Add(c);
        }

        // ------------------------------------------------------------------ data
        public void RefreshData()
        {
            var u = Session.User;
            if (u == null) return;
            int? uid = u.IsAdmin ? (int?)null : u.UserId;
            try
            {
                daily = DashboardService.Daily(7, uid);
                mix = DashboardService.PaymentMix(7, uid);
                hourly = DashboardService.Hourly(uid);
                cats = DashboardService.ByCategory(7, 6, uid);
                top = DashboardService.Top(7, 5, uid);
                recent = DashboardService.Recent(5, uid);
                staff = u.IsAdmin ? DashboardService.Staff(7) : new List<StaffSale>();

                var products = ProductService.GetProducts();
                productCount = products.Count;
                var lows = products.Where(p => p.Stock <= p.MinStock).OrderBy(p => (double)p.Stock / Math.Max(1, p.MinStock)).ThenBy(p => p.Name).ToList();
                lowTotal = lows.Count;
                outTotal = lows.Count(p => p.IsOut);
                lowOnly = lowTotal - outTotal;
                inStock = productCount - lowTotal;
                low = lows.Take(5).ToList();
                costValue = products.Sum(p => p.StockValue);
                retailValue = products.Sum(p => p.Stock <= 0 ? 0m : (p.IsWeighed ? p.Price * p.Stock / 1000m : p.Price * p.Stock));

                var z = SaleService.GetZReport(DateTime.Today, uid, "");
                var today = daily[daily.Count - 1];
                var yest = daily[daily.Count - 2];

                Set(0, Fmt.Money(today.Total), Delta(today.Total, yest.Total, ref kpiColors[0]), kpiColors[0], daily.Select(d => d.Total).ToArray());
                Set(1, today.Orders.ToString(), Delta(today.Orders, yest.Orders, ref kpiColors[1]), kpiColors[1], daily.Select(d => (decimal)d.Orders).ToArray());
                Set(2, z.ItemsSold.ToString(), "Units sold today", Theme.TextFaint, null);
                Set(3, Fmt.Money(z.AverageBasket), "Per order today", Theme.TextFaint, daily.Select(d => d.Orders == 0 ? 0m : d.Total / d.Orders).ToArray());

                if (u.IsAdmin)
                {
                    decimal profit = DashboardService.ProfitToday(null) - z.Discounts;
                    decimal margin = z.NetSales > 0 ? profit / z.NetSales * 100m : 0m;
                    Set(4, Fmt.Money(profit), z.NetSales > 0 ? margin.ToString("0.#") + "% margin today" : "No sales yet", profit >= 0 ? Theme.PrimaryDark : Theme.DangerDark, null);
                    Set(5, Fmt.Money(costValue), productCount + " active products", Theme.TextFaint, null);
                    int owing;
                    decimal owed = DashboardService.CreditOwed(out owing);
                    Set(6, Fmt.Money(owed), owing == 0 ? "No outstanding credit" : "Owed by " + owing + (owing == 1 ? " customer" : " customers"), owing == 0 ? Theme.TextFaint : Theme.WarningDark, null);
                    Set(7, lowTotal + (lowTotal == 1 ? " item" : " items"), outTotal == 0 ? "None out of stock" : outTotal + " out of stock", outTotal == 0 ? Theme.TextFaint : Theme.DangerDark, null);
                }

                int h = DateTime.Now.Hour;
                greeting = (h < 12 ? "Good morning, " : h < 17 ? "Good afternoon, " : "Good evening, ") + u.FullName;
                summary = today.Orders == 0
                    ? "No sales yet today - the till is ready when you are."
                    : Fmt.Money(today.Total) + " from " + today.Orders + (today.Orders == 1 ? " order" : " orders") + " so far today.";
                if (lowTotal > 0) summary += "   " + lowTotal + (lowTotal == 1 ? " item needs" : " items need") + " restocking.";
            }
            catch (Exception ex)
            {
                greeting = "Dashboard unavailable"; summary = ex.Message;
            }
            DoLayout();
            foreach (Control c in Controls) c.Invalidate();
        }

        void Set(int i, string value, string caption, Color captionColor, decimal[] spark)
        {
            kpi[i].Value = value; kpi[i].Caption = caption; kpi[i].CaptionColor = captionColor; kpi[i].Spark = spark;
        }

        static string Delta(decimal now, decimal before, ref Color color)
        {
            if (before == 0) { color = now > 0 ? Theme.PrimaryDark : Theme.TextFaint; return now > 0 ? "New vs yesterday" : "No sales yet"; }
            decimal pct = (now - before) / before * 100m;
            color = pct >= 0 ? Theme.PrimaryDark : Theme.DangerDark;
            return (pct >= 0 ? "▲ " : "▼ ") + Math.Abs(pct).ToString("0.#") + "% vs yesterday";
        }

        // ------------------------------------------------------------------ layout
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        int Grid(Control[] cs, int cols, int y, int h, int W, int gap)
        {
            int cw = (W - gap * (cols - 1)) / cols;
            for (int i = 0; i < cs.Length; i++)
                cs[i].SetBounds((i % cols) * (cw + gap), y + (i / cols) * (h + gap), cw, h);
            int rows = (cs.Length + cols - 1) / cols;
            return y + rows * (h + gap);
        }

        int Split(Control a, Control b, int y, int h, int W, int gap, double leftShare)
        {
            if (W >= Theme.S(900))
            {
                int cw = (int)((W - gap) * leftShare);
                a.SetBounds(0, y, cw, h);
                b.SetBounds(cw + gap, y, W - cw - gap, h);
                return y + h + gap;
            }
            return Grid(new[] { a, b }, 1, y, h, W, gap);
        }

        void DoLayout()
        {
            if (Width < 100) return;
            AutoScrollPosition = Point.Empty;
            bool admin = IsAdmin;
            int W = Width - SystemInformation.VerticalScrollBarWidth - Theme.S(4), gap = Theme.S(18);
            int y = 0;

            banner.SetBounds(0, y, W, Theme.S(100));
            newSale.SetBounds(W - Theme.S(170) - Theme.S(26), (banner.Height - Theme.S(54)) / 2, Theme.S(170), Theme.S(54));
            y += banner.Height + gap;

            for (int i = 0; i < kpi.Length; i++) kpi[i].Visible = admin || i < 4;
            y = Grid(admin ? kpi : kpi.Take(4).ToArray(), W >= Theme.S(900) ? 4 : 2, y, Theme.S(106), W, gap);

            y = Split(chartCard, donutCard, y, Theme.S(332), W, gap, 0.62);
            y = Split(hourlyCard, catCard, y, Theme.S(332), W, gap, 0.62);

            int cols = W >= Theme.S(1000) ? 3 : W >= Theme.S(680) ? 2 : 1;
            y = Grid(new Control[] { topCard, lowCard, recentCard }, cols, y, Theme.S(334), W, gap);

            staffCard.Visible = stockCard.Visible = admin;
            if (admin) y = Split(staffCard, stockCard, y, Theme.S(332), W, gap, 0.5);

            AutoScrollMinSize = new Size(0, y - gap + Theme.S(4));
        }

        // ------------------------------------------------------------------ painting helpers
        static void CardHeader(Graphics g, Control card, string title, string sub)
        {
            Gfx.Text(g, title, Theme.FB(12f), Theme.Text, new RectangleF(Theme.S(24), Theme.S(16), card.Width - Theme.S(48), Theme.S(26)));
            if (sub != null)
                Gfx.Text(g, sub, Theme.F(9f), Theme.TextMuted, new RectangleF(Theme.S(24), Theme.S(41), card.Width - Theme.S(48), Theme.S(20)));
        }

        static void Tile(Graphics g, string icon, string colorHex, string name, RectangleF r)
        {
            Color cc = Theme.Hex(colorHex ?? "#16A34A");
            Gfx.FillRound(g, Gfx.Lighten(cc, .85f), r, Theme.S(11));
            if (!string.IsNullOrEmpty(icon)) Gfx.Text(g, icon, Theme.Emoji(14f), Gfx.Darken(cc, .12f), r, StringAlignment.Center);
            else Gfx.Text(g, ProductGrid.Initials(name), Theme.FB(10f), Gfx.Darken(cc, .1f), r, StringAlignment.Center);
        }

        static void PillRight(Graphics g, string text, Color bg, Color fg, float right, float cy)
        {
            var f = Theme.FS(8.5f);
            float w = Gfx.Measure(g, text, f).Width + Theme.S(20);
            var r = new RectangleF(right - w, cy - Theme.S(13), w, Theme.S(26));
            Gfx.FillRound(g, bg, r, Theme.S(13));
            Gfx.Text(g, text, f, fg, r, StringAlignment.Center);
        }

        static void Empty(Graphics g, Control card, string glyph, string title, string sub)
        {
            var ic = new RectangleF(card.Width / 2f - Theme.S(34), card.Height / 2f - Theme.S(52), Theme.S(68), Theme.S(68));
            Gfx.FillCircle(g, Theme.SurfaceAlt, ic);
            Gfx.Icon(g, glyph, 20f, Theme.TextFaint, ic);
            Gfx.Text(g, title, Theme.FS(11f), Theme.TextMuted, new RectangleF(0, ic.Bottom + Theme.S(6), card.Width, Theme.S(26)), StringAlignment.Center);
            Gfx.Text(g, sub, Theme.F(9f), Theme.TextFaint, new RectangleF(Theme.S(20), ic.Bottom + Theme.S(30), card.Width - Theme.S(40), Theme.S(22)), StringAlignment.Center);
        }

        static string Short(decimal v)
        {
            if (v >= 1000000m) return (v / 1000000m).ToString("0.#") + "M";
            if (v >= 1000m) return (v / 1000m).ToString("0.#") + "k";
            return v.ToString("0");
        }

        static decimal NiceMax(decimal m)
        {
            if (m <= 0) return 1000m;
            double e = Math.Pow(10, Math.Floor(Math.Log10((double)m)));
            double f = (double)m / e;
            double nf = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
            return (decimal)(nf * e);
        }

        static string HourLabel(int h)
        {
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return h12 + (h < 12 ? " AM" : " PM");
        }

        static void GridLines(Graphics g, decimal nice, float left, float right, float topY, float bottom)
        {
            using (var pen = new Pen(Theme.Border) { DashStyle = DashStyle.Dash })
                for (int i = 0; i <= 4; i++)
                {
                    float y = bottom - (bottom - topY) * i / 4f;
                    g.DrawLine(pen, left, y, right, y);
                    Gfx.Text(g, Short(nice * i / 4m), Theme.F(8.5f), Theme.TextFaint, new RectangleF(Theme.S(10), y - Theme.S(10), left - Theme.S(18), Theme.S(20)), StringAlignment.Far);
                }
        }

        // ------------------------------------------------------------------ banner
        void PaintBanner(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = new RectangleF(0, 0, banner.Width, banner.Height);
            using (var clip = Gfx.Round(new RectangleF(0.5f, 0.5f, banner.Width - 1.5f, banner.Height - 1.5f), Theme.S(22)))
            {
                var old = g.Clip;
                g.SetClip(clip, CombineMode.Intersect);
                using (var br = new LinearGradientBrush(r, Color.FromArgb(0x16, 0xA3, 0x4A), Color.FromArgb(0x06, 0x5F, 0x46), 20f))
                    g.FillRectangle(br, r);
                Gfx.FillCircle(g, Color.FromArgb(26, 255, 255, 255), new RectangleF(banner.Width - Theme.S(330), -Theme.S(120), Theme.S(300), Theme.S(300)));
                Gfx.FillCircle(g, Color.FromArgb(20, 255, 255, 255), new RectangleF(banner.Width - Theme.S(520), Theme.S(30), Theme.S(240), Theme.S(240)));
                g.Clip = old;
            }
            float tw = banner.Width - Theme.S(260);
            Gfx.Text(g, greeting, Theme.FB(19f), Color.White, new RectangleF(Theme.S(30), Theme.S(16), tw, Theme.S(38)));
            Gfx.Text(g, summary, Theme.F(10.5f), Color.FromArgb(225, 255, 255, 255), new RectangleF(Theme.S(30), Theme.S(54), tw, Theme.S(28)));
        }

        // ------------------------------------------------------------------ 7-day sales bars
        void PaintChart(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            decimal sum = daily.Sum(d => d.Total);
            CardHeader(g, chartCard, "Sales overview", "Last 7 days  ·  " + Fmt.Money(sum) + " total");

            float left = Theme.S(64), right = chartCard.Width - Theme.S(26), topY = Theme.S(92), bottom = chartCard.Height - Theme.S(50);
            decimal nice = NiceMax(daily.Count == 0 ? 0 : daily.Max(d => d.Total));
            GridLines(g, nice, left, right, topY, bottom);
            if (daily.Count == 0) return;

            float slot = (right - left) / daily.Count, barW = Math.Min(slot * 0.5f, Theme.S(54));
            for (int i = 0; i < daily.Count; i++)
            {
                var d = daily[i];
                bool isToday = d.Day == DateTime.Today;
                float h = (bottom - topY) * (float)(d.Total / nice);
                if (d.Total > 0 && h < Theme.S(5)) h = Theme.S(5);
                float x = left + slot * i + (slot - barW) / 2f;
                if (d.Total > 0)
                {
                    var old = g.Clip;
                    g.SetClip(new RectangleF(x - 1, topY - Theme.S(30), barW + 2, bottom - topY + Theme.S(30)));
                    Color c = isToday ? Theme.Primary : Color.FromArgb(0xBB, 0xF1, 0xCC);
                    Gfx.FillRound(g, c, new RectangleF(x, bottom - h, barW, h + Theme.S(12)), Theme.S(9));
                    g.Clip = old;
                    Gfx.Text(g, Short(d.Total), isToday ? Theme.FB(9f) : Theme.F(8.5f), isToday ? Theme.PrimaryDark : Theme.TextMuted,
                        new RectangleF(x - Theme.S(20), bottom - h - Theme.S(24), barW + Theme.S(40), Theme.S(20)), StringAlignment.Center);
                }
                else Gfx.FillRound(g, Theme.Track, new RectangleF(x, bottom - Theme.S(4), barW, Theme.S(4)), Theme.S(2));
                Gfx.Text(g, isToday ? "Today" : d.Day.ToString("ddd"), isToday ? Theme.FS(9f) : Theme.F(9f), isToday ? Theme.Text : Theme.TextMuted,
                    new RectangleF(left + slot * i, bottom + Theme.S(8), slot, Theme.S(22)), StringAlignment.Center);
                Gfx.Text(g, d.Day.ToString("d MMM"), Theme.F(7.5f), Theme.TextFaint,
                    new RectangleF(left + slot * i, bottom + Theme.S(26), slot, Theme.S(18)), StringAlignment.Center);
            }
            if (sum == 0)
                Gfx.Text(g, "No sales recorded in the last 7 days", Theme.FS(10.5f), Theme.TextMuted,
                    new RectangleF(left, topY, right - left, bottom - topY), StringAlignment.Center);
        }

        // ------------------------------------------------------------------ today by hour (area chart)
        void PaintHourly(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            decimal tot = hourly.Sum();
            int first = 24, last = -1, peak = 0;
            for (int h = 0; h < 24; h++)
            {
                if (hourly[h] > 0) { first = Math.Min(first, h); last = Math.Max(last, h); }
                if (hourly[h] > hourly[peak]) peak = h;
            }
            int start = Math.Min(7, first < 24 ? first : 7), end = Math.Max(21, last);
            CardHeader(g, hourlyCard, "Sales by hour", tot == 0 ? "Today  ·  no sales yet" : "Today  ·  busiest hour " + HourLabel(peak) + " (" + Fmt.Money(hourly[peak]) + ")");

            float left = Theme.S(64), right = hourlyCard.Width - Theme.S(30), topY = Theme.S(92), bottom = hourlyCard.Height - Theme.S(46);
            decimal nice = NiceMax(hourly.Max());
            GridLines(g, nice, left, right, topY, bottom);

            int n = end - start + 1;
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
                pts[i] = new PointF(left + (right - left) * (i + 0.5f) / n, bottom - (bottom - topY) * (float)(hourly[start + i] / nice));

            // x labels every 2 hours + current-hour marker
            for (int i = 0; i < n; i++)
                if ((start + i) % 2 == 1 || i == 0)
                    Gfx.Text(g, HourLabel(start + i).Replace(" ", ""), Theme.F(8.5f), Theme.TextMuted, new RectangleF(pts[i].X - Theme.S(26), bottom + Theme.S(8), Theme.S(52), Theme.S(20)), StringAlignment.Center);
            int nowH = DateTime.Now.Hour;
            if (nowH >= start && nowH <= end)
            {
                float nx = pts[nowH - start].X;
                using (var pen = new Pen(Gfx.Alpha(Theme.Info, 120)) { DashStyle = DashStyle.Dot }) g.DrawLine(pen, nx, topY - Theme.S(4), nx, bottom);
                Gfx.Text(g, "Now", Theme.FS(8f), Theme.Info, new RectangleF(nx - Theme.S(24), topY - Theme.S(22), Theme.S(48), Theme.S(16)), StringAlignment.Center);
            }

            if (tot == 0)
            {
                Gfx.Text(g, "Sales will plot here as the day goes on", Theme.FS(10.5f), Theme.TextMuted, new RectangleF(left, topY, right - left, bottom - topY), StringAlignment.Center);
                return;
            }

            using (var area = new GraphicsPath())
            {
                area.AddLines(pts);
                area.AddLine(pts[n - 1].X, bottom, pts[0].X, bottom);
                area.CloseFigure();
                using (var br = new LinearGradientBrush(new RectangleF(left, topY, right - left, bottom - topY + 1), Gfx.Alpha(Theme.Primary, 80), Gfx.Alpha(Theme.Primary, 0), 90f))
                    g.FillPath(br, area);
            }
            using (var pen = new Pen(Theme.Primary, 2.5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(pen, pts);
            for (int i = 0; i < n; i++)
                if (hourly[start + i] > 0)
                {
                    bool isPeak = start + i == peak;
                    float rr = isPeak ? 6f : 4f;
                    Gfx.FillCircle(g, Color.White, new RectangleF(pts[i].X - rr - 1.5f, pts[i].Y - rr - 1.5f, rr * 2 + 3, rr * 2 + 3));
                    Gfx.FillCircle(g, isPeak ? Theme.PrimaryDark : Theme.Primary, new RectangleF(pts[i].X - rr, pts[i].Y - rr, rr * 2, rr * 2));
                    if (isPeak) Gfx.Text(g, Short(hourly[start + i]), Theme.FB(9f), Theme.PrimaryDark, new RectangleF(pts[i].X - Theme.S(30), pts[i].Y - Theme.S(30), Theme.S(60), Theme.S(18)), StringAlignment.Center);
                }
        }

        // ------------------------------------------------------------------ payment donut
        void PaintDonut(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            CardHeader(g, donutCard, "Payment methods", "Share of sales  ·  last 7 days");
            decimal total = mix.Sum();
            float d = Math.Min(Theme.S(150), donutCard.Width - Theme.S(60)), th = Theme.S(22);
            var ring = new RectangleF((donutCard.Width - d) / 2f + th / 2f, Theme.S(72) + th / 2f, d - th, d - th);
            using (var track = new Pen(Theme.Track, th))
                g.DrawEllipse(track, ring);

            Color[] cols = { CashC, CardC, CreditC };
            if (total > 0)
            {
                int parts = mix.Count(m => m > 0);
                float start = -90f;
                for (int i = 0; i < 3; i++)
                {
                    if (mix[i] <= 0) continue;
                    float sweep = 360f * (float)(mix[i] / total);
                    float gap = parts > 1 ? 3f : 0f;
                    using (var pen = new Pen(cols[i], th) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                        g.DrawArc(pen, ring, start + gap / 2f, Math.Max(0.5f, sweep - gap));
                    start += sweep;
                }
            }
            Gfx.Text(g, total > 0 ? "Total" : "No sales", Theme.F(8.5f), Theme.TextMuted, new RectangleF(ring.X, ring.Y + ring.Height * 0.28f, ring.Width, Theme.S(18)), StringAlignment.Center);
            if (total > 0)
                Gfx.Text(g, Short(total), Theme.FB(15f), Theme.Text, new RectangleF(ring.X, ring.Y + ring.Height * 0.42f, ring.Width, Theme.S(30)), StringAlignment.Center);

            string[] names = { "Cash", "Card", "Credit" };
            float y = Theme.S(72) + d + Theme.S(22), x = Theme.S(24), w = donutCard.Width - Theme.S(48);
            for (int i = 0; i < 3; i++)
            {
                float ry = y + i * Theme.S(30);
                Gfx.FillCircle(g, cols[i], new RectangleF(x, ry + Theme.S(8), Theme.S(10), Theme.S(10)));
                Gfx.Text(g, names[i], Theme.F(10f), Theme.Text, new RectangleF(x + Theme.S(20), ry, w, Theme.S(26)));
                Gfx.Text(g, total > 0 ? (mix[i] / total * 100m).ToString("0") + "%" : "-", Theme.F(9.5f), Theme.TextMuted, new RectangleF(x, ry, w, Theme.S(26)), StringAlignment.Far);
                Gfx.Text(g, Fmt.Money(mix[i]), Theme.FS(10f), Theme.Text, new RectangleF(x, ry, w - Theme.S(54), Theme.S(26)), StringAlignment.Far);
            }
        }

        // ------------------------------------------------------------------ sales by category
        void PaintCategories(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            CardHeader(g, catCard, "Sales by category", "Revenue share  ·  last 7 days");
            if (cats.Count == 0) { Empty(g, catCard, Icons.Library, "No sales yet", "Category revenue will appear here"); return; }
            decimal total = cats.Sum(c => c.Amount), max = cats.Max(c => c.Amount);
            if (total <= 0) total = 1;
            if (max <= 0) max = 1;
            for (int i = 0; i < cats.Count; i++)
            {
                var c = cats[i];
                float y = Theme.S(80 + i * 41), x = Theme.S(24), w = catCard.Width - Theme.S(48);
                Color cc = Theme.Hex(c.Color);
                Gfx.FillCircle(g, cc, new RectangleF(x, y + Theme.S(7), Theme.S(10), Theme.S(10)));
                Gfx.Text(g, c.Name, Theme.FS(10f), Theme.Text, new RectangleF(x + Theme.S(20), y, w, Theme.S(24)));
                Gfx.Text(g, Fmt.Money(c.Amount) + "  ·  " + (c.Amount / total * 100m).ToString("0") + "%", Theme.F(9.5f), Theme.TextMuted, new RectangleF(x, y, w, Theme.S(24)), StringAlignment.Far);
                Gfx.FillRound(g, Theme.Track, new RectangleF(x, y + Theme.S(26), w, Theme.S(8)), Theme.S(4));
                Gfx.FillRound(g, cc, new RectangleF(x, y + Theme.S(26), Math.Max(Theme.S(8), w * (float)(c.Amount / max)), Theme.S(8)), Theme.S(4));
            }
        }

        // ------------------------------------------------------------------ lists
        const int RowH = 50, ListTop = 78;

        void PaintTop(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            CardHeader(g, topCard, "Top products", "Best sellers  ·  last 7 days");
            if (top.Count == 0) { Empty(g, topCard, Icons.Box, "No sales yet", "Best sellers will appear here"); return; }
            decimal max = top.Max(t => t.Revenue);
            if (max <= 0) max = 1;
            for (int i = 0; i < top.Count; i++)
            {
                var t = top[i];
                float y = Theme.S(ListTop + i * RowH), x = Theme.S(24), w = topCard.Width - Theme.S(48);
                Tile(g, t.Icon, t.Color, t.Name, new RectangleF(x, y, Theme.S(38), Theme.S(38)));
                float tx = x + Theme.S(50), rw = Theme.S(78);
                Gfx.Text(g, t.Name, Theme.FS(10f), Theme.Text, new RectangleF(tx, y - Theme.S(2), w - Theme.S(50) - rw, Theme.S(22)));
                Gfx.Text(g, Fmt.Qty(t.Weighed, t.Qty) + " sold", Theme.FS(9.5f), Theme.Text, new RectangleF(x, y - Theme.S(2), w, Theme.S(22)), StringAlignment.Far);
                float bw = w - Theme.S(50) - rw;
                Gfx.FillRound(g, Theme.Track, new RectangleF(tx, y + Theme.S(25), bw, Theme.S(8)), Theme.S(4));
                Gfx.FillRound(g, Theme.Hex(t.Color), new RectangleF(tx, y + Theme.S(25), Math.Max(Theme.S(8), bw * (float)(t.Revenue / max)), Theme.S(8)), Theme.S(4));
                Gfx.Text(g, Fmt.Money(t.Revenue), Theme.F(8.5f), Theme.TextMuted, new RectangleF(x, y + Theme.S(20), w, Theme.S(20)), StringAlignment.Far);
            }
        }

        void PaintLow(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            CardHeader(g, lowCard, "Low stock", lowTotal == 0 ? "Everything is well stocked" : lowTotal + " item" + (lowTotal == 1 ? "" : "s") + " need attention");
            if (lowTotal > 0)
            {
                var f = Theme.FS(9.5f);
                float tw = Gfx.Measure(g, "View all", f).Width;
                viewAllRect = new RectangleF(lowCard.Width - Theme.S(24) - tw - Theme.S(10), Theme.S(16), tw + Theme.S(20), Theme.S(28));
                Gfx.Text(g, "View all", f, Theme.PrimaryDark, viewAllRect, StringAlignment.Center);
            }
            else viewAllRect = RectangleF.Empty;

            if (low.Count == 0)
            {
                var ic = new RectangleF(lowCard.Width / 2f - Theme.S(34), lowCard.Height / 2f - Theme.S(52), Theme.S(68), Theme.S(68));
                Gfx.FillCircle(g, Theme.PrimarySoft, ic);
                Gfx.Icon(g, Icons.Check, 20f, Theme.Primary, ic);
                Gfx.Text(g, "All stocked up", Theme.FS(11f), Theme.TextMuted, new RectangleF(0, ic.Bottom + Theme.S(6), lowCard.Width, Theme.S(26)), StringAlignment.Center);
                return;
            }
            for (int i = 0; i < low.Count; i++)
            {
                var p = low[i];
                float y = Theme.S(ListTop + i * RowH), x = Theme.S(24), w = lowCard.Width - Theme.S(48);
                Tile(g, p.Icon, p.CategoryColor, p.Name, new RectangleF(x, y, Theme.S(38), Theme.S(38)));
                Gfx.Text(g, p.Name, Theme.FS(10f), Theme.Text, new RectangleF(x + Theme.S(50), y - Theme.S(2), w - Theme.S(150), Theme.S(22)));
                Gfx.Text(g, "Alert at " + p.MinText + "  ·  " + p.CategoryName, Theme.F(8.5f), Theme.TextMuted, new RectangleF(x + Theme.S(50), y + Theme.S(19), w - Theme.S(150), Theme.S(20)));
                if (p.IsOut) PillRight(g, "Out of stock", Theme.DangerSoft, Theme.DangerDark, x + w, y + Theme.S(19));
                else PillRight(g, p.StockText + " left", Theme.WarningSoft, Theme.WarningDark, x + w, y + Theme.S(19));
            }
        }

        void PaintRecent(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            CardHeader(g, recentCard, "Recent sales", "Latest transactions");
            if (recent.Count == 0) { Empty(g, recentCard, Icons.Document, "No transactions yet", "Completed sales will show up here"); return; }
            for (int i = 0; i < recent.Count; i++)
            {
                var r = recent[i];
                float y = Theme.S(ListTop + i * RowH - 4), x = Theme.S(24), w = recentCard.Width - Theme.S(48);
                bool voided = r.Status == "Voided";
                Color mc = r.Method == "Cash" ? CashC : r.Method == "Card" ? CardC : CreditC;
                string glyph = r.Method == "Cash" ? Icons.Cash : r.Method == "Card" ? Icons.Card : Icons.People;
                var tile = new RectangleF(x, y, Theme.S(38), Theme.S(38));
                Gfx.FillRound(g, Gfx.Lighten(mc, .85f), tile, Theme.S(11));
                Gfx.Icon(g, glyph, 11f, mc, tile);
                Gfx.Text(g, r.InvoiceNo, Theme.FS(10f), voided ? Theme.TextFaint : Theme.Text, new RectangleF(x + Theme.S(50), y - Theme.S(2), w - Theme.S(150), Theme.S(22)));
                Gfx.Text(g, r.When.ToString("HH:mm") + "  ·  " + r.Cashier, Theme.F(8.5f), Theme.TextMuted, new RectangleF(x + Theme.S(50), y + Theme.S(19), w - Theme.S(150), Theme.S(20)));
                Gfx.Text(g, Fmt.Money(r.Total), Theme.FS(10f), voided ? Theme.TextFaint : Theme.Text, new RectangleF(x, y - Theme.S(2), w, Theme.S(22)), StringAlignment.Far);
                Gfx.Text(g, voided ? "Voided" : r.Method, Theme.F(8.5f), voided ? Theme.DangerDark : Theme.TextMuted, new RectangleF(x, y + Theme.S(19), w, Theme.S(20)), StringAlignment.Far);
            }
        }

        // ------------------------------------------------------------------ staff performance (admin)
        void PaintStaff(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            CardHeader(g, staffCard, "Staff performance", "Sales by cashier  ·  last 7 days");
            if (staff.Count == 0) { Empty(g, staffCard, Icons.People, "No sales yet", "Cashier results will appear here"); return; }
            decimal max = staff.Max(s => s.Amount);
            if (max <= 0) max = 1;
            Color[] pal = { Theme.Primary, Theme.Info, Color.FromArgb(0x8B, 0x5C, 0xF6), Theme.Warning, Color.FromArgb(0xEC, 0x48, 0x99) };
            for (int i = 0; i < staff.Count; i++)
            {
                var s = staff[i];
                float y = Theme.S(78 + i * 50), x = Theme.S(24), w = staffCard.Width - Theme.S(48);
                Color c = pal[i % pal.Length];
                var av = new RectangleF(x, y, Theme.S(40), Theme.S(40));
                Gfx.FillCircle(g, Gfx.Lighten(c, .82f), av);
                var parts = s.Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length > 1 ? (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpper() : s.Name.Substring(0, 1).ToUpper();
                Gfx.Text(g, ini, Theme.FB(9.5f), Gfx.Darken(c, .15f), av, StringAlignment.Center);
                float tx = x + Theme.S(54), bw = w - Theme.S(54);
                Gfx.Text(g, s.Name, Theme.FS(10f), Theme.Text, new RectangleF(tx, y - Theme.S(2), bw - Theme.S(120), Theme.S(22)));
                Gfx.Text(g, Fmt.Money(s.Amount), Theme.FS(10f), Theme.Text, new RectangleF(tx, y - Theme.S(2), bw, Theme.S(22)), StringAlignment.Far);
                Gfx.Text(g, s.Orders + (s.Orders == 1 ? " order" : " orders") + "  ·  avg " + Fmt.Money(s.Amount / s.Orders), Theme.F(8.5f), Theme.TextMuted, new RectangleF(tx, y + Theme.S(18), bw, Theme.S(18)));
                Gfx.FillRound(g, Theme.Track, new RectangleF(tx, y + Theme.S(36), bw, Theme.S(7)), Theme.S(3));
                Gfx.FillRound(g, c, new RectangleF(tx, y + Theme.S(36), Math.Max(Theme.S(7), bw * (float)(s.Amount / max)), Theme.S(7)), Theme.S(3));
            }
        }

        // ------------------------------------------------------------------ stock health (admin)
        void PaintStock(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            CardHeader(g, stockCard, "Stock health", productCount + " active products tracked");
            float x = Theme.S(24), w = stockCard.Width - Theme.S(48);
            int total = Math.Max(1, productCount);

            // stacked bar
            var bar = new RectangleF(x, Theme.S(80), w, Theme.S(18));
            Gfx.FillRound(g, Theme.Track, bar, Theme.S(9));
            var old = g.Clip;
            using (var clip = Gfx.Round(bar, Theme.S(9)))
            {
                g.SetClip(clip, CombineMode.Intersect);
                float cx = bar.X;
                int[] counts = { inStock, lowOnly, outTotal };
                Color[] cols = { Theme.Primary, Theme.Warning, Theme.Danger };
                for (int i = 0; i < 3; i++)
                {
                    float sw = bar.Width * counts[i] / total;
                    if (sw <= 0) continue;
                    using (var br = new SolidBrush(cols[i])) g.FillRectangle(br, cx, bar.Y, sw + 0.5f, bar.Height);
                    cx += sw;
                }
            }
            g.Clip = old;

            string[] labels = { "In stock", "Running low", "Out of stock" };
            int[] cs = { inStock, lowOnly, outTotal };
            Color[] lc = { Theme.Primary, Theme.Warning, Theme.Danger };
            for (int i = 0; i < 3; i++)
            {
                float y = Theme.S(112 + i * 28);
                Gfx.FillCircle(g, lc[i], new RectangleF(x, y + Theme.S(8), Theme.S(10), Theme.S(10)));
                Gfx.Text(g, labels[i], Theme.F(10f), Theme.Text, new RectangleF(x + Theme.S(20), y, w, Theme.S(26)));
                Gfx.Text(g, cs[i] + "   (" + (cs[i] * 100 / total) + "%)", Theme.FS(10f), Theme.Text, new RectangleF(x, y, w, Theme.S(26)), StringAlignment.Far);
            }

            float ly = Theme.S(206);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, x, ly, x + w, ly);
            string[] k = { "Stock value at cost", "Stock value at retail", "Potential margin" };
            string[] v = { Fmt.Money(costValue), Fmt.Money(retailValue), Fmt.Money(retailValue - costValue) };
            Color[] vc = { Theme.Text, Theme.Text, Theme.PrimaryDark };
            for (int i = 0; i < 3; i++)
            {
                float y = ly + Theme.S(10) + i * Theme.S(34);
                Gfx.Text(g, k[i], Theme.F(10f), Theme.TextMuted, new RectangleF(x, y, w, Theme.S(28)));
                Gfx.Text(g, v[i], i == 2 ? Theme.FB(11.5f) : Theme.FS(10.5f), vc[i], new RectangleF(x, y, w, Theme.S(28)), StringAlignment.Far);
            }
        }
    }
}
