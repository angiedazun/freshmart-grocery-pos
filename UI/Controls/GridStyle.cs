using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Flat, roomy DataGridView styling used by the back-office screens.</summary>
    public static class GridStyle
    {
        public static DataGridView Create()
        {
            var g = new DataGridView();
            Apply(g);
            return g;
        }

        public static void Apply(DataGridView g)
        {
            typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(g, true, null);
            g.BorderStyle = BorderStyle.None;
            g.BackgroundColor = Color.White;
            g.GridColor = Theme.Border;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToResizeColumns = false;
            g.ReadOnly = true;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = Theme.S(46);
            g.RowTemplate.Height = Theme.S(54);
            g.ScrollBars = ScrollBars.Vertical;
            g.StandardTab = true;

            g.ColumnHeadersDefaultCellStyle.BackColor = Theme.SurfaceAlt;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.SurfaceAlt;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.TextMuted;
            g.ColumnHeadersDefaultCellStyle.Font = Theme.FS(9f);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(Theme.S(12), 0, Theme.S(8), 0);
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            g.DefaultCellStyle.BackColor = Color.White;
            g.DefaultCellStyle.ForeColor = Theme.Text;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0xEA, 0xF1, 0xFB);
            g.DefaultCellStyle.SelectionForeColor = Theme.Text;
            g.DefaultCellStyle.Font = Theme.F(10.5f);
            g.DefaultCellStyle.Padding = new Padding(Theme.S(12), 0, Theme.S(8), 0);
            g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }

        public static DataGridViewTextBoxColumn Col(string header, string property, float weight, bool right = false, string format = null)
        {
            var c = new DataGridViewTextBoxColumn
            {
                HeaderText = header, DataPropertyName = property, FillWeight = weight,
                SortMode = DataGridViewColumnSortMode.NotSortable, Name = property
            };
            if (right)
            {
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (format != null) c.DefaultCellStyle.Format = format;
            return c;
        }

        /// <summary>Paints a rounded status pill inside a cell. Call from CellPainting and mark the event handled.</summary>
        public static void PaintPill(DataGridViewCellPaintingEventArgs e, string text, Color bg, Color fg)
        {
            e.PaintBackground(e.ClipBounds, true);
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            var f = Theme.FS(8.5f);
            float w = Gfx.Measure(g, text, f).Width + Theme.S(24);
            var r = new RectangleF(e.CellBounds.X + Theme.S(12), e.CellBounds.Y + (e.CellBounds.Height - Theme.S(28)) / 2f, w, Theme.S(28));
            Gfx.FillRound(g, bg, r, Theme.S(14));
            Gfx.Text(g, text, f, fg, r, StringAlignment.Center);
            e.Handled = true;
        }
    }

    /// <summary>Rounded date picker.</summary>
    public class DateBox : SurfacePanel
    {
        public readonly DateTimePicker Picker = new DateTimePicker();
        public event EventHandler ValueChanged;

        public DateBox()
        {
            Radius = 14;
            Height = Theme.S(50);
            Padding = new Padding(Theme.S(14), 0, Theme.S(10), 0);
            Picker.Format = DateTimePickerFormat.Custom;
            Picker.CustomFormat = "dd MMM yyyy";
            Picker.Font = Theme.F(11f);
            Picker.CalendarFont = Theme.F(11f);
            Picker.Value = DateTime.Today;
            Picker.ValueChanged += (s, e) => { if (ValueChanged != null) ValueChanged(this, e); };
            Controls.Add(Picker);
        }

        public DateTime Value { get { return Picker.Value.Date; } set { Picker.Value = value; } }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Picker.SetBounds(Theme.S(16), (Height - Picker.Height) / 2, Math.Max(10, Width - Theme.S(32)), Picker.Height);
        }
    }
}
