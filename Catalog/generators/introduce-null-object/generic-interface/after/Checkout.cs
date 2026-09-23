namespace Shop;

public class Order
{
}

public class Checkout
{
    private readonly IRepository<Order> _orders;

    public Checkout(IRepository<Order> orders)
    {
        _orders = orders ?? NullRepository<Order>.Instance;
    }

    public int Pending() => _orders.Count();

    public void Place(Order order)
    {
        _orders.Save(order);
    }
}
