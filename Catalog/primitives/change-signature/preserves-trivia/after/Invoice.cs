namespace Shop;

public class Invoice
{
    /// <summary>Builds a line.</summary>
    public string Line(
        int quantity,
        string product, // what was sold
        decimal price)
    {
        return product + quantity + price;
    }

    public string Print()
    {
        // a single line item
        return Line(
            2,
            "pen", // product
            1.5m);
    }
}
