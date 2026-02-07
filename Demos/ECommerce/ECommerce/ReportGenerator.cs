using System.Text;

namespace ECommerce;

/// <summary>
/// Report generation with static methods that should be reorganized.
///
/// Refactoring opportunities:
///   - move-static-method: FormatAsTable is generic utility, belongs in a TableFormatter class
///   - extract-method: GenerateOrderReport has distinct formatting sections
///   - create-adapter: wrap ReportGenerator for a different report format interface
///   - safe-delete-variable: unused 'separator' variable in GenerateSalesReport
/// </summary>
public class ReportGenerator
{
    private readonly CustomerRepository _customerRepo;

    public ReportGenerator(CustomerRepository customerRepo)
    {
        _customerRepo = customerRepo;
    }

    public string GenerateOrderReport(Order order, Customer customer)
    {
        var sb = new StringBuilder();

        // Header section
        sb.AppendLine("╔════════════════════════════════════════╗");
        sb.AppendLine("║         ORDER CONFIRMATION             ║");
        sb.AppendLine("╚════════════════════════════════════════╝");
        sb.AppendLine();

        // Customer info
        sb.AppendLine($"  Customer: {customer.Name}");
        sb.AppendLine($"  Email:    {customer.Email}");
        sb.AppendLine($"  Tier:     {customer.Tier}");
        sb.AppendLine();

        // Order details
        sb.AppendLine($"  Order ID:    {order.Id}");
        sb.AppendLine($"  Date:        {order.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"  Status:      {order.Status}");
        sb.AppendLine();

        // Line items
        sb.AppendLine("  Items:");
        sb.AppendLine("  " + new string('-', 50));
        foreach (var item in order.Items)
        {
            var lineTotal = item.UnitPrice * item.Quantity;
            sb.AppendLine($"  {item.ProductName,-25} {item.Quantity,5} x {item.UnitPrice,8:C} = {lineTotal,10:C}");
        }
        sb.AppendLine("  " + new string('-', 50));

        decimal subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        sb.AppendLine($"  {"Subtotal",-42} {subtotal,10:C}");

        // Shipping address
        sb.AppendLine();
        sb.AppendLine("  Ship to:");
        sb.AppendLine($"    {order.ShippingAddress.Street}");
        sb.AppendLine($"    {order.ShippingAddress.City}, {order.ShippingAddress.State} {order.ShippingAddress.ZipCode}");
        sb.AppendLine($"    {order.ShippingAddress.Country}");

        return sb.ToString();
    }

    /// <summary>
    /// Contains an unused local variable — safe-delete-variable candidate.
    /// </summary>
    public string GenerateSalesReport(List<Order> orders)
    {
        var sb = new StringBuilder();
        string separator = "============================"; // unused variable
        var totalRevenue = 0m;
        var orderCount = orders.Count;

        sb.AppendLine("SALES REPORT");
        sb.AppendLine($"Period: {DateTime.UtcNow:yyyy-MM}");
        sb.AppendLine($"Total Orders: {orderCount}");
        sb.AppendLine();

        foreach (var order in orders)
        {
            var orderTotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            totalRevenue += orderTotal;
            sb.AppendLine($"  {order.Id}: {orderTotal:C} ({order.Items.Count} items)");
        }

        sb.AppendLine();
        sb.AppendLine($"Total Revenue: {totalRevenue:C}");
        sb.AppendLine($"Average Order Value: {(orderCount > 0 ? totalRevenue / orderCount : 0):C}");

        return sb.ToString();
    }

    /// <summary>
    /// A static utility method that doesn't belong in ReportGenerator.
    /// Should be moved to a dedicated TableFormatter class.
    /// </summary>
    public static string FormatAsTable(List<string[]> rows, string[] headers)
    {
        var sb = new StringBuilder();
        var columnWidths = new int[headers.Length];

        for (int i = 0; i < headers.Length; i++)
            columnWidths[i] = headers[i].Length;

        foreach (var row in rows)
        {
            for (int i = 0; i < Math.Min(row.Length, headers.Length); i++)
                columnWidths[i] = Math.Max(columnWidths[i], row[i].Length);
        }

        // Header
        for (int i = 0; i < headers.Length; i++)
            sb.Append(headers[i].PadRight(columnWidths[i] + 2));
        sb.AppendLine();

        // Separator
        for (int i = 0; i < headers.Length; i++)
            sb.Append(new string('-', columnWidths[i] + 2));
        sb.AppendLine();

        // Rows
        foreach (var row in rows)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var value = i < row.Length ? row[i] : "";
                sb.Append(value.PadRight(columnWidths[i] + 2));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public class TableFormatter
{
    // Target class for FormatAsTable move
}
