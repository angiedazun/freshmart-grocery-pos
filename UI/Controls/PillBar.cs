using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Category "pills" that wrap onto extra rows so every category is always visible.</summary>
    public class PillBar : Control
    {
        List<string> items = new List<string>();
        List<Color> dots = new List<Color>();
        readonly List<RectangleF> rects = new List<RectangleF>();
        int selected;

        public event EventHandler SelectedChanged;
        public int PillHeight { get; set; } = Theme.S(44);

        public PillBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Height = Theme.S(48);
            Font = Theme.FS(10.5f);
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return selected; }
            set { selected = value; Invalidate(); if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); }
        }

        public string SelectedItem { get { return selected >= 0 && selected < items.Count ? items[selected] : null; } }

        public void SetItems(IList<string> list, IList<Color> dotColors = null)
        {
            string prev = SelectedItem;
            items = new List<string>(list);
            dots = dotColors == null ? new List<Color>() : new List<Color>(dotColors);
            selected = Math.Max(0, items.IndexOf(prev));
            Invalidate();
        }

        /// <summary>Height needed to show every pill when the bar is <paramref name="width"/> wide.</summary>
        public int MeasureHeight(int width)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                int rows = Place(g, Math.Max(width, Theme.S(200)));
                return rows * PillHeight + (rows - 1) * Theme.S(8) + Theme.S(4);
            }
        }

        /// <summary>Computes pill rectangles (wrapping) and returns the number of rows.</summary>
        int Place(Graphics g, int width)
        {
            rects.Clear();
            float x = 0, y = 2, pad = Theme.S(18), gap = Theme.S(8);
            int rows = 1;
            for (int i = 0; i < items.Count; i++)
            {
                float dot = i > 0 && i - 1 < dots.Count ? Theme.S(16) : 0;
                float w = Gfx.Measure(g, items[i], Font).Width + pad * 2 + dot;
                if (x > 0 && x + w > width) { x = 0; y += PillHeight + Theme.S(8); rows++; }
                rects.Add(new RectangleF(x, y, w, PillHeight));
                x += w + gap;
            }
            return rows;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            Place(g, Width);
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                bool sel = i == selected;
                if (sel) { Gfx.Shadow(g, r, r.Height / 2f, 3, 10, 2); Gfx.FillRound(g, Theme.Primary, r, r.Height / 2f); }
                else { Gfx.FillRound(g, Color.White, r, r.Height / 2f); Gfx.StrokeRound(g, Theme.Border, r, r.Height / 2f); }

                float tx = r.X;
                if (i > 0 && i - 1 < dots.Count)
                {
                    float ds = Theme.S(9);
                    Gfx.FillCircle(g, sel ? Color.White : dots[i - 1], new RectangleF(r.X + Theme.S(16), r.Y + (r.Height - ds) / 2f, ds, ds));
                    tx += Theme.S(13);
                }
                Gfx.Text(g, items[i], Font, sel ? Color.White : Theme.Text, new RectangleF(tx, r.Y, r.Width - (tx - r.X), r.Height), StringAlignment.Center);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            for (int i = 0; i < rects.Count; i++)
                if (rects[i].Contains(e.Location)) { if (i != selected) SelectedIndex = i; break; }
        }
    }
    /// <summary>Segmented switch (e.g. Cash | Card | Credit).</summary>
    public class SegmentedControl : Control
    {
        List<string> items = new List<string>();
        int selected;

        public event EventHandler SelectedChanged;

        public SegmentedControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Height = Theme.S(50);
            Font = Theme.FS(10.5f);
            Cursor = Cursors.Hand;
        }

        public string[] Items
        {
            get { return items.ToArray(); }
            set { items = new List<string>(value); selected = 0; Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return selected; }
            set { if (value == selected) return; selected = value; Invalidate(); if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            var all = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Gfx.FillRound(g, Theme.Track, all, all.Height / 2.6f);
            if (items.Count == 0) return;
            float w = (all.Width - 8) / items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                var r = new RectangleF(all.X + 4 + i * w, all.Y + 4, w, all.Height - 8);
                bool sel = i == selected;
                if (sel)
                {
                    Gfx.Shadow(g, r, r.Height / 2.6f, 2, 10, 1);
                    Gfx.FillRound(g, Color.White, r, r.Height / 2.6f);
                }
                Gfx.Text(g, items[i], Font, sel ? Theme.PrimaryDark : Theme.TextMuted, r, StringAlignment.Center);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (items.Count == 0 || !ClientRectangle.Contains(e.Location)) return;
            float w = (Width - 8f) / items.Count;
            int i = (int)((e.X - 4) / w);
            if (i >= 0 && i < items.Count) SelectedIndex = i;
        }
    }
}
