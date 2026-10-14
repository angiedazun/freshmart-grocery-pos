using System;
using System.Collections.Generic;
using System.Linq;
using GroceryPOS.Services;

namespace GroceryPOS.Models
{
    public class Category
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Color { get; set; } = "#16A34A";
        public int SortOrder { get; set; }
        public int ProductCount { get; set; }
        public override string ToString() { return Name; }
    }

    public class Product
    {
        public int ProductId { get; set; }
        public string Barcode { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string CategoryColor { get; set; } = "#16A34A";
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public decimal CostPrice { get; set; }
        public int Stock { get; set; }
        public int MinStock { get; set; } = 5;
        public string Icon { get; set; }
        public string ImagePath { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Sold by weight: Price/CostPrice are per kg and Stock/MinStock are in grams.</summary>
        public bool IsWeighed { get; set; }

        public string StockText { get { return IsWeighed ? Fmt.Weight(Stock) : Stock.ToString(); } }
        public string MinText { get { return IsWeighed ? Fmt.Weight(MinStock) : MinStock.ToString(); } }
        public string PriceText { get { return IsWeighed ? Fmt.Num(Price) + " /kg" : Fmt.Num(Price); } }
        public string SuggestedText { get { return IsWeighed ? Fmt.Weight(SuggestedReorder) : SuggestedReorder.ToString(); } }
        public decimal SuggestedCost { get { return IsWeighed ? SuggestedReorder * CostPrice / 1000m : SuggestedReorder * CostPrice; } }
        public decimal StockValue { get { var s = Math.Max(0, Stock); return IsWeighed ? s * CostPrice / 1000m : s * CostPrice; } }

        public bool IsOut { get { return Stock <= 0; } }
        public bool IsLow { get { return Stock > 0 && Stock <= MinStock; } }
        public string Status { get { return IsOut ? "Out of stock" : IsLow ? "Low stock" : "In stock"; } }
        public int SuggestedReorder { get { return Math.Max(MinStock * 2 - Stock, MinStock); } }
    }

    public class UserAccount
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public string PinHash { get; set; }
        public bool IsAdmin { get { return Role == "Admin"; } }
        public string Status { get { return IsActive ? "Active" : "Disabled"; } }
        public override string ToString() { return FullName; }

        public string Initials
        {
            get
            {
                var parts = (FullName ?? "?").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "?";
                if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpper();
                return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpper();
            }
        }
    }

    public class Customer
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public decimal Balance { get; set; }
    }

    public class CartLine
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int MaxStock { get; set; }
        public string Icon { get; set; }
        public string ImagePath { get; set; }
        public string CategoryColor { get; set; }
        public bool IsWeighed { get; set; }
        /// <summary>For weighed lines Quantity is grams and UnitPrice is per kg.</summary>
        public decimal LineTotal { get { return IsWeighed ? Math.Round(UnitPrice * Quantity / 1000m, 2) : UnitPrice * Quantity; } }
        public string QtyText { get { return Fmt.Qty(IsWeighed, Quantity); } }
    }

    public enum AddResult { Added, OutOfStock, LimitReached }

    /// <summary>The live shopping cart: lines + discount + tax maths.</summary>
    public class Cart
    {
        public List<CartLine> Lines { get; } = new List<CartLine>();
        public bool DiscountIsPercent { get; set; } = true;
        public decimal DiscountValue { get; set; }
        public decimal TaxRate { get; set; }
        public event Action Changed;

        void Raise() { var h = Changed; if (h != null) h(); }

        public int ItemCount { get { return Lines.Sum(l => l.IsWeighed ? 1 : l.Quantity); } }
        public decimal Subtotal { get { return Lines.Sum(l => l.LineTotal); } }

        public decimal DiscountAmount
        {
            get
            {
                if (DiscountValue <= 0 || Lines.Count == 0) return 0;
                decimal d = DiscountIsPercent ? Math.Round(Subtotal * Math.Min(DiscountValue, 100m) / 100m, 2) : DiscountValue;
                return Math.Min(d, Subtotal);
            }
        }

        public decimal TaxAmount { get { return Math.Round((Subtotal - DiscountAmount) * TaxRate / 100m, 2); } }
        public decimal Total { get { return Subtotal - DiscountAmount + TaxAmount; } }

        public AddResult Add(Product p, int qty = 1)
        {
            if (p.Stock <= 0) return AddResult.OutOfStock;
            var line = Lines.FirstOrDefault(l => l.ProductId == p.ProductId);
            if (line != null)
            {
                if (line.Quantity + qty > p.Stock) return AddResult.LimitReached;
                line.Quantity += qty;
                line.MaxStock = p.Stock;
            }
            else
            {
                if (qty > p.Stock) return AddResult.LimitReached;
                Lines.Add(new CartLine
                {
                    ProductId = p.ProductId, Name = p.Name, Unit = p.Unit, UnitPrice = p.Price,
                    Quantity = qty, MaxStock = p.Stock, IsWeighed = p.IsWeighed, Icon = p.Icon, ImagePath = p.ImagePath, CategoryColor = p.CategoryColor
                });
            }
            Raise();
            return AddResult.Added;
        }

        public bool SetQuantity(CartLine line, int qty)
        {
            if (qty <= 0) { Lines.Remove(line); Raise(); return true; }
            if (qty > line.MaxStock) return false;
            line.Quantity = qty;
            Raise();
            return true;
        }

        public void Remove(CartLine line) { Lines.Remove(line); if (Lines.Count == 0) { DiscountValue = 0; } Raise(); }
        public void Clear() { Lines.Clear(); DiscountValue = 0; Raise(); }
        public void NotifyChanged() { Raise(); }
    }

    public class SaleItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public bool IsWeighed { get; set; }
    }

    public class Sale
    {
        public int SaleId { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime SaleDate { get; set; }
        public int UserId { get; set; }
        public string CashierName { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; }
        public decimal AmountGiven { get; set; }
        public decimal ChangeAmount { get; set; }
        public string Reference { get; set; }
        public string Status { get; set; }
        public List<SaleItem> Items { get; set; } = new List<SaleItem>();
        public int ItemCount { get { return Items.Sum(i => i.IsWeighed ? 1 : i.Quantity); } }
    }

    public class PaymentInfo
    {
        public string Method { get; set; } = "Cash";
        public decimal AmountGiven { get; set; }
        public decimal Change { get; set; }
        public int? CustomerId { get; set; }
        public string Reference { get; set; }
    }

    public class ZReport
    {
        public DateTime Date { get; set; }
        public string CashierName { get; set; }
        public int Transactions { get; set; }
        public int ItemsSold { get; set; }
        public decimal GrossSales { get; set; }
        public decimal Discounts { get; set; }
        public decimal Tax { get; set; }
        public decimal NetSales { get; set; }
        public decimal Cash { get; set; }
        public decimal Card { get; set; }
        public decimal Credit { get; set; }
        public int VoidedCount { get; set; }
        public decimal VoidedAmount { get; set; }
        public decimal AverageBasket { get { return Transactions == 0 ? 0 : NetSales / Transactions; } }
    }

    public class HeldOrder
    {
        public int HeldId { get; set; }
        public string Label { get; set; }
        public DateTime HeldAt { get; set; }
        public int ItemCount { get; set; }
        public decimal Total { get; set; }
        public bool DiscountIsPercent { get; set; }
        public decimal DiscountValue { get; set; }
        public List<KeyValuePair<int, int>> Lines { get; set; } = new List<KeyValuePair<int, int>>();
    }
}
