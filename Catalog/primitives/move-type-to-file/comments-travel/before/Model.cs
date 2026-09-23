using System;

namespace Shop
{
    // Orders placed through the web shop.
    public class Order
    {
        public DateTime Placed { get; set; }
    }

    /// <summary>
    /// A customer who places orders.
    /// </summary>
    // Names are not yet split into first and last.
    public class Customer
    {
        public string Name { get; set; }
    }

    #region Payments

    public class Payment
    {
        public decimal Amount { get; set; }
    }

    #endregion
}
