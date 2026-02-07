# Demo 2: Method Transformation -- Extract Method & Inline Method

> **Goal:** Demonstrate two complementary refactorings that reshape method boundaries.
> *Extract Method* pulls a coherent block out of a long method into its own named method.
> *Inline Method* does the opposite -- it collapses a trivial wrapper back into its call site.
> Together they keep every method at the right level of abstraction.

---

## Demo 2a: Extract Method -- Pull validation out of `ProcessOrder`

### The Problem

`OrderProcessor.ProcessOrder` is a monolithic ~100-line method that handles **six distinct
phases**: validation, pricing, payment, inventory, auditing, and notification. Reading it
requires holding all of those concerns in your head at once. The first 25 lines are pure
input validation -- a self-contained block that checks for null arguments, empty orders,
invalid quantities, negative prices, insufficient stock, and incomplete addresses.

That validation block:

- has a single responsibility (reject bad inputs early),
- depends only on the method parameters and `_inventory`,
- returns early with an `OrderResult` on failure.

It is a textbook candidate for **Extract Method**.

### BEFORE -- `OrderProcessor.ProcessOrder` (full method)

The validation section that will be extracted is marked with `// <<<` annotations.

```csharp
public OrderResult ProcessOrder(Order order, Customer customer)
{
    // -- Phase 1: Validation (lines to extract into ValidateOrder) --
    if (order == null)                                                          // <<<
        return new OrderResult { Success = false, Error = "Order is null" };    // <<<
                                                                                // <<<
    if (customer == null)                                                       // <<<
        return new OrderResult { Success = false, Error = "Customer is null" }; // <<<
                                                                                // <<<
    if (order.Items == null || order.Items.Count == 0)                          // <<<
        return new OrderResult { Success = false, Error = "Order contains no items" }; // <<<
                                                                                // <<<
    foreach (var item in order.Items)                                           // <<<
    {                                                                           // <<<
        if (item.Quantity <= 0)                                                 // <<<
            return new OrderResult { ... };                                     // <<<
                                                                                // <<<
        if (item.UnitPrice < 0)                                                // <<<
            return new OrderResult { ... };                                     // <<<
                                                                                // <<<
        var stock = _inventory.GetStockLevel(item.ProductId);                   // <<<
        if (stock < item.Quantity)                                              // <<<
            return new OrderResult { ... };                                     // <<<
    }                                                                           // <<<
                                                                                // <<<
    if (string.IsNullOrWhiteSpace(order.ShippingAddress.Street) ||              // <<<
        string.IsNullOrWhiteSpace(order.ShippingAddress.City) ||                // <<<
        string.IsNullOrWhiteSpace(order.ShippingAddress.ZipCode))               // <<<
        return new OrderResult { Success = false, Error = "Incomplete shipping address" }; // <<<

    // -- Phase 2: Pricing calculation --
    decimal subtotal = 0m;
    foreach (var item in order.Items)
    {
        subtotal += item.UnitPrice * item.Quantity;
    }

    // Apply customer tier discount
    decimal x = customer.Tier switch
    {
        CustomerTier.Silver => 0.05m,
        CustomerTier.Gold => 0.10m,
        CustomerTier.Platinum => 0.15m,
        _ => 0m
    };
    subtotal *= (1 - x);

    // Apply coupon if present
    if (!string.IsNullOrEmpty(order.CouponCode))
    {
        if (order.CouponCode == "SAVE10")
            subtotal *= 0.90m;
        else if (order.CouponCode == "SAVE20")
            subtotal *= 0.80m;
        else if (order.CouponCode == "HALFOFF")
            subtotal *= 0.50m;
    }

    // Tax calculation
    decimal taxAmount = subtotal * 0.08m;
    decimal totalAmount = subtotal + taxAmount;

    // Shipping cost based on weight
    decimal totalWeight = order.Items.Sum(i => i.Weight * i.Quantity);
    decimal shippingCost;
    if (totalWeight <= 1.0m)
        shippingCost = 5.99m;
    else if (totalWeight <= 5.0m)
        shippingCost = 9.99m;
    else if (totalWeight <= 20.0m)
        shippingCost = 14.99m;
    else
        shippingCost = 24.99m;

    if (order.ShippingAddress.Country != "US")
        shippingCost *= 2.5m;

    totalAmount += shippingCost;

    // -- Phase 3: Payment processing --
    var paymentResult = _paymentGateway.Charge(customer.Id, totalAmount);
    if (!paymentResult.Success)
        return new OrderResult { Success = false, Error = $"Payment failed: {paymentResult.ErrorMessage}" };

    // -- Phase 4: Inventory reservation --
    foreach (var item in order.Items)
    {
        _inventory.ReserveStock(item.ProductId, item.Quantity);
    }

    order.Status = OrderStatus.PaymentProcessed;
    _processedCount++;

    // -- Phase 5: Audit logging --
    var logEntry = FormatAuditLogEntry(order, customer, totalAmount, paymentResult.TransactionId ?? "N/A");
    _auditLogger.WriteEntry(logEntry);

    // -- Phase 6: Customer notification --
    _notificationService.SendOrderConfirmation(customer.Email, order.Id, totalAmount);

    return new OrderResult
    {
        Success = true,
        OrderId = order.Id,
        TotalCharged = totalAmount,
        TransactionId = paymentResult.TransactionId
    };
}
```

### AFTER -- Validation extracted into `ValidateOrder`

```csharp
public OrderResult ProcessOrder(Order order, Customer customer)
{
    // -- Phase 1: Validation --
    var validationResult = ValidateOrder(order, customer);
    if (!validationResult.Success)
        return validationResult;

    // -- Phase 2: Pricing calculation --
    decimal subtotal = 0m;
    foreach (var item in order.Items)
    {
        subtotal += item.UnitPrice * item.Quantity;
    }

    // ... rest of ProcessOrder unchanged ...
}

private OrderResult ValidateOrder(Order order, Customer customer)
{
    if (order == null)
        return new OrderResult { Success = false, Error = "Order is null" };

    if (customer == null)
        return new OrderResult { Success = false, Error = "Customer is null" };

    if (order.Items == null || order.Items.Count == 0)
        return new OrderResult { Success = false, Error = "Order contains no items" };

    foreach (var item in order.Items)
    {
        if (item.Quantity <= 0)
            return new OrderResult { Success = false, Error = $"Invalid quantity for {item.ProductName}" };

        if (item.UnitPrice < 0)
            return new OrderResult { Success = false, Error = $"Negative price for {item.ProductName}" };

        var stock = _inventory.GetStockLevel(item.ProductId);
        if (stock < item.Quantity)
            return new OrderResult { Success = false, Error = $"Insufficient stock for {item.ProductName}: need {item.Quantity}, have {stock}" };
    }

    if (string.IsNullOrWhiteSpace(order.ShippingAddress.Street) ||
        string.IsNullOrWhiteSpace(order.ShippingAddress.City) ||
        string.IsNullOrWhiteSpace(order.ShippingAddress.ZipCode))
        return new OrderResult { Success = false, Error = "Incomplete shipping address" };

    return new OrderResult { Success = true };
}
```

### What changed -- a diff view

```diff
 public OrderResult ProcessOrder(Order order, Customer customer)
 {
-    // -- Phase 1: Validation (lines to extract into ValidateOrder) --
-    if (order == null)
-        return new OrderResult { Success = false, Error = "Order is null" };
-
-    if (customer == null)
-        return new OrderResult { Success = false, Error = "Customer is null" };
-
-    if (order.Items == null || order.Items.Count == 0)
-        return new OrderResult { Success = false, Error = "Order contains no items" };
-
-    foreach (var item in order.Items)
-    {
-        if (item.Quantity <= 0)
-            return new OrderResult { ... };
-        if (item.UnitPrice < 0)
-            return new OrderResult { ... };
-        var stock = _inventory.GetStockLevel(item.ProductId);
-        if (stock < item.Quantity)
-            return new OrderResult { ... };
-    }
-
-    if (string.IsNullOrWhiteSpace(order.ShippingAddress.Street) ||
-        string.IsNullOrWhiteSpace(order.ShippingAddress.City) ||
-        string.IsNullOrWhiteSpace(order.ShippingAddress.ZipCode))
-        return new OrderResult { Success = false, Error = "Incomplete shipping address" };
+    // -- Phase 1: Validation --
+    var validationResult = ValidateOrder(order, customer);
+    if (!validationResult.Success)
+        return validationResult;

     // -- Phase 2: Pricing calculation --
     decimal subtotal = 0m;
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-method '{
    "solutionPath": "Demos/ECommerce/ECommerce.sln",
    "filePath": "Demos/ECommerce/ECommerce/OrderProcessor.cs",
    "selectionRange": "47:9-68:100",
    "methodName": "ValidateOrder"
}'
```

| Parameter        | Value                                          | Purpose                                           |
|------------------|------------------------------------------------|---------------------------------------------------|
| `solutionPath`   | `Demos/ECommerce/ECommerce.sln`                | Solution context for Roslyn semantic analysis      |
| `filePath`       | `Demos/ECommerce/ECommerce/OrderProcessor.cs`  | File containing the method to refactor             |
| `selectionRange` | `47:9-68:100`                                  | Lines 47-68 -- the validation block inside `ProcessOrder` |
| `methodName`     | `ValidateOrder`                                | Name for the newly extracted method                |

### Why This Improves the Code

| Benefit                         | Explanation |
|---------------------------------|-------------|
| **Single Responsibility**       | `ProcessOrder` now delegates validation instead of implementing it. Each method does one thing. |
| **Readability**                 | A reader can scan `ProcessOrder` and see "validate, price, charge, reserve, log, notify" at a glance without wading through 25 lines of guard clauses. |
| **Testability**                 | `ValidateOrder` can be tested in isolation -- feed it bad inputs and assert the correct error without setting up payment gateways, notification services, or inventory mocks. |
| **Reusability**                 | Other entry points (batch processing, API endpoints) can call `ValidateOrder` directly without duplicating the checks. |
| **Reduced cognitive load**      | The main method drops from ~100 lines to ~75 lines. Each extracted method is small enough to fit on one screen. |

---

## Demo 2b: Inline Method -- Inline trivial `GetBaseMultiplier` in `PricingEngine`

### The Problem

`PricingEngine.GetBaseMultiplier` is a one-line method:

```csharp
public decimal GetBaseMultiplier(string category)
{
    return category == "Premium" ? 1.25m : 1.0m;
}
```

It is a trivial wrapper around a single ternary expression. The method name adds no
explanatory value beyond what the expression itself communicates, and the indirection
forces a reader to jump to a separate method definition just to understand one comparison.
It is the kind of "helper" that made sense during an early draft but now just adds
navigational overhead.

When a method is this simple -- a single expression with no branching complexity, no shared
state, and no reuse across multiple callers -- **inlining** it removes a layer of
indirection and keeps the logic visible at the point where it matters.

### BEFORE -- `GetBaseMultiplier` as a separate method

```csharp
public class PricingEngine
{
    // ... other methods ...

    /// <summary>
    /// Trivial wrapper -- inline-method candidate (called only from CalculateBulkDiscount conceptually).
    /// </summary>
    public decimal GetBaseMultiplier(string category)
    {
        return category == "Premium" ? 1.25m : 1.0m;
    }
}
```

A call site using this method would look like:

```csharp
decimal multiplier = GetBaseMultiplier(product.Category);
decimal adjustedPrice = basePrice * multiplier;
```

### AFTER -- Expression inlined, method removed

```csharp
public class PricingEngine
{
    // ... other methods ...

    // GetBaseMultiplier is gone -- its body has been inlined at every call site.
}
```

At the former call site, the expression now appears directly:

```csharp
decimal multiplier = product.Category == "Premium" ? 1.25m : 1.0m;
decimal adjustedPrice = basePrice * multiplier;
```

### What changed -- a diff view

At the call site:

```diff
-decimal multiplier = GetBaseMultiplier(product.Category);
+decimal multiplier = product.Category == "Premium" ? 1.25m : 1.0m;
 decimal adjustedPrice = basePrice * multiplier;
```

In `PricingEngine.cs`, the method definition is removed entirely:

```diff
-    /// <summary>
-    /// Trivial wrapper -- inline-method candidate (called only from CalculateBulkDiscount conceptually).
-    /// </summary>
-    public decimal GetBaseMultiplier(string category)
-    {
-        return category == "Premium" ? 1.25m : 1.0m;
-    }
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json inline-method '{
    "solutionPath": "Demos/ECommerce/ECommerce.sln",
    "filePath": "Demos/ECommerce/ECommerce/PricingEngine.cs",
    "methodName": "GetBaseMultiplier"
}'
```

| Parameter      | Value                                         | Purpose                                           |
|----------------|-----------------------------------------------|---------------------------------------------------|
| `solutionPath` | `Demos/ECommerce/ECommerce.sln`               | Solution context for Roslyn semantic analysis      |
| `filePath`     | `Demos/ECommerce/ECommerce/PricingEngine.cs`  | File containing the method to inline               |
| `methodName`   | `GetBaseMultiplier`                           | The method whose body will replace its call sites  |

### When Is Inlining Appropriate?

Inline Method is the right choice when:

| Criterion                        | `GetBaseMultiplier` | Guidance |
|----------------------------------|---------------------|----------|
| **Trivial body**                 | Yes -- single ternary expression | If the body is as easy to read as the method name, inline it. |
| **Single or very few call sites**| Yes -- called in one place       | Multiple call sites raise the risk of duplication; keep a method if it is called three or more times. |
| **No meaningful abstraction**    | Yes -- name adds no insight      | If the method name merely restates what the code does (`GetBaseMultiplier` = "return a multiplier"), the abstraction is not earning its keep. |
| **Not part of a public API**     | Context-dependent                | Inlining a public method changes the class's surface area. Prefer inlining for `private` or internal-only methods. |
| **No side effects**              | Yes -- pure expression           | Side-effecting methods are harder to reason about when inlined; keep them as named methods. |

**Do not inline** when:
- The method body is complex (multiple statements, loops, error handling).
- The method is called from many locations -- a named method avoids duplication.
- The method name communicates important domain intent that the raw expression does not.
- The method is virtual, abstract, or part of an interface contract.

---

## Summary -- Two Sides of the Same Coin

| Aspect               | Extract Method (2a)                             | Inline Method (2b)                              |
|-----------------------|-------------------------------------------------|-------------------------------------------------|
| **Direction**         | Code moves *out* of a long method               | Code moves *into* the call site                 |
| **When to use**       | Method is too long; a block has a clear purpose  | Wrapper is trivial; indirection adds no value    |
| **Net effect**        | More methods, each shorter and focused           | Fewer methods, less navigational overhead         |
| **Readability gain**  | High-level flow becomes scannable                | Inline expression is immediately visible          |
| **Risk if misapplied**| Over-extraction makes code hard to follow        | Over-inlining creates bloated methods             |

The key insight is that **method boundaries are a design decision, not a fixed property
of the code**. RefactorMCP gives you precise, Roslyn-powered tools to move those
boundaries in either direction -- safely, across an entire solution, with full semantic
awareness of types, overloads, and references.
