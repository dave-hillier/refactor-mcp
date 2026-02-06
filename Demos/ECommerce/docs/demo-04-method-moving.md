# Demo 4: Moving Methods Between Classes

> **Goal**: Use RefactorMCP's move tools to relocate methods and types to where they
> logically belong -- improving cohesion, enforcing single responsibility, and
> following the one-type-per-file convention.

---

## Demo 4a: Move Instance Method -- `FormatAuditLogEntry` from `OrderProcessor` to `AuditLogger`

### The Code Smell: Feature Envy

`OrderProcessor.FormatAuditLogEntry` builds a formatted audit log string. It accepts
`Order`, `Customer`, `amount`, and `transactionId` as parameters, and its *entire
purpose* is to produce text that gets handed to `AuditLogger.WriteEntry`. The method
is clearly more interested in the `AuditLogger`'s domain than in `OrderProcessor`'s.

This is the classic **Feature Envy** smell -- a method that would rather live in
another class.

### Before

**`OrderProcessor.cs`** -- the method that does not belong here:

```csharp
public class OrderProcessor
{
    private PaymentGateway _paymentGateway;
    private AuditLogger _auditLogger;
    private readonly NotificationService _notificationService;
    private readonly InventoryManager _inventory;
    private DateTime _migrationTimestamp;
    private int _processedCount;

    // ... constructor ...

    public OrderResult ProcessOrder(Order order, Customer customer)
    {
        // ... phases 1-4 ...

        // Phase 5: Audit logging (method belongs in AuditLogger)
        var logEntry = FormatAuditLogEntry(order, customer, totalAmount,
                                           paymentResult.TransactionId ?? "N/A");
        _auditLogger.WriteEntry(logEntry);

        // ... phase 6 ...
    }

    /// <summary>
    /// This method formats audit log entries but it really belongs in AuditLogger.
    /// It only uses OrderProcessor state minimally -- prime candidate for
    /// move-instance-method.
    /// </summary>
    public string FormatAuditLogEntry(Order order, Customer customer,
                                      decimal amount, string transactionId)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Order Audit Entry ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        sb.AppendLine($"Order ID: {order.Id}");
        sb.AppendLine($"Customer: {customer.Name} ({customer.Id})");
        sb.AppendLine($"Items: {order.Items.Count}");
        sb.AppendLine($"Amount: {amount:C}");
        sb.AppendLine($"Transaction: {transactionId}");
        sb.AppendLine($"Status: {order.Status}");
        sb.AppendLine($"Processor Count: {_processedCount}");   // <-- uses OrderProcessor state
        sb.AppendLine($"========================");
        return sb.ToString();
    }
}
```

**`AuditLogger.cs`** -- simple class with no formatting logic:

```csharp
public class AuditLogger
{
    private readonly List<string> _entries = new();

    public void WriteEntry(string entry)
    {
        _entries.Add(entry);
        Console.WriteLine($"[AUDIT] {entry.Split('\n').FirstOrDefault()}");
    }

    public List<string> GetEntries() => new(_entries);

    public void Clear() => _entries.Clear();
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-instance-method '{
  "solutionPath": "./Demos/ECommerce/ECommerce.sln",
  "sourceFilePath": "ECommerce/OrderProcessor.cs",
  "methodName": "FormatAuditLogEntry",
  "sourceClassName": "OrderProcessor",
  "targetClassName": "AuditLogger",
  "targetFieldName": "_auditLogger"
}'
```

### After

**`AuditLogger.cs`** -- now owns the formatting logic:

```csharp
public class AuditLogger
{
    private readonly List<string> _entries = new();

    public void WriteEntry(string entry)
    {
        _entries.Add(entry);
        Console.WriteLine($"[AUDIT] {entry.Split('\n').FirstOrDefault()}");
    }

    public List<string> GetEntries() => new(_entries);

    public void Clear() => _entries.Clear();

    /// <summary>
    /// Moved from OrderProcessor. The processedCount parameter replaces the
    /// original _processedCount field access, since AuditLogger does not own
    /// that state.
    /// </summary>
    public string FormatAuditLogEntry(Order order, Customer customer,
                                      decimal amount, string transactionId,
                                      int processedCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Order Audit Entry ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        sb.AppendLine($"Order ID: {order.Id}");
        sb.AppendLine($"Customer: {customer.Name} ({customer.Id})");
        sb.AppendLine($"Items: {order.Items.Count}");
        sb.AppendLine($"Amount: {amount:C}");
        sb.AppendLine($"Transaction: {transactionId}");
        sb.AppendLine($"Status: {order.Status}");
        sb.AppendLine($"Processor Count: {processedCount}");
        sb.AppendLine($"========================");
        return sb.ToString();
    }
}
```

**`OrderProcessor.cs`** -- now delegates to `_auditLogger`:

```csharp
// Phase 5: Audit logging
var logEntry = _auditLogger.FormatAuditLogEntry(
    order, customer, totalAmount,
    paymentResult.TransactionId ?? "N/A",
    _processedCount);
_auditLogger.WriteEntry(logEntry);
```

### What the Tool Did

| Concern | How the tool handled it |
|---|---|
| **Dependency on `_processedCount`** | The field belongs to `OrderProcessor`, not `AuditLogger`. The tool promoted it to an explicit parameter on the moved method and updated the call site to pass `_processedCount`. |
| **Call-site rewriting** | Every place that called `FormatAuditLogEntry(...)` now calls `_auditLogger.FormatAuditLogEntry(...)` with the extra argument. |
| **Removed the original** | The method body is deleted from `OrderProcessor` to avoid duplication. |

### Why This Improves the Code

- **Higher cohesion**: `AuditLogger` now owns both *writing* and *formatting* of
  audit entries -- the two responsibilities that naturally go together.
- **Lower coupling**: `OrderProcessor` no longer knows *how* audit log entries are
  formatted; it just passes data to the logger.
- **Easier testing**: `FormatAuditLogEntry` can now be tested through `AuditLogger`
  in isolation, without instantiating a full `OrderProcessor` with all its
  dependencies.

---

## Demo 4b: Move Static Method -- `FormatAsTable` from `ReportGenerator` to `TableFormatter`

### The Code Smell: Misplaced Responsibility

`ReportGenerator.FormatAsTable` is a general-purpose static utility that turns a list
of string arrays into a neatly aligned ASCII table. It has nothing to do with report
generation -- it works on raw string data with no knowledge of orders, customers, or
sales. Keeping it in `ReportGenerator` violates the **Single Responsibility
Principle**.

An empty `TableFormatter` class already exists, waiting to receive it.

### Before

**`ReportGenerator.cs`** -- generic table formatting buried in a report class:

```csharp
public class ReportGenerator
{
    private readonly CustomerRepository _customerRepo;

    public ReportGenerator(CustomerRepository customerRepo)
    {
        _customerRepo = customerRepo;
    }

    public string GenerateOrderReport(Order order, Customer customer)
    {
        // ... report building logic ...
    }

    public string GenerateSalesReport(List<Order> orders)
    {
        // ... sales report logic ...
    }

    /// <summary>
    /// A static utility method that doesn't belong in ReportGenerator.
    /// Should be moved to a dedicated TableFormatter class.
    /// </summary>
    public static string FormatAsTable(List<string[]> rows, string[] headers)
    {
        var sb = new StringBuilder();
        var columnWidths = new int[headers.Length];

        for (int i = 0; i < headers.Length; i++)
            columnWidths[i] = headers[i].Length;

        foreach (var row in rows)
        {
            for (int i = 0; i < Math.Min(row.Length, headers.Length); i++)
                columnWidths[i] = Math.Max(columnWidths[i], row[i].Length);
        }

        // Header
        for (int i = 0; i < headers.Length; i++)
            sb.Append(headers[i].PadRight(columnWidths[i] + 2));
        sb.AppendLine();

        // Separator
        for (int i = 0; i < headers.Length; i++)
            sb.Append(new string('-', columnWidths[i] + 2));
        sb.AppendLine();

        // Rows
        foreach (var row in rows)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var value = i < row.Length ? row[i] : "";
                sb.Append(value.PadRight(columnWidths[i] + 2));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public class TableFormatter
{
    // Target class for FormatAsTable move
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-static-method '{
  "solutionPath": "./Demos/ECommerce/ECommerce.sln",
  "sourceFilePath": "ECommerce/ReportGenerator.cs",
  "methodName": "FormatAsTable",
  "sourceClassName": "ReportGenerator",
  "targetClassName": "TableFormatter"
}'
```

### After

**`ReportGenerator.cs`** -- clean, focused on reports only:

```csharp
public class ReportGenerator
{
    private readonly CustomerRepository _customerRepo;

    public ReportGenerator(CustomerRepository customerRepo)
    {
        _customerRepo = customerRepo;
    }

    public string GenerateOrderReport(Order order, Customer customer)
    {
        // ... report building logic (unchanged) ...
    }

    public string GenerateSalesReport(List<Order> orders)
    {
        // ... sales report logic (unchanged) ...
    }

    /// <summary>
    /// Delegating call preserved so existing callers of
    /// ReportGenerator.FormatAsTable still compile.
    /// </summary>
    public static string FormatAsTable(List<string[]> rows, string[] headers)
        => TableFormatter.FormatAsTable(rows, headers);
}
```

**`TableFormatter`** -- now owns the table-formatting logic:

```csharp
public class TableFormatter
{
    public static string FormatAsTable(List<string[]> rows, string[] headers)
    {
        var sb = new StringBuilder();
        var columnWidths = new int[headers.Length];

        for (int i = 0; i < headers.Length; i++)
            columnWidths[i] = headers[i].Length;

        foreach (var row in rows)
        {
            for (int i = 0; i < Math.Min(row.Length, headers.Length); i++)
                columnWidths[i] = Math.Max(columnWidths[i], row[i].Length);
        }

        // Header
        for (int i = 0; i < headers.Length; i++)
            sb.Append(headers[i].PadRight(columnWidths[i] + 2));
        sb.AppendLine();

        // Separator
        for (int i = 0; i < headers.Length; i++)
            sb.Append(new string('-', columnWidths[i] + 2));
        sb.AppendLine();

        // Rows
        foreach (var row in rows)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var value = i < row.Length ? row[i] : "";
                sb.Append(value.PadRight(columnWidths[i] + 2));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
```

### What the Tool Did

| Concern | How the tool handled it |
|---|---|
| **Method relocation** | The full `FormatAsTable` body was moved into `TableFormatter` as a `public static` method. |
| **Delegating call** | A thin forwarding method was left in `ReportGenerator` so that any code calling `ReportGenerator.FormatAsTable(...)` continues to compile without changes. |
| **No instance state** | Because the method is static, no dependency injection or parameter rewriting was needed -- the move is a clean cut-and-paste with redirect. |

### Why This Improves the Code

- **Single Responsibility**: `ReportGenerator` generates reports. `TableFormatter`
  formats tables. Each class has one reason to change.
- **Reusability**: Other classes that need table formatting can depend on
  `TableFormatter` directly, without pulling in the entire `ReportGenerator`.
- **Discoverability**: A developer looking for "table formatting" will find
  `TableFormatter` immediately, rather than hunting through a report class.

---

## Demo 4c: Move Type to File -- `NotificationTemplate` out of `NotificationService.cs`

### The Convention: One Type Per File

C# convention (and many linting rules, including SA1402) dictates that each top-level
type should live in its own file, named after the type. `NotificationTemplate` is
currently defined at the bottom of `NotificationService.cs` -- easy to overlook and
hard to find via file navigation.

### Before

**`NotificationService.cs`** -- two unrelated types in one file:

```csharp
namespace ECommerce;

using System.Diagnostics;

public class NotificationService
{
    private readonly EmailService _emailService;
    private readonly List<string> _notificationLog = new();

    public NotificationService(EmailService emailService)
    {
        _emailService = emailService;
    }

    public void SendOrderConfirmation(string email, string orderId, decimal total)
    {
        var subject = $"Order Confirmation - {orderId}";
        var body = $"Thank you for your order!\n\nOrder ID: {orderId}\n" +
                   $"Total: {FormatCurrency(total)}\n\nYour order is being processed.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] ORDER_CONFIRM: {orderId} -> {email}");
    }

    public void SendShippingNotification(string email, string orderId,
                                         string trackingNumber, int estimatedDays)
    {
        // ... shipping notification logic ...
    }

    public void SendPaymentFailedNotification(string email, string orderId, string reason)
    {
        // ... payment failure notification logic ...
    }

    public void SendTierUpgradeNotification(string email, string customerName,
                                            CustomerTier newTier)
    {
        // ... tier upgrade notification logic ...
    }

    public string FormatCurrency(decimal amount)
    {
        if (amount < 0)
            return $"-${Math.Abs(amount):N2}";
        return $"${amount:N2}";
    }

    public List<string> GetNotificationLog() => new(_notificationLog);
}

/// <summary>
/// This type is defined in NotificationService.cs but should be in its own file.
/// move-type-to-file candidate.
/// </summary>
public class NotificationTemplate
{
    public string TemplateName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyTemplate { get; set; } = "";
    public Dictionary<string, string> Placeholders { get; set; } = new();

    public string Render(Dictionary<string, string> values)
    {
        var result = BodyTemplate;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-to-separate-file '{
  "solutionPath": "./Demos/ECommerce/ECommerce.sln",
  "sourceFilePath": "ECommerce/NotificationService.cs",
  "typeName": "NotificationTemplate"
}'
```

### After

**`NotificationService.cs`** -- contains only `NotificationService`:

```csharp
namespace ECommerce;

using System.Diagnostics;

public class NotificationService
{
    private readonly EmailService _emailService;
    private readonly List<string> _notificationLog = new();

    public NotificationService(EmailService emailService)
    {
        _emailService = emailService;
    }

    public void SendOrderConfirmation(string email, string orderId, decimal total)
    {
        var subject = $"Order Confirmation - {orderId}";
        var body = $"Thank you for your order!\n\nOrder ID: {orderId}\n" +
                   $"Total: {FormatCurrency(total)}\n\nYour order is being processed.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] ORDER_CONFIRM: {orderId} -> {email}");
    }

    public void SendShippingNotification(string email, string orderId,
                                         string trackingNumber, int estimatedDays)
    {
        // ... shipping notification logic ...
    }

    public void SendPaymentFailedNotification(string email, string orderId, string reason)
    {
        // ... payment failure notification logic ...
    }

    public void SendTierUpgradeNotification(string email, string customerName,
                                            CustomerTier newTier)
    {
        // ... tier upgrade notification logic ...
    }

    public string FormatCurrency(decimal amount)
    {
        if (amount < 0)
            return $"-${Math.Abs(amount):N2}";
        return $"${amount:N2}";
    }

    public List<string> GetNotificationLog() => new(_notificationLog);
}
```

**`NotificationTemplate.cs`** (new file) -- the type in its own home:

```csharp
namespace ECommerce;

/// <summary>
/// Moved from NotificationService.cs to its own file.
/// </summary>
public class NotificationTemplate
{
    public string TemplateName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyTemplate { get; set; } = "";
    public Dictionary<string, string> Placeholders { get; set; } = new();

    public string Render(Dictionary<string, string> values)
    {
        var result = BodyTemplate;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }
}
```

### What the Tool Did

| Concern | How the tool handled it |
|---|---|
| **Type extraction** | The entire `NotificationTemplate` class (including its XML doc comment) was removed from `NotificationService.cs`. |
| **New file creation** | A new `NotificationTemplate.cs` was created in the same directory, with the correct `namespace ECommerce;` declaration. |
| **Using directives** | Any `using` statements needed by the moved type were copied to the new file. |
| **No broken references** | Because the type stays in the same namespace and project, all existing code that references `NotificationTemplate` continues to compile. |

### Why This Improves the Code

- **Discoverability**: `NotificationTemplate` now appears in the file explorer exactly
  where you would expect -- in a file named `NotificationTemplate.cs`.
- **Reduced merge conflicts**: Changes to `NotificationService` and
  `NotificationTemplate` are now in separate files, reducing the chance of merge
  conflicts when two developers work on related features.
- **Convention compliance**: Follows the widely adopted C# convention (and StyleCop
  rule SA1402) of one top-level type per file.

---

## Summary

| Demo | Tool | What moved | Key insight |
|---|---|---|---|
| **4a** | `move-instance-method` | `FormatAuditLogEntry` from `OrderProcessor` to `AuditLogger` | The tool detects that `_processedCount` is owned by the source class and promotes it to a parameter on the moved method. |
| **4b** | `move-static-method` | `FormatAsTable` from `ReportGenerator` to `TableFormatter` | A delegating call is left behind so existing callers are not broken. |
| **4c** | `move-to-separate-file` | `NotificationTemplate` out of `NotificationService.cs` | The namespace is preserved and a new file is created automatically. |

All three moves share a common theme: **put code where it belongs**. When methods and
types live in the right class and the right file, the codebase becomes easier to
navigate, easier to test, and easier to maintain.
