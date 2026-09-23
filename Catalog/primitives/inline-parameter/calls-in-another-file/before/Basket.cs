namespace Shop;

public class Basket
{
    public string Total(Discount discount, decimal subtotal, decimal delivery)
    {
        return discount.Describe(subtotal, 0.1m) + discount.Describe(delivery, 0.1m);
    }
}
