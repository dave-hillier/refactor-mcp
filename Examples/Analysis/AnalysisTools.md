# Analysis and Utility Tools

## Overview
RefactorMCP includes several tools for analyzing code quality and performing utility operations:

- **analyze-refactoring-opportunities**: Detect code smells and suggest refactorings
- **class-length-metrics**: Measure class sizes across the solution
- **cleanup-usings**: Remove unused using directives
- **rename-symbol**: Rename symbols across the solution
- **feature-flag-refactor**: Convert feature flags to strategy pattern

---

## Tool 1: Analyze Refactoring Opportunities

### Purpose
Scans a C# file and identifies potential refactoring opportunities based on common code smells.

### Example Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json analyze-refactoring-opportunities '{
    "filePath": "Services/OrderService.cs"
}'
```

### Sample Output
```json
{
  "file": "Services/OrderService.cs",
  "opportunities": [
    {
      "type": "LongMethod",
      "location": "ProcessOrderAsync",
      "lineCount": 87,
      "threshold": 30,
      "suggestion": "Consider extracting this method into smaller, focused methods"
    },
    {
      "type": "LargeClass",
      "location": "OrderService",
      "memberCount": 42,
      "threshold": 20,
      "suggestion": "Consider splitting this class by responsibility"
    },
    {
      "type": "FeatureEnvy",
      "location": "CalculateShipping",
      "accessedClass": "ShippingRate",
      "accessCount": 8,
      "suggestion": "This method accesses ShippingRate more than its own class. Consider moving it"
    },
    {
      "type": "DuplicateCode",
      "location": "Lines 45-52, 78-85",
      "similarity": 0.92,
      "suggestion": "These code blocks are very similar. Consider extracting a shared method"
    },
    {
      "type": "UnusedParameter",
      "location": "SendNotification(customerId, orderId, priority)",
      "parameter": "priority",
      "suggestion": "Parameter 'priority' is never used. Consider removing it"
    },
    {
      "type": "DeadCode",
      "location": "FormatLegacyOrder",
      "suggestion": "This method has no callers. Consider removing it"
    }
  ],
  "summary": {
    "totalOpportunities": 6,
    "highPriority": 2,
    "mediumPriority": 3,
    "lowPriority": 1
  }
}
```

### Code Being Analyzed
```csharp
public class OrderService
{
    // 42 members - triggers LargeClass warning

    public async Task<Order> ProcessOrderAsync(OrderRequest request)
    {
        // 87 lines - triggers LongMethod warning

        // Validation (lines 10-25)
        if (request == null) throw new ArgumentNullException();
        if (request.Items == null || !request.Items.Any())
            throw new InvalidOperationException("Order must have items");
        // ... more validation ...

        // Inventory check (lines 26-40)
        foreach (var item in request.Items)
        {
            var available = await _inventory.GetAvailableQuantityAsync(item.ProductId);
            if (available < item.Quantity)
                throw new InsufficientInventoryException(item.ProductId);
        }

        // Pricing calculation - accesses ShippingRate 8 times (Feature Envy)
        var shipping = CalculateShipping(request);

        // Duplicate code block #1 (lines 45-52)
        var taxRate = GetTaxRate(request.ShippingAddress.State);
        var subtotal = request.Items.Sum(i => i.Price * i.Quantity);
        var tax = subtotal * taxRate;
        var total = subtotal + tax + shipping;
        // ...

        // More processing...

        // Duplicate code block #2 (lines 78-85)
        var invoiceTaxRate = GetTaxRate(request.BillingAddress.State);
        var invoiceSubtotal = request.Items.Sum(i => i.Price * i.Quantity);
        var invoiceTax = invoiceSubtotal * invoiceTaxRate;
        var invoiceTotal = invoiceSubtotal + invoiceTax + shipping;

        // ... continues for 87 lines total
    }

    private decimal CalculateShipping(OrderRequest request)
    {
        // Accesses ShippingRate properties 8 times - Feature Envy
        var rate = _shippingRates.GetRate(request.ShippingAddress);
        var weight = rate.BaseWeight;
        var perPound = rate.PerPoundCharge;
        var handling = rate.HandlingFee;
        var insurance = rate.InsuranceRate;
        var fuelSurcharge = rate.FuelSurcharge;
        var residential = rate.ResidentialSurcharge;
        var signature = rate.SignatureRequired ? rate.SignatureFee : 0;

        return weight * perPound + handling + insurance + fuelSurcharge + residential + signature;
    }

    // Unused parameter
    public void SendNotification(int customerId, int orderId, NotificationPriority priority)
    {
        // priority is never used!
        _notificationService.Send(customerId, $"Order {orderId} confirmed");
    }

    // Dead code - no callers
    private string FormatLegacyOrder(Order order)
    {
        return $"ORDER-{order.Id:D8}";
    }
}
```

---

## Tool 2: Class Length Metrics

### Purpose
Lists all classes in the solution ordered by line count, helping identify classes that might need splitting.

### Example Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json class-length-metrics '{
    "solutionPath": "MyApp.sln"
}'
```

### Sample Output
```json
{
  "solution": "MyApp.sln",
  "classes": [
    { "name": "OrderService", "file": "Services/OrderService.cs", "lines": 542, "members": 47 },
    { "name": "UserManager", "file": "Identity/UserManager.cs", "lines": 389, "members": 35 },
    { "name": "ReportGenerator", "file": "Reports/ReportGenerator.cs", "lines": 312, "members": 28 },
    { "name": "PaymentProcessor", "file": "Payments/PaymentProcessor.cs", "lines": 287, "members": 24 },
    { "name": "DataImporter", "file": "Import/DataImporter.cs", "lines": 256, "members": 22 }
  ],
  "statistics": {
    "totalClasses": 127,
    "averageLines": 89,
    "medianLines": 52,
    "classesOver200Lines": 12,
    "classesOver500Lines": 1
  },
  "recommendations": [
    "OrderService (542 lines) is significantly larger than average. Consider splitting by responsibility.",
    "12 classes exceed 200 lines. Review for potential decomposition."
  ]
}
```

---

## Tool 3: Cleanup Usings

### Purpose
Removes unused `using` directives from C# files, cleaning up unnecessary imports.

### Example Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json cleanup-usings '{
    "filePath": "Services/OrderService.cs"
}'
```

### Before
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;                    // Unused
using System.Threading.Tasks;
using System.IO;                      // Unused
using System.Net.Http;                // Unused
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;                // Unused - we use System.Text.Json
using MyApp.Models;
using MyApp.Repositories;
using MyApp.Services.Legacy;          // Unused

namespace MyApp.Services
{
    public class OrderService
    {
        // Only uses: System, System.Collections.Generic, System.Linq,
        // System.Threading.Tasks, Microsoft.Extensions.Logging,
        // MyApp.Models, MyApp.Repositories
    }
}
```

### After
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Models;
using MyApp.Repositories;

namespace MyApp.Services
{
    public class OrderService
    {
        // Clean imports
    }
}
```

### Sample Output
```json
{
  "file": "Services/OrderService.cs",
  "removed": [
    "System.Text",
    "System.IO",
    "System.Net.Http",
    "Newtonsoft.Json",
    "MyApp.Services.Legacy"
  ],
  "remaining": 7,
  "savedLines": 5
}
```

---

## Tool 4: Rename Symbol

### Purpose
Renames a symbol (class, method, field, property, variable, etc.) across the entire solution, updating all references.

### Example Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json rename-symbol '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/OrderService.cs",
    "symbolName": "ProcessOrder",
    "newName": "ProcessOrderAsync"
}'
```

### Before (Multiple files affected)
```csharp
// OrderService.cs
public class OrderService
{
    public async Task<Order> ProcessOrder(OrderRequest request)
    {
        // Implementation
    }
}

// OrderController.cs
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request)
{
    var order = await _orderService.ProcessOrder(request);
    return Ok(order);
}

// OrderServiceTests.cs
[Fact]
public async Task ProcessOrder_ValidRequest_ReturnsOrder()
{
    var result = await _service.ProcessOrder(validRequest);
    Assert.NotNull(result);
}

// IntegrationTests.cs
[Fact]
public async Task FullOrderFlow_Success()
{
    var order = await _orderService.ProcessOrder(request);
    // ...
}
```

### After
```csharp
// OrderService.cs
public class OrderService
{
    public async Task<Order> ProcessOrderAsync(OrderRequest request)  // Renamed
    {
        // Implementation
    }
}

// OrderController.cs
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request)
{
    var order = await _orderService.ProcessOrderAsync(request);  // Updated
    return Ok(order);
}

// OrderServiceTests.cs
[Fact]
public async Task ProcessOrderAsync_ValidRequest_ReturnsOrder()  // Test name updated too
{
    var result = await _service.ProcessOrderAsync(validRequest);  // Updated
    Assert.NotNull(result);
}

// IntegrationTests.cs
[Fact]
public async Task FullOrderFlow_Success()
{
    var order = await _orderService.ProcessOrderAsync(request);  // Updated
    // ...
}
```

### Sample Output
```json
{
  "symbol": "ProcessOrder",
  "newName": "ProcessOrderAsync",
  "filesModified": [
    "Services/OrderService.cs",
    "Controllers/OrderController.cs",
    "Tests/OrderServiceTests.cs",
    "Tests/IntegrationTests.cs"
  ],
  "referencesUpdated": 4
}
```

---

## Tool 5: Feature Flag Refactor

### Purpose
Converts feature flag conditional logic into the Strategy pattern, making the code more maintainable and testable.

### Example Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json feature-flag-refactor '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/CheckoutService.cs",
    "featureFlagName": "UseNewCheckoutFlow"
}'
```

### Before
```csharp
public class CheckoutService
{
    private readonly IFeatureFlags _features;

    public async Task<CheckoutResult> ProcessCheckoutAsync(Cart cart)
    {
        if (_features.IsEnabled("UseNewCheckoutFlow"))
        {
            // New checkout flow - 50 lines of code
            var validation = await ValidateCartAsync(cart);
            if (!validation.IsValid)
                return CheckoutResult.ValidationFailed(validation.Errors);

            var payment = await ProcessPaymentWithNewGatewayAsync(cart);
            if (!payment.Success)
                return CheckoutResult.PaymentFailed(payment.Error);

            var order = await CreateOrderAsync(cart, payment);
            await SendNewConfirmationEmailAsync(order);

            return CheckoutResult.Success(order);
        }
        else
        {
            // Legacy checkout flow - 50 lines of code
            var isValid = ValidateCartLegacy(cart);
            if (!isValid)
                return CheckoutResult.ValidationFailed(new[] { "Invalid cart" });

            var paymentResult = ProcessPaymentLegacy(cart);
            if (paymentResult.Code != 0)
                return CheckoutResult.PaymentFailed("Payment failed");

            var order = CreateOrderLegacy(cart, paymentResult);
            SendLegacyConfirmationEmail(order);

            return CheckoutResult.Success(order);
        }
    }
}
```

### After
```csharp
// ICheckoutStrategy.cs
public interface ICheckoutStrategy
{
    Task<CheckoutResult> ProcessAsync(Cart cart);
}

// NewCheckoutStrategy.cs
public class NewCheckoutStrategy : ICheckoutStrategy
{
    public async Task<CheckoutResult> ProcessAsync(Cart cart)
    {
        var validation = await ValidateCartAsync(cart);
        if (!validation.IsValid)
            return CheckoutResult.ValidationFailed(validation.Errors);

        var payment = await ProcessPaymentWithNewGatewayAsync(cart);
        if (!payment.Success)
            return CheckoutResult.PaymentFailed(payment.Error);

        var order = await CreateOrderAsync(cart, payment);
        await SendNewConfirmationEmailAsync(order);

        return CheckoutResult.Success(order);
    }
}

// LegacyCheckoutStrategy.cs
public class LegacyCheckoutStrategy : ICheckoutStrategy
{
    public async Task<CheckoutResult> ProcessAsync(Cart cart)
    {
        var isValid = ValidateCartLegacy(cart);
        if (!isValid)
            return CheckoutResult.ValidationFailed(new[] { "Invalid cart" });

        var paymentResult = ProcessPaymentLegacy(cart);
        if (paymentResult.Code != 0)
            return CheckoutResult.PaymentFailed("Payment failed");

        var order = CreateOrderLegacy(cart, paymentResult);
        SendLegacyConfirmationEmail(order);

        return CheckoutResult.Success(order);
    }
}

// CheckoutService.cs - Now clean
public class CheckoutService
{
    private readonly ICheckoutStrategy _strategy;

    public CheckoutService(ICheckoutStrategy strategy)
    {
        _strategy = strategy;
    }

    public Task<CheckoutResult> ProcessCheckoutAsync(Cart cart)
    {
        return _strategy.ProcessAsync(cart);
    }
}

// DI Configuration - feature flag now controls strategy selection
services.AddScoped<ICheckoutStrategy>(sp =>
{
    var features = sp.GetRequiredService<IFeatureFlags>();
    return features.IsEnabled("UseNewCheckoutFlow")
        ? sp.GetRequiredService<NewCheckoutStrategy>()
        : sp.GetRequiredService<LegacyCheckoutStrategy>();
});
```

---

## Benefits of Analysis Tools

1. **Proactive Quality**: Find issues before they become problems
2. **Metrics-Driven**: Make data-informed refactoring decisions
3. **Automation**: Clean up code systematically across large codebases
4. **Consistency**: Apply naming conventions uniformly
5. **Technical Debt**: Identify and address code smells early
