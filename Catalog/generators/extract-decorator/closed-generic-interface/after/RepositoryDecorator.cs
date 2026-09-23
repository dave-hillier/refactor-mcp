namespace Shop
{
    public class RepositoryDecorator : IRepository<Order>
    {
        private readonly IRepository<Order> _inner;

        public RepositoryDecorator(IRepository<Order> inner)
        {
            _inner = inner;
        }

        public Order Find(int id) => _inner.Find(id);

        public void Save(Order item) => _inner.Save(item);
    }
}
