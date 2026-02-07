# Demo 5: Static Conversion, Dependency Injection & Interface Usage

This demo explores six refactoring tools that improve how methods interact with their
enclosing class and its dependencies. The common theme: **tighten the contract** between
a method and the state it actually needs. Methods that use no instance state should be
static or extension methods. Parameters that carry concrete types should use interfaces.
Dependencies passed ad-hoc through method parameters should be constructor-injected. Fields
that never change after construction should be `readonly`. Properties set only at
initialization time should use `init` accessors.

Each sub-demo targets a real method in the ECommerce codebase and can be run independently.

---

## Demo 5a: Convert to Static -- `PricingEngine.CalculateShippingCost`

**Tool:** `convert-to-static-with-parameters`

### Why It Matters

`CalculateShippingCost` is a pure function. It takes three explicit parameters
(`totalWeight`, `destinationCountry`, `expedited`) and returns a result computed entirely
from those inputs. It never touches `_config` or any other field on `PricingEngine`. Yet
it is declared as an instance method, which means callers must instantiate a `PricingEngine`
(and provide an `AppConfig`) just to compute a shipping cost. Making the method `static`
communicates to every reader that it is side-effect-free and state-independent, and enables
calling it without an instance.

### Before

```csharp
// PricingEngine.cs
public class PricingEngine
{
    private readonly AppConfig _config;

    public PricingEngine(AppConfig config)
    {
        _config = config;
    }

    // Instance method -- but never reads _config or any field
    public decimal CalculateShippingCost(decimal totalWeight, string destinationCountry, bool expedited)
    {
        decimal baseCost = totalWeight switch
        {
            <= 0.5m => 3.99m,
            <= 2.0m => 7.99m,
            <= 5.0m => 12.99m,
            <= 10.0m => 18.99m,
            <= 25.0m => 29.99m,
            _ => 29.99m + (totalWeight - 25.0m) * 1.50m
        };

        decimal countryMultiplier = destinationCountry switch
        {
            "US" => 1.0m,
            "CA" or "MX" => 1.5m,
            "GB" or "DE" or "FR" => 2.0m,
            "JP" or "AU" => 2.5m,
            _ => 3.0m
        };

        decimal expeditedSurcharge = expedited ? baseCost * 0.75m : 0m;

        return Math.Round(baseCost * countryMultiplier + expeditedSurcharge, 2);
    }

    // ... other methods that DO use _config ...
}
```

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-static-with-parameters '{
  "filePath": "Demos/ECommerce/ECommerce/PricingEngine.cs",
  "className": "PricingEngine",
  "methodName": "CalculateShippingCost"
}'
```

### After

```csharp
// PricingEngine.cs (after)
public class PricingEngine
{
    private readonly AppConfig _config;

    public PricingEngine(AppConfig config)
    {
        _config = config;
    }

    // Now static -- callers can invoke PricingEngine.CalculateShippingCost(...)
    // without constructing an instance.
    public static decimal CalculateShippingCost(decimal totalWeight, string destinationCountry, bool expedited)
    {
        decimal baseCost = totalWeight switch
        {
            <= 0.5m => 3.99m,
            <= 2.0m => 7.99m,
            <= 5.0m => 12.99m,
            <= 10.0m => 18.99m,
            <= 25.0m => 29.99m,
            _ => 29.99m + (totalWeight - 25.0m) * 1.50m
        };

        decimal countryMultiplier = destinationCountry switch
        {
            "US" => 1.0m,
            "CA" or "MX" => 1.5m,
            "GB" or "DE" or "FR" => 2.0m,
            "JP" or "AU" => 2.5m,
            _ => 3.0m
        };

        decimal expeditedSurcharge = expedited ? baseCost * 0.75m : 0m;

        return Math.Round(baseCost * countryMultiplier + expeditedSurcharge, 2);
    }

    // ...
}
```

### What Changed

| Aspect | Before | After |
|--------|--------|-------|
| Method signature | `public decimal CalculateShippingCost(...)` | `public static decimal CalculateShippingCost(...)` |
| Call site | `engine.CalculateShippingCost(w, c, e)` | `PricingEngine.CalculateShippingCost(w, c, e)` |
| Instance required? | Yes | No |

The `static` keyword makes the contract explicit: this method depends only on its
parameters. Unit tests can call it directly without constructing a `PricingEngine` and
providing a dummy `AppConfig`.

---

## Demo 5b: Convert to Extension Method -- `NotificationService.FormatCurrency`

**Tool:** `convert-to-extension-method`

### Why It Matters

`FormatCurrency` takes a `decimal amount` parameter and returns a formatted string. It
reads no fields, writes no state, and has no dependency on `NotificationService` at all.
It is a general-purpose utility that conceptually "extends" the `decimal` type. Converting
it to a C# extension method lets callers write `total.FormatCurrency()` instead of
`service.FormatCurrency(total)`, which reads more naturally and removes the false
dependency on a `NotificationService` instance.

### Before

```csharp
// NotificationService.cs
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

    // Instance method -- but uses no instance state at all
    public string FormatCurrency(decimal amount)
    {
        if (amount < 0)
            return $"-${Math.Abs(amount):N2}";
        return $"${amount:N2}";
    }

    // ...
}
```

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-extension-method '{
  "filePath": "Demos/ECommerce/ECommerce/NotificationService.cs",
  "className": "NotificationService",
  "methodName": "FormatCurrency"
}'
```

### After

```csharp
// NotificationService.cs (after) -- method removed from class
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
        // Call site updated: total.FormatCurrency() instead of FormatCurrency(total)
        var body = $"Thank you for your order!\n\nOrder ID: {orderId}\nTotal: {total.FormatCurrency()}\n\nYour order is being processed.";

        _emailService.Send(email, subject, body);
        _notificationLog.Add($"[{DateTime.UtcNow:O}] ORDER_CONFIRM: {orderId} -> {email}");
    }

    // ...
}

// New extension method in a static class
public static class DecimalExtensions
{
    public static string FormatCurrency(this decimal amount)
    {
        if (amount < 0)
            return $"-${Math.Abs(amount):N2}";
        return $"${amount:N2}";
    }
}
```

### What Changed

| Aspect | Before | After |
|--------|--------|-------|
| Declaration | Instance method on `NotificationService` | Extension method in `DecimalExtensions` static class |
| First parameter | `decimal amount` | `this decimal amount` |
| Call site (internal) | `FormatCurrency(total)` | `total.FormatCurrency()` |
| Call site (external) | `notificationService.FormatCurrency(val)` | `val.FormatCurrency()` |
| Instance required? | Yes -- needed a `NotificationService` | No -- works on any `decimal` |

The method is now reusable across the entire codebase. Any `decimal` value can call
`.FormatCurrency()` without importing or instantiating `NotificationService`.

---

## Demo 5c: Use Interface -- `CustomerService.UpdateCustomerTier`

**Tool:** `use-interface`

### Why It Matters

`UpdateCustomerTier` accepts a `CustomerRepository repository` parameter -- a concrete
class. But `CustomerRepository` already implements `ICustomerRepository`, and the method
only calls `.Save(customer)`, which is defined on the interface. Using the concrete type
couples this method to a specific implementation, making it impossible to pass a mock, a
caching decorator, or any other implementation without inheriting from the concrete class.
Changing the parameter type to `ICustomerRepository` follows the Dependency Inversion
Principle and enables testability.

### Before

```csharp
// CustomerService.cs
public class CustomerService
{
    private CustomerRepository _repository;
    private readonly EmailService _emailService;

    public CustomerService(CustomerRepository repository, EmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }

    // Parameter uses concrete type CustomerRepository
    public bool UpdateCustomerTier(Customer customer, CustomerRepository repository)
    {
        var updatedTier = CalculateTierUpgrade(customer);
        if (updatedTier != customer.Tier)
        {
            customer.Tier = updatedTier;
            repository.Save(customer);
            return true;
        }
        return false;
    }

    // ...
}

// The interface already exists
public interface ICustomerRepository
{
    void Save(Customer customer);
    Customer? FindById(string id);
    List<Customer> GetAll();
}

public class CustomerRepository : ICustomerRepository { /* ... */ }
```

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json use-interface '{
  "filePath": "Demos/ECommerce/ECommerce/CustomerService.cs",
  "className": "CustomerService",
  "methodName": "UpdateCustomerTier",
  "parameterName": "repository",
  "interfaceName": "ICustomerRepository"
}'
```

### After

```csharp
// CustomerService.cs (after)
public class CustomerService
{
    private CustomerRepository _repository;
    private readonly EmailService _emailService;

    public CustomerService(CustomerRepository repository, EmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }

    // Parameter now uses ICustomerRepository interface
    public bool UpdateCustomerTier(Customer customer, ICustomerRepository repository)
    {
        var updatedTier = CalculateTierUpgrade(customer);
        if (updatedTier != customer.Tier)
        {
            customer.Tier = updatedTier;
            repository.Save(customer);
            return true;
        }
        return false;
    }

    // ...
}
```

### What Changed

| Aspect | Before | After |
|--------|--------|-------|
| Parameter type | `CustomerRepository repository` | `ICustomerRepository repository` |
| Coupling | Tied to concrete `CustomerRepository` | Accepts any `ICustomerRepository` impl |
| Testability | Must use real `CustomerRepository` or subclass | Can pass a mock / stub / decorator |

This is a minimal, targeted change: only the parameter type changes. The method body is
identical because it already used only `.Save()`, which is on the interface. Callers that
previously passed a `CustomerRepository` continue to work because `CustomerRepository`
implements `ICustomerRepository`.

---

## Demo 5d: Constructor Injection -- `CustomerService.RegisterCustomer`

**Tool:** `convert-to-constructor-injection`

### Why It Matters

`RegisterCustomer` receives an `EmailService emailService` parameter on every call. But
`EmailService` is a service dependency, not request-specific data. Passing it as a method
parameter means every caller must know how to create or locate an `EmailService`. This is
the "service locator as parameter" anti-pattern. The class already has an `_emailService`
field (injected via the constructor), so the method parameter is redundant. Converting to
constructor injection removes the parameter, uses the existing field, and ensures a single,
consistent `EmailService` instance across all methods.

### Before

```csharp
// CustomerService.cs
public class CustomerService
{
    private CustomerRepository _repository;
    private readonly EmailService _emailService;

    public CustomerService(CustomerRepository repository, EmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }

    // EmailService passed as a method parameter -- should use the injected field
    public void RegisterCustomer(string name, string email, EmailService emailService)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Email = email,
            MemberSince = DateTime.UtcNow
        };

        _repository.Save(customer);
        emailService.Send(email, "Welcome!", $"Welcome to our store, {name}!");
    }

    // ...
}
```

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-constructor-injection '{
  "filePath": "Demos/ECommerce/ECommerce/CustomerService.cs",
  "className": "CustomerService",
  "methodName": "RegisterCustomer",
  "parameterName": "emailService"
}'
```

### After

```csharp
// CustomerService.cs (after)
public class CustomerService
{
    private CustomerRepository _repository;
    private readonly EmailService _emailService;

    public CustomerService(CustomerRepository repository, EmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }

    // emailService parameter removed -- uses constructor-injected _emailService field
    public void RegisterCustomer(string name, string email)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Email = email,
            MemberSince = DateTime.UtcNow
        };

        _repository.Save(customer);
        _emailService.Send(email, "Welcome!", $"Welcome to our store, {name}!");
    }

    // ...
}
```

### What Changed

| Aspect | Before | After |
|--------|--------|-------|
| Method signature | `RegisterCustomer(string name, string email, EmailService emailService)` | `RegisterCustomer(string name, string email)` |
| Email sending | `emailService.Send(...)` (local param) | `_emailService.Send(...)` (injected field) |
| Caller responsibility | Must supply an `EmailService` on every call | Only supplies business data (`name`, `email`) |
| Constructor | Already had `EmailService` -- unchanged | Same constructor, now the sole source of `EmailService` |

In this case, the class already had a constructor-injected `_emailService` field. The tool
recognized this, removed the redundant method parameter, and rewired the method body to use
the existing field. If no matching field had existed, the tool would have added a new
constructor parameter and backing field automatically.

---

## Demo 5e: Make Field Readonly -- `OrderProcessor._paymentGateway`

**Tool:** `make-field-readonly`

### Why It Matters

`_paymentGateway` is assigned once in the `OrderProcessor` constructor and never
reassigned anywhere in the class. The same is true for `_auditLogger`. Without the
`readonly` modifier, a future developer could accidentally reassign the field in a method
body, breaking the invariant that the payment gateway is fixed for the lifetime of the
processor. Adding `readonly` makes the compiler enforce this invariant and communicates
intent to readers.

### Before

```csharp
// OrderProcessor.cs
public class OrderProcessor
{
    private PaymentGateway _paymentGateway;         // No readonly -- but never reassigned
    private AuditLogger _auditLogger;               // Same issue
    private readonly NotificationService _notificationService;  // Already readonly
    private readonly InventoryManager _inventory;               // Already readonly
    private DateTime _migrationTimestamp;
    private int _processedCount;

    public OrderProcessor(
        PaymentGateway paymentGateway,
        AuditLogger auditLogger,
        NotificationService notificationService,
        InventoryManager inventory)
    {
        _paymentGateway = paymentGateway;
        _auditLogger = auditLogger;
        _notificationService = notificationService;
        _inventory = inventory;
    }

    // ... _paymentGateway is used in ProcessOrder but never reassigned ...
}
```

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json make-field-readonly '{
  "filePath": "Demos/ECommerce/ECommerce/OrderProcessor.cs",
  "className": "OrderProcessor",
  "fieldName": "_paymentGateway"
}'
```

### After

```csharp
// OrderProcessor.cs (after)
public class OrderProcessor
{
    private readonly PaymentGateway _paymentGateway;  // Now readonly
    private AuditLogger _auditLogger;
    private readonly NotificationService _notificationService;
    private readonly InventoryManager _inventory;
    private DateTime _migrationTimestamp;
    private int _processedCount;

    public OrderProcessor(
        PaymentGateway paymentGateway,
        AuditLogger auditLogger,
        NotificationService notificationService,
        InventoryManager inventory)
    {
        _paymentGateway = paymentGateway;
        _auditLogger = auditLogger;
        _notificationService = notificationService;
        _inventory = inventory;
    }

    // ...
}
```

### What Changed

| Aspect | Before | After |
|--------|--------|-------|
| Field declaration | `private PaymentGateway _paymentGateway;` | `private readonly PaymentGateway _paymentGateway;` |
| Compiler enforcement | None -- could be reassigned anywhere | Compiler error if reassigned outside constructor |
| Reader signal | Ambiguous -- might be mutable | Clearly immutable after construction |

This is a one-word change with outsized impact. The `readonly` modifier serves as both
documentation and a compiler-enforced contract. It also enables certain JIT optimizations
because the runtime knows the field reference will not change after construction.

> **Tip:** You could apply the same refactoring to `_auditLogger`, which is also assigned
> only in the constructor and never reassigned.

---

## Demo 5f: Transform Setter to Init -- `StockSnapshot.ProductId`

**Tool:** `transform-setter-to-init`

### Why It Matters

`StockSnapshot` is a data-transfer object created by `InventoryManager.GetSnapshot()`.
Its properties (`ProductId`, `TotalStock`, `Reserved`, `Available`, `NeedsReorder`) are
set once using an object initializer and never modified afterward. Using `{ get; set; }`
allows any code with a reference to the snapshot to mutate it at any time, which is
undesirable for what is conceptually an immutable point-in-time reading. C# 9's `init`
accessor allows properties to be set during object initialization but prevents subsequent
mutation, making the immutability intent explicit.

### Before

```csharp
// InventoryManager.cs
public class StockSnapshot
{
    public string ProductId { get; set; } = "";
    public int TotalStock { get; set; }
    public int Reserved { get; set; }
    public int Available { get; set; }
    public bool NeedsReorder { get; set; }
}

// Creation site -- uses object initializer (works with both set and init)
public StockSnapshot GetSnapshot(string productId)
{
    return new StockSnapshot
    {
        ProductId = productId,
        TotalStock = _stockLevels.GetValueOrDefault(productId, 0),
        Reserved = _reservations.GetValueOrDefault(productId, 0),
        Available = _stockLevels.GetValueOrDefault(productId, 0) - _reservations.GetValueOrDefault(productId, 0),
        NeedsReorder = CheckReorderNeeded(productId)
    };
}
```

### Command

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json transform-setter-to-init '{
  "filePath": "Demos/ECommerce/ECommerce/InventoryManager.cs",
  "className": "StockSnapshot",
  "propertyName": "ProductId"
}'
```

### After

```csharp
// InventoryManager.cs (after)
public class StockSnapshot
{
    public string ProductId { get; init; } = "";   // set -> init
    public int TotalStock { get; set; }
    public int Reserved { get; set; }
    public int Available { get; set; }
    public bool NeedsReorder { get; set; }
}
```

### What Changed

| Aspect | Before | After |
|--------|--------|-------|
| Property accessor | `{ get; set; }` | `{ get; init; }` |
| Initialization | Works with object initializer | Works with object initializer (unchanged) |
| Post-init mutation | `snapshot.ProductId = "X";` compiles | `snapshot.ProductId = "X";` is a compiler error |
| Semantic signal | Mutable data object | Immutable-after-creation value |

The creation site in `GetSnapshot()` continues to work unchanged because object
initializers are compatible with `init` accessors. Only post-construction assignments are
blocked.

> **Tip:** You can apply this same refactoring to all five properties on `StockSnapshot`
> (`TotalStock`, `Reserved`, `Available`, `NeedsReorder`) to make the entire snapshot
> immutable after creation, which matches its intended use as a point-in-time reading.

---

## Summary

| Demo | Tool | Target | Core Change |
|------|------|--------|-------------|
| 5a | `convert-to-static-with-parameters` | `PricingEngine.CalculateShippingCost` | Add `static` to method that uses no instance state |
| 5b | `convert-to-extension-method` | `NotificationService.FormatCurrency` | Move to static extension class; `amount.FormatCurrency()` |
| 5c | `use-interface` | `CustomerService.UpdateCustomerTier` | `CustomerRepository` param becomes `ICustomerRepository` |
| 5d | `convert-to-constructor-injection` | `CustomerService.RegisterCustomer` | Remove `EmailService` param; use injected `_emailService` field |
| 5e | `make-field-readonly` | `OrderProcessor._paymentGateway` | Add `readonly` to field set only in constructor |
| 5f | `transform-setter-to-init` | `StockSnapshot.ProductId` | `{ get; set; }` becomes `{ get; init; }` |

All six refactorings share a principle: **reduce the gap between what a piece of code
actually needs and what it declares it needs.** A method that uses no instance state should
not require an instance. A parameter that only calls interface methods should not demand a
concrete type. A field that never changes should say so. These small, mechanical
transformations compound into code that is easier to test, harder to misuse, and clearer
to read.
