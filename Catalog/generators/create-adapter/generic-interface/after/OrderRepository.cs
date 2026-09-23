using Shop.Contracts;

namespace Shop
{
    public class OrderRepository : IRepository<Order>
    {
        private readonly LegacyOrderStore _adaptee;

        public OrderRepository(LegacyOrderStore adaptee)
        {
            _adaptee = adaptee;
        }

        public Order Find(int id) => _adaptee.Load(id);

        public void Save(Order item) => _adaptee.Store(item);
    }
}
