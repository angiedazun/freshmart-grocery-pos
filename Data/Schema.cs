using System;
using System.Collections.Generic;
using GroceryPOS.Services;

namespace GroceryPOS.Data
{
    public static class Schema
    {
        public static readonly string[] Statements =
        {
            @"IF OBJECT_ID('dbo.Users','U') IS NULL
              CREATE TABLE dbo.Users(
                UserId INT IDENTITY(1,1) PRIMARY KEY,
                FullName NVARCHAR(100) NOT NULL,
                Username NVARCHAR(50) NOT NULL UNIQUE,
                PinHash NVARCHAR(200) NOT NULL,
                Role NVARCHAR(20) NOT NULL DEFAULT 'Cashier',
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE())",

            @"IF OBJECT_ID('dbo.Categories','U') IS NULL
              CREATE TABLE dbo.Categories(
                CategoryId INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(60) NOT NULL UNIQUE,
                Color NVARCHAR(9) NOT NULL DEFAULT '#16A34A',
                SortOrder INT NOT NULL DEFAULT 0)",

            @"IF OBJECT_ID('dbo.Products','U') IS NULL
              CREATE TABLE dbo.Products(
                ProductId INT IDENTITY(1,1) PRIMARY KEY,
                Barcode NVARCHAR(50) NULL,
                Name NVARCHAR(150) NOT NULL,
                CategoryId INT NOT NULL REFERENCES dbo.Categories(CategoryId),
                Unit NVARCHAR(30) NOT NULL DEFAULT '',
                Price DECIMAL(18,2) NOT NULL,
                CostPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                Stock INT NOT NULL DEFAULT 0,
                MinStock INT NOT NULL DEFAULT 5,
                Icon NVARCHAR(16) NULL,
                ImagePath NVARCHAR(260) NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE())",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Products_Barcode')
              CREATE UNIQUE INDEX UX_Products_Barcode ON dbo.Products(Barcode) WHERE Barcode IS NOT NULL",

            @"IF OBJECT_ID('dbo.Customers','U') IS NULL
              CREATE TABLE dbo.Customers(
                CustomerId INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                Phone NVARCHAR(30) NULL,
                Balance DECIMAL(18,2) NOT NULL DEFAULT 0,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE())",

            @"IF OBJECT_ID('dbo.Sales','U') IS NULL
              CREATE TABLE dbo.Sales(
                SaleId INT IDENTITY(1,1) PRIMARY KEY,
                InvoiceNo NVARCHAR(30) NOT NULL DEFAULT '',
                SaleDate DATETIME NOT NULL DEFAULT GETDATE(),
                UserId INT NOT NULL REFERENCES dbo.Users(UserId),
                CustomerId INT NULL REFERENCES dbo.Customers(CustomerId),
                Subtotal DECIMAL(18,2) NOT NULL,
                DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                TaxRate DECIMAL(6,2) NOT NULL DEFAULT 0,
                TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                Total DECIMAL(18,2) NOT NULL,
                PaymentMethod NVARCHAR(20) NOT NULL,
                AmountGiven DECIMAL(18,2) NOT NULL DEFAULT 0,
                ChangeAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                Reference NVARCHAR(60) NULL,
                Status NVARCHAR(15) NOT NULL DEFAULT 'Completed',
                VoidedAt DATETIME NULL,
                VoidedBy INT NULL)",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sales_SaleDate')
              CREATE INDEX IX_Sales_SaleDate ON dbo.Sales(SaleDate)",

            @"IF OBJECT_ID('dbo.SaleItems','U') IS NULL
              CREATE TABLE dbo.SaleItems(
                SaleItemId INT IDENTITY(1,1) PRIMARY KEY,
                SaleId INT NOT NULL REFERENCES dbo.Sales(SaleId) ON DELETE CASCADE,
                ProductId INT NOT NULL,
                ProductName NVARCHAR(150) NOT NULL,
                Unit NVARCHAR(30) NOT NULL DEFAULT '',
                UnitPrice DECIMAL(18,2) NOT NULL,
                Quantity INT NOT NULL,
                LineTotal DECIMAL(18,2) NOT NULL)",

            @"IF OBJECT_ID('dbo.StockMovements','U') IS NULL
              CREATE TABLE dbo.StockMovements(
                MovementId INT IDENTITY(1,1) PRIMARY KEY,
                ProductId INT NOT NULL,
                Delta INT NOT NULL,
                Reason NVARCHAR(60) NOT NULL,
                UserId INT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE())",

            @"IF OBJECT_ID('dbo.HeldOrders','U') IS NULL
              CREATE TABLE dbo.HeldOrders(
                HeldId INT IDENTITY(1,1) PRIMARY KEY,
                UserId INT NOT NULL,
                Label NVARCHAR(60) NOT NULL,
                HeldAt DATETIME NOT NULL DEFAULT GETDATE(),
                DiscountIsPercent BIT NOT NULL DEFAULT 1,
                DiscountValue DECIMAL(18,2) NOT NULL DEFAULT 0)",

            @"IF OBJECT_ID('dbo.HeldOrderItems','U') IS NULL
              CREATE TABLE dbo.HeldOrderItems(
                HeldItemId INT IDENTITY(1,1) PRIMARY KEY,
                HeldId INT NOT NULL REFERENCES dbo.HeldOrders(HeldId) ON DELETE CASCADE,
                ProductId INT NOT NULL,
                Quantity INT NOT NULL)",

            @"IF COL_LENGTH('dbo.Products','IsWeighed') IS NULL
              ALTER TABLE dbo.Products ADD IsWeighed BIT NOT NULL CONSTRAINT DF_Products_IsWeighed DEFAULT 0",

            @"IF COL_LENGTH('dbo.SaleItems','IsWeighed') IS NULL
              ALTER TABLE dbo.SaleItems ADD IsWeighed BIT NOT NULL CONSTRAINT DF_SaleItems_IsWeighed DEFAULT 0",

            @"IF OBJECT_ID('dbo.Settings','U') IS NULL
              CREATE TABLE dbo.Settings(K NVARCHAR(50) NOT NULL PRIMARY KEY, V NVARCHAR(MAX) NULL)"
        };

        // name, category, unit, price, cost, stock, min, icon (unicode escape), barcode
        class Seed
        {
            public string Name, Category, Unit, Icon, Barcode;
            public decimal Price, Cost;
            public int Stock, Min;
            public Seed(string n, string c, string u, decimal p, decimal cost, int s, int m, string icon, string bc)
            { Name = n; Category = c; Unit = u; Price = p; Cost = cost; Stock = s; Min = m; Icon = icon; Barcode = bc; }
        }

        /// <summary>One-time: adds a "Meat & Fish" category with sample items sold by weight.</summary>
        public static void SeedWeighedOnce()
        {
            if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Settings WHERE K='SeededWeighed'")) > 0) return;
            if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Categories WHERE Name='Meat & Fish'")) == 0)
                Db.Exec("INSERT INTO dbo.Categories(Name,Color,SortOrder) VALUES ('Meat & Fish','#DC2626',(SELECT ISNULL(MAX(SortOrder),0)+1 FROM dbo.Categories))");

            // price / cost are per kg; stock and alert level are in grams
            var items = new[]
            {
                new object[] { "Chicken (fresh)", 1450m, 1150m, 18000, 5000, "\U0001F357" },
                new object[] { "Beef", 2200m, 1800m, 12000, 4000, "\U0001F969" },
                new object[] { "Tuna", 1300m, 1000m, 9000, 3000, "\U0001F41F" },
                new object[] { "Prawns", 2400m, 1950m, 6000, 2000, "\U0001F990" }
            };
            foreach (var it in items)
            {
                if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Products WHERE Name=@n", Db.P("@n", it[0]))) > 0) continue;
                Db.Exec(@"INSERT INTO dbo.Products(Name,CategoryId,Unit,Price,CostPrice,Stock,MinStock,Icon,IsWeighed)
                          VALUES (@n,(SELECT CategoryId FROM dbo.Categories WHERE Name='Meat & Fish'),'per kg',@p,@c,@s,@m,@i,1)",
                    Db.P("@n", it[0]), Db.P("@p", it[1]), Db.P("@c", it[2]), Db.P("@s", it[3]), Db.P("@m", it[4]), Db.P("@i", it[5]));
            }
            Db.Exec("INSERT INTO dbo.Settings(K,V) VALUES ('SeededWeighed','1')");
        }

        public static void SeedIfEmpty()
        {
            if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Users")) == 0)
            {
                Db.Exec("INSERT INTO dbo.Users(FullName,Username,PinHash,Role) VALUES (@n,@u,@p,'Admin')",
                    Db.P("@n", "Store Manager"), Db.P("@u", "admin"), Db.P("@p", Security.HashPin("1234")));
                Db.Exec("INSERT INTO dbo.Users(FullName,Username,PinHash,Role) VALUES (@n,@u,@p,'Cashier')",
                    Db.P("@n", "Cashier One"), Db.P("@u", "cashier1"), Db.P("@p", Security.HashPin("1111")));
            }

            if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Categories")) == 0)
            {
                var cats = new[]
                {
                    new[] { "Beverages", "#3B82F6" }, new[] { "Snacks", "#F59E0B" }, new[] { "Dairy", "#0EA5E9" },
                    new[] { "Bakery", "#F97316" }, new[] { "Household", "#8B5CF6" }, new[] { "Vegetables", "#22C55E" },
                    new[] { "Fruits", "#EF4444" }
                };
                for (int i = 0; i < cats.Length; i++)
                    Db.Exec("INSERT INTO dbo.Categories(Name,Color,SortOrder) VALUES (@n,@c,@s)",
                        Db.P("@n", cats[i][0]), Db.P("@c", cats[i][1]), Db.P("@s", i));
            }

            if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Products")) == 0)
            {
                var seeds = new List<Seed>
                {
                    new Seed("Coca-Cola", "Beverages", "1.5L", 480, 410, 48, 12, "\U0001F964", "4890008100011"),
                    new Seed("Sprite", "Beverages", "500ml", 190, 160, 60, 12, "\U0001F964", "4890008100028"),
                    new Seed("Orange Juice", "Beverages", "1L", 620, 520, 24, 8, "\U0001F9C3", "4890008100035"),
                    new Seed("Mineral Water", "Beverages", "1L", 120, 85, 120, 24, "\U0001F4A7", "4890008100042"),
                    new Seed("Ceylon Tea", "Beverages", "200g", 540, 450, 30, 8, "\U0001F375", "4890008100059"),
                    new Seed("Instant Coffee", "Beverages", "50g", 780, 650, 7, 8, "\u2615", "4890008100066"),

                    new Seed("Potato Chips", "Snacks", "100g", 250, 200, 55, 10, "\U0001F35F", "4890008200018"),
                    new Seed("Chocolate Bar", "Snacks", "45g", 180, 140, 80, 20, "\U0001F36B", "4890008200025"),
                    new Seed("Cream Biscuits", "Snacks", "200g", 220, 170, 45, 10, "\U0001F36A", "4890008200032"),
                    new Seed("Salted Peanuts", "Snacks", "150g", 260, 200, 4, 10, "\U0001F95C", "4890008200049"),
                    new Seed("Popcorn", "Snacks", "80g", 210, 160, 35, 8, "\U0001F37F", "4890008200056"),

                    new Seed("Fresh Milk", "Dairy", "1L", 420, 360, 36, 10, "\U0001F95B", "4890008300015"),
                    new Seed("Cheddar Cheese", "Dairy", "200g", 890, 740, 18, 6, "\U0001F9C0", "4890008300022"),
                    new Seed("Yoghurt Cup", "Dairy", "80g", 95, 70, 70, 20, "\U0001F366", "4890008300039"),
                    new Seed("Butter", "Dairy", "100g", 410, 340, 0, 6, "\U0001F9C8", "4890008300046"),
                    new Seed("Farm Eggs", "Dairy", "10 pcs", 520, 440, 4, 10, "\U0001F95A", "4890008300053"),

                    new Seed("White Bread", "Bakery", "450g", 160, 120, 40, 10, "\U0001F35E", "4890008400012"),
                    new Seed("Croissant", "Bakery", "1 pc", 140, 95, 3, 8, "\U0001F950", "4890008400029"),
                    new Seed("Sandwich Buns", "Bakery", "4 pcs", 120, 85, 28, 8, "\U0001F956", "4890008400036"),
                    new Seed("Chocolate Cake", "Bakery", "Slice", 350, 250, 12, 4, "\U0001F370", "4890008400043"),
                    new Seed("Glazed Donut", "Bakery", "1 pc", 130, 90, 22, 6, "\U0001F369", "4890008400050"),

                    new Seed("Dish Soap", "Household", "500ml", 340, 270, 26, 6, "\U0001F9F4", "4890008500019"),
                    new Seed("Laundry Powder", "Household", "1kg", 690, 570, 20, 5, "\U0001F9FA", "4890008500026"),
                    new Seed("Tissue Rolls", "Household", "4 pack", 380, 300, 5, 8, "\U0001F9FB", "4890008500033"),
                    new Seed("Toothpaste", "Household", "120g", 290, 220, 32, 8, "\U0001F9B7", "4890008500040"),
                    new Seed("Bath Soap", "Household", "100g", 150, 110, 64, 12, "\U0001F9FC", "4890008500057"),

                    new Seed("Carrots", "Vegetables", "1kg", 320, 240, 40, 8, "\U0001F955", "4890008600016"),
                    new Seed("Tomatoes", "Vegetables", "1kg", 380, 290, 35, 8, "\U0001F345", "4890008600023"),
                    new Seed("Potatoes", "Vegetables", "1kg", 290, 220, 60, 12, "\U0001F954", "4890008600030"),
                    new Seed("Red Onions", "Vegetables", "1kg", 340, 260, 45, 10, "\U0001F9C5", "4890008600047"),
                    new Seed("Broccoli", "Vegetables", "500g", 450, 350, 14, 5, "\U0001F966", "4890008600054"),
                    new Seed("Green Chilli", "Vegetables", "100g", 90, 60, 9, 10, "\U0001F336", "4890008600061"),

                    new Seed("Red Apples", "Fruits", "1kg", 780, 640, 30, 8, "\U0001F34E", "4890008700013"),
                    new Seed("Bananas", "Fruits", "1kg", 280, 200, 50, 10, "\U0001F34C", "4890008700020"),
                    new Seed("Oranges", "Fruits", "1kg", 650, 520, 26, 8, "\U0001F34A", "4890008700037"),
                    new Seed("Grapes", "Fruits", "500g", 720, 580, 16, 6, "\U0001F347", "4890008700044"),
                    new Seed("Watermelon", "Fruits", "1kg", 210, 150, 22, 6, "\U0001F349", "4890008700051"),
                    new Seed("Pineapple", "Fruits", "1 pc", 330, 250, 18, 5, "\U0001F34D", "4890008700068")
                };

                foreach (var s in seeds)
                {
                    Db.Exec(@"INSERT INTO dbo.Products(Barcode,Name,CategoryId,Unit,Price,CostPrice,Stock,MinStock,Icon)
                              VALUES (@b,@n,(SELECT CategoryId FROM dbo.Categories WHERE Name=@c),@u,@p,@cost,@s,@m,@i)",
                        Db.P("@b", s.Barcode), Db.P("@n", s.Name), Db.P("@c", s.Category), Db.P("@u", s.Unit),
                        Db.P("@p", s.Price), Db.P("@cost", s.Cost), Db.P("@s", s.Stock), Db.P("@m", s.Min), Db.P("@i", s.Icon));
                }
            }

            if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Customers")) == 0)
            {
                Db.Exec("INSERT INTO dbo.Customers(Name,Phone) VALUES (@n,@p)", Db.P("@n", "Nimal Perera"), Db.P("@p", "077 123 4567"));
                Db.Exec("INSERT INTO dbo.Customers(Name,Phone) VALUES (@n,@p)", Db.P("@n", "Kamala Silva"), Db.P("@p", "071 987 6543"));
            }

            if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Settings")) == 0)
            {
                var defaults = new Dictionary<string, string>
                {
                    { "StoreName", "FreshMart Grocery" },
                    { "StoreAddress", "123 Main Street, Colombo" },
                    { "StorePhone", "+94 11 234 5678" },
                    { "Currency", "Rs." },
                    { "TaxRate", "0" },
                    { "ReceiptFooter", "Thank you for shopping with us!" },
                    { "AutoPrint", "1" },
                    { "Printer", "" }
                };
                foreach (var kv in defaults)
                    Db.Exec("INSERT INTO dbo.Settings(K,V) VALUES (@k,@v)", Db.P("@k", kv.Key), Db.P("@v", kv.Value));
            }
        }
    }
}
