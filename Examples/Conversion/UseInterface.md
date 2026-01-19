# Use Interface Refactoring

## Overview
The `use-interface` refactoring changes a method parameter or field from a concrete class type to an interface type. This promotes the Dependency Inversion Principle and improves testability.

## When to Use
- When a method only uses a subset of a class's capabilities
- When you want to enable substitution with different implementations
- When improving testability by allowing mocks
- When decoupling from specific implementations

---

## Example 1: Enable Mocking for Tests

### Before
```csharp
public class PaymentProcessor
{
    public async Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request,
        CreditCardValidator validator,  // Concrete class
        PaymentGateway gateway,          // Concrete class
        TransactionLogger logger)        // Concrete class
    {
        // Validate the card
        var validationResult = validator.Validate(
            request.CardNumber,
            request.ExpirationDate,
            request.Cvv);

        if (!validationResult.IsValid)
        {
            return new PaymentResult
            {
                Success = false,
                Error = validationResult.ErrorMessage
            };
        }

        // Process the payment
        var transaction = await gateway.ChargeAsync(
            request.Amount,
            request.CardNumber,
            request.ExpirationDate);

        // Log the transaction
        await logger.LogAsync(transaction);

        return new PaymentResult
        {
            Success = transaction.IsApproved,
            TransactionId = transaction.Id,
            Error = transaction.DeclineReason
        };
    }
}

// Testing is hard - requires real implementations
[Fact]
public async Task ProcessPayment_ValidCard_Succeeds()
{
    var processor = new PaymentProcessor();
    // Can't easily test without hitting real services!
    var result = await processor.ProcessPaymentAsync(
        request,
        new CreditCardValidator(),      // Real validator
        new PaymentGateway(realConfig), // Real gateway - hits API!
        new TransactionLogger(realDb)); // Real logger - writes to DB!

    Assert.True(result.Success);
}
```

### After
```csharp
public class PaymentProcessor
{
    public async Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request,
        ICreditCardValidator validator,   // Interface
        IPaymentGateway gateway,           // Interface
        ITransactionLogger logger)         // Interface
    {
        // Validate the card
        var validationResult = validator.Validate(
            request.CardNumber,
            request.ExpirationDate,
            request.Cvv);

        if (!validationResult.IsValid)
        {
            return new PaymentResult
            {
                Success = false,
                Error = validationResult.ErrorMessage
            };
        }

        // Process the payment
        var transaction = await gateway.ChargeAsync(
            request.Amount,
            request.CardNumber,
            request.ExpirationDate);

        // Log the transaction
        await logger.LogAsync(transaction);

        return new PaymentResult
        {
            Success = transaction.IsApproved,
            TransactionId = transaction.Id,
            Error = transaction.DeclineReason
        };
    }
}

// Testing is easy with mocks
[Fact]
public async Task ProcessPayment_ValidCard_Succeeds()
{
    var processor = new PaymentProcessor();

    var mockValidator = new Mock<ICreditCardValidator>();
    mockValidator.Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
        .Returns(new ValidationResult { IsValid = true });

    var mockGateway = new Mock<IPaymentGateway>();
    mockGateway.Setup(g => g.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>()))
        .ReturnsAsync(new Transaction { IsApproved = true, Id = "tx_123" });

    var mockLogger = new Mock<ITransactionLogger>();

    var result = await processor.ProcessPaymentAsync(
        request,
        mockValidator.Object,
        mockGateway.Object,
        mockLogger.Object);

    Assert.True(result.Success);
    Assert.Equal("tx_123", result.TransactionId);
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json use-interface '{
    "solutionPath": "PaymentService.sln",
    "filePath": "Services/PaymentProcessor.cs",
    "methodName": "ProcessPaymentAsync",
    "parameterName": "gateway",
    "interfaceName": "IPaymentGateway"
}'
```

---

## Example 2: Support Multiple Implementations

### Before
```csharp
public class DocumentExporter
{
    private readonly FileSystem _fileSystem;  // Concrete class

    public DocumentExporter(FileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task ExportAsync(Document document, string outputPath)
    {
        var content = document.Format switch
        {
            DocumentFormat.Json => JsonSerializer.Serialize(document),
            DocumentFormat.Xml => SerializeToXml(document),
            DocumentFormat.Csv => SerializeToCsv(document),
            _ => throw new NotSupportedException($"Format {document.Format} not supported")
        };

        // Only works with local file system
        await _fileSystem.WriteAllTextAsync(outputPath, content);
    }

    public async Task<IEnumerable<Document>> ImportFromDirectoryAsync(string directoryPath)
    {
        var files = _fileSystem.GetFiles(directoryPath, "*.json");
        var documents = new List<Document>();

        foreach (var file in files)
        {
            var content = await _fileSystem.ReadAllTextAsync(file);
            documents.Add(JsonSerializer.Deserialize<Document>(content));
        }

        return documents;
    }
}

// Can only use local file system
var exporter = new DocumentExporter(new FileSystem());
```

### After
```csharp
public class DocumentExporter
{
    private readonly IFileSystem _fileSystem;  // Interface

    public DocumentExporter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task ExportAsync(Document document, string outputPath)
    {
        var content = document.Format switch
        {
            DocumentFormat.Json => JsonSerializer.Serialize(document),
            DocumentFormat.Xml => SerializeToXml(document),
            DocumentFormat.Csv => SerializeToCsv(document),
            _ => throw new NotSupportedException($"Format {document.Format} not supported")
        };

        await _fileSystem.WriteAllTextAsync(outputPath, content);
    }

    public async Task<IEnumerable<Document>> ImportFromDirectoryAsync(string directoryPath)
    {
        var files = _fileSystem.GetFiles(directoryPath, "*.json");
        var documents = new List<Document>();

        foreach (var file in files)
        {
            var content = await _fileSystem.ReadAllTextAsync(file);
            documents.Add(JsonSerializer.Deserialize<Document>(content));
        }

        return documents;
    }
}

// Now supports multiple file system implementations
var localExporter = new DocumentExporter(new LocalFileSystem());
var s3Exporter = new DocumentExporter(new S3FileSystem(s3Client, bucketName));
var azureExporter = new DocumentExporter(new AzureBlobFileSystem(blobClient));
var inMemoryExporter = new DocumentExporter(new InMemoryFileSystem()); // For testing
```

---

## Example 3: Depend on Abstraction, Not Details

### Before
```csharp
public class ReportGenerator
{
    private readonly SqlServerDatabase _database;
    private readonly ExcelWriter _excelWriter;
    private readonly SmtpEmailSender _emailSender;

    public ReportGenerator(
        SqlServerDatabase database,
        ExcelWriter excelWriter,
        SmtpEmailSender emailSender)
    {
        _database = database;
        _excelWriter = excelWriter;
        _emailSender = emailSender;
    }

    public async Task GenerateAndSendReportAsync(ReportRequest request)
    {
        // Tightly coupled to SQL Server
        var data = await _database.ExecuteQueryAsync(request.Query);

        // Tightly coupled to Excel format
        var reportBytes = _excelWriter.CreateWorkbook(data);

        // Tightly coupled to SMTP
        await _emailSender.SendWithAttachmentAsync(
            request.RecipientEmail,
            "Your Report",
            "Please find attached your requested report.",
            reportBytes,
            "report.xlsx");
    }
}
```

### After
```csharp
public class ReportGenerator
{
    private readonly IDatabase _database;
    private readonly IReportWriter _reportWriter;
    private readonly IEmailSender _emailSender;

    public ReportGenerator(
        IDatabase database,
        IReportWriter reportWriter,
        IEmailSender emailSender)
    {
        _database = database;
        _reportWriter = reportWriter;
        _emailSender = emailSender;
    }

    public async Task GenerateAndSendReportAsync(ReportRequest request)
    {
        // Works with any database
        var data = await _database.ExecuteQueryAsync(request.Query);

        // Works with any format (Excel, PDF, CSV)
        var reportBytes = _reportWriter.CreateReport(data);

        // Works with any email transport (SMTP, SendGrid, AWS SES)
        await _emailSender.SendWithAttachmentAsync(
            request.RecipientEmail,
            "Your Report",
            "Please find attached your requested report.",
            reportBytes,
            _reportWriter.GetFileName());
    }
}

// Flexible configurations
services.AddScoped<IDatabase, SqlServerDatabase>();      // Production
services.AddScoped<IDatabase, PostgresDatabase>();       // Alternative
services.AddScoped<IDatabase, InMemoryDatabase>();       // Testing

services.AddScoped<IReportWriter, ExcelReportWriter>();  // Excel output
services.AddScoped<IReportWriter, PdfReportWriter>();    // PDF output
services.AddScoped<IReportWriter, CsvReportWriter>();    // CSV output

services.AddScoped<IEmailSender, SmtpEmailSender>();     // Traditional SMTP
services.AddScoped<IEmailSender, SendGridEmailSender>(); // SendGrid API
services.AddScoped<IEmailSender, NullEmailSender>();     // Testing/dev
```

---

## Benefits
1. **Dependency Inversion**: High-level modules don't depend on low-level modules
2. **Testability**: Easy to mock dependencies in unit tests
3. **Flexibility**: Swap implementations without changing consuming code
4. **Loose Coupling**: Changes to implementations don't affect consumers
5. **Open/Closed Principle**: Add new implementations without modifying existing code
