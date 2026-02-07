# Demo 6: Design Pattern Refactorings

> **Theme**: Applying Gang of Four (GoF) structural and behavioral patterns to improve
> extensibility, testability, and separation of concerns in the ECommerce codebase.

---

## Table of Contents

| Sub-demo | Pattern (GoF) | Tool | Target |
|----------|---------------|------|--------|
| [6a](#demo-6a-extract-interface--inotificationservice) | **Interface Segregation** | `extract-interface` | `NotificationService` |
| [6b](#demo-6b-extract-decorator--logging-around-sendorderconfirmation) | **Decorator** | `extract-decorator` | `NotificationService.SendOrderConfirmation` |
| [6c](#demo-6c-create-adapter--warehouseapiadapter) | **Adapter** | `create-adapter` | `InventoryManager` -> `IWarehouseApi` |
| [6d](#demo-6d-add-observer--stockreserved-event) | **Observer** | `add-observer` | `InventoryManager.ReserveStock` |
| [6e](#demo-6e-feature-flag-refactor--strategy-for-enablenewpricingengine) | **Strategy** | `feature-flag-refactor` | `PricingEngine.ApplyDynamicPricing` |

---

## Demo 6a: Extract Interface -- INotificationService

> **GoF Pattern**: _Not a GoF pattern per se, but the foundational enabler for Decorator (6b),
> Adapter, Proxy, and virtually every other structural pattern. Closely aligned with the
> **Dependency Inversion Principle** (DIP) -- "depend on abstractions, not concretions."_

### Why

`NotificationService` is a concrete class with four public notification methods. Every consumer
that depends on it is tightly coupled to the implementation. This makes it impossible to:

- Substitute a fake/mock in unit tests
- Swap in an alternative notification provider (e.g., SMS, push notifications)
- Apply the Decorator pattern (Demo 6b) without an interface to wrap

### BEFORE

```
NotificationService.cs
```

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
        var body = $"Thank you for your order!\n\nOrder ID: {orderId}\nTotal: {FormatCurrency(total)}\n\nYour order is being processed.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] ORDER_CONFIRM: {orderId} -> {email}");
    }

    public void SendShippingNotification(string email, string orderId, string trackingNumber, int estimatedDays)
    {
        var subject = $"Your Order {orderId} Has Shipped!";
        var body = $"Great news! Your order is on its way.\n\nTracking: {trackingNumber}\nEstimated delivery: {estimatedDays} business days";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] SHIPPING: {orderId} -> {email}");
    }

    public void SendPaymentFailedNotification(string email, string orderId, string reason)
    {
        var subject = $"Payment Issue - Order {orderId}";
        var body = $"We were unable to process your payment.\n\nOrder ID: {orderId}\nReason: {reason}\n\nPlease update your payment method.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] PAYMENT_FAIL: {orderId} -> {email}");
    }

    public void SendTierUpgradeNotification(string email, string customerName, CustomerTier newTier)
    {
        var subject = $"Congratulations {customerName}! You've been upgraded!";
        var discountPercent = newTier switch
        {
            CustomerTier.Silver => 5,
            CustomerTier.Gold => 10,
            CustomerTier.Platinum => 15,
            _ => 0
        };
        var body = $"You've reached {newTier} status!\n\nYou now enjoy {discountPercent}% off all orders.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] TIER_UPGRADE: {newTier} -> {email}");
    }

    // ... other members omitted for brevity
}
```

**Problem summary**: No interface. All consumers depend directly on the concrete class.

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-interface '{
    "solutionPath": "./Demos/ECommerce/ECommerce.sln",
    "filePath": "./Demos/ECommerce/ECommerce/NotificationService.cs",
    "className": "NotificationService",
    "memberList": "SendOrderConfirmation,SendShippingNotification,SendPaymentFailedNotification,SendTierUpgradeNotification",
    "interfaceFilePath": "./Demos/ECommerce/ECommerce/INotificationService.cs"
}'
```

### AFTER

**New file: `INotificationService.cs`**

```csharp
namespace ECommerce;

public interface INotificationService
{
    void SendOrderConfirmation(string email, string orderId, decimal total);
    void SendShippingNotification(string email, string orderId, string trackingNumber, int estimatedDays);
    void SendPaymentFailedNotification(string email, string orderId, string reason);
    void SendTierUpgradeNotification(string email, string customerName, CustomerTier newTier);
}
```

**Modified: `NotificationService.cs`**

```csharp
public class NotificationService : INotificationService   // <-- now implements the interface
{
    // ... implementation unchanged
}
```

### What Changed

```
 NotificationService.cs          |  1 line changed  (adds : INotificationService)
 INotificationService.cs         |  NEW FILE -- interface with 4 method signatures
```

### Design Impact

```
                  BEFORE                                    AFTER
         +-----------------------+                +-----------------------+
         |   OrderProcessor      |                |   OrderProcessor      |
         +-----------+-----------+                +-----------+-----------+
                     |                                        |
                     | depends on                             | depends on
                     v                                        v
         +-----------------------+              +---------------------------+
         | NotificationService   |              | <<interface>>             |
         | (concrete)            |              | INotificationService      |
         +-----------------------+              +-------------+-------------+
                                                              ^
                                                              | implements
                                                +-------------+-------------+
                                                | NotificationService       |
                                                | (concrete)                |
                                                +---------------------------+
```

- Consumers now depend on the abstraction (`INotificationService`), not the concrete class
- Unit tests can provide a mock `INotificationService` with zero email infrastructure
- New notification backends (SMS, Slack, push) can implement the same interface
- This is the prerequisite for Demo 6b (Decorator)

---

## Demo 6b: Extract Decorator -- Logging Around SendOrderConfirmation

> **GoF Pattern**: **Decorator** -- _"Attach additional responsibilities to an object dynamically.
> Decorators provide a flexible alternative to subclassing for extending functionality."_
> (Design Patterns, Gamma et al., p. 175)

### Why

We want to add logging and timing instrumentation around `SendOrderConfirmation` without modifying
the method itself. Hard-coding logging inside the method violates the **Open/Closed Principle** --
the class should be open for extension but closed for modification. The Decorator pattern lets us
layer cross-cutting concerns (logging, metrics, retries, circuit-breaking) around the original
behavior.

### BEFORE

```csharp
// Caller code -- direct invocation, no logging, no metrics
notificationService.SendOrderConfirmation("customer@example.com", "ORD-1234", 99.95m);
```

There is no way to intercept, log, or time the call without editing `NotificationService` itself.

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-decorator '{
    "solutionPath": "./Demos/ECommerce/ECommerce.sln",
    "filePath": "./Demos/ECommerce/ECommerce/NotificationService.cs",
    "className": "NotificationService",
    "methodName": "SendOrderConfirmation"
}'
```

### AFTER

**New class: `LoggingNotificationServiceDecorator`**

```csharp
namespace ECommerce;

using System.Diagnostics;

/// <summary>
/// Decorator that wraps INotificationService and adds logging/timing
/// around SendOrderConfirmation. All other methods delegate directly.
/// </summary>
public class LoggingNotificationServiceDecorator : INotificationService
{
    private readonly INotificationService _inner;

    public LoggingNotificationServiceDecorator(INotificationService inner)
    {
        _inner = inner;
    }

    public void SendOrderConfirmation(string email, string orderId, decimal total)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[LOG] Sending order confirmation for {orderId} to {email}...");

        _inner.SendOrderConfirmation(email, orderId, total);

        sw.Stop();
        Console.WriteLine($"[LOG] Order confirmation for {orderId} sent in {sw.ElapsedMilliseconds}ms");
    }

    // Remaining methods delegate directly to _inner:
    public void SendShippingNotification(string email, string orderId, string trackingNumber, int estimatedDays)
        => _inner.SendShippingNotification(email, orderId, trackingNumber, estimatedDays);

    public void SendPaymentFailedNotification(string email, string orderId, string reason)
        => _inner.SendPaymentFailedNotification(email, orderId, reason);

    public void SendTierUpgradeNotification(string email, string customerName, CustomerTier newTier)
        => _inner.SendTierUpgradeNotification(email, orderId, newTier);
}
```

### What Changed

```
 LoggingNotificationServiceDecorator.cs  |  NEW FILE -- decorator class
```

### Design Impact

```
                              BEFORE

        Caller -----> NotificationService.SendOrderConfirmation()
                      (no logging, no timing)


                              AFTER

        Caller -----> LoggingNotificationServiceDecorator
                          |
                          |  1. Log "Sending..."
                          |  2. Start timer
                          |  3. Delegate to inner
                          |  4. Stop timer
                          |  5. Log "Sent in Xms"
                          |
                          +-----> NotificationService.SendOrderConfirmation()
                                  (original behavior, untouched)
```

- **Open/Closed Principle**: `NotificationService` is not modified at all
- **Single Responsibility**: Logging concern is isolated in its own class
- **Composable**: Decorators can be stacked -- add a `RetryNotificationServiceDecorator` later:
  ```
  Caller -> RetryDecorator -> LoggingDecorator -> NotificationService
  ```
- **DI-friendly**: Wire up in the composition root:
  ```csharp
  services.AddScoped<NotificationService>();
  services.AddScoped<INotificationService>(sp =>
      new LoggingNotificationServiceDecorator(sp.GetRequiredService<NotificationService>()));
  ```

---

## Demo 6c: Create Adapter -- WarehouseApiAdapter

> **GoF Pattern**: **Adapter** (Object Adapter variant) -- _"Convert the interface of a class into
> another interface clients expect. Adapter lets classes work together that couldn't otherwise
> because of incompatible interfaces."_ (Design Patterns, Gamma et al., p. 139)

### Why

The `InventoryManager` class has its own API -- `ReserveStock(productId, quantity)`,
`ReleaseReservation(productId, quantity)`, etc. But an external warehouse integration system
expects the `IWarehouseApi` interface, which has completely different method signatures:

```csharp
public interface IWarehouseApi
{
    bool CheckAvailability(string sku, int quantity);
    string PlaceHold(string sku, int quantity);
    void CancelHold(string holdId);
}
```

We cannot modify `InventoryManager` (it has many internal consumers), and we cannot modify the
external system's expected interface. The **Adapter** pattern bridges this gap.

### BEFORE

```csharp
// InventoryManager API:
public bool ReserveStock(string productId, int q)    // returns bool
public void ReleaseReservation(string productId, int quantity)
public int GetStockLevel(string productId)

// IWarehouseApi (external contract):
bool CheckAvailability(string sku, int quantity);
string PlaceHold(string sku, int quantity);           // returns a hold ID string
void CancelHold(string holdId);                       // takes a hold ID, not productId + quantity
```

**Mismatch summary**:

| Aspect | `InventoryManager` | `IWarehouseApi` |
|--------|-------------------|-----------------|
| Check stock | `GetStockLevel(productId)` returns `int` | `CheckAvailability(sku, qty)` returns `bool` |
| Reserve | `ReserveStock(productId, q)` returns `bool` | `PlaceHold(sku, qty)` returns `string` hold ID |
| Release | `ReleaseReservation(productId, qty)` | `CancelHold(holdId)` takes a single hold ID |

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json create-adapter '{
    "solutionPath": "./Demos/ECommerce/ECommerce.sln",
    "filePath": "./Demos/ECommerce/ECommerce/InventoryManager.cs",
    "className": "InventoryManager",
    "methodName": "ReserveStock",
    "adapterName": "WarehouseApiAdapter"
}'
```

### AFTER

**New class: `WarehouseApiAdapter`**

```csharp
namespace ECommerce;

/// <summary>
/// Adapter (GoF) that bridges InventoryManager to the IWarehouseApi contract
/// expected by the external warehouse integration system.
/// </summary>
public class WarehouseApiAdapter : IWarehouseApi
{
    private readonly InventoryManager _inventoryManager;
    private readonly Dictionary<string, (string ProductId, int Quantity)> _holds = new();

    public WarehouseApiAdapter(InventoryManager inventoryManager)
    {
        _inventoryManager = inventoryManager;
    }

    public bool CheckAvailability(string sku, int quantity)
    {
        var stockLevel = _inventoryManager.GetStockLevel(sku);
        return stockLevel >= quantity;
    }

    public string PlaceHold(string sku, int quantity)
    {
        var success = _inventoryManager.ReserveStock(sku, quantity);
        if (!success)
            throw new InvalidOperationException($"Unable to reserve {quantity} units of {sku}");

        var holdId = Guid.NewGuid().ToString();
        _holds[holdId] = (sku, quantity);
        return holdId;
    }

    public void CancelHold(string holdId)
    {
        if (_holds.TryGetValue(holdId, out var hold))
        {
            _inventoryManager.ReleaseReservation(hold.ProductId, hold.Quantity);
            _holds.Remove(holdId);
        }
    }
}
```

### What Changed

```
 WarehouseApiAdapter.cs  |  NEW FILE -- adapter bridging InventoryManager to IWarehouseApi
 InventoryManager.cs     |  UNCHANGED
```

### Design Impact

```
                              BEFORE

    External System ----X----> InventoryManager
                       incompatible interfaces!


                              AFTER

    External System              WarehouseApiAdapter              InventoryManager
    (expects IWarehouseApi)      (implements IWarehouseApi)       (unchanged)
         |                              |                               |
         |  CheckAvailability(sku, 5)   |                               |
         |----------------------------->|  GetStockLevel(sku)           |
         |                              |------------------------------>|
         |                              |<------------------------------|
         |  true                        |                               |
         |<-----------------------------|                               |
         |                              |                               |
         |  PlaceHold(sku, 5)           |                               |
         |----------------------------->|  ReserveStock(sku, 5)         |
         |                              |------------------------------>|
         |  "hold-abc-123"              |  true                         |
         |<-----------------------------|<------------------------------|
         |                              |                               |
         |  CancelHold("hold-abc-123")  |                               |
         |----------------------------->|  ReleaseReservation(sku, 5)   |
         |                              |------------------------------>|
```

- `InventoryManager` is **completely untouched** -- no risk to existing consumers
- The adapter **translates** between the two incompatible APIs:
  - Maps `string holdId` to `(productId, quantity)` pairs internally
  - Converts `int` stock levels to `bool` availability checks
  - Generates hold IDs to bridge the identifier mismatch
- New external integrations simply receive a `WarehouseApiAdapter` instance

---

## Demo 6d: Add Observer -- StockReserved Event

> **GoF Pattern**: **Observer** -- _"Define a one-to-many dependency between objects so that when
> one object changes state, all its dependents are notified and updated automatically."_
> (Design Patterns, Gamma et al., p. 293)

### Why

`InventoryManager.ReserveStock` currently processes a reservation and silently returns `true` or
`false`. There is no way for external code to react to successful reservations without polling.
Real systems need to:

- Send low-stock alerts when inventory drops below a threshold
- Update dashboards and monitoring systems in real time
- Trigger automatic reorder workflows
- Emit analytics events for demand forecasting

The **Observer** pattern (implemented via C# `event`) allows loose coupling between the inventory
system and any number of subscribers.

### BEFORE

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
    // <-- No notification to external code. Monitoring? Alerting? Nothing.
}
```

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json add-observer '{
    "solutionPath": "./Demos/ECommerce/ECommerce.sln",
    "filePath": "./Demos/ECommerce/ECommerce/InventoryManager.cs",
    "className": "InventoryManager",
    "methodName": "ReserveStock",
    "eventName": "StockReserved"
}'
```

### AFTER

**Modified: `InventoryManager.cs`**

```csharp
public class InventoryManager
{
    private readonly Dictionary<string, int> _stockLevels = new();
    private readonly Dictionary<string, int> _reservations = new();
    private readonly List<string> _restockQueue = new();

    // --- NEW: Observer event ---
    public event EventHandler<StockReservedEventArgs>? StockReserved;

    // ... existing methods ...

    public bool ReserveStock(string productId, int q)
    {
        if (!_stockLevels.ContainsKey(productId))
            return false;

        var available = _stockLevels[productId] - _reservations.GetValueOrDefault(productId, 0);
        if (available < q)
            return false;

        _reservations[productId] = _reservations.GetValueOrDefault(productId, 0) + q;

        var remainingStock = _stockLevels[productId] - _reservations[productId];
        if (remainingStock <= 5)
        {
            _restockQueue.Add(productId);
        }

        // --- NEW: Raise event to notify all observers ---
        StockReserved?.Invoke(this, new StockReservedEventArgs
        {
            ProductId = productId,
            QuantityReserved = q,
            RemainingStock = remainingStock
        });

        return true;
    }

    // ... other methods unchanged ...
}

// --- NEW: Event args class ---
public class StockReservedEventArgs : EventArgs
{
    public string ProductId { get; init; } = "";
    public int QuantityReserved { get; init; }
    public int RemainingStock { get; init; }
}
```

### What Changed

```
 InventoryManager.cs  |  + event field declaration
                      |  + event raise at end of ReserveStock
                      |  + StockReservedEventArgs class
```

### Design Impact

```
                BEFORE                                      AFTER

     ReserveStock()                           ReserveStock()
          |                                        |
          | returns bool                           | returns bool
          v                                        |
       (silence)                                   +---> StockReserved event fires
                                                         |
                                                         +---> LowStockAlertHandler
                                                         |     "SKU-42 has only 3 left!"
                                                         |
                                                         +---> DashboardUpdater
                                                         |     Real-time stock display
                                                         |
                                                         +---> ReorderWorkflow
                                                         |     Auto-trigger purchase order
                                                         |
                                                         +---> AnalyticsEmitter
                                                               Demand forecasting data
```

**Subscriber example**:

```csharp
var inventory = new InventoryManager();

// Subscribe -- any number of observers, loosely coupled
inventory.StockReserved += (sender, args) =>
{
    if (args.RemainingStock <= 5)
        Console.WriteLine($"[ALERT] Low stock: {args.ProductId} has only {args.RemainingStock} units remaining!");
};

inventory.StockReserved += (sender, args) =>
{
    Console.WriteLine($"[METRICS] Reserved {args.QuantityReserved} of {args.ProductId}");
};

// This call now notifies all subscribers automatically
inventory.ReserveStock("SKU-42", 10);
```

- **Loose coupling**: `InventoryManager` knows nothing about its observers
- **Open/Closed**: New reactions to stock reservations require zero changes to `InventoryManager`
- **C# idiom**: Uses standard `event`/`EventHandler<T>` pattern, familiar to all .NET developers

---

## Demo 6e: Feature Flag Refactor -- Strategy for EnableNewPricingEngine

> **GoF Pattern**: **Strategy** -- _"Define a family of algorithms, encapsulate each one, and make
> them interchangeable. Strategy lets the algorithm vary independently from clients that use it."_
> (Design Patterns, Gamma et al., p. 315)

### Why

`PricingEngine.ApplyDynamicPricing` contains an `if/else` branch controlled by the
`_config.EnableNewPricingEngine` feature flag. This is a common pattern during feature rollouts,
but it creates problems:

- **Branching complexity grows**: Every new pricing algorithm adds another `else if`
- **Testing difficulty**: Must set up `AppConfig` flags to reach each branch
- **Violation of Open/Closed**: Adding a third pricing model requires editing the method
- **Dead code risk**: Once the flag is permanently enabled, the old branch becomes dead code
  that nobody dares to remove

The **Strategy** pattern replaces the conditional with polymorphism.

### BEFORE

```csharp
public class PricingEngine
{
    private readonly AppConfig _config;

    public PricingEngine(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Feature flag branching -- should be refactored to strategy pattern.
    /// </summary>
    public decimal ApplyDynamicPricing(decimal basePrice, Product product, Customer customer)
    {
        if (_config.EnableNewPricingEngine)
        {
            // New pricing: demand-based with customer loyalty
            decimal demandFactor = product.StockQuantity < 10 ? 1.20m
                                 : product.StockQuantity < 50 ? 1.05m
                                 : 1.0m;
            decimal loyaltyFactor = customer.LifetimeSpend > 10000 ? 0.90m
                                  : customer.LifetimeSpend > 5000 ? 0.95m
                                  : 1.0m;
            return Math.Round(basePrice * demandFactor * loyaltyFactor, 2);
        }
        else
        {
            // Old pricing: flat category-based markup
            decimal markup = product.Category switch
            {
                "Electronics" => 1.15m,
                "Clothing"    => 1.20m,
                "Food"        => 1.05m,
                _             => 1.10m
            };
            return Math.Round(basePrice * markup, 2);
        }
    }
}
```

**Code smell**: Feature flag `if/else` directly in the business logic method.

### CLI Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json feature-flag-refactor '{
    "solutionPath": "./Demos/ECommerce/ECommerce.sln",
    "filePath": "./Demos/ECommerce/ECommerce/PricingEngine.cs",
    "flagName": "EnableNewPricingEngine"
}'
```

### AFTER

**New: Strategy interface and two implementations**

```csharp
namespace ECommerce;

/// <summary>
/// Strategy interface for dynamic pricing algorithms.
/// Each implementation encapsulates a single pricing strategy.
/// </summary>
public interface IDynamicPricingStrategy
{
    decimal ApplyDynamicPricing(decimal basePrice, Product product, Customer customer);
}

/// <summary>
/// Legacy pricing strategy: flat category-based markup.
/// Formerly the "else" branch of the feature flag.
/// </summary>
public class LegacyDynamicPricingStrategy : IDynamicPricingStrategy
{
    public decimal ApplyDynamicPricing(decimal basePrice, Product product, Customer customer)
    {
        decimal markup = product.Category switch
        {
            "Electronics" => 1.15m,
            "Clothing"    => 1.20m,
            "Food"        => 1.05m,
            _             => 1.10m
        };
        return Math.Round(basePrice * markup, 2);
    }
}

/// <summary>
/// New pricing strategy: demand-based pricing with customer loyalty factors.
/// Formerly the "if" branch of the feature flag.
/// </summary>
public class DemandBasedDynamicPricingStrategy : IDynamicPricingStrategy
{
    public decimal ApplyDynamicPricing(decimal basePrice, Product product, Customer customer)
    {
        decimal demandFactor = product.StockQuantity < 10 ? 1.20m
                             : product.StockQuantity < 50 ? 1.05m
                             : 1.0m;
        decimal loyaltyFactor = customer.LifetimeSpend > 10000 ? 0.90m
                              : customer.LifetimeSpend > 5000 ? 0.95m
                              : 1.0m;
        return Math.Round(basePrice * demandFactor * loyaltyFactor, 2);
    }
}
```

**Modified: `PricingEngine.cs`**

```csharp
public class PricingEngine
{
    private readonly IDynamicPricingStrategy _dynamicPricingStrategy;

    public PricingEngine(AppConfig config)
    {
        // Strategy selected at construction time based on the feature flag.
        // The flag is read ONCE -- no runtime branching in the hot path.
        _dynamicPricingStrategy = config.EnableNewPricingEngine
            ? new DemandBasedDynamicPricingStrategy()
            : new LegacyDynamicPricingStrategy();
    }

    public decimal ApplyDynamicPricing(decimal basePrice, Product product, Customer customer)
    {
        // No if/else. Pure delegation to the selected strategy.
        return _dynamicPricingStrategy.ApplyDynamicPricing(basePrice, product, customer);
    }

    // ... other methods unchanged ...
}
```

### What Changed

```
 PricingEngine.cs                      |  if/else removed, delegates to strategy
 IDynamicPricingStrategy.cs            |  NEW FILE -- strategy interface
 LegacyDynamicPricingStrategy.cs       |  NEW FILE -- old pricing logic
 DemandBasedDynamicPricingStrategy.cs  |  NEW FILE -- new pricing logic
```

### Design Impact

```
              BEFORE                                        AFTER

  +---------------------------+               +---------------------------+
  |      PricingEngine        |               |      PricingEngine        |
  +---------------------------+               +---------------------------+
  | ApplyDynamicPricing()     |               | - _strategy               |
  |   if (flag)               |               | ApplyDynamicPricing()     |
  |     // new pricing        |               |   return _strategy.Apply()|
  |   else                    |               +-------------+-------------+
  |     // old pricing        |                             |
  +---------------------------+                             | uses
                                                            v
                                              +---------------------------+
                                              | <<interface>>             |
                                              | IDynamicPricingStrategy   |
                                              +---------------------------+
                                              | ApplyDynamicPricing()     |
                                              +-------------+-------------+
                                                            ^
                                               +------------+------------+
                                               |                         |
                                  +------------+--------+   +------------+------------+
                                  | LegacyDynamic       |   | DemandBasedDynamic      |
                                  | PricingStrategy     |   | PricingStrategy         |
                                  +---------------------+   +-------------------------+
                                  | Category-based      |   | Demand + loyalty        |
                                  | markup               |   | factors                 |
                                  +---------------------+   +-------------------------+
```

- **Feature flag eliminated from business logic**: Read once at construction, never again
- **Open/Closed**: Adding a third pricing strategy (e.g., `SeasonalPricingStrategy`) requires
  zero changes to `PricingEngine` -- just create a new class implementing `IDynamicPricingStrategy`
- **Testability**: Each strategy is a standalone class, unit-testable in isolation with no
  `AppConfig` dependency
- **Clean removal path**: When the legacy strategy is permanently retired, delete
  `LegacyDynamicPricingStrategy` and remove the conditional from the constructor

---

## Summary

| Demo | GoF Pattern | Tool | Key Principle |
|------|-------------|------|---------------|
| **6a** | (Interface Extraction) | `extract-interface` | Dependency Inversion -- depend on abstractions |
| **6b** | **Decorator** | `extract-decorator` | Open/Closed -- extend behavior without modification |
| **6c** | **Adapter** | `create-adapter` | Bridge incompatible interfaces without changing either side |
| **6d** | **Observer** | `add-observer` | Loose coupling -- publisher knows nothing about subscribers |
| **6e** | **Strategy** | `feature-flag-refactor` | Replace conditionals with polymorphism |

### Running This Demo

```bash
# From the repository root
./Demos/ECommerce/run-demos.sh 6

# Or run individual sub-demos by copying the CLI commands above
```

### Prerequisites

- .NET 9.0 SDK
- RefactorMCP built: `dotnet build` from the repository root
- ECommerce demo project: `dotnet build ./Demos/ECommerce/ECommerce.sln`
