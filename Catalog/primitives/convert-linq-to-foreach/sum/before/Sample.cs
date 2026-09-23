using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public decimal Paid(IEnumerable<Order> orders)
    {
        var total = /*^*/orders.Where(order => order.IsPaid).Sum(o => o.Amount);

        return total;
    }
}
