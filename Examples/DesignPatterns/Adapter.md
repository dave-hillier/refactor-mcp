# Create Adapter Refactoring

## Overview
The `create-adapter` refactoring generates an adapter class that wraps an existing class, transforming its interface to match what clients expect. This is useful for integrating incompatible interfaces.

## When to Use
- When integrating a third-party library with a different interface
- When you can't modify the adaptee class
- When you need to translate between different API styles
- When wrapping legacy code with a modern interface

---

## Example 1: Adapt Third-Party Payment Gateway

### Before (Incompatible third-party SDK)
```csharp
// Third-party SDK - can't modify
namespace ThirdParty.Payments
{
    public class StripePaymentProcessor
    {
        public StripePaymentProcessor(string apiKey, string merchantId) { }

        public StripeChargeResult CreateCharge(
            long amountInCents,
            string currencyCode,
            StripeCardDetails card,
            Dictionary<string, string> metadata)
        {
            // Third-party implementation
        }

        public StripeRefundResult IssueRefund(
            string chargeId,
            long? amountInCents = null)
        {
            // Third-party implementation
        }

        public StripeChargeResult RetrieveCharge(string chargeId)
        {
            // Third-party implementation
        }
    }

    public class StripeCardDetails
    {
        public string Number { get; set; }
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string Cvc { get; set; }
    }

    public class StripeChargeResult
    {
        public string Id { get; set; }
        public string Status { get; set; }  // "succeeded", "pending", "failed"
        public long Amount { get; set; }
        public string FailureCode { get; set; }
        public string FailureMessage { get; set; }
    }
}

// Our application's interface
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request);
    Task<RefundResult> RefundAsync(string transactionId, decimal? amount = null);
    Task<PaymentStatus> GetStatusAsync(string transactionId);
}

public record PaymentRequest(
    decimal Amount,
    string Currency,
    CreditCard Card,
    string OrderId);

public record CreditCard(
    string Number,
    int ExpiryMonth,
    int ExpiryYear,
    string Cvv);

public record PaymentResult(
    bool Success,
    string TransactionId,
    string ErrorCode,
    string ErrorMessage);
```

### After
```csharp
// StripePaymentGatewayAdapter.cs
public class StripePaymentGatewayAdapter : IPaymentGateway
{
    private readonly StripePaymentProcessor _stripe;

    public StripePaymentGatewayAdapter(string apiKey, string merchantId)
    {
        _stripe = new StripePaymentProcessor(apiKey, merchantId);
    }

    public Task<PaymentResult> ChargeAsync(PaymentRequest request)
    {
        // Convert from our domain to Stripe's format
        var stripeCard = new StripeCardDetails
        {
            Number = request.Card.Number,
            ExpiryMonth = request.Card.ExpiryMonth,
            ExpiryYear = request.Card.ExpiryYear,
            Cvc = request.Card.Cvv
        };

        var amountInCents = (long)(request.Amount * 100);

        var metadata = new Dictionary<string, string>
        {
            ["orderId"] = request.OrderId
        };

        // Call the third-party SDK
        var stripeResult = _stripe.CreateCharge(
            amountInCents,
            request.Currency,
            stripeCard,
            metadata);

        // Convert back to our domain
        var result = new PaymentResult(
            Success: stripeResult.Status == "succeeded",
            TransactionId: stripeResult.Id,
            ErrorCode: stripeResult.FailureCode,
            ErrorMessage: stripeResult.FailureMessage);

        return Task.FromResult(result);
    }

    public Task<RefundResult> RefundAsync(string transactionId, decimal? amount = null)
    {
        var amountInCents = amount.HasValue
            ? (long?)(amount.Value * 100)
            : null;

        var stripeResult = _stripe.IssueRefund(transactionId, amountInCents);

        var result = new RefundResult(
            Success: stripeResult.Status == "succeeded",
            RefundId: stripeResult.Id);

        return Task.FromResult(result);
    }

    public Task<PaymentStatus> GetStatusAsync(string transactionId)
    {
        var stripeResult = _stripe.RetrieveCharge(transactionId);

        var status = stripeResult.Status switch
        {
            "succeeded" => PaymentStatus.Completed,
            "pending" => PaymentStatus.Pending,
            "failed" => PaymentStatus.Failed,
            _ => PaymentStatus.Unknown
        };

        return Task.FromResult(status);
    }
}

// DI Registration
services.AddSingleton<IPaymentGateway>(sp =>
    new StripePaymentGatewayAdapter(
        configuration["Stripe:ApiKey"],
        configuration["Stripe:MerchantId"]));
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json create-adapter '{
    "solutionPath": "MyApp.sln",
    "filePath": "ThirdParty/StripePaymentProcessor.cs",
    "methodName": "CreateCharge",
    "adapterClassName": "StripePaymentGatewayAdapter"
}'
```

---

## Example 2: Adapt Legacy Code

### Before (Legacy code with procedural interface)
```csharp
// Legacy library from 15 years ago
public static class LegacyReportEngine
{
    public static int InitializeReport(string templatePath, out IntPtr handle)
    {
        // Returns 0 on success, error code otherwise
        handle = IntPtr.Zero;
        // Legacy implementation
        return 0;
    }

    public static int SetParameter(IntPtr handle, string name, object value)
    {
        // Returns 0 on success
        return 0;
    }

    public static int GenerateReport(IntPtr handle, string outputPath)
    {
        // Returns 0 on success
        return 0;
    }

    public static int CloseReport(IntPtr handle)
    {
        // Cleanup
        return 0;
    }

    public static string GetErrorMessage(int errorCode)
    {
        return errorCode switch
        {
            1 => "Template not found",
            2 => "Invalid parameter",
            3 => "Generation failed",
            _ => "Unknown error"
        };
    }
}

// What we want
public interface IReportGenerator
{
    Task<ReportResult> GenerateAsync(
        string templateName,
        Dictionary<string, object> parameters,
        ReportFormat format);
}
```

### After
```csharp
// LegacyReportAdapter.cs
public class LegacyReportAdapter : IReportGenerator, IDisposable
{
    private readonly string _templateDirectory;
    private readonly string _outputDirectory;
    private IntPtr _currentHandle = IntPtr.Zero;

    public LegacyReportAdapter(string templateDirectory, string outputDirectory)
    {
        _templateDirectory = templateDirectory;
        _outputDirectory = outputDirectory;
    }

    public async Task<ReportResult> GenerateAsync(
        string templateName,
        Dictionary<string, object> parameters,
        ReportFormat format)
    {
        var templatePath = Path.Combine(_templateDirectory, $"{templateName}.rpt");
        var outputPath = Path.Combine(
            _outputDirectory,
            $"{templateName}_{DateTime.UtcNow:yyyyMMddHHmmss}.{GetExtension(format)}");

        // Run legacy code on thread pool to not block
        return await Task.Run(() =>
        {
            // Initialize
            var result = LegacyReportEngine.InitializeReport(templatePath, out var handle);
            if (result != 0)
            {
                return new ReportResult
                {
                    Success = false,
                    ErrorMessage = LegacyReportEngine.GetErrorMessage(result)
                };
            }

            _currentHandle = handle;

            try
            {
                // Set parameters
                foreach (var (name, value) in parameters)
                {
                    result = LegacyReportEngine.SetParameter(handle, name, value);
                    if (result != 0)
                    {
                        return new ReportResult
                        {
                            Success = false,
                            ErrorMessage = $"Parameter '{name}': {LegacyReportEngine.GetErrorMessage(result)}"
                        };
                    }
                }

                // Generate
                result = LegacyReportEngine.GenerateReport(handle, outputPath);
                if (result != 0)
                {
                    return new ReportResult
                    {
                        Success = false,
                        ErrorMessage = LegacyReportEngine.GetErrorMessage(result)
                    };
                }

                return new ReportResult
                {
                    Success = true,
                    FilePath = outputPath,
                    Format = format
                };
            }
            finally
            {
                LegacyReportEngine.CloseReport(handle);
                _currentHandle = IntPtr.Zero;
            }
        });
    }

    private string GetExtension(ReportFormat format) => format switch
    {
        ReportFormat.Pdf => "pdf",
        ReportFormat.Excel => "xlsx",
        ReportFormat.Csv => "csv",
        _ => "txt"
    };

    public void Dispose()
    {
        if (_currentHandle != IntPtr.Zero)
        {
            LegacyReportEngine.CloseReport(_currentHandle);
            _currentHandle = IntPtr.Zero;
        }
    }
}

// Clean, modern usage
var report = await _reportGenerator.GenerateAsync(
    "monthly-sales",
    new Dictionary<string, object>
    {
        ["StartDate"] = startDate,
        ["EndDate"] = endDate,
        ["Region"] = "North America"
    },
    ReportFormat.Pdf);

if (report.Success)
{
    await _emailService.SendReportAsync(recipient, report.FilePath);
}
```

---

## Example 3: Adapt Different Data Formats

### Before
```csharp
// External API returns XML
public class ExternalInventoryApi
{
    public string GetInventoryLevels(string warehouseId)
    {
        return @"
            <inventory>
                <item sku='ABC123' quantity='50' reserved='10' />
                <item sku='XYZ789' quantity='25' reserved='5' />
            </inventory>";
    }

    public string UpdateQuantity(string sku, int adjustment, string reason)
    {
        return @"<result status='success' newQuantity='45' />";
    }
}

// Our system uses strongly-typed objects
public interface IInventoryService
{
    Task<IEnumerable<InventoryLevel>> GetLevelsAsync(string warehouseId);
    Task<InventoryUpdateResult> AdjustQuantityAsync(string sku, int adjustment, string reason);
}

public record InventoryLevel(string Sku, int Available, int Reserved, int Total);
public record InventoryUpdateResult(bool Success, int NewQuantity, string ErrorMessage);
```

### After
```csharp
// ExternalInventoryAdapter.cs
public class ExternalInventoryAdapter : IInventoryService
{
    private readonly ExternalInventoryApi _api;

    public ExternalInventoryAdapter(ExternalInventoryApi api)
    {
        _api = api;
    }

    public Task<IEnumerable<InventoryLevel>> GetLevelsAsync(string warehouseId)
    {
        var xml = _api.GetInventoryLevels(warehouseId);
        var doc = XDocument.Parse(xml);

        var levels = doc.Descendants("item")
            .Select(item => new InventoryLevel(
                Sku: item.Attribute("sku")?.Value ?? "",
                Available: int.Parse(item.Attribute("quantity")?.Value ?? "0") -
                          int.Parse(item.Attribute("reserved")?.Value ?? "0"),
                Reserved: int.Parse(item.Attribute("reserved")?.Value ?? "0"),
                Total: int.Parse(item.Attribute("quantity")?.Value ?? "0")))
            .ToList();

        return Task.FromResult<IEnumerable<InventoryLevel>>(levels);
    }

    public Task<InventoryUpdateResult> AdjustQuantityAsync(
        string sku,
        int adjustment,
        string reason)
    {
        var xml = _api.UpdateQuantity(sku, adjustment, reason);
        var doc = XDocument.Parse(xml);

        var resultElement = doc.Root;
        var status = resultElement?.Attribute("status")?.Value;
        var newQuantity = int.Parse(resultElement?.Attribute("newQuantity")?.Value ?? "0");
        var error = resultElement?.Attribute("error")?.Value;

        var result = new InventoryUpdateResult(
            Success: status == "success",
            NewQuantity: newQuantity,
            ErrorMessage: error);

        return Task.FromResult(result);
    }
}

// Rest of application uses clean interface
var levels = await _inventoryService.GetLevelsAsync("warehouse-1");
foreach (var level in levels.Where(l => l.Available < 10))
{
    Console.WriteLine($"Low stock alert: {level.Sku} has only {level.Available} available");
}
```

---

## Benefits
1. **Integration**: Connect incompatible interfaces without modifying either
2. **Isolation**: Shield your code from third-party API changes
3. **Translation**: Convert between different data formats or conventions
4. **Testability**: Mock the adapter interface instead of the third-party code
5. **Single Responsibility**: Adaptation logic is isolated in one place
