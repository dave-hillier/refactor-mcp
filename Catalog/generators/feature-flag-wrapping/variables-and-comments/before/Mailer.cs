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
            if (_flags.IsEnabled("Receipts"))
                Console.WriteLine(subject + ": " + total); // print the receipt
        }
    }
}
