using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using GroceryPOS.Models;

namespace GroceryPOS.Services
{
    /// <summary>Builds fixed-width (42 col) receipts and prints them through System.Drawing.Printing.</summary>
    public static class ReceiptPrinter
    {
        public const int Cols = 42;

        static string Center(string s)
        {
            if (s == null) s = "";
            if (s.Length >= Cols) return s.Substring(0, Cols);
            return new string(' ', (Cols - s.Length) / 2) + s;
        }

        static string Row(string left, string right)
        {
            left = left ?? ""; right = right ?? "";
            int space = Cols - right.Length - 1;
            if (left.Length > space) left = left.Substring(0, Math.Max(0, space));
            return left.PadRight(Cols - right.Length) + right;
        }

        static string Rule(char c = '-') { return new string(c, Cols); }

        public static List<string> BuildSale(Sale s, bool duplicate = false)
        {
            var L = new List<string>();
            L.Add(Center(AppSettings.StoreName.ToUpper()));
            if (!string.IsNullOrWhiteSpace(AppSettings.StoreAddress)) L.Add(Center(AppSettings.StoreAddress));
            if (!string.IsNullOrWhiteSpace(AppSettings.StorePhone)) L.Add(Center("Tel: " + AppSettings.StorePhone));
            L.Add(Rule());
            L.Add(Row("Invoice:", s.InvoiceNo));
            L.Add(Row("Date:", s.SaleDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
            L.Add(Row("Cashier:", s.CashierName));
            if (!string.IsNullOrEmpty(s.CustomerName)) L.Add(Row("Customer:", s.CustomerName));
            L.Add(Rule());
            foreach (var i in s.Items)
            {
                string name = i.ProductName + (i.IsWeighed || string.IsNullOrEmpty(i.Unit) ? "" : " " + i.Unit);
                L.Add(name.Length > Cols ? name.Substring(0, Cols) : name);
                L.Add(Row("   " + Fmt.Qty(i.IsWeighed, i.Quantity) + " x " + Fmt.Num(i.UnitPrice) + (i.IsWeighed ? "/kg" : ""), Fmt.Num(i.LineTotal)));
            }
            L.Add(Rule());
            L.Add(Row("Items", s.ItemCount.ToString()));
            L.Add(Row("Subtotal", Fmt.Num(s.Subtotal)));
            if (s.DiscountAmount > 0) L.Add(Row("Discount", "-" + Fmt.Num(s.DiscountAmount)));
            if (s.TaxAmount > 0) L.Add(Row("Tax (" + s.TaxRate.ToString("0.##", CultureInfo.InvariantCulture) + "%)", Fmt.Num(s.TaxAmount)));
            L.Add(Rule('='));
            L.Add(Row("TOTAL", AppSettings.Currency + " " + Fmt.Num(s.Total)));
            L.Add(Rule('='));
            L.Add(Row("Paid by", s.PaymentMethod));
            if (s.PaymentMethod == "Cash")
            {
                L.Add(Row("Given", Fmt.Num(s.AmountGiven)));
                L.Add(Row("Change", Fmt.Num(s.ChangeAmount)));
            }
            else if (s.PaymentMethod == "Credit")
            {
                L.Add(Row("Charged to account", s.CustomerName ?? ""));
            }
            else if (!string.IsNullOrEmpty(s.Reference))
            {
                L.Add(Row("Reference", s.Reference));
            }
            if (s.Status == "Voided") { L.Add(Rule()); L.Add(Center("*** VOIDED ***")); }
            L.Add(Rule());
            if (!string.IsNullOrWhiteSpace(AppSettings.ReceiptFooter)) L.Add(Center(AppSettings.ReceiptFooter));
            if (duplicate) L.Add(Center("-- DUPLICATE COPY --"));
            return L;
        }

        public static List<string> BuildZ(ZReport z)
        {
            var L = new List<string>();
            L.Add(Center(AppSettings.StoreName.ToUpper()));
            L.Add(Center("Z-REPORT / SHIFT SUMMARY"));
            L.Add(Rule());
            L.Add(Row("Date:", z.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            L.Add(Row("Cashier:", z.CashierName));
            L.Add(Row("Printed:", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
            L.Add(Rule());
            L.Add(Row("Transactions", z.Transactions.ToString()));
            L.Add(Row("Items sold", z.ItemsSold.ToString()));
            L.Add(Row("Gross sales", Fmt.Num(z.GrossSales)));
            L.Add(Row("Discounts given", "-" + Fmt.Num(z.Discounts)));
            L.Add(Row("Tax collected", Fmt.Num(z.Tax)));
            L.Add(Rule('='));
            L.Add(Row("NET SALES", AppSettings.Currency + " " + Fmt.Num(z.NetSales)));
            L.Add(Rule('='));
            L.Add(Row("Cash collected", Fmt.Num(z.Cash)));
            L.Add(Row("Card payments", Fmt.Num(z.Card)));
            L.Add(Row("Credit (store account)", Fmt.Num(z.Credit)));
            L.Add(Rule());
            L.Add(Row("Voided sales (" + z.VoidedCount + ")", Fmt.Num(z.VoidedAmount)));
            L.Add(Row("Average basket", Fmt.Num(z.AverageBasket)));
            L.Add(Rule());
            L.Add(Center("End of report"));
            return L;
        }

        public static bool IsVirtualPrinter(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string n = name.ToLowerInvariant();
            return n.Contains("pdf") || n.Contains("xps") || n.Contains("onenote") || n.Contains("fax");
        }

        public static string DefaultPrinterName()
        {
            try { return new PrinterSettings().PrinterName; } catch { return ""; }
        }

        /// <summary>Called after checkout. Skips silently when auto-print is off or only virtual printers exist.</summary>
        public static bool AutoPrint(IList<string> lines)
        {
            if (!AppSettings.AutoPrint) return false;
            string name = AppSettings.Printer;
            if (string.IsNullOrEmpty(name))
            {
                name = DefaultPrinterName();
                if (IsVirtualPrinter(name)) return false;
            }
            string err;
            return Print(lines, name, out err);
        }

        public static bool Print(IList<string> lines, string printerName, out string error)
        {
            error = null;
            try
            {
                using (var pd = new PrintDocument())
                {
                    if (!string.IsNullOrEmpty(printerName)) pd.PrinterSettings.PrinterName = printerName;
                    if (!pd.PrinterSettings.IsValid) { error = "The selected printer is not available."; return false; }

                    pd.DocumentName = "Receipt";
                    pd.PrintController = new StandardPrintController();
                    pd.DefaultPageSettings.Margins = new Margins(10, 10, 10, 10);

                    // Prefer an 80mm roll size when the driver offers one.
                    try
                    {
                        foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
                            if (ps.Width >= 270 && ps.Width <= 330) { pd.DefaultPageSettings.PaperSize = ps; break; }
                    }
                    catch { }

                    int idx = 0;
                    pd.PrintPage += (s, e) =>
                    {
                        using (var f = new Font("Consolas", 8f))
                        {
                            float lh = f.GetHeight(e.Graphics);
                            float y = e.MarginBounds.Top;
                            while (idx < lines.Count)
                            {
                                if (y + lh > e.MarginBounds.Bottom && y > e.MarginBounds.Top) { e.HasMorePages = true; return; }
                                e.Graphics.DrawString(lines[idx], f, Brushes.Black, e.MarginBounds.Left, y);
                                y += lh;
                                idx++;
                            }
                            e.HasMorePages = false;
                        }
                    };
                    pd.Print();
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
