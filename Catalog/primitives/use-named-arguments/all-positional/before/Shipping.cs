namespace Shop;

public class Shipping
{
    public decimal Quote(decimal weight, string region, bool express)
    {
        return express ? weight * 2m : weight;
    }

    public decimal Parcel()
    {
        return /*^*/Quote(1.5m, "EU", true);
    }
}
