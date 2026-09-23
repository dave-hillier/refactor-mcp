namespace Shop;

public class Pricing
{
    public decimal Discount(decimal price, int percent)
    {
        return price * percent / 100m;
    }
}
