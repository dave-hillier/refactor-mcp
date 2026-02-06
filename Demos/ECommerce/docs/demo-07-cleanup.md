# Demo 7: Safe Deletion, Rename & Cleanup

> **Theme:** Eliminate dead code, fix poor names, and remove unused imports -- all with
> compiler-verified safety guarantees that manual edits cannot provide.

Every codebase accumulates cruft: methods nobody calls, parameters nobody reads, fields
that lost their purpose three sprints ago, and variable names chosen during a midnight
debugging session. Manual deletion is risky because a simple text search can miss dynamic
dispatch, reflection, or cross-project references. RefactorMCP's safe-delete family uses
Roslyn's full semantic model to verify **zero references** before touching a single line.

---

## Source Files

| File | Key Smells Targeted |
|---|---|
| `OrderProcessor.cs` | Dead method, unused field, cryptic variable name |
| `CustomerService.cs` | Unused parameter on a public method |
| `ReportGenerator.cs` | Unused local variable |
| `InventoryManager.cs` | Single-letter parameter name |
| `NotificationService.cs` | Unused `using` directive |

---

## Demo 7a: Safe Delete Method -- Remove `LegacyExportXml`

**Tool:** `safe-delete-method`

`OrderProcessor` carries a leftover XML export method from a retired integration.
Nobody calls it, but nobody dares delete it "just in case." The safe-delete tool proves
it has zero callers across the entire solution before removing it.

### BEFORE (`OrderProcessor.cs`)

```csharp
/// <summary>
/// Legacy method that is never called anywhere — safe-delete candidate.
/// </summary>
public string LegacyExportXml(Order order)
{
    return $"<order><id>{order.Id}</id><status>{order.Status}</status></order>";
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-method '{
  "filePath": "Demos/ECommerce/ECommerce/OrderProcessor.cs",
  "methodName": "LegacyExportXml"
}'
```

### AFTER (`OrderProcessor.cs`)

```csharp
// The entire LegacyExportXml method and its XML-doc comment are removed.
// No other file is modified because no call sites existed.
```

The `FormatAuditLogEntry` and `ProcessOrder` methods remain untouched. Only the dead
method disappears.

### Why This Is Safe

The tool performs a **solution-wide reference search** using Roslyn's `FindReferencesAsync`.
If any project, test, or script references `LegacyExportXml` -- even via a delegate or
reflection attribute -- the tool will **refuse to delete** and report every call site.
This is fundamentally more reliable than `grep` or `Ctrl+Shift+F`, which miss indirect
references and produce false positives on comments.

---

## Demo 7b: Safe Delete Field -- Remove `_migrationTimestamp`

**Tool:** `safe-delete-field`

`OrderProcessor` declares a `_migrationTimestamp` field that is never read or written
anywhere in the class body. It was likely added for a data migration that has long since
completed.

### BEFORE (`OrderProcessor.cs`)

```csharp
public class OrderProcessor
{
    private PaymentGateway _paymentGateway;
    private AuditLogger _auditLogger;
    private readonly NotificationService _notificationService;
    private readonly InventoryManager _inventory;
    private DateTime _migrationTimestamp;   // <-- never used
    private int _processedCount;
    ...
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-field '{
  "filePath": "Demos/ECommerce/ECommerce/OrderProcessor.cs",
  "fieldName": "_migrationTimestamp"
}'
```

### AFTER (`OrderProcessor.cs`)

```csharp
public class OrderProcessor
{
    private PaymentGateway _paymentGateway;
    private AuditLogger _auditLogger;
    private readonly NotificationService _notificationService;
    private readonly InventoryManager _inventory;
    private int _processedCount;
    ...
}
```

The `_migrationTimestamp` declaration line is gone. No constructor assignments or method
references needed updating because none existed -- the tool confirmed this before making
the deletion.

---

## Demo 7c: Safe Delete Parameter -- Remove Unused `verbose` Parameter

**Tool:** `safe-delete-parameter`

`CustomerService.GetCustomerSummary` accepts a `bool verbose` parameter, but the method
body never reads it. Every call site passes a value that is silently ignored -- a source
of confusion for future maintainers.

### BEFORE (`CustomerService.cs`)

```csharp
/// <summary>
/// The 'verbose' parameter is never actually used — safe-delete-parameter candidate.
/// </summary>
public string GetCustomerSummary(Customer customer, bool verbose)
{
    return $"{customer.Name} | Tier: {customer.Tier} | Lifetime: {customer.LifetimeSpend:C} | Member since: {customer.MemberSince:yyyy-MM-dd}";
}
```

A typical call site might look like:

```csharp
var summary = customerService.GetCustomerSummary(customer, true);
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-parameter '{
  "filePath": "Demos/ECommerce/ECommerce/CustomerService.cs",
  "methodName": "GetCustomerSummary",
  "parameterName": "verbose"
}'
```

### AFTER (`CustomerService.cs`)

```csharp
public string GetCustomerSummary(Customer customer)
{
    return $"{customer.Name} | Tier: {customer.Tier} | Lifetime: {customer.LifetimeSpend:C} | Member since: {customer.MemberSince:yyyy-MM-dd}";
}
```

Every call site is updated in the same operation:

```csharp
// BEFORE
var summary = customerService.GetCustomerSummary(customer, true);

// AFTER
var summary = customerService.GetCustomerSummary(customer);
```

### Why This Is Safe

The tool verifies that `verbose` is never read inside the method body -- not in `if`
statements, not passed to other methods, not captured in a lambda. It also updates **all**
call sites across the solution to drop the corresponding argument, so the build stays
green.

---

## Demo 7d: Safe Delete Variable -- Remove Unused `separator` Local

**Tool:** `safe-delete-variable`

`ReportGenerator.GenerateSalesReport` declares a local string `separator` that was likely
intended for formatting but is never referenced after its initialization.

### BEFORE (`ReportGenerator.cs`)

```csharp
public string GenerateSalesReport(List<Order> orders)
{
    var sb = new StringBuilder();
    string separator = "============================"; // unused variable
    var totalRevenue = 0m;
    var orderCount = orders.Count;

    sb.AppendLine("SALES REPORT");
    // ... rest of method uses sb, totalRevenue, orderCount — but never separator
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-variable '{
  "filePath": "Demos/ECommerce/ECommerce/ReportGenerator.cs",
  "methodName": "GenerateSalesReport",
  "variableName": "separator"
}'
```

### AFTER (`ReportGenerator.cs`)

```csharp
public string GenerateSalesReport(List<Order> orders)
{
    var sb = new StringBuilder();
    var totalRevenue = 0m;
    var orderCount = orders.Count;

    sb.AppendLine("SALES REPORT");
    // ... rest of method unchanged
}
```

The declaration `string separator = "============================";` is removed entirely.
The surrounding code and its indentation remain untouched.

### Why This Is Safe

The tool uses Roslyn's data-flow analysis to confirm that `separator` is assigned but
never subsequently read. If any line -- even a commented-out debug statement that was
later uncommented -- referenced `separator`, the tool would refuse the deletion.

---

## Demo 7e: Rename Symbol -- `x` to `tierDiscountRate` in `OrderProcessor`

**Tool:** `rename-symbol`

Deep inside `ProcessOrder`, the discount multiplier is stored in a variable named `x`.
This is an opaque name that forces every reader to re-derive its meaning from context.

### BEFORE (`OrderProcessor.cs`, line 79)

```csharp
// Apply customer tier discount
decimal x = customer.Tier switch
{
    CustomerTier.Silver => 0.05m,
    CustomerTier.Gold => 0.10m,
    CustomerTier.Platinum => 0.15m,
    _ => 0m
};
subtotal *= (1 - x);
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json rename-symbol '{
  "filePath": "Demos/ECommerce/ECommerce/OrderProcessor.cs",
  "line": 79,
  "column": 16,
  "newName": "tierDiscountRate"
}'
```

The `line` and `column` parameters pinpoint the declaration of `x` on line 79, column 16,
so Roslyn renames exactly the right symbol even if other variables named `x` exist
elsewhere in the solution.

### AFTER (`OrderProcessor.cs`)

```csharp
// Apply customer tier discount
decimal tierDiscountRate = customer.Tier switch
{
    CustomerTier.Silver => 0.05m,
    CustomerTier.Gold => 0.10m,
    CustomerTier.Platinum => 0.15m,
    _ => 0m
};
subtotal *= (1 - tierDiscountRate);
```

Every reference to `x` within its scope is updated to `tierDiscountRate`. The semantic
rename guarantees that an unrelated `x` in another method or class is left alone.

---

## Demo 7f: Rename Symbol -- `q` to `quantity` in `InventoryManager.ReserveStock`

**Tool:** `rename-symbol`

The `ReserveStock` method uses `q` as a parameter name for the quantity to reserve.
Single-letter parameter names are acceptable in tight mathematical expressions, but
here `q` obscures the domain meaning across a 20-line method body.

### BEFORE (`InventoryManager.cs`)

```csharp
public bool ReserveStock(string productId, int q)
{
    if (!_stockLevels.ContainsKey(productId))
        return false;

    var available = _stockLevels[productId] - _reservations.GetValueOrDefault(productId, 0);
    if (available < q)
        return false;

    _reservations[productId] = _reservations.GetValueOrDefault(productId, 0) + q;

    // Check if we need to reorder
    var remainingStock = _stockLevels[productId] - _reservations[productId];
    if (remainingStock <= 5)
    {
        _restockQueue.Add(productId);
    }

    return true;
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json rename-symbol '{
  "filePath": "Demos/ECommerce/ECommerce/InventoryManager.cs",
  "symbolName": "q",
  "methodName": "ReserveStock",
  "newName": "quantity"
}'
```

### AFTER (`InventoryManager.cs`)

```csharp
public bool ReserveStock(string productId, int quantity)
{
    if (!_stockLevels.ContainsKey(productId))
        return false;

    var available = _stockLevels[productId] - _reservations.GetValueOrDefault(productId, 0);
    if (available < quantity)
        return false;

    _reservations[productId] = _reservations.GetValueOrDefault(productId, 0) + quantity;

    // Check if we need to reorder
    var remainingStock = _stockLevels[productId] - _reservations[productId];
    if (remainingStock <= 5)
    {
        _restockQueue.Add(productId);
    }

    return true;
}
```

All three usages of `q` in the method body (`available < q`, `+ q`) plus the parameter
declaration itself are renamed to `quantity`. Call sites that pass named arguments
(e.g., `q: 5`) would also be updated automatically.

---

## Demo 7g: Cleanup Usings -- Remove Unused Imports from `NotificationService.cs`

**Tool:** `cleanup-usings`

`NotificationService.cs` imports `System.Diagnostics`, but no type from that namespace
is referenced anywhere in the file. Unused `using` directives add noise, slow
IntelliSense in large files, and can mask namespace conflicts.

### BEFORE (`NotificationService.cs`)

```csharp
namespace ECommerce;

/// <summary>
/// Notification handling — monolithic class ...
/// </summary>
using System.Diagnostics;

public class NotificationService
{
    ...
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json cleanup-usings '{
  "filePath": "Demos/ECommerce/ECommerce/NotificationService.cs"
}'
```

### AFTER (`NotificationService.cs`)

```csharp
namespace ECommerce;

/// <summary>
/// Notification handling — monolithic class ...
/// </summary>

public class NotificationService
{
    ...
}
```

The `using System.Diagnostics;` line is removed. All other `using` directives that are
actually referenced (implicit global usings for `System`, `System.Collections.Generic`,
etc.) remain in place.

---

## Summary: Reducing Code Debt Safely

| Sub-Demo | Tool | What Was Removed / Changed | Safety Mechanism |
|---|---|---|---|
| 7a | `safe-delete-method` | `LegacyExportXml` method | Solution-wide caller search |
| 7b | `safe-delete-field` | `_migrationTimestamp` field | Read/write reference analysis |
| 7c | `safe-delete-parameter` | `verbose` parameter + all call-site args | Body usage check + call-site rewrite |
| 7d | `safe-delete-variable` | `separator` local variable | Data-flow analysis (assigned but never read) |
| 7e | `rename-symbol` | `x` renamed to `tierDiscountRate` | Semantic scope resolution via line/column |
| 7f | `rename-symbol` | `q` renamed to `quantity` | Scoped rename with named-argument support |
| 7g | `cleanup-usings` | Unused `using System.Diagnostics` | Namespace reference analysis |

### Why Automated Safe Deletion Beats Manual Deletion

1. **Semantic accuracy.** Roslyn resolves symbols through the full compilation model --
   overloads, extension methods, implicit conversions, and cross-project references are
   all accounted for. A text search cannot distinguish between `LegacyExportXml` the
   method and `LegacyExportXml` mentioned in a comment or string literal.

2. **Atomic call-site updates.** When removing a parameter (Demo 7c), the tool rewrites
   every call site in a single operation. Manual deletion requires finding each caller,
   counting argument positions, and hoping you did not miss one in a test project or
   generated code file.

3. **Fail-safe refusal.** If a symbol *is* in use, the tool does not silently proceed --
   it reports every reference and aborts. This is strictly safer than deleting first and
   discovering the breakage at compile time (or worse, at runtime via reflection).

4. **Composability.** These seven operations can be chained in sequence: delete dead
   methods, then clean up fields those methods used, then rename surviving variables for
   clarity, then strip unused `using` directives. Each step leaves the codebase in a
   compilable state.

### The Cost of Keeping Dead Code

Dead code is not free. It:
- Increases cognitive load for every developer reading the file
- Creates false positives in code searches and impact analyses
- Gets copied into new features by developers who assume it is active
- Inflates code coverage denominators, making metrics misleading
- Accumulates transitive dependencies that bloat build times

RefactorMCP's safe-delete tools let you clean aggressively with the confidence that
nothing will break -- turning "we might need this someday" into a verifiable
"nothing needs this today."
