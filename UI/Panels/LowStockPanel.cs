using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;
using GroceryPOS.UI.Dialogs;

namespace GroceryPOS.UI.Panels
{
    /// <summary>Everything at or below its reorder level, flagged red (out) or amber (low).</summary>
    public class LowStockPanel : Panel, IRefreshable
    {
        readonly StatCard outCard = new StatCard { Title = "Out of stock", Glyph = Icons.Warning, Accent = Theme.Danger };
        readonly StatCard lowCard = new StatCard { Title = "Running low", Glyph = Icons.Box, Accent = Theme.Warning };
        readonly StatCard costCard = new StatCard { Title = "Estimated restock cost", Glyph = Icons.Cash, Accent = Theme.Info };
        readonly RoundButton restockBtn = new RoundButton { Text = "Restock", Glyph = Icons.Refresh, Kind = BtnKind.Primary, GlyphSize = 11f };
        readonly RoundButton exportBtn = new RoundButton { Text = "Export list", Glyph = Icons.Save, Kind = BtnKind.Secondary, GlyphSize = 11f };
        readonly RoundButton refreshBtn = new RoundButton { Text = "Refresh", Glyph = Icons.Refresh, Kind = BtnKind.Ghost, GlyphSize = 11f };
        readonly SurfacePanel card = new SurfacePanel();
        readonly DataGridView grid = GridStyle.Create();
        List<Product> items = new List<Product>();

        public LowStockPanel()
        {
            BackColor = Theme.Bg;
            DoubleBuffered = true;

            restockBtn.Click += (s, e) => Restock();
            exportBtn.Click += (s, e) => Export();
            refreshBtn.Click += (s, e) => RefreshData();

            grid.AutoGenerateColumns = false;
            grid.Columns.Add(GridStyle.Col("PRODUCT", "Name", 28));
            grid.Columns.Add(GridStyle.Col("CATEGORY", "CategoryName", 15));
            grid.Columns.Add(GridStyle.Col("UNIT", "Unit", 9));
            grid.Columns.Add(GridStyle.Col("IN STOCK", "StockText", 10, true));
            grid.Columns.Add(GridStyle.Col("ALERT AT", "MinText", 10, true));
            grid.Columns.Add(GridStyle.Col("SUGGESTED ORDER", "SuggestedText", 14, true));
            grid.Columns.Add(GridStyle.Col("STATUS", "Status", 14));
            grid.Columns["Name"].DefaultCellStyle.Font = Theme.FS(10.5f);
            grid.CellPainting += OnCellPainting;
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Restock(); };

            card.Padding = new Padding(Theme.S(8));
            grid.Dock = DockStyle.Fill;
            card.Controls.Add(grid);
            card.Paint += PaintEmpty;

            Controls.Add(card);
            Controls.Add(outCard);
            Controls.Add(lowCard);
            Controls.Add(costCard);
            Controls.Add(restockBtn);
            Controls.Add(exportBtn);
            Controls.Add(refreshBtn);
        }

        public void RefreshData()
        {
            try { items = ProductService.GetLowStock(); }
            catch (Exception ex) { Msg.Info(this, "Could not load stock levels", ex.Message, true); return; }

            int outN = items.Count(p => p.IsOut), lowN = items.Count - outN;
            outCard.Value = outN.ToString();
            outCard.Caption = outN == 0 ? "Nothing sold out" : "Reorder these first";
            lowCard.Value = lowN.ToString();
            lowCard.Caption = "At or below alert level";
            costCard.Value = Fmt.Money(items.Sum(p => p.SuggestedCost));
            costCard.Caption = "To bring items back to 2x alert level";

            grid.DataSource = items;
            foreach (DataGridViewRow r in grid.Rows)
            {
                var p = r.DataBoundItem as Product;
                if (p == null) continue;
                r.DefaultCellStyle.BackColor = p.IsOut ? Color.FromArgb(0xFF, 0xF5, 0xF5) : Color.FromArgb(0xFF, 0xFB, 0xEB);
                r.DefaultCellStyle.SelectionBackColor = p.IsOut ? Theme.DangerSoft : Theme.WarningSoft;
                if (p.IsOut) r.Cells["StockText"].Style.ForeColor = Theme.DangerDark;
            }
            card.Invalidate();
        }

        Product Selected { get { return grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as Product; } }

        void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Status") return;
            var p = grid.Rows[e.RowIndex].DataBoundItem as Product;
            if (p == null) return;
            if (p.IsOut) GridStyle.PaintPill(e, "Out of stock", Theme.DangerSoft, Theme.DangerDark);
            else GridStyle.PaintPill(e, "Low stock", Theme.WarningSoft, Theme.WarningDark);
        }

        void PaintEmpty(object sender, PaintEventArgs e)
        {
            if (items.Count > 0) return;
            var g = e.Graphics;
            var ic = new RectangleF(card.Width / 2f - Theme.S(44), card.Height / 2f - Theme.S(90), Theme.S(88), Theme.S(88));
            Gfx.FillCircle(g, Theme.PrimarySoft, ic);
            Gfx.Icon(g, Icons.Check, 26f, Theme.Primary, ic);
            Gfx.Text(g, "Everything is well stocked", Theme.FS(14f), Theme.Text, new RectangleF(0, ic.Bottom + Theme.S(8), card.Width, Theme.S(32)), StringAlignment.Center);
            Gfx.Text(g, "No product is at or below its alert level.", Theme.F(10f), Theme.TextMuted, new RectangleF(0, ic.Bottom + Theme.S(40), card.Width, Theme.S(26)), StringAlignment.Center);
        }

        void Restock()
        {
            var p = Selected;
            if (p == null) { Toast.Show(this, "Select a product first", ToastKind.Info); return; }
            using (var d = new NumberDialog("Restock " + p.Name, p.IsWeighed ? "Weight to add (kg)" : "Quantity to add", p.IsWeighed ? p.SuggestedReorder / 1000m : p.SuggestedReorder, p.IsWeighed, 100000,
                "In stock " + p.StockText + "  ·  alert level " + p.MinText))
            {
                if (d.ShowOver(this) != DialogResult.OK || d.Value <= 0) return;
                try
                {
                    int amount = p.IsWeighed ? (int)Math.Round(d.Value * 1000) : (int)d.Value;
                    ProductService.AdjustStock(p.ProductId, amount, "Restock");
                    RefreshData();
                    var main = FindForm() as MainForm;
                    if (main != null) main.RefreshBadge();
                    Toast.Show(this, "Added " + Fmt.Qty(p.IsWeighed, amount) + " to " + p.Name, ToastKind.Success);
                }
                catch (Exception ex) { Msg.Info(this, "Could not restock", ex.Message, true); }
            }
        }

        void Export()
        {
            if (items.Count == 0) { Toast.Show(this, "There is nothing to export", ToastKind.Info); return; }
            using (var sfd = new SaveFileDialog { Title = "Export reorder list", Filter = "CSV file|*.csv", FileName = "reorder-list-" + DateTime.Today.ToString("yyyyMMdd") + ".csv" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder();
                sb.AppendLine("Product,Category,Unit,In stock,Alert level,Suggested order,Unit cost,Estimated cost");
                foreach (var p in items)
                    sb.AppendLine(string.Join(",", Csv(p.Name), Csv(p.CategoryName), Csv(p.Unit), p.StockText, p.MinText, p.SuggestedText,
                        p.CostPrice.ToString("0.00"), p.SuggestedCost.ToString("0.00")));
                try { File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true)); Toast.Show(this, "Reorder list exported", ToastKind.Success); }
                catch (Exception ex) { Msg.Info(this, "Export failed", ex.Message, true); }
            }
        }

        static string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width < 100) return;
            int gap = Theme.S(18), ch = Theme.S(108);
            int cw = (Width - gap * 2) / 3;
            outCard.SetBounds(0, 0, cw, ch);
            lowCard.SetBounds(cw + gap, 0, cw, ch);
            costCard.SetBounds((cw + gap) * 2, 0, Width - (cw + gap) * 2, ch);

            int y = ch + Theme.S(20), bh = Theme.S(50);
            restockBtn.SetBounds(0, y, Theme.S(150), bh);
            exportBtn.SetBounds(Theme.S(162), y, Theme.S(150), bh);
            refreshBtn.SetBounds(Width - Theme.S(130), y, Theme.S(130), bh);
            card.SetBounds(0, y + bh + Theme.S(16), Width, Height - y - bh - Theme.S(16));
        }
    }
}
