using System.Collections.Generic;
using System.Linq;

namespace Shop;

public class Order
{
    public string Customer { get; set; } = "";

    public decimal Total { get; set; }
}

public class Reports
{
    public int DistinctTotals(List<Order> orders)
    {
        var rows = orders.Select(o => /*^*/new { o.Customer, o.Total }).Distinct();
        var empty = new { Customer = "", Total = 0m };
        return rows.Count(r => !r.Equals(empty));
    }

    public object Other() => new { Customer = "x", Total = 1m };
}
