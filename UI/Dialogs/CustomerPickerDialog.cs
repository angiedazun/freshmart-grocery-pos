using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Controls;

namespace GroceryPOS.UI.Dialogs
{
    /// <summary>Pick (or quickly add) a customer for a store-credit sale.</summary>
    public class CustomerPickerDialog : DialogBase
    {
        readonly ModernTextBox search = new ModernTextBox { Glyph = Icons.Search, Placeholder = "Search by name or phone" };
        readonly ListBox list = new ListBox();
        readonly ModernTextBox newName = new ModernTextBox { Placeholder = "Full name" };
        readonly ModernTextBox newPhone = new ModernTextBox { Placeholder = "Phone (optional)" };
        readonly RoundButton addBtn = new RoundButton { Text = "Add", Kind = BtnKind.Soft, Glyph = Icons.Add, GlyphSize = 11f };
        readonly RoundButton select;
        readonly Panel newPanel = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(150), BackColor = Color.White };

        readonly Label hint = new Label { Text = "No customers found - add one below", Dock = DockStyle.Top, Height = Theme.S(90), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextMuted, Font = Theme.F(10.5f), BackColor = Color.White };

        public Customer Selected { get; private set; }

        public CustomerPickerDialog() : base("Select customer", 640, 720, "Credit sales are added to the customer's account balance")
        {
            list.BorderStyle = BorderStyle.None;
            list.Dock = DockStyle.Fill;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = Theme.S(68);
            list.IntegralHeight = false;
            list.BackColor = Color.White;
            list.DrawItem += DrawRow;
            list.DoubleClick += (s, e) => Commit();
            list.Controls.Add(hint);
            list.SelectedIndexChanged += (s, e) => select.Enabled = list.SelectedItem != null;

            // new-customer strip
            var cap = new Label { Text = "New customer", Font = Theme.FS(9.5f), ForeColor = Theme.TextMuted, AutoSize = false, Height = Theme.S(28), Dock = DockStyle.Top, BackColor = Color.Transparent, TextAlign = ContentAlignment.BottomLeft };
            var fields = new Panel { Dock = DockStyle.Top, Height = Theme.S(66), BackColor = Color.White };
            fields.Controls.Add(newName);
            fields.Controls.Add(newPhone);
            fields.Controls.Add(addBtn);
            fields.Resize += (s, e) =>
            {
                int w = fields.Width, g = Theme.S(10), bw = Theme.S(110);
                int each = (w - bw - g * 2) / 2;
                newName.SetBounds(0, Theme.S(8), each, Theme.S(50));
                newPhone.SetBounds(each + g, Theme.S(8), each, Theme.S(50));
                addBtn.SetBounds(w - bw, Theme.S(8), bw, Theme.S(50));
            };
            newPanel.Controls.Add(fields);
            newPanel.Controls.Add(cap);
            addBtn.Click += (s, e) => AddNew();

            select = new RoundButton { Text = "Select customer", Kind = BtnKind.Primary, Width = Theme.S(200), Enabled = false };
            select.Click += (s, e) => Commit();
            var cancel = new RoundButton { Text = "Cancel", Kind = BtnKind.Secondary, Width = Theme.S(120) };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            search.TextChanged += (s, e) => LoadList();

            Body.Controls.Add(list);
            Body.Controls.Add(newPanel);
            Body.Controls.Add(ButtonRow(cancel, select));
            Body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = Theme.S(10), BackColor = Color.White });
            search.Dock = DockStyle.Top;
            search.Height = Theme.S(50);
            Body.Controls.Add(search);
            LoadList();
        }

        void LoadList()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var c in ProductService.GetCustomers(search.Text)) list.Items.Add(c);
            list.EndUpdate();
            hint.Visible = list.Items.Count == 0;
            select.Enabled = false;
        }

        void DrawRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var c = (Customer)list.Items[e.Index];
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            bool sel = (e.State & DrawItemState.Selected) != 0;
            g.FillRectangle(Brushes.White, e.Bounds);
            var r = new RectangleF(e.Bounds.X + 2, e.Bounds.Y + 4, e.Bounds.Width - 6, e.Bounds.Height - 8);
            if (sel) { Gfx.FillRound(g, Theme.PrimarySoft, r, Theme.S(14)); Gfx.StrokeRound(g, Theme.Primary, r, Theme.S(14)); }

            var av = new RectangleF(r.X + Theme.S(12), r.Y + (r.Height - Theme.S(42)) / 2f, Theme.S(42), Theme.S(42));
            Gfx.FillCircle(g, Theme.InfoSoft, av);
            Gfx.Text(g, ProductGrid.Initials(c.Name), Theme.FB(10f), Theme.Info, av, StringAlignment.Center);
            Gfx.Text(g, c.Name, Theme.FS(11f), Theme.Text, new RectangleF(av.Right + Theme.S(14), r.Y + Theme.S(6), r.Width - Theme.S(220), Theme.S(26)));
            Gfx.Text(g, string.IsNullOrEmpty(c.Phone) ? "No phone" : c.Phone, Theme.F(9f), Theme.TextMuted,
                new RectangleF(av.Right + Theme.S(14), r.Y + Theme.S(30), r.Width - Theme.S(220), Theme.S(22)));
            if (c.Balance > 0)
                Gfx.Text(g, "Owes " + Fmt.Money(c.Balance), Theme.FS(9.5f), Theme.WarningDark,
                    new RectangleF(r.X, r.Y, r.Width - Theme.S(14), r.Height), StringAlignment.Far);
        }

        void AddNew()
        {
            if (string.IsNullOrWhiteSpace(newName.Text)) { Toast.Show(this, "Enter the customer's name", ToastKind.Warning); return; }
            try
            {
                var c = ProductService.AddCustomer(newName.Text, newPhone.Text);
                newName.Text = ""; newPhone.Text = ""; search.Text = "";
                LoadList();
                for (int i = 0; i < list.Items.Count; i++)
                    if (((Customer)list.Items[i]).CustomerId == c.CustomerId) { list.SelectedIndex = i; break; }
            }
            catch (Exception ex) { Msg.Info(this, "Could not add customer", ex.Message, true); }
        }

        void Commit()
        {
            if (list.SelectedItem == null) return;
            Selected = (Customer)list.SelectedItem;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
