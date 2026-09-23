namespace Shop;

public class Shipping
{
    public decimal Quote(decimal weight, string region, bool express)
    {
        return express ? weight * 2m : weight;
    }

    public decimal Parcel()
    {
        return Quote(weight: 1.5m, express: true, region: "EU");
    }
}
