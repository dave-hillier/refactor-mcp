using System.Collections.Generic;
using System.Linq;

namespace Shop.Queries
{
    public static class OrderQueries
    {
        public static Order Largest(IEnumerable<Order> orders) => orders.OrderByDescending(o => o.Total).First();
    }
}
