using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>KPI tile: tinted icon, label, big value and a caption.</summary>
    public class StatCard : Control
    {
        string title, value, caption, glyph;
        Color accent = Theme.Primary, captionColor = Theme.TextFaint;

        public StatCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Theme.S(240), Theme.S(104));
        }

        public string Title { get { return title; } set { title = value; Invalidate(); } }
        public string Value { get { return value; } set { this.value = value; Invalidate(); } }
        public string Caption { get { return caption; } set { caption = value; Invalidate(); } }
        public string Glyph { get { return glyph; } set { glyph = value; Invalidate(); } }
        decimal[] spark;
        public decimal[] Spark { get { return spark; } set { spark = value; Invalidate(); } }
        public Color CaptionColor { get { return captionColor; } set { captionColor = value; Invalidate(); } }
        public Color Accent { get { return accent; } set { accent = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Gfx.FillRound(g, Color.White, r, Theme.S(18));
            Gfx.StrokeRound(g, Theme.Border, r, Theme.S(18));

            float ic = Theme.S(48);
            var ib = new RectangleF(r.X + Theme.S(16), r.Y + Theme.S(16), ic, ic);
            Gfx.FillRound(g, Gfx.Lighten(accent, .86f), ib, Theme.S(14));
            Gfx.Icon(g, glyph, 15f, accent, ib);

            float tx = ib.Right + Theme.S(14);
            float w = r.Right - tx - Theme.S(10);
            Gfx.Text(g, title, Theme.F(9.5f), Theme.TextMuted, new RectangleF(tx, r.Y + Theme.S(12), w, Theme.S(22)));
            Gfx.Text(g, value, Theme.FB(17f), Theme.Text, new RectangleF(tx, r.Y + Theme.S(32), w, Theme.S(34)));
            Gfx.Text(g, caption, Theme.FS(8.5f), captionColor, new RectangleF(tx, r.Y + Theme.S(66), w, Theme.S(22)));

            if (spark != null && spark.Length > 1 && Width >= Theme.S(250))
            {
                var box = new RectangleF(r.Right - Theme.S(86), r.Y + Theme.S(14), Theme.S(66), Theme.S(24));
                decimal max = spark.Max();
                if (max <= 0) max = 1;
                var pts = new PointF[spark.Length];
                for (int i = 0; i < spark.Length; i++)
                    pts[i] = new PointF(box.X + box.Width * i / (spark.Length - 1), box.Bottom - box.Height * (float)(spark[i] / max));
                using (var area = new GraphicsPath())
                {
                    area.AddLines(pts);
                    area.AddLine(pts[pts.Length - 1].X, box.Bottom, pts[0].X, box.Bottom);
                    area.CloseFigure();
                    using (var br = new SolidBrush(Gfx.Alpha(accent, 30))) g.FillPath(br, area);
                }
                using (var pen = new Pen(accent, 2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(pen, pts);
                var last = pts[pts.Length - 1];
                Gfx.FillCircle(g, Color.White, new RectangleF(last.X - 4, last.Y - 4, 8, 8));
                Gfx.FillCircle(g, accent, new RectangleF(last.X - 2.5f, last.Y - 2.5f, 5, 5));
            }
        }
    }
}
