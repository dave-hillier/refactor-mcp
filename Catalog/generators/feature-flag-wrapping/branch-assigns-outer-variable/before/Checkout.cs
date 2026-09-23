using System;

namespace Shop
{
    public interface IFeatureFlags
    {
        bool IsEnabled(string flag);
    }

    public class Checkout
    {
        private readonly IFeatureFlags _flags;

        public Checkout(IFeatureFlags flags)
        {
            _flags = flags;
        }

        public decimal Total(decimal price)
        {
            var total = price;
            if (_flags.IsEnabled("Discounts"))
            {
                total = total * 0.9m;
            }

            return total;
        }
    }
}
