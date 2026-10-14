using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GroceryPOS.UI.Controls
{
    public struct FlowItem
    {
        public Control C;
        public int W;
        public int Flex;
        public FlowItem(Control c, int w, int flex = 0) { C = c; W = w; Flex = flex; }
    }

    /// <summary>Tiny layout helpers so toolbars wrap gracefully on small / high-DPI screens.</summary>
    public static class Layouts
    {
        /// <summary>
        /// Places items left-to-right, wrapping to a new row when they do not fit. Leftover width in a row is
        /// shared between items with Flex > 0. Returns the total height used.
        /// </summary>
        public static int Flow(int x0, int y0, int availW, int rowH, int gap, IList<FlowItem> items)
        {
            var row = new List<FlowItem>();
            int used = 0, y = y0;
            foreach (var it in items)
            {
                int need = it.W + (row.Count > 0 ? gap : 0);
                if (row.Count > 0 && used + need > availW)
                {
                    Place(row, x0, y, availW, used, rowH, gap);
                    y += rowH + gap;
                    row.Clear();
                    used = 0;
                    need = it.W;
                }
                row.Add(it);
                used += need;
            }
            if (row.Count > 0) { Place(row, x0, y, availW, used, rowH, gap); y += rowH + gap; }
            return Math.Max(0, y - gap - y0);
        }

        static void Place(List<FlowItem> row, int x0, int y, int availW, int used, int rowH, int gap)
        {
            int free = Math.Max(0, availW - used), flex = 0;
            foreach (var it in row) flex += it.Flex;
            int x = x0;
            foreach (var it in row)
            {
                int w = it.W + (flex > 0 && it.Flex > 0 ? free * it.Flex / flex : 0);
                it.C.SetBounds(x, y, w, rowH);
                x += w + gap;
            }
        }

        /// <summary>Lays a host's children out left-to-right and sizes the host to fit. Returns the width.</summary>
        public static int Pack(Control host, int height, int gap)
        {
            int x = 0;
            foreach (Control c in host.Controls)
            {
                c.SetBounds(x, 0, c.Width, height);
                x += c.Width + gap;
            }
            int w = Math.Max(0, x - gap);
            host.Size = new Size(w, height);
            return w;
        }

        /// <summary>A transparent container for a group of buttons.</summary>
        public static Panel ButtonGroup(params Control[] buttons)
        {
            var p = new Panel { BackColor = Color.Transparent };
            foreach (var b in buttons) p.Controls.Add(b);
            Pack(p, Theme.S(52), Theme.S(12));
            return p;
        }
    }
}
