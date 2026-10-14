using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;
using GroceryPOS.UI.Dialogs;

namespace GroceryPOS.UI.Panels
{
    /// <summary>Transaction history (view / reprint / void) and the shift Z-report.</summary>
    public class ReportsPanel : Panel, IRefreshable
    {
        readonly SegmentedControl tabs = new SegmentedControl();

        // transactions
        readonly DateBox from = new DateBox();
        readonly DateBox to = new DateBox();
        readonly ModernTextBox invoice = new ModernTextBox { Glyph = Icons.Search, Placeholder = "Invoice no." };
        readonly ModernCombo cashier = new ModernCombo { Glyph = Icons.Person };
        readonly RoundButton searchBtn = new RoundButton { Text = "Search", Kind = BtnKind.Primary, Glyph = Icons.Search, GlyphSize = 11f };
        readonly RoundButton detailsBtn = new RoundButton { Text = "View invoice", Kind = BtnKind.Secondary, Glyph = Icons.Document, GlyphSize = 11f };
        readonly SurfacePanel card = new SurfacePanel();
        readonly DataGridView grid = GridStyle.Create();
        DataTable table = new DataTable();

        // z-report
        readonly DateBox zDate = new DateBox();
        readonly ModernCombo zCashier = new ModernCombo { Glyph = Icons.Person };
        readonly RoundButton zGenerate = new RoundButton { Text = "Generate", Kind = BtnKind.Primary, Glyph = Icons.Refresh, GlyphSize = 11f };
        readonly RoundButton zPrint = new RoundButton { Text = "Print Z-report", Kind = BtnKind.Secondary, Glyph = Icons.Print, GlyphSize = 11f };
        readonly List<StatCard> zCards = new List<StatCard>();
        readonly Panel zHost = new Panel { BackColor = Theme.Bg };
        ZReport zReport;

        bool isAdmin { get { return Session.User != null && Session.User.IsAdmin; } }

        public ReportsPanel()
        {
            BackColor = Theme.Bg;
            DoubleBuffered = true;

            tabs.Items = new[] { "Transactions", "Z-Report" };
            tabs.SelectedChanged += (s, e) => { UpdateTab(); LoadCurrent(); };

            from.Value = DateTime.Today;
            to.Value = DateTime.Today;
            searchBtn.Click += (s, e) => LoadTransactions();
            invoice.EnterPressed += (s, e) => LoadTransactions();
            detailsBtn.Click += (s, e) => OpenSelected();

            grid.AutoGenerateColumns = false;
            grid.Columns.Add(GridStyle.Col("INVOICE", "InvoiceNo", 14));
            grid.Columns.Add(GridStyle.Col("DATE & TIME", "SaleDate", 18, false, "yyyy-MM-dd  HH:mm"));
            grid.Columns.Add(GridStyle.Col("CASHIER", "Cashier", 18));
            grid.Columns.Add(GridStyle.Col("ITEMS", "Items", 8, true));
            grid.Columns.Add(GridStyle.Col("TOTAL", "Total", 14, true, "N2"));
            grid.Columns.Add(GridStyle.Col("PAYMENT", "PaymentMethod", 12));
            grid.Columns.Add(GridStyle.Col("STATUS", "Status", 14));
            grid.Columns["InvoiceNo"].DefaultCellStyle.Font = Theme.FS(10.5f);
            grid.CellPainting += OnCellPainting;
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenSelected(); };
            card.Padding = new Padding(Theme.S(8), Theme.S(8), Theme.S(8), Theme.S(46));
            grid.Dock = DockStyle.Fill;
            card.Controls.Add(grid);
            card.Paint += PaintFooter;

            zDate.Value = DateTime.Today;
            zGenerate.Click += (s, e) => LoadZ();
            zDate.ValueChanged += (s, e) => LoadZ();
            zCashier.SelectedIndexChanged += (s, e) => LoadZ();
            zPrint.Click += (s, e) => PrintZ();
            string[] zt = { "Net sales", "Transactions", "Items sold", "Average basket", "Cash collected", "Card payments", "Credit sales", "Discounts given", "Tax collected", "Voided sales" };
            string[] zg = { Icons.Chart, Icons.Document, Icons.Box, Icons.Cart, Icons.Cash, Icons.Card, Icons.People, Icons.Tag, Icons.Library, Icons.Warning };
            Color[] zc = { Theme.Primary, Theme.Info, Color.FromArgb(0x8B, 0x5C, 0xF6), Color.FromArgb(0x0E, 0xA5, 0xE9), Theme.Primary, Theme.Info, Theme.Warning, Theme.Danger, Color.FromArgb(0x64, 0x74, 0x8B), Theme.Danger };
            for (int i = 0; i < zt.Length; i++)
            {
                var sc = new StatCard { Title = zt[i], Glyph = zg[i], Accent = zc[i], Value = "-" };
                zCards.Add(sc);
                zHost.Controls.Add(sc);
            }

            Controls.Add(tabs);
            Controls.Add(from); Controls.Add(to); Controls.Add(invoice); Controls.Add(cashier);
            searchBtn.Width = Theme.S(124); detailsBtn.Width = Theme.S(156); zGenerate.Width = Theme.S(140); zPrint.Width = Theme.S(180);
            txActions = Layouts.ButtonGroup(searchBtn, detailsBtn);
            zActions = Layouts.ButtonGroup(zGenerate, zPrint);
            Controls.Add(txActions); Controls.Add(card);
            Controls.Add(zDate); Controls.Add(zCashier); Controls.Add(zActions); Controls.Add(zHost);
            UpdateTab();
        }

        Panel txActions, zActions;

        bool ZTab { get { return tabs.SelectedIndex == 1; } }

        void UpdateTab()
        {
            bool z = ZTab;
            foreach (Control c in new Control[] { from, to, invoice, txActions, card }) c.Visible = !z;
            cashier.Visible = !z && isAdmin;
            foreach (Control c in new Control[] { zDate, zActions, zHost }) c.Visible = z;
            zCashier.Visible = z && isAdmin;
            DoLayout();
        }

        public void RefreshData()
        {
            var admin = isAdmin;
            if (admin)
            {
                var users = AuthService.GetUsers(false);
                var names = new List<object> { "All cashiers" };
                names.AddRange(users.Cast<object>());
                int prevC = cashier.SelectedIndex, prevZ = zCashier.SelectedIndex;
                cashier.SetItems(names);
                zCashier.SetItems(names);
                if (prevC > 0 && prevC < names.Count) cashier.SelectedIndex = prevC;
                if (prevZ > 0 && prevZ < names.Count) zCashier.SelectedIndex = prevZ;
            }
            UpdateTab();
            LoadCurrent();
        }

        void LoadCurrent() { if (ZTab) LoadZ(); else LoadTransactions(); }

        int? CashierId(ModernCombo combo)
        {
            if (!isAdmin) return Session.User == null ? (int?)null : Session.User.UserId;
            var u = combo.SelectedItem as UserAccount;
            return u == null ? (int?)null : u.UserId;
        }

        string CashierName(ModernCombo combo)
        {
            if (!isAdmin) return Session.User.FullName;
            var u = combo.SelectedItem as UserAccount;
            return u == null ? "All cashiers" : u.FullName;
        }

        // ------------------------------------------------------------------ transactions
        void LoadTransactions()
        {
            try
            {
                var a = from.Value; var b = to.Value;
                if (b < a) { var t = a; a = b; b = t; }
                table = SaleService.SearchSales(a, b, invoice.Text, CashierId(cashier));
                grid.DataSource = table;
                foreach (DataGridViewRow r in grid.Rows)
                    if (Convert.ToString(r.Cells["Status"].Value) == "Voided") r.DefaultCellStyle.ForeColor = Theme.TextFaint;
                card.Invalidate();
            }
            catch (Exception ex) { Msg.Info(this, "Could not load transactions", ex.Message, true); }
        }

        void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Status") return;
            string s = Convert.ToString(e.Value);
            if (s == "Voided") GridStyle.PaintPill(e, "Voided", Theme.DangerSoft, Theme.DangerDark);
            else GridStyle.PaintPill(e, "Completed", Theme.PrimarySoft, Theme.PrimaryDark);
        }

        void PaintFooter(object sender, PaintEventArgs e)
        {
            var rows = table.Rows.Cast<DataRow>().Where(r => Convert.ToString(r["Status"]) == "Completed").ToList();
            decimal total = rows.Sum(r => Convert.ToDecimal(r["Total"]));
            string text = table.Rows.Count + " transactions   ·   " + rows.Count + " completed   ·   Sales total " + Fmt.Money(total);
            Gfx.Text(e.Graphics, text, Theme.F(9.5f), Theme.TextMuted, new RectangleF(Theme.S(24), card.Height - Theme.S(42), card.Width - Theme.S(48), Theme.S(34)));
        }

        void OpenSelected()
        {
            if (grid.CurrentRow == null) { Toast.Show(this, "Select an invoice first", ToastKind.Info); return; }
            var rv = grid.CurrentRow.DataBoundItem as DataRowView;
            if (rv == null) return;
            var sale = SaleService.GetSale(Convert.ToInt32(rv["SaleId"]));
            if (sale == null) return;
            using (var d = new ReceiptDialog(sale, isAdmin))
            {
                d.ShowOver(this);
                if (d.Voided)
                {
                    LoadTransactions();
                    var main = FindForm() as MainForm;
                    if (main != null) main.RefreshBadge();
                    Toast.Show(this, "Sale voided and stock restored", ToastKind.Success);
                }
            }
        }

        // ------------------------------------------------------------------ z-report
        void LoadZ()
        {
            try
            {
                zReport = SaleService.GetZReport(zDate.Value, CashierId(zCashier), CashierName(zCashier));
                var z = zReport;
                Set(0, Fmt.Money(z.NetSales), "Gross " + Fmt.Money(z.GrossSales));
                Set(1, z.Transactions.ToString(), "Completed sales");
                Set(2, z.ItemsSold.ToString(), "Units across all baskets");
                Set(3, Fmt.Money(z.AverageBasket), "Per transaction");
                Set(4, Fmt.Money(z.Cash), "Cash in the drawer from sales");
                Set(5, Fmt.Money(z.Card), "Settled by card terminal");
                Set(6, Fmt.Money(z.Credit), "Charged to store accounts");
                Set(7, Fmt.Money(z.Discounts), "Total discounts applied");
                Set(8, Fmt.Money(z.Tax), "Included in net sales");
                Set(9, Fmt.Money(z.VoidedAmount), z.VoidedCount + " voided invoice(s)");
            }
            catch (Exception ex) { Msg.Info(this, "Could not build report", ex.Message, true); }
        }

        void Set(int i, string value, string caption) { zCards[i].Value = value; zCards[i].Caption = caption; }

        void PrintZ()
        {
            if (zReport == null) LoadZ();
            string err;
            if (ReceiptPrinter.Print(ReceiptPrinter.BuildZ(zReport), AppSettings.Printer, out err))
                Toast.Show(this, "Z-report sent to printer", ToastKind.Success);
            else Toast.Show(this, "Printing failed: " + err, ToastKind.Error);
        }

        // ------------------------------------------------------------------ layout
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        void DoLayout()
        {
            if (Width < 100) return;
            int h = Theme.S(50), gap = Theme.S(12);
            tabs.SetBounds(0, 0, Theme.S(330), h);

            int y = h + Theme.S(18);
            if (!ZTab)
            {
                var items = new List<FlowItem> { new FlowItem(from, Theme.S(190)), new FlowItem(to, Theme.S(190)), new FlowItem(invoice, Theme.S(190), 1) };
                if (isAdmin) items.Add(new FlowItem(cashier, Theme.S(200)));
                items.Add(new FlowItem(txActions, txActions.Width));
                int used = Layouts.Flow(0, y, Width, h, gap, items);
                int top = y + used + Theme.S(16);
                card.SetBounds(0, top, Width, Math.Max(Theme.S(120), Height - top));
            }
            else
            {
                var items = new List<FlowItem> { new FlowItem(zDate, Theme.S(250)) };
                if (isAdmin) items.Add(new FlowItem(zCashier, Theme.S(230), 1));
                items.Add(new FlowItem(zActions, zActions.Width));
                int used = Layouts.Flow(0, y, Width, h, gap, items);
                int top = y + used + Theme.S(18);
                zHost.SetBounds(0, top, Width, Math.Max(Theme.S(120), Height - top));

                int cols = Width >= Theme.S(1000) ? 4 : Width >= Theme.S(660) ? 3 : 2;
                int g = Theme.S(16), cw = (zHost.Width - g * (cols - 1)) / cols, ch = Theme.S(108);
                for (int k = 0; k < zCards.Count; k++)
                    zCards[k].SetBounds((k % cols) * (cw + g), (k / cols) * (ch + g), cw, ch);
            }
        }    }
}
