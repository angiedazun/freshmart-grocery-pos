using System;
using System.Globalization;

namespace GroceryPOS.Services
{
    public static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Num(decimal v) { return v.ToString("N2", Inv); }
        public static string Money(decimal v) { return AppSettings.Currency + " " + Num(v); }
        public static string Weight(int grams)
        {
            return grams >= 1000 ? (grams / 1000m).ToString("0.###", Inv) + " kg" : grams + " g";
        }

        public static string Qty(bool weighed, int q) { return weighed ? Weight(q) : q.ToString(); }

        public static string Date(DateTime d) { return d.ToString("yyyy-MM-dd", Inv); }
        public static string DateTimeShort(DateTime d) { return d.ToString("yyyy-MM-dd HH:mm", Inv); }

        public static bool TryDecimal(string s, out decimal v)
        {
            return decimal.TryParse((s ?? "").Trim(), NumberStyles.Number, Inv, out v);
        }
    }
}
