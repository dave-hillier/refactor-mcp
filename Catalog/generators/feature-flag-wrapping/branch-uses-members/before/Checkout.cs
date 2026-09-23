using System;
using System.Collections.Generic;

namespace Shop
{
    public interface IFeatureFlags
    {
        bool IsEnabled(string flag);
    }

    public class Checkout
    {
        private readonly IFeatureFlags _flags;
        private readonly List<decimal> _payments = new List<decimal>();

        public Checkout(IFeatureFlags flags)
        {
            _flags = flags;
        }

        public void Pay(decimal amount)
        {
            if (_flags.IsEnabled("NewCheckout"))
            {
                _payments.Add(amount);
            }
        }
    }
}
