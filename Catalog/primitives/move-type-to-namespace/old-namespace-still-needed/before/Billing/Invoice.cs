using System.Collections.Generic;

namespace Shop.Billing
{
    public class Invoice
    {
        public List<TaxLine> Taxes { get; } = new List<TaxLine>();

        public decimal Net { get; set; }
    }
}
