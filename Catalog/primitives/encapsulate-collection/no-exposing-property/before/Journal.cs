using System.Collections.Generic;

namespace Shop
{
    public class Journal
    {
        private readonly List<decimal> _entries = new List<decimal>();

        public decimal Balance()
        {
            var total = 0m;
            foreach (var entry in _entries)
                total += entry;
            return total;
        }
    }
}
