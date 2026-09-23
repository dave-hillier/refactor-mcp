namespace Shop;

public class Parcel
{
    public Parcel(decimal weight, string label)
    {
        Weight = weight;
        Label = label;
    }

    public decimal Weight { get; }

    public string Label { get; }
}
