namespace Shop
{
    namespace Billing
    {
        using System.Collections.Generic;

        public class Ledger
        {
            public List<Invoice> Invoices { get; } = new List<Invoice>();
        }
    }
}
