using System;
using System.Collections.Generic;

public class Order
{
    public decimal Amount { get; set; }
}

public class Sample
{
    public void Print(List<Order> orders)
    {
        for (var i = 0; /*^*/i < orders.Count; ++i)
        {
            Console.WriteLine(orders[i].Amount);
            Console.WriteLine(orders[i]);
        }
    }
}
