namespace Shop;

public class Invoice
{
    public decimal Total(Order order)
    {
        return 100m + order.Tax();
    }
}
