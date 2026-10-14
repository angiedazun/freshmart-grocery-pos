using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using GroceryPOS.Data;

namespace GroceryPOS.Services
{
    public class DayPoint { public DateTime Day; public decimal Total; public int Orders; }
    public class TopProduct { public string Name, Icon, Color; public int Qty; public bool Weighed; public decimal Revenue; }
    public class CategorySale { public string Name, Color; public decimal Amount; }
    public class StaffSale { public string Name; public int Orders; public decimal Amount; }
    public class RecentSale { public string InvoiceNo, Cashier, Method, Status; public DateTime When; public decimal Total; }

    /// <summary>Aggregate queries for the dashboard home page. A null userId means "all cashiers".</summary>
    public static class DashboardService
    {
        static SqlParameter Uid(int? userId)
        {
            return new SqlParameter("@uid", SqlDbType.Int) { Value = userId.HasValue ? (object)userId.Value : DBNull.Value };
        }

        /// <summary>One point per day for the last <paramref name="days"/> days (missing days are zero), oldest first.</summary>
        public static List<DayPoint> Daily(int days, int? userId)
        {
            var from = DateTime.Today.AddDays(-(days - 1));
            var dt = Db.Query(@"SELECT CAST(SaleDate AS DATE) AS D, SUM(Total) AS T, COUNT(*) AS C FROM dbo.Sales
                                WHERE Status='Completed' AND SaleDate >= @from AND (@uid IS NULL OR UserId=@uid)
                                GROUP BY CAST(SaleDate AS DATE)", Db.P("@from", from), Uid(userId));
            var map = new Dictionary<DateTime, DayPoint>();
            foreach (DataRow r in dt.Rows)
            {
                var d = Convert.ToDateTime(r["D"]).Date;
                map[d] = new DayPoint { Day = d, Total = Convert.ToDecimal(r["T"]), Orders = Convert.ToInt32(r["C"]) };
            }
            var list = new List<DayPoint>();
            for (int i = 0; i < days; i++)
            {
                var d = from.AddDays(i);
                list.Add(map.ContainsKey(d) ? map[d] : new DayPoint { Day = d });
            }
            return list;
        }

        public static List<TopProduct> Top(int days, int take, int? userId)
        {
            var list = new List<TopProduct>();
            var dt = Db.Query(@"SELECT TOP (@n) i.ProductName, SUM(i.Quantity) AS Q, SUM(i.LineTotal) AS R, MAX(CAST(i.IsWeighed AS INT)) AS W, MAX(p.Icon) AS Icon, MAX(c.Color) AS Color
                                FROM dbo.SaleItems i
                                JOIN dbo.Sales s ON s.SaleId = i.SaleId
                                LEFT JOIN dbo.Products p ON p.ProductId = i.ProductId
                                LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
                                WHERE s.Status='Completed' AND s.SaleDate >= @from AND (@uid IS NULL OR s.UserId=@uid)
                                GROUP BY i.ProductName ORDER BY SUM(i.LineTotal) DESC",
                Db.P("@n", take), Db.P("@from", DateTime.Today.AddDays(-(days - 1))), Uid(userId));
            foreach (DataRow r in dt.Rows)
                list.Add(new TopProduct
                {
                    Name = Convert.ToString(r["ProductName"]), Qty = Convert.ToInt32(r["Q"]), Weighed = Convert.ToInt32(r["W"]) == 1, Revenue = Convert.ToDecimal(r["R"]),
                    Icon = r["Icon"] == DBNull.Value ? null : Convert.ToString(r["Icon"]),
                    Color = r["Color"] == DBNull.Value ? "#16A34A" : Convert.ToString(r["Color"])
                });
            return list;
        }

        /// <summary>Sales split by payment method (cash, card, credit) over the last days.</summary>
        public static decimal[] PaymentMix(int days, int? userId)
        {
            var mix = new decimal[3];
            var dt = Db.Query(@"SELECT PaymentMethod, SUM(Total) AS T FROM dbo.Sales
                                WHERE Status='Completed' AND SaleDate >= @from AND (@uid IS NULL OR UserId=@uid) GROUP BY PaymentMethod",
                Db.P("@from", DateTime.Today.AddDays(-(days - 1))), Uid(userId));
            foreach (DataRow r in dt.Rows)
            {
                string m = Convert.ToString(r["PaymentMethod"]);
                int i = m == "Cash" ? 0 : m == "Card" ? 1 : 2;
                mix[i] += Convert.ToDecimal(r["T"]);
            }
            return mix;
        }

        /// <summary>Today's completed sales per hour of day (index 0-23).</summary>
        public static decimal[] Hourly(int? userId)
        {
            var h = new decimal[24];
            var dt = Db.Query(@"SELECT DATEPART(HOUR, SaleDate) AS H, SUM(Total) AS T FROM dbo.Sales
                                WHERE Status='Completed' AND SaleDate >= @d AND SaleDate < @d2 AND (@uid IS NULL OR UserId=@uid)
                                GROUP BY DATEPART(HOUR, SaleDate)",
                Db.P("@d", DateTime.Today), Db.P("@d2", DateTime.Today.AddDays(1)), Uid(userId));
            foreach (DataRow r in dt.Rows) h[Convert.ToInt32(r["H"])] = Convert.ToDecimal(r["T"]);
            return h;
        }

        public static List<CategorySale> ByCategory(int days, int take, int? userId)
        {
            var list = new List<CategorySale>();
            var dt = Db.Query(@"SELECT TOP (@n) ISNULL(c.Name, 'Other') AS N, ISNULL(MAX(c.Color), '#94A3B8') AS C, SUM(i.LineTotal) AS A
                                FROM dbo.SaleItems i
                                JOIN dbo.Sales s ON s.SaleId = i.SaleId
                                LEFT JOIN dbo.Products p ON p.ProductId = i.ProductId
                                LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
                                WHERE s.Status='Completed' AND s.SaleDate >= @from AND (@uid IS NULL OR s.UserId=@uid)
                                GROUP BY ISNULL(c.Name, 'Other') ORDER BY SUM(i.LineTotal) DESC",
                Db.P("@n", take), Db.P("@from", DateTime.Today.AddDays(-(days - 1))), Uid(userId));
            foreach (DataRow r in dt.Rows)
                list.Add(new CategorySale { Name = Convert.ToString(r["N"]), Color = Convert.ToString(r["C"]), Amount = Convert.ToDecimal(r["A"]) });
            return list;
        }

        /// <summary>Estimated gross profit for today: line totals minus product cost (before sale-level discounts).</summary>
        public static decimal ProfitToday(int? userId)
        {
            var v = Db.Scalar(@"SELECT SUM(i.LineTotal - CASE WHEN i.IsWeighed = 1 THEN ISNULL(p.CostPrice, 0) * i.Quantity / 1000.0
                                                              ELSE ISNULL(p.CostPrice, 0) * i.Quantity END)
                                FROM dbo.SaleItems i
                                JOIN dbo.Sales s ON s.SaleId = i.SaleId
                                LEFT JOIN dbo.Products p ON p.ProductId = i.ProductId
                                WHERE s.Status='Completed' AND s.SaleDate >= @d AND s.SaleDate < @d2 AND (@uid IS NULL OR s.UserId=@uid)",
                Db.P("@d", DateTime.Today), Db.P("@d2", DateTime.Today.AddDays(1)), Uid(userId));
            return v == null ? 0m : Convert.ToDecimal(v);
        }

        /// <summary>Total store credit owed by customers, and how many customers owe money.</summary>
        public static decimal CreditOwed(out int customers)
        {
            var r = Db.Query("SELECT ISNULL(SUM(Balance), 0) AS T, COUNT(*) AS C FROM dbo.Customers WHERE Balance > 0").Rows[0];
            customers = Convert.ToInt32(r["C"]);
            return Convert.ToDecimal(r["T"]);
        }

        public static List<StaffSale> Staff(int days)
        {
            var list = new List<StaffSale>();
            var dt = Db.Query(@"SELECT TOP 5 u.FullName, COUNT(*) AS C, SUM(s.Total) AS T FROM dbo.Sales s JOIN dbo.Users u ON u.UserId = s.UserId
                                WHERE s.Status='Completed' AND s.SaleDate >= @from GROUP BY u.FullName ORDER BY SUM(s.Total) DESC",
                Db.P("@from", DateTime.Today.AddDays(-(days - 1))));
            foreach (DataRow r in dt.Rows)
                list.Add(new StaffSale { Name = Convert.ToString(r["FullName"]), Orders = Convert.ToInt32(r["C"]), Amount = Convert.ToDecimal(r["T"]) });
            return list;
        }

        public static List<RecentSale> Recent(int take, int? userId)
        {
            var list = new List<RecentSale>();
            var dt = Db.Query(@"SELECT TOP (@n) s.InvoiceNo, s.SaleDate, s.Total, s.PaymentMethod, s.Status, u.FullName
                                FROM dbo.Sales s JOIN dbo.Users u ON u.UserId = s.UserId
                                WHERE (@uid IS NULL OR s.UserId=@uid) ORDER BY s.SaleDate DESC, s.SaleId DESC",
                Db.P("@n", take), Uid(userId));
            foreach (DataRow r in dt.Rows)
                list.Add(new RecentSale
                {
                    InvoiceNo = Convert.ToString(r["InvoiceNo"]), When = Convert.ToDateTime(r["SaleDate"]), Total = Convert.ToDecimal(r["Total"]),
                    Method = Convert.ToString(r["PaymentMethod"]), Status = Convert.ToString(r["Status"]), Cashier = Convert.ToString(r["FullName"])
                });
            return list;
        }
    }
}
