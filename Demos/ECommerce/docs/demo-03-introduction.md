# Demo 3: Introduce Variable, Field & Parameter

> **Goal:** Demonstrate three "introduce" refactorings that give names to anonymous
> expressions, pull magic numbers into fields, and promote hardcoded values to method
> parameters. Each one attacks a different kind of code smell -- unreadable expressions,
> duplicated literals, and rigid configuration -- using the same underlying principle:
> **make implicit knowledge explicit.**

---

## Demo 3a: Introduce Variable -- Name the complex pricing expression

### The Problem

`PricingEngine.CalculateLineTotal` compresses an entire pricing formula into a single
`return` statement spanning well over 200 characters:

```csharp
return item.UnitPrice * item.Quantity * (1 - (customer.Tier == CustomerTier.Platinum ? 0.15m : customer.Tier == CustomerTier.Gold ? 0.10m : customer.Tier == CustomerTier.Silver ? 0.05m : 0m)) * (isHolidaySeason ? 1.10m : 1.0m) * (item.Quantity >= 10 ? 0.95m : 1.0m);
```

A developer reading this line must mentally parse **three** nested ternary sub-expressions
to understand the business rules:

1. A **tier discount** based on customer membership level (Platinum 15%, Gold 10%, Silver 5%).
2. A **holiday season surcharge** multiplier (10% bump when `isHolidaySeason` is true).
3. A **bulk quantity discount** (5% off when ordering 10 or more units).

None of these concepts have names. A reviewer cannot skim the line and see "ah, this is
where the tier discount is applied" -- they have to read the entire ternary chain, character
by character, to figure out what each sub-expression means.

The first step toward clarity is to extract the tier discount ternary into a named local
variable. Once it has a name, the expression documents itself.

### BEFORE -- `PricingEngine.CalculateLineTotal`

The tier discount ternary that will be extracted is marked with `// <<<`.

```csharp
public class PricingEngine
{
    private readonly AppConfig _config;

    public PricingEngine(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Contains a complex inline expression that should be broken into named variables.
    /// </summary>
    public decimal CalculateLineTotal(OrderItem item, Customer customer, bool isHolidaySeason)
    {
        // This expression is hard to read -- introduce-variable candidates
        return item.UnitPrice * item.Quantity
            * (1 - (customer.Tier == CustomerTier.Platinum ? 0.15m          // <<<
                   : customer.Tier == CustomerTier.Gold ? 0.10m             // <<<
                   : customer.Tier == CustomerTier.Silver ? 0.05m           // <<<
                   : 0m))                                                   // <<<
            * (isHolidaySeason ? 1.10m : 1.0m)
            * (item.Quantity >= 10 ? 0.95m : 1.0m);
    }
}
```

The nested ternary `customer.Tier == CustomerTier.Platinum ? 0.15m : ... : 0m` resolves to
a decimal between `0` and `0.15`. But without a variable name there is no hint that this
represents a *discount percentage based on the customer's tier*.

### AFTER -- Tier discount extracted into a named variable

```csharp
public decimal CalculateLineTotal(OrderItem item, Customer customer, bool isHolidaySeason)
{
    var tierDiscount = customer.Tier == CustomerTier.Platinum ? 0.15m
                     : customer.Tier == CustomerTier.Gold ? 0.10m
                     : customer.Tier == CustomerTier.Silver ? 0.05m
                     : 0m;

    return item.UnitPrice * item.Quantity
        * (1 - tierDiscount)
        * (isHolidaySeason ? 1.10m : 1.0m)
        * (item.Quantity >= 10 ? 0.95m : 1.0m);
}
```

### What changed -- a diff view

```diff
 public decimal CalculateLineTotal(OrderItem item, Customer customer, bool isHolidaySeason)
 {
-    // This expression is hard to read -- introduce-variable candidates
-    return item.UnitPrice * item.Quantity * (1 - (customer.Tier == CustomerTier.Platinum ? 0.15m : customer.Tier == CustomerTier.Gold ? 0.10m : customer.Tier == CustomerTier.Silver ? 0.05m : 0m)) * (isHolidaySeason ? 1.10m : 1.0m) * (item.Quantity >= 10 ? 0.95m : 1.0m);
+    var tierDiscount = customer.Tier == CustomerTier.Platinum ? 0.15m
+                     : customer.Tier == CustomerTier.Gold ? 0.10m
+                     : customer.Tier == CustomerTier.Silver ? 0.05m
+                     : 0m;
+
+    return item.UnitPrice * item.Quantity
+        * (1 - tierDiscount)
+        * (isHolidaySeason ? 1.10m : 1.0m)
+        * (item.Quantity >= 10 ? 0.95m : 1.0m);
 }
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-variable '{
    "solutionPath": "Demos/ECommerce/ECommerce.sln",
    "filePath": "Demos/ECommerce/ECommerce/PricingEngine.cs",
    "selectionRange": "29:55-29:198",
    "variableName": "tierDiscount"
}'
```

| Parameter        | Value                                         | Purpose                                                       |
|------------------|-----------------------------------------------|---------------------------------------------------------------|
| `solutionPath`   | `Demos/ECommerce/ECommerce.sln`               | Solution context for Roslyn semantic analysis                  |
| `filePath`       | `Demos/ECommerce/ECommerce/PricingEngine.cs`  | File containing the expression to extract                      |
| `selectionRange` | `29:55-29:198`                                | The tier-discount ternary: `customer.Tier == ... : 0m`         |
| `variableName`   | `tierDiscount`                                | Name for the newly introduced local variable                   |

### Why Named Variables Make Code Self-Documenting

| Benefit                           | Explanation |
|-----------------------------------|-------------|
| **Instant comprehension**         | A reader sees `(1 - tierDiscount)` and immediately understands the business intent. The raw ternary chain required careful character-by-character parsing to reach the same understanding. |
| **Grep-friendly**                 | Searching the codebase for `tierDiscount` now surfaces every place this concept is computed or used. Searching for `0.15m` would return hundreds of unrelated numeric literals. |
| **Debugger-friendly**             | Setting a breakpoint after the variable assignment lets you inspect `tierDiscount` directly. With the one-liner, you would need to evaluate sub-expressions manually in the watch window. |
| **Stepping stone for further refactoring** | Once the variable exists, it becomes easy to extract the other two sub-expressions (`holidayMultiplier`, `bulkDiscount`) into named variables too, or to extract the entire pricing formula into its own method. |
| **Zero runtime cost**             | The JIT compiler will optimize the local variable away. This is purely a source-level clarity improvement with no performance impact. |

---

## Demo 3b: Introduce Parameter -- Extract hardcoded tax rate

### The Problem

`OrderProcessor.ProcessOrder` contains a hardcoded tax rate buried in the middle of a
100-line method:

```csharp
// Tax calculation -- hardcoded rate should be a parameter
decimal taxAmount = subtotal * 0.08m;
decimal totalAmount = subtotal + taxAmount;
```

The literal `0.08m` (representing an 8% tax rate) is invisible to callers. Every invocation
of `ProcessOrder` silently applies the same rate regardless of the customer's jurisdiction,
the type of goods being sold, or the current tax law. The value cannot be changed without
editing the method body, and unit tests cannot exercise different tax scenarios without
modifying production code.

**Introduce Parameter** promotes the hardcoded literal to a method parameter, making the
dependency explicit and the method configurable from the outside.

### BEFORE -- `OrderProcessor.ProcessOrder` (pricing section)

```csharp
public OrderResult ProcessOrder(Order order, Customer customer)
{
    // ... validation ...

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

    // Tax calculation -- hardcoded rate should be a parameter
    decimal taxAmount = subtotal * 0.08m;                         // <<<
    decimal totalAmount = subtotal + taxAmount;

    // ... payment, inventory, audit, notification ...
}
```

### AFTER -- Tax rate promoted to a parameter

```csharp
public OrderResult ProcessOrder(Order order, Customer customer, object taxRate)
{
    // ... validation ...

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
    decimal taxAmount = subtotal * taxRate;
    decimal totalAmount = subtotal + taxAmount;

    // ... payment, inventory, audit, notification ...
}
```

### What changed -- a diff view

```diff
-public OrderResult ProcessOrder(Order order, Customer customer)
+public OrderResult ProcessOrder(Order order, Customer customer, object taxRate)
 {
     // ... validation ...

     // ... pricing ...

-    // Tax calculation -- hardcoded rate should be a parameter
-    decimal taxAmount = subtotal * 0.08m;
+    // Tax calculation
+    decimal taxAmount = subtotal * taxRate;
     decimal totalAmount = subtotal + taxAmount;

     // ... rest unchanged ...
 }
```

The literal `0.08m` disappears from the method body entirely. Every call site must now
provide the tax rate explicitly, making the dependency visible and configurable.

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-parameter '{
    "solutionPath": "Demos/ECommerce/ECommerce.sln",
    "filePath": "Demos/ECommerce/ECommerce/OrderProcessor.cs",
    "methodName": "ProcessOrder",
    "selectionRange": "100:40-100:45",
    "parameterName": "taxRate"
}'
```

| Parameter        | Value                                          | Purpose                                                     |
|------------------|------------------------------------------------|-------------------------------------------------------------|
| `solutionPath`   | `Demos/ECommerce/ECommerce.sln`                | Solution context for Roslyn semantic analysis                |
| `filePath`       | `Demos/ECommerce/ECommerce/OrderProcessor.cs`  | File containing the hardcoded literal                        |
| `methodName`     | `ProcessOrder`                                 | Method to which the new parameter will be added              |
| `selectionRange` | `100:40-100:45`                                | The literal `0.08m` on line 100                              |
| `parameterName`  | `taxRate`                                      | Name for the newly introduced parameter                      |

### Why This Improves the Code

| Benefit                            | Explanation |
|------------------------------------|-------------|
| **Configurability**                | Different jurisdictions charge different tax rates. With a parameter, the caller can pass `0.06m` for one state and `0.10m` for another without touching `ProcessOrder`. |
| **Testability**                    | Unit tests can exercise edge cases: zero tax, maximum tax, fractional rates. Before, every test implicitly used 8% with no way to vary it. |
| **Explicitness**                   | The method signature now *advertises* that tax rate is a required input. A developer reading `ProcessOrder(order, customer, taxRate)` immediately knows the method needs a tax rate -- no need to scan 100 lines of implementation to discover the hidden `0.08m`. |
| **Separation of concerns**         | The decision of *which* tax rate to apply belongs to the caller (or a configuration service), not to the order processing logic. The method becomes a pure computation over its inputs. |
| **Audit trail**                    | When the tax rate is a parameter, logging and debugging can capture the exact rate used for each order. A hardcoded literal leaves no trace in call-site telemetry. |

---

## Demo 3c: Introduce Field -- Extract magic number for max discount cap

### The Problem

`PricingEngine.CalculateBulkDiscount` caps the combined discount at 25% using a magic
number buried inside a `Math.Min` call:

```csharp
decimal totalDiscount = Math.Min(volumeDiscount + loyaltyBonus, 0.25m);
return Math.Round(rawTotal * (1 - totalDiscount), 2);
```

The literal `0.25m` has no name. A reader must infer from context that this is a maximum
discount cap. If the business later decides to change the cap to 30%, a developer must
search for `0.25m` across the codebase -- and hope they do not accidentally modify an
unrelated occurrence of the same number.

**Introduce Field** extracts the literal into a named class-level field, creating a single
source of truth for the value and giving it a descriptive name.

### BEFORE -- `PricingEngine.CalculateBulkDiscount` (Phase 3)

```csharp
public class PricingEngine
{
    private readonly AppConfig _config;

    public PricingEngine(AppConfig config)
    {
        _config = config;
    }

    // ... other methods ...

    public decimal CalculateBulkDiscount(List<OrderItem> items, Customer customer)
    {
        // Phase 1: Calculate raw totals
        decimal rawTotal = 0m;
        int totalUnits = 0;
        foreach (var item in items)
        {
            rawTotal += item.UnitPrice * item.Quantity;
            totalUnits += item.Quantity;
        }

        // Phase 2: Determine volume discount tier
        decimal volumeDiscount;
        if (totalUnits >= 100)
            volumeDiscount = 0.20m;
        else if (totalUnits >= 50)
            volumeDiscount = 0.15m;
        else if (totalUnits >= 25)
            volumeDiscount = 0.10m;
        else if (totalUnits >= 10)
            volumeDiscount = 0.05m;
        else
            volumeDiscount = 0m;

        // Phase 3: Apply loyalty bonus on top of volume discount
        decimal loyaltyBonus = 0m;
        if (customer.Tier >= CustomerTier.Gold && totalUnits >= 25)
            loyaltyBonus = 0.03m;
        if (customer.Tier >= CustomerTier.Platinum && totalUnits >= 50)
            loyaltyBonus = 0.05m;

        decimal totalDiscount = Math.Min(volumeDiscount + loyaltyBonus, 0.25m);  // <<<
        return Math.Round(rawTotal * (1 - totalDiscount), 2);
    }
}
```

The `0.25m` is the cap, but nothing in the code says so. It could be mistaken for a
discount tier, a tax rate, or any other decimal constant.

### AFTER -- Magic number extracted to a named field

```csharp
public class PricingEngine
{
    private readonly AppConfig _config;
    private decimal _maxDiscountCap = 0.25m;

    public PricingEngine(AppConfig config)
    {
        _config = config;
    }

    // ... other methods ...

    public decimal CalculateBulkDiscount(List<OrderItem> items, Customer customer)
    {
        // Phase 1: Calculate raw totals
        decimal rawTotal = 0m;
        int totalUnits = 0;
        foreach (var item in items)
        {
            rawTotal += item.UnitPrice * item.Quantity;
            totalUnits += item.Quantity;
        }

        // Phase 2: Determine volume discount tier
        decimal volumeDiscount;
        if (totalUnits >= 100)
            volumeDiscount = 0.20m;
        else if (totalUnits >= 50)
            volumeDiscount = 0.15m;
        else if (totalUnits >= 25)
            volumeDiscount = 0.10m;
        else if (totalUnits >= 10)
            volumeDiscount = 0.05m;
        else
            volumeDiscount = 0m;

        // Phase 3: Apply loyalty bonus on top of volume discount
        decimal loyaltyBonus = 0m;
        if (customer.Tier >= CustomerTier.Gold && totalUnits >= 25)
            loyaltyBonus = 0.03m;
        if (customer.Tier >= CustomerTier.Platinum && totalUnits >= 50)
            loyaltyBonus = 0.05m;

        decimal totalDiscount = Math.Min(volumeDiscount + loyaltyBonus, _maxDiscountCap);
        return Math.Round(rawTotal * (1 - totalDiscount), 2);
    }
}
```

### What changed -- a diff view

```diff
 public class PricingEngine
 {
     private readonly AppConfig _config;
+    private decimal _maxDiscountCap = 0.25m;

     public PricingEngine(AppConfig config)
     {
         _config = config;
     }

     // ... other methods ...

     public decimal CalculateBulkDiscount(List<OrderItem> items, Customer customer)
     {
         // ... phases 1 and 2 unchanged ...

-        decimal totalDiscount = Math.Min(volumeDiscount + loyaltyBonus, 0.25m);
+        decimal totalDiscount = Math.Min(volumeDiscount + loyaltyBonus, _maxDiscountCap);
         return Math.Round(rawTotal * (1 - totalDiscount), 2);
     }
 }
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-field '{
    "solutionPath": "Demos/ECommerce/ECommerce.sln",
    "filePath": "Demos/ECommerce/ECommerce/PricingEngine.cs",
    "selectionRange": "121:73-121:78",
    "fieldName": "_maxDiscountCap",
    "accessModifier": "private"
}'
```

| Parameter         | Value                                         | Purpose                                                    |
|-------------------|-----------------------------------------------|------------------------------------------------------------|
| `solutionPath`    | `Demos/ECommerce/ECommerce.sln`               | Solution context for Roslyn semantic analysis               |
| `filePath`        | `Demos/ECommerce/ECommerce/PricingEngine.cs`  | File containing the magic number                            |
| `selectionRange`  | `121:73-121:78`                               | The literal `0.25m` inside `Math.Min(..., 0.25m)`           |
| `fieldName`       | `_maxDiscountCap`                             | Name for the newly introduced field                         |
| `accessModifier`  | `private`                                     | Visibility of the new field                                 |

### Why This Improves the Code

| Benefit                            | Explanation |
|------------------------------------|-------------|
| **Single source of truth**         | If the business changes the maximum discount cap from 25% to 30%, there is exactly one place to update: the `_maxDiscountCap` field declaration. No need to search-and-replace numeric literals. |
| **Self-documenting**               | The name `_maxDiscountCap` communicates the *purpose* of the value. A bare `0.25m` communicates nothing -- it could be a tax rate, a commission percentage, or any other quarter-value. |
| **Discoverability**                | New team members browsing the class fields immediately see the configurable business constants. The field listing becomes a summary of the class's tuning knobs. |
| **Testability**                    | With the value in a field, a test can use reflection or a subclass to override `_maxDiscountCap` for edge-case testing (e.g., what happens when the cap is 0%? 100%?). A hardcoded literal offers no such flexibility. |
| **Stepping stone to configuration** | Once the magic number is a field, the next natural step is to inject it through the constructor or load it from `AppConfig`. The field serves as a clean intermediate state on that path. |

---

## Summary -- Three Flavors of "Give It a Name"

| Aspect               | Introduce Variable (3a)                        | Introduce Parameter (3b)                        | Introduce Field (3c)                            |
|-----------------------|------------------------------------------------|-------------------------------------------------|-------------------------------------------------|
| **Scope**             | Local to the method                            | Exposed on the method signature                  | Class-level member                               |
| **What it names**     | A complex sub-expression                       | A hardcoded literal the caller should control    | A magic number used by the class                 |
| **Primary benefit**   | Readability and debuggability                  | Configurability and testability                  | Single source of truth for constants             |
| **Typical smell**     | Long, unreadable expressions                   | Hidden dependencies on fixed values              | Repeated or unexplained numeric literals         |
| **Caller impact**     | None -- purely internal refactoring            | Breaking change -- callers must pass the value   | None -- internal extraction                      |
| **Next step**         | Extract more variables, then extract method    | Add a default value or configuration lookup      | Make the field `readonly`, inject via constructor |

The three refactorings share a common philosophy: **unnamed things are hard to understand,
hard to change, and hard to test.** Whether the unnamed thing is a nested ternary expression,
a hardcoded tax rate, or a magic discount cap, the fix is the same -- give it a name that
communicates its purpose, and place that name at the right scope for how it needs to be used.
