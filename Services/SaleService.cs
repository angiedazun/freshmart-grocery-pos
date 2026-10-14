using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using GroceryPOS.Data;
using GroceryPOS.Models;

namespace GroceryPOS.Services
{
    public class StockException : Exception
    {
        public StockException(string message) : base(message) { }
    }

    public static class SaleService
    {
        static SqlCommand Cmd(SqlConnection c, SqlTransaction tx, string sql, params SqlParameter[] ps)
        {
            var cmd = new SqlCommand(sql, c, tx);
            if (ps != null) cmd.Parameters.AddRange(ps);
            return cmd;
        }

        /// <summary>Atomically records the sale, decrements stock and (for credit) updates the customer balance.</summary>
        public static Sale Checkout(Cart cart, PaymentInfo pay, int userId)
        {
            int saleId;
            using (var c = Db.Open())
            using (var tx = c.BeginTransaction())
            {
                try
                {
                    foreach (var l in cart.Lines)
                    {
                        int rows = Cmd(c, tx, "UPDATE dbo.Products SET Stock = Stock - @q WHERE ProductId=@id AND IsActive=1 AND Stock >= @q",
                            Db.P("@q", l.Quantity), Db.P("@id", l.ProductId)).ExecuteNonQuery();
                        if (rows == 0)
                            throw new StockException("Not enough stock for \"" + l.Name + "\". Please refresh the cart.");
                        Cmd(c, tx, "INSERT INTO dbo.StockMovements(ProductId,Delta,Reason,UserId) VALUES (@p,@d,'Sale',@u)",
                            Db.P("@p", l.ProductId), Db.P("@d", -l.Quantity), Db.P("@u", userId)).ExecuteNonQuery();
                    }

                    saleId = Convert.ToInt32(Cmd(c, tx,
                        @"INSERT INTO dbo.Sales(UserId,CustomerId,Subtotal,DiscountAmount,TaxRate,TaxAmount,Total,PaymentMethod,AmountGiven,ChangeAmount,Reference)
                          VALUES (@u,@cu,@sub,@disc,@tr,@tax,@tot,@pm,@given,@chg,@ref); SELECT SCOPE_IDENTITY();",
                        Db.P("@u", userId), Db.P("@cu", pay.CustomerId), Db.P("@sub", cart.Subtotal), Db.P("@disc", cart.DiscountAmount),
                        Db.P("@tr", cart.TaxRate), Db.P("@tax", cart.TaxAmount), Db.P("@tot", cart.Total), Db.P("@pm", pay.Method),
                        Db.P("@given", pay.AmountGiven), Db.P("@chg", pay.Change),
                        Db.P("@ref", string.IsNullOrWhiteSpace(pay.Reference) ? null : pay.Reference.Trim())).ExecuteScalar());

                    Cmd(c, tx, "UPDATE dbo.Sales SET InvoiceNo = 'INV-' + RIGHT('000000' + CAST(SaleId AS NVARCHAR(10)), 6) WHERE SaleId=@id",
                        Db.P("@id", saleId)).ExecuteNonQuery();

                    foreach (var l in cart.Lines)
                        Cmd(c, tx, @"INSERT INTO dbo.SaleItems(SaleId,ProductId,ProductName,Unit,UnitPrice,Quantity,LineTotal,IsWeighed)
                                     VALUES (@s,@p,@n,@u,@up,@q,@lt,@w)",
                            Db.P("@s", saleId), Db.P("@p", l.ProductId), Db.P("@n", l.Name), Db.P("@u", l.Unit ?? ""),
                            Db.P("@up", l.UnitPrice), Db.P("@q", l.Quantity), Db.P("@lt", l.LineTotal), Db.P("@w", l.IsWeighed)).ExecuteNonQuery();

                    if (pay.Method == "Credit" && pay.CustomerId.HasValue)
                        Cmd(c, tx, "UPDATE dbo.Customers SET Balance = Balance + @t WHERE CustomerId=@id",
                            Db.P("@t", cart.Total), Db.P("@id", pay.CustomerId.Value)).ExecuteNonQuery();

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            return GetSale(saleId);
        }

        public static Sale GetSale(int saleId)
        {
            var dt = Db.Query(@"SELECT s.*, u.FullName AS CashierName, cu.Name AS CustomerName
                                FROM dbo.Sales s JOIN dbo.Users u ON u.UserId = s.UserId
                                LEFT JOIN dbo.Customers cu ON cu.CustomerId = s.CustomerId WHERE s.SaleId=@id", Db.P("@id", saleId));
            if (dt.Rows.Count == 0) return null;
            var r = dt.Rows[0];
            var sale = new Sale
            {
                SaleId = saleId,
                InvoiceNo = Convert.ToString(r["InvoiceNo"]),
                SaleDate = Convert.ToDateTime(r["SaleDate"]),
                UserId = Convert.ToInt32(r["UserId"]),
                CashierName = Convert.ToString(r["CashierName"]),
                CustomerId = r["CustomerId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["CustomerId"]),
                CustomerName = r["CustomerName"] == DBNull.Value ? null : Convert.ToString(r["CustomerName"]),
                Subtotal = Convert.ToDecimal(r["Subtotal"]),
                DiscountAmount = Convert.ToDecimal(r["DiscountAmount"]),
                TaxRate = Convert.ToDecimal(r["TaxRate"]),
                TaxAmount = Convert.ToDecimal(r["TaxAmount"]),
                Total = Convert.ToDecimal(r["Total"]),
                PaymentMethod = Convert.ToString(r["PaymentMethod"]),
                AmountGiven = Convert.ToDecimal(r["AmountGiven"]),
                ChangeAmount = Convert.ToDecimal(r["ChangeAmount"]),
                Reference = r["Reference"] == DBNull.Value ? null : Convert.ToString(r["Reference"]),
                Status = Convert.ToString(r["Status"])
            };
            foreach (DataRow i in Db.Query("SELECT * FROM dbo.SaleItems WHERE SaleId=@id ORDER BY SaleItemId", Db.P("@id", saleId)).Rows)
                sale.Items.Add(new SaleItem
                {
                    ProductId = Convert.ToInt32(i["ProductId"]), ProductName = Convert.ToString(i["ProductName"]),
                    Unit = Convert.ToString(i["Unit"]), UnitPrice = Convert.ToDecimal(i["UnitPrice"]),
                    Quantity = Convert.ToInt32(i["Quantity"]), LineTotal = Convert.ToDecimal(i["LineTotal"]), IsWeighed = Convert.ToBoolean(i["IsWeighed"])
                });
            return sale;
        }

        public static DataTable SearchSales(DateTime from, DateTime to, string invoice, int? userId)
        {
            return Db.Query(@"SELECT s.SaleId, s.InvoiceNo, s.SaleDate, u.FullName AS Cashier,
                                     ISNULL((SELECT SUM(CASE WHEN i.IsWeighed = 1 THEN 1 ELSE i.Quantity END) FROM dbo.SaleItems i WHERE i.SaleId = s.SaleId), 0) AS Items,
                                     s.Total, s.PaymentMethod, s.Status
                              FROM dbo.Sales s JOIN dbo.Users u ON u.UserId = s.UserId
                              WHERE s.SaleDate >= @from AND s.SaleDate < @to
                                AND (@inv = '' OR s.InvoiceNo LIKE @invLike)
                                AND (@uid IS NULL OR s.UserId = @uid)
                              ORDER BY s.SaleDate DESC",
                Db.P("@from", from.Date), Db.P("@to", to.Date.AddDays(1)),
                Db.P("@inv", (invoice ?? "").Trim()), Db.P("@invLike", "%" + (invoice ?? "").Trim() + "%"),
                new SqlParameter("@uid", SqlDbType.Int) { Value = userId.HasValue ? (object)userId.Value : DBNull.Value });
        }

        public static void VoidSale(int saleId, int userId)
        {
            using (var c = Db.Open())
            using (var tx = c.BeginTransaction())
            {
                try
                {
                    var status = Convert.ToString(Cmd(c, tx, "SELECT Status FROM dbo.Sales WITH (UPDLOCK) WHERE SaleId=@id", Db.P("@id", saleId)).ExecuteScalar());
                    if (status != "Completed") throw new InvalidOperationException("This sale is already voided.");

                    var items = new List<KeyValuePair<int, int>>();
                    using (var rd = Cmd(c, tx, "SELECT ProductId, Quantity FROM dbo.SaleItems WHERE SaleId=@id", Db.P("@id", saleId)).ExecuteReader())
                        while (rd.Read()) items.Add(new KeyValuePair<int, int>(rd.GetInt32(0), rd.GetInt32(1)));

                    foreach (var it in items)
                    {
                        Cmd(c, tx, "UPDATE dbo.Products SET Stock = Stock + @q WHERE ProductId=@id", Db.P("@q", it.Value), Db.P("@id", it.Key)).ExecuteNonQuery();
                        Cmd(c, tx, "INSERT INTO dbo.StockMovements(ProductId,Delta,Reason,UserId) VALUES (@p,@d,'Void',@u)",
                            Db.P("@p", it.Key), Db.P("@d", it.Value), Db.P("@u", userId)).ExecuteNonQuery();
                    }

                    Cmd(c, tx, @"UPDATE dbo.Customers SET Balance = Balance - s.Total FROM dbo.Customers cu
                                 JOIN dbo.Sales s ON s.CustomerId = cu.CustomerId WHERE s.SaleId=@id AND s.PaymentMethod='Credit'",
                        Db.P("@id", saleId)).ExecuteNonQuery();
                    Cmd(c, tx, "UPDATE dbo.Sales SET Status='Voided', VoidedAt=GETDATE(), VoidedBy=@u WHERE SaleId=@id",
                        Db.P("@u", userId), Db.P("@id", saleId)).ExecuteNonQuery();
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public static ZReport GetZReport(DateTime date, int? userId, string cashierName)
        {
            var uid = new SqlParameter("@uid", SqlDbType.Int) { Value = userId.HasValue ? (object)userId.Value : DBNull.Value };
            var uid2 = new SqlParameter("@uid", SqlDbType.Int) { Value = userId.HasValue ? (object)userId.Value : DBNull.Value };
            var uid3 = new SqlParameter("@uid", SqlDbType.Int) { Value = userId.HasValue ? (object)userId.Value : DBNull.Value };

            var r = Db.Query(@"SELECT COUNT(*) AS Cnt, ISNULL(SUM(Subtotal),0) AS Gross, ISNULL(SUM(DiscountAmount),0) AS Disc,
                                      ISNULL(SUM(TaxAmount),0) AS Tax, ISNULL(SUM(Total),0) AS Net,
                                      ISNULL(SUM(CASE WHEN PaymentMethod='Cash' THEN Total END),0) AS Cash,
                                      ISNULL(SUM(CASE WHEN PaymentMethod='Card' THEN Total END),0) AS Card,
                                      ISNULL(SUM(CASE WHEN PaymentMethod='Credit' THEN Total END),0) AS Credit
                               FROM dbo.Sales WHERE Status='Completed' AND SaleDate >= @d AND SaleDate < @d2 AND (@uid IS NULL OR UserId=@uid)",
                Db.P("@d", date.Date), Db.P("@d2", date.Date.AddDays(1)), uid).Rows[0];

            var items = Db.Scalar(@"SELECT SUM(CASE WHEN i.IsWeighed = 1 THEN 1 ELSE i.Quantity END) FROM dbo.SaleItems i JOIN dbo.Sales s ON s.SaleId = i.SaleId
                                    WHERE s.Status='Completed' AND s.SaleDate >= @d AND s.SaleDate < @d2 AND (@uid IS NULL OR s.UserId=@uid)",
                Db.P("@d", date.Date), Db.P("@d2", date.Date.AddDays(1)), uid2);

            var v = Db.Query(@"SELECT COUNT(*) AS Cnt, ISNULL(SUM(Total),0) AS Amt FROM dbo.Sales
                               WHERE Status='Voided' AND SaleDate >= @d AND SaleDate < @d2 AND (@uid IS NULL OR UserId=@uid)",
                Db.P("@d", date.Date), Db.P("@d2", date.Date.AddDays(1)), uid3).Rows[0];

            return new ZReport
            {
                Date = date.Date,
                CashierName = cashierName,
                Transactions = Convert.ToInt32(r["Cnt"]),
                ItemsSold = items == null ? 0 : Convert.ToInt32(items),
                GrossSales = Convert.ToDecimal(r["Gross"]),
                Discounts = Convert.ToDecimal(r["Disc"]),
                Tax = Convert.ToDecimal(r["Tax"]),
                NetSales = Convert.ToDecimal(r["Net"]),
                Cash = Convert.ToDecimal(r["Cash"]),
                Card = Convert.ToDecimal(r["Card"]),
                Credit = Convert.ToDecimal(r["Credit"]),
                VoidedCount = Convert.ToInt32(v["Cnt"]),
                VoidedAmount = Convert.ToDecimal(v["Amt"])
            };
        }

        // ---------------- held orders ----------------
        public static void HoldOrder(int userId, string label, Cart cart)
        {
            using (var c = Db.Open())
            using (var tx = c.BeginTransaction())
            {
                try
                {
                    int id = Convert.ToInt32(Cmd(c, tx,
                        @"INSERT INTO dbo.HeldOrders(UserId,Label,DiscountIsPercent,DiscountValue) VALUES (@u,@l,@dp,@dv); SELECT SCOPE_IDENTITY();",
                        Db.P("@u", userId), Db.P("@l", label), Db.P("@dp", cart.DiscountIsPercent), Db.P("@dv", cart.DiscountValue)).ExecuteScalar());
                    foreach (var l in cart.Lines)
                        Cmd(c, tx, "INSERT INTO dbo.HeldOrderItems(HeldId,ProductId,Quantity) VALUES (@h,@p,@q)",
                            Db.P("@h", id), Db.P("@p", l.ProductId), Db.P("@q", l.Quantity)).ExecuteNonQuery();
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public static List<HeldOrder> GetHeldOrders()
        {
            var list = new List<HeldOrder>();
            var dt = Db.Query(@"SELECT h.HeldId, h.Label, h.HeldAt, h.DiscountIsPercent, h.DiscountValue,
                                       ISNULL(SUM(CASE WHEN p.IsWeighed = 1 THEN 1 ELSE i.Quantity END),0) AS Cnt, ISNULL(SUM(CASE WHEN p.IsWeighed = 1 THEN ROUND(i.Quantity * p.Price / 1000.0, 2) ELSE i.Quantity * p.Price END),0) AS Total
                                FROM dbo.HeldOrders h
                                LEFT JOIN dbo.HeldOrderItems i ON i.HeldId = h.HeldId
                                LEFT JOIN dbo.Products p ON p.ProductId = i.ProductId
                                GROUP BY h.HeldId, h.Label, h.HeldAt, h.DiscountIsPercent, h.DiscountValue
                                ORDER BY h.HeldAt DESC");
            foreach (DataRow r in dt.Rows)
                list.Add(new HeldOrder
                {
                    HeldId = Convert.ToInt32(r["HeldId"]), Label = Convert.ToString(r["Label"]),
                    HeldAt = Convert.ToDateTime(r["HeldAt"]), ItemCount = Convert.ToInt32(r["Cnt"]),
                    Total = Convert.ToDecimal(r["Total"]),
                    DiscountIsPercent = Convert.ToBoolean(r["DiscountIsPercent"]),
                    DiscountValue = Convert.ToDecimal(r["DiscountValue"])
                });
            return list;
        }

        public static HeldOrder LoadHeldLines(HeldOrder h)
        {
            h.Lines.Clear();
            foreach (DataRow r in Db.Query("SELECT ProductId, Quantity FROM dbo.HeldOrderItems WHERE HeldId=@id", Db.P("@id", h.HeldId)).Rows)
                h.Lines.Add(new KeyValuePair<int, int>(Convert.ToInt32(r["ProductId"]), Convert.ToInt32(r["Quantity"])));
            return h;
        }

        public static void DeleteHeld(int heldId)
        {
            Db.Exec("DELETE FROM dbo.HeldOrders WHERE HeldId=@id", Db.P("@id", heldId));
        }

        public static int HeldCount()
        {
            return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.HeldOrders"));
        }
    }
}
