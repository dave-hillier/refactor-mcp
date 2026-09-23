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
            NewCheckout.Apply();
        }

        private INewCheckoutStrategy NewCheckout => _flags.IsEnabled("NewCheckout") ? new NewCheckoutStrategy() : new NoNewCheckoutStrategy();
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
