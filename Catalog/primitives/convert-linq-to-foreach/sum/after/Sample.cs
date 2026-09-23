using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public decimal Paid(IEnumerable<Order> orders)
    {
        decimal total = 0;
        foreach (var order in orders)
        {
            if (order.IsPaid)
            {
                total += order.Amount;
            }
        }

        return total;
    }
}
