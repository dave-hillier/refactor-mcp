namespace Shop;

public class Checkout
{
    public decimal Pay(Order order)
    {
        return order.Total(100m, 0m) + order.Total(20m, 0m);
    }
}
