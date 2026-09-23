namespace Shop;

public class Shipping
{
    public decimal Quote(decimal weight, string region)
    {
        return weight;
    }

    public decimal Weight(int items, decimal each)
    {
        return items * each;
    }

    public decimal Parcel()
    {
        return Quote(Weight(3, /*^*/0.5m), "EU");
    }
}
