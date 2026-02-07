# Demo 1: Analysis & Code Metrics

> **RefactorMCP** -- Roslyn-powered refactoring tools exposed via the Model Context Protocol.
> This demo showcases the **analysis tools** that scan your C# codebase and surface
> actionable refactoring opportunities without modifying any code.

---

## What the Analysis Tools Do

RefactorMCP includes two complementary analysis tools:

| Tool | Purpose |
|------|---------|
| **`analyze-refactoring-opportunities`** | Deep-scans a single file and reports code smells: long methods, unused members, poorly named variables, complex expressions, and more. |
| **`list-class-lengths`** | Measures every class in the loaded solution and ranks them by line count -- a quick way to spot "god classes" that have grown too large. |

Both tools are **read-only** -- they inspect your code using the Roslyn semantic model and
produce structured JSON reports. Nothing is changed on disk.

---

## Demo 1a: Analyze Refactoring Opportunities -- `OrderProcessor.cs`

`OrderProcessor` is a classic "god class." It handles validation, pricing, payment,
inventory, audit logging, and customer notification all in a single 160+ line file.
Let's see what the analyzer finds.

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json analyze-refactoring-opportunities \
  '{"filePath":"Demos/ECommerce/ECommerce/OrderProcessor.cs"}'
```

### Expected Output

```json
{
  "filePath": "Demos/ECommerce/ECommerce/OrderProcessor.cs",
  "analysisTimestamp": "2026-02-06T12:00:00Z",
  "summary": {
    "totalIssues": 5,
    "bySeverity": { "high": 2, "medium": 2, "low": 1 }
  },
  "opportunities": [
    {
      "type": "long-method",
      "severity": "high",
      "location": {
        "className": "OrderProcessor",
        "memberName": "ProcessOrder",
        "startLine": 41,
        "endLine": 148
      },
      "description": "Method 'ProcessOrder' is 107 lines long (threshold: 30). It contains 6 distinct phases: validation, pricing, payment, inventory reservation, audit logging, and notification.",
      "suggestedRefactorings": [
        "extract-method: Extract validation logic (lines 44-69) into ValidateOrder",
        "extract-method: Extract pricing calculation (lines 72-117) into CalculateTotal",
        "extract-method: Extract shipping cost logic (lines 104-117) into CalculateShippingCost"
      ]
    },
    {
      "type": "unused-field",
      "severity": "medium",
      "location": {
        "className": "OrderProcessor",
        "memberName": "_migrationTimestamp",
        "startLine": 23,
        "endLine": 23
      },
      "description": "Field '_migrationTimestamp' of type 'DateTime' is declared but never read or written anywhere in the class.",
      "suggestedRefactorings": [
        "safe-delete-field: Remove '_migrationTimestamp' from OrderProcessor"
      ]
    },
    {
      "type": "unused-method",
      "severity": "medium",
      "location": {
        "className": "OrderProcessor",
        "memberName": "LegacyExportXml",
        "startLine": 173,
        "endLine": 176
      },
      "description": "Method 'LegacyExportXml' is declared public but has no callers anywhere in the solution.",
      "suggestedRefactorings": [
        "safe-delete-method: Remove 'LegacyExportXml' from OrderProcessor"
      ]
    },
    {
      "type": "poorly-named-variable",
      "severity": "high",
      "location": {
        "className": "OrderProcessor",
        "memberName": "ProcessOrder",
        "startLine": 79,
        "endLine": 79
      },
      "description": "Local variable 'x' is a single-character name assigned from a customer tier discount switch expression. The name does not convey its purpose.",
      "suggestedRefactorings": [
        "rename-symbol: Rename 'x' to 'tierDiscountRate' or 'discountMultiplier'"
      ]
    },
    {
      "type": "non-readonly-field",
      "severity": "low",
      "location": {
        "className": "OrderProcessor",
        "memberName": "_paymentGateway",
        "startLine": 19,
        "endLine": 19
      },
      "description": "Field '_paymentGateway' is assigned only in the constructor but is not marked readonly. Same applies to '_auditLogger' (line 20).",
      "suggestedRefactorings": [
        "make-field-readonly: Mark '_paymentGateway' as readonly",
        "make-field-readonly: Mark '_auditLogger' as readonly"
      ]
    }
  ]
}
```

### What This Tells Us

> **Key Takeaway:** `ProcessOrder` is a monolithic 107-line method with six clearly
> separable phases. The analyzer identifies it as the highest-priority target. The unused
> field `_migrationTimestamp` and dead method `LegacyExportXml` are safe to remove
> immediately. The variable `x` on line 79 should be renamed to something descriptive
> like `tierDiscountRate`.

| Finding | Severity | Suggested Action |
|---------|----------|-----------------|
| `ProcessOrder` is 107 lines | **High** | `extract-method` -- split into validation, pricing, payment phases |
| `_migrationTimestamp` is unused | Medium | `safe-delete-field` -- remove the dead field |
| `LegacyExportXml` has no callers | Medium | `safe-delete-method` -- remove the dead method |
| Variable `x` on line 79 | **High** | `rename-symbol` -- rename to `tierDiscountRate` |
| `_paymentGateway` not readonly | Low | `make-field-readonly` -- add `readonly` modifier |

---

## Demo 1b: List Class Lengths Across the Solution

Before diving into file-level analysis, it helps to get a bird's-eye view of every class
in the solution ranked by size. Large classes are often the ones most in need of refactoring.

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json list-class-lengths \
  '{"solutionPath":"Demos/ECommerce/ECommerce.sln"}'
```

### Expected Output

```json
{
  "solutionPath": "Demos/ECommerce/ECommerce.sln",
  "totalClasses": 18,
  "classes": [
    { "className": "OrderProcessor",       "filePath": "ECommerce/OrderProcessor.cs",       "lineCount": 161, "methodCount": 3 },
    { "className": "ReportGenerator",       "filePath": "ECommerce/ReportGenerator.cs",      "lineCount": 125, "methodCount": 3 },
    { "className": "PricingEngine",         "filePath": "ECommerce/PricingEngine.cs",        "lineCount": 119, "methodCount": 5 },
    { "className": "CustomerService",       "filePath": "ECommerce/CustomerService.cs",      "lineCount":  76, "methodCount": 5 },
    { "className": "InventoryManager",      "filePath": "ECommerce/InventoryManager.cs",     "lineCount":  75, "methodCount": 6 },
    { "className": "PaymentGateway",        "filePath": "ECommerce/PaymentGateway.cs",       "lineCount":  74, "methodCount": 4 },
    { "className": "NotificationService",   "filePath": "ECommerce/NotificationService.cs",  "lineCount":  66, "methodCount": 6 },
    { "className": "CustomerRepository",    "filePath": "ECommerce/CustomerService.cs",      "lineCount":  19, "methodCount": 3 },
    { "className": "NotificationTemplate",  "filePath": "ECommerce/NotificationService.cs",  "lineCount":  17, "methodCount": 1 },
    { "className": "AuditLogger",           "filePath": "ECommerce/AuditLogger.cs",          "lineCount":  14, "methodCount": 3 },
    { "className": "EmailService",          "filePath": "ECommerce/CustomerService.cs",      "lineCount":  12, "methodCount": 2 },
    { "className": "Invoice",               "filePath": "ECommerce/Models.cs",               "lineCount":  11, "methodCount": 0 },
    { "className": "Order",                 "filePath": "ECommerce/Models.cs",               "lineCount":  10, "methodCount": 0 },
    { "className": "Customer",              "filePath": "ECommerce/Models.cs",               "lineCount":  10, "methodCount": 0 },
    { "className": "Product",               "filePath": "ECommerce/Models.cs",               "lineCount":  10, "methodCount": 0 },
    { "className": "OrderResult",           "filePath": "ECommerce/OrderProcessor.cs",       "lineCount":   8, "methodCount": 0 },
    { "className": "StockSnapshot",         "filePath": "ECommerce/InventoryManager.cs",     "lineCount":   8, "methodCount": 0 },
    { "className": "PaymentTransaction",    "filePath": "ECommerce/PaymentGateway.cs",       "lineCount":   8, "methodCount": 0 }
  ]
}
```

### What This Tells Us

> **Key Takeaway:** Three classes stand out as significantly larger than the rest --
> `OrderProcessor` (161 lines), `ReportGenerator` (125 lines), and `PricingEngine`
> (119 lines). These are the primary candidates for refactoring. The remaining classes
> are reasonably sized.

```
  OrderProcessor     ████████████████████████████████  161 lines
  ReportGenerator    █████████████████████████         125 lines
  PricingEngine      ███████████████████████▊          119 lines
  CustomerService    ███████████████▏                   76 lines
  InventoryManager   ██████████████▊                    75 lines
  PaymentGateway     ██████████████▋                    74 lines
  NotificationService █████████████▏                    66 lines
  ─────────────────────────────────────────────────────
  (remaining 11 classes are under 20 lines each)
```

The top three classes together account for over **400 lines** -- more than half the
application logic. They are the best place to start refactoring.

---

## Demo 1c: Analyze Refactoring Opportunities -- `PricingEngine.cs`

`PricingEngine` contains a different set of code smells: deeply nested ternary
expressions, a pure function that should be static, and feature-flag branching
that complicates the control flow.

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json analyze-refactoring-opportunities \
  '{"filePath":"Demos/ECommerce/ECommerce/PricingEngine.cs"}'
```

### Expected Output

```json
{
  "filePath": "Demos/ECommerce/ECommerce/PricingEngine.cs",
  "analysisTimestamp": "2026-02-06T12:00:00Z",
  "summary": {
    "totalIssues": 4,
    "bySeverity": { "high": 2, "medium": 1, "low": 1 }
  },
  "opportunities": [
    {
      "type": "complex-expression",
      "severity": "high",
      "location": {
        "className": "PricingEngine",
        "memberName": "CalculateLineTotal",
        "startLine": 29,
        "endLine": 29
      },
      "description": "Single return statement contains a deeply nested expression with 5 multiplied terms including chained ternary operators. The expression spans 180+ characters and combines tier discount, seasonal multiplier, and bulk discount in one line.",
      "suggestedRefactorings": [
        "introduce-variable: Extract 'customer.Tier == CustomerTier.Platinum ? 0.15m : ...' into 'tierDiscount'",
        "introduce-variable: Extract 'isHolidaySeason ? 1.10m : 1.0m' into 'seasonalMultiplier'",
        "introduce-variable: Extract 'item.Quantity >= 10 ? 0.95m : 1.0m' into 'bulkMultiplier'"
      ]
    },
    {
      "type": "can-be-static",
      "severity": "medium",
      "location": {
        "className": "PricingEngine",
        "memberName": "CalculateShippingCost",
        "startLine": 35,
        "endLine": 59
      },
      "description": "Method 'CalculateShippingCost' does not access any instance fields or properties. It is a pure function that operates only on its parameters and can be safely converted to a static method.",
      "suggestedRefactorings": [
        "convert-to-static-with-parameters: Make 'CalculateShippingCost' static"
      ]
    },
    {
      "type": "feature-flag-branching",
      "severity": "high",
      "location": {
        "className": "PricingEngine",
        "memberName": "ApplyDynamicPricing",
        "startLine": 64,
        "endLine": 85
      },
      "description": "Method 'ApplyDynamicPricing' branches on feature flag '_config.EnableNewPricingEngine', splitting entirely into two unrelated pricing strategies. Both branches contain complete, independent logic with no shared code paths.",
      "suggestedRefactorings": [
        "extract-method: Extract the new-pricing branch (lines 68-71) into 'ApplyDemandBasedPricing'",
        "extract-method: Extract the old-pricing branch (lines 76-83) into 'ApplyCategoryMarkup'",
        "Consider extracting an IPricingStrategy interface with separate implementations"
      ]
    },
    {
      "type": "trivial-method",
      "severity": "low",
      "location": {
        "className": "PricingEngine",
        "memberName": "GetBaseMultiplier",
        "startLine": 128,
        "endLine": 131
      },
      "description": "Method 'GetBaseMultiplier' is a trivial one-line wrapper with a single ternary expression. It is called conceptually from one location only.",
      "suggestedRefactorings": [
        "inline-method: Inline 'GetBaseMultiplier' at its call site"
      ]
    }
  ]
}
```

### What This Tells Us

> **Key Takeaway:** `PricingEngine` suffers from a different pattern than `OrderProcessor`.
> Rather than one massive method, it has concentrated complexity: a single 180-character
> expression in `CalculateLineTotal` and a feature-flag fork in `ApplyDynamicPricing`
> that doubles the maintenance burden. The pure function `CalculateShippingCost` is a
> quick win -- making it static improves testability immediately.

| Finding | Severity | Suggested Action |
|---------|----------|-----------------|
| Complex chained ternary in `CalculateLineTotal` (line 29) | **High** | `introduce-variable` -- break into `tierDiscount`, `seasonalMultiplier`, `bulkMultiplier` |
| `CalculateShippingCost` uses no instance state | Medium | `convert-to-static-with-parameters` -- mark as `static` |
| Feature flag fork in `ApplyDynamicPricing` | **High** | `extract-method` or strategy pattern -- separate the two pricing paths |
| `GetBaseMultiplier` is trivial one-liner | Low | `inline-method` -- inline at call site |

---

## Summary: Prioritized Refactoring Roadmap

Based on the three analyses above, here is a prioritized order of operations:

```
Priority  File                  Finding                           Tool to Apply
--------  --------------------  --------------------------------  ----------------------------
  1       OrderProcessor.cs     ProcessOrder is 107 lines         extract-method
  2       PricingEngine.cs      Complex expression in             introduce-variable
                                CalculateLineTotal
  3       PricingEngine.cs      Feature flag branching in         extract-method
                                ApplyDynamicPricing
  4       OrderProcessor.cs     Variable 'x' is poorly named     rename-symbol
  5       OrderProcessor.cs     _migrationTimestamp is unused     safe-delete-field
  6       OrderProcessor.cs     LegacyExportXml has no callers   safe-delete-method
  7       PricingEngine.cs      CalculateShippingCost can be      convert-to-static-with-parameters
                                static
  8       PricingEngine.cs      GetBaseMultiplier is trivial      inline-method
  9       OrderProcessor.cs     _paymentGateway not readonly      make-field-readonly
```

> The next demos in this series will walk through **applying** these refactorings
> step by step using RefactorMCP's transformation tools.

---

*Generated for the RefactorMCP ECommerce demo suite.*
