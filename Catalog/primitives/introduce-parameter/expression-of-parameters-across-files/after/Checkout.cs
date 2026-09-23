namespace Shop;

public class Checkout
{
    public string Receipt(Order order, int count)
    {
        return order.Summary(count + 1, 2.5m, (count + 1) * 2.5m);
    }
}
