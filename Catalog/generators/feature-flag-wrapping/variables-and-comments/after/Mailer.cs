using System;

namespace Shop
{
    public interface IFeatureFlags
    {
        bool IsEnabled(string flag);
    }

    public class Mailer
    {
        private readonly IFeatureFlags _flags;

        public Mailer(IFeatureFlags flags)
        {
            _flags = flags;
        }

        public void Send(string to, decimal total)
        {
            var subject = "Order for " + to;

            // Receipts are being trialled
            Receipts.Apply(total, subject);
        }

        private IReceiptsStrategy Receipts => _flags.IsEnabled("Receipts") ? new ReceiptsStrategy() : new NoReceiptsStrategy();
    }

    internal interface IReceiptsStrategy
    {
        void Apply(decimal total, string subject);
    }

    internal sealed class ReceiptsStrategy : IReceiptsStrategy
    {
        public void Apply(decimal total, string subject)
        {
            Console.WriteLine(subject + ": " + total); // print the receipt
        }
    }

    internal sealed class NoReceiptsStrategy : IReceiptsStrategy
    {
        public void Apply(decimal total, string subject)
        {
        }
    }
}
