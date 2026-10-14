using System;
using System.Collections.Generic;
using System.Data;
using GroceryPOS.Data;
using GroceryPOS.Models;

namespace GroceryPOS.Services
{
    public static class ProductService
    {
        const string ProductSelect =
            @"SELECT p.*, c.Name AS CategoryName, c.Color AS CategoryColor
              FROM dbo.Products p JOIN dbo.Categories c ON c.CategoryId = p.CategoryId ";

        static Product Map(DataRow r)
        {
            return new Product
            {
                ProductId = Convert.ToInt32(r["ProductId"]),
                Barcode = r["Barcode"] == DBNull.Value ? null : Convert.ToString(r["Barcode"]),
                Name = Convert.ToString(r["Name"]),
                CategoryId = Convert.ToInt32(r["CategoryId"]),
                CategoryName = Convert.ToString(r["CategoryName"]),
                CategoryColor = Convert.ToString(r["CategoryColor"]),
                Unit = Convert.ToString(r["Unit"]),
                Price = Convert.ToDecimal(r["Price"]),
                CostPrice = Convert.ToDecimal(r["CostPrice"]),
                Stock = Convert.ToInt32(r["Stock"]),
                MinStock = Convert.ToInt32(r["MinStock"]),
                Icon = r["Icon"] == DBNull.Value ? null : Convert.ToString(r["Icon"]),
                ImagePath = r["ImagePath"] == DBNull.Value ? null : Convert.ToString(r["ImagePath"]),
                IsActive = Convert.ToBoolean(r["IsActive"]),
                IsWeighed = r.Table.Columns.Contains("IsWeighed") && r["IsWeighed"] != DBNull.Value && Convert.ToBoolean(r["IsWeighed"])
            };
        }

        public static List<Product> GetProducts(bool includeInactive = false)
        {
            var list = new List<Product>();
            var dt = Db.Query(ProductSelect + (includeInactive ? "" : "WHERE p.IsActive = 1 ") + "ORDER BY c.SortOrder, p.Name");
            foreach (DataRow r in dt.Rows) list.Add(Map(r));
            return list;
        }

        public static List<Product> GetLowStock()
        {
            var list = new List<Product>();
            var dt = Db.Query(ProductSelect + "WHERE p.IsActive = 1 AND p.Stock <= p.MinStock ORDER BY p.Stock, p.Name");
            foreach (DataRow r in dt.Rows) list.Add(Map(r));
            return list;
        }

        public static int LowStockCount()
        {
            return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Products WHERE IsActive = 1 AND Stock <= MinStock"));
        }

        public static void SaveProduct(Product p)
        {
            var ps = new[]
            {
                Db.P("@b", string.IsNullOrWhiteSpace(p.Barcode) ? null : p.Barcode.Trim()),
                Db.P("@n", p.Name.Trim()), Db.P("@c", p.CategoryId), Db.P("@u", (p.Unit ?? "").Trim()),
                Db.P("@p", p.Price), Db.P("@cost", p.CostPrice), Db.P("@s", p.Stock), Db.P("@m", p.MinStock),
                Db.P("@i", string.IsNullOrWhiteSpace(p.Icon) ? null : p.Icon.Trim()),
                Db.P("@img", string.IsNullOrWhiteSpace(p.ImagePath) ? null : p.ImagePath),
                Db.P("@a", p.IsActive), Db.P("@w", p.IsWeighed), Db.P("@id", p.ProductId)
            };
            if (p.ProductId == 0)
            {
                var id = Db.Scalar(@"INSERT INTO dbo.Products(Barcode,Name,CategoryId,Unit,Price,CostPrice,Stock,MinStock,Icon,ImagePath,IsActive,IsWeighed)
                                     VALUES (@b,@n,@c,@u,@p,@cost,@s,@m,@i,@img,@a,@w); SELECT SCOPE_IDENTITY();", ps);
                p.ProductId = Convert.ToInt32(id);
                if (p.Stock != 0) LogMovement(p.ProductId, p.Stock, "Opening stock");
            }
            else
            {
                int before = Convert.ToInt32(Db.Scalar("SELECT Stock FROM dbo.Products WHERE ProductId=@id", Db.P("@id", p.ProductId)));
                Db.Exec(@"UPDATE dbo.Products SET Barcode=@b, Name=@n, CategoryId=@c, Unit=@u, Price=@p, CostPrice=@cost,
                          Stock=@s, MinStock=@m, Icon=@i, ImagePath=@img, IsActive=@a, IsWeighed=@w WHERE ProductId=@id", ps);
                if (before != p.Stock) LogMovement(p.ProductId, p.Stock - before, "Manual edit");
            }
        }

        /// <summary>Hard-deletes unsold products; products with sales history are archived instead.</summary>
        public static bool DeleteProduct(int productId)
        {
            bool used = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.SaleItems WHERE ProductId=@id", Db.P("@id", productId))) > 0;
            if (used)
            {
                Db.Exec("UPDATE dbo.Products SET IsActive=0 WHERE ProductId=@id", Db.P("@id", productId));
                return false;
            }
            Db.Exec("DELETE FROM dbo.HeldOrderItems WHERE ProductId=@id", Db.P("@id", productId));
            Db.Exec("DELETE FROM dbo.StockMovements WHERE ProductId=@id", Db.P("@id", productId));
            Db.Exec("DELETE FROM dbo.Products WHERE ProductId=@id", Db.P("@id", productId));
            return true;
        }

        public static void AdjustStock(int productId, int delta, string reason)
        {
            Db.Exec("UPDATE dbo.Products SET Stock = Stock + @d WHERE ProductId=@id", Db.P("@d", delta), Db.P("@id", productId));
            LogMovement(productId, delta, reason);
        }

        static void LogMovement(int productId, int delta, string reason)
        {
            Db.Exec("INSERT INTO dbo.StockMovements(ProductId,Delta,Reason,UserId) VALUES (@p,@d,@r,@u)",
                Db.P("@p", productId), Db.P("@d", delta), Db.P("@r", reason),
                Db.P("@u", Session.User == null ? (object)null : Session.User.UserId));
        }

        // ---- categories ----
        public static List<Category> GetCategories()
        {
            var list = new List<Category>();
            var dt = Db.Query(@"SELECT c.*, (SELECT COUNT(*) FROM dbo.Products p WHERE p.CategoryId=c.CategoryId AND p.IsActive=1) AS Cnt
                                FROM dbo.Categories c ORDER BY c.SortOrder, c.Name");
            foreach (DataRow r in dt.Rows)
                list.Add(new Category
                {
                    CategoryId = Convert.ToInt32(r["CategoryId"]), Name = Convert.ToString(r["Name"]),
                    Color = Convert.ToString(r["Color"]), SortOrder = Convert.ToInt32(r["SortOrder"]),
                    ProductCount = Convert.ToInt32(r["Cnt"])
                });
            return list;
        }

        public static void SaveCategory(Category c)
        {
            if (c.CategoryId == 0)
                Db.Exec("INSERT INTO dbo.Categories(Name,Color,SortOrder) VALUES (@n,@c,(SELECT ISNULL(MAX(SortOrder),0)+1 FROM dbo.Categories))",
                    Db.P("@n", c.Name.Trim()), Db.P("@c", c.Color));
            else
                Db.Exec("UPDATE dbo.Categories SET Name=@n, Color=@c WHERE CategoryId=@id",
                    Db.P("@n", c.Name.Trim()), Db.P("@c", c.Color), Db.P("@id", c.CategoryId));
        }

        public static void DeleteCategory(int id)
        {
            Db.Exec("DELETE FROM dbo.Categories WHERE CategoryId=@id", Db.P("@id", id));
        }

        // ---- customers (store credit) ----
        public static List<Customer> GetCustomers(string filter = null)
        {
            var list = new List<Customer>();
            var dt = string.IsNullOrWhiteSpace(filter)
                ? Db.Query("SELECT * FROM dbo.Customers ORDER BY Name")
                : Db.Query("SELECT * FROM dbo.Customers WHERE Name LIKE @f OR Phone LIKE @f ORDER BY Name", Db.P("@f", "%" + filter.Trim() + "%"));
            foreach (DataRow r in dt.Rows)
                list.Add(new Customer
                {
                    CustomerId = Convert.ToInt32(r["CustomerId"]), Name = Convert.ToString(r["Name"]),
                    Phone = r["Phone"] == DBNull.Value ? "" : Convert.ToString(r["Phone"]),
                    Balance = Convert.ToDecimal(r["Balance"])
                });
            return list;
        }

        public static Customer AddCustomer(string name, string phone)
        {
            var id = Convert.ToInt32(Db.Scalar("INSERT INTO dbo.Customers(Name,Phone) VALUES (@n,@p); SELECT SCOPE_IDENTITY();",
                Db.P("@n", name.Trim()), Db.P("@p", string.IsNullOrWhiteSpace(phone) ? null : phone.Trim())));
            return new Customer { CustomerId = id, Name = name.Trim(), Phone = phone == null ? "" : phone.Trim() };
        }
    }
}
