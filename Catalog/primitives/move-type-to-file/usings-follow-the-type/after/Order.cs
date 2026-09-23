using System.Collections.Generic;
using System.Linq;

namespace Shop
{
    public class Order
    {
        private readonly List<decimal> _prices = new List<decimal>();

        public decimal Total() => _prices.Sum();
    }
}
