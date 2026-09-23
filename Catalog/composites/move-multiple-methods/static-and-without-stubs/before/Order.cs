using System;

namespace Shop
{
    public class Order
    {
        private readonly Pricing _pricing = new Pricing();

        public int Quantity { get; set; }

        public decimal Total() => Quantity * UnitPrice();

        private decimal UnitPrice() => Round(_pricing.Base * (1 - _pricing.Discount));

        private static decimal Round(decimal amount) => Math.Round(amount, 2);
    }
}
