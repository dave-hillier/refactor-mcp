namespace Shop;

public class Discount
{
    public string Describe(decimal price, decimal rate)
    {
        return (price - price * rate) + " after " + rate * 100m + "% off";
    }
}
