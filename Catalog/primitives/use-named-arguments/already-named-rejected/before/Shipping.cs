namespace Shop;

public class Shipping
{
    public decimal Quote(decimal weight, string region)
    {
        return weight;
    }

    public decimal Parcel()
    {
        return /*^*/Quote(region: "EU", weight: 1.5m);
    }
}
