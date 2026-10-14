#if DEBUG
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GroceryPOS.Data;
using GroceryPOS.Models;
using GroceryPOS.Services;
using GroceryPOS.UI.Dialogs;

namespace GroceryPOS.UI
{
    /// <summary>Debug-only: renders every screen/dialog to PNG against a throw-away database for visual review.</summary>
    static class SnapShots
    {
        static void Snap(Control c, string dir, string name)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(150);
            Application.DoEvents();
            using (var bmp = new Bitmap(c.Width, c.Height))
            {
                c.DrawToBitmap(bmp, new Rectangle(Point.Empty, c.Size));
                bmp.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
            }
        }


        static Panels.DashboardPanel FindDash(Control c)
        {
            foreach (Control k in c.Controls)
            {
                var d = k as Panels.DashboardPanel;
                if (d != null) return d;
                d = FindDash(k);
                if (d != null) return d;
            }
            return null;
        }

        static void SelfTest(string dir, List<UserAccount> users, List<Product> products)
        {
            var log = new List<string>();
            Action<string, bool> check = (name, ok) => log.Add((ok ? "PASS  " : "FAIL  ") + name);

            var admin = users.First(u => u.IsAdmin);
            decimal salesBefore = SaleService.GetZReport(DateTime.Today, null, "all").NetSales;
            int txBefore = SaleService.GetZReport(DateTime.Today, null, "all").Transactions;

            // 1. cash sale: stock drops, totals correct
            var milk = ProductService.GetProducts().First(p => p.Name == "Fresh Milk");
            var cart = new Cart();
            cart.Add(milk, 3);
            cart.DiscountIsPercent = true; cart.DiscountValue = 10;
            decimal expectTotal = Math.Round(milk.Price * 3 * 0.9m, 2);
            check("cart total with 10% discount = " + expectTotal, cart.Total == expectTotal);
            var sale = SaleService.Checkout(cart, new PaymentInfo { Method = "Cash", AmountGiven = 2000, Change = 2000 - cart.Total }, admin.UserId);
            var milkAfter = ProductService.GetProducts().First(p => p.Name == "Fresh Milk");
            check("stock decremented by 3 (" + milk.Stock + " -> " + milkAfter.Stock + ")", milkAfter.Stock == milk.Stock - 3);
            check("invoice number issued: " + sale.InvoiceNo, sale.InvoiceNo.StartsWith("INV-"));
            check("sale persisted with 1 line, discount stored", sale.Items.Count == 1 && sale.DiscountAmount == Math.Round(milk.Price * 3 * 0.1m, 2));

            // 2. z-report reflects it
            var z = SaleService.GetZReport(DateTime.Today, null, "all");
            check("z-report net sales rose by sale total", z.NetSales == salesBefore + sale.Total);
            check("z-report transaction count +1", z.Transactions == txBefore + 1);
            check("z-report cash+card+credit == net", z.Cash + z.Card + z.Credit == z.NetSales);

            // 3. void restores stock and drops it from the report
            SaleService.VoidSale(sale.SaleId, admin.UserId);
            check("void restores stock", ProductService.GetProducts().First(p => p.Name == "Fresh Milk").Stock == milk.Stock);
            var z2 = SaleService.GetZReport(DateTime.Today, null, "all");
            check("void removes sale from net sales", z2.NetSales == salesBefore && z2.VoidedCount == 1);
            bool doubleVoid = false;
            try { SaleService.VoidSale(sale.SaleId, admin.UserId); } catch (InvalidOperationException) { doubleVoid = true; }
            check("double void is rejected", doubleVoid);

            // 4. credit sale raises customer balance, void lowers it
            var cust = ProductService.GetCustomers().First();
            var c2 = new Cart(); c2.Add(milk, 2);
            var cs = SaleService.Checkout(c2, new PaymentInfo { Method = "Credit", CustomerId = cust.CustomerId }, admin.UserId);
            check("credit sale adds to balance", ProductService.GetCustomers().First(c => c.CustomerId == cust.CustomerId).Balance == cust.Balance + cs.Total);
            SaleService.VoidSale(cs.SaleId, admin.UserId);
            check("void credit sale reverses balance", ProductService.GetCustomers().First(c => c.CustomerId == cust.CustomerId).Balance == cust.Balance);

            // 5. stock conflict is rejected atomically
            var bad = new Cart();
            bad.Add(milk, 1);
            bad.Lines[0].Quantity = 99999;           // simulate stale cart
            int before = ProductService.GetProducts().First(p => p.Name == "Fresh Milk").Stock;
            bool rejected = false;
            try { SaleService.Checkout(bad, new PaymentInfo { Method = "Card", AmountGiven = bad.Total }, admin.UserId); } catch (StockException) { rejected = true; }
            check("oversell rejected with StockException", rejected);
            check("rejected sale leaves stock untouched", ProductService.GetProducts().First(p => p.Name == "Fresh Milk").Stock == before);

            // 6. cart rules
            var butter = ProductService.GetProducts().First(p => p.Name == "Butter");
            check("out-of-stock item cannot be added", new Cart().Add(butter) == AddResult.OutOfStock);
            var cc = new Cart(); var eggs = ProductService.GetProducts().First(p => p.Name == "Farm Eggs");
            check("cannot exceed available stock", cc.Add(eggs, eggs.Stock) == AddResult.Added && cc.Add(eggs, 1) == AddResult.LimitReached);

            // 7. low stock + login
            check("low-stock list includes Butter (out) and Eggs", ProductService.GetLowStock().Any(p => p.Name == "Butter") && ProductService.GetLowStock().Any(p => p.Name == "Farm Eggs"));
            check("PIN 1234 logs the manager in", AuthService.Login(admin.UserId, "1234") != null);
            check("wrong PIN is rejected", AuthService.Login(admin.UserId, "9999") == null);

            // 8. held orders
            check("held order stored", SaleService.GetHeldOrders().Count == 1 && SaleService.LoadHeldLines(SaleService.GetHeldOrders()[0]).Lines.Count == 2);

            // 9. receipt text
            var lines = ReceiptPrinter.BuildSale(SaleService.GetSale(cs.SaleId));
            check("receipt lines all <= 42 cols", lines.All(l => l.Length <= ReceiptPrinter.Cols));

            // 10. items sold by weight
            var chicken = ProductService.GetProducts().First(p => p.Name == "Chicken (fresh)");
            var wc = new Cart(); wc.Add(chicken, 350);
            check("350 g of chicken @ 1450/kg = 507.50 (counts as 1 item)", wc.Total == 507.50m && wc.ItemCount == 1);
            var ws = SaleService.Checkout(wc, new PaymentInfo { Method = "Cash", AmountGiven = 600, Change = 600 - wc.Total }, admin.UserId);
            check("weighed stock decrements in grams", ProductService.GetProducts().First(p => p.Name == "Chicken (fresh)").Stock == chicken.Stock - 350);
            check("weighed line stored as grams + flag", ws.Items[0].IsWeighed && ws.Items[0].Quantity == 350 && ws.Items[0].LineTotal == 507.50m && ws.ItemCount == 1);
            check("receipt prints 350 g", ReceiptPrinter.BuildSale(ws).Any(l => l.Contains("350 g")));
            SaleService.VoidSale(ws.SaleId, admin.UserId);
            check("void restores grams", ProductService.GetProducts().First(p => p.Name == "Chicken (fresh)").Stock == chicken.Stock);
            var w2 = new Cart();
            check("cannot weigh more than stock", w2.Add(chicken, chicken.Stock) == AddResult.Added && w2.Add(chicken, 1) == AddResult.LimitReached);

            File.WriteAllLines(Path.Combine(dir, "selftest.txt"), log);
        }
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            if (Environment.GetEnvironmentVariable("SNAP_SCALE") == "1") Theme.Scale = 1f;
            Db.SetDatabase("GroceryPOSDB_snap");
            try
            {
                Db.DropDatabase();
                Db.Initialize();
                AppSettings.Load();
                var users = AuthService.GetUsers(true);
                Session.User = users.First(u => u.IsAdmin);

                // sample sales
                var products = ProductService.GetProducts();
                var rnd = new Random(7);
                Sale last = null;
                for (int i = 0; i < 16; i++)
                {
                    var cart = new Cart();
                    for (int k = 0; k < rnd.Next(1, 5); k++) cart.Add(products[rnd.Next(products.Count)], rnd.Next(1, 3));
                    if (cart.Lines.Count == 0) continue;
                    var method = i % 5 == 0 ? "Card" : "Cash";
                    var pay = new PaymentInfo { Method = method, AmountGiven = Math.Ceiling(cart.Total / 100) * 100, Change = Math.Ceiling(cart.Total / 100) * 100 - cart.Total };
                    if (method == "Card") { pay.AmountGiven = cart.Total; pay.Change = 0; }
                    last = SaleService.Checkout(cart, pay, users[i % users.Count].UserId);
                }
                Db.Exec("UPDATE dbo.Sales SET SaleDate = DATEADD(DAY, -(SaleId % 7), SaleDate) WHERE SaleId % 7 IN (1,2,3,4,5,6)");
                Db.Exec("UPDATE dbo.Sales SET SaleDate = DATEADD(HOUR, -(SaleId % 9), SaleDate) WHERE CAST(SaleDate AS DATE) = CAST(GETDATE() AS DATE)");
                var held = new Cart();
                held.Add(products[0], 2); held.Add(products[8], 1);
                SaleService.HoldOrder(Session.User.UserId, "Order 14:05", held);

                // ---- functional self-test (written next to the screenshots)
                SelfTest(dir, users, products);
                // ---- login
                using (var login = new LoginForm(false))
                {
                    login.Show();
                    Snap(login, dir, "01-login");
                    login.Close();
                }

                // ---- main screens
                using (var main = new MainForm())
                {
                    main.StartPosition = FormStartPosition.Manual;
                    main.WindowState = FormWindowState.Normal;
                    var sz = (Environment.GetEnvironmentVariable("SNAP_SIZE") ?? "1366x768").Split((char)120); main.Bounds = new Rectangle(0, 0, int.Parse(sz[0]), int.Parse(sz[1]));
                    main.Show();
                    main.Bounds = new Rectangle(0, 0, int.Parse(sz[0]), int.Parse(sz[1]));
                    Application.DoEvents();
                    main.Navigate(1);
                    var pos = (Panels.PosPanel)typeof(MainForm).GetMethod("get_Pos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(main, null);
                    Snap(main, dir, "02-pos-empty");
                    pos.DebugAdd("Coca-Cola", 2); pos.DebugAdd("White Bread", 1); pos.DebugAdd("Red Apples", 3); pos.DebugAdd("Fresh Milk", 2);
                    pos.DebugAdd("Bananas", 1); pos.DebugAdd("Chocolate Bar", 4);
                    Snap(main, dir, "03-pos-cart");
                    main.Navigate(0); Snap(main, dir, "00-dashboard"); var dash = FindDash(main); if (dash != null) { dash.AutoScrollPosition = new Point(0, 520); Snap(main, dir, "00a-dashboard-mid"); dash.AutoScrollPosition = new Point(0, 1100); Snap(main, dir, "00c-dashboard-low"); dash.AutoScrollPosition = new Point(0, 3000); Snap(main, dir, "00b-dashboard-bottom"); dash.AutoScrollPosition = Point.Empty; } main.Navigate(2); Snap(main, dir, "04-inventory");
                    main.Navigate(3); Snap(main, dir, "05-lowstock");
                    main.Navigate(4); Snap(main, dir, "06-reports");
                    main.Navigate(5); Snap(main, dir, "07-settings");
                    main.Close();
                }

                // ---- dialogs
                var cats = ProductService.GetCategories();
                var dlgs = new List<KeyValuePair<string, DialogBase>>
                {
                    new KeyValuePair<string, DialogBase>("10-payment", new PaymentDialog(1864.50m)),
                    new KeyValuePair<string, DialogBase>("11-discount", new DiscountDialog(1864.50m, true, 10)),
                    new KeyValuePair<string, DialogBase>("12-number", new NumberDialog("Coca-Cola", "Quantity", 3, false, 48, "Available in stock: 48")),
                    new KeyValuePair<string, DialogBase>("13-product", new ProductDialog(products[0], cats)),
                    new KeyValuePair<string, DialogBase>("14-receipt", new ReceiptDialog(last, true)),
                    new KeyValuePair<string, DialogBase>("15-complete", new SaleCompleteDialog(last, "Receipt sent to printer")),
                    new KeyValuePair<string, DialogBase>("16-held", new HeldOrdersDialog()),
                    new KeyValuePair<string, DialogBase>("17-customers", new CustomerPickerDialog()),
                    new KeyValuePair<string, DialogBase>("18-user", new UserDialog(null)),
                    new KeyValuePair<string, DialogBase>("19-category", new CategoryDialog(null)),
                    new KeyValuePair<string, DialogBase>("21-weight", new WeightDialog("Chicken (fresh)", 1450m, 18000, 350)),
                    new KeyValuePair<string, DialogBase>("20-confirm", new MessageDialog("Clear this order?", "All 7 items will be removed from the cart.", "Clear order", "Cancel", MessageKind.Danger))
                };
                foreach (var kv in dlgs)
                {
                    kv.Value.StartPosition = FormStartPosition.Manual;
                    kv.Value.Location = new Point(10, 10);
                    kv.Value.Show();
                    Snap(kv.Value, dir, kv.Key);
                    kv.Value.Close();
                    kv.Value.Dispose();
                }
            }
            finally
            {
                try { Db.DropDatabase(); } catch { }
            }
        }
    }
}
#endif
