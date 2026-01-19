# Introduce Field Refactoring

## Overview
The `introduce-field` refactoring extracts an expression into a new class field, allowing the value to be shared across methods and maintained as class state.

## When to Use
- When a value needs to be shared across multiple methods
- When a computed value should be cached at the instance level
- When converting local configuration to class-level settings
- When preparing to extract a class (field becomes a candidate for extraction)

---

## Example 1: Extract Repeated Configuration Values

### Before
```csharp
public class EmailNotificationService
{
    private readonly IEmailClient _emailClient;
    private readonly ITemplateEngine _templateEngine;

    public async Task SendOrderConfirmationAsync(Order order)
    {
        var from = "noreply@mystore.com";
        var subject = $"Order Confirmation - #{order.OrderNumber}";

        var template = await _templateEngine.RenderAsync("order-confirmation", order);
        await _emailClient.SendAsync(from, order.Customer.Email, subject, template);
    }

    public async Task SendShippingNotificationAsync(Shipment shipment)
    {
        var from = "noreply@mystore.com";
        var subject = $"Your Order Has Shipped - #{shipment.OrderNumber}";

        var template = await _templateEngine.RenderAsync("shipping-notification", shipment);
        await _emailClient.SendAsync(from, shipment.Customer.Email, subject, template);
    }

    public async Task SendDeliveryConfirmationAsync(Delivery delivery)
    {
        var from = "noreply@mystore.com";
        var subject = $"Delivery Confirmation - #{delivery.OrderNumber}";

        var template = await _templateEngine.RenderAsync("delivery-confirmation", delivery);
        await _emailClient.SendAsync(from, delivery.Customer.Email, subject, template);
    }

    public async Task SendReturnLabelAsync(Return returnRequest)
    {
        var from = "noreply@mystore.com";
        var subject = $"Return Label - #{returnRequest.OrderNumber}";

        var template = await _templateEngine.RenderAsync("return-label", returnRequest);
        await _emailClient.SendAsync(from, returnRequest.Customer.Email, subject, template);
    }
}
```

### After
```csharp
public class EmailNotificationService
{
    private readonly IEmailClient _emailClient;
    private readonly ITemplateEngine _templateEngine;
    private readonly string _senderAddress = "noreply@mystore.com";

    public async Task SendOrderConfirmationAsync(Order order)
    {
        var subject = $"Order Confirmation - #{order.OrderNumber}";

        var template = await _templateEngine.RenderAsync("order-confirmation", order);
        await _emailClient.SendAsync(_senderAddress, order.Customer.Email, subject, template);
    }

    public async Task SendShippingNotificationAsync(Shipment shipment)
    {
        var subject = $"Your Order Has Shipped - #{shipment.OrderNumber}";

        var template = await _templateEngine.RenderAsync("shipping-notification", shipment);
        await _emailClient.SendAsync(_senderAddress, shipment.Customer.Email, subject, template);
    }

    public async Task SendDeliveryConfirmationAsync(Delivery delivery)
    {
        var subject = $"Delivery Confirmation - #{delivery.OrderNumber}";

        var template = await _templateEngine.RenderAsync("delivery-confirmation", delivery);
        await _emailClient.SendAsync(_senderAddress, delivery.Customer.Email, subject, template);
    }

    public async Task SendReturnLabelAsync(Return returnRequest)
    {
        var subject = $"Return Label - #{returnRequest.OrderNumber}";

        var template = await _templateEngine.RenderAsync("return-label", returnRequest);
        await _emailClient.SendAsync(_senderAddress, returnRequest.Customer.Email, subject, template);
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-field '{
    "filePath": "EmailNotificationService.cs",
    "startLine": 9,
    "startColumn": 20,
    "endLine": 9,
    "endColumn": 42,
    "fieldName": "_senderAddress",
    "accessModifier": "private"
}'
```

---

## Example 2: Extract Computed Configuration

### Before
```csharp
public class RateLimiter
{
    private readonly ICacheProvider _cache;
    private readonly ILogger<RateLimiter> _logger;

    public async Task<bool> TryAcquireAsync(string clientId, string endpoint)
    {
        // Magic numbers scattered throughout
        var key = $"rate-limit:{clientId}:{endpoint}";
        var currentCount = await _cache.GetAsync<int>(key);

        if (currentCount >= 100) // Max requests
        {
            _logger.LogWarning("Rate limit exceeded for {ClientId} on {Endpoint}", clientId, endpoint);
            return false;
        }

        await _cache.SetAsync(key, currentCount + 1, TimeSpan.FromMinutes(1));
        return true;
    }

    public async Task<RateLimitStatus> GetStatusAsync(string clientId, string endpoint)
    {
        var key = $"rate-limit:{clientId}:{endpoint}";
        var currentCount = await _cache.GetAsync<int>(key);

        return new RateLimitStatus
        {
            CurrentCount = currentCount,
            MaxRequests = 100,
            WindowMinutes = 1,
            RemainingRequests = Math.Max(0, 100 - currentCount)
        };
    }

    public async Task ResetAsync(string clientId, string endpoint)
    {
        var key = $"rate-limit:{clientId}:{endpoint}";
        await _cache.RemoveAsync(key);
        _logger.LogInformation("Rate limit reset for {ClientId} on {Endpoint}", clientId, endpoint);
    }
}
```

### After
```csharp
public class RateLimiter
{
    private readonly ICacheProvider _cache;
    private readonly ILogger<RateLimiter> _logger;

    private readonly int _maxRequestsPerWindow = 100;
    private readonly TimeSpan _windowDuration = TimeSpan.FromMinutes(1);

    public async Task<bool> TryAcquireAsync(string clientId, string endpoint)
    {
        var key = BuildRateLimitKey(clientId, endpoint);
        var currentCount = await _cache.GetAsync<int>(key);

        if (currentCount >= _maxRequestsPerWindow)
        {
            _logger.LogWarning("Rate limit exceeded for {ClientId} on {Endpoint}", clientId, endpoint);
            return false;
        }

        await _cache.SetAsync(key, currentCount + 1, _windowDuration);
        return true;
    }

    public async Task<RateLimitStatus> GetStatusAsync(string clientId, string endpoint)
    {
        var key = BuildRateLimitKey(clientId, endpoint);
        var currentCount = await _cache.GetAsync<int>(key);

        return new RateLimitStatus
        {
            CurrentCount = currentCount,
            MaxRequests = _maxRequestsPerWindow,
            WindowMinutes = (int)_windowDuration.TotalMinutes,
            RemainingRequests = Math.Max(0, _maxRequestsPerWindow - currentCount)
        };
    }

    public async Task ResetAsync(string clientId, string endpoint)
    {
        var key = BuildRateLimitKey(clientId, endpoint);
        await _cache.RemoveAsync(key);
        _logger.LogInformation("Rate limit reset for {ClientId} on {Endpoint}", clientId, endpoint);
    }

    private string BuildRateLimitKey(string clientId, string endpoint)
        => $"rate-limit:{clientId}:{endpoint}";
}
```

---

## Example 3: Extract Expensive Computation for Caching

### Before
```csharp
public class DocumentProcessor
{
    private readonly IFileSystem _fileSystem;

    public DocumentAnalysis AnalyzeDocument(string documentPath)
    {
        var content = _fileSystem.ReadAllText(documentPath);

        // Expensive regex patterns created on every call
        var emailPattern = new Regex(
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled);

        var phonePattern = new Regex(
            @"(\+\d{1,3}[-.]?)?\(?\d{3}\)?[-.]?\d{3}[-.]?\d{4}",
            RegexOptions.Compiled);

        var urlPattern = new Regex(
            @"https?://[^\s<>""{}|\\^`\[\]]+",
            RegexOptions.Compiled);

        var ssnPattern = new Regex(
            @"\d{3}-\d{2}-\d{4}",
            RegexOptions.Compiled);

        return new DocumentAnalysis
        {
            Emails = emailPattern.Matches(content).Select(m => m.Value).ToList(),
            PhoneNumbers = phonePattern.Matches(content).Select(m => m.Value).ToList(),
            Urls = urlPattern.Matches(content).Select(m => m.Value).ToList(),
            ContainsSensitiveData = ssnPattern.IsMatch(content),
            WordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
        };
    }

    public bool ContainsPII(string documentPath)
    {
        var content = _fileSystem.ReadAllText(documentPath);

        // Same patterns recreated again!
        var emailPattern = new Regex(
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled);

        var ssnPattern = new Regex(
            @"\d{3}-\d{2}-\d{4}",
            RegexOptions.Compiled);

        return emailPattern.IsMatch(content) || ssnPattern.IsMatch(content);
    }
}
```

### After
```csharp
public class DocumentProcessor
{
    private readonly IFileSystem _fileSystem;

    private static readonly Regex _emailPattern = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex _phonePattern = new(
        @"(\+\d{1,3}[-.]?)?\(?\d{3}\)?[-.]?\d{3}[-.]?\d{4}",
        RegexOptions.Compiled);

    private static readonly Regex _urlPattern = new(
        @"https?://[^\s<>""{}|\\^`\[\]]+",
        RegexOptions.Compiled);

    private static readonly Regex _ssnPattern = new(
        @"\d{3}-\d{2}-\d{4}",
        RegexOptions.Compiled);

    public DocumentAnalysis AnalyzeDocument(string documentPath)
    {
        var content = _fileSystem.ReadAllText(documentPath);

        return new DocumentAnalysis
        {
            Emails = _emailPattern.Matches(content).Select(m => m.Value).ToList(),
            PhoneNumbers = _phonePattern.Matches(content).Select(m => m.Value).ToList(),
            Urls = _urlPattern.Matches(content).Select(m => m.Value).ToList(),
            ContainsSensitiveData = _ssnPattern.IsMatch(content),
            WordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
        };
    }

    public bool ContainsPII(string documentPath)
    {
        var content = _fileSystem.ReadAllText(documentPath);

        return _emailPattern.IsMatch(content) || _ssnPattern.IsMatch(content);
    }
}
```

---

## Benefits
1. **DRY Principle**: Eliminates duplicate literal values
2. **Configurability**: Makes values easier to change in one place
3. **Performance**: Caches expensive object creation (like compiled Regex)
4. **Testability**: Fields can be made accessible for testing
5. **Preparation for DI**: Fields are stepping stones to constructor injection
