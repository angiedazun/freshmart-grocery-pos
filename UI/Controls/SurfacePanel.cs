using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Double-buffered panel for custom painting without flicker.</summary>
    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }
    }

    /// <summary>A rounded white card (optionally with a soft shadow) that hosts child controls.</summary>
    public class SurfacePanel : Panel
    {
        public int Radius { get; set; } = 18;
        public Color Fill { get; set; } = Color.White;
        public Color Stroke { get; set; } = Theme.Border;
        public bool Shadow { get; set; }

        public SurfacePanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.SmoothOn(g);
            int m = Shadow ? 6 : 0;
            var r = new RectangleF(m + 0.5f, m + 0.5f - (Shadow ? 2 : 0), Width - 1 - m * 2, Height - 1 - m * 2);
            float rad = Theme.S(Radius);
            if (Shadow) Gfx.Shadow(g, r, rad, 5, 8, 3);
            Gfx.FillRound(g, Fill, r, rad);
            if (Stroke != Color.Empty) Gfx.StrokeRound(g, Stroke, r, rad);
            base.OnPaint(e); // raises Paint so callers can draw custom content on the card
        }
    }
}
