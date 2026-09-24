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
                NewCheckout.Apply();
            }

            private INewCheckoutStrategy NewCheckout => _flags.IsEnabled("NewCheckout") ? new NewCheckoutStrategy() : new NoNewCheckoutStrategy();
        }
    }

    internal interface INewCheckoutStrategy
    {
        void Apply();
    }

    internal sealed class NewCheckoutStrategy : INewCheckoutStrategy
    {
        public void Apply()
        {
            Console.WriteLine("New checkout");
        }
    }

    internal sealed class NoNewCheckoutStrategy : INewCheckoutStrategy
    {
        public void Apply()
        {
            Console.WriteLine("Old checkout");
        }
    }
}
