namespace Shop;

public class Quote
{
    public decimal Price(IShipping shipping, Courier courier)
    {
        return shipping.Cost("EU", 1.5m) + courier.Cost("US", 2m);
    }
}
