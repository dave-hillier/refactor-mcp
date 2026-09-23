using System.Collections.Generic;

public class Order
{
    public decimal Amount { get; set; }

    public bool IsPaid { get; set; }
}

public class Sample
{
    public decimal Paid(IEnumerable<Order> orders)
    {
        decimal total = 0;
        /*^*/foreach (var order in orders)
        {
            if (order.IsPaid)
            {
                total += order.Amount;
            }
        }

        return total;
    }
}
