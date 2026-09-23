namespace Shop;

public class Checkout
{
    public decimal Pay(Order order)
    {
        return order.Total(100m) + order.Total(20m);
    }
}
