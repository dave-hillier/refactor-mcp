# Safe Delete Refactoring

## Overview
Safe Delete refactorings verify that code elements have no references before removing them. This prevents accidental breaking changes. RefactorMCP provides safe deletion for:

- **safe-delete-field**: Remove unused fields
- **safe-delete-method**: Remove unused methods
- **safe-delete-parameter**: Remove unused parameters (updates all callers)
- **safe-delete-variable**: Remove unused local variables

## When to Use
- When cleaning up dead code
- After refactoring when elements become unused
- When removing deprecated functionality
- During code review cleanup

---

## Example 1: Safe Delete Unused Fields

### Before
```csharp
public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;

    // These fields were used for a feature that was removed
    private readonly ILegacyAuthProvider _legacyAuth;        // Never referenced
    private readonly Dictionary<int, User> _userCache;       // Never referenced
    private readonly int _maxCacheSize = 1000;               // Never referenced
    private readonly TimeSpan _cacheTimeout;                 // Never referenced

    // This field is only assigned, never read
    private DateTime _lastCacheCleanup;                      // Write-only

    public UserService(
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<UserService> logger,
        ILegacyAuthProvider legacyAuth)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
        _legacyAuth = legacyAuth;
        _userCache = new Dictionary<int, User>();
        _cacheTimeout = TimeSpan.FromMinutes(30);
    }

    public async Task<User> GetUserAsync(int id)
    {
        _lastCacheCleanup = DateTime.UtcNow;  // Assigned but never read
        return await _userRepository.GetByIdAsync(id);
    }

    public async Task CreateUserAsync(User user)
    {
        await _userRepository.CreateAsync(user);
        await _emailService.SendWelcomeAsync(user.Email);
        _logger.LogInformation("Created user {UserId}", user.Id);
    }
}
```

### After (Unused fields removed)
```csharp
public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<User> GetUserAsync(int id)
    {
        return await _userRepository.GetByIdAsync(id);
    }

    public async Task CreateUserAsync(User user)
    {
        await _userRepository.CreateAsync(user);
        await _emailService.SendWelcomeAsync(user.Email);
        _logger.LogInformation("Created user {UserId}", user.Id);
    }
}
```

### Tool Usage
```bash
# Each field deletion is safe-checked independently
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-field '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/UserService.cs",
    "fieldName": "_legacyAuth"
}'

dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-field '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/UserService.cs",
    "fieldName": "_userCache"
}'
```

---

## Example 2: Safe Delete Unused Methods

### Before
```csharp
public class OrderCalculator
{
    public decimal CalculateTotal(Order order)
    {
        var subtotal = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        var tax = CalculateTax(subtotal, order.ShippingAddress.State);
        var shipping = CalculateShipping(order);

        return subtotal + tax + shipping;
    }

    private decimal CalculateTax(decimal subtotal, string state)
    {
        var rate = GetTaxRate(state);
        return subtotal * rate;
    }

    private decimal GetTaxRate(string state)
    {
        return state switch
        {
            "CA" => 0.0725m,
            "NY" => 0.08m,
            "TX" => 0.0625m,
            _ => 0.05m
        };
    }

    private decimal CalculateShipping(Order order)
    {
        var weight = order.Items.Sum(i => i.Weight * i.Quantity);
        return weight * 0.5m + 5.99m;
    }

    // UNUSED: This was the old calculation before we simplified
    private decimal CalculateTotalLegacy(Order order)
    {
        var subtotal = 0m;
        foreach (var item in order.Items)
        {
            subtotal += item.Quantity * item.UnitPrice;
            if (item.IsDiscounted)
            {
                subtotal -= item.DiscountAmount;
            }
        }
        return subtotal * 1.08m;
    }

    // UNUSED: Helper that was only used by legacy calculation
    private bool IsEligibleForDiscount(Order order)
    {
        return order.Customer.IsPremium && order.Total > 100;
    }

    // UNUSED: Debug method left from development
    private void LogCalculationDetails(Order order, decimal total)
    {
        Console.WriteLine($"Order {order.Id}: {total:C}");
    }
}
```

### After (Unused methods removed)
```csharp
public class OrderCalculator
{
    public decimal CalculateTotal(Order order)
    {
        var subtotal = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        var tax = CalculateTax(subtotal, order.ShippingAddress.State);
        var shipping = CalculateShipping(order);

        return subtotal + tax + shipping;
    }

    private decimal CalculateTax(decimal subtotal, string state)
    {
        var rate = GetTaxRate(state);
        return subtotal * rate;
    }

    private decimal GetTaxRate(string state)
    {
        return state switch
        {
            "CA" => 0.0725m,
            "NY" => 0.08m,
            "TX" => 0.0625m,
            _ => 0.05m
        };
    }

    private decimal CalculateShipping(Order order)
    {
        var weight = order.Items.Sum(i => i.Weight * i.Quantity);
        return weight * 0.5m + 5.99m;
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-method '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/OrderCalculator.cs",
    "methodName": "CalculateTotalLegacy"
}'
```

---

## Example 3: Safe Delete Unused Parameters

### Before
```csharp
public class NotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly IPushNotificationService _pushService;

    // The 'priority' parameter is never used
    public async Task SendOrderConfirmationAsync(
        Order order,
        Customer customer,
        NotificationPriority priority,  // <-- Unused
        CancellationToken cancellationToken = default)
    {
        var subject = $"Order Confirmation - #{order.OrderNumber}";
        var body = $"Thank you for your order, {customer.Name}!";

        await _emailSender.SendAsync(
            customer.Email,
            subject,
            body,
            cancellationToken);

        if (customer.PushNotificationsEnabled)
        {
            await _pushService.SendAsync(
                customer.DeviceToken,
                subject,
                body,
                cancellationToken);
        }
    }

    // The 'includeAttachment' parameter is never used
    public async Task SendShippingNotificationAsync(
        Shipment shipment,
        bool includeAttachment,  // <-- Unused
        CancellationToken cancellationToken = default)
    {
        var subject = "Your order has shipped!";
        var body = $"Tracking: {shipment.TrackingNumber}";

        await _emailSender.SendAsync(
            shipment.Customer.Email,
            subject,
            body,
            cancellationToken);
    }
}

// Callers pass values that are never used
await notificationService.SendOrderConfirmationAsync(
    order,
    customer,
    NotificationPriority.High,  // This value is ignored!
    cancellationToken);

await notificationService.SendShippingNotificationAsync(
    shipment,
    true,  // This value is ignored!
    cancellationToken);
```

### After (Unused parameters removed, callers updated)
```csharp
public class NotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly IPushNotificationService _pushService;

    public async Task SendOrderConfirmationAsync(
        Order order,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Order Confirmation - #{order.OrderNumber}";
        var body = $"Thank you for your order, {customer.Name}!";

        await _emailSender.SendAsync(
            customer.Email,
            subject,
            body,
            cancellationToken);

        if (customer.PushNotificationsEnabled)
        {
            await _pushService.SendAsync(
                customer.DeviceToken,
                subject,
                body,
                cancellationToken);
        }
    }

    public async Task SendShippingNotificationAsync(
        Shipment shipment,
        CancellationToken cancellationToken = default)
    {
        var subject = "Your order has shipped!";
        var body = $"Tracking: {shipment.TrackingNumber}";

        await _emailSender.SendAsync(
            shipment.Customer.Email,
            subject,
            body,
            cancellationToken);
    }
}

// Callers are automatically updated
await notificationService.SendOrderConfirmationAsync(
    order,
    customer,
    cancellationToken);

await notificationService.SendShippingNotificationAsync(
    shipment,
    cancellationToken);
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-parameter '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/NotificationService.cs",
    "methodName": "SendOrderConfirmationAsync",
    "parameterName": "priority"
}'
```

---

## Example 4: Safe Delete Unused Variables

### Before
```csharp
public class DataProcessor
{
    public ProcessingResult Process(DataSet data)
    {
        // Unused variable - value computed but never used
        var recordCount = data.Records.Count;

        // Unused variable - intermediate result discarded
        var validRecords = data.Records.Where(r => r.IsValid).ToList();
        var invalidCount = data.Records.Count(r => !r.IsValid);  // Used below

        // Another unused variable
        var startTime = DateTime.UtcNow;

        var results = new List<ProcessedRecord>();
        foreach (var record in data.Records.Where(r => r.IsValid))
        {
            // Unused loop variable
            var originalValue = record.Value;

            var processed = new ProcessedRecord
            {
                Id = record.Id,
                Value = record.Value * 2,
                ProcessedAt = DateTime.UtcNow
            };
            results.Add(processed);
        }

        // Unused variable
        var endTime = DateTime.UtcNow;

        return new ProcessingResult
        {
            ProcessedRecords = results,
            SkippedCount = invalidCount  // This one IS used
        };
    }
}
```

### After (Unused variables removed)
```csharp
public class DataProcessor
{
    public ProcessingResult Process(DataSet data)
    {
        var invalidCount = data.Records.Count(r => !r.IsValid);

        var results = new List<ProcessedRecord>();
        foreach (var record in data.Records.Where(r => r.IsValid))
        {
            var processed = new ProcessedRecord
            {
                Id = record.Id,
                Value = record.Value * 2,
                ProcessedAt = DateTime.UtcNow
            };
            results.Add(processed);
        }

        return new ProcessingResult
        {
            ProcessedRecords = results,
            SkippedCount = invalidCount
        };
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-variable '{
    "filePath": "Services/DataProcessor.cs",
    "startLine": 5,
    "endLine": 5
}'
```

---

## Safety Guarantees

Each safe-delete operation:
1. **Scans for references** - Searches entire solution for usages
2. **Reports if blocked** - Returns an error if references exist
3. **Shows blockers** - Lists where the element is referenced
4. **Atomic operation** - Either succeeds completely or makes no changes

## When Safe Delete Fails

```bash
# Example: Trying to delete a method that's still called
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-method '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/Calculator.cs",
    "methodName": "CalculateTax"
}'

# Response:
# {
#   "success": false,
#   "error": "Cannot delete 'CalculateTax': method is referenced",
#   "references": [
#     "OrderService.cs:45 - CalculateTotal",
#     "InvoiceService.cs:23 - GenerateInvoice"
#   ]
# }
```

---

## Benefits
1. **Safety**: Never accidentally break working code
2. **Confidence**: Remove dead code knowing nothing depends on it
3. **Solution-wide**: Checks all files, not just the current one
4. **Actionable Feedback**: Shows exactly what's blocking deletion
5. **Clean Codebase**: Enables aggressive cleanup of unused code
