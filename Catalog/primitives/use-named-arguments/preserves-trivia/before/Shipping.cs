namespace Shop;

public class Shipping
{
    public decimal Quote(decimal weight, string region, bool express)
    {
        return express ? weight * 2m : weight;
    }

    public decimal Parcel()
    {
        // next-day delivery
        return /*^*/Quote(
            1.5m, // kilos
            /* zone */ "EU",
            true);
    }
}
