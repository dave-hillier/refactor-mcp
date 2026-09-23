using System.Collections.Generic;
using Shop.Billing;

namespace Shop.Accounts
{
    public class Invoice
    {
        public List<TaxLine> Taxes { get; } = new List<TaxLine>();

        public decimal Net { get; set; }
    }
}
