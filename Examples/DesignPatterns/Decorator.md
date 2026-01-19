# Extract Decorator Refactoring

## Overview
The `extract-decorator` refactoring creates a decorator class that wraps an existing implementation, allowing you to add behavior before or after method calls without modifying the original class.

## When to Use
- When you want to add logging, caching, or validation to existing functionality
- When you need to extend behavior without modifying the original class
- When implementing cross-cutting concerns
- When you want to layer multiple behaviors on top of each other

---

## Example 1: Add Logging Decorator

### Before
```csharp
public interface IOrderService
{
    Task<Order> CreateOrderAsync(OrderRequest request);
    Task<Order> GetOrderAsync(Guid orderId);
    Task CancelOrderAsync(Guid orderId);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly IPaymentGateway _payments;

    public async Task<Order> CreateOrderAsync(OrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Items = request.Items,
            Total = request.Items.Sum(i => i.Price * i.Quantity),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var paymentResult = await _payments.ChargeAsync(request.PaymentInfo, order.Total);
        order.PaymentTransactionId = paymentResult.TransactionId;
        order.Status = OrderStatus.Confirmed;

        await _repository.SaveAsync(order);
        return order;
    }

    public async Task<Order> GetOrderAsync(Guid orderId)
    {
        return await _repository.GetByIdAsync(orderId);
    }

    public async Task CancelOrderAsync(Guid orderId)
    {
        var order = await _repository.GetByIdAsync(orderId);
        if (order.Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot cancel shipped order");

        order.Status = OrderStatus.Cancelled;
        await _repository.UpdateAsync(order);

        if (!string.IsNullOrEmpty(order.PaymentTransactionId))
            await _payments.RefundAsync(order.PaymentTransactionId);
    }
}

// No logging - hard to debug production issues
```

### After
```csharp
// Original service unchanged

// LoggingOrderServiceDecorator.cs - New decorator
public class LoggingOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;
    private readonly ILogger<LoggingOrderServiceDecorator> _logger;

    public LoggingOrderServiceDecorator(
        IOrderService inner,
        ILogger<LoggingOrderServiceDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(OrderRequest request)
    {
        _logger.LogInformation(
            "Creating order with {ItemCount} items, total: {Total}",
            request.Items.Count,
            request.Items.Sum(i => i.Price * i.Quantity));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var order = await _inner.CreateOrderAsync(request);

            _logger.LogInformation(
                "Order {OrderId} created successfully in {ElapsedMs}ms",
                order.Id,
                stopwatch.ElapsedMilliseconds);

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create order after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<Order> GetOrderAsync(Guid orderId)
    {
        _logger.LogDebug("Fetching order {OrderId}", orderId);

        var order = await _inner.GetOrderAsync(orderId);

        if (order == null)
            _logger.LogWarning("Order {OrderId} not found", orderId);
        else
            _logger.LogDebug("Retrieved order {OrderId}, status: {Status}", orderId, order.Status);

        return order;
    }

    public async Task CancelOrderAsync(Guid orderId)
    {
        _logger.LogInformation("Cancelling order {OrderId}", orderId);

        try
        {
            await _inner.CancelOrderAsync(orderId);
            _logger.LogInformation("Order {OrderId} cancelled successfully", orderId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot cancel order {OrderId}: {Reason}", orderId, ex.Message);
            throw;
        }
    }
}

// DI Registration - wrap the real service
services.AddScoped<OrderService>();
services.AddScoped<IOrderService>(sp =>
    new LoggingOrderServiceDecorator(
        sp.GetRequiredService<OrderService>(),
        sp.GetRequiredService<ILogger<LoggingOrderServiceDecorator>>()));
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-decorator '{
    "solutionPath": "MyApp.sln",
    "filePath": "Services/OrderService.cs",
    "methodName": "CreateOrderAsync",
    "decoratorClassName": "LoggingOrderServiceDecorator"
}'
```

---

## Example 2: Add Caching Decorator

### Before
```csharp
public interface IProductCatalog
{
    Task<Product> GetProductAsync(string sku);
    Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category);
    Task<IEnumerable<Product>> SearchProductsAsync(string query);
}

public class ProductCatalog : IProductCatalog
{
    private readonly IProductRepository _repository;

    public async Task<Product> GetProductAsync(string sku)
    {
        return await _repository.FindBySkuAsync(sku);
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        return await _repository.FindByCategoryAsync(category);
    }

    public async Task<IEnumerable<Product>> SearchProductsAsync(string query)
    {
        return await _repository.SearchAsync(query);
    }
}
```

### After
```csharp
// CachingProductCatalogDecorator.cs
public class CachingProductCatalogDecorator : IProductCatalog
{
    private readonly IProductCatalog _inner;
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);

    public CachingProductCatalogDecorator(
        IProductCatalog inner,
        IDistributedCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<Product> GetProductAsync(string sku)
    {
        var cacheKey = $"product:{sku}";

        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<Product>(cached);
        }

        var product = await _inner.GetProductAsync(sku);

        if (product != null)
        {
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(product),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                });
        }

        return product;
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        var cacheKey = $"products:category:{category}";

        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<IEnumerable<Product>>(cached);
        }

        var products = await _inner.GetProductsByCategoryAsync(category);
        var productList = products.ToList();

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(productList),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheDuration
            });

        return productList;
    }

    public async Task<IEnumerable<Product>> SearchProductsAsync(string query)
    {
        // Don't cache search results - too many variations
        return await _inner.SearchProductsAsync(query);
    }
}

// Stack multiple decorators
services.AddScoped<ProductCatalog>();
services.AddScoped<IProductCatalog>(sp =>
    new LoggingProductCatalogDecorator(           // Outer: Logging
        new CachingProductCatalogDecorator(        // Inner: Caching
            sp.GetRequiredService<ProductCatalog>(), // Core: Real implementation
            sp.GetRequiredService<IDistributedCache>()),
        sp.GetRequiredService<ILogger<LoggingProductCatalogDecorator>>()));
```

---

## Example 3: Add Validation Decorator

### Before
```csharp
public interface IUserRegistration
{
    Task<User> RegisterAsync(RegistrationRequest request);
}

public class UserRegistration : IUserRegistration
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public async Task<User> RegisterAsync(RegistrationRequest request)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Username = request.Username,
            PasswordHash = _hasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        await _users.CreateAsync(user);
        return user;
    }
}
```

### After
```csharp
// ValidatingUserRegistrationDecorator.cs
public class ValidatingUserRegistrationDecorator : IUserRegistration
{
    private readonly IUserRegistration _inner;
    private readonly IUserRepository _users;

    public ValidatingUserRegistrationDecorator(
        IUserRegistration inner,
        IUserRepository users)
    {
        _inner = inner;
        _users = users;
    }

    public async Task<User> RegisterAsync(RegistrationRequest request)
    {
        // Validate email format
        if (!IsValidEmail(request.Email))
        {
            throw new ValidationException("Invalid email format");
        }

        // Validate password strength
        var passwordErrors = ValidatePassword(request.Password);
        if (passwordErrors.Any())
        {
            throw new ValidationException(string.Join("; ", passwordErrors));
        }

        // Check for duplicate email
        var existingUser = await _users.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new ValidationException("Email is already registered");
        }

        // Check for duplicate username
        existingUser = await _users.FindByUsernameAsync(request.Username);
        if (existingUser != null)
        {
            throw new ValidationException("Username is already taken");
        }

        // All validations passed - delegate to inner implementation
        return await _inner.RegisterAsync(request);
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private List<string> ValidatePassword(string password)
    {
        var errors = new List<string>();

        if (password.Length < 8)
            errors.Add("Password must be at least 8 characters");
        if (!password.Any(char.IsUpper))
            errors.Add("Password must contain an uppercase letter");
        if (!password.Any(char.IsLower))
            errors.Add("Password must contain a lowercase letter");
        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain a number");

        return errors;
    }
}

// Compose decorators
services.AddScoped<IUserRegistration>(sp =>
    new LoggingDecorator<IUserRegistration>(              // Logging
        new ValidatingUserRegistrationDecorator(           // Validation
            new UserRegistration(                          // Core logic
                sp.GetRequiredService<IUserRepository>(),
                sp.GetRequiredService<IPasswordHasher>()),
            sp.GetRequiredService<IUserRepository>()),
        sp.GetRequiredService<ILogger>()));
```

---

## Benefits
1. **Open/Closed Principle**: Add behavior without modifying existing code
2. **Single Responsibility**: Each decorator has one concern
3. **Composability**: Stack multiple decorators for combined behaviors
4. **Runtime Flexibility**: Choose decorators at configuration time
5. **Testability**: Test each decorator in isolation
