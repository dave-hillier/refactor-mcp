using System.Collections.Generic;

namespace Shop
{
    public static class Report
    {
        public static decimal Total(Ledger ledger, List<Invoice> invoices) => ledger.Sum(invoices);
    }
}
