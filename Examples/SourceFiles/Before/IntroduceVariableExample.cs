using System;
using System.Collections.Generic;
using System.Linq;

namespace Examples.IntroduceVariable;

/// <summary>
/// Example: Sales analyzer with repeated LINQ expressions that should be extracted to variables.
/// Refactoring: introduce-variable on the filtered transactions expression
/// </summary>
public class SalesAnalyzer
{
    public SalesReport AnalyzeSales(List<Transaction> transactions, DateTime reportDate)
    {
        var report = new SalesReport
        {
            Date = reportDate,

            // This filter is repeated - should be extracted to a variable
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

            // Complex nested calculation using the same filter
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

// Supporting types
public class Transaction
{
    public DateTime Date { get; set; }
    public string Region { get; set; } = "";
    public decimal Amount { get; set; }
}

public class SalesReport
{
    public DateTime Date { get; set; }
    public string TopPerformingRegion { get; set; } = "";
    public decimal MonthlyRevenue { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageOrderValue { get; set; }
}
