namespace Shop;

public class Discount
{
    public string Describe(decimal price)
    {
        return (price - price * 0.1m) + " after " + 0.1m * 100m + "% off";
    }
}
