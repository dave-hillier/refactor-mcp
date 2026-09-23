using System.Collections.Generic;
using Shop.Queries;

namespace Shop
{
    public class Report
    {
        public decimal Top(List<Order> orders) => OrderQueries.Largest(orders).Total;
    }
}
