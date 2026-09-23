using System;

namespace Shop
{
    public interface IFeatureFlags
    {
        bool IsEnabled(string flag);
    }

    public class Checkout
    {
        public void Pay(IFeatureFlags flags)
        {
            if (flags.IsEnabled("NewCheckout"))
            {
                Console.WriteLine("New checkout");
            }
        }
    }
}
