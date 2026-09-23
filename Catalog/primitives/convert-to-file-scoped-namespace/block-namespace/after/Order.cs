using System;

namespace Shop.Orders;

public class Order
{
    public DateTime Placed { get; set; }

    public bool IsLate(DateTime now)
    {
        return now - Placed > TimeSpan.FromDays(3);
    }
}

public enum Status
{
    Open,
    Closed,
}
