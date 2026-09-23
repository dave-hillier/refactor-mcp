using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shop
{
    public class Order
    {
        private readonly List<decimal> _prices = new List<decimal>();

        public decimal Total() => _prices.Sum();
    }

    public class Receipt
    {
        public string Print(Order order)
        {
            var text = new StringBuilder();
            text.Append("Total: ").Append(order.Total());
            return text.ToString();
        }
    }
}
