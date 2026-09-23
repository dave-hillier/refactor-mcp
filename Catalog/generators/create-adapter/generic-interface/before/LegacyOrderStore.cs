using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        public int Id { get; set; }
    }

    public class LegacyOrderStore
    {
        private readonly Dictionary<int, Order> _orders = new Dictionary<int, Order>();

        public Order Load(int id) => _orders[id];

        public void Store(Order order) => _orders[order.Id] = order;
    }
}
