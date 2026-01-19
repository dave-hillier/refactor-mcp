using System;
using System.Collections.Generic;
using System.Linq;

namespace Examples.ConvertToStatic;

/// <summary>
/// Example: Invoice class with a GenerateSummary method that should be static (it only uses its parameters).
/// Refactoring: convert-to-static-with-instance on GenerateSummary
/// </summary>
public class Invoice
{
    public string InvoiceNumber { get; set; } = "";
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public List<InvoiceLineItem> LineItems { get; set; } = new();
    public decimal TaxRate { get; set; }
    public Customer Customer { get; set; } = new();
    public InvoiceStatus Status { get; set; }

    // This method could be static - it only accesses instance members through 'this'
    public string GenerateSummary()
    {
        var subtotal = LineItems.Sum(i => i.Total);
        var tax = subtotal * TaxRate;
        var total = subtotal + tax;

        return $"""
            Invoice #{InvoiceNumber}
            Date: {IssueDate:d}
            Due: {DueDate:d}

            Bill To: {Customer.Name}
            {Customer.Address}

            Items:
            {string.Join("\n", LineItems.Select(i => $"  {i.Description}: {i.Quantity} x ${i.UnitPrice:F2} = ${i.Total:F2}"))}

            Subtotal: ${subtotal:F2}
            Tax ({TaxRate:P0}): ${tax:F2}
            Total: ${total:F2}
            """;
    }
}

// Supporting types
public class InvoiceLineItem
{
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => Quantity * UnitPrice;
}

public class Customer
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
}

public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
    Overdue
}
