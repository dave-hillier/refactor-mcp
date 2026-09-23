using System.Collections.Generic;
using System.Linq;

namespace Shop
{
    public class Checkout
    {
        public decimal Tax(decimal amount) => TaxRules.Vat(amount);

        public IEnumerable<decimal> Taxes(IEnumerable<decimal> amounts) => amounts.Select(TaxRules.Vat);
    }
}
