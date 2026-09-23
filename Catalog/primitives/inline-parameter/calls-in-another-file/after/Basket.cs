namespace Shop;

public class Basket
{
    public string Total(Discount discount, decimal subtotal, decimal delivery)
    {
        return discount.Describe(subtotal) + discount.Describe(delivery);
    }
}
