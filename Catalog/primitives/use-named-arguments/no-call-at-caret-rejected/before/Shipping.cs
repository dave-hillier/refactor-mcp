namespace Shop;

public class Shipping
{
    public decimal Quote(decimal weight, string region, bool express)
    {
        return /*^*/express ? weight * 2m : weight;
    }
}
