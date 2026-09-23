namespace Shop;

public class Order
{
}

public class Checkout
{
    private readonly IRepository<Order> _orders;

    public Checkout(IRepository<Order> orders)
    {
        _orders = orders;
    }

    public int Pending() => _orders != null ? _orders.Count() : 0;

    public void Place(Order order)
    {
        if (_orders is not null)
        {
            _orders.Save(order);
        }
    }
}
