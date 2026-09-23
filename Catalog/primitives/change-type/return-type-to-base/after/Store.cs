using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        public bool Shipped;
    }

    public class Store
    {
        private readonly List<Order> _orders = new List<Order>();

        public IEnumerable<Order> Pending() => _orders.FindAll(o => !o.Shipped);
    }
}
