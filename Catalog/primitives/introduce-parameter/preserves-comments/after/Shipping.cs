namespace Shop;

public class Shipping
{
    public decimal Cost(decimal weight, decimal baseFee)
    {
        // flat fee plus a rate per kilo
        var fee = baseFee; // standard fee

        return fee + weight * 2m;
    }

    public decimal Parcel()
    {
        // a small parcel
        return Cost(1.5m, 5m);
    }
}
