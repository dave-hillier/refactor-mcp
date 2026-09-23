namespace Shop;

public class Quote
{
    public decimal Price(IShipping shipping, Courier courier)
    {
        return shipping.Cost(1.5m, "EU") + courier.Cost(2m, "US");
    }
}
