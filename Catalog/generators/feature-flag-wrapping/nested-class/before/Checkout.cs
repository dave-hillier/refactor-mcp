using System;

namespace Shop
{
    public interface IFeatureFlags
    {
        bool IsEnabled(string flag);
    }

    public class Checkout
    {
        public void Start()
        {
            Console.WriteLine("Starting");
        }

        public class Payment
        {
            private readonly IFeatureFlags _flags;

            public Payment(IFeatureFlags flags)
            {
                _flags = flags;
            }

            public void Pay()
            {
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
}
