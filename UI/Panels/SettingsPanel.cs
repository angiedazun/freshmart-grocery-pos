using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;
using GroceryPOS.UI.Dialogs;

namespace GroceryPOS.UI.Panels
{
    /// <summary>Store details, receipt/printing options, staff accounts and categories (admin).</summary>
    public class SettingsPanel : Panel, IRefreshable
    {
        readonly SegmentedControl tabs = new SegmentedControl();
        readonly SurfacePanel card = new SurfacePanel();

        // general
        readonly ModernTextBox storeName = new ModernTextBox();
        readonly ModernTextBox storePhone = new ModernTextBox();
        readonly ModernTextBox storeAddress = new ModernTextBox();
        readonly ModernTextBox currency = new ModernTextBox { MaxLength = 6 };
        readonly ModernTextBox taxRate = new ModernTextBox { Mode = InputMode.Decimal, MaxLength = 6 };
        readonly ModernTextBox footer = new ModernTextBox();
        readonly ModernCombo printer = new ModernCombo { Glyph = Icons.Print };
        readonly CheckBox autoPrint = new CheckBox { Text = "Print receipt automatically after each sale", Font = Theme.F(10.5f), AutoSize = true, ForeColor = Theme.Text, BackColor = Color.White };
        readonly RoundButton saveBtn = new RoundButton { Text = "Save changes", Kind = BtnKind.Primary, Glyph = Icons.Save, GlyphSize = 12f };
        readonly RoundButton testBtn = new RoundButton { Text = "Print test receipt", Kind = BtnKind.Secondary, Glyph = Icons.Print, GlyphSize = 12f };
        readonly Panel general = new Panel { BackColor = Color.White, AutoScroll = true };

        // staff + categories share one grid each
        readonly DataGridView staffGrid = GridStyle.Create();
        readonly DataGridView catGrid = GridStyle.Create();
        readonly RoundButton addBtn = new RoundButton { Kind = BtnKind.Primary, Glyph = Icons.Add, GlyphSize = 12f };
        readonly RoundButton editBtn = new RoundButton { Text = "Edit", Kind = BtnKind.Secondary, Glyph = Icons.Edit, GlyphSize = 11f };
        readonly RoundButton deleteBtn = new RoundButton { Text = "Delete", Kind = BtnKind.DangerSoft, Glyph = Icons.Delete, GlyphSize = 11f };
        List<UserAccount> staff = new List<UserAccount>();
        List<Category> cats = new List<Category>();

        public SettingsPanel()
        {
            BackColor = Theme.Bg;
            DoubleBuffered = true;

            tabs.Items = new[] { "General", "Staff", "Categories" };
            tabs.SelectedChanged += (s, e) => UpdateTab();

            card.Padding = new Padding(Theme.S(8));
            BuildGeneral();

            staffGrid.AutoGenerateColumns = false;
            staffGrid.Columns.Add(GridStyle.Col("NAME", "FullName", 30));
            staffGrid.Columns.Add(GridStyle.Col("USERNAME", "Username", 22));
            staffGrid.Columns.Add(GridStyle.Col("ROLE", "Role", 18));
            staffGrid.Columns.Add(GridStyle.Col("STATUS", "Status", 18));
            staffGrid.Columns["FullName"].DefaultCellStyle.Font = Theme.FS(10.5f);
            staffGrid.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string col = staffGrid.Columns[e.ColumnIndex].Name;
                string v = Convert.ToString(e.Value);
                if (col == "Status") { if (v == "Active") GridStyle.PaintPill(e, "Active", Theme.PrimarySoft, Theme.PrimaryDark); else GridStyle.PaintPill(e, "Disabled", Theme.Track, Theme.TextMuted); }
                else if (col == "Role") { if (v == "Admin") GridStyle.PaintPill(e, "Admin", Theme.InfoSoft, Theme.Info); else GridStyle.PaintPill(e, "Cashier", Theme.WarningSoft, Theme.WarningDark); }
            };
            staffGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditCurrent(); };

            catGrid.AutoGenerateColumns = false;
            catGrid.Columns.Add(GridStyle.Col("CATEGORY", "Name", 40));
            catGrid.Columns.Add(GridStyle.Col("COLOUR", "Color", 30));
            catGrid.Columns.Add(GridStyle.Col("PRODUCTS", "ProductCount", 20, true));
            catGrid.Columns["Name"].DefaultCellStyle.Font = Theme.FS(10.5f);
            catGrid.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || catGrid.Columns[e.ColumnIndex].Name != "Color") return;
                e.PaintBackground(e.ClipBounds, true);
                var g = e.Graphics;
                Gfx.SmoothOn(g);
                var dot = new RectangleF(e.CellBounds.X + Theme.S(12), e.CellBounds.Y + (e.CellBounds.Height - Theme.S(24)) / 2f, Theme.S(24), Theme.S(24));
                Gfx.FillCircle(g, Theme.Hex(Convert.ToString(e.Value)), dot);
                Gfx.Text(g, Convert.ToString(e.Value).ToUpper(), Theme.F(10f), Theme.TextMuted,
                    new RectangleF(dot.Right + Theme.S(12), e.CellBounds.Y, e.CellBounds.Width - Theme.S(60), e.CellBounds.Height));
                e.Handled = true;
            };
            catGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditCurrent(); };

            addBtn.Click += (s, e) => AddCurrent();
            editBtn.Click += (s, e) => EditCurrent();
            deleteBtn.Click += (s, e) => DeleteCurrent();

            Controls.Add(tabs);
            Controls.Add(addBtn);
            Controls.Add(editBtn);
            Controls.Add(deleteBtn);
            Controls.Add(card);
            UpdateTab();
        }

        // ------------------------------------------------------------------ general tab
        static Panel Labeled(string caption, Control input)
        {
            var p = new Panel { BackColor = Color.White };
            var l = new Label { Text = caption, Dock = DockStyle.Top, Height = Theme.S(26), Font = Theme.FS(9f), ForeColor = Theme.TextMuted, TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent };
            input.Dock = DockStyle.Top;
            input.Height = Theme.S(50);
            p.Controls.Add(input);
            p.Controls.Add(l);
            return p;
        }

        readonly List<Control> fields = new List<Control>();
        readonly Dictionary<Control, int> spans = new Dictionary<Control, int>();

        void BuildGeneral()
        {
            printer.Placeholder = "Default printer";
            var wrap = new List<Tuple<Control, int>>
            {
                Tuple.Create<Control, int>(Labeled("Store name", storeName), 1),
                Tuple.Create<Control, int>(Labeled("Phone", storePhone), 1),
                Tuple.Create<Control, int>(Labeled("Address", storeAddress), 2),
                Tuple.Create<Control, int>(Labeled("Currency symbol", currency), 1),
                Tuple.Create<Control, int>(Labeled("Tax rate (%)", taxRate), 1),
                Tuple.Create<Control, int>(Labeled("Receipt footer message", footer), 2),
                Tuple.Create<Control, int>(Labeled("Receipt printer", printer), 2)
            };
            foreach (var t in wrap) { general.Controls.Add(t.Item1); fields.Add(t.Item1); spans[t.Item1] = t.Item2; }
            general.Controls.Add(autoPrint);
            general.Controls.Add(saveBtn);
            general.Controls.Add(testBtn);
            saveBtn.Click += (s, e) => SaveGeneral();
            testBtn.Click += (s, e) => TestPrint();
            general.Resize += (s, e) => LayoutGeneral();
            general.Dock = DockStyle.Fill;
            card.Controls.Add(general);
        }

        void LayoutGeneral()
        {
            int pad = Theme.S(20), gap = Theme.S(20), rowH = Theme.S(80);
            int w = general.ClientSize.Width - pad * 2;
            int colW = (w - gap) / 2;
            int x = 0, y = pad;
            foreach (var f in fields)
            {
                int span = spans[f];
                if (span == 2) { if (x != 0) { y += rowH; x = 0; } f.SetBounds(pad, y, w, rowH - Theme.S(8)); y += rowH; x = 0; }
                else
                {
                    f.SetBounds(pad + x * (colW + gap), y, colW, rowH - Theme.S(8));
                    x++;
                    if (x == 2) { x = 0; y += rowH; }
                }
            }
            if (x != 0) y += rowH;
            autoPrint.Location = new Point(pad, y + Theme.S(6));
            y += Theme.S(52);
            saveBtn.SetBounds(pad, y, Theme.S(190), Theme.S(54));
            testBtn.SetBounds(pad + Theme.S(204), y, Theme.S(220), Theme.S(54));
        }

        void LoadGeneral()
        {
            storeName.Text = AppSettings.StoreName;
            storePhone.Text = AppSettings.StorePhone;
            storeAddress.Text = AppSettings.StoreAddress;
            currency.Text = AppSettings.Currency;
            taxRate.Text = AppSettings.TaxRate.ToString("0.##", CultureInfo.InvariantCulture);
            footer.Text = AppSettings.ReceiptFooter;
            autoPrint.Checked = AppSettings.AutoPrint;

            var names = new List<object> { "Default printer" };
            try { foreach (string p in PrinterSettings.InstalledPrinters) names.Add(p); } catch { }
            printer.SetItems(names);
            if (!string.IsNullOrEmpty(AppSettings.Printer) && names.Contains(AppSettings.Printer)) printer.SelectedItem = AppSettings.Printer;
            LayoutGeneral();
        }

        void SaveGeneral()
        {
            decimal tax;
            if (string.IsNullOrWhiteSpace(storeName.Text)) { Toast.Show(this, "Store name is required", ToastKind.Warning); return; }
            if (string.IsNullOrWhiteSpace(currency.Text)) { Toast.Show(this, "Currency symbol is required", ToastKind.Warning); return; }
            if (!Fmt.TryDecimal(string.IsNullOrWhiteSpace(taxRate.Text) ? "0" : taxRate.Text, out tax) || tax < 0 || tax > 100)
            { Toast.Show(this, "Tax rate must be between 0 and 100", ToastKind.Warning); return; }
            try
            {
                AppSettings.Set("StoreName", storeName.Text.Trim());
                AppSettings.Set("StorePhone", storePhone.Text.Trim());
                AppSettings.Set("StoreAddress", storeAddress.Text.Trim());
                AppSettings.Set("Currency", currency.Text.Trim());
                AppSettings.Set("TaxRate", tax.ToString(CultureInfo.InvariantCulture));
                AppSettings.Set("ReceiptFooter", footer.Text.Trim());
                AppSettings.Set("AutoPrint", autoPrint.Checked ? "1" : "0");
                AppSettings.Set("Printer", printer.SelectedIndex > 0 ? Convert.ToString(printer.SelectedItem) : "");
                var f = FindForm();
                if (f != null) f.Invalidate(true);
                Toast.Show(this, "Settings saved", ToastKind.Success);
            }
            catch (Exception ex) { Msg.Info(this, "Could not save settings", ex.Message, true); }
        }

        void TestPrint()
        {
            var sale = new Sale
            {
                InvoiceNo = "INV-TEST", SaleDate = DateTime.Now, CashierName = Session.User.FullName, PaymentMethod = "Cash", Status = "Completed",
                Subtotal = 740, Total = 740, AmountGiven = 1000, ChangeAmount = 260,
                Items = new List<SaleItem>
                {
                    new SaleItem { ProductName = "Fresh Milk", Unit = "1L", UnitPrice = 420, Quantity = 1, LineTotal = 420 },
                    new SaleItem { ProductName = "White Bread", Unit = "450g", UnitPrice = 160, Quantity = 2, LineTotal = 320 }
                }
            };
            string chosen = printer.SelectedIndex > 0 ? Convert.ToString(printer.SelectedItem) : "";
            string err;
            if (ReceiptPrinter.Print(ReceiptPrinter.BuildSale(sale), chosen, out err)) Toast.Show(this, "Test receipt sent to printer", ToastKind.Success);
            else Toast.Show(this, "Printing failed: " + err, ToastKind.Error);
        }

        // ------------------------------------------------------------------ staff / categories
        int Tab { get { return tabs.SelectedIndex; } }

        void UpdateTab()
        {
            card.Controls.Clear();
            if (Tab == 0) card.Controls.Add(general);
            else if (Tab == 1) { staffGrid.Dock = DockStyle.Fill; card.Controls.Add(staffGrid); }
            else { catGrid.Dock = DockStyle.Fill; card.Controls.Add(catGrid); }

            addBtn.Visible = Tab != 0;
            editBtn.Visible = Tab != 0;
            deleteBtn.Visible = Tab == 2;
            addBtn.Text = Tab == 1 ? "Add staff" : "Add category";
            DoLayout();
            RefreshData();
        }

        public void RefreshData()
        {
            try
            {
                if (Tab == 0) LoadGeneral();
                else if (Tab == 1) { staff = AuthService.GetUsers(false); staffGrid.DataSource = staff; }
                else { cats = ProductService.GetCategories(); catGrid.DataSource = cats; }
            }
            catch (Exception ex) { Msg.Info(this, "Could not load data", ex.Message, true); }
        }

        void AddCurrent()
        {
            if (Tab == 1) { using (var d = new UserDialog(null)) if (d.ShowOver(this) == DialogResult.OK) { RefreshData(); Toast.Show(this, "Staff member added", ToastKind.Success); } }
            else if (Tab == 2) { using (var d = new CategoryDialog(null)) if (d.ShowOver(this) == DialogResult.OK) { RefreshData(); Toast.Show(this, "Category added", ToastKind.Success); } }
        }

        void EditCurrent()
        {
            if (Tab == 1)
            {
                var u = staffGrid.CurrentRow == null ? null : staffGrid.CurrentRow.DataBoundItem as UserAccount;
                if (u == null) { Toast.Show(this, "Select a staff member first", ToastKind.Info); return; }
                using (var d = new UserDialog(u)) if (d.ShowOver(this) == DialogResult.OK) { RefreshData(); Toast.Show(this, "Staff member updated", ToastKind.Success); }
                RefreshData();
            }
            else if (Tab == 2)
            {
                var c = catGrid.CurrentRow == null ? null : catGrid.CurrentRow.DataBoundItem as Category;
                if (c == null) { Toast.Show(this, "Select a category first", ToastKind.Info); return; }
                using (var d = new CategoryDialog(c)) if (d.ShowOver(this) == DialogResult.OK) { RefreshData(); Toast.Show(this, "Category updated", ToastKind.Success); }
                RefreshData();
            }
        }

        void DeleteCurrent()
        {
            var c = catGrid.CurrentRow == null ? null : catGrid.CurrentRow.DataBoundItem as Category;
            if (c == null) { Toast.Show(this, "Select a category first", ToastKind.Info); return; }
            if (c.ProductCount > 0) { Msg.Info(this, "Category in use", "\"" + c.Name + "\" still has " + c.ProductCount + " product(s). Move or remove them first.", true); return; }
            if (!Msg.Confirm(this, "Delete category?", "\"" + c.Name + "\" will be removed.", "Delete", true)) return;
            try { ProductService.DeleteCategory(c.CategoryId); RefreshData(); Toast.Show(this, "Category deleted", ToastKind.Success); }
            catch (Exception ex) { Msg.Info(this, "Could not delete category", "It may still be referenced by archived products.\n\n" + ex.Message, true); }
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
            tabs.SetBounds(0, 0, Theme.S(420), h);
            int x = Width;
            if (Tab == 2) { deleteBtn.SetBounds(x -= Theme.S(124), 0, Theme.S(124), h); x -= gap; }
            if (Tab != 0)
            {
                editBtn.SetBounds(x -= Theme.S(104), 0, Theme.S(104), h); x -= gap;
                addBtn.SetBounds(x -= Theme.S(176), 0, Theme.S(176), h);
            }
            card.SetBounds(0, h + Theme.S(18), Width, Height - h - Theme.S(18));
        }
    }
}