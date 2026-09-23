using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        public int Id { get; set; }
    }

    public interface IRepository<T>
    {
        T Find(int id);

        void Save(T item);
    }

    public class OrderRepository : IRepository<Order>
    {
        private readonly Dictionary<int, Order> _orders = new Dictionary<int, Order>();

        public Order Find(int id) => _orders[id];

        public void Save(Order item) => _orders[item.Id] = item;
    }
}
