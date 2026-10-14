using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Rounded drop-down selector with a touch-sized popup list.</summary>
    public class ModernCombo : Control
    {
        readonly List<object> items = new List<object>();
        int selected = -1;
        bool hover, open;
        ToolStripDropDown dropDown;

        public event EventHandler SelectedIndexChanged;
        public string Placeholder { get; set; } = "Select...";
        public string Glyph { get; set; }

        public ModernCombo()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.F(11f);
            Height = Theme.S(50);
        }

        public IList<object> Items { get { return items; } }

        public void SetItems(IEnumerable<object> list)
        {
            items.Clear();
            items.AddRange(list);
            selected = items.Count > 0 ? 0 : -1;
            Invalidate();
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value == selected) return;
                selected = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return selected >= 0 && selected < items.Count ? items[selected] : null; }
            set { int i = items.IndexOf(value); if (i >= 0) SelectedIndex = i; }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Theme.S(14);
            Gfx.FillRound(g, hover ? Theme.SurfaceAlt : Color.White, r, rad);
            Gfx.StrokeRound(g, open ? Theme.Primary : Theme.Border, r, rad, open ? 2f : 1f);
            float x = Theme.S(16);
            if (!string.IsNullOrEmpty(Glyph))
            {
                Gfx.Icon(g, Glyph, 12f, Theme.TextFaint, new RectangleF(Theme.S(12), 0, Theme.S(28), Height));
                x += Theme.S(28);
            }
            string t = SelectedItem == null ? Placeholder : SelectedItem.ToString();
            Gfx.Text(g, t, Font, SelectedItem == null ? Theme.TextFaint : Theme.Text, new RectangleF(x, 0, Width - x - Theme.S(44), Height));
            Gfx.Icon(g, "\uE70D", 9f, Theme.TextMuted, new RectangleF(Width - Theme.S(40), 0, Theme.S(28), Height));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) ShowPopup();
        }

        void ShowPopup()
        {
            if (items.Count == 0) return;
            int rowH = Theme.S(46);
            var list = new ListBox
            {
                BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = rowH,
                IntegralHeight = false, BackColor = Color.White, Font = Font
            };
            foreach (var it in items) list.Items.Add(it);
            list.SelectedIndex = selected;
            int visible = Math.Min(items.Count, 7);
            list.Size = new Size(Width, visible * rowH + 2);
            list.DrawItem += (s, ev) =>
            {
                if (ev.Index < 0) return;
                var gg = ev.Graphics;
                Gfx.SmoothOn(gg);
                bool sel = ev.Index == selected;
                bool hot = (ev.State & DrawItemState.Selected) != 0 || ev.Index == hoverIdx;
                gg.FillRectangle(Brushes.White, ev.Bounds);
                var rr = new RectangleF(ev.Bounds.X + 4, ev.Bounds.Y + 2, ev.Bounds.Width - 8, ev.Bounds.Height - 4);
                if (sel) Gfx.FillRound(gg, Theme.PrimarySoft, rr, Theme.S(10));
                else if (hot) Gfx.FillRound(gg, Theme.SurfaceAlt, rr, Theme.S(10));
                Gfx.Text(gg, list.Items[ev.Index].ToString(), Font, sel ? Theme.PrimaryDark : Theme.Text,
                    new RectangleF(rr.X + Theme.S(12), rr.Y, rr.Width - Theme.S(24), rr.Height));
                if (sel) Gfx.Icon(gg, Icons.Check, 10f, Theme.PrimaryDark, new RectangleF(rr.Right - Theme.S(34), rr.Y, Theme.S(24), rr.Height));
            };
            list.MouseMove += (s, ev) =>
            {
                int i = list.IndexFromPoint(ev.Location);
                if (i != hoverIdx) { hoverIdx = i; list.Invalidate(); }
            };
            list.MouseUp += (s, ev) =>
            {
                int i = list.IndexFromPoint(ev.Location);
                if (i >= 0) { SelectedIndex = i; if (dropDown != null) dropDown.Close(); }
            };

            var host = new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size };
            dropDown = new ToolStripDropDown { Padding = Padding.Empty, AutoClose = true, BackColor = Color.White };
            dropDown.Items.Add(host);
            dropDown.Closed += (s, ev) => { open = false; Invalidate(); };
            open = true;
            hoverIdx = -1;
            Invalidate();
            dropDown.Show(this, new Point(0, Height + Theme.S(4)));
        }

        int hoverIdx = -1;
    }
}
