using System;
using System.Collections.Generic;
using System.Globalization;
using GroceryPOS.Data;
using GroceryPOS.Models;

namespace GroceryPOS.Services
{
    /// <summary>Store settings (cached in memory, persisted in dbo.Settings).</summary>
    public static class AppSettings
    {
        static Dictionary<string, string> cache = new Dictionary<string, string>();

        public static void Load()
        {
            var d = new Dictionary<string, string>();
            foreach (System.Data.DataRow r in Db.Query("SELECT K, V FROM dbo.Settings").Rows)
                d[Convert.ToString(r["K"])] = r["V"] == DBNull.Value ? "" : Convert.ToString(r["V"]);
            cache = d;
        }

        public static string Get(string key, string fallback = "")
        {
            string v;
            return cache.TryGetValue(key, out v) ? v : fallback;
        }

        public static void Set(string key, string value)
        {
            Db.Exec(@"IF EXISTS (SELECT 1 FROM dbo.Settings WHERE K=@k) UPDATE dbo.Settings SET V=@v WHERE K=@k
                      ELSE INSERT INTO dbo.Settings(K,V) VALUES (@k,@v)", Db.P("@k", key), Db.P("@v", value ?? ""));
            cache[key] = value ?? "";
        }

        public static string StoreName { get { return Get("StoreName", "FreshMart Grocery"); } }
        public static string StoreAddress { get { return Get("StoreAddress"); } }
        public static string StorePhone { get { return Get("StorePhone"); } }
        public static string Currency { get { return Get("Currency", "Rs."); } }
        public static string ReceiptFooter { get { return Get("ReceiptFooter", "Thank you!"); } }
        public static bool AutoPrint { get { return Get("AutoPrint", "1") == "1"; } }
        public static string Printer { get { return Get("Printer"); } }

        public static decimal TaxRate
        {
            get
            {
                decimal d;
                return decimal.TryParse(Get("TaxRate", "0"), NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : 0m;
            }
        }
    }

    /// <summary>The signed-in cashier / manager.</summary>
    public static class Session
    {
        public static UserAccount User { get; set; }
    }
}
