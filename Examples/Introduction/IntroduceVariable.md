# Introduce Variable Refactoring

## Overview
The `introduce-variable` refactoring extracts a selected expression into a new local variable, replacing the original expression with the variable reference.

## When to Use
- When an expression is complex and needs a descriptive name
- When the same expression is used multiple times
- When debugging requires inspecting intermediate values
- When code readability would benefit from named values

---

## Example 1: Extract Complex LINQ Expression

### Before
```csharp
public class SalesAnalyzer
{
    public SalesReport AnalyzeSales(List<Transaction> transactions, DateTime reportDate)
    {
        var report = new SalesReport
        {
            Date = reportDate,

            // Complex expression that's hard to read
            TopPerformingRegion = transactions
                .Where(t => t.Date.Month == reportDate.Month && t.Date.Year == reportDate.Year)
                .GroupBy(t => t.Region)
                .OrderByDescending(g => g.Sum(t => t.Amount))
                .FirstOrDefault()?.Key ?? "Unknown",

            // Same filtering logic duplicated
            MonthlyRevenue = transactions
                .Where(t => t.Date.Month == reportDate.Month && t.Date.Year == reportDate.Year)
                .Sum(t => t.Amount),

            // And again
            TransactionCount = transactions
                .Where(t => t.Date.Month == reportDate.Month && t.Date.Year == reportDate.Year)
                .Count(),

            // Complex nested calculation
            AverageOrderValue = transactions
                .Where(t => t.Date.Month == reportDate.Month && t.Date.Year == reportDate.Year)
                .Count() > 0
                    ? transactions
                        .Where(t => t.Date.Month == reportDate.Month && t.Date.Year == reportDate.Year)
                        .Sum(t => t.Amount) /
                      transactions
                        .Where(t => t.Date.Month == reportDate.Month && t.Date.Year == reportDate.Year)
                        .Count()
                    : 0
        };

        return report;
    }
}
```

### After
```csharp
public class SalesAnalyzer
{
    public SalesReport AnalyzeSales(List<Transaction> transactions, DateTime reportDate)
    {
        // Introduce variable for the filtered transactions
        var monthlyTransactions = transactions
            .Where(t => t.Date.Month == reportDate.Month && t.Date.Year == reportDate.Year)
            .ToList();

        var totalRevenue = monthlyTransactions.Sum(t => t.Amount);
        var transactionCount = monthlyTransactions.Count;

        var report = new SalesReport
        {
            Date = reportDate,

            TopPerformingRegion = monthlyTransactions
                .GroupBy(t => t.Region)
                .OrderByDescending(g => g.Sum(t => t.Amount))
                .FirstOrDefault()?.Key ?? "Unknown",

            MonthlyRevenue = totalRevenue,

            TransactionCount = transactionCount,

            AverageOrderValue = transactionCount > 0
                ? totalRevenue / transactionCount
                : 0
        };

        return report;
    }
}
```

### Tool Usage
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-variable '{
    "filePath": "SalesAnalyzer.cs",
    "startLine": 10,
    "startColumn": 17,
    "endLine": 10,
    "endColumn": 95,
    "variableName": "monthlyTransactions"
}'
```

---

## Example 2: Extract Magic Numbers and Formulas

### Before
```csharp
public class TaxCalculator
{
    public TaxResult CalculateTax(Income income)
    {
        decimal federalTax;
        decimal stateTax;

        // Complex tax bracket calculation with magic numbers
        if (income.GrossAmount <= 10275)
        {
            federalTax = income.GrossAmount * 0.10m;
        }
        else if (income.GrossAmount <= 41775)
        {
            federalTax = 1027.50m + (income.GrossAmount - 10275) * 0.12m;
        }
        else if (income.GrossAmount <= 89075)
        {
            federalTax = 4807.50m + (income.GrossAmount - 41775) * 0.22m;
        }
        else if (income.GrossAmount <= 170050)
        {
            federalTax = 15213.50m + (income.GrossAmount - 89075) * 0.24m;
        }
        else
        {
            federalTax = 34647.50m + (income.GrossAmount - 170050) * 0.32m;
        }

        // State tax with deductions
        stateTax = (income.GrossAmount - income.Deductions.Sum(d => d.Amount)) * 0.0495m;

        return new TaxResult
        {
            FederalTax = federalTax,
            StateTax = stateTax,
            TotalTax = federalTax + stateTax,
            EffectiveRate = (federalTax + stateTax) / income.GrossAmount
        };
    }
}
```

### After
```csharp
public class TaxCalculator
{
    public TaxResult CalculateTax(Income income)
    {
        decimal federalTax;
        decimal stateTax;

        var grossAmount = income.GrossAmount;

        // Tax bracket boundaries
        var bracket10Limit = 10275m;
        var bracket12Limit = 41775m;
        var bracket22Limit = 89075m;
        var bracket24Limit = 170050m;

        // Base amounts for each bracket
        var bracket12Base = 1027.50m;
        var bracket22Base = 4807.50m;
        var bracket24Base = 15213.50m;
        var bracket32Base = 34647.50m;

        if (grossAmount <= bracket10Limit)
        {
            federalTax = grossAmount * 0.10m;
        }
        else if (grossAmount <= bracket12Limit)
        {
            var amountInBracket = grossAmount - bracket10Limit;
            federalTax = bracket12Base + amountInBracket * 0.12m;
        }
        else if (grossAmount <= bracket22Limit)
        {
            var amountInBracket = grossAmount - bracket12Limit;
            federalTax = bracket22Base + amountInBracket * 0.22m;
        }
        else if (grossAmount <= bracket24Limit)
        {
            var amountInBracket = grossAmount - bracket22Limit;
            federalTax = bracket24Base + amountInBracket * 0.24m;
        }
        else
        {
            var amountInBracket = grossAmount - bracket24Limit;
            federalTax = bracket32Base + amountInBracket * 0.32m;
        }

        // State tax with deductions
        var totalDeductions = income.Deductions.Sum(d => d.Amount);
        var taxableStateIncome = grossAmount - totalDeductions;
        var stateRate = 0.0495m;
        stateTax = taxableStateIncome * stateRate;

        var totalTax = federalTax + stateTax;
        var effectiveRate = totalTax / grossAmount;

        return new TaxResult
        {
            FederalTax = federalTax,
            StateTax = stateTax,
            TotalTax = totalTax,
            EffectiveRate = effectiveRate
        };
    }
}
```

---

## Example 3: Extract for Debugging

### Before
```csharp
public class PathFinder
{
    public Route FindOptimalRoute(Location start, Location end, List<Location> waypoints)
    {
        // Hard to debug: what values are being calculated?
        return new Route
        {
            Segments = waypoints
                .Prepend(start)
                .Append(end)
                .Zip(waypoints.Prepend(start).Append(end).Skip(1),
                    (from, to) => new Segment
                    {
                        From = from,
                        To = to,
                        Distance = Math.Sqrt(
                            Math.Pow(to.Latitude - from.Latitude, 2) +
                            Math.Pow(to.Longitude - from.Longitude, 2)) * 111.32
                    })
                .ToList(),

            TotalDistance = waypoints
                .Prepend(start)
                .Append(end)
                .Zip(waypoints.Prepend(start).Append(end).Skip(1),
                    (from, to) => Math.Sqrt(
                        Math.Pow(to.Latitude - from.Latitude, 2) +
                        Math.Pow(to.Longitude - from.Longitude, 2)) * 111.32)
                .Sum()
        };
    }
}
```

### After
```csharp
public class PathFinder
{
    public Route FindOptimalRoute(Location start, Location end, List<Location> waypoints)
    {
        // Build the complete path with all points
        var allPoints = waypoints.Prepend(start).Append(end).ToList();

        // Create pairs of consecutive points
        var pointPairs = allPoints.Zip(allPoints.Skip(1)).ToList();

        // Calculate each segment with distance
        var segments = pointPairs.Select(pair =>
        {
            var from = pair.First;
            var to = pair.Second;

            var latDiff = to.Latitude - from.Latitude;
            var lonDiff = to.Longitude - from.Longitude;
            var distanceInDegrees = Math.Sqrt(Math.Pow(latDiff, 2) + Math.Pow(lonDiff, 2));
            var distanceInKm = distanceInDegrees * 111.32;

            return new Segment
            {
                From = from,
                To = to,
                Distance = distanceInKm
            };
        }).ToList();

        var totalDistance = segments.Sum(s => s.Distance);

        return new Route
        {
            Segments = segments,
            TotalDistance = totalDistance
        };
    }
}
```

---

## Benefits
1. **Self-Documenting Code**: Variable names explain what values represent
2. **DRY Principle**: Eliminates duplicate expressions
3. **Easier Debugging**: Can inspect intermediate values
4. **Performance**: Avoids recalculating the same expression
5. **Maintainability**: Changes to calculation only needed in one place
