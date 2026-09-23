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

        public void Pay()
        {
            Console.WriteLine("Paying");
            if (_flags.IsEnabled("NewCheckout"))
            {
                Console.WriteLine("New checkout");
            }
            else
            {
                Console.WriteLine("Old checkout");
            }
        }
    }
}
