namespace Shop;

public class Packing
{
    public Parcel Wrap()
    {
        return new Parcel(weight: 2m, label: "fragile");
    }
}
