# Introduce Parameter Refactoring

## Overview
The `introduce-parameter` refactoring extracts an expression from within a method body into a new parameter, making the value configurable by the caller.

## When to Use
- When a hardcoded value should be caller-configurable
- When a method needs to be more flexible without changing its internal logic
- When preparing to test a method with different values
- When removing hidden dependencies (like DateTime.Now)

---

## Example 1: Extract Hardcoded Configuration

### Before
```csharp
public class PasswordValidator
{
    public ValidationResult Validate(string password)
    {
        var errors = new List<string>();

        // Hardcoded values that should be configurable
        if (password.Length < 8)
        {
            errors.Add("Password must be at least 8 characters long");
        }

        if (password.Length > 128)
        {
            errors.Add("Password must be no more than 128 characters long");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("Password must contain at least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one number");
        }

        var specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
        if (!password.Any(c => specialChars.Contains(c)))
        {
            errors.Add("Password must contain at least one special character");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
```

### After
```csharp
public class PasswordValidator
{
    public ValidationResult Validate(
        string password,
        int minLength = 8,
        int maxLength = 128,
        bool requireUppercase = true,
        bool requireLowercase = true,
        bool requireDigit = true,
        bool requireSpecialChar = true)
    {
        var errors = new List<string>();

        if (password.Length < minLength)
        {
            errors.Add($"Password must be at least {minLength} characters long");
        }

        if (password.Length > maxLength)
        {
            errors.Add($"Password must be no more than {maxLength} characters long");
        }

        if (requireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        if (requireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("Password must contain at least one lowercase letter");
        }

        if (requireDigit && !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one number");
        }

        var specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
        if (requireSpecialChar && !password.Any(c => specialChars.Contains(c)))
        {
            errors.Add("Password must contain at least one special character");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-parameter '{
    "filePath": "PasswordValidator.cs",
    "startLine": 9,
    "startColumn": 31,
    "endLine": 9,
    "endColumn": 32,
    "parameterName": "minLength"
}'
```

---

## Example 2: Remove Hidden Time Dependencies

### Before
```csharp
public class SubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IPaymentGateway _paymentGateway;

    public async Task<RenewalResult> ProcessRenewalAsync(Subscription subscription)
    {
        // Hidden dependency on current time - makes testing difficult
        if (subscription.ExpirationDate > DateTime.Now)
        {
            return new RenewalResult
            {
                Success = false,
                Message = "Subscription is not yet expired"
            };
        }

        var gracePeriodEnd = subscription.ExpirationDate.AddDays(7);
        if (DateTime.Now > gracePeriodEnd)
        {
            // Past grace period - need to handle lapsed subscription
            return await HandleLapsedSubscriptionAsync(subscription);
        }

        // Within grace period - normal renewal
        var paymentResult = await _paymentGateway.ChargeAsync(
            subscription.CustomerId,
            subscription.MonthlyPrice);

        if (!paymentResult.Success)
        {
            return new RenewalResult { Success = false, Message = "Payment failed" };
        }

        subscription.ExpirationDate = DateTime.Now.AddMonths(1);
        await _repository.UpdateAsync(subscription);

        return new RenewalResult
        {
            Success = true,
            NewExpirationDate = subscription.ExpirationDate
        };
    }

    public bool IsExpiringSoon(Subscription subscription)
    {
        // Another hidden time dependency
        var warningThreshold = DateTime.Now.AddDays(7);
        return subscription.ExpirationDate <= warningThreshold;
    }
}
```

### After
```csharp
public class SubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SubscriptionService(
        ISubscriptionRepository repository,
        IPaymentGateway paymentGateway,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _paymentGateway = paymentGateway;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<RenewalResult> ProcessRenewalAsync(
        Subscription subscription,
        DateTime? asOfDate = null)
    {
        var currentDate = asOfDate ?? _dateTimeProvider.Now;

        if (subscription.ExpirationDate > currentDate)
        {
            return new RenewalResult
            {
                Success = false,
                Message = "Subscription is not yet expired"
            };
        }

        var gracePeriodEnd = subscription.ExpirationDate.AddDays(7);
        if (currentDate > gracePeriodEnd)
        {
            return await HandleLapsedSubscriptionAsync(subscription);
        }

        var paymentResult = await _paymentGateway.ChargeAsync(
            subscription.CustomerId,
            subscription.MonthlyPrice);

        if (!paymentResult.Success)
        {
            return new RenewalResult { Success = false, Message = "Payment failed" };
        }

        subscription.ExpirationDate = currentDate.AddMonths(1);
        await _repository.UpdateAsync(subscription);

        return new RenewalResult
        {
            Success = true,
            NewExpirationDate = subscription.ExpirationDate
        };
    }

    public bool IsExpiringSoon(Subscription subscription, DateTime? asOfDate = null)
    {
        var currentDate = asOfDate ?? _dateTimeProvider.Now;
        var warningThreshold = currentDate.AddDays(7);
        return subscription.ExpirationDate <= warningThreshold;
    }
}
```

---

## Example 3: Parameterize Algorithm Behavior

### Before
```csharp
public class SearchEngine
{
    private readonly ISearchIndex _index;

    public SearchResults Search(string query)
    {
        var terms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Hardcoded search behavior
        var results = _index.FindDocuments(terms);

        // Fixed relevance scoring
        var scoredResults = results.Select(doc => new ScoredDocument
        {
            Document = doc,
            Score = CalculateRelevance(doc, terms)
        });

        // Hardcoded sorting and limiting
        var sortedResults = scoredResults
            .OrderByDescending(r => r.Score)
            .Take(10)
            .ToList();

        return new SearchResults
        {
            Query = query,
            Items = sortedResults,
            TotalMatches = results.Count
        };
    }

    private double CalculateRelevance(Document doc, string[] terms)
    {
        double score = 0;

        foreach (var term in terms)
        {
            // Title matches weighted higher
            if (doc.Title.ToLower().Contains(term))
                score += 10;

            // Content matches
            var contentMatches = doc.Content.ToLower().Split(' ')
                .Count(w => w == term);
            score += contentMatches;

            // Recency boost - documents from last 30 days
            if (doc.PublishedDate > DateTime.Now.AddDays(-30))
                score *= 1.5;
        }

        return score;
    }
}
```

### After
```csharp
public class SearchEngine
{
    private readonly ISearchIndex _index;

    public SearchResults Search(
        string query,
        int maxResults = 10,
        double titleWeight = 10.0,
        double recencyBoostMultiplier = 1.5,
        int recencyBoostDays = 30,
        SearchSortOrder sortOrder = SearchSortOrder.ByRelevance)
    {
        var terms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var results = _index.FindDocuments(terms);

        var scoredResults = results.Select(doc => new ScoredDocument
        {
            Document = doc,
            Score = CalculateRelevance(doc, terms, titleWeight, recencyBoostMultiplier, recencyBoostDays)
        });

        var orderedResults = sortOrder switch
        {
            SearchSortOrder.ByRelevance => scoredResults.OrderByDescending(r => r.Score),
            SearchSortOrder.ByDate => scoredResults.OrderByDescending(r => r.Document.PublishedDate),
            SearchSortOrder.ByTitle => scoredResults.OrderBy(r => r.Document.Title),
            _ => scoredResults.OrderByDescending(r => r.Score)
        };

        var limitedResults = orderedResults.Take(maxResults).ToList();

        return new SearchResults
        {
            Query = query,
            Items = limitedResults,
            TotalMatches = results.Count
        };
    }

    private double CalculateRelevance(
        Document doc,
        string[] terms,
        double titleWeight,
        double recencyBoostMultiplier,
        int recencyBoostDays)
    {
        double score = 0;

        foreach (var term in terms)
        {
            if (doc.Title.ToLower().Contains(term))
                score += titleWeight;

            var contentMatches = doc.Content.ToLower().Split(' ')
                .Count(w => w == term);
            score += contentMatches;

            if (doc.PublishedDate > DateTime.Now.AddDays(-recencyBoostDays))
                score *= recencyBoostMultiplier;
        }

        return score;
    }
}

public enum SearchSortOrder
{
    ByRelevance,
    ByDate,
    ByTitle
}
```

---

## Benefits
1. **Flexibility**: Methods become adaptable to different use cases
2. **Testability**: Hidden dependencies become explicit and mockable
3. **Reusability**: Single method handles multiple scenarios
4. **Documentation**: Parameters document what values affect behavior
5. **Backwards Compatibility**: Default values preserve existing behavior
