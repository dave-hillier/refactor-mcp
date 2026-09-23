using System;

namespace Shop
{
    // Orders placed through the web shop.
    public class Order
    {
        public DateTime Placed { get; set; }
    }

    #region Payments

    public class Payment
    {
        public decimal Amount { get; set; }
    }

    #endregion
}
