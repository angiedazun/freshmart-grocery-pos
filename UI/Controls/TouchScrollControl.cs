using System;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    /// <summary>
    /// Base class for owner-drawn lists that scroll by finger-drag (with inertia) or mouse wheel,
    /// and distinguish a tap from a drag.
    /// </summary>
    public abstract class TouchScrollControl : Control
    {
        float scrollY, velocity;
        int contentHeight;
        bool down, dragging;
        Point downPt;
        float downScroll, lastY;
        int lastTick;
        readonly Timer inertia = new Timer { Interval = 15 };

        protected TouchScrollControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Theme.Bg;
            inertia.Tick += (s, e) =>
            {
                scrollY += velocity * inertia.Interval;
                velocity *= 0.93f;
                float before = scrollY;
                ClampScroll();
                if (Math.Abs(velocity) < 0.02f || before != scrollY) inertia.Stop();
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) inertia.Dispose();
            base.Dispose(disposing);
        }

        protected int Offset { get { return (int)Math.Round(scrollY); } }
        public int MaxScroll { get { return Math.Max(0, contentHeight - Height); } }

        protected void SetContentHeight(int h)
        {
            contentHeight = h;
            ClampScroll();
            Invalidate();
        }

        void ClampScroll()
        {
            if (scrollY > MaxScroll) scrollY = MaxScroll;
            if (scrollY < 0) scrollY = 0;
        }

        public void ScrollBy(float dy) { scrollY += dy; ClampScroll(); Invalidate(); }
        public void ScrollToTop() { scrollY = 0; Invalidate(); }
        public void ScrollToBottom() { scrollY = MaxScroll; Invalidate(); }

        public void EnsureVisible(Rectangle contentRect)
        {
            if (contentRect.Top < scrollY) scrollY = contentRect.Top;
            else if (contentRect.Bottom > scrollY + Height) scrollY = contentRect.Bottom - Height;
            ClampScroll();
            Invalidate();
        }

        protected abstract void PaintContent(Graphics g, Rectangle visible);
        protected virtual void OnRelayout() { }
        protected virtual void OnPressAt(Point contentPt) { }
        protected virtual void OnPressCancel() { }
        protected virtual void OnTapAt(Point contentPt) { }
        protected virtual void OnHoverAt(Point contentPt) { }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            OnRelayout();
            ClampScroll();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Gfx.SmoothOn(g);
            int off = Offset;
            g.TranslateTransform(0, -off);
            PaintContent(g, new Rectangle(0, off, Width, Height));
            g.ResetTransform();

            if (MaxScroll > 0)
            {
                float trackH = Height - 12;
                float thumbH = Math.Max(Theme.S(36), trackH * Height / contentHeight);
                float y = 6 + (trackH - thumbH) * (scrollY / MaxScroll);
                Gfx.FillRound(g, Gfx.Alpha(Theme.TextFaint, 150), new RectangleF(Width - 7, y, 4, thumbH), 2);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            inertia.Stop();
            velocity = 0;
            down = true; dragging = false;
            downPt = e.Location; downScroll = scrollY; lastY = scrollY; lastTick = Environment.TickCount;
            OnPressAt(new Point(e.X, e.Y + Offset));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (down)
            {
                int dy = e.Y - downPt.Y;
                if (!dragging && MaxScroll > 0 && Math.Abs(dy) > Theme.S(8)) { dragging = true; OnPressCancel(); }
                if (dragging)
                {
                    scrollY = downScroll - dy;
                    ClampScroll();
                    int now = Environment.TickCount, dt = now - lastTick;
                    if (dt > 0) { velocity = 0.6f * velocity + 0.4f * ((scrollY - lastY) / dt); lastTick = now; lastY = scrollY; }
                    Invalidate();
                }
            }
            else OnHoverAt(new Point(e.X, e.Y + Offset));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!down) return;
            down = false;
            if (!dragging)
            {
                OnPressCancel();
                if (ClientRectangle.Contains(e.Location)) OnTapAt(new Point(e.X, e.Y + Offset));
            }
            else if (Environment.TickCount - lastTick < 90 && Math.Abs(velocity) > 0.1f) inertia.Start();
            dragging = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!down) OnHoverAt(new Point(-1, -1));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            inertia.Stop();
            scrollY -= e.Delta * 0.7f;
            ClampScroll();
            Invalidate();
        }
    }
}
