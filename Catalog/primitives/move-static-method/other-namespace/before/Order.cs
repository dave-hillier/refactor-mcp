using System.Collections.Generic;
using System.Linq;

namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        public static Order Largest(IEnumerable<Order> orders) => orders.OrderByDescending(o => o.Total).First();
    }
}
