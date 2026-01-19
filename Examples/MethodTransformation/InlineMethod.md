# Inline Method Refactoring

## Overview
The `inline-method` refactoring replaces all calls to a method with the method's body, then removes the method declaration. This is the inverse of Extract Method.

## When to Use
- When a method's body is as clear as its name
- When the method is only called once and adds indirection
- When you need to refactor and the method boundaries are getting in the way
- When a method is trivially simple and the abstraction adds no value

---

## Example 1: Inline Simple Delegation Methods

### Before
```csharp
public class CustomerService
{
    private readonly ICustomerRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ILogger _logger;

    public async Task<Customer> GetCustomerAsync(int id)
    {
        return await FindCustomerByIdAsync(id);
    }

    public async Task SendWelcomeEmailAsync(Customer customer)
    {
        await DispatchWelcomeEmailAsync(customer);
    }

    public bool IsCustomerActive(Customer customer)
    {
        return CheckCustomerActiveStatus(customer);
    }

    // These methods are overly abstracted - they just wrap simple calls
    private async Task<Customer> FindCustomerByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    private async Task DispatchWelcomeEmailAsync(Customer customer)
    {
        var template = EmailTemplates.Welcome;
        await _emailService.SendAsync(customer.Email, template, new { Name = customer.Name });
    }

    private bool CheckCustomerActiveStatus(Customer customer)
    {
        return customer.Status == CustomerStatus.Active && !customer.IsDeleted;
    }
}
```

### After
```csharp
public class CustomerService
{
    private readonly ICustomerRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ILogger _logger;

    public async Task<Customer> GetCustomerAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task SendWelcomeEmailAsync(Customer customer)
    {
        var template = EmailTemplates.Welcome;
        await _emailService.SendAsync(customer.Email, template, new { Name = customer.Name });
    }

    public bool IsCustomerActive(Customer customer)
    {
        return customer.Status == CustomerStatus.Active && !customer.IsDeleted;
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json inline-method '{
    "filePath": "CustomerService.cs",
    "methodName": "FindCustomerByIdAsync"
}'
```

---

## Example 2: Inline Before Restructuring

### Before
```csharp
public class ReportGenerator
{
    private readonly IDataSource _dataSource;

    public Report GenerateMonthlyReport(int month, int year)
    {
        var data = FetchData(month, year);
        var processedData = ProcessData(data);
        var formattedData = FormatData(processedData);
        return CreateReport(formattedData);
    }

    private RawData FetchData(int month, int year)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        return _dataSource.Query(startDate, endDate);
    }

    private ProcessedData ProcessData(RawData data)
    {
        // This is called only once and the name doesn't add clarity
        return new ProcessedData
        {
            TotalRevenue = data.Transactions.Sum(t => t.Amount),
            TransactionCount = data.Transactions.Count,
            AverageTransactionValue = data.Transactions.Average(t => t.Amount),
            TopCategories = data.Transactions
                .GroupBy(t => t.Category)
                .OrderByDescending(g => g.Sum(t => t.Amount))
                .Take(5)
                .Select(g => new CategorySummary
                {
                    Name = g.Key,
                    Total = g.Sum(t => t.Amount)
                })
                .ToList()
        };
    }

    private FormattedData FormatData(ProcessedData data)
    {
        // Another single-use method
        return new FormattedData
        {
            Revenue = $"${data.TotalRevenue:N2}",
            Count = data.TransactionCount.ToString("N0"),
            Average = $"${data.AverageTransactionValue:N2}",
            Categories = data.TopCategories
                .Select(c => $"{c.Name}: ${c.Total:N2}")
                .ToList()
        };
    }

    private Report CreateReport(FormattedData data)
    {
        return new Report { Data = data, GeneratedAt = DateTime.UtcNow };
    }
}
```

### After (Inlined to enable better restructuring)
```csharp
public class ReportGenerator
{
    private readonly IDataSource _dataSource;

    public Report GenerateMonthlyReport(int month, int year)
    {
        // Fetch data
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        var data = _dataSource.Query(startDate, endDate);

        // Process data
        var processedData = new ProcessedData
        {
            TotalRevenue = data.Transactions.Sum(t => t.Amount),
            TransactionCount = data.Transactions.Count,
            AverageTransactionValue = data.Transactions.Average(t => t.Amount),
            TopCategories = data.Transactions
                .GroupBy(t => t.Category)
                .OrderByDescending(g => g.Sum(t => t.Amount))
                .Take(5)
                .Select(g => new CategorySummary
                {
                    Name = g.Key,
                    Total = g.Sum(t => t.Amount)
                })
                .ToList()
        };

        // Format data
        var formattedData = new FormattedData
        {
            Revenue = $"${processedData.TotalRevenue:N2}",
            Count = processedData.TransactionCount.ToString("N0"),
            Average = $"${processedData.AverageTransactionValue:N2}",
            Categories = processedData.TopCategories
                .Select(c => $"{c.Name}: ${c.Total:N2}")
                .ToList()
        };

        // Create report
        return new Report { Data = formattedData, GeneratedAt = DateTime.UtcNow };
    }
}
```

---

## Example 3: Inline Premature Abstraction

### Before
```csharp
public class ShoppingCart
{
    private readonly List<CartItem> _items = new();

    public void AddItem(Product product, int quantity)
    {
        ValidateProduct(product);
        ValidateQuantity(quantity);

        var existingItem = FindExistingItem(product);
        if (existingItem != null)
        {
            UpdateExistingItemQuantity(existingItem, quantity);
        }
        else
        {
            AddNewItem(product, quantity);
        }

        RecalculateTotals();
    }

    private void ValidateProduct(Product product)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
    }

    private void ValidateQuantity(int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive", nameof(quantity));
    }

    private CartItem FindExistingItem(Product product)
    {
        return _items.FirstOrDefault(i => i.ProductId == product.Id);
    }

    private void UpdateExistingItemQuantity(CartItem item, int additionalQuantity)
    {
        item.Quantity += additionalQuantity;
    }

    private void AddNewItem(Product product, int quantity)
    {
        _items.Add(new CartItem { ProductId = product.Id, Product = product, Quantity = quantity });
    }

    private void RecalculateTotals()
    {
        // Actual calculation logic
    }
}
```

### After
```csharp
public class ShoppingCart
{
    private readonly List<CartItem> _items = new();

    public void AddItem(Product product, int quantity)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive", nameof(quantity));

        var existingItem = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            _items.Add(new CartItem { ProductId = product.Id, Product = product, Quantity = quantity });
        }

        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        // Actual calculation logic
    }
}
```

---

## Benefits
1. **Reduced Indirection**: Code flow is easier to follow
2. **Better Context**: All relevant code is visible together
3. **Enables Further Refactoring**: Often a stepping stone to better structure
4. **Removes Unnecessary Abstraction**: Simplifies over-engineered code
5. **Easier Debugging**: Fewer stack frames to step through

## When NOT to Inline
- When the method is called from multiple places
- When the method name provides valuable documentation
- When the method body is complex and benefits from isolation
- When the method is part of a public API
