# Convert to Extension Method Refactoring

## Overview
The `convert-to-extension-method` refactoring converts an instance method to an extension method in a static class. This is useful when you want to add functionality that feels like a natural part of a type without modifying the type itself.

## When to Use
- When adding methods to types you don't own (framework types, third-party libraries)
- When a method logically "extends" a type but shouldn't be in the type itself
- When creating fluent APIs
- When moving helper methods from a class without changing call sites

---

## Example 1: Extend Built-in Types

### Before
```csharp
public class StringUtilities
{
    private readonly string _value;

    public StringUtilities(string value)
    {
        _value = value;
    }

    public string TruncateWithEllipsis(int maxLength)
    {
        if (string.IsNullOrEmpty(_value) || _value.Length <= maxLength)
            return _value;

        return _value.Substring(0, maxLength - 3) + "...";
    }

    public string ToTitleCase()
    {
        if (string.IsNullOrEmpty(_value))
            return _value;

        var words = _value.Split(' ');
        var titleCased = words.Select(word =>
            word.Length > 0
                ? char.ToUpper(word[0]) + word.Substring(1).ToLower()
                : word);

        return string.Join(" ", titleCased);
    }

    public string RemoveDiacritics()
    {
        var normalized = _value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public bool IsValidEmail()
    {
        if (string.IsNullOrWhiteSpace(_value))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(_value);
            return addr.Address == _value;
        }
        catch
        {
            return false;
        }
    }
}

// Usage is clunky
var utils = new StringUtilities(userInput);
var display = utils.TruncateWithEllipsis(50);
```

### After
```csharp
public static class StringExtensions
{
    public static string TruncateWithEllipsis(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength - 3) + "...";
    }

    public static string ToTitleCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var words = value.Split(' ');
        var titleCased = words.Select(word =>
            word.Length > 0
                ? char.ToUpper(word[0]) + word.Substring(1).ToLower()
                : word);

        return string.Join(" ", titleCased);
    }

    public static string RemoveDiacritics(this string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool IsValidEmail(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(value);
            return addr.Address == value;
        }
        catch
        {
            return false;
        }
    }
}

// Usage is now natural and chainable
var display = userInput.TruncateWithEllipsis(50);
var isValid = email.IsValidEmail();
var searchable = title.RemoveDiacritics().ToLower();
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-extension-method '{
    "filePath": "Utilities/StringUtilities.cs",
    "methodName": "TruncateWithEllipsis",
    "extensionClassName": "StringExtensions",
    "extensionClassFilePath": "Extensions/StringExtensions.cs"
}'
```

---

## Example 2: Create Fluent API

### Before
```csharp
public class QueryBuilder
{
    private readonly IQueryable<T> _query;

    public QueryBuilder(IQueryable<T> query)
    {
        _query = query;
    }

    public IQueryable<T> WithPagination(int pageNumber, int pageSize)
    {
        return _query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);
    }

    public IQueryable<T> OrderByProperty(string propertyName, bool descending = false)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyName);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        return (IQueryable<T>)method.Invoke(null, new object[] { _query, lambda });
    }

    public IQueryable<T> FilterByDateRange(
        Expression<Func<T, DateTime>> dateSelector,
        DateTime? startDate,
        DateTime? endDate)
    {
        var query = _query;

        if (startDate.HasValue)
        {
            var start = startDate.Value;
            query = query.Where(x => dateSelector.Compile()(x) >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value;
            query = query.Where(x => dateSelector.Compile()(x) <= end);
        }

        return query;
    }
}

// Usage requires wrapper instantiation
var builder = new QueryBuilder<Order>(dbContext.Orders);
var paged = builder.WithPagination(1, 20);
```

### After
```csharp
public static class QueryableExtensions
{
    public static IQueryable<T> WithPagination<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize)
    {
        return query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);
    }

    public static IQueryable<T> OrderByProperty<T>(
        this IQueryable<T> query,
        string propertyName,
        bool descending = false)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyName);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        return (IQueryable<T>)method.Invoke(null, new object[] { query, lambda });
    }

    public static IQueryable<T> FilterByDateRange<T>(
        this IQueryable<T> query,
        Expression<Func<T, DateTime>> dateSelector,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue)
        {
            var start = startDate.Value;
            query = query.Where(x => dateSelector.Compile()(x) >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value;
            query = query.Where(x => dateSelector.Compile()(x) <= end);
        }

        return query;
    }
}

// Fluent, chainable usage
var orders = dbContext.Orders
    .FilterByDateRange(o => o.OrderDate, startDate, endDate)
    .OrderByProperty("Total", descending: true)
    .WithPagination(page, pageSize)
    .ToList();
```

---

## Example 3: Extend Domain Objects

### Before
```csharp
// Money is a value object from a library - can't modify it
public record Money(decimal Amount, string Currency);

// Helper class for Money operations
public class MoneyOperations
{
    private readonly Money _money;

    public MoneyOperations(Money money)
    {
        _money = money;
    }

    public string FormatForDisplay()
    {
        var symbol = _money.Currency switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "JPY" => "¥",
            _ => _money.Currency + " "
        };

        return _money.Currency == "JPY"
            ? $"{symbol}{_money.Amount:N0}"
            : $"{symbol}{_money.Amount:N2}";
    }

    public Money ConvertTo(string targetCurrency, IExchangeRateProvider rates)
    {
        if (_money.Currency == targetCurrency)
            return _money;

        var rate = rates.GetRate(_money.Currency, targetCurrency);
        return new Money(_money.Amount * rate, targetCurrency);
    }

    public bool IsPositive() => _money.Amount > 0;
    public bool IsNegative() => _money.Amount < 0;
    public bool IsZero() => _money.Amount == 0;
}
```

### After
```csharp
public static class MoneyExtensions
{
    public static string FormatForDisplay(this Money money)
    {
        var symbol = money.Currency switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "JPY" => "¥",
            _ => money.Currency + " "
        };

        return money.Currency == "JPY"
            ? $"{symbol}{money.Amount:N0}"
            : $"{symbol}{money.Amount:N2}";
    }

    public static Money ConvertTo(
        this Money money,
        string targetCurrency,
        IExchangeRateProvider rates)
    {
        if (money.Currency == targetCurrency)
            return money;

        var rate = rates.GetRate(money.Currency, targetCurrency);
        return new Money(money.Amount * rate, targetCurrency);
    }

    public static bool IsPositive(this Money money) => money.Amount > 0;
    public static bool IsNegative(this Money money) => money.Amount < 0;
    public static bool IsZero(this Money money) => money.Amount == 0;
}

// Natural, intuitive usage
var price = new Money(99.99m, "USD");
Console.WriteLine(price.FormatForDisplay()); // $99.99

var priceInEuros = price.ConvertTo("EUR", exchangeRates);

if (refund.IsNegative())
{
    ProcessRefund(refund);
}
```

---

## Benefits
1. **Natural Syntax**: Methods feel like they belong to the type
2. **Chainability**: Enables fluent, readable code
3. **No Modification**: Extend types you can't change
4. **Discoverability**: IntelliSense shows extension methods on the type
5. **Organization**: Group related extensions by concern
