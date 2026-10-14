using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;
using GroceryPOS.UI.Dialogs;

namespace GroceryPOS.UI.Panels
{
    /// <summary>Product catalogue management (admin): add, edit, restock, archive.</summary>
    public class InventoryPanel : Panel, IRefreshable
    {
        readonly ModernTextBox search = new ModernTextBox { Glyph = Icons.Search, Placeholder = "Search name or barcode" };
        readonly ModernCombo catFilter = new ModernCombo { Glyph = Icons.Library };
        readonly ModernCombo stockFilter = new ModernCombo { Glyph = Icons.Box };
        readonly RoundButton addBtn = new RoundButton { Text = "Add product", Glyph = Icons.Add, Kind = BtnKind.Primary, GlyphSize = 12f };
        readonly RoundButton editBtn = new RoundButton { Text = "Edit", Glyph = Icons.Edit, Kind = BtnKind.Secondary, GlyphSize = 11f };
        readonly RoundButton restockBtn = new RoundButton { Text = "Restock", Glyph = Icons.Refresh, Kind = BtnKind.Soft, GlyphSize = 11f };
        readonly RoundButton deleteBtn = new RoundButton { Text = "Delete", Glyph = Icons.Delete, Kind = BtnKind.DangerSoft, GlyphSize = 11f };
        readonly SurfacePanel card = new SurfacePanel();
        readonly DataGridView grid = GridStyle.Create();

        List<Product> all = new List<Product>();
        List<Category> cats = new List<Category>();
        List<Product> shown = new List<Product>();

        public InventoryPanel()
        {
            BackColor = Theme.Bg;
            DoubleBuffered = true;

            stockFilter.SetItems(new object[] { "All stock levels", "In stock", "Low stock", "Out of stock" });
            search.TextChanged += (s, e) => ApplyFilter();
            catFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            stockFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            addBtn.Click += (s, e) => AddProduct();
            editBtn.Click += (s, e) => EditProduct();
            restockBtn.Click += (s, e) => Restock();
            deleteBtn.Click += (s, e) => DeleteProduct();

            grid.Columns.Add(GridStyle.Col("PRODUCT", "Name", 22));
            grid.Columns.Add(GridStyle.Col("BARCODE", "Barcode", 17));
            grid.Columns.Add(GridStyle.Col("CATEGORY", "CategoryName", 12));
            grid.Columns.Add(GridStyle.Col("UNIT", "Unit", 8));
            grid.Columns.Add(GridStyle.Col("PRICE", "PriceText", 12, true));
            grid.Columns.Add(GridStyle.Col("COST", "CostPrice", 9, true, "N2"));
            grid.Columns.Add(GridStyle.Col("STOCK", "StockText", 9, true));
            grid.Columns.Add(GridStyle.Col("ALERT", "MinText", 9, true));
            grid.Columns.Add(GridStyle.Col("STATUS", "Status", 13));
            grid.Columns["Name"].DefaultCellStyle.Font = Theme.FS(10.5f);
            grid.CellPainting += OnCellPainting;
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditProduct(); };
            grid.AutoGenerateColumns = false;

            card.Padding = new Padding(Theme.S(8), Theme.S(8), Theme.S(8), Theme.S(46));
            grid.Dock = DockStyle.Fill;
            card.Controls.Add(grid);
            card.Paint += PaintFooter;

            Controls.Add(card);
            Controls.Add(search);
            Controls.Add(catFilter);
            Controls.Add(stockFilter);

            addBtn.Width = Theme.S(166); editBtn.Width = Theme.S(100); restockBtn.Width = Theme.S(130); deleteBtn.Width = Theme.S(124);
            actions = Layouts.ButtonGroup(addBtn, editBtn, restockBtn, deleteBtn);
            Controls.Add(actions);
        }

        readonly Panel actions;

        public void RefreshData()
        {
            try
            {
                all = ProductService.GetProducts(true);
                cats = ProductService.GetCategories();
            }
            catch (Exception ex) { Msg.Info(this, "Could not load inventory", ex.Message, true); return; }

            string prev = catFilter.SelectedItem as string;
            var names = new List<object> { "All categories" };
            names.AddRange(cats.Select(c => (object)c.Name));
            catFilter.SetItems(names);
            if (prev != null) catFilter.SelectedItem = prev;
            ApplyFilter();
        }

        void ApplyFilter()
        {
            string q = (search.Text ?? "").Trim().ToLowerInvariant();
            string cat = catFilter.SelectedIndex > 0 ? Convert.ToString(catFilter.SelectedItem) : null;
            int lvl = stockFilter.SelectedIndex;
            shown = all.Where(p =>
                (cat == null || p.CategoryName == cat) &&
                (lvl == 0 || (lvl == 1 && !p.IsLow && !p.IsOut) || (lvl == 2 && p.IsLow) || (lvl == 3 && p.IsOut)) &&
                (q.Length == 0 || p.Name.ToLowerInvariant().Contains(q) || (p.Barcode != null && p.Barcode.ToLowerInvariant().Contains(q))))
                .ToList();
            grid.DataSource = shown;
            card.Invalidate();
        }

        Product Selected { get { return grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as Product; } }

        void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var p = grid.Rows[e.RowIndex].DataBoundItem as Product;
            if (p == null) return;
            if (grid.Columns[e.ColumnIndex].Name == "Status")
            {
                if (!p.IsActive) GridStyle.PaintPill(e, "Archived", Theme.Track, Theme.TextMuted);
                else if (p.IsOut) GridStyle.PaintPill(e, "Out of stock", Theme.DangerSoft, Theme.DangerDark);
                else if (p.IsLow) GridStyle.PaintPill(e, "Low stock", Theme.WarningSoft, Theme.WarningDark);
                else GridStyle.PaintPill(e, "In stock", Theme.PrimarySoft, Theme.PrimaryDark);
            }
            else if (!p.IsActive) e.CellStyle.ForeColor = Theme.TextFaint;
        }

        void PaintFooter(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            decimal value = shown.Sum(p => p.StockValue);
            string text = shown.Count + " of " + all.Count + " products   ·   Stock value at cost " + Fmt.Money(value);
            Gfx.Text(g, text, Theme.F(9.5f), Theme.TextMuted, new RectangleF(Theme.S(24), card.Height - Theme.S(42), card.Width - Theme.S(48), Theme.S(34)));
        }

        // ------------------------------------------------------------------ actions
        void AddProduct()
        {
            using (var d = new ProductDialog(null, cats))
                if (d.ShowOver(this) == DialogResult.OK) { RefreshData(); Toast.Show(this, "Product added", ToastKind.Success); }
        }

        void EditProduct()
        {
            var p = Selected;
            if (p == null) { Toast.Show(this, "Select a product first", ToastKind.Info); return; }
            using (var d = new ProductDialog(p, cats))
                if (d.ShowOver(this) == DialogResult.OK) { RefreshData(); Toast.Show(this, "Product updated", ToastKind.Success); }
                else RefreshData();
        }

        void Restock()
        {
            var p = Selected;
            if (p == null) { Toast.Show(this, "Select a product first", ToastKind.Info); return; }
            using (var d = new NumberDialog("Restock " + p.Name, p.IsWeighed ? "Weight to add (kg)" : "Quantity to add", 0, p.IsWeighed, 100000, "Currently " + p.StockText + " in stock"))
            {
                if (d.ShowOver(this) != DialogResult.OK || d.Value <= 0) return;
                try
                {
                    int amount = p.IsWeighed ? (int)Math.Round(d.Value * 1000) : (int)d.Value;
                    ProductService.AdjustStock(p.ProductId, amount, "Restock");
                    RefreshData();
                    Toast.Show(this, "Added " + Fmt.Qty(p.IsWeighed, amount) + " to " + p.Name, ToastKind.Success);
                }
                catch (Exception ex) { Msg.Info(this, "Could not restock", ex.Message, true); }
            }
        }

        void DeleteProduct()
        {
            var p = Selected;
            if (p == null) { Toast.Show(this, "Select a product first", ToastKind.Info); return; }
            if (!Msg.Confirm(this, "Delete product?", "\"" + p.Name + "\" will be removed from the catalogue. Products with sales history are archived instead.", "Delete", true)) return;
            try
            {
                bool hard = ProductService.DeleteProduct(p.ProductId);
                RefreshData();
                Toast.Show(this, hard ? "Product deleted" : "Product archived (it has sales history)", ToastKind.Success);
            }
            catch (Exception ex) { Msg.Info(this, "Could not delete product", ex.Message, true); }
        }

        // ------------------------------------------------------------------ layout
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width < 100) return;
            int h = Theme.S(52), gap = Theme.S(12);
            int used = Layouts.Flow(0, 0, Width, h, gap, new[]
            {
                new FlowItem(search, Theme.S(240), 1),
                new FlowItem(catFilter, Theme.S(190)),
                new FlowItem(stockFilter, Theme.S(180)),
                new FlowItem(actions, actions.Width)
            });
            int top = used + Theme.S(18);
            card.SetBounds(0, top, Width, Math.Max(Theme.S(120), Height - top));
        }
    }
}
