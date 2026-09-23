using System.Collections.Generic;

namespace Shop
{
    public class Ledger
    {
        public decimal Sum<T>(List<T> items) where T : Invoice
        {
            decimal sum = 0;
            foreach (var item in items)
                sum += item.Total;
            return sum;
        }
    }
}
