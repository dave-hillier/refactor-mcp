using System.Collections.Generic;

namespace Shop
{
    public class Report
    {
        public decimal Top(List<Order> orders) => Order.Largest(orders).Total;
    }
}
