using System;
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
        var rows = orders.Select(o => new Row(o.Customer, o.Total)).Distinct();
        var empty = new Row("", 0m);
        return rows.Count(r => !r.Equals(empty));
    }

    public object Other() => new { Customer = "x", Total = 1m };
}

internal sealed class Row
{
    public Row(string customer, decimal total)
    {
        Customer = customer;
        Total = total;
    }

    public string Customer { get; }

    public decimal Total { get; }

    public override bool Equals(object obj)
    {
        return obj is Row other
            && EqualityComparer<string>.Default.Equals(Customer, other.Customer)
            && EqualityComparer<decimal>.Default.Equals(Total, other.Total);
    }

    public override int GetHashCode() => HashCode.Combine(Customer, Total);

    public override string ToString() => $"{{ Customer = {Customer}, Total = {Total} }}";
}
