using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Dialogs
{
    /// <summary>Add / edit a product, with a live preview of its POS card.</summary>
    public class ProductDialog : DialogBase
    {
        readonly Product product;
        readonly List<Category> cats;
        readonly ModernTextBox name = new ModernTextBox { Placeholder = "e.g. Fresh Milk" };
        readonly ModernTextBox barcode = new ModernTextBox { Placeholder = "Scan or type", Glyph = Icons.Tag };
        readonly ModernCombo category = new ModernCombo();
        readonly ModernTextBox unit = new ModernTextBox { Placeholder = "1kg, 500ml, 6 pcs" };
        readonly ModernTextBox icon = new ModernTextBox { Placeholder = "Emoji, e.g. \U0001F95B" };
        readonly ModernTextBox price = new ModernTextBox { Mode = InputMode.Decimal, Placeholder = "0.00" };
        readonly ModernTextBox cost = new ModernTextBox { Mode = InputMode.Decimal, Placeholder = "0.00" };
        readonly ModernTextBox stock = new ModernTextBox { Mode = InputMode.Integer, Placeholder = "0" };
        readonly ModernTextBox minStock = new ModernTextBox { Mode = InputMode.Integer, Placeholder = "5" };
        readonly CheckBox active = new CheckBox { Text = "Active - show on POS terminal", Checked = true, Font = Theme.F(10.5f), AutoSize = true, ForeColor = Theme.Text, BackColor = Color.White };
        readonly SurfacePanel preview = new SurfacePanel { Fill = Theme.SurfaceAlt };
        string imagePath;
        readonly CheckBox weighed = new CheckBox { Text = "Sold by weight (price per kg)", Font = Theme.F(10.5f), AutoSize = true, ForeColor = Theme.Text, BackColor = Color.White };
        Panel priceF, costF, stockF, minF;

        public ProductDialog(Product existing, List<Category> categories)
            : base(existing == null ? "Add product" : "Edit product", 900, 740)
        {
            product = existing ?? new Product();
            cats = categories;
            imagePath = product.ImagePath;

            category.SetItems(cats.Cast<object>());
            category.Glyph = Icons.Library;
            category.SelectedIndexChanged += (s, e) => preview.Invalidate();
            icon.TextChanged += (s, e) => preview.Invalidate();
            name.TextChanged += (s, e) => preview.Invalidate();

            // ----- form grid -----
            var grid = new TableLayoutPanel { Dock = DockStyle.Left, Width = Theme.S(520), ColumnCount = 2, BackColor = Color.White };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            int rowH = Theme.S(92);
            for (int i = 0; i < 6; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));

            var barcodeRow = new Panel { Height = Theme.S(50) };
            var gen = new RoundButton { Text = "Auto", Kind = BtnKind.Soft, Width = Theme.S(72), Dock = DockStyle.Right };
            gen.Click += (s, e) => barcode.Text = "2" + DateTime.Now.ToString("yyMMddHHmmss");
            barcode.Dock = DockStyle.Fill;
            barcodeRow.Controls.Add(barcode);
            barcodeRow.Controls.Add(new Panel { Dock = DockStyle.Right, Width = Theme.S(8) });
            barcodeRow.Controls.Add(gen);
            barcodeRow.Controls.SetChildIndex(barcode, 0);

            var nameField = Field("Product name", name);
            grid.Controls.Add(nameField, 0, 0);
            grid.SetColumnSpan(nameField, 2);
            nameField.Margin = new Padding(0);
            grid.Controls.Add(Field("Barcode", barcodeRow), 0, 1);
            grid.Controls.Add(Field("Category", category), 1, 1);
            grid.Controls.Add(Field("Package size / unit", unit), 0, 2);
            grid.Controls.Add(Field("Card icon (emoji)", icon), 1, 2);
            priceF = Field("Selling price (" + AppSettings.Currency + ")", price); grid.Controls.Add(priceF, 0, 3);
            costF = Field("Cost price (" + AppSettings.Currency + ")", cost); grid.Controls.Add(costF, 1, 3);
            stockF = Field("Stock on hand", stock); grid.Controls.Add(stockF, 0, 4);
            minF = Field("Low-stock alert at", minStock); grid.Controls.Add(minF, 1, 4);
            weighed.Margin = new Padding(0, Theme.S(14), 0, 0);
            active.Margin = new Padding(0, Theme.S(14), 0, 0);
            active.Text = "Active on POS";
            grid.Controls.Add(weighed, 0, 5);
            grid.Controls.Add(active, 1, 5);
            weighed.CheckedChanged += (s, e) => ApplyWeighed();

            // ----- preview column -----
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(Theme.S(28), Theme.S(26), 0, 0) };
            var cap = new Label { Text = "POS card preview", Dock = DockStyle.Top, Height = Theme.S(26), Font = Theme.FS(9f), ForeColor = Theme.TextMuted, BackColor = Color.Transparent, TextAlign = ContentAlignment.BottomLeft };
            preview.Dock = DockStyle.Top;
            preview.Height = Theme.S(250);
            preview.Paint += PaintPreview;
            var pick = new RoundButton { Text = "Choose photo", Kind = BtnKind.Secondary, Glyph = Icons.Add, GlyphSize = 11f, Dock = DockStyle.Top, Height = Theme.S(50) };
            pick.Click += (s, e) => PickImage();
            var clear = new RoundButton { Text = "Remove photo", Kind = BtnKind.Ghost, Dock = DockStyle.Top, Height = Theme.S(46) };
            clear.Click += (s, e) => { imagePath = null; preview.Invalidate(); };
            right.Controls.Add(clear);
            right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = Theme.S(8) });
            right.Controls.Add(pick);
            right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = Theme.S(14) });
            right.Controls.Add(preview);
            right.Controls.Add(cap);

            var save = new RoundButton { Text = "Save product", Kind = BtnKind.Primary, Glyph = Icons.Save, GlyphSize = 12f, Width = Theme.S(200) };
            save.Click += (s, e) => Save();
            var cancel = new RoundButton { Text = "Cancel", Kind = BtnKind.Secondary, Width = Theme.S(130) };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Body.Controls.Add(right);
            Body.Controls.Add(grid);
            Body.Controls.Add(ButtonRow(cancel, save));

            // ----- populate -----
            name.Text = product.Name ?? "";
            barcode.Text = product.Barcode ?? "";
            unit.Text = product.Unit ?? "";
            icon.Text = product.Icon ?? "";
            if (existing != null)
            {
                price.Text = product.Price.ToString("0.##", CultureInfo.InvariantCulture);
                cost.Text = product.CostPrice.ToString("0.##", CultureInfo.InvariantCulture);
                weighed.Checked = product.IsWeighed;
                stock.Text = product.IsWeighed ? (product.Stock / 1000m).ToString("0.###", CultureInfo.InvariantCulture) : product.Stock.ToString();
                minStock.Text = product.IsWeighed ? (product.MinStock / 1000m).ToString("0.###", CultureInfo.InvariantCulture) : product.MinStock.ToString();
                active.Checked = product.IsActive;
                var c = cats.FirstOrDefault(x => x.CategoryId == product.CategoryId);
                if (c != null) category.SelectedItem = c;
            }
            else { minStock.Text = "5"; stock.Text = "0"; }
        }

        void ApplyWeighed()
        {
            bool w = weighed.Checked;
            ((Label)priceF.Controls[1]).Text = w ? "Selling price per kg (" + AppSettings.Currency + ")" : "Selling price (" + AppSettings.Currency + ")";
            ((Label)costF.Controls[1]).Text = w ? "Cost price per kg (" + AppSettings.Currency + ")" : "Cost price (" + AppSettings.Currency + ")";
            ((Label)stockF.Controls[1]).Text = w ? "Stock on hand (kg)" : "Stock on hand";
            ((Label)minF.Controls[1]).Text = w ? "Low-stock alert at (kg)" : "Low-stock alert at";
            stock.Mode = minStock.Mode = w ? InputMode.Decimal : InputMode.Integer;
            if (w && string.IsNullOrWhiteSpace(unit.Text)) unit.Text = "per kg";
            preview.Invalidate();
        }

        void PickImage()
        {
            using (var ofd = new OpenFileDialog { Title = "Choose product photo", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*" })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string stored = ImageCache.Import(ofd.FileName);
                    if (ImageCache.Get(stored) == null) { Msg.Info(this, "Unsupported image", "That file could not be read as an image.", true); return; }
                    imagePath = stored;
                    preview.Invalidate();
                }
                catch (Exception ex) { Msg.Info(this, "Could not import image", ex.Message, true); }
            }
        }

        void PaintPreview(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var cat = category.SelectedItem as Category;
            Color cc = Theme.Hex(cat == null ? "#16A34A" : cat.Color);
            var card = new RectangleF((preview.Width - Theme.S(176)) / 2f, Theme.S(16), Theme.S(176), preview.Height - Theme.S(32));
            Gfx.FillRound(g, Color.White, card, Theme.S(18));
            Gfx.StrokeRound(g, Theme.Border, card, Theme.S(18));
            var tile = new RectangleF(card.X + Theme.S(8), card.Y + Theme.S(8), card.Width - Theme.S(16), Theme.S(100));
            var img = ImageCache.Get(imagePath);
            if (img != null) Gfx.DrawCover(g, img, tile, Theme.S(13));
            else
            {
                Gfx.FillRound(g, Gfx.Lighten(cc, .8f), tile, Theme.S(13));
                string t = icon.Text.Trim();
                if (t.Length > 0) Gfx.Text(g, t, Theme.Emoji(34f), Gfx.Darken(cc, .12f), tile, StringAlignment.Center);
                else Gfx.Text(g, ProductGrid.Initials(name.Text), Theme.FB(24f), Gfx.Darken(cc, .1f), tile, StringAlignment.Center);
            }
            Gfx.Text(g, string.IsNullOrWhiteSpace(name.Text) ? "Product name" : name.Text, Theme.FS(10.5f), Theme.Text,
                new RectangleF(card.X + Theme.S(12), tile.Bottom + Theme.S(8), card.Width - Theme.S(24), Theme.S(40)), StringAlignment.Near, StringAlignment.Near, true);
            decimal p;
            Gfx.Text(g, Fmt.TryDecimal(price.Text, out p) ? Fmt.Money(p) : AppSettings.Currency + " 0.00", Theme.FB(11f), Theme.PrimaryDark,
                new RectangleF(card.X + Theme.S(12), card.Bottom - Theme.S(38), card.Width - Theme.S(24), Theme.S(28)));
        }

        void Save()
        {
            decimal p, c;
            int s, m;
            var cat = category.SelectedItem as Category;
            if (string.IsNullOrWhiteSpace(name.Text)) { Toast.Show(this, "Product name is required", ToastKind.Warning); return; }
            if (cat == null) { Toast.Show(this, "Choose a category", ToastKind.Warning); return; }
            if (!Fmt.TryDecimal(price.Text, out p) || p <= 0) { Toast.Show(this, "Enter a valid selling price", ToastKind.Warning); return; }
            if (!Fmt.TryDecimal(string.IsNullOrWhiteSpace(cost.Text) ? "0" : cost.Text, out c) || c < 0) { Toast.Show(this, "Enter a valid cost price", ToastKind.Warning); return; }
            decimal sd, md;
            Fmt.TryDecimal(string.IsNullOrWhiteSpace(stock.Text) ? "0" : stock.Text, out sd);
            Fmt.TryDecimal(string.IsNullOrWhiteSpace(minStock.Text) ? "0" : minStock.Text, out md);
            s = weighed.Checked ? (int)Math.Round(sd * 1000m) : (int)sd;
            m = weighed.Checked ? (int)Math.Round(md * 1000m) : (int)md;

            product.Name = name.Text.Trim();
            product.Barcode = barcode.Text.Trim();
            product.CategoryId = cat.CategoryId;
            product.Unit = unit.Text.Trim();
            product.Icon = icon.Text.Trim();
            product.ImagePath = imagePath;
            product.Price = p; product.CostPrice = c; product.Stock = s; product.MinStock = m;
            product.IsActive = active.Checked;
            product.IsWeighed = weighed.Checked;
            if (product.IsWeighed && string.IsNullOrWhiteSpace(product.Unit)) product.Unit = "per kg";
            try
            {
                ProductService.SaveProduct(product);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                Toast.Show(this, "That barcode is already used by another product", ToastKind.Error);
            }
            catch (Exception ex) { Msg.Info(this, "Could not save product", ex.Message, true); }
        }
    }

    /// <summary>Add / rename a category and pick its colour.</summary>
    public class CategoryDialog : DialogBase
    {
        static readonly string[] Palette =
        {
            "#16A34A", "#22C55E", "#3B82F6", "#0EA5E9", "#8B5CF6", "#EC4899",
            "#EF4444", "#F97316", "#F59E0B", "#14B8A6", "#64748B", "#A16207"
        };

        readonly Category category;
        readonly ModernTextBox name = new ModernTextBox { Placeholder = "e.g. Frozen Foods" };
        readonly Control swatches;
        string color;

        public CategoryDialog(Category existing) : base(existing == null ? "Add category" : "Edit category", 560, 470)
        {
            category = existing ?? new Category();
            color = category.Color ?? Palette[0];
            name.Text = category.Name ?? "";

            swatches = new SwatchPicker(Palette, () => color, c => { color = c; swatches.Invalidate(); });
            var canvas = AddCanvas();
            var nameField = Field("Category name", name);
            nameField.Dock = DockStyle.Top;
            var swField = Field("Colour", swatches, Theme.S(120));
            swField.Dock = DockStyle.Top;

            var save = new RoundButton { Text = "Save category", Kind = BtnKind.Primary, Width = Theme.S(190) };
            save.Click += (s, e) => Save();
            var cancel = new RoundButton { Text = "Cancel", Kind = BtnKind.Secondary, Width = Theme.S(130) };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            canvas.Controls.Add(swField);
            canvas.Controls.Add(nameField);
            Body.Controls.Add(ButtonRow(cancel, save));
        }

        void Save()
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { Toast.Show(this, "Category name is required", ToastKind.Warning); return; }
            category.Name = name.Text.Trim();
            category.Color = color;
            try
            {
                ProductService.SaveCategory(category);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                Toast.Show(this, "A category with that name already exists", ToastKind.Error);
            }
            catch (Exception ex) { Msg.Info(this, "Could not save category", ex.Message, true); }
        }

        class SwatchPicker : Control
        {
            readonly string[] colors;
            readonly Func<string> get;
            readonly Action<string> set;

            public SwatchPicker(string[] colors, Func<string> get, Action<string> set)
            {
                this.colors = colors; this.get = get; this.set = set;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
            }

            RectangleF Cell(int i)
            {
                int perRow = 6;
                float size = Theme.S(54), gap = (Width - perRow * size) / (perRow - 1);
                return new RectangleF((i % perRow) * (size + gap), (i / perRow) * (size + Theme.S(12)), size, size);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Gfx.SmoothOn(g);
                for (int i = 0; i < colors.Length; i++)
                {
                    var r = Cell(i);
                    Color c = Theme.Hex(colors[i]);
                    Gfx.FillRound(g, c, r, Theme.S(16));
                    if (string.Equals(get(), colors[i], StringComparison.OrdinalIgnoreCase))
                    {
                        Gfx.StrokeRound(g, Theme.Text, new RectangleF(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6), Theme.S(19), 2f);
                        Gfx.Icon(g, Icons.Check, 14f, Color.White, r);
                    }
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                for (int i = 0; i < colors.Length; i++)
                    if (Cell(i).Contains(e.Location)) { set(colors[i]); break; }
            }
        }
    }

    /// <summary>Add / edit a staff account (PIN based).</summary>
    public class UserDialog : DialogBase
    {
        readonly UserAccount user;
        readonly ModernTextBox fullName = new ModernTextBox { Placeholder = "e.g. Nimali Perera", Glyph = Icons.Person };
        readonly ModernTextBox username = new ModernTextBox { Placeholder = "login name" };
        readonly ModernCombo role = new ModernCombo();
        readonly ModernTextBox pin = new ModernTextBox { Mode = InputMode.Integer, MaxLength = 6, Password = true, Glyph = Icons.Key };
        readonly ModernTextBox pin2 = new ModernTextBox { Mode = InputMode.Integer, MaxLength = 6, Password = true, Glyph = Icons.Key };
        readonly CheckBox active = new CheckBox { Text = "Account is active", Checked = true, Font = Theme.F(10.5f), AutoSize = true, ForeColor = Theme.Text, BackColor = Color.White };

        public UserDialog(UserAccount existing)
            : base(existing == null ? "Add staff member" : "Edit staff member", 600, 720, "Staff sign in with a 4-6 digit PIN")
        {
            user = existing ?? new UserAccount { IsActive = true, Role = "Cashier" };
            role.SetItems(new object[] { "Cashier", "Admin" });
            role.Glyph = Icons.Person;
            pin.Placeholder = existing == null ? "4-6 digits" : "Leave blank to keep current PIN";
            pin2.Placeholder = "Repeat PIN";

            fullName.Text = user.FullName ?? "";
            username.Text = user.Username ?? "";
            role.SelectedItem = user.Role;
            active.Checked = user.IsActive;

            var canvas = AddCanvas();
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.White };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var f in new[] { Field("Full name", fullName), Field("Username", username), Field("Role", role), Field("PIN", pin), Field("Confirm PIN", pin2) })
            {
                f.Margin = new Padding(0);
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(88)));
                panel.Controls.Add(f);
            }
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(48)));
            active.Margin = new Padding(0, Theme.S(8), 0, 0);
            panel.Controls.Add(active);
            canvas.Controls.Add(panel);

            var save = new RoundButton { Text = "Save", Kind = BtnKind.Primary, Width = Theme.S(170) };
            save.Click += (s, e) => Save();
            var cancel = new RoundButton { Text = "Cancel", Kind = BtnKind.Secondary, Width = Theme.S(130) };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Body.Controls.Add(ButtonRow(cancel, save));
        }

        void Save()
        {
            string r = Convert.ToString(role.SelectedItem) ?? "Cashier";
            if (string.IsNullOrWhiteSpace(fullName.Text) || string.IsNullOrWhiteSpace(username.Text))
            { Toast.Show(this, "Name and username are required", ToastKind.Warning); return; }

            bool isNew = user.UserId == 0;
            if (isNew || pin.Text.Length > 0 || pin2.Text.Length > 0)
            {
                if (pin.Text.Length < 4) { Toast.Show(this, "PIN must be 4-6 digits", ToastKind.Warning); return; }
                if (pin.Text != pin2.Text) { Toast.Show(this, "PINs do not match", ToastKind.Warning); return; }
            }

            bool wasAdmin = user.IsAdmin && user.IsActive;
            bool staysAdmin = r == "Admin" && active.Checked;
            if (!isNew && wasAdmin && !staysAdmin && AuthService.ActiveAdminCount() <= 1)
            { Toast.Show(this, "At least one active admin is required", ToastKind.Warning); return; }

            user.FullName = fullName.Text.Trim();
            user.Username = username.Text.Trim().ToLowerInvariant();
            user.Role = r;
            user.IsActive = active.Checked;
            try
            {
                AuthService.SaveUser(user, pin.Text);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                Toast.Show(this, "That username is already taken", ToastKind.Error);
            }
            catch (Exception ex) { Msg.Info(this, "Could not save user", ex.Message, true); }
        }
    }
}
