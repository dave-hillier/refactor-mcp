namespace Shop;

public class Pricing
{
    public decimal Discount(decimal price, int percent)
    {
        return price * percent / 100m;
    }

    public decimal Sale(decimal price)
    {
        return price - Discount(price, 10);
    }
}
