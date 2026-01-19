# Extract Method Refactoring

## Overview
The `extract-method` refactoring extracts a block of code into a new method, automatically detecting variables that need to be passed as parameters and handling return values.

## When to Use
- When a method is too long and contains distinct logical sections
- When code is duplicated and should be reused
- When a block of code needs its own unit test
- When a section of code has a clear, nameable purpose

---

## Example 1: Extract Validation Logic

### Before
```csharp
public class OrderProcessor
{
    private readonly IInventoryService _inventory;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<OrderProcessor> _logger;

    public async Task<OrderResult> ProcessOrderAsync(Order order)
    {
        // Validation logic that should be extracted
        if (order == null)
        {
            _logger.LogWarning("Order is null");
            return new OrderResult { Success = false, Error = "Order cannot be null" };
        }

        if (order.Items == null || !order.Items.Any())
        {
            _logger.LogWarning("Order has no items: {OrderId}", order.Id);
            return new OrderResult { Success = false, Error = "Order must contain at least one item" };
        }

        foreach (var item in order.Items)
        {
            if (item.Quantity <= 0)
            {
                _logger.LogWarning("Invalid quantity for item {ItemId}: {Quantity}", item.ProductId, item.Quantity);
                return new OrderResult { Success = false, Error = $"Invalid quantity for product {item.ProductId}" };
            }

            if (!await _inventory.IsInStockAsync(item.ProductId, item.Quantity))
            {
                _logger.LogWarning("Insufficient stock for {ItemId}", item.ProductId);
                return new OrderResult { Success = false, Error = $"Product {item.ProductId} is out of stock" };
            }
        }

        // Continue with payment processing
        var totalAmount = order.Items.Sum(i => i.Price * i.Quantity);
        var paymentResult = await _paymentGateway.ChargeAsync(order.CustomerId, totalAmount);

        if (!paymentResult.Success)
        {
            return new OrderResult { Success = false, Error = "Payment failed" };
        }

        return new OrderResult { Success = true, OrderId = order.Id };
    }
}
```

### After
```csharp
public class OrderProcessor
{
    private readonly IInventoryService _inventory;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<OrderProcessor> _logger;

    public async Task<OrderResult> ProcessOrderAsync(Order order)
    {
        var validationResult = await ValidateOrderAsync(order);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Continue with payment processing
        var totalAmount = order.Items.Sum(i => i.Price * i.Quantity);
        var paymentResult = await _paymentGateway.ChargeAsync(order.CustomerId, totalAmount);

        if (!paymentResult.Success)
        {
            return new OrderResult { Success = false, Error = "Payment failed" };
        }

        return new OrderResult { Success = true, OrderId = order.Id };
    }

    private async Task<OrderResult?> ValidateOrderAsync(Order order)
    {
        if (order == null)
        {
            _logger.LogWarning("Order is null");
            return new OrderResult { Success = false, Error = "Order cannot be null" };
        }

        if (order.Items == null || !order.Items.Any())
        {
            _logger.LogWarning("Order has no items: {OrderId}", order.Id);
            return new OrderResult { Success = false, Error = "Order must contain at least one item" };
        }

        foreach (var item in order.Items)
        {
            if (item.Quantity <= 0)
            {
                _logger.LogWarning("Invalid quantity for item {ItemId}: {Quantity}", item.ProductId, item.Quantity);
                return new OrderResult { Success = false, Error = $"Invalid quantity for product {item.ProductId}" };
            }

            if (!await _inventory.IsInStockAsync(item.ProductId, item.Quantity))
            {
                _logger.LogWarning("Insufficient stock for {ItemId}", item.ProductId);
                return new OrderResult { Success = false, Error = $"Product {item.ProductId} is out of stock" };
            }
        }

        return null; // Validation passed
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-method '{
    "filePath": "OrderProcessor.cs",
    "startLine": 12,
    "endLine": 32,
    "newMethodName": "ValidateOrderAsync"
}'
```

---

## Example 2: Extract Complex Calculation

### Before
```csharp
public class PricingEngine
{
    public decimal CalculateFinalPrice(Product product, Customer customer, PromotionContext promotions)
    {
        // Complex pricing calculation that should be extracted
        decimal basePrice = product.BasePrice;

        // Apply category-specific markup
        decimal categoryMultiplier = product.Category switch
        {
            ProductCategory.Electronics => 1.15m,
            ProductCategory.Luxury => 1.25m,
            ProductCategory.Essential => 1.05m,
            _ => 1.10m
        };
        basePrice *= categoryMultiplier;

        // Apply seasonal adjustments
        var currentMonth = DateTime.Now.Month;
        if (currentMonth >= 11 && currentMonth <= 12)
        {
            basePrice *= 1.20m; // Holiday markup
        }
        else if (currentMonth >= 6 && currentMonth <= 8)
        {
            basePrice *= 0.90m; // Summer discount
        }

        // Apply volume discount based on customer's purchase history
        decimal volumeDiscount = 0m;
        if (customer.TotalPurchasesThisYear > 10000m)
            volumeDiscount = 0.15m;
        else if (customer.TotalPurchasesThisYear > 5000m)
            volumeDiscount = 0.10m;
        else if (customer.TotalPurchasesThisYear > 1000m)
            volumeDiscount = 0.05m;

        basePrice *= (1 - volumeDiscount);

        // Apply promotional discounts
        foreach (var promo in promotions.ActivePromotions)
        {
            if (promo.AppliesTo(product))
            {
                basePrice *= (1 - promo.DiscountPercent);
            }
        }

        // Apply loyalty tier benefits
        decimal loyaltyDiscount = customer.LoyaltyTier switch
        {
            LoyaltyTier.Gold => 0.05m,
            LoyaltyTier.Platinum => 0.10m,
            LoyaltyTier.Diamond => 0.15m,
            _ => 0m
        };
        basePrice *= (1 - loyaltyDiscount);

        return Math.Round(basePrice, 2);
    }
}
```

### After (Multiple Extractions)
```csharp
public class PricingEngine
{
    public decimal CalculateFinalPrice(Product product, Customer customer, PromotionContext promotions)
    {
        decimal basePrice = product.BasePrice;

        basePrice = ApplyCategoryMarkup(basePrice, product.Category);
        basePrice = ApplySeasonalAdjustment(basePrice);
        basePrice = ApplyVolumeDiscount(basePrice, customer.TotalPurchasesThisYear);
        basePrice = ApplyPromotionalDiscounts(basePrice, product, promotions);
        basePrice = ApplyLoyaltyDiscount(basePrice, customer.LoyaltyTier);

        return Math.Round(basePrice, 2);
    }

    private decimal ApplyCategoryMarkup(decimal price, ProductCategory category)
    {
        decimal categoryMultiplier = category switch
        {
            ProductCategory.Electronics => 1.15m,
            ProductCategory.Luxury => 1.25m,
            ProductCategory.Essential => 1.05m,
            _ => 1.10m
        };
        return price * categoryMultiplier;
    }

    private decimal ApplySeasonalAdjustment(decimal price)
    {
        var currentMonth = DateTime.Now.Month;
        if (currentMonth >= 11 && currentMonth <= 12)
        {
            return price * 1.20m; // Holiday markup
        }
        else if (currentMonth >= 6 && currentMonth <= 8)
        {
            return price * 0.90m; // Summer discount
        }
        return price;
    }

    private decimal ApplyVolumeDiscount(decimal price, decimal totalPurchases)
    {
        decimal volumeDiscount = 0m;
        if (totalPurchases > 10000m)
            volumeDiscount = 0.15m;
        else if (totalPurchases > 5000m)
            volumeDiscount = 0.10m;
        else if (totalPurchases > 1000m)
            volumeDiscount = 0.05m;

        return price * (1 - volumeDiscount);
    }

    private decimal ApplyPromotionalDiscounts(decimal price, Product product, PromotionContext promotions)
    {
        foreach (var promo in promotions.ActivePromotions)
        {
            if (promo.AppliesTo(product))
            {
                price *= (1 - promo.DiscountPercent);
            }
        }
        return price;
    }

    private decimal ApplyLoyaltyDiscount(decimal price, LoyaltyTier tier)
    {
        decimal loyaltyDiscount = tier switch
        {
            LoyaltyTier.Gold => 0.05m,
            LoyaltyTier.Platinum => 0.10m,
            LoyaltyTier.Diamond => 0.15m,
            _ => 0m
        };
        return price * (1 - loyaltyDiscount);
    }
}
```

---

## Benefits
1. **Improved Readability**: Main method now clearly shows the high-level algorithm
2. **Testability**: Each extracted method can be unit tested independently
3. **Reusability**: Extracted methods can be called from other places
4. **Single Responsibility**: Each method has one clear purpose
5. **Maintainability**: Changes to specific logic are isolated
