namespace ECommerce;

// ── Domain Models ──────────────────────────────────────────────────

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string CustomerId { get; set; } = "";
    public List<OrderItem> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? CouponCode { get; set; }
    public ShippingAddress ShippingAddress { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Weight { get; set; }
}

public enum OrderStatus
{
    Pending,
    Validated,
    PaymentProcessed,
    Shipped,
    Delivered,
    Cancelled,
    Refunded
}

public class ShippingAddress
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string Country { get; set; } = "US";
}

public class Customer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public CustomerTier Tier { get; set; } = CustomerTier.Standard;
    public decimal LifetimeSpend { get; set; }
    public DateTime MemberSince { get; set; }
    public List<string> OrderHistory { get; set; } = new();
}

public enum CustomerTier
{
    Standard,
    Silver,
    Gold,
    Platinum
}

public class Product
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal BasePrice { get; set; }
    public string Category { get; set; } = "";
    public int StockQuantity { get; set; }
    public decimal Weight { get; set; }
    public bool IsDiscontinued { get; set; }
}

public class PaymentResult
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal AmountCharged { get; set; }
}

public class ShippingRate
{
    public string Carrier { get; set; } = "";
    public decimal Cost { get; set; }
    public int EstimatedDays { get; set; }
}

public class Invoice
{
    public string InvoiceNumber { get; set; } = "";
    public string OrderId { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public DateTime IssuedAt { get; set; }
    public List<InvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine
{
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

// ── Configuration ──────────────────────────────────────────────────

public class AppConfig
{
    public bool EnableNewPricingEngine { get; set; }
    public bool EnableBulkDiscounts { get; set; }
    public decimal TaxRate { get; set; } = 0.08m;
    public string DefaultCurrency { get; set; } = "USD";
}
