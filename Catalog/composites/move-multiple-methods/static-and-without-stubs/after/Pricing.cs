using System;

namespace Shop
{
    public class Pricing
    {
        public decimal Base { get; set; }

        public decimal Discount { get; set; }

        internal static decimal Round(decimal amount) => Math.Round(amount, 2);

        internal decimal UnitPrice() => Round(Base * (1 - Discount));
    }
}
