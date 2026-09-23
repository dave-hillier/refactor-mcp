// Orders.
namespace Shop;

using System;

/// <summary>
/// An order.
/// </summary>
public class Order
{
    #region Dates
    public DateTime Placed { get; set; } // local time
    #endregion

#if DEBUG
    public string Trace => "debug";
#endif

    /* The day
       after placing. */
    public DateTime Next => Placed.AddDays(1);
}
