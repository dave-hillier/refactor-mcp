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
        foreach (var order in orders)
        {
            Console.WriteLine(order.Amount);
            Console.WriteLine(order);
        }
    }
}
