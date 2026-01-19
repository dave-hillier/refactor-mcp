# Move Static Method Refactoring

## Overview
The `move-static-method` refactoring moves a static method from one class to another, optionally leaving a delegating method in the original class to preserve backward compatibility.

## When to Use
- When a static utility method is in the wrong class
- When reorganizing helper methods into more logical groupings
- When a static method is used primarily by another class
- When consolidating related functionality

---

## Example 1: Organize Scattered Utility Methods

### Before
```csharp
// StringHelper.cs - Mixed concerns
public static class StringHelper
{
    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }

    public static string ToSlug(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return Regex.Replace(value.ToLower(), @"[^a-z0-9]+", "-").Trim('-');
    }

    // These methods are about file paths, not strings
    public static string GetSafeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string EnsureExtension(string fileName, string extension)
    {
        if (!extension.StartsWith(".")) extension = "." + extension;
        return Path.GetExtension(fileName).Equals(extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + extension;
    }

    public static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    // These are about formatting numbers, not strings
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:0.#} days";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:0.#} hours";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:0.#} minutes";
        return $"{duration.TotalSeconds:0.#} seconds";
    }
}
```

### After
```csharp
// StringHelper.cs - Now focused on string manipulation
public static class StringHelper
{
    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }

    public static string ToSlug(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return Regex.Replace(value.ToLower(), @"[^a-z0-9]+", "-").Trim('-');
    }
}

// PathHelper.cs - File path utilities
public static class PathHelper
{
    public static string GetSafeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string EnsureExtension(string fileName, string extension)
    {
        if (!extension.StartsWith(".")) extension = "." + extension;
        return Path.GetExtension(fileName).Equals(extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + extension;
    }

    public static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }
}

// FormatHelper.cs - Formatting utilities
public static class FormatHelper
{
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:0.#} days";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:0.#} hours";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:0.#} minutes";
        return $"{duration.TotalSeconds:0.#} seconds";
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-static-method '{
    "solutionPath": "MyProject.sln",
    "sourceFilePath": "Helpers/StringHelper.cs",
    "methodName": "GetSafeFileName",
    "targetClassName": "PathHelper",
    "targetFilePath": "Helpers/PathHelper.cs"
}'
```

---

## Example 2: Move Extension Methods to Appropriate Class

### Before
```csharp
// Extensions.cs - Mixed extension methods
public static class Extensions
{
    // String extensions
    public static bool IsNullOrWhiteSpace(this string value) =>
        string.IsNullOrWhiteSpace(value);

    public static string DefaultIfEmpty(this string value, string defaultValue) =>
        string.IsNullOrEmpty(value) ? defaultValue : value;

    // Collection extensions - should be separate
    public static bool IsEmpty<T>(this IEnumerable<T> source) =>
        source == null || !source.Any();

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T> source) where T : class =>
        source.Where(x => x != null);

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source) =>
        source.Select((item, index) => (item, index));

    public static Dictionary<TKey, TValue> ToDictionarySafe<TSource, TKey, TValue>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>();
        foreach (var item in source)
        {
            var key = keySelector(item);
            if (!dict.ContainsKey(key))
                dict[key] = valueSelector(item);
        }
        return dict;
    }

    // Task extensions - should be separate
    public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
    {
        var delayTask = Task.Delay(timeout);
        var completedTask = await Task.WhenAny(task, delayTask);
        if (completedTask == delayTask)
            throw new TimeoutException();
        return await task;
    }

    public static async Task WhenAllSequential<T>(this IEnumerable<Func<Task<T>>> tasks)
    {
        foreach (var task in tasks)
            await task();
    }
}
```

### After
```csharp
// StringExtensions.cs
public static class StringExtensions
{
    public static bool IsNullOrWhiteSpace(this string value) =>
        string.IsNullOrWhiteSpace(value);

    public static string DefaultIfEmpty(this string value, string defaultValue) =>
        string.IsNullOrEmpty(value) ? defaultValue : value;
}

// EnumerableExtensions.cs
public static class EnumerableExtensions
{
    public static bool IsEmpty<T>(this IEnumerable<T> source) =>
        source == null || !source.Any();

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T> source) where T : class =>
        source.Where(x => x != null);

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source) =>
        source.Select((item, index) => (item, index));

    public static Dictionary<TKey, TValue> ToDictionarySafe<TSource, TKey, TValue>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>();
        foreach (var item in source)
        {
            var key = keySelector(item);
            if (!dict.ContainsKey(key))
                dict[key] = valueSelector(item);
        }
        return dict;
    }
}

// TaskExtensions.cs
public static class TaskExtensions
{
    public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
    {
        var delayTask = Task.Delay(timeout);
        var completedTask = await Task.WhenAny(task, delayTask);
        if (completedTask == delayTask)
            throw new TimeoutException();
        return await task;
    }

    public static async Task WhenAllSequential<T>(this IEnumerable<Func<Task<T>>> tasks)
    {
        foreach (var task in tasks)
            await task();
    }
}
```

---

## Example 3: Move Validation Methods to Validator Class

### Before
```csharp
// FormProcessor.cs - Validation mixed with processing
public class FormProcessor
{
    public FormResult Process(FormSubmission form)
    {
        var errors = new List<string>();

        if (!IsValidEmail(form.Email))
            errors.Add("Invalid email address");

        if (!IsValidPhone(form.Phone))
            errors.Add("Invalid phone number");

        if (!IsValidCreditCard(form.CreditCardNumber))
            errors.Add("Invalid credit card number");

        if (!IsValidPostalCode(form.PostalCode, form.Country))
            errors.Add("Invalid postal code");

        if (errors.Any())
            return FormResult.Invalid(errors);

        // Actual processing logic
        return ProcessValidForm(form);
    }

    // These static validation methods should be in a Validators class
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern);
    }

    public static bool IsValidPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        return digitsOnly.Length >= 10 && digitsOnly.Length <= 15;
    }

    public static bool IsValidCreditCard(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return false;
        var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 13 || digitsOnly.Length > 19) return false;

        // Luhn algorithm
        int sum = 0;
        bool alternate = false;
        for (int i = digitsOnly.Length - 1; i >= 0; i--)
        {
            int n = int.Parse(digitsOnly[i].ToString());
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    public static bool IsValidPostalCode(string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(postalCode)) return false;

        return country.ToUpperInvariant() switch
        {
            "US" => Regex.IsMatch(postalCode, @"^\d{5}(-\d{4})?$"),
            "CA" => Regex.IsMatch(postalCode, @"^[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d$"),
            "UK" => Regex.IsMatch(postalCode, @"^[A-Z]{1,2}\d[A-Z\d]? ?\d[A-Z]{2}$", RegexOptions.IgnoreCase),
            _ => postalCode.Length >= 3 && postalCode.Length <= 10
        };
    }

    private FormResult ProcessValidForm(FormSubmission form)
    {
        // Processing logic
        return FormResult.Success();
    }
}
```

### After
```csharp
// FormProcessor.cs - Now focused on processing
public class FormProcessor
{
    public FormResult Process(FormSubmission form)
    {
        var errors = new List<string>();

        if (!Validators.IsValidEmail(form.Email))
            errors.Add("Invalid email address");

        if (!Validators.IsValidPhone(form.Phone))
            errors.Add("Invalid phone number");

        if (!Validators.IsValidCreditCard(form.CreditCardNumber))
            errors.Add("Invalid credit card number");

        if (!Validators.IsValidPostalCode(form.PostalCode, form.Country))
            errors.Add("Invalid postal code");

        if (errors.Any())
            return FormResult.Invalid(errors);

        return ProcessValidForm(form);
    }

    private FormResult ProcessValidForm(FormSubmission form)
    {
        // Processing logic
        return FormResult.Success();
    }
}

// Validators.cs - Reusable validation methods
public static class Validators
{
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern);
    }

    public static bool IsValidPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        return digitsOnly.Length >= 10 && digitsOnly.Length <= 15;
    }

    public static bool IsValidCreditCard(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return false;
        var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 13 || digitsOnly.Length > 19) return false;

        // Luhn algorithm
        int sum = 0;
        bool alternate = false;
        for (int i = digitsOnly.Length - 1; i >= 0; i--)
        {
            int n = int.Parse(digitsOnly[i].ToString());
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    public static bool IsValidPostalCode(string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(postalCode)) return false;

        return country.ToUpperInvariant() switch
        {
            "US" => Regex.IsMatch(postalCode, @"^\d{5}(-\d{4})?$"),
            "CA" => Regex.IsMatch(postalCode, @"^[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d$"),
            "UK" => Regex.IsMatch(postalCode, @"^[A-Z]{1,2}\d[A-Z\d]? ?\d[A-Z]{2}$", RegexOptions.IgnoreCase),
            _ => postalCode.Length >= 3 && postalCode.Length <= 10
        };
    }
}
```

---

## Benefits
1. **Logical Organization**: Related methods grouped together
2. **Discoverability**: Developers can find methods in expected locations
3. **Reusability**: Separated utilities can be used from anywhere
4. **Single Responsibility**: Each class has a clear purpose
5. **Easier Testing**: Isolated static methods are simple to test
