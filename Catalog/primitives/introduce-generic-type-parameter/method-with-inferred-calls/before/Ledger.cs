using System.Collections.Generic;

namespace Shop
{
    public class Ledger
    {
        public decimal Sum(List<Invoice> items)
        {
            decimal sum = 0;
            foreach (var item in items)
                sum += item.Total;
            return sum;
        }
    }
}
