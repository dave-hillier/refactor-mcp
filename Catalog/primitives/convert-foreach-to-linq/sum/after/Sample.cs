using System.Collections.Generic;
using System.Linq;

public class Order
{
    public decimal Amount { get; set; }

    public bool IsPaid { get; set; }
}

public class Sample
{
    public decimal Paid(IEnumerable<Order> orders)
    {
        decimal total = orders.Where(order => order.IsPaid).Sum(order => order.Amount);

        return total;
    }
}
