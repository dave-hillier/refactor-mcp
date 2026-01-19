# Convert to Static Refactoring

## Overview
RefactorMCP provides two ways to convert instance methods to static:

1. **convert-to-static-with-instance**: Adds the instance as a parameter (good for moving methods)
2. **convert-to-static-with-parameters**: Adds each used member as a parameter (good for pure functions)

## When to Use
- When preparing to move a method to another class
- When a method doesn't conceptually belong to the instance
- When you want to test a method independently of the class
- When converting to a pure function for better reasoning

---

## Example 1: Convert to Static with Instance Parameter

### Before
```csharp
public class Invoice
{
    public string InvoiceNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public List<InvoiceLineItem> LineItems { get; set; } = new();
    public decimal TaxRate { get; set; }
    public Customer Customer { get; set; }
    public InvoiceStatus Status { get; set; }

    // This method could be moved to an InvoicePdfGenerator class
    public byte[] GeneratePdf()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var graphics = XGraphics.FromPdfPage(page);

        // Header
        graphics.DrawString($"Invoice #{InvoiceNumber}",
            new XFont("Arial", 20, XFontStyle.Bold),
            XBrushes.Black, new XPoint(50, 50));

        graphics.DrawString($"Date: {IssueDate:d}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(50, 80));

        graphics.DrawString($"Due: {DueDate:d}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(50, 100));

        // Customer info
        graphics.DrawString($"Bill To: {Customer.Name}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(50, 140));

        graphics.DrawString(Customer.Address,
            new XFont("Arial", 10),
            XBrushes.Black, new XPoint(50, 160));

        // Line items
        var yPos = 200;
        foreach (var item in LineItems)
        {
            graphics.DrawString(
                $"{item.Description}: {item.Quantity} x ${item.UnitPrice:F2} = ${item.Total:F2}",
                new XFont("Arial", 10),
                XBrushes.Black, new XPoint(50, yPos));
            yPos += 20;
        }

        // Totals
        var subtotal = LineItems.Sum(i => i.Total);
        var tax = subtotal * TaxRate;
        var total = subtotal + tax;

        graphics.DrawString($"Subtotal: ${subtotal:F2}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(350, yPos + 20));

        graphics.DrawString($"Tax ({TaxRate:P0}): ${tax:F2}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(350, yPos + 40));

        graphics.DrawString($"Total: ${total:F2}",
            new XFont("Arial", 14, XFontStyle.Bold),
            XBrushes.Black, new XPoint(350, yPos + 60));

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }
}
```

### After (convert-to-static-with-instance)
```csharp
public class Invoice
{
    public string InvoiceNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public List<InvoiceLineItem> LineItems { get; set; } = new();
    public decimal TaxRate { get; set; }
    public Customer Customer { get; set; }
    public InvoiceStatus Status { get; set; }

    // Wrapper method preserves the original API
    public byte[] GeneratePdf() => GeneratePdf(this);

    // Static version that can be moved to InvoicePdfGenerator
    public static byte[] GeneratePdf(Invoice invoice)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var graphics = XGraphics.FromPdfPage(page);

        // Header - now using invoice parameter
        graphics.DrawString($"Invoice #{invoice.InvoiceNumber}",
            new XFont("Arial", 20, XFontStyle.Bold),
            XBrushes.Black, new XPoint(50, 50));

        graphics.DrawString($"Date: {invoice.IssueDate:d}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(50, 80));

        graphics.DrawString($"Due: {invoice.DueDate:d}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(50, 100));

        // Customer info
        graphics.DrawString($"Bill To: {invoice.Customer.Name}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(50, 140));

        graphics.DrawString(invoice.Customer.Address,
            new XFont("Arial", 10),
            XBrushes.Black, new XPoint(50, 160));

        // Line items
        var yPos = 200;
        foreach (var item in invoice.LineItems)
        {
            graphics.DrawString(
                $"{item.Description}: {item.Quantity} x ${item.UnitPrice:F2} = ${item.Total:F2}",
                new XFont("Arial", 10),
                XBrushes.Black, new XPoint(50, yPos));
            yPos += 20;
        }

        // Totals
        var subtotal = invoice.LineItems.Sum(i => i.Total);
        var tax = subtotal * invoice.TaxRate;
        var total = subtotal + tax;

        graphics.DrawString($"Subtotal: ${subtotal:F2}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(350, yPos + 20));

        graphics.DrawString($"Tax ({invoice.TaxRate:P0}): ${tax:F2}",
            new XFont("Arial", 12),
            XBrushes.Black, new XPoint(350, yPos + 40));

        graphics.DrawString($"Total: ${total:F2}",
            new XFont("Arial", 14, XFontStyle.Bold),
            XBrushes.Black, new XPoint(350, yPos + 60));

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-static-with-instance '{
    "filePath": "Models/Invoice.cs",
    "methodName": "GeneratePdf"
}'
```

---

## Example 2: Convert to Static with Parameters (Pure Function)

### Before
```csharp
public class ShippingCalculator
{
    private readonly decimal _baseRate;
    private readonly decimal _ratePerPound;
    private readonly decimal _internationalSurcharge;
    private readonly decimal _expeditedMultiplier;

    public ShippingCalculator(
        decimal baseRate,
        decimal ratePerPound,
        decimal internationalSurcharge,
        decimal expeditedMultiplier)
    {
        _baseRate = baseRate;
        _ratePerPound = ratePerPound;
        _internationalSurcharge = internationalSurcharge;
        _expeditedMultiplier = expeditedMultiplier;
    }

    // This calculation method only uses the fields - it can be a pure function
    public decimal CalculateRate(
        decimal weightInPounds,
        bool isInternational,
        bool isExpedited)
    {
        var rate = _baseRate + (weightInPounds * _ratePerPound);

        if (isInternational)
        {
            rate += _internationalSurcharge;
        }

        if (isExpedited)
        {
            rate *= _expeditedMultiplier;
        }

        return Math.Round(rate, 2);
    }
}
```

### After (convert-to-static-with-parameters)
```csharp
public class ShippingCalculator
{
    private readonly decimal _baseRate;
    private readonly decimal _ratePerPound;
    private readonly decimal _internationalSurcharge;
    private readonly decimal _expeditedMultiplier;

    public ShippingCalculator(
        decimal baseRate,
        decimal ratePerPound,
        decimal internationalSurcharge,
        decimal expeditedMultiplier)
    {
        _baseRate = baseRate;
        _ratePerPound = ratePerPound;
        _internationalSurcharge = internationalSurcharge;
        _expeditedMultiplier = expeditedMultiplier;
    }

    // Wrapper preserves original API
    public decimal CalculateRate(
        decimal weightInPounds,
        bool isInternational,
        bool isExpedited)
    {
        return CalculateRate(
            weightInPounds,
            isInternational,
            isExpedited,
            _baseRate,
            _ratePerPound,
            _internationalSurcharge,
            _expeditedMultiplier);
    }

    // Pure function - all inputs are parameters, no side effects
    public static decimal CalculateRate(
        decimal weightInPounds,
        bool isInternational,
        bool isExpedited,
        decimal baseRate,
        decimal ratePerPound,
        decimal internationalSurcharge,
        decimal expeditedMultiplier)
    {
        var rate = baseRate + (weightInPounds * ratePerPound);

        if (isInternational)
        {
            rate += internationalSurcharge;
        }

        if (isExpedited)
        {
            rate *= expeditedMultiplier;
        }

        return Math.Round(rate, 2);
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-static-with-parameters '{
    "filePath": "Services/ShippingCalculator.cs",
    "methodName": "CalculateRate"
}'
```

---

## Example 3: Preparing for Testability

### Before
```csharp
public class OrderValidator
{
    private readonly IInventoryService _inventory;
    private readonly ICustomerService _customers;

    // This validation method is hard to test because it uses instance services
    public ValidationResult Validate(Order order)
    {
        var errors = new List<string>();

        // Pure validation logic mixed with service calls
        if (order.Items.Count == 0)
        {
            errors.Add("Order must contain at least one item");
        }

        foreach (var item in order.Items)
        {
            if (item.Quantity <= 0)
            {
                errors.Add($"Item {item.ProductId} has invalid quantity");
            }

            if (item.UnitPrice < 0)
            {
                errors.Add($"Item {item.ProductId} has negative price");
            }
        }

        if (order.Total != order.Items.Sum(i => i.LineTotal))
        {
            errors.Add("Order total doesn't match item totals");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }
}
```

### After (Static for pure validation)
```csharp
public class OrderValidator
{
    private readonly IInventoryService _inventory;
    private readonly ICustomerService _customers;

    public ValidationResult Validate(Order order) => Validate(order);

    // Pure validation - easily unit tested without any dependencies
    public static ValidationResult Validate(Order order)
    {
        var errors = new List<string>();

        if (order.Items.Count == 0)
        {
            errors.Add("Order must contain at least one item");
        }

        foreach (var item in order.Items)
        {
            if (item.Quantity <= 0)
            {
                errors.Add($"Item {item.ProductId} has invalid quantity");
            }

            if (item.UnitPrice < 0)
            {
                errors.Add($"Item {item.ProductId} has negative price");
            }
        }

        if (order.Total != order.Items.Sum(i => i.LineTotal))
        {
            errors.Add("Order total doesn't match item totals");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }
}

// Now we can easily test:
[Fact]
public void Validate_EmptyOrder_ReturnsError()
{
    var order = new Order { Items = new List<OrderItem>() };

    var result = OrderValidator.Validate(order);

    Assert.False(result.IsValid);
    Assert.Contains("Order must contain at least one item", result.Errors);
}
```

---

## Benefits

### With Instance Parameter
- Preserves access to all instance members
- Good stepping stone to moving methods between classes
- Minimal changes to method body

### With Parameters (Pure Function)
- Complete isolation from class state
- Easier to test with any combination of inputs
- Can be cached/memoized based on inputs
- Thread-safe by default
- Easier to reason about behavior
