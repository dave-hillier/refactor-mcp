using System;

namespace Shop
{
    public class LegacyPricing
    {
        public decimal Price(decimal amount) => Math.Round(amount, 2);
    }
}
