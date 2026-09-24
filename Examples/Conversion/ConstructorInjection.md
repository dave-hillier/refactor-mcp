# Constructor Injection Refactoring

## Overview
Constructor injection gives a class its dependencies through its constructor and keeps them in fields, making them available to all methods.

The `inject-constructor-dependency` tool handles the case where a method constructs an object for itself: the object becomes a constructor parameter kept in a `private readonly` field, and every construction of the class passes a new one, built as the method built it. Examples 2 and 3 show the other common shape, dependencies passed into every method as parameters. Moving such a parameter to the constructor changes what each caller supplies, so no tool does it automatically; they show the target design to work towards by hand.

## When to Use
- When multiple methods need the same dependencies
- When transitioning from procedural to object-oriented design
- When implementing dependency injection patterns
- When the dependency lifetime should match the class lifetime

---

## Example 1: Inject an Object the Method Constructs

### Before
```csharp
public class InvoiceService
{
    private readonly IInvoiceRepository _repository;

    public InvoiceService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task SendAsync(int invoiceId)
    {
        var invoice = await _repository.GetAsync(invoiceId);
        var mailer = new SmtpMailer("smtp.example.com");
        await mailer.SendAsync(invoice.CustomerEmail, $"Invoice {invoice.Number}");
    }
}

// Elsewhere
var service = new InvoiceService(repository);
```

### After
```csharp
public class InvoiceService
{
    private readonly IInvoiceRepository _repository;
    private readonly SmtpMailer _mailer;

    public InvoiceService(IInvoiceRepository repository, SmtpMailer mailer)
    {
        _repository = repository;
        _mailer = mailer;
    }

    public async Task SendAsync(int invoiceId)
    {
        var invoice = await _repository.GetAsync(invoiceId);
        await _mailer.SendAsync(invoice.CustomerEmail, $"Invoice {invoice.Number}");
    }
}

// Elsewhere
var service = new InvoiceService(repository, new SmtpMailer("smtp.example.com"));
```

### Tool Usage
`line` and `column` locate the local holding the constructed object, on its declaration or any use. `parameterName` and `fieldName` are optional, defaulting to the local's name and the local's name with a leading underscore.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json inject-constructor-dependency '{
    "solutionPath": "MyProject.sln",
    "filePath": "Services/InvoiceService.cs",
    "line": 13,
    "column": 13
}'
```

The tool refuses when the construction's arguments read the method's state, when the object is built with an object initializer, or when the class has several constructors. One object now serves every call of the method, so an object that keeps state between uses behaves differently.

---

## Example 2: Convert Repeated Parameters to Fields

### Before
```csharp
public class OrderProcessor
{
    public async Task<Order> ProcessNewOrderAsync(
        OrderRequest request,
        IInventoryService inventory,
        IPaymentGateway payments,
        IShippingService shipping,
        INotificationService notifications,
        ILogger logger)
    {
        logger.LogInformation("Processing new order for customer {CustomerId}", request.CustomerId);

        // Validate inventory
        foreach (var item in request.Items)
        {
            if (!await inventory.CheckAvailabilityAsync(item.ProductId, item.Quantity))
            {
                throw new InsufficientInventoryException(item.ProductId);
            }
        }

        // Calculate totals
        var order = CreateOrder(request);

        // Process payment
        var paymentResult = await payments.ChargeAsync(
            request.CustomerId,
            order.Total,
            request.PaymentMethod);

        if (!paymentResult.Success)
        {
            logger.LogWarning("Payment failed for order {OrderId}", order.Id);
            throw new PaymentFailedException(paymentResult.Error);
        }

        // Reserve inventory
        await inventory.ReserveAsync(order);

        // Arrange shipping
        order.ShippingInfo = await shipping.CreateShipmentAsync(order);

        // Send confirmation
        await notifications.SendOrderConfirmationAsync(order);

        logger.LogInformation("Order {OrderId} processed successfully", order.Id);
        return order;
    }

    public async Task CancelOrderAsync(
        Order order,
        IInventoryService inventory,
        IPaymentGateway payments,
        INotificationService notifications,
        ILogger logger)
    {
        logger.LogInformation("Cancelling order {OrderId}", order.Id);

        // Release inventory
        await inventory.ReleaseReservationAsync(order);

        // Refund payment
        await payments.RefundAsync(order.PaymentTransactionId);

        // Notify customer
        await notifications.SendCancellationNoticeAsync(order);

        logger.LogInformation("Order {OrderId} cancelled", order.Id);
    }

    public async Task UpdateShippingAsync(
        Order order,
        Address newAddress,
        IShippingService shipping,
        INotificationService notifications,
        ILogger logger)
    {
        logger.LogInformation("Updating shipping for order {OrderId}", order.Id);

        order.ShippingInfo = await shipping.UpdateDestinationAsync(
            order.ShippingInfo.TrackingNumber,
            newAddress);

        await notifications.SendShippingUpdateAsync(order);
    }

    private Order CreateOrder(OrderRequest request)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Items = request.Items,
            Total = request.Items.Sum(i => i.UnitPrice * i.Quantity),
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

### After
```csharp
public class OrderProcessor
{
    private readonly IInventoryService _inventory;
    private readonly IPaymentGateway _payments;
    private readonly IShippingService _shipping;
    private readonly INotificationService _notifications;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(
        IInventoryService inventory,
        IPaymentGateway payments,
        IShippingService shipping,
        INotificationService notifications,
        ILogger<OrderProcessor> logger)
    {
        _inventory = inventory;
        _payments = payments;
        _shipping = shipping;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Order> ProcessNewOrderAsync(OrderRequest request)
    {
        _logger.LogInformation("Processing new order for customer {CustomerId}", request.CustomerId);

        // Validate inventory
        foreach (var item in request.Items)
        {
            if (!await _inventory.CheckAvailabilityAsync(item.ProductId, item.Quantity))
            {
                throw new InsufficientInventoryException(item.ProductId);
            }
        }

        // Calculate totals
        var order = CreateOrder(request);

        // Process payment
        var paymentResult = await _payments.ChargeAsync(
            request.CustomerId,
            order.Total,
            request.PaymentMethod);

        if (!paymentResult.Success)
        {
            _logger.LogWarning("Payment failed for order {OrderId}", order.Id);
            throw new PaymentFailedException(paymentResult.Error);
        }

        // Reserve inventory
        await _inventory.ReserveAsync(order);

        // Arrange shipping
        order.ShippingInfo = await _shipping.CreateShipmentAsync(order);

        // Send confirmation
        await _notifications.SendOrderConfirmationAsync(order);

        _logger.LogInformation("Order {OrderId} processed successfully", order.Id);
        return order;
    }

    public async Task CancelOrderAsync(Order order)
    {
        _logger.LogInformation("Cancelling order {OrderId}", order.Id);

        // Release inventory
        await _inventory.ReleaseReservationAsync(order);

        // Refund payment
        await _payments.RefundAsync(order.PaymentTransactionId);

        // Notify customer
        await _notifications.SendCancellationNoticeAsync(order);

        _logger.LogInformation("Order {OrderId} cancelled", order.Id);
    }

    public async Task UpdateShippingAsync(Order order, Address newAddress)
    {
        _logger.LogInformation("Updating shipping for order {OrderId}", order.Id);

        order.ShippingInfo = await _shipping.UpdateDestinationAsync(
            order.ShippingInfo.TrackingNumber,
            newAddress);

        await _notifications.SendShippingUpdateAsync(order);
    }

    private Order CreateOrder(OrderRequest request)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Items = request.Items,
            Total = request.Items.Sum(i => i.UnitPrice * i.Quantity),
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

---

## Example 3: Convert Static Helper to Injectable Service

### Before
```csharp
public static class EmailHelper
{
    public static async Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        ISmtpClient smtpClient,
        ITemplateEngine templates,
        EmailConfiguration config)
    {
        var message = new EmailMessage
        {
            From = config.DefaultFromAddress,
            To = to,
            Subject = subject,
            Body = await templates.RenderAsync("email-layout", new { Content = body })
        };

        return await smtpClient.SendAsync(message, config.SmtpSettings);
    }

    public static async Task<bool> SendTemplatedAsync<T>(
        string to,
        string templateName,
        T model,
        ISmtpClient smtpClient,
        ITemplateEngine templates,
        EmailConfiguration config)
    {
        var subject = await templates.RenderAsync($"{templateName}-subject", model);
        var body = await templates.RenderAsync($"{templateName}-body", model);

        return await SendAsync(to, subject, body, smtpClient, templates, config);
    }

    public static async Task<BulkSendResult> SendBulkAsync(
        IEnumerable<string> recipients,
        string subject,
        string body,
        ISmtpClient smtpClient,
        ITemplateEngine templates,
        EmailConfiguration config,
        int batchSize = 50)
    {
        var result = new BulkSendResult();
        var batches = recipients.Chunk(batchSize);

        foreach (var batch in batches)
        {
            var tasks = batch.Select(to =>
                SendAsync(to, subject, body, smtpClient, templates, config));

            var results = await Task.WhenAll(tasks);
            result.Successful += results.Count(r => r);
            result.Failed += results.Count(r => !r);
        }

        return result;
    }
}

// Usage is verbose
await EmailHelper.SendTemplatedAsync(
    customer.Email,
    "welcome",
    new { Name = customer.Name },
    smtpClient,
    templateEngine,
    emailConfig);
```

### After
```csharp
public class EmailService : IEmailService
{
    private readonly ISmtpClient _smtpClient;
    private readonly ITemplateEngine _templates;
    private readonly EmailConfiguration _config;

    public EmailService(
        ISmtpClient smtpClient,
        ITemplateEngine templates,
        EmailConfiguration config)
    {
        _smtpClient = smtpClient;
        _templates = templates;
        _config = config;
    }

    public async Task<bool> SendAsync(string to, string subject, string body)
    {
        var message = new EmailMessage
        {
            From = _config.DefaultFromAddress,
            To = to,
            Subject = subject,
            Body = await _templates.RenderAsync("email-layout", new { Content = body })
        };

        return await _smtpClient.SendAsync(message, _config.SmtpSettings);
    }

    public async Task<bool> SendTemplatedAsync<T>(
        string to,
        string templateName,
        T model)
    {
        var subject = await _templates.RenderAsync($"{templateName}-subject", model);
        var body = await _templates.RenderAsync($"{templateName}-body", model);

        return await SendAsync(to, subject, body);
    }

    public async Task<BulkSendResult> SendBulkAsync(
        IEnumerable<string> recipients,
        string subject,
        string body,
        int batchSize = 50)
    {
        var result = new BulkSendResult();
        var batches = recipients.Chunk(batchSize);

        foreach (var batch in batches)
        {
            var tasks = batch.Select(to => SendAsync(to, subject, body));
            var results = await Task.WhenAll(tasks);
            result.Successful += results.Count(r => r);
            result.Failed += results.Count(r => !r);
        }

        return result;
    }
}

// Clean usage with DI
public class CustomerService
{
    private readonly IEmailService _emailService;

    public CustomerService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task WelcomeCustomerAsync(Customer customer)
    {
        await _emailService.SendTemplatedAsync(
            customer.Email,
            "welcome",
            new { Name = customer.Name });
    }
}
```

---

## Benefits
1. **Cleaner Method Signatures**: Methods only receive data, not dependencies
2. **Single Responsibility**: Dependency wiring is in one place
3. **DI Framework Compatible**: Works with ASP.NET Core, Autofac, etc.
4. **Testability**: Dependencies can be mocked at construction time
5. **Reduced Duplication**: Dependencies listed once, not in every method call
