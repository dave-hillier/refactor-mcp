namespace Shop;

public class Pricing
{
    public decimal Discount(int percent, decimal price)
    {
        return price * percent / 100m;
    }

    public decimal Sale(decimal price)
    {
        return price - Discount(10, price);
    }
}
